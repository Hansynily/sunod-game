using System.Collections;
using UnityEngine;

/// <summary>
/// R quest (R7). Assemble Joy's fundraiser kits on the assembly line. Any APPROPRIATE skill works -
/// how well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   build    R  best fit     snaps the kits together down the line; 3 boxes close and stack  ~2s
///   push     R  workaround   press-fits each lid shut, one kit per press                     x3
///   repair   R  workaround   fixes the broken pieces first, then packs                       4s
///   direct   C  off-letter   labels the stations in order, then packs slowly                 8s
///   command  E  off-letter   gets a helper to man the line                                   8s
///   anything else: a hint, no effect
///
/// Best fit (build) takes over a slow fix that's already running.
/// </summary>
[DisallowMultipleComponent]
public class vc_FundraiserKitsQuest : vc_FitQuestBase
{
    [Header("Line")]
    [SerializeField] private SpriteRenderer[] bins;
    [SerializeField] private SpriteRenderer[] kits;
    [SerializeField] private Sprite kitOpen;
    [SerializeField] private Sprite kitClosed;
    [Tooltip("Where finished kits stack up (bottom kit position).")]
    [SerializeField] private Transform stackSpot;
    [SerializeField] private SpriteRenderer[] stationLabels;

    [Header("Effects")]
    [SerializeField] private Sprite[] partSprites;
    [SerializeField] private Sprite plusOne;
    [SerializeField] private Sprite crackMark;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] dustFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float buildTime = 2f;
    [SerializeField] private int pushPresses = 3;
    [SerializeField] private float repairTime = 4f;
    [SerializeField] private float directTime = 8f;
    [SerializeField] private float commandWalk = 4f;
    [SerializeField] private float commandWork = 4f;

    // Skill system v2 (QUEST_REDESIGN.md): Inspect (prep) flags every cracked piece, then Repair fixes
    // them all and packs in one go. Preference: Repair (Joy wants the broken pieces mended, not skipped).
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Joy",
        presses = 5,
        bestFitTag = "build",
        bestFitSeconds = 2f,
        prepTag = "inspect",
        prepUnlocksTag = "repair",
        prepLine = "You look over the line... every cracked piece gets flagged.",
        preferredTag = "repair",
        preferenceLine = "You fixed the broken pieces instead of tossing them? Thank you, so thoughtful!",
        fastLines = new[]
        {
            ("push", "Every lid shut in one push!"),
            ("repair", "No broken pieces left, and so fast!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private Vector3[] _kitRest;
    private int[] _kitOrder;
    private bool[] _packed;
    private int _stacked, _pushes;
    private float _idleClock;

    protected override Vector3 BarWorldPosition =>
        (kits != null && kits.Length > 1 ? kits[1].transform.position : transform.position) + new Vector3(-0.5f, 1.5f, 0f);

    private void Awake()
    {
        int n = kits != null ? kits.Length : 0;
        _kitRest = new Vector3[n];
        _kitOrder = new int[n];
        _packed = new bool[n];
        for (int i = 0; i < n; i++) { _kitRest[i] = kits[i].transform.position; _kitOrder[i] = kits[i].sortingOrder; }
    }

    protected override void OnBegin()
    {
        _stacked = 0; _pushes = 0; _idleClock = 1f;
        if (bins != null) foreach (var bn in bins) if (bn != null) bn.color = Color.white;
        for (int i = 0; i < kits.Length; i++)
        {
            _packed[i] = false;
            kits[i].transform.position = _kitRest[i];
            kits[i].sortingOrder = _kitOrder[i];
            if (kitOpen != null) kits[i].sprite = kitOpen;
        }
        if (stationLabels != null) foreach (var l in stationLabels) if (l != null) l.gameObject.SetActive(false);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Fundraiser Kits",
            "Joy needs the fundraiser kits put together, but some of the pieces came broken. She'd rather have them mended than thrown out.",
            new[] { "Assemble the kits" });
    }

    // ── Idle life: the open kits wait, parts rattle in the bins ──────────

    private void Update()
    {
        if (!Started || Done || Working || bins == null || bins.Length == 0) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = Random.Range(1.4f, 2.4f);
        var b = bins[Random.Range(0, bins.Length)];
        StartCoroutine(Shake(b.transform, b.transform.localPosition, 0.2f, 1));
        SunodGame.Core.vc_Sfx.Play("shuffle", 0.15f, 0.2f, 0.6f);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("build")) { StartWork(BuildRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("push")) { PushPress(); return; }
        if (d.HasTag("repair")) { StartWork(RepairRoutine(PathTime(repairTime), CurrentInstant)); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine(PathTime(directTime))); return; }
        if (d.HasTag("command")) { StartWork(CommandRoutine(PathTime(commandWalk + commandWork))); return; }
        Toast("These kits need hands-on work.", 2.2f);
    }

    // ── Build: snap the kits together down the line ──────────────────────

    private IEnumerator BuildRoutine()
    {
        Toast("You snap the kits together...", buildTime);
        ShowBar(true);
        yield return PackAll(0f, buildTime, buildTime, fast: true);
        Finish("Three kits packed and stacked. Done!");
    }

    // ── Push: press-fit one lid per press ────────────────────────────────

    // 3 presses at Lv1, 2 at Lv2, 1 at Lv3: each press closes its share of the kits; the last one packs the rest.
    private void PushPress()
    {
        _pushes++;
        int need = Mathf.Max(1, PathPresses(pushPresses));
        if (_pushes >= need)
        {
            StartWork(PushFinishRoutine(need == 1));
            return;
        }

        int left = 0;
        for (int n = 0; n < kits.Length; n++) if (!_packed[n]) left++;
        int share = Mathf.Max(1, Mathf.CeilToInt(left / (float)(need - _pushes + 1)));
        for (int s = 0; s < share; s++)
        {
            int ki = NextOpen();
            if (ki < 0) break;
            FillKit(ki);
            StartCoroutine(CloseAndStack(ki, 0.25f));
        }
        Toast($"You press a lid shut ({_pushes}/{need})", 2f);
    }

    private IEnumerator PushFinishRoutine(bool oneGo)
    {
        Toast(oneGo ? "One push per lid, all the way down the line!" : "You press the last lids shut...", 2f);
        ShowBar(true);
        int count = 0;
        for (int n = 0; n < kits.Length; n++) if (!_packed[n]) count++;
        float per = oneGo ? 0.15f : 0.25f;
        float t = 0f, total = Mathf.Max(0.3f, count * per + 0.6f);
        int ki;
        while ((ki = NextOpen()) >= 0)
        {
            FillKit(ki);
            StartCoroutine(CloseAndStack(ki, 0.25f));
            yield return BarStep(per, t / total, (t + per) / total);
            t += per;
        }
        yield return BarStep(0.6f, t / total, 1f);
        Finish(oneGo ? "Every lid shut in one push. Kits stacked!" : "Every lid pressed shut. Kits stacked!");
    }

    // ── Repair: fix the broken pieces, then pack ─────────────────────────

    private IEnumerator RepairRoutine(float total, bool oneGo)
    {
        Toast(oneGo ? "Every flagged piece gets fixed in one go!" : "You fix the broken pieces first...", total);
        ShowBar(true);
        float fix = total * 0.55f, t = 0f;
        float per = fix / Mathf.Max(1, bins.Length);
        for (int b = 0; b < bins.Length; b++)
        {
            Vector3 at = bins[b].bounds.center + new Vector3(0f, 0.35f, 0f);
            if (crackMark != null) Fx(new[] { crackMark }, at, 2.5f, 0f, 24);
            yield return BarStep(per * 0.4f, t / total, (t + per * 0.4f) / total);
            Fx(hammerFrames, at + new Vector3(0.15f, 0.3f, 0f), 8f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.55f, 0.1f);
            Fx(sparkFrames, at, 12f, 0.1f);
            bins[b].color = Color.white;
            yield return BarStep(per * 0.6f, (t + per * 0.4f) / total, (t + per) / total);
            t += per;
        }
        yield return PackAll(t, total, Mathf.Max(0.3f, total - t), fast: true);
        Finish(oneGo ? "Every cracked piece fixed and every kit packed in one go!" : "Broken pieces fixed and every kit packed!");
    }

    // ── Direct: label the stations, then pack slowly ─────────────────────

    private IEnumerator DirectRoutine(float total)
    {
        Toast("You set up the line in order...", total);
        ShowBar(true);
        float setup = total * 0.45f, t = 0f;
        int n = stationLabels != null ? stationLabels.Length : 0;
        float per = setup / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            if (stationLabels[i] != null) { stationLabels[i].gameObject.SetActive(true); StartCoroutine(Pop(stationLabels[i].transform)); }
            SunodGame.Core.vc_Sfx.Play("stamp", 0.45f, 0.05f);
            yield return BarStep(per, t / total, (t + per) / total);
            t += per;
        }
        yield return PackAll(t, total, Mathf.Max(0.3f, total - t), fast: false);
        Finish("Neat stations, steady packing. All kits done!");
    }

    // ── Command: a helper mans the line ──────────────────────────────────

    private IEnumerator CommandRoutine(float total)
    {
        Toast("You get a helper to man the stations!", 2.5f);
        ShowBar(true);
        float walkT = total * commandWalk / (commandWalk + commandWork), workT = total - walkT;
        Vector3 to = (bins != null && bins.Length > 0 ? bins[0].transform.position : transform.position) + new Vector3(-0.2f, -1.1f, 0f);
        Vector3 from = to + new Vector3(-6f, 0f, 0f);
        SpawnHelper(from, 7);
        yield return HelperWalk(from, to, walkT, t => SetBar(t / total));
        Toast("The helper packs the kits...", workT);
        yield return PackAll(walkT, total, workT, fast: false);
        Finish("Your helper kept the line moving. Kits done!");
    }

    // Prep (Inspect): a quick look along the line; every bin with a cracked piece is flagged and tinted.
    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        SunodGame.Core.vc_Sfx.Play("scan", 0.5f, 0.05f);
        if (bins != null)
        {
            foreach (var bn in bins)
            {
                if (bn == null) continue;
                bn.color = new Color(1f, 0.82f, 0.5f);
                Vector3 at = bn.bounds.center + new Vector3(0f, 0.35f, 0f);
                if (crackMark != null) Fx(new[] { crackMark }, at, 1.5f, 0.1f, 24);
                else Fx(sparkFrames, at, 12f, 0.1f);
                yield return new WaitForSeconds(0.25f);
            }
        }
        yield return new WaitForSeconds(0.25f);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private IEnumerator PackAll(float t0, float total, float span, bool fast)
    {
        int left = 0;
        for (int i = 0; i < kits.Length; i++) if (!_packed[i]) left++;
        float per = span / Mathf.Max(1, left), t = t0;
        int k;
        while ((k = NextOpen()) >= 0)
        {
            FillKit(k);
            yield return BarStep(per * 0.5f, t / total, (t + per * 0.5f) / total);
            StartCoroutine(CloseAndStack(k, fast ? 0.18f : 0.3f));
            yield return BarStep(per * 0.5f, (t + per * 0.5f) / total, (t + per) / total);
            t += per;
        }
    }

    /// <summary>Parts hop from each bin into kit i.</summary>
    private void FillKit(int i)
    {
        _packed[i] = true;
        if (partSprites == null || partSprites.Length == 0 || bins == null) return;
        for (int b = 0; b < bins.Length; b++)
            StartCoroutine(Arc(partSprites[b % partSprites.Length], bins[b].bounds.center + new Vector3(0f, 0.2f, 0f),
                               kits[i].bounds.center + new Vector3(0f, 0.1f, 0f), 0.25f, b * 0.06f));
        SunodGame.Core.vc_Sfx.Play("plop", 0.4f, 0.15f);
    }

    private IEnumerator CloseAndStack(int i, float slide)
    {
        yield return new WaitForSeconds(0.12f);
        if (kitClosed != null) kits[i].sprite = kitClosed;
        SunodGame.Core.vc_Sfx.Play("clack", 0.55f, 0.08f);
        Fx(dustFrames, kits[i].transform.position, 12f, 0.1f);
        if (plusOne != null) Fx(new[] { plusOne }, kits[i].bounds.center + new Vector3(0f, 0.5f, 0f), 1.5f, 0.35f, 24);
        Vector3 from = kits[i].transform.position;
        Vector3 to = (stackSpot != null ? stackSpot.position : from) + new Vector3(0f, _stacked * 0.42f, 0f);
        kits[i].sortingOrder = _kitOrder[i] + 2 + _stacked;
        _stacked++;
        float t = 0f;
        while (t < slide)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / slide);
            kits[i].transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.4f, 0f));
            yield return null;
        }
        kits[i].transform.position = to;
    }

    private int NextOpen()
    {
        for (int i = 0; i < kits.Length; i++) if (!_packed[i]) return i;
        return -1;
    }

    private IEnumerator Arc(Sprite s, Vector3 from, Vector3 to, float dur, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        var go = new GameObject("Part");
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 22;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            go.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f));
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator Pop(Transform tr)
    {
        Vector3 s0 = Vector3.one * 1f;
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
