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
            "Kuya Joel's friend is having a hard time. Help them reach out.", new[] { "Help them find support" });
    }

    // ── Idle life: a little worry cloud over the friend ──────────────────

    private void Update()
    {
        if (!Started || Done || Working || friend == null) return;
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
        if (d.HasTag("guide")) { StartWork(GuideRoutine()); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }
        if (d.HasTag("summon")) { StartWork(CallRoutine()); return; }
        Toast("Your friend needs someone who cares.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (friend != null) { friend.gameObject.SetActive(true); friend.transform.position = _friendRest; if (friend.sprite == friendStand) friend.sprite = friendUp; }
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

    private IEnumerator GuideRoutine()
    {
        Toast("You walk with them to the support center...", guideTime);
        ShowBar(true);
        var c = new float[1];
        Brighten();
        yield return Tick(0.6f, c, guideTime);
        SetDoor(true);
        Sfx("door_open", 0.5f, 0f);
        Vector3 door = supportDoor != null ? new Vector3(supportDoor.bounds.center.x, supportDoor.bounds.min.y + 0.1f, 0f) : _friendRest + Vector3.right * 2f;
        if (friendStand != null) friend.sprite = friendStand;
        float walk = guideTime - c[0] - 0.4f, t = 0f;
        while (t < walk)
        {
            t += Time.deltaTime;
            float k = t / walk;
            friend.transform.position = Snap(Vector3.Lerp(_friendRest, door, k) + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 10f)) / 22f, 0f));
            SetBar((c[0] + t) / guideTime);
            yield return null;
        }
        c[0] += walk;
        Bubble(heart, friend, 1.2f);
        friend.gameObject.SetActive(false);
        yield return Tick(guideTime - c[0], c, guideTime);
        Finish("You walked them right to the door. That took courage.");
    }

    private IEnumerator TeachRoutine()
    {
        Toast("You share what the support group is like...", teachTime);
        ShowBar(true);
        var c = new float[1];
        if (noticeBoard != null) Fx(sparkFrames, Above(noticeBoard, 0f), 12f, 0.15f);
        Sprite[] beats = { dotsBubble, dotsBubble, smileBubble, ideaBubble };
        float per = (teachTime - 0.8f) / beats.Length;
        foreach (var s in beats)
        {
            Bubble(s, friend, per);
            Sfx("text_blip", 0.5f);
            yield return Tick(per, c, teachTime);
        }
        Brighten();
        SetDoor(true);
        yield return Tick(teachTime - c[0], c, teachTime);
        Finish("Now they know what to expect. They feel ready.");
    }

    private IEnumerator CallRoutine()
    {
        Toast("You call the campus counselor...", 3f);
        ShowBar(true);
        var c = new float[1];
        yield return Tick(1.5f, c, callTime);
        SetDoor(true);
        Sfx("door_open", 0.5f, 0f);
        Vector3 from = supportDoor != null ? new Vector3(supportDoor.bounds.center.x, supportDoor.bounds.min.y, 0f) : _friendRest + Vector3.right * 3f;
        Vector3 to = _friendRest + new Vector3(0.9f, 0f, 0f);
        SpawnHelper(from, 7);
        float t0 = c[0];
        yield return HelperWalk(from, to, 3.5f, t => SetBar((t0 + t) / callTime));
        c[0] += 3.5f;
        Toast("The counselor sits with them and talks...", callTime - c[0]);
        while (c[0] < callTime - 1.2f)
        {
            Bubble(dotsBubble, friend, 0.9f);
            yield return Tick(0.9f, c, callTime);
        }
        Brighten();
        yield return Tick(callTime - c[0], c, callTime);
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
