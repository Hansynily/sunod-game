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

    private Vector3 _genRest, _boatRest;
    private bool _running;
    private int _pulls;
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
        if (generator != null) { generator.sprite = generatorOff; generator.transform.localPosition = _genRest; }
        if (warnLight != null) warnLight.sprite = warnRed;
        foreach (var l in dockLamps) if (l != null) l.sprite = lampOff;
        foreach (var g in lampGlows) if (g != null) g.enabled = false;
        if (boat != null) { boat.transform.position = _boatRest + boatOffset; boat.enabled = false; }

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Port Generator",
            "The port's generator died. Engr. Paz needs it running.",
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
        if (d.HasTag("build")) { StartWork(BuildRoutine()); return; }
        if (d.HasTag("unlock")) { StartWork(SolveRoutine()); return; }
        if (d.HasTag("summon")) { StartWork(CallRoutine()); return; }
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

    private void PullPress()
    {
        _pulls++;
        StartCoroutine(Shake(generator.transform, _genRest, 0.3f, 2));
        SunodGame.Core.vc_Sfx.Play("scrape", 0.6f, 0.1f);
        if (_pulls < pullPresses)
        {
            SunodGame.Core.vc_Sfx.Play("engine", 0.25f, 0.2f);
            Fx(smokeFrames, Exhaust(), 6f, 0.5f, 22);
            Toast($"You yank the cord... it sputters ({_pulls}/{pullPresses})", 2f);
            return;
        }
        StartEngine("Third pull - it catches! The lights come on.");
    }

    private IEnumerator BuildRoutine()
    {
        Toast("You rig a backup cable from the fuel line...", buildTime);
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
        float t = 0f, lay = buildTime - 0.8f;
        float full = Vector3.Distance(from, to) / (cable != null ? cable.bounds.size.x : 1f);
        while (t < lay)
        {
            t += Time.deltaTime;
            float k = Mathf.Round(t / lay * 8f) / 8f;
            if (_cable != null)
            {
                _cable.transform.position = Snap(Vector3.Lerp(from, Vector3.Lerp(from, to, k), 0.5f));
                _cable.transform.localScale = new Vector3(Mathf.Max(0.05f, full * k), 1f, 1f);
            }
            if (Mathf.Repeat(t, 0.6f) < Time.deltaTime)
            {
                Fx(sparkFrames, Vector3.Lerp(from, to, t / lay), 12f, 0.1f);
                SunodGame.Core.vc_Sfx.Play("zap", 0.3f, 0.15f);
            }
            SetBar(t / buildTime);
            yield return null;
        }
        yield return BarStep(0.8f, lay / buildTime, 1f);
        StartEngine("The backup cable works. Power's on!");
    }

    private IEnumerator SolveRoutine()
    {
        Toast("You study the gauges to find the fault...", solveTime);
        ShowBar(true);
        Vector3 c = generator.bounds.center;
        float study = solveTime - 2f, t = 0f;
        int f = 0;
        if (questionBubble != null) Fx(new[] { questionBubble }, c + new Vector3(0f, 1.3f, 0f), 1f, 0.2f, 24);
        while (t < study)
        {
            if (gaugeFrames != null && gaugeFrames.Length > 0)
                Fx(new[] { gaugeFrames[f++ % gaugeFrames.Length] }, c + new Vector3(-0.6f + (f % 3) * 0.6f, 0.9f, 0f), 2.5f, 0f, 24);
            SunodGame.Core.vc_Sfx.Play("timer_tick", 0.35f, 0.1f);
            yield return BarStep(0.4f, t / solveTime, (t + 0.4f) / solveTime);
            t += 0.4f;
        }
        if (ideaBubble != null) Fx(new[] { ideaBubble }, c + new Vector3(0f, 1.3f, 0f), 1.2f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("idea", 0.6f);
        for (int i = 0; i < 2; i++)
        {
            Fx(hammerFrames, c + new Vector3(0.2f * i, 0.3f, 0f), 8f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.6f, 0.1f);
            yield return BarStep(0.8f, t / solveTime, (t + 0.8f) / solveTime);
            t += 0.8f;
        }
        StartEngine("Found it: a clogged fuel filter. Fixed!");
    }

    private IEnumerator CallRoutine()
    {
        Toast("You radio the port mechanic...", 3f);
        ShowBar(true);
        float t = 0f, sail = 3.5f, walk = 3f;
        if (boat != null)
        {
            boat.enabled = true;
            Vector3 from = _boatRest + boatOffset;
            while (t < sail)
            {
                t += Time.deltaTime;
                boat.transform.position = Snap(Vector3.Lerp(from, _boatRest, Mathf.SmoothStep(0f, 1f, t / sail)) + new Vector3(0f, Mathf.Round(Mathf.Sin(t * 6f)) / 22f, 0f));
                SetBar(t / callTime);
                yield return null;
            }
        }
        else { yield return BarStep(sail, 0f, sail / callTime); t = sail; }
        Vector3 land = landing != null ? landing.position : _boatRest + Vector3.left;
        Vector3 at = generator.bounds.center + new Vector3(1.2f, -0.5f, 0f);
        SpawnHelper(land, 7);
        float w0 = t;
        yield return HelperWalk(land, at, walk, x => SetBar((w0 + x) / callTime));
        t += walk;
        Toast("The mechanic gets to work...", callTime - t);
        float fix = callTime - t;
        int hits = 4;
        for (int i = 0; i < hits; i++)
        {
            Fx(hammerFrames, generator.bounds.center + new Vector3(0.5f, 0.3f, 0f), 8f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.6f, 0.1f);
            StartCoroutine(Shake(generator.transform, _genRest, 0.15f, 1));
            yield return BarStep(fix / hits, t / callTime, (t + fix / hits) / callTime);
            t += fix / hits;
        }
        StartEngine("The mechanic got it running. Thanks for calling!");
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
