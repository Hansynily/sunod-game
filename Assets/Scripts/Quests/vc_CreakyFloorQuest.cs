using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R quest (R8). Fix the creaky, loose floorboards. Any APPROPRIATE skill works - how well it
/// fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   build    R  best fit     lay fresh planks across the whole patch, plank by plank   ~2s
///   repair   R  workaround   nail down the loose boards, a pair per press              x3
///   push     R  workaround   shove the heavy cabinet off the warped boards             4s
///   inspect  C  off-letter   tap every board to find the loose ones, then fix them      8s
///   summon   E  off-letter   Lolo's carpenter friend walks over and fixes it            10s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (build) takes over at once. Loose boards bounce and
/// creak when the player walks over them, and twitch now and then while the floor is broken.
/// </summary>
[DisallowMultipleComponent]
public class vc_CreakyFloorQuest : vc_FitQuestBase
{
    [Header("Floor")]
    [Tooltip("Parent of the 12 floor tiles. Tiles that start with the Loose sprite are the loose boards.")]
    [SerializeField] private Transform floorRoot;
    [SerializeField] private Sprite looseSprite;
    [SerializeField] private Sprite flatSprite;
    [SerializeField] private Sprite nailedSprite;
    [SerializeField] private Sprite freshSprite;

    [Header("Cabinet (push path)")]
    [SerializeField] private Transform cabinet;
    [SerializeField] private Vector3 cabinetPushOffset = new Vector3(2.4f, 0f, 0f);

    [Header("Effects")]
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] exclaimFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float buildTime = 2f;
    [SerializeField] private int repairPresses = 3;
    [SerializeField] private float pushTime = 4f;
    [SerializeField] private float inspectScanTime = 6.4f;
    [SerializeField] private float inspectFixTime = 1.6f;
    [SerializeField] private float carpenterWalkTime = 6f;
    [SerializeField] private float carpenterFixTime = 4f;

    private class Board
    {
        public SpriteRenderer sr;
        public Vector3 restLocal;
        public Sprite initial;
        public bool loose;
        public float cooldown;
    }

    // Skill system v2 (QUEST_REDESIGN.md): Inspect (prep) taps the floor and lights up the loose
    // boards, then Repair nails them all in one pass. Preference: Push (the quiet way).
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Lolo Ador",
        presses = 6,
        bestFitTag = "build",
        bestFitSeconds = 2f,
        prepTag = "inspect",
        prepUnlocksTag = "repair",
        prepLine = "You tap the floor... the loose boards light up.",
        preferredTag = "push",
        preferenceLine = "Shh... so quiet. Salamat, apo!",
        fastLines = new[]
        {
            ("repair", "All of them, in one go? And I barely heard it!"),
            ("push", "Shh... you moved that cabinet like a feather!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private readonly List<Board> _boards = new List<Board>();
    private Vector3 _cabinetRest;
    private bool _cabinetPushed;
    private int _repairPress;
    private Vector3 _patchCenter;
    private float _twitchClock;
    private Transform _player;

    protected override Vector3 BarWorldPosition => _patchCenter + new Vector3(-0.5f, 0.95f, 0f);

    private void Awake()
    {
        if (floorRoot == null) return;
        foreach (SpriteRenderer sr in floorRoot.GetComponentsInChildren<SpriteRenderer>(true))
            _boards.Add(new Board { sr = sr, restLocal = sr.transform.localPosition, initial = sr.sprite, loose = sr.sprite == looseSprite });

        // left-to-right, top-to-bottom: plank-by-plank effects read as a sweep
        _boards.Sort((a, b) =>
        {
            int c = a.restLocal.x.CompareTo(b.restLocal.x);
            return Mathf.Abs(a.restLocal.x - b.restLocal.x) > 0.05f ? c : b.restLocal.y.CompareTo(a.restLocal.y);
        });

        if (cabinet != null) _cabinetRest = cabinet.localPosition;
    }

    protected override void OnBegin()
    {
        _repairPress = 0;
        _cabinetPushed = false;
        _twitchClock = 0f;
        Vector3 sum = Vector3.zero;
        foreach (Board b in _boards)
        {
            b.sr.sprite = b.initial;
            b.sr.color = Color.white;
            b.sr.transform.localPosition = b.restLocal;
            b.loose = b.initial == looseSprite;
            b.cooldown = 0f;
            sum += b.sr.transform.position;
        }
        if (_boards.Count > 0) _patchCenter = sum / _boards.Count;
        if (cabinet != null) cabinet.localPosition = _cabinetRest;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Creaky Floorboards",
            "Lolo Ador's floorboards creak with every step. Make the floor solid again, and keep it quiet.",
            new[] { "Repair the floorboards" });
    }

    // ── Idle life: creak underfoot, twitching boards ─────────────────────

    private void Update()
    {
        if (!Started || Done) return;

        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }

        _twitchClock += Time.deltaTime;
        bool twitch = _twitchClock >= 2.8f;
        if (twitch) _twitchClock = Random.Range(0f, 1.2f);

        Board twitchBoard = null;
        if (twitch)
        {
            var loose = _boards.FindAll(b => b.loose);
            if (loose.Count > 0) twitchBoard = loose[Random.Range(0, loose.Count)];
        }

        foreach (Board b in _boards)
        {
            if (!b.loose) continue;
            b.cooldown -= Time.deltaTime;

            if (b == twitchBoard)
            {
                StartCoroutine(Bounce(b, 1, 0.14f));
                SunodGame.Core.vc_Sfx.Play("creak", 0.18f, 0.12f, 0.4f);
            }

            if (_player != null && b.cooldown <= 0f)
            {
                Vector3 d = _player.position - b.sr.transform.position;
                float k = (d.x * d.x) / (0.45f * 0.45f) + (d.y * d.y) / (0.40f * 0.40f);
                if (k < 1f)
                {
                    b.cooldown = 0.7f;
                    StartCoroutine(Bounce(b, 2, 0.16f));
                    Fx(dustFrames, b.sr.transform.position + new Vector3(0f, -0.1f, 0f), 8f, 0.15f);
                    SunodGame.Core.vc_Sfx.Play("creak", 0.5f, 0.12f, 0.3f);
                }
            }
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        // best fit always wins, even over a slow fix that's already running
        if (d.HasTag("build"))
        {
            StartWork(BuildRoutine());
            return;
        }

        if (BlockedByWork()) return;

        if (d.HasTag("repair"))
        {
            RepairPress();
            return;
        }
        if (d.HasTag("push"))
        {
            StartWork(PushRoutine(PathTime(pushTime)));
            return;
        }
        if (d.HasTag("inspect"))
        {
            StartWork(InspectRoutine(PathTime(inspectScanTime + inspectFixTime)));
            return;
        }
        if (d.HasTag("summon"))
        {
            StartWork(CarpenterRoutine(PathTime(carpenterWalkTime + carpenterFixTime)));
            return;
        }

        Toast("The floor needs hands-on work.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        // a half-pushed cabinet finishes its slide instead of freezing mid-room
        if (_cabinetPushed && cabinet != null) cabinet.localPosition = _cabinetRest + cabinetPushOffset;
    }

    // ── Repair: nail a pair of loose boards per press ────────────────────

    // 3 presses at Lv1, 2 at Lv2, 1 at Lv3 or after the Inspect prep (then it's one quick pass).
    private void RepairPress()
    {
        _repairPress++;
        int need = Mathf.Max(1, PathPresses(repairPresses));
        if (_repairPress >= need)
        {
            StartWork(RepairSweepRoutine(need == 1));
            return;
        }

        var loose = _boards.FindAll(b => b.loose);
        int share = Mathf.Max(1, Mathf.CeilToInt(loose.Count / (float)(need - _repairPress + 1)));
        for (int i = 0; i < share && i < loose.Count; i++)
            StartCoroutine(Nail(loose[i], i * 0.18f));
        Toast($"You hammer in the loose boards ({_repairPress}/{need})", 2f);
    }

    private IEnumerator RepairSweepRoutine(bool oneGo)
    {
        Toast(oneGo ? "One pass along the floor, nailing every loose board!" : "You hammer in the last of the loose boards...", 2f);
        ShowBar(true);
        var loose = _boards.FindAll(b => b.loose);
        float gap = oneGo ? 0.1f : 0.16f;
        for (int i = 0; i < loose.Count; i++)
        {
            StartCoroutine(Nail(loose[i], 0f));
            SetBar((i + 1f) / loose.Count);
            yield return new WaitForSeconds(gap);
        }
        yield return new WaitForSeconds(0.3f);
        Finish(oneGo ? "Every loose board nailed in one pass. Solid!" : "Every loose board is nailed down. Solid!");
    }

    // Prep (Inspect): tap along the floor; the loose boards ring, glow gold and say "!".
    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        SunodGame.Core.vc_Sfx.Play("scan", 0.5f, 0.05f);
        foreach (Board b in _boards)
        {
            Vector3 at = b.sr.transform.position;
            Fx(ringFrames, at + new Vector3(0f, 0.05f, 0f), 10f);
            if (b.loose)
            {
                b.sr.color = new Color(1f, 0.88f, 0.45f);
                Fx(exclaimFrames, at + new Vector3(0f, 0.55f, 0f), 2.5f, 0.1f);
                StartCoroutine(Bounce(b, 1, 0.12f));
                SunodGame.Core.vc_Sfx.Play("creak", 0.3f, 0.1f);
            }
            else SunodGame.Core.vc_Sfx.Play("clack", 0.25f, 0.08f);
            yield return new WaitForSeconds(0.07f);
        }
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>Hammer swings over a board, a spark flies, the board is nailed flat.</summary>
    private IEnumerator Nail(Board b, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Vector3 at = b.sr.transform.position;
        Fx(hammerFrames, at + new Vector3(0.1f, 0.4f, 0f), 8f);
        yield return new WaitForSeconds(0.14f);
        SunodGame.Core.vc_Sfx.Play("hammer", 0.7f, 0.08f);
        Fx(sparkFrames, at + new Vector3(0f, 0.05f, 0f), 12f, 0.1f);
        b.loose = false;
        b.sr.color = Color.white;
        if (nailedSprite != null) b.sr.sprite = nailedSprite;
        yield return Bounce(b, -1, 0.12f);
    }

    // ── Build: fresh planks, left to right ───────────────────────────────

    private IEnumerator BuildRoutine()
    {
        Toast("Laying brand-new planks...", 2f);
        ShowBar(true);
        int n = _boards.Count;
        float per = buildTime / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            Board b = _boards[i];
            b.loose = false;
            b.sr.color = Color.white;
            if (freshSprite != null) b.sr.sprite = freshSprite;
            StartCoroutine(Bounce(b, 1, 0.12f));
            Fx(dustFrames, b.sr.transform.position + new Vector3(0f, -0.12f, 0f), 12f, 0.1f);
            if (i % 2 == 0) SunodGame.Core.vc_Sfx.Play("clack", 0.55f, 0.1f);
            SetBar((i + 1f) / n);
            yield return new WaitForSeconds(per);
        }
        foreach (Board b in _boards) Fx(sparkFrames, b.sr.transform.position + new Vector3(0.1f, 0.05f, 0f), 12f, 0.15f);
        Finish("You laid brand-new flooring!");
    }

    // ── Push: slide the cabinet off the warped boards ────────────────────

    private IEnumerator PushRoutine(float pushTime)
    {
        Toast("You heave the heavy cabinet aside...", pushTime);
        ShowBar(true);
        _cabinetPushed = true;
        float t = 0f, scrape = 0f;
        while (t < pushTime)
        {
            t += Time.deltaTime;
            scrape += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / pushTime);
            SetBar(t / pushTime);
            if (cabinet != null)
                cabinet.localPosition = Snap(_cabinetRest + cabinetPushOffset * k);
            if (scrape >= 0.42f)
            {
                scrape = 0f;
                SunodGame.Core.vc_Sfx.Play("scrape", 0.5f, 0.1f);
                if (cabinet != null) Fx(dustFrames, cabinet.position + new Vector3(-0.5f, 0.05f, 0f), 8f, 0.2f);
            }
            yield return null;
        }
        if (cabinet != null) cabinet.localPosition = _cabinetRest + cabinetPushOffset;

        // the warped boards settle flat, one after another
        SunodGame.Core.vc_Sfx.Play("hammer", 0.8f);
        for (int i = 0; i < _boards.Count; i++)
        {
            Board b = _boards[i];
            if (b.loose)
            {
                b.loose = false;
                b.sr.color = Color.white;
                if (flatSprite != null) b.sr.sprite = flatSprite;
                StartCoroutine(Bounce(b, -1, 0.12f));
                Fx(dustFrames, b.sr.transform.position + new Vector3(0f, -0.1f, 0f), 12f, 0.12f);
            }
            if (i % 3 == 0) yield return new WaitForSeconds(0.06f);
        }
        yield return new WaitForSeconds(0.25f);
        Finish("The boards settle flat. No more creaks!");
    }

    // ── Inspect: tap every board, then fix the loose ones by hand ────────

    private IEnumerator InspectRoutine(float total)
    {
        float scale = total / (inspectScanTime + inspectFixTime);
        float scanT = inspectScanTime * scale, fixT = inspectFixTime * scale;
        Toast("Tapping each board to find loose ones...", scanT);
        ShowBar(true);
        float elapsed = 0f;
        int n = _boards.Count;
        float per = scanT / Mathf.Max(1, n);

        for (int i = 0; i < n; i++)
        {
            Board b = _boards[i];
            Vector3 at = b.sr.transform.position;
            Fx(ringFrames, at + new Vector3(0f, 0.05f, 0f), 6f);
            if (b.loose)
            {
                SunodGame.Core.vc_Sfx.Play("creak", 0.5f, 0.1f);
                Fx(exclaimFrames, at + new Vector3(0f, 0.55f, 0f), 2.5f, 0.1f);
                StartCoroutine(Bounce(b, 1, 0.14f));
            }
            else
            {
                SunodGame.Core.vc_Sfx.Play("clack", 0.4f, 0.08f);
            }
            yield return BarStep(per, elapsed / total, (elapsed + per) / total);
            elapsed += per;
        }

        Toast("Found them. Fixing by hand...", fixT);
        var loose = _boards.FindAll(b => b.loose);
        float fixPer = fixT / Mathf.Max(1, loose.Count);
        foreach (Board b in loose)
        {
            StartCoroutine(Nail(b, 0f));
            yield return BarStep(fixPer, elapsed / total, (elapsed + fixPer) / total);
            elapsed += fixPer;
        }
        Finish("Found every loose board and nailed it down.");
    }

    // ── Call: the carpenter walks over and fixes the floor ───────────────

    private IEnumerator CarpenterRoutine(float total)
    {
        Toast("You call Lolo's carpenter friend...", 3f);
        ShowBar(true);

        Vector3 to = _patchCenter + new Vector3(-1.7f, -0.9f, 0f);
        Vector3 from = to + new Vector3(-6f, 0f, 0f);
        SpawnHelper(from, 6);
        float walkTime = total * carpenterWalkTime / (carpenterWalkTime + carpenterFixTime);
        float fixTime = total - walkTime;

        yield return HelperWalk(from, to, walkTime, t => SetBar(t / total));

        Toast("The carpenter gets to work...", fixTime);
        var loose = _boards.FindAll(b => b.loose);
        float per = fixTime / Mathf.Max(1, loose.Count);
        float done = walkTime;
        foreach (Board b in loose)
        {
            StartCoroutine(Nail(b, 0f));
            yield return BarStep(per, done / total, (done + per) / total);
            done += per;
        }
        Finish("The carpenter fixed the floor. Thanks, Lolo's friend!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Waits 'seconds' while the progress bar eases from k0 to k1.</summary>
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

    /// <summary>One-pixel-step hop of a board (dir +1 = up, -1 = down), then back to rest.</summary>
    private IEnumerator Bounce(Board b, int dir, float seconds)
    {
        if (b.sr == null) yield break;
        b.sr.transform.localPosition = b.restLocal + new Vector3(0f, dir * 2f / 32f, 0f);
        yield return new WaitForSeconds(seconds * 0.5f);
        if (b.sr == null) yield break;
        b.sr.transform.localPosition = b.restLocal + new Vector3(0f, dir * 1f / 32f, 0f);
        yield return new WaitForSeconds(seconds * 0.5f);
        if (b.sr != null) b.sr.transform.localPosition = b.restLocal;
    }
}
