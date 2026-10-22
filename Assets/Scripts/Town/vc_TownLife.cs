using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Townsfolk who make a town feel lived in. Added to the town root by vc_TownStage; needs no
/// scene setup. Works on what the town builder made (Bounds walls, the water tilemap, solid
/// colliders), so every town gets it.
///   - roamers: a handful of named townsfolk walk real paths around the town (vc_TownGrid A*)
///     to places people go - shop doors, benches, the plaza - stop there a while, then move
///     on. Now and then one who passes close to the player turns, hops and says hi. Tap one
///     to talk: everyone has their own line for the day (vc_NpcDialogue);
///   - daily happenings: some days a few people get together somewhere - a picnic, a ball
///     game, kids playing tag, someone flying a kite, a chat circle, joggers. Picked per day
///     from a seed, so a resumed day shows the same thing;
///   - birds: a small flock crosses the sky once in a while, shadows sliding over the ground.
/// Nothing here is scored or saved. Sprites come from
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
    private readonly Dictionary<Actor, string> _eventKind = new Dictionary<Actor, string>();
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
        public string name;
        public float speed;
        public readonly List<Vector3> path = new List<Vector3>();
        public int pathIndex;
        public bool walking;
        public float waitUntil;
        public Vector2 dir = Vector2.down;
        public Vector2 faceOnArrive;   // which way to look once there (e.g. at a shop window)
        public bool greetedToday;      // rolled for this encounter; resets once the player walks away
        public float nextGreet;
        public float blockedFor;
    }

    private vc_TownGrid _grid;
    private readonly List<(Vector3 spot, Vector2 face)> _places = new List<(Vector3, Vector2)>();
    private string[] _names = new string[0];

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

        _grid = vc_TownGrid.For(transform);
        CollectPlaces();
        vc_TownStage stage = GetComponent<vc_TownStage>();
        _names = vc_NpcDialogue.RoamersFor(stage != null ? stage.stageId : "");

        // a different set of townsfolk per town, each always the same person (name + look)
        var looks = new List<string>(vc_NpcSprites.TownsfolkLooks);
        var rng = new System.Random(StageSeed(0));
        for (int i = looks.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (looks[i], looks[j]) = (looks[j], looks[i]); }
        for (int i = 0; i < roamers && i < looks.Count; i++)
        {
            Vector3 p = RandomOpenSpot(1.2f, 60);
            if (float.IsNaN(p.x)) continue;
            var r = new Roamer
            {
                name = i < _names.Length ? _names[i] : "Neighbor",
                speed = walkSpeed * (0.8f + (float)rng.NextDouble() * 0.45f),   // some stroll, some hurry
            };
            Build(r, looks[i], p, "Roamer_" + r.name);
            r.waitUntil = Time.time + Random.Range(0f, 3f);
            r.Play(vc_NpcSprites.Idle(r.look, vc_NpcSprites.Dir.Down));
            _roamers.Add(r);
        }
        _nextBird = Time.time + Random.Range(8f, 16f);
    }

    private void OnDestroy() => vc_TownGrid.Forget(transform);

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
            PickDestination(r, pos, toPlayer);
            return;
        }

        // the player's in the way: wait a moment, then find a way around them
        if (toPlayer < 0.75f)
        {
            r.Play(vc_NpcSprites.Idle(r.look, vc_NpcSprites.DirFor(r.dir)));
            r.blockedFor += dt;
            if (r.blockedFor > 1.6f) { r.walking = false; r.path.Clear(); r.waitUntil = Time.time + 0.4f; r.blockedFor = 0f; }
            return;
        }
        r.blockedFor = 0f;

        if (r.pathIndex >= r.path.Count) { Arrive(r); return; }
        Vector3 wp = r.path[r.pathIndex];
        Vector2 to = wp - pos;
        float stepLen = r.speed * dt;
        if (to.magnitude <= stepLen)
        {
            r.go.transform.position = wp;
            r.pathIndex++;
            // now and then people stop at a corner to look around, like real strollers do
            if (r.pathIndex < r.path.Count && Random.value < 0.08f)
            {
                r.walking = false;
                r.waitUntil = Time.time + Random.Range(0.6f, 1.4f);
                r.Play(vc_NpcSprites.Idle(r.look, vc_NpcSprites.DirFor(r.dir)));
            }
            return;
        }
        Vector2 step = to.normalized;
        r.dir = step;
        r.Play(vc_NpcSprites.Walk(r.look, vc_NpcSprites.DirFor(step)));
        r.go.transform.position = pos + (Vector3)(step * stepLen);
    }

    /// <summary>Where to next: a place people go (shop door, bench, plaza), over to the player to say hi, or a stroll.</summary>
    private void PickDestination(Roamer r, Vector3 pos, float toPlayer)
    {
        // a paused stroll (corner stop, a chat with the player) just carries on
        if (r.pathIndex < r.path.Count) { r.walking = true; return; }

        Vector3 target = new Vector3(float.NaN, 0f, 0f);
        r.faceOnArrive = Vector2.down;
        if (_player != null && toPlayer < 6f && toPlayer > 1.5f && Time.time > r.nextGreet && Random.value < approachChance)
        {
            Vector3 side = _player.position + new Vector3(pos.x < _player.position.x ? -1.1f : 1.1f, 0f, 0f);
            if (_grid != null && _grid.IsWalkable(side)) { target = side; r.faceOnArrive = (Vector2)(_player.position - side); }
        }
        if (float.IsNaN(target.x) && _places.Count > 0 && Random.value < 0.55f)
        {
            var place = _places[Random.Range(0, _places.Count)];
            target = place.spot + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.2f), 0f);
            r.faceOnArrive = place.face;
        }
        if (float.IsNaN(target.x) && _grid != null)
        {
            Vector2? p = _grid.RandomWalkable(pos, 9f, q => Vector2.Distance(q, pos) > 2f);
            if (p.HasValue) target = p.Value;
            r.faceOnArrive = Random.value < 0.5f ? Vector2.down : (Random.value < 0.5f ? Vector2.left : Vector2.right);
        }
        if (float.IsNaN(target.x) || _grid == null) { r.waitUntil = Time.time + 1.5f; return; }

        List<Vector3> path = _grid.FindPath(pos, target);
        if (path.Count == 0) { r.waitUntil = Time.time + 1f; return; }
        r.path.Clear();
        foreach (Vector3 w in path) r.path.Add(Snap(w));
        r.pathIndex = 0;
        r.walking = true;
    }

    private void Arrive(Roamer r)
    {
        r.walking = false;
        r.path.Clear();
        r.pathIndex = 0;
        r.dir = r.faceOnArrive;
        r.Play(vc_NpcSprites.Idle(r.look, vc_NpcSprites.DirFor(r.dir)));
        r.waitUntil = Time.time + Random.Range(2.5f, 7f);
        // a little thought bubble now and then (window shopping, people watching)
        if (Random.value < 0.25f)
        {
            string[] e = { "dots", "note", "heart", "sun" };
            r.Emote(EmoteSprite(e[Random.Range(0, e.Length)]), 1.8f, false);
        }
    }

    /// <summary>
    /// Places people go: in front of each building's door (looking at it), by benches and
    /// lamps, and around the plaza.
    /// </summary>
    private void CollectPlaces()
    {
        _places.Clear();
        if (_grid == null) return;
        Transform objects = transform.Find("Objects");
        if (objects != null)
        {
            foreach (Transform o in objects)
            {
                Transform baseT = o.Find("Base");
                SpriteRenderer b = baseT != null ? baseT.GetComponent<SpriteRenderer>() : o.GetComponent<SpriteRenderer>();
                if (b == null || b.sprite == null) continue;
                string n = o.name.ToLowerInvariant();
                Bounds bb = b.bounds;
                Vector3 spot; Vector2 face;
                if (n.Contains("bench")) { spot = new Vector3(bb.center.x, bb.min.y - 0.45f, 0f); face = Vector2.down; }
                else if (n.Contains("lamp") || n.Contains("sign")) { spot = new Vector3(bb.center.x + 0.8f, bb.min.y - 0.3f, 0f); face = Vector2.left; }
                else if (bb.size.x > 2f) { spot = new Vector3(bb.center.x, bb.min.y - 0.7f, 0f); face = Vector2.up; }   // a building: stand at its door
                else continue;
                Vector2? ok = _grid.NearestWalkable(spot, 2);
                if (ok.HasValue) _places.Add((ok.Value, face));
            }
        }
        Tilemap plaza = transform.Find("Grid")?.Find("plaza")?.GetComponent<Tilemap>();
        if (plaza != null)
        {
            plaza.CompressBounds();
            BoundsInt cb = plaza.cellBounds;
            for (int i = 0; i < 12 && _places.Count < 60; i++)
            {
                var c = new Vector3Int(Random.Range(cb.xMin, cb.xMax), Random.Range(cb.yMin, cb.yMax), 0);
                if (!plaza.HasTile(c)) continue;
                Vector3 w = plaza.GetCellCenterWorld(c);
                if (_grid.IsWalkable(w)) _places.Add((w, Vector2.down));
            }
        }
    }

    // ── talking ───────────────────────────────────────────────────────────

    /// <summary>
    /// Tap on a townsperson standing near the player: they stop, turn to the player and say
    /// their line for the day. Returns true if someone was tapped.
    /// </summary>
    public bool TryTalk(Vector2 tapWorld, Vector2 playerPos, int day, string stageId)
    {
        Actor who = null; string name = null; string line = null;
        float best = 0.95f;
        foreach (Roamer r in _roamers)
        {
            Vector2 c = (Vector2)r.go.transform.position + new Vector2(0f, 0.6f);
            float d = Vector2.Distance(tapWorld, c);
            if (d > best || Vector2.Distance(playerPos, r.go.transform.position) > 2.4f) continue;
            best = d; who = r; name = r.name; line = vc_NpcDialogue.DailyLine(stageId, r.name, day);
        }
        foreach (Actor a in _eventActors)
        {
            Vector2 c = (Vector2)a.go.transform.position + new Vector2(0f, 0.6f);
            float d = Vector2.Distance(tapWorld, c);
            if (d > best || Vector2.Distance(playerPos, a.go.transform.position) > 2.4f) continue;
            best = d; who = a; name = "Neighbor";
            string[] pool = _eventKind.TryGetValue(a, out string k) && vc_NpcDialogue.EventLines.TryGetValue(k, out string[] l) ? l : null;
            line = pool != null ? pool[(a.look.Length + day) % pool.Length] : "Lovely day, isn't it?";
        }
        if (who == null || vc_DialogPanel.Instance == null) return false;

        if (who is Roamer rr)
        {
            rr.walking = false;   // keeps its path; carries on after the chat
            rr.waitUntil = Time.time + 4f;
            rr.dir = playerPos - (Vector2)rr.go.transform.position;
            rr.Play(vc_NpcSprites.Idle(rr.look, vc_NpcSprites.DirFor(rr.dir)));
        }
        who.Emote(EmoteSprite("hi"), 1.2f, true);
        SunodGame.Core.vc_Sfx.Play("bubble", 0.4f, 0.08f, 1f);
        vc_DialogPanel.Instance.ShowMessage(name, line, null);
        return true;
    }

    // ── daily happenings ──────────────────────────────────────────────────

    private void StartDayEvents(int day)
    {
        _eventsDay = day;
        foreach (Roamer r in _roamers) r.greetedToday = false;
        foreach (Actor a in _eventActors) if (a.go != null) Destroy(a.go);
        foreach (GameObject g in _eventProps) if (g != null) Destroy(g);
        _eventActors.Clear();
        _eventKind.Clear();
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
            int before = _eventActors.Count;
            switch (k)
            {
                case 0: Picnic(spot, rng); break;
                case 1: BallGame(spot, rng); break;
                case 2: Tag(spot, rng); break;
                case 3: Kite(spot, rng); break;
                case 4: Chat(spot, rng); break;
                case 5: Joggers(spot, rng); break;
            }
            string kind = new[] { "picnic", "ball", "tag", "kite", "chat", "joggers" }[k];
            for (int i = before; i < _eventActors.Count; i++) _eventKind[_eventActors[i]] = kind;
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
}
