#if UNITY_EDITOR
using System.Collections;
using UnityEngine.UIElements;
using UnityEngine;
using D = vc_TrailerDirector;

/// <summary>TRAILER ONLY. The scripted shots. Call e.g. vc_TrailerDirector.Run(vc_TrailerShots.FlyElementary("fly_elem_a")).</summary>
public static class vc_TrailerShots
{
    // ── establishing flyovers (HUD hidden) ────────────────────────────────

    public static IEnumerator Flyover(string name, string town, Vector2[] path, float seconds, int ratio)
    {
        yield return D.LoadTown(town);
        D.HideUi(true);
        D.SetRatio(ratio);
        D.HoldCamera(true);
        D.PlaceCam(path[0]);
        yield return D.Wait(0.5f);
        yield return D.Shot(name, seconds, D.Fly(path, seconds));
        D.HideUi(false);
        D.HoldCamera(false);
    }

    public static IEnumerator FlyElementary(string name) => Flyover(name, "Town_Elementary",
        new[] { new Vector2(44f, 6f), new Vector2(36f, 10f), new Vector2(30f, 18f), new Vector2(28f, 26f) }, 10f, 3);

    // river town: boardwalk over the river mouth, along the harbour promenade, up to the market
    public static IEnumerator FlyHighSchool(string name) => Flyover(name, "Town_HighSchool",
        new[] { new Vector2(11f, 10f), new Vector2(20f, 12f), new Vector2(28f, 15f), new Vector2(30f, 22f) }, 8f, 3);

    // city on the bay: from the pier up the port avenue to the roundabout and the campus
    public static IEnumerator FlyCollege(string name) => Flyover(name, "Town_College",
        new[] { new Vector2(44f, 7f), new Vector2(40f, 12f), new Vector2(32f, 17f), new Vector2(31f, 24f), new Vector2(30.5f, 31f) }, 8f, 3);

    // end card background: a slow drift over the Elementary plaza
    public static IEnumerator DriftEnd(string name) => Flyover(name, "Town_Elementary",
        new[] { new Vector2(24f, 21f), new Vector2(31f, 23.5f) }, 12f, 4);

    // ── cutscenes ─────────────────────────────────────────────────────────

    private static SunodGame.Cutscene.vc_CutscenePlayer Cut => Object.FindFirstObjectByType<SunodGame.Cutscene.vc_CutscenePlayer>();

    public static IEnumerator OpenCutscene(string res)
    {
        SunodGame.Cutscene.vc_CutscenePlayer.Play(res, () => { });
        float t = 0;
        while (Cut == null && t < 15f) { t += Time.unscaledDeltaTime; yield return null; }
        yield return D.Wait(0.2f);
    }

    public static void Tap() => typeof(SunodGame.Cutscene.vc_CutscenePlayer).GetMethod("OnTap", D.Any).Invoke(Cut, new object[] { null });

    /// <summary>Next page (finishing the typing first if needed).</summary>
    public static void Advance()
    {
        bool typing = (bool)typeof(SunodGame.Cutscene.vc_CutscenePlayer).GetField("_typing", D.Any).GetValue(Cut);
        if (typing) Tap();
        Tap();
    }

    public static void CutText(bool show)
    {
        var doc = Object.FindFirstObjectByType<SunodGame.Cutscene.vc_CutscenePlayer>().GetComponent<UnityEngine.UIElements.UIDocument>();
        var root = doc != null ? doc.rootVisualElement : null;
        if (root == null) { foreach (var d in Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None)) if (d.name.Contains("Cutscene")) root = d.rootVisualElement; }
        if (root == null) return;
        foreach (string n in new[] { "dialog-box", "btn-skip" })
        {
            var e = root.Q(n);
            if (e != null) e.style.visibility = show ? UnityEngine.UIElements.Visibility.Visible : UnityEngine.UIElements.Visibility.Hidden;
        }
        if (show) { var s = root.Q("btn-skip"); if (s != null) s.style.visibility = UnityEngine.UIElements.Visibility.Hidden; }
    }

    /// <summary>Cutscene art with no text box (the cold open).</summary>
    public static IEnumerator CutArt(string name, string res, int taps, float seconds)
    {
        yield return OpenCutscene(res);
        CutText(false);
        for (int i = 0; i < taps; i++) { Advance(); yield return D.Wait(1.1f); }
        CutText(false);
        yield return D.Wait(0.3f);
        yield return D.Shot(name, seconds);
    }

    /// <summary>A cutscene with its own text: record while tapping at the given times.</summary>
    public static IEnumerator CutWithText(string name, string res, int skipTaps, float seconds, float[] tapAt)
    {
        yield return OpenCutscene(res);
        CutText(false);
        for (int i = 0; i < skipTaps; i++) { Advance(); yield return D.Wait(1.1f); }
        CutText(true);
        yield return D.Shot(name, seconds, TapsAt(tapAt));
    }

    private static IEnumerator TapsAt(float[] times)
    {
        float t = 0; int i = 0;
        while (i < times.Length)
        {
            if (t >= times[i]) { Tap(); i++; }
            t += Time.deltaTime;
            yield return null;
        }
    }

    public static IEnumerator AllCutscenes()
    {
        vc_LoopMeta.Set(new SunodGame.Models.LoopMetaResponse { current_loop = 2, loops_completed = 1, threat = "typhoon", memory_slots = 3, pockets = 2, max_pockets = 4 });
        yield return CutArt("cut_storm_art", "Threat_typhoon", 0, 12f);
        yield return CutArt("cut_aftermath_art", "Threat_typhoon", 3, 6f);
        // the drop: "The typhoon comes, just like the dreams said." then the aftermath
        yield return CutWithText("cut_storm_text", "Threat_typhoon", 2, 10f, new[] { 6f });
        // "Go back. And this time, remember." then the rewind clock
        yield return CutWithText("cut_goback", "GoBack", 2, 12f, new[] { 3.6f });
        // home: "You wake up in your old bed..." then "Loop 2 begins."
        yield return CutWithText("cut_wake", "GoBack", 5, 10f, new[] { 3.5f });
    }

    // title: sea -> beach hut -> plaza -> school, long enough for the whole title section
    public static IEnumerator FlyElementaryLong(string name) => Flyover(name, "Town_Elementary",
        new[] { new Vector2(46f, 4.5f), new Vector2(38f, 9f), new Vector2(31f, 16f), new Vector2(28f, 23f), new Vector2(27.5f, 30f) }, 14f, 3);

    public static IEnumerator AllFlyovers()
    {
        yield return FlyElementary("fly_elem");
        yield return FlyHighSchool("fly_hs");
        yield return FlyCollege("fly_college");
        yield return DriftEnd("drift_end");
    }
}
#endif
