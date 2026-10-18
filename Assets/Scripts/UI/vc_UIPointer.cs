using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Tutorial pointer: a bouncing pixel arrow plus a blinking frame around a piece of UI, so
/// "tap the skill button" or "drag the joystick" always shows WHERE. Works on UI Toolkit
/// elements, uGUI RectTransforms (joystick, timer stars) and world objects.
///
/// Self-creating: the first call makes an overlay UIDocument on the scene's shared panel
/// settings, drawn above dialogs so it can point while a message is open.
///   vc_UIPointer.PointAt(element1, element2...)   // frame around all of them
///   vc_UIPointer.PointAtWorldUI(rectTransform)
///   vc_UIPointer.PointAtWorld(transform)
///   vc_UIPointer.Hide()
/// </summary>
public class vc_UIPointer : MonoBehaviour
{
    private const int SortingOrder = 25;      // over the dialog panel (20) and skill award (21)
    private const float Pad = 8f;
    private const float ArrowSize = 56f;      // 14px art at 4x

    private static vc_UIPointer _instance;

    private VisualElement _root, _frame, _arrow;
    private Func<Rect?> _target;
    private float _shownAt;

    // ── API ─────────────────────────────────────────────────────────────

    public static void PointAt(params VisualElement[] elements)
    {
        var list = new List<VisualElement>();
        if (elements != null) foreach (VisualElement e in elements) if (e != null) list.Add(e);
        if (list.Count == 0) { Hide(); return; }
        Show(() =>
        {
            Rect? r = null;
            foreach (VisualElement e in list)
            {
                if (e.panel == null || e.resolvedStyle.display == DisplayStyle.None) continue;
                Rect b = e.worldBound;
                if (b.width <= 0f || float.IsNaN(b.width)) continue;
                r = r.HasValue ? Union(r.Value, b) : b;
            }
            return r;
        });
    }

    /// <summary>A uGUI element on a Screen Space - Overlay canvas.</summary>
    public static void PointAtWorldUI(RectTransform rt)
    {
        if (rt == null) { Hide(); return; }
        Show(() =>
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return null;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return ScreenRectToPanel(corners[0], corners[2]);
        });
    }

    /// <summary>Something in the game world (seen through the main camera).</summary>
    public static void PointAtWorld(Transform t, float worldSize = 1f)
    {
        if (t == null) { Hide(); return; }
        Show(() =>
        {
            Camera cam = Camera.main;
            if (t == null || cam == null || !t.gameObject.activeInHierarchy) return null;
            Vector3 a = cam.WorldToScreenPoint(t.position - new Vector3(worldSize, worldSize) * 0.5f);
            Vector3 b = cam.WorldToScreenPoint(t.position + new Vector3(worldSize, worldSize) * 0.5f);
            return ScreenRectToPanel(a, b);
        });
    }

    public static void Hide()
    {
        if (_instance == null) return;
        _instance._target = null;
        _instance._root?.SetDisplay(false);
    }

    public static bool IsShowing => _instance != null && _instance._target != null;

    // ── internals ───────────────────────────────────────────────────────

    private static void Show(Func<Rect?> target)
    {
        vc_UIPointer p = Ensure();
        if (p == null) return;
        p._target = target;
        p._shownAt = Time.unscaledTime;
        p._root.SetDisplay(true);
        p.Tick();
    }

    private static vc_UIPointer Ensure()
    {
        if (_instance != null) return _instance;

        PanelSettings settings = null;
        foreach (UIDocument d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            if (d.panelSettings != null) { settings = d.panelSettings; break; }
        if (settings == null) return null;

        var go = new GameObject("[UI Pointer]");
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = settings;
        doc.sortingOrder = SortingOrder;
        _instance = go.AddComponent<vc_UIPointer>();
        _instance.Build(doc.rootVisualElement);
        return _instance;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Build(VisualElement root)
    {
        _root = new VisualElement { pickingMode = PickingMode.Ignore };
        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;
        root.Add(_root);

        _frame = new VisualElement { pickingMode = PickingMode.Ignore };
        _frame.style.position = Position.Absolute;
        SetBorder(_frame, 4f, (Color)new Color32(255, 214, 90, 255));
        _root.Add(_frame);

        _arrow = new VisualElement { pickingMode = PickingMode.Ignore };
        _arrow.style.position = Position.Absolute;
        _arrow.style.width = ArrowSize;
        _arrow.style.height = ArrowSize;
        Texture2D tex = Resources.Load<Texture2D>("UIPointer/pointer_arrow");
        if (tex != null)
        {
            tex.filterMode = FilterMode.Point;
            _arrow.style.backgroundImage = new StyleBackground(tex);
        }
        else
        {
            _arrow.style.backgroundColor = (Color)new Color32(255, 214, 90, 255);
        }
        _root.Add(_arrow);
        _root.SetDisplay(false);
    }

    private void Update() => Tick();

    private void Tick()
    {
        if (_target == null || _root == null) return;
        Rect? maybe = _target();
        if (!maybe.HasValue) { _frame.visible = _arrow.visible = false; return; }

        Rect r = maybe.Value;
        float t = Time.unscaledTime - _shownAt;

        // blinking frame, a little larger than the target
        _frame.visible = true;
        _frame.style.left = r.xMin - Pad;
        _frame.style.top = r.yMin - Pad;
        _frame.style.width = r.width + Pad * 2f;
        _frame.style.height = r.height + Pad * 2f;
        bool hi = Mathf.Repeat(t, 0.8f) < 0.4f;
        SetBorder(_frame, 4f, hi ? (Color)new Color32(255, 214, 90, 255) : (Color)new Color32(255, 250, 230, 200));

        // arrow bounces toward the target in whole-pixel steps; it sits above the target, or
        // below it when the target is near the top of the screen
        float panelH = _root.layout.height > 0f ? _root.layout.height : 720f;
        bool below = r.yMin - Pad - ArrowSize - 16f < 0f && r.yMax + ArrowSize + 16f < panelH;
        float bob = Mathf.Round(Mathf.Abs(Mathf.Sin(t * 5f)) * 3f) * 4f;
        _arrow.visible = true;
        _arrow.style.left = r.center.x - ArrowSize * 0.5f;
        _arrow.style.top = below ? r.yMax + Pad + 4f + bob : r.yMin - Pad - ArrowSize - 4f - bob;
        _arrow.style.rotate = new Rotate(below ? 180f : 0f);
    }

    private static void SetBorder(VisualElement e, float w, Color c)
    {
        e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = w;
        e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = c;
    }

    private static Rect Union(Rect a, Rect b) =>
        Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    /// <summary>Two screen-pixel corners (bottom-left origin) to a panel-space rect.</summary>
    private static Rect? ScreenRectToPanel(Vector3 a, Vector3 b)
    {
        if (_instance == null || _instance._root == null || _instance._root.panel == null) return null;
        IPanel panel = _instance._root.panel;
        Vector2 pa = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(a.x, Screen.height - a.y));
        Vector2 pb = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(b.x, Screen.height - b.y));
        return Rect.MinMaxRect(Mathf.Min(pa.x, pb.x), Mathf.Min(pa.y, pb.y), Mathf.Max(pa.x, pb.x), Mathf.Max(pa.y, pb.y));
    }
}
