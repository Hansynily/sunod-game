using System;
using System.Collections.Generic;
using SunodGame.Core;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class vc_QuestRoom : MonoBehaviour
{
    public enum QuestLockType { None, Soft, Hard }

    public static event Action OnAnyQuestComplete;

    public event Action<vc_QuestRoom> QuestStarted;
    public event Action<vc_QuestRoom> QuestCompleted;

    [SerializeField] private MonoBehaviour questLogic;
    [SerializeField] private string questId;
    [SerializeField] private string questName;
    [SerializeField] private string primaryRiasec;
    [Tooltip("Option B: the RIASEC questionnaire item this quest represents (R1..C8). Fill per room. Leave blank until assigned - the incomplete-run guard handles unfilled quests.")]
    [SerializeField] private string questionCode;
    [SerializeField] private string objectiveText;
    [SerializeField] private string questDescription;
    [SerializeField] private string[] questHints;

    [Header("Save")]
    [Tooltip("Quest ID of the next quest in sequence. Set on each room so Continue picks up here. Leave blank on the final quest.")]
    [SerializeField] private string nextQuestId;

    [Header("Availability")]
    [SerializeField] private QuestLockType lockType = QuestLockType.None;
    [SerializeField] private string requiredSkillTag;
    [SerializeField] private bool isExposureEligible;
    [Tooltip("ALL of these tags must be in the player's inventory for this quest to surface. Leave empty = universally solvable.")]
    [SerializeField] private string[] requiredComboTags;
    [Tooltip("Legacy OR-logic pool. Leave empty unless quest has multiple independent single-skill solutions.")]
    [SerializeField] private string[] solvableWithTags;

    private bool questStarted = false;
    private bool questResultRecorded = false;
    private bool questCompletionNotified = false;
    private bool isQuestTimerSubscribed = false;
    private vc_IQuestLogic cachedQuestLogic;

    private void Awake()
    {
        BoxCollider2D triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;

        cachedQuestLogic = questLogic as vc_IQuestLogic;
    }

    private void OnDestroy()
    {
        UnsubscribeFromQuestTimer();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartQuest(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartQuest(other);
    }

    public void OnQuestComplete()
    {
        // questResultRecorded is set when the timer's fail already ended this quest -
        // a late NPC arrival after expiry must not also complete it.
        if (questCompletionNotified || questResultRecorded) return;

        questCompletionNotified = true;
        Debug.Log($"[QuestRoom] '{questId}' OnQuestComplete called. QuestTimer={(vc_QuestTimer.Instance == null ? "NULL" : "OK")}, subscribed={isQuestTimerSubscribed}");
        vc_QuestTimer.Instance?.CompleteQuest();

        int stars = vc_QuestTimer.Instance != null ? vc_QuestTimer.Instance.FinalStarsEarned : 3;

        WriteQuestSave(stars);

        if (vc_QuestDonePopup.Instance != null)
            vc_QuestDonePopup.Instance.Show(questName, stars, OnQuestDoneAcknowledged);
        else
            OnQuestDoneAcknowledged();

        QuestCompleted?.Invoke(this);
    }

    private void OnQuestDoneAcknowledged()
    {
        OnAnyQuestComplete?.Invoke();
    }

    private void TryStartQuest(Collider2D other)
    {
        if (SceneLoader.SCENE_TUTORIAL == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) return;
        if (questStarted || other == null || !other.CompareTag("Player")) return;

        if (lockType == QuestLockType.Hard)
        {
            bool available = vc_QuestAvailabilityFilter.Instance != null
                && vc_QuestAvailabilityFilter.Instance.IsAvailable(this);
            if (!available)
            {
                vc_FloatingMessage.Instance?.Show("Locked. You need a different skill.");
                return;
            }
        }

        questStarted = true;
        ActiveRoom = this;
        questResultRecorded = false;
        questCompletionNotified = false;

        Debug.Log($"[QuestRoom] '{questId}' starting. QuestTimer={(vc_QuestTimer.Instance == null ? "NULL" : "OK")}, QuestLogic={(cachedQuestLogic == null ? "NULL" : cachedQuestLogic.GetType().Name)}");

        vc_Sfx.Play("quest_start", 0.8f);
        QuestStarted?.Invoke(this);

        vc_QuestHUD.Instance?.SetDescription(questDescription);
        vc_QuestHUD.Instance?.SetObjective(objectiveText);
        vc_QuestHUD.Instance?.SetHints(questHints);

        vc_SkillZone.ResetCounter();
        vc_SkillManager.Instance?.SetSkillsInteractable(false);

        SubscribeToQuestTimer();
        vc_QuestTimer.Instance?.StartQuest();
        vc_SkillManager.Instance?.ResetUsageCounts();

        cachedQuestLogic?.BeginQuest(this, vc_QuestTimer.Instance);
    }

    private void SubscribeToQuestTimer()
    {
        if (vc_QuestTimer.Instance == null || isQuestTimerSubscribed) return;
        vc_QuestTimer.Instance.QuestEnded += HandleQuestEnded;
        isQuestTimerSubscribed = true;
    }

    private void UnsubscribeFromQuestTimer()
    {
        if (vc_QuestTimer.Instance == null || !isQuestTimerSubscribed) return;
        vc_QuestTimer.Instance.QuestEnded -= HandleQuestEnded;
        isQuestTimerSubscribed = false;
    }

    private void HandleQuestEnded(vc_QuestTimer.QuestCompletionResult result)
    {
        Debug.Log($"[QuestRoom] '{questId}' HandleQuestEnded fired. alreadyRecorded={questResultRecorded}, passed={result?.DidPassQuest}");
        if (ActiveRoom == this) ActiveRoom = null;
        if (questResultRecorded) { UnsubscribeFromQuestTimer(); return; }

        questResultRecorded = true;

        Dictionary<string, int> usageSummary = vc_SkillManager.Instance != null
            ? vc_SkillManager.Instance.GetUsageSummary()
            : new Dictionary<string, int>();

        vc_SessionTelemetry.Instance?.RecordQuestResult(
            questId, questName, primaryRiasec, questionCode,
            result != null && result.DidPassQuest,
            result != null ? result.FinalStarsEarnedNormalized : 0,
            result != null ? result.FinalTimeRemaining : 0f,
            result != null ? result.TotalTimeSpent : 0f,
            usageSummary);

        UnsubscribeFromQuestTimer();
    }

    /// <summary>The room whose quest is running right now (null between quests).</summary>
    public static vc_QuestRoom ActiveRoom { get; private set; }

    private void OnDisable()
    {
        if (ActiveRoom == this) ActiveRoom = null;
    }

    /// <summary>
    /// Does this quest accept the skill? True when one of its tags is in Solvable With Tags or
    /// Required Combo Tags; a quest that lists no tags accepts skills of its own RIASEC letter.
    /// Used by the skill cooldown: a fitting press is never penalised.
    /// </summary>
    public bool AcceptsSkill(vc_SkillData skill)
    {
        if (skill == null) return false;
        bool anyTags = false;
        foreach (string[] list in new[] { solvableWithTags, requiredComboTags })
        {
            if (list == null) continue;
            foreach (string t in list)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                anyTags = true;
                if (skill.HasTag(t)) return true;
            }
        }
        if (anyTags) return false;
        return !string.IsNullOrEmpty(primaryRiasec) && !string.IsNullOrEmpty(skill.riaSecLetter)
            && primaryRiasec.StartsWith(skill.riaSecLetter.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    public string QuestId => questId;
    public string QuestName => questName;
    public string PrimaryRiasec => primaryRiasec;
    public string QuestionCode => questionCode;
    public QuestLockType LockType => lockType;
    public string RequiredSkillTag => requiredSkillTag;
    public bool IsExposureEligible => isExposureEligible;
    public string[] RequiredComboTags => requiredComboTags;
    public string[] SolvableWithTags => solvableWithTags;

    /// <summary>True once this quest has ended, whether it succeeded or failed on the timer.
    /// Used by vc_AreaSeal to decide whether a hallway can close behind the player.</summary>
    public bool IsFinished => questResultRecorded || questCompletionNotified;
    public bool IsInProgress => questStarted && !IsFinished;

    // ── Save ────────────────────────────────────────────────────────────────

    private static readonly string[] RiasecOrder = { "R", "I", "A", "S", "E", "C" };

    /// <summary>
    /// Town mode: a failed quest still counts as attempted (one try per quest), so it must
    /// survive Continue too. Saves the attempt and pushes the checkpoint without touching
    /// the local RIASEC scores or stars. Passed quests are already saved by OnQuestComplete.
    /// </summary>
    public void WriteAttemptCheckpoint()
    {
        if (questCompletionNotified) return;
        SaveProgressIds(new[] { questId }, gameObject.scene.name);
    }

    /// <summary>
    /// Town mode: a quest that was started but never finished (quit to the menu, or the app
    /// closed mid-quest). Recorded as a failed attempt, so its item scores 1 (same rule as a
    /// timer fail), and saved for Continue. Can be called on the quest prefab's room.
    /// </summary>
    public void RecordNotFinished(string sceneName, params string[] extraSaveIds)
    {
        vc_SessionTelemetry.Instance?.RecordQuestResult(
            questId, questName, primaryRiasec, questionCode,
            false, 0, 0f, 0f, null);

        var ids = new List<string> { questId };
        if (extraSaveIds != null) ids.AddRange(extraSaveIds);
        SaveProgressIds(ids, sceneName);
    }

    /// <summary>
    /// Adds ids to the save's completed list (quest ids, or town markers like "left:&lt;id&gt;")
    /// and pushes the run-state checkpoint, without touching RIASEC scores or stars.
    /// </summary>
    public static void SaveProgressIds(IEnumerable<string> ids, string sceneName)
    {
        vc_SaveManager.SaveData data = vc_SaveManager.Load() ?? new vc_SaveManager.SaveData { riasecScores = new int[6] };
        if (ids != null)
            foreach (string id in ids)
                if (!string.IsNullOrWhiteSpace(id) && !data.completedQuestIds.Contains(id))
                    data.completedQuestIds.Add(id);

        if (vc_PlayerInventory.Instance != null)
            data.unlockedSkills = OwnedSkillNamesEquippedLast();

        vc_SaveManager.Save(data);
        PushRunStateCheckpoint(data, sceneName);
    }

    /// <summary>
    /// Owned skill names with the ones sitting in the skill buttons moved to the end. Restore
    /// equips in list order (last of each kind wins its slot), so this brings back the
    /// player's own loadout, not just their inventory.
    /// </summary>
    public static List<string> OwnedSkillNamesEquippedLast()
    {
        var owned = new List<string>();
        var equipped = new List<string>();
        if (vc_PlayerInventory.Instance == null) return owned;

        var inSlots = new HashSet<vc_SkillData>();
        if (vc_SkillManager.Instance != null)
            for (int i = 0; i < vc_SkillManager.SlotCount; i++)
            {
                vc_SkillData s = vc_SkillManager.Instance.GetSkillInSlot(i);
                if (s != null) inSlots.Add(s);
            }

        foreach (vc_SkillData s in vc_PlayerInventory.Instance.GatheredSkills)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.skillName)) continue;
            (inSlots.Contains(s) ? equipped : owned).Add(s.skillName);
        }
        owned.AddRange(equipped);
        return owned;
    }

    private void WriteQuestSave(int starsEarned)
    {
        vc_SaveManager.SaveData data = vc_SaveManager.Load() ?? new vc_SaveManager.SaveData
        {
            riasecScores = new int[6]
        };

        data.currentQuestId = nextQuestId;

        int riasecIndex = System.Array.FindIndex(RiasecOrder,
            r => primaryRiasec.StartsWith(r, System.StringComparison.OrdinalIgnoreCase));

        if (riasecIndex >= 0)
            data.riasecScores[riasecIndex] += Mathf.Max(1, starsEarned);

        if (!string.IsNullOrWhiteSpace(questId) && !data.completedQuestIds.Contains(questId))
            data.completedQuestIds.Add(questId);

        data.totalStars += Mathf.Max(0, starsEarned);
        data.tutorialComplete = SessionState.Instance != null && SessionState.Instance.HasCompletedTutorial;

        // Capture the player's currently-owned skills by name (unique per skill) so Continue
        // can re-equip them. Snapshot from the live inventory, not the old empty local list.
        if (vc_PlayerInventory.Instance != null)
            data.unlockedSkills = OwnedSkillNamesEquippedLast();

        vc_SaveManager.Save(data);
        Debug.Log($"[QuestRoom] Save written - nextQuestId='{nextQuestId}', riasec[{riasecIndex}]+={Mathf.Max(1, starsEarned)}");

        PushRunStateCheckpoint(data, gameObject.scene.name);
    }

    // Server save-state (Continue/Reset) - pushed after each quest so the resumed run's
    // checkpoint always reflects what was actually completed. Fire-and-forget: a failed
    // push just means the NEXT quest's push retries with the fuller picture; it must
    // never block gameplay.
    private static void PushRunStateCheckpoint(vc_SaveManager.SaveData data, string sceneName)
    {
        SunodGame.Telemetry.TelemetryManager telemetryManager = SunodGame.Telemetry.TelemetryManager.Instance;
        if (telemetryManager == null) return;

        var payload = new SunodGame.Models.RunStatePayload
        {
            session_id = vc_SessionTelemetry.Instance != null ? vc_SessionTelemetry.Instance.SessionId : string.Empty,
            completed_quest_ids = new List<string>(data.completedQuestIds),
            // Per-quest results ride along so Continue can rebuild the playthrough's
            // telemetry records. The telemetry list is already cumulative within this
            // session, and seeded from this same checkpoint on resume, so this is
            // always the whole playthrough.
            quest_records = vc_SessionTelemetry.Instance != null
                ? vc_SessionTelemetry.Instance.BuildRunStateRecords()
                : new List<SunodGame.Models.RunStateQuestRecordDto>(),
            owned_skills = new List<string>(data.unlockedSkills),
            total_stars = data.totalStars,
            floor_scene = SunodGame.Core.vc_FloorLoader.Instance != null && !string.IsNullOrEmpty(SunodGame.Core.vc_FloorLoader.Instance.CurrentFloorScene)
                ? SunodGame.Core.vc_FloorLoader.Instance.CurrentFloorScene
                : (!string.IsNullOrEmpty(sceneName) ? sceneName : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name),
            tutorial_completed = data.tutorialComplete,
        };

        var riasec = new Dictionary<string, float>();
        for (int i = 0; i < RiasecOrder.Length && i < data.riasecScores.Length; i++)
            riasec[RiasecOrder[i]] = data.riasecScores[i];

        telemetryManager.PutMyRunState(payload, riasec,
            onError: error => Debug.LogWarning($"[QuestRoom] Run-state checkpoint push failed (will retry next quest): {error}"));
    }
}
