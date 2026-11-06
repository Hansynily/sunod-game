using System.Collections;
using UnityEngine;

/// <summary>
/// I quest (I1). Put Ms. Garcia's skeleton model together. Any APPROPRIATE skill works - how well
/// it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   survey   I  best fit     identifies each bone; labels pop and bones fly into place      ~2s
///   unlock   I  workaround   works out the order: skull, ribs, arms, legs                   4s
///   plan     I  workaround   lays the bones out by body region, then assembles              5s
///   craft    A  off-letter   wires the bones together like an art model, joint by joint     7s
///   heal     S  off-letter   names the bones from first-aid know-how, slowly                8s
///   build    R  off-letter   puts it together by hand, trial and error                      8s
///   anything else: a hint, no effect
///
/// Best fit (survey) takes over a slow fix that's already running.
/// </summary>
[DisallowMultipleComponent]
public class vc_SkeletonModelQuest : vc_FitQuestBase
{
    [Header("Model")]
    [SerializeField] private SpriteRenderer skeleton;
    [Tooltip("0 = empty stand, 1 = skull, 2 = torso, 3 = full.")]
    [SerializeField] private Sprite[] stages;
    [SerializeField] private SpriteRenderer bonePile;
    [Tooltip("Spots on the table where Plan lays the bones out by region.")]
    [SerializeField] private Transform[] regionSpots;

    [Header("Effects")]
    [SerializeField] private Sprite boneFly;
    [SerializeField] private Sprite boneLabel;
    [SerializeField] private Sprite questionBubble;
    [SerializeField] private Sprite crossMark;
    [SerializeField] private Sprite heart;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] scanFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float surveyTime = 2f;
    [SerializeField] private float solveTime = 4f;
    [SerializeField] private float planTime = 5f;
    [SerializeField] private float craftTime = 7f;
    [SerializeField] private float healTime = 8f;
    [SerializeField] private float buildTime = 8f;

    // Skill system v2 (QUEST_REDESIGN.md): Heal (prep) names the bones from first-aid know-how,
    // then Solve assembles it in order at once. Preference: Plan (laid out by body region).
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Ms. Garcia",
        presses = 4,
        bestFitTag = "survey",
        bestFitSeconds = 2f,
        prepTag = "heal",
        prepUnlocksTag = "unlock",
        prepLine = "You point at your own elbow, knee and ribs, naming each bone...",
        preferredTag = "plan",
        preferenceLine = "Laid out by body region, just so! The class can follow that.",
        fastLines = new[]
        {
            ("unlock", "Skull to toes in seconds! You know your bones."),
            ("plan", "Laid out by region already! The class can follow that."),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private int _stage;
    private Vector3 _skeletonRest;
    private float _idleClock;
    private readonly System.Collections.Generic.List<GameObject> _laid = new System.Collections.Generic.List<GameObject>();

    protected override Vector3 BarWorldPosition =>
        (skeleton != null ? skeleton.transform.position : transform.position) + new Vector3(-0.5f, 2.4f, 0f);

    private void Awake()
    {
        if (skeleton != null) _skeletonRest = skeleton.transform.localPosition;
    }

    protected override void OnBegin()
    {
        ClearLaid();
        _stage = 0; _idleClock = 1.5f;
        if (skeleton != null) { skeleton.flipX = false; skeleton.transform.localPosition = _skeletonRest; }
        SetStage(0);
        if (bonePile != null) bonePile.enabled = true;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Skeleton Model",
            "Help Ms. Garcia put the skeleton model together for biology. She wants the bones laid out by body region so the class can follow.",
            new[] { "Assemble the skeleton" });
    }

    // ── Idle life: the bone pile clatters, the empty stand wobbles ───────

    private void Update()
    {
        if (!Started || Done || Working || bonePile == null) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = Random.Range(1.8f, 2.8f);
        StartCoroutine(Shake(bonePile.transform, bonePile.transform.localPosition, 0.2f, 1));
        SunodGame.Core.vc_Sfx.Play("clack", 0.15f, 0.3f, 0.6f);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("survey")) { StartWork(SurveyRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("unlock")) { StartWork(SolveRoutine(PathTime(solveTime), CurrentInstant)); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine(PathTime(planTime))); return; }
        if (d.HasTag("craft")) { StartWork(CraftRoutine(PathTime(craftTime))); return; }
        if (d.HasTag("heal")) { StartWork(HealRoutine(PathTime(healTime))); return; }
        if (d.HasTag("build")) { StartWork(BuildRoutine(PathTime(buildTime))); return; }
        Toast("This needs some careful study.", 2.2f);
    }

    protected override void OnWorkCancelled() => ClearLaid();

    // ── Survey: identify, label, bones fly into place ────────────────────

    // Prep (Heal): first-aid know-how. Hearts and bone labels pop over the pile as you name each bone.
    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f, 0.1f);
        if (heart != null) Fx(new[] { heart }, Top(), 1.5f, 0.2f, 24);
        Vector3 c = bonePile != null ? bonePile.bounds.center : transform.position;
        for (int i = 0; i < 3; i++)
        {
            if (boneLabel != null) Fx(new[] { boneLabel }, c + new Vector3(-0.5f + 0.5f * i, 0.35f, 0f), 1.4f, 0.15f, 24);
            Fx(sparkFrames, c + new Vector3(-0.5f + 0.5f * i, 0.1f, 0f), 12f, 0.1f, 23);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.35f, 0.15f);
            yield return new WaitForSeconds(0.3f);
        }
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator SurveyRoutine()
    {
        Toast("You identify each bone...", surveyTime);
        ShowBar(true);
        if (scanFrames != null && bonePile != null) Fx(scanFrames, bonePile.bounds.center, 6f, 0f, 22);
        SunodGame.Core.vc_Sfx.Play("scan", 0.45f);
        yield return AssembleSteps(0f, surveyTime, surveyTime, label: true, fromPile: true);
        Done1("Every bone named and in place!");
    }

    // ── Solve: the puzzle order, skull to legs ───────────────────────────

    private IEnumerator SolveRoutine(float seconds, bool oneGo)
    {
        Toast(oneGo ? "Skull, ribs, arms, legs... you know the order by heart!" : "Skull, ribs, arms, legs... in order.", seconds);
        ShowBar(true);
        float think = Mathf.Min(1f, seconds * 0.25f);
        if (questionBubble != null && !oneGo) Fx(new[] { questionBubble }, Top(), 1.2f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("idea", 0.4f);
        yield return BarStep(think, 0f, think / seconds);
        yield return AssembleSteps(think, seconds, seconds - think, label: false, fromPile: true);
        Done1(oneGo ? "Skull to toes in one go. The skeleton stands tall!" : "Puzzle solved. The skeleton stands tall!");
    }

    // ── Plan: lay out by region, then assemble ───────────────────────────

    private IEnumerator PlanRoutine(float seconds)
    {
        Toast("You lay the bones out by body region...", seconds);
        ShowBar(true);
        float lay = 2.4f * seconds / Mathf.Max(0.1f, planTime), t = 0f;
        int n = regionSpots != null ? regionSpots.Length : 0;
        float per = lay / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            if (bonePile != null && boneFly != null)
                yield return Arc(bonePile.bounds.center, regionSpots[i].position, per * 0.6f, keep: true);
            if (boneLabel != null) Fx(new[] { boneLabel }, regionSpots[i].position + new Vector3(0f, 0.3f, 0f), 1.2f, 0.1f, 24);
            yield return BarStep(per * 0.4f, (t + per * 0.6f) / seconds, (t + per) / seconds);
            t += per;
        }
        if (bonePile != null) bonePile.enabled = false;
        yield return AssembleSteps(t, seconds, Mathf.Max(0.3f, seconds - t), label: false, fromPile: false);
        Done1("Sorted by region, then built. Nicely planned!");
    }

    // ── Craft: wire the bones together like an art model ─────────────────

    private IEnumerator CraftRoutine(float seconds)
    {
        Toast("You wire the bones together like an art model...", seconds);
        ShowBar(true);
        float t = 0f;
        float f = seconds / Mathf.Max(0.1f, craftTime);
        float wireStep = 0.3f * f;
        // bend a wire frame first: sparks trace the spine from the stand up
        Vector3 b = skeleton != null ? skeleton.transform.position : transform.position;
        const int wireSteps = 6;
        for (int i = 0; i < wireSteps; i++)
        {
            Fx(sparkFrames, b + new Vector3(Random.Range(-0.08f, 0.08f), 0.3f + i * 0.28f, 0f), 12f, 0.05f);
            SunodGame.Core.vc_Sfx.Play("brush", 0.3f, 0.15f);
            yield return BarStep(wireStep, t / seconds, (t + wireStep) / seconds);
            t += wireStep;
        }
        // then each part is tied on with a few twists of wire (slow, careful joints)
        int steps = Mathf.Max(1, 3 - _stage);
        float per = Mathf.Max(0.3f, (seconds - t - 0.2f * f) / steps);
        while (_stage < 3)
        {
            Vector3 dst = StageSpot(_stage + 1);
            yield return Arc(bonePile != null ? bonePile.bounds.center : LaidSpot(), dst, per * 0.35f, keep: false);
            for (int k = 0; k < 3; k++)
            {
                Fx(sparkFrames, dst + new Vector3(-0.25f + 0.25f * k, 0.05f, 0f), 12f, 0.08f);
                SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.2f);
                yield return BarStep(per * 0.15f, t / seconds, (t + per * 0.15f) / seconds);
                t += per * 0.15f;
            }
            SetStage(_stage + 1);
            StartCoroutine(Settle());
            yield return BarStep(per * 0.2f, t / seconds, (t + per * 0.2f) / seconds);
            t += per * 0.2f;
        }
        if (bonePile != null) bonePile.enabled = false;
        Done1("Wired up and posed. It looks great!");
    }

    // ── Heal: name the bones from first aid, slowly ──────────────────────

    private IEnumerator HealRoutine(float seconds)
    {
        Toast("You name bones from first-aid lessons...", seconds);
        ShowBar(true);
        float f = seconds / Mathf.Max(0.1f, healTime);
        float recall = 3.2f * f, recallStep = Mathf.Max(0.1f, 0.8f * f), t = 0f;
        while (t < recall)
        {
            if (heart != null && t < 0.2f) Fx(new[] { heart }, Top(), 1.5f, 0.2f, 24);
            if (boneLabel != null && bonePile != null) Fx(new[] { boneLabel }, bonePile.bounds.center + new Vector3(Random.Range(-0.4f, 0.4f), 0.35f, 0f), 1.4f, 0.15f, 24);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f, 0.1f);
            yield return BarStep(recallStep, t / seconds, (t + recallStep) / seconds);
            t += recallStep;
        }
        yield return AssembleSteps(t, seconds, Mathf.Max(0.3f, seconds - t), label: true, fromPile: true);
        Done1("You knew your bones! The skeleton is complete.");
    }

    // ── Build: trial and error by hand ───────────────────────────────────

    private IEnumerator BuildRoutine(float seconds)
    {
        Toast("You fit bones together, trial and error...", seconds);
        ShowBar(true);
        float t = 0f;
        float f = seconds / Mathf.Max(0.1f, buildTime);
        // two wrong tries knock bones back off, then it clicks
        for (int miss = 0; miss < 2; miss++)
        {
            yield return Arc(bonePile.bounds.center, Top() + new Vector3(0f, -0.9f, 0f), 0.4f * f, keep: false);
            if (crossMark != null) Fx(new[] { crossMark }, Top(), 1.4f, 0.15f, 24);
            SunodGame.Core.vc_Sfx.Play("buzz", 0.35f);
            if (skeleton != null) StartCoroutine(Shake(skeleton.transform, _skeletonRest, 0.3f, 1));
            Fx(dustFrames, skeleton.transform.position + new Vector3(0.4f, 0.1f, 0f), 10f, 0.1f);
            yield return BarStep(1.6f * f, (t + 0.4f * f) / seconds, (t + 2f * f) / seconds);
            t += 2f * f;
        }
        yield return AssembleSteps(t, seconds, Mathf.Max(0.3f, seconds - t), label: false, fromPile: true);
        Done1("Trial and error, but it's together!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Takes the skeleton from its current stage to full over 'span' seconds.</summary>
    private IEnumerator AssembleSteps(float t0, float total, float span, bool label, bool fromPile)
    {
        int steps = Mathf.Max(1, 3 - _stage);
        float per = span / steps, t = t0;
        while (_stage < 3)
        {
            Vector3 dst = StageSpot(_stage + 1);
            Vector3 src = fromPile && bonePile != null ? bonePile.bounds.center : LaidSpot();
            if (label && boneLabel != null) Fx(new[] { boneLabel }, src + new Vector3(0f, 0.35f, 0f), 2f, 0.15f, 24);
            yield return Arc(src, dst, per * 0.45f, keep: false);
            SetStage(_stage + 1);
            StartCoroutine(Settle());
            SunodGame.Core.vc_Sfx.Play("clack", 0.55f, 0.1f);
            Fx(sparkFrames, dst, 12f, 0.1f);
            if (_laid.Count > 0) { Destroy(_laid[0]); _laid.RemoveAt(0); }
            yield return BarStep(per * 0.55f, (t + per * 0.45f) / total, (t + per) / total);
            t += per;
        }
        if (bonePile != null) bonePile.enabled = false;
    }

    private void Done1(string msg)
    {
        SetStage(3);
        ClearLaid();
        if (bonePile != null) bonePile.enabled = false;
        if (sparkFrames != null) Fx(sparkFrames, Top() + new Vector3(0.3f, -0.3f, 0f), 12f, 0.2f);
        StartCoroutine(Dance());
        Finish(msg);
    }

    private void SetStage(int s)
    {
        _stage = Mathf.Clamp(s, 0, 3);
        if (skeleton != null && stages != null && _stage < stages.Length && stages[_stage] != null) skeleton.sprite = stages[_stage];
    }

    private Vector3 StageSpot(int s)
    {
        if (skeleton == null) return transform.position;
        Vector3 b = skeleton.transform.position;
        return s == 1 ? b + new Vector3(0f, 1.7f, 0f) : s == 2 ? b + new Vector3(0f, 1.1f, 0f) : b + new Vector3(0f, 0.6f, 0f);
    }

    /// <summary>A bone clicks on: the model jolts up a pixel, wobbles and settles.</summary>
    private IEnumerator Settle()
    {
        if (skeleton == null) yield break;
        Transform tr = skeleton.transform;
        int[] px = { 2, -1, 1, 0 };
        foreach (int p in px)
        {
            if (tr == null) yield break;
            tr.localPosition = _skeletonRest + new Vector3(0f, p / 22f, 0f);
            yield return new WaitForSeconds(0.05f);
        }
        if (tr != null) tr.localPosition = _skeletonRest;
        if (dustFrames != null && tr != null) Fx(dustFrames, tr.position + new Vector3(0.35f, 0.05f, 0f), 12f, 0.05f);
    }

    /// <summary>Finished: the skeleton does a happy little jig on its stand.</summary>
    private IEnumerator Dance()
    {
        if (skeleton == null) yield break;
        Transform tr = skeleton.transform;
        for (int i = 0; i < 4 && tr != null; i++)
        {
            skeleton.flipX = i % 2 == 0;
            tr.localPosition = _skeletonRest + new Vector3(0f, 3f / 22f, 0f);
            yield return new WaitForSeconds(0.08f);
            if (tr == null) yield break;
            tr.localPosition = _skeletonRest;
            SunodGame.Core.vc_Sfx.Play("clack", 0.25f, 0.3f);
            yield return new WaitForSeconds(0.08f);
        }
        if (skeleton != null) skeleton.flipX = false;
    }

    private Vector3 LaidSpot() => _laid.Count > 0 && _laid[0] != null ? _laid[0].transform.position : (bonePile != null ? bonePile.bounds.center : transform.position);

    private Vector3 Top() => skeleton != null ? skeleton.transform.position + new Vector3(0f, 2.3f, 0f) : transform.position;

    private IEnumerator Arc(Vector3 from, Vector3 to, float dur, bool keep)
    {
        if (boneFly == null) { yield return new WaitForSeconds(dur); yield break; }
        var go = new GameObject("Bone");
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = boneFly; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 22;
        SunodGame.Core.vc_Sfx.Play("cut_whoosh", 0.3f, 0.15f);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            go.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f));
            // tumbles end over end in 45-degree steps (keeps the pixels crisp)
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(k * 8f) * -45f);
            yield return null;
        }
        if (keep) _laid.Add(go); else Destroy(go);
    }

    private void ClearLaid()
    {
        foreach (var g in _laid) if (g != null) Destroy(g);
        _laid.Clear();
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
