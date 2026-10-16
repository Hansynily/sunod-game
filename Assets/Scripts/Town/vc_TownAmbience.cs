using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Makes a town feel alive without touching its layout. Sits on the town root (next to
/// vc_TownStage) and works on what the town builder made:
///   - wind: gusts roll across the town; tree canopies, grass tufts and flowers shift 1px
///   - butterflies flutter around the flower beds
///   - leaves drift down from trees near the camera
///   - soft cloud shadows slide over the town
///   - glints twinkle on the water
/// Everything moves in whole pixels and stepped frames, like the rest of the pixel art.
/// Only things near the camera are updated, so it stays cheap on phones.
/// Sprites come from Resources/Ambience.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownAmbience : MonoBehaviour
{
    [Header("Wind")]
    [SerializeField] private float windTick = 0.14f;
    [SerializeField] private float gustSpeed = 1.6f;
    [SerializeField] private float gustWavelength = 0.35f;

    [Header("Critters / particles")]
    [SerializeField] private int butterflies = 7;
    [SerializeField] private int leaves = 6;
    [SerializeField] private int cloudShadows = 3;
    [SerializeField] private int waterGlints = 10;
    [SerializeField] private float cloudAlpha = 0.11f;

    private const float Ppu = 16f;              // town art is 16 px per unit
    private const float Px = 1f / Ppu;

    private struct Swayer { public Transform t; public Vector3 basePos; public float phase; public bool canopy; public int last; }

    private readonly List<Swayer> _sway = new List<Swayer>();
    private readonly List<Vector3> _flowerSpots = new List<Vector3>();
    private readonly List<Vector3> _treeTops = new List<Vector3>();
    private readonly List<Vector3> _waterCells = new List<Vector3>();

    private Camera _cam;
    private float _windClock;
    private Rect _townRect;

    // pooled actors
    private class Flyer { public SpriteRenderer sr; public Vector3 home, pos; public float t, speed, seed; }
    private class Faller { public SpriteRenderer sr; public Vector3 pos; public float life, dur, seed; }
    private class Glint { public SpriteRenderer sr; public float life, dur; }

    private readonly List<Flyer> _flyers = new List<Flyer>();
    private readonly List<Faller> _fallers = new List<Faller>();
    private readonly List<Glint> _glints = new List<Glint>();
    private readonly List<SpriteRenderer> _clouds = new List<SpriteRenderer>();
    private Sprite[] _butterflyFrames, _leafFrames, _glintFrames;

    private static readonly Color[] ButterflyColors =
    {
        new Color32(255, 214, 102, 255), new Color32(255, 160, 190, 255), new Color32(170, 210, 255, 255),
        new Color32(255, 255, 255, 255), new Color32(200, 170, 255, 255),
    };

    private void Start()
    {
        _cam = Camera.main;
        _butterflyFrames = new[] { Load("Ambience/butterfly_0"), Load("Ambience/butterfly_1") };
        _leafFrames = new[] { Load("Ambience/leaf_0"), Load("Ambience/leaf_1") };
        _glintFrames = new[] { Load("Ambience/glint_0"), Load("Ambience/glint_1") };

        CollectTownBits();
        BuildActors();
    }

    // ── Setup ────────────────────────────────────────────────────────────

    private void CollectTownBits()
    {
        Transform walls = transform.Find("Bounds");
        _townRect = new Rect(0f, 0f, 48f, 36f);
        if (walls != null && walls.childCount > 0)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Transform w in walls)
            {
                x0 = Mathf.Min(x0, w.position.x); x1 = Mathf.Max(x1, w.position.x);
                y0 = Mathf.Min(y0, w.position.y); y1 = Mathf.Max(y1, w.position.y);
            }
            _townRect = Rect.MinMaxRect(x0, y0, x1, y1);
        }

        foreach (string group in new[] { "Nature", "QuestAreaBackdrop" })
        {
            Transform g = transform.Find(group);
            if (g == null) continue;
            foreach (Transform tree in g)
            {
                Transform top = tree.Find("Top");
                if (top == null) { AddSwayer(tree, false); continue; }   // backdrop tufts
                AddSwayer(top, true);
                if (group == "Nature") _treeTops.Add(top.position + new Vector3(0f, 0.8f, 0f));
            }
        }

        Transform flora = transform.Find("Flora");
        if (flora != null)
        {
            foreach (Transform f in flora)
            {
                string n = f.name;
                if (n.StartsWith("rock") || n.StartsWith("pebble")) continue;
                AddSwayer(f, false);
                if (n.Contains("flower") || n.StartsWith("bloom")) _flowerSpots.Add(f.position);
            }
        }

        Transform grid = transform.Find("Grid");
        Tilemap water = grid != null ? grid.Find("water")?.GetComponent<Tilemap>() : null;
        if (water != null)
        {
            foreach (Vector3Int c in water.cellBounds.allPositionsWithin)
                if (water.HasTile(c)) _waterCells.Add(water.GetCellCenterWorld(c));
        }
    }

    private void AddSwayer(Transform t, bool canopy)
    {
        _sway.Add(new Swayer { t = t, basePos = t.localPosition, phase = Random.value * 0.6f, canopy = canopy, last = 0 });
    }

    private void BuildActors()
    {
        var root = new GameObject("Ambience").transform;
        root.SetParent(transform, false);

        if (_butterflyFrames[0] != null && _flowerSpots.Count > 0)
            for (int i = 0; i < butterflies; i++)
            {
                var f = new Flyer { sr = MakeSprite(root, "Butterfly", _butterflyFrames[0], "Vegetation", 6), seed = Random.value * 100f, speed = Random.Range(0.6f, 1.1f) };
                f.sr.color = ButterflyColors[i % ButterflyColors.Length];
                f.home = _flowerSpots[Random.Range(0, _flowerSpots.Count)];
                f.pos = f.home;
                _flyers.Add(f);
            }

        if (_leafFrames[0] != null && _treeTops.Count > 0)
            for (int i = 0; i < leaves; i++)
            {
                var l = new Faller { sr = MakeSprite(root, "Leaf", _leafFrames[0], "Vegetation", 5), life = 1f, dur = 1f };
                l.sr.enabled = false;
                _fallers.Add(l);
            }

        Sprite cloud = Load("Ambience/cloud_shadow", 12f, FilterMode.Bilinear);
        if (cloud != null)
            for (int i = 0; i < cloudShadows; i++)
            {
                var c = MakeSprite(root, "CloudShadow", cloud, "Vegetation", 30);
                c.color = new Color(0f, 0f, 0f, cloudAlpha);
                float s = Random.Range(1.4f, 2.2f);
                c.transform.localScale = new Vector3(s, s * 0.8f, 1f);
                c.transform.position = new Vector3(
                    Mathf.Lerp(_townRect.xMin - 6f, _townRect.xMax, (i + Random.value * 0.5f) / cloudShadows),
                    Random.Range(_townRect.yMin, _townRect.yMax), 0f);
                _clouds.Add(c);
            }

        if (_glintFrames[0] != null && _waterCells.Count > 0)
            for (int i = 0; i < waterGlints; i++)
            {
                var g = new Glint { sr = MakeSprite(root, "Glint", _glintFrames[0], "Default", 3), life = Random.value, dur = Random.Range(0.6f, 1.2f) };
                g.sr.enabled = false;
                _glints.Add(g);
            }
    }

    private static SpriteRenderer MakeSprite(Transform parent, string name, Sprite s, string layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    private static Sprite Load(string path, float ppu = Ppu, FilterMode filter = FilterMode.Point)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null) return null;
        tex.filterMode = filter;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
    }

    // ── Per frame ────────────────────────────────────────────────────────

    private void Update()
    {
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
        Rect view = CameraRect(2f);

        _windClock += Time.deltaTime;
        if (_windClock >= windTick)
        {
            _windClock = 0f;
            UpdateWind(view);
        }

        float dt = Time.deltaTime;
        UpdateButterflies(view, dt);
        UpdateLeaves(view, dt);
        UpdateClouds(dt);
        UpdateGlints(view, dt);
    }

    private Rect CameraRect(float margin)
    {
        float h = _cam.orthographicSize, w = h * _cam.aspect;
        Vector3 c = _cam.transform.position;
        return Rect.MinMaxRect(c.x - w - margin, c.y - h - margin, c.x + w + margin, c.y + h + margin);
    }

    /// <summary>A gust is a wave rolling left to right; strength itself breathes slowly.</summary>
    private void UpdateWind(Rect view)
    {
        float t = Time.time;
        float strength = 0.45f + 0.35f * Mathf.Sin(t * 0.23f) + 0.2f * Mathf.Sin(t * 0.61f + 1.3f);
        float threshold = Mathf.Lerp(0.95f, 0.35f, Mathf.Clamp01(strength));

        for (int i = 0; i < _sway.Count; i++)
        {
            Swayer s = _sway[i];
            if (s.t == null) continue;
            Vector3 p = s.t.position;
            if (!view.Contains(p)) continue;

            float wave = Mathf.Sin(t * gustSpeed - p.x * gustWavelength + s.phase);
            int off = wave > threshold ? 1 : 0;
            if (s.canopy && wave > threshold + 0.25f) off = 2;          // canopies lean a bit further
            if (off == s.last) continue;
            s.last = off;
            s.t.localPosition = s.basePos + new Vector3(off * Px, 0f, 0f);
            _sway[i] = s;
        }
    }

    private void UpdateButterflies(Rect view, float dt)
    {
        foreach (Flyer f in _flyers)
        {
            // re-home off-screen butterflies onto flowers near the camera so they're seen
            if (!view.Contains(f.pos))
            {
                Vector3? near = RandomSpotIn(_flowerSpots, view);
                if (near.HasValue) { f.home = near.Value; f.pos = f.home; }
            }
            f.t += dt * f.speed;
            float a = f.t + f.seed;
            Vector3 target = f.home + new Vector3(Mathf.Sin(a * 0.9f) * 1.6f + Mathf.Sin(a * 2.3f) * 0.4f,
                                                  0.6f + Mathf.Sin(a * 1.3f) * 0.5f + Mathf.Abs(Mathf.Sin(a * 4.1f)) * 0.25f, 0f);
            f.pos = target;
            f.sr.transform.position = Snap(f.pos);
            f.sr.sprite = _butterflyFrames[(int)(a * 8f) % 2];
            f.sr.flipX = Mathf.Cos(a * 0.9f) < 0f;
        }
    }

    private void UpdateLeaves(Rect view, float dt)
    {
        foreach (Faller l in _fallers)
        {
            l.life += dt / l.dur;
            if (l.life >= 1f)
            {
                l.sr.enabled = false;
                if (Random.value > dt * 0.6f) continue;              // ~one new leaf every couple of seconds per slot
                Vector3? top = RandomSpotIn(_treeTops, view);
                if (!top.HasValue) continue;
                l.pos = top.Value + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.2f, 0.3f), 0f);
                l.life = 0f;
                l.dur = Random.Range(2.2f, 3.4f);
                l.seed = Random.value * 10f;
                l.sr.color = Color.Lerp(new Color32(120, 190, 80, 255), new Color32(230, 190, 80, 255), Random.value * 0.6f);
                l.sr.enabled = true;
            }
            float k = l.life;
            Vector3 p = l.pos + new Vector3(Mathf.Sin((k * 6f) + l.seed) * 0.35f + k * 0.6f, -k * 1.6f, 0f);
            l.sr.transform.position = Snap(p);
            l.sr.sprite = _leafFrames[(int)(k * 10f + l.seed) % 2];
            Color c = l.sr.color; c.a = k < 0.8f ? 1f : (1f - k) / 0.2f; l.sr.color = c;
        }
    }

    private void UpdateClouds(float dt)
    {
        foreach (SpriteRenderer c in _clouds)
        {
            Vector3 p = c.transform.position;
            p.x += dt * 0.35f;
            if (p.x > _townRect.xMax + 8f)
            {
                p.x = _townRect.xMin - 8f;
                p.y = Random.Range(_townRect.yMin, _townRect.yMax);
            }
            c.transform.position = p;
        }
    }

    private void UpdateGlints(Rect view, float dt)
    {
        foreach (Glint g in _glints)
        {
            g.life += dt / g.dur;
            if (g.life >= 1f)
            {
                Vector3? cell = RandomSpotIn(_waterCells, view);
                g.sr.enabled = cell.HasValue;
                if (!cell.HasValue) { g.life = 0f; continue; }
                g.sr.transform.position = Snap(cell.Value + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), 0f));
                g.life = 0f;
                g.dur = Random.Range(0.5f, 1.1f);
            }
            // tiny -> big -> tiny twinkle
            g.sr.sprite = _glintFrames[g.life > 0.3f && g.life < 0.7f ? 0 : 1];
            g.sr.color = new Color(1f, 1f, 1f, Mathf.Sin(g.life * Mathf.PI) * 0.85f);
        }
    }

    private static Vector3? RandomSpotIn(List<Vector3> spots, Rect view)
    {
        if (spots.Count == 0) return null;
        for (int tries = 0; tries < 12; tries++)
        {
            Vector3 s = spots[Random.Range(0, spots.Count)];
            if (view.Contains(s)) return s;
        }
        return null;
    }

    private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
}
