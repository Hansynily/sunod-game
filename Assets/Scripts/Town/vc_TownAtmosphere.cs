using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Morning town details. The game is always morning, so this only adds things that belong to
/// a morning: chimneys puffing breakfast smoke over the houses. Stepped and pixel snapped like
/// the rest of the art; the puff sprite is generated here, nothing to import.
/// Added to the town root by vc_TownStage.
/// </summary>
[DisallowMultipleComponent]
public class vc_TownAtmosphere : MonoBehaviour
{
    private const float Px = 1f / 16f;
    private const string Layer = "Decor";

    private Camera _cam;
    private vc_TownStage _stage;
    private Sprite _puffSprite;

    private class Puff { public SpriteRenderer sr; public Vector3 pos; public float life, dur, drift; }
    private readonly List<Puff> _puffs = new List<Puff>();
    private readonly List<(Vector3 top, float next)> _chimneys = new List<(Vector3, float)>();

    private void Start()
    {
        _cam = Camera.main;
        _stage = GetComponent<vc_TownStage>();
        _puffSprite = Sprite.Create(PuffTex(), new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 16f);
        CollectChimneys();
    }

    private void LateUpdate()
    {
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
        TickChimneys(Time.deltaTime);
    }

    private void CollectChimneys()
    {
        Transform objects = transform.Find("Objects");
        if (objects == null) return;
        foreach (Transform o in objects)
        {
            string n = o.name.ToLowerInvariant();
            SpriteRenderer top = o.Find("Top") != null ? o.Find("Top").GetComponent<SpriteRenderer>() : null;
            if (top == null || top.sprite == null) continue;
            Bounds b = top.bounds;
            if (b.size.x <= 2.5f || !(n.Contains("house") || n.Contains("cabin") || n.Contains("hall"))) continue;
            // a chimney on the roof, a bit off-centre
            float side = (o.GetInstanceID() & 1) == 0 ? -0.28f : 0.28f;
            _chimneys.Add((new Vector3(b.center.x + b.extents.x * side, b.max.y - 0.2f, 0f), Time.time + Random.Range(0f, 3f)));
        }
    }

    private void TickChimneys(float dt)
    {
        bool show = _stage == null || !_stage.InQuest;
        for (int i = 0; i < _chimneys.Count; i++)
        {
            var (top, next) = _chimneys[i];
            if (!show || Time.time < next || !NearCamera(top, 4f)) continue;
            _chimneys[i] = (top, Time.time + Random.Range(0.8f, 1.5f));
            var p = new Puff { pos = top, life = 0f, dur = Random.Range(2.4f, 3.4f), drift = Random.Range(0.15f, 0.35f) };
            p.sr = new GameObject("Smoke").AddComponent<SpriteRenderer>();
            p.sr.transform.SetParent(transform, false);
            p.sr.sprite = _puffSprite;
            p.sr.sortingLayerName = Layer;
            p.sr.sortingOrder = 60;
            _puffs.Add(p);
        }
        for (int i = _puffs.Count - 1; i >= 0; i--)
        {
            Puff p = _puffs[i];
            p.life += dt;
            float k = p.life / p.dur;
            if (k >= 1f || !show) { Destroy(p.sr.gameObject); _puffs.RemoveAt(i); continue; }
            Vector3 pos = p.pos + new Vector3(p.drift * k * 2f + Mathf.Sin(k * 6f) * 0.08f, k * 1.6f, 0f);
            p.sr.transform.position = new Vector3(Mathf.Round(pos.x / Px) * Px, Mathf.Round(pos.y / Px) * Px, 0f);
            float sc = Mathf.Round((0.9f + k * 1.3f) * 4f) / 4f;   // grows in steps
            p.sr.transform.localScale = new Vector3(sc, sc, 1f);
            p.sr.color = new Color(0.86f, 0.86f, 0.9f, (1f - k) * 0.85f * (k < 0.1f ? k * 10f : 1f));
        }
    }

    private bool NearCamera(Vector3 p, float margin)
    {
        float hh = _cam.orthographicSize + margin, ww = _cam.orthographicSize * _cam.aspect + margin;
        Vector3 c = _cam.transform.position;
        return Mathf.Abs(p.x - c.x) < ww && Mathf.Abs(p.y - c.y) < hh;
    }

    private static Texture2D PuffTex()
    {
        var t = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        t.SetPixels32(new Color32[64]);
        string[] rows = { "..####..", ".######.", "########", "########", "########", "########", ".######.", "..####.." };
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            if (rows[y][x] == '#')
            {
                bool shade = y >= 5 || (x >= 6 && y >= 2);   // a soft shadow on the lower right
                t.SetPixel(x, 7 - y, shade ? new Color(0.78f, 0.78f, 0.84f) : Color.white);
            }
        t.Apply();
        return t;
    }
}
