using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws the far-forest tile used behind the towns and quest rooms (vc_TownBorder): a seamless
/// 128x128 patch of packed tree crowns seen from above, in the town trees' own greens pulled a
/// little toward the dark void colour so it reads as "far away" behind the playable map.
/// SUNOD > Towns > Make Far Forest Tile. Writes Assets/Resources/Town/far_forest.png.
/// </summary>
public static class vc_FarForestTile
{
    private const int Size = 128;
    private const string OutPath = "Assets/Resources/Town/far_forest.png";
    private static readonly string[] PaletteFrom =
        { "Assets/Art/Town/Sprites/tree_green_top.png", "Assets/Art/Town/Sprites/tree_round_top.png" };

    [MenuItem("SUNOD/Towns/Make Far Forest Tile")]
    public static void Make()
    {
        Color32[] pal = Palette();   // 0 = gap shadow (void colour) .. 4 = highlight
        var px = new Color32[Size * Size];
        for (int i = 0; i < px.Length; i++) px[i] = pal[0];

        var rng = new System.Random(20261104);
        // crowns on a torus (so the tile wraps), drawn back to front: higher crowns first
        var crowns = new List<(float x, float y, float r, float phase)>();
        for (int i = 0; i < 118; i++)
            crowns.Add(((float)rng.NextDouble() * Size, (float)rng.NextDouble() * Size,
                        6.5f + (float)rng.NextDouble() * 4.5f, (float)rng.NextDouble() * 6.283f));
        crowns.Sort((a, b) => b.y.CompareTo(a.y));

        foreach (var c in crowns)
        {
            int reach = Mathf.CeilToInt(c.r + 2f);
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    float ang = Mathf.Atan2(dy, dx);
                    // lumpy leafy outline instead of a perfect circle
                    float edge = c.r + 0.9f * Mathf.Sin(5f * ang + c.phase) + 0.5f * Mathf.Sin(9f * ang + 2f * c.phase);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > edge) continue;

                    int x = Wrap(Mathf.FloorToInt(c.x) + dx), y = Wrap(Mathf.FloorToInt(c.y) + dy);
                    // light comes from the top-left
                    float lx = -0.6f, ly = 0.8f;
                    float facing = d > 0.01f ? (dx * lx + dy * ly) / d : 0f;   // -1 away from the light .. 1 toward it
                    int shade;
                    if (d > edge - 1f) shade = facing > 0.25f ? 2 : 1;               // 1px rim
                    else if (d > edge - 2.6f && facing < -0.2f) shade = 1;           // shaded lower-right
                    else
                    {
                        // lit cap toward the top-left, mid green elsewhere
                        float hx = dx + c.r * 0.32f, hy = dy - c.r * 0.32f;
                        shade = hx * hx + hy * hy < (c.r * 0.5f) * (c.r * 0.5f) ? 3 : 2;
                    }
                    // leaf clumps: small 2px speckles one step lighter / darker
                    int h = Hash(x, y);
                    if (shade == 2 && (h & 15) == 0) shade = 3;
                    else if (shade == 3 && (h & 31) == 1) shade = 4;
                    else if (shade == 3 && (h & 15) == 2) shade = 2;
                    else if (shade == 2 && (h & 31) == 3 && facing < 0f) shade = 1;
                    px[y * Size + x] = pal[shade];
                }
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixels32(px);
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
        File.WriteAllBytes(OutPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(OutPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16;
        imp.filterMode = FilterMode.Point;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;   // needed for Tiled draw mode
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
        Debug.Log($"[vc_FarForestTile] Wrote {OutPath}");
    }

    private static int Wrap(int v) => ((v % Size) + Size) % Size;

    private static int Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    /// <summary>Four greens from the town trees (dark to light), pulled toward the void colour.</summary>
    private static Color32[] Palette()
    {
        var greens = new List<Color32>();
        foreach (string path in PaletteFrom)
        {
            if (!File.Exists(path)) continue;
            var t = new Texture2D(2, 2);
            if (t.LoadImage(File.ReadAllBytes(path)))
                foreach (Color32 c in t.GetPixels32())
                    if (c.a > 200 && c.g > c.r && c.g >= c.b) greens.Add(new Color32(c.r, c.g, c.b, 255));
            Object.DestroyImmediate(t);
        }
        Color voidC = vc_TownBorder.VoidColor;
        var levels = new Color[4];
        if (greens.Count >= 4)
        {
            // most common greens, sorted by brightness, spread over 4 levels
            var common = greens.GroupBy(c => (c.r, c.g, c.b)).OrderByDescending(g => g.Count()).Take(8)
                .Select(g => (Color)new Color32(g.Key.r, g.Key.g, g.Key.b, 255))
                .OrderBy(c => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f).ToList();
            for (int i = 0; i < 4; i++) levels[i] = common[Mathf.RoundToInt(i * (common.Count - 1) / 3f)];
        }
        else
        {
            levels[0] = new Color32(38, 74, 50, 255); levels[1] = new Color32(56, 104, 60, 255);
            levels[2] = new Color32(84, 138, 70, 255); levels[3] = new Color32(128, 172, 86, 255);
        }
        var pal = new Color32[5];
        pal[0] = voidC;
        for (int i = 0; i < 4; i++)
        {
            // far away: darker and a touch bluer than the trees you can walk up to
            Color c = Color.Lerp(levels[i], voidC, 0.42f - i * 0.04f);
            c.b = Mathf.Min(1f, c.b + 0.02f);
            pal[i + 1] = c;
        }
        return pal;
    }
}
