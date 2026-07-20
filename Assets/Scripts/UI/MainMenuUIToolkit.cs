using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;

namespace SunodGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUIToolkit : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private string playSceneName     = SceneLoader.SCENE_CUTSCENE;
        [SerializeField] private string tutorialSceneName = SceneLoader.SCENE_TUTORIAL;
        [SerializeField] private string fallbackContinueScene = SceneLoader.SCENE_PLAY;

        [Header("Player Card")]
        [SerializeField] private Sprite playerCharacterSprite;

        private Button _btnPlay;
        private Button _btnTutorial;
        private Button _btnLogout;
        private Button _btnSettings;
        private Button _btnContinue;
        private Button _btnStats;
        private Button _btnReset;
        private VisualElement _playerCardImage;
        private Label _lblUsername;
        private VisualElement _leftPanel;
        private VisualElement _rightPanel;

        // Progress page
        private VisualElement _statsOverlay;
        private VisualElement _statsEmptyLabel;
        private VisualElement _statsContent;
        private Label _lblRunComplete;
        private Label _lblProvisionalNote;
        private Label _lblCareerName;
        private Label _lblClusterLabel;
        private Label _lblHollandCode;
        private Label _lblQuests;
        private Label _lblStars;
        private Label _lblSource;
        private Label _lblStatsError;
        private Button _btnStatsClose;

        // Reset-confirm overlay
        private VisualElement _resetConfirmOverlay;
        private Button _btnResetConfirm;
        private Button _btnResetCancel;

        // New-run-confirm overlay
        private VisualElement _newRunConfirmOverlay;
        private Button _btnNewRunConfirm;
        private Button _btnNewRunCancel;

        private const int TotalQuestSlots = 48;

        // Cached from the last GET so Continue can hand it straight to the target scene
        // without a second round trip.
        private RunStateResponse _cachedRunState;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            _btnPlay      = root.Q<Button>("btn-play");
            _btnTutorial  = root.Q<Button>("btn-tutorial");
            _btnLogout    = root.Q<Button>("btn-logout");
            _btnSettings  = root.Q<Button>("btn-settings");
            _btnContinue  = root.Q<Button>("btn-continue");
            _btnStats     = root.Q<Button>("btn-stats");
            _btnReset     = root.Q<Button>("btn-reset");
            _playerCardImage = root.Q<VisualElement>("player-card-image");
            _lblUsername     = root.Q<Label>("lbl-username");
            _leftPanel       = root.Q<VisualElement>("left-panel");
            _rightPanel      = root.Q<VisualElement>("right-panel");

            _statsOverlay      = root.Q<VisualElement>("stats-overlay");
            _statsEmptyLabel   = root.Q<VisualElement>("lbl-stats-empty");
            _statsContent      = root.Q<VisualElement>("stats-content");
            _lblRunComplete    = root.Q<Label>("lbl-run-complete");
            _lblProvisionalNote = root.Q<Label>("lbl-provisional-note");
            _lblCareerName     = root.Q<Label>("lbl-career-name");
            _lblClusterLabel   = root.Q<Label>("lbl-cluster-label");
            _lblHollandCode    = root.Q<Label>("lbl-holland-code");
            _lblQuests         = root.Q<Label>("lbl-quests");
            _lblStars          = root.Q<Label>("lbl-stars");
            _lblSource         = root.Q<Label>("lbl-source");
            _lblStatsError     = root.Q<Label>("lbl-stats-error");
            _btnStatsClose     = root.Q<Button>("btn-stats-close");

            _resetConfirmOverlay = root.Q<VisualElement>("reset-confirm-overlay");
            _btnResetConfirm     = root.Q<Button>("btn-reset-confirm");
            _btnResetCancel      = root.Q<Button>("btn-reset-cancel");

            _newRunConfirmOverlay = root.Q<VisualElement>("newrun-confirm-overlay");
            _btnNewRunConfirm     = root.Q<Button>("btn-newrun-confirm");
            _btnNewRunCancel      = root.Q<Button>("btn-newrun-cancel");

            _btnPlay?    .RegisterCallback<ClickEvent>(OnPlayClicked);
            _btnTutorial?.RegisterCallback<ClickEvent>(OnTutorialClicked);
            _btnLogout?  .RegisterCallback<ClickEvent>(OnLogoutClicked);
            _btnSettings?.RegisterCallback<ClickEvent>(OnSettingsClicked);
            _btnContinue?.RegisterCallback<ClickEvent>(OnContinueClicked);
            _btnStats?   .RegisterCallback<ClickEvent>(OnStatsClicked);
            _btnReset?   .RegisterCallback<ClickEvent>(OnResetClicked);
            _btnStatsClose?  .RegisterCallback<ClickEvent>(OnStatsCloseClicked);
            _btnResetConfirm?.RegisterCallback<ClickEvent>(OnResetConfirmClicked);
            _btnResetCancel? .RegisterCallback<ClickEvent>(OnResetCancelClicked);
            _btnNewRunConfirm?.RegisterCallback<ClickEvent>(OnNewRunConfirmClicked);
            _btnNewRunCancel? .RegisterCallback<ClickEvent>(OnNewRunCancelClicked);

            _statsOverlay?.SetDisplay(false);
            _resetConfirmOverlay?.SetDisplay(false);
            _newRunConfirmOverlay?.SetDisplay(false);

            PopulatePlayerCard();
            RefreshContinueAvailability();
        }

        private void OnDisable()
        {
            _btnPlay?    .UnregisterCallback<ClickEvent>(OnPlayClicked);
            _btnTutorial?.UnregisterCallback<ClickEvent>(OnTutorialClicked);
            _btnLogout?  .UnregisterCallback<ClickEvent>(OnLogoutClicked);
            _btnSettings?.UnregisterCallback<ClickEvent>(OnSettingsClicked);
            _btnContinue?.UnregisterCallback<ClickEvent>(OnContinueClicked);
            _btnStats?   .UnregisterCallback<ClickEvent>(OnStatsClicked);
            _btnReset?   .UnregisterCallback<ClickEvent>(OnResetClicked);
            _btnStatsClose?  .UnregisterCallback<ClickEvent>(OnStatsCloseClicked);
            _btnResetConfirm?.UnregisterCallback<ClickEvent>(OnResetConfirmClicked);
            _btnResetCancel? .UnregisterCallback<ClickEvent>(OnResetCancelClicked);
            _btnNewRunConfirm?.UnregisterCallback<ClickEvent>(OnNewRunConfirmClicked);
            _btnNewRunCancel? .UnregisterCallback<ClickEvent>(OnNewRunCancelClicked);
        }

        private void PopulatePlayerCard()
        {
            var session = SessionState.Instance;

            if (_lblUsername != null)
            {
                string name = (session != null && !string.IsNullOrWhiteSpace(session.Username))
                    ? session.Username
                    : "Adventurer";   // dev placeholder - not shown to real logged-in users
                _lblUsername.text = $"Hi, {name}!";
            }

            if (_playerCardImage != null && playerCharacterSprite != null)
                _playerCardImage.style.backgroundImage = new StyleBackground(playerCharacterSprite);
        }

        // ── Play / Tutorial / Logout / Settings (unchanged) ────────────────

        private void OnPlayClicked(ClickEvent _)
        {
            bool hasLiveRun = _cachedRunState != null && _cachedRunState.has_state
                && !_cachedRunState.run_finished
                && _cachedRunState.completed_quest_ids != null
                && _cachedRunState.completed_quest_ids.Count > 0;

            if (hasLiveRun)
            {
                _newRunConfirmOverlay?.SetDisplay(true);
                return;
            }

            ProceedToNewRun();
        }

        private void OnNewRunConfirmClicked(ClickEvent _)
        {
            _newRunConfirmOverlay?.SetDisplay(false);
            ProceedToNewRun();
        }

        private void OnNewRunCancelClicked(ClickEvent _) => _newRunConfirmOverlay?.SetDisplay(false);

        private void ProceedToNewRun()
        {
            // A new run must not inherit the previous run's checkpoint, or the first quest
            // completion would push a save claiming the old quests are done and double-count
            // stars and RIASEC scores. Clear both the local cache and the server save-state.
            vc_SaveManager.DeleteSave();
            vc_QuestAvailabilityFilter.PendingCompletedQuestIds = null;
            vc_QuestAvailabilityFilter.PendingOwnedSkillNames = null;
            PlayerPrefs.DeleteKey(TelemetryManager.RunFinishPendingPrefKey);
            _cachedRunState = null;
            SetContinueEnabled(false);

            TelemetryManager.Instance?.ResetMyRunState(
                onError: error => Debug.LogWarning($"[MainMenu] New-run reset failed server-side: {error}"));

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

        // ── Continue ────────────────────────────────────────────────────

        private void RefreshContinueAvailability()
        {
            SetContinueEnabled(false); // disabled until the fetch resolves

            if (TelemetryManager.Instance == null) return;

            // End Run happened but the server flag never landed (offline/crash): treat the
            // run as finished locally, keep Continue greyed, and retry the flag until it
            // sticks. "End Run ends the run, regardless."
            if (PlayerPrefs.GetInt(TelemetryManager.RunFinishPendingPrefKey, 0) == 1)
            {
                TelemetryManager.Instance.FinishMyRunState(
                    _ =>
                    {
                        PlayerPrefs.DeleteKey(TelemetryManager.RunFinishPendingPrefKey);
                        PlayerPrefs.Save();
                        Debug.Log("[MainMenu] Pending run-finish flag delivered to the server.");
                    },
                    error => Debug.LogWarning($"[MainMenu] Pending run-finish retry failed: {error}"));
                return;
            }

            TelemetryManager.Instance.GetMyRunState(
                (state, riasec) =>
                {
                    _cachedRunState = state;
                    bool hasProgress = state != null && state.has_state
                        && !state.run_finished
                        && state.completed_quest_ids != null && state.completed_quest_ids.Count > 0;
                    SetContinueEnabled(hasProgress);
                },
                error =>
                {
                    Debug.LogWarning($"[MainMenu] Could not check run-state: {error}");
                    SetContinueEnabled(false);
                });
        }

        private void SetContinueEnabled(bool enabled)
        {
            if (_btnContinue == null) return;
            _btnContinue.SetEnabled(enabled);
        }

        private void OnContinueClicked(ClickEvent _)
        {
            if (_cachedRunState == null || !_cachedRunState.has_state || _cachedRunState.run_finished) return;

            // Seed the resumed run's completed-quest exclusion set + owned skills. Both are
            // consumed by vc_QuestAvailabilityFilter once the floor scene loads (completed
            // quests in Awake; skills resolved against its gameSettings master list and
            // re-equipped before the quest draw computes availability).
            vc_QuestAvailabilityFilter.PendingCompletedQuestIds =
                _cachedRunState.completed_quest_ids != null
                    ? new List<string>(_cachedRunState.completed_quest_ids)
                    : new List<string>();

            vc_QuestAvailabilityFilter.PendingOwnedSkillNames =
                _cachedRunState.owned_skills != null
                    ? new List<string>(_cachedRunState.owned_skills)
                    : new List<string>();

            // Rebuild the playthrough's telemetry records from the checkpoint. Without this,
            // the resumed session's run summary / prediction / progress counts only cover
            // quests played since app launch (the "shows 2 instead of 5" bug).
            if (vc_SessionTelemetry.Instance != null && _cachedRunState.quest_records != null)
                vc_SessionTelemetry.Instance.SeedRestoredRecords(_cachedRunState.quest_records);

            // Carry the restored progress into the local save-state cache so vc_QuestRoom's
            // existing local scoring keeps incrementing from here.
            var data = vc_SaveManager.Load() ?? new vc_SaveManager.SaveData { riasecScores = new int[6] };
            data.completedQuestIds = _cachedRunState.completed_quest_ids != null
                ? new List<string>(_cachedRunState.completed_quest_ids)
                : new List<string>();
            data.unlockedSkills = _cachedRunState.owned_skills != null
                ? new List<string>(_cachedRunState.owned_skills)
                : new List<string>();
            data.totalStars = _cachedRunState.total_stars;
            data.tutorialComplete = _cachedRunState.tutorial_completed;
            vc_SaveManager.Save(data);

            string targetScene = !string.IsNullOrWhiteSpace(_cachedRunState.floor_scene)
                ? _cachedRunState.floor_scene
                : fallbackContinueScene;

            TelemetryManager.Instance?.TagButtonClick("Continue");
            SceneLoader.LoadByName(targetScene);
        }

        // ── My Progress (stats overlay) ─────────────────────────────────

        private void OnStatsClicked(ClickEvent _)
        {
            SetMenuVisible(false);
            _statsOverlay?.SetDisplay(true);
            _statsContent?.SetDisplay(false);
            _statsEmptyLabel?.SetDisplay(false);
            SetStatsError(string.Empty);

            if (TelemetryManager.Instance == null)
            {
                SetStatsError("Not signed in.");
                return;
            }

            TelemetryManager.Instance.GetMyProgress(
                ApplyProgressResponse,
                error =>
                {
                    Debug.LogWarning($"[MainMenu] Progress fetch failed: {error}");
                    SetStatsError("Couldn't reach the server - check your connection.");
                });
        }

        private void ApplyProgressResponse(PlayerProgressResponse progress)
        {
            if (progress == null || !progress.has_data)
            {
                _statsEmptyLabel?.SetDisplay(true);
                _statsContent?.SetDisplay(false);
                return;
            }

            _statsEmptyLabel?.SetDisplay(false);
            _statsContent?.SetDisplay(true);

            bool runFinished = _cachedRunState != null && _cachedRunState.run_finished;

            if (_lblRunComplete != null)
            {
                _lblRunComplete.text = runFinished ? "Run complete. Start a new run to explore more careers." : string.Empty;
                _lblRunComplete.SetDisplay(runFinished);
            }

            bool isProvisional = !runFinished && progress.quests_completed < TotalQuestSlots;
            if (_lblProvisionalNote != null)
            {
                _lblProvisionalNote.text = isProvisional
                    ? $"Provisional, based on {progress.quests_completed} of {TotalQuestSlots} quests. Keep playing to sharpen this."
                    : string.Empty;
                _lblProvisionalNote.SetDisplay(isProvisional);
            }

            string career = !string.IsNullOrWhiteSpace(progress.predicted_career_result)
                ? progress.predicted_career_result
                : (!string.IsNullOrWhiteSpace(progress.predicted_career_family)
                    ? progress.predicted_career_family
                    : "Still forming...");

            if (_lblCareerName   != null) _lblCareerName.text   = career;
            if (_lblClusterLabel != null) _lblClusterLabel.text = progress.predicted_cluster_label ?? string.Empty;
            if (_lblHollandCode  != null) _lblHollandCode.text  = string.IsNullOrWhiteSpace(progress.predicted_holland_code) ? "---" : progress.predicted_holland_code;
            if (_lblQuests       != null) _lblQuests.text       = $"{progress.quests_completed}/{TotalQuestSlots}";
            if (_lblStars        != null) _lblStars.text        = progress.total_stars.ToString();
            if (_lblSource       != null) _lblSource.text       = string.IsNullOrWhiteSpace(progress.prediction_source) ? "--" : progress.prediction_source;
        }

        private void SetStatsError(string message)
        {
            if (_lblStatsError == null) return;
            _lblStatsError.text = message ?? string.Empty;
            _lblStatsError.SetDisplay(!string.IsNullOrWhiteSpace(message));
        }

        private void OnStatsCloseClicked(ClickEvent _)
        {
            _statsOverlay?.SetDisplay(false);
            SetMenuVisible(true);
        }

        private void SetMenuVisible(bool visible)
        {
            _leftPanel?.SetDisplay(visible);
            _rightPanel?.SetDisplay(visible);
            // Logout is rendered last (on top of everything), so it must be hidden
            // explicitly while the progress page is open or it floats over the page.
            _btnLogout?.SetDisplay(visible);
        }

        // ── Reset Run ───────────────────────────────────────────────────

        private void OnResetClicked(ClickEvent _) => _resetConfirmOverlay?.SetDisplay(true);
        private void OnResetCancelClicked(ClickEvent _) => _resetConfirmOverlay?.SetDisplay(false);

        private void OnResetConfirmClicked(ClickEvent _)
        {
            _resetConfirmOverlay?.SetDisplay(false);

            // Reset never deletes session_runs history server-side, only the in-progress
            // save-state (both the local staging cache and the server doc).
            vc_SaveManager.DeleteSave();
            vc_QuestAvailabilityFilter.PendingCompletedQuestIds = null;
            vc_QuestAvailabilityFilter.PendingOwnedSkillNames = null;
            PlayerPrefs.DeleteKey(TelemetryManager.RunFinishPendingPrefKey);
            _cachedRunState = null;
            SetContinueEnabled(false);

            TelemetryManager.Instance?.ResetMyRunState(
                onSuccess: _ => Debug.Log("[MainMenu] Run reset."),
                onError: error => Debug.LogWarning($"[MainMenu] Run reset failed server-side (local cache still cleared): {error}"));

            // Reflect the reset on the page immediately and unconditionally: empty state,
            // no stale counts, no run-complete/provisional notes, no old error text.
            _lblRunComplete?.SetDisplay(false);
            _lblProvisionalNote?.SetDisplay(false);
            _statsContent?.SetDisplay(false);
            _statsEmptyLabel?.SetDisplay(true);
            SetStatsError(string.Empty);
            if (_lblQuests != null) _lblQuests.text = $"0/{TotalQuestSlots}";
            if (_lblStars  != null) _lblStars.text  = "0";
        }
    }
}
