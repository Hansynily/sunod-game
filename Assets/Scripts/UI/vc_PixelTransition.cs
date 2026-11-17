using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The game's screen transition: a wall is laid brick by brick. Bricks drop straight down, one
/// after another, starting on the right: the bottom course first, right to left, then the next
/// course up, with the joints staggered like a real wall. A brick in the air is a solid block -
/// white on its face, green or blue on its side and underside - that thins out smoothly as it
/// glides into place, so the finished wall is plain white. To open, the bricks drop out in the same order.
/// Drawn as crisp pixel art: chunky pixels, hard edges, a dark rim on the far edge of each side.
///   - Every scene load opens this way (automatic, nothing to call).
///   - vc_PixelTransition.Through(action): the wall is laid, 'action' runs while the screen is
///     covered (a teleport, say), the wall drops away.
///   - Cover() / Open() for coroutines that do their own work in between; Reveal() is the
///     fire-and-forget version of Open().
/// Creates itself at startup and lives across scenes. It never blocks touches and runs on
/// unscaled time, so pausing can't leave the screen covered.
/// </summary>
public class vc_PixelTransition : MonoBehaviour
{
    public enum Pattern { Iris, Sweep }        // kept for callers; there is one transition now

    private const int H = 112;                 // texture height in chunky pixels; the width follows the screen's shape
    private const int Courses = 4;             // rows of bricks
    private const int BrickH = H / Courses;    // 28
    private const int BrickW = BrickH * 2;     // bricks are twice as wide as tall
    private const int Depth = 7;               // how thick a brick looks while it is in the air
    private const float MinClose = 0.8f, MinOpen = 0.6f;   // shorter than this and you can't see bricks land one by one

    private static readonly Color32 Face = new Color32(252, 252, 248, 255);
    private static readonly Color32 FaceLine = new Color32(226, 230, 234, 255);
    // side (facing right), underside (the darker one)
    // ...and the rim on their far edge
    private static readonly Color32[] Green = { new Color32(96, 196, 128, 255), new Color32(52, 148, 96, 255), new Color32(28, 92, 72, 255) };
    private static readonly Color32[] Blue = { new Color32(96, 168, 236, 255), new Color32(52, 112, 192, 255), new Color32(32, 64, 136, 255) };

    private struct Brick { public int x, y, w; public Color32[] tint; }

    private static vc_PixelTransition _instance;
    private Canvas _canvas;
    private RawImage _image;
    private Texture2D _tex;
    private Color32[] _px;
    private int _w;
    private readonly List<Brick> _bricks = new List<Brick>();   // in the order they are laid
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
        var imgGo = new GameObject("Bricks", typeof(RawImage));
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

    /// <summary>Sizes the texture to the screen's shape and lays out the wall.</summary>
    private void FitScreen()
    {
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        int w = Mathf.Max(BrickW, Mathf.RoundToInt(aspect * H));
        if (_tex != null && w == _w) return;
        _w = w;
        _tex = new Texture2D(_w, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        _image.texture = _tex;
        _px = new Color32[_w * H];

        _bricks.Clear();
        int n = 0;
        for (int course = 0; course < Courses; course++)        // course 0 is the bottom one
        {
            int y = H - (course + 1) * BrickH;                  // top of the course (y grows down)
            int shift = (course & 1) == 0 ? 0 : BrickW / 2;     // every other course is offset by half a brick
            for (int right = _w + shift; right > 0; right -= BrickW)
            {
                int x0 = Mathf.Max(0, right - BrickW), x1 = Mathf.Min(_w, right);
                if (x1 - x0 <= 0) continue;
                _bricks.Add(new Brick { x = x0, y = y, w = x1 - x0, tint = (n++ & 1) == 0 ? Green : Blue });
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_through) return;                      // a Through() is already handling the screen
        if (mode == LoadSceneMode.Additive && !scene.name.StartsWith("Town_")) return;
        Reveal(0.7f);
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
    /// progress 0..1. Bricks move one after another in laying order; a few are in the air at once.
    /// Closing, one brick: glides straight down into its place in one smooth move, thinning out
    /// as it arrives - no bounce and no settling step.
    /// Opening, one brick: slips away off the bottom, its thickness growing in as it leaves.
    /// </summary>
    private void Paint(float progress, bool opening)
    {
        var clear = new Color32(0, 0, 0, 0);
        for (int i = 0; i < _px.Length; i++) _px[i] = clear;

        int count = _bricks.Count;
        const float own = 0.3f;                                 // share of the whole time one brick is moving
        float gap = (1f - own) / Mathf.Max(1, count - 1);

        // bricks already down first, bricks in the air on top of them
        for (int pass = 0; pass < 2; pass++)
            for (int n = 0; n < count; n++)
            {
                float k = Mathf.Clamp01((progress - n * gap) / own);
                Brick b = _bricks[n];
                float y, depth;
                bool moving;
                if (!opening)
                {
                    if (k <= 0f) continue;                      // not dropped yet
                    moving = k < 1f;
                    float f = k * k * (3f - 2f * k);                                // one smooth glide: eases in and out
                    y = Mathf.Lerp(-BrickH - Depth, b.y, f);
                    depth = Depth * (1f - f);                                       // thins out as it nears its place: no snap, no bounce
                }
                else
                {
                    if (k >= 1f) continue;                      // gone
                    moving = k > 0f;
                    float f = k * k;                                                // slips away, speeding up
                    y = Mathf.Lerp(b.y, H + Depth, f);
                    depth = Depth * Mathf.Clamp01(k * 3f);                          // its thickness grows in as it leaves
                }
                if ((pass == 0) == moving) continue;
                DrawBrick(b.x, Mathf.RoundToInt(y), b.w, Mathf.RoundToInt(depth), b.tint);
            }
        _tex.SetPixels32(_px);
        _tex.Apply(false);
    }

    /// <summary>
    /// A brick with its face's top-left at (x0, y0), y growing down. With depth 0 it is a plain
    /// white rectangle; with depth it gains a coloured side on the right and an underside, drawn
    /// slanting down and to the right, plus a faint outline so bricks in the air read apart.
    /// </summary>
    private void DrawBrick(int x0, int y0, int w, int depth, Color32[] tint)
    {
        for (int d = depth; d >= 1; d--)                        // far slices first
        {
            bool rim = d == depth && depth > 1;             // the far edge gets a darker line
            for (int j = 0; j < BrickH; j++) Plot(x0 + w - 1 + d, y0 + j + d, rim ? tint[2] : tint[0]);   // right side
            for (int i = 0; i < w; i++) Plot(x0 + i + d, y0 + BrickH - 1 + d, rim ? tint[2] : tint[1]);    // underside
        }
        for (int j = 0; j < BrickH; j++)
        {
            int y = y0 + j;
            if (y < 0 || y >= H) continue;
            int row = (H - 1 - y) * _w;                         // texture rows run bottom-up
            bool edgeRow = depth > 0 && (j == 0 || j == BrickH - 1);
            for (int i = 0; i < w; i++)
            {
                int x = x0 + i;
                if (x < 0 || x >= _w) continue;
                _px[row + x] = (edgeRow || (depth > 0 && (i == 0 || i == w - 1))) ? FaceLine : Face;
            }
        }
    }

    private void Plot(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= _w || y >= H) return;
        _px[(H - 1 - y) * _w + x] = c;
    }
}
