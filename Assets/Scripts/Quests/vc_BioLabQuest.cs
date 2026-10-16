using System.Collections;
using UnityEngine;

/// <summary>
/// I quest (I7). Test the three lab samples. Any APPROPRIATE skill works - how well it fits
/// decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   survey   I  best fit     a scanner sweeps the bench; the tubes change colour          ~2s
///   plan     I  workaround   write a test plan on the clipboard, then run it              4s
///   unlock   I  workaround   open the sample cabinet and match its vials to the tubes     5s
///   inspect  C  off-letter   read every label, then sort the samples by hand              8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (survey) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_BioLabQuest : vc_FitQuestBase
{
    [Header("Bench")]
    [SerializeField] private Transform[] dishes;
    [SerializeField] private Transform microscope;
    [Tooltip("The three test tubes, left to right.")]
    [SerializeField] private SpriteRenderer[] tubes;
    [SerializeField] private Sprite tubePale;
    [Tooltip("Result colour per tube: red, green, blue.")]
    [SerializeField] private Sprite[] tubeResult;

    [Header("Cabinet / report")]
    [SerializeField] private SpriteRenderer cabinet;
    [SerializeField] private Sprite cabinetOpen;
    [SerializeField] private GameObject report;

    [Header("Effects")]
    [SerializeField] private Sprite scanLine;
    [SerializeField] private Sprite[] labels;
    [SerializeField] private Sprite[] bubbleFrames;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] bulbFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite magnifier;

    [Header("Timing (seconds)")]
    [SerializeField] private float surveyTime = 2f;
    [SerializeField] private float planTime = 4f;
    [SerializeField] private float solveTime = 5f;
    [SerializeField] private float inspectTime = 8f;

    private Sprite _cabinetClosed;
    private Vector3[] _dishRest;
    private Vector3 _barPos;
    private float _idleClock, _sparkClock;
    private GameObject _scan, _lens;

    protected override Vector3 BarWorldPosition => _barPos;

    private void Awake()
    {
        if (cabinet != null) _cabinetClosed = cabinet.sprite;
        if (dishes != null)
        {
            _dishRest = new Vector3[dishes.Length];
            for (int i = 0; i < dishes.Length; i++) _dishRest[i] = dishes[i].localPosition;
        }
    }

    protected override void OnBegin()
    {
        if (tubes != null) foreach (SpriteRenderer t in tubes) if (t != null) t.sprite = tubePale;
        if (cabinet != null && _cabinetClosed != null) cabinet.sprite = _cabinetClosed;
        if (report != null) report.SetActive(false);
        if (dishes != null) for (int i = 0; i < dishes.Length; i++) dishes[i].localPosition = _dishRest[i];
        _barPos = (microscope != null ? microscope.position : transform.position) + new Vector3(-0.5f, 1.1f, 0f);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Biology Lab",
            "Three samples need testing. Find out what each one is.",
            new[] { "Test the samples" });
    }

    // ── Idle life: bubbling tubes, a blinking microscope lamp ────────────

    private void Update()
    {
        if (!Started || Done) return;

        _idleClock -= Time.deltaTime;
        if (_idleClock <= 0f && tubes != null && tubes.Length > 0)
        {
            _idleClock = Random.Range(0.7f, 1.4f);
            SpriteRenderer t = tubes[Random.Range(0, tubes.Length)];
            if (t != null) Fx(bubbleFrames, t.transform.position + new Vector3(0f, 0.3f, 0f), 5f, 0.45f);
        }

        _sparkClock -= Time.deltaTime;
        if (_sparkClock <= 0f && microscope != null)
        {
            _sparkClock = Random.Range(1.6f, 2.6f);
            Fx(sparkFrames, microscope.position + new Vector3(0.18f, 0.25f, 0f), 8f, 0.1f, 8);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("survey")) { StartWork(SurveyRoutine()); return; }
        if (BlockedByWork()) return;

        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        if (d.HasTag("unlock")) { StartWork(SolveRoutine()); return; }
        if (d.HasTag("inspect")) { StartWork(InspectRoutine()); return; }

        Toast("Lab work needs careful study.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (_scan != null) Destroy(_scan);
        if (_lens != null) Destroy(_lens);
        if (dishes != null && _dishRest != null)
            for (int i = 0; i < dishes.Length; i++) dishes[i].localPosition = _dishRest[i];
    }

    // ── Survey: scanner sweep ────────────────────────────────────────────

    private IEnumerator SurveyRoutine()
    {
        Toast("Scanning the samples...", surveyTime);
        ShowBar(true);
        SunodGame.Core.vc_Sfx.Play("scan", 0.6f);

        float x0 = dishes[0].position.x - 0.8f, x1 = tubes[tubes.Length - 1].transform.position.x + 0.6f;
        float y = dishes[0].position.y - 0.1f;
        _scan = MakeSprite("ScanLine", scanLine, 24);
        float sweep = surveyTime * 0.7f, t = 0f;
        bool[] hitD = new bool[dishes.Length], hitT = new bool[tubes.Length];
        while (t < surveyTime)
        {
            t += Time.deltaTime;
            float x = Mathf.Lerp(x0, x1, Mathf.Clamp01(t / sweep));
            if (_scan != null) _scan.transform.position = Snap(new Vector3(x, y, 0f));
            for (int i = 0; i < dishes.Length; i++)
                if (!hitD[i] && x >= dishes[i].position.x) { hitD[i] = true; Fx(checkFrames, dishes[i].position + new Vector3(0f, 0.45f, 0f), 3f, 0.25f); }
            for (int i = 0; i < tubes.Length; i++)
                if (!hitT[i] && x >= tubes[i].transform.position.x) { hitT[i] = true; ColourTube(i); }
            SetBar(t / surveyTime);
            yield return null;
        }
        if (_scan != null) Destroy(_scan);
        Finish("Sample tested. The tubes changed colour!");
    }

    private void ColourTube(int i)
    {
        if (tubes[i] == null) return;
        if (tubeResult != null && i < tubeResult.Length) tubes[i].sprite = tubeResult[i];
        Vector3 p = tubes[i].transform.position + new Vector3(0f, 0.3f, 0f);
        Fx(bubbleFrames, p, 6f, 0.5f);
        Fx(sparkFrames, p + new Vector3(0f, 0.2f, 0f), 10f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("bubble", 0.5f, 0.1f);
    }

    // ── Plan: write the plan on the clipboard, then run it ───────────────

    private IEnumerator PlanRoutine()
    {
        Toast("Writing out a test plan...", planTime);
        ShowBar(true);
        Vector3 rp = report != null ? report.transform.position : transform.position;
        if (report != null) { report.SetActive(true); StartCoroutine(PopScale(report.transform)); }
        SunodGame.Core.vc_Sfx.Play("page_next", 0.7f);

        float t = 0f;
        float planPhase = planTime * 0.5f;
        int ticks = 0;
        while (t < planPhase)
        {
            t += Time.deltaTime;
            SetBar(t / planTime);
            if (ticks < 3 && t >= (ticks + 1) * planPhase / 3.2f)
            {
                Fx(checkFrames, rp + new Vector3(0.05f, 0.5f - ticks * 0.28f, 0f), 3f, 0.05f, 26);
                SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.1f);
                ticks++;
            }
            yield return null;
        }

        Toast("Running the plan...", planTime * 0.5f);
        int n = tubes.Length;
        float per = (planTime - planPhase) / n;
        for (int i = 0; i < n; i++)
        {
            ColourTube(i);
            float s = 0f;
            while (s < per) { s += Time.deltaTime; t += Time.deltaTime; SetBar(t / planTime); yield return null; }
        }
        Finish("Your test plan sorted out all three samples!");
    }

    // ── Solve: open the cabinet, match vials to tubes ────────────────────

    private IEnumerator SolveRoutine()
    {
        Toast("Looking for the matching reagents...", solveTime);
        ShowBar(true);
        float t = 0f;
        while (t < 0.6f) { t += Time.deltaTime; SetBar(t / solveTime); yield return null; }

        if (cabinet != null && cabinetOpen != null) cabinet.sprite = cabinetOpen;
        SunodGame.Core.vc_Sfx.Play("door_open", 0.6f);
        Fx(sparkFrames, cabinet != null ? cabinet.transform.position + new Vector3(0f, 0.5f, 0f) : transform.position, 10f, 0.3f);

        float per = (solveTime - 1.6f) / tubes.Length;
        for (int i = 0; i < tubes.Length; i++)
        {
            Vector3 from = (cabinet != null ? cabinet.transform.position : transform.position) + new Vector3(0.1f, 0.3f, 0f);
            Vector3 to = tubes[i].transform.position + new Vector3(0f, 0.5f, 0f);
            GameObject vial = MakeSprite("Vial", tubeResult[i], 24);
            float s = 0f;
            while (s < per)
            {
                s += Time.deltaTime; t += Time.deltaTime;
                float k = Mathf.Clamp01(s / per);
                if (vial != null) vial.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.9f, 0f));
                SetBar(t / solveTime);
                yield return null;
            }
            if (vial != null) Destroy(vial);
            Fx(ringFrames, tubes[i].transform.position + new Vector3(0f, 0.3f, 0f), 6f);
            ColourTube(i);
        }

        Fx(bulbFrames, microscope != null ? microscope.position + new Vector3(0.9f, 1.3f, 0f) : transform.position, 2f, 0.3f, 26);
        SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
        float rest = solveTime - t;
        while (rest > 0f) { rest -= Time.deltaTime; t += Time.deltaTime; SetBar(t / solveTime); yield return null; }
        Finish("You opened the cabinet and matched the results!");
    }

    // ── Inspect: read every label, then sort by hand ─────────────────────

    private IEnumerator InspectRoutine()
    {
        Toast("Reading every label carefully...", inspectTime);
        ShowBar(true);
        _lens = MakeSprite("Magnifier", magnifier, 24);
        int steps = dishes.Length + tubes.Length;
        float labelTime = inspectTime * 0.75f, per = labelTime / steps, t = 0f;

        for (int i = 0; i < steps; i++)
        {
            Vector3 at = i < dishes.Length ? dishes[i].position : tubes[i - dishes.Length].transform.position;
            if (_lens != null) _lens.transform.position = Snap(at + new Vector3(0.15f, 0.45f, 0f));
            Fx(ringFrames, at + new Vector3(0f, 0.1f, 0f), 6f);
            if (i < dishes.Length && labels != null && i < labels.Length)
                Fx(new[] { labels[i] }, at + new Vector3(0f, 0.55f, 0f), 1.6f, 0.3f, 26);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.1f);
            float s = 0f;
            while (s < per) { s += Time.deltaTime; t += Time.deltaTime; SetBar(t / inspectTime); yield return null; }
        }
        if (_lens != null) Destroy(_lens);

        // sort by hand: each dish hops, each tube gets its colour
        Toast("Sorting the samples...", inspectTime - labelTime);
        float sortPer = (inspectTime - labelTime) / Mathf.Max(1, tubes.Length);
        for (int i = 0; i < tubes.Length; i++)
        {
            if (i < dishes.Length) StartCoroutine(Hop(dishes[i]));
            SunodGame.Core.vc_Sfx.Play("shuffle", 0.5f, 0.1f);
            ColourTube(i);
            float s = 0f;
            while (s < sortPer) { s += Time.deltaTime; t += Time.deltaTime; SetBar(t / inspectTime); yield return null; }
        }
        Finish("Every label read and every sample sorted.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private GameObject MakeSprite(string name, Sprite s, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingLayerName = sortingLayer; sr.sortingOrder = order;
        return go;
    }

    private IEnumerator Hop(Transform tr)
    {
        Vector3 rest = tr.localPosition;
        for (int i = 0; i < 3; i++)
        {
            if (tr == null) yield break;
            tr.localPosition = rest + new Vector3(0f, 2f / 22f, 0f);
            yield return new WaitForSeconds(0.08f);
            tr.localPosition = rest;
            yield return new WaitForSeconds(0.08f);
        }
    }

    private IEnumerator PopScale(Transform tr)
    {
        Vector3 s0 = tr.localScale;
        foreach (float k in new[] { 0.4f, 0.7f, 1.2f, 1f })
        {
            if (tr == null) yield break;
            tr.localScale = s0 * k;
            yield return new WaitForSeconds(0.06f);
        }
        if (tr != null) tr.localScale = s0;
    }
}
