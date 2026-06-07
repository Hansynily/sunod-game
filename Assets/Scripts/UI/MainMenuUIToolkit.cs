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

        [Header("Player Card")]
        [SerializeField] private Sprite playerCharacterSprite;

        private Button _btnPlay;
        private Button _btnTutorial;
        private Button _btnContinue;
        private Button _btnLogout;
        private Button _btnSettings;
        private VisualElement _playerCardImage;
        private Label _lblUsername;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _btnPlay         = root.Q<Button>("btn-play");
            _btnTutorial     = root.Q<Button>("btn-tutorial");
            _btnContinue     = root.Q<Button>("btn-continue");
            _btnLogout       = root.Q<Button>("btn-logout");
            _btnSettings     = root.Q<Button>("btn-settings");
            _playerCardImage = root.Q<VisualElement>("player-card-image");
            _lblUsername     = root.Q<Label>("lbl-username");

            _btnPlay?    .RegisterCallback<ClickEvent>(OnPlayClicked);
            _btnTutorial?.RegisterCallback<ClickEvent>(OnTutorialClicked);
            _btnContinue?.RegisterCallback<ClickEvent>(OnContinueClicked);
            _btnLogout?  .RegisterCallback<ClickEvent>(OnLogoutClicked);
            _btnSettings?.RegisterCallback<ClickEvent>(OnSettingsClicked);

            PopulatePlayerCard();
            SetContinueState();
        }

        private void OnDisable()
        {
            _btnPlay?    .UnregisterCallback<ClickEvent>(OnPlayClicked);
            _btnTutorial?.UnregisterCallback<ClickEvent>(OnTutorialClicked);
            _btnContinue?.UnregisterCallback<ClickEvent>(OnContinueClicked);
            _btnLogout?  .UnregisterCallback<ClickEvent>(OnLogoutClicked);
            _btnSettings?.UnregisterCallback<ClickEvent>(OnSettingsClicked);
        }

        private void PopulatePlayerCard()
        {
            var session = SessionState.Instance;

            if (_lblUsername != null)
                _lblUsername.text = session != null ? session.Username ?? "Player" : "Player";

            if (_playerCardImage != null && playerCharacterSprite != null)
                _playerCardImage.style.backgroundImage = new StyleBackground(playerCharacterSprite);
        }

        private void SetContinueState()
        {
            if (_btnContinue == null) return;
            bool hasRun = SessionState.Instance != null && SessionState.Instance.HasActiveRun;
            _btnContinue.SetEnabled(hasRun);
        }

        private void OnPlayClicked(ClickEvent _)     => SceneLoader.LoadByName(playSceneName);
        private void OnTutorialClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("Tutorial");
            SceneLoader.LoadByName(tutorialSceneName);
        }
        private void OnContinueClicked(ClickEvent _) => SceneLoader.LoadByName(playSceneName);
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
