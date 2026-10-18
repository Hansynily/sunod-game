using System.Collections;
using UnityEngine;

/// <summary>
/// C quest (C7). Handle a teller window for Ms. Uy. Any APPROPRIATE skill works - how well it fits
/// decides how fast it goes (and so the stars).
///
///   inspect  C  best fit     counts and verifies the cash, stamps each slip; the line clears  ~2s
///   collect  C  workaround   takes in the deposit slips, one customer per press               x3
///   direct   C  workaround   directs the line to the right windows, then serves               4s
///   command  E  off-letter   gets everyone to fill out their slips first, then serves          8s
///   anything else: a hint, no effect
/// </summary>
[DisallowMultipleComponent]
public class vc_BankTellerQuest : vc_FitQuestKit
{
    [Header("Counter")]
    [SerializeField] private SpriteRenderer drawer;
    [SerializeField] private Sprite drawerClosed;
    [SerializeField] private Sprite drawerOpen;
    [SerializeField] private SpriteRenderer stamp;
    [SerializeField] private Sprite stampUp;
    [SerializeField] private Sprite stampDown;
    [SerializeField] private SpriteRenderer vault;
    [SerializeField] private Sprite vaultDull;
    [SerializeField] private Sprite vaultShine;
    [Tooltip("Where a customer stands to be served: main window first, then the second window.")]
    [SerializeField] private Transform[] windowSpots;
    [SerializeField] private Transform exitSpot;

    [Header("Customers")]
    [SerializeField] private SpriteRenderer[] customers;
    [SerializeField] private Sprite[] customerWait;
    [SerializeField] private Sprite[] customerHappy;

    [Header("Effects")]
    [SerializeField] private Sprite approved;
    [SerializeField] private Sprite billStack;
    [SerializeField] private Sprite depositSlip;
    [SerializeField] private Sprite shoutBubble;
    [SerializeField] private Sprite dotsBubble;
    [SerializeField] private Sprite[] scribbleFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float inspectTime = 2f;
    [SerializeField] private int collectPresses = 3;
    [SerializeField] private float directTime = 4f;
    [SerializeField] private float commandTime = 8f;

    private Vector3[] _rest;
    private bool[] _served;
    private int _slips;
    private float _dotsClock, _shineClock;

    protected override Vector3 BarWorldPosition => (drawer != null ? drawer.transform.position : transform.position) + new Vector3(-0.5f, 1.6f, 0f);

    private void Awake()
    {
        _rest = new Vector3[customers.Length];
        _served = new bool[customers.Length];
        for (int i = 0; i < customers.Length; i++) _rest[i] = customers[i].transform.position;
    }

    protected override void OnBegin()
    {
        ClearSpawned();
        _slips = 0; _dotsClock = 1.2f; _shineClock = 2f;
        for (int i = 0; i < customers.Length; i++)
        {
            _served[i] = false;
            customers[i].gameObject.SetActive(true);
            customers[i].transform.position = _rest[i];
            customers[i].sprite = customerWait[i];
        }
        if (drawer != null) drawer.sprite = drawerClosed;
        if (stamp != null) stamp.sprite = stampUp;
        if (vault != null) vault.sprite = vaultDull;
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Bank Teller",
            "The bank line is long. Handle a teller window for Ms. Uy.", new[] { "Serve the customers" });
    }

    // ── Idle life: impatient line, the vault glints ──────────────────────

    private void Update()
    {
        if (!Started || Done) return;
        _shineClock -= Time.deltaTime;
        if (_shineClock <= 0f && vault != null)
        {
            _shineClock = Random.Range(2.5f, 3.5f);
            vault.sprite = vaultShine;
            Later(0.25f, () => { if (vault != null && !Done) vault.sprite = vaultDull; });
        }
        if (Working) return;
        _dotsClock -= Time.deltaTime;
        if (_dotsClock > 0f) return;
        _dotsClock = Random.Range(1.6f, 2.4f);
        int i = NextWaiting();
        if (i >= 0) { Bubble(dotsBubble, customers[Random.Range(i, customers.Length)], 1.3f); }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("inspect")) { StartWork(InspectRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("collect")) { CollectPress(); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        Toast("Banking needs careful, orderly work.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        for (int i = 0; i < customers.Length; i++) if (!_served[i]) customers[i].transform.position = _rest[i];
    }

    private IEnumerator InspectRoutine()
    {
        Toast("You count, verify and stamp...", inspectTime);
        ShowBar(true);
        var c = new float[1];
        float per = inspectTime / Mathf.Max(1, customers.Length);
        for (int i = 0; i < customers.Length; i++)
        {
            if (!_served[i]) StartCoroutine(Serve(i, Spot(0), per * 0.9f));
            yield return Tick(per, c, inspectTime);
        }
        Cleared("Cash verified, slips stamped. The line is clear!");
    }

    private void CollectPress()
    {
        int i = NextWaiting();
        if (i < 0) return;
        _slips++;
        StartCoroutine(Serve(i, Spot(0), 0.9f));
        if (_slips < collectPresses && NextWaiting() >= 0) { Toast($"Deposit slip taken in ({_slips}/{collectPresses})", 2f); return; }
        Cleared("Every deposit slip handled. All done!");
    }

    private IEnumerator DirectRoutine()
    {
        Toast("You direct the line to the open windows...", directTime);
        ShowBar(true);
        var c = new float[1];
        if (shoutBubble != null && customers.Length > 0) Bubble(shoutBubble, customers[0], 0.8f);
        float per = (directTime - 0.2f) / Mathf.Max(1, customers.Length);
        for (int i = 0; i < customers.Length; i++)
        {
            if (!_served[i]) StartCoroutine(Serve(i, Spot(i % 2), per * 1.6f));
            yield return Tick(per, c, directTime);
        }
        yield return Tick(directTime - c[0], c, directTime);
        Cleared("Two windows, no waiting. Everyone's served!");
    }

    private IEnumerator CommandRoutine()
    {
        Toast("Everyone fills out their slips first!", commandTime);
        ShowBar(true);
        var c = new float[1];
        for (int i = 0; i < customers.Length; i++) Bubble(shoutBubble, customers[i], 0.8f);
        Sfx("bell", 0.5f, 0f);
        while (c[0] < commandTime - 3f)
        {
            int i = Random.Range(0, customers.Length);
            if (!_served[i]) Fx(scribbleFrames, Above(customers[i], -0.6f), 6f, 0f, 22);
            Sfx("scribble", 0.3f);
            yield return Tick(0.45f, c, commandTime);
        }
        float per = (commandTime - c[0]) / Mathf.Max(1, customers.Length);
        for (int i = 0; i < customers.Length; i++)
        {
            if (!_served[i]) StartCoroutine(Serve(i, Spot(0), per * 0.9f));
            yield return Tick(per, c, commandTime);
        }
        Cleared("Slips ready, service quick. All served!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Customer steps up, cash is counted, the slip is stamped, they leave happy.</summary>
    private IEnumerator Serve(int i, Vector3 at, float seconds)
    {
        if (_served[i]) yield break;
        _served[i] = true;
        SpriteRenderer cu = customers[i];
        float step = seconds * 0.3f;
        yield return WalkSprite(cu, null, cu.transform.position, at, step);
        if (depositSlip != null && drawer != null) Fx(new[] { depositSlip }, Above(cu, -0.5f), 4f, 0.3f, 24);
        if (drawer != null) drawer.sprite = drawerOpen;
        if (billStack != null && drawer != null) Fx(new[] { billStack }, Above(drawer, 0f), 4f, 0.2f, 24);
        Sfx("coin", 0.5f);
        yield return new WaitForSeconds(seconds * 0.25f);
        if (stamp != null) stamp.sprite = stampDown;
        Sfx("stamp", 0.7f, 0f);
        if (approved != null && stamp != null) Fx(new[] { approved }, Above(stamp, 0.1f), 2.5f, 0.2f, 25);
        yield return new WaitForSeconds(seconds * 0.15f);
        if (stamp != null) stamp.sprite = stampUp;
        if (drawer != null) drawer.sprite = drawerClosed;
        cu.sprite = customerHappy[i];
        StartCoroutine(Hop(cu.transform, 2));
        Vector3 exit = exitSpot != null ? exitSpot.position : at + Vector3.right * 2f;
        yield return WalkSprite(cu, null, cu.transform.position, exit, seconds * 0.3f);
        cu.gameObject.SetActive(false);
    }

    private void Cleared(string msg)
    {
        if (vault != null) { vault.sprite = vaultShine; Fx(sparkFrames, vault.bounds.center + new Vector3(0.3f, 0.4f, 0f), 12f, 0.2f); }
        Sfx("chime", 0.5f, 0f);
        Finish(msg);
    }

    private Vector3 Spot(int k)
    {
        if (windowSpots != null && windowSpots.Length > 0) return windowSpots[Mathf.Min(k, windowSpots.Length - 1)].position;
        return transform.position;
    }

    private int NextWaiting()
    {
        for (int i = 0; i < customers.Length; i++) if (!_served[i]) return i;
        return -1;
    }
}
