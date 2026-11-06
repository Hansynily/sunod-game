using System;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.UI;

[DisallowMultipleComponent]
public class vc_DialogPanel : MonoBehaviour
{
    public static vc_DialogPanel Instance { get; private set; }

    // When true, PlayerController.MoveAction is disabled while the dialog is open.
    public bool pausePlayerOnShow = true;

    /// <summary>
    /// True while a message or choice is on screen. Goes false the moment it's dismissed,
    /// even though the card still animates out for a few frames.
    /// </summary>
    public bool IsOpen => _dialogRoot != null && _open;

    private bool          _open;
    private bool          _docked;
    private VisualElement _dialogRoot;
    private VisualElement _dialogBox;
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

    private static readonly Color ChoiceScrim = new Color(0.18f, 0.12f, 0.08f, 0.55f);   // --sn-scrim

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
        _dialogBox  = root.Q<VisualElement>("dialog-box");
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
            _thirdBtn.AddToClassList("sn-btn--ghost");      // third choice is the quiet one
            _thirdBtn.AddToClassList("dialog-third-btn");
            _cancelBtn.parent.Insert(_cancelBtn.parent.IndexOf(_cancelBtn) + 1, _thirdBtn);
            _thirdBtn.RegisterCallback<ClickEvent>(_ => OnThirdClicked());
            _thirdBtn.SetDisplay(false);
        }

        vc_UIAnim.Press(_confirmBtn);
        vc_UIAnim.Press(_cancelBtn);
        vc_UIAnim.Press(_thirdBtn);

        _open = false;
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

        // Dimmed backdrop (the theme's warm scrim).
        if (_dialogRoot != null)
            _dialogRoot.style.backgroundColor = new StyleColor(ChoiceScrim);

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
            string key = (r.Badge ?? string.Empty).Trim().ToUpperInvariant();
            bool riasec = key.Length == 1 && "RIASEC".IndexOf(key[0]) >= 0;
            badge.style.backgroundColor = riasec ? vc_UITheme.Riasec(key) : vc_UITheme.Brown;   // numbers etc.: brown
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
        vc_UIAnim.Stagger(_list.Children(), vc_UIAnim.Enter.SlideUp, 0.04f, 0.14f);
    }

    /// <summary>
    /// Cutscenes (town events): the box sits at the bottom of the screen instead of the middle,
    /// so the scene above it stays visible. Off = back to centered.
    /// </summary>
    public void SetDockedBottom(bool on)
    {
        if (_dialogRoot == null) return;
        _docked = on;
        _dialogRoot.style.justifyContent = on ? new StyleEnum<Justify>(Justify.FlexEnd) : new StyleEnum<Justify>(StyleKeyword.Null);
        _dialogRoot.style.paddingBottom = on ? new StyleLength(8f) : new StyleLength(StyleKeyword.Null);
    }

    public void Hide()
    {
        // Card slides down + fades, then the scrim fades. A Show() that comes in before this
        // finishes (a callback opening the next line) cancels the exit and plays a fresh entrance.
        bool wasOpen = _open;
        _open = false;
        if (wasOpen)
        {
            AnimHide(_dialogBox, null, vc_UIAnim.Exit.SlideDown);
            AnimHide(_dialogRoot, null, vc_UIAnim.Exit.Fade);
        }
        else
        {
            HideNow(_dialogRoot);
        }
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
        _open = true;
        if (_dialogRoot != null)
        {
            // Still on screen (open, or mid exit because a callback opened the next line):
            // bring the scrim straight back from where it is and give the card a short
            // re-entrance, so back-to-back lines don't flash the backdrop.
            bool onScreen = _dialogRoot.style.display != DisplayStyle.None
                         && _dialogRoot.resolvedStyle.display != DisplayStyle.None;
            if (onScreen)
            {
                VisualElement scrim = _dialogRoot;
                float from = scrim.resolvedStyle.opacity;
                scrim.style.display = DisplayStyle.Flex;
                vc_UIAnim.Tween(scrim, 0.12f, vc_UIAnim.OutCubic,
                    e => scrim.style.opacity = Mathf.Lerp(from, 1f, e),
                    () => scrim.style.opacity = StyleKeyword.Null);   // exclusive: cancels its exit
                vc_UIAnim.Show(_dialogBox, vc_UIAnim.Enter.SlideUp, 0f, null, 0.22f);
            }
            else
            {
                vc_UIAnim.Show(_dialogRoot, vc_UIAnim.Enter.Fade);
                vc_UIAnim.Show(_dialogBox, vc_UIAnim.Enter.SlideUp, 0.03f);
            }
            if (_titleLabel != null && _titleLabel.style.display != DisplayStyle.None)
                vc_UIAnim.Show(_titleLabel, _docked ? vc_UIAnim.Enter.SlideRight : vc_UIAnim.Enter.Pop, 0.12f, null, 0.26f);
        }
        if (pausePlayerOnShow)
            _player?.MoveAction.Disable();
    }

    private void OnConfirmClicked()
    {
        if (!_open) return;     // already dismissed, card is just animating out
        var cb = _onConfirm;
        Hide();
        cb?.Invoke();
    }

    private void OnCancelClicked()
    {
        if (!_open) return;
        var cb = _onCancel;
        Hide();
        cb?.Invoke();
    }

    private void OnThirdClicked()
    {
        if (!_open) return;
        var cb = _onThird;
        Hide();
        cb?.Invoke();
    }

    /// <summary>
    /// vc_UIAnim.Hide, but also safe when the element was shown this same frame (resolved
    /// display still None, so Hide would skip it and leave the entrance running).
    /// </summary>
    private static void AnimHide(VisualElement ve, Action done, vc_UIAnim.Exit kind)
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
}
