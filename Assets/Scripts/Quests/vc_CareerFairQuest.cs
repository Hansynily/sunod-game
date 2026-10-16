using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S quest (S1). Help the lost student find the right career booth. Any APPROPRIATE skill works -
/// how well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   guide    S  best fit     the student takes your side and follows you to the booth   ~2s + walk
///   teach    S  workaround   explain the careers; the "?" turns into a lightbulb         3s
///   heal     S  workaround   calm their worries first, then they find the way            5s
///   map      E  off-letter   draw their career path on a board                           8s
///   anything else: a hint, no effect
///
/// Guide is a mode, not a timed fix: the student trails the player until they reach the booth.
/// While a timed fix is running, guide takes over from wherever the student is.
/// </summary>
[DisallowMultipleComponent]
public class vc_CareerFairQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private SpriteRenderer student;
    [SerializeField] private Sprite[] studentWalk;
    [Tooltip("Where the student has to end up (in front of the right booth).")]
    [SerializeField] private Transform rightBooth;
    [Tooltip("The three booths: science, art, health.")]
    [SerializeField] private Transform[] booths;

    [Header("Art")]
    [SerializeField] private Sprite bubbleQuestion;
    [SerializeField] private Sprite bubbleSmile;
    [SerializeField] private Sprite worryCloud;
    [SerializeField] private Sprite boardSprite;
    [SerializeField] private Sprite arrowSprite;
    [Tooltip("Career icons: science, art, health.")]
    [SerializeField] private Sprite[] careerIcons;

    [Header("Effects")]
    [SerializeField] private Sprite[] heartFrames;
    [SerializeField] private Sprite[] bulbFrames;
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float teachTime = 3f;
    [SerializeField] private float healTime = 5f;
    [SerializeField] private float mapTime = 8f;
    [SerializeField] private float followSpeed = 3.2f;
    [SerializeField] private float followDistance = 1.0f;
    [SerializeField] private float arriveRange = 1.3f;

    private Vector3 _start;
    private float _elapsed, _total = 1f;
    private float _idleClock;
    private bool _following;
    private Transform _player;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    protected override Vector3 BarWorldPosition => (student != null ? student.transform.position : transform.position) + new Vector3(-0.5f, 1.9f, 0f);

    private void Awake()
    {
        if (student != null) _start = student.transform.position;
    }

    protected override void OnBegin()
    {
        CleanSpawned();
        _following = false;
        if (student != null)
        {
            student.transform.position = _start;
            student.flipX = false;
            student.color = Color.white;
            if (studentWalk != null && studentWalk.Length > 0) student.sprite = studentWalk[0];
        }
        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Career Fair",
            "A student is lost at the career fair. Help them find the right booth.",
            new[] { "Guide the student" });
    }

    // ── Idle life + guide-follow ─────────────────────────────────────────

    private void Update()
    {
        if (!Started || Done || student == null) return;

        if (_following) { UpdateFollow(); return; }
        if (Working) return;

        _idleClock -= Time.deltaTime;
        if (_idleClock <= 0f)
        {
            _idleClock = 2.6f;
            Fx(new[] { bubbleQuestion }, student.transform.position + new Vector3(0f, 1.2f, 0f), 1.4f, 0.15f, 26);
            StartCoroutine(Fidget());
        }
    }

    private IEnumerator Fidget()
    {
        Vector3 rest = student.transform.position;
        for (int i = 0; i < 2; i++)
        {
            if (student == null || Working || _following) yield break;
            student.transform.position = rest + new Vector3(0f, 1f / 22f, 0f);
            yield return new WaitForSeconds(0.1f);
            student.transform.position = rest;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void UpdateFollow()
    {
        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
            if (_player == null) return;
        }

        Vector3 p = student.transform.position;
        Vector3 target = _player.position;
        float d = Vector2.Distance(p, target);
        if (d > followDistance)
        {
            Vector3 next = Vector3.MoveTowards(p, target, followSpeed * Time.deltaTime);
            Face(next - p);
            student.transform.position = Snap(next);
            Animate();
        }
        else if (studentWalk != null && studentWalk.Length > 0) student.sprite = studentWalk[0];

        if (rightBooth != null && Vector2.Distance(student.transform.position, rightBooth.position) <= arriveRange)
        {
            _following = false;
            StartCoroutine(Arrive("The student followed you to the right booth!"));
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("guide"))
        {
            if (_following) { Toast("They're already following you.", 1.5f); return; }
            StartGuide();
            return;
        }
        if (_following) { Toast("They're already following you.", 1.5f); return; }
        if (BlockedByWork()) return;

        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }
        if (d.HasTag("heal")) { StartWork(HealRoutine()); return; }
        if (d.HasTag("map")) { StartWork(MapRoutine()); return; }

        Toast("They need someone to guide them.", 2.2f);
    }

    private void StartGuide()
    {
        CancelWork();
        _following = true;
        Fx(new[] { bubbleSmile }, student.transform.position + new Vector3(0f, 1.2f, 0f), 1.4f, 0.15f, 26);
        Fx(heartFrames, student.transform.position + new Vector3(0.3f, 0.9f, 0f), 3f, 0.4f, 26);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
        Toast("The student follows you. Lead them to the booth!", 3f);
    }

    protected override void OnWorkCancelled()
    {
        CleanSpawned();
    }

    private void CleanSpawned()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
    }

    private void Timeline(float total)
    {
        _elapsed = 0f; _total = Mathf.Max(0.1f, total);
        ShowBar(true);
    }

    private void Tick(float dt)
    {
        _elapsed += dt;
        SetBar(_elapsed / _total);
    }

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; Tick(Time.deltaTime); yield return null; }
    }

    // ── Teach: explain the careers, then they know where to go ───────────

    private IEnumerator TeachRoutine()
    {
        Toast("You explain the careers one by one...", teachTime);
        Timeline(teachTime);
        Fx(new[] { bubbleQuestion }, student.transform.position + new Vector3(0f, 1.2f, 0f), 1.5f, 0.1f, 26);
        yield return PopIcons(teachTime * 0.45f);
        Fx(bulbFrames, student.transform.position + new Vector3(0f, 1.3f, 0f), 2f, 0.3f, 26);
        SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
        yield return WalkToBooth(teachTime * 0.55f);
        yield return Arrive("You explained the health careers. They know where to go now!");
    }

    private IEnumerator PopIcons(float seconds)
    {
        int n = Mathf.Min(booths.Length, careerIcons.Length);
        float per = seconds / n;
        for (int i = 0; i < n; i++)
        {
            Fx(new[] { careerIcons[i] }, booths[i].position + new Vector3(0f, 0.9f, 0f), 1.5f, 0.35f, 26);
            Fx(ringFrames, booths[i].position + new Vector3(0f, 0.2f, 0f), 6f);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.1f);
            yield return Wait(per);
        }
    }

    // ── Heal: calm the worries away ──────────────────────────────────────

    private IEnumerator HealRoutine()
    {
        Toast("You calm the student's worries...", healTime);
        Timeline(healTime);
        var clouds = new List<GameObject>();
        Vector3 s = student.transform.position;
        for (int i = 0; i < 3; i++)
        {
            GameObject c = Spawn("Worry", worryCloud, 26);
            c.transform.position = Snap(s + new Vector3(-0.5f + i * 0.5f, 1.2f + (i % 2) * 0.25f, 0f));
            clouds.Add(c);
        }
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f);

        float phase = healTime * 0.55f, per = phase / clouds.Count;
        for (int i = 0; i < clouds.Count; i++)
        {
            yield return Wait(per * 0.6f);
            Fx(heartFrames, s + new Vector3(-0.3f + i * 0.3f, 0.8f, 0f), 3f, 0.6f, 26);
            Fx(sparkFrames, clouds[i].transform.position, 10f, 0.1f, 27);
            Destroy(clouds[i]);
            SunodGame.Core.vc_Sfx.Play("step_done", 0.3f, 0.1f);
            yield return Wait(per * 0.4f);
        }
        Fx(new[] { bubbleSmile }, s + new Vector3(0f, 1.2f, 0f), 1.5f, 0.15f, 26);
        yield return WalkToBooth(healTime * 0.45f);
        yield return Arrive("You calmed their worries. They know where to go now!");
    }

    // ── Map: draw the career path on a board ─────────────────────────────

    private IEnumerator MapRoutine()
    {
        Toast("Mapping out their career path...", mapTime);
        Timeline(mapTime);
        Vector3 at = student.transform.position + new Vector3(1.9f, 1.5f, 0f);
        GameObject board = Spawn("Board", boardSprite, 25);
        board.transform.position = Snap(at);
        StartCoroutine(PopScale(board.transform));
        SunodGame.Core.vc_Sfx.Play("page_next", 0.7f);
        yield return Wait(mapTime * 0.08f);

        int n = Mathf.Min(3, careerIcons.Length);
        float draw = mapTime * 0.5f, per = draw / (n * 2);
        for (int i = 0; i < n; i++)
        {
            Vector3 slot = at + new Vector3((i - 1) * 0.5f * (36f / 22f) * 0.62f, 0.05f, 0f);
            GameObject ic = Spawn("BoardIcon", careerIcons[i], 26);
            ic.transform.position = Snap(slot);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.1f);
            yield return Wait(per);
            if (i < n - 1)
            {
                GameObject ar = Spawn("BoardArrow", arrowSprite, 26);
                ar.transform.position = Snap(slot + new Vector3(0.37f, 0f, 0f));
                SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.1f);
            }
            yield return Wait(per);
        }
        Fx(checkFrames, at + new Vector3(0.6f, 0.6f, 0f), 3f, 0.3f, 27);
        Fx(bulbFrames, student.transform.position + new Vector3(0f, 1.3f, 0f), 2f, 0.3f, 26);
        SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
        yield return Wait(mapTime * 0.1f);
        yield return WalkToBooth(mapTime * 0.32f);
        yield return Arrive("You mapped out their path. They know where to go now!");
    }

    // ── Shared movement / ending ─────────────────────────────────────────

    private IEnumerator WalkToBooth(float seconds)
    {
        Vector3 from = student.transform.position, to = rightBooth.position;
        float t = 0f;
        seconds = Mathf.Max(0.3f, seconds);
        while (t < seconds)
        {
            t += Time.deltaTime; Tick(Time.deltaTime);
            Vector3 next = Vector3.Lerp(from, to, t / seconds);
            Face(next - student.transform.position);
            student.transform.position = Snap(next);
            Animate();
            yield return null;
        }
    }

    private IEnumerator Arrive(string msg)
    {
        if (studentWalk != null && studentWalk.Length > 0) student.sprite = studentWalk[0];
        Vector3 c = student.transform.position;
        Fx(new[] { bubbleSmile }, c + new Vector3(0f, 1.2f, 0f), 1.4f, 0.15f, 26);
        Fx(heartFrames, c + new Vector3(0.3f, 0.9f, 0f), 3f, 0.5f, 26);
        Fx(sparkFrames, c + new Vector3(0f, 0.8f, 0f), 10f, 0.2f, 27);
        SunodGame.Core.vc_Sfx.Play("bell", 0.5f);
        for (int i = 0; i < 2; i++)
        {
            student.transform.position = c + new Vector3(0f, 4f / 22f, 0f);
            yield return new WaitForSeconds(0.1f);
            student.transform.position = c;
            yield return new WaitForSeconds(0.1f);
        }
        Finish(msg);
    }

    private void Face(Vector3 delta)
    {
        if (Mathf.Abs(delta.x) > 0.0005f) student.flipX = delta.x < 0f;
    }

    private void Animate()
    {
        if (studentWalk != null && studentWalk.Length > 0)
            student.sprite = studentWalk[(int)(Time.time * 8f) % studentWalk.Length];
    }

    private GameObject Spawn(string name, Sprite s, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingLayerName = sortingLayer; sr.sortingOrder = order;
        _spawned.Add(go);
        return go;
    }

    private IEnumerator PopScale(Transform tr)
    {
        Vector3 s0 = tr.localScale;
        foreach (float k in new[] { 0.5f, 0.8f, 1.15f, 1f })
        {
            if (tr == null) yield break;
            tr.localScale = s0 * k;
            yield return new WaitForSeconds(0.06f);
        }
    }
}
