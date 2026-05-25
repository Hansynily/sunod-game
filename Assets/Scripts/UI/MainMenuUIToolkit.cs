using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.Telemetry;

namespace SunodGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUIToolkit : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private string playSceneName     = SceneLoader.SCENE_CUTSCENE;
        [SerializeField] private string tutorialSceneName = SceneLoader.SCENE_TUTORIAL;

        private Button _btnPlay;
        private Button _btnTutorial;
        private Button _btnLogout;
        private Button _btnSettings;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _btnPlay     = root.Q<Button>("btn-play");
            _btnTutorial = root.Q<Button>("btn-tutorial");
            _btnLogout   = root.Q<Button>("btn-logout");
            _btnSettings = root.Q<Button>("btn-settings");

            _btnPlay?    .RegisterCallback<ClickEvent>(OnPlayClicked);
            _btnTutorial?.RegisterCallback<ClickEvent>(OnTutorialClicked);
            _btnLogout?  .RegisterCallback<ClickEvent>(OnLogoutClicked);
            _btnSettings?.RegisterCallback<ClickEvent>(OnSettingsClicked);
        }

        private void OnDisable()
        {
            _btnPlay?    .UnregisterCallback<ClickEvent>(OnPlayClicked);
            _btnTutorial?.UnregisterCallback<ClickEvent>(OnTutorialClicked);
            _btnLogout?  .UnregisterCallback<ClickEvent>(OnLogoutClicked);
            _btnSettings?.UnregisterCallback<ClickEvent>(OnSettingsClicked);
        }

        private void OnPlayClicked(ClickEvent _)
        {
            SceneLoader.LoadByName(playSceneName);
        }

        private void OnTutorialClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("Tutorial");
            SceneLoader.LoadByName(tutorialSceneName);
        }

        private void OnLogoutClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("Logout");
            TelemetryManager.Instance?.TagSessionEnd();
            SessionState.Instance?.ClearUser();
            SceneLoader.GoToLogin();
        }

        private void OnSettingsClicked(ClickEvent _) { }
    }
}
