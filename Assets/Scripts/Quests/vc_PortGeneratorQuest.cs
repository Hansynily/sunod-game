using System.Collections;
using UnityEngine;

/// <summary>
/// R quest (R3). Restart the port's dead generator. Any APPROPRIATE skill works - how well it fits
/// decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   repair   R  best fit     swap the fried part; it roars to life, the dock lamps light    ~2s
///   push     R  workaround   yank the pull-start, one per press; the third one catches      x3
///   build    R  workaround   rig a backup cable from the fuel line                          5s
///   unlock   I  off-letter   diagnose the fault (gauges flicker), then fix it                8s
///   summon   E  off-letter   the port mechanic boats over and fixes it                      10s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (repair) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_PortGeneratorQuest : vc_FitQuestBase
{
    [Header("Generator")]
    [SerializeField] private SpriteRenderer generator;
    [SerializeField] private Sprite generatorOff;
    [SerializeField] private Sprite[] generatorOn;
    [SerializeField] private SpriteRenderer warnLight;
    [SerializeField] private Sprite warnRed;
    [SerializeField] private Sprite warnDark;
    [SerializeField] private Sprite warnGreen;
    [SerializeField] private SpriteRenderer fuelDrum;

    [Header("Dock lamps")]
    [SerializeField] private SpriteRenderer[] dockLamps;
    [SerializeField] private Sprite lampOff;
    [SerializeField] private Sprite lampOn;
    [SerializeField] private SpriteRenderer[] lampGlows;

    [Header("Effects")]
    [SerializeField] private Sprite[] smokeFrames;
    [SerializeField] private Sprite[] exhaustFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] gaugeFrames;
    [SerializeField] private Sprite partIcon;
    [SerializeField] private Sprite cable;
    [SerializeField] private Sprite questionBubble;
    [SerializeField] private Sprite ideaBubble;

    [Header("Mechanic (summon path)")]
    [SerializeField] private SpriteRenderer boat;
    [SerializeField] private Vector3 boatOffset = new Vector3(0f, -3.6f, 0f);
    [SerializeField] private Transform landing;

    [Header("Timing (seconds)")]
    [SerializeField] private float repairTime = 2f;
    [SerializeField] private int pullPresses = 3;
    [SerializeField] private float buildTime = 5f;
    [SerializeField] private float solveTime = 8f;
    [SerializeField] private float callTime = 10f;

    // Skill system v2 (QUEST_REDESIGN.md): Solve (prep) finds the fault as the gauges flicker and settle,
    // then Build rigs the backup line and the generator starts at once. Preference: Build.
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Engr. Paz",
        presses = 4,
        bestFitTag = "repair",
        bestFitSeconds = 2f,
        prepTag = "unlock",
        prepUnlocksTag = "build",
        prepLine = "The gauges flicker and settle on the fault...",
        preferredTag = "build",
        preferenceLine = "A backup line so it never dies again. Exactly what I asked for!",
        fastLines = new[]
        {
            ("push", "First pull?! The whole port's lit up!"),
            ("build", "A backup line already? It'll never die again."),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private Vector3 _genRest, _boatRest;
    private bool _running;
    private int _pulls, _pullUnits;
    private float _puffClock, _blinkClock, _animClock;
    private GameObject _cable;

    protected override Vector3 BarWorldPosition => (generator != null ? generator.transform.position : transform.position) + new Vector3(-0.5f, 2f, 0f);

    private void Awake()
    {
        if (generator != null) _genRest = generator.transform.localPosition;
        if (boat != null) _boatRest = boat.transform.position;
    }

    protected override void OnBegin()
    {
        if (_cable != null) Destroy(_cable);
        _running = false;
        _pulls = 0;
        _pullUnits = 0;
        if (generator != null) { generator.sprite = generatorOff; generator.transform.localPosition = _genRest; }
        if (warnLight != null) warnLight.sprite = warnRed;
        foreach (var l in dockLamps) if (l != null) l.sprite = lampOff;
        foreach (var g in lampGlows) if (g != null) g.enabled = false;
        if (boat != null) { boat.transform.position = _boatRest + boatOffset; boat.enabled = false; }

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Port Generator",
            "The port's generator died and the dock lamps are dark. Engr. Paz needs it running, and a backup line so it never dies again would be ideal.",
            new[] { "Restart the generator" });
    }

    // ── Idle life: dead engine smokes, warning light blinks; once running it rumbles ──

    private void Update()
    {
        if (generator == null) return;
        if (_running)
        {
            _animClock += Time.deltaTime;
            if (_animClock >= 0.08f)
            {
                _animClock = 0f;
                if (generatorOn != null && generatorOn.Length > 1)
                    generator.sprite = generator.sprite == generatorOn[0] ? generatorOn[1] : generatorOn[0];
            }
            _puffClock -= Time.deltaTime;
            if (_puffClock <= 0f) { _puffClock = 0.35f; Fx(exhaustFrames, Exhaust(), 6f, 0.6f, 22); }
            return;
        }
        if (!Started || Done) return;
        _puffClock -= Time.deltaTime;
        if (_puffClock <= 0f) { _puffClock = Random.Range(1.1f, 1.6f); Fx(smokeFrames, Exhaust(), 4f, 0.5f, 22); }
        _blinkClock -= Time.deltaTime;
        if (_blinkClock <= 0f && warnLight != null)
        {
            _blinkClock = 0.45f;
            warnLight.sprite = warnLight.sprite == warnRed ? warnDark : warnRed;
        }
    }

    private Vector3 Exhaust()
    {
        Bounds b = generator.bounds;
        return new Vector3(b.min.x + b.size.x * 0.22f, b.max.y + 0.1f, 0f);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("repair")) { StartWork(RepairRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("push")) { PullPress(); return; }
        if (d.HasTag("build")) { StartWork(BuildRoutine(PathTime(buildTime), CurrentInstant)); return; }
        if (d.HasTag("unlock")) { StartWork(SolveRoutine(PathTime(solveTime))); return; }
        if (d.HasTag("summon")) { StartWork(CallRoutine(PathTime(callTime))); return; }
        Toast("This needs hands-on work.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (generator != null) generator.transform.localPosition = _genRest;
        if (boat != null) boat.enabled = false;
    }

    private IEnumerator RepairRoutine()
    {
        Toast("You swap out the fried part...", repairTime);
        ShowBar(true);
        Vector3 c = generator.bounds.center;
        if (partIcon != null) Fx(new[] { partIcon }, c + new Vector3(0.4f, 0.4f, 0f), 1.5f, 0.4f, 24);
        for (int i = 0; i < 3; i++)
        {
            Fx(hammerFrames, c + new Vector3(-0.3f + i * 0.3f, 0.3f, 0f), 8f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.6f, 0.1f);
            StartCoroutine(Shake(generator.transform, _genRest, 0.15f, 1));
            yield return BarStep(0.45f, i * 0.45f / repairTime, (i + 1) * 0.45f / repairTime);
        }
        Fx(sparkFrames, c, 12f, 0.15f);
        yield return BarStep(repairTime - 1.35f, 1.35f / repairTime, 1f);
        StartEngine("New part in. The generator roars to life!");
    }

    // Push (x3 press): 3 presses at Lv1, 2 at Lv2, 1 at Lv3 / prepped. Each press yanks its share of
    // the pulls; the last one runs through StartWork, yanks the rest in a row and the engine catches.
    private void PullPress()
    {
        int total = Mathf.Max(1, pullPresses);
        if (_pullUnits >= total) return;
        _pulls++;
        int need = Mathf.Clamp(PathPresses(total), 1, total);
        int remaining = total - _pullUnits;
        int pressesLeft = Mathf.Max(1, need - _pulls + 1);
        int count = _pulls >= need ? remaining : Mathf.CeilToInt(remaining / (float)pressesLeft);
        _pullUnits += count;

        if (_pullUnits >= total)
        {
            StartWork(PullFinishRoutine(count, CurrentInstant));
            return;
        }
        Toast($"You yank the cord... it sputters ({_pullUnits}/{total})", 2f);
        SunodGame.Core.vc_Sfx.Play("engine", 0.25f, 0.2f);
        Fx(smokeFrames, Exhaust(), 6f, 0.5f, 22);
        for (int n = 0; n < count; n++) StartCoroutine(Yank(n * 0.3f));
    }

    private IEnumerator Yank(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (generator == null) yield break;
        StartCoroutine(Shake(generator.transform, _genRest, 0.3f, 2));
        SunodGame.Core.vc_Sfx.Play("scrape", 0.6f, 0.1f);
    }

    private IEnumerator PullFinishRoutine(int count, bool oneGo)
    {
        Toast(oneGo ? "You yank the cord once, hard..." : (count > 1 ? "You yank the cord again and again..." : "One more yank..."), 2f);
        ShowBar(true);
        for (int n = 0; n < count; n++)
        {
            StartCoroutine(Yank(0f));
            SetBar((n + 1f) / count);
            yield return new WaitForSeconds(count > 1 ? 0.3f : 0f);
        }
        yield return new WaitForSeconds(0.4f);
        StartEngine(oneGo ? "First pull - it catches! The lights come on." : (count > 1 ? "A few hard pulls in a row - it catches! The lights come on." : "Third pull - it catches! The lights come on."));
    }

    private IEnumerator BuildRoutine(float seconds, bool oneGo)
    {
        Toast(oneGo ? "You rig the backup line in one go!" : "You rig a backup cable from the fuel line...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        Vector3 from = fuelDrum != null ? fuelDrum.bounds.center : generator.bounds.center + Vector3.left * 2f;
        Vector3 to = generator.bounds.center + new Vector3(-0.8f, -0.3f, 0f);
        if (cable != null)
        {
            _cable = new GameObject("BackupCable");
            _cable.transform.SetParent(transform, true);
            var sr = _cable.AddComponent<SpriteRenderer>();
            sr.sprite = cable; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 5;
            _cable.transform.position = Snap(Vector3.Lerp(from, to, 0.5f));
        }
        float tail = Mathf.Min(0.8f, seconds * 0.2f);
        float t = 0f, lay = seconds - tail;
        float full = Vector3.Distance(from, to) / (cable != null ? cable.bounds.size.x : 1f);
        float sparkEvery = Mathf.Min(0.6f, lay / 3f);
        while (t < lay)
        {
            t += Time.deltaTime;
            float k = Mathf.Round(t / lay * 8f) / 8f;
            if (_cable != null)
            {
                _cable.transform.position = Snap(Vector3.Lerp(from, Vector3.Lerp(from, to, k), 0.5f));
                _cable.transform.localScale = new Vector3(Mathf.Max(0.05f, full * k), 1f, 1f);
            }
            if (Mathf.Repeat(t, sparkEvery) < Time.deltaTime)
            {
                Fx(sparkFrames, Vector3.Lerp(from, to, t / lay), 12f, 0.1f);
                SunodGame.Core.vc_Sfx.Play("zap", 0.3f, 0.15f);
            }
            SetBar(t / seconds);
            yield return null;
        }
        yield return BarStep(tail, lay / seconds, 1f);
        StartEngine(oneGo ? "The backup line's in and the generator starts at once!" : "The backup cable works. Power's on!");
    }

    private IEnumerator SolveRoutine(float seconds)
    {
        Toast("You study the gauges to find the fault...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        Vector3 c = generator.bounds.center;
        float study = seconds * 0.75f, t = 0f;
        float step = seconds * 0.05f, hit = seconds * 0.1f;
        int f = 0;
        if (questionBubble != null) Fx(new[] { questionBubble }, c + new Vector3(0f, 1.3f, 0f), 1f, 0.2f, 24);
        while (t < study)
        {
            if (gaugeFrames != null && gaugeFrames.Length > 0)
                Fx(new[] { gaugeFrames[f++ % gaugeFrames.Length] }, c + new Vector3(-0.6f + (f % 3) * 0.6f, 0.9f, 0f), 2.5f, 0f, 24);
            SunodGame.Core.vc_Sfx.Play("timer_tick", 0.35f, 0.1f);
            yield return BarStep(step, t / seconds, (t + step) / seconds);
            t += step;
        }
        if (ideaBubble != null) Fx(new[] { ideaBubble }, c + new Vector3(0f, 1.3f, 0f), 1.2f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("idea", 0.6f);
        for (int i = 0; i < 2; i++)
        {
            Fx(hammerFrames, c + new Vector3(0.2f * i, 0.3f, 0f), 8f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.6f, 0.1f);
            yield return BarStep(hit, t / seconds, (t + hit) / seconds);
            t += hit;
        }
        StartEngine("Found it: a clogged fuel filter. Fixed!");
    }

    private IEnumerator CallRoutine(float seconds)
    {
        Toast("You radio the port mechanic...", 3f);
        ShowBar(true);
        float t = 0f, sail = seconds * 0.35f, walk = seconds * 0.3f;
        if (boat != null)
        {
            boat.enabled = true;
            Vector3 from = _boatRest + boatOffset;
            while (t < sail)
            {
                t += Time.deltaTime;
                boat.transform.position = Snap(Vector3.Lerp(from, _boatRest, Mathf.SmoothStep(0f, 1f, t / sail)) + new Vector3(0f, Mathf.Round(Mathf.Sin(t * 6f)) / 22f, 0f));
                SetBar(t / seconds);
                yield return null;
            }
        }
        else { yield return BarStep(sail, 0f, sail / seconds); t = sail; }
        Vector3 land = landing != null ? landing.position : _boatRest + Vector3.left;
        Vector3 at = generator.bounds.center + new Vector3(1.2f, -0.5f, 0f);
        SpawnHelper(land, 7);
        float w0 = t;
        yield return HelperWalk(land, at, walk, x => SetBar((w0 + x) / seconds));
        t += walk;
        float fix = Mathf.Max(0.5f, seconds - t);
        Toast("The mechanic gets to work...", fix);
        int hits = 4;
        for (int i = 0; i < hits; i++)
        {
            Fx(hammerFrames, generator.bounds.center + new Vector3(0.5f, 0.3f, 0f), 8f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.6f, 0.1f);
            StartCoroutine(Shake(generator.transform, _genRest, 0.15f, 1));
            yield return BarStep(fix / hits, t / seconds, (t + fix / hits) / seconds);
            t += fix / hits;
        }
        StartEngine("The mechanic got it running. Thanks for calling!");
    }

    // ── Prep: Solve finds the fault; the gauges flicker, then settle ─────

    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        Vector3 gc = generator != null ? generator.bounds.center : transform.position;
        for (int n = 0; n < 4; n++)
        {
            if (gaugeFrames != null && gaugeFrames.Length > 0)
                Fx(new[] { gaugeFrames[n % gaugeFrames.Length] }, gc + new Vector3(-0.6f + (n % 3) * 0.6f, 0.9f, 0f), 4f, 0f, 24);
            SunodGame.Core.vc_Sfx.Play("timer_tick", 0.35f, 0.1f);
            yield return new WaitForSeconds(0.15f);
        }
        if (ideaBubble != null) Fx(new[] { ideaBubble }, gc + new Vector3(0f, 1.3f, 0f), 1.2f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("idea", 0.6f);
        yield return new WaitForSeconds(0.4f);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void StartEngine(string msg)
    {
        _running = true;
        _puffClock = 0f;
        if (generator != null) generator.transform.localPosition = _genRest;
        if (warnLight != null) warnLight.sprite = warnGreen;
        SunodGame.Core.vc_Sfx.Play("engine", 0.8f);
        StartCoroutine(LightLamps());
        Finish(msg);
    }

    private IEnumerator LightLamps()
    {
        for (int i = 0; i < dockLamps.Length; i++)
        {
            yield return new WaitForSeconds(0.18f);
            if (dockLamps[i] != null) dockLamps[i].sprite = lampOn;
            if (i < lampGlows.Length && lampGlows[i] != null) lampGlows[i].enabled = true;
            SunodGame.Core.vc_Sfx.Play("clack", 0.4f, 0.1f);
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
