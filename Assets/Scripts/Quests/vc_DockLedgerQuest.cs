using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// C quest (C8). Log the crates on the dock. Any APPROPRIATE skill works - how well it fits decides
/// how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   inspect  C  best fit     check each numbered crate against the manifest; ledger fills   ~2s
///   collect  C  workaround   gather the delivery slips off the crates, then log them         4s
///   direct   C  workaround   slide the crates into a neat row, then log them                 5s
///   survey   I  off-letter   count the crates one by one from the top of the dock steps      8s
///   command  E  off-letter   a dockhand calls out the numbers while you log them             8s
///   walking to the boat's manifest still works with no skill (existing)
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (inspect) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_DockLedgerQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private SpriteRenderer[] crates;
    [Tooltip("Where each crate ends up when sorted into a row (same count as crates).")]
    [SerializeField] private Transform[] rowSlots;
    [SerializeField] private SpriteRenderer ledger;
    [SerializeField] private Sprite[] ledgerFrames;
    [SerializeField] private Transform manifest;
    [SerializeField] private Transform boat;
    [SerializeField] private SpriteRenderer[] gulls;
    [SerializeField] private Sprite gullStand, gullPeck, gullFly;

    [Header("Walk to the manifest (no skill)")]
    [SerializeField] private float manifestRange = 1.3f;
    [SerializeField] private float manifestSeconds = 1f;

    [Header("Effects")]
    [SerializeField] private Sprite[] digits;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite slip;
    [SerializeField] private Sprite magnifier;
    [SerializeField] private Sprite shout;

    [Header("Timing (seconds)")]
    [SerializeField] private float inspectTime = 2f;
    [SerializeField] private float collectTime = 4f;
    [SerializeField] private float directTime = 5f;
    [SerializeField] private float surveyTime = 8f;
    [SerializeField] private float commandTime = 8f;

    private Vector3[] _crateRest;
    private Vector3 _boatRest;
    private Vector3[] _gullRest;
    private float _boatClock, _hideClock;
    private bool _near;
    private Transform _player;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    protected override Vector3 BarWorldPosition => (ledger != null ? ledger.transform.position : transform.position) + new Vector3(-0.5f, 1.9f, 0f);

    private void Awake()
    {
        int n = crates != null ? crates.Length : 0;
        _crateRest = new Vector3[n];
        for (int i = 0; i < n; i++) _crateRest[i] = crates[i].transform.position;
        if (boat != null) _boatRest = boat.position;
        int g = gulls != null ? gulls.Length : 0;
        _gullRest = new Vector3[g];
        for (int i = 0; i < g; i++) _gullRest[i] = gulls[i].transform.position;
    }

    protected override void OnBegin()
    {
        foreach (GameObject o in _spawned) if (o != null) Destroy(o);
        _spawned.Clear();
        for (int i = 0; i < crates.Length; i++) crates[i].transform.position = _crateRest[i];
        SetLedger(0);
        for (int i = 0; i < gulls.Length; i++)
        {
            gulls[i].gameObject.SetActive(true);
            gulls[i].transform.position = _gullRest[i];
            gulls[i].sprite = gullStand;
        }
        _near = false; _hideClock = 0f; _boatClock = 0f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Dockside Ledger",
            "The boat just unloaded. Write down every crate on the dock.",
            new[] { "Log the crates" });
    }

    // ── Idle life: the boat bobs, gulls peck and scatter ─────────────────

    private void Update()
    {
        if (!Started || Done) return;

        _boatClock += Time.deltaTime;
        if (boat != null) boat.position = _boatRest + new Vector3(0f, Mathf.Round(Mathf.Sin(_boatClock * 2f) * 1.2f) / 22f, 0f);

        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }

        for (int i = 0; i < gulls.Length; i++)
        {
            if (!gulls[i].gameObject.activeSelf) continue;
            if (gullPeck != null && gulls[i].sprite != gullFly)
                gulls[i].sprite = ((int)((_boatClock + i * 0.9f) * 1.6f) % 3 == 0) ? gullPeck : gullStand;
            if (_player != null && Vector2.Distance(_player.position, gulls[i].transform.position) < 1.3f && gulls[i].sprite != gullFly)
                StartCoroutine(Scatter(i));
        }

        // walking to the boat's manifest still works with no skill
        if (_player == null || manifest == null || Working) { StopNear(); return; }
        if (Vector2.Distance(_player.position, manifest.position) <= manifestRange)
        {
            if (!_near)
            {
                _near = true; _hideClock = 0f;
                ShowBar(true);
                Toast("You read the boat's manifest...", manifestSeconds + 0.5f);
                SunodGame.Core.vc_Sfx.Play("page_next", 0.5f);
            }
            _hideClock += Time.deltaTime;
            SetBar(_hideClock / manifestSeconds);
            if (_hideClock >= manifestSeconds)
            {
                _near = false;
                SetLedger(2);
                Finish("You read the boat's list and logged every crate!");
            }
        }
        else StopNear();
    }

    private void StopNear()
    {
        if (!_near) return;
        _near = false;
        ShowBar(false);
    }

    private IEnumerator Scatter(int i)
    {
        SpriteRenderer g = gulls[i];
        g.sprite = gullFly;
        Vector3 from = g.transform.position, to = from + new Vector3(2.5f, 3.2f, 0f);
        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            g.transform.position = Vector3.Lerp(from, to, t / 1.2f);
            g.flipX = ((int)(t * 12f) % 2) == 0;
            yield return null;
        }
        g.gameObject.SetActive(false);
        yield return new WaitForSeconds(5f);
        if (g == null || Done) yield break;
        g.flipX = false;
        g.transform.position = _gullRest[i];
        g.sprite = gullStand;
        g.gameObject.SetActive(true);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("inspect")) { StopNear(); StartWork(InspectRoutine()); return; }
        if (BlockedByWork()) return;
        StopNear();
        if (d.HasTag("collect")) { StartWork(CollectRoutine()); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        if (d.HasTag("survey")) { StartWork(SurveyRoutine()); return; }
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        Toast("Records need careful checking.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (_spawned == null) return;
        foreach (GameObject o in _spawned) if (o != null) Destroy(o);
        _spawned.Clear();
    }

    // ── Inspect: check each crate against the manifest ───────────────────

    private IEnumerator InspectRoutine()
    {
        Toast("Checking each crate against the manifest...", inspectTime);
        ShowBar(true);
        float step = (inspectTime - 0.5f) / crates.Length;
        yield return Timeline(inspectTime, 0f,
            At(0f, () => CheckCrate(0)),
            At(step, () => CheckCrate(1)),
            At(step * 2f, () => CheckCrate(2)),
            At(inspectTime - 0.5f, () => Log(1)),
            At(inspectTime - 0.2f, () => Log(2)));
        Finish("You checked every crate against the manifest. Logged!");
    }

    private void CheckCrate(int i)
    {
        if (i >= crates.Length) return;
        Vector3 p = crates[i].transform.position;
        Fx(ringFrames, p + new Vector3(0f, 0.1f, 0f), 6f);
        Digit(i + 1, p + new Vector3(0f, 0.95f, 0f));
        Fx(checkFrames, p + new Vector3(0.55f, 0.75f, 0f), 2.2f, 0.25f);
        SunodGame.Core.vc_Sfx.Play("tally", 0.6f, 0.1f);
    }

    // ── Collect: gather the delivery slips ───────────────────────────────

    private IEnumerator CollectRoutine()
    {
        Toast("Gathering the delivery slips...", collectTime);
        ShowBar(true);
        yield return Timeline(collectTime, 0f,
            At(0.1f, () => FlySlip(0)),
            At(0.65f, () => FlySlip(1)),
            At(1.2f, () => FlySlip(2)),
            At(collectTime - 1.0f, () => Log(1)),
            At(collectTime - 0.4f, () => Log(2)));
        Finish("You gathered the slips and logged every crate!");
    }

    private void FlySlip(int i)
    {
        if (i >= crates.Length || slip == null) return;
        var go = new GameObject("Slip");
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = slip; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 22;
        _spawned.Add(go);
        SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.3f, 0.15f);
        StartCoroutine(SlipFlight(go.transform, crates[i].transform.position + new Vector3(0f, 0.5f, 0f),
                                  (ledger != null ? ledger.transform.position : transform.position) + new Vector3(0f, 0.8f, 0f)));
    }

    private IEnumerator SlipFlight(Transform tr, Vector3 from, Vector3 to)
    {
        float t = 0f, dur = 1.0f;
        while (t < dur && tr != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            tr.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.7f, 0f));
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
        Fx(sparkFrames, to, 12f, 0.1f);
        SunodGame.Core.vc_Sfx.Play("tally", 0.5f, 0.12f);
    }

    // ── Direct: slide the crates into a neat row ─────────────────────────

    private IEnumerator DirectRoutine()
    {
        Toast("Sorting the crates into a neat row...", directTime);
        ShowBar(true);
        for (int i = 0; i < crates.Length; i++)
        {
            int k = i;
            StartCoroutine(Slide(crates[k].transform, rowSlots != null && k < rowSlots.Length ? rowSlots[k].position : _crateRest[k], 0.4f + k * 0.55f, 0.9f));
        }
        yield return Timeline(directTime, 0f,
            At(2.6f, () => Digit(1, crates[0].transform.position + new Vector3(0f, 0.95f, 0f))),
            At(3.0f, () => Digit(2, crates[1].transform.position + new Vector3(0f, 0.95f, 0f))),
            At(3.4f, () => Digit(3, crates[2].transform.position + new Vector3(0f, 0.95f, 0f))),
            At(directTime - 1.2f, () => Log(1)),
            At(directTime - 0.5f, () => Log(2)));
        Finish("You sorted the crates into rows and logged them!");
    }

    private IEnumerator Slide(Transform tr, Vector3 to, float delay, float dur)
    {
        yield return new WaitForSeconds(delay);
        Vector3 from = tr.position;
        float t = 0f, scrape = 0f;
        SunodGame.Core.vc_Sfx.Play("scrape", 0.4f, 0.1f);
        while (t < dur && tr != null)
        {
            t += Time.deltaTime;
            scrape += Time.deltaTime;
            tr.position = Snap(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur)));
            if (scrape > 0.3f) { scrape = 0f; Fx(dustFrames, tr.position + new Vector3(0f, -0.4f, 0f), 10f, 0.15f); }
            yield return null;
        }
        if (tr != null) tr.position = to;
        SunodGame.Core.vc_Sfx.Play("clack", 0.5f, 0.1f);
    }

    // ── Survey: count from the dock steps, slowly ────────────────────────

    private IEnumerator SurveyRoutine()
    {
        Toast("Counting the crates from the dock steps...", surveyTime);
        ShowBar(true);
        var ev = new List<Ev>();
        for (int i = 0; i < crates.Length; i++)
        {
            int k = i; float b = 0.3f + k * 2f;
            ev.Add(At(b, () => Fx(new[] { magnifier }, crates[k].transform.position + new Vector3(0.2f, 0.8f, 0f), 0.75f, 0.1f, 22)));
            ev.Add(At(b + 0.5f, () => Fx(ringFrames, crates[k].transform.position + new Vector3(0f, 0.1f, 0f), 6f)));
            ev.Add(At(b + 1.2f, () => CheckCrate(k)));
        }
        ev.Add(At(surveyTime - 1.6f, () => Log(1)));
        ev.Add(At(surveyTime - 0.7f, () => Log(2)));
        yield return Timeline(surveyTime, 0f, ev.ToArray());
        Finish("You counted every crate from the steps. Logged!");
    }

    // ── Command: a dockhand calls out the numbers ────────────────────────

    private IEnumerator CommandRoutine()
    {
        Toast("You call over a dockhand...", 3f);
        ShowBar(true);
        Vector3 to = _crateRest[0] + new Vector3(-1.5f, -0.9f, 0f);
        Vector3 from = to + new Vector3(-5.5f, 0f, 0f);
        SpawnHelper(from, 7);
        float walk = 2.4f;
        yield return HelperWalk(from, to, walk, t => SetBar(t / commandTime));
        Toast("The dockhand calls out the numbers...", 4f);
        Vector3 hp = to + new Vector3(0f, 1.3f, 0f);
        yield return Timeline(commandTime - walk, walk / commandTime,
            At(0.3f, () => { Fx(new[] { shout }, hp, 1.4f, 0.2f, 24); CheckCrate(0); }),
            At(1.5f, () => { Fx(new[] { shout }, hp, 1.4f, 0.2f, 24); CheckCrate(1); }),
            At(2.7f, () => { Fx(new[] { shout }, hp, 1.4f, 0.2f, 24); CheckCrate(2); }),
            At(commandTime - walk - 1.7f, () => Log(1)),
            At(commandTime - walk - 0.7f, () => Log(2)));
        Finish("The dockhands called out the numbers. All logged!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private struct Ev { public float at; public Action act; }
    private static Ev At(float t, Action a) => new Ev { at = t, act = a };

    /// <summary>Runs the bar over 'total' seconds (starting at fraction 'startK' of the full bar) and fires events at their times.</summary>
    private IEnumerator Timeline(float total, float startK, params Ev[] events)
    {
        float t = 0f; int next = 0;
        float span = startK >= 1f ? 1f : 1f - startK;
        while (t < total)
        {
            t += Time.deltaTime;
            SetBar(startK + span * (t / total));
            while (next < events.Length && t >= events[next].at) { events[next].act?.Invoke(); next++; }
            yield return null;
        }
        while (next < events.Length) { events[next].act?.Invoke(); next++; }
    }

    private void SetLedger(int frame)
    {
        if (ledger != null && ledgerFrames != null && frame < ledgerFrames.Length) ledger.sprite = ledgerFrames[frame];
    }

    private void Log(int frame)
    {
        SetLedger(frame);
        SunodGame.Core.vc_Sfx.Play(frame >= 2 ? "step_done" : "scribble", 0.5f);
        if (ledger != null) Fx(sparkFrames, ledger.transform.position + new Vector3(0f, 0.9f, 0f), 12f, 0.15f);
    }

    private void Digit(int n, Vector3 pos)
    {
        if (digits != null && n >= 0 && n < digits.Length) Fx(new[] { digits[n] }, pos, 1.6f, 0.35f, 24);
    }
}
