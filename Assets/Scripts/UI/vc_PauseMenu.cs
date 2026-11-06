using SunodGame.Core;
using SunodGame.Demo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SunodGame.UI;

/// <summary>
/// UI Toolkit replacement for the legacy uGUI vc_PauseMenuController.
/// Attach to a GameObject that also has a UIDocument pointing at PauseMenu.uxml.
/// Pause freezes both time (Time.timeScale = 0) and player input - except during a town
/// quest, where the world (and the quest timer) keeps running so pausing can't be used to
/// stop the clock. The notice under the title says which one applies, and what quitting
/// does to the save.
/// The "quit to menu" confirmation is shown through vc_DialogPanel
/// (same pattern as vc_EndRunUI); a local inline confirm panel is kept as a
/// fallback for when no DialogPanel exists in the scene.
/// UI V2: scrim fades, the sheet pops in and shrinks out (vc_UIAnim, unscaled time so it
/// plays while Time.timeScale = 0). Closing restores the game state at once; the sheet then
/// animates out on its own.
/// </summary>
[DisallowMultipleComponent]
public class vc_PauseMenu : MonoBehaviour
{
    public static vc_PauseMenu Instance { get; private set; }

    [SerializeField] private bool allowEscapeKey  = true;
    [SerializeField] private bool hidePauseButton = false;

    // ── Queried elements ──────────────────────────────────────────────────
    private Button        _hudBtn;
    private VisualElement _pauseOverlay;
    private VisualElement _pauseBox;
    private VisualElement _settings;
    private VisualElement _zoomRow;
    private Button        _btnClose;
    private VisualElement _mainBtns;
    private VisualElement _confirmPanel;
    private Label         _confirmBody;
    private Label         _notice;
    private Button        _btnResume;
    private Button        _btnExit;
    private Button        _btnExitYes;
    private Button        _btnExitNo;

    private Slider        _sfxSlider;
    private Slider        _musicSlider;
    private SliderInt     _zoomSlider;
    private Label         _zoomLabel;
    private Button        _btnNamesEn;
    private Button        _btnNamesFil;

    private bool             _pauseOpen;
    private PlayerController _player;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;

        _player = FindFirstObjectByType<PlayerController>();

        // NOTE: never set enabled = false in Awake - that suppresses OnEnable,
        // leaving all element fields null and every callback unregistered.
        // The scene guard is enforced inside OpenPauseMenu / OnEnable instead.
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _hudBtn       = root.Q<Button>("pause-hud-btn");
        _pauseOverlay = root.Q<VisualElement>("pause-overlay");
        _pauseBox     = root.Q<VisualElement>("pause-box");
        _settings     = root.Q<VisualElement>("pause-settings");
        _zoomRow      = root.Q<VisualElement>("pause-zoom-row");
        _btnClose     = root.Q<Button>("pause-close-btn");
        _mainBtns     = root.Q<VisualElement>("pause-main-btns");
        _confirmPanel = root.Q<VisualElement>("pause-confirm");
        _confirmBody  = root.Q<Label>("confirm-body");
        _notice       = root.Q<Label>("pause-notice");
        _btnResume    = root.Q<Button>("btn-resume");
        _btnExit      = root.Q<Button>("btn-exit");
        _btnExitYes   = root.Q<Button>("btn-exit-yes");
        _btnExitNo    = root.Q<Button>("btn-exit-no");

        _sfxSlider    = root.Q<Slider>("pause-sfx-slider");
        _musicSlider  = root.Q<Slider>("pause-music-slider");
        if (_musicSlider != null)
        {
            _musicSlider.SetValueWithoutNotify(vc_PlayerOptions.MusicVolume);
            _musicSlider.RegisterValueChangedCallback(e => vc_PlayerOptions.MusicVolume = e.newValue);
        }
        _btnNamesEn   = root.Q<Button>("pause-names-en");
        _btnNamesFil  = root.Q<Button>("pause-names-fil");

        if (_sfxSlider != null)
        {
            _sfxSlider.SetValueWithoutNotify(vc_PlayerOptions.SfxVolume);
            _sfxSlider.RegisterValueChangedCallback(e => vc_PlayerOptions.SfxVolume = e.newValue);
            _sfxSlider.RegisterCallback<PointerCaptureOutEvent>(_ => vc_Sfx.Play("click", 0.6f));
        }
        _zoomSlider   = root.Q<SliderInt>("pause-zoom-slider");
        _zoomLabel    = root.Q<Label>("pause-zoom-label");
        if (_zoomSlider != null)
        {
            // slider runs far (left) -> near (right); zoom levels run near (0) -> far
            _zoomSlider.RegisterValueChangedCallback(e => { var z = vc_CameraZoom.Instance; if (z != null) z.SetLevel(z.LevelCount - 1 - e.newValue); });
            _zoomSlider.RegisterCallback<PointerCaptureOutEvent>(_ => vc_Sfx.Play("click", 0.6f));
        }
        vc_CameraZoom.LevelChanged += OnZoomLevelChanged;
        RefreshZoomSlider();

        _btnNamesEn?.RegisterCallback<ClickEvent>(_ => SetNameLanguage(false));
        _btnNamesFil?.RegisterCallback<ClickEvent>(_ => SetNameLanguage(true));
        RefreshNameButtons();

        _hudBtn?.RegisterCallback<ClickEvent>(_ => OpenPauseMenu());
        _btnResume?.RegisterCallback<ClickEvent>(_ => ClosePauseMenu());
        _btnClose?.RegisterCallback<ClickEvent>(_ => ClosePauseMenu());
        _btnExit?.RegisterCallback<ClickEvent>(_ => RequestExitToMenu());
        _btnExitYes?.RegisterCallback<ClickEvent>(_ => HandleExitConfirmed());
        _btnExitNo?.RegisterCallback<ClickEvent>(_ => ShowMainButtons());

        // Reset to clean closed state
        _pauseOverlay?.SetDisplay(false);
        _confirmPanel?.SetDisplay(false);
        _mainBtns?.SetDisplay(true);

        // Only show the HUD pause button in gameplay/tutorial scenes (slides in from the right edge)
        bool supported = IsSupportedScene(SceneManager.GetActiveScene());
        if (supported && !hidePauseButton) vc_UIAnim.Show(_hudBtn, vc_UIAnim.Enter.SlideLeft, 0.15f);
        else _hudBtn?.SetDisplay(false);

        vc_UIAnim.PressAll(root);
    }

    private void Update()
    {
        if (!allowEscapeKey || Keyboard.current == null) return;
        if (!IsSupportedScene(SceneManager.GetActiveScene())) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            OpenPauseMenu();
    }

    private void OnDisable()
    {
        vc_CameraZoom.LevelChanged -= OnZoomLevelChanged;
        ForceRestoreGameState();
    }
    private void OnDestroy()  => ForceRestoreGameState();

    // ── Public API ────────────────────────────────────────────────────────

    public void OpenPauseMenu()
    {
        // Guard: only pause in supported scenes
        if (!IsSupportedScene(SceneManager.GetActiveScene())) return;

        if (_pauseOpen)
        {
            ShowMainButtons();
            return;
        }

        _pauseOpen = true;
        _sfxSlider?.SetValueWithoutNotify(vc_PlayerOptions.SfxVolume);
        _musicSlider?.SetValueWithoutNotify(vc_PlayerOptions.MusicVolume);
        RefreshZoomSlider();
        RefreshNameButtons();
        ShowMainButtons();
        vc_UIAnim.Show(_pauseOverlay, vc_UIAnim.Enter.Fade);
        vc_UIAnim.Show(_pauseBox, vc_UIAnim.Enter.Pop, 0.04f);
        if (_settings != null) vc_UIAnim.Stagger(_settings.Children(), vc_UIAnim.Enter.SlideUp, 0.04f, 0.12f);

        // Outside a quest the background is fully locked. During a town quest only input stops:
        // the room and its timer keep going.
        bool inQuest = IsInTownQuest();
        RefreshNotice(inQuest);
        Time.timeScale = inQuest ? 1f : 0f;
        _player?.MoveAction.Disable();
        ChallengeSessionController.Instance?.SetExternalPause(true);
    }

    public void ClosePauseMenu()
    {
        _pauseOpen = false;
        // Game state comes back immediately; the sheet animates out on unscaled time and the
        // scrim follows, so input is only held for the 0.18 s exit.
        AnimHide(_pauseBox, null, vc_UIAnim.Exit.Shrink);
        AnimHide(_pauseOverlay, () => _confirmPanel?.SetDisplay(false), vc_UIAnim.Exit.Fade);

        Time.timeScale = 1f;
        _player?.MoveAction.Enable();
        ChallengeSessionController.Instance?.SetExternalPause(false);
    }

    private static bool IsInTownQuest() => vc_TownStage.Current != null && vc_TownStage.Current.InQuest;

    /// <summary>What pausing and quitting mean right now (timer still running / where Continue resumes).</summary>
    private void RefreshNotice(bool inQuest)
    {
        if (_notice == null) return;
        vc_TownStage stage = vc_TownStage.Current;
        string text = null;
        if (inQuest)
            text = "Quitting now fails this quest.";
        else if (stage != null)
            text = $"Saved. You'll continue on Day {stage.Day}.";
        _notice.text = text ?? string.Empty;
        _notice.SetDisplay(text != null);
        _notice.EnableInClassList("pause-notice--warn", inQuest);
    }

    // ── Settings ──────────────────────────────────────────────────────────

    private void SetNameLanguage(bool filipino)
    {
        vc_Sfx.Play("click", 0.6f);
        vc_PlayerOptions.UseBaybayinNames = filipino;
        RefreshNameButtons();
    }

    private void OnZoomLevelChanged(int level)
    {
        var z = vc_CameraZoom.Instance;
        if (z != null) _zoomSlider?.SetValueWithoutNotify(z.LevelCount - 1 - level);
    }

    /// <summary>Zoom slider only shows where a vc_CameraZoom exists (the towns).</summary>
    private void RefreshZoomSlider()
    {
        vc_CameraZoom zoom = vc_CameraZoom.Instance;
        bool show = zoom != null && zoom.LevelCount > 1;
        _zoomSlider?.SetDisplay(show);
        _zoomLabel?.SetDisplay(show);
        _zoomRow?.SetDisplay(show);
        if (!show || _zoomSlider == null) return;
        _zoomSlider.lowValue  = 0;
        _zoomSlider.highValue = zoom.LevelCount - 1;
        _zoomSlider.SetValueWithoutNotify(zoom.LevelCount - 1 - zoom.Level);
    }

    private void RefreshNameButtons()
    {
        bool fil = vc_PlayerOptions.UseBaybayinNames;
        _btnNamesEn?.EnableInClassList("pause-choice-btn--on", !fil);
        _btnNamesFil?.EnableInClassList("pause-choice-btn--on", fil);
    }

    // ── Exit-to-menu flow ─────────────────────────────────────────────────

    /// <summary>
    /// Asks for confirmation before quitting. Prefers the shared vc_DialogPanel
    /// (centered board notice, matches vc_EndRunUI); falls back to the inline
    /// confirm panel if no DialogPanel is present.
    /// </summary>
    private void RequestExitToMenu()
    {
        vc_DialogPanel dialog = vc_DialogPanel.Instance
            ?? FindFirstObjectByType<vc_DialogPanel>();

        if (dialog == null)
        {
            // Fallback: inline confirm panel inside the pause card
            ShowExitConfirm();
            return;
        }

        dialog.ShowChoice(
            title:        "Quit?",
            body:         GetDefaultExitConfirmBody(),
            icon:         null,
            confirmLabel: "Quit",
            cancelLabel:  "Stay",
            onConfirm:    HandleExitConfirmed,
            // Cancel returns to the still-open pause menu - re-freeze input
            // because DialogPanel.Hide() re-enables it on close.
            onCancel:     () => _player?.MoveAction.Disable()
        );
    }

    private void HandleExitConfirmed()
    {
        // Quitting mid-quest ends that quest as not completed and saves it first, so it can't
        // be retried by quitting and pressing Continue.
        if (IsInTownQuest()) vc_TownStage.Current.AbandonQuestForQuit();

        // timeScale must be restored or the menu scene loads frozen.
        ClosePauseMenu();
        StartCoroutine(ExitAfterSave());
    }

    /// <summary>
    /// The save lives on the server only: give the last checkpoint a moment to land before
    /// leaving, so Continue (on this device or another) picks up exactly where this left off.
    /// </summary>
    private System.Collections.IEnumerator ExitAfterSave()
    {
        _player?.MoveAction.Disable();
        var telemetry = SunodGame.Telemetry.TelemetryManager.Instance;
        if (telemetry != null && telemetry.HasUnsavedRunState)
        {
            vc_QuestHUD.Instance?.ShowFeedbackTimed("Saving...", 6f);
            yield return telemetry.WaitForRunStateSaved(6f);
        }

        // Clear everything gathered during the run before leaving.
        ClearRunRecords();
        SceneLoader.GoToMainMenu();
    }

    /// <summary>
    /// Cancels the active run's state before leaving the scene.
    /// Intentionally does NOT touch vc_SkillManager or vc_PlayerInventory -
    /// those get reset inside SceneLoader.GoToPlay() → ResetForNewRun() when
    /// the player starts the next run. Calling ResetForNewRun() here too
    /// destroys skill child objects, leaving a stale Instance that crashes
    /// the second call from SceneLoader.
    /// Matches what the original vc_PauseMenuController did on exit.
    /// </summary>
    private void ClearRunRecords()
    {
        SessionState.Instance?.CancelRun();
        GameSessionData.Reset();
        vc_SessionTelemetry.Instance?.CancelCurrentSession();
    }

    // ── Inline-confirm fallback helpers ───────────────────────────────────

    private void ShowMainButtons()
    {
        bool wasHidden = _mainBtns != null && _mainBtns.style.display == DisplayStyle.None;
        _confirmPanel?.SetDisplay(false);
        if (wasHidden) vc_UIAnim.Show(_mainBtns, vc_UIAnim.Enter.Fade);
        else _mainBtns?.SetDisplay(true);
    }

    private void ShowExitConfirm()
    {
        if (_confirmBody != null)
            _confirmBody.text = GetDefaultExitConfirmBody();

        _mainBtns?.SetDisplay(false);
        vc_UIAnim.Show(_confirmPanel, vc_UIAnim.Enter.SlideUp);
    }

    /// <summary>
    /// vc_UIAnim.Hide, but also safe when the element was shown this same frame (its resolved
    /// display is still None, so Hide would skip it and leave the entrance running).
    /// </summary>
    private static void AnimHide(VisualElement ve, System.Action done, vc_UIAnim.Exit kind)
    {
        if (ve == null) { done?.Invoke(); return; }
        if (ve.resolvedStyle.display == DisplayStyle.None)
        {
            HideNow(ve);
            done?.Invoke();
            return;
        }
        vc_UIAnim.Hide(ve, done, kind);
    }

    /// <summary>Instant hide: cancels any running tween and clears its leftovers.</summary>
    private static void HideNow(VisualElement ve)
    {
        if (ve == null) return;
        vc_UIAnim.StopAll(ve);
        ve.style.display = DisplayStyle.None;
        ve.style.opacity = StyleKeyword.Null;
        ve.style.scale = StyleKeyword.Null;
        ve.style.translate = StyleKeyword.Null;
    }

    // ── State restoration ─────────────────────────────────────────────────

    private void ForceRestoreGameState()
    {
        _pauseOpen = false;
        HideNow(_pauseOverlay);
        HideNow(_pauseBox);
        _pauseBox?.SetDisplay(true);   // the box lives inside the overlay; only the overlay toggles
        _confirmPanel?.SetDisplay(false);
        Time.timeScale = 1f;
        _player?.MoveAction.Enable();
        ChallengeSessionController.Instance?.SetExternalPause(false);
    }

    // ── Scene helpers ─────────────────────────────────────────────────────

    private static bool IsSupportedScene(Scene scene)
    {
        if (!scene.IsValid()) return false;
        return IsTutorialSceneName(scene.name) || IsRealRunSceneName(scene.name);
    }

    private static bool IsTutorialSceneName(string sceneName)
        => sceneName == SceneLoader.SCENE_TUTORIAL;   // "Level0_Tutorial"

    private static bool IsRealRunSceneName(string sceneName)
        => sceneName == SceneLoader.SCENE_GAME        // "Game_Scene" (loaded by GoToPlay)
        || sceneName == SceneLoader.SCENE_PLAY        // "Level1_Scene" (kept for safety)
        || sceneName == vc_TownFlow.GameSceneName     // "Game_Scene_Town" (life-stage towns)
        || System.Array.IndexOf(vc_TownFlow.TownScenes, sceneName) >= 0;   // a town played directly

    private static bool IsTutorialScene()
        => IsTutorialSceneName(SceneManager.GetActiveScene().name);

    private string GetDefaultExitConfirmBody()
    {
        if (IsTutorialScene()) return "Back to the menu?";
        vc_TownStage stage = vc_TownStage.Current;
        if (stage == null) return "Quit to the menu?\nThis run will be lost.";
        return stage.InQuest
            ? "Quit to the menu?\nThis quest counts as failed."
            : "Quit to the menu?";
    }
}
