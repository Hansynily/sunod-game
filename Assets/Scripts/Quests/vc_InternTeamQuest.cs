using System.Collections;
using UnityEngine;

/// <summary>
/// E quest (E5). Lead Ms. Ria's internship team for a day. Any APPROPRIATE skill works - how well
/// it fits decides how fast it goes (and so the stars).
///
///   command   E  best fit     assigns tasks; notes fly to each desk, monitors light up       ~2s
///   persuade  E  workaround   motivates the team; they speed up                              3s
///   charm     E  workaround   boosts morale with a team cheer                                3s
///   map       E  workaround   maps out the project plan on the whiteboard                    4s
///   summon    E  workaround   calls a quick huddle, then work starts                         4s
///   direct    C  off-letter   organizes the task board first                                 7s
///   plan      I  off-letter   writes a detailed schedule, then work starts                   8s
///   anything else: a hint, no effect
///
/// Prep: Direct (pressed first) sorts the task board; Map then draws the whole plan at once.
/// Preference: Charm (Ms. Ria: "go easy on them, they're tired"). Bonus only.
/// </summary>
[DisallowMultipleComponent]
public class vc_InternTeamQuest : vc_FitQuestKit
{
    [Header("Office")]
    [SerializeField] private SpriteRenderer[] desks;
    [SerializeField] private Sprite deskIdle;
    [SerializeField] private Sprite[] deskBusy;
    [SerializeField] private SpriteRenderer[] interns;
    [SerializeField] private Sprite[] internIdle;
    [SerializeField] private Sprite[] internHappy;
    [SerializeField] private SpriteRenderer taskBoard;
    [Tooltip("0 to-do, 1 in progress, 2 done.")]
    [SerializeField] private Sprite[] boardFrames;
    [SerializeField] private SpriteRenderer whiteboard;
    [SerializeField] private Transform huddleSpot;

    [Header("Effects")]
    [SerializeField] private Sprite[] stickies;
    [SerializeField] private Sprite doneStamp;
    [SerializeField] private Sprite shoutBubble;
    [SerializeField] private Sprite pitchBubble;
    [SerializeField] private Sprite heartBubble;
    [SerializeField] private Sprite dotsBubble;
    [SerializeField] private Sprite planSheet;
    [SerializeField] private Sprite[] scribbleFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float commandTime = 2f;
    [SerializeField] private float persuadeTime = 3f;
    [SerializeField] private float charmTime = 3f;
    [SerializeField] private float mapTime = 4f;
    [SerializeField] private float huddleTime = 4f;
    [SerializeField] private float directTime = 7f;
    [SerializeField] private float planTime = 8f;

    private Vector3[] _rest;
    private bool _busy;
    private float _anim, _dotsClock;

    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Ms. Ria",
        presses = 3,
        bestFitTag = "command",
        bestFitSeconds = 2f,
        prepTag = "direct",
        prepUnlocksTag = "map",
        prepLine = "You sort the task board...",
        preferredTag = "charm",
        preferenceLine = "Look at them smiling. Thank you for going easy on them!",
        fastLines = new[]
        {
            ("persuade", "They're fired up!"),
            ("charm", "Look at them smiling!"),
            ("map", "The whole plan, mapped already!"),
            ("summon", "Huddle done and they're working!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    protected override Vector3 BarWorldPosition => (taskBoard != null ? taskBoard.transform.position : transform.position) + new Vector3(-0.5f, 1.9f, 0f);

    private void Awake()
    {
        _rest = new Vector3[interns.Length];
        for (int i = 0; i < interns.Length; i++) _rest[i] = interns[i].transform.position;
    }

    protected override void OnBegin()
    {
        ClearSpawned();
        _busy = false;
        foreach (var d in desks) d.sprite = deskIdle;
        for (int i = 0; i < interns.Length; i++) { interns[i].transform.position = _rest[i]; interns[i].sprite = internIdle[i]; }
        SetBoard(0);
        _dotsClock = 1f;
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Intern Team",
            "Ms. Ria's intern team needs a leader today. Go easy on them, they're tired. Something friendly might work.", new[] { "Lead the team" });
    }

    // ── Idle life: idle screens, waiting interns; once working, screens animate ──

    private void Update()
    {
        if (_busy)
        {
            _anim += Time.deltaTime;
            if (_anim >= 0.25f)
            {
                _anim = 0f;
                foreach (var d in desks) d.sprite = d.sprite == deskBusy[0] ? deskBusy[1] : deskBusy[0];
            }
        }
        if (!Started || Done || Working) return;
        _dotsClock -= Time.deltaTime;
        if (_dotsClock > 0f) return;
        _dotsClock = Random.Range(1.6f, 2.6f);
        Bubble(dotsBubble, interns[Random.Range(0, interns.Length)], 1.3f);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("command")) { StartWork(Lead(commandTime, "You assign everyone a task!", shoutBubble, 0.3f, "Clear tasks, quick work. Project done!")); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("persuade")) { StartWork(Lead(PathTime(persuadeTime), "You fire up the team...", pitchBubble, 0.9f, "Motivated team, faster work. Done!")); return; }
        if (d.HasTag("charm")) { StartWork(Lead(PathTime(charmTime), "Team cheer!", heartBubble, 0.9f, "High spirits got it done. Great teamwork!")); return; }
        if (d.HasTag("map")) { StartWork(MapRoutine(PathTime(mapTime), CurrentInstant)); return; }
        if (d.HasTag("summon")) { StartWork(HuddleRoutine(PathTime(huddleTime))); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine(PathTime(directTime))); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine(PathTime(planTime))); return; }
        Toast("The team needs a leader.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        for (int i = 0; i < interns.Length; i++) interns[i].transform.position = _rest[i];
    }

    // ── Prep: Direct sorts the task board ────────────────────────────────

    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        Vector3 at = taskBoard != null ? taskBoard.bounds.center : transform.position;
        for (int i = 0; i < 5; i++)
        {
            if (stickies != null && stickies.Length > 0)
                Fx(new[] { stickies[i % stickies.Length] }, at + new Vector3(-0.6f + i * 0.3f, Random.Range(-0.3f, 0.3f), 0f), 3f, 0.1f, 22);
            Sfx("shuffle", 0.35f);
            yield return new WaitForSeconds(0.15f);
        }
        if (taskBoard != null) Fx(sparkFrames, at + new Vector3(0.5f, 0.4f, 0f), 12f, 0.15f);
        Sfx("clack", 0.4f);
        yield return new WaitForSeconds(0.25f);
    }

    private IEnumerator Lead(float total, string start, Sprite bubble, float lead, string done)
    {
        lead = Mathf.Min(lead, total * 0.4f);
        Toast(start, total);
        ShowBar(true);
        var c = new float[1];
        for (int i = 0; i < interns.Length; i++) { Bubble(bubble, interns[i], lead + 0.3f); StartCoroutine(Hop(interns[i].transform, 2)); }
        Sfx(bubble == heartBubble ? "cheer" : "text_blip", 0.5f);
        yield return Tick(lead, c, total);
        yield return Work(Mathf.Max(0.05f, total - c[0]), c, total);
        Complete(done);
    }

    private IEnumerator MapRoutine(float seconds, bool oneGo)
    {
        Toast(oneGo ? "The whole project plan appears at once!" : "You map out the project on the whiteboard...", Mathf.Max(1.2f, seconds));
        ShowBar(true);
        var c = new float[1];
        Vector3 at = whiteboard != null ? whiteboard.bounds.center : transform.position;
        float draw = seconds * 0.45f, drawStep = Mathf.Max(0.1f, 0.3f * seconds / 4f);
        while (c[0] < draw)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.3f, 0.3f), 0f), 6f, 0f, 22);
            Sfx("scribble", 0.35f);
            yield return Tick(drawStep, c, seconds);
        }
        yield return Work(Mathf.Max(0.05f, seconds - c[0]), c, seconds);
        Complete(oneGo ? "The whole plan, mapped in one go. Project done!" : "Everyone followed the plan. Project done!");
    }

    private IEnumerator HuddleRoutine(float seconds)
    {
        float sc = seconds / 4f;
        Toast("Quick huddle, everyone!", Mathf.Max(1.5f, seconds * 0.6f));
        ShowBar(true);
        var c = new float[1];
        Vector3 h = huddleSpot != null ? huddleSpot.position : transform.position;
        for (int i = 0; i < interns.Length; i++) StartCoroutine(WalkSprite(interns[i], null, _rest[i], h + new Vector3((i - 1) * 0.5f, 0f, 0f), 0.7f * sc));
        yield return Tick(0.8f * sc, c, seconds);
        for (int i = 0; i < interns.Length; i++) Bubble(i == 1 ? shoutBubble : dotsBubble, interns[i], 0.6f);
        yield return Tick(0.6f * sc, c, seconds);
        for (int i = 0; i < interns.Length; i++) StartCoroutine(WalkSprite(interns[i], null, interns[i].transform.position, _rest[i], 0.6f * sc));
        yield return Tick(0.6f * sc, c, seconds);
        yield return Work(Mathf.Max(0.05f, seconds - c[0]), c, seconds);
        Complete("Short huddle, smooth work. Project done!");
    }

    private IEnumerator DirectRoutine(float seconds)
    {
        Toast("You organize the task board first...", Mathf.Max(1.5f, seconds * 0.6f));
        ShowBar(true);
        var c = new float[1];
        Vector3 at = taskBoard != null ? taskBoard.bounds.center : transform.position;
        int n = 0;
        float sortTime = seconds * (4.6f / 7f), sortStep = Mathf.Max(0.15f, 0.5f * seconds / 7f);
        while (c[0] < sortTime)
        {
            if (stickies.Length > 0) Fx(new[] { stickies[n++ % stickies.Length] }, at + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.3f, 0.3f), 0f), 3f, 0.1f, 22);
            Sfx("shuffle", 0.35f);
            yield return Tick(sortStep, c, seconds);
        }
        if (taskBoard != null) Fx(sparkFrames, at + new Vector3(0.5f, 0.4f, 0f), 12f, 0.15f);
        yield return Work(Mathf.Max(0.05f, seconds - c[0]), c, seconds);
        Complete("A tidy board made the work easy. Done!");
    }

    private IEnumerator PlanRoutine(float seconds)
    {
        Toast("You write a detailed schedule first...", Mathf.Max(1.5f, seconds * 0.6f));
        ShowBar(true);
        var c = new float[1];
        Vector3 at = huddleSpot != null ? huddleSpot.position + new Vector3(0f, -0.2f, 0f) : transform.position;
        Spawn(planSheet, at, 14, "Schedule");
        float writeTime = seconds * (5.5f / 8f), writeStep = Mathf.Max(0.15f, 0.4f * seconds / 8f);
        while (c[0] < writeTime)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.6f), 0f), 6f, 0f, 22);
            Sfx("scribble", 0.35f);
            yield return Tick(writeStep, c, seconds);
        }
        yield return Work(Mathf.Max(0.05f, seconds - c[0]), c, seconds);
        Complete("Right on schedule. Project done!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Sticky notes fly from the board to each desk, screens light up, the board moves to done.</summary>
    private IEnumerator Work(float seconds, float[] c, float total)
    {
        SetBoard(1);
        float per = seconds / Mathf.Max(1, desks.Length);
        for (int i = 0; i < desks.Length; i++)
        {
            StartCoroutine(FlyNote(i, per * 0.6f));
            yield return Tick(per, c, total);
        }
    }

    private IEnumerator FlyNote(int i, float seconds)
    {
        if (taskBoard == null || stickies.Length == 0) yield break;
        var n = Spawn(stickies[i % stickies.Length], taskBoard.bounds.center, 24, "Sticky");
        Vector3 from = taskBoard.bounds.center, to = Above(desks[i], -0.1f);
        Sfx("page_next", 0.4f);
        float t = 0f;
        while (t < seconds && n != null)
        {
            t += Time.deltaTime;
            float k = t / seconds;
            n.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f));
            yield return null;
        }
        if (n != null) Destroy(n.gameObject);
        _busy = true;
        if (i < interns.Length && internHappy != null && i < internHappy.Length) interns[i].sprite = internHappy[i];
        Sfx("beep", 0.4f);
    }

    private void Complete(string msg)
    {
        _busy = true;
        SetBoard(2);
        if (doneStamp != null && taskBoard != null) Fx(new[] { doneStamp }, taskBoard.bounds.center, 0.7f, 0.2f, 26);
        Sfx("stamp", 0.7f, 0f);
        Sfx("cheer", 0.4f, 0f);
        Finish(msg);
    }

    private void SetBoard(int f)
    {
        if (taskBoard != null && boardFrames != null && f < boardFrames.Length) taskBoard.sprite = boardFrames[f];
    }
}
