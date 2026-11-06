using System.Collections;
using UnityEngine;

/// <summary>
/// R quest (R4). Put the school radio back together. Any APPROPRIATE skill works - how well it
/// fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   repair   R  best fit     solder the loose wire; the static clears and music plays      ~2s
///   push     R  workaround   push a loose part back into the radio, one per press          x3
///   build    R  workaround   rebuild the radio from spare parts (a patched-up DIY radio)    5s
///   unlock   I  off-letter   troubleshoot the circuit with a probe, then fix it by hand     8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (repair) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_RadioRepairQuest : vc_FitQuestBase
{
    [Header("Radio")]
    [SerializeField] private SpriteRenderer radio;
    [SerializeField] private Sprite radioBroken;
    [SerializeField] private Sprite radioFixed;
    [SerializeField] private Sprite radioBuilt;
    [SerializeField] private Transform[] parts;

    [Header("Effects")]
    [SerializeField] private Sprite[] staticFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] smokeFrames;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] exclaimFrames;
    [SerializeField] private Sprite[] bulbFrames;
    [SerializeField] private Sprite[] noteFrames;
    [SerializeField] private Sprite solderIron;
    [SerializeField] private Sprite probe;

    [Header("Timing (seconds)")]
    [SerializeField] private float repairTime = 2f;
    [SerializeField] private int pushPresses = 3;
    [SerializeField] private float buildTime = 5f;
    [SerializeField] private float troubleshootTime = 8f;

    // Skill system v2 (QUEST_REDESIGN.md): Solve (prep) probes the board and finds the loose part,
    // then Push clicks every part home in one go. Preference: Push (keep it original).
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Kuya Dan",
        presses = 5,
        bestFitTag = "repair",
        bestFitSeconds = 2f,
        prepTag = "unlock",
        prepUnlocksTag = "push",
        prepLine = "You probe the board and find the loose part...",
        preferredTag = "push",
        preferenceLine = "Nothing replaced, all original. Salamat, pre!",
        fastLines = new[]
        {
            ("push", "Click, click, click and it plays! Still all original!"),
            ("build", "You built a working radio that fast?!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private Vector3[] _partRest;
    private int _pushes, _pushPress;
    private float _staticClock;
    private GameObject _tool;

    protected override Vector3 BarWorldPosition => RadioTop + new Vector3(-0.5f, 0.35f, 0f);
    private Vector3 RadioTop => radio != null ? radio.bounds.center + new Vector3(0f, radio.bounds.extents.y, 0f) : transform.position;
    private Vector3 RadioMid => radio != null ? radio.bounds.center : transform.position;

    private void Awake()
    {
        int n = parts != null ? parts.Length : 0;
        _partRest = new Vector3[n];
        for (int i = 0; i < n; i++) _partRest[i] = parts[i].position;
    }

    protected override void OnBegin()
    {
        _pushes = 0;
        _pushPress = 0;
        _staticClock = 0.5f;
        if (_tool != null) Destroy(_tool);
        if (radio != null) radio.sprite = radioBroken;
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].position = _partRest[i];
            parts[i].gameObject.SetActive(true);
        }
        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Radio Repair",
            "Kuya Dan's radio club radio stopped working. He wants it kept original if possible, so he'd love a hands-on fix.",
            new[] { "Put the school radio back together" });
    }

    // ── Idle life: crackling static while it's broken ────────────────────

    private void Update()
    {
        if (!Started || Done || radio == null || radio.sprite != radioBroken) return;
        _staticClock -= Time.deltaTime;
        if (_staticClock <= 0f)
        {
            _staticClock = Random.Range(1.1f, 2.0f);
            Fx(staticFrames, RadioTop + new Vector3(-0.1f, 0.25f, 0f), 7f, 0.1f, 20);
            SunodGame.Core.vc_Sfx.Play("radio_static", 0.22f, 0.1f, 0.5f);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("repair")) { StartWork(RepairRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("push")) { PushPress(); return; }
        if (d.HasTag("build")) { StartWork(BuildRoutine(PathTime(buildTime), CurrentInstant)); return; }
        if (d.HasTag("unlock")) { StartWork(TroubleshootRoutine(PathTime(troubleshootTime))); return; }
        Toast("Electronics need hands-on work.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (_tool != null) Destroy(_tool);
        _tool = null;
    }

    // ── Repair: solder the loose wire ────────────────────────────────────

    private IEnumerator RepairRoutine()
    {
        Toast("You solder the loose wire...", repairTime);
        ShowBar(true);
        yield return Solder(repairTime - 0.3f, 0f, repairTime);
        SetBar(1f);
        yield return new WaitForSeconds(0.3f);
        SwapRadio(radioFixed);
        HideParts(true);
        Finish("The static clears and music plays. Fixed!");
    }

    /// <summary>Iron tip at the loose wire: sizzle, spark and smoke ticks while the bar fills.</summary>
    private IEnumerator Solder(float seconds, float k0Time, float total)
    {
        Vector3 wire = RadioMid + new Vector3(0.75f, -0.1f, 0f);
        MakeTool(solderIron, wire + new Vector3(0.45f, 0.05f, 0f));
        float t = 0f, tick = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            SetBar((k0Time + t) / total);
            if (_tool != null) _tool.transform.position = Snap(wire + new Vector3(0.45f + Mathf.Sin(t * 30f) * 0.03f, 0.05f, 0f));
            if (tick >= 0.4f)
            {
                tick = 0f;
                SunodGame.Core.vc_Sfx.Play("sizzle", 0.4f, 0.1f);
                Fx(sparkFrames, wire, 12f, 0.1f);
                Fx(smokeFrames, wire + new Vector3(0f, 0.15f, 0f), 8f, 0.3f);
            }
            yield return null;
        }
        if (_tool != null) Destroy(_tool);
        _tool = null;
    }

    private void MakeTool(Sprite s, Vector3 at)
    {
        if (_tool != null) Destroy(_tool);
        _tool = new GameObject("Tool");
        _tool.transform.SetParent(transform, true);
        _tool.transform.position = Snap(at);
        var sr = _tool.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 22;
    }

    // ── Push: shove a loose part back in, one per press ──────────────────

    // 3 presses at Lv1, 2 at Lv2, 1 at Lv3 or after the Solve prep: each press pushes its share
    // of the parts that are left; the last one runs through StartWork and finishes.
    private void PushPress()
    {
        if (parts == null || parts.Length == 0) return;
        int total = Mathf.Max(1, pushPresses);
        if (_pushes >= total) return;
        _pushPress++;
        int need = Mathf.Clamp(PathPresses(total), 1, total);
        int remaining = total - _pushes;
        int pressesLeft = Mathf.Max(1, need - _pushPress + 1);
        int count = _pushPress >= need ? remaining : Mathf.CeilToInt(remaining / (float)pressesLeft);
        int first = _pushes;
        _pushes += count;

        if (_pushes >= total)
        {
            StartWork(PushFinishRoutine(first, count, count > 1));
            return;
        }
        Toast($"You push a part back in ({_pushes}/{total})", 2f);
        for (int k = 0; k < count; k++) StartCoroutine(PushPartLater(first + k, k * 0.2f));
    }

    private IEnumerator PushPartLater(int unit, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        yield return PushPart(Mathf.Min(unit, parts.Length - 1));
    }

    private IEnumerator PushPart(int i)
    {
        Transform p = parts[i];
        Vector3 from = p.position, to = RadioMid + new Vector3(0f, -0.15f, 0f);
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            p.position = Snap(Vector3.Lerp(from, to, t / 0.35f) + new Vector3(0f, Mathf.Sin(t / 0.35f * Mathf.PI) * 0.25f, 0f));
            yield return null;
        }
        p.gameObject.SetActive(false);
        SunodGame.Core.vc_Sfx.Play("zap", 0.5f, 0.1f);
        Fx(sparkFrames, to, 12f, 0.1f);
        if (radio != null) StartCoroutine(Flicker());
    }

    private IEnumerator Flicker()
    {
        if (radio == null || Done) yield break;
        Sprite keep = radio.sprite;
        radio.sprite = radioFixed; yield return new WaitForSeconds(0.08f);
        if (!Done && radio != null) radio.sprite = keep; yield return new WaitForSeconds(0.06f);
        if (!Done && radio != null) radio.sprite = radioFixed; yield return new WaitForSeconds(0.06f);
        if (!Done && radio != null && keep == radioBroken) radio.sprite = keep;
    }

    private IEnumerator PushFinishRoutine(int first, int count, bool oneGo)
    {
        Toast(oneGo ? "Click, click, click! Every part goes home!" : "The last part clicks home!", 2f);
        ShowBar(true);
        for (int k = 0; k < count; k++)
        {
            StartCoroutine(PushPart(Mathf.Min(first + k, parts.Length - 1)));
            SetBar((k + 1f) / count);
            yield return new WaitForSeconds(count > 1 ? 0.2f : 0f);
        }
        yield return new WaitForSeconds(0.55f);
        SwapRadio(radioFixed);
        HideParts(false);
        Finish(oneGo ? "Every part clicked home in one go. It works!" : "You pushed the parts back in place. It works!");
    }

    // ── Build: rebuild from spare parts ──────────────────────────────────

    private IEnumerator BuildRoutine(float seconds, bool oneGo)
    {
        Toast(oneGo ? "You snap a working radio together in one go!" : "Rebuilding the radio from spare parts...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        int n = parts.Length;
        float tapT = seconds * 0.16f;
        float per = (seconds - tapT) / Mathf.Max(1, n), t = 0f;
        for (int i = 0; i < n; i++)
        {
            StartCoroutine(PushPart(i));
            SunodGame.Core.vc_Sfx.Play("clack", 0.5f, 0.1f);
            yield return BarStep(per, t / seconds, (t + per) / seconds);
            t += per;
        }
        // patch it up with a few last taps
        for (int k = 0; k < 3; k++)
        {
            SunodGame.Core.vc_Sfx.Play("hammer", 0.45f, 0.1f);
            Fx(sparkFrames, RadioMid + new Vector3(-0.5f + k * 0.5f, 0.3f, 0f), 12f, 0.1f);
            yield return BarStep(tapT / 3f, (t + k * tapT / 3f) / seconds, (t + (k + 1) * tapT / 3f) / seconds);
        }
        SwapRadio(radioBuilt != null ? radioBuilt : radioFixed);
        Finish(oneGo ? "A working radio, built in one go!" : "You rebuilt the radio from spare parts. It plays!");
    }

    // ── Troubleshoot: probe the circuit, then fix it by hand ─────────────

    private IEnumerator TroubleshootRoutine(float seconds)
    {
        Toast("Testing the circuit with a probe...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        Vector3 m = RadioMid;
        Vector3[] pts = { m + new Vector3(-0.55f, 0.1f, 0f), m + new Vector3(0.25f, 0.2f, 0f), m + new Vector3(0.75f, -0.1f, 0f) };
        float testT = seconds * 0.66f, per = testT / pts.Length, t = 0f;
        float moveT = per * 0.4f;

        MakeTool(probe, pts[0] + new Vector3(0f, 0.5f, 0f));
        for (int i = 0; i < pts.Length; i++)
        {
            Vector3 from = _tool != null ? _tool.transform.position : pts[i], to = pts[i] + new Vector3(0f, 0.5f, 0f);
            float mv = 0f;
            while (mv < moveT)
            {
                mv += Time.deltaTime;
                if (_tool != null) _tool.transform.position = Snap(Vector3.Lerp(from, to, mv / moveT));
                SetBar((t + mv) / seconds);
                yield return null;
            }
            Fx(ringFrames, pts[i], 6f);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.1f);
            bool faulty = i == pts.Length - 1;
            Fx(faulty ? exclaimFrames : checkFrames, pts[i] + new Vector3(0f, 0.7f, 0f), 2f, 0.3f, 24);
            if (faulty) SunodGame.Core.vc_Sfx.Play("zap", 0.4f);
            yield return BarStep(per - moveT, (t + moveT) / seconds, (t + per) / seconds);
            t += per;
        }
        if (_tool != null) Destroy(_tool);
        _tool = null;

        Fx(bulbFrames, m + new Vector3(0f, 1.1f, 0f), 2f, 0.3f, 25);
        SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
        float bulbT = seconds * 0.11f;
        yield return BarStep(bulbT, t / seconds, (t + bulbT) / seconds);
        t += bulbT;

        float fix = Mathf.Max(0.3f, seconds - t);
        yield return Solder(fix, t, seconds);
        SwapRadio(radioFixed);
        HideParts(true);
        Finish("Found the fault and fixed it by hand. It works!");
    }

    // ── Prep: Solve probes the board and finds the loose part ────────────

    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        Vector3 m = RadioMid;
        Vector3 loose = parts != null && parts.Length > 0 && parts[0] != null ? parts[0].position : m + new Vector3(-0.55f, 0.1f, 0f);
        Vector3 a = m + new Vector3(0.8f, 0.55f, 0f), b = loose + new Vector3(0f, 0.45f, 0f);
        MakeTool(probe, a);
        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            if (_tool != null) _tool.transform.position = Snap(Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / 0.7f)));
            yield return null;
        }
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.05f);
        Fx(ringFrames, loose, 6f);
        Fx(exclaimFrames, loose + new Vector3(0f, 0.7f, 0f), 2f, 0.3f, 24);
        yield return new WaitForSeconds(0.3f);
        if (_tool != null) Destroy(_tool);
        _tool = null;
    }

    // ── Results ──────────────────────────────────────────────────────────

    private void SwapRadio(Sprite s)
    {
        if (radio == null) return;
        radio.sprite = s;
        SunodGame.Core.vc_Sfx.Play("radio_tune", 0.6f);
        Vector3 top = RadioTop;
        for (int i = 0; i < 3; i++) StartCoroutine(NoteLater(i, top));
        StartCoroutine(Hop(radio.transform));
    }

    private IEnumerator NoteLater(int i, Vector3 top)
    {
        yield return new WaitForSeconds(i * 0.18f);
        if (noteFrames != null && noteFrames.Length > 0)
            Fx(new[] { noteFrames[i % noteFrames.Length] }, top + new Vector3(-0.5f + i * 0.5f, 0.1f, 0f), 1.3f, 0.6f, 24);
    }

    private IEnumerator Hop(Transform tr)
    {
        Vector3 rest = tr.position;
        tr.position = rest + new Vector3(0f, 2f / 22f, 0f);
        yield return new WaitForSeconds(0.08f);
        if (tr != null) tr.position = rest + new Vector3(0f, 1f / 22f, 0f);
        yield return new WaitForSeconds(0.06f);
        if (tr != null) tr.position = rest;
    }

    private void HideParts(bool spark)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (!parts[i].gameObject.activeSelf) continue;
            if (spark) Fx(sparkFrames, parts[i].position, 12f, 0.1f);
            parts[i].gameObject.SetActive(false);
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
