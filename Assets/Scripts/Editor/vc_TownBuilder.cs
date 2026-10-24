using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

/// <summary>
/// Builds the three life-stage town scenes (Elementary / High School / College) and the
/// Game_Scene_Town gameplay scene from code. Re-run "SUNOD/Towns/Build All Towns" after
/// changing a layout below - the town scenes are regenerated from scratch each time.
///
/// Art: ground/paths/water are tiles from the Super Retro Collection atlas (autotiled);
/// buildings, trees and props are sprites made by Assets/Art/Town/Tools/town_art.py,
/// split into a base (under the player, collides) and a top (over the player).
/// Coordinates are world units: 1 unit = 1 tile = 16 px, origin = town's bottom-left.
/// Objects are placed by their bottom-center.
/// </summary>
public static partial class vc_TownBuilder
{
    private const string SpriteDir = "Assets/Art/Town/Sprites";
    private const string NpcDir = "Assets/Art/Town/NPC";
    private const string SceneDir = "Assets/Scenes/Towns";
    private const string RoomDir = "Assets/Prefabs/Rooms";
    private const string TileDir = "Assets/Gif/Super_Retro_Collection/Resources/Environments/TilePalette/Tiles";
    private const int Margin = 9;   // extra grass + trees outside the playable area so the camera never shows the void

    // â”€â”€ layout data types â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private class Town
    {
        public string scene, id, title, intro, complete;
        public int w, h, seed;
        public Vector2 spawn;
        public readonly List<(string mat, RectInt r)> paths = new List<(string, RectInt)>();
        public readonly List<RectInt> water = new List<RectInt>();
        public readonly List<(string sprite, float x, float y, bool collide)> objs = new List<(string, float, float, bool)>();
        public readonly List<Npc> npcs = new List<Npc>();
        public readonly List<(RectInt area, int count, string[] kinds)> trees = new List<(RectInt, int, string[])>();
        public string[] borderTrees = { "tree_green", "tree_pine", "tree_round" };
        public string[] decor =
        {
            "flower_red", "flower_white", "flower_blue", "flowers_white", "tiny_flower1", "tiny_flower2", "tiny_flower3",
            "bloom_orange", "bloom_blue", "bloom_pink", "clover", "clover", "bush",
            "mush_green", "mush_purple", "mush_orange", "mush_brown",
        };
        public float decorDensity = 0.085f;
        public string home;                 // house sprite used for the rebuildable home
        public Vector2 homePos, requestPos;
        public string[] nightDreams = new string[0];
        public string[] dreamAfter = new string[0];

        public void Path(string mat, int x, int y, int w, int h) => paths.Add((mat, new RectInt(x, y, w, h)));
        public void Water(int x, int y, int w, int h) => water.Add(new RectInt(x, y, w, h));
        public void Obj(string s, float x, float y, bool collide = true) => objs.Add((s, x, y, collide));
        public void Trees(int x, int y, int w, int h, int count, params string[] kinds) => trees.Add((new RectInt(x, y, w, h), count, kinds));
        public void N(string code, string name, string look, float x, float y, string prefab, string ask, string thanks)
            => npcs.Add(new Npc { code = code, name = name, look = look, x = x, y = y, prefab = prefab, ask = ask, thanks = thanks });
    }

    private class Npc { public string code, name, look, prefab, ask, thanks; public float x, y; }

    // autotile blocks in atlas cell coords (col, row-from-top): 3x3 edge block + 2x2 inner corners
    private static readonly Dictionary<string, (int bx, int by, int ix, int iy, int order)> Materials =
        new Dictionary<string, (int, int, int, int, int)>
        {
            { "dirt",   (17, 24, 17, 22, -19) },
            { "sand",   (23, 24, 23, 22, -18) },
            { "cobble", (26, 24, 26, 22, -17) },
            { "plaza",  (29, 24, 29, 22, -16) },
            { "water",  (23, 44, 23, 42, -15) },
        };
    private static readonly Vector2Int GrassCell = new Vector2Int(72, 29);   // flat grass, same green as the path rims

    // â”€â”€ menu â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Safe build: only creates town scenes that don't exist yet, so hand edits are never
    /// overwritten. Always refreshes Game_Scene_Town (stage data, progress UI) and the paint
    /// tiles / prefabs.
    /// </summary>
    [MenuItem("SUNOD/Towns/Build Missing Towns (safe)")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build(false);
    }

    [MenuItem("SUNOD/Towns/Rebuild All Towns (overwrites hand edits!)")]
    public static void RebuildAll()
    {
        if (!EditorUtility.DisplayDialog("Rebuild all towns?",
                "This regenerates Town_Elementary, Town_HighSchool and Town_College from the layouts in vc_TownBuilder.cs.\n\n" +
                "ANY CHANGES YOU MADE TO THOSE SCENES BY HAND WILL BE LOST.",
                "Rebuild and overwrite", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build(true);
    }

    /// <summary>For the Unity CLI: rebuilds without the confirmation dialog.</summary>
    public static void RebuildAllNoPrompt() => Build(true);

    private static void Build(bool overwrite)
    {
        ImportSprites();
        Directory.CreateDirectory(SceneDir);
        var towns = new[] { Elementary(), HighSchool(), College() };
        foreach (Town t in towns)
        {
            if (!overwrite && File.Exists($"{SceneDir}/{t.scene}.unity"))
            {
                Debug.Log($"[vc_TownBuilder] {t.scene} exists - left untouched (use Rebuild to overwrite).");
                continue;
            }
            BuildTown(t);
        }
        BuildGameScene(towns);
        AddToBuildSettings();
        MakePaintTiles();
        MakePropPrefabs();
        AssetDatabase.SaveAssets();
        Debug.Log("[vc_TownBuilder] Towns built.");
    }

    [MenuItem("SUNOD/Towns/Import Town Sprites")]
    public static void ImportSprites()
    {
        AssetDatabase.Refresh();
        foreach (string dir in new[] { SpriteDir, NpcDir })
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool npc = path.StartsWith(NpcDir);
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                bool changed = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single
                    || settings.spriteAlignment != (int)SpriteAlignment.BottomCenter || ti.filterMode != FilterMode.Point
                    || ti.textureCompression != TextureImporterCompression.Uncompressed
                    || !Mathf.Approximately(ti.spritePixelsPerUnit, npc ? 22f : 16f);
                if (!changed) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = npc ? 22f : 16f;   // NPCs match the player's on-screen size
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
            }
        }
    }

    // â”€â”€ scene building â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static Dictionary<Vector2Int, TileBase> _tiles;

    private static TileBase Cell(int col, int row)
    {
        if (_tiles == null)
        {
            _tiles = new Dictionary<Vector2Int, TileBase>();
            foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { TileDir }))
            {
                var t = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
                if (t == null || t.sprite == null || t.sprite.texture == null || t.sprite.texture.name != "original_atlas") continue;
                Rect r = t.sprite.rect;
                var key = new Vector2Int((int)r.x / 16, (t.sprite.texture.height - (int)r.y - (int)r.height) / 16);
                if (!_tiles.ContainsKey(key)) _tiles[key] = t;
            }
        }
        _tiles.TryGetValue(new Vector2Int(col, row), out TileBase tile);
        return tile;
    }

    private static void BuildTown(Town t)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rng = new System.Random(t.seed);
        var occ = new bool[t.w, t.h];

        var root = new GameObject("Town_" + t.id);
        var stage = root.AddComponent<vc_TownStage>();
        root.AddComponent<vc_TownAmbience>();   // wind, butterflies, leaves, cloud shadows, water glints
        stage.stageId = t.id;
        stage.stageTitle = t.title;
        stage.introLine = t.intro;
        stage.completeLine = t.complete;
        stage.nightDreams = t.nightDreams;
        stage.questsPerDay = 4;

        // ground + paths
        var grid = new GameObject("Grid", typeof(Grid));
        grid.transform.SetParent(root.transform, false);
        Tilemap ground = MakeTilemap(grid.transform, "Ground", -22, false);
        TileBase grass = Cell(GrassCell.x, GrassCell.y);
        for (int x = -Margin; x < t.w + Margin; x++)
            for (int y = -Margin; y < t.h + Margin; y++)
                ground.SetTile(new Vector3Int(x, y, 0), grass);

        var byMat = new Dictionary<string, HashSet<Vector2Int>>();
        foreach (var (mat, r) in t.paths) AddRect(byMat, mat, r);
        foreach (RectInt r in t.water) AddRect(byMat, "water", r);

        foreach (var kv in byMat)
        {
            var m = Materials[kv.Key];
            Tilemap tm = MakeTilemap(grid.transform, kv.Key, m.order, kv.Key == "water");
            foreach (Vector2Int c in kv.Value)
            {
                Vector2Int cell = AutotileCell(kv.Value, c, m.bx, m.by, m.ix, m.iy);
                tm.SetTile(new Vector3Int(c.x, c.y, 0), Cell(cell.x, cell.y));
                if (c.x >= 0 && c.y >= 0 && c.x < t.w && c.y < t.h) occ[c.x, c.y] = true;
            }
        }

        // ground variation: darker / lighter grass patches, under everything else
        var patchRoot = new GameObject("GrassPatches").transform;
        patchRoot.SetParent(root.transform, false);
        string[] patches = { "grass_dark1", "grass_dark2", "grass_dark3", "grass_light1", "grass_light2", "grass_light3" };
        for (int x = -Margin + 2; x < t.w + Margin - 2; x += 2)
            for (int y = -Margin + 2; y < t.h + Margin - 2; y += 2)
            {
                if (rng.NextDouble() > 0.24) continue;
                Vector2Int c = new Vector2Int(x, y);
                bool onPath = false;
                foreach (var set in byMat.Values) if (set.Contains(c)) { onPath = true; break; }
                if (onPath) continue;
                GameObject p = PlaceSprite(patchRoot, patches[rng.Next(patches.Length)], x + (float)rng.NextDouble(), y + (float)rng.NextDouble(), false);
                if (p != null) p.GetComponentInChildren<SpriteRenderer>().sortingOrder = -21;
            }

        // buildings / props
        var objRoot = new GameObject("Objects").transform;
        objRoot.SetParent(root.transform, false);
        foreach (var (s, x, y, collide) in t.objs)
        {
            GameObject go = PlaceSprite(objRoot, s, x, y, collide);
            if (go != null) MarkSprite(occ, s, x, y);
        }

        // the rebuildable home
        if (!string.IsNullOrEmpty(t.home))
        {
            PlaceHome(objRoot, t.home, t.homePos.x, t.homePos.y);
            MarkSprite(occ, t.home + "_home3", t.homePos.x, t.homePos.y);
        }

        // where today's "!" NPCs gather (near the plaza / spawn)
        var requestSpot = new GameObject("RequestSpot").transform;
        requestSpot.SetParent(root.transform, false);
        requestSpot.position = new Vector3(t.requestPos.x, t.requestPos.y, 0f);
        stage.requestSpot = requestSpot;

        // NPCs
        var npcRoot = new GameObject("NPCs").transform;
        npcRoot.SetParent(root.transform, false);
        Sprite mQuest = LoadSprite("marker_quest"), mDone = LoadSprite("marker_done"), mSoon = LoadSprite("marker_soon");
        foreach (Npc n in t.npcs)
        {
            PlaceNpc(npcRoot, t.id, n, mQuest, mDone, mSoon);
            MarkRect(occ, n.x - 0.8f, n.y - 0.5f, n.x + 0.8f, n.y + 1.8f);
        }
        MarkRect(occ, t.spawn.x - 1.5f, t.spawn.y - 1.5f, t.spawn.x + 1.5f, t.spawn.y + 1.5f);

        // tree clusters, then the border ring, then small decor
        var natureRoot = new GameObject("Nature").transform;
        natureRoot.SetParent(root.transform, false);
        foreach (var (area, count, kinds) in t.trees)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 40 && placed < count; attempt++)
            {
                string kind = kinds[rng.Next(kinds.Length)];
                float x = area.x + (float)rng.NextDouble() * area.width;
                float y = area.y + (float)rng.NextDouble() * area.height;
                x = Mathf.Round(x * 2f) / 2f; y = Mathf.Round(y * 2f) / 2f;
                if (!SpriteFits(occ, kind, x, y)) continue;
                PlaceSprite(natureRoot, kind, x, y, true);
                MarkSprite(occ, kind, x, y);
                placed++;
            }
        }
        BuildBorder(natureRoot, t, rng);

        // Ground flora: lots of tiny grass tufts, then flowers / mushrooms / clover, then the odd
        // rock. Goes on any grass cell that isn't a path, building, NPC or tree spot - including
        // the margin outside the town, so the forest floor isn't bare either.
        var floraRoot = new GameObject("Flora").transform;
        floraRoot.SetParent(root.transform, false);
        string[] tufts = { "tuft1", "tuft2", "tuft3", "tuft4", "tuft5", "tuft6", "tuft7", "tuft8",
                           "tuft_wide1", "tuft_wide2", "tuft_wide3", "tuft_wide4" };
        string[] rocks = { "rock1", "rock2", "rock3", "rock_moss1", "rock_moss2", "pebble1", "pebble2", "pebble1" };
        for (int x = -Margin + 1; x < t.w + Margin - 1; x++)
            for (int y = -Margin + 1; y < t.h + Margin - 1; y++)
            {
                bool inTown = x >= 0 && y >= 0 && x < t.w && y < t.h;
                if (inTown && occ[x, y]) continue;
                Vector2Int c = new Vector2Int(x, y);
                bool onPath = false;
                foreach (var set in byMat.Values) if (set.Contains(c)) { onPath = true; break; }
                if (onPath) continue;

                if (rng.NextDouble() < 0.45)
                    PlaceSprite(floraRoot, tufts[rng.Next(tufts.Length)], x + 0.15f + (float)rng.NextDouble() * 0.7f, y + (float)rng.NextDouble() * 0.8f, false);

                double roll = rng.NextDouble();
                if (roll < t.decorDensity)
                {
                    PlaceSprite(floraRoot, t.decor[rng.Next(t.decor.Length)], x + 0.5f, y + 0.15f, false);
                    if (inTown) occ[x, y] = true;
                }
                else if (roll < t.decorDensity + 0.012)
                {
                    string rock = rocks[rng.Next(rocks.Length)];
                    // real rocks inside the town are solid (same as the rock prefabs); pebbles stay walk-over
                    PlaceSprite(floraRoot, rock, x + 0.5f, y + 0.15f, inTown && !rock.StartsWith("pebble"));
                    if (inTown) occ[x, y] = true;
                }
            }

        // bounds
        var bounds = new GameObject("Bounds").transform;
        bounds.SetParent(root.transform, false);
        AddWall(bounds, new Vector2(t.w / 2f, -0.5f), new Vector2(t.w + 2, 1));
        AddWall(bounds, new Vector2(t.w / 2f, t.h + 0.5f), new Vector2(t.w + 2, 1));
        AddWall(bounds, new Vector2(-0.5f, t.h / 2f), new Vector2(1, t.h + 2));
        AddWall(bounds, new Vector2(t.w + 0.5f, t.h / 2f), new Vector2(1, t.h + 2));

        var spawn = new GameObject("PlayerSpawn").transform;
        spawn.SetParent(root.transform, false);
        spawn.position = t.spawn;
        stage.playerSpawn = spawn;

        var questArea = new GameObject("QuestArea").transform;
        questArea.SetParent(root.transform, false);
        questArea.position = new Vector3(t.w / 2f, -80f, 0f);
        stage.questArea = questArea;

        // Backdrop around the quest area: quest rooms are 16x16 and were built with hallways
        // beside them, so without this the camera shows empty void next to the room.
        Vector2Int qa = new Vector2Int(Mathf.RoundToInt(questArea.position.x), Mathf.RoundToInt(questArea.position.y));
        for (int x = -22; x < 22; x++)
            for (int y = -16; y < 16; y++)
                ground.SetTile(new Vector3Int(qa.x + x, qa.y + y, 0), grass);
        var qaNature = new GameObject("QuestAreaBackdrop").transform;
        qaNature.SetParent(root.transform, false);
        string[] qaTufts = { "tuft1", "tuft3", "tuft5", "tuft_wide2", "grass_dark2", "clover", "flower_white" };
        for (int ring = 0; ring < 4; ring++)
        {
            float r = 9.5f + ring * 2.4f;
            for (float x = -r; x <= r; x += 2.6f)
            {
                PlaceSprite(qaNature, Pick(t, rng), qa.x + x, qa.y - r - 1.5f, false);
                PlaceSprite(qaNature, Pick(t, rng), qa.x + x, qa.y + r - 1f, false);
            }
            for (float y = -r + 1f; y <= r - 1f; y += 2.6f)
            {
                PlaceSprite(qaNature, Pick(t, rng), qa.x - r - 0.5f, qa.y + y, false);
                PlaceSprite(qaNature, Pick(t, rng), qa.x + r + 0.5f, qa.y + y, false);
            }
        }
        for (int i = 0; i < 70; i++)
        {
            float x = (float)rng.NextDouble() * 40f - 20f, y = (float)rng.NextDouble() * 28f - 14f;
            if (Mathf.Abs(x) < 9f && Mathf.Abs(y) < 9f) continue;   // keep the room footprint clear
            GameObject d = PlaceSprite(qaNature, qaTufts[rng.Next(qaTufts.Length)], qa.x + x, qa.y + y, false);
            if (d != null) d.GetComponentInChildren<SpriteRenderer>().sortingOrder = -21;
        }

        string path = $"{SceneDir}/{t.scene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[vc_TownBuilder] {path}: {t.npcs.Count} NPCs, {t.npcs.FindAll(n => n.prefab != null).Count} with built quests.");
    }

    private static void AddRect(Dictionary<string, HashSet<Vector2Int>> byMat, string mat, RectInt r)
    {
        if (!byMat.TryGetValue(mat, out var set)) byMat[mat] = set = new HashSet<Vector2Int>();
        for (int x = r.xMin; x < r.xMax; x++)
            for (int y = r.yMin; y < r.yMax; y++)
                set.Add(new Vector2Int(x, y));
    }

    // world y goes up; atlas rows go down - "north" neighbour = y + 1 = top row of the block
    private static Vector2Int AutotileCell(HashSet<Vector2Int> set, Vector2Int c, int bx, int by, int ix, int iy)
    {
        bool n = set.Contains(c + Vector2Int.up), s = set.Contains(c + Vector2Int.down);
        bool w = set.Contains(c + Vector2Int.left), e = set.Contains(c + Vector2Int.right);
        int col = !w ? 0 : (!e ? 2 : 1);
        int row = !n ? 0 : (!s ? 2 : 1);
        if (col == 1 && row == 1)
        {
            if (!set.Contains(c + new Vector2Int(-1, 1))) return new Vector2Int(ix, iy);
            if (!set.Contains(c + new Vector2Int(1, 1))) return new Vector2Int(ix + 1, iy);
            if (!set.Contains(c + new Vector2Int(-1, -1))) return new Vector2Int(ix, iy + 1);
            if (!set.Contains(c + new Vector2Int(1, -1))) return new Vector2Int(ix + 1, iy + 1);
        }
        return new Vector2Int(bx + col, by + row);
    }

    private static Tilemap MakeTilemap(Transform parent, string name, int order, bool solid)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<TilemapRenderer>();
        r.sortingLayerName = "Default";
        r.sortingOrder = order;
        if (solid) go.AddComponent<TilemapCollider2D>();
        return go.GetComponent<Tilemap>();
    }

    private static Sprite LoadSprite(string name)
        => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

    private static (Sprite baseS, Sprite topS) Pieces(string name)
        => (LoadSprite(name + "_base"), LoadSprite(name + "_top"));

    private static GameObject PlaceSprite(Transform parent, string name, float x, float y, bool collide)
    {
        var (baseS, topS) = Pieces(name);
        if (baseS == null) { Debug.LogWarning($"[vc_TownBuilder] Missing sprite {name}"); return null; }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);

        var b = new GameObject("Base", typeof(SpriteRenderer));
        b.transform.SetParent(go.transform, false);
        var br = b.GetComponent<SpriteRenderer>();
        br.sprite = baseS;
        br.sortingLayerName = "Default";
        br.sortingOrder = collide ? 10 : 2;

        float baseH = baseS.rect.height / 16f;
        if (topS != null)
        {
            var tp = new GameObject("Top", typeof(SpriteRenderer));
            tp.transform.SetParent(go.transform, false);
            tp.transform.localPosition = new Vector3(0f, baseH, 0f);
            var tr = tp.GetComponent<SpriteRenderer>();
            tr.sprite = topS;
            tr.sortingLayerName = "Vegetation";   // drawn over the player -> you can walk behind it
            tr.sortingOrder = 0;
        }

        if (collide)
        {
            float w = baseS.rect.width / 16f;
            bool tree = name.StartsWith("tree_");
            var col = go.AddComponent<BoxCollider2D>();
            col.size = tree ? new Vector2(Mathf.Min(0.9f, w * 0.35f), 0.45f) : new Vector2(w * 0.92f, Mathf.Max(0.4f, baseH * 0.85f));
            col.offset = new Vector2(0f, col.size.y / 2f);
        }
        return go;
    }

    private static Vector2 SpriteSize(string name)
    {
        var (baseS, topS) = Pieces(name);
        if (baseS == null) return Vector2.one;
        float h = baseS.rect.height + (topS != null ? topS.rect.height : 0f);
        return new Vector2(baseS.rect.width / 16f, h / 16f);
    }

    private static void MarkSprite(bool[,] occ, string name, float x, float y)
    {
        Vector2 s = SpriteSize(name);
        MarkRect(occ, x - s.x / 2f - 0.3f, y - 0.6f, x + s.x / 2f + 0.3f, y + s.y);
    }

    private static bool SpriteFits(bool[,] occ, string name, float x, float y)
    {
        Vector2 s = SpriteSize(name);
        int x0 = Mathf.FloorToInt(x - s.x / 2f - 0.3f), x1 = Mathf.CeilToInt(x + s.x / 2f + 0.3f);
        int y0 = Mathf.FloorToInt(y - 0.6f), y1 = Mathf.CeilToInt(y + s.y);
        for (int cx = x0; cx < x1; cx++)
            for (int cy = y0; cy < y1; cy++)
            {
                if (cx < 1 || cy < 1 || cx >= occ.GetLength(0) - 1 || cy >= occ.GetLength(1) - 2) return false;
                if (occ[cx, cy]) return false;
            }
        return true;
    }

    private static void MarkRect(bool[,] occ, float x0, float y0, float x1, float y1)
    {
        for (int cx = Mathf.FloorToInt(x0); cx < Mathf.CeilToInt(x1); cx++)
            for (int cy = Mathf.FloorToInt(y0); cy < Mathf.CeilToInt(y1); cy++)
                if (cx >= 0 && cy >= 0 && cx < occ.GetLength(0) && cy < occ.GetLength(1)) occ[cx, cy] = true;
    }

    private static void BuildBorder(Transform parent, Town t, System.Random rng)
    {
        // A thick ring of trees: the inner row marks the playable edge, the outer rows
        // fill the margin so the camera only ever sees forest past the town.
        for (int ring = 0; ring < 4; ring++)
        {
            float step = 2.6f;
            float inset = -0.4f - ring * 2.4f;
            for (float x = inset; x <= t.w - inset; x += step)
            {
                PlaceSprite(parent, Pick(t, rng), x + Jit(rng), inset - 0.2f + Jit(rng), ring == 0);
                PlaceSprite(parent, Pick(t, rng), x + Jit(rng), t.h - inset - 2.4f + Jit(rng), ring == 0);
            }
            for (float y = inset + step; y <= t.h - inset - step; y += step)
            {
                PlaceSprite(parent, Pick(t, rng), inset + Jit(rng), y + Jit(rng), ring == 0);
                PlaceSprite(parent, Pick(t, rng), t.w - inset + Jit(rng), y + Jit(rng), ring == 0);
            }
        }
    }

    private static string Pick(Town t, System.Random rng) => t.borderTrees[rng.Next(t.borderTrees.Length)];
    private static float Jit(System.Random rng) => Mathf.Round(((float)rng.NextDouble() - 0.5f) * 1.2f * 2f) / 2f;

    private static void AddWall(Transform parent, Vector2 center, Vector2 size)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    private static void PlaceNpc(Transform parent, string townId, Npc n, Sprite mQuest, Sprite mDone, Sprite mSoon)
    {
        var go = new GameObject($"NPC_{n.code}_{n.name}");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(n.x, n.y, 0f);

        var frames = new Sprite[6];
        for (int f = 0; f < 6; f++)
            frames[f] = AssetDatabase.LoadAssetAtPath<Sprite>($"{NpcDir}/npc_{n.look}_f{f}.png");

        var body = new GameObject("Body", typeof(SpriteRenderer));
        body.transform.SetParent(go.transform, false);
        var br = body.GetComponent<SpriteRenderer>();
        br.sprite = frames[0];
        br.sortingLayerName = "Default";
        br.sortingOrder = 40;

        var marker = new GameObject("Marker", typeof(SpriteRenderer));
        marker.transform.SetParent(go.transform, false);
        marker.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        var mr = marker.GetComponent<SpriteRenderer>();
        mr.sortingLayerName = "Decor";
        mr.sortingOrder = 10;

        var solid = go.AddComponent<BoxCollider2D>();
        solid.size = new Vector2(0.45f, 0.3f);
        solid.offset = new Vector2(0f, 0.15f);
        var talk = go.AddComponent<CircleCollider2D>();
        talk.isTrigger = true;
        talk.radius = 0.85f;
        talk.offset = new Vector2(0f, 0.3f);

        var npc = go.AddComponent<vc_TownNPC>();
        npc.npcId = $"{townId}_{n.code}";
        npc.npcName = n.name;
        npc.questCode = n.code;
        npc.askLine = n.ask;
        npc.thanksLine = n.thanks;
        npc.idleFrames = frames;
        npc.body = br;
        npc.marker = mr;
        npc.markerQuest = mQuest;
        npc.markerDone = mDone;
        npc.markerSoon = mSoon;
        if (!string.IsNullOrEmpty(n.prefab))
        {
            npc.questPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomDir}/{n.prefab}.prefab");
            if (npc.questPrefab == null) Debug.LogWarning($"[vc_TownBuilder] Quest prefab not found: {n.prefab}");
        }
        mr.sprite = npc.questPrefab == null ? mSoon : mQuest;
    }

    private static void PlaceHome(Transform parent, string house, float x, float y)
    {
        GameObject go = PlaceSprite(parent, house + "_home3", x, y, true);
        if (go == null) return;
        go.name = "Home";
        var home = go.AddComponent<vc_TownHome>();
        home.baseRenderer = go.transform.Find("Base").GetComponent<SpriteRenderer>();
        Transform top = go.transform.Find("Top");
        home.topRenderer = top != null ? top.GetComponent<SpriteRenderer>() : null;
        home.baseStages = new Sprite[4];
        home.topStages = new Sprite[4];
        for (int k = 0; k < 4; k++)
        {
            home.baseStages[k] = LoadSprite($"{house}_home{k}_base");
            home.topStages[k] = LoadSprite($"{house}_home{k}_top");
        }
        home.baseRenderer.sprite = home.baseStages[0];
        if (home.topRenderer != null) home.topRenderer.sprite = home.topStages[0];

        var debris = new List<GameObject>();
        float[] dx = { -1.6f, 0.4f, 1.9f };
        for (int i = 0; i < 3; i++)
        {
            GameObject d = PlaceSprite(go.transform, "debris" + i, x + dx[i], y - 0.55f - 0.25f * (i % 2), false);
            if (d != null) debris.Add(d);
        }
        home.debris = debris.ToArray();
        PlaceSprite(parent, "sign_home", x + 3.4f, y + 0.1f, true);
    }

    private static void BuildGameScene(Town[] towns)
    {
        string path = $"{SceneDir}/Game_Scene_Town.unity";
        if (!File.Exists(path))
            AssetDatabase.CopyAsset("Assets/Scenes/Game_Scene.unity", path);

        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        UIDocument anyDoc = null;
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            // The corridor's floor system stays in Game_Scene; the town flow replaces it here.
            foreach (var fl in r.GetComponentsInChildren<SunodGame.Core.vc_FloorLoader>(true)) fl.enabled = false;
            foreach (var fi in r.GetComponentsInChildren<SunodGame.Core.vc_FloorInitializer>(true)) fi.enabled = false;
            if (anyDoc == null) anyDoc = r.GetComponentInChildren<UIDocument>(true);
        }

        vc_TownFlow flow = null;
        vc_RunProgressUI progress = null;
        foreach (GameObject r in scene.GetRootGameObjects())
        {
            if (flow == null) flow = r.GetComponentInChildren<vc_TownFlow>(true);
            if (progress == null) progress = r.GetComponentInChildren<vc_RunProgressUI>(true);
        }
        if (flow == null) flow = new GameObject("TownFlow").AddComponent<vc_TownFlow>();

        // stage data for the progress bar + Continue
        var stages = new List<vc_TownFlow.StageInfo>();
        foreach (Town t in towns)
        {
            var codes = new List<string>();
            var ids = new List<string>();
            foreach (Npc n in t.npcs)
            {
                if (string.IsNullOrEmpty(n.prefab)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomDir}/{n.prefab}.prefab");
                var room = prefab != null ? prefab.GetComponentInChildren<vc_QuestRoom>(true) : null;
                if (room == null) continue;
                codes.Add(n.code);
                ids.Add(room.QuestId);
            }
            stages.Add(new vc_TownFlow.StageInfo
            {
                scene = t.scene, title = t.title, codes = codes.ToArray(), questIds = ids.ToArray(), dreamAfter = t.dreamAfter,
            });
        }
        var so = new SerializedObject(flow);
        var prop = so.FindProperty("stages");
        prop.arraySize = stages.Count;
        for (int i = 0; i < stages.Count; i++)
        {
            var e = prop.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("scene").stringValue = stages[i].scene;
            e.FindPropertyRelative("title").stringValue = stages[i].title;
            SetStrings(e.FindPropertyRelative("codes"), stages[i].codes);
            SetStrings(e.FindPropertyRelative("questIds"), stages[i].questIds);
            SetStrings(e.FindPropertyRelative("dreamAfter"), stages[i].dreamAfter);
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // progress banner / day summary / dream overlay
        if (progress == null)
        {
            var go = new GameObject("UIToolkit_RunProgress", typeof(UIDocument));
            progress = go.AddComponent<vc_RunProgressUI>();
        }
        var doc = progress.GetComponent<UIDocument>();
        if (anyDoc != null) doc.panelSettings = anyDoc.panelSettings;
        doc.sortingOrder = 5;
        var pso = new SerializedObject(progress);
        pso.FindProperty("styleSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Art/UI/RunProgress.uss");
        string[] bgs =
        {
            "Assets/Art/Cutscenes/Cut04_HouseWrecked/Cutscene04_Background_px.png",
            "Assets/Art/Cutscenes/Cut05_MysteriousMan/Cutscene05_Background_px.png",
            "Assets/Art/Cutscenes/Cut03_HouseNight/Cutscene03_Background_px.png",
            "Assets/Art/Cutscenes/Cut06_Symbols/Cutscene06_Background_px.png",
        };
        var bgProp = pso.FindProperty("dreamBackgrounds");
        bgProp.arraySize = bgs.Length;
        for (int i = 0; i < bgs.Length; i++)
            bgProp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(bgs[i]);
        pso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetStrings(SerializedProperty arr, string[] values)
    {
        values = values ?? new string[0];
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    // â”€â”€ hand-editing support: rule tiles, palette, prop prefabs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private const string PaintDir = "Assets/Art/Town/Paint";

    /// <summary>
    /// One RuleTile per ground material (dirt, sand, cobble, plaza, water): paint with it and
    /// the grass edges and corners fill themselves, the same way the builder autotiles.
    /// Also makes a "Town" tile palette holding them plus the plain grass tile.
    /// </summary>
    [MenuItem("SUNOD/Towns/Make Paint Tiles + Palette")]
    public static void MakePaintTiles()
    {
        Directory.CreateDirectory(PaintDir);
        var tiles = new List<TileBase>();
        foreach (var kv in Materials)
        {
            var m = kv.Value;
            string path = $"{PaintDir}/{kv.Key}_auto.asset";
            var rule = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
            if (rule == null)
            {
                rule = ScriptableObject.CreateInstance<RuleTile>();
                AssetDatabase.CreateAsset(rule, path);
            }
            rule.m_DefaultSprite = SpriteOf(m.bx + 1, m.by + 1);
            rule.m_DefaultColliderType = kv.Key == "water" ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
            rule.m_TilingRules = new List<RuleTile.TilingRule>
            {
                // outer edges/corners: "not" = no same tile on that side
                Rule(SpriteOf(m.bx, m.by),         ("N", false), ("W", false), ("E", true), ("S", true)),
                Rule(SpriteOf(m.bx + 2, m.by),     ("N", false), ("E", false), ("W", true), ("S", true)),
                Rule(SpriteOf(m.bx, m.by + 2),     ("S", false), ("W", false), ("E", true), ("N", true)),
                Rule(SpriteOf(m.bx + 2, m.by + 2), ("S", false), ("E", false), ("W", true), ("N", true)),
                Rule(SpriteOf(m.bx + 1, m.by),     ("N", false), ("W", true), ("E", true), ("S", true)),
                Rule(SpriteOf(m.bx + 1, m.by + 2), ("S", false), ("W", true), ("E", true), ("N", true)),
                Rule(SpriteOf(m.bx, m.by + 1),     ("W", false), ("N", true), ("S", true), ("E", true)),
                Rule(SpriteOf(m.bx + 2, m.by + 1), ("E", false), ("N", true), ("S", true), ("W", true)),
                // inner corners: all four sides present, one diagonal missing
                Rule(SpriteOf(m.ix, m.iy),         ("N", true), ("S", true), ("E", true), ("W", true), ("NW", false)),
                Rule(SpriteOf(m.ix + 1, m.iy),     ("N", true), ("S", true), ("E", true), ("W", true), ("NE", false)),
                Rule(SpriteOf(m.ix, m.iy + 1),     ("N", true), ("S", true), ("E", true), ("W", true), ("SW", false)),
                Rule(SpriteOf(m.ix + 1, m.iy + 1), ("N", true), ("S", true), ("E", true), ("W", true), ("SE", false)),
            };
            foreach (var r in rule.m_TilingRules)
                r.m_ColliderType = rule.m_DefaultColliderType;
            EditorUtility.SetDirty(rule);
            tiles.Add(rule);
        }
        tiles.Add(Cell(GrassCell.x, GrassCell.y));
        AssetDatabase.SaveAssets();

        string palettePath = $"{PaintDir}/Town Palette.prefab";
        if (!File.Exists(palettePath))
        {
            GameObject pal = UnityEditor.Tilemaps.GridPaletteUtility.CreateNewPalette(
                PaintDir, "Town Palette", GridLayout.CellLayout.Rectangle,
                UnityEditor.GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
            palettePath = AssetDatabase.GetAssetPath(pal);
        }
        GameObject contents = PrefabUtility.LoadPrefabContents(palettePath);
        var tm = contents.GetComponentInChildren<Tilemap>();
        tm.ClearAllTiles();
        for (int i = 0; i < tiles.Count; i++)
            tm.SetTile(new Vector3Int(i, 0, 0), tiles[i]);
        PrefabUtility.SaveAsPrefabAsset(contents, palettePath);
        PrefabUtility.UnloadPrefabContents(contents);
    }

    private static Sprite SpriteOf(int col, int row) => (Cell(col, row) as Tile)?.sprite;

    private static readonly Dictionary<string, Vector3Int> Dir = new Dictionary<string, Vector3Int>
    {
        { "N", new Vector3Int(0, 1, 0) }, { "S", new Vector3Int(0, -1, 0) },
        { "E", new Vector3Int(1, 0, 0) }, { "W", new Vector3Int(-1, 0, 0) },
        { "NE", new Vector3Int(1, 1, 0) }, { "NW", new Vector3Int(-1, 1, 0) },
        { "SE", new Vector3Int(1, -1, 0) }, { "SW", new Vector3Int(-1, -1, 0) },
    };

    private static RuleTile.TilingRule Rule(Sprite sprite, params (string dir, bool same)[] conditions)
    {
        var rule = new RuleTile.TilingRule { m_Sprites = new[] { sprite } };
        var positions = new List<Vector3Int>();
        var neighbors = new List<int>();
        foreach (var (dir, same) in conditions)
        {
            positions.Add(Dir[dir]);
            neighbors.Add(same ? RuleTile.TilingRuleOutput.Neighbor.This : RuleTile.TilingRuleOutput.Neighbor.NotThis);
        }
        rule.m_NeighborPositions = positions;
        rule.m_Neighbors = neighbors;
        return rule;
    }

    /// <summary>
    /// One prefab per building / tree / prop (base + top + collider, ready to drag into a
    /// town scene) in Assets/Art/Town/Prefabs.
    /// </summary>
    [MenuItem("SUNOD/Towns/Make Prop Prefabs")]
    public static void MakePropPrefabs()
    {
        string dir = "Assets/Art/Town/Prefabs";
        Directory.CreateDirectory(dir);
        var names = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpriteDir }))
        {
            string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (!file.EndsWith("_base")) continue;
            string name = file.Substring(0, file.Length - 5);
            if (name.Contains("_home")) continue;
            names.Add(name);
        }
        foreach (string name in names)
        {
            bool small = name.StartsWith("tuft") || name.StartsWith("flower") || name.StartsWith("tiny_") || name.StartsWith("grass_")
                || name.StartsWith("mush") || name.StartsWith("bloom") || name.StartsWith("clover") || name.StartsWith("pebble")
                || name.StartsWith("debris") || name == "bush";
            GameObject go = PlaceSprite(null, name, 0f, 0f, !small);
            if (go == null) continue;
            PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{name}.prefab");
            Object.DestroyImmediate(go);
        }
    }

    private static void AddToBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (string name in new[] { "Game_Scene_Town", "Town_Elementary", "Town_HighSchool", "Town_College" })
        {
            string p = $"{SceneDir}/{name}.unity";
            if (list.Exists(s => s.path == p)) continue;
            list.Add(new EditorBuildSettingsScene(p, true));
        }
        EditorBuildSettings.scenes = list.ToArray();
    }

    // â”€â”€ layouts â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static Town Elementary()
    {
        var t = new Town
        {
            scene = "Town_Elementary", id = "elementary", title = "Elementary", w = 48, h = 36, seed = 11,
            spawn = new Vector2(24f, 6.5f),
            intro = "You're in grade school now. People around the neighborhood need a hand - look for anyone with a ! above their head.",
            complete = "You helped the whole neighborhood! The years go by, and high school is next.",
            borderTrees = new[] { "tree_green", "tree_round", "tree_pine" },
        };
        // streets (dirt), plaza, schoolyard, pond
        t.Path("dirt", 3, 15, 42, 2);
        t.Path("dirt", 23, 3, 2, 23);
        t.Path("dirt", 8, 25, 32, 2);
        t.Path("dirt", 10, 5, 2, 10);
        t.Path("dirt", 10, 17, 2, 8);
        t.Path("dirt", 36, 5, 2, 10);
        t.Path("plaza", 20, 12, 8, 8);
        t.Path("sand", 13, 28, 22, 6);
        t.Water(39, 4, 6, 5);

        // school
        t.Obj("hall_grand", 24f, 29f);
        t.Obj("hall_small2", 17f, 29.2f);
        t.Obj("sign_school", 19.5f, 28.1f);
        t.Obj("flagpole", 28.6f, 28.4f);
        t.Obj("swing", 30.8f, 30.6f);
        t.Obj("slide", 33.2f, 28.5f);
        t.Obj("cabin", 37.6f, 28f);
        t.Obj("sign_canteen", 40.8f, 28f);
        // homes (west)
        t.Obj("house_red", 6f, 19f);
        t.Obj("house_peach", 16f, 19f);
        t.Obj("house_slate", 6f, 9.4f);
        t.Obj("house_red_window", 16f, 9.4f);
        t.home = "house_peach2"; t.homePos = new Vector2(16.4f, 3.2f);
        t.requestPos = new Vector2(24f, 15f);
        t.nightDreams = new[]
        {
            "You dream of your house again - the roof torn open, the street empty. It feels far away... for now.",
            "In the dream, the shadowy figure points at the sky. \"Learn,\" it says. \"Learn everything you can.\"",
            "Tonight the dream is quiet. Just the sound of wind, and a clock ticking somewhere.",
        };
        t.dreamAfter = new[]
        {
            "The years pass. Grade school ends, and you start to grow up.",
            "The dreams don't stop. If anything, they're clearer now: cracked walls, flooded streets, people running.",
            "Whatever is coming, you're not a little kid anymore. High school is next.",
        };
        // fair + toy store (east of plaza)
        t.Obj("stall_orange", 30.6f, 17.6f);
        t.Obj("stall_green", 34.2f, 17.6f);
        t.Obj("sign_fair", 37.2f, 17.7f);
        t.Obj("shop_reddoor", 43f, 17.6f);
        t.Obj("sign_toys", 39.9f, 17.7f);
        // plaza
        t.Obj("lamp_white", 19.5f, 12.2f); t.Obj("lamp_white", 28.5f, 12.2f);
        t.Obj("lamp_white", 19.5f, 19.6f); t.Obj("lamp_white", 28.5f, 19.6f);
        t.Obj("bench", 26.5f, 18.1f);
        // park + garden (south-east)
        t.Obj("sign_park", 38.6f, 12.4f);
        t.Obj("bench", 41.5f, 10.4f);
        t.Obj("garden_bed", 31f, 10.4f);
        t.Obj("garden_bed", 33.6f, 10.4f);
        t.Obj("sign_garden", 28.6f, 10.4f);
        t.Obj("lamp_blue", 12.8f, 24.2f); t.Obj("lamp_blue", 34.8f, 24.2f);

        t.Trees(26, 3, 9, 6, 4, "tree_round", "tree_green");
        t.Trees(2, 26, 6, 9, 5, "tree_green", "tree_pine");
        t.Trees(40, 27, 7, 8, 4, "tree_round", "tree_pink");
        t.Trees(28, 20, 18, 4, 4, "tree_round");
        t.Trees(2, 11, 7, 4, 2, "tree_green");

        const string R = "Room_";
        t.N("R6", "Aling Rosa", "amelia_0", 6f, 18.3f, R + "House_BrokenPumpQuestNew",
            "Oh no, my faucet is leaking again! Can you help me fix it?", "No more drip-drip-drip. Thank you!");
        t.N("R2", "Mang Ben", "bob_1", 16f, 18.3f, R + "River_RiverCrossing",
            "The river is too deep and my old bridge is broken. Can you help me get across?", "Now I can get to the other side again!");
        t.N("R8", "Lolo Ador", "bob_3", 6f, 8.7f, R + "House_CreakyFloorboards",
            "My floor creaks every time I take a step. Can you fix the loose boards?", "So quiet now. Salamat, apo!");
        t.N("S4", "Tita Lorna", "amelia_2", 16f, 8.7f, R + "Forest_MorningExercise",
            "Can you show me some morning stretches? I always forget how!", "I feel so awake now!");
        t.N("I2", "Kuya Migs", "adam_1", 30f, 6.6f, R + "Forest_WatchTheRabbits",
            "There are rabbits in the meadow! Want to watch what they do?", "You noticed so much about them!");
        t.N("I3", "Teacher Joy", "amelia_1", 33.6f, 9.6f, R + "Forest_SeedlingStudy",
            "Our class is growing seedlings. Can you help me check how they're doing?", "Great notes, little scientist!");
        t.N("I6", "Nico", "adam_2", 38.4f, 6.4f, R + "River_FishCount",
            "How many fish live in this pond? Let's count them!", "Wow, that's a lot of fish!");
        t.N("A6", "Paolo", "alex_1", 21.9f, 16.6f, R + "School_ParadeDrum",
            "The parade needs a drummer! Can you play the drum with us?", "You kept the beat perfectly!");
        t.N("E2", "Jun", "alex_2", 30.6f, 16.9f, R + "School_SnackStall",
            "Help me sell snacks at our school fair stall?", "We sold out! Thanks, partner!");
        t.N("E8", "Mr. Tan", "bob_2", 43f, 16.9f, R + "House_ToyStore",
            "My toy store is so busy today! Can you help me run it?", "Best helper in town!");
        t.N("A3", "Ms. Dela Cruz", "amelia_3", 22f, 28.2f, R + "School_FairPoster",
            "We need a poster for the school fair. Will you make one?", "That poster is beautiful!");
        t.N("C2", "Principal Reyes", "bob_0", 26f, 28.2f, R + "House_MissingKeyQuest",
            "I can't find the key to the supply room. Can you help me look?", "Everything's back in order. Thank you!");
        t.N("A4", "Bea", "alex_3", 14.3f, 30.2f, R + "School_ClassSong",
            "Our class needs a song for the program. Will you help me write it?", "Everyone's humming our song!");
        t.N("S6", "Ate Mia", "amelia_0", 32f, 29.4f, R + "School_LostFriendQuest",
            "Can you help me look after the younger kids at the playground?", "They had so much fun with you!");
        t.N("S7", "Toto", "adam_3", 9.6f, 27.8f, R + "School_StoryTime",
            "Can you help me read this storybook? Some words are hard.", "I read the whole book!");
        t.N("C6", "Manang Fe", "amelia_2", 37.6f, 27.3f, R + "School_CanteenChange",
            "The canteen is so busy! Can you help me count the change?", "Not a single peso missing!");
        return t;
    }

    private static Town HighSchool()
    {
        var t = new Town
        {
            scene = "Town_HighSchool", id = "highschool", title = "High School", w = 50, h = 38, seed = 22,
            spawn = new Vector2(24.9f, 12f),
            intro = "You're in high school now, and the town is busier than ever. Look for people with a ! - they could use your help.",
            complete = "The whole town knows your name now. The dreams are getting sharper... college is next.",
            borderTrees = new[] { "tree_green", "tree_pine", "tree_round", "tree_pink" },
        };
        t.Water(2, 2, 46, 4);
        t.Path("sand", 4, 6, 42, 2);
        t.Path("cobble", 3, 17, 44, 2);
        t.Path("cobble", 24, 8, 2, 27);
        t.Path("cobble", 6, 27, 38, 2);
        t.Path("cobble", 10, 8, 2, 9);
        t.Path("cobble", 38, 8, 2, 9);
        t.Path("plaza", 19, 14, 12, 8);

        t.Obj("hall_grand", 12f, 29.4f);
        t.Obj("sign_hs", 7.6f, 29.4f);
        t.Obj("flagpole", 15.8f, 29.5f);
        t.Obj("hall_deck", 38f, 29.4f);
        t.Obj("sign_hall", 33.4f, 29.4f);
        t.Obj("sign_choir", 42.4f, 29.4f);
        t.Obj("house_red2", 14f, 19.6f);
        t.Obj("sign_radio", 10.4f, 19.7f);
        t.Obj("house_slate2", 35f, 19.6f);
        t.Obj("sign_barber", 31.8f, 23.5f);
        t.Obj("house_peach3", 43f, 19.6f);
        t.Obj("sign_thrift", 43f, 23.5f);
        t.Obj("tent_red", 15.5f, 10f);
        t.Obj("tent_green", 20.5f, 9.6f);
        t.Obj("stall_blue", 6.4f, 11.4f);
        t.Obj("sign_market", 6.4f, 15.4f);
        t.Obj("house_red_tall", 44f, 10.4f);
        t.home = "house_slate"; t.homePos = new Vector2(31f, 10.4f);
        t.requestPos = new Vector2(25f, 17f);
        t.nightDreams = new[]
        {
            "You dream of the town square, but the buildings are leaning, and the river is far too high.",
            "The shadowy figure is closer tonight. \"You're getting stronger,\" it says. \"Keep going.\"",
            "In the dream you see your friends, helping each other carry things to higher ground.",
        };
        t.dreamAfter = new[]
        {
            "High school is over. The town you grew up in feels smaller now.",
            "Every night the dream is the same, and every night it's a little closer.",
            "You leave for college. You can feel it: the day is almost here.",
        };
        t.Obj("dock", 25f, 5f, false);
        t.Obj("sign_river", 29f, 8.2f);
        t.Obj("lamp_red", 18.5f, 14f); t.Obj("lamp_red", 31.5f, 14f);
        t.Obj("lamp_red", 18.5f, 21.6f); t.Obj("lamp_red", 31.5f, 21.6f);
        t.Obj("bench_long", 22f, 20.6f); t.Obj("bench_long", 28f, 20.6f);
        t.Obj("plant_pot", 23f, 14.4f); t.Obj("plant_pot2", 27f, 14.4f);
        t.Obj("crates", 17.8f, 7.9f);

        t.Trees(2, 30, 4, 6, 3, "tree_green", "tree_pine");
        t.Trees(44, 24, 5, 12, 4, "tree_round", "tree_pink");
        t.Trees(26, 30, 6, 6, 3, "tree_pink", "tree_round");
        t.Trees(2, 19, 6, 6, 2, "tree_green");

        const string R = "Room_";
        t.N("R4", "Kuya Dan", "adam_0", 14f, 19f, R + "School_RadioRepair",
            "The radio club's old radio stopped working. Can you put it back together?", "We're back on the air!");
        t.N("A8", "Ate Carla", "amelia_1", 36.2f, 28.8f, R + "House_MuralQuestNew",
            "We need a set painted for the town play. Want to design it?", "The stage looks amazing!");
        t.N("A1", "Mr. Santos", "bob_0", 40.6f, 28.8f, R + "School_ChoirPractice",
            "The school choir needs someone to lead them today. Can you do it?", "What a performance!");
        t.N("S2", "Liza", "alex_2", 33f, 7.6f, R + "Forest_FallenSparrowQuest",
            "We're volunteering to clean up by the river. Want to join?", "The riverbank looks so much better!");
        t.N("I8", "Prof. Lim", "bob_1", 27.8f, 7.6f, R + "River_RiverbedMap",
            "Help me map the bottom of the river for our field trip?", "This map is perfect for the class!");
        t.N("C8", "Mang Tonyo", "bob_3", 20.4f, 7.6f, R + "River_DocksideLedger",
            "Boats keep coming in. Can you keep track of what we ship and receive?", "Every box accounted for!");
        t.N("R1", "Coach Ramon", "adam_3", 10.2f, 28.8f, R + "School_RoboticsParts",
            "Our robotics team needs every part checked before the contest. Can you test them?", "Not a single bad part!");
        t.N("I1", "Ms. Garcia", "amelia_3", 13.8f, 28.8f, R + "School_SkeletonModel",
            "Can you help me put the skeleton model together for biology class?", "You know your bones!");
        t.N("C4", "Mrs. Ong", "amelia_0", 17.4f, 28.8f, R + "School_StudentRecords",
            "The student records are all mixed up. Can you sort them?", "Everything's filed. Thank you!");
        t.N("R7", "Joy", "alex_0", 15.5f, 9.3f, R + "School_FundraiserKits",
            "We're putting together kits for the fundraiser. Can you help assemble them?", "We'll raise so much with these!");
        t.N("E1", "Marco", "adam_2", 20.5f, 8.9f, R + "School_StallPitch",
            "I want to pitch my food stall idea to the fair committee. Help me sell it?", "They said yes!");
        t.N("C5", "Aling Nena", "amelia_2", 6.4f, 10.7f, R + "River_HarvestTally",
            "Can you help me tally today's harvest and write down the numbers?", "Neat and exact. Salamat!");
        t.N("A2", "Direk Paulo", "alex_1", 25f, 20.6f, R + "School_OpeningNight",
            "Opening night is tonight! Can you help me direct the class play?", "Standing ovation!");
        t.N("S8", "Lola Ising", "amelia_1", 44f, 9.7f, R + "House_WalkLolaHome",
            "Could you walk me home and help me with my chores today?", "You're such a kind young one.");
        t.N("E4", "Mang Ramil", "bob_2", 35f, 18.9f, R + "House_Barbershop",
            "My barbershop has a long line today. Can you help me run it?", "Every customer left smiling!");
        t.N("E6", "Ate Joan", "alex_3", 43f, 18.9f, R + "House_ThriftShop",
            "Can you run a shift at the thrift shop while I'm out?", "Best sales day this month!");
        return t;
    }

    private static Town College()
    {
        var t = new Town
        {
            scene = "Town_College", id = "college", title = "College", w = 52, h = 40, seed = 33,
            spawn = new Vector2(26f, 12f),
            intro = "College, and the city is full of people who need you. The disaster is close now. Help everyone you can.",
            complete = "You're ready. Whatever comes next, everything you've learned will decide your future.",
            borderTrees = new[] { "tree_pine", "tree_round", "tree_green", "tree_palm" },
            decor = new[] { "flower_white", "flower_blue", "tiny_flower1", "tiny_flower3", "clover", "clover", "bush", "bloom_blue", "mush_brown" },
            decorDensity = 0.06f,
        };
        t.Water(30, 2, 20, 5);
        t.Path("sand", 28, 7, 22, 2);
        t.Path("plaza", 3, 19, 46, 3);
        t.Path("plaza", 25, 9, 3, 26);
        t.Path("plaza", 6, 29, 40, 2);
        t.Path("cobble", 18, 31, 18, 6);
        t.Path("plaza", 9, 8, 16, 2);

        t.Obj("hall_grand", 22f, 32.6f);
        t.Obj("sign_campus", 18.6f, 31.6f);
        t.Obj("hall_deck", 31.4f, 32.6f);
        t.Obj("sign_lab", 35.4f, 31.6f);
        t.Obj("hall_small", 12f, 32.4f);
        t.Obj("sign_paper", 8.4f, 31.8f);
        t.Obj("house_slate_tall", 10f, 22.6f);
        t.Obj("sign_hotel", 6.6f, 22.6f);
        t.Obj("house_red_tall", 17.4f, 22.6f);
        t.Obj("sign_bank", 21.4f, 22.6f);
        t.Obj("house_slate3", 32f, 22.6f);
        t.Obj("sign_office", 28.8f, 26.4f);
        t.Obj("house_peach", 40f, 22.6f);
        t.Obj("sign_cafe", 44.4f, 22.6f);
        t.Obj("hall_small2", 10f, 12.6f);
        t.Obj("sign_clinic", 6.2f, 12.6f);
        t.Obj("house_peach_tall", 18f, 12.6f);
        t.Obj("sign_realty", 18f, 17.4f);
        t.Obj("cabin", 5.8f, 4.4f);
        t.home = "house_red"; t.homePos = new Vector2(15.4f, 3.6f);
        t.requestPos = new Vector2(26f, 15f);
        t.nightDreams = new[]
        {
            "You dream of the city at night. The lights flicker, then go out, one street at a time.",
            "\"It's almost time,\" the shadowy figure whispers. \"Everything you've learned will matter.\"",
            "You dream of the port. The water is still. Too still.",
        };
        t.dreamAfter = new[]
        {
            "And then, just before you become an adult... it happens. The disaster strikes.",
            "But this time, you're ready. Every person you helped, every skill you learned, is part of who you are now.",
            "Let's see what path all of that has opened for you.",
        };
        t.Obj("cabin", 34f, 10f);
        t.Obj("sign_workshop", 30.6f, 10f);
        t.Obj("tent_blue", 44f, 11f);
        t.Obj("sign_studio", 44f, 15.6f);
        t.Obj("dock", 38f, 5.6f, false);
        t.Obj("sign_port", 42.6f, 9.6f);
        t.Obj("crates", 46.5f, 8.2f);
        foreach (float x in new[] { 4f, 13.5f, 22.5f, 30f, 37f, 46.5f }) t.Obj("lamp_white", x, 18.2f);
        foreach (float x in new[] { 13.6f, 23.6f, 36f }) t.Obj("lamp_white", x, 22.4f);   // kept clear of the shop signs
        t.Obj("plant_pot", 24f, 31.2f); t.Obj("plant_pot2", 29f, 31.2f);
        t.Obj("bench_long", 16f, 28.2f); t.Obj("bench_long", 36f, 28.2f);

        t.Trees(2, 27, 4, 9, 3, "tree_pine", "tree_round");
        t.Trees(38, 31, 10, 6, 4, "tree_round", "tree_pine");
        t.Trees(22, 3, 6, 4, 2, "tree_palm");
        t.Trees(44, 25, 6, 3, 2, "tree_round");

        const string R = "Room_";
        t.N("I5", "Dr. Cruz", "bob_1", 24.4f, 32f, R + "Forest_GlowingMushrooms",
            "Our research team found strange glowing mushrooms. Can you help us study them?", "This could be a big discovery!");
        t.N("S1", "Ms. Rivera", "amelia_3", 17f, 31.6f, R + "School_CareerFair",
            "Students keep asking me about careers. Can you help out at the career booth?", "You really helped them find a direction.");
        t.N("I7", "Ana", "alex_2", 29.4f, 32f, R + "School_BioLab",
            "I need help with the samples in the biology lab. Can you give me a hand?", "Perfect results. Great lab work!");
        t.N("A5", "Kevin", "adam_0", 12f, 31.7f, R + "School_CampusPaper",
            "The campus paper needs a story by tonight. Can you help me write it?", "Front page material!");
        t.N("E3", "Mr. Lee", "bob_0", 10f, 21.9f, R + "House_HotelLobby",
            "Guests are arriving all at once. Can you help me manage the hotel?", "Every guest checked in happy.");
        t.N("C7", "Ms. Uy", "amelia_0", 17.4f, 21.9f, R + "House_BankTeller",
            "The bank line is long. Can you handle a teller window for me?", "Every peso balanced. Thank you!");
        t.N("E5", "Ms. Ria", "amelia_1", 31f, 21.9f, R + "School_InternTeam",
            "My team at the internship needs a leader today. Can you run it?", "The team did great under you.");
        t.N("C1", "Mr. Go", "bob_3", 34f, 21.9f, R + "School_PaydayOffice",
            "It's payday. Can you help me prepare everyone's pay envelopes?", "Everyone got paid on time!");
        t.N("C3", "Jay", "adam_1", 40f, 21.9f, R + "School_NetCafe",
            "Can you help me make the customer bills at the net cafe?", "No billing mistakes today!");
        t.N("I4", "Dr. Santos", "bob_2", 10f, 11.9f, R + "House_CatMedicineQuest",
            "A sick cat was brought in. Can you help find the right treatment?", "She's going to be okay. Thank you.");
        t.N("E7", "Agent Mel", "alex_0", 18f, 11.9f, R + "House_OpenHouse",
            "I'm holding an open house today. Can you help me sell it?", "Sold! You're a natural.");
        t.N("S3", "Kuya Joel", "adam_3", 8.4f, 4f, R + "School_SupportCorner",
            "A friend is going through a hard time. Can you help them find support?", "Thank you for being there for them.");
        t.N("S5", "Aling Cora", "amelia_2", 16f, 2.9f, R + "House_FamilyTalk",
            "My family is having a hard day. Could you help us work things out?", "We feel so much better. Salamat.");
        t.N("R5", "Mang Erning", "bob_1", 34f, 9.3f, R + "House_WorkshopGrinder",
            "The water pump's motor bracket is too rough to fit. Can you grind it down on the workshop machine?", "A perfect fit!");
        t.N("A7", "Direk Bong", "alex_1", 44f, 10.3f, R + "Forest_StuntSet",
            "Our student film needs a stunt double. Up for it?", "That stunt was incredible!");
        t.N("R3", "Engr. Paz", "adam_2", 35.6f, 7.4f, R + "River_PortGenerator",
            "The port generator broke down. Can you help me get it running?", "Power's back on. Great work!");
        return t;
    }
}
