using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// I quest (I2). Study how the rabbits behave. Any APPROPRIATE skill works - how well it fits
/// decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   survey   I  best fit     notebook fills with 3 observations as the rabbits hop around   ~1.5s
///   plan     I  workaround   set up a blind behind the bush; the rabbits drift out to you    4s
///   unlock   I  workaround   work out why they dig: the burrow is revealed                    5s
///   inspect  C  off-letter   follow the paw tracks to the burrow, noting each one             7s
///   walking to the bush and keeping still for a moment still works with no skill (existing)
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (survey) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_RabbitWatchQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private Transform[] rabbits;
    [Tooltip("3 per rabbit, in rabbit order: idle, alert (ear twitch), dig.")]
    [SerializeField] private Sprite[] rabbitSprites;
    [SerializeField] private Transform center;
    [SerializeField] private SpriteRenderer burrow;
    [SerializeField] private SpriteRenderer notebook;
    [SerializeField] private Transform bush;
    [SerializeField] private Transform hideSpot;

    [Header("Walk-and-watch (no skill)")]
    [SerializeField] private float hideRange = 1.3f;
    [SerializeField] private float hideSeconds = 1.2f;

    [Header("Effects")]
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] bulbFrames;
    [SerializeField] private Sprite[] noteIcons;
    [SerializeField] private Sprite pawPrint;
    [SerializeField] private Sprite magnifier;

    [Header("Timing (seconds)")]
    [SerializeField] private float surveyTime = 1.5f;
    [SerializeField] private float planTime = 4f;
    [SerializeField] private float solveTime = 5f;
    [SerializeField] private float inspectTime = 7f;

    private SpriteRenderer[] _sr;
    private vc_HopWander[] _hop;
    private Vector3[] _restLocal;
    private Vector3 _bushRest;
    private Vector3 _barPos;
    private bool _scripted, _digging;
    private float _twitchClock, _hideClock;
    private bool _hiding;
    private Transform _player;
    private readonly List<Coroutine> _routes = new List<Coroutine>();
    private readonly List<GameObject> _spawned = new List<GameObject>();

    protected override Vector3 BarWorldPosition => _barPos;

    private void Awake()
    {
        int n = rabbits != null ? rabbits.Length : 0;
        _sr = new SpriteRenderer[n];
        _hop = new vc_HopWander[n];
        _restLocal = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            _sr[i] = rabbits[i].GetComponent<SpriteRenderer>();
            _hop[i] = rabbits[i].GetComponent<vc_HopWander>();
            _restLocal[i] = rabbits[i].localPosition;
        }
        if (bush != null) _bushRest = bush.localPosition;
    }

    protected override void OnBegin()
    {
        StopRoutes();
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        _scripted = false; _digging = false; _hiding = false;
        _twitchClock = 0f; _hideClock = 0f;
        for (int i = 0; i < _sr.Length; i++)
        {
            if (_hop[i] != null) _hop[i].enabled = false;
            rabbits[i].localPosition = _restLocal[i];
            SetPose(i, 0);
            if (_hop[i] != null) _hop[i].enabled = true;
        }
        if (burrow != null) burrow.gameObject.SetActive(false);
        if (notebook != null) notebook.gameObject.SetActive(false);
        if (bush != null) bush.localPosition = _bushRest;
        _barPos = (center != null ? center.position : transform.position) + new Vector3(-0.5f, 2.2f, 0f);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Watch the Rabbits",
            "The rabbits behave in curious ways. Find out what they're up to.",
            new[] { "Study the rabbits' behavior" });
    }

    private void SetPose(int i, int pose)
    {
        int k = i * 3 + pose;
        if (_sr[i] != null && rabbitSprites != null && k < rabbitSprites.Length) _sr[i].sprite = rabbitSprites[k];
    }

    // ── Idle life + walk-and-watch ───────────────────────────────────────

    private void Update()
    {
        if (!Started || Done) return;

        _twitchClock -= Time.deltaTime;
        if (_twitchClock <= 0f && !_scripted)
        {
            _twitchClock = Random.Range(1.2f, 2.8f);
            StartCoroutine(Twitch(Random.Range(0, _sr.Length)));
        }

        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }
        if (_player == null || hideSpot == null || Working) { StopHiding(); return; }

        if (Vector2.Distance(_player.position, hideSpot.position) <= hideRange)
        {
            if (!_hiding)
            {
                _hiding = true;
                _hideClock = 0f;
                _barPos = hideSpot.position + new Vector3(-0.5f, 1.3f, 0f);
                ShowBar(true);
                Toast("Keep still and watch quietly...", hideSeconds + 0.5f);
                if (bush != null) StartCoroutine(Shake(bush, _bushRest, 0.3f, 1));
                SunodGame.Core.vc_Sfx.Play("rustle", 0.4f);
            }
            _hideClock += Time.deltaTime;
            SetBar(_hideClock / hideSeconds);
            if (_hideClock >= hideSeconds)
            {
                _hiding = false;
                Finish("You watched quietly from the bush and learned plenty.");
            }
        }
        else StopHiding();
    }

    private void StopHiding()
    {
        if (!_hiding) return;
        _hiding = false;
        ShowBar(false);
    }

    private IEnumerator Twitch(int i)
    {
        SetPose(i, 1);
        yield return new WaitForSeconds(0.35f);
        if (!_scripted) SetPose(i, 0);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("survey"))
        {
            StopHiding();
            StartWork(SurveyRoutine());
            return;
        }

        if (BlockedByWork()) return;

        if (d.HasTag("plan")) { StopHiding(); StartWork(PlanRoutine()); return; }
        if (d.HasTag("unlock")) { StopHiding(); StartWork(SolveRoutine()); return; }
        if (d.HasTag("inspect")) { StopHiding(); StartWork(InspectRoutine()); return; }

        Toast("Rabbits spook easily. Watch and think.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (_sr == null) return;
        StopRoutes();
        _digging = false;
        _scripted = false;
        for (int i = 0; i < _sr.Length; i++)
        {
            SetPose(i, 0);
            if (_hop[i] != null) { _hop[i].enabled = false; _hop[i].enabled = true; }
        }
    }

    // ── Survey: three observations pop up over the rabbits ───────────────

    private IEnumerator SurveyRoutine()
    {
        Toast("Noting down what the rabbits do...", surveyTime);
        ShowBar(true);
        int n = Mathf.Min(_sr.Length, 3);
        float step = 0.33f, t = 0f;
        for (int i = 0; i < n; i++)
        {
            if (noteIcons != null && i < noteIcons.Length)
                Fx(new[] { noteIcons[i] }, rabbits[i].position + new Vector3(0f, 0.85f, 0f), 1.6f, 0.4f);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f, 0.1f);
            StartCoroutine(Twitch(i));
            yield return BarStep(step, t / surveyTime, (t + step) / surveyTime);
            t += step;
        }
        PopNotebook();
        SunodGame.Core.vc_Sfx.Play("page_next", 0.7f);
        yield return BarStep(Mathf.Max(0.05f, surveyTime - t), t / surveyTime, 1f);
        Finish("Eating, twitching, digging... you jotted it all down!");
    }

    private void PopNotebook()
    {
        if (notebook == null) return;
        notebook.gameObject.SetActive(true);
        notebook.transform.position = Snap((center != null ? center.position : transform.position) + new Vector3(0f, 1.5f, 0f));
        StartCoroutine(PopScale(notebook.transform));
    }

    private IEnumerator PopScale(Transform tr)
    {
        float[] s = { 0.4f, 0.7f, 1.2f, 1f };
        foreach (float k in s)
        {
            if (tr == null) yield break;
            tr.localScale = Vector3.one * k;
            yield return new WaitForSeconds(0.06f);
        }
    }

    // ── Plan: set up a blind, the rabbits drift out to you ───────────────

    private IEnumerator PlanRoutine()
    {
        Toast("Setting up a quiet spot by the bush...", planTime);
        ShowBar(true);
        _scripted = true;
        SunodGame.Core.vc_Sfx.Play("rustle", 0.5f);
        if (bush != null) StartCoroutine(Shake(bush, _bushRest, 0.5f, 1));

        Vector3 anchor = hideSpot != null ? hideSpot.position : transform.position;
        Vector3[] spots = { anchor + new Vector3(-1.3f, -1.9f, 0f), anchor + new Vector3(0.1f, -2.3f, 0f), anchor + new Vector3(1.4f, -1.8f, 0f) };
        for (int i = 0; i < _sr.Length; i++)
        {
            if (_hop[i] != null) _hop[i].enabled = false;
            _routes.Add(StartCoroutine(HopRoute(i, spots[i % spots.Length], 5, 0.6f + i * 0.7f)));
        }

        float t = 0f;
        while (t < planTime)
        {
            t += Time.deltaTime;
            SetBar(t / planTime);
            yield return null;
        }
        for (int i = 0; i < _sr.Length; i++) if (_hop[i] != null) { _hop[i].enabled = false; _hop[i].enabled = true; }
        _scripted = false;
        Finish("The rabbits wandered out. You got a good look at them.");
    }

    private IEnumerator HopRoute(int i, Vector3 to, int hops, float delay)
    {
        yield return new WaitForSeconds(delay);
        Transform tr = rabbits[i];
        Vector3 from = tr.position;
        if (_sr[i] != null) _sr[i].flipX = to.x > from.x;
        for (int h = 0; h < hops; h++)
        {
            Vector3 a = Vector3.Lerp(from, to, h / (float)hops);
            Vector3 b = Vector3.Lerp(from, to, (h + 1f) / hops);
            float t = 0f;
            while (t < 0.28f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.28f);
                tr.position = Vector3.Lerp(a, b, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.16f, 0f);
                yield return null;
            }
            tr.position = b;
            yield return new WaitForSeconds(0.12f);
        }
        SetPose(i, 1);
        Fx(checkFrames, tr.position + new Vector3(0f, 0.8f, 0f), 2f, 0.3f);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.35f, 0.1f);
    }

    // ── Solve: work out why they dig - the burrow shows up ───────────────

    private IEnumerator SolveRoutine()
    {
        Toast("Working out why they keep digging...", solveTime);
        ShowBar(true);
        _scripted = true;
        _digging = true;
        Vector3 c = center != null ? center.position : transform.position;

        int a = 0, b = Mathf.Min(2, _sr.Length - 1);
        foreach (int i in new[] { a, b })
        {
            if (_hop[i] != null) _hop[i].enabled = false;
            Vector3 spot = c + new Vector3(i == a ? -0.9f : 0.9f, -0.2f, 0f);
            _routes.Add(StartCoroutine(HopRoute(i, spot, 2, 0.1f)));
            _routes.Add(StartCoroutine(DigLoop(i, 0.9f)));
        }

        bool revealed = false, bulb = false;
        float t = 0f;
        while (t < solveTime)
        {
            t += Time.deltaTime;
            SetBar(t / solveTime);
            if (!revealed && t >= 2.2f)
            {
                revealed = true;
                if (burrow != null)
                {
                    burrow.gameObject.SetActive(true);
                    burrow.transform.position = Snap(c + new Vector3(0f, -0.35f, 0f));
                    StartCoroutine(FadeIn(burrow, 0.6f));
                }
                Fx(dustFrames, c + new Vector3(0f, 0.1f, 0f), 8f, 0.25f);
            }
            if (!bulb && t >= solveTime - 1.1f)
            {
                bulb = true;
                Fx(bulbFrames, c + new Vector3(0f, 1.6f, 0f), 2f, 0.3f, 25);
                SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
            }
            yield return null;
        }
        _digging = false;
        _scripted = false;
        for (int i = 0; i < _sr.Length; i++) { SetPose(i, 0); if (_hop[i] != null) { _hop[i].enabled = false; _hop[i].enabled = true; } }
        Finish("They dig to make a burrow. Mystery solved!");
    }

    private IEnumerator DigLoop(int i, float delay)
    {
        yield return new WaitForSeconds(delay);
        float sound = 0f;
        bool dig = true;
        while (_digging)
        {
            SetPose(i, dig ? 2 : 0);
            dig = !dig;
            sound += 0.22f;
            if (sound >= 0.45f)
            {
                sound = 0f;
                SunodGame.Core.vc_Sfx.Play("dig", 0.45f, 0.1f);
                Fx(dustFrames, rabbits[i].position + new Vector3(i == 0 ? 0.35f : -0.35f, 0.1f, 0f), 10f, 0.25f);
            }
            yield return new WaitForSeconds(0.22f);
        }
    }

    private IEnumerator FadeIn(SpriteRenderer sr, float seconds)
    {
        float t = 0f;
        while (t < seconds && sr != null)
        {
            t += Time.deltaTime;
            Color c = sr.color; c.a = Mathf.Clamp01(t / seconds); sr.color = c;
            yield return null;
        }
        if (sr != null) { Color c = sr.color; c.a = 1f; sr.color = c; }
    }

    // ── Inspect: follow the paw tracks to the burrow ─────────────────────

    private IEnumerator InspectRoutine()
    {
        Toast("Checking the ground for tracks...", inspectTime);
        ShowBar(true);
        Vector3 c = center != null ? center.position : transform.position;
        Vector3 start = c + new Vector3(-4.2f, -0.6f, 0f), end = c + new Vector3(-0.9f, -0.15f, 0f);
        int prints = 8;
        float printTime = inspectTime * 0.68f, per = printTime / prints, t = 0f;

        GameObject lens = new GameObject("Magnifier");
        lens.transform.SetParent(transform, true);
        var lr = lens.AddComponent<SpriteRenderer>();
        lr.sprite = magnifier; lr.sortingLayerName = sortingLayer; lr.sortingOrder = 22;
        _spawned.Add(lens);

        for (int i = 0; i < prints; i++)
        {
            float k = i / (prints - 1f);
            Vector3 p = Vector3.Lerp(start, end, k) + new Vector3(0f, (i % 2 == 0 ? 0.12f : -0.12f), 0f);
            var go = new GameObject("Paw");
            go.transform.SetParent(transform, true);
            go.transform.position = Snap(p);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pawPrint; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 2;
            _spawned.Add(go);

            lens.transform.position = Snap(p + new Vector3(0.1f, 0.5f, 0f));
            Fx(ringFrames, p, 6f);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.45f, 0.12f);
            yield return BarStep(per, t / inspectTime, (t + per) / inspectTime);
            t += per;
        }
        Destroy(lens);

        if (burrow != null)
        {
            burrow.gameObject.SetActive(true);
            burrow.transform.position = Snap(c + new Vector3(0f, -0.35f, 0f));
            StartCoroutine(FadeIn(burrow, 0.5f));
        }
        SunodGame.Core.vc_Sfx.Play("page_next", 0.6f);
        int n = Mathf.Min(_sr.Length, 3);
        float rest = (inspectTime - t) / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            if (noteIcons != null && i < noteIcons.Length)
                Fx(new[] { noteIcons[i] }, rabbits[i].position + new Vector3(0f, 0.85f, 0f), 1.6f, 0.4f);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.1f);
            yield return BarStep(rest, t / inspectTime, (t + rest) / inspectTime);
            t += rest;
        }
        Finish("The tracks lead to a burrow. You noted everything down.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

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

    private void StopRoutes()
    {
        foreach (Coroutine r in _routes) if (r != null) StopCoroutine(r);
        _routes.Clear();
    }
}
