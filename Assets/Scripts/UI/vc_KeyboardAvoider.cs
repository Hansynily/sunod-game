using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class vc_KeyboardAvoider : MonoBehaviour
{
    [SerializeField] private float gapAbovePanelPx = 24f;
    [SerializeField] private float fallbackKeyboardRatio = 0.45f;
    [SerializeField] private float slideSpeed = 12f;

    private VisualElement _root;
    private VisualElement _content;
    private TextField _focused;
    private float _currentOffset;
    private float _targetOffset;

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

    private void Update()
    {
        if (_root == null || _content == null) return;

        bool keyboardVisible = TouchScreenKeyboard.visible && _focused != null;

        if (keyboardVisible)
        {
            float kbHeightScreen = TouchScreenKeyboard.area.height;
            if (kbHeightScreen <= 0) kbHeightScreen = Screen.height * fallbackKeyboardRatio;

            float panelHeight = _root.layout.height;
            float scale = panelHeight > 0 ? panelHeight / Screen.height : 1f;
            float kbHeightPanel = kbHeightScreen * scale;

            float fieldBottom = _focused.worldBound.yMax - _currentOffset;
            float keyboardTop = panelHeight - kbHeightPanel;

            _targetOffset = Mathf.Max(0f, fieldBottom + gapAbovePanelPx - keyboardTop);
        }
        else
        {
            _targetOffset = 0f;
        }

        _currentOffset = Mathf.Lerp(_currentOffset, _targetOffset, slideSpeed * Time.deltaTime);
        if (Mathf.Abs(_currentOffset - _targetOffset) < 0.5f) _currentOffset = _targetOffset;

        _content.style.translate = new Translate(0, -_currentOffset);
    }
}
