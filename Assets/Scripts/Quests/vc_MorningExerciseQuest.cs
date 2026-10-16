using System.Collections;
using UnityEngine;

/// <summary>
/// S quest (S4). Lead Tita Lorna through her morning stretches. Any APPROPRIATE skill works -
/// how well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   teach    S  best fit     show each stretch; she copies (up, side, toes) with a 1-2-3 count   ~2s
///   guide    S  workaround   do the routine side by side with her, slowly                         4s
///   heal     S  workaround   ease her stiff back first (heal sparks), then she exercises          5s
///   command  E  off-letter   call out a loud "1! 2! 3!"; she follows the beat                     8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (teach) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_MorningExerciseQuest : vc_FitQuestBase
{
    [Header("Tita Lorna")]
    [SerializeField] private SpriteRenderer lorna;
    [SerializeField] private Sprite poseStand;
    [Tooltip("The routine in order: arms up, side bend, toe touch.")]
    [SerializeField] private Sprite[] routine;

    [Header("Effects")]
    [Tooltip("Count bubbles 1, 2, 3.")]
    [SerializeField] private Sprite[] countBubbles;
    [SerializeField] private Sprite sweatDrop;
    [SerializeField] private Sprite healSpark;
    [SerializeField] private Sprite heart;
    [SerializeField] private Sprite shoutBubble;
    [SerializeField] private Sprite questionBubble;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float teachTime = 2f;
    [SerializeField] private float guideTime = 4f;
    [SerializeField] private float healTime = 5f;
    [SerializeField] private float commandTime = 8f;

    private Vector3 _rest;
    private float _idleClock;

    protected override Vector3 BarWorldPosition => (lorna != null ? lorna.transform.position : transform.position) + new Vector3(-0.5f, 2.1f, 0f);

    private void Awake()
    {
        if (lorna != null) _rest = lorna.transform.position;
    }

    protected override void OnBegin()
    {
        _idleClock = 1.2f;
        Pose(poseStand);
        if (lorna != null) lorna.transform.position = _rest;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Morning Exercise",
            "Tita Lorna keeps forgetting her stretches. Help her through a routine.",
            new[] { "Lead the exercise routine" });
    }

    // ── Idle life: she's stiff, tries a stretch, forgets ─────────────────

    private void Update()
    {
        if (!Started || Done || Working || lorna == null) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = Random.Range(2.2f, 3.2f);
        StartCoroutine(ForgetfulTry());
    }

    private IEnumerator ForgetfulTry()
    {
        // a half-hearted arms-up, a creaky wobble, then a "?"
        if (routine != null && routine.Length > 0) Pose(routine[0]);
        yield return Wobble(0.35f);
        if (Working || Done) yield break;
        Pose(poseStand);
        if (questionBubble != null) Fx(new[] { questionBubble }, Top(), 1.4f, 0.15f, 24);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("guide")) { StartWork(GuideRoutine()); return; }
        if (d.HasTag("heal")) { StartWork(HealRoutine()); return; }
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        Toast("She needs someone to show her how.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (lorna != null) lorna.transform.position = _rest;
    }

    // ── Teach: show each stretch, she copies ─────────────────────────────

    private IEnumerator TeachRoutine()
    {
        Toast("You show her each stretch...", teachTime);
        ShowBar(true);
        yield return DoRoutine(0f, teachTime, teachTime, slow: false);
        Done1("She copied every stretch. Great routine!");
    }

    // ── Guide: side by side, slowly ──────────────────────────────────────

    private IEnumerator GuideRoutine()
    {
        Toast("You do the routine side by side, slowly...", guideTime);
        ShowBar(true);
        yield return DoRoutine(0f, guideTime, guideTime, slow: true);
        Done1("Side by side, you finished the routine!");
    }

    // ── Heal: ease the stiff back, then exercise ─────────────────────────

    private IEnumerator HealRoutine()
    {
        Toast("You ease her stiff back first...", healTime);
        ShowBar(true);
        float ease = healTime - 2.2f, t = 0f;
        while (t < ease)
        {
            if (healSpark != null && lorna != null)
            {
                Bounds b = lorna.bounds;
                Fx(new[] { healSpark }, new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.center.y), 0f), 2f, 0.6f, 22);
            }
            SunodGame.Core.vc_Sfx.Play("chime", 0.25f, 0.2f, 0.25f);
            yield return BarStep(0.3f, t / healTime, (t + 0.3f) / healTime);
            t += 0.3f;
        }
        if (heart != null) Fx(new[] { heart }, Top(), 1.5f, 0.3f, 24);
        yield return DoRoutine(t, healTime, healTime - t, slow: false);
        Done1("No more stiff back. She stretched it all out!");
    }

    // ── Command: a loud count, she follows the beat ──────────────────────

    private IEnumerator CommandRoutine()
    {
        Toast("You call out a loud count!", commandTime);
        ShowBar(true);
        int n = routine != null ? routine.Length : 0;
        float per = commandTime / Mathf.Max(1, n), t = 0f;
        for (int i = 0; i < n; i++)
        {
            // the shout comes first, she follows a beat later
            if (shoutBubble != null) Fx(new[] { shoutBubble }, Top() + new Vector3(-0.9f, 0f, 0f), 1.4f, 0.1f, 24);
            if (countBubbles != null && i < countBubbles.Length) Fx(new[] { countBubbles[i] }, Top() + new Vector3(-0.9f, 0.6f, 0f), 1.2f, 0.2f, 25);
            SunodGame.Core.vc_Sfx.Play("bell", 0.5f, 0.05f);
            yield return BarStep(per * 0.4f, t / commandTime, (t + per * 0.4f) / commandTime);
            Pose(routine[i]);
            SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.35f, 0.1f);
            if (sweatDrop != null) Fx(new[] { sweatDrop }, Top() + new Vector3(0.35f, -0.2f, 0f), 2f, -0.3f, 24);
            yield return BarStep(per * 0.6f, (t + per * 0.4f) / commandTime, (t + per) / commandTime);
            t += per;
        }
        Done1("She kept up with every count!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Runs the 3-pose routine over 'span' seconds, bar from t0 to total.</summary>
    private IEnumerator DoRoutine(float t0, float total, float span, bool slow)
    {
        int n = routine != null ? routine.Length : 0;
        float per = span / Mathf.Max(1, n), t = t0;
        for (int i = 0; i < n; i++)
        {
            Pose(routine[i]);
            SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.35f, 0.1f);
            if (countBubbles != null && i < countBubbles.Length) Fx(new[] { countBubbles[i] }, Top(), slow ? 1f : 2f, 0.2f, 24);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f, 0.05f);
            if (slow && sweatDrop != null) Fx(new[] { sweatDrop }, Top() + new Vector3(0.35f, -0.2f, 0f), 1.5f, -0.3f, 24);
            yield return BarStep(per, t / total, (t + per) / total);
            t += per;
        }
    }

    private void Done1(string msg)
    {
        Pose(poseStand);
        if (sparkFrames != null) Fx(sparkFrames, Top() + new Vector3(0.3f, -0.4f, 0f), 12f, 0.2f);
        if (heart != null) Fx(new[] { heart }, Top(), 1.5f, 0.3f, 24);
        StartCoroutine(HappyHop());
        Finish(msg);
    }

    private IEnumerator HappyHop()
    {
        for (int i = 0; i < 2 && lorna != null; i++)
        {
            lorna.transform.position = _rest + new Vector3(0f, 2f / 22f, 0f);
            yield return new WaitForSeconds(0.1f);
            if (lorna == null) yield break;
            lorna.transform.position = _rest;
            yield return new WaitForSeconds(0.12f);
        }
    }

    private IEnumerator Wobble(float seconds)
    {
        if (lorna == null) yield break;
        yield return Shake(lorna.transform, lorna.transform.localPosition, seconds, 1);
    }

    private void Pose(Sprite s)
    {
        if (lorna != null && s != null) lorna.sprite = s;
    }

    private Vector3 Top()
    {
        if (lorna == null) return transform.position;
        return new Vector3(_rest.x, _rest.y + 1.75f, 0f);
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
