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
    [SerializeField] private string objectiveText;
    [SerializeField] private string questDescription;
    [SerializeField] private string[] questHints;

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
        if (questCompletionNotified) return;

        questCompletionNotified = true;
        Debug.Log($"[QuestRoom] '{questId}' OnQuestComplete called. QuestTimer={(vc_QuestTimer.Instance == null ? "NULL" : "OK")}, subscribed={isQuestTimerSubscribed}");
        vc_QuestTimer.Instance?.CompleteQuest();

        int stars = vc_QuestTimer.Instance != null ? vc_QuestTimer.Instance.FinalStarsEarned : 5;

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
                vc_FloatingMessage.Instance?.Show("Locked — you don't have the required skill.");
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
            questId, questName, primaryRiasec,
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
    public QuestLockType LockType => lockType;
    public string RequiredSkillTag => requiredSkillTag;
    public bool IsExposureEligible => isExposureEligible;
    public string[] RequiredComboTags => requiredComboTags;
    public string[] SolvableWithTags => solvableWithTags;
}
