using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SunodGame.Cutscene;
using Cut = SunodGame.Cutscene.vc_CutsceneData.Cut;
using Layer = SunodGame.Cutscene.vc_CutsceneData.Layer;
using Track = SunodGame.Cutscene.vc_CutsceneData.Track;
using Key = SunodGame.Cutscene.vc_CutsceneData.Key;
using Prop = SunodGame.Cutscene.vc_CutsceneData.TrackProperty;

/// <summary>
/// Builds the loop cutscenes from the art in Assets/Art/Cutscenes/Loop (make_loop_art.py):
///  - Resources/Cutscenes/Threat_{typhoon|flood|earthquake|fire}: the threat strikes at the
///    end of a run, then the quiet aftermath (before the results),
///  - Resources/Cutscenes/GoBack: the figure sends you back, time rewinds, you wake up a
///    child again at the start of the next loop.
/// The figure and the morning house are copied from the intro cutscene (Cutscene_Intro).
/// SUNOD > Cutscenes > Build Loop Cutscenes. Safe to run again (overwrites these assets).
/// </summary>
public static class vc_LoopCutsceneBuilder
{
    private const string ArtDir = "Assets/Art/Cutscenes/Loop";
    private const string OutDir = "Assets/Resources/Cutscenes";
    private const string IntroPath = "Assets/Data/Cutscenes/Cutscene_Intro.asset";
    private const string AftermathBg = "Assets/Art/Cutscenes/Cut05_MysteriousMan/Cutscene05_Background_px.png";

    [MenuItem("SUNOD/Cutscenes/Build Loop Cutscenes")]
    public static void BuildAll()
    {
        FixImports();
        Directory.CreateDirectory(OutDir);
        var intro = AssetDatabase.LoadAssetAtPath<vc_CutsceneData>(IntroPath);

        foreach (string threat in new[] { "typhoon", "flood", "earthquake", "fire" })
            Save(BuildThreat(threat), $"{OutDir}/Threat_{threat}.asset");
        Save(BuildGoBack(intro), $"{OutDir}/GoBack.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[vc_LoopCutsceneBuilder] Built 5 loop cutscenes in " + OutDir);
    }

    // ── threats ───────────────────────────────────────────────────────────

    private static vc_CutsceneData BuildThreat(string t)
    {
        var data = NewData();
        var strike = new Cut { name = "Strike", stageColor = SkyTop(t) };
        var add = strike.layers;
        string[] lines;

        switch (t)
        {
            case "typhoon":
                add.Add(L("bg_typhoon"));
                add.Add(L("house_typhoon", Tr(Prop.TranslateX, 0.5f, true, 2, K(0, 0), K(0.5f, 1), K(1, 0))));
                add.Add(L("leaves", R(0, 0, 640, 180), Tr(Prop.TranslateX, 2.2f, true, 0, K(0, 0), K(1, -320))));
                add.Add(L("rain_heavy", R(0, -180, 320, 360), Tr(Prop.TranslateY, 0.5f, true, 0, K(0, 0), K(1, 180))));
                add.Add(L("wind", R(0, 0, 640, 180), Tr(Prop.TranslateX, 1.0f, true, 0, K(0, 0), K(1, -320))));
                add.Add(L("flash", Tr(Prop.Opacity, 6f, true, 0,
                    K(0, 0), K(0.86f, 0), K(0.88f, 0.75f), K(0.9f, 0), K(0.93f, 0.45f), K(0.96f, 0), K(1, 0))));
                lines = new[] { "Then, just before you become an adult, the sky turns grey-green.",
                                "The typhoon comes, just like the dreams said. The wind tears at everything." };
                break;
            case "flood":
                add.Add(L("bg_flood"));
                add.Add(L("house_flood"));
                add.Add(L("water", R(-40, 95, 400, 90),
                    Tr(Prop.TranslateY, 7f, false, 0, true, K(0, 90), K(1, 0)),
                    Alt(Prop.TranslateX, 3f, K(0, -5), K(1, 5))));
                add.Add(L("rain_light", R(0, -180, 320, 360), Tr(Prop.TranslateY, 0.8f, true, 0, K(0, 0), K(1, 180))));
                lines = new[] { "Then, just before you become an adult, it rains for days without stopping.",
                                "The river breaks its banks. The flood comes, just like the dreams said." };
                break;
            case "earthquake":
                var shake = Tr(Prop.TranslateX, 0.4f, true, 4, K(0, 0), K(0.25f, -2), K(0.5f, 2), K(0.75f, -1), K(1, 0));
                add.Add(L("bg_earthquake", shake));
                add.Add(LP("house_earthquake", R(0, 0, 320, 180), new Vector2(175, 122), new[] {
                    Tr(Prop.TranslateX, 0.3f, true, 3, K(0, 2), K(0.33f, -3), K(0.66f, 2), K(1, 2)),
                    Alt(Prop.Rotate, 0.6f, K(0, -1), K(1, 1.5f)) }));
                add.Add(Page(L("cracks", Tr(Prop.Opacity, 0.4f, false, 2, K(0, 0), K(1, 1))), 1));
                add.Add(Page(L("dust", new[] {
                    Alt(Prop.TranslateY, 3f, K(0, 0), K(1, -10)),
                    Tr(Prop.Opacity, 1f, false, 0, K(0, 0), K(1, 1)) }), 1));
                lines = new[] { "Then, just before you become an adult, the birds go silent one quiet morning.",
                                "The ground heaves. The earthquake comes, just like the dreams said." };
                break;
            default: // fire
                add.Add(L("bg_fire"));
                add.Add(L("glow", Alt(Prop.Opacity, 1.2f, K(0, 0.7f), K(1, 1), steps: 3)));
                add.Add(L("house_fire"));
                add.Add(L("smoke", new[] {
                    Alt(Prop.TranslateX, 4f, K(0, 0), K(1, 4)),
                    Alt(Prop.Opacity, 2.5f, K(0, 0.8f), K(1, 1)) }));
                add.Add(L("flames_a", Tr(Prop.Opacity, 0.4f, true, 2, K(0, 1), K(0.5f, 0), K(1, 0))));
                add.Add(L("flames_b", Tr(Prop.Opacity, 0.4f, true, 2, K(0, 0), K(0.5f, 1), K(1, 1))));
                add.Add(L("embers", R(0, 0, 320, 360), Tr(Prop.TranslateY, 3f, true, 0, K(0, 0), K(1, -180))));
                lines = new[] { "Then, just before you become an adult, a dry wind blows through town.",
                                "A spark, then smoke, then fire. It comes just like the dreams said." };
                break;
        }

        strike.pages = new[] { "The years pass. You grow up, and the dreams grow clearer.", lines[0], lines[1] };
        data.cuts.Add(strike);

        var after = new Cut { name = "Aftermath", stageColor = new Color32(42, 46, 58, 255) };
        after.layers.Add(new Layer { texture = Tex(AftermathBg), rect = R(0, 0, 320, 180), pivot = new Vector2(160, 90) });
        after.layers.Add(L("after_" + t));
        if (t == "typhoon" || t == "flood")
            after.layers.Add(L("rain_light", R(0, -180, 320, 360), Tr(Prop.TranslateY, 1.1f, true, 0, K(0, 0), K(1, 180))));
        if (t == "fire")
            after.layers.Add(L("smoke", new[] { Alt(Prop.TranslateX, 5f, K(0, 0), K(1, 6)), Tr(Prop.Opacity, 1f, false, 0, K(0, 0.45f), K(1, 0.45f)) }));
        after.pages = new[]
        {
            "When it's over, the town is quiet.",
            "But you remember all of it. Every person you helped. Every skill you found.",
            "And somewhere in all of it, a path that could be yours...",
        };
        data.cuts.Add(after);
        return data;
    }

    private static Color SkyTop(string t) => t switch
    {
        "typhoon" => new Color32(36, 46, 50, 255),
        "flood" => new Color32(32, 38, 54, 255),
        "earthquake" => new Color32(60, 50, 50, 255),
        _ => new Color32(38, 20, 30, 255),
    };

    // ── going back ────────────────────────────────────────────────────────

    private static vc_CutsceneData BuildGoBack(vc_CutsceneData intro)
    {
        var data = NewData();

        Cut figure = CopyCut(intro, "MysteriousMan") ?? new Cut { name = "Figure", stageColor = new Color32(42, 46, 58, 255) };
        figure.pages = new[]
        {
            "The figure from your dreams is standing in the ruins.",
            "\"Not yet,\" they say. \"You can still change what's coming.\"",
            "\"Go back. And this time, remember.\"",
        };
        data.cuts.Add(figure);

        var rewind = new Cut { name = "Rewind", stageColor = new Color32(14, 12, 34, 255) };
        rewind.layers.Add(L("rewind_bg"));
        rewind.layers.Add(L("motes", R(0, 0, 320, 360), Tr(Prop.TranslateY, 4f, true, 0, K(0, 0), K(1, -180))));
        rewind.layers.Add(LP("spiral", R(70, -30, 180, 180), new Vector2(160, 60), Tr(Prop.Rotate, 6f, true, 0, K(0, 0), K(1, -360))));
        rewind.layers.Add(L("clock", R(128, 28, 64, 64)));
        rewind.layers.Add(LP("hand_short", R(128, 28, 64, 64), new Vector2(160, 60), Tr(Prop.Rotate, 4f, true, 12, K(0, 0), K(1, -360))));
        rewind.layers.Add(LP("hand_long", R(128, 28, 64, 64), new Vector2(160, 60), Tr(Prop.Rotate, 0.8f, true, 12, K(0, 0), K(1, -360))));
        rewind.pages = new[]
        {
            "Everything rushes backward. Years fold up like paper.",
            "The rubble lifts. Walls stand up again. The town rebuilds itself, brick by brick.",
        };
        data.cuts.Add(rewind);

        Cut home = CopyCut(intro, "HouseDay") ?? new Cut { name = "Morning", stageColor = new Color32(30, 99, 214, 255) };
        home.pages = new[]
        {
            "You wake up in your old bed. You're a child again.",
            "But this time, you remember. Loop {loop} begins.",
        };
        data.cuts.Add(home);
        return data;
    }

    private static Cut CopyCut(vc_CutsceneData intro, string name)
    {
        if (intro == null) return null;
        Cut src = intro.cuts.Find(c => c.name == name);
        if (src == null) return null;
        // JsonUtility round-trip = deep copy (layers, tracks, keys), asset refs kept.
        return JsonUtility.FromJson<Cut>(JsonUtility.ToJson(src));
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static vc_CutsceneData NewData()
    {
        var d = ScriptableObject.CreateInstance<vc_CutsceneData>();
        d.charsPerSecond = 40f;
        d.punctuationPause = 0.12f;
        d.fadeDuration = 0.45f;
        return d;
    }

    private static void Save(vc_CutsceneData data, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<vc_CutsceneData>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(data, existing);
            EditorUtility.SetDirty(existing);
        }
        else AssetDatabase.CreateAsset(data, path);
    }

    private static Rect R(float x, float y, float w, float h) => new Rect(x, y, w, h);
    private static Key K(float t, float v) => new Key(t, v);

    /// <summary>Full-stage layer.</summary>
    private static Layer L(string art, params Track[] tracks) => LP(art, R(0, 0, 320, 180), null, tracks);

    /// <summary>Layer at a stage rect.</summary>
    private static Layer L(string art, Rect rect, params Track[] tracks) => LP(art, rect, null, tracks);

    /// <summary>Layer with a rotate/scale pivot (stage units).</summary>
    private static Layer LP(string art, Rect rect, Vector2? pivot, params Track[] tracks) => new Layer
    {
        texture = Tex($"{ArtDir}/{art}.png"),
        rect = rect,
        pivot = pivot ?? new Vector2(rect.x + rect.width / 2f, rect.y + rect.height / 2f),
        tracks = new List<Track>(tracks ?? new Track[0]),
    };

    private static Layer Page(Layer layer, int page) { layer.showFromPage = page; return layer; }

    private static Track Tr(Prop p, float duration, bool loop, int steps, params Key[] keys) =>
        Tr(p, duration, loop, steps, false, keys);

    private static Track Tr(Prop p, float duration, bool loop, int steps, bool ease, params Key[] keys) => new Track
    {
        property = p, duration = duration, loop = loop, steps = steps, easeInOut = ease,
        holdLastKey = true, keys = new List<Key>(keys),
    };

    private static Track Alt(Prop p, float duration, Key from, Key to, int steps = 0) => new Track
    {
        property = p, duration = duration, loop = true, alternate = true, steps = steps, easeInOut = steps == 0,
        holdLastKey = true, keys = new List<Key> { from, to },
    };

    private static Texture2D Tex(string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) Debug.LogWarning("[vc_LoopCutsceneBuilder] Missing art: " + path);
        return tex;
    }

    /// <summary>Crisp pixels: point filter, no compression or mipmaps, true size.</summary>
    private static void FixImports()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir, "Assets/Resources/Loop" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
            bool forSprites = path.StartsWith("Assets/Resources/Loop");
            imp.textureType = TextureImporterType.Default;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.isReadable = forSprites;
            imp.SaveAndReimport();
        }
    }
}
