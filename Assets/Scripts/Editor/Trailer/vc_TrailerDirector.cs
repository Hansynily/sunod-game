#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// TRAILER ONLY (Editor, Play mode, offline Adventurer session). Building blocks for scripted
/// shots: camera paths snapped to whole screen pixels, HUD hiding, walking, button presses.
/// Nothing here is saved; it only drives the running game.
/// </summary>
public static class vc_TrailerDirector
{
    public const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    public static string Note = "";
    public static bool Busy;

    // ── running things ────────────────────────────────────────────────────

    public static string Run(IEnumerator routine)
    {
        Busy = true; Note = "";
        vc_OfflineSession.Run(Wrap(routine));
        return "started";
    }

    private static IEnumerator Wrap(IEnumerator r)
    {
        // manual stepping so an exception is reported instead of killing the routine silently
        var stack = new Stack<IEnumerator>(); stack.Push(r);
        while (stack.Count > 0)
        {
            IEnumerator top = stack.Peek();
            bool moved;
            try { moved = top.MoveNext(); }
            catch (Exception ex) { Note = "EXC " + ex.GetType().Name + ": " + ex.Message; Debug.LogException(ex); break; }
            if (!moved) { stack.Pop(); continue; }
            if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
            yield return top.Current;
        }
        if (vc_TrailerCapture.Recording) vc_TrailerCapture.Stop();
        Busy = false;
    }

    /// <summary>Record 'seconds' of footage while 'action' runs.</summary>
    public static IEnumerator Shot(string name, float seconds, IEnumerator action = null)
    {
        HideControls();
        string r = vc_TrailerCapture.Start(name, seconds);
        if (!r.StartsWith("recording")) { Note = r; yield break; }
        if (action != null) vc_OfflineSession.Run(action);
        while (vc_TrailerCapture.Recording) yield return null;
    }

    public static IEnumerator Wait(float s) { float t = 0; while (t < s) { t += Time.deltaTime; yield return null; } }

    // ── scene setup ───────────────────────────────────────────────────────

    public static IEnumerator LoadTown(string scene, IEnumerable<string> done = null, bool fair = false)
    {
        if (!vc_OfflineSession.Active) vc_OfflineSession.Start(true);
        vc_FairMode.Enabled = fair;
        vc_TownFlow.PendingStartTown = scene;
        vc_TownFlow.PendingCompletedQuestIds = done != null ? done.ToList() : new List<string>();
        SceneManager.LoadScene("Game_Scene_Town");
        float t = 0;
        while ((vc_TownStage.Current == null || vc_TownStage.Current.gameObject.scene.name != scene) && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
        yield return Wait(1.2f);
        CloseDialogs();
    }

    public static void CloseDialogs()
    {
        if (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) vc_DialogPanel.Instance.Hide();
    }

    // ── camera ────────────────────────────────────────────────────────────

    public static Camera Cam => Camera.main;
    public static PlayerController Player => UnityEngine.Object.FindFirstObjectByType<PlayerController>();

    public static void HoldCamera(bool on) { PlayerController p = Player; if (p != null) p.CameraHeld = on; }

    /// <summary>Pixel ratio (screen px per art px): 4 = normal town zoom, 3 = wider.</summary>
    public static void SetRatio(int ratio)
    {
        var ppc = Cam.GetComponent<PixelPerfectCamera>();
        if (ppc == null) return;
        ppc.refResolutionX = 1920 / ratio; ppc.refResolutionY = 1080 / ratio;
    }

    public static float UnitPx => 22f * Ratio;   // screen px per world unit
    public static int Ratio { get { var p = Cam.GetComponent<PixelPerfectCamera>(); return p != null ? Mathf.Max(1, 1920 / p.refResolutionX) : 4; } }

    public static void PlaceCam(Vector2 p)
    {
        float u = UnitPx;
        Cam.transform.position = new Vector3(Mathf.Round(p.x * u) / u, Mathf.Round(p.y * u) / u, Cam.transform.position.z);
    }

    /// <summary>Camera along a Catmull-Rom path through the points, eased in/out, snapped per frame.</summary>
    public static IEnumerator Fly(Vector2[] pts, float seconds, bool ease = true)
    {
        HoldCamera(true);
        float t = 0;
        while (t <= seconds)
        {
            float u = Mathf.Clamp01(t / seconds);
            if (ease) u = u * u * (3f - 2f * u);
            PlaceCam(Spline(pts, u));
            t += Time.deltaTime;
            yield return null;
        }
        PlaceCam(pts[pts.Length - 1]);
    }

    public static Vector2 Spline(Vector2[] p, float u)
    {
        if (p.Length == 1) return p[0];
        // arc-length-ish: equal time per segment weighted by length
        float[] len = new float[p.Length - 1]; float total = 0;
        for (int i = 0; i < len.Length; i++) { len[i] = Vector2.Distance(p[i], p[i + 1]); total += len[i]; }
        float d = u * total; int s = 0;
        while (s < len.Length - 1 && d > len[s]) { d -= len[s]; s++; }
        float k = len[s] > 0 ? d / len[s] : 0;
        Vector2 p0 = p[Mathf.Max(0, s - 1)], p1 = p[s], p2 = p[s + 1], p3 = p[Mathf.Min(p.Length - 1, s + 2)];
        float k2 = k * k, k3 = k2 * k;
        return 0.5f * (2f * p1 + (-p0 + p2) * k + (2f * p0 - 5f * p1 + 4f * p2 - p3) * k2 + (-p0 + 3f * p1 - 3f * p2 + p3) * k3);
    }

    // ── UI ────────────────────────────────────────────────────────────────

    private static readonly Dictionary<UIDocument, DisplayStyle> _hidden = new Dictionary<UIDocument, DisplayStyle>();

    /// <summary>Hide every UI Toolkit document (HUD, menus) for clean establishing shots.</summary>
    public static void HideUi(bool hide, bool keepLoopMoments = false)
    {
        if (hide)
        {
            foreach (UIDocument d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (d.rootVisualElement == null || _hidden.ContainsKey(d)) continue;
                if (keepLoopMoments && d.name == "[Loop Moments]") continue;
                _hidden[d] = d.rootVisualElement.resolvedStyle.display;
                d.rootVisualElement.style.display = DisplayStyle.None;
            }
        }
        else
        {
            foreach (var kv in _hidden) if (kv.Key != null && kv.Key.rootVisualElement != null) kv.Key.rootVisualElement.style.display = StyleKeyword.Null;
            _hidden.Clear();
        }
        foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.name != "PixelTransition" && c.name != "Controls UI") c.enabled = !hide;
        HideControls();
        // the IMGUI debug button
        var dbg = Type.GetType("vc_DebugMenu, Assembly-CSharp");
        var f = dbg?.GetField("ButtonHidden", Any);
        if (f != null && f.IsStatic) f.SetValue(null, true);
    }

    /// <summary>The on-screen joystick (a phone control) is hidden in every trailer shot.</summary>
    public static void HideControls()
    {
        foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.name == "Controls UI") c.enabled = false;
        var dbg = Type.GetType("vc_DebugMenu, Assembly-CSharp");
        var f = dbg?.GetField("ButtonHidden", Any);
        if (f != null && f.IsStatic) f.SetValue(null, true);
    }

    /// <summary>Press a visible UI Toolkit button whose text contains 'text' (case-insensitive).</summary>
    public static bool Press(string text)
    {
        foreach (UIDocument d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (d.rootVisualElement == null) continue;
            foreach (Button b in d.rootVisualElement.Query<Button>().ToList())
            {
                if (b.resolvedStyle.display == DisplayStyle.None || !b.enabledInHierarchy) continue;
                if (string.IsNullOrEmpty(b.text) || b.text.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (b.worldBound.width <= 0) continue;
                using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
                return true;
            }
        }
        return false;
    }

    public static string Buttons()
    {
        var names = new List<string>();
        foreach (UIDocument d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            if (d.rootVisualElement != null)
                foreach (Button b in d.rootVisualElement.Query<Button>().ToList())
                    if (b.resolvedStyle.display != DisplayStyle.None && b.worldBound.width > 0 && !string.IsNullOrEmpty(b.text)) names.Add(b.text);
        return string.Join(" | ", names);
    }

    // ── player ────────────────────────────────────────────────────────────

    /// <summary>Walk the player to a point along an A* path (vc_TownGrid), like a real player would.</summary>
    public static IEnumerator WalkTo(Vector2 target, float maxSeconds = 8f, float arrive = 0.15f)
    {
        PlayerController p = Player;
        if (p == null) yield break;
        vc_TownGrid grid = vc_TownStage.Current != null ? vc_TownGrid.For(vc_TownStage.Current.transform) : null;
        List<Vector3> path = grid != null ? grid.FindPath(p.transform.position, target) : null;
        if (path == null || path.Count == 0) path = new List<Vector3> { target };
        float t = 0; int i = 0;
        while (i < path.Count && t < maxSeconds)
        {
            Vector2 d = (Vector2)(path[i] - p.transform.position);
            if (d.magnitude < (i == path.Count - 1 ? arrive : 0.3f)) { i++; continue; }
            p.ScriptedMove = d.normalized;
            t += Time.deltaTime;
            yield return null;
        }
        p.ScriptedMove = Vector2.zero;
        yield return null;
        p.ScriptedMove = null;
    }

    public static void Teleport(Vector2 pos)
    {
        PlayerController p = Player; if (p == null) return;
        p.transform.position = new Vector3(pos.x, pos.y, p.transform.position.z);
        var rb = p.GetComponent<Rigidbody2D>(); if (rb) rb.position = pos;
    }
}
#endif
