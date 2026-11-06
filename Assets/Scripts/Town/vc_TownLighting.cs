using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The towns' light (2026-11-07, after Han's references): a low warm sun.
///  - cast shadows: every building, tree, prop, the home, the stall and the board throws a
///    soft slanted shadow down-right (SUNOD/SpriteCastShadow),
///  - a warm wash and a soft vignette over the world (camera-attached, under the HUD),
///  - the sea gets deeper away from the shore (darker blue past the foam).
/// Added by vc_TownStage at runtime, so it works on every town without a rebuild. Purely
/// visual. Tweak the numbers below.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownLighting : MonoBehaviour
{
    public static Color ShadowColor = new Color32(36, 24, 70, 100);
    public static float Skew = 0.55f;        // shadow slants right by this much per unit of height
    public static float Drop = -0.5f;       // ...and falls down by this much
    public static Color Warm = new Color(1f, 0.72f, 0.36f, 0.06f);
    public static float VignetteStrength = 0f;   // Han: no vignette (2026-11-07)

    private static readonly string[] SmallThings =
        { "tuft", "flower", "tiny_", "grass_", "bloom", "mush", "clover", "pebble", "debris", "towel", "shell", "starfish", "foam", "plank", "bush", "dock", "boat",
          "umbrella" };   // has its own drawn round shadow; a slanted canopy shadow floats off the pole

    private Material _shadowMat;
    private Transform _cam;
    private SpriteRenderer _vignette, _warm;
    private Camera _camera;

    private void Start()
    {
        Shader sh = Shader.Find("SUNOD/SpriteCastShadow");
        Material res = Resources.Load<Material>("Lighting/CastShadow");
        _shadowMat = res != null ? res : (sh != null ? new Material(sh) : null);
        if (_shadowMat != null)
        {
            _shadowMat.SetColor("_ShadowColor", ShadowColor);
            _shadowMat.SetFloat("_Skew", Skew);
            _shadowMat.SetFloat("_Squash", Drop);
            AddShadows();
            Invoke(nameof(AddShadows), 1.5f);   // things placed a moment later (the board) get theirs too
        }
        else Debug.LogWarning("[vc_TownLighting] Shadow shader missing (Resources/Lighting/CastShadow).");

        DeepenSea();
        BuildScreenWash();
    }

    // ── cast shadows ──────────────────────────────────────────────────────

    private void AddShadows()
    {
        int made = 0;
        foreach (string rootName in new[] { "Objects", "Nature" })
        {
            Transform root = transform.Find(rootName);
            if (root == null) continue;
            foreach (Transform obj in root) made += ShadowFor(obj, obj.position.y);
        }
        foreach (vc_KeepsakeStall s in GetComponentsInChildren<vc_KeepsakeStall>(true)) made += ShadowFor(s.transform, s.transform.position.y);
        Transform board = transform.Find("TownEvents/BulletinBoard");
        if (board != null) made += ShadowFor(board, board.position.y);
        if (made > 0) Debug.Log($"[vc_TownLighting] {made} cast shadows");
    }

    /// <summary>One shadow sprite per part (base + top), all slanted from the object's ground line.</summary>
    private int ShadowFor(Transform obj, float groundY)
    {
        string n = obj.name.ToLowerInvariant();
        foreach (string s in SmallThings) if (n.StartsWith(s)) return 0;
        if (obj.Find("CastShadow") != null) return 0;

        var parts = new List<SpriteRenderer>();
        float top = groundY;
        foreach (SpriteRenderer sr in obj.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || sr.name == "Sign" || sr.name.StartsWith("Shadow")) continue;
            parts.Add(sr);
            top = Mathf.Max(top, sr.bounds.max.y);
        }
        if (parts.Count == 0 || top - groundY < 0.8f) return 0;   // too low to cast anything you'd see

        var holder = new GameObject("CastShadow").transform;
        holder.SetParent(obj, false);
        var mpb = new MaterialPropertyBlock();
        mpb.SetFloat("_GroundY", groundY);
        foreach (SpriteRenderer part in parts)
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(holder, false);
            go.transform.position = part.transform.position;
            go.transform.rotation = part.transform.rotation;
            go.transform.localScale = part.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = part.sprite;
            sr.flipX = part.flipX;
            sr.sharedMaterial = _shadowMat;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = -12;    // on the ground: over the tiles, under everything standing
            sr.SetPropertyBlock(mpb);
            // culling: cover where the slanted shadow actually lands, or it pops out at the screen edge
            Bounds b = part.bounds;
            float hTop = Mathf.Max(0f, b.max.y - groundY);
            var reach = new Bounds(b.center, b.size);
            reach.Encapsulate(new Vector3(b.max.x + hTop * Skew + 0.5f, groundY + hTop * Drop - 0.5f, 0f));
            reach.Encapsulate(new Vector3(b.min.x - 0.5f, groundY + 0.5f, 0f));
            sr.bounds = reach;
        }
        return 1;
    }

    // ── sea depth ─────────────────────────────────────────────────────────

    private void DeepenSea()
    {
        Transform foam = transform.Find("ShoreFoam");
        if (foam == null || foam.childCount == 0) return;
        float shore = float.MaxValue, x0 = float.MaxValue, x1 = float.MinValue;
        foreach (Transform f in foam)
        {
            shore = Mathf.Min(shore, f.position.y);
            x0 = Mathf.Min(x0, f.position.x); x1 = Mathf.Max(x1, f.position.x);
            // foam over the deep-water layer
            foreach (SpriteRenderer sr in f.GetComponentsInChildren<SpriteRenderer>()) sr.sortingOrder = -13;
        }

        const int H = 64;
        var tex = new Texture2D(1, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color deep = new Color32(12, 46, 104, 255);
        for (int y = 0; y < H; y++)
        {
            float dist = (1f - y / (H - 1f)) * 40f;            // units below the top edge (just past the foam)
            float a = Mathf.Clamp01((dist - 0.5f) / 7f);       // shallow right at the shore, deep blue ~7 tiles out
            a = Mathf.Round(a * 6f) / 6f * 0.72f;              // stepped, like pixel-art water bands
            tex.SetPixel(0, y, new Color(deep.r, deep.g, deep.b, a));
        }
        tex.Apply();

        float depth = 40f, width = (x1 - x0) + 140f;
        var go = new GameObject("DeepSea");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3((x0 + x1) / 2f, shore - 0.4f - depth / 2f, 0f);
        var sr2 = go.AddComponent<SpriteRenderer>();
        sr2.sprite = Sprite.Create(tex, new Rect(0, 0, 1, H), new Vector2(0.5f, 0.5f), 1f);
        sr2.drawMode = SpriteDrawMode.Simple;
        go.transform.localScale = new Vector3(width, depth / H, 1f);
        sr2.sortingLayerName = "Default";
        sr2.sortingOrder = -14;   // over the water tiles, under the foam and wave crests
    }

    // ── warm wash + vignette (camera-attached, over the world, under the screen UI) ──

    private void BuildScreenWash()
    {
        _camera = Camera.main;
        if (_camera == null) return;
        _cam = _camera.transform;
        string topLayer = SortingLayer.layers[SortingLayer.layers.Length - 1].name;

        const int S = 128;
        var vt = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color edge = new Color32(36, 18, 30, 255);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f) / S * 2f - 1f, dy = (y + 0.5f) / S * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.2f, r)) * VignetteStrength;
                vt.SetPixel(x, y, new Color(edge.r, edge.g, edge.b, a));
            }
        vt.Apply();
        if (VignetteStrength > 0f) _vignette = ScreenSprite("Vignette", Sprite.Create(vt, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S), topLayer, 32001);

        var wt = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        wt.SetPixel(0, 0, Color.white); wt.Apply();
        _warm = ScreenSprite("WarmLight", Sprite.Create(wt, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f), topLayer, 32000);
        _warm.color = Warm;
    }

    private SpriteRenderer ScreenSprite(string name, Sprite s, string layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    private void LateUpdate()
    {
        // the gameplay scene (and its camera) can load a moment after the town starts
        if (_warm == null && Camera.main != null) BuildScreenWash();
        if (_cam == null || _camera == null) return;
        float h = _camera.orthographicSize * 2f * 1.04f, w = h * _camera.aspect;
        Vector3 at = new Vector3(_cam.position.x, _cam.position.y, 0f);
        if (_vignette != null) { _vignette.transform.position = at; _vignette.transform.localScale = new Vector3(w, h, 1f); }
        if (_warm != null) { _warm.transform.position = at; _warm.transform.localScale = new Vector3(w, h, 1f); }
    }
}
