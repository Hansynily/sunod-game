using System.Collections;
using UnityEngine;

/// <summary>
/// C quest (C3). Make the customer bills at Jay's net cafe. Any APPROPRIATE skill works - how well
/// it fits decides how fast it goes (and so the stars).
///
///   inspect  C  best fit     checks each PC's time; receipts print, coins pop                 ~2s
///   collect  C  workaround   collects each time slip, one customer per press                  x3
///   direct   C  workaround   sorts the bills per customer, then hands them out                5s
///   unlock   I  off-letter   fixes the glitchy billing program (error to OK), then bills      8s
///   anything else: a hint, no effect
/// </summary>
[DisallowMultipleComponent]
public class vc_NetCafeQuest : vc_FitQuestKit
{
    [Header("PCs")]
    [SerializeField] private SpriteRenderer[] pcs;
    [SerializeField] private Sprite pcGame;
    [SerializeField] private Sprite pcBill;
    [SerializeField] private Sprite pcError;
    [SerializeField] private Sprite pcOk;
    [Tooltip("Two digit slots per PC (tens, ones), in PC order.")]
    [SerializeField] private SpriteRenderer[] timerDigits;
    [SerializeField] private Sprite[] digits;
    [SerializeField] private int[] startMinutes = { 34, 52, 17 };

    [Header("Customers & counter")]
    [SerializeField] private SpriteRenderer[] customers;
    [SerializeField] private Sprite[] customerWait;
    [SerializeField] private Sprite[] customerHappy;
    [SerializeField] private SpriteRenderer printer;
    [SerializeField] private Sprite printerIdle;
    [SerializeField] private Sprite printerOut;

    [Header("Effects")]
    [SerializeField] private Sprite receipt;
    [SerializeField] private Sprite timeSlip;
    [SerializeField] private Sprite[] coinFrames;
    [SerializeField] private Sprite magnifier;
    [SerializeField] private Sprite questionBubble;
    [SerializeField] private Sprite ideaBubble;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float inspectTime = 2f;
    [SerializeField] private int collectPresses = 3;
    [SerializeField] private float directTime = 5f;
    [SerializeField] private float solveTime = 8f;

    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Jay",
        presses = 4,
        bestFitTag = "inspect",
        bestFitSeconds = 2f,
        prepTag = "unlock",
        prepUnlocksTag = "collect",
        prepLine = "You debug the billing program... the error turns to OK.",
        preferredTag = "direct",
        preferenceLine = "Each bill to the right customer. Salamat, ha!",
        fastLines = new[]
        {
            ("collect", "Every slip in one go!"),
            ("direct", "Everyone got the right bill. No mistakes today!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private int[] _minutes;
    private bool[] _billed;
    private bool[] _slipped;
    private int _slips;
    private float _clock, _tapClock;

    protected override Vector3 BarWorldPosition => (printer != null ? printer.transform.position : transform.position) + new Vector3(-0.5f, 1.4f, 0f);

    private void Awake()
    {
        _minutes = new int[pcs.Length];
        _billed = new bool[pcs.Length];
        _slipped = new bool[pcs.Length];
    }

    protected override void OnBegin()
    {
        ClearSpawned();
        _slips = 0; _clock = 0f; _tapClock = 1f;
        for (int i = 0; i < pcs.Length; i++)
        {
            _billed[i] = false;
            _slipped[i] = false;
            _minutes[i] = i < startMinutes.Length ? startMinutes[i] : 10;
            pcs[i].sprite = pcGame;
            customers[i].sprite = customerWait[i];
            ShowTime(i);
        }
        if (printer != null) printer.sprite = printerIdle;
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Net Cafe Billing",
            "Help Jay make the customer bills at the net cafe. He'd love to see each bill handed to the right customer.", new[] { "Bill every customer" });
    }

    // ── Idle life: the PC timers keep counting, gamers tap away ──────────

    private void Update()
    {
        if (!Started || Done) return;
        _clock += Time.deltaTime;
        if (_clock >= 1f)
        {
            _clock = 0f;
            for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) { _minutes[i] = (_minutes[i] + 1) % 100; ShowTime(i); }
        }
        _tapClock -= Time.deltaTime;
        if (_tapClock <= 0f && !Working)
        {
            _tapClock = Random.Range(0.6f, 1.2f);
            int i = Random.Range(0, customers.Length);
            if (!_billed[i]) { StartCoroutine(Hop(customers[i].transform, 1)); Sfx("clack", 0.15f, 0.4f); }
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("inspect")) { StartWork(InspectRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("collect")) { CollectPress(); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine(PathTime(directTime), CurrentInstant)); return; }
        if (d.HasTag("unlock")) { StartWork(SolveRoutine(PathTime(solveTime))); return; }
        Toast("Billing needs careful, orderly work.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) { pcs[i].sprite = Prepared ? pcOk : pcGame; _slipped[i] = false; }
    }

    // Prep (Solve): the billing program's error turns to "OK"; Collect then gathers every slip at once.
    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        if (questionBubble != null && printer != null) Bubble(questionBubble, printer, 0.5f);
        for (int j = 0; j < 2; j++)
        {
            for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) pcs[i].sprite = pcError;
            Sfx("buzz", 0.3f);
            yield return new WaitForSeconds(0.25f);
            for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) pcs[i].sprite = pcGame;
            yield return new WaitForSeconds(0.1f);
        }
        for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) pcs[i].sprite = pcOk;
        if (ideaBubble != null && printer != null) Bubble(ideaBubble, printer, 0.6f);
        Sfx("idea", 0.6f, 0f);
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator InspectRoutine()
    {
        Toast("You check each PC's time in the program...", inspectTime);
        ShowBar(true);
        var c = new float[1];
        float per = inspectTime / Mathf.Max(1, pcs.Length);
        for (int i = 0; i < pcs.Length; i++)
        {
            if (_billed[i]) { yield return Tick(per, c, inspectTime); continue; }
            Bubble(magnifier, pcs[i], 0.4f);
            yield return Tick(per * 0.4f, c, inspectTime);
            Bill(i);
            yield return Tick(per * 0.6f, c, inspectTime);
        }
        Finish("Every bill printed and paid. Spot on!");
    }

    private void CollectPress()
    {
        int need = Mathf.Max(1, PathPresses(collectPresses));
        _slips++;
        if (_slips < need && NextUnslipped() >= 0)
        {
            // each early press collects its share of the slips
            int left = 0;
            for (int k = 0; k < pcs.Length; k++) if (!_billed[k] && !_slipped[k]) left++;
            int share = Mathf.CeilToInt(left / (float)(need - _slips + 1));
            for (int s = 0; s < share; s++)
            {
                int i = NextUnslipped();
                if (i < 0) break;
                _slipped[i] = true;
                StartCoroutine(SlipThenBill(i));
            }
            Toast($"Time slips collected ({_slips}/{need})", 2f);
            return;
        }
        StartWork(CollectRestRoutine(need == 1 && CurrentInstant));
    }

    // The last press: gather every remaining slip (staggered), wait for the printing, then finish.
    private IEnumerator CollectRestRoutine(bool oneGo)
    {
        Toast(oneGo ? "Every slip in one go!" : "You gather the last time slips...", 1.6f);
        ShowBar(true);
        var c = new float[1];
        float total = oneGo ? 1.2f : 2f;
        float gap = oneGo ? 0.12f : 0.3f;
        for (int i = 0; i < pcs.Length; i++)
        {
            if (_billed[i] || _slipped[i]) continue;
            _slipped[i] = true;
            StartCoroutine(SlipThenBill(i));
            yield return Tick(gap, c, total);
        }
        float guard = 0f;
        while (NextUnbilled() >= 0 && guard < 3f)
        {
            guard += Time.deltaTime;
            yield return null;
        }
        yield return new WaitForSeconds(0.6f);
        Finish(oneGo ? "Every slip collected in one go. All bills paid!" : "All the slips collected, all bills paid!");
    }

    private IEnumerator SlipThenBill(int i)
    {
        if (timeSlip != null && printer != null) yield return Fly(timeSlip, Above(pcs[i], -0.2f), printer.bounds.center, 0.35f);
        Bill(i);
    }

    private IEnumerator DirectRoutine(float seconds, bool oneGo)
    {
        Toast(oneGo ? "Sorting and handing out in one go!" : "You sort the bills per customer...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        var c = new float[1];
        Vector3 desk = printer != null ? printer.bounds.center + new Vector3(-1.2f, -0.4f, 0f) : transform.position;
        float sortPer = seconds * 0.48f / Mathf.Max(1, pcs.Length);
        for (int i = 0; i < pcs.Length; i++)
        {
            var r = Spawn(receipt, desk + new Vector3(i * 0.45f, 0f, 0f), 14, "SortedBill");
            StartCoroutine(PopIn(r.transform));
            Sfx("shuffle", 0.4f);
            yield return Tick(sortPer, c, seconds);
        }
        float per = Mathf.Max(0.05f, (seconds - c[0]) / Mathf.Max(1, pcs.Length));
        for (int i = 0; i < pcs.Length; i++)
        {
            if (!_billed[i]) Bill(i);
            yield return Tick(per, c, seconds);
        }
        yield return new WaitForSeconds(0.5f);
        Finish(oneGo ? "Sorted and handed out in one go. Neat billing!" : "Sorted and handed out. Neat billing!");
    }

    private IEnumerator SolveRoutine(float seconds)
    {
        Toast("The billing program glitches... you debug it.", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        var c = new float[1];
        if (questionBubble != null && printer != null) Bubble(questionBubble, printer, Mathf.Min(1.2f, seconds * 0.15f));
        bool on = true;
        float blink = Mathf.Max(0.12f, seconds * 0.0625f);
        while (c[0] < seconds * 0.625f)
        {
            for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) pcs[i].sprite = on ? pcError : pcGame;
            if (on) Sfx("buzz", 0.3f);
            on = !on;
            yield return Tick(blink, c, seconds);
        }
        if (ideaBubble != null && printer != null) Bubble(ideaBubble, printer, 1f);
        for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) pcs[i].sprite = pcOk;
        Sfx("idea", 0.6f, 0f);
        yield return Tick(seconds * 0.11f, c, seconds);
        float per = Mathf.Max(0.05f, (seconds - c[0]) / Mathf.Max(1, pcs.Length));
        for (int i = 0; i < pcs.Length; i++)
        {
            if (!_billed[i]) Bill(i);
            yield return Tick(per, c, seconds);
        }
        yield return new WaitForSeconds(0.5f);
        Finish("Bug fixed, bills printed. Nice debugging!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void Bill(int i)
    {
        if (_billed[i]) return;
        _billed[i] = true;
        pcs[i].sprite = pcBill;
        StartCoroutine(PrintAndPay(i));
    }

    private IEnumerator PrintAndPay(int i)
    {
        if (printer != null) printer.sprite = printerOut;
        Sfx("buzz", 0.3f, 0.2f);
        yield return new WaitForSeconds(0.2f);
        if (printer != null) printer.sprite = printerIdle;
        if (receipt != null && printer != null) yield return Fly(receipt, printer.bounds.center, Above(customers[i], -0.4f), 0.3f);
        customers[i].sprite = customerHappy[i];
        StartCoroutine(Hop(customers[i].transform, 3));
        if (printer != null) Fx(coinFrames, printer.bounds.center + new Vector3(0f, 0.4f, 0f), 12f, 0.4f, 24);
        Sfx("coin", 0.6f);
    }

    private IEnumerator Fly(Sprite s, Vector3 from, Vector3 to, float seconds)
    {
        var go = Spawn(s, from, 25, "Flying");
        float t = 0f;
        while (t < seconds && go != null)
        {
            t += Time.deltaTime;
            float k = t / seconds;
            go.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.5f, 0f));
            yield return null;
        }
        if (go != null) Destroy(go.gameObject);
    }

    private void ShowTime(int i)
    {
        if (timerDigits == null || digits == null || digits.Length < 10) return;
        int a = i * 2, b = i * 2 + 1;
        if (b >= timerDigits.Length) return;
        timerDigits[a].sprite = digits[_minutes[i] / 10];
        timerDigits[b].sprite = digits[_minutes[i] % 10];
    }

    private int NextUnslipped()
    {
        for (int i = 0; i < pcs.Length; i++) if (!_billed[i] && !_slipped[i]) return i;
        return -1;
    }

    private int NextUnbilled()
    {
        for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) return i;
        return -1;
    }
}
