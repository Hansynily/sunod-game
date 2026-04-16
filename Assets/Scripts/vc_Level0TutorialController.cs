using TMPro;
using SunodGame.Core;
using SunodGame.Telemetry;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_Level0TutorialController : MonoBehaviour
{
    [SerializeField] private GameObject gateToSkillsRoom;
    [SerializeField] private GameObject markerMoveTarget;
    [SerializeField] private GameObject panelTutorialComplete;
    [SerializeField] private Button btnTutorialBackToMenu;
    [SerializeField] private TMP_Text questObjectiveText;
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private GameObject skill2;
    [SerializeField] private GameObject skill3;
    [SerializeField] private vc_CatQuest catQuest;
    [SerializeField] private vc_SkillSlot skillBuild;
    [SerializeField] private vc_SkillSlot skillCharm;

    private bool moveStepCompleted;
    private bool tutorialCompleted;
    private bool tutorialCompletionSyncInProgress;
    private bool listenersBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneLoader.SCENE_TUTORIAL)
        {
            return;
        }

        GameObject tutorialRootObject = FindSceneObject(scene, "TutorialRoot");
        if (tutorialRootObject == null)
        {
            Debug.LogWarning("[vc_Level0TutorialController] TutorialRoot was not found in the tutorial scene.");
            return;
        }

        if (tutorialRootObject.GetComponent<vc_Level0TutorialController>() == null)
        {
            tutorialRootObject.AddComponent<vc_Level0TutorialController>();
        }
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureMoveTrigger();
    }

    private void Start()
    {
        ResolveReferences();
        BindListeners();
        InitializeTutorial();
    }

    private void OnDestroy()
    {
        if (catQuest != null)
        {
            catQuest.TutorialBuildCompleted -= HandleTutorialBuildCompleted;
            catQuest.TutorialCharmStarted -= HandleTutorialCharmStarted;
            catQuest.TutorialQuestCompleted -= HandleTutorialQuestCompleted;
        }

        if (btnTutorialBackToMenu != null)
        {
            btnTutorialBackToMenu.onClick.RemoveListener(OnBackToMenuClicked);
        }

        listenersBound = false;
    }

    public void HandleTriggerEntered(vc_TutorialTriggerType triggerType)
    {
        if (triggerType != vc_TutorialTriggerType.MoveTarget || moveStepCompleted)
        {
            return;
        }

        moveStepCompleted = true;

        if (markerMoveTarget != null)
        {
            markerMoveTarget.SetActive(false);
        }

        if (gateToSkillsRoom != null)
        {
            gateToSkillsRoom.SetActive(false);
        }

        SetSkillInteractable(skillBuild, true);
        SetSkillInteractable(skillCharm, false);
        SetObjectiveText("Go to the river and press the Build skill button.");
        catQuest?.BeginTutorialQuest();
    }

    private void BindListeners()
    {
        if (listenersBound)
        {
            return;
        }

        if (catQuest != null)
        {
            catQuest.TutorialBuildCompleted -= HandleTutorialBuildCompleted;
            catQuest.TutorialBuildCompleted += HandleTutorialBuildCompleted;
            catQuest.TutorialCharmStarted -= HandleTutorialCharmStarted;
            catQuest.TutorialCharmStarted += HandleTutorialCharmStarted;
            catQuest.TutorialQuestCompleted -= HandleTutorialQuestCompleted;
            catQuest.TutorialQuestCompleted += HandleTutorialQuestCompleted;
        }
        else
        {
            Debug.LogWarning("[vc_Level0TutorialController] CatQuest reference is missing.");
        }

        if (btnTutorialBackToMenu != null)
        {
            btnTutorialBackToMenu.onClick.RemoveListener(OnBackToMenuClicked);
            btnTutorialBackToMenu.onClick.AddListener(OnBackToMenuClicked);
        }
        else
        {
            Debug.LogWarning("[vc_Level0TutorialController] BTN_TutorialBackToMenu reference is missing.");
        }

        listenersBound = true;
    }

    private void InitializeTutorial()
    {
        moveStepCompleted = false;
        tutorialCompleted = false;
        tutorialCompletionSyncInProgress = false;

        if (markerMoveTarget != null)
        {
            markerMoveTarget.SetActive(true);
        }

        if (gateToSkillsRoom != null)
        {
            gateToSkillsRoom.SetActive(true);
        }

        if (panelTutorialComplete != null)
        {
            panelTutorialComplete.SetActive(false);
        }

        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
        }

        if (skillBuild != null)
        {
            skillBuild.gameObject.SetActive(true);
        }

        if (skillCharm != null)
        {
            skillCharm.gameObject.SetActive(true);
        }

        if (skill2 != null)
        {
            skill2.SetActive(false);
        }

        if (skill3 != null)
        {
            skill3.SetActive(false);
        }

        SetSkillInteractable(skillBuild, false);
        SetSkillInteractable(skillCharm, false);
        SetObjectiveText("Use the left stick to move to the glowing marker.");
    }

    private void HandleTutorialBuildCompleted()
    {
        SetSkillInteractable(skillBuild, true);
        SetSkillInteractable(skillCharm, true);
        SetObjectiveText("Nice. Press the Charm skill button to call the cat to you.");
    }

    private void HandleTutorialCharmStarted()
    {
        SetObjectiveText("Great. Stay there and wait for the cat to reach you.");
    }

    private void HandleTutorialQuestCompleted()
    {
        if (tutorialCompleted || tutorialCompletionSyncInProgress)
        {
            return;
        }

        SetObjectiveText("Tutorial complete.");
        tutorialCompletionSyncInProgress = true;
        SyncTutorialCompletion();
    }

    private void OnBackToMenuClicked()
    {
        TelemetryManager.Instance?.TagButtonClick("TutorialBackToMenu");
        SceneLoader.GoToMainMenu();
    }

    private void ConfigureMoveTrigger()
    {
        GameObject triggerObject = FindSceneObject("Trigger_MoveTarget");
        if (triggerObject == null)
        {
            Debug.LogWarning("[vc_Level0TutorialController] Trigger_MoveTarget was not found.");
            return;
        }

        vc_TutorialTrigger tutorialTrigger = triggerObject.GetComponent<vc_TutorialTrigger>();
        if (tutorialTrigger == null)
        {
            tutorialTrigger = triggerObject.AddComponent<vc_TutorialTrigger>();
        }

        tutorialTrigger.Configure(this, vc_TutorialTriggerType.MoveTarget);
    }

    private void ResolveReferences()
    {
        gateToSkillsRoom ??= FindSceneObject("Gate_ToSkillsRoom");
        markerMoveTarget ??= FindSceneObject("Marker_MoveTarget");
        panelTutorialComplete ??= FindSceneObject("Panel_TutorialComplete");
        hintPanel ??= FindSceneObject("HintPanel");
        skill2 ??= FindSceneObject("Skill2");
        skill3 ??= FindSceneObject("Skill3");

        btnTutorialBackToMenu ??= FindComponentInScene<Button>("BTN_TutorialBackToMenu");
        questObjectiveText ??= FindComponentInScene<TMP_Text>("QuestObjective");
        catQuest ??= FindComponentInScene<vc_CatQuest>("CatQuest") ?? FindFirstObjectByType<vc_CatQuest>();
        skillBuild ??= FindComponentInScene<vc_SkillSlot>("Skill0");
        skillCharm ??= FindComponentInScene<vc_SkillSlot>("Skill1");
    }

    private void SetObjectiveText(string value)
    {
        if (questObjectiveText == null)
        {
            return;
        }

        questObjectiveText.gameObject.SetActive(true);
        questObjectiveText.text = value;
    }

    private static void SetSkillInteractable(vc_SkillSlot skillSlot, bool isInteractable)
    {
        if (skillSlot == null)
        {
            return;
        }

        Button button = skillSlot.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = isInteractable;
        }
    }

    private static T FindComponentInScene<T>(string objectName) where T : Component
    {
        GameObject sceneObject = FindSceneObject(objectName);
        return sceneObject != null ? sceneObject.GetComponent<T>() : null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        return FindSceneObject(SceneManager.GetActiveScene(), objectName);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform match = FindChildRecursive(rootObjects[i].transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform match = FindChildRecursive(parent.GetChild(i), objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private void SyncTutorialCompletion()
    {
        if (AuthManager.Instance == null || SessionState.Instance == null || string.IsNullOrWhiteSpace(SessionState.Instance.AccessToken))
        {
            Debug.LogWarning("[vc_Level0TutorialController] Tutorial completion could not be synced. No authenticated session was available.");
            SessionState.Instance?.SetTutorialCompletionState(true);
            CompleteTutorialLocally();
            return;
        }

        AuthManager.Instance.MarkTutorialCompleted(
            onResolved: (result) =>
            {
                SessionState.Instance?.SetTutorialCompletionState(result != null && result.tutorial_completed);
                CompleteTutorialLocally();
            },
            onError: (errorMessage) =>
            {
                Debug.LogWarning($"[vc_Level0TutorialController] Tutorial completion sync failed: {errorMessage}");
                SessionState.Instance?.SetTutorialCompletionState(true);
                CompleteTutorialLocally();
            });
    }

    private void CompleteTutorialLocally()
    {
        tutorialCompletionSyncInProgress = false;
        tutorialCompleted = true;

        if (panelTutorialComplete != null)
        {
            panelTutorialComplete.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[vc_Level0TutorialController] Panel_TutorialComplete reference is missing.");
        }
    }
}
