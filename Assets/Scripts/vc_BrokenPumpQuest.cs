using System.Collections;
using UnityEngine;

/// <summary>
/// R quest (R6, Elementary, Aling Rosa). The yard pump leaks from a cracked joint and the
/// handle is stuck. Skill system v2 (QUEST_REDESIGN.md): any sensible skill works, how well
/// it fits decides how fast it goes (and so the stars); nonsense skills only give a hint.
///
///   repair   R  best fit     tighten the cracked joint                          ~1s
///   push     R  same kind    work the stuck handle loose                         x3 (Lv2 x2, Lv3 x1)
///   build    R  same kind    build a bypass pipe round the crack                 5s (Lv3 / prepped: ~1s)
///   plan / survey / inspect  other kind: work out the fault, fix it by hand      8s
///   summon   E  other kind   call the plumber; he walks over and fixes it        ~10s
///   anything else: a hint, no effect
///
/// Prep: Inspect (pressed first) sweeps a magnifier along the pipe and rings the crack; Build
/// then snaps a short bypass on in one go. A second Inspect press is the slow fix above.
/// Preference: Push ("my late husband always fixed it by hand"). Bonus only.
/// While a slow fix runs, the best fit (repair) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_BrokenPumpQuest : vc_FitQuestKit
{
    [Header("Pump art")]
    [SerializeField] private SpriteRenderer pump;
    [SerializeField] private Sprite pumpBroken;
    [SerializeField] private Sprite pumpFixed;
    [SerializeField] private Sprite pumpBypass;
    [SerializeField] private Sprite waterDrop;

    [Header("Plumber (call path)")]
    [SerializeField] private Sprite[] plumberWalk;
    [SerializeField] private float plumberPpuScale = 1f;

    [Header("Effects")]
    [SerializeField] private Sprite magnifier;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] splashFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite exclaim;

    [Header("Legacy (unused)")]
    [SerializeField] private GameObject brokenPump;
    [SerializeField] private GameObject fixedPump;
    [SerializeField] private float animDuration = 0.5f;

    [Header("Timing (seconds, Elementary Lv1)")]
    [SerializeField] private float repairTime = 1f;
    [SerializeField] private int pushPresses = 3;
    [SerializeField] private float buildTime = 5f;
    [SerializeField] private float figureOutTime = 8f;
    [SerializeField] private float plumberWalkTime = 6f;
    [SerializeField] private float plumberFixTime = 4f;

    // where the cracked joint sits on the pump sprite (local units; pivot is bottom-centre)
    private static readonly Vector3 CrackLocal = new Vector3(-0.3f, 0.95f, 0f);
    private static readonly Vector3 HandleLocal = new Vector3(0.35f, 1.25f, 0f);

    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Aling Rosa",
        presses = 6,
        bestFitTag = "repair",
        bestFitSeconds = 1f,
        prepTag = "inspect",
        prepUnlocksTag = "build",
        prepLine = "You sweep the pipe with a magnifier...",
        preferredTag = "push",
        preferenceLine = "By hand, just like he used to. Salamat, anak!",
        fastLines = new[]
        {
            ("push", "One shove and it's free?! Just like he used to do it!"),
            ("build", "A whole new pipe that fast? I didn't know you could do that!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private int _pushes;
    private Vector3 _pumpBase;
    private float _dripClock, _sprayClock;
    private SpriteRenderer _crackRing;

    protected override Vector3 BarWorldPosition =>
        (pump != null ? pump.transform.position : transform.position) + new Vector3(-0.5f, 1.65f, 0f);

    private Vector3 Crack => pump != null ? pump.transform.TransformPoint(CrackLocal) : transform.position;
    private Vector3 Handle => pump != null ? pump.transform.TransformPoint(HandleLocal) : transform.position;

    private void Awake()
    {
        if (pump != null)
        {
            sortingLayer = pump.sortingLayerName;
            _pumpBase = pump.transform.localPosition;
        }
        if ((helperWalk == null || helperWalk.Length == 0) && plumberWalk != null) helperWalk = plumberWalk;
    }

    protected override void OnBegin()
    {
        _pushes = 0;
        _dripClock = _sprayClock = 0f;
        ClearSpawned();
        _crackRing = null;
        if (pump != null)
        {
            pump.sprite = pumpBroken;
            pump.transform.localPosition = _pumpBase;
        }

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Broken Pump",
            "Aling Rosa's yard pump leaks from a cracked joint, and the handle is stuck.",
            new[] { "Fix the water pump" });
    }

    // ── Idle life: the crack drips and spurts while the pump is broken ───

    private void Update()
    {
        if (!Started || Done || pump == null) return;
        _dripClock += Time.deltaTime;
        _sprayClock += Time.deltaTime;
        if (_dripClock >= 0.9f && waterDrop != null) { _dripClock = 0f; StartCoroutine(Drip()); }
        if (_sprayClock >= 2.2f)
        {
            _sprayClock = Random.Range(0f, 0.8f);
            Fx(splashFrames, Crack + new Vector3(-0.12f, 0.02f, 0f), 10f, 0.12f, pump.sortingOrder + 3);
            Sfx("splash", 0.25f, 0.15f);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        // best fit always wins, even over a slow fix that's already running
        if (d.HasTag("repair"))
        {
            StartWork(TightenRoutine(PathTime(repairTime)));
            return;
        }

        if (BlockedByWork()) return;

        if (d.HasTag("push")) { PushPress(); return; }
        if (d.HasTag("build")) { StartWork(BuildRoutine(PathTime(buildTime), CurrentInstant)); return; }
        if (d.HasTag("plan") || d.HasTag("survey") || d.HasTag("inspect"))
        {
            StartWork(FigureOutRoutine(PathTime(figureOutTime)));
            return;
        }
        if (d.HasTag("summon")) { StartWork(PlumberRoutine(PathTime(plumberWalkTime + plumberFixTime))); return; }

        Toast("That won't fix a pump. Something hands-on might.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (pump != null) pump.transform.localPosition = _pumpBase;
    }

    // ── Prep: Inspect sweeps a magnifier along the pipe and rings the crack ─

    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        Sfx("scan", 0.6f, 0.05f);
        SpriteRenderer glass = magnifier != null ? Spawn(magnifier, Crack + new Vector3(-0.7f, 0.7f, 0f), (pump != null ? pump.sortingOrder : 5) + 8, "Magnifier") : null;
        Vector3 a = Crack + new Vector3(-0.7f, 0.7f, 0f), b = Crack + new Vector3(0.7f, -0.2f, 0f);
        float t = 0f;
        while (t < 0.75f)
        {
            t += Time.deltaTime;
            if (glass != null) glass.transform.position = Snap(Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / 0.75f)));
            yield return null;
        }
        // back to the crack: found it
        if (glass != null) glass.transform.position = Snap(Crack + new Vector3(0.08f, 0.1f, 0f));
        Sfx("step_done", 0.45f, 0.05f);
        if (exclaim != null) Fx(new[] { exclaim }, Crack + new Vector3(0f, 0.5f, 0f), 2.5f, 0.15f, 40);
        Fx(ringFrames, Crack, 8f, 0f, 39);
        _crackRing = ringFrames != null && ringFrames.Length > 0
            ? Spawn(ringFrames[ringFrames.Length - 1], Crack, (pump != null ? pump.sortingOrder : 5) + 7, "CrackRing") : null;
        yield return new WaitForSeconds(0.25f);
        if (glass != null) Destroy(glass.gameObject);
    }

    // ── Repair (best fit): tighten the cracked joint ─────────────────────

    private IEnumerator TightenRoutine(float seconds)
    {
        Toast("You tighten the cracked joint...", Mathf.Max(1.2f, seconds));
        ShowBar(true);
        float t = 0f, tick = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            SetBar(t / seconds);
            if (tick >= 0.25f)
            {
                tick = 0f;
                Sfx("clack", 0.5f);
                Fx(sparkFrames, Crack, 14f, 0.1f, 40);
            }
            yield return null;
        }
        Fixed(pumpFixed, "You tighten the cracked joint. Good as new!");
    }

    // ── Push (same kind, x3): work the stuck handle loose ────────────────

    private void PushPress()
    {
        _pushes++;
        int need = PathPresses(pushPresses);
        StartCoroutine(Shake(pump != null ? pump.transform : null, _pumpBase, 0.35f, 2));
        Fx(dustFrames, Handle, 10f, 0.15f, 40);
        Sfx("scrape", 0.55f, 0.1f);

        if (_pushes < need)
        {
            Toast($"You heave the stuck handle... it budges ({_pushes}/{need})", 2f);
            return;
        }
        StartWork(HandleFreeRoutine());
    }

    private IEnumerator HandleFreeRoutine()
    {
        Toast("The handle pops free!", 1.2f);
        ShowBar(true);
        yield return Step(0.6f, 0f, 1f);
        Fx(splashFrames, pump != null ? pump.transform.position + new Vector3(0.25f, 0.2f, 0f) : transform.position, 10f, 0.1f, 40);
        Fixed(pumpFixed, "The handle pops free. Water flows again!");
    }

    // ── Build (same kind, 5s; prepped or Lv3 = one go): a bypass pipe ────

    private IEnumerator BuildRoutine(float seconds, bool snap)
    {
        Toast(snap ? "You snap a short bypass on in one go!" : "Building a bypass pipe around the crack...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        float t = 0f, tick = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            SetBar(t / seconds);
            if (tick >= (snap ? 0.18f : 0.45f))
            {
                tick = 0f;
                Sfx("hammer", 0.5f, 0.1f);
                Fx(hammerFrames, Crack + new Vector3(0.15f, 0.35f, 0f), 10f, 0f, 41);
                StartCoroutine(Shake(pump != null ? pump.transform : null, _pumpBase, 0.12f, 1));
            }
            yield return null;
        }
        Fx(sparkFrames, Crack, 14f, 0.15f, 40);
        Fixed(pumpBypass != null ? pumpBypass : pumpFixed,
              snap ? "A neat bypass, on in one go!" : "You build a bypass pipe around the crack. Slow, but it works.");
    }

    // ── Plan / Survey / Inspect (other kind, 8s): work out the fault ─────

    private IEnumerator FigureOutRoutine(float seconds)
    {
        Toast("Figuring out what's wrong...", Mathf.Max(1.5f, seconds * 0.6f));
        ShowBar(true);
        float look = seconds * 0.65f, t = 0f, tick = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            SetBar(t / seconds);
            if (tick >= 0.6f)
            {
                tick = 0f;
                if (t < look) Fx(ringFrames, Crack + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.4f), 0f), 8f, 0f, 40);
                else { Sfx("clack", 0.4f); Fx(sparkFrames, Crack, 14f, 0.1f, 40); }
            }
            if (t >= look && t - Time.deltaTime < look) Toast("Found it: a loose joint. Fixing it by hand...", 2f);
            yield return null;
        }
        Fixed(pumpFixed, "Found the fault and fixed it by hand.");
    }

    // ── Call (other kind, ~10s): the plumber walks over and fixes it ─────

    private IEnumerator PlumberRoutine(float seconds)
    {
        float walk = seconds * plumberWalkTime / Mathf.Max(0.01f, plumberWalkTime + plumberFixTime);
        float fix = seconds - walk;
        Toast("You call the plumber. He's on his way...", 3f);
        ShowBar(true);

        Vector3 to = pump != null ? pump.transform.position + new Vector3(-0.9f, -0.3f, 0f) : transform.position;
        Vector3 from = to + new Vector3(-6f, 0f, 0f);
        SpriteRenderer plumber = SpawnHelper(from, (pump != null ? pump.sortingOrder : 5) + 2);
        if (plumber != null) plumber.transform.localScale = Vector3.one * plumberPpuScale;

        yield return HelperWalk(from, to, walk, t => SetBar(t / seconds));

        Toast("The plumber gets to work...", fix);
        float t2 = 0f, tick = 0f;
        while (t2 < fix)
        {
            t2 += Time.deltaTime;
            tick += Time.deltaTime;
            SetBar((walk + t2) / seconds);
            if (tick >= 0.45f)
            {
                tick = 0f;
                Sfx("hammer", 0.4f, 0.1f);
                StartCoroutine(Shake(pump != null ? pump.transform : null, _pumpBase, 0.15f, 1));
            }
            yield return null;
        }
        Fixed(pumpFixed, "The plumber fixed it. Thanks for calling him!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void Fixed(Sprite result, string msg)
    {
        if (pump != null)
        {
            pump.transform.localPosition = _pumpBase;
            if (result != null) pump.sprite = result;
        }
        if (_crackRing != null) Destroy(_crackRing.gameObject);
        Fx(splashFrames, pump != null ? pump.transform.position + new Vector3(0.3f, 0.15f, 0f) : transform.position, 10f, 0.1f, 40);
        Finish(msg);
    }

    private IEnumerator Drip()
    {
        var go = new GameObject("Drip");
        go.transform.SetParent(pump.transform, false);
        go.transform.localPosition = CrackLocal;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = waterDrop;
        sr.sortingLayerName = pump.sortingLayerName;
        sr.sortingOrder = pump.sortingOrder + 1;
        float t = 0f;
        while (t < 0.5f && go != null)
        {
            t += Time.deltaTime;
            go.transform.localPosition = new Vector3(CrackLocal.x, CrackLocal.y - Mathf.Round(t * 1.8f * 22f) / 22f, 0f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
