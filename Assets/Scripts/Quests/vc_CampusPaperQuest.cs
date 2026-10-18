using System.Collections;
using UnityEngine;

/// <summary>
/// A quest (A5). Help Kevin write the campus paper story before the deadline. Any APPROPRIATE
/// skill works - how well it fits decides how fast it goes (and so the stars).
///
///   craft    A  best fit     writes the story; typewriter clacks, page fills, front page    ~2s
///   paint    A  workaround   illustrates the story and writes captions                       4s
///   barrier  A  workaround   reshapes a messy draft, one rewrite per press                   x3
///   survey   I  off-letter   interviews and researches first, then writes                    8s
///   anything else: a hint, no effect
/// </summary>
[DisallowMultipleComponent]
public class vc_CampusPaperQuest : vc_FitQuestKit
{
    [Header("Newsroom")]
    [SerializeField] private SpriteRenderer typewriter;
    [SerializeField] private Sprite[] typeFrames;
    [SerializeField] private SpriteRenderer pageBoard;
    [Tooltip("0 blank, 1 half, 2 full.")]
    [SerializeField] private Sprite[] pageFrames;
    [SerializeField] private SpriteRenderer deadlineClock;
    [SerializeField] private Sprite[] clockFrames;

    [Header("Effects")]
    [SerializeField] private Sprite frontPage;
    [SerializeField] private Sprite paperBall;
    [SerializeField] private Sprite notebook;
    [SerializeField] private Sprite[] scribbleFrames;
    [SerializeField] private Sprite[] splatFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite questionBubble;

    [Header("Timing (seconds)")]
    [SerializeField] private float craftTime = 2f;
    [SerializeField] private float paintTime = 4f;
    [SerializeField] private int rewritePresses = 3;
    [SerializeField] private float surveyTime = 8f;

    private int _rewrites;
    private float _clockTick, _ballClock;

    protected override Vector3 BarWorldPosition => (pageBoard != null ? pageBoard.transform.position : transform.position) + new Vector3(-0.5f, 2.2f, 0f);

    protected override void OnBegin()
    {
        ClearSpawned();
        _rewrites = 0;
        SetPage(0);
        if (typewriter != null && typeFrames.Length > 0) typewriter.sprite = typeFrames[0];
        _clockTick = 0f; _ballClock = 1.5f;
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Campus Paper",
            "The campus paper needs a story by tonight. Help Kevin write it.", new[] { "Write the story" });
    }

    // ── Idle life: the deadline clock ticks, drafts get crumpled ─────────

    private void Update()
    {
        if (!Started || Done) return;
        _clockTick += Time.deltaTime;
        if (_clockTick >= 0.5f && deadlineClock != null && clockFrames.Length > 1)
        {
            _clockTick = 0f;
            deadlineClock.sprite = deadlineClock.sprite == clockFrames[0] ? clockFrames[1] : clockFrames[0];
        }
        _ballClock -= Time.deltaTime;
        if (_ballClock <= 0f && !Working)
        {
            _ballClock = Random.Range(2.6f, 3.6f);
            TossBall();
        }
    }

    private void TossBall()
    {
        if (paperBall == null || typewriter == null) return;
        StartCoroutine(BallArc(typewriter.bounds.center + new Vector3(0.2f, 0.3f, 0f), typewriter.bounds.center + new Vector3(1.1f, -0.6f, 0f)));
    }

    private IEnumerator BallArc(Vector3 from, Vector3 to)
    {
        var b = Spawn(paperBall, from, 22, "PaperBall");
        float t = 0f;
        while (t < 0.4f && b != null)
        {
            t += Time.deltaTime;
            float k = t / 0.4f;
            b.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.5f, 0f));
            yield return null;
        }
        Sfx("plop", 0.3f);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("craft")) { StartWork(CraftRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("paint")) { StartWork(PaintRoutine()); return; }
        if (d.HasTag("barrier")) { RewritePress(); return; }
        if (d.HasTag("survey")) { StartWork(SurveyRoutine()); return; }
        Toast("A story needs a writer's touch.", 2.2f);
    }

    private IEnumerator CraftRoutine()
    {
        Toast("You write the story...", craftTime);
        ShowBar(true);
        var c = new float[1];
        yield return Type(0.7f, c, craftTime);
        SetPage(1);
        yield return Type(0.7f, c, craftTime);
        SetPage(2);
        yield return Tick(craftTime - c[0], c, craftTime);
        Splash("Front page story, done before the deadline!");
    }

    private IEnumerator PaintRoutine()
    {
        Toast("You illustrate the story...", paintTime);
        ShowBar(true);
        var c = new float[1];
        SetPage(1);
        for (int i = 0; i < 5; i++)
        {
            Fx(splatFrames, BoardCenter() + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.4f, 0.4f), 0f), 10f, 0f, 22);
            Sfx("brush", 0.4f);
            yield return Tick(0.5f, c, paintTime);
        }
        Toast("...and write the captions.", 1.4f);
        yield return Type(paintTime - c[0] - 0.1f, c, paintTime);
        SetPage(2);
        yield return Tick(paintTime - c[0], c, paintTime);
        Splash("Pictures and captions. The story pops!");
    }

    private void RewritePress()
    {
        _rewrites++;
        TossBall();
        StartCoroutine(TypeBurst(0.4f));
        SetPage(Mathf.Min(2, _rewrites));
        Fx(sparkFrames, BoardCenter(), 12f, 0.15f);
        if (_rewrites < rewritePresses) { Toast($"You rework the messy draft ({_rewrites}/{rewritePresses})", 2f); return; }
        Splash("Three rewrites later, the story shines!");
    }

    private IEnumerator SurveyRoutine()
    {
        Toast("You interview people and do research...", surveyTime);
        ShowBar(true);
        var c = new float[1];
        Vector3 at = typewriter != null ? typewriter.bounds.center + new Vector3(-1.2f, 0.6f, 0f) : transform.position;
        var nb = notebook != null ? Spawn(notebook, at, 20, "InterviewNotes") : null;
        if (questionBubble != null) Fx(new[] { questionBubble }, at + new Vector3(0f, 0.8f, 0f), 0.8f, 0.2f, 24);
        while (c[0] < surveyTime - 2.6f)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.1f, 0.2f), 0f), 6f, 0f, 22);
            Sfx("scribble", 0.35f);
            yield return Tick(0.4f, c, surveyTime);
        }
        if (nb != null) Destroy(nb.gameObject);
        yield return Type(1.2f, c, surveyTime);
        SetPage(1);
        yield return Type(surveyTime - c[0] - 0.1f, c, surveyTime);
        SetPage(2);
        yield return Tick(surveyTime - c[0], c, surveyTime);
        Splash("Well researched. A solid front-page story!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private IEnumerator Type(float seconds, float[] c, float total)
    {
        float t = 0f, k = 0f, start = c[0];
        while (t < seconds)
        {
            t += Time.deltaTime; k += Time.deltaTime;
            if (k >= 0.09f)
            {
                k = 0f;
                if (typewriter != null && typeFrames.Length > 1) typewriter.sprite = typewriter.sprite == typeFrames[0] ? typeFrames[1] : typeFrames[0];
                Sfx("clack", 0.3f, 0.3f);
            }
            SetBar((start + t) / total);
            yield return null;
        }
        c[0] = start + seconds;
        if (typewriter != null && typeFrames.Length > 0) typewriter.sprite = typeFrames[0];
    }

    private IEnumerator TypeBurst(float seconds)
    {
        var c = new float[1];
        yield return Type(seconds, c, 1000f);
    }

    private void Splash(string msg)
    {
        SetPage(2);
        if (frontPage != null) Fx(new[] { frontPage }, BoardCenter() + new Vector3(0f, 0.2f, 0f), 0.8f, 0.5f, 26);
        Fx(sparkFrames, BoardCenter() + new Vector3(0.5f, 0.6f, 0f), 12f, 0.2f);
        Sfx("cheer", 0.5f, 0f);
        Finish(msg);
    }

    private void SetPage(int f)
    {
        if (pageBoard != null && pageFrames != null && f < pageFrames.Length) pageBoard.sprite = pageFrames[f];
    }

    private Vector3 BoardCenter()
    {
        if (pageBoard == null) return transform.position;
        Bounds b = pageBoard.bounds;
        return new Vector3(b.center.x, b.center.y + b.extents.y * 0.25f, 0f);
    }
}
