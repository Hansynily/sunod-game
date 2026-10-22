using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A town's morning, the small living things:
///   - chickens peck around near gardens and homes; run at them and they scatter, flapping;
///   - the town cat naps on someone's doorstep. Come close and it wakes up and sits; tap it
///     to give it a pet (hearts). Left alone long enough it stretches and wanders off to nap
///     on another doorstep;
///   - laundry lines beside a few houses, the clothes flapping in the breeze;
///   - soft morning mist drifting low over the water and the edges of town;
///   - dew sparkling on the grass around the player.
/// Placed at runtime on open grass (vc_TownGrid), off the roads, so every town gets them
/// with no scene setup. Nothing here blocks the player or is saved. Art: Resources/Morning
/// (Tools/make_morning.py); mist and dew are generated here. Added by vc_TownStage.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownCritters : MonoBehaviour
{
    [SerializeField] private int chickens = 4;
    [SerializeField] private int laundryLines = 2;
    [SerializeField] private int mistPatches = 6;
    [SerializeField] private int dewSparkles = 10;

    private const float Px = 1f / 16f;

    private vc_TownGrid _grid;
    private Rect _rect;
    private Tilemap _water;
    private readonly List<Tilemap> _roads = new List<Tilemap>();
    private Transform _root, _player;
    private Camera _cam;
    private string _layer = "Default";
    private int _order = 40;
    private Sprite _shadow;

    private Sprite _chStand, _chPeck, _chWalk, _catSleep, _catSit, _catStretch, _catWalk, _heart, _zz;
    private Sprite[] _laundry;
    private Sprite _mistSprite, _dewSprite;

    private void Start()
    {
        _cam = Camera.main;
        _grid = vc_TownGrid.For(transform);
        _rect = vc_TownGrid.TownRect(transform);
        Transform grid = transform.Find("Grid");
        _water = grid?.Find("water")?.GetComponent<Tilemap>();
        foreach (string n in new[] { "dirt", "cobble", "plaza", "sand" })
        {
            Tilemap t = grid?.Find(n)?.GetComponent<Tilemap>();
            if (t != null) _roads.Add(t);
        }
        foreach (vc_TownNPC npc in GetComponentsInChildren<vc_TownNPC>(true))
            if (npc.body != null) { _layer = npc.body.sortingLayerName; _order = npc.body.sortingOrder; break; }
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        _player = pc != null ? pc.transform : null;
        Transform ps = _player != null ? _player.Find("Shadow") : null;
        _shadow = ps != null ? ps.GetComponent<SpriteRenderer>()?.sprite : null;

        _chStand = Load("Morning/chicken_stand"); _chPeck = Load("Morning/chicken_peck"); _chWalk = Load("Morning/chicken_walk");
        _catSleep = Load("Morning/cat_sleep"); _catSit = Load("Morning/cat_sit"); _catStretch = Load("Morning/cat_stretch"); _catWalk = Load("Morning/cat_walk");
        _laundry = new[] { Load("Morning/laundry_0"), Load("Morning/laundry_1") };
        _heart = Resources.Load<Sprite>("TownLife/emote_heart");
        _zz = Resources.Load<Sprite>("TownLife/emote_dots");
        _mistSprite = Sprite.Create(MistTex(), new Rect(0, 0, 64, 20), new Vector2(0.5f, 0.5f), 16f);
        _dewSprite = Sprite.Create(DewTex(), new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 16f);

        _root = new GameObject("Morning").transform;
        _root.SetParent(transform, false);

        var rng = new System.Random(StageSeed());
        SpawnLaundry(rng);
        SpawnChickens(rng);
        SpawnCat(rng);
        SpawnMist(rng);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (Chicken c in _chickens) TickChicken(c, dt);
        if (_cat != null) TickCat(dt);
        TickLaundry();
        TickMist(dt);
        TickDew(dt);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private int StageSeed()
    {
        vc_TownStage st = GetComponent<vc_TownStage>();
        string id = st != null ? st.stageId : name;
        unchecked { int h = 7; foreach (char ch in id) h = h * 31 + ch; return h; }
    }

    private static Sprite Load(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 16f);   // feet at the bottom
    }

    private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x / Px) * Px, Mathf.Round(p.y / Px) * Px, 0f);

    private bool OnRoad(Vector3 p)
    {
        foreach (Tilemap t in _roads) if (t.HasTile(t.WorldToCell(p))) return true;
        return false;
    }

    /// <summary>Open grass: walkable, not a road, not water.</summary>
    private bool Grass(Vector3 p) => _grid != null && _grid.IsWalkable(p) && !OnRoad(p) && (_water == null || !_water.HasTile(_water.WorldToCell(p)));

    private SpriteRenderer MakeRenderer(string name, Sprite s, Vector3 pos, int orderOffset, Transform parent = null)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(parent != null ? parent : _root, false);
        sr.transform.position = Snap(pos);
        sr.sprite = s;
        sr.sortingLayerName = _layer;
        sr.sortingOrder = _order + orderOffset;
        return sr;
    }

    private SpriteRenderer AddShadow(Transform parent, float scale)
    {
        if (_shadow == null) return null;
        var sh = new GameObject("Shadow").AddComponent<SpriteRenderer>();
        sh.transform.SetParent(parent, false);
        sh.transform.localScale = new Vector3(scale, scale, 1f);
        sh.sprite = _shadow;
        sh.sortingLayerName = _layer;
        sh.sortingOrder = _order - 2;
        sh.color = new Color(1f, 1f, 1f, 0.7f);
        return sh;
    }

    private bool NearCamera(Vector3 p, float margin)
    {
        if (_cam == null) return false;
        float hh = _cam.orthographicSize + margin, ww = _cam.orthographicSize * _cam.aspect + margin;
        Vector3 c = _cam.transform.position;
        return Mathf.Abs(p.x - c.x) < ww && Mathf.Abs(p.y - c.y) < hh;
    }

    /// <summary>Houses (wide buildings) in the town: their base sprite bounds.</summary>
    private List<Bounds> Houses()
    {
        var list = new List<Bounds>();
        Transform objects = transform.Find("Objects");
        if (objects == null) return list;
        foreach (Transform o in objects)
        {
            string n = o.name.ToLowerInvariant();
            if (!(n.Contains("house") || n.Contains("cabin") || n == "home")) continue;
            SpriteRenderer b = o.Find("Base") != null ? o.Find("Base").GetComponent<SpriteRenderer>() : o.GetComponentInChildren<SpriteRenderer>();
            if (b != null && b.sprite != null) list.Add(b.bounds);
        }
        return list;
    }

    // ── laundry ───────────────────────────────────────────────────────────

    private readonly List<(SpriteRenderer sr, float phase)> _lines = new List<(SpriteRenderer, float)>();

    private void SpawnLaundry(System.Random rng)
    {
        if (_laundry[0] == null) return;
        List<Bounds> houses = Houses();
        for (int i = houses.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (houses[i], houses[j]) = (houses[j], houses[i]); }
        var filter = new ContactFilter2D { useTriggers = false };
        var hits = new Collider2D[2];
        foreach (Bounds h in houses)
        {
            if (_lines.Count >= laundryLines) break;
            // beside the house, either side, standing on the yard
            foreach (float side in rng.Next(2) == 0 ? new[] { 1f, -1f } : new[] { -1f, 1f })
            {
                var spot = new Vector3(side > 0 ? h.max.x + 2.0f : h.min.x - 2.0f, h.min.y + 0.1f, 0f);
                bool ok = true;
                for (float dx = -1.4f; dx <= 1.41f && ok; dx += 0.7f)
                    for (float dy = 0f; dy <= 1.2f && ok; dy += 0.6f)
                        if (!Grass(spot + new Vector3(dx, dy, 0f))) ok = false;
                if (!ok) continue;
                if (Physics2D.OverlapBox(spot + new Vector3(0f, 0.6f, 0f), new Vector2(3.2f, 1.4f), 0f, filter, hits) > 0) continue;
                var sr = MakeRenderer("Laundry", _laundry[0], spot, -1);
                _lines.Add((sr, (float)rng.NextDouble() * 3f));
                break;
            }
        }
    }

    private void TickLaundry()
    {
        foreach (var (sr, phase) in _lines)
        {
            // flaps in little gusts, not a steady beat
            float t = Time.time + phase;
            bool gust = Mathf.Repeat(t, 5f) < 2.2f;
            int f = gust ? (Mathf.FloorToInt(t * 3f) & 1) : (Mathf.FloorToInt(t * 0.8f) & 1);
            sr.sprite = _laundry[f];
        }
    }

    // ── chickens ──────────────────────────────────────────────────────────

    private class Chicken
    {
        public Transform t; public SpriteRenderer body;
        public Vector3 home, target; public float wait, peckUntil, speed; public bool moving, fleeing;
    }
    private readonly List<Chicken> _chickens = new List<Chicken>();

    private void SpawnChickens(System.Random rng)
    {
        if (_chStand == null) return;
        // a little flock around one yard (a garden or a home), so they read as someone's chickens
        var homes = new List<Vector3>();
        Transform objects = transform.Find("Objects");
        if (objects != null)
            foreach (Transform o in objects)
            {
                string n = o.name.ToLowerInvariant();
                if (n.Contains("garden") || n.Contains("house") || n == "home" || n.Contains("cabin"))
                {
                    SpriteRenderer b = o.GetComponentInChildren<SpriteRenderer>();
                    if (b != null) homes.Add(new Vector3(b.bounds.center.x, b.bounds.min.y - 1.2f, 0f));
                }
            }
        if (homes.Count == 0) return;
        Vector3 yard = homes[rng.Next(homes.Count)];
        for (int i = 0; i < chickens; i++)
        {
            Vector2? p = _grid.RandomWalkable(yard, 2.2f, q => Grass(q));
            if (!p.HasValue) continue;
            var c = new Chicken { home = yard, speed = 1.1f + (float)rng.NextDouble() * 0.4f, wait = Random.Range(0f, 2f) };
            c.t = new GameObject("Chicken").transform;
            c.t.SetParent(_root, false);
            c.t.position = Snap(p.Value);
            AddShadow(c.t, 0.55f);
            c.body = MakeRenderer("Body", _chStand, c.t.position, 0, c.t);
            c.body.transform.localPosition = Vector3.zero;
            c.body.flipX = rng.Next(2) == 0;
            _chickens.Add(c);
        }
    }

    private void TickChicken(Chicken c, float dt)
    {
        Vector3 pos = c.t.position;
        float toPlayer = _player != null ? Vector2.Distance(pos, _player.position) : 99f;

        // the player runs at them: scatter
        if (toPlayer < 1.3f && !c.fleeing)
        {
            Vector2 away = ((Vector2)(pos - _player.position)).normalized;
            Vector3 dest = pos + (Vector3)(away * Random.Range(1.6f, 2.4f) + Random.insideUnitCircle * 0.6f);
            if (Grass(dest) || (_grid != null && _grid.IsWalkable(dest)))
            {
                c.target = dest; c.moving = true; c.fleeing = true;
            }
        }

        if (c.moving)
        {
            Vector2 to = c.target - pos;
            float sp = c.fleeing ? c.speed * 3f : c.speed;
            if (to.magnitude < 0.05f) { c.moving = false; c.fleeing = false; c.wait = Random.Range(0.8f, 3f); c.body.sprite = _chStand; return; }
            Vector3 next = pos + (Vector3)(to.normalized * Mathf.Min(sp * dt, to.magnitude));
            if (_grid != null && !_grid.IsWalkable(next)) { c.moving = false; c.fleeing = false; c.wait = 0.5f; return; }
            c.t.position = next;
            c.body.flipX = to.x < 0f;
            // hop-walk: alternate frames, and a 1px flutter hop while fleeing
            c.body.sprite = (Mathf.FloorToInt(Time.time * (c.fleeing ? 14f : 8f)) & 1) == 0 ? _chWalk : _chStand;
            c.body.transform.localPosition = new Vector3(0f, c.fleeing && (Mathf.FloorToInt(Time.time * 14f) & 1) == 0 ? Px * 2f : 0f, 0f);
            return;
        }

        c.body.transform.localPosition = Vector3.zero;
        if (Time.time < c.peckUntil)
        {
            c.body.sprite = (Mathf.FloorToInt(Time.time * 6f) & 1) == 0 ? _chPeck : _chStand;
            return;
        }
        c.body.sprite = _chStand;
        c.wait -= dt;
        if (c.wait > 0f) return;
        if (Random.value < 0.55f) { c.peckUntil = Time.time + Random.Range(0.8f, 2f); c.wait = Random.Range(0.4f, 1.5f); return; }
        // a few steps somewhere near their yard
        Vector2? p = _grid != null ? _grid.RandomWalkable(c.home, 2.5f, q => Grass(q) && Vector2.Distance(q, pos) < 2.2f) : null;
        if (p.HasValue) { c.target = p.Value; c.moving = true; }
        else c.wait = 1f;
    }

    // ── the town cat ──────────────────────────────────────────────────────

    private enum CatState { Sleep, Sit, Stretch, Walk }
    private class Cat
    {
        public Transform t; public SpriteRenderer body, emote;
        public CatState state; public float until, emoteUntil, aloneFor;
        public List<Vector3> path = new List<Vector3>(); public int pathIndex;
    }
    private Cat _cat;
    private readonly List<Vector3> _napSpots = new List<Vector3>();

    private void SpawnCat(System.Random rng)
    {
        if (_catSleep == null) return;
        foreach (Bounds h in Houses())
        {
            Vector3 door = new Vector3(h.center.x + 0.9f, h.min.y - 0.35f, 0f);   // beside the door, on the step
            Vector2? ok = _grid != null ? _grid.NearestWalkable(door, 2) : null;
            if (ok.HasValue) _napSpots.Add(ok.Value);
        }
        if (_napSpots.Count == 0) return;
        _cat = new Cat { state = CatState.Sleep };
        _cat.t = new GameObject("TownCat").transform;
        _cat.t.SetParent(_root, false);
        _cat.t.position = Snap(_napSpots[rng.Next(_napSpots.Count)]);
        AddShadow(_cat.t, 0.6f);
        _cat.body = MakeRenderer("Body", _catSleep, _cat.t.position, 0, _cat.t);
        _cat.body.transform.localPosition = Vector3.zero;
        _cat.emote = MakeRenderer("Emote", null, _cat.t.position, 0, _cat.t);
        _cat.emote.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        _cat.emote.sortingLayerName = "Decor";
        _cat.emote.sortingOrder = 10;
        _cat.emote.enabled = false;
    }

    private void CatEmote(Sprite s, float seconds)
    {
        if (s == null) return;
        _cat.emote.sprite = s;
        _cat.emote.enabled = true;
        _cat.emoteUntil = Time.time + seconds;
    }

    private void TickCat(float dt)
    {
        Cat c = _cat;
        if (c.emote.enabled && Time.time > c.emoteUntil) c.emote.enabled = false;
        float toPlayer = _player != null ? Vector2.Distance(c.t.position, _player.position) : 99f;
        if (_player != null && c.state != CatState.Walk) c.body.flipX = _player.position.x < c.t.position.x;

        switch (c.state)
        {
            case CatState.Sleep:
                c.body.sprite = _catSleep;
                // breathing: a 1px rise now and then; the odd "..." while dreaming
                c.body.transform.localPosition = new Vector3(0f, (Mathf.FloorToInt(Time.time * 0.8f) & 1) == 0 ? 0f : Px, 0f);
                if (!c.emote.enabled && Random.value < dt * 0.08f) CatEmote(_zz, 1.6f);
                if (toPlayer < 1.8f) { c.state = CatState.Sit; c.until = Time.time + 3f; c.body.transform.localPosition = Vector3.zero; }
                else
                {
                    c.aloneFor += dt;
                    if (c.aloneFor > 25f && Random.value < dt * 0.05f) { c.state = CatState.Stretch; c.until = Time.time + 1.4f; }
                }
                break;

            case CatState.Sit:
                c.body.sprite = _catSit;
                if (toPlayer < 2.4f) c.until = Time.time + 3f;
                if (Time.time > c.until) { c.state = Random.value < 0.4f ? CatState.Stretch : CatState.Sleep; c.until = Time.time + 1.4f; c.aloneFor = 0f; }
                break;

            case CatState.Stretch:
                c.body.sprite = _catStretch;
                if (Time.time > c.until) StartCatWalk();
                break;

            case CatState.Walk:
                if (c.pathIndex >= c.path.Count) { c.state = CatState.Sleep; c.aloneFor = 0f; break; }
                Vector3 wp = c.path[c.pathIndex];
                Vector2 to = wp - c.t.position;
                float step = 1.0f * dt;
                if (to.magnitude <= step) { c.t.position = wp; c.pathIndex++; break; }
                c.t.position += (Vector3)(to.normalized * step);
                c.body.flipX = to.x < 0f;
                c.body.sprite = (Mathf.FloorToInt(Time.time * 6f) & 1) == 0 ? _catWalk : _catStretch;
                break;
        }
    }

    private void StartCatWalk()
    {
        Cat c = _cat;
        c.aloneFor = 0f;
        if (_napSpots.Count < 2 || _grid == null) { c.state = CatState.Sleep; return; }
        Vector3 next = _napSpots[Random.Range(0, _napSpots.Count)];
        List<Vector3> path = _grid.FindPath(c.t.position, next);
        if (path.Count == 0 || Vector2.Distance(next, c.t.position) < 1f) { c.state = CatState.Sleep; return; }
        c.path = path; c.pathIndex = 0; c.state = CatState.Walk;
    }

    /// <summary>Tap the cat while standing near it: a pet, a purr, hearts.</summary>
    public bool TryPet(Vector2 tapWorld, Vector2 playerPos)
    {
        if (_cat == null) return false;
        Vector2 c = (Vector2)_cat.t.position + new Vector2(0f, 0.3f);
        if (Vector2.Distance(tapWorld, c) > 0.8f || Vector2.Distance(playerPos, _cat.t.position) > 2.2f) return false;
        if (_cat.state == CatState.Walk) _cat.path.Clear();
        _cat.state = CatState.Sit;
        _cat.until = Time.time + 4f;
        CatEmote(_heart, 1.6f);
        SunodGame.Core.vc_Sfx.Play("bubble", 0.35f, 0.15f, 0.5f);
        vc_QuestHUD.Instance?.ShowFeedbackTimed(Random.value < 0.5f ? "Purrrr..." : "The cat leans into your hand.", 1.8f);
        return true;
    }

    // ── morning mist ──────────────────────────────────────────────────────

    private readonly List<(SpriteRenderer sr, Vector3 home, float phase, float speed)> _mist = new List<(SpriteRenderer, Vector3, float, float)>();

    private void SpawnMist(System.Random rng)
    {
        // over the water first, then along the town's edges (where the trees are)
        var spots = new List<Vector3>();
        if (_water != null)
        {
            _water.CompressBounds();
            BoundsInt b = _water.cellBounds;
            for (int i = 0; i < 40 && spots.Count < mistPatches / 2; i++)
            {
                var cell = new Vector3Int(rng.Next(b.xMin, b.xMax), rng.Next(b.yMin, b.yMax), 0);
                if (_water.HasTile(cell)) spots.Add(_water.GetCellCenterWorld(cell));
            }
        }
        while (spots.Count < mistPatches)
        {
            float t = (float)rng.NextDouble();
            int edge = rng.Next(4);
            spots.Add(edge == 0 ? new Vector3(Mathf.Lerp(_rect.xMin, _rect.xMax, t), _rect.yMin + 0.5f, 0f)
                    : edge == 1 ? new Vector3(Mathf.Lerp(_rect.xMin, _rect.xMax, t), _rect.yMax - 1f, 0f)
                    : edge == 2 ? new Vector3(_rect.xMin + 1f, Mathf.Lerp(_rect.yMin, _rect.yMax, t), 0f)
                    : new Vector3(_rect.xMax - 1f, Mathf.Lerp(_rect.yMin, _rect.yMax, t), 0f));
        }
        foreach (Vector3 s in spots)
        {
            var sr = MakeRenderer("Mist", _mistSprite, s, 0);
            sr.sortingLayerName = "Decor";
            sr.sortingOrder = 5;
            float scale = 1f + (float)rng.NextDouble() * 0.8f;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            _mist.Add((sr, s, (float)rng.NextDouble() * 20f, 0.15f + (float)rng.NextDouble() * 0.15f));
        }
    }

    private void TickMist(float dt)
    {
        foreach (var (sr, home, phase, speed) in _mist)
        {
            float t = Time.time * speed + phase;
            sr.transform.position = Snap(home + new Vector3(Mathf.Sin(t) * 1.6f, Mathf.Sin(t * 0.6f) * 0.25f, 0f));
            // breathes in and out slowly
            sr.color = new Color(1f, 1f, 1f, 0.16f + 0.08f * Mathf.Sin(t * 0.7f));
        }
    }

    // ── dew ───────────────────────────────────────────────────────────────

    private readonly List<(SpriteRenderer sr, float born, float life)> _dew = new List<(SpriteRenderer, float, float)>();

    private void TickDew(float dt)
    {
        if (_cam == null || _player == null) return;
        while (_dew.Count < dewSparkles)
        {
            Vector3 p = _player.position + new Vector3(Random.Range(-5f, 5f), Random.Range(-3.5f, 3.5f), 0f);
            if (!Grass(p)) { if (Random.value < 0.5f) break; continue; }
            var sr = MakeRenderer("Dew", _dewSprite, p, -3);
            _dew.Add((sr, Time.time + Random.Range(0f, 2f), Random.Range(0.5f, 0.9f)));
        }
        for (int i = _dew.Count - 1; i >= 0; i--)
        {
            var (sr, born, life) = _dew[i];
            float k = (Time.time - born) / life;
            if (k < 0f) { sr.color = Color.clear; continue; }
            if (k >= 1f) { Destroy(sr.gameObject); _dew.RemoveAt(i); continue; }
            // twinkle: off, bright, off - stepped like the water glints
            float a = k < 0.33f ? 0.5f : k < 0.66f ? 1f : 0.35f;
            sr.color = new Color(1f, 1f, 1f, a);
        }
    }

    // ── generated sprites ─────────────────────────────────────────────────

    private static Texture2D MistTex()
    {
        var t = new Texture2D(64, 20, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        t.SetPixels32(new Color32[64 * 20]);
        for (int y = 0; y < 20; y++)
        for (int x = 0; x < 64; x++)
        {
            float dx = (x - 31.5f) / 32f, dy = (y - 9.5f) / 10f;
            float d = dx * dx + dy * dy;
            // three soft bands with a dithered edge, so it stays pixel art
            float a = d < 0.25f ? 1f : d < 0.55f ? 0.65f : d < 0.85f ? ((x + y) % 2 == 0 ? 0.4f : 0f) : d < 1f && (x + y) % 4 == 0 ? 0.25f : 0f;
            if (a > 0f) t.SetPixel(x, y, new Color(0.96f, 0.97f, 1f, a));
        }
        t.Apply();
        return t;
    }

    private static Texture2D DewTex()
    {
        var t = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        t.SetPixels32(new Color32[9]);
        var soft = new Color(0.85f, 0.95f, 1f, 0.6f);
        t.SetPixel(1, 0, soft); t.SetPixel(0, 1, soft); t.SetPixel(2, 1, soft); t.SetPixel(1, 2, soft);
        t.SetPixel(1, 1, Color.white);
        t.Apply();
        return t;
    }
}
