using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SunodGame.Core;
using SunodGame.Telemetry;

namespace SunodGame.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button btnPlay;
        [SerializeField] private Button btnTutorial;
        [SerializeField] private Button btnLogout;
        [SerializeField] private Button btnTutorialPromptYes;
        [SerializeField] private Button btnTutorialPromptNo;

        [Header("Tutorial Prompt")]
        [SerializeField] private GameObject panelTutorialPrompt;
        [SerializeField] private TMP_Text txtTutorialPromptTitle;
        [SerializeField] private TMP_Text txtTutorialPromptBody;

        [Header("Navigation")]
        [SerializeField] private string playSceneName = SceneLoader.SCENE_CUTSCENE;
        [SerializeField] private string tutorialSceneName = SceneLoader.SCENE_TUTORIAL;

        private bool listenersBound;

        private void Start()
        {
            ResolveButtonReferences();
            BindButtonListeners();
            UpdateTutorialPromptState();
        }

        private void OnDestroy()
        {
            if (btnPlay != null)
                btnPlay.onClick.RemoveListener(OnPlayClicked);

            if (btnTutorial != null)
                btnTutorial.onClick.RemoveListener(OnTutorialClicked);

            if (btnLogout != null)
                btnLogout.onClick.RemoveListener(OnLogoutClicked);

            if (btnTutorialPromptYes != null)
                btnTutorialPromptYes.onClick.RemoveListener(OnTutorialPromptYesClicked);

            if (btnTutorialPromptNo != null)
                btnTutorialPromptNo.onClick.RemoveListener(OnTutorialPromptNoClicked);

            listenersBound = false;
        }

        private void ResolveButtonReferences()
        {
            if (btnPlay == null)
            {
                GameObject playObject = FindSceneObject("BTN_Play");
                if (playObject != null)
                {
                    btnPlay = playObject.GetComponent<Button>();
                }
            }

            if (btnTutorial == null)
            {
                GameObject tutorialObject = FindSceneObject("BTN_Tutorial");
                if (tutorialObject != null)
                {
                    btnTutorial = tutorialObject.GetComponent<Button>();
                }
            }

            if (btnLogout == null)
            {
                GameObject logoutObject = FindSceneObject("BTN_Logout");
                if (logoutObject != null)
                {
                    btnLogout = logoutObject.GetComponent<Button>();
                }
            }

            panelTutorialPrompt ??= FindSceneObject("Panel_TutorialPrompt");
            txtTutorialPromptTitle ??= FindSceneText("TXT_TutorialPromptTitle");
            txtTutorialPromptBody ??= FindSceneText("TXT_TutorialPromptBody");
            btnTutorialPromptYes ??= FindSceneButton("BTN_TutorialPromptYes");
            btnTutorialPromptNo ??= FindSceneButton("BTN_TutorialPromptNo");
        }

        private void BindButtonListeners()
        {
            if (listenersBound)
            {
                return;
            }

            if (btnPlay != null)
            {
                btnPlay.onClick.RemoveListener(OnPlayClicked);
                btnPlay.onClick.AddListener(OnPlayClicked);
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] Play button reference is missing.");
            }

            if (btnTutorial != null)
            {
                btnTutorial.onClick.RemoveListener(OnTutorialClicked);
                btnTutorial.onClick.AddListener(OnTutorialClicked);
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] Tutorial button reference is missing.");
            }

            if (btnLogout != null)
            {
                btnLogout.onClick.RemoveListener(OnLogoutClicked);
                btnLogout.onClick.AddListener(OnLogoutClicked);
            }
            else
            {
                Debug.LogWarning("[MainMenuUI] Logout button reference is missing.");
            }

            if (btnTutorialPromptYes != null)
            {
                btnTutorialPromptYes.onClick.RemoveListener(OnTutorialPromptYesClicked);
                btnTutorialPromptYes.onClick.AddListener(OnTutorialPromptYesClicked);
            }

            if (btnTutorialPromptNo != null)
            {
                btnTutorialPromptNo.onClick.RemoveListener(OnTutorialPromptNoClicked);
                btnTutorialPromptNo.onClick.AddListener(OnTutorialPromptNoClicked);
            }

            listenersBound = true;
        }

        private void OnPlayClicked()
        {
            SceneLoader.LoadByName(playSceneName);
        }

        private void OnTutorialClicked()
        {
            TelemetryManager.Instance?.TagButtonClick("Tutorial");
            SceneLoader.LoadByName(tutorialSceneName);
        }

        private void OnLogoutClicked()
        {
            TelemetryManager.Instance?.TagButtonClick("Logout");
            TelemetryManager.Instance?.TagSessionEnd();
            SessionState.Instance?.ClearUser();
            SceneLoader.GoToLogin();
        }

        private void OnTutorialPromptYesClicked()
        {
            TelemetryManager.Instance?.TagButtonClick("TutorialPromptYes");
            SceneLoader.LoadByName(tutorialSceneName);
        }

        private void OnTutorialPromptNoClicked()
        {
            TelemetryManager.Instance?.TagButtonClick("TutorialPromptNo");
            SetTutorialPromptVisible(false);
        }

        private void UpdateTutorialPromptState()
        {
            if (txtTutorialPromptTitle != null && string.IsNullOrWhiteSpace(txtTutorialPromptTitle.text))
                txtTutorialPromptTitle.text = "New user";

            if (txtTutorialPromptBody != null && string.IsNullOrWhiteSpace(txtTutorialPromptBody.text))
                txtTutorialPromptBody.text = "You have not completed the tutorial yet. Do you want to start it now?";

            bool shouldShowPrompt = SessionState.Instance != null && !SessionState.Instance.HasCompletedTutorial;
            SetTutorialPromptVisible(shouldShowPrompt);
        }

        private void SetTutorialPromptVisible(bool isVisible)
        {
            if (panelTutorialPrompt != null)
                panelTutorialPrompt.SetActive(isVisible);

            SetMainButtonsInteractable(!isVisible);
        }

        private void SetMainButtonsInteractable(bool isInteractable)
        {
            if (btnPlay != null)
                btnPlay.interactable = isInteractable;

            if (btnTutorial != null)
                btnTutorial.interactable = isInteractable;

            if (btnLogout != null)
                btnLogout.interactable = isInteractable;
        }

        private static Button FindSceneButton(string objectName)
        {
            GameObject sceneObject = FindSceneObject(objectName);
            return sceneObject != null ? sceneObject.GetComponent<Button>() : null;
        }

        private static TMP_Text FindSceneText(string objectName)
        {
            GameObject sceneObject = FindSceneObject(objectName);
            return sceneObject != null ? sceneObject.GetComponent<TMP_Text>() : null;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                Transform match = FindInChildren(rootObjects[i].transform, objectName);
                if (match != null)
                    return match.gameObject;
            }

            return null;
        }

        private static Transform FindInChildren(Transform parent, string objectName)
        {
            if (parent.name == objectName)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform match = FindInChildren(parent.GetChild(i), objectName);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
