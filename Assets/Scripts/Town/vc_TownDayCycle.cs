using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// POST-FINAL (not active): the day passes, and you can see it. Built 2026-10-22, parked
/// because the towns stay morning for now (see TASKS.md, "Post-final"). Nothing adds this
/// component yet; to switch it on, add it to the town root in vc_TownStage.Awake next to
/// vc_TownAtmosphere.
///
/// The town's light follows the day's energy: a soft peach morning, clear midday, a golden
/// afternoon, and a deep blue-violet dusk once the day is used up. As evening comes:
///   - streetlamps switch on one by one, with a pixel halo that flickers now and then;
///   - fireflies drift up out of the grass and blink over the meadows and the water;
///   - the shed signs glow warm, so the way to bed is easy to spot.
/// Stays out of quests (rooms keep their normal light). Everything is stepped and pixel
/// snapped like the rest of the art; sprites are generated here, nothing to import.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownDayCycle : MonoBehaviour
{
    [SerializeField] private int fireflies = 16;
    [SerializeField] private float lightChangeSeconds = 3f;

    private const float Px = 1f / 16f;
    private const string Layer = "Decor";

    // time-of-day keyframes: (day progress 0..1, tint colour with strength in alpha)
    private static readonly (float t, Color c)[] Sky =
    {
        (0.00f, new Color(1.00f, 0.80f, 0.62f, 0.10f)),   // early morning: peach
        (0.30f, new Color(1.00f, 0.95f, 0.85f, 0.00f)),   // midday: clear
        (0.62f, new Color(1.00f, 0.74f, 0.32f, 0.10f)),   // afternoon: golden
        (0.85f, new Color(1.00f, 0.50f, 0.38f, 0.18f)),   // sunset: coral
        (1.00f, new Color(0.30f, 0.24f, 0.62f, 0.30f)),   // dusk: deep blue-violet
    };

    private Camera _cam;
    private SpriteRenderer _tint;
    private float _shown;                 // displayed day progress (eases toward the real one)
    private float _questFade = 1f;        // 0 while in a quest room
    private vc_TownStage _stage;
    private vc_TownGrid _grid;
    private Rect _rect;
    private Tilemap _water;

    private Sprite _glowSprite, _fireflySprite, _pixelSprite;

    private class Lamp { public SpriteRenderer halo; public float onAt, flickerUntil, nextFlicker; }
    private readonly List<Lamp> _lamps = new List<Lamp>();

    private class Fly { public SpriteRenderer sr; public Vector3 home, pos; public float t, speed, blink; }
    private readonly List<Fly> _flies = new List<Fly>();

    private readonly List<SpriteRenderer> _shedGlows = new List<SpriteRenderer>();

    private void Start()
    {
        _cam = Camera.main;
        _stage = GetComponent<vc_TownStage>();
        _grid = vc_TownGrid.For(transform);
        _rect = vc_TownGrid.TownRect(transform);
        _water = transform.Find("Grid")?.Find("water")?.GetComponent<Tilemap>();

        _pixelSprite = MakeSprite(Solid(2, 2, Color.white), 2f);
        _glowSprite = MakeSprite(Halo(28, new Color(1f, 0.86f, 0.5f)), 16f);
        _fireflySprite = MakeSprite(Firefly(), 16f);

        var tintGo = new GameObject("DayTint");
        tintGo.transform.SetParent(transform, false);
        _tint = tintGo.AddComponent<SpriteRenderer>();
        _tint.sprite = _pixelSprite;
        _tint.sortingLayerName = Layer;
        _tint.sortingOrder = 300;
        _tint.color = Color.clear;

        CollectLamps();
        _shown = DayProgress();
    }

    /// <summary>0 at the start of the day, 1 when the day's energy is all spent.</summary>
    private float DayProgress()
    {
        if (_stage == null || _stage.questsPerDay <= 0) return 0f;
        return Mathf.Clamp01((_stage.questsPerDay - _stage.Energy) / (float)_stage.questsPerDay);
    }

    private void LateUpdate()
    {
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
        float dt = Time.deltaTime;

        // light eases toward the real time of day (a quest done = the sun moves on)
        _shown = Mathf.MoveTowards(_shown, DayProgress(), dt / Mathf.Max(0.1f, lightChangeSeconds));
        bool inQuest = _stage != null && _stage.InQuest;
        _questFade = Mathf.MoveTowards(_questFade, inQuest ? 0f : 1f, dt * 2f);

        // the tint covers whatever the camera sees
        float h = _cam.orthographicSize * 2f + 2f, w = h * _cam.aspect + 2f;
        Vector3 cp = _cam.transform.position;
        _tint.transform.position = new Vector3(cp.x, cp.y, 0f);
        _tint.transform.localScale = new Vector3(w, h, 1f);
        Color sky = SkyAt(_shown);
        sky.a *= _questFade;
        _tint.color = sky;

        float dusk = Mathf.Clamp01((_shown - 0.7f) / 0.3f) * _questFade;   // 0 by day, 1 at dusk
        TickLamps(dusk);
        TickFireflies(dusk, dt);
        foreach (SpriteRenderer g in _shedGlows)
            if (g != null) g.color = new Color(1f, 0.8f, 0.45f, dusk * (0.55f + 0.1f * Mathf.Round(Mathf.Sin(Time.time * 2.3f) * 2f) / 2f));
    }

    private static Color SkyAt(float t)
    {
        for (int i = 1; i < Sky.Length; i++)
            if (t <= Sky[i].t)
                return Color.Lerp(Sky[i - 1].c, Sky[i].c, Mathf.InverseLerp(Sky[i - 1].t, Sky[i].t, t));
        return Sky[Sky.Length - 1].c;
    }

    // ── lamps ─────────────────────────────────────────────────────────────

    private void CollectLamps()
    {
        Transform objects = transform.Find("Objects");
        if (objects == null) return;
        int lampIndex = 0;
        foreach (Transform o in objects)
        {
            string n = o.name.ToLowerInvariant();
            if (n.StartsWith("shed"))
            {
                var shed = o.GetComponent<vc_TownShed>();
                if (shed != null && shed.sign != null) _shedGlows.Add(MakeGlow(shed.sign.transform.position, 0.9f, 305));
                continue;
            }
            if (!n.Contains("lamp")) continue;
            SpriteRenderer top = o.Find("Top") != null ? o.Find("Top").GetComponent<SpriteRenderer>() : null;
            SpriteRenderer any = top != null ? top : o.GetComponentInChildren<SpriteRenderer>();
            if (any == null || any.sprite == null) continue;
            Bounds b = any.bounds;
            // the light sits near the top of the post
            var halo = MakeGlow(new Vector3(b.center.x, b.max.y - 0.35f, 0f), 1.25f, 310);
            _lamps.Add(new Lamp { halo = halo, onAt = 0.15f + 0.12f * (lampIndex++ % 5), nextFlicker = Time.time + Random.Range(4f, 12f) });
        }
    }

    private SpriteRenderer MakeGlow(Vector3 pos, float scale, int order)
    {
        var sr = new GameObject("Glow").AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.transform.position = Snap(pos);
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.sprite = _glowSprite;
        sr.sortingLayerName = Layer;
        sr.sortingOrder = order;
        sr.color = Color.clear;
        return sr;
    }

    private void TickLamps(float dusk)
    {
        foreach (Lamp l in _lamps)
        {
            // lamps come on one after another as it gets dark
            float on = Mathf.Clamp01((dusk - l.onAt) / 0.15f);
            if (on > 0.9f && Time.time > l.nextFlicker)
            {
                l.flickerUntil = Time.time + 0.25f;
                l.nextFlicker = Time.time + Random.Range(6f, 16f);
            }
            float flicker = Time.time < l.flickerUntil && Mathf.Repeat(Time.time * 20f, 1f) < 0.5f ? 0.45f : 1f;
            l.halo.color = new Color(1f, 0.88f, 0.55f, on * 0.75f * flicker);
        }
    }

    // ── fireflies ─────────────────────────────────────────────────────────

    private void TickFireflies(float dusk, float dt)
    {
        int want = Mathf.RoundToInt(fireflies * Mathf.Clamp01((dusk - 0.3f) / 0.5f));
        while (_flies.Count < want)
        {
            Vector3? home = FireflySpot();
            if (!home.HasValue) break;
            var f = new Fly { home = home.Value, pos = home.Value, t = Random.value * 10f, speed = Random.Range(0.5f, 1.1f), blink = Random.value * 3f };
            f.sr = new GameObject("Firefly").AddComponent<SpriteRenderer>();
            f.sr.transform.SetParent(transform, false);
            f.sr.sprite = _fireflySprite;
            f.sr.sortingLayerName = Layer;
            f.sr.sortingOrder = 320;
            _flies.Add(f);
        }
        while (_flies.Count > want) { Destroy(_flies[_flies.Count - 1].sr.gameObject); _flies.RemoveAt(_flies.Count - 1); }

        foreach (Fly f in _flies)
        {
            f.t += dt * f.speed;
            // lazy loops around its spot, slowly rising and settling
            f.pos = f.home + new Vector3(Mathf.Sin(f.t * 0.9f) * 1.1f + Mathf.Sin(f.t * 2.3f) * 0.25f,
                                         0.6f + Mathf.Sin(f.t * 0.7f) * 0.5f + Mathf.Cos(f.t * 1.9f) * 0.15f, 0f);
            f.sr.transform.position = Snap(f.pos);
            float b = Mathf.Repeat(f.t + f.blink, 2.4f);
            float a = b < 1.2f ? Mathf.Round(Mathf.Sin(b / 1.2f * Mathf.PI) * 3f) / 3f : 0.15f;   // stepped blink
            f.sr.color = new Color(0.86f, 1f, 0.45f, a * dusk);
            if (!NearCamera(f.home, 3f)) { Vector3? h = FireflySpot(); if (h.HasValue) f.home = h.Value; }
        }
    }

    /// <summary>Somewhere open near the camera: grass first, the water's edge counts too.</summary>
    private Vector3? FireflySpot()
    {
        if (_cam == null) return null;
        float hh = _cam.orthographicSize, ww = hh * _cam.aspect;
        Vector3 c = _cam.transform.position;
        for (int i = 0; i < 12; i++)
        {
            var p = new Vector3(c.x + Random.Range(-ww, ww), c.y + Random.Range(-hh, hh), 0f);
            if (!_rect.Contains(p)) continue;
            bool water = _water != null && _water.HasTile(_water.WorldToCell(p));
            if (water || _grid == null || _grid.IsWalkable(p)) return p;
        }
        return null;
    }

    private bool NearCamera(Vector3 p, float margin)
    {
        if (_cam == null) return false;
        float hh = _cam.orthographicSize + margin, ww = _cam.orthographicSize * _cam.aspect + margin;
        Vector3 c = _cam.transform.position;
        return Mathf.Abs(p.x - c.x) < ww && Mathf.Abs(p.y - c.y) < hh;
    }

    // ── generated pixel sprites ───────────────────────────────────────────

    private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x / Px) * Px, Mathf.Round(p.y / Px) * Px, 0f);

    private static Sprite MakeSprite(Texture2D tex, float ppu) =>
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);

    private static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        t.SetPixels32(new Color32[w * h]);
        return t;
    }

    private static Texture2D Solid(int w, int h, Color c)
    {
        Texture2D t = NewTex(w, h);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) t.SetPixel(x, y, c);
        t.Apply();
        return t;
    }

    /// <summary>A round halo in 3 stepped rings with a dithered edge (pixel-art light).</summary>
    private static Texture2D Halo(int size, Color c)
    {
        Texture2D t = NewTex(size, size);
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
            float a = d < 0.3f ? 0.7f : d < 0.55f ? 0.42f : d < 0.8f ? 0.2f : d < 1f && ((x + y) & 1) == 0 ? 0.1f : 0f;
            if (a > 0f) t.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
        }
        t.Apply();
        return t;
    }

    private static Texture2D Firefly()
    {
        Texture2D t = NewTex(3, 3);
        var glow = new Color(1f, 1f, 0.6f, 0.45f);
        t.SetPixel(1, 0, glow); t.SetPixel(0, 1, glow); t.SetPixel(2, 1, glow); t.SetPixel(1, 2, glow);
        t.SetPixel(1, 1, Color.white);
        t.Apply();
        return t;
    }
}
