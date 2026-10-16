using System.Collections;
using UnityEngine;

/// <summary>
/// I quest (I1). Put Ms. Garcia's skeleton model together. Any APPROPRIATE skill works - how well
/// it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   survey   I  best fit     identifies each bone; labels pop and bones fly into place      ~2s
///   unlock   I  workaround   works out the order: skull, ribs, arms, legs                   4s
///   plan     I  workaround   lays the bones out by body region, then assembles              5s
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
    [SerializeField] private float healTime = 8f;
    [SerializeField] private float buildTime = 8f;

    private int _stage;
    private float _idleClock;
    private readonly System.Collections.Generic.List<GameObject> _laid = new System.Collections.Generic.List<GameObject>();

    protected override Vector3 BarWorldPosition =>
        (skeleton != null ? skeleton.transform.position : transform.position) + new Vector3(-0.5f, 2.4f, 0f);

    protected override void OnBegin()
    {
        ClearLaid();
        _stage = 0; _idleClock = 1.5f;
        SetStage(0);
        if (bonePile != null) bonePile.enabled = true;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Skeleton Model",
            "Help Ms. Garcia put the skeleton model together for biology.",
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
        if (d.HasTag("unlock")) { StartWork(SolveRoutine()); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        if (d.HasTag("heal")) { StartWork(HealRoutine()); return; }
        if (d.HasTag("build")) { StartWork(BuildRoutine()); return; }
        Toast("This needs some careful study.", 2.2f);
    }

    protected override void OnWorkCancelled() => ClearLaid();

    // ── Survey: identify, label, bones fly into place ────────────────────

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

    private IEnumerator SolveRoutine()
    {
        Toast("Skull, ribs, arms, legs... in order.", solveTime);
        ShowBar(true);
        float think = 1f;
        if (questionBubble != null) Fx(new[] { questionBubble }, Top(), 1.2f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("idea", 0.4f);
        yield return BarStep(think, 0f, think / solveTime);
        yield return AssembleSteps(think, solveTime, solveTime - think, label: false, fromPile: true);
        Done1("Puzzle solved. The skeleton stands tall!");
    }

    // ── Plan: lay out by region, then assemble ───────────────────────────

    private IEnumerator PlanRoutine()
    {
        Toast("You lay the bones out by body region...", planTime);
        ShowBar(true);
        float lay = 2.4f, t = 0f;
        int n = regionSpots != null ? regionSpots.Length : 0;
        float per = lay / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            if (bonePile != null && boneFly != null)
                yield return Arc(bonePile.bounds.center, regionSpots[i].position, per * 0.6f, keep: true);
            if (boneLabel != null) Fx(new[] { boneLabel }, regionSpots[i].position + new Vector3(0f, 0.3f, 0f), 1.2f, 0.1f, 24);
            yield return BarStep(per * 0.4f, (t + per * 0.6f) / planTime, (t + per) / planTime);
            t += per;
        }
        if (bonePile != null) bonePile.enabled = false;
        yield return AssembleSteps(t, planTime, planTime - t, label: false, fromPile: false);
        Done1("Sorted by region, then built. Nicely planned!");
    }

    // ── Heal: name the bones from first aid, slowly ──────────────────────

    private IEnumerator HealRoutine()
    {
        Toast("You name bones from first-aid lessons...", healTime);
        ShowBar(true);
        float recall = 3.2f, t = 0f;
        while (t < recall)
        {
            if (heart != null && t < 0.2f) Fx(new[] { heart }, Top(), 1.5f, 0.2f, 24);
            if (boneLabel != null && bonePile != null) Fx(new[] { boneLabel }, bonePile.bounds.center + new Vector3(Random.Range(-0.4f, 0.4f), 0.35f, 0f), 1.4f, 0.15f, 24);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f, 0.1f);
            yield return BarStep(0.8f, t / healTime, (t + 0.8f) / healTime);
            t += 0.8f;
        }
        yield return AssembleSteps(t, healTime, healTime - t, label: true, fromPile: true);
        Done1("You knew your bones! The skeleton is complete.");
    }

    // ── Build: trial and error by hand ───────────────────────────────────

    private IEnumerator BuildRoutine()
    {
        Toast("You fit bones together, trial and error...", buildTime);
        ShowBar(true);
        float t = 0f;
        // two wrong tries knock bones back off, then it clicks
        for (int miss = 0; miss < 2; miss++)
        {
            yield return Arc(bonePile.bounds.center, Top() + new Vector3(0f, -0.9f, 0f), 0.4f, keep: false);
            if (crossMark != null) Fx(new[] { crossMark }, Top(), 1.4f, 0.15f, 24);
            SunodGame.Core.vc_Sfx.Play("buzz", 0.35f);
            if (skeleton != null) StartCoroutine(Shake(skeleton.transform, skeleton.transform.localPosition, 0.3f, 1));
            Fx(dustFrames, skeleton.transform.position + new Vector3(0.4f, 0.1f, 0f), 10f, 0.1f);
            yield return BarStep(1.6f, (t + 0.4f) / buildTime, (t + 2f) / buildTime);
            t += 2f;
        }
        yield return AssembleSteps(t, buildTime, buildTime - t, label: false, fromPile: true);
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
