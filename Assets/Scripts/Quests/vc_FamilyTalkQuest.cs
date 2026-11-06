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

    // Skill system v2 (QUEST_REDESIGN.md): Plan (prep) pins a fair chore chart up, then Guide brings
    // everyone to the table at once. Preference: Teach (taking turns). Bonus only.
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Aling Cora",
        presses = 4,
        bestFitTag = "heal",
        bestFitSeconds = 2f,
        prepTag = "plan",
        prepUnlocksTag = "guide",
        prepLine = "You pin a fair chore chart up where everyone can see...",
        preferredTag = "teach",
        preferenceLine = "Taking turns... that's exactly what they needed. Salamat, anak.",
        fastLines = new[]
        {
            ("teach", "They're taking turns already. Salamat."),
            ("guide", "Everyone's at the table together. It's been so long."),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

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
            "Aling Cora's family is having a hard day. She thinks they need to learn to take turns, so teaching them to would mean a lot to her.", new[] { "Help the family make up" });
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
        if (d.HasTag("guide")) { StartWork(GuideRoutine(PathTime(guideTime), CurrentInstant)); return; }
        if (d.HasTag("persuade")) { StartWork(PersuadeRoutine(PathTime(persuadeTime))); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine(PathTime(planTime))); return; }
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

    // Teach (x3 press): 3 presses at Lv1, 2 at Lv2, 1 at Lv3 / prepped. Each press has its share of
    // the people still standing take a turn; the last press runs through StartWork and seats the rest.
    private void TeachPress()
    {
        int need = Mathf.Max(1, PathPresses(teachPresses));
        _turns++;
        int remaining = StandingCount();
        int pressesLeft = Mathf.Max(1, need - _turns + 1);
        int count = _turns >= need ? remaining : Mathf.CeilToInt(remaining / (float)pressesLeft);
        count = Mathf.Min(count, remaining);

        var picked = new System.Collections.Generic.List<int>();
        for (int i = 0; i < family.Length && picked.Count < count; i++) if (!_seated[i]) picked.Add(i);

        bool last = _turns >= need || remaining - picked.Count <= 0;
        if (last)
        {
            StartWork(TeachFinishRoutine(picked.ToArray(), CurrentInstant));
            return;
        }

        Toast($"One person talks, the others listen ({_turns}/{need})", 2f);
        Sfx("text_blip", 0.6f);
        for (int k = 0; k < picked.Count; k++) StartCoroutine(TeachTurn(picked[k], k * 0.3f, 0.8f));
    }

    private IEnumerator TeachTurn(int who, float delay, float walkSeconds)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Bubble(dotsBubble, family[who], 0.8f);
        Later(0.6f, () => Bubble(smileBubble, family[who], 1f));
        StartCoroutine(SitOne(who, walkSeconds));
    }

    private IEnumerator TeachFinishRoutine(int[] people, bool oneGo)
    {
        Toast(oneGo ? "You teach them to take turns, all in one go!" : (people.Length > 1 ? "You teach everyone left to take turns..." : "The last one takes a turn to talk..."), 2f);
        ShowBar(true);
        Sfx("text_blip", 0.6f);
        int n = Mathf.Max(1, people.Length);
        for (int k = 0; k < people.Length; k++)
        {
            StartCoroutine(TeachTurn(people[k], 0f, 0.8f));
            SetBar((k + 1f) / n);
            yield return new WaitForSeconds(people.Length > 1 ? 0.3f : 0f);
        }
        yield return new WaitForSeconds(1.0f);
        Together(oneGo ? "They took turns to talk, all at once. They made up." : "Everyone got a turn to talk. They made up.");
    }

    private IEnumerator GuideRoutine(float seconds, bool oneGo)
    {
        Toast(oneGo ? "You bring everyone to the table in one go!" : "You gently bring each one to the table...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        var c = new float[1];
        float lead = Mathf.Min(0.2f, seconds * 0.1f);
        float per = (seconds - lead) / Mathf.Max(1, family.Length);
        for (int i = 0; i < family.Length; i++)
        {
            if (_seated[i]) { yield return Tick(per, c, seconds); continue; }
            Bubble(heartBubble, family[i], Mathf.Max(0.3f, per));
            StartCoroutine(SitOne(i, per * 0.8f));
            yield return Tick(per, c, seconds);
        }
        yield return Tick(Mathf.Max(0f, seconds - c[0]), c, seconds);
        Together(oneGo ? "Everyone came to the table at once." : "One by one, they came back to the table.");
    }

    private IEnumerator PersuadeRoutine(float seconds)
    {
        Toast("You get both sides to just listen...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        var c = new float[1];
        int side = 0;
        float tail = Mathf.Min(2.4f, seconds * 0.3f);
        while (c[0] < seconds - tail)
        {
            int i = side++ % family.Length;
            Bubble(pitchBubble, family[i], 0.9f);
            Later(0.5f, () => Bubble(c[0] > seconds * 0.4f ? smileBubble : angryBubble, family[i], 0.8f));
            Sfx("text_blip", 0.5f);
            yield return Tick(0.9f, c, seconds);
        }
        yield return SitAll(Mathf.Max(0.2f, seconds - c[0] - 0.1f), c, seconds);
        Together("They listened to each other. Peace at last.");
    }

    private IEnumerator PlanRoutine(float seconds)
    {
        Toast("You draw up a fair chore plan...", Mathf.Max(1.5f, seconds));
        ShowBar(true);
        var c = new float[1];
        Vector3 at = table != null ? Above(table, -0.2f) : transform.position;
        Spawn(planSheet, at, 12, "ChorePlan");
        float tail = Mathf.Min(2.4f, seconds * 0.3f);
        while (c[0] < seconds - tail)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.5f), 0f), 6f, 0f, 22);
            Sfx("scribble", 0.35f);
            yield return Tick(0.4f, c, seconds);
        }
        Bubble(checkMark, table, 1f);
        yield return SitAll(Mathf.Max(0.2f, seconds - c[0] - 0.1f), c, seconds);
        Together("A fair plan for everyone. No more arguing!");
    }

    // ── Prep: Plan pins a fair chore chart where everyone can see it ─────

    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        Vector3 at = table != null ? Above(table, -0.2f) : transform.position;
        if (planSheet != null) Spawn(planSheet, at, 12, "ChorePlan");
        for (int s = 0; s < 3; s++)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.5f), 0f), 6f, 0f, 22);
            Sfx("scribble", 0.35f);
            yield return new WaitForSeconds(0.25f);
        }
        Bubble(checkMark, table, 1f);
        Sfx("step_done", 0.45f, 0.05f);
        for (int i = 0; i < family.Length; i++) if (!_seated[i]) Bubble(dotsBubble, family[i], 0.8f);
        yield return new WaitForSeconds(0.3f);
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

    private int StandingCount()
    {
        int n = 0;
        for (int i = 0; i < family.Length; i++) if (!_seated[i]) n++;
        return n;
    }

    private Vector3 Seat(int i) => seats != null && i < seats.Length && seats[i] != null ? seats[i].position : _rest[i];
    private Sprite[] Walk(int i) => i == 0 ? walkA : (i == 1 ? walkB : walkC);
}
