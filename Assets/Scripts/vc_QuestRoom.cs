using System;
using System.Collections.Generic;
using TMPro;
using SunodGame.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class vc_QuestRoom : MonoBehaviour
{
    public event Action<vc_QuestRoom> QuestStarted;
    public event Action<vc_QuestRoom> QuestCompleted;

    [SerializeField] private vc_QuestTimer questTimer;
    [SerializeField] private vc_DoorController exitDoor;
    [SerializeField] private MonoBehaviour questLogic;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private TextMeshProUGUI questCounter;
    [SerializeField] private TextMeshProUGUI questObjective;
    [SerializeField] private TextMeshProUGUI questDescriptionText;
    [SerializeField] private vc_HintSystem hintSystem;
    [SerializeField] private vc_SkillData[] roomSkills = new vc_SkillData[4];
    [SerializeField] private string questId;
    [SerializeField] private string questName;
    [SerializeField] private string primaryRiasec;
    [SerializeField] private string objectiveText;
    [SerializeField] private string questDescription;
    [SerializeField] private string[] questHints;
    [SerializeField] private int totalQuestsInScene = 1;
    [SerializeField] private int currentQuestNumber = 1;
    [SerializeField] private bool isLastQuestInScene = false;

    private bool questStarted = false;
    private bool questResultRecorded = false;
    private bool questCompletionNotified = false;
    private bool isQuestTimerSubscribed = false;
    private vc_IQuestLogic cachedQuestLogic;
    private vc_SkillManager cachedSkillManager;

    private void Awake()
    {
        BoxCollider2D triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        cachedQuestLogic = questLogic as vc_IQuestLogic;
        cachedSkillManager = FindFirstObjectByType<vc_SkillManager>();

        if (questObjective != null)
        {
            questObjective.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!questStarted)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            OnQuestComplete(); // TEMP TEST - REMOVE
        }
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
        if (questCompletionNotified)
        {
            return;
        }

        questCompletionNotified = true;

        if (questTimer != null)
        {
            questTimer.CompleteQuest();
        }

        if (exitDoor != null)
        {
            exitDoor.Unlock();
        }

        if (isLastQuestInScene && nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(true);
        }

        QuestCompleted?.Invoke(this);
    }

    public void LoadNextScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private void TryStartQuest(Collider2D other)
    {
        if (SceneManager.GetActiveScene().name == SceneLoader.SCENE_TUTORIAL)
        {
            return;
        }

        if (questStarted || other == null || !other.CompareTag("Player"))
        {
            return;
        }

        questStarted = true;
        questResultRecorded = false;
        questCompletionNotified = false;

        QuestStarted?.Invoke(this);

        if (questCounter != null)
        {
            questCounter.text = $"Quest {currentQuestNumber}/{totalQuestsInScene}";
        }

        if (questDescriptionText != null)
        {
            questDescriptionText.text = questDescription;
        }

        if (hintSystem != null)
        {
            hintSystem.SetHints(questHints);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(false);
        }

        SubscribeToQuestTimer();

        if (questTimer != null)
        {
            questTimer.StartQuest();
        }

        if (cachedSkillManager != null && HasConfiguredRoomSkills())
        {
            cachedSkillManager.ResetUsageCounts();
            cachedSkillManager.LoadSkills(roomSkills);
        }

        cachedQuestLogic?.BeginQuest(this, questTimer);
    }

    private void SubscribeToQuestTimer()
    {
        if (questTimer == null || isQuestTimerSubscribed)
        {
            return;
        }

        questTimer.QuestEnded += HandleQuestEnded;
        isQuestTimerSubscribed = true;
    }

    private void UnsubscribeFromQuestTimer()
    {
        if (questTimer == null || !isQuestTimerSubscribed)
        {
            return;
        }

        questTimer.QuestEnded -= HandleQuestEnded;
        isQuestTimerSubscribed = false;
    }

    private void HandleQuestEnded(vc_QuestTimer.QuestCompletionResult result)
    {
        if (questResultRecorded)
        {
            UnsubscribeFromQuestTimer();
            return;
        }

        questResultRecorded = true;

        Dictionary<string, int> usageSummary = cachedSkillManager != null
            ? cachedSkillManager.GetUsageSummary()
            : new Dictionary<string, int>();

        vc_SessionTelemetry.Instance?.RecordQuestResult(
            questId,
            questName,
            primaryRiasec,
            result != null && result.DidPassQuest,
            result != null ? result.FinalStarsEarnedNormalized : 0,
            result != null ? result.FinalTimeRemaining : 0f,
            result != null ? result.TotalTimeSpent : 0f,
            usageSummary);

        UnsubscribeFromQuestTimer();
    }

    private bool HasConfiguredRoomSkills()
    {
        if (roomSkills == null || roomSkills.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < roomSkills.Length; i++)
        {
            if (roomSkills[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    public string QuestId => questId;
    public string QuestName => questName;
    public bool IsLastQuestInScene => isLastQuestInScene;
    public int CurrentQuestNumber => currentQuestNumber;
    public int TotalQuestsInScene => totalQuestsInScene;
    public Button NextLevelButton => nextLevelButton;
}
