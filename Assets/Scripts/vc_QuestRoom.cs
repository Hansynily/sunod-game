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
                vc_FloatingMessage.Instance?.Show("Locked - you don't have the required skill.");
                return;
            }
        }

        questStarted = true;
        questResultRecorded = false;
        questCompletionNotified = false;

        Debug.Log($"[QuestRoom] '{questId}' starting. QuestTimer={(vc_QuestTimer.Instance == null ? "NULL" : "OK")}, QuestLogic={(cachedQuestLogic == null ? "NULL" : cachedQuestLogic.GetType().Name)}");

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

    public string QuestId => questId;
    public string QuestName => questName;
    public string PrimaryRiasec => primaryRiasec;
    public string QuestionCode => questionCode;
    public QuestLockType LockType => lockType;
    public string RequiredSkillTag => requiredSkillTag;
    public bool IsExposureEligible => isExposureEligible;
    public string[] RequiredComboTags => requiredComboTags;
    public string[] SolvableWithTags => solvableWithTags;

    // ── Save ────────────────────────────────────────────────────────────────

    private static readonly string[] RiasecOrder = { "R", "I", "A", "S", "E", "C" };

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
        {
            data.unlockedSkills.Clear();
            foreach (vc_SkillData owned in vc_PlayerInventory.Instance.GatheredSkills)
                if (owned != null && !string.IsNullOrWhiteSpace(owned.skillName))
                    data.unlockedSkills.Add(owned.skillName);
        }

        vc_SaveManager.Save(data);
        Debug.Log($"[QuestRoom] Save written - nextQuestId='{nextQuestId}', riasec[{riasecIndex}]+={Mathf.Max(1, starsEarned)}");

        PushRunStateCheckpoint(data);
    }

    // Server save-state (Continue/Reset) - pushed after each quest so the resumed run's
    // checkpoint always reflects what was actually completed. Fire-and-forget: a failed
    // push just means the NEXT quest's push retries with the fuller picture; it must
    // never block gameplay.
    private void PushRunStateCheckpoint(vc_SaveManager.SaveData data)
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
            floor_scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            tutorial_completed = data.tutorialComplete,
        };

        var riasec = new Dictionary<string, float>();
        for (int i = 0; i < RiasecOrder.Length && i < data.riasecScores.Length; i++)
            riasec[RiasecOrder[i]] = data.riasecScores[i];

        telemetryManager.PutMyRunState(payload, riasec,
            onError: error => Debug.LogWarning($"[QuestRoom] Run-state checkpoint push failed (will retry next quest): {error}"));
    }
}
