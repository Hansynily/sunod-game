using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class vc_SafeArea : MonoBehaviour
{
    private Rect _lastSafeArea;
    private Vector2Int _lastScreen;

    private void Update()
    {
        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.safeArea == _lastSafeArea && screen == _lastScreen) return;

        _lastSafeArea = Screen.safeArea;
        _lastScreen = screen;

        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null || root.panel == null) return;

        // Screen.safeArea is bottom-up (origin bottom-left); the panel is top-down.
        Vector2 panelMin = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(_lastSafeArea.xMin, screen.y - _lastSafeArea.yMax));
        Vector2 panelMax = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(_lastSafeArea.xMax, screen.y - _lastSafeArea.yMin));

        root.style.paddingLeft = panelMin.x;
        root.style.paddingTop = panelMin.y;
        root.style.paddingRight = root.layout.width - panelMax.x;
        root.style.paddingBottom = root.layout.height - panelMax.y;
    }
}
