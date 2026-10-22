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
        [SerializeField] private string fallbackContinueScene = SceneLoader.SCENE_PLAY; // used only as a floor fallback (see OnContinueClicked)

        [Header("Player Card")]
        [SerializeField] private Sprite playerCharacterSprite;

        private Button _btnPlay;
        private Button _btnTutorial;
        private Button _btnLogout;
        private Button _btnSettings;
        private Button _btnContinue;
        private Button _btnStats;
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
        private Label _lblQuestsSplit;
        private Label _lblStars;
        private Label _lblSource;
        private Label _lblStatsError;
        private Button _btnStatsClose;

        // New-run-confirm overlay
        private VisualElement _newRunConfirmOverlay;
        private Button _btnNewRunConfirm;
        private Button _btnNewRunCancel;

        // Settings overlay
        private VisualElement _settingsOverlay;
        private VisualElement _privacyOverlay;
        private Button _btnPrivacyAgree;
        private Button _btnPrivacyDecline;
        private Toggle _privacyCheckRead;
        private Toggle _privacyCheckAgree;
        private Slider _sfxSlider;
        private Slider _musicSlider;
        private Button _btnNamesEnglish;
        private Button _btnNamesFilipino;
        private Button _btnAvatarBoy;
        private Button _btnAvatarGirl;
        private Button _btnSettingsClose;

        // Character overlay (opened by tapping the player card)
        private VisualElement _characterOverlay;
        private VisualElement _characterPreview;
        private Label _lblCharacterName;
        private Button _btnCharacterClose;

        // Progress page: past playthroughs
        private VisualElement _historyList;
        private Label _lblHistoryEmpty;
        private VisualElement _settingsSurveyRow;
        private Button _btnSettingsSurveyLink;

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
            _lblQuestsSplit    = root.Q<Label>("lbl-quests-split");
            _lblStars          = root.Q<Label>("lbl-stars");
            _lblSource         = root.Q<Label>("lbl-source");
            _lblStatsError     = root.Q<Label>("lbl-stats-error");
            _btnStatsClose     = root.Q<Button>("btn-stats-close");

            _newRunConfirmOverlay = root.Q<VisualElement>("newrun-confirm-overlay");
            _btnNewRunConfirm     = root.Q<Button>("btn-newrun-confirm");
            _btnNewRunCancel      = root.Q<Button>("btn-newrun-cancel");

            _settingsOverlay  = root.Q<VisualElement>("settings-overlay");
            _privacyOverlay   = root.Q<VisualElement>("privacy-overlay");
            _btnPrivacyAgree  = root.Q<Button>("btn-privacy-agree");
            _btnPrivacyDecline = root.Q<Button>("btn-privacy-decline");
            _privacyCheckRead  = root.Q<Toggle>("privacy-check-read");
            _privacyCheckAgree = root.Q<Toggle>("privacy-check-agree");
            _sfxSlider        = root.Q<Slider>("settings-sfx-slider");
            _musicSlider      = root.Q<Slider>("settings-music-slider");
            _btnNamesEnglish  = root.Q<Button>("btn-names-english");
            _btnNamesFilipino = root.Q<Button>("btn-names-filipino");
            _btnAvatarBoy     = root.Q<Button>("btn-avatar-boy");
            _btnAvatarGirl    = root.Q<Button>("btn-avatar-girl");
            _characterOverlay  = root.Q<VisualElement>("character-overlay");
            _characterPreview  = root.Q<VisualElement>("character-preview");
            _lblCharacterName  = root.Q<Label>("lbl-character-name");
            _btnCharacterClose = root.Q<Button>("btn-character-close");
            _historyList       = root.Q<VisualElement>("history-list");
            _lblHistoryEmpty   = root.Q<Label>("lbl-history-empty");
            RefreshAvatarChoice();
            _btnSettingsClose = root.Q<Button>("btn-settings-close");
            _settingsSurveyRow     = root.Q<VisualElement>("settings-survey-row");
            _btnSettingsSurveyLink = root.Q<Button>("btn-settings-survey-link");
            if (_btnSettingsSurveyLink != null) _btnSettingsSurveyLink.text = vc_Survey.DisplayUrl;

            _btnPlay?    .RegisterCallback<ClickEvent>(OnPlayClicked);
            _btnTutorial?.RegisterCallback<ClickEvent>(OnTutorialClicked);
            _btnLogout?  .RegisterCallback<ClickEvent>(OnLogoutClicked);
            _btnSettings?.RegisterCallback<ClickEvent>(OnSettingsClicked);
            _btnContinue?.RegisterCallback<ClickEvent>(OnContinueClicked);
            _btnStats?   .RegisterCallback<ClickEvent>(OnStatsClicked);
            _btnStatsClose?  .RegisterCallback<ClickEvent>(OnStatsCloseClicked);
            _btnNewRunConfirm?.RegisterCallback<ClickEvent>(OnNewRunConfirmClicked);
            _btnNewRunCancel? .RegisterCallback<ClickEvent>(OnNewRunCancelClicked);
            _btnNamesEnglish? .RegisterCallback<ClickEvent>(OnNamesEnglishClicked);
            _btnNamesFilipino?.RegisterCallback<ClickEvent>(OnNamesFilipinoClicked);
            _btnAvatarBoy?.RegisterCallback<ClickEvent>(OnAvatarBoyClicked);
            _btnAvatarGirl?.RegisterCallback<ClickEvent>(OnAvatarGirlClicked);
            _rightPanel?.RegisterCallback<ClickEvent>(OnPlayerCardClicked);
            _btnCharacterClose?.RegisterCallback<ClickEvent>(OnCharacterCloseClicked);
            _btnSettingsClose?.RegisterCallback<ClickEvent>(OnSettingsCloseClicked);
            _btnSettingsSurveyLink?.RegisterCallback<ClickEvent>(OnSettingsSurveyLinkClicked);
            _btnPrivacyAgree?.RegisterCallback<ClickEvent>(OnPrivacyAgreeClicked);
            _btnPrivacyDecline?.RegisterCallback<ClickEvent>(OnPrivacyDeclineClicked);
            _privacyCheckRead?.RegisterValueChangedCallback(OnPrivacyCheckChanged);
            _privacyCheckAgree?.RegisterValueChangedCallback(OnPrivacyCheckChanged);
            _sfxSlider?.RegisterValueChangedCallback(OnSfxSliderChanged);
            // Dragger releases pointer capture on release - play a click so the level is audible.
            _sfxSlider?.RegisterCallback<PointerCaptureOutEvent>(OnSfxSliderReleased);
            _musicSlider?.RegisterValueChangedCallback(OnMusicSliderChanged);

            _statsOverlay?.SetDisplay(false);
            _newRunConfirmOverlay?.SetDisplay(false);
            _settingsOverlay?.SetDisplay(false);
            _characterOverlay?.SetDisplay(false);

            PopulatePlayerCard();
            RefreshContinueAvailability();
            bool needsConsent = !HasAcceptedPrivacy();
            _privacyCheckRead?.SetValueWithoutNotify(false);
            _privacyCheckAgree?.SetValueWithoutNotify(false);
            RefreshPrivacyAgreeButton();
            _privacyOverlay?.SetDisplay(needsConsent);
        }

        // â”€â”€ Privacy notice (once per account, per notice version) â”€â”€â”€â”€â”€â”€â”€
        // Bump PrivacyVersion whenever the notice text changes, so every player sees and
        // agrees to the new wording once. The server stores the version agreed to.

        private const string PrivacyVersion = "2";

        private static string PrivacyKey()
        {
            var session = SessionState.Instance;
            string who = session != null && !string.IsNullOrWhiteSpace(session.Username) ? session.Username : "guest";
            return $"sunod.privacy_ok.v{PrivacyVersion}." + who;
        }

        private static bool HasAcceptedPrivacy()
        {
            try { return PlayerPrefs.GetInt(PrivacyKey(), 0) == 1; }
            catch { return false; }
        }

        private bool PrivacyBoxesTicked() =>
            (_privacyCheckRead == null || _privacyCheckRead.value) &&
            (_privacyCheckAgree == null || _privacyCheckAgree.value);

        private void RefreshPrivacyAgreeButton() => _btnPrivacyAgree?.SetEnabled(PrivacyBoxesTicked());

        private void OnPrivacyCheckChanged(ChangeEvent<bool> _) => RefreshPrivacyAgreeButton();

        private void OnPrivacyAgreeClicked(ClickEvent _)
        {
            if (!PrivacyBoxesTicked()) return;
            try { PlayerPrefs.SetInt(PrivacyKey(), 1); PlayerPrefs.Save(); } catch { }
            TelemetryManager.Instance?.PostMyPrivacyConsent(PrivacyVersion);
            _privacyOverlay?.SetDisplay(false);
        }

        // Declining means no game: sign out and leave. Back to Login where the scene is in
        // the build; otherwise quit (Quit is a no-op on WebGL, hence Login first).
        private void OnPrivacyDeclineClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("PrivacyDecline");
            vc_PlayTimeTracker.Instance?.EndSession();
            SessionState.Instance?.ClearUser();
            if (Application.CanStreamedLevelBeLoaded(SceneLoader.SCENE_LOGIN))
                SceneLoader.GoToLogin();
            else
                Application.Quit();
        }

        private void OnDisable()
        {
            _btnPlay?    .UnregisterCallback<ClickEvent>(OnPlayClicked);
            _btnTutorial?.UnregisterCallback<ClickEvent>(OnTutorialClicked);
            _btnLogout?  .UnregisterCallback<ClickEvent>(OnLogoutClicked);
            _btnSettings?.UnregisterCallback<ClickEvent>(OnSettingsClicked);
            _btnContinue?.UnregisterCallback<ClickEvent>(OnContinueClicked);
            _btnStats?   .UnregisterCallback<ClickEvent>(OnStatsClicked);
            _btnStatsClose?  .UnregisterCallback<ClickEvent>(OnStatsCloseClicked);
            _btnNewRunConfirm?.UnregisterCallback<ClickEvent>(OnNewRunConfirmClicked);
            _btnNewRunCancel? .UnregisterCallback<ClickEvent>(OnNewRunCancelClicked);
            _btnNamesEnglish? .UnregisterCallback<ClickEvent>(OnNamesEnglishClicked);
            _btnNamesFilipino?.UnregisterCallback<ClickEvent>(OnNamesFilipinoClicked);
            _btnAvatarBoy?.UnregisterCallback<ClickEvent>(OnAvatarBoyClicked);
            _btnAvatarGirl?.UnregisterCallback<ClickEvent>(OnAvatarGirlClicked);
            _rightPanel?.UnregisterCallback<ClickEvent>(OnPlayerCardClicked);
            _btnCharacterClose?.UnregisterCallback<ClickEvent>(OnCharacterCloseClicked);
            _btnSettingsClose?.UnregisterCallback<ClickEvent>(OnSettingsCloseClicked);
            _btnSettingsSurveyLink?.UnregisterCallback<ClickEvent>(OnSettingsSurveyLinkClicked);
            _btnPrivacyAgree?.UnregisterCallback<ClickEvent>(OnPrivacyAgreeClicked);
            _btnPrivacyDecline?.UnregisterCallback<ClickEvent>(OnPrivacyDeclineClicked);
            _privacyCheckRead?.UnregisterValueChangedCallback(OnPrivacyCheckChanged);
            _privacyCheckAgree?.UnregisterValueChangedCallback(OnPrivacyCheckChanged);
            _sfxSlider?.UnregisterValueChangedCallback(OnSfxSliderChanged);
            _sfxSlider?.UnregisterCallback<PointerCaptureOutEvent>(OnSfxSliderReleased);
            _musicSlider?.UnregisterValueChangedCallback(OnMusicSliderChanged);
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

        // â”€â”€ Play / Tutorial / Logout / Settings (unchanged) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

            // The save lives on the server: wait for the reset to land before the new run's
            // first checkpoint goes out, or the reset could wipe the new run instead.
            if (TelemetryManager.Instance == null) { SceneLoader.LoadByName(playSceneName); return; }
            bool loaded = false;
            System.Action go = () => { if (loaded) return; loaded = true; SceneLoader.LoadByName(playSceneName); };
            TelemetryManager.Instance.ResetMyRunState(
                onSuccess: _ => go(),
                onError: error =>
                {
                    Debug.LogWarning($"[MainMenu] New-run reset failed server-side: {error}");
                    go();
                });
            StartCoroutine(LoadAfter(8f, go));   // never get stuck on the menu
        }

        private static System.Collections.IEnumerator LoadAfter(float seconds, System.Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action?.Invoke();
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
            vc_PlayTimeTracker.Instance?.EndSession();
            SessionState.Instance?.ClearUser();
            SceneLoader.GoToLogin();
        }

        // â”€â”€ Settings overlay â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void OnSettingsClicked(ClickEvent _)
        {
            _sfxSlider?.SetValueWithoutNotify(vc_PlayerOptions.SfxVolume);
            _musicSlider?.SetValueWithoutNotify(vc_PlayerOptions.MusicVolume);
            RefreshNameChoice();
            _settingsOverlay?.SetDisplay(true);
        }

        private void OnSettingsSurveyLinkClicked(ClickEvent _)
        {
            TelemetryManager.Instance?.TagButtonClick("SurveyLink");
            vc_Survey.Open();
        }

        private void OnSettingsCloseClicked(ClickEvent _) => _settingsOverlay?.SetDisplay(false);

        private void OnSfxSliderChanged(ChangeEvent<float> evt) => vc_PlayerOptions.SfxVolume = evt.newValue;

        private void OnSfxSliderReleased(PointerCaptureOutEvent _) => vc_Sfx.Play("click", 0.6f);

        private void OnMusicSliderChanged(ChangeEvent<float> evt) => vc_PlayerOptions.MusicVolume = evt.newValue;

        private void OnNamesEnglishClicked(ClickEvent _)  => SetBaybayinNames(false);
        private void OnNamesFilipinoClicked(ClickEvent _) => SetBaybayinNames(true);

        private void SetBaybayinNames(bool useFilipino)
        {
            if (vc_PlayerOptions.UseBaybayinNames != useFilipino)
                vc_PlayerOptions.UseBaybayinNames = useFilipino;
            RefreshNameChoice();
        }

        private void RefreshNameChoice()
        {
            bool filipino = vc_PlayerOptions.UseBaybayinNames;
            _btnNamesEnglish?.EnableInClassList("settings-choice-btn--on", !filipino);
            _btnNamesFilipino?.EnableInClassList("settings-choice-btn--on", filipino);
        }

        private void OnAvatarBoyClicked(ClickEvent _)  => SetGirlAvatar(false);
        private void OnAvatarGirlClicked(ClickEvent _) => SetGirlAvatar(true);

        private void SetGirlAvatar(bool girl)
        {
            vc_PlayerOptions.UseGirlAvatar = girl;   // always store, so the choice beats the sign-up default
            RefreshAvatarChoice();
        }

        private void RefreshAvatarChoice()
        {
            bool girl = vc_PlayerOptions.UseGirlAvatar;
            _btnAvatarBoy?.EnableInClassList("settings-choice-btn--on", !girl);
            _btnAvatarGirl?.EnableInClassList("settings-choice-btn--on", girl);
            _playerCardImage?.EnableInClassList("player-card-image--girl", girl);
            _characterPreview?.EnableInClassList("character-preview--girl", girl);
        }

        // ── Character panel (player card, top-right) ────────────────────

        private void OnPlayerCardClicked(ClickEvent _)
        {
            vc_Sfx.Play("click", 0.6f);
            var session = SessionState.Instance;
            if (_lblCharacterName != null)
                _lblCharacterName.text = session != null && !string.IsNullOrWhiteSpace(session.Username)
                    ? session.Username
                    : "Adventurer";
            RefreshAvatarChoice();
            _characterOverlay?.SetDisplay(true);
        }

        private void OnCharacterCloseClicked(ClickEvent _) => _characterOverlay?.SetDisplay(false);

        // â”€â”€ Continue â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

            // The inventory and skill buttons survive scene loads: start the resumed run from
            // a clean slate so the restore below puts back exactly what the save holds.
            vc_PlayerInventory.Instance?.ClearInventory();
            vc_SkillManager.Instance?.ResetForNewRun();

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

            // The checkpoint's floor_scene is where the player left off; Game_Scene isn't a
            // valid floor (that used to be the bug - Continue always dropped onto Level1
            // because the checkpoint stored the active-but-not-a-floor scene name).
            TelemetryManager.Instance?.TagButtonClick("Continue");

            // Town run: the checkpoint's floor_scene is the town the player was in.
            string saved = _cachedRunState.floor_scene;
            if (!string.IsNullOrWhiteSpace(saved) && System.Array.IndexOf(vc_TownFlow.TownScenes, saved) >= 0)
            {
                vc_TownFlow.PendingStartTown = saved;
                vc_TownFlow.PendingCompletedQuestIds = _cachedRunState.completed_quest_ids != null
                    ? new List<string>(_cachedRunState.completed_quest_ids)
                    : new List<string>();
                SceneLoader.LoadByName(SceneLoader.SCENE_GAME_TOWN);
                return;
            }

            string floor = !string.IsNullOrWhiteSpace(saved)
                && saved != SceneLoader.SCENE_GAME && saved != SceneLoader.SCENE_GAME_TOWN
                    ? saved
                    : fallbackContinueScene;

            SunodGame.Core.vc_FloorLoader.PendingStartFloor = floor;
            SceneLoader.LoadByName(SceneLoader.SCENE_GAME);
        }

        // â”€â”€ My Progress (stats overlay) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void OnStatsClicked(ClickEvent _)
        {
            SetMenuVisible(false);
            _statsOverlay?.SetDisplay(true);
            _statsContent?.SetDisplay(false);
            _statsEmptyLabel?.SetDisplay(false);
            _historyList?.Clear();
            _lblHistoryEmpty?.SetDisplay(false);
            SetStatsError(string.Empty);
            _statsOverlay?.Q<ScrollView>("progress-scroll")?.schedule.Execute(() =>
                _statsOverlay.Q<ScrollView>("progress-scroll").scrollOffset = Vector2.zero);

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

            TelemetryManager.Instance.GetMyRunHistory(
                ApplyRunHistory,
                error =>
                {
                    Debug.LogWarning($"[MainMenu] Run history fetch failed: {error}");
                    _lblHistoryEmpty?.SetDisplay(true);
                });
        }

        // One card per finished playthrough, newest on top (numbered oldest = #1).
        private void ApplyRunHistory(PlayerRunHistoryResponse history)
        {
            if (_historyList == null) return;
            _historyList.Clear();
            List<PlayerRunHistoryItem> runs = history?.runs ?? new List<PlayerRunHistoryItem>();
            _lblHistoryEmpty?.SetDisplay(runs.Count == 0);

            for (int i = 0; i < runs.Count; i++)
            {
                PlayerRunHistoryItem run = runs[i];
                if (run == null) continue;

                var card = new VisualElement();
                card.AddToClassList("history-card");
                card.AddToClassList("sl-panel-cream");

                var index = new Label($"#{runs.Count - i}");
                index.AddToClassList("history-index");
                card.Add(index);

                var body = new VisualElement();
                body.AddToClassList("history-body");
                string career = !string.IsNullOrWhiteSpace(run.career_result) ? run.career_result
                    : !string.IsNullOrWhiteSpace(run.career_family) ? run.career_family
                    : "No career result";
                var careerLabel = new Label(career);
                careerLabel.AddToClassList("history-career");
                body.Add(careerLabel);

                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(run.career_family) && run.career_family != career) parts.Add(run.career_family);
                string when = FormatRunDate(run.finished_at);
                if (!string.IsNullOrEmpty(when)) parts.Add(when);
                parts.Add($"{run.quests_attempted} quests ({run.quests_completed} passed)");
                parts.Add($"{run.total_stars} stars");
                if (run.play_seconds >= 60f) parts.Add($"{Mathf.RoundToInt(run.play_seconds / 60f)} min");
                var sub = new Label(string.Join("  -  ", parts));
                sub.AddToClassList("history-sub");
                body.Add(sub);
                card.Add(body);

                var code = new Label(string.IsNullOrWhiteSpace(run.holland_code) ? "---" : run.holland_code);
                code.AddToClassList("history-code");
                card.Add(code);

                _historyList.Add(card);
            }
        }

        private static string FormatRunDate(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return string.Empty;
            if (System.DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out System.DateTime utc))
                return utc.ToLocalTime().ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
            return string.Empty;
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

            // Provisional until the run ends: after College, with all 48 quests played.
            bool isProvisional = !runFinished;
            if (_lblProvisionalNote != null)
            {
                _lblProvisionalNote.text = isProvisional
                    ? $"Not final yet. Finish all {TotalQuestSlots} quests (Elementary, High School and College) for your real result."
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
            // Every quest counts once it's played (passed or failed); all 48 are required.
            int played = Mathf.Min(progress.quests_attempted, TotalQuestSlots);
            if (_lblQuests       != null) _lblQuests.text       = $"{played} of {TotalQuestSlots}";
            if (_lblQuestsSplit  != null) _lblQuestsSplit.text  = $"{progress.quests_completed} / {Mathf.Max(0, progress.quests_attempted - progress.quests_completed)}";
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
    }
}
