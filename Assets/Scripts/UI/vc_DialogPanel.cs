using System;
using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public class vc_DialogPanel : MonoBehaviour
{
    public static vc_DialogPanel Instance { get; private set; }

    // When true, PlayerController.MoveAction is disabled while the dialog is open.
    public bool pausePlayerOnShow = true;

    private VisualElement _dialogRoot;
    private VisualElement _dialogIcon;
    private Label         _titleLabel;
    private Label         _bodyLabel;
    private Button        _confirmBtn;
    private Button        _cancelBtn;

    private Action _onConfirm;
    private Action _onCancel;
    private PlayerController _player;

    // ── Lifecycle - mirrors vc_QuestHUD exactly ───────────────────────────

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
        _player = FindFirstObjectByType<PlayerController>();
    }

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _dialogRoot = root.Q<VisualElement>("dialog-root");
        _dialogIcon = root.Q<VisualElement>("dialog-icon");
        _titleLabel = root.Q<Label>("dialog-title");
        _bodyLabel  = root.Q<Label>("dialog-body");
        _confirmBtn = root.Q<Button>("dialog-confirm-btn");
        _cancelBtn  = root.Q<Button>("dialog-cancel-btn");

        _confirmBtn?.RegisterCallback<ClickEvent>(_ => OnConfirmClicked());
        _cancelBtn?.RegisterCallback<ClickEvent>(_ => OnCancelClicked());

        _dialogRoot?.SetDisplay(false);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Narration / tap-to-continue: single "Continue" button, no backdrop dim.
    /// </summary>
    public void ShowMessage(string title, string body, Action onContinue)
    {
        Populate(title, body, null, "Continue", null);
        _cancelBtn?.SetDisplay(false);

        // No backdrop for narration - game world stays visible.
        if (_dialogRoot != null)
            _dialogRoot.style.backgroundColor = new StyleColor(Color.clear);

        _onConfirm = onContinue;
        _onCancel  = null;
        Show();
    }

    /// <summary>
    /// Two-button choice with optional skill icon and dimmed backdrop.
    /// </summary>
    public void ShowChoice(string title, string body, Sprite icon,
                           string confirmLabel, string cancelLabel,
                           Action onConfirm, Action onCancel)
    {
        Populate(title, body, icon, confirmLabel, cancelLabel);
        _cancelBtn?.SetDisplay(true);

        // Dimmed backdrop matching the inventory panel convention.
        if (_dialogRoot != null)
            _dialogRoot.style.backgroundColor =
                new StyleColor(new Color(0.016f, 0.031f, 0.078f, 0.80f));

        _onConfirm = onConfirm;
        _onCancel  = onCancel;
        Show();
    }

    public void Hide()
    {
        _dialogRoot?.SetDisplay(false);
        _onConfirm = null;
        _onCancel  = null;

        if (pausePlayerOnShow)
            _player?.MoveAction.Enable();
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void Populate(string title, string body, Sprite icon,
                          string confirmLabel, string cancelLabel)
    {
        if (_titleLabel != null) _titleLabel.text = title  ?? string.Empty;
        if (_bodyLabel  != null) _bodyLabel.text  = body   ?? string.Empty;
        if (_confirmBtn != null) _confirmBtn.text = confirmLabel ?? "Confirm";
        if (_cancelBtn  != null) _cancelBtn.text  = cancelLabel  ?? "Cancel";

        if (_dialogIcon != null)
        {
            if (icon != null)
            {
                _dialogIcon.style.backgroundImage = new StyleBackground(icon);
                _dialogIcon.SetDisplay(true);
            }
            else
            {
                _dialogIcon.SetDisplay(false);
            }
        }
    }

    private void Show()
    {
        _dialogRoot?.SetDisplay(true);
        if (pausePlayerOnShow)
            _player?.MoveAction.Disable();
    }

    private void OnConfirmClicked()
    {
        var cb = _onConfirm;
        Hide();
        cb?.Invoke();
    }

    private void OnCancelClicked()
    {
        var cb = _onCancel;
        Hide();
        cb?.Invoke();
    }
}
