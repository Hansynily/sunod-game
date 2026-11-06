using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.UI;

/// <summary>
/// Shared shell for minigame quests: a dimmed full-screen layer with a cream card (title,
/// message line, content area) built inside the town HUD's UI Toolkit root. While it is open
/// the player can't walk. Each minigame quest fills Content with its own elements.
/// </summary>
public class vc_MinigameOverlay : MonoBehaviour
{
    // UI V2 tokens (minigames are shelved; this only keeps the shell on-theme)
    public static readonly Color Ink = vc_UITheme.Ink;
    public static readonly Color Good = vc_UITheme.GreenDark;
    public static readonly Color Bad = vc_UITheme.RedDark;

    private VisualElement _overlay;
    private Label _title, _message, _status;
    private PlayerController _player;
    private bool _movementLocked;
    private bool _closing;
    private VisualElement _card;

    public VisualElement Content { get; private set; }
    public bool IsOpen => _overlay != null && _overlay.parent != null && !_closing;

    public void Open(string title)
    {
        VisualElement root = HostRoot();
        if (root == null) return;
        if (_overlay == null) Build();
        bool wasOpen = _overlay.parent == root && !_closing;
        _closing = false;
        if (_overlay.parent != root) root.Add(_overlay);
        _overlay.BringToFront();
        if (!wasOpen)
        {
            vc_UIAnim.Show(_overlay, vc_UIAnim.Enter.Fade);
            vc_UIAnim.Show(_card, vc_UIAnim.Enter.Pop);
        }
        _title.text = title;
        SetMessage("", Ink);
        SetStatus("");
        Content.Clear();

        if (!_movementLocked)
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_player != null) { _player.MoveAction.Disable(); _movementLocked = true; }
        }
    }

    public void Close()
    {
        if (_overlay != null && _overlay.parent != null && !_closing)
        {
            _closing = true;
            VisualElement overlay = _overlay;
            vc_UIAnim.Hide(_card);
            vc_UIAnim.Hide(overlay, () =>
            {
                if (!_closing) return;
                _closing = false;
                overlay.RemoveFromHierarchy();
                overlay.style.display = DisplayStyle.Flex;
            }, vc_UIAnim.Exit.Fade);
        }
        if (_movementLocked && _player != null) _player.MoveAction.Enable();
        _movementLocked = false;
    }

    private void OnDisable()
    {
        Close();
        // the host is going away: drop the layer at once instead of waiting for the fade
        if (_overlay != null) { vc_UIAnim.StopAll(_overlay); _overlay.RemoveFromHierarchy(); _overlay.style.display = DisplayStyle.Flex; }
        _closing = false;
    }

    public void SetTitle(string text) { if (_title != null) _title.text = text; }

    public void SetMessage(string text, Color color)
    {
        if (_message == null) return;
        _message.text = text;
        _message.style.color = color;
    }

    public void SetStatus(string text) { if (_status != null) _status.text = text; }

    public static Label MakeLabel(string text, int size)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.fontSize = Mathf.Max(21, size);
        l.style.color = Ink;
        l.style.whiteSpace = WhiteSpace.Normal;
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        return l;
    }

    public static Button MakeButton(string text, int size, System.Action onClick)
    {
        var b = new Button(onClick) { text = text };
        b.AddToClassList("sn-btn");
        b.style.fontSize = Mathf.Max(21, size);
        vc_UIAnim.Press(b);
        return b;
    }

    private VisualElement HostRoot()
    {
        if (vc_RunProgressUI.Instance != null && vc_RunProgressUI.Instance.OverlayRoot != null)
            return vc_RunProgressUI.Instance.OverlayRoot;
        var doc = FindFirstObjectByType<UIDocument>();
        return doc != null ? doc.rootVisualElement : null;
    }

    private void Build()
    {
        _overlay = new VisualElement { pickingMode = PickingMode.Position };
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0; _overlay.style.right = 0; _overlay.style.top = 0; _overlay.style.bottom = 0;
        _overlay.AddToClassList("sn-scrim");

        var card = new VisualElement();
        card.AddToClassList("sn-panel");
        card.AddToClassList("sn-sheet");
        card.style.paddingLeft = 36; card.style.paddingRight = 36;
        card.style.alignItems = Align.Center;
        card.style.minWidth = 760;
        _overlay.Add(card);
        _card = card;

        _title = MakeLabel("", 40);
        _title.AddToClassList("sn-bold");
        card.Add(_title);
        _status = MakeLabel("", 26);
        _status.style.color = vc_UITheme.InkMuted;
        card.Add(_status);
        _message = MakeLabel("", 26);
        _message.style.minHeight = 36;
        _message.style.marginBottom = 4;
        card.Add(_message);

        Content = new VisualElement();
        Content.style.alignItems = Align.Center;
        card.Add(Content);
    }
}
