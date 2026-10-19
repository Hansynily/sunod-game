using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Slides the screen's content up so the text field being typed in sits above the phone's
/// on-screen keyboard. Attach next to a UIDocument; it moves the document's first child.
///
/// Android often reports the keyboard late or not at all (TouchScreenKeyboard.visible false,
/// area zero), so on a phone a focused text field is treated as "keyboard open" and the
/// keyboard height falls back to a landscape-sized guess until the real one is known.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class vc_KeyboardAvoider : MonoBehaviour
{
    [SerializeField] private float gapAbovePanelPx = 24f;
    [Tooltip("Keyboard height as a share of the screen when the device doesn't report it (landscape phones: ~0.55-0.6).")]
    [SerializeField] private float fallbackKeyboardRatio = 0.58f;
    [SerializeField] private float slideSpeed = 12f;
    [SerializeField] private float topMarginPx = 8f;

    private VisualElement _root;
    private VisualElement _content;
    private TextField _focused;
    private float _currentOffset;
    private float _targetOffset;
    private float _lastKnownKeyboard;   // screen px, remembered once the device reports it

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        if (_root == null) return;

        _content = _root.childCount > 0 ? _root[0] : _root;

        _root.RegisterCallback<FocusInEvent>(OnFocusIn);
        _root.RegisterCallback<FocusOutEvent>(OnFocusOut);

        _root.Query<TextField>().ForEach(field => field.hideMobileInput = true);
    }

    private void OnDisable()
    {
        if (_root != null)
        {
            _root.UnregisterCallback<FocusInEvent>(OnFocusIn);
            _root.UnregisterCallback<FocusOutEvent>(OnFocusOut);
        }

        _focused = null;
        _currentOffset = 0f;
        _targetOffset = 0f;
        if (_content != null) _content.style.translate = new Translate(0, 0);
    }

    private void OnFocusIn(FocusInEvent e)
    {
        if (e.target is TextField field) _focused = field;
        else if (e.target is VisualElement ve)
        {
            TextField owner = ve.GetFirstAncestorOfType<TextField>();
            if (owner != null) _focused = owner;
        }
    }

    private void OnFocusOut(FocusOutEvent e)
    {
        bool leavingFocused = e.target is TextField field && field == _focused;
        if (!leavingFocused && e.target is VisualElement ve)
            leavingFocused = ve.GetFirstAncestorOfType<TextField>() == _focused;

        if (leavingFocused) _focused = null;
    }

    private bool KeyboardOpen()
    {
        if (_focused == null || _focused.panel == null) return false;
        if (TouchScreenKeyboard.visible) return true;
        // Phones: focusing a text field always opens the keyboard, even if Unity doesn't say so.
        return Application.isMobilePlatform && TouchScreenKeyboard.isSupported;
    }

    private void Update()
    {
        if (_root == null || _content == null) return;

        if (KeyboardOpen())
        {
            float reported = TouchScreenKeyboard.area.height;
            if (reported > 0f) _lastKnownKeyboard = reported;
            float kbHeightScreen = _lastKnownKeyboard > 0f ? _lastKnownKeyboard : Screen.height * fallbackKeyboardRatio;

            float panelHeight = _root.layout.height;
            float scale = panelHeight > 0 && Screen.height > 0 ? panelHeight / Screen.height : 1f;
            float keyboardTop = panelHeight - kbHeightScreen * scale;

            // worldBound already includes the current lift; add it back to get the field's
            // resting position (subtracting it made the panel sink back behind the keyboard).
            Rect field = _focused.worldBound;
            float restingBottom = field.yMax + _currentOffset;
            float restingTop = field.yMin + _currentOffset;

            float needed = restingBottom + gapAbovePanelPx - keyboardTop;
            // never push the field itself off the top of the screen
            _targetOffset = Mathf.Clamp(needed, 0f, Mathf.Max(0f, restingTop - topMarginPx));
        }
        else
        {
            _targetOffset = 0f;
        }

        _currentOffset = Mathf.Lerp(_currentOffset, _targetOffset, 1f - Mathf.Exp(-slideSpeed * Time.unscaledDeltaTime));
        if (Mathf.Abs(_currentOffset - _targetOffset) < 0.5f) _currentOffset = _targetOffset;

        _content.style.translate = new Translate(0, -_currentOffset);
    }
}
