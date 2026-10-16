using System.Collections;
using UnityEngine;

/// <summary>
/// I quest (I3). Run a plant study on the seedlings. Any APPROPRIATE skill works - how well it
/// fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   plan     I  best fit     set up a sun-vs-shade test; the pots grow in time-lapse         ~2s
///   survey   I  workaround   measure one pot per press with a ruler and log it               x3
///   unlock   I  workaround   work out why the shaded pot wilts, then lift the shade           5s
///   collect  C  off-letter   gather each pot's data onto the chart                            8s
///   walking over to the sunny bed and noticing the tall plants still works (existing)
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (plan) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_SeedlingStudyQuest : vc_FitQuestBase
{
    [Header("Pots (A = sun, B = normal, C = shade)")]
    [SerializeField] private SpriteRenderer[] pots;
    [Tooltip("pot_0..pot_3 (sprout to tall), then pot_wilt.")]
    [SerializeField] private Sprite[] potStages;

    [Header("Props (inactive until used)")]
    [SerializeField] private Transform sunSign;
    [SerializeField] private Transform awning;
    [SerializeField] private SpriteRenderer beam;
    [SerializeField] private Transform ruler;
    [SerializeField] private Transform notes;
    [SerializeField] private Transform chart;
    [SerializeField] private Transform[] chartBars;

    [Header("Walk-and-notice (no skill)")]
    [SerializeField] private Transform sunnyBed;
    [SerializeField] private float noticeRange = 1.3f;
    [SerializeField] private float noticeSeconds = 1f;

    [Header("Effects")]
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] bulbFrames;
    [SerializeField] private Sprite[] questionFrames;
    [SerializeField] private Sprite[] tapRingFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite leafToken;
    [SerializeField] private Sprite magnifier;

    [Header("Timing (seconds)")]
    [SerializeField] private float planTime = 2f;
    [SerializeField] private int measurePresses = 3;
    [SerializeField] private float solveTime = 5f;
    [SerializeField] private float collectTime = 8f;

    private const int Wilt = 4;
    private int _measured;
    private Vector3 _barPos;
    private float _sparkClock, _noticeClock;
    private bool _noticing;
    private Transform _player;
    private Vector3[] _potRest;
    private Vector3 _awningRest;
    private GameObject _lens;

    protected override Vector3 BarWorldPosition => _barPos;

    private void Awake()
    {
        _potRest = new Vector3[pots.Length];
        for (int i = 0; i < pots.Length; i++) _potRest[i] = pots[i].transform.localPosition;
        if (awning != null) _awningRest = awning.localPosition;
    }

    protected override void OnBegin()
    {
        _measured = 0;
        _noticing = false;
        _noticeClock = 0f;
        for (int i = 0; i < pots.Length; i++)
        {
            pots[i].transform.localPosition = _potRest[i];
            SetStage(i, 0);
        }
        foreach (Transform t in new[] { sunSign, awning, ruler, notes, chart })
            if (t != null) { t.gameObject.SetActive(false); t.localScale = Vector3.one; }
        if (awning != null) awning.localPosition = _awningRest;
        if (beam != null) beam.gameObject.SetActive(false);
        if (chartBars != null) foreach (Transform b in chartBars) if (b != null) b.localScale = new Vector3(1f, 0f, 1f);
        _barPos = Center() + new Vector3(-0.5f, 1.7f, 0f);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Seedling Study",
            "Find out what makes the seedlings grow.",
            new[] { "Study the seedlings" });
    }

    protected override void OnWorkCancelled()
    {
        if (_lens != null) Destroy(_lens);
        _lens = null;
    }

    private Vector3 Center() => pots[pots.Length / 2].transform.position;

    private void SetStage(int pot, int stage)
    {
        if (potStages != null && stage < potStages.Length) pots[pot].sprite = potStages[stage];
    }

    // ── Idle life + walk-and-notice ──────────────────────────────────────

    private void Update()
    {
        if (!Started || Done) return;

        _sparkClock -= Time.deltaTime;
        if (_sparkClock <= 0f && !Working)
        {
            _sparkClock = Random.Range(2f, 3.5f);
            int i = Random.Range(0, pots.Length);
            Fx(sparkFrames, pots[i].transform.position + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.5f, 1f), 0f), 10f, 0.15f);
            StartCoroutine(Hop(pots[i].transform, _potRest[i], 1));
        }

        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }
        if (_player == null || sunnyBed == null || Working) { StopNoticing(); return; }

        if (Vector2.Distance(_player.position, sunnyBed.position) <= noticeRange)
        {
            if (!_noticing)
            {
                _noticing = true;
                _noticeClock = 0f;
                _barPos = sunnyBed.position + new Vector3(-0.5f, 1.5f, 0f);
                ShowBar(true);
                Toast("These sunny pots look much taller...", noticeSeconds + 0.5f);
            }
            _noticeClock += Time.deltaTime;
            SetBar(_noticeClock / noticeSeconds);
            if (_noticeClock >= noticeSeconds)
            {
                _noticing = false;
                Finish("The sunny pots grew the tallest. Plants love the sun!");
            }
        }
        else StopNoticing();
    }

    private void StopNoticing()
    {
        if (!_noticing) return;
        _noticing = false;
        ShowBar(false);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("plan"))
        {
            StopNoticing();
            _barPos = Center() + new Vector3(-0.5f, 1.7f, 0f);
            StartWork(PlanRoutine());
            return;
        }

        if (BlockedByWork()) return;

        if (d.HasTag("survey")) { StopNoticing(); MeasurePress(); return; }
        if (d.HasTag("unlock")) { StopNoticing(); _barPos = Center() + new Vector3(-0.5f, 1.7f, 0f); StartWork(SolveRoutine()); return; }
        if (d.HasTag("collect")) { StopNoticing(); _barPos = Center() + new Vector3(-0.5f, 1.7f, 0f); StartWork(CollectRoutine()); return; }

        Toast("This needs some careful study.", 2.2f);
    }

    // ── Plan: sun-vs-shade test, time-lapse ──────────────────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("Setting up a sun-and-shade test...", planTime);
        ShowBar(true);
        PlaceSetup(true);
        SunodGame.Core.vc_Sfx.Play("clack", 0.6f);

        float t = 0f;
        bool s1 = false, s2 = false, s3 = false;
        while (t < planTime)
        {
            t += Time.deltaTime;
            SetBar(t / planTime);
            if (!s1 && t >= planTime * 0.2f) { s1 = true; Grow(0, 1); Grow(1, 1); Grow(2, 1); }
            if (!s2 && t >= planTime * 0.45f) { s2 = true; Grow(0, 2); Grow(1, 2); }
            if (!s3 && t >= planTime * 0.7f) { s3 = true; Grow(0, 3); Grow(1, 2); Grow(2, Wilt); }
            yield return null;
        }
        Finish("Your test shows it: plants need sunlight!");
    }

    /// <summary>Sun sign + beam over pot A, shade awning over pot C.</summary>
    private void PlaceSetup(bool withBeam)
    {
        if (sunSign != null) { sunSign.gameObject.SetActive(true); StartCoroutine(PopScale(sunSign)); }
        if (awning != null) { awning.gameObject.SetActive(true); StartCoroutine(PopScale(awning)); }
        if (beam != null && withBeam) { beam.gameObject.SetActive(true); StartCoroutine(BeamPulse()); }
    }

    private void Grow(int pot, int stage)
    {
        SetStage(pot, stage);
        StartCoroutine(Hop(pots[pot].transform, _potRest[pot], 2));
        Fx(sparkFrames, pots[pot].transform.position + new Vector3(0.1f, 0.9f, 0f), 12f, 0.2f);
        SunodGame.Core.vc_Sfx.Play(stage == Wilt ? "quest_fail" : "grow", stage == Wilt ? 0.2f : 0.45f, 0.08f);
    }

    // ── Survey: measure one pot per press ────────────────────────────────

    private void MeasurePress()
    {
        int i = Mathf.Clamp(_measured, 0, pots.Length - 1);
        _measured++;
        int[] shown = { 3, 2, 1 };
        SetStage(i, shown[i % 3]);

        if (notes != null && !notes.gameObject.activeSelf) { notes.gameObject.SetActive(true); StartCoroutine(PopScale(notes)); }
        if (ruler != null)
        {
            ruler.gameObject.SetActive(true);
            ruler.position = Snap(pots[i].transform.position + new Vector3(0.75f, 0f, 0f));
            StartCoroutine(PopScale(ruler));
        }
        Fx(tapRingFrames, pots[i].transform.position + new Vector3(0.75f, 0.7f, 0f), 6f);
        Fx(checkFrames, (notes != null ? notes.position : Center()) + new Vector3(-0.4f + i * 0.4f, 0.8f, 0f), 2f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("page_next", 0.6f, 0.08f);

        if (_measured < measurePresses)
            Toast($"You measure a pot and log it ({_measured}/{measurePresses})", 2f);
        else
            Finish("Three pots measured. The sunny one grew the most!");
    }

    // ── Solve: why does the shaded pot wilt? ─────────────────────────────

    private IEnumerator SolveRoutine()
    {
        Toast("Working out why one pot is wilting...", solveTime);
        ShowBar(true);
        PlaceSetup(false);
        Grow(0, 3); Grow(1, 2); Grow(2, Wilt);

        Transform potC = pots[pots.Length - 1].transform;
        if (_lens != null) Destroy(_lens);
        _lens = new GameObject("Magnifier");
        _lens.transform.SetParent(transform, true);
        var lr = _lens.AddComponent<SpriteRenderer>();
        lr.sprite = magnifier; lr.sortingLayerName = sortingLayer; lr.sortingOrder = 22;

        bool q = false, lift = false, bulb = false, perk = false;
        float t = 0f;
        while (t < solveTime)
        {
            t += Time.deltaTime;
            SetBar(t / solveTime);
            // magnifier sweeps over the wilted pot
            float sway = Mathf.Sin(t * 3f) * 0.25f;
            if (_lens != null) _lens.transform.position = Snap(potC.position + new Vector3(sway, 0.55f, 0f));
            if (!q && t >= 1.4f) { q = true; Fx(questionFrames, potC.position + new Vector3(0.2f, 1.4f, 0f), 2f, 0.3f); SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f); }
            if (!lift && t >= 2.8f)
            {
                lift = true;
                SunodGame.Core.vc_Sfx.Play("rustle", 0.5f);
                if (awning != null) StartCoroutine(Lift(awning));
                if (beam != null) { beam.transform.position = Snap(potC.position + new Vector3(0f, 0.3f, 0f)); beam.gameObject.SetActive(true); StartCoroutine(BeamPulse()); }
            }
            if (!bulb && t >= solveTime - 1.2f)
            {
                bulb = true;
                if (_lens != null) Destroy(_lens);
                _lens = null;
                Fx(bulbFrames, potC.position + new Vector3(0f, 1.6f, 0f), 2f, 0.3f, 25);
                SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
            }
            if (!perk && t >= solveTime - 0.6f) { perk = true; Grow(2, 2); }
            yield return null;
        }
        if (_lens != null) Destroy(_lens);
        _lens = null;
        Finish("Too little light! Back in the sun, it perks right up.");
    }

    private IEnumerator Lift(Transform tr)
    {
        Vector3 from = tr.position;
        for (int i = 1; i <= 8 && tr != null; i++)
        {
            tr.position = Snap(from + new Vector3(0f, i * 0.12f, 0f));
            yield return new WaitForSeconds(0.04f);
        }
        if (tr != null) tr.gameObject.SetActive(false);
    }

    // ── Collect: leaf data flies onto the chart ──────────────────────────

    private IEnumerator CollectRoutine()
    {
        Toast("Gathering each pot's results...", collectTime);
        ShowBar(true);
        Grow(0, 3); Grow(1, 2); Grow(2, 1);
        if (chart != null) { chart.gameObject.SetActive(true); StartCoroutine(PopScale(chart)); }
        SunodGame.Core.vc_Sfx.Play("page_next", 0.6f);

        float[] heights = { 0.75f, 0.5f, 0.2f };
        float per = collectTime / (pots.Length + 0.4f);
        float t = 0f;
        yield return BarStep(per * 0.4f, 0f, 0.4f / (pots.Length + 0.4f));
        t = per * 0.4f;
        for (int i = 0; i < pots.Length; i++)
        {
            Vector3 from = pots[i].transform.position + new Vector3(0f, 0.8f, 0f);
            Vector3 to = (chartBars != null && i < chartBars.Length ? chartBars[i].position : chart.position) + new Vector3(0f, 0.6f, 0f);
            GameObject tok = new GameObject("Token");
            tok.transform.SetParent(transform, true);
            var sr = tok.AddComponent<SpriteRenderer>();
            sr.sprite = leafToken; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 24;
            SunodGame.Core.vc_Sfx.Play("skill_pickup", 0.5f, 0.08f);

            float flight = per * 0.45f, f = 0f;
            while (f < flight)
            {
                f += Time.deltaTime;
                float k = Mathf.Clamp01(f / flight);
                tok.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f));
                SetBar((t + f) / collectTime);
                yield return null;
            }
            Destroy(tok);
            Fx(sparkFrames, to, 12f, 0.15f);
            SunodGame.Core.vc_Sfx.Play("chime", 0.4f);

            float grow = per * 0.55f, g = 0f;
            while (g < grow)
            {
                g += Time.deltaTime;
                if (chartBars != null && i < chartBars.Length)
                    chartBars[i].localScale = new Vector3(1f, Mathf.Round(Mathf.Clamp01(g / grow) * heights[i] * 12f) / 12f, 1f);
                SetBar((t + flight + g) / collectTime);
                yield return null;
            }
            t += per;
        }
        Finish("All the data is charted. Sun wins!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private IEnumerator BarStep(float seconds, float k0, float k1)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetBar(Mathf.Lerp(k0, k1, t / seconds));
            yield return null;
        }
    }

    private IEnumerator PopScale(Transform tr)
    {
        float[] s = { 0.4f, 0.7f, 1.2f, 1f };
        foreach (float k in s)
        {
            if (tr == null) yield break;
            tr.localScale = Vector3.one * k;
            yield return new WaitForSeconds(0.06f);
        }
    }

    private IEnumerator Hop(Transform tr, Vector3 restLocal, int px)
    {
        tr.localPosition = restLocal + new Vector3(0f, px * 2f / 22f, 0f);
        yield return new WaitForSeconds(0.08f);
        if (tr != null) tr.localPosition = restLocal + new Vector3(0f, px / 22f, 0f);
        yield return new WaitForSeconds(0.08f);
        if (tr != null) tr.localPosition = restLocal;
    }

    private IEnumerator BeamPulse()
    {
        float t = 0f;
        while (beam != null && beam.gameObject.activeSelf && !Done)
        {
            t += Time.deltaTime;
            Color c = beam.color; c.a = 0.75f + 0.25f * Mathf.Sin(t * 5f); beam.color = c;
            yield return null;
        }
    }
}
