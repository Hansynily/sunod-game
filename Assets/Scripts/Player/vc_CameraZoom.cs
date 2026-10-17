using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

/// <summary>
/// Camera zoom for the towns. Zoom levels are Pixel Perfect Camera reference resolutions
/// (16:9), so the pixel art stays crisp; without a Pixel Perfect Camera it scales the
/// orthographic size instead. Zoom with the +/- buttons (vc_RunProgressUI) or the mouse
/// wheel. The chosen level is remembered on the device.
/// </summary>
[RequireComponent(typeof(Camera))]
public class vc_CameraZoom : MonoBehaviour
{
    public static vc_CameraZoom Instance { get; private set; }

    private const string PrefKey = "sunod.camera_zoom";

    [Tooltip("Reference heights in pixels, most zoomed-in first. Width = height * 16 / 9.")]
    [SerializeField] private int[] levels = { 135, 180, 225, 270 };
    [Tooltip("Default level index (higher = more zoomed out).")]
    [SerializeField] private int defaultLevel = 1;
    [Tooltip("Add its own -/+ buttons under the pause button (scenes without the town HUD, e.g. the tutorial).")]
    [SerializeField] private bool ownButtons;
    [Tooltip("Furthest zoom-out level allowed in this scene (-1 = all). Small maps (tutorial) cap it so the void past the edge never shows.")]
    [SerializeField] private int maxLevel = -1;

    private Camera _cam;
    private PixelPerfectCamera _ppc;
    private float _baseOrthoSize;
    private int _baseHeight;
    private int _level;

    public int Level => _level;

    /// <summary>
    /// Usable zoom levels. With a Pixel Perfect Camera a level only counts if its whole pixel
    /// ratio differs from the one before it; on short screens the far levels all fall to x1,
    /// and "-" used to do nothing there (it looked like the button couldn't be pressed).
    /// </summary>
    public int LevelCount
    {
        get
        {
            if (levels == null || levels.Length == 0) return 0;
            int count = maxLevel >= 0 ? Mathf.Min(maxLevel + 1, levels.Length) : levels.Length;
            if (_ppc == null) return count;
            int prev = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int r = RatioFor(i);
                if (r >= prev) return i;
                prev = r;
            }
            return count;
        }
    }

    public bool CanZoomOut => _level < LevelCount - 1;
    public bool CanZoomIn => _level > 0;

    /// <summary>Fired after the zoom level changes (keeps the pause slider and +/- in sync).</summary>
    public static event System.Action<int> LevelChanged;

    private void Awake()
    {
        Instance = this;
        _cam = GetComponent<Camera>();
        _ppc = GetComponent<PixelPerfectCamera>();
        _baseOrthoSize = _cam.orthographicSize;
        _baseHeight = _ppc != null ? _ppc.refResolutionY : (levels.Length > 0 ? levels[0] : 135);

        int saved = defaultLevel;
        try { saved = PlayerPrefs.GetInt(PrefKey, defaultLevel); } catch { }
        Apply(saved, false);
    }

    private void Start()
    {
        if (ownButtons) BuildOwnButtons();
    }

    /// <summary>-/+ row hung on the pause menu's UI document (top-right, under the pause button).</summary>
    private void BuildOwnButtons()
    {
        var pause = FindFirstObjectByType<vc_PauseMenu>();
        var doc = pause != null ? pause.GetComponent<UnityEngine.UIElements.UIDocument>() : null;
        var root = doc != null ? doc.rootVisualElement?.Q("pause-root") ?? doc.rootVisualElement : null;
        if (root == null) return;

        var row = new UnityEngine.UIElements.VisualElement { pickingMode = UnityEngine.UIElements.PickingMode.Ignore };
        row.style.position = UnityEngine.UIElements.Position.Absolute;
        row.style.top = 150; row.style.right = 24;
        row.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
        var minus = new UnityEngine.UIElements.Button { text = "-" };
        var plus = new UnityEngine.UIElements.Button { text = "+" };
        foreach (var b in new[] { minus, plus })
        {
            b.AddToClassList("sl-button");
            b.style.width = 56; b.style.height = 52; b.style.marginLeft = 8;
            b.style.fontSize = 28;
            b.style.color = (Color)new Color32(60, 54, 66, 255);
        }
        BindButtons(minus, plus);
        row.Add(minus);
        row.Add(plus);
        root.Insert(1, row);            // under the pause overlay so the paused menu covers it
    }

    /// <summary>
    /// Wires a -/+ pair: fires on pointer DOWN (a click needs press and release inside the
    /// button, and a thumb sliding off it on a phone silently lost the press) and greys out
    /// whichever direction is at its limit. Used by the town HUD and the tutorial row.
    /// </summary>
    public static void BindButtons(UnityEngine.UIElements.VisualElement zoomOut, UnityEngine.UIElements.VisualElement zoomIn)
    {
        void Press(int step)
        {
            var z = Instance;
            if (z == null) return;
            if (step > 0 ? !z.CanZoomOut : !z.CanZoomIn) return;
            SunodGame.Core.vc_Sfx.Play("click", 0.6f);
            z.Step(step);
        }

        void Sync(int _ = 0)
        {
            var z = Instance;
            zoomOut.SetEnabled(z != null && z.CanZoomOut);
            zoomIn.SetEnabled(z != null && z.CanZoomIn);
        }

        if (zoomOut is UnityEngine.UIElements.Button bo) bo.clickable = null;
        if (zoomIn is UnityEngine.UIElements.Button bi) bi.clickable = null;
        zoomOut.RegisterCallback<UnityEngine.UIElements.PointerDownEvent>(_ => Press(+1), UnityEngine.UIElements.TrickleDown.TrickleDown);
        zoomIn.RegisterCallback<UnityEngine.UIElements.PointerDownEvent>(_ => Press(-1), UnityEngine.UIElements.TrickleDown.TrickleDown);

        LevelChanged += Sync;
        zoomOut.RegisterCallback<UnityEngine.UIElements.DetachFromPanelEvent>(_ => LevelChanged -= Sync);
        zoomOut.RegisterCallback<UnityEngine.UIElements.GeometryChangedEvent>(_ => Sync());
        Sync();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>+1 = zoom out one level, -1 = zoom in one level.</summary>
    public void Step(int delta) => Apply(_level + delta, true);

    /// <summary>Jump straight to a level (0 = most zoomed in). Used by the pause menu slider.</summary>
    public void SetLevel(int level) => Apply(level, true);

    private void Apply(int level, bool save)
    {
        if (levels == null || levels.Length == 0) return;
        int previous = _level;
        _level = Mathf.Clamp(level, 0, LevelCount - 1);
        int h = levels[_level];

        if (_ppc != null)
        {
            // Pick a whole pixel ratio per level and make each level's ratio distinct for this
            // screen, so every step visibly changes the zoom (225 and 270 both gave x4 at 1080p).
            int r = RatioFor(_level);
            _ppc.refResolutionY = Mathf.Max(1, Screen.height / r);
            _ppc.refResolutionX = Mathf.Max(1, Screen.width / r);
        }
        else if (_baseHeight > 0)
        {
            _cam.orthographicSize = _baseOrthoSize * h / _baseHeight;
        }

        if (!save) return;
        try { PlayerPrefs.SetInt(PrefKey, _level); PlayerPrefs.Save(); } catch { }
        if (_level != previous) LevelChanged?.Invoke(_level);
    }

    private int RatioFor(int level)
    {
        int prev = int.MaxValue;
        int r = 1;
        for (int i = 0; i <= level; i++)
        {
            r = Mathf.Max(1, Mathf.RoundToInt(Screen.height / (float)levels[i]));
            if (r >= prev) r = Mathf.Max(1, prev - 1);
            prev = r;
        }
        return r;
    }

    private void Update()
    {
        // Mouse wheel (editor / desktop). No pinch on phones: the joystick thumb plus a tap
        // would read as a pinch - the +/- buttons cover touch.
        if (Mouse.current == null) return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0.01f) Step(-1);
        else if (scroll < -0.01f) Step(+1);
    }
}
