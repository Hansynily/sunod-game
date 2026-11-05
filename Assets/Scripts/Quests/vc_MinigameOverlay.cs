using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shared shell for minigame quests: a dimmed full-screen layer with a cream card (title,
/// message line, content area) built inside the town HUD's UI Toolkit root. While it is open
/// the player can't walk. Each minigame quest fills Content with its own elements.
/// </summary>
public class vc_MinigameOverlay : MonoBehaviour
{
    public static readonly Color Ink = new Color(42 / 255f, 40 / 255f, 52 / 255f);
    public static readonly Color Good = new Color(59 / 255f, 140 / 255f, 60 / 255f);
    public static readonly Color Bad = new Color(200 / 255f, 60 / 255f, 50 / 255f);

    private VisualElement _overlay;
    private Label _title, _message, _status;
    private PlayerController _player;
    private bool _movementLocked;

    public VisualElement Content { get; private set; }
    public bool IsOpen => _overlay != null && _overlay.parent != null;

    public void Open(string title)
    {
        VisualElement root = HostRoot();
        if (root == null) return;
        if (_overlay == null) Build();
        if (_overlay.parent != root) root.Add(_overlay);
        _overlay.BringToFront();
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
        if (_overlay != null) _overlay.RemoveFromHierarchy();
        if (_movementLocked && _player != null) _player.MoveAction.Enable();
        _movementLocked = false;
    }

    private void OnDisable() => Close();

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
        l.style.fontSize = size;
        l.style.color = Ink;
        l.style.whiteSpace = WhiteSpace.Normal;
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        return l;
    }

    public static Button MakeButton(string text, int size, System.Action onClick)
    {
        var b = new Button(onClick) { text = text };
        b.AddToClassList("sl-button-wide");
        b.style.fontSize = size;
        b.style.color = Ink;
        b.style.paddingLeft = 28; b.style.paddingRight = 28;
        b.style.paddingTop = 10; b.style.paddingBottom = 14;
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
        _overlay.style.backgroundColor = new Color(0.06f, 0.05f, 0.08f, 0.55f);
        _overlay.style.alignItems = Align.Center;
        _overlay.style.justifyContent = Justify.Center;

        var card = new VisualElement();
        card.AddToClassList("sl-panel-cream");
        card.style.paddingLeft = 36; card.style.paddingRight = 36;
        card.style.paddingTop = 14; card.style.paddingBottom = 18;
        card.style.alignItems = Align.Center;
        card.style.minWidth = 760;
        _overlay.Add(card);

        _title = MakeLabel("", 42);
        card.Add(_title);
        _status = MakeLabel("", 28);
        _status.style.opacity = 0.8f;
        card.Add(_status);
        _message = MakeLabel("", 28);
        _message.style.minHeight = 36;
        _message.style.marginBottom = 4;
        card.Add(_message);

        Content = new VisualElement();
        Content.style.alignItems = Align.Center;
        card.Add(Content);
    }
}
