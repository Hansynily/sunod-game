using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The stall's cosmetics on the player (bought with stars, kept across loops; server keeps
/// what is worn - vc_LoopMeta.Equipped):
///  - outfit: a palette swap of the clothes, done on the player's own frames (every facing,
///    walk and skill-use pose). Only pixels below the collar line are touched, so hair that
///    shares a colour with the trousers / skirt keeps its colour,
///  - hat: an overlay sprite per facing that sits on the top of the head of whatever frame is
///    showing,
///  - trail: little sprites left behind while walking (and the Old Sneakers Lv2 dust),
///  - pet: Muning, the town cat, follows you around town.
/// Art: Tools/art/make_cosmetics.py -> Resources/Cosmetics. Purely visual: nothing here touches
/// quests, the timer, stars or scoring. Adds itself to the player on every scene load.
/// </summary>
[DisallowMultipleComponent]
public class vc_PlayerCosmetics : MonoBehaviour
{
    private const float Ppu = 32f;   // the player frames are 32 px per unit

    private static vc_PlayerCosmetics _current;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded += (_, __) => Attach();
        Attach();
    }

    private static void Attach()
    {
        vc_SpriteFrameAnimator anim = FindFirstObjectByType<vc_SpriteFrameAnimator>();
        if (anim != null && anim.GetComponent<vc_PlayerCosmetics>() == null)
            anim.gameObject.AddComponent<vc_PlayerCosmetics>();
    }

    /// <summary>The equip beat: the player spins into the new look with a sparkle.</summary>
    public static void SpinPlayer()
    {
        if (_current == null) Attach();
        if (_current != null) _current.StartCoroutine(_current.Spin());
    }

    private SpriteRenderer _body, _hat;
    private string _outfit = "", _hatId = "", _trail = "", _pet = "";
    private OutfitDef _def;
    private readonly Dictionary<Sprite, Sprite> _recolored = new Dictionary<Sprite, Sprite>();
    private readonly HashSet<Sprite> _ours = new HashSet<Sprite>();
    private readonly Dictionary<Sprite, Vector2Int> _headTop = new Dictionary<Sprite, Vector2Int>();
    private readonly Dictionary<string, Sprite> _hatSprites = new Dictionary<string, Sprite>();
    private Vector3 _lastPos;
    private float _trailClock;
    private bool _spinning;

    private void Awake()
    {
        _current = this;
        _body = GetComponent<SpriteRenderer>();
        var hatGo = new GameObject("Hat");
        hatGo.transform.SetParent(transform, false);
        _hat = hatGo.AddComponent<SpriteRenderer>();
        _lastPos = transform.position;
        vc_LoopMeta.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        vc_LoopMeta.Changed -= Refresh;
        if (_current == this) _current = null;
        if (_catGo != null) Destroy(_catGo);
    }

    /// <summary>The outfit chosen at the stall. A school uniform only shows once that school is
    /// reached in this loop (a new loop is a new childhood).</summary>
    private static string DesiredOutfit()
    {
        string chosen = vc_LoopMeta.Equipped("outfit");
        if (vc_LoopMeta.IsStageUniform(chosen) && !vc_LoopMeta.UniformUnlocked(chosen)) return "";
        return chosen;
    }

    private void Refresh()
    {
        string outfit = DesiredOutfit();
        if (outfit != _outfit)
        {
            _outfit = outfit;
            _recolored.Clear();
            _def = OutfitDef.Load(outfit, vc_PlayerOptions.UseGirlAvatar);
        }
        _hatId = vc_LoopMeta.Equipped("hat");
        _trail = vc_LoopMeta.Equipped("trail");
        _pet = vc_LoopMeta.Equipped("pet");
    }

    private void LateUpdate()
    {
        if (_body == null) return;
        if (DesiredOutfit() != _outfit) Refresh();   // moved to a new school stage
        Sprite src = _body.sprite;
        if (src != null && !_ours.Contains(src))
        {
            // outfit: swap in the recoloured copy of the frame the animator just set
            if (_def != null)
            {
                if (!_recolored.TryGetValue(src, out Sprite rc))
                {
                    rc = Recolor(src, _def);
                    _recolored[src] = rc;
                    if (rc != null) _ours.Add(rc);
                }
                if (rc != null) _body.sprite = rc;
            }
            UpdateHat(src);
        }
        else if (src != null) UpdateHat(src);

        _hat.sortingLayerID = _body.sortingLayerID;
        _hat.sortingOrder = _body.sortingOrder + 1;
        _hat.flipX = false;

        UpdateTrail();
        UpdatePet();
    }

    // ── outfit: palette swap below the collar ─────────────────────────────

    [System.Serializable]
    private class OutfitJson
    {
        public string id;
        public OutfitSide boy, girl;
    }

    [System.Serializable]
    private class OutfitSide
    {
        public string[] from;   // "#rrggbb" source colours
        public string[] to;     // replacements (same order)
        public string[] lower;  // which of `from` are lower-body colours (trousers / skirt)
    }

    private class OutfitDef
    {
        public readonly Dictionary<Color32, Color32> map = new Dictionary<Color32, Color32>();
        public readonly HashSet<Color32> lower = new HashSet<Color32>();
        public readonly HashSet<Color32> shirt = new HashSet<Color32>();

        public static OutfitDef Load(string id, bool girl)
        {
            if (string.IsNullOrEmpty(id)) return null;
            TextAsset ta = Resources.Load<TextAsset>("Cosmetics/outfits");
            if (ta == null) return null;
            OutfitList list = JsonUtility.FromJson<OutfitList>(ta.text);
            if (list?.outfits == null) return null;
            foreach (OutfitJson o in list.outfits)
            {
                if (o.id != id) continue;
                OutfitSide side = girl ? o.girl : o.boy;
                if (side == null || side.from == null || side.to == null) return null;
                var def = new OutfitDef();
                var lowerSet = new HashSet<string>(side.lower ?? new string[0]);
                for (int i = 0; i < side.from.Length && i < side.to.Length; i++)
                {
                    Color32 a = Hex(side.from[i]), b = Hex(side.to[i]);
                    def.map[a] = b;
                    if (lowerSet.Contains(side.from[i])) def.lower.Add(a); else def.shirt.Add(a);
                }
                return def;
            }
            return null;
        }
    }

    [System.Serializable]
    private class OutfitList { public OutfitJson[] outfits; }

    private static Color32 Hex(string h)
    {
        ColorUtility.TryParseHtmlString(h, out Color c);
        Color32 c32 = c;
        c32.a = 255;
        return c32;
    }

    private static Color32[] ReadPixels(Sprite s, out int w, out int h)
    {
        Rect r = s.textureRect;
        w = (int)r.width; h = (int)r.height;
        Texture2D tex = s.texture;
        // the frames aren't import-readable: copy through a render texture
        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] px = copy.GetPixels32();
        Destroy(copy);
        return px;
    }

    private static Sprite Recolor(Sprite src, OutfitDef def)
    {
        Color32[] px = ReadPixels(src, out int w, out int h);
        // rows run bottom -> top in Unity; find the collar (topmost shirt row) and the waist
        // (lowest shirt row): shirt colours change below the collar, trousers below the waist
        // A shirt row has a run of 4+ shirt pixels; the eyes share the shirt's white but are
        // only 1-2 pixels wide, so they never count.
        int collar = -1, waist = h;
        for (int y = h - 1; y >= 0; y--)
        {
            int run = 0, best = 0;
            for (int x = 0; x < w; x++)
            {
                Color32 c = px[y * w + x];
                run = c.a > 0 && def.shirt.Contains(Opaque(c)) ? run + 1 : 0;
                best = Mathf.Max(best, run);
            }
            if (best < 4) continue;
            if (collar < 0) collar = y;
            waist = Mathf.Min(waist, y);
        }
        if (collar < 0) return null;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color32 c = px[i];
                if (c.a == 0) continue;
                Color32 k = Opaque(c);
                if (!def.map.TryGetValue(k, out Color32 to)) continue;
                bool lower = def.lower.Contains(k);
                if (lower ? y > waist + 1 : y > collar) continue;
                to.a = c.a;
                px[i] = to;
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        Vector2 pivot = new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height);
        Sprite rc = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, src.pixelsPerUnit);
        rc.name = src.name + "_outfit";
        return rc;
    }

    private static Color32 Opaque(Color32 c) { c.a = 255; return c; }

    // ── hat: overlay on the head top ──────────────────────────────────────

    private void UpdateHat(Sprite frame)
    {
        if (string.IsNullOrEmpty(_hatId) || frame == null) { _hat.sprite = null; return; }
        string facing = FacingOf(frame.name);
        string key = _hatId + "_" + facing;
        if (!_hatSprites.TryGetValue(key, out Sprite hs))
        {
            Texture2D t = Resources.Load<Texture2D>("Cosmetics/" + key) ?? Resources.Load<Texture2D>("Cosmetics/" + _hatId + "_down");
            hs = t != null ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0f), Ppu) : null;
            if (t != null) t.filterMode = FilterMode.Point;
            _hatSprites[key] = hs;
        }
        _hat.sprite = hs;
        if (hs == null) return;

        Vector2Int top = HeadTop(frame);
        // hat's bottom-centre sits a few pixels below the top of the hair
        float px = top.x - frame.pivot.x, py = top.y - frame.pivot.y - HatSink;
        _hat.transform.localPosition = new Vector3(Mathf.Round(px) / Ppu, Mathf.Round(py) / Ppu, -0.001f);
    }

    private const int HatSink = 5;

    private Vector2Int HeadTop(Sprite frame)
    {
        Sprite key = frame;
        if (_headTop.TryGetValue(key, out Vector2Int v)) return v;
        Color32[] px = ReadPixels(frame, out int w, out int h);
        int top = -1, minX = w, maxX = -1;
        for (int y = h - 1; y >= 0 && top < 0; y--)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0) { top = y; break; }
        if (top < 0) top = h - 1;
        // centre of the head: widest opaque span in the 6 rows under the top
        for (int y = top; y >= Mathf.Max(0, top - 6); y--)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 0) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
        v = new Vector2Int(maxX >= 0 ? (minX + maxX + 1) / 2 : w / 2, top + 1);
        _headTop[key] = v;
        return v;
    }

    private static string FacingOf(string name)
    {
        name = name ?? "";
        if (name.Contains("_up_")) return "up";
        if (name.Contains("_left_")) return "left";
        if (name.Contains("_right_")) return "right";
        return "down";
    }

    // ── trail (+ Old Sneakers Lv2 dust) ────────────────────────────────────

    private void UpdateTrail()
    {
        Vector3 pos = transform.position;
        float moved = Vector2.Distance(pos, _lastPos);
        _lastPos = pos;
        bool dust = vc_LoopMeta.Level("sneakers") == 2;
        if ((string.IsNullOrEmpty(_trail) && !dust) || moved < 0.001f || Time.deltaTime <= 0f) return;
        _trailClock += moved;
        if (_trailClock < 0.45f) return;
        _trailClock = 0f;
        if (dust) Puff("dust", pos, 0.45f);
        if (!string.IsNullOrEmpty(_trail)) Puff(_trail, pos, 0.9f);
    }

    private void Puff(string id, Vector3 at, float life)
    {
        Texture2D t = Resources.Load<Texture2D>("Cosmetics/" + id + "_" + Random.Range(0, 3));
        if (t == null) t = Resources.Load<Texture2D>("Cosmetics/" + id + "_0");
        if (t == null) return;
        t.filterMode = FilterMode.Point;
        var go = new GameObject("Trail");
        go.transform.position = at + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.02f, 0.12f), 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 16f);
        sr.sortingLayerID = _body.sortingLayerID;
        sr.sortingOrder = _body.sortingOrder - 1;
        StartCoroutine(FadePuff(sr, life, id == "trail_bubbles" || id == "trail_sparkles" ? 0.35f : id == "dust" ? 0.1f : -0.05f));
    }

    private static IEnumerator FadePuff(SpriteRenderer sr, float life, float rise)
    {
        float t = 0f;
        Vector3 start = sr.transform.position;
        while (t < life && sr != null)
        {
            t += Time.deltaTime;
            float k = t / life;
            sr.transform.position = start + new Vector3(0f, Mathf.Round(rise * k * 16f) / 16f, 0f);
            Color c = sr.color; c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f; sr.color = c;
            yield return null;
        }
        if (sr != null) Destroy(sr.gameObject);
    }

    // ── pet: Muning follows you around town ───────────────────────────────

    private GameObject _catGo;
    private SpriteRenderer _cat;

    private void UpdatePet()
    {
        bool want = _pet == "pet_cat" && vc_TownStage.Current != null && !vc_TownStage.Current.InQuest;
        if (!want) { if (_catGo != null) _catGo.SetActive(false); return; }
        if (_catGo == null)
        {
            _catGo = new GameObject("Muning (follows you)");
            _cat = _catGo.AddComponent<SpriteRenderer>();
            _catGo.transform.position = transform.position + new Vector3(-0.8f, -0.2f, 0f);
        }
        _catGo.SetActive(true);
        Vector3 target = transform.position + new Vector3(-0.7f, -0.25f, 0f);
        Vector3 p = _catGo.transform.position;
        float d = Vector2.Distance(p, target);
        bool walking = d > 0.35f;
        if (d > 6f) p = target;                                       // teleports (journal Go, new day)
        else if (walking) p = Vector3.MoveTowards(p, target, Time.deltaTime * Mathf.Lerp(2.5f, 6f, d / 3f));
        _catGo.transform.position = new Vector3(Mathf.Round(p.x * 16f) / 16f, Mathf.Round(p.y * 16f) / 16f, p.z);
        Texture2D t = Resources.Load<Texture2D>(walking ? "Morning/cat_walk" : "Morning/cat_sit");
        if (t != null)
        {
            t.filterMode = FilterMode.Point;
            if (_cat.sprite == null || _cat.sprite.texture != t)
                _cat.sprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0f), 16f);
        }
        if (walking) _cat.flipX = target.x < p.x;
        _cat.sortingLayerID = _body.sortingLayerID;
        _cat.sortingOrder = _body.sortingOrder + (p.y < transform.position.y ? 1 : -1);
    }

    // ── the equip spin ────────────────────────────────────────────────────

    private IEnumerator Spin()
    {
        if (_spinning) yield break;
        _spinning = true;
        Vector3 baseScale = transform.localScale;
        float sx = Mathf.Abs(baseScale.x) > 0f ? baseScale.x : 1f;
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.6f;
            // two turns: the sprite narrows to a sliver and flips, like turning on the spot
            float c = Mathf.Cos(k * Mathf.PI * 4f);
            transform.localScale = new Vector3(sx * (Mathf.Abs(c) < 0.15f ? 0.15f * Mathf.Sign(c == 0 ? 1 : c) : c), baseScale.y * (1f + 0.08f * Mathf.Sin(k * Mathf.PI)), baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;
        for (int i = 0; i < 8; i++) Puff("sparkle", transform.position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.2f, 1.2f), 0f), 0.5f);
        _spinning = false;
    }
}
