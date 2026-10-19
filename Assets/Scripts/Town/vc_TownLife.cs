using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Townsfolk who make a town feel lived in. Added to the town root by vc_TownStage; needs no
/// scene setup. Works on what the town builder made (Bounds walls, the water tilemap, solid
/// colliders), so every town gets it.
///   - roamers: a handful of people stroll between random spots, stop, look around. Now and
///     then (rarely) one who passes close to the player turns, hops and says hi;
///   - daily happenings: some days a few people get together somewhere - a picnic, a ball
///     game, kids playing tag, someone flying a kite, a chat circle, joggers. Picked per day
///     from a seed, so a resumed day shows the same thing;
///   - birds: a small flock crosses the sky once in a while, shadows sliding over the ground.
/// Purely visual: nothing here is scored, saved or clickable. Sprites come from
/// Resources/TownLife (Tools/bake_npc_sheets.py, Tools/make_town_icons.py).
/// </summary>
[DisallowMultipleComponent]
public class vc_TownLife : MonoBehaviour
{
    [SerializeField] private int roamers = 6;
    [SerializeField] private float walkSpeed = 1.3f;
    [Tooltip("Chance that a roamer passing close to the player greets them.")]
    [Range(0f, 1f)] [SerializeField] private float greetChance = 0.65f;
    [Tooltip("Seconds between any two greetings (all roamers).")]
    [SerializeField] private float greetCooldown = 5f;
    [Tooltip("Seconds before the same roamer can greet again (after the player has walked away).")]
    [SerializeField] private float regreetDelay = 30f;
    [Tooltip("Chance that a roamer picking a new spot walks over to the player instead (if nearby).")]
    [Range(0f, 1f)] [SerializeField] private float approachChance = 0.3f;
    [SerializeField] private float birdInterval = 35f;

    private const float FrameTime = 0.11f;

    private Rect _rect;
    private Tilemap _water;
    private Transform _root;
    private string _sortLayer = "Default";
    private int _sortOrder = 40;
    private Sprite _shadowSprite;
    private ContactFilter2D _solid;
    private readonly Collider2D[] _hits = new Collider2D[4];
    private readonly List<Vector3> _avoid = new List<Vector3>();   // quest givers, home: keep events clear of them

    private readonly List<Roamer> _roamers = new List<Roamer>();
    private readonly List<Actor> _eventActors = new List<Actor>();
    private readonly List<GameObject> _eventProps = new List<GameObject>();
    private readonly List<System.Action<float>> _eventTicks = new List<System.Action<float>>();
    private int _eventsDay = -1;
    private float _lastGreet = -999f;
    private float _nextBird;
    private Camera _cam;
    private Transform _player;

    // ── a person on screen ────────────────────────────────────────────────

    private class Actor
    {
        public GameObject go;
        public SpriteRenderer body, emote;
        public string look;
        public Sprite[] frames;
        public int frame;
        public float frameTimer;
        public float emoteUntil, hopUntil;
        public Vector3 bodyBase;

        public void Play(Sprite[] f)
        {
            if (f == null || f == frames) return;
            frames = f; frame = 0; frameTimer = 0f;
            if (body != null && f.Length > 0) body.sprite = f[0];
        }

        public void Tick(float dt)
        {
            if (frames != null && frames.Length > 0)
            {
                frameTimer += dt;
                if (frameTimer >= FrameTime) { frameTimer = 0f; frame = (frame + 1) % frames.Length; body.sprite = frames[frame]; }
            }
            if (emote != null && emote.enabled && Time.time > emoteUntil) emote.enabled = false;
            // a little whole-pixel hop (greeting / excitement)
            float hop = Time.time < hopUntil ? Mathf.Round(Mathf.Abs(Mathf.Sin((hopUntil - Time.time) * 12f)) * 3f) / 16f : 0f;
            body.transform.localPosition = bodyBase + new Vector3(0f, hop, 0f);
        }

        public void Emote(Sprite s, float seconds, bool hop)
        {
            if (emote == null || s == null) return;
            emote.sprite = s;
            emote.enabled = true;
            emoteUntil = Time.time + seconds;
            if (hop) hopUntil = Time.time + 0.55f;
        }
    }

    private class Roamer : Actor
    {
        public Vector3 target;
        public bool walking;
        public float waitUntil;
        public Vector2 dir = Vector2.down;
        public bool greetedToday;      // rolled for this encounter; resets once the player walks away
        public float nextGreet;
        public float stuckTimer;
    }

    // ── setup ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _cam = Camera.main;
        _solid = new ContactFilter2D { useTriggers = false };
        _solid.SetLayerMask(Physics2D.DefaultRaycastLayers);
        _solid.useLayerMask = true;

        Transform walls = transform.Find("Bounds");
        _rect = new Rect(0f, 0f, 48f, 36f);
        if (walls != null && walls.childCount > 0)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Transform w in walls)
            {
                x0 = Mathf.Min(x0, w.position.x); x1 = Mathf.Max(x1, w.position.x);
                y0 = Mathf.Min(y0, w.position.y); y1 = Mathf.Max(y1, w.position.y);
            }
            _rect = Rect.MinMaxRect(x0 + 1.5f, y0 + 1.5f, x1 - 1.5f, y1 - 1.5f);
        }
        Transform grid = transform.Find("Grid");
        _water = grid != null ? grid.Find("water")?.GetComponent<Tilemap>() : null;

        foreach (vc_TownNPC npc in GetComponentsInChildren<vc_TownNPC>(true))
        {
            _avoid.Add(npc.transform.position);
            if (npc.body != null) { _sortLayer = npc.body.sortingLayerName; _sortOrder = npc.body.sortingOrder; }
        }
        foreach (vc_TownHome home in GetComponentsInChildren<vc_TownHome>(true)) _avoid.Add(home.WakePosition);

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        _player = pc != null ? pc.transform : null;
        Transform playerShadow = _player != null ? _player.Find("Shadow") : null;
        _shadowSprite = playerShadow != null ? playerShadow.GetComponent<SpriteRenderer>()?.sprite : null;

        _root = new GameObject("TownLife").transform;
        _root.SetParent(transform, false);

        // a different set of townsfolk per town
        var looks = new List<string>(vc_NpcSprites.TownsfolkLooks);
        var rng = new System.Random(StageSeed(0));
        for (int i = looks.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (looks[i], looks[j]) = (looks[j], looks[i]); }
        for (int i = 0; i < roamers && i < looks.Count; i++)
        {
            Vector3 p = RandomOpenSpot(1.2f, 60);
            if (float.IsNaN(p.x)) continue;
            var r = new Roamer();
            Build(r, looks[i], p, "Roamer");
            r.waitUntil = Time.time + Random.Range(0f, 3f);
            r.Play(vc_NpcSprites.Idle(r.look, vc_NpcSprites.Dir.Down));
            _roamers.Add(r);
        }
        _nextBird = Time.time + Random.Range(8f, 16f);
    }

    private int StageSeed(int salt)
    {
        string id = GetComponent<vc_TownStage>() != null ? GetComponent<vc_TownStage>().stageId : name;
        unchecked
        {
            int h = 17;
            foreach (char c in id) h = h * 31 + c;
            return h * 31 + salt;
        }
    }

    private void Build(Actor a, string look, Vector3 pos, string name)
    {
        a.look = look;
        a.go = new GameObject(name);
        a.go.transform.SetParent(_root, false);
        a.go.transform.position = pos;

        if (_shadowSprite != null)
        {
            var sh = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            sh.transform.SetParent(a.go.transform, false);
            sh.sprite = _shadowSprite;
            sh.sortingLayerName = _sortLayer;
            sh.sortingOrder = _sortOrder - 1;
            sh.color = new Color(1f, 1f, 1f, 0.8f);
        }

        a.body = new GameObject("Body").AddComponent<SpriteRenderer>();
        a.body.transform.SetParent(a.go.transform, false);
        a.body.sortingLayerName = _sortLayer;
        a.body.sortingOrder = _sortOrder;
        a.bodyBase = Vector3.zero;

        a.emote = new GameObject("Emote").AddComponent<SpriteRenderer>();
        a.emote.transform.SetParent(a.go.transform, false);
        a.emote.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        a.emote.sortingLayerName = "Decor";
        a.emote.sortingOrder = 10;
        a.emote.enabled = false;
    }

    // ── per frame ─────────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.deltaTime;
        vc_TownStage stage = vc_TownStage.Current;
        if (stage != null && stage.Day != _eventsDay) StartDayEvents(stage.Day);

        foreach (Roamer r in _roamers) TickRoamer(r, dt);
        foreach (Actor a in _eventActors) a.Tick(dt);
        foreach (var tick in _eventTicks) tick(dt);
        TickBirds(dt);
    }

    private void TickRoamer(Roamer r, float dt)
    {
        r.Tick(dt);
        Vector3 pos = r.go.transform.position;
        float toPlayer = _player != null ? Vector2.Distance(pos, _player.position) : 99f;

        // Rare greeting when the player comes close.
        if (toPlayer > 3.5f) r.greetedToday = false;   // a new encounter next time they meet
        if (!r.greetedToday && toPlayer < 2.2f && Time.time > r.nextGreet && Time.time - _lastGreet > greetCooldown)
        {
            r.greetedToday = true;   // one roll per encounter
            r.nextGreet = Time.time + regreetDelay;
            if (Random.value < greetChance)
            {
                _lastGreet = Time.time;
                r.walking = false;
                r.waitUntil = Time.time + 2.2f;
                r.dir = (Vector2)(_player.position - pos);
                r.Play(vc_NpcSprites.Idle(r.look, vc_NpcSprites.DirFor(r.dir)));
                string[] pick = { "emote_hi", "emote_hi", "emote_heart", "emote_note", "emote_sun" };
                r.Emote(Resources.Load<Sprite>("TownLife/" + pick[Random.Range(0, pick.Length)]), 2f, true);
                SunodGame.Core.vc_Sfx.Play("bubble", 0.35f, 0.08f, 1f);
            }
        }

        if (!r.walking)
        {
            if (Time.time < r.waitUntil) return;
            // Sometimes they come over to say hi instead of wandering off.
            Vector3 t = new Vector3(float.NaN, 0f, 0f);
            if (_player != null && toPlayer < 6f && toPlayer > 1.5f && Time.time > r.nextGreet && Random.value < approachChance)
            {
                Vector3 side = _player.position + new Vector3(pos.x < _player.position.x ? -1.1f : 1.1f, 0f, 0f);
                if (IsOpen(side, 0.25f) && PathClear(pos, new Vector3(side.x, pos.y, 0f)) && PathClear(new Vector3(side.x, pos.y, 0f), side)) t = Snap(side);
            }
            if (float.IsNaN(t.x)) t = RandomOpenSpotNear(pos, 7f, 25);
            if (float.IsNaN(t.x)) { r.waitUntil = Time.time + 1.5f; return; }
            r.target = t;
            r.walking = true;
            r.stuckTimer = 0f;
        }

        // don't walk into the player; wait for them to pass
        if (toPlayer < 0.8f) { r.Play(vc_NpcSprites.Idle(r.look, vc_NpcSprites.DirFor(r.dir))); return; }

        Vector2 to = r.target - pos;
        if (to.magnitude < 0.08f)
        {
            r.walking = false;
            r.waitUntil = Time.time + Random.Range(1.5f, 5f);
            r.Play(vc_NpcSprites.Idle(r.look, vc_NpcSprites.DirFor(r.dir)));
            return;
        }
        // one axis at a time, like the player walks
        Vector2 step = Mathf.Abs(to.x) > 0.06f ? new Vector2(Mathf.Sign(to.x), 0f) : new Vector2(0f, Mathf.Sign(to.y));
        Vector3 next = pos + (Vector3)(step * walkSpeed * dt);
        if (!IsOpen(next, 0.18f))
        {
            r.stuckTimer += dt;
            if (r.stuckTimer > 0.3f) { r.walking = false; r.waitUntil = Time.time + 0.5f; r.Play(vc_NpcSprites.Idle(r.look, vc_NpcSprites.DirFor(r.dir))); }
            return;
        }
        r.dir = step;
        r.Play(vc_NpcSprites.Walk(r.look, vc_NpcSprites.DirFor(step)));
        r.go.transform.position = next;
    }

    // ── daily happenings ──────────────────────────────────────────────────

    private void StartDayEvents(int day)
    {
        _eventsDay = day;
        foreach (Roamer r in _roamers) r.greetedToday = false;
        foreach (Actor a in _eventActors) if (a.go != null) Destroy(a.go);
        foreach (GameObject g in _eventProps) if (g != null) Destroy(g);
        _eventActors.Clear();
        _eventProps.Clear();
        _eventTicks.Clear();

        var rng = new System.Random(StageSeed(day * 101));
        int count = rng.NextDouble() < 0.75 ? 1 : 0;
        if (rng.NextDouble() < 0.4) count++;
        var kinds = new List<int> { 0, 1, 2, 3, 4, 5 };
        for (int n = 0; n < count && kinds.Count > 0; n++)
        {
            int k = kinds[rng.Next(kinds.Count)];
            kinds.Remove(k);
            Vector3 spot = RandomOpenSpot(2.4f, 80, rng);
            if (float.IsNaN(spot.x)) continue;
            switch (k)
            {
                case 0: Picnic(spot, rng); break;
                case 1: BallGame(spot, rng); break;
                case 2: Tag(spot, rng); break;
                case 3: Kite(spot, rng); break;
                case 4: Chat(spot, rng); break;
                case 5: Joggers(spot, rng); break;
            }
        }
    }

    private string NextEventLook(System.Random rng)
    {
        // event people reuse the town's palettes, but never one walking around as a roamer
        var used = new HashSet<string>();
        foreach (Roamer r in _roamers) used.Add(r.look);
        foreach (Actor a in _eventActors) used.Add(a.look);
        var pool = new List<string>();
        foreach (string l in vc_NpcSprites.TownsfolkLooks) if (!used.Contains(l)) pool.Add(l);
        if (pool.Count == 0) pool.AddRange(vc_NpcSprites.TownsfolkLooks);
        return pool[rng.Next(pool.Count)];
    }

    private Actor AddEventActor(Vector3 pos, System.Random rng)
    {
        var a = new Actor();
        Build(a, NextEventLook(rng), pos, "EventPerson");
        _eventActors.Add(a);
        return a;
    }

    private SpriteRenderer AddProp(string sprite, Vector3 pos, int orderOffset)
    {
        var sr = new GameObject(sprite).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(_root, false);
        sr.transform.position = pos;
        sr.sprite = Resources.Load<Sprite>("TownLife/" + sprite);
        sr.sortingLayerName = _sortLayer;
        sr.sortingOrder = _sortOrder + orderOffset;
        _eventProps.Add(sr.gameObject);
        return sr;
    }

    private Sprite EmoteSprite(string n) => Resources.Load<Sprite>("TownLife/emote_" + n);

    /// <summary>2-3 people sitting on a picnic mat, chatting (hearts / notes now and then).</summary>
    private void Picnic(Vector3 c, System.Random rng)
    {
        AddProp("picnic_mat", c, -30);
        var a = AddEventActor(c + new Vector3(-0.55f, -0.1f), rng);
        var b = AddEventActor(c + new Vector3(0.55f, -0.1f), rng);
        a.Play(vc_NpcSprites.Sit(a.look, true));
        b.Play(vc_NpcSprites.Sit(b.look, false));
        var people = new[] { a, b };
        float next = 0f;
        _eventTicks.Add(dt =>
        {
            next -= dt;
            if (next > 0f) return;
            next = Random.Range(4f, 8f);
            people[Random.Range(0, people.Length)].Emote(EmoteSprite(Random.value < 0.5f ? "heart" : "note"), 1.6f, false);
        });
    }

    /// <summary>Two people passing a ball back and forth in an arc.</summary>
    private void BallGame(Vector3 c, System.Random rng)
    {
        var a = AddEventActor(c + new Vector3(-1.6f, 0f), rng);
        var b = AddEventActor(c + new Vector3(1.6f, 0f), rng);
        a.Play(vc_NpcSprites.Idle(a.look, vc_NpcSprites.Dir.Right));
        b.Play(vc_NpcSprites.Idle(b.look, vc_NpcSprites.Dir.Left));
        SpriteRenderer ball = AddProp("ball", c, 5);
        SpriteRenderer shadow = _shadowSprite != null ? AddProp("ball", c, -1) : null;
        if (shadow != null) { shadow.sprite = _shadowSprite; shadow.transform.localScale = new Vector3(0.45f, 0.45f, 1f); shadow.color = new Color(1, 1, 1, 0.6f); }
        float t = 0f; bool toB = true;
        Vector3 pa = a.go.transform.position + new Vector3(0.3f, 0.35f), pb = b.go.transform.position + new Vector3(-0.3f, 0.35f);
        _eventTicks.Add(dt =>
        {
            t += dt / 1.1f;
            if (t >= 1f) { t = 0f; toB = !toB; (toB ? a : b).hopUntil = Time.time + 0.3f; }
            Vector3 from = toB ? pa : pb, to = toB ? pb : pa;
            Vector3 ground = Vector3.Lerp(from, to, t);
            float h = Mathf.Sin(t * Mathf.PI) * 1.1f;
            ball.transform.position = Snap(ground + new Vector3(0f, h, 0f));
            if (shadow != null) shadow.transform.position = Snap(ground - new Vector3(0f, 0.3f, 0f));
        });
    }

    /// <summary>Three kids running around in a circle (tag), laughing now and then.</summary>
    private void Tag(Vector3 c, System.Random rng)
    {
        var kids = new[] { AddEventActor(c, rng), AddEventActor(c, rng), AddEventActor(c, rng) };
        float angle = 0f, next = 2f;
        const float radius = 1.5f;
        _eventTicks.Add(dt =>
        {
            angle += dt * 1.4f;
            for (int i = 0; i < kids.Length; i++)
            {
                float a = angle + i * Mathf.PI * 2f / kids.Length;
                Vector3 p = c + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * radius;
                Vector2 vel = new Vector2(-Mathf.Sin(a), Mathf.Cos(a) * 0.7f);
                kids[i].go.transform.position = Snap(p);
                kids[i].Play(vc_NpcSprites.Walk(kids[i].look, vc_NpcSprites.DirFor(vel)));
            }
            next -= dt;
            if (next <= 0f) { next = Random.Range(3f, 6f); kids[Random.Range(0, kids.Length)].Emote(EmoteSprite("note"), 1.2f, true); }
        });
    }

    /// <summary>Someone flying a kite that sways in the wind.</summary>
    private void Kite(Vector3 c, System.Random rng)
    {
        var a = AddEventActor(c, rng);
        a.Play(vc_NpcSprites.Idle(a.look, vc_NpcSprites.Dir.Up));
        SpriteRenderer kite = AddProp("kite", c + new Vector3(0.8f, 3f), 60);
        kite.sortingLayerName = "Decor";
        float t = (float)rng.NextDouble() * 10f;
        _eventTicks.Add(dt =>
        {
            t += dt;
            kite.transform.position = Snap(c + new Vector3(0.8f + Mathf.Sin(t * 0.7f) * 0.6f, 3f + Mathf.Sin(t * 1.3f) * 0.35f));
            kite.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(Mathf.Sin(t * 1.1f) * 2f) * 6f);
        });
    }

    /// <summary>A little circle of people chatting.</summary>
    private void Chat(Vector3 c, System.Random rng)
    {
        var a = AddEventActor(c + new Vector3(-0.7f, 0f), rng);
        var b = AddEventActor(c + new Vector3(0.7f, 0f), rng);
        var d = AddEventActor(c + new Vector3(0f, 0.6f), rng);
        a.Play(vc_NpcSprites.Idle(a.look, vc_NpcSprites.Dir.Right));
        b.Play(vc_NpcSprites.Idle(b.look, vc_NpcSprites.Dir.Left));
        d.Play(vc_NpcSprites.Idle(d.look, vc_NpcSprites.Dir.Down));
        var people = new[] { a, b, d };
        float next = 1f;
        _eventTicks.Add(dt =>
        {
            next -= dt;
            if (next > 0f) return;
            next = Random.Range(2.5f, 5f);
            string[] e = { "dots", "dots", "hi", "note", "heart" };
            people[Random.Range(0, people.Length)].Emote(EmoteSprite(e[Random.Range(0, e.Length)]), 1.8f, Random.value < 0.3f);
        });
    }

    /// <summary>Two joggers running a loop.</summary>
    private void Joggers(Vector3 c, System.Random rng)
    {
        var a = AddEventActor(c, rng);
        var b = AddEventActor(c, rng);
        Vector3[] loop = { c + new Vector3(-2.2f, -1.4f), c + new Vector3(2.2f, -1.4f), c + new Vector3(2.2f, 1.4f), c + new Vector3(-2.2f, 1.4f) };
        float perimeter = 2f * (4.4f + 2.8f), s = 0f;
        _eventTicks.Add(dt =>
        {
            s = (s + dt * 1.9f) % perimeter;
            PlaceOnLoop(a, loop, s);
            PlaceOnLoop(b, loop, (s + 1.1f) % perimeter);
        });
    }

    private static void PlaceOnLoop(Actor a, Vector3[] loop, float s)
    {
        for (int i = 0; i < loop.Length; i++)
        {
            Vector3 p0 = loop[i], p1 = loop[(i + 1) % loop.Length];
            float len = Vector3.Distance(p0, p1);
            if (s <= len)
            {
                a.go.transform.position = Snap(Vector3.Lerp(p0, p1, s / len));
                a.Play(vc_NpcSprites.Walk(a.look, vc_NpcSprites.DirFor(p1 - p0)));
                return;
            }
            s -= len;
        }
    }

    // ── birds ─────────────────────────────────────────────────────────────

    private readonly List<(SpriteRenderer bird, SpriteRenderer shadow, Vector3 vel)> _birds = new List<(SpriteRenderer, SpriteRenderer, Vector3)>();
    private Sprite[] _birdFrames;

    private void TickBirds(float dt)
    {
        if (_cam == null) return;
        if (_birdFrames == null) _birdFrames = new[] { Resources.Load<Sprite>("TownLife/bird_0"), Resources.Load<Sprite>("TownLife/bird_1") };

        if (Time.time > _nextBird && _birds.Count == 0 && _birdFrames[0] != null)
        {
            _nextBird = Time.time + birdInterval * Random.Range(0.7f, 1.4f);
            float h = _cam.orthographicSize, w = h * _cam.aspect;
            bool leftToRight = Random.value < 0.5f;
            Vector3 start = _cam.transform.position + new Vector3(leftToRight ? -w - 2f : w + 2f, Random.Range(-h * 0.3f, h * 0.6f), 0f);
            start.z = 0f;
            Vector3 vel = new Vector3(leftToRight ? 3.2f : -3.2f, Random.Range(-0.4f, 0.6f), 0f);
            int n = Random.Range(3, 6);
            for (int i = 0; i < n; i++)
            {
                Vector3 off = new Vector3(-Mathf.Sign(vel.x) * i * 0.6f, (i % 2 == 0 ? 1 : -1) * i * 0.35f, 0f);
                var b = new GameObject("Bird").AddComponent<SpriteRenderer>();
                b.transform.SetParent(_root, false);
                b.transform.position = start + off;
                b.sprite = _birdFrames[0];
                b.sortingLayerName = "Decor";
                b.sortingOrder = 50;
                var s = new GameObject("BirdShadow").AddComponent<SpriteRenderer>();
                s.transform.SetParent(_root, false);
                s.sprite = _birdFrames[1];
                s.color = new Color(0f, 0f, 0f, 0.18f);
                s.sortingLayerName = _sortLayer;
                s.sortingOrder = _sortOrder - 5;
                s.transform.position = b.transform.position + new Vector3(0.6f, -2.4f, 0f);
                _birds.Add((b, s, vel));
            }
        }

        bool flap = Mathf.Repeat(Time.time, 0.3f) < 0.15f;
        for (int i = _birds.Count - 1; i >= 0; i--)
        {
            var (b, s, vel) = _birds[i];
            b.transform.position += vel * dt;
            s.transform.position += vel * dt;
            b.sprite = _birdFrames[flap ? 1 : 0];
            Vector3 vp = _cam.WorldToViewportPoint(b.transform.position);
            if (vp.x < -0.4f || vp.x > 1.4f)
            {
                Destroy(b.gameObject);
                Destroy(s.gameObject);
                _birds.RemoveAt(i);
            }
        }
    }

    // ── places ────────────────────────────────────────────────────────────

    private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 16f) / 16f, Mathf.Round(p.y * 16f) / 16f, p.z);

    private bool IsOpen(Vector3 p, float radius)
    {
        if (!_rect.Contains(p)) return false;
        if (_water != null && _water.HasTile(_water.WorldToCell(p))) return false;
        return Physics2D.OverlapCircle(p, radius, _solid, _hits) == 0;
    }

    private Vector3 RandomOpenSpot(float clearRadius, int tries, System.Random rng = null)
    {
        for (int i = 0; i < tries; i++)
        {
            float rx = rng != null ? (float)rng.NextDouble() : Random.value;
            float ry = rng != null ? (float)rng.NextDouble() : Random.value;
            var p = new Vector3(Mathf.Lerp(_rect.xMin, _rect.xMax, rx), Mathf.Lerp(_rect.yMin, _rect.yMax, ry), 0f);
            if (!AreaOpen(p, clearRadius)) continue;
            bool nearSomeone = false;
            foreach (Vector3 a in _avoid) if (Vector2.Distance(a, p) < clearRadius + 1.2f) { nearSomeone = true; break; }
            if (nearSomeone) continue;
            return Snap(p);
        }
        return new Vector3(float.NaN, 0f, 0f);
    }

    /// <summary>A circle of ground with nothing solid or wet in it.</summary>
    private bool AreaOpen(Vector3 c, float radius)
    {
        if (!IsOpen(c, radius)) return false;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            if (!IsOpen(c + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius, 0.1f)) return false;
        }
        return true;
    }

    private Vector3 RandomOpenSpotNear(Vector3 from, float range, int tries)
    {
        for (int i = 0; i < tries; i++)
        {
            Vector3 p = from + new Vector3(Random.Range(-range, range), Random.Range(-range, range), 0f);
            if (Vector2.Distance(p, from) < 1.5f || !IsOpen(p, 0.3f)) continue;
            // the L-shaped path (sideways, then up/down) must be clear too
            Vector3 corner = new Vector3(p.x, from.y, 0f);
            if (!PathClear(from, corner) || !PathClear(corner, p)) continue;
            return Snap(p);
        }
        return new Vector3(float.NaN, 0f, 0f);
    }

    private bool PathClear(Vector3 a, Vector3 b)
    {
        float d = Vector2.Distance(a, b);
        int steps = Mathf.CeilToInt(d / 0.4f);
        for (int i = 1; i <= steps; i++)
            if (!IsOpen(Vector3.Lerp(a, b, i / (float)steps), 0.2f)) return false;
        return true;
    }
}
