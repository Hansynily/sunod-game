using System;
using System.Collections.Generic;
using SunodGame.Core;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class vc_QuestRoom : MonoBehaviour
{
    public event Action<vc_QuestRoom> QuestStarted;
    public event Action<vc_QuestRoom> QuestCompleted;

    [SerializeField] private vc_DoorController exitDoor;
    [SerializeField] private MonoBehaviour questLogic;
    [SerializeField] private string questId;
    [SerializeField] private string questName;
    [SerializeField] private string primaryRiasec;
    [SerializeField] private string objectiveText;
    [SerializeField] private string questDescription;
    [SerializeField] private string[] questHints;

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
        exitDoor?.Unlock();
    }

    private void TryStartQuest(Collider2D other)
    {
        if (SceneLoader.SCENE_TUTORIAL == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) return;
        if (questStarted || other == null || !other.CompareTag("Player")) return;

        questStarted = true;
        questResultRecorded = false;
        questCompletionNotified = false;

        QuestStarted?.Invoke(this);

        vc_QuestHUD.Instance?.SetDescription(questDescription);
        vc_QuestHUD.Instance?.SetObjective(objectiveText);
        vc_QuestHUD.Instance?.SetHints(questHints);

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
}
