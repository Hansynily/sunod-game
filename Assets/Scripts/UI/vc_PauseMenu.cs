using SunodGame.Core;
using SunodGame.Demo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit replacement for the legacy uGUI vc_PauseMenuController.
/// Attach to a GameObject that also has a UIDocument pointing at PauseMenu.uxml.
/// Pause freezes both time (Time.timeScale = 0) and player input.
/// The "quit to menu" confirmation is shown through vc_DialogPanel
/// (same pattern as vc_EndRunUI); a local inline confirm panel is kept as a
/// fallback for when no DialogPanel exists in the scene.
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
    private VisualElement _mainBtns;
    private VisualElement _confirmPanel;
    private Label         _confirmBody;
    private Button        _btnResume;
    private Button        _btnExit;
    private Button        _btnExitYes;
    private Button        _btnExitNo;

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
        _mainBtns     = root.Q<VisualElement>("pause-main-btns");
        _confirmPanel = root.Q<VisualElement>("pause-confirm");
        _confirmBody  = root.Q<Label>("confirm-body");
        _btnResume    = root.Q<Button>("btn-resume");
        _btnExit      = root.Q<Button>("btn-exit");
        _btnExitYes   = root.Q<Button>("btn-exit-yes");
        _btnExitNo    = root.Q<Button>("btn-exit-no");

        _hudBtn?.RegisterCallback<ClickEvent>(_ => OpenPauseMenu());
        _btnResume?.RegisterCallback<ClickEvent>(_ => ClosePauseMenu());
        _btnExit?.RegisterCallback<ClickEvent>(_ => RequestExitToMenu());
        _btnExitYes?.RegisterCallback<ClickEvent>(_ => HandleExitConfirmed());
        _btnExitNo?.RegisterCallback<ClickEvent>(_ => ShowMainButtons());

        // Reset to clean closed state
        _pauseOverlay?.SetDisplay(false);
        _confirmPanel?.SetDisplay(false);
        _mainBtns?.SetDisplay(true);

        // Only show the HUD pause button in gameplay/tutorial scenes
        bool supported = IsSupportedScene(SceneManager.GetActiveScene());
        _hudBtn?.SetDisplay(supported && !hidePauseButton);
    }

    private void Update()
    {
        if (!allowEscapeKey || Keyboard.current == null) return;
        if (!IsSupportedScene(SceneManager.GetActiveScene())) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            OpenPauseMenu();
    }

    private void OnDisable()  => ForceRestoreGameState();
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
        ShowMainButtons();
        _pauseOverlay?.SetDisplay(true);

        // Freeze time AND player input so the background is fully locked
        Time.timeScale = 0f;
        _player?.MoveAction.Disable();
        ChallengeSessionController.Instance?.SetExternalPause(true);
    }

    public void ClosePauseMenu()
    {
        _pauseOpen = false;
        _pauseOverlay?.SetDisplay(false);
        _confirmPanel?.SetDisplay(false);

        Time.timeScale = 1f;
        _player?.MoveAction.Enable();
        ChallengeSessionController.Instance?.SetExternalPause(false);
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
            title:        "Quit to Menu",
            body:         GetDefaultExitConfirmBody(),
            icon:         null,
            confirmLabel: "Yes",
            cancelLabel:  "No",
            onConfirm:    HandleExitConfirmed,
            // Cancel returns to the still-open pause menu - re-freeze input
            // because DialogPanel.Hide() re-enables it on close.
            onCancel:     () => _player?.MoveAction.Disable()
        );
    }

    private void HandleExitConfirmed()
    {
        // Clear everything gathered during the run before leaving.
        ClearRunRecords();

        // timeScale must be restored or the menu scene loads frozen.
        ClosePauseMenu();
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
        _mainBtns?.SetDisplay(true);
        _confirmPanel?.SetDisplay(false);
    }

    private void ShowExitConfirm()
    {
        if (_confirmBody != null)
            _confirmBody.text = GetDefaultExitConfirmBody();

        _mainBtns?.SetDisplay(false);
        _confirmPanel?.SetDisplay(true);
    }

    // ── State restoration ─────────────────────────────────────────────────

    private void ForceRestoreGameState()
    {
        _pauseOpen = false;
        _pauseOverlay?.SetDisplay(false);
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
        || sceneName == SceneLoader.SCENE_PLAY;       // "Level1_Scene" (kept for safety)

    private static bool IsTutorialScene()
        => IsTutorialSceneName(SceneManager.GetActiveScene().name);

    private string GetDefaultExitConfirmBody()
        => IsTutorialScene()
            ? "Return to the main menu?"
            : "Return to the main menu?\nThis will cancel the current run and clear your progress.";
}
