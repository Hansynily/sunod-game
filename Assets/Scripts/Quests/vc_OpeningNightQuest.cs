using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A quest (A2). Get the class play ready for opening night. Any APPROPRIATE skill works - how
/// well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   craft    A  best fit     set the props, cue the cast: "Action!"                       ~2s
///   paint    A  workaround   paint the blank scenery, then the curtain opens                4s
///   barrier  A  workaround   shape the costumes one by one, then the scene starts           5s
///   command  E  off-letter   call "Places!"; the cast scrambles into position              8s
///   teach    S  off-letter   coach the lead's lines, then everyone takes their marks        8s
///   anything else: a hint, no effect
///
/// Every path ends the same way: curtain opens, spotlights sweep, the cast bows, applause.
/// While a slow fix is running the best fit (craft) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_OpeningNightQuest : vc_FitQuestBase
{
    [Header("Cast (lead first)")]
    [SerializeField] private Transform[] cast;
    [Tooltip("6 walk frames per cast member, in cast order (plain clothes).")]
    [SerializeField] private Sprite[] plainFrames;
    [Tooltip("6 walk frames per cast member, in cast order (costume).")]
    [SerializeField] private Sprite[] costumeFrames;
    [SerializeField] private Transform stageMark;
    [SerializeField] private Vector3[] markOffsets = { new Vector3(0f, 0f, 0f), new Vector3(-1.2f, -0.35f, 0f), new Vector3(1.2f, -0.35f, 0f) };

    [Header("Stage")]
    [SerializeField] private Transform setsRoot;
    [SerializeField] private SpriteRenderer[] sets;
    [SerializeField] private Sprite setPainted;
    [SerializeField] private Sprite setBlank;
    [SerializeField] private Transform curtainL;
    [SerializeField] private Transform curtainR;
    [SerializeField] private Transform coneL;
    [SerializeField] private Transform coneR;

    [Header("Effects")]
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] heartFrames;
    [SerializeField] private Sprite[] dabFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite bubbleShout;
    [SerializeField] private Sprite bubbleQuestion;
    [SerializeField] private Sprite bubbleDots;
    [SerializeField] private Sprite bubbleLine;
    [SerializeField] private Sprite bubblePitch;
    [SerializeField] private Sprite bubbleSmile;

    [Header("Timing (seconds)")]
    [SerializeField] private float craftTime = 2f;
    [SerializeField] private float paintTime = 4f;
    [SerializeField] private float barrierTime = 5f;
    [SerializeField] private float commandTime = 8f;
    [SerializeField] private float teachTime = 8f;

    private SpriteRenderer[] _sr;
    private Vector3[] _wait;
    private bool[] _costumed;
    private Vector3[] _curtainRest;
    private Vector3[] _curtainScale;
    private Vector3[] _setScale;
    private bool _curtainOpen, _spotsOn, _setsUp;
    private float _idleClock;
    private bool _bowing;
    private readonly List<Coroutine> _routes = new List<Coroutine>();

    protected override Vector3 BarWorldPosition => (stageMark != null ? stageMark.position : transform.position) + new Vector3(-0.5f, -1.9f, 0f);

    private void Awake()
    {
        int n = cast != null ? cast.Length : 0;
        _sr = new SpriteRenderer[n];
        _wait = new Vector3[n];
        _costumed = new bool[n];
        for (int i = 0; i < n; i++)
        {
            _sr[i] = cast[i].GetComponent<SpriteRenderer>();
            _wait[i] = cast[i].position;
        }
        _curtainRest = new[] { curtainL != null ? curtainL.position : Vector3.zero, curtainR != null ? curtainR.position : Vector3.zero };
        _curtainScale = new[] { curtainL != null ? curtainL.localScale : Vector3.one, curtainR != null ? curtainR.localScale : Vector3.one };
        _setScale = new Vector3[sets != null ? sets.Length : 0];
        for (int i = 0; i < _setScale.Length; i++) _setScale[i] = sets[i].transform.localScale;
    }

    protected override void OnBegin()
    {
        StopRoutes();
        for (int i = 0; i < _sr.Length; i++)
        {
            cast[i].position = _wait[i];
            _costumed[i] = false;
            Pose(i, 0);
        }
        _curtainOpen = false; _spotsOn = false; _setsUp = false; _bowing = false;
        if (curtainL != null) { curtainL.position = _curtainRest[0]; curtainL.localScale = _curtainScale[0]; curtainL.gameObject.SetActive(true); }
        if (curtainR != null) { curtainR.position = _curtainRest[1]; curtainR.localScale = _curtainScale[1]; curtainR.gameObject.SetActive(true); }
        if (coneL != null) coneL.gameObject.SetActive(false);
        if (coneR != null) coneR.gameObject.SetActive(false);
        if (setsRoot != null) setsRoot.gameObject.SetActive(false);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Opening Night",
            "Opening night is tonight and the stage is still bare. Get the play ready.",
            new[] { "Get the play started" });
    }

    protected override void OnWorkCancelled()
    {
        StopRoutes();
        if (_sr == null) return;
        for (int i = 0; i < _sr.Length; i++) Pose(i, 0);
    }

    private void StopRoutes()
    {
        foreach (Coroutine r in _routes) if (r != null) StopCoroutine(r);
        _routes.Clear();
    }

    // ── Idle life ────────────────────────────────────────────────────────

    private void Update()
    {
        if (!Started || Done || _bowing) return;
        _idleClock += Time.deltaTime;
        if (_idleClock >= 3.2f && !Working)
        {
            _idleClock = 0f;
            if (bubbleDots != null && _sr.Length > 0)
                Fx(new[] { bubbleDots }, cast[0].position + new Vector3(0.1f, 1.0f, 0f), 1.5f, 0.2f);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("craft")) { StartWork(CraftRoutine()); return; }

        if (BlockedByWork()) return;

        if (d.HasTag("paint")) { StartWork(PaintRoutine()); return; }
        if (d.HasTag("barrier")) { StartWork(BarrierRoutine()); return; }
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }

        Toast("The show needs a creative eye.", 2.2f);
    }

    // ── Craft ★ ──────────────────────────────────────────────────────────

    private IEnumerator CraftRoutine()
    {
        Toast("Props up! Cueing the cast...", craftTime);
        ShowBar(true);
        SendCast(0.9f, 0.3f, 0.08f);
        yield return ShowSets(setPainted, true);
        SunodGame.Core.vc_Sfx.Play("page_next", 0.6f);
        yield return BarStep(0.5f, 0.2f, 0.7f);
        OpenCurtains();
        yield return BarStep(0.4f, 0.7f, 1f);
        yield return Curtain();
    }

    // ── Paint ◆ ──────────────────────────────────────────────────────────

    private IEnumerator PaintRoutine()
    {
        Toast("Painting the scenery...", paintTime);
        ShowBar(true);
        yield return ShowSets(setBlank, false);
        float t = 0.4f;
        // paint dabs fly onto the flats
        for (int k = 0; k < 8; k++)
        {
            SpriteRenderer set = sets[k % sets.Length];
            Vector3 from = (stageMark != null ? stageMark.position : transform.position) + new Vector3(Random.Range(-0.8f, 0.8f), -1.6f, 0f);
            Vector3 to = set.transform.position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.4f), 0f);
            StartCoroutine(Fly(dabFrames != null && dabFrames.Length > 0 ? dabFrames[k % dabFrames.Length] : null, from, to, 0.3f));
            SunodGame.Core.vc_Sfx.Play("brush", 0.4f, 0.1f);
            yield return BarStep(0.25f, t / paintTime, (t + 0.25f) / paintTime);
            t += 0.25f;
        }
        foreach (SpriteRenderer s in sets)
        {
            s.sprite = setPainted;
            Fx(sparkFrames, s.transform.position + new Vector3(0f, 0.3f, 0f), 12f, 0.2f);
        }
        SunodGame.Core.vc_Sfx.Play("chime", 0.6f);
        SendCast(1.5f, 0f, 0.15f);
        yield return BarStep(1.1f, t / paintTime, (t + 1.1f) / paintTime);
        t += 1.1f;
        OpenCurtains();
        yield return BarStep(Mathf.Max(0.1f, paintTime - 0.7f - t), t / paintTime, 1f);
        yield return Curtain();
    }

    // ── Barrier ◆ ────────────────────────────────────────────────────────

    private IEnumerator BarrierRoutine()
    {
        Toast("Shaping costumes for the cast...", barrierTime);
        ShowBar(true);
        yield return ShowSets(setBlank, false);
        float t = 0.4f;
        float per = 0.95f;
        for (int i = 0; i < _sr.Length; i++)
        {
            SunodGame.Core.vc_Sfx.Play("snip", 0.5f, 0.1f);
            Fx(sparkFrames, cast[i].position + new Vector3(-0.3f, 0.5f, 0f), 12f, 0.2f);
            Fx(sparkFrames, cast[i].position + new Vector3(0.3f, 0.2f, 0f), 12f, 0.2f);
            yield return BarStep(per * 0.5f, t / barrierTime, (t + per * 0.5f) / barrierTime);
            t += per * 0.5f;
            _costumed[i] = true;
            Pose(i, 0);
            Fx(new[] { bubbleSmile }, cast[i].position + new Vector3(0.1f, 1.05f, 0f), 1.6f, 0.3f);
            SunodGame.Core.vc_Sfx.Play("chime", 0.35f, 0.1f);
            yield return BarStep(per * 0.5f, t / barrierTime, (t + per * 0.5f) / barrierTime);
            t += per * 0.5f;
        }
        SendCast(1.0f, 0f, 0.1f);
        yield return BarStep(0.8f, t / barrierTime, (t + 0.8f) / barrierTime);
        t += 0.8f;
        OpenCurtains();
        yield return BarStep(Mathf.Max(0.1f, barrierTime - 0.7f - t), t / barrierTime, 1f);
        yield return Curtain();
    }

    // ── Command ○ ────────────────────────────────────────────────────────

    private IEnumerator CommandRoutine()
    {
        Toast("\"Places, everyone!\"", 2.5f);
        ShowBar(true);
        for (int i = 0; i < _sr.Length; i++)
            Fx(new[] { bubbleShout }, cast[i].position + new Vector3(0.1f, 1.05f, 0f), 1.5f, 0.3f);
        SunodGame.Core.vc_Sfx.Play("drum_hit", 0.5f);
        yield return BarStep(1.0f, 0f, 1f / commandTime);
        float t = 1.0f;
        yield return ShowSets(setPainted, true);
        t += 0.4f;

        // the lead runs straight; the extras first run the wrong way and have to turn around
        Vector3 lead = Mark(0);
        _routes.Add(StartCoroutine(WalkCast(0, lead, 4.6f, 0f)));
        for (int i = 1; i < _sr.Length; i++)
        {
            Vector3 wrong = cast[i].position + new Vector3(i == 1 ? -1.5f : 1.0f, -1.4f, 0f);
            _routes.Add(StartCoroutine(Scramble(i, wrong, Mark(i), 2.0f, 2.8f)));
        }
        yield return BarStep(4.6f, t / commandTime, (t + 4.6f) / commandTime);
        t += 4.6f;
        OpenCurtains();
        yield return BarStep(Mathf.Max(0.1f, commandTime - 0.7f - t), t / commandTime, 1f);
        yield return Curtain();
    }

    private IEnumerator Scramble(int i, Vector3 wrong, Vector3 right, float firstSecs, float secondSecs)
    {
        yield return WalkCast(i, wrong, firstSecs, 0.2f * i);
        Fx(new[] { bubbleQuestion }, cast[i].position + new Vector3(0.1f, 1.05f, 0f), 1.6f, 0.3f);
        yield return new WaitForSeconds(0.3f);
        yield return WalkCast(i, right, secondSecs, 0f);
    }

    // ── Teach ○ ──────────────────────────────────────────────────────────

    private IEnumerator TeachRoutine()
    {
        Toast("Coaching the lead's lines...", teachTime);
        ShowBar(true);
        yield return ShowSets(setPainted, true);
        float t = 0.4f;
        Sprite[] steps = { bubbleQuestion, bubbleDots, bubbleLine, bubblePitch, bubbleSmile };
        float per = 1.0f;
        foreach (Sprite s in steps)
        {
            Fx(new[] { s }, cast[0].position + new Vector3(0.1f, 1.05f, 0f), 1.2f, 0.25f);
            StartCoroutine(Hop(cast[0]));
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.55f, 0.1f);
            yield return BarStep(per, t / teachTime, (t + per) / teachTime);
            t += per;
        }
        SunodGame.Core.vc_Sfx.Play("chime", 0.6f);
        SendCast(1.3f, 0f, 0.15f);
        yield return BarStep(1.4f, t / teachTime, (t + 1.4f) / teachTime);
        t += 1.4f;
        OpenCurtains();
        yield return BarStep(Mathf.Max(0.1f, teachTime - 0.7f - t), t / teachTime, 1f);
        yield return Curtain();
    }

    private IEnumerator Hop(Transform tr)
    {
        Vector3 b = tr.position;
        for (int k = 0; k < 2; k++)
        {
            tr.position = b + new Vector3(0f, 2f / 22f, 0f);
            yield return new WaitForSeconds(0.1f);
            tr.position = b;
            yield return new WaitForSeconds(0.1f);
        }
    }

    // ── Shared stage actions ─────────────────────────────────────────────

    private Vector3 Mark(int i)
    {
        Vector3 b = stageMark != null ? stageMark.position : transform.position;
        return b + (i < markOffsets.Length ? markOffsets[i] : Vector3.zero);
    }

    private void SendCast(float seconds, float delay, float stagger)
    {
        for (int i = 0; i < _sr.Length; i++)
            _routes.Add(StartCoroutine(WalkCast(i, Mark(i), seconds, delay + stagger * i)));
    }

    private IEnumerator WalkCast(int i, Vector3 to, float seconds, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Transform tr = cast[i];
        Vector3 from = tr.position;
        float t = 0f;
        Sprite[] frames = _costumed[i] ? costumeFrames : plainFrames;
        while (t < seconds)
        {
            t += Time.deltaTime;
            tr.position = Snap(Vector3.Lerp(from, to, t / seconds));
            int idx = i * 6 + (int)(t * 9f) % 6;
            if (frames != null && idx < frames.Length) _sr[i].sprite = frames[idx];
            yield return null;
        }
        tr.position = to;
        Pose(i, 0);
    }

    private void Pose(int i, int frame)
    {
        Sprite[] frames = _costumed[i] ? costumeFrames : plainFrames;
        int idx = i * 6 + frame;
        if (frames != null && idx < frames.Length && _sr[i] != null) _sr[i].sprite = frames[idx];
    }

    private IEnumerator ShowSets(Sprite sprite, bool sound)
    {
        if (setsRoot != null) setsRoot.gameObject.SetActive(true);
        if (sets != null)
        {
            foreach (SpriteRenderer s in sets) s.sprite = sprite;
            for (int i = 0; i < sets.Length; i++)
                StartCoroutine(PopSet(i, sets[i].transform));
        }
        if (sound) SunodGame.Core.vc_Sfx.Play("clack", 0.6f, 0.1f);
        if (!_setsUp) SunodGame.Core.vc_Sfx.Play("grow", 0.5f);
        _setsUp = true;
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator PopSet(int i, Transform tr)
    {
        Vector3 full = i < _setScale.Length ? _setScale[i] : Vector3.one;
        float[] k = { 0.3f, 0.65f, 1.15f, 1f };
        foreach (float s in k)
        {
            if (tr == null) yield break;
            tr.localScale = full * s;
            yield return new WaitForSeconds(0.06f);
        }
        Fx(dustFrames, tr.position + new Vector3(0f, -0.3f, 0f), 10f, 0.2f);
    }

    private IEnumerator Fly(Sprite sprite, Vector3 from, Vector3 to, float seconds)
    {
        if (sprite == null) yield break;
        var go = new GameObject("Dab");
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 22;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = t / seconds;
            go.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.5f, 0f));
            yield return null;
        }
        Destroy(go);
    }

    private void OpenCurtains()
    {
        if (_curtainOpen) return;
        _curtainOpen = true;
        SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.7f);
        StartCoroutine(SlideCurtains());
    }

    private IEnumerator SlideCurtains()
    {
        const int steps = 8;
        for (int k = 1; k <= steps; k++)
        {
            float f = k / (float)steps;
            if (curtainL != null)
            {
                curtainL.position = Snap(_curtainRest[0] + new Vector3(-2.1f * f, 0f, 0f));
                curtainL.localScale = new Vector3(Mathf.Lerp(_curtainScale[0].x, 0.8f, f), _curtainScale[0].y, 1f);
            }
            if (curtainR != null)
            {
                curtainR.position = Snap(_curtainRest[1] + new Vector3(2.1f * f, 0f, 0f));
                curtainR.localScale = new Vector3(Mathf.Lerp(_curtainScale[1].x, 0.8f, f), _curtainScale[1].y, 1f);
            }
            yield return new WaitForSeconds(0.07f);
        }
        if (curtainL != null) curtainL.gameObject.SetActive(false);
        if (curtainR != null) curtainR.gameObject.SetActive(false);
    }

    /// <summary>Curtain up: spotlights on, the cast takes a bow, applause. Then finish.</summary>
    private IEnumerator Curtain()
    {
        _bowing = true;
        ShowBar(false);
        // everyone must be on their mark by now (a takeover may have cut a walk short)
        for (int i = 0; i < _sr.Length; i++) { cast[i].position = Mark(i); Pose(i, 0); }
        OpenCurtains();
        _spotsOn = true;
        if (coneL != null) { coneL.gameObject.SetActive(true); }
        if (coneR != null) { coneR.gameObject.SetActive(true); }
        SunodGame.Core.vc_Sfx.Play("cheer", 0.7f);
        StartCoroutine(SweepSpots());
        StartCoroutine(Applause());
        for (int b = 0; b < 2; b++)
        {
            for (int i = 0; i < _sr.Length; i++) cast[i].position = Mark(i) + new Vector3(0f, -2f / 22f, 0f);
            yield return new WaitForSeconds(0.18f);
            for (int i = 0; i < _sr.Length; i++) cast[i].position = Mark(i);
            yield return new WaitForSeconds(0.18f);
        }
        Finish("Action! The play begins!");
    }

    private IEnumerator SweepSpots()
    {
        float t = 0f;
        while (_spotsOn && coneL != null && coneR != null)
        {
            t += Time.deltaTime;
            float a = Mathf.Round(Mathf.Sin(t * 3f) * 3f) * 2f;
            coneL.localRotation = Quaternion.Euler(0f, 0f, 45f + a);
            coneR.localRotation = Quaternion.Euler(0f, 0f, -45f - a);
            yield return new WaitForSeconds(0.08f);
        }
    }

    private IEnumerator Applause()
    {
        Vector3 c = stageMark != null ? stageMark.position : transform.position;
        for (int i = 0; i < 9; i++)
        {
            Vector3 p = c + new Vector3(Random.Range(-3.2f, 3.2f), Random.Range(-4.6f, -2.2f), 0f);
            Fx(i % 2 == 0 ? heartFrames : sparkFrames, p, 8f, 0.5f);
            yield return new WaitForSeconds(0.1f);
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
