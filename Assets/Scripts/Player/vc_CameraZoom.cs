using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

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

    private Camera _cam;
    private PixelPerfectCamera _ppc;
    private float _baseOrthoSize;
    private int _baseHeight;
    private int _level;

    public int Level => _level;

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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>+1 = zoom out one level, -1 = zoom in one level.</summary>
    public void Step(int delta) => Apply(_level + delta, true);

    private void Apply(int level, bool save)
    {
        if (levels == null || levels.Length == 0) return;
        _level = Mathf.Clamp(level, 0, levels.Length - 1);
        int h = levels[_level];

        if (_ppc != null)
        {
            _ppc.refResolutionY = h;
            _ppc.refResolutionX = Mathf.RoundToInt(h * 16f / 9f);
        }
        else if (_baseHeight > 0)
        {
            _cam.orthographicSize = _baseOrthoSize * h / _baseHeight;
        }

        if (!save) return;
        try { PlayerPrefs.SetInt(PrefKey, _level); PlayerPrefs.Save(); } catch { }
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
