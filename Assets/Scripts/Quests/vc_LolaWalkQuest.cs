using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S quest (S8). Help Lola Ising get to her rocking chair. Any APPROPRIATE skill works - how well
/// it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   guide    S  best fit     Lola takes your arm; you walk her to her chair             ~2s + walk
///   heal     S  workaround   soothe her sore knees, then she walks over smiling          ~5s
///   teach    S  workaround   show her an easier way to stand and walk, then she goes     ~5s
///   build    R  off-letter   put up a handrail along the way; she walks leaning on it    ~8s
///   summon   E  off-letter   call her grandson; he walks over and escorts her            ~10s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (guide) takes over at once: Lola stops where she is
/// and follows the player from there.
/// </summary>
[DisallowMultipleComponent]
public class vc_LolaWalkQuest : vc_FitQuestBase
{
    [Header("Lola")]
    [SerializeField] private Transform lola;
    [Tooltip("6 walk frames (with cane).")]
    [SerializeField] private Sprite[] lolaWalk;
    [SerializeField] private Sprite lolaSit;

    [Header("Home")]
    [Tooltip("Where Lola stands before sitting.")]
    [SerializeField] private Transform home;
    [SerializeField] private Transform chair;
    [SerializeField] private Vector3 seatOffset = new Vector3(0f, 0.15f, 0f);

    [Header("Handrail (build path)")]
    [SerializeField] private Sprite railPiece;
    [SerializeField] private int railPieces = 6;

    [Header("Effects")]
    [SerializeField] private Sprite[] heartFrames;
    [SerializeField] private Sprite[] healFrames;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite bubbleQuestion;
    [SerializeField] private Sprite bubbleDots;
    [SerializeField] private Sprite bubblePitch;
    [SerializeField] private Sprite bubbleSmile;

    [Header("Timing (seconds)")]
    [SerializeField] private float healTreatTime = 1.6f;
    [SerializeField] private float teachDemoTime = 1.8f;
    [SerializeField] private float railTime = 4.4f;
    [SerializeField] private float routeWalkTime = 3.2f;
    [SerializeField] private float grandsonWalkTime = 5.6f;
    [SerializeField] private float guideSpeed = 2.6f;

    // Skill system v2 (QUEST_REDESIGN.md): Build (prep) puts a handrail up along the wall, then Teach
    // shows her how to use it and she walks over at once. Preference: Heal (her knees).
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Lola Ising",
        presses = 4,
        bestFitTag = "guide",
        bestFitSeconds = 2f,
        prepTag = "build",
        prepUnlocksTag = "teach",
        prepLine = "A handrail goes up along the wall...",
        preferredTag = "heal",
        preferenceLine = "Ay, my knees feel so light. Salamat, hijo!",
        fastLines = new[]
        {
            ("heal", "My knees don't hurt at all! You have magic hands."),
            ("teach", "I can do it myself now! Thank you, anak."),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private bool _railUp;
    private SpriteRenderer _lsr;
    private Vector3 _start;
    private Vector3 _chairRest;
    private bool _guiding;
    private bool _seated;
    private float _animT, _heartClock, _idleClock;
    private float _guideStartDist;
    private Transform _player;
    private Vector3 _barPos;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Coroutine> _routes = new List<Coroutine>();

    protected override Vector3 BarWorldPosition => _barPos;

    private void Awake()
    {
        if (lola != null)
        {
            _lsr = lola.GetComponent<SpriteRenderer>();
            _start = lola.position;
        }
        if (chair != null) _chairRest = chair.position;
    }

    protected override void OnBegin()
    {
        StopRoutes();
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        _guiding = false; _seated = false; _railUp = false;
        _animT = 0f; _heartClock = 0f; _idleClock = 0f;
        if (lola != null) lola.position = _start;
        if (_lsr != null) { _lsr.sprite = lolaWalk != null && lolaWalk.Length > 0 ? lolaWalk[0] : _lsr.sprite; }
        if (chair != null) chair.position = _chairRest;
        _barPos = _start + new Vector3(-0.5f, 1.6f, 0f);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Walk Lola Home",
            "Lola Ising: her knees ache and her rocking chair is across the room. Get her there. She says her knees hurt the most.",
            new[] { "Get Lola to her chair" });
    }

    protected override void OnWorkCancelled()
    {
        StopRoutes();
        if (_lsr != null && !_seated && lolaWalk != null && lolaWalk.Length > 0) _lsr.sprite = lolaWalk[0];
    }

    private void StopRoutes()
    {
        foreach (Coroutine r in _routes) if (r != null) StopCoroutine(r);
        _routes.Clear();
    }

    // ── Idle life and guided walking ─────────────────────────────────────

    private void Update()
    {
        if (!Started || Done || lola == null) return;

        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }

        if (_guiding && _player != null && !_seated)
        {
            Vector3 lp = lola.position, pp = _player.position;
            float d = Vector2.Distance(lp, pp);
            if (d > 1.1f)
            {
                lola.position = Snap(Vector3.MoveTowards(lp, new Vector3(pp.x, pp.y, lp.z), guideSpeed * Time.deltaTime));
                _animT += Time.deltaTime;
                Frame(_animT * 7f);
            }
            else if (lolaWalk != null && lolaWalk.Length > 0) _lsr.sprite = lolaWalk[0];

            _heartClock += Time.deltaTime;
            if (_heartClock >= 1.4f)
            {
                _heartClock = 0f;
                Fx(heartFrames, lola.position + new Vector3(0f, 0.95f, 0f), 5f, 0.3f);
            }

            float remaining = Vector2.Distance(lola.position, home.position);
            _barPos = lola.position + new Vector3(-0.5f, 1.6f, 0f);
            MoveBar(_barPos);
            SetBar(1f - Mathf.Clamp01(remaining / Mathf.Max(0.5f, _guideStartDist)));
            if (remaining <= 0.9f)
            {
                _guiding = false;
                ShowBar(false);
                HideGoal();
                _routes.Add(StartCoroutine(SitAndFinish("You walked Lola home. She's so grateful!")));
            }
            return;
        }

        if (!Working && !_seated)
        {
            _idleClock += Time.deltaTime;
            if (_idleClock >= 3f)
            {
                _idleClock = 0f;
                if (bubbleDots != null) Fx(new[] { bubbleDots }, lola.position + new Vector3(0.1f, 1.0f, 0f), 1.4f, 0.2f);
                StartCoroutine(Wince());
            }
        }
    }

    private IEnumerator Wince()
    {
        Vector3 b = lola.position;
        lola.position = b + new Vector3(0f, -1f / 22f, 0f);
        yield return new WaitForSeconds(0.2f);
        if (!_guiding && !Working && !_seated) lola.position = b;
    }

    private void Frame(float f)
    {
        if (_lsr == null || lolaWalk == null || lolaWalk.Length == 0) return;
        _lsr.sprite = lolaWalk[(int)f % lolaWalk.Length];
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (_seated) return;

        if (d.HasTag("guide"))
        {
            if (_guiding) return;
            StartGuide();
            return;
        }

        if (_guiding) { Toast("Lola is walking with you.", 1.5f); return; }
        if (BlockedByWork()) return;

        if (d.HasTag("heal")) { StartWork(HealRoutine(PathTime(healTreatTime + routeWalkTime))); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine(PathTime(teachDemoTime + routeWalkTime), CurrentInstant)); return; }
        if (d.HasTag("build")) { StartWork(RailRoutine(PathTime(railTime + routeWalkTime))); return; }
        if (d.HasTag("summon")) { StartWork(GrandsonRoutine(PathTime(grandsonWalkTime + 1f + routeWalkTime))); return; }

        Toast("Lola needs a kind helper.", 2.2f);
    }

    private void StartGuide()
    {
        CancelWork();
        _guiding = true;
        _guideStartDist = Vector2.Distance(lola.position, home.position);
        Toast("Lola takes your arm. Walk her to her rocking chair (follow the arrow).", 3.5f);
        ShowGoal(chair != null ? chair : home);
        Fx(heartFrames, lola.position + new Vector3(0f, 0.95f, 0f), 5f, 0.3f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.5f);
        ShowBar(true);
    }

    // ── Heal ─────────────────────────────────────────────────────────────

    private IEnumerator HealRoutine(float seconds)
    {
        float sc = seconds / (healTreatTime + routeWalkTime);
        float treatSecs = healTreatTime * sc, walkSecs = routeWalkTime * sc;
        Toast("Soothing Lola's sore knees...", Mathf.Min(2.5f, seconds));
        ShowBar(true);
        MoveBar(lola.position + new Vector3(-0.5f, 1.6f, 0f));
        float total = treatSecs + walkSecs, t = 0f, spark = 0f;
        while (t < treatSecs)
        {
            t += Time.deltaTime;
            spark += Time.deltaTime;
            SetBar(t / total);
            if (spark >= 0.3f)
            {
                spark = 0f;
                Fx(healFrames, lola.position + new Vector3(Random.Range(-0.3f, 0.3f), -0.35f, 0f), 8f, 0.4f);
                SunodGame.Core.vc_Sfx.Play("chime", 0.3f, 0.15f);
            }
            yield return null;
        }
        Fx(heartFrames, lola.position + new Vector3(0f, 0.95f, 0f), 5f, 0.4f);
        Toast("Her knees feel better. Off she goes!", walkSecs);
        yield return WalkRoute(walkSecs, null, k => SetBar((treatSecs + k * walkSecs) / total), 0f);
        yield return SitAndFinish("Her knees feel better. She walks to her chair smiling!");
    }

    // ── Teach ────────────────────────────────────────────────────────────

    private IEnumerator TeachRoutine(float seconds, bool oneGo)
    {
        float sc = seconds / (teachDemoTime + routeWalkTime);
        float demoSecs = teachDemoTime * sc, walkSecs = routeWalkTime * sc;
        Toast(oneGo ? "You show her how to use the new handrail..." : "Showing Lola an easier way to stand...", Mathf.Min(2.5f, seconds));
        ShowBar(true);
        MoveBar(lola.position + new Vector3(-0.5f, 1.6f, 0f));
        float total = demoSecs + walkSecs, t = 0f;
        Sprite[] steps = { bubbleQuestion, bubblePitch, bubbleSmile };
        float per = demoSecs / steps.Length;
        for (int s = 0; s < steps.Length; s++)
        {
            Fx(new[] { steps[s] }, lola.position + new Vector3(0.1f, 1.0f, 0f), 1.3f, 0.25f);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.55f, 0.1f);
            // lean forward, push up, stand tall
            Vector3 b = lola.position;
            lola.position = b + new Vector3(0f, -1f / 22f, 0f);
            yield return Wait(per * 0.4f, t, total);
            t += per * 0.4f;
            lola.position = b + new Vector3(0f, 2f / 22f, 0f);
            yield return Wait(per * 0.3f, t, total);
            t += per * 0.3f;
            lola.position = b;
            yield return Wait(per * 0.3f, t, total);
            t += per * 0.3f;
        }
        Toast(oneGo ? "She grips the rail and goes at once!" : "She's got it. Off she goes!", walkSecs);
        yield return WalkRoute(walkSecs, null, k => SetBar((demoSecs + k * walkSecs) / total), 0f);
        yield return SitAndFinish(oneGo ? "She used the handrail and walked over in one go!" : "You showed her an easier way to stand and walk. She made it!");
    }

    // ── Build: handrail along the way ────────────────────────────────────

    private IEnumerator RailRoutine(float seconds)
    {
        float sc = seconds / (railTime + routeWalkTime);
        float railSecs = _railUp ? 0f : railTime * sc, walkSecs = routeWalkTime * sc;
        Toast(_railUp ? "The handrail is already up..." : "Putting up a handrail...", Mathf.Max(1f, railSecs));
        ShowBar(true);
        MoveBar(lola.position + new Vector3(-0.5f, 1.6f, 0f));
        float total = Mathf.Max(0.1f, railSecs + walkSecs);
        Vector3 leg = LegEnd();
        float x0 = _start.x + 0.9f, x1 = leg.x - 0.5f, y = _start.y - 0.6f;
        float per = railSecs / Mathf.Max(1, railPieces), t = 0f;
        for (int i = 0; i < railPieces && !_railUp; i++)
        {
            float x = Mathf.Lerp(x0, x1, railPieces > 1 ? i / (railPieces - 1f) : 0f);
            var go = new GameObject("Rail");
            go.transform.SetParent(transform, true);
            go.transform.position = Snap(new Vector3(x, y, 0f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = railPiece; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 11;
            _spawned.Add(go);
            Fx(hammerFrames, go.transform.position + new Vector3(0.2f, 0.6f, 0f), 8f);
            StartCoroutine(RailPop(go.transform));
            yield return new WaitForSeconds(per * 0.4f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.55f, 0.08f);
            Fx(sparkFrames, go.transform.position + new Vector3(0f, 0.2f, 0f), 12f, 0.1f);
            yield return Wait(per * 0.6f, t + per * 0.4f, total);
            t += per;
            SetBar(t / total);
        }
        _railUp = true;
        Toast("Lola holds the rail and walks over...", walkSecs);
        yield return WalkRoute(walkSecs, null, k => SetBar((railSecs + k * walkSecs) / total), 0f);
        yield return SitAndFinish("You built a handrail and Lola used it all the way!");
    }

    private IEnumerator RailPop(Transform tr)
    {
        Vector3 b = tr.position;
        tr.position = b + new Vector3(0f, 6f / 22f, 0f);
        yield return new WaitForSeconds(0.06f);
        if (tr != null) tr.position = b + new Vector3(0f, 2f / 22f, 0f);
        yield return new WaitForSeconds(0.06f);
        if (tr != null) tr.position = b;
        Fx(dustFrames, b + new Vector3(0f, -0.1f, 0f), 12f, 0.1f);
    }

    // ── Prep (Build): the handrail goes up along the wall; Teach then uses it at once ──

    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        Vector3 leg = LegEnd();
        float x0 = _start.x + 0.9f, x1 = leg.x - 0.5f, y = _start.y - 0.6f;
        float gap = 0.8f / Mathf.Max(1, railPieces);
        for (int i = 0; i < railPieces; i++)
        {
            float x = Mathf.Lerp(x0, x1, railPieces > 1 ? i / (railPieces - 1f) : 0f);
            var go = new GameObject("Rail");
            go.transform.SetParent(transform, true);
            go.transform.position = Snap(new Vector3(x, y, 0f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = railPiece; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 11;
            _spawned.Add(go);
            Fx(hammerFrames, go.transform.position + new Vector3(0.2f, 0.6f, 0f), 8f);
            StartCoroutine(RailPop(go.transform));
            SunodGame.Core.vc_Sfx.Play("hammer", 0.45f, 0.08f);
            yield return new WaitForSeconds(gap);
        }
        _railUp = true;
        Fx(sparkFrames, new Vector3((x0 + x1) * 0.5f, y + 0.3f, 0f), 12f, 0.2f);
        yield return new WaitForSeconds(0.2f);
    }

    // ── Call: grandson walks over and escorts her ────────────────────────

    private IEnumerator GrandsonRoutine(float seconds)
    {
        float sc = seconds / (grandsonWalkTime + 1f + routeWalkTime);
        float comeSecs = grandsonWalkTime * sc, pauseSecs = 1f * sc, escortSecs = routeWalkTime * sc;
        Toast("You call Lola's grandson...", Mathf.Min(3f, seconds));
        ShowBar(true);
        MoveBar(lola.position + new Vector3(-0.5f, 1.6f, 0f));
        float total = comeSecs + pauseSecs + escortSecs + 0.2f, t = 0f;

        Vector3 to = lola.position + new Vector3(-0.8f, -0.05f, 0f);
        Vector3 from = to + new Vector3(-6.5f, 0f, 0f);
        SpawnHelper(from, 10);
        yield return HelperWalk(from, to, comeSecs, tt => SetBar(tt / total));
        t = comeSecs;

        Fx(heartFrames, lola.position + new Vector3(0f, 0.95f, 0f), 5f, 0.4f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.5f);
        Toast("\"Lola! Let me help you.\"", 1.2f);
        yield return Wait(pauseSecs, t, total);
        t += pauseSecs;

        Toast("Her grandson walks her to her chair...", escortSecs);
        Transform helperTr = HelperTransform();
        Vector3 offset = new Vector3(-0.8f, -0.05f, 0f);
        yield return WalkRoute(escortSecs, p => { if (helperTr != null) helperTr.position = Snap(p + offset); }, k => SetBar((t + k * escortSecs) / total), 0f);
        yield return SitAndFinish("Her grandson helped her home. Thanks for calling him!");
    }

    private Transform HelperTransform()
    {
        // the base spawns the helper under this transform as "Helper"
        Transform h = transform.Find("Helper");
        return h;
    }

    // ── Shared movement ──────────────────────────────────────────────────

    private Vector3 LegEnd() => new Vector3(home.position.x, _start.y, 0f);

    /// <summary>Walks Lola from where she is along start-level then up to her chair, in 'seconds'.</summary>
    private IEnumerator WalkRoute(float seconds, System.Action<Vector3> onMove, System.Action<float> onProgress, float delay)
    {
        Vector3 a = lola.position;
        Vector3 b = new Vector3(home.position.x, a.y, 0f);
        Vector3 c = home.position;
        float l1 = Vector3.Distance(a, b), l2 = Vector3.Distance(b, c), len = Mathf.Max(0.01f, l1 + l2);
        float t = 0f, step = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            float d = k * len;
            Vector3 p = d <= l1 ? Vector3.Lerp(a, b, l1 > 0.001f ? d / l1 : 1f) : Vector3.Lerp(b, c, l2 > 0.001f ? (d - l1) / l2 : 1f);
            lola.position = Snap(p);
            Frame(t * 7f);
            step += Time.deltaTime;
            if (step >= 0.32f) { step = 0f; SunodGame.Core.vc_Sfx.Play("clack", 0.2f, 0.12f); }
            onMove?.Invoke(lola.position);
            onProgress?.Invoke(k);
            yield return null;
        }
        lola.position = c;
        if (lolaWalk != null && lolaWalk.Length > 0) _lsr.sprite = lolaWalk[0];
    }

    private IEnumerator SitAndFinish(string msg)
    {
        _seated = true;
        ShowBar(false);
        Vector3 seat = (chair != null ? chair.position : home.position) + seatOffset;
        lola.position = seat;
        if (_lsr != null && lolaSit != null) { _lsr.sprite = lolaSit; _lsr.sortingOrder = 11; }
        Fx(heartFrames, seat + new Vector3(0f, 0.95f, 0f), 5f, 0.5f);
        Fx(heartFrames, seat + new Vector3(-0.4f, 0.7f, 0f), 5f, 0.4f);
        Fx(sparkFrames, seat + new Vector3(0.4f, 0.8f, 0f), 12f, 0.3f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.6f);
        // the chair rocks gently
        for (int i = 0; i < 4; i++)
        {
            Vector3 off = new Vector3((i % 2 == 0 ? 1 : -1) / 22f, 0f, 0f);
            if (chair != null) chair.position = _chairRest + off;
            lola.position = seat + off;
            yield return new WaitForSeconds(0.18f);
        }
        if (chair != null) chair.position = _chairRest;
        lola.position = seat;
        Finish(msg);
    }

    private IEnumerator Wait(float seconds, float t0, float total)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetBar((t0 + t) / total);
            yield return null;
        }
    }
}
