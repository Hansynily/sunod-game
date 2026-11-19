using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The game's screen transition (reworked 2026-11-19): a wall of small voxel cubes. Closing, the
/// cubes drop in one after another in a diagonal wave from the top-right corner; opening, they pop away in the same order, shrinking in hard steps.
///
/// Drawn as real pixel art: the texture is sized so every texture pixel covers a whole number
/// of screen pixels (about 180 rows on any screen), point-filtered, and the cubes only ever move
/// in whole pixels, so nothing looks like a scaled-up image. Colours are the Umaga palette's
/// teal and cyan (calm, never bright white).
///   - Every scene load opens this way (automatic, nothing to call).
///   - vc_PixelTransition.Through(action): close, run 'action' while covered (a teleport, say), open.
///   - Cover() / Open() for coroutines that do their own work in between; Reveal() is the
///     fire-and-forget version of Open().
/// Creates itself at startup and lives across scenes. It never blocks touches and runs on
/// unscaled time, so pausing can't leave the screen covered.
/// </summary>
public class vc_PixelTransition : MonoBehaviour
{
    public enum Pattern { Iris, Sweep }        // kept for callers; there is one transition now

    private const int TargetRows = 180;        // texture pixels down the screen (each one a whole number of screen pixels)
    private const int Cell = 12;               // one cube is 12 x 12 texture pixels
    private const float MinClose = 0.55f, MinOpen = 0.45f;
    private const float Own = 0.32f;           // share of the whole time one cube is moving

    // Umaga palette (Tools/art/sunod_palette.py): lit top, face, shaded side, outline
    private static readonly Color32[] Teal = { new Color32(92, 201, 167, 255), new Color32(62, 168, 138, 255), new Color32(46, 135, 126, 255), new Color32(36, 102, 99, 255) };
    private static readonly Color32[] Cyan = { new Color32(86, 186, 201, 255), new Color32(55, 149, 168, 255), new Color32(41, 103, 135, 255), new Color32(32, 74, 102, 255) };

    private static vc_PixelTransition _instance;
    private Canvas _canvas;
    private RawImage _image;
    private Texture2D _tex;
    private Color32[] _px;
    private int _w, _h, _cols, _rows, _screenW, _screenH;
    private float[] _start;                    // per cube: when it starts moving (0..1-Own)
    private Coroutine _run;
    private bool _through;
    private Action _pending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (_instance != null) return;
        var go = new GameObject("PixelTransition");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<vc_PixelTransition>();
    }

    private void Awake()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32000;
        _canvas.pixelPerfect = true;
        var imgGo = new GameObject("Voxels", typeof(RawImage));
        imgGo.transform.SetParent(transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        _image = imgGo.GetComponent<RawImage>();
        _image.raycastTarget = false;
        _canvas.enabled = false;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// Sizes the texture so one texture pixel = 'scale' whole screen pixels, and crops the
    /// leftover with the UV rect (so the scale is exact, never fractional).
    /// </summary>
    private void FitScreen()
    {
        int sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
        if (_tex != null && sw == _screenW && sh == _screenH) return;
        _screenW = sw; _screenH = sh;
        int scale = Mathf.Max(2, Mathf.RoundToInt(sh / (float)TargetRows));
        _w = Mathf.CeilToInt(sw / (float)scale);
        _h = Mathf.CeilToInt(sh / (float)scale);
        _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        _image.texture = _tex;
        // texture rows run bottom-up; show exactly sw x sh screen pixels' worth from the top-left
        float uw = sw / (float)(_w * scale), uh = sh / (float)(_h * scale);
        _image.uvRect = new Rect(0f, 1f - uh, uw, uh);
        _px = new Color32[_w * _h];

        _cols = Mathf.CeilToInt(_w / (float)Cell);
        _rows = Mathf.CeilToInt(_h / (float)Cell);
        _start = new float[_cols * _rows];
        int span = _cols + _rows - 2;
        var rng = new System.Random(19);
        for (int cy = 0; cy < _rows; cy++)
            for (int cx = 0; cx < _cols; cx++)
            {
                // diagonal wave from the top-right corner, with a little jitter so it isn't a ruler line
                float d = ((_cols - 1 - cx) + cy) / (float)Mathf.Max(1, span);
                float jitter = (float)rng.NextDouble() * 0.06f;
                _start[cy * _cols + cx] = Mathf.Clamp01(d * 0.94f + jitter) * (1f - Own);
            }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_through) return;                      // a Through() is already handling the screen
        if (mode == LoadSceneMode.Additive && !scene.name.StartsWith("Town_")) return;
        Reveal(0.6f);
    }

    /// <summary>The screen starts covered and opens. Does not wait.</summary>
    public static void Reveal(float seconds = 0.6f, Pattern pattern = Pattern.Iris)
    {
        if (_instance == null) return;
        _instance.Begin(_instance.Animate(Mathf.Max(MinOpen, seconds), opening: true));
    }

    /// <summary>The screen starts covered and opens. Yield on it.</summary>
    public static IEnumerator Open(float seconds = 0.6f, Pattern pattern = Pattern.Iris)
    {
        if (_instance == null) yield break;
        yield return _instance.Animate(Mathf.Max(MinOpen, seconds), opening: true);
    }

    /// <summary>The screen closes and stays covered until Open() or Reveal(). Yield on it.</summary>
    public static IEnumerator Cover(float seconds = 0.8f, Pattern pattern = Pattern.Iris)
    {
        if (_instance == null) yield break;
        yield return _instance.Animate(Mathf.Max(MinClose, seconds), opening: false);
    }

    /// <summary>Close, run 'midpoint' while covered, open.</summary>
    public static void Through(Action midpoint, float closeSeconds = 0.8f, float openSeconds = 0.6f)
    {
        if (_instance == null) { midpoint?.Invoke(); return; }
        _instance.Begin(_instance.ThroughRoutine(midpoint, Mathf.Max(MinClose, closeSeconds), Mathf.Max(MinOpen, openSeconds)));
    }

    private void Begin(IEnumerator routine)
    {
        if (_run != null) StopCoroutine(_run);
        // Never lose a teleport: if a Through() is cut short, its action still runs.
        Action pending = _pending;
        _pending = null;
        _through = false;
        if (pending != null) { try { pending(); } catch (Exception e) { Debug.LogException(e); } }
        _run = StartCoroutine(routine);
    }

    private IEnumerator ThroughRoutine(Action midpoint, float closeSeconds, float openSeconds)
    {
        _through = true;
        _pending = midpoint;
        yield return Animate(closeSeconds, opening: false);
        _pending = null;
        try { midpoint?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
        float hold = 0f;                           // a short beat on the finished wall; the camera catches up
        while (hold < 0.08f) { yield return null; hold += Time.unscaledDeltaTime; }
        _through = false;
        yield return Animate(openSeconds, opening: true);
    }

    private IEnumerator Animate(float seconds, bool opening)
    {
        FitScreen();
        _canvas.enabled = true;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            Paint(t / seconds, opening);
            yield return null;
        }
        Paint(1f, opening);
        if (opening) _canvas.enabled = false;
    }

    /// <summary>
    /// progress 0..1. Closing, one cube: drops in from a few cells up (speeding up). Opening, one cube: shrinks away in hard pixel steps (12, 10, ... 2, gone).
    /// </summary>
    private void Paint(float progress, bool opening)
    {
        var clear = new Color32(0, 0, 0, 0);
        for (int i = 0; i < _px.Length; i++) _px[i] = clear;

        for (int cy = 0; cy < _rows; cy++)
            for (int cx = 0; cx < _cols; cx++)
            {
                int idx = cy * _cols + cx;
                float k = Mathf.Clamp01((progress - _start[idx]) / Own);
                Color32[] tone = ((cx + cy) & 1) == 0 ? Teal : Cyan;
                int x0 = cx * Cell, y0 = cy * Cell;          // y grows down
                if (!opening)
                {
                    if (k <= 0f) continue;                   // not dropped yet
                    if (k >= 1f) { Cube(x0, y0, Cell, Cell, tone); continue; }
                    // fall: from 3 cells up, speeding up, snapped to whole pixels
                    float f = k * k;
                    int dy = -Mathf.RoundToInt((1f - f) * Cell * 3f);
                    Cube(x0, y0 + dy, Cell, Cell, tone);
                }
                else
                {
                    if (k >= 1f) continue;                   // gone
                    if (k <= 0f) { Cube(x0, y0, Cell, Cell, tone); continue; }
                    int size = Mathf.Clamp(Cell - 2 * Mathf.FloorToInt(k * 7f), 0, Cell);   // 12, 10, ... 2, gone
                    if (size <= 0) continue;
                    int o = (Cell - size) / 2;
                    Cube(x0 + o, y0 + o, size, size, tone);
                }
            }
        _tex.SetPixels32(_px);
        _tex.Apply(false);
    }

    /// <summary>A voxel cube: lit top band, face, shaded right side, dark outline on the bottom-right.</summary>
    private void Cube(int x0, int y0, int w, int h, Color32[] tone)
    {
        int top = h >= 10 ? 3 : h >= 4 ? 2 : 1;
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                Color32 c;
                if (j == h - 1 || i == w - 1) c = tone[3];                 // outline (bottom, right)
                else if (j < top) c = tone[0];                             // lit top
                else if (i >= w - 2 && w >= 4) c = tone[2];                // shaded side
                else c = tone[1];                                          // face
                if (i == 0 && j == 0 && w >= 4) c = tone[0];               // corner glint
                Plot(x0 + i, y0 + j, c);
            }
    }

    private void Plot(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= _w || y >= _h) return;
        _px[(_h - 1 - y) * _w + x] = c;
    }
}
