using System.Collections;
using UnityEngine;

/// <summary>
/// C quest (C6). Help Manang Fe count the change for the canteen line. Any APPROPRIATE skill
/// works - how well it fits decides how fast it goes (and so the stars); skills that make no
/// sense do nothing.
///
///   inspect  C  best fit     punch the sums into the calculator; coins pop to each kid       ~2s
///   collect  C  workaround   gather the coins into piles by value, then pay out               4s
///   direct   C  workaround   sort the tray into slots, then serve one kid per press           x3
///   plan     I  off-letter   work the sums out on paper first, then pay                       8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (inspect) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_CanteenChangeQuest : vc_FitQuestBase
{
    [Header("Counter")]
    [SerializeField] private SpriteRenderer calculator;
    [Tooltip("The 3 LCD digit slots on the calculator screen, left to right.")]
    [SerializeField] private SpriteRenderer[] lcdSlots;
    [Tooltip("LCD digit sprites 0..9.")]
    [SerializeField] private Sprite[] lcdDigits;
    [SerializeField] private SpriteRenderer coinTray;
    [SerializeField] private Sprite trayMessy;
    [SerializeField] private Sprite traySorted;

    [Header("Kids in line")]
    [SerializeField] private SpriteRenderer[] kids;
    [SerializeField] private Sprite[] kidWaiting;
    [SerializeField] private Sprite[] kidPaid;
    [Tooltip("Change owed to each kid, in pesos (shown on the calculator).")]
    [SerializeField] private int[] changeOwed = { 15, 30, 5 };

    [Header("Effects")]
    [SerializeField] private Sprite[] coinFrames;
    [SerializeField] private Sprite paperSlip;
    [SerializeField] private Sprite[] scribbleFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite dotsBubble;
    [SerializeField] private Sprite checkMark;

    [Header("Timing (seconds)")]
    [SerializeField] private float inspectTime = 2f;
    [SerializeField] private float collectTime = 4f;
    [SerializeField] private float planTime = 8f;

    private Vector3[] _kidRest;
    private bool[] _paid;
    private int _served;
    private float _idleClock, _dotsClock;
    private GameObject _slip;

    protected override Vector3 BarWorldPosition =>
        (calculator != null ? calculator.transform.position : transform.position) + new Vector3(-0.5f, 2.3f, 0f);

    private void Awake()
    {
        int n = kids != null ? kids.Length : 0;
        _kidRest = new Vector3[n];
        _paid = new bool[n];
        for (int i = 0; i < n; i++) _kidRest[i] = kids[i].transform.position;
    }

    protected override void OnBegin()
    {
        if (_slip != null) Destroy(_slip);
        _served = 0;
        _idleClock = 0.8f; _dotsClock = 2f;
        for (int i = 0; i < kids.Length; i++)
        {
            _paid[i] = false;
            kids[i].transform.position = _kidRest[i];
            if (kidWaiting != null && i < kidWaiting.Length) kids[i].sprite = kidWaiting[i];
        }
        if (coinTray != null && trayMessy != null) coinTray.sprite = trayMessy;
        ShowNumber(-1);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Canteen Change",
            "The canteen line is long. Help Manang Fe count the change.",
            new[] { "Count the change" });
    }

    // ── Idle life: the line shuffles and waits ───────────────────────────

    private void Update()
    {
        if (!Started || Done || kids == null || kids.Length == 0) return;

        _idleClock -= Time.deltaTime;
        if (_idleClock <= 0f)
        {
            _idleClock = Random.Range(0.9f, 1.8f);
            int i = Random.Range(0, kids.Length);
            if (!_paid[i]) StartCoroutine(Hop(i, 1));
        }
        _dotsClock -= Time.deltaTime;
        if (_dotsClock <= 0f && !Working)
        {
            _dotsClock = Random.Range(2.2f, 3.2f);
            int i = NextUnpaid();
            if (i >= 0 && dotsBubble != null) Fx(new[] { dotsBubble }, Top(i), 1.4f, 0.15f, 24);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("inspect")) { StartWork(InspectRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("collect")) { StartWork(CollectRoutine()); return; }
        if (d.HasTag("direct")) { DirectPress(); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        Toast("This needs careful counting.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (_slip != null) Destroy(_slip);
    }

    // ── Inspect: calculator sums, coins to each kid ──────────────────────

    private IEnumerator InspectRoutine()
    {
        Toast("You punch the sums into the calculator...", inspectTime);
        ShowBar(true);
        int left = UnpaidCount();
        float per = inspectTime / Mathf.Max(1, left), t = 0f;
        int i;
        while ((i = NextUnpaid()) >= 0)
        {
            yield return Roll(i, per * 0.5f, t, inspectTime);
            t += per * 0.5f;
            Pay(i);
            yield return BarStep(per * 0.5f, t / inspectTime, (t + per * 0.5f) / inspectTime);
            t += per * 0.5f;
        }
        Finish("Every kid got the right change. Exact!");
    }

    // ── Collect: piles by value, then pay ────────────────────────────────

    private IEnumerator CollectRoutine()
    {
        Toast("You gather the coins into piles...", collectTime);
        ShowBar(true);
        float gather = collectTime - 1.5f, t = 0f;
        int hops = 8;
        float per = gather / hops;
        for (int h = 0; h < hops; h++)
        {
            if (coinTray != null && coinFrames != null && coinFrames.Length > 0)
            {
                Bounds b = coinTray.bounds;
                Fx(coinFrames, new Vector3(Random.Range(b.min.x, b.max.x), b.max.y + 0.05f, 0f), 12f, 0.25f, 22);
            }
            SunodGame.Core.vc_Sfx.Play("coin", 0.35f, 0.2f);
            if (h == hops / 2) SortTray();
            yield return BarStep(per, t / collectTime, (t + per) / collectTime);
            t += per;
        }
        int left = UnpaidCount();
        float payPer = (collectTime - t) / Mathf.Max(1, left);
        int i;
        while ((i = NextUnpaid()) >= 0)
        {
            ShowNumber(Owed(i));
            Pay(i);
            yield return BarStep(payPer, t / collectTime, (t + payPer) / collectTime);
            t += payPer;
        }
        Finish("Coins sorted into piles. Change paid out!");
    }

    // ── Direct: sort the tray, then serve the line one kid per press ─────

    private void DirectPress()
    {
        if (_served == 0) SortTray();
        int i = NextUnpaid();
        if (i < 0) return;
        _served++;
        ShowNumber(Owed(i));
        SunodGame.Core.vc_Sfx.Play("beep", 0.5f);
        Pay(i);
        int total = kids.Length;
        if (NextUnpaid() >= 0)
        {
            Toast($"Next in line! ({_served}/{total})", 1.8f);
            return;
        }
        Finish("The whole line is served, neat and orderly.");
    }

    // ── Plan: sums on paper first, then pay ──────────────────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("You work the sums out on paper first...", planTime);
        ShowBar(true);
        Vector3 at = (calculator != null ? calculator.transform.position : transform.position) + new Vector3(-1.1f, 0.15f, 0f);
        if (paperSlip != null)
        {
            _slip = new GameObject("PaperSlip");
            _slip.transform.SetParent(transform, true);
            _slip.transform.position = Snap(at);
            var sr = _slip.AddComponent<SpriteRenderer>();
            sr.sprite = paperSlip; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 14;
        }
        float work = planTime - 2.4f, t = 0f;
        while (t < work)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.05f, 0.25f), 0f), 6f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("scribble", 0.35f, 0.15f, 0.3f);
            yield return BarStep(0.4f, t / planTime, (t + 0.4f) / planTime);
            t += 0.4f;
        }
        if (checkMark != null) Fx(new[] { checkMark }, at + new Vector3(0f, 0.5f, 0f), 1.5f, 0.2f, 24);
        int left = UnpaidCount();
        float per = (planTime - t) / Mathf.Max(1, left);
        int i;
        while ((i = NextUnpaid()) >= 0)
        {
            ShowNumber(Owed(i));
            Pay(i);
            yield return BarStep(per, t / planTime, (t + per) / planTime);
            t += per;
        }
        Finish("Worked out on paper, paid to the peso!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Digits roll on the LCD, then settle on what kid i is owed.</summary>
    private IEnumerator Roll(int i, float seconds, float t0, float total)
    {
        float t = 0f, step = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            step += Time.deltaTime;
            if (step >= 0.06f)
            {
                step = 0f;
                ShowNumber(Random.Range(0, 1000));
                SunodGame.Core.vc_Sfx.Play("beep", 0.25f, 0.3f);
            }
            SetBar((t0 + t) / total);
            yield return null;
        }
        ShowNumber(Owed(i));
        SunodGame.Core.vc_Sfx.Play("beep", 0.5f);
    }

    private void Pay(int i)
    {
        if (i < 0 || i >= kids.Length || _paid[i]) return;
        _paid[i] = true;
        StartCoroutine(CoinArc(i));
    }

    private IEnumerator CoinArc(int i)
    {
        Vector3 from = coinTray != null ? coinTray.bounds.center + new Vector3(0f, 0.2f, 0f) : transform.position;
        Vector3 to = Top(i) + new Vector3(0f, -0.5f, 0f);
        GameObject go = null;
        SpriteRenderer sr = null;
        if (coinFrames != null && coinFrames.Length > 0)
        {
            go = new GameObject("Coin");
            go.transform.SetParent(transform, true);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayer; sr.sortingOrder = 25;
        }
        SunodGame.Core.vc_Sfx.Play("coin", 0.6f, 0.1f);
        float dur = 0.35f, t = 0f;
        while (t < dur && go != null)
        {
            t += Time.deltaTime;
            float k = t / dur;
            go.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.8f, 0f));
            sr.sprite = coinFrames[(int)(t * 16f) % coinFrames.Length];
            yield return null;
        }
        if (go != null) Destroy(go);
        if (kids[i] != null && kidPaid != null && i < kidPaid.Length) kids[i].sprite = kidPaid[i];
        if (sparkFrames != null) Fx(sparkFrames, Top(i), 12f, 0.15f);
        StartCoroutine(Hop(i, 3));
    }

    private void SortTray()
    {
        if (coinTray != null && traySorted != null) coinTray.sprite = traySorted;
        if (sparkFrames != null && coinTray != null) Fx(sparkFrames, coinTray.bounds.center + new Vector3(0f, 0.2f, 0f), 12f, 0.1f);
        SunodGame.Core.vc_Sfx.Play("shuffle", 0.5f);
    }

    private void ShowNumber(int value)
    {
        if (lcdSlots == null) return;
        for (int s = 0; s < lcdSlots.Length; s++)
        {
            if (lcdSlots[s] == null) continue;
            if (value < 0) { lcdSlots[s].enabled = false; continue; }
            int place = (int)Mathf.Pow(10, lcdSlots.Length - 1 - s);
            int digit = (value / place) % 10;
            bool leading = value < place && s < lcdSlots.Length - 1;
            lcdSlots[s].enabled = !leading;
            if (lcdDigits != null && digit < lcdDigits.Length) lcdSlots[s].sprite = lcdDigits[digit];
        }
    }

    private int Owed(int i) => changeOwed != null && i < changeOwed.Length ? changeOwed[i] : 10;

    private int NextUnpaid()
    {
        for (int i = 0; i < kids.Length; i++) if (!_paid[i]) return i;
        return -1;
    }

    private int UnpaidCount()
    {
        int n = 0;
        for (int i = 0; i < kids.Length; i++) if (!_paid[i]) n++;
        return n;
    }

    private Vector3 Top(int i)
    {
        Bounds b = kids[i].bounds;
        return new Vector3(b.center.x, b.max.y + 0.3f, 0f);
    }

    private IEnumerator Hop(int i, int px)
    {
        if (i < 0 || i >= kids.Length || kids[i] == null) yield break;
        Transform tr = kids[i].transform;
        tr.position = _kidRest[i] + new Vector3(0f, px / 22f, 0f);
        yield return new WaitForSeconds(0.1f);
        if (tr != null) tr.position = _kidRest[i];
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
