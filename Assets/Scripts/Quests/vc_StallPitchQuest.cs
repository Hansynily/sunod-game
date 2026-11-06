using System.Collections;
using UnityEngine;

/// <summary>
/// E quest (E1). Win the fair committee over for Marco's food stall. Any APPROPRIATE skill
/// works - how well it fits decides how fast it goes (and so the stars); skills that make no
/// sense do nothing.
///
///   persuade E  best fit     delivers the pitch; judges nod one by one, "Approved!" stamp   ~2s
///   charm    E  workaround   wins the room over with a big smile                           3s
///   map      E  workaround   shows on a fair map where the stall catches the crowd         4s
///   command  E  workaround   a confident, take-charge presentation                         4s
///   summon   E  workaround   calls a student over for a taste test; the cheer convinces    5s
///   plan     I  off-letter   a careful cost-and-profit chart                               8s
///   craft    A  off-letter   a tasty sample plate and a poster                             8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (persuade) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_StallPitchQuest : vc_FitQuestBase
{
    [Header("Committee")]
    [SerializeField] private SpriteRenderer[] judges;
    [SerializeField] private Sprite[] judgeNeutral;
    [SerializeField] private Sprite[] judgeNod;
    [SerializeField] private Sprite[] judgeSmile;
    [SerializeField] private Transform table;

    [Header("Pitch props")]
    [SerializeField] private SpriteRenderer pitchBoard;
    [Tooltip("0 = blank, 1 = profit chart, 2 = fair map, 3 = food poster.")]
    [SerializeField] private Sprite[] boardFrames;
    [SerializeField] private SpriteRenderer sampleTray;
    [SerializeField] private Sprite approvedStamp;

    [Header("Effects")]
    [SerializeField] private Sprite pitchBubble;
    [SerializeField] private Sprite dealBubble;
    [SerializeField] private Sprite shoutBubble;
    [SerializeField] private Sprite smileBubble;
    [SerializeField] private Sprite heartBubble;
    [SerializeField] private Sprite dotsBubble;
    [SerializeField] private Sprite[] steamFrames;
    [SerializeField] private Sprite[] scribbleFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float persuadeTime = 2f;
    [SerializeField] private float charmTime = 3f;
    [SerializeField] private float mapTime = 4f;
    [SerializeField] private float commandTime = 4f;
    [SerializeField] private float callTime = 5f;
    [SerializeField] private float planTime = 8f;
    [SerializeField] private float craftTime = 8f;

    // Skill system v2 (QUEST_REDESIGN.md): Plan (prep) puts a cost-and-profit chart on the easel, then
    // Command runs a confident presentation in one go. Preference: Call (the taste test).
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Marco",
        presses = 4,
        bestFitTag = "persuade",
        bestFitSeconds = 2f,
        prepTag = "plan",
        prepUnlocksTag = "command",
        prepLine = "You pin a cost-and-profit chart to the easel...",
        preferredTag = "summon",
        preferenceLine = "A real taste test! That's exactly what I hoped for. Salamat!",
        fastLines = new[]
        {
            ("charm", "They loved you instantly!"),
            ("map", "Showing them the crowd map sealed it!"),
            ("command", "You owned that room!"),
            ("summon", "One bite and they said yes!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private Vector3[] _rest;
    private float _idleClock;
    private GameObject _stamp;

    protected override Vector3 BarWorldPosition =>
        (table != null ? table.position : transform.position) + new Vector3(-0.5f, 2.3f, 0f);

    private void Awake()
    {
        int n = judges != null ? judges.Length : 0;
        _rest = new Vector3[n];
        for (int i = 0; i < n; i++) _rest[i] = judges[i].transform.position;
    }

    protected override void OnBegin()
    {
        if (_stamp != null) Destroy(_stamp);
        _idleClock = 1.2f;
        for (int i = 0; i < judges.Length; i++) { judges[i].transform.position = _rest[i]; Face(i, judgeNeutral); }
        Board(0);
        if (sampleTray != null) sampleTray.enabled = false;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Stall Pitch",
            "Marco wants the fair committee to say yes to his food stall. He's sure they would, if they could just taste it.",
            new[] { "Win over the committee" });
    }

    protected override void OnWorkCancelled()
    {
        for (int i = 0; i < judges.Length; i++) judges[i].transform.position = _rest[i];
    }

    // ── Idle life: the judges wait, unimpressed ──────────────────────────

    private void Update()
    {
        if (!Started || Done || Working || judges == null || judges.Length == 0) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = Random.Range(1.8f, 2.8f);
        int i = Random.Range(0, judges.Length);
        if (dotsBubble != null) Fx(new[] { dotsBubble }, Top(i), 1.4f, 0.15f, 24);
        StartCoroutine(Shake(judges[i].transform, judges[i].transform.localPosition, 0.2f, 1));
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("persuade")) { StartWork(PersuadeRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("charm")) { StartWork(CharmRoutine(PathTime(charmTime))); return; }
        if (d.HasTag("map")) { StartWork(MapRoutine(PathTime(mapTime))); return; }
        if (d.HasTag("command")) { StartWork(CommandRoutine(PathTime(commandTime), CurrentInstant)); return; }
        if (d.HasTag("summon")) { StartWork(TasteTestRoutine(PathTime(callTime))); return; }
        if (d.HasTag("plan")) { StartWork(ChartRoutine(PathTime(planTime))); return; }
        if (d.HasTag("craft")) { StartWork(SampleRoutine(PathTime(craftTime))); return; }
        Toast("That won't win the committee over.", 2.2f);
    }

    // ── Persuade: the pitch ──────────────────────────────────────────────

    private IEnumerator PersuadeRoutine()
    {
        Toast("You deliver the pitch...", persuadeTime);
        ShowBar(true);
        Bubble(pitchBubble, Speaker());
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f);
        yield return BarStep(0.5f, 0f, 0.5f / persuadeTime);
        yield return NodAll(0.5f, persuadeTime - 0.5f, persuadeTime);
        Approve("Approved! Marco's stall is in the fair.");
    }

    // ── Charm: a big smile wins the room ─────────────────────────────────

    private IEnumerator CharmRoutine(float total)
    {
        Toast("You win the room over with a smile...", total);
        ShowBar(true);
        Bubble(smileBubble, Speaker());
        float warm = total * 0.6f, t = 0f;
        for (int i = 0; i < judges.Length; i++)
        {
            Bubble(heartBubble, Top(i));
            SunodGame.Core.vc_Sfx.Play("chime", 0.4f, 0.2f);
            float per = warm / judges.Length;
            yield return BarStep(per, t / total, (t + per) / total);
            Face(i, judgeSmile);
            t += per;
        }
        yield return NodAll(t, total - t, total);
        Approve("They couldn't say no to that smile. Approved!");
    }

    // ── Map: where the stall catches the crowd ───────────────────────────

    private IEnumerator MapRoutine(float total)
    {
        Toast("You show them the best spot on the fair map...", total);
        ShowBar(true);
        Board(2);
        SunodGame.Core.vc_Sfx.Play("page_next", 0.5f);
        float t = 0f, point = total * 0.625f, step = Mathf.Clamp(point / 4f, 0.1f, 0.5f);
        while (t < point)
        {
            Fx(sparkFrames, BoardSpot(0.25f, 0.2f), 12f, 0.1f);
            yield return BarStep(step, t / total, (t + step) / total);
            t += step;
        }
        yield return NodAll(t, total - t, total);
        Approve("Right where the crowd passes. Approved!");
    }

    // ── Command: confident presentation ──────────────────────────────────

    private IEnumerator CommandRoutine(float total, bool oneGo)
    {
        Toast(oneGo ? "The chart backs every word. You take charge!" : "You take charge of the presentation!", total);
        ShowBar(true);
        float t = 0f, talk = total * 0.625f, step = Mathf.Clamp(talk / 4f, 0.1f, 0.6f);
        int beat = 0;
        while (t < talk)
        {
            Bubble(beat % 2 == 0 ? shoutBubble : pitchBubble, Speaker());
            SunodGame.Core.vc_Sfx.Play("bell", 0.4f, 0.1f);
            // the judges sit up straight
            for (int i = 0; i < judges.Length; i++) StartCoroutine(Hop(i, 1));
            yield return BarStep(step, t / total, (t + step) / total);
            t += step; beat++;
        }
        yield return NodAll(t, total - t, total);
        Approve(oneGo ? "A confident presentation, in one go. Approved!" : "Confident and clear. Approved!");
    }

    // ── Call: a taste test crowd ─────────────────────────────────────────

    private IEnumerator TasteTestRoutine(float total)
    {
        Toast("You call students over for a taste test...", total);
        ShowBar(true);
        if (sampleTray != null) sampleTray.enabled = true;
        Vector3 to = (sampleTray != null ? sampleTray.transform.position : transform.position) + new Vector3(-0.8f, -0.4f, 0f);
        Vector3 from = to + new Vector3(-5f, 0f, 0f);
        SpawnHelper(from, 6);
        float walk = total * 0.6f;
        yield return HelperWalk(from, to, walk, t => SetBar(t / total));
        Bubble(heartBubble, to + new Vector3(0f, 1.6f, 0f));
        SunodGame.Core.vc_Sfx.Play("cheer", 0.6f);
        yield return NodAll(walk, total - walk, total);
        Approve("The students loved it. Approved!");
    }

    // ── Plan: cost-and-profit chart ──────────────────────────────────────

    private IEnumerator ChartRoutine(float total)
    {
        Toast("You lay out a cost-and-profit chart...", total);
        ShowBar(true);
        float t = 0f, draw = total * 0.75f, step = Mathf.Clamp(draw / 5f, 0.1f, 0.45f);
        while (t < draw)
        {
            Fx(scribbleFrames, BoardSpot(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.3f)), 6f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("scribble", 0.3f, 0.15f, 0.3f);
            if (t > draw * 0.5f) Board(1);
            yield return BarStep(step, t / total, (t + step) / total);
            t += step;
        }
        Board(1);
        yield return NodAll(t, total - t, total);
        Approve("The numbers add up. Approved!");
    }

    // ── Craft: sample plate and poster ───────────────────────────────────

    private IEnumerator SampleRoutine(float total)
    {
        Toast("You make a tasty sample plate and a poster...", total);
        ShowBar(true);
        float t = 0f, make = total * 0.725f, step = Mathf.Clamp(make / 6f, 0.1f, 0.5f);
        bool trayShown = false;
        while (t < make)
        {
            SunodGame.Core.vc_Sfx.Play("snip", 0.35f, 0.2f, 0.3f);
            if (!trayShown && t > make * 0.4f && sampleTray != null) { sampleTray.enabled = true; trayShown = true; Fx(sparkFrames, sampleTray.bounds.center, 12f, 0.1f); }
            if (t > make * 0.75f) Board(3);
            if (trayShown && sampleTray != null) Fx(steamFrames, sampleTray.bounds.center + new Vector3(0f, 0.3f, 0f), 6f, 0.3f, 22);
            yield return BarStep(step, t / total, (t + step) / total);
            t += step;
        }
        Board(3);
        if (sampleTray != null) sampleTray.enabled = true;
        yield return NodAll(t, total - t, total);
        Approve("It looks delicious. Approved!");
    }

    // Prep (Plan): scribbles, then the profit chart appears on the easel board (it stays up for the pitch).
    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        for (int k = 0; k < 4; k++)
        {
            Fx(scribbleFrames, BoardSpot(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.3f)), 6f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("scribble", 0.3f, 0.15f, 0.3f);
            if (k == 2) Board(1);
            yield return new WaitForSeconds(0.2f);
        }
        Board(1);
        Fx(sparkFrames, BoardSpot(0f, 0.2f), 12f, 0.1f);
        for (int i = 0; i < judges.Length; i++) Bubble(dotsBubble, Top(i));
        yield return new WaitForSeconds(0.2f);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Judges nod one by one, then smile.</summary>
    private IEnumerator NodAll(float t0, float span, float total)
    {
        float per = Mathf.Max(0.2f, span) / Mathf.Max(1, judges.Length), t = t0;
        for (int i = 0; i < judges.Length; i++)
        {
            Face(i, judgeNod);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.1f);
            yield return BarStep(per * 0.5f, t / total, (t + per * 0.5f) / total);
            Face(i, judgeSmile);
            yield return BarStep(per * 0.5f, (t + per * 0.5f) / total, (t + per) / total);
            t += per;
        }
    }

    private void Approve(string msg)
    {
        for (int i = 0; i < judges.Length; i++) Face(i, judgeSmile);
        if (approvedStamp != null && table != null)
        {
            _stamp = new GameObject("ApprovedStamp");
            _stamp.transform.SetParent(transform, true);
            _stamp.transform.position = Snap(table.position + new Vector3(0f, 0.55f, 0f));
            var sr = _stamp.AddComponent<SpriteRenderer>();
            sr.sprite = approvedStamp; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 26;
            StartCoroutine(StampPop(_stamp.transform));
        }
        SunodGame.Core.vc_Sfx.Play("stamp", 0.8f);
        Bubble(dealBubble, Top(judges.Length / 2));
        Finish(msg);
    }

    private IEnumerator StampPop(Transform tr)
    {
        float[] s = { 1.8f, 1.3f, 0.95f, 1f };
        foreach (float k in s)
        {
            if (tr == null) yield break;
            tr.localScale = Vector3.one * k;
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void Face(int i, Sprite[] set)
    {
        if (i < 0 || i >= judges.Length || judges[i] == null || set == null || i >= set.Length || set[i] == null) return;
        judges[i].sprite = set[i];
    }

    private void Board(int f)
    {
        if (pitchBoard != null && boardFrames != null && f < boardFrames.Length && boardFrames[f] != null) pitchBoard.sprite = boardFrames[f];
    }

    private Vector3 BoardSpot(float dx, float dy)
    {
        if (pitchBoard == null) return transform.position;
        Bounds b = pitchBoard.bounds;
        return new Vector3(b.center.x + dx, b.center.y + b.extents.y * 0.2f + dy, 0f);
    }

    private Vector3 Speaker() => (pitchBoard != null ? pitchBoard.transform.position : transform.position) + new Vector3(0.9f, 1.9f, 0f);

    private Vector3 Top(int i) => _rest[Mathf.Clamp(i, 0, _rest.Length - 1)] + new Vector3(0f, 1.4f, 0f);

    private void Bubble(Sprite s, Vector3 at)
    {
        if (s != null) Fx(new[] { s }, at, 1.2f, 0.2f, 25);
    }

    private IEnumerator Hop(int i, int px)
    {
        if (judges[i] == null) yield break;
        judges[i].transform.position = _rest[i] + new Vector3(0f, px / 22f, 0f);
        yield return new WaitForSeconds(0.1f);
        if (judges[i] != null) judges[i].transform.position = _rest[i];
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
