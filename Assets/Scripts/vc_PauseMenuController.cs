using SunodGame.Core;
using SunodGame.Demo;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class vc_PauseMenuController : MonoBehaviour
{
    [SerializeField] private Button btnPause;
    [SerializeField] private GameObject panelPauseMenu;
    [SerializeField] private TMP_Text txtPauseTitle;
    [SerializeField] private Button btnPauseResume;
    [SerializeField] private Button btnPauseExit;
    [SerializeField] private GameObject panelPauseExitConfirm;
    [SerializeField] private TMP_Text txtPauseExitConfirmBody;
    [SerializeField] private Button btnPauseExitYes;
    [SerializeField] private Button btnPauseExitNo;
    [SerializeField] private bool allowEscapeKey = true;

    private bool _listenersBound;
    private bool _pauseOpen;

    private void Awake()
    {
        if (!IsSupportedScene(SceneManager.GetActiveScene()))
        {
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled)
        {
            return;
        }

        ResolveReferences();
        BindListeners();
        ForceRestoreGameState();
    }

    private void Update()
    {
        if (!enabled || !allowEscapeKey || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            OpenPauseMenu();
        }
    }

    private void OnDisable()
    {
        UnbindListeners();
        ForceRestoreGameState();
    }

    private void OnDestroy()
    {
        UnbindListeners();
        ForceRestoreGameState();
    }

    private void ResolveReferences()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Transform hierarchyRoot = transform != null ? transform.root : null;

        btnPause ??= FindHierarchyButton(hierarchyRoot, "BTN_Pause") ?? FindSceneButton("BTN_Pause", activeScene);
        panelPauseMenu ??= FindHierarchyObject(hierarchyRoot, "Panel_PauseMenu") ?? FindSceneObject("Panel_PauseMenu", activeScene);
        txtPauseTitle ??= FindHierarchyText(hierarchyRoot, "TXT_PauseTitle") ?? FindSceneText("TXT_PauseTitle", activeScene);
        btnPauseResume ??= FindHierarchyButton(hierarchyRoot, "BTN_PauseResume") ?? FindSceneButton("BTN_PauseResume", activeScene);
        btnPauseExit ??= FindHierarchyButton(hierarchyRoot, "BTN_PauseExit") ?? FindSceneButton("BTN_PauseExit", activeScene);
        panelPauseExitConfirm ??= FindHierarchyObject(hierarchyRoot, "Panel_PauseExitConfirm") ?? FindSceneObject("Panel_PauseExitConfirm", activeScene);
        txtPauseExitConfirmBody ??= FindHierarchyText(hierarchyRoot, "TXT_PauseExitConfirmBody") ?? FindSceneText("TXT_PauseExitConfirmBody", activeScene);
        btnPauseExitYes ??= FindHierarchyButton(hierarchyRoot, "BTN_PauseExitYes") ?? FindSceneButton("BTN_PauseExitYes", activeScene);
        btnPauseExitNo ??= FindHierarchyButton(hierarchyRoot, "BTN_PauseExitNo") ?? FindSceneButton("BTN_PauseExitNo", activeScene);
    }

    private void BindListeners()
    {
        if (_listenersBound)
        {
            return;
        }

        if (btnPause != null)
        {
            btnPause.onClick.RemoveListener(OpenPauseMenu);
            btnPause.onClick.AddListener(OpenPauseMenu);
        }

        if (btnPauseResume != null)
        {
            btnPauseResume.onClick.RemoveListener(HandlePauseResumeClicked);
            btnPauseResume.onClick.AddListener(HandlePauseResumeClicked);
        }

        if (btnPauseExit != null)
        {
            btnPauseExit.onClick.RemoveListener(HandlePauseExitClicked);
            btnPauseExit.onClick.AddListener(HandlePauseExitClicked);
        }

        if (btnPauseExitYes != null)
        {
            btnPauseExitYes.onClick.RemoveListener(HandlePauseExitYesClicked);
            btnPauseExitYes.onClick.AddListener(HandlePauseExitYesClicked);
        }

        if (btnPauseExitNo != null)
        {
            btnPauseExitNo.onClick.RemoveListener(HandlePauseExitNoClicked);
            btnPauseExitNo.onClick.AddListener(HandlePauseExitNoClicked);
        }

        _listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (btnPause != null)
        {
            btnPause.onClick.RemoveListener(OpenPauseMenu);
        }

        if (btnPauseResume != null)
        {
            btnPauseResume.onClick.RemoveListener(HandlePauseResumeClicked);
        }

        if (btnPauseExit != null)
        {
            btnPauseExit.onClick.RemoveListener(HandlePauseExitClicked);
        }

        if (btnPauseExitYes != null)
        {
            btnPauseExitYes.onClick.RemoveListener(HandlePauseExitYesClicked);
        }

        if (btnPauseExitNo != null)
        {
            btnPauseExitNo.onClick.RemoveListener(HandlePauseExitNoClicked);
        }

        _listenersBound = false;
    }

    private void OpenPauseMenu()
    {
        if (panelPauseMenu == null || panelPauseExitConfirm == null)
        {
            ResolveReferences();
        }

        if (_pauseOpen)
        {
            ShowMainPauseMenu();
            HideExitConfirm();
            return;
        }

        _pauseOpen = true;
        Time.timeScale = 0f;
        ShowMainPauseMenu();
        HideExitConfirm();
        ChallengeSessionController.Instance?.SetExternalPause(true);
    }

    private void ClosePauseMenu()
    {
        _pauseOpen = false;
        Time.timeScale = 1f;
        HideMainPauseMenu();
        HideExitConfirm();
        ChallengeSessionController.Instance?.SetExternalPause(false);
    }

    private void HandlePauseResumeClicked()
    {
        ClosePauseMenu();
    }

    private void HandlePauseExitClicked()
    {
        ShowMainPauseMenu();
        ShowExitConfirm();
    }

    private void HandlePauseExitNoClicked()
    {
        ShowMainPauseMenu();
        HideExitConfirm();
    }

    private void HandlePauseExitYesClicked()
    {
        if (IsTutorialScene())
        {
            ClosePauseMenu();
            SceneLoader.GoToMainMenu();
            return;
        }

        SessionState.Instance?.CancelRun();
        GameSessionData.Reset();
        vc_SessionTelemetry.Instance?.CancelCurrentSession();
        ClosePauseMenu();
        SceneLoader.GoToMainMenu();
    }

    private void ShowMainPauseMenu()
    {
        if (panelPauseMenu != null)
        {
            panelPauseMenu.SetActive(true);
            panelPauseMenu.transform.SetAsLastSibling();
        }
    }

    private void HideMainPauseMenu()
    {
        if (panelPauseMenu != null)
        {
            panelPauseMenu.SetActive(false);
        }
    }

    private void ShowExitConfirm()
    {
        if (panelPauseExitConfirm != null)
        {
            if (txtPauseExitConfirmBody != null && string.IsNullOrWhiteSpace(txtPauseExitConfirmBody.text))
            {
                txtPauseExitConfirmBody.text = GetDefaultExitConfirmBody();
            }

            panelPauseExitConfirm.SetActive(true);
            panelPauseExitConfirm.transform.SetAsLastSibling();
        }
    }

    private void HideExitConfirm()
    {
        if (panelPauseExitConfirm != null)
        {
            panelPauseExitConfirm.SetActive(false);
        }
    }

    private void ForceRestoreGameState()
    {
        Time.timeScale = 1f;
        _pauseOpen = false;
        HideMainPauseMenu();
        HideExitConfirm();
        ChallengeSessionController.Instance?.SetExternalPause(false);
    }

    private static bool IsSupportedScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return false;
        }

        return IsTutorialSceneName(scene.name) || IsRealRunSceneName(scene.name);
    }

    private static bool IsTutorialSceneName(string sceneName)
    {
        return sceneName == SceneLoader.SCENE_TUTORIAL;
    }

    private static bool IsRealRunSceneName(string sceneName)
    {
        return sceneName == SceneLoader.SCENE_PLAY;
    }

    private static bool IsTutorialScene()
    {
        return IsTutorialSceneName(SceneManager.GetActiveScene().name);
    }

    private string GetDefaultExitConfirmBody()
    {
        return IsTutorialScene()
            ? "Return to the main menu?"
            : "Return to the main menu? This will cancel the current run.";
    }

    private static Button FindSceneButton(string objectName, Scene scene)
    {
        GameObject sceneObject = FindSceneObject(objectName, scene);
        return sceneObject != null ? sceneObject.GetComponent<Button>() : null;
    }

    private static Button FindHierarchyButton(Transform hierarchyRoot, string objectName)
    {
        GameObject sceneObject = FindHierarchyObject(hierarchyRoot, objectName);
        return sceneObject != null ? sceneObject.GetComponent<Button>() : null;
    }

    private static TMP_Text FindSceneText(string objectName, Scene scene)
    {
        GameObject sceneObject = FindSceneObject(objectName, scene);
        return sceneObject != null ? sceneObject.GetComponent<TMP_Text>() : null;
    }

    private static TMP_Text FindHierarchyText(Transform hierarchyRoot, string objectName)
    {
        GameObject sceneObject = FindHierarchyObject(hierarchyRoot, objectName);
        return sceneObject != null ? sceneObject.GetComponent<TMP_Text>() : null;
    }

    private static GameObject FindSceneObject(string objectName, Scene scene)
    {
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform match = FindInChildren(rootObjects[i].transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static GameObject FindHierarchyObject(Transform hierarchyRoot, string objectName)
    {
        if (hierarchyRoot == null)
        {
            return null;
        }

        Transform[] transforms = hierarchyRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static Transform FindInChildren(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform match = FindInChildren(parent.GetChild(i), objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
