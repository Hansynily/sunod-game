using System.Collections;
using UnityEngine;

/// <summary>
/// S quest (S3). Kuya Joel's friend is having a hard time; help them reach out to the campus
/// support group. Kept gentle on purpose: no specifics, just listening and support.
///
///   heal     S  best fit     sit and listen; sad turns to a small smile, they agree to go   ~2s
///   guide    S  workaround   walk with them to the support center door                      4s + walk
///   teach    S  workaround   share what the support group is like; they feel ready          5s
///   summon   E  off-letter   call the campus counselor, who comes over to talk              10s
///   anything else: a hint, no effect
/// </summary>
[DisallowMultipleComponent]
public class vc_SupportCornerQuest : vc_FitQuestKit
{
    [Header("Scene")]
    [SerializeField] private SpriteRenderer friend;
    [SerializeField] private Sprite friendSad;
    [SerializeField] private Sprite friendUp;
    [SerializeField] private Sprite friendStand;
    [SerializeField] private SpriteRenderer supportDoor;
    [SerializeField] private Sprite doorClosed;
    [SerializeField] private Sprite doorOpen;
    [SerializeField] private SpriteRenderer doorGlow;
    [SerializeField] private SpriteRenderer noticeBoard;

    [Header("Effects")]
    [SerializeField] private Sprite sadBubble;
    [SerializeField] private Sprite smileBubble;
    [SerializeField] private Sprite dotsBubble;
    [SerializeField] private Sprite ideaBubble;
    [SerializeField] private Sprite heart;
    [SerializeField] private Sprite healSpark;
    [SerializeField] private Sprite worryCloud;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float healTime = 2f;
    [SerializeField] private float guideTime = 4f;
    [SerializeField] private float teachTime = 5f;
    [SerializeField] private float callTime = 10f;

    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Kuya Joel",
        presses = 3,
        bestFitTag = "heal",
        bestFitSeconds = 2f,
        prepTag = "charm",
        prepUnlocksTag = "teach",
        prepLine = "You greet them with a warm, easy hello...",
        preferredTag = "guide",
        preferenceLine = "Walking with them, that's what I hoped for. Thank you.",
        fastLines = new[]
        {
            ("guide", "You walked them there already. Thank you for being there."),
            ("teach", "They feel ready now. That means a lot."),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private Vector3 _friendRest;
    private float _cloudClock;

    protected override Vector3 BarWorldPosition => (friend != null ? friend.transform.position : transform.position) + new Vector3(-0.5f, 1.7f, 0f);

    private void Awake() { if (friend != null) _friendRest = friend.transform.position; }

    protected override void OnBegin()
    {
        ClearSpawned();
        if (friend != null) { friend.gameObject.SetActive(true); friend.transform.position = _friendRest; friend.sprite = friendSad; }
        SetDoor(false);
        _cloudClock = 0.8f;
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Finding Support",
            "Kuya Joel's friend is having a hard time. Help them reach out, gently. Joel hopes someone will simply walk with them.", new[] { "Help them find support" });
    }

    // ── Idle life: a little worry cloud over the friend ──────────────────

    private void Update()
    {
        if (!Started || Done || Working || Prepared || friend == null) return;
        _cloudClock -= Time.deltaTime;
        if (_cloudClock > 0f) return;
        _cloudClock = Random.Range(2.2f, 3f);
        Bubble(worryCloud != null ? worryCloud : sadBubble, friend, 1.6f);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("heal")) { StartWork(HealRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("guide")) { StartWork(GuideRoutine(PathTime(guideTime))); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine(PathTime(teachTime), CurrentInstant)); return; }
        if (d.HasTag("summon")) { StartWork(CallRoutine(PathTime(callTime))); return; }
        Toast("Your friend needs someone who cares.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (friend != null) { friend.gameObject.SetActive(true); friend.transform.position = _friendRest; if (friend.sprite == friendStand) friend.sprite = friendUp; }
    }

    // Prep (Charm): a warm, easy hello; they look up. Teach then explains the group and they nod at once.
    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        Sfx("chime", 0.4f, 0f);
        Bubble(dotsBubble, friend, 0.5f);
        yield return new WaitForSeconds(0.5f);
        if (friend != null) friend.sprite = friendUp;
        Bubble(smileBubble, friend, 0.8f);
        Sparkles();
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator HealRoutine()
    {
        Toast("You sit down and just listen...", healTime);
        ShowBar(true);
        var c = new float[1];
        for (int i = 0; i < 3; i++)
        {
            Sparkles();
            yield return Tick(0.35f, c, healTime);
        }
        Bubble(sadBubble, friend, 0.5f);
        yield return Tick(0.4f, c, healTime);
        Brighten();
        SetDoor(true);
        yield return Tick(healTime - c[0], c, healTime);
        Finish("They feel heard. They'll visit the support group.");
    }

    private IEnumerator GuideRoutine(float seconds)
    {
        Toast("You walk with them to the support center...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        var c = new float[1];
        Brighten();
        yield return Tick(seconds * 0.15f, c, seconds);
        SetDoor(true);
        Sfx("door_open", 0.5f, 0f);
        Vector3 door = supportDoor != null ? new Vector3(supportDoor.bounds.center.x, supportDoor.bounds.min.y + 0.1f, 0f) : _friendRest + Vector3.right * 2f;
        if (friendStand != null) friend.sprite = friendStand;
        float walk = Mathf.Max(0.3f, seconds - c[0] - seconds * 0.1f), t = 0f;
        while (t < walk)
        {
            t += Time.deltaTime;
            float k = t / walk;
            friend.transform.position = Snap(Vector3.Lerp(_friendRest, door, k) + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 10f)) / 22f, 0f));
            SetBar((c[0] + t) / seconds);
            yield return null;
        }
        c[0] += walk;
        Bubble(heart, friend, 1.2f);
        friend.gameObject.SetActive(false);
        yield return Tick(Mathf.Max(0.1f, seconds - c[0]), c, seconds);
        Finish("You walked them right to the door. That took courage.");
    }

    private IEnumerator TeachRoutine(float seconds, bool oneGo)
    {
        Toast(oneGo ? "You explain what the group is like, and they nod..." : "You share what the support group is like...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        var c = new float[1];
        if (noticeBoard != null) Fx(sparkFrames, Above(noticeBoard, 0f), 12f, 0.15f);
        Sprite[] beats = { dotsBubble, dotsBubble, smileBubble, ideaBubble };
        float per = seconds * 0.84f / beats.Length;
        foreach (var s in beats)
        {
            Bubble(s, friend, per);
            Sfx("text_blip", 0.5f);
            yield return Tick(per, c, seconds);
        }
        Brighten();
        SetDoor(true);
        yield return Tick(Mathf.Max(0.1f, seconds - c[0]), c, seconds);
        Finish(oneGo ? "They nodded right away. They feel ready." : "Now they know what to expect. They feel ready.");
    }

    private IEnumerator CallRoutine(float seconds)
    {
        Toast("You call the campus counselor...", Mathf.Max(1.5f, seconds * 0.3f));
        ShowBar(true);
        var c = new float[1];
        yield return Tick(seconds * 0.15f, c, seconds);
        SetDoor(true);
        Sfx("door_open", 0.5f, 0f);
        Vector3 from = supportDoor != null ? new Vector3(supportDoor.bounds.center.x, supportDoor.bounds.min.y, 0f) : _friendRest + Vector3.right * 3f;
        Vector3 to = _friendRest + new Vector3(0.9f, 0f, 0f);
        SpawnHelper(from, 7);
        float t0 = c[0], walkTime = seconds * 0.35f;
        yield return HelperWalk(from, to, walkTime, t => SetBar((t0 + t) / seconds));
        c[0] += walkTime;
        Toast("The counselor sits with them and talks...", Mathf.Max(1f, seconds - c[0]));
        float beat = Mathf.Max(0.3f, seconds * 0.09f);
        while (c[0] < seconds * 0.88f)
        {
            Bubble(dotsBubble, friend, beat);
            yield return Tick(beat, c, seconds);
        }
        Brighten();
        yield return Tick(Mathf.Max(0.1f, seconds - c[0]), c, seconds);
        Finish("The counselor is helping now. Thanks for calling.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void Brighten()
    {
        if (friend != null) friend.sprite = friendUp;
        Bubble(smileBubble, friend, 1.4f);
        if (heart != null) Later(0.4f, () => Bubble(heart, friend, 1.2f));
        Sfx("chime", 0.5f, 0f);
    }

    private void Sparkles()
    {
        if (friend == null || healSpark == null) return;
        Bounds b = friend.bounds;
        Fx(new[] { healSpark }, new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.center.y), 0f), 2f, 0.5f, 22);
        Sfx("chime", 0.2f, 0.3f);
    }

    private void SetDoor(bool open)
    {
        if (supportDoor != null) supportDoor.sprite = open ? doorOpen : doorClosed;
        if (doorGlow != null) doorGlow.enabled = open;
    }
}
