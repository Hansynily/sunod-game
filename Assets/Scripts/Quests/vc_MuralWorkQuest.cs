using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A quest (A8). Fill the blank wall with a mural. Any APPROPRIATE skill works - how well it
/// fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   paint    A  best fit     the mural is painted in, layer by layer, brush sweeping      ~2s
///   craft    A  workaround   paper cut-outs fly onto the wall and are pasted down as a collage  4s
///   barrier  A  workaround   a raised clay relief is sculpted layer by layer               5s
///   plan     I  off-letter   draw a design grid and sketch first, then paint it in          8s
///   anything else: a hint, no effect
///
/// Each result looks different: bright paint, a paper collage, or a clay relief.
/// While a slow fix is running the best fit (paint) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_MuralWorkQuest : vc_FitQuestBase
{
    [Header("Wall")]
    [SerializeField] private Transform wall;
    [SerializeField] private SpriteRenderer blankWall;
    [Tooltip("5 layers each, bottom to top.")]
    [SerializeField] private Sprite[] paintLayers;
    [SerializeField] private Sprite[] paperLayers;
    [SerializeField] private Sprite[] reliefLayers;
    [SerializeField] private Sprite grid;
    [SerializeField] private Sprite sketch;

    [Header("Effects")]
    [SerializeField] private Sprite brush;
    [SerializeField] private Sprite[] splats;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] ringFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float paintTime = 2f;
    [SerializeField] private float craftTime = 4f;
    [SerializeField] private float reliefTime = 5f;
    [SerializeField] private float planTime = 8f;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GameObject _brushGo;
    private float _idleClock;

    private Vector3 WallCenter => wall != null ? wall.position : transform.position;
    protected override Vector3 BarWorldPosition => WallCenter + new Vector3(-0.5f, -1.35f, 0f);

    protected override void OnBegin()
    {
        ClearSpawned();
        _idleClock = 1.2f;
        if (blankWall != null) { blankWall.gameObject.SetActive(true); blankWall.sortingOrder = 1; }
        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "The Mural",
            "The wall is blank. Bring it to life.",
            new[] { "Fill the wall with a mural" });
    }

    protected override void OnWorkCancelled()
    {
        if (_brushGo != null) Destroy(_brushGo);
        _brushGo = null;
        // a half-done look is replaced by the takeover: clear what was laid so far
        ClearSpawned();
    }

    private void ClearSpawned()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
    }

    // ── Idle life: flaking plaster dust ──────────────────────────────────

    private void Update()
    {
        if (!Started || Done || Working) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock <= 0f)
        {
            _idleClock = Random.Range(2.2f, 4.0f);
            Fx(dustFrames, WallCenter + new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(-0.2f, 0.6f), 0f), 6f, -0.25f, 8);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("paint")) { StartWork(PaintRoutine(paintTime, 1.0f)); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("craft")) { StartWork(CraftRoutine()); return; }
        if (d.HasTag("barrier")) { StartWork(ReliefRoutine()); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        Toast("The wall needs an artist.", 2.2f);
    }

    // ── Paint: layers wash in while a brush sweeps ───────────────────────

    private IEnumerator PaintRoutine(float seconds, float strength)
    {
        Toast("You paint the wall in bold strokes...", seconds);
        ShowBar(true);
        yield return PaintLayers(seconds, 0f, seconds);
        Finish("You finished the mural!");
    }

    private IEnumerator PaintLayers(float seconds, float barStart, float barTotal)
    {
        int n = paintLayers.Length;
        float per = seconds / n;
        MakeBrush();
        for (int k = 0; k < n; k++)
        {
            SpriteRenderer sr = AddLayer(paintLayers[k], 2 + k, 0f);
            Vector3 from = WallCenter + new Vector3(-2.3f, Mathf.Lerp(0.7f, -0.6f, k / (float)(n - 1)), 0f);
            Vector3 to = from + new Vector3(4.6f, k % 2 == 0 ? -0.15f : 0.15f, 0f);
            SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.35f, 0.15f);
            if (splats != null && splats.Length > 0)
                Fx(new[] { splats[k % splats.Length] }, from + new Vector3(0.5f, 0.1f, 0f), 6f, 0f, 21);
            float t = 0f;
            while (t < per)
            {
                t += Time.deltaTime;
                float f = Mathf.Clamp01(t / per);
                SetAlpha(sr, f);
                if (_brushGo != null) _brushGo.transform.position = Snap(Vector3.Lerp(from, to, f) + new Vector3(0f, Mathf.Sin(f * 20f) * 0.04f, 0f));
                SetBar((barStart + k * per + t) / barTotal);
                yield return null;
            }
            SetAlpha(sr, 1f);
        }
        if (_brushGo != null) Destroy(_brushGo);
        _brushGo = null;
        Fx(sparkFrames, WallCenter + new Vector3(1.8f, 0.5f, 0f), 12f, 0.2f);
        Fx(sparkFrames, WallCenter + new Vector3(-1.6f, 0.1f, 0f), 12f, 0.2f);
    }

    // ── Craft: paper pieces fly in and are pasted down ───────────────────

    private IEnumerator CraftRoutine()
    {
        Toast("You cut and paste a paper collage...", craftTime);
        ShowBar(true);
        int n = paperLayers.Length;
        float per = craftTime / n;
        Vector3[] starts = { new Vector3(0f, -3.2f, 0f), new Vector3(-3.4f, -2.2f, 0f), new Vector3(0f, 2.6f, 0f), new Vector3(3.2f, -2.4f, 0f), new Vector3(-3f, 1.8f, 0f) };
        for (int k = 0; k < n; k++)
        {
            SpriteRenderer sr = AddLayer(paperLayers[k], 2 + k, 1f);
            Vector3 home = sr.transform.position, from = home + starts[k % starts.Length];
            float t = 0f, fly = per * 0.7f;
            while (t < fly)
            {
                t += Time.deltaTime;
                float f = Mathf.SmoothStep(0f, 1f, t / fly);
                sr.transform.position = Snap(Vector3.Lerp(from, home, f) + new Vector3(0f, Mathf.Sin(f * Mathf.PI) * 0.4f, 0f));
                SetBar((k * per + t) / craftTime);
                yield return null;
            }
            sr.transform.position = home;
            SunodGame.Core.vc_Sfx.Play("clack", 0.5f, 0.12f);
            Fx(dustFrames, home + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.5f, 0.5f), 0f), 10f, 0.15f);
            Fx(sparkFrames, home + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.5f, 0.5f), 0f), 12f, 0.1f);
            float rest = per - fly, r = 0f;
            while (r < rest) { r += Time.deltaTime; SetBar((k * per + fly + r) / craftTime); yield return null; }
        }
        Finish("Your paper collage is stuck on the wall. Lovely!");
    }

    // ── Barrier: sculpt a raised relief ──────────────────────────────────

    private IEnumerator ReliefRoutine()
    {
        Toast("You sculpt a raised relief on the wall...", reliefTime);
        ShowBar(true);
        int n = reliefLayers.Length;
        float per = reliefTime / n;
        for (int k = 0; k < n; k++)
        {
            SpriteRenderer sr = AddLayer(reliefLayers[k], 2 + k, 0f);
            Vector3 home = sr.transform.position;
            SunodGame.Core.vc_Sfx.Play("scrape", 0.4f, 0.1f);
            float t = 0f;
            while (t < per)
            {
                t += Time.deltaTime;
                float f = Mathf.Clamp01(t / per);
                SetAlpha(sr, f);
                sr.transform.position = Snap(home + new Vector3(0f, (1f - f) * -0.16f, 0f));
                if (((int)(t * 8f)) % 2 == 0 && t < per - 0.1f && Time.frameCount % 7 == 0)
                    Fx(dustFrames, home + new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(-0.6f, 0.6f), 0f), 10f, 0.15f);
                SetBar((k * per + t) / reliefTime);
                yield return null;
            }
            sr.transform.position = home;
            SetAlpha(sr, 1f);
        }
        Finish("You sculpted a raised relief. It looks amazing!");
    }

    // ── Plan: draw a grid and sketch, then paint ─────────────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("You sketch a design on a grid first...", planTime);
        ShowBar(true);
        float gridT = 1.3f, sketchT = 2.4f, paintT = planTime - gridT - sketchT;

        SpriteRenderer g = AddLayer(grid, 12, 0f);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f);
        for (int i = 0; i < 4; i++) Fx(ringFrames, WallCenter + new Vector3(-2f + i * 1.3f, 0.3f, 0f), 6f);
        float t = 0f;
        while (t < gridT) { t += Time.deltaTime; SetAlpha(g, t / gridT); SetBar(t / planTime); yield return null; }

        SpriteRenderer s = AddLayer(sketch, 13, 0f);
        MakeBrush();
        t = 0f;
        Vector3 a = WallCenter + new Vector3(-2.4f, 0.4f, 0f), b = WallCenter + new Vector3(2.4f, -0.4f, 0f);
        while (t < sketchT)
        {
            t += Time.deltaTime;
            float f = t / sketchT;
            SetAlpha(s, f);
            if (_brushGo != null) _brushGo.transform.position = Snap(Vector3.Lerp(a, b, f) + new Vector3(0f, Mathf.Sin(f * 14f) * 0.15f, 0f));
            if (Time.frameCount % 12 == 0) SunodGame.Core.vc_Sfx.Play("text_blip", 0.25f, 0.2f);
            SetBar((gridT + t) / planTime);
            yield return null;
        }
        if (_brushGo != null) Destroy(_brushGo);
        _brushGo = null;

        yield return PaintLayers(paintT, gridT + sketchT, planTime);
        // the guide lines fade away under the paint
        t = 0f;
        while (t < 0.3f) { t += Time.deltaTime; SetAlpha(g, 1f - t / 0.3f); SetAlpha(s, 1f - t / 0.3f); yield return null; }
        Finish("Your careful design came out perfectly!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private SpriteRenderer AddLayer(Sprite sprite, int order, float alpha)
    {
        var go = new GameObject("MuralLayer");
        go.transform.SetParent(transform, true);
        go.transform.position = WallCenter;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.sortingLayerName = sortingLayer; sr.sortingOrder = order;
        SetAlpha(sr, alpha);
        _spawned.Add(go);
        return sr;
    }

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c;
    }

    private void MakeBrush()
    {
        if (_brushGo != null) Destroy(_brushGo);
        _brushGo = new GameObject("Brush");
        _brushGo.transform.SetParent(transform, true);
        var sr = _brushGo.AddComponent<SpriteRenderer>();
        sr.sprite = brush; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 22;
    }
}
