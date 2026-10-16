using System.Collections;
using UnityEngine;

/// <summary>
/// R quest (R1). Test the robotics team's parts before the contest. Any APPROPRIATE skill works -
/// how well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   repair   R  best fit     tests each part fast, finds the bad gear and fixes it              ~2s
///   push     R  workaround   stress-tests the parts by hand; the bad gear snaps and is swapped  x3
///   build    R  workaround   builds a test rig and runs every part through it                   5s
///   inspect  C  off-letter   checks each part against the spec sheet, slowly                    8s
///   anything else: a hint, no effect
///
/// When every part passes, the robot's eyes light up. Best fit (repair) takes over a slow fix.
/// </summary>
[DisallowMultipleComponent]
public class vc_RoboticsPartsQuest : vc_FitQuestBase
{
    [Header("Bench")]
    [SerializeField] private SpriteRenderer[] parts;
    [Tooltip("Index in Parts of the faulty part (the cracked gear).")]
    [SerializeField] private int badIndex = 0;
    [SerializeField] private Sprite badSprite;
    [SerializeField] private Sprite goodSprite;
    [SerializeField] private SpriteRenderer robot;
    [SerializeField] private Sprite robotOff;
    [SerializeField] private Sprite robotOn;
    [SerializeField] private SpriteRenderer testRig;
    [SerializeField] private SpriteRenderer specSheet;

    [Header("Effects")]
    [SerializeField] private Sprite checkMark;
    [SerializeField] private Sprite crossMark;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite magnifier;

    [Header("Timing (seconds)")]
    [SerializeField] private float repairTime = 2f;
    [SerializeField] private int pushPresses = 3;
    [SerializeField] private float buildTime = 5f;
    [SerializeField] private float inspectTime = 8f;

    private Vector3[] _rest;
    private bool[] _tested;
    private int _pushes;
    private float _idleClock;

    protected override Vector3 BarWorldPosition =>
        (robot != null ? robot.transform.position : transform.position) + new Vector3(-0.5f, 1.9f, 0f);

    private void Awake()
    {
        int n = parts != null ? parts.Length : 0;
        _rest = new Vector3[n];
        _tested = new bool[n];
        for (int i = 0; i < n; i++) _rest[i] = parts[i].transform.position;
    }

    protected override void OnBegin()
    {
        _pushes = 0;
        _idleClock = 1.5f;
        for (int i = 0; i < parts.Length; i++)
        {
            _tested[i] = false;
            parts[i].transform.position = _rest[i];
        }
        if (Valid(badIndex) && badSprite != null) parts[badIndex].sprite = badSprite;
        if (robot != null && robotOff != null) robot.sprite = robotOff;
        if (testRig != null) testRig.gameObject.SetActive(false);
        if (specSheet != null) specSheet.gameObject.SetActive(false);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Parts Check",
            "The robotics team needs every part tested before the contest.",
            new[] { "Test the robot parts" });
    }

    // ── Idle life: the cracked gear rattles and sparks ───────────────────

    private void Update()
    {
        if (!Started || Done || Working || !Valid(badIndex) || _tested[badIndex]) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = Random.Range(1.6f, 2.6f);
        StartCoroutine(Shake(parts[badIndex].transform, parts[badIndex].transform.localPosition, 0.25f, 1));
        Fx(sparkFrames, parts[badIndex].transform.position + new Vector3(0.2f, 0.25f, 0f), 12f, 0.1f);
        SunodGame.Core.vc_Sfx.Play("zap", 0.2f, 0.2f, 0.5f);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("repair")) { StartWork(RepairRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("push")) { PushPress(); return; }
        if (d.HasTag("build")) { StartWork(BuildRoutine()); return; }
        if (d.HasTag("inspect")) { StartWork(InspectRoutine()); return; }
        Toast("These parts need hands-on testing.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        for (int i = 0; i < parts.Length; i++) parts[i].transform.position = _rest[i];
        if (testRig != null) testRig.gameObject.SetActive(false);
        if (specSheet != null) specSheet.gameObject.SetActive(false);
    }

    // ── Repair: fast test, fix the bad gear ──────────────────────────────

    private IEnumerator RepairRoutine()
    {
        Toast("You test each part, fast...", repairTime);
        ShowBar(true);
        float per = (repairTime - 0.2f) / Mathf.Max(1, parts.Length), t = 0f;
        for (int i = 0; i < parts.Length; i++)
        {
            if (_tested[i]) { yield return BarStep(0.05f, t / repairTime, t / repairTime); continue; }
            StartCoroutine(Hop(parts[i].transform, _rest[i]));
            Fx(ringFrames, parts[i].transform.position, 8f);
            SunodGame.Core.vc_Sfx.Play("clack", 0.4f, 0.1f);
            if (i == badIndex)
            {
                Mark(i, false);
                yield return BarStep(per * 0.5f, t / repairTime, (t + per * 0.5f) / repairTime);
                Fix(i);
                yield return BarStep(per * 0.5f, (t + per * 0.5f) / repairTime, (t + per) / repairTime);
            }
            else
            {
                Pass(i);
                yield return BarStep(per, t / repairTime, (t + per) / repairTime);
            }
            t += per;
        }
        AllPass("Every part tested. The bad gear is fixed!");
    }

    // ── Push: stress-test by hand, one press at a time ───────────────────

    private void PushPress()
    {
        _pushes++;
        // the good parts first; the cracked gear snaps on the last press
        int good = 0;
        for (int i = 0; i < parts.Length; i++) if (i != badIndex) good++;
        int share = Mathf.CeilToInt(good / (float)Mathf.Max(1, pushPresses - 1));
        int done = 0;
        for (int i = 0; i < parts.Length && done < share && _pushes < pushPresses; i++)
        {
            if (i == badIndex || _tested[i]) continue;
            StartCoroutine(Shake(parts[i].transform, parts[i].transform.localPosition, 0.3f, 1));
            Pass(i);
            done++;
        }
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f, 0.05f);

        if (_pushes < pushPresses)
        {
            Toast($"You bend and twist the parts ({_pushes}/{pushPresses})", 2f);
            return;
        }
        for (int i = 0; i < parts.Length; i++) if (i != badIndex && !_tested[i]) Pass(i);
        if (Valid(badIndex) && !_tested[badIndex])
        {
            Mark(badIndex, false);
            Fx(dustFrames, parts[badIndex].transform.position, 10f, 0.15f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.6f);
            Fix(badIndex);
        }
        AllPass("The cracked gear snapped. Swapped for a good one!");
    }

    // ── Build: a test rig, parts run through one by one ──────────────────

    private IEnumerator BuildRoutine()
    {
        Toast("You build a test rig...", buildTime);
        ShowBar(true);
        float t = 0f;
        if (testRig != null)
        {
            testRig.gameObject.SetActive(true);
            yield return Pop(testRig.transform);
            for (int k = 0; k < 3; k++)
            {
                Fx(hammerFrames, testRig.bounds.center + new Vector3(0.3f, 0.4f, 0f), 8f);
                SunodGame.Core.vc_Sfx.Play("hammer", 0.5f, 0.1f);
                yield return BarStep(0.3f, t / buildTime, (t + 0.3f) / buildTime);
                t += 0.3f;
            }
        }
        float per = (buildTime - t) / Mathf.Max(1, parts.Length);
        Vector3 slot = testRig != null ? testRig.transform.position + new Vector3(0f, 0.25f, 0f) : transform.position;
        for (int i = 0; i < parts.Length; i++)
        {
            Transform p = parts[i].transform;
            yield return Move(p, _rest[i], slot, per * 0.3f, t, buildTime);
            t += per * 0.3f;
            if (testRig != null) StartCoroutine(Shake(testRig.transform, testRig.transform.localPosition, per * 0.3f, 1));
            SunodGame.Core.vc_Sfx.Play("clack", 0.45f, 0.1f);
            yield return BarStep(per * 0.3f, t / buildTime, (t + per * 0.3f) / buildTime);
            t += per * 0.3f;
            if (i == badIndex && !_tested[i]) { Mark(i, false); Fix(i); }
            else if (!_tested[i]) Pass(i);
            yield return Move(p, slot, _rest[i], per * 0.4f, t, buildTime);
            t += per * 0.4f;
        }
        AllPass("The test rig caught the bad gear. All parts pass!");
    }

    // ── Inspect: check against the spec sheet, slowly ────────────────────

    private IEnumerator InspectRoutine()
    {
        Toast("You check each part against the spec sheet...", inspectTime);
        ShowBar(true);
        if (specSheet != null) { specSheet.gameObject.SetActive(true); StartCoroutine(Pop(specSheet.transform)); }
        SunodGame.Core.vc_Sfx.Play("page_next", 0.5f);
        float per = inspectTime / Mathf.Max(1, parts.Length), t = 0f;
        for (int i = 0; i < parts.Length; i++)
        {
            float k = 0f;
            while (k < per * 0.75f)
            {
                if (magnifier != null) Fx(new[] { magnifier }, parts[i].transform.position + new Vector3(0.15f, 0.2f, 0f), 4f, 0f, 24);
                SunodGame.Core.vc_Sfx.Play("scan", 0.25f, 0.1f, 0.4f);
                yield return BarStep(0.25f, (t + k) / inspectTime, (t + k + 0.25f) / inspectTime);
                k += 0.25f;
            }
            if (i == badIndex && !_tested[i]) { Mark(i, false); Fix(i); }
            else if (!_tested[i]) Pass(i);
            yield return BarStep(per - k, (t + k) / inspectTime, (t + per) / inspectTime);
            t += per;
        }
        AllPass("Checked against spec. Every part is good now.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void Pass(int i)
    {
        _tested[i] = true;
        Mark(i, true);
    }

    private void Fix(int i)
    {
        if (!Valid(i)) return;
        StartCoroutine(FixRoutine(i));
    }

    private IEnumerator FixRoutine(int i)
    {
        _tested[i] = true;
        yield return new WaitForSeconds(0.25f);
        Fx(hammerFrames, parts[i].transform.position + new Vector3(0.15f, 0.35f, 0f), 8f);
        SunodGame.Core.vc_Sfx.Play("hammer", 0.6f, 0.08f);
        Fx(sparkFrames, parts[i].transform.position, 12f, 0.15f);
        if (goodSprite != null) parts[i].sprite = goodSprite;
        yield return new WaitForSeconds(0.15f);
        Mark(i, true);
    }

    private void Mark(int i, bool ok)
    {
        Sprite s = ok ? checkMark : crossMark;
        if (s != null) Fx(new[] { s }, parts[i].transform.position + new Vector3(0f, 0.45f, 0f), 1.4f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play(ok ? "chime" : "buzz", 0.35f, 0.05f);
    }

    private void AllPass(string msg)
    {
        if (robot != null && robotOn != null) robot.sprite = robotOn;
        if (robot != null) Fx(sparkFrames, robot.bounds.center + new Vector3(0f, 0.4f, 0f), 12f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("idea", 0.5f);
        if (specSheet != null) specSheet.gameObject.SetActive(false);
        Finish(msg);
    }

    private bool Valid(int i) => parts != null && i >= 0 && i < parts.Length && parts[i] != null;

    private IEnumerator Move(Transform tr, Vector3 from, Vector3 to, float seconds, float t0, float total)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            tr.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.3f, 0f));
            SetBar((t0 + t) / total);
            yield return null;
        }
        tr.position = to;
    }

    private IEnumerator Hop(Transform tr, Vector3 rest)
    {
        tr.position = rest + new Vector3(0f, 2f / 22f, 0f);
        yield return new WaitForSeconds(0.1f);
        if (tr != null) tr.position = rest;
    }

    private IEnumerator Pop(Transform tr)
    {
        Vector3 s0 = tr.localScale;
        foreach (float k in new[] { 0.4f, 0.8f, 1.15f, 1f })
        {
            if (tr == null) yield break;
            tr.localScale = s0 * k;
            yield return new WaitForSeconds(0.06f);
        }
    }

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
}
