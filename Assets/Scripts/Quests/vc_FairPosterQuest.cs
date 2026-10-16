using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A quest (A3). Make the poster for the fair. Any APPROPRIATE skill works - how well it fits
/// decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   paint    A  best fit     a few bold brush strokes paint the whole poster          ~1.5s
///   craft    A  workaround   collage pieces fly onto the board and stick              3s
///   barrier  A  workaround   cut letter stencils for "FAIR", then fill them in         5s
///   anything else: a hint, no effect (there is no sensible off-letter path here)
///
/// While a slow fix is running the best fit (paint) takes over at once.
/// Poster geometry: the board sprite is 52x56 px (PPU 22); layout constants below are board pixels.
/// </summary>
[DisallowMultipleComponent]
public class vc_FairPosterQuest : vc_FitQuestBase
{
    private const float Ppu = 22f;
    private const int BoardW = 52, BoardH = 56;

    [Header("Poster")]
    [SerializeField] private SpriteRenderer poster;
    [SerializeField] private Sprite posterSketch;
    [Tooltip("Progressively painted: 3 partial stages, then the finished poster.")]
    [SerializeField] private Sprite[] paintStages;
    [SerializeField] private Sprite stencilBase;
    [SerializeField] private Transform pile;

    [Header("Collage (craft path) - order: bunting, sun, ticket, wheel, tent, hill")]
    [SerializeField] private Sprite[] pieces;

    [Header("Stencil (barrier path) - F, A, I, R")]
    [SerializeField] private Sprite[] letters;
    [SerializeField] private Sprite[] scissors;

    [Header("Effects")]
    [SerializeField] private Sprite brush;
    [SerializeField] private Sprite[] dabs;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] checkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float paintTime = 1.5f;
    [SerializeField] private float craftTime = 3f;
    [SerializeField] private float stencilTime = 5f;

    // board pixel centres (x from left, y from top)
    private static readonly Vector2[] PiecePx =
    {
        new Vector2(26f, 8f),   // bunting
        new Vector2(11f, 20f),  // sun
        new Vector2(40f, 18f),  // ticket
        new Vector2(32f, 34f),  // wheel
        new Vector2(12f, 38f),  // tent
        new Vector2(26f, 48f),  // hill
    };
    private static readonly float[] LetterX = { 9f, 20f, 31f, 42f };
    private const float LetterY = 12f;
    private static readonly int[] StrokeY = { 8, 21, 35, 48 };

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Coroutine> _subs = new List<Coroutine>();
    private float _sparkClock;

    protected override Vector3 BarWorldPosition => (poster != null ? poster.transform.position : transform.position) + new Vector3(-0.5f, 1.75f, 0f);

    private Vector3 PX(float x, float y) =>
        poster.transform.position + new Vector3((x - BoardW * 0.5f) / Ppu, (BoardH * 0.5f - y) / Ppu, 0f);

    protected override void OnBegin()
    {
        ClearSpawned();
        if (poster != null && posterSketch != null) poster.sprite = posterSketch;
        _sparkClock = 1f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Poster for the Fair",
            "The fair is almost here and the poster board is bare. Make it eye-catching.",
            new[] { "Make the fair poster" });
    }

    private void Update()
    {
        if (!Started || Done || Working || poster == null) return;
        _sparkClock -= Time.deltaTime;
        if (_sparkClock <= 0f)
        {
            _sparkClock = Random.Range(2.2f, 3.6f);
            Fx(sparkFrames, PX(Random.Range(8f, 44f), Random.Range(10f, 48f)), 4f, 0.1f);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("paint")) { StartWork(PaintRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("craft")) { StartWork(CollageRoutine()); return; }
        if (d.HasTag("barrier")) { StartWork(StencilRoutine()); return; }

        Toast("This needs an artist's touch.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        foreach (Coroutine c in _subs) if (c != null) StopCoroutine(c);
        _subs.Clear();
        ClearSpawned();
        if (!Done && poster != null && posterSketch != null) poster.sprite = posterSketch;
    }

    private void ClearSpawned()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
    }

    // ── Paint: four bold strokes ─────────────────────────────────────────

    private IEnumerator PaintRoutine()
    {
        Toast("Painting bold strokes...", paintTime);
        ShowBar(true);
        GameObject brushGo = MakeSprite("Brush", brush, 22);
        float per = paintTime / 4f;

        for (int s = 0; s < 4; s++)
        {
            SunodGame.Core.vc_Sfx.Play("brush", 0.6f, 0.08f);
            bool leftToRight = s % 2 == 0;
            float t = 0f, dabClock = 0f;
            while (t < per)
            {
                t += Time.deltaTime;
                dabClock += Time.deltaTime;
                float k = Mathf.Clamp01(t / per);
                float x = Mathf.Lerp(leftToRight ? 6f : 46f, leftToRight ? 46f : 6f, k);
                float y = StrokeY[s] + Mathf.Sin(k * Mathf.PI * 3f) * 2.5f;
                brushGo.transform.position = Snap(PX(x, y) + new Vector3(0.25f, 0.25f, 0f));
                if (dabClock >= 0.07f && dabs != null && dabs.Length > 0)
                {
                    dabClock = 0f;
                    Fx(new[] { dabs[Random.Range(0, dabs.Length)] }, PX(x, y), 5f, 0.1f, 21);
                }
                SetBar((s + k) / 4f);
                yield return null;
            }
            if (paintStages != null && s < paintStages.Length) poster.sprite = paintStages[s];
        }
        Destroy(brushGo);
        for (int i = 0; i < 4; i++) Fx(sparkFrames, PX(8f + i * 12f, 10f + (i % 2) * 28f), 6f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.7f);
        yield return new WaitForSeconds(0.15f);
        Finish("The poster looks amazing!");
    }

    // ── Craft: collage pieces fly in from the scrap pile ─────────────────

    private IEnumerator CollageRoutine()
    {
        Toast("Gluing a bold collage together...", craftTime);
        ShowBar(true);
        int n = PiecePx.Length;
        float gap = (craftTime - 0.6f) / Mathf.Max(1, n - 1);
        Vector3 from = pile != null ? pile.position + new Vector3(0f, 0.4f, 0f) : PX(26f, 70f);

        for (int i = 0; i < n; i++)
        {
            GameObject go = MakeSprite("Piece", pieces != null && i < pieces.Length ? pieces[i] : null, 6 + i);
            _subs.Add(StartCoroutine(Fly(go.transform, from, PX(PiecePx[i].x, PiecePx[i].y), 0.55f)));
            SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.4f, 0.12f);
            float t = 0f;
            while (t < gap) { t += Time.deltaTime; SetBar((i * gap + t) / craftTime); yield return null; }
        }
        yield return new WaitForSeconds(0.6f);
        for (int i = 0; i < n; i += 2) Fx(sparkFrames, PX(PiecePx[i].x, PiecePx[i].y), 6f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.7f);
        Finish("You crafted a bold collage!");
    }

    private IEnumerator Fly(Transform tr, Vector3 from, Vector3 to, float seconds)
    {
        float t = 0f;
        while (t < seconds && tr != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            Vector3 p = Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.9f, 0f);
            tr.position = Snap(p);
            tr.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, k);
            yield return null;
        }
        if (tr == null) yield break;
        tr.position = Snap(to);
        tr.localScale = Vector3.one * 1.12f;
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.1f);
        yield return new WaitForSeconds(0.07f);
        if (tr != null) tr.localScale = Vector3.one;
    }

    // ── Barrier: cut letter stencils, then fill them in ──────────────────

    private IEnumerator StencilRoutine()
    {
        Toast("Cutting letter stencils...", stencilTime * 0.6f);
        ShowBar(true);
        if (stencilBase != null) poster.sprite = stencilBase;

        int n = 4;
        float cut = 0.7f, fill = 0.45f;
        float total = n * (cut + fill) + 0.3f, elapsed = 0f;
        var go = new GameObject[n];
        Color[] fills = { new Color(0.9f, 0.31f, 0.27f), new Color(1f, 0.84f, 0.31f), new Color(0.36f, 0.55f, 0.92f), new Color(0.4f, 0.75f, 0.36f) };

        GameObject sc = MakeSprite("Scissors", scissors != null && scissors.Length > 0 ? scissors[0] : null, 22);
        for (int i = 0; i < n; i++)
        {
            // cut: scissors snip around the slot, then the paper letter drops in
            Vector3 c = PX(LetterX[i], LetterY);
            float t = 0f; int frame = 0; float snap = 0f;
            SunodGame.Core.vc_Sfx.Play("snip", 0.6f, 0.08f);
            while (t < cut)
            {
                t += Time.deltaTime; snap += Time.deltaTime;
                float a = t / cut * Mathf.PI * 2f;
                sc.transform.position = Snap(c + new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.4f, 0f));
                if (snap >= 0.12f && scissors != null && scissors.Length > 1)
                {
                    snap = 0f; frame ^= 1;
                    sc.GetComponent<SpriteRenderer>().sprite = scissors[frame];
                }
                SetBar((elapsed + t) / total);
                yield return null;
            }
            elapsed += cut;
            go[i] = MakeSprite("Letter", letters != null && i < letters.Length ? letters[i] : null, 7);
            go[i].transform.position = Snap(c);
            go[i].GetComponent<SpriteRenderer>().color = new Color(0.93f, 0.86f, 0.7f);
            StartCoroutine(PopScale(go[i].transform));
            Fx(sparkFrames, c, 6f, 0.15f);
        }
        Destroy(sc);

        // fill: a brush sweeps each letter and it takes its colour
        GameObject br = MakeSprite("Brush", brush, 22);
        for (int i = 0; i < n; i++)
        {
            Vector3 c = PX(LetterX[i], LetterY);
            SunodGame.Core.vc_Sfx.Play("brush", 0.5f, 0.08f);
            float t = 0f;
            while (t < fill)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fill);
                br.transform.position = Snap(c + new Vector3(Mathf.Lerp(-0.3f, 0.3f, k), Mathf.Sin(k * Mathf.PI * 2f) * 0.15f + 0.25f, 0f));
                if (go[i] != null) go[i].GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.93f, 0.86f, 0.7f), fills[i], k);
                SetBar((elapsed + t) / total);
                yield return null;
            }
            elapsed += fill;
            if (dabs != null && dabs.Length > 0) Fx(new[] { dabs[i % dabs.Length] }, c, 5f, 0.15f, 21);
        }
        Destroy(br);
        for (int i = 0; i < n; i++) Fx(sparkFrames, PX(LetterX[i], LetterY - 6f), 6f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.7f);
        yield return new WaitForSeconds(0.3f);
        Finish("You cut bold stencils and filled them in!");
    }

    private IEnumerator PopScale(Transform tr)
    {
        float[] s = { 0.5f, 0.8f, 1.2f, 1f };
        foreach (float k in s)
        {
            if (tr == null) yield break;
            tr.localScale = Vector3.one * k;
            yield return new WaitForSeconds(0.05f);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private GameObject MakeSprite(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = order;
        _spawned.Add(go);
        return go;
    }
}
