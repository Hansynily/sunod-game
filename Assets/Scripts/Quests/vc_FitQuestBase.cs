using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Shared plumbing for hand-built "skill fit" quests (see vc_CreakyFloorQuest). Handles the
/// skill subscription, the "already working" lock, best-fit takeover, the progress bar,
/// pixel-snapped shake, one-shot sprite effects, a walking helper NPC, and the finish flow.
/// A quest script only decides what each skill DOES (OnSkill) and how the props look.
/// Skill zones gate WHERE skills can be pressed, so no proximity checks here.
/// </summary>
public abstract class vc_FitQuestBase : MonoBehaviour, vc_IQuestLogic
{
    [Header("Shared")]
    [SerializeField] protected vc_FloatingMarker mainMarker;
    [Tooltip("Town NPC walk frames for the 'Call' helper (npc_<look>_f0..f5).")]
    [SerializeField] protected Sprite[] helperWalk;
    [SerializeField] protected string sortingLayer = "Default";

    protected vc_QuestRoom QuestRoom;
    protected bool Started, Done, Working;

    private vc_QuestTimer _timer;
    private Coroutine _work;
    private Transform _bar, _barFill;
    private SpriteRenderer _helper;
    private vc_QuestGoalMarker _goal;

    // ── Quest-specific hooks ─────────────────────────────────────────────

    protected abstract void OnBegin();
    protected abstract void OnSkill(vc_SkillData skill);
    /// <summary>Put props back after a cancelled slow fix (bar and helper are cleaned up here).</summary>
    protected virtual void OnWorkCancelled() { }
    /// <summary>World position the progress bar's left edge sits at.</summary>
    protected abstract Vector3 BarWorldPosition { get; }

    // ── Skill system v2 hooks (SKILL_SYSTEM_DESIGN.md) ───────────────────

    /// <summary>This quest's best fit / prep / preference / lines. Null = presses + XP only.</summary>
    protected virtual vc_QuestSkillProfile SkillProfile => null;

    /// <summary>False for the minigame quests ("New" gameplay): no press limit, no XP.</summary>
    protected virtual bool UsesSkillSystem => true;

    /// <summary>
    /// The prep action (~1 s) when the prep skill is pressed first. Default: a toast and a
    /// short wait. Afterwards the unlocked skill glows on its button and works instantly.
    /// </summary>
    protected virtual IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        string line = SkillProfile?.prepLine;
        Toast(string.IsNullOrEmpty(line) ? $"You use {prepSkill.DisplayName} to size up the problem..." : line, 1.4f);
        yield return new WaitForSeconds(1f);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    public void BeginQuest(vc_QuestRoom questRoom, vc_QuestTimer questTimer)
    {
        CancelWork();
        QuestRoom = questRoom;
        Started = true;
        Done = false;
        Working = false;

        if (_timer != null) _timer.QuestEnded -= HandleQuestEnded;
        _timer = questTimer;
        if (_timer != null) _timer.QuestEnded += HandleQuestEnded;

        ResetSkillSystem();
        OnBegin();
        Subscribe();
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!Started || Done || skill == null || skill.SkillData == null) return;
        vc_SkillData d = skill.SkillData;

        if (!UsesSkillSystem) { OnSkill(d); return; }

        // Every press uses one, hints and prep included. Only reachable at 0 while a fix runs.
        if (_pressesLeft <= 0)
        {
            Toast("No presses left. Let it finish!", 1.6f);
            return;
        }
        _pressesLeft--;
        ShowPresses(true);

        ReadPress(d);
        if (IsPrepPress(d)) StartCoroutine(PrepRoutine(d));
        else OnSkill(d);

        if (_pressesLeft <= 0 && !Done && !Working && !_prepping) StartCoroutine(OutOfPresses());
    }

    // The timer ends a failed quest without going through Finish; stop reacting to skills.
    private void HandleQuestEnded(vc_QuestTimer.QuestCompletionResult result)
    {
        if (_timer != null) _timer.QuestEnded -= HandleQuestEnded;
        if (Done) return;
        Started = false;
        HideGoal();
        CancelWork();
        Unsubscribe();
        EndSkillSystem();
    }

    // ── Work lock ────────────────────────────────────────────────────────

    /// <summary>Start a slow fix. The best-fit skill calls this too, replacing any slow fix.</summary>
    protected void StartWork(IEnumerator routine)
    {
        CancelWork();
        Working = true;
        _workSkill = CurrentSkill;
        _workPath = CurrentPath;
        _workInstant = CurrentInstant;
        _work = StartCoroutine(routine);
    }

    protected void CancelWork()
    {
        if (_work != null) StopCoroutine(_work);
        _work = null;
        Working = false;
        _workSkill = null;
        ShowBar(false);
        if (_helper != null) Destroy(_helper.gameObject);
        _helper = null;
        HideGoal();
        OnWorkCancelled();
    }

    /// <summary>True (and shows the toast) when a slow fix is already running.</summary>
    protected bool BlockedByWork()
    {
        if (!Working) return false;
        Toast("Already working on it...", 1.5f);
        return true;
    }

    protected static void Toast(string msg, float seconds = 2f) => vc_QuestHUD.Instance?.ShowFeedbackTimed(msg, seconds);

    // ── Finish ───────────────────────────────────────────────────────────

    protected void Finish(string msg)
    {
        if (Done) return;
        Done = true;
        Working = false;
        _work = null;
        ShowBar(false);
        HideGoal();
        Unsubscribe();
        SunodGame.Core.vc_Sfx.Play("step_done", 0.8f);
        vc_FloatingMessage.Instance?.Show(msg);

        // XP lands before the quest completes, so the completion checkpoint saves it. The
        // reactions play alongside: the clock stops at the same moment as before.
        if (UsesSkillSystem) AwardFinish();
        EndSkillSystem();
        StartCoroutine(CompleteAfter(0.6f));
    }

    private IEnumerator CompleteAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Started = false;
        vc_QuestHUD.Instance?.CheckObjective(0);
        if (mainMarker != null) mainMarker.Hide();
        if (QuestRoom != null) QuestRoom.OnQuestComplete();
    }

    // ── Progress bar ─────────────────────────────────────────────────────

    protected void ShowBar(bool on)
    {
        if (on && _bar == null)
        {
            Sprite px = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            _bar = new GameObject("WorkBar").transform;
            _bar.SetParent(transform, true);
            var bg = _bar.gameObject.AddComponent<SpriteRenderer>();
            bg.sprite = px; bg.color = new Color32(46, 34, 40, 230);
            bg.sortingLayerName = sortingLayer; bg.sortingOrder = 30;
            _bar.localScale = new Vector3(1f, 0.16f, 1f);

            _barFill = new GameObject("Fill").transform;
            _barFill.SetParent(_bar, false);
            _barFill.localPosition = new Vector3(0.03f, 0f, 0f);
            var fg = _barFill.gameObject.AddComponent<SpriteRenderer>();
            fg.sprite = px; fg.color = new Color32(255, 204, 66, 255);
            fg.sortingLayerName = sortingLayer; fg.sortingOrder = 31;
            _barFill.localScale = new Vector3(0f, 0.6f, 1f);
        }
        if (_bar != null)
        {
            if (on) _bar.position = BarWorldPosition;
            _bar.gameObject.SetActive(on);
        }
        if (on) SetBar(0f);
    }

    /// <summary>Re-anchors a visible progress bar (for bars that follow a moving character).</summary>
    protected void MoveBar(Vector3 worldPos)
    {
        if (_bar != null && _bar.gameObject.activeSelf) _bar.position = worldPos;
    }

    protected void SetBar(float k)
    {
        if (_barFill == null) return;
        k = Mathf.Clamp01(k);
        _barFill.localScale = new Vector3(Mathf.Round(k * 30f) / 30f * 0.94f, 0.6f, 1f);
    }

    // ── Effects ──────────────────────────────────────────────────────────

    protected static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 32f) / 32f, Mathf.Round(p.y * 32f) / 32f, p.z);

    /// <summary>Plays sprite frames once at a world position, drifting up by 'rise' units, then disappears.</summary>
    protected void Fx(Sprite[] frames, Vector3 worldPos, float fps = 10f, float rise = 0f, int order = 20)
    {
        if (frames == null || frames.Length == 0) return;
        StartCoroutine(FxRoutine(frames, worldPos, fps, rise, order));
    }

    private IEnumerator FxRoutine(Sprite[] frames, Vector3 pos, float fps, float rise, int order)
    {
        var go = new GameObject("Fx");
        go.transform.SetParent(transform, true);
        go.transform.position = Snap(pos);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = order;
        float dur = frames.Length / fps, t = 0f;
        while (t < dur && go != null)
        {
            t += Time.deltaTime;
            sr.sprite = frames[Mathf.Min(frames.Length - 1, (int)(t * fps))];
            go.transform.position = Snap(pos + new Vector3(0f, rise * (t / dur), 0f));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>1-px-step horizontal shake of a transform around its resting local position.</summary>
    protected IEnumerator Shake(Transform tr, Vector3 restLocal, float duration, int px)
    {
        if (tr == null) yield break;
        float t = 0f;
        int i = 0;
        while (t < duration && tr != null)
        {
            t += Time.deltaTime;
            tr.localPosition = restLocal + new Vector3((i++ % 2 == 0 ? 1 : -1) * px / 22f, 0f, 0f);
            yield return new WaitForSeconds(0.04f);
        }
        if (tr != null) tr.localPosition = restLocal;
    }

    // ── Goal marker (walk-along paths like Guide) ────────────────────────

    /// <summary>Shows an arrow over 'target' and a pointer by the player aiming at it.</summary>
    protected void ShowGoal(Transform target)
    {
        HideGoal();
        if (target == null) return;
        _goal = vc_QuestGoalMarker.Create(transform, target, target.position, sortingLayer);
    }

    protected void HideGoal()
    {
        if (_goal != null) Destroy(_goal.gameObject);
        _goal = null;
    }

    // ── Walking helper (Call path) ───────────────────────────────────────

    protected SpriteRenderer SpawnHelper(Vector3 at, int order)
    {
        if (_helper != null) Destroy(_helper.gameObject);
        var go = new GameObject("Helper");
        go.transform.SetParent(transform, true);
        go.transform.position = Snap(at);
        _helper = go.AddComponent<SpriteRenderer>();
        if (helperWalk != null && helperWalk.Length > 0) _helper.sprite = helperWalk[0];
        _helper.sortingLayerName = sortingLayer;
        _helper.sortingOrder = order;
        return _helper;
    }

    /// <summary>Walks the spawned helper in a straight line, animating the walk frames.</summary>
    protected IEnumerator HelperWalk(Vector3 from, Vector3 to, float seconds, Action<float> each = null)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            if (_helper != null)
            {
                _helper.transform.position = Snap(Vector3.Lerp(from, to, t / seconds));
                if (helperWalk != null && helperWalk.Length > 0)
                    _helper.sprite = helperWalk[(int)(t * 8f) % helperWalk.Length];
            }
            each?.Invoke(t);
            yield return null;
        }
        if (_helper != null && helperWalk != null && helperWalk.Length > 0) _helper.sprite = helperWalk[0];
    }

    // ── Skill system v2: presses, paths, levels, prep, XP ────────────────
    // SKILL_SYSTEM_DESIGN.md. The quest script reads the current press (CurrentPath,
    // CurrentLevel, CurrentInstant) and asks PathTime / PathPresses how long its path takes;
    // everything else (press limit, prep, XP, reactions) happens here.

    /// <summary>The skill a prep made instant (glows on its button), or null. Read by the HUD.</summary>
    public static string PrepGlowTag { get; private set; }

    /// <summary>One line about the last finished quest's XP, shown on the quest-done banner.</summary>
    public static string LastXpReport { get; private set; }
    public static string TakeXpReport() { string r = LastXpReport; LastXpReport = null; return r; }

    protected vc_SkillData CurrentSkill { get; private set; }
    protected vc_SkillPath CurrentPath { get; private set; }
    protected int CurrentLevel { get; private set; } = 1;
    /// <summary>The pressed skill is the one the prep unlocked: it runs at best-fit speed.</summary>
    protected bool CurrentInstant { get; private set; }
    protected bool Prepared => _prepared;
    protected int Stage => vc_SkillLevels.StageIndex;

    private int _pressesLeft, _pressesTotal;
    private bool _prepared, _prepping;
    private vc_SkillData _prepSkill;
    private vc_SkillData _workSkill;
    private vc_SkillPath _workPath;
    private bool _workInstant;

    /// <summary>Seconds this path takes for the current press (level, stage and prep applied).</summary>
    protected float PathTime(float baseSeconds)
    {
        vc_QuestSkillProfile p = SkillProfile;
        float best = p != null ? p.bestFitSeconds : baseSeconds;
        if (CurrentInstant) return best;
        if (p == null) return baseSeconds;   // quest not redesigned yet: authored times
        return vc_SkillLevels.PathSeconds(CurrentPath, baseSeconds, CurrentLevel, Stage, best);
    }

    /// <summary>Presses an "x3" path needs for the current skill (fewer with levels, 1 when prepped).</summary>
    protected int PathPresses(int basePresses)
    {
        if (CurrentInstant) return 1;
        if (SkillProfile == null || CurrentPath != vc_SkillPath.SameCategory) return basePresses;
        return vc_SkillLevels.PathPresses(basePresses, CurrentLevel);
    }

    private void ResetSkillSystem()
    {
        _pressesTotal = UsesSkillSystem ? vc_SkillLevels.PressesForStage(Stage) : 0;
        _pressesLeft = _pressesTotal;
        _prepared = _prepping = false;
        _prepSkill = _workSkill = null;
        CurrentSkill = null;
        CurrentPath = vc_SkillPath.Hint;
        CurrentLevel = 1;
        CurrentInstant = false;
        PrepGlowTag = null;
        LastXpReport = null;
        ShowPresses(UsesSkillSystem);
    }

    private void EndSkillSystem()
    {
        PrepGlowTag = null;
        ShowPresses(false);
    }

    private void ShowPresses(bool on) =>
        vc_RunProgressUI.Instance?.SetPresses(on ? _pressesLeft : 0, on ? _pressesTotal : 0);

    private void ReadPress(vc_SkillData d)
    {
        CurrentSkill = d;
        CurrentPath = ClassifyPath(d);
        CurrentLevel = vc_SkillLevels.Level(d);
        vc_QuestSkillProfile p = SkillProfile;
        CurrentInstant = _prepared && p != null && !string.IsNullOrEmpty(p.prepUnlocksTag) && d.HasTag(p.prepUnlocksTag);
    }

    /// <summary>Best fit (the profile says so), same category (quest's letter) or other category.</summary>
    protected vc_SkillPath ClassifyPath(vc_SkillData d)
    {
        if (d == null) return vc_SkillPath.Hint;
        vc_QuestSkillProfile p = SkillProfile;
        if (p != null && !string.IsNullOrEmpty(p.bestFitTag) && d.HasTag(p.bestFitTag)) return vc_SkillPath.BestFit;
        string quest = QuestRoom != null ? QuestRoom.PrimaryRiasec : null;
        string letter = d.riaSecLetter;
        if (!string.IsNullOrEmpty(quest) && !string.IsNullOrEmpty(letter)
            && quest.Trim().StartsWith(letter.Trim(), System.StringComparison.OrdinalIgnoreCase))
            return vc_SkillPath.SameCategory;
        return vc_SkillPath.OtherCategory;
    }

    private bool IsPrepPress(vc_SkillData d)
    {
        vc_QuestSkillProfile p = SkillProfile;
        return p != null && !_prepared && !_prepping && !Working
            && !string.IsNullOrEmpty(p.prepTag) && d.HasTag(p.prepTag);
    }

    private IEnumerator PrepRoutine(vc_SkillData d)
    {
        _prepping = true;
        _prepSkill = d;
        yield return OnPrep(d);
        _prepping = false;
        if (!Started || Done) yield break;
        _prepared = true;

        vc_QuestSkillProfile p = SkillProfile;
        PrepGlowTag = p.prepUnlocksTag;
        vc_SkillData unlocked = FindSkillWithTag(p.prepUnlocksTag);
        string name = unlocked != null ? unlocked.DisplayName : p.prepUnlocksTag;
        bool holding = unlocked != null && vc_SkillManager.Instance != null
            && vc_SkillManager.Instance.GetSkillInCategory(unlocked.riaSecLetter) == unlocked;
        Toast(holding ? $"Prepared! {name} will do it in one go." : $"Prepared! {name} could do this in one go.", 2.4f);
        SunodGame.Core.vc_Sfx.Play("idea", 0.7f, 0.05f);
        vc_RunProgressUI.Instance?.ShowStamp("Prepared!", new Color32(150, 110, 255, 255));

        // the prep was the last press and nothing can follow
        if (_pressesLeft <= 0 && !Done && !Working) StartCoroutine(OutOfPresses());
    }

    private IEnumerator OutOfPresses()
    {
        Toast("Out of presses...", 1.6f);
        yield return new WaitForSeconds(0.9f);
        if (!Started || Done || Working) yield break;   // a fix started in the meantime
        vc_QuestTimer.Instance?.AbortQuest();           // same as the timer running out
    }

    /// <summary>XP, the Lv3 / prep surprise line, the NPC's preference, the "Perfect fit!" stamp.</summary>
    private void AwardFinish()
    {
        vc_SkillData who = _workSkill != null ? _workSkill : CurrentSkill;
        vc_SkillPath path = _workSkill != null ? _workPath : CurrentPath;
        bool instant = _workSkill != null ? _workInstant : CurrentInstant;
        // finished by walking (a walk-solve path): no skill did it, even if a hint was pressed before
        if (_workSkill == null && who != null && QuestRoom != null && !QuestRoom.AcceptsSkill(who)) who = null;
        if (who == null) return;

        int level = vc_SkillLevels.Level(who);
        vc_QuestSkillProfile p = SkillProfile;
        var report = new System.Text.StringBuilder();

        int xp = vc_SkillLevels.XpFor(path);
        int newLevel = vc_SkillLevels.AddXp(who, xp);
        if (xp > 0) report.Append($"{who.DisplayName} +{xp} XP");
        if (newLevel > level) report.Append($"  (Lv{newLevel}!)");

        if (_prepSkill != null && _prepSkill != who)
        {
            int prepBefore = vc_SkillLevels.Level(_prepSkill);
            int prepAfter = vc_SkillLevels.AddXp(_prepSkill, vc_SkillLevels.XpPrep);
            report.Append($"   {_prepSkill.DisplayName} +{vc_SkillLevels.XpPrep} XP");
            if (prepAfter > prepBefore) report.Append($"  (Lv{prepAfter}!)");
        }
        LastXpReport = report.Length > 0 ? report.ToString() : null;

        // Stamp: the best fit, or a same-category skill at best-fit speed (Lv3 or prepped).
        bool fastSame = path == vc_SkillPath.SameCategory && (level >= vc_SkillLevels.MaxLevel || instant);
        if (path == vc_SkillPath.BestFit) vc_RunProgressUI.Instance?.ShowStamp("Perfect fit!", new Color32(255, 196, 40, 255));
        else if (fastSame) vc_RunProgressUI.Instance?.ShowStamp(instant ? "One go!" : "Mastered!", new Color32(255, 196, 40, 255));

        string npc = p != null && !string.IsNullOrEmpty(p.npcName) ? p.npcName : "They";
        string line = fastSame ? p?.FastLineFor(who) : null;
        bool preferred = p != null && !string.IsNullOrEmpty(p.preferredTag) && who.HasTag(p.preferredTag);
        if (preferred && !string.IsNullOrEmpty(p.preferenceLine)) line = string.IsNullOrEmpty(line) ? p.preferenceLine : line;

        if (!string.IsNullOrEmpty(line)) StartCoroutine(SayLater($"{npc}: \"{line}\"", 0.5f));
        if (preferred) StartCoroutine(PreferenceHeart(npc));
    }

    private IEnumerator SayLater(string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        Toast(text, 3.2f);
    }

    // NPC preferences give a bonus only (the bonus currency is still to be designed, TASKS §14).
    private IEnumerator PreferenceHeart(string npc)
    {
        yield return new WaitForSeconds(0.25f);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.45f, 0.1f);
        vc_RunProgressUI.Instance?.ShowStamp($"{npc} loved it!", new Color32(240, 110, 150, 255), 0.55f);
    }

    protected static vc_SkillData FindSkillWithTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        var filter = vc_QuestAvailabilityFilter.Instance;
        vc_SkillData[] all = filter != null && filter.GameSettings != null ? filter.GameSettings.skills : null;
        if (all == null) return null;
        foreach (vc_SkillData s in all) if (s != null && s.HasTag(tag)) return s;
        return null;
    }

    // ── Skill subscription ───────────────────────────────────────────────

    private void Subscribe()
    {
        if (vc_SkillManager.Instance == null) return;
        vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
        vc_SkillManager.Instance.SkillUsed += HandleSkillUsed;
    }

    protected void Unsubscribe()
    {
        if (vc_SkillManager.Instance != null)
            vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
        if (_timer != null) _timer.QuestEnded -= HandleQuestEnded;
    }
}
