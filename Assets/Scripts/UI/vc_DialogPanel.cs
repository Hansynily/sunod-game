using System;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;

[DisallowMultipleComponent]
public class vc_DialogPanel : MonoBehaviour
{
    public static vc_DialogPanel Instance { get; private set; }

    // When true, PlayerController.MoveAction is disabled while the dialog is open.
    public bool pausePlayerOnShow = true;

    /// <summary>True while a message or choice is on screen.</summary>
    public bool IsOpen => _dialogRoot != null && _dialogRoot.style.display != UnityEngine.UIElements.DisplayStyle.None;

    private VisualElement _dialogRoot;
    private VisualElement _dialogIcon;
    private Label         _titleLabel;
    private Label         _bodyLabel;
    private Button        _confirmBtn;
    private Button        _cancelBtn;
    private Button        _thirdBtn;    // built in code, only shown by ShowChoice3
    private VisualElement _divider;
    private VisualElement _list;        // built in code, only shown by ShowList

    /// <summary>One row of <see cref="ShowList"/>: a coloured badge, a name and a short line.</summary>
    public readonly struct ListRow
    {
        public readonly string Badge, Name, Detail;
        public ListRow(string badge, string name, string detail) { Badge = badge; Name = name; Detail = detail; }
    }

    private static readonly System.Collections.Generic.Dictionary<string, Color> BadgeColors =
        new System.Collections.Generic.Dictionary<string, Color>
        {
            { "R", new Color32(217, 58, 74, 255) },  { "I", new Color32(240, 138, 36, 255) },
            { "A", new Color32(142, 79, 214, 255) }, { "S", new Color32(47, 163, 90, 255) },
            { "E", new Color32(47, 111, 214, 255) }, { "C", new Color32(34, 179, 196, 255) },
        };

    private Action _onConfirm;
    private Action _onCancel;
    private Action _onThird;
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
        _divider    = root.Q<VisualElement>(className: "dialog-divider");
        _titleLabel = root.Q<Label>("dialog-title");
        _bodyLabel  = root.Q<Label>("dialog-body");
        _confirmBtn = root.Q<Button>("dialog-confirm-btn");
        _cancelBtn  = root.Q<Button>("dialog-cancel-btn");

        _confirmBtn?.RegisterCallback<ClickEvent>(_ => OnConfirmClicked());
        _cancelBtn?.RegisterCallback<ClickEvent>(_ => OnCancelClicked());

        // Third button: a copy of the cancel button's look, placed right after it.
        _thirdBtn = root.Q<Button>("dialog-third-btn");
        if (_thirdBtn == null && _cancelBtn != null && _cancelBtn.parent != null)
        {
            _thirdBtn = new Button { name = "dialog-third-btn" };
            foreach (string cls in _cancelBtn.GetClasses()) _thirdBtn.AddToClassList(cls);
            _cancelBtn.parent.Insert(_cancelBtn.parent.IndexOf(_cancelBtn) + 1, _thirdBtn);
            _thirdBtn.RegisterCallback<ClickEvent>(_ => OnThirdClicked());
            _thirdBtn.SetDisplay(false);
        }

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
        _thirdBtn?.SetDisplay(false);

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
        _thirdBtn?.SetDisplay(false);

        // Dimmed backdrop matching the inventory panel convention.
        if (_dialogRoot != null)
            _dialogRoot.style.backgroundColor =
                new StyleColor(new Color(0.016f, 0.031f, 0.078f, 0.80f));

        _onConfirm = onConfirm;
        _onCancel  = onCancel;
        _onThird   = null;
        Show();
    }

    /// <summary>
    /// Three-button choice (e.g. Help / Later / Not for me). Falls back to two buttons if
    /// the third couldn't be built.
    /// </summary>
    public void ShowChoice3(string title, string body, Sprite icon,
                            string confirmLabel, string cancelLabel, string thirdLabel,
                            Action onConfirm, Action onCancel, Action onThird)
    {
        ShowChoice(title, body, icon, confirmLabel, cancelLabel, onConfirm, onCancel);
        if (_thirdBtn == null) return;
        _thirdBtn.text = thirdLabel ?? string.Empty;
        _thirdBtn.SetDisplay(true);
        _onThird = onThird;
    }

    /// <summary>
    /// Message with an aligned list under the intro line (e.g. the 6 kinds of skills): each
    /// row is a coloured badge, a name and a short line, in columns that line up.
    /// </summary>
    public void ShowList(string title, string intro, ListRow[] rows, Action onContinue)
    {
        ShowMessage(title, intro, onContinue);
        if (_bodyLabel == null || rows == null) return;

        if (_list == null)
        {
            _list = new VisualElement { name = "dialog-list", pickingMode = PickingMode.Ignore };
            _list.AddToClassList("dialog-list");
            _bodyLabel.parent.Insert(_bodyLabel.parent.IndexOf(_bodyLabel) + 1, _list);
        }
        _list.Clear();
        foreach (ListRow r in rows)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("dialog-list-row");

            var badge = new Label(r.Badge) { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("dialog-list-badge");
            badge.style.backgroundColor = !string.IsNullOrEmpty(r.Badge) && BadgeColors.TryGetValue(r.Badge.ToUpperInvariant(), out Color c)
                ? c : (Color)new Color32(196, 140, 72, 255);   // numbers etc.: warm wood
            var name = new Label(r.Name) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("dialog-list-name");
            var detail = new Label(r.Detail) { pickingMode = PickingMode.Ignore };
            detail.AddToClassList("dialog-list-detail");

            row.Add(badge);
            row.Add(name);
            row.Add(detail);
            _list.Add(row);
        }
        _list.SetDisplay(true);
        _bodyLabel.AddToClassList("dialog-body--intro");
    }

    public void Hide()
    {
        _dialogRoot?.SetDisplay(false);
        _onConfirm = null;
        _onCancel  = null;
        _onThird   = null;

        if (pausePlayerOnShow)
            _player?.MoveAction.Enable();
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void Populate(string title, string body, Sprite icon,
                          string confirmLabel, string cancelLabel)
    {
        bool hasTitle = !string.IsNullOrWhiteSpace(title);
        if (_titleLabel != null)
        {
            _titleLabel.text = title ?? string.Empty;
            // no empty title plate when a dialog has no title
            _titleLabel.style.display = hasTitle ? DisplayStyle.Flex : DisplayStyle.None;
        }
        // The title's own underline already separates it from the body; the extra divider made
        // two lines under titles and a lone line above untitled messages.
        _divider?.SetDisplay(false);
        _list?.SetDisplay(false);
        _bodyLabel?.RemoveFromClassList("dialog-body--intro");
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

    private void OnThirdClicked()
    {
        var cb = _onThird;
        Hide();
        cb?.Invoke();
    }
}
