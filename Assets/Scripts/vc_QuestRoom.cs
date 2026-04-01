using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class vc_QuestRoom : MonoBehaviour
{
    [SerializeField] private vc_QuestTimer questTimer;
    [SerializeField] private vc_DoorController exitDoor;
    [SerializeField] private MonoBehaviour questLogic;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private TextMeshProUGUI questCounter;
    [SerializeField] private TextMeshProUGUI questObjective;
    [SerializeField] private TextMeshProUGUI questDescriptionText;
    [SerializeField] private vc_HintSystem hintSystem;
    [SerializeField] private vc_SkillData[] roomSkills = new vc_SkillData[4];
    [SerializeField] private string objectiveText;
    [SerializeField] private string questDescription;
    [SerializeField] private string[] questHints;
    [SerializeField] private int totalQuestsInScene = 1;
    [SerializeField] private int currentQuestNumber = 1;
    [SerializeField] private bool isLastQuestInScene = false;

    private bool questStarted = false;
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
    }

    public void LoadNextScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private void TryStartQuest(Collider2D other)
    {
        if (questStarted || other == null || !other.CompareTag("Player"))
        {
            return;
        }

        questStarted = true;

        if (questCounter != null)
        {
            questCounter.text = $"Quest {currentQuestNumber}/{totalQuestsInScene}";
        }

        if (questObjective != null)
        {
            questObjective.text = objectiveText;
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

        if (questTimer != null)
        {
            questTimer.StartQuest();
        }

        if (cachedSkillManager != null && roomSkills != null && roomSkills.Length == 4)
        {
            cachedSkillManager.LoadSkills(roomSkills);
        }

        cachedQuestLogic?.BeginQuest(this, questTimer);
    }
}
