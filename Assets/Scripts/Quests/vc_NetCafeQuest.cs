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

    private int[] _minutes;
    private bool[] _billed;
    private int _slips;
    private float _clock, _tapClock;

    protected override Vector3 BarWorldPosition => (printer != null ? printer.transform.position : transform.position) + new Vector3(-0.5f, 1.4f, 0f);

    private void Awake()
    {
        _minutes = new int[pcs.Length];
        _billed = new bool[pcs.Length];
    }

    protected override void OnBegin()
    {
        ClearSpawned();
        _slips = 0; _clock = 0f; _tapClock = 1f;
        for (int i = 0; i < pcs.Length; i++)
        {
            _billed[i] = false;
            _minutes[i] = i < startMinutes.Length ? startMinutes[i] : 10;
            pcs[i].sprite = pcGame;
            customers[i].sprite = customerWait[i];
            ShowTime(i);
        }
        if (printer != null) printer.sprite = printerIdle;
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Net Cafe Billing",
            "Help Jay make the customer bills at the net cafe.", new[] { "Bill every customer" });
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
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        if (d.HasTag("unlock")) { StartWork(SolveRoutine()); return; }
        Toast("Billing needs careful, orderly work.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) pcs[i].sprite = pcGame;
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
        int i = NextUnbilled();
        if (i < 0) return;
        _slips++;
        StartCoroutine(SlipThenBill(i));
        if (_slips < collectPresses && NextUnbilled() >= 0) { Toast($"Time slip collected ({_slips}/{collectPresses})", 2f); return; }
        Finish("All the slips collected, all bills paid!");
    }

    private IEnumerator SlipThenBill(int i)
    {
        if (timeSlip != null && printer != null) yield return Fly(timeSlip, Above(pcs[i], -0.2f), printer.bounds.center, 0.35f);
        Bill(i);
    }

    private IEnumerator DirectRoutine()
    {
        Toast("You sort the bills per customer...", directTime);
        ShowBar(true);
        var c = new float[1];
        Vector3 desk = printer != null ? printer.bounds.center + new Vector3(-1.2f, -0.4f, 0f) : transform.position;
        for (int i = 0; i < pcs.Length; i++)
        {
            var r = Spawn(receipt, desk + new Vector3(i * 0.45f, 0f, 0f), 14, "SortedBill");
            StartCoroutine(PopIn(r.transform));
            Sfx("shuffle", 0.4f);
            yield return Tick(0.8f, c, directTime);
        }
        float per = (directTime - c[0]) / Mathf.Max(1, pcs.Length);
        for (int i = 0; i < pcs.Length; i++)
        {
            if (!_billed[i]) Bill(i);
            yield return Tick(per, c, directTime);
        }
        Finish("Sorted and handed out. Neat billing!");
    }

    private IEnumerator SolveRoutine()
    {
        Toast("The billing program glitches... you debug it.", solveTime);
        ShowBar(true);
        var c = new float[1];
        if (questionBubble != null && printer != null) Bubble(questionBubble, printer, 1.2f);
        bool on = true;
        while (c[0] < solveTime - 3f)
        {
            for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) pcs[i].sprite = on ? pcError : pcGame;
            if (on) Sfx("buzz", 0.3f);
            on = !on;
            yield return Tick(0.5f, c, solveTime);
        }
        if (ideaBubble != null && printer != null) Bubble(ideaBubble, printer, 1f);
        for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) pcs[i].sprite = pcOk;
        Sfx("idea", 0.6f, 0f);
        yield return Tick(0.9f, c, solveTime);
        float per = (solveTime - c[0]) / Mathf.Max(1, pcs.Length);
        for (int i = 0; i < pcs.Length; i++)
        {
            if (!_billed[i]) Bill(i);
            yield return Tick(per, c, solveTime);
        }
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

    private int NextUnbilled()
    {
        for (int i = 0; i < pcs.Length; i++) if (!_billed[i]) return i;
        return -1;
    }
}
