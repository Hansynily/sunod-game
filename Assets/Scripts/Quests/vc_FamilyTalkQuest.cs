using System.Collections;
using UnityEngine;

/// <summary>
/// S quest (S5). Aling Cora's family had an argument; help them make up. Any APPROPRIATE skill
/// works - how well it fits decides how fast it goes (and so the stars).
///
///   heal      S  best fit     calms everyone: angry, calm, hearts; they sit together         ~2s
///   teach     S  workaround   shows them how to take turns talking, one person per press      x3
///   guide     S  workaround   brings each one to the table, gently                            5s
///   persuade  E  off-letter   convinces both sides to just listen                             8s
///   plan      I  off-letter   makes a fair chore plan that ends the fight                     8s
///   anything else: a hint, no effect
/// </summary>
[DisallowMultipleComponent]
public class vc_FamilyTalkQuest : vc_FitQuestKit
{
    [Header("Family")]
    [SerializeField] private SpriteRenderer[] family;
    [SerializeField] private Sprite[] walkA;
    [SerializeField] private Sprite[] walkB;
    [SerializeField] private Sprite[] walkC;
    [Tooltip("Where each member stands at the table once they make up.")]
    [SerializeField] private Transform[] seats;
    [SerializeField] private SpriteRenderer table;
    [SerializeField] private SpriteRenderer photo;
    [SerializeField] private Sprite photoDull;
    [SerializeField] private Sprite photoShine;

    [Header("Effects")]
    [SerializeField] private Sprite angryBubble;
    [SerializeField] private Sprite sadBubble;
    [SerializeField] private Sprite smileBubble;
    [SerializeField] private Sprite heartBubble;
    [SerializeField] private Sprite dotsBubble;
    [SerializeField] private Sprite pitchBubble;
    [SerializeField] private Sprite healSpark;
    [SerializeField] private Sprite planSheet;
    [SerializeField] private Sprite checkMark;
    [SerializeField] private Sprite[] scribbleFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float healTime = 2f;
    [SerializeField] private int teachPresses = 3;
    [SerializeField] private float guideTime = 5f;
    [SerializeField] private float persuadeTime = 8f;
    [SerializeField] private float planTime = 8f;

    private Vector3[] _rest;
    private bool[] _seated;
    private int _turns;
    private float _bubbleClock;

    protected override Vector3 BarWorldPosition => (table != null ? table.transform.position : transform.position) + new Vector3(-0.5f, 2.2f, 0f);

    private void Awake()
    {
        _rest = new Vector3[family.Length];
        for (int i = 0; i < family.Length; i++) _rest[i] = family[i].transform.position;
        _seated = new bool[family.Length];
    }

    protected override void OnBegin()
    {
        ClearSpawned();
        for (int i = 0; i < family.Length; i++) { family[i].transform.position = _rest[i]; family[i].sprite = Walk(i)[0]; _seated[i] = false; }
        if (photo != null) photo.sprite = photoDull;
        _turns = 0; _bubbleClock = 0.6f;
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Family Talk",
            "Aling Cora's family had an argument. Help them work it out.", new[] { "Help the family make up" });
    }

    // ── Idle life: grumpy bubbles, turned shoulders ──────────────────────

    private void Update()
    {
        if (!Started || Done || Working) return;
        _bubbleClock -= Time.deltaTime;
        if (_bubbleClock > 0f) return;
        _bubbleClock = Random.Range(1f, 1.7f);
        int i = Random.Range(0, family.Length);
        if (_seated[i]) return;
        Bubble(i % 2 == 0 ? angryBubble : sadBubble, family[i], 1.3f);
        StartCoroutine(Shake(family[i].transform, family[i].transform.localPosition, 0.2f, 1));
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("heal")) { StartWork(HealRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("teach")) { TeachPress(); return; }
        if (d.HasTag("guide")) { StartWork(GuideRoutine()); return; }
        if (d.HasTag("persuade")) { StartWork(PersuadeRoutine()); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        Toast("This family needs a caring helper.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        for (int i = 0; i < family.Length; i++)
        {
            family[i].transform.position = _seated[i] ? Seat(i) : _rest[i];
            family[i].sprite = Walk(i)[0];
        }
    }

    private IEnumerator HealRoutine()
    {
        Toast("You calm everyone down...", healTime);
        ShowBar(true);
        var c = new float[1];
        for (int i = 0; i < family.Length; i++) { Spark(i); Bubble(angryBubble, family[i], 0.4f); }
        Sfx("chime", 0.5f, 0f);
        yield return Tick(0.4f, c, healTime);
        for (int i = 0; i < family.Length; i++) Bubble(smileBubble, family[i], 0.4f);
        yield return Tick(0.4f, c, healTime);
        yield return SitAll(healTime - c[0] - 0.1f, c, healTime);
        Together("Calm voices, warm hearts. They sit together again.");
    }

    private void TeachPress()
    {
        int i = NextStanding();
        if (i < 0) return;
        _turns++;
        Bubble(dotsBubble, family[i], 0.8f);
        Later(0.6f, () => Bubble(smileBubble, family[i], 1f));
        Sfx("text_blip", 0.6f);
        StartCoroutine(SitOne(i, 0.8f));
        if (_turns < teachPresses && NextStanding() >= 0) { Toast($"One person talks, the others listen ({_turns}/{teachPresses})", 2f); return; }
        Together("Everyone got a turn to talk. They made up.");
    }

    private IEnumerator GuideRoutine()
    {
        Toast("You gently bring each one to the table...", guideTime);
        ShowBar(true);
        var c = new float[1];
        float per = (guideTime - 0.2f) / family.Length;
        for (int i = 0; i < family.Length; i++)
        {
            if (_seated[i]) { yield return Tick(per, c, guideTime); continue; }
            Bubble(heartBubble, family[i], per);
            StartCoroutine(SitOne(i, per * 0.8f));
            yield return Tick(per, c, guideTime);
        }
        yield return Tick(guideTime - c[0], c, guideTime);
        Together("One by one, they came back to the table.");
    }

    private IEnumerator PersuadeRoutine()
    {
        Toast("You get both sides to just listen...", persuadeTime);
        ShowBar(true);
        var c = new float[1];
        int side = 0;
        while (c[0] < persuadeTime - 2.4f)
        {
            int i = side++ % family.Length;
            Bubble(pitchBubble, family[i], 0.9f);
            Later(0.5f, () => Bubble(c[0] > persuadeTime * 0.4f ? smileBubble : angryBubble, family[i], 0.8f));
            Sfx("text_blip", 0.5f);
            yield return Tick(0.9f, c, persuadeTime);
        }
        yield return SitAll(persuadeTime - c[0] - 0.1f, c, persuadeTime);
        Together("They listened to each other. Peace at last.");
    }

    private IEnumerator PlanRoutine()
    {
        Toast("You draw up a fair chore plan...", planTime);
        ShowBar(true);
        var c = new float[1];
        Vector3 at = table != null ? Above(table, -0.2f) : transform.position;
        Spawn(planSheet, at, 12, "ChorePlan");
        while (c[0] < planTime - 2.4f)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.5f), 0f), 6f, 0f, 22);
            Sfx("scribble", 0.35f);
            yield return Tick(0.4f, c, planTime);
        }
        Bubble(checkMark, table, 1f);
        yield return SitAll(planTime - c[0] - 0.1f, c, planTime);
        Together("A fair plan for everyone. No more arguing!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private IEnumerator SitAll(float seconds, float[] c, float total)
    {
        for (int i = 0; i < family.Length; i++) if (!_seated[i]) StartCoroutine(SitOne(i, seconds * 0.9f));
        yield return Tick(seconds, c, total);
    }

    private IEnumerator SitOne(int i, float seconds)
    {
        _seated[i] = true;
        yield return WalkSprite(family[i], Walk(i), family[i].transform.position, Seat(i), seconds);
        Bubble(heartBubble, family[i], 1.2f);
    }

    private void Together(string msg)
    {
        if (photo != null) { photo.sprite = photoShine; Fx(sparkFrames, photo.bounds.center + new Vector3(0.3f, 0.2f, 0f), 12f, 0.15f); }
        Sfx("cheer", 0.4f, 0f);
        Finish(msg);
    }

    private void Spark(int i)
    {
        if (healSpark == null) return;
        Bounds b = family[i].bounds;
        Fx(new[] { healSpark }, new Vector3(b.center.x, b.center.y, 0f), 2f, 0.5f, 22);
    }

    private int NextStanding()
    {
        for (int i = 0; i < family.Length; i++) if (!_seated[i]) return i;
        return -1;
    }

    private Vector3 Seat(int i) => seats != null && i < seats.Length && seats[i] != null ? seats[i].position : _rest[i];
    private Sprite[] Walk(int i) => i == 0 ? walkA : (i == 1 ? walkB : walkC);
}
