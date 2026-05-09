using System;
using System.Collections;
using TMPro;
using SunodGame.Core;
using SunodGame.Telemetry;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_Level0TutorialController : MonoBehaviour
{
    private enum TutorialStep { Intro, Explore, Equip, Build, Charm, Wait, Complete }

    [SerializeField] private GameObject gateToSkillsRoom;
    [SerializeField] private GameObject panelTutorialComplete;
    [SerializeField] private Button btnTutorialBackToMenu;
    [SerializeField] private TMP_Text questObjectiveText;
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private GameObject skill2;
    [SerializeField] private GameObject skill3;
    [SerializeField] private vc_CatQuest catQuest;
    [SerializeField] private vc_SkillSlot skillBuild;
    [SerializeField] private vc_SkillSlot skillCharm;
    [SerializeField] private GameObject panelDialog;
    [SerializeField] private TMP_Text textDialogTitle;
    [SerializeField] private TMP_Text textDialogBody;
    [SerializeField] private TMP_Text textCompletionSummary;
    [SerializeField] private Button btnInventory;

    private TutorialStep currentStep;
    private bool tutorialCompleted;
    private bool tutorialCompletionSyncInProgress;
    private bool listenersBound;
    private PlayerController playerController;
    private Action pendingDialogConfirm;
    private Button btnDialog;
    private int skillsPickedUpCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneLoader.SCENE_TUTORIAL)
            return;

        GameObject tutorialRootObject = FindSceneObject(scene, "TutorialRoot");
        if (tutorialRootObject == null)
        {
            Debug.LogWarning("[vc_Level0TutorialController] TutorialRoot was not found in the tutorial scene.");
            return;
        }

        if (tutorialRootObject.GetComponent<vc_Level0TutorialController>() == null)
            tutorialRootObject.AddComponent<vc_Level0TutorialController>();
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

    private void Update()
    {
        if (currentStep != TutorialStep.Equip)
            return;

        bool buildReady = skillBuild != null && skillBuild.AssignedSkill != null;
        bool charmReady = skillCharm != null && skillCharm.AssignedSkill != null;
        if (buildReady && charmReady)
            AdvanceToStep(TutorialStep.Build);
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
            btnTutorialBackToMenu.onClick.RemoveListener(OnBackToMenuClicked);

        if (btnDialog != null)
            btnDialog.onClick.RemoveListener(OnDialogConfirmClicked);

        if (vc_PlayerInventory.Instance != null)
            vc_PlayerInventory.Instance.OnSkillAdded -= HandleSkillPickedUp;

        listenersBound = false;
    }

    public void HandleTriggerEntered(vc_TutorialTriggerType triggerType)
    {
        // marker removed — no longer used
    }

    private void AdvanceToStep(TutorialStep step)
    {
        currentStep = step;

        switch (step)
        {
            case TutorialStep.Intro:
                playerController?.MoveAction.Disable();
                ShowDialog(() => AdvanceToStep(TutorialStep.Explore));
                break;

            case TutorialStep.Explore:
                HideDialog();
                playerController?.MoveAction.Enable();
                gateToSkillsRoom?.SetActive(false);
                SetObjectiveText("Head to the next room and pick up both skills.");
                break;

            case TutorialStep.Equip:
                SetButtonInteractable(btnInventory, true);
                SetObjectiveText("Open your inventory and equip both skills to your slots.");
                break;

            case TutorialStep.Build:
                SetButtonInteractable(btnInventory, false);
                SetSlotInteractableBySkillType<vc_BuildSkill>(true);
                SetSlotInteractableBySkillType<vc_CharmSkill>(false);
                SetObjectiveText("Walk to the river and use the Build skill to cross.");
                catQuest?.BeginTutorialQuest();
                vc_QuestHUD.Instance?.ShowQuestInfo(
                    "Tutorial Quest 1/2",
                    "Rescue the Cat",
                    "A cat is trapped across the river. Find a way to reach it and help it get free.",
                    new[] { "Find the path to the cat", "Reach the trapped cat", "Help the cat get free" }
                );
                break;

            case TutorialStep.Charm:
                SetSlotInteractableBySkillType<vc_CharmSkill>(true);
                SetObjectiveText("Nice! Use Charm to call the cat to you.");
                break;

            case TutorialStep.Wait:
                SetObjectiveText("Stay there, the cat is coming to you.");
                break;

            case TutorialStep.Complete:
                SetObjectiveText("");
                if (textCompletionSummary != null)
                    textCompletionSummary.text = "You used the Build and Charm skills, both tied to the RIASEC tags. The more quests you complete, the clearer your career profile becomes!";
                SyncTutorialCompletion();
                break;
        }
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

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

        if (btnDialog != null)
        {
            btnDialog.onClick.RemoveListener(OnDialogConfirmClicked);
            btnDialog.onClick.AddListener(OnDialogConfirmClicked);
        }
        else
        {
            Debug.LogWarning("[vc_Level0TutorialController] Panel_Dialog is missing a Button component.");
        }

        if (vc_PlayerInventory.Instance != null)
            vc_PlayerInventory.Instance.OnSkillAdded += HandleSkillPickedUp;
        else
            Debug.LogWarning("[vc_Level0TutorialController] vc_PlayerInventory.Instance is not available.");

        listenersBound = true;
    }

    private void InitializeTutorial()
    {
        tutorialCompleted = false;
        tutorialCompletionSyncInProgress = false;
        skillsPickedUpCount = 0;

        gateToSkillsRoom?.SetActive(true);
        panelTutorialComplete?.SetActive(false);
        hintPanel?.SetActive(false);
        panelDialog?.SetActive(false);
        skill2?.SetActive(false);
        skill3?.SetActive(false);

        skillBuild?.gameObject.SetActive(true);
        skillCharm?.gameObject.SetActive(true);

        SetSkillInteractable(skillBuild, false);
        SetSkillInteractable(skillCharm, false);
        SetButtonInteractable(btnInventory, false);
        HideObjectiveText();

        AdvanceToStep(TutorialStep.Intro);
    }

    private void HandleSkillPickedUp(vc_SkillData skill)
    {
        if (currentStep != TutorialStep.Explore)
            return;

        skillsPickedUpCount++;
        StartCoroutine(ShowSkillPickupDialog(skillsPickedUpCount));
    }

    private IEnumerator ShowSkillPickupDialog(int count)
    {
        yield return null;
        playerController?.MoveAction.Disable();

        if (count == 1)
        {
            ShowDialog(
                "Nice!",
                "You picked up your first skill. There's one more in the room. Go find it!",
                () => playerController?.MoveAction.Enable());
        }
        else if (count == 2)
        {
            ShowDialog(
                "Save the Cat",
                "A cat is trapped across the river!\n\nOpen your inventory, assign both skills to your slots. Use Build to make a bridge, then Charm to call the cat to you.",
                () =>
                {
                    playerController?.MoveAction.Enable();
                    AdvanceToStep(TutorialStep.Equip);
                });
        }
    }

    private void ShowDialog(Action onConfirm)
    {
        pendingDialogConfirm = onConfirm;
        panelDialog?.SetActive(true);
    }

    private void ShowDialog(string title, string body, Action onConfirm)
    {
        if (textDialogTitle != null) textDialogTitle.text = title;
        if (textDialogBody != null) textDialogBody.text = body;
        pendingDialogConfirm = onConfirm;
        panelDialog?.SetActive(true);
    }

    private void HideDialog()
    {
        panelDialog?.SetActive(false);
        pendingDialogConfirm = null;
    }

    private void OnDialogConfirmClicked()
    {
        Action action = pendingDialogConfirm;
        pendingDialogConfirm = null;
        panelDialog?.SetActive(false);
        action?.Invoke();
    }

    private void HandleTutorialBuildCompleted()
    {
        vc_QuestHUD.Instance?.CheckObjective(0);
        AdvanceToStep(TutorialStep.Charm);
    }

    private void HandleTutorialCharmStarted()
    {
        vc_QuestHUD.Instance?.CheckObjective(1);
        AdvanceToStep(TutorialStep.Wait);
    }

    private void HandleTutorialQuestCompleted()
    {
        if (tutorialCompleted || tutorialCompletionSyncInProgress)
            return;

        vc_QuestHUD.Instance?.CheckObjective(2);
        tutorialCompletionSyncInProgress = true;
        AdvanceToStep(TutorialStep.Complete);
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
            return;

        vc_TutorialTrigger tutorialTrigger = triggerObject.GetComponent<vc_TutorialTrigger>();
        if (tutorialTrigger == null)
            tutorialTrigger = triggerObject.AddComponent<vc_TutorialTrigger>();

        tutorialTrigger.Configure(this, vc_TutorialTriggerType.MoveTarget);
    }

    private void ResolveReferences()
    {
        gateToSkillsRoom ??= FindSceneObject("Gate_ToSkillsRoom");
        panelTutorialComplete ??= FindSceneObject("Panel_TutorialComplete");
        hintPanel ??= FindSceneObject("HintPanel");
        skill2 ??= FindSceneObject("Skill2");
        skill3 ??= FindSceneObject("Skill3");
        panelDialog ??= FindSceneObject("Panel_Dialog");

        btnTutorialBackToMenu ??= FindComponentInScene<Button>("BTN_TutorialBackToMenu");
        questObjectiveText ??= FindComponentInScene<TMP_Text>("QuestObjective");
        catQuest ??= FindComponentInScene<vc_CatQuest>("CatQuest") ?? FindFirstObjectByType<vc_CatQuest>();
        skillBuild ??= FindComponentInScene<vc_SkillSlot>("Skill0");
        skillCharm ??= FindComponentInScene<vc_SkillSlot>("Skill1");
        playerController ??= FindFirstObjectByType<PlayerController>();

        if (panelDialog != null)
        {
            btnDialog ??= panelDialog.GetComponent<Button>();
            textDialogTitle ??= FindComponentInChild<TMP_Text>(panelDialog, "Text_DialogTitle");
            textDialogBody ??= FindComponentInChild<TMP_Text>(panelDialog, "Text_DialogBody");
        }

        if (panelTutorialComplete != null)
            textCompletionSummary ??= FindComponentInChild<TMP_Text>(panelTutorialComplete, "Text_CompletionSummary");
    }

    private void SetObjectiveText(string value)
    {
        if (questObjectiveText == null)
            return;

        questObjectiveText.gameObject.SetActive(true);
        questObjectiveText.text = value;
    }

    private void HideObjectiveText()
    {
        if (questObjectiveText != null)
            questObjectiveText.gameObject.SetActive(false);
    }

    private static void SetSkillInteractable(vc_SkillSlot skillSlot, bool isInteractable)
    {
        if (skillSlot == null)
            return;

        Button button = skillSlot.GetComponent<Button>();
        if (button != null)
            button.interactable = isInteractable;
    }

    private void SetSlotInteractableBySkillType<T>(bool isInteractable) where T : vc_PlayerSkill
    {
        vc_SkillSlot[] slots = { skillBuild, skillCharm };
        foreach (vc_SkillSlot slot in slots)
        {
            if (slot == null || slot.AssignedSkill == null)
                continue;

            if (slot.AssignedSkill is T)
            {
                Button button = slot.GetComponent<Button>();
                if (button != null)
                    button.interactable = isInteractable;
            }
        }
    }

    private static void SetButtonInteractable(Button button, bool isInteractable)
    {
        if (button != null)
            button.interactable = isInteractable;
    }

    private static T FindComponentInChild<T>(GameObject root, string childName) where T : Component
    {
        if (root == null)
            return null;

        Transform match = FindChildRecursive(root.transform, childName);
        return match != null ? match.GetComponent<T>() : null;
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
            return null;

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform match = FindChildRecursive(rootObjects[i].transform, objectName);
            if (match != null)
                return match.gameObject;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform match = FindChildRecursive(parent.GetChild(i), objectName);
            if (match != null)
                return match;
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
        panelTutorialComplete?.SetActive(true);
    }
}
