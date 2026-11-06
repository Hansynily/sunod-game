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

        // ── planned layouts (2026-11-07): curved streets, round plazas, ponds, a coast ──
        public readonly List<(string mat, Vector2[] pts, float width, bool bridge)> roads = new List<(string, Vector2[], float, bool)>();
        public readonly List<(string mat, Vector2 c, Vector2 r, float wobble)> blobs = new List<(string, Vector2, Vector2, float)>();
        public readonly List<(string name, Vector2 p)> spots = new List<(string, Vector2)>();
        public float seaY = float.NaN, seaAmp = 1f, sandWidth = 4f;   // a coast along the bottom edge

        /// <summary>A street through these points (x, y pairs), smoothed into curves, 'width' tiles wide.</summary>
        public void Road(string mat, float width, params float[] xy) => roads.Add((mat, Pts(xy), width, false));
        /// <summary>A paved crossing over water: a road that also removes the water under it.</summary>
        public void Bridge(string mat, float width, params float[] xy) => roads.Add((mat, Pts(xy), width, true));
        /// <summary>A rounded patch (plaza, yard, pond, meadow) with a slightly wavy edge.</summary>
        public void Blob(string mat, float cx, float cy, float rx, float ry, float wobble = 0.08f) => blobs.Add((mat, new Vector2(cx, cy), new Vector2(rx, ry), wobble));
        /// <summary>The sea along the bottom: shoreline around y, sand beach 'sand' tiles deep.</summary>
        public void Sea(float y, float amp, float sand) { seaY = y; seaAmp = amp; sandWidth = sand; }
        /// <summary>A named spot for the town's life: "play" (happenings), "hang" (roamers), "stage", "board".</summary>
        public void Spot(string name, float x, float y) => spots.Add((name, new Vector2(x, y)));

        /// <summary>A house with a small fenced front yard (gap at the door) and a side fence.</summary>
        public void House(string s, float x, float y, bool fence = true)
        {
            Obj(s, x, y);
            if (fence) yards.Add((s, x, y));
        }
        public readonly List<(string house, float x, float y)> yards = new List<(string, float, float)>();

        private static Vector2[] Pts(float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return p;
        }
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

    private static Town[] AllTowns() => new[] { ElementaryPlan(), HighSchoolPlan(), CollegePlan() };

    /// <summary>For the Unity CLI: rebuild one town ("elementary", "highschool", "college") + Game_Scene_Town.</summary>
    public static string RebuildTownNoPrompt(string id)
    {
        LastWarnings.Clear();
        ImportSprites();
        Directory.CreateDirectory(SceneDir);
        var towns = AllTowns();
        foreach (Town t in towns) if (t.id == id) BuildTown(t);
        BuildGameScene(towns);
        AssetDatabase.SaveAssets();
        return LastWarnings.Count == 0 ? $"{id}: nothing on a street" : $"{id}: {LastWarnings.Count} on a street: " + string.Join(" | ", LastWarnings);
    }

    /// <summary>Objects the last build placed on a street (Road cells), for fixing the layout.</summary>
    public static readonly List<string> LastWarnings = new List<string>();

    /// <summary>Does this footprint (bottom strip of a sprite) touch a street cell?</summary>
    private static bool OnStreet(HashSet<Vector2Int> streets, float x0, float x1, float y0, float y1)
    {
        for (int cx = Mathf.FloorToInt(x0); cx <= Mathf.FloorToInt(x1 - 0.01f); cx++)
            for (int cy = Mathf.FloorToInt(y0); cy <= Mathf.FloorToInt(y1 - 0.01f); cy++)
                if (streets.Contains(new Vector2Int(cx, cy))) return true;
        return false;
    }

    private static void Build(bool overwrite)
    {
        ImportSprites();
        Directory.CreateDirectory(SceneDir);
        var towns = AllTowns();
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

        // planned shapes: blobs (plazas, yards, ponds), the sea + beach, curved streets, bridges
        var sea = new HashSet<Vector2Int>();
        var shore = new List<Vector2Int>();
        foreach (var (mat, c, rad, wobble) in t.blobs) AddBlob(byMat, mat, c, rad, wobble, t.seed);
        if (!float.IsNaN(t.seaY)) AddSea(byMat, sea, shore, t);
        foreach (var (mat, pts, width, bridge) in t.roads) if (!bridge) AddRoad(byMat, mat, pts, width);
        foreach (string mat in new List<string>(byMat.Keys)) Tidy(byMat[mat], sea);
        // the streets people walk (Road() only; plazas, yards and soil are meant to have things on them)
        var streets = new HashSet<Vector2Int>();
        foreach (var (mat, pts, width, bridge) in t.roads)
        {
            if (mat == "water") continue;
            var tmp = new Dictionary<string, HashSet<Vector2Int>>();
            AddRoad(tmp, mat, pts, width);
            streets.UnionWith(tmp[mat]);
        }
        // Bridges and piers: over the water they're a wooden deck (water stays visible under it,
        // but nothing blocks walking); on land the approach is ordinary road.
        var deck = new List<(Vector2Int cell, bool alongX)>();
        foreach (var (mat, pts, width, bridge) in t.roads)
        {
            if (!bridge) continue;
            var cells = new HashSet<Vector2Int>();
            AddRoad(new Dictionary<string, HashSet<Vector2Int>> { { mat, cells } }, mat, pts, width);
            bool alongX = Mathf.Abs(pts[pts.Length - 1].x - pts[0].x) >= Mathf.Abs(pts[pts.Length - 1].y - pts[0].y);
            byMat.TryGetValue("water", out var wat);
            var overWater = new HashSet<Vector2Int>();
            if (wat != null) foreach (Vector2Int c in cells) if (wat.Contains(c)) overWater.Add(c);
            foreach (Vector2Int c in overWater) deck.Add((c, alongX));
            cells.ExceptWith(overWater);
            if (cells.Count > 0)
            {
                var set = SetFor(byMat, mat);
                set.UnionWith(cells);
                Tidy(set, null);
            }
            if (wat != null) { wat.ExceptWith(overWater); Tidy(wat, sea); wat.ExceptWith(overWater); }
            sea.ExceptWith(overWater);
        }
        if (byMat.TryGetValue("water", out var waterAfter))
            foreach (var (c, _) in deck) waterAfter.Remove(c);

        foreach (var kv in byMat)
        {
            var m = Materials[kv.Key];
            Tilemap tm = MakeTilemap(grid.transform, kv.Key, m.order, kv.Key == "water");
            foreach (Vector2Int c in kv.Value)
            {
                // the open sea is all open water (no stone rim); the foam line marks the shore
                Vector2Int cell = kv.Key == "water" && sea.Contains(c) ? new Vector2Int(m.bx + 1, m.by + 1)
                    : AutotileCell(kv.Value, c, m.bx, m.by, m.ix, m.iy);
                tm.SetTile(new Vector3Int(c.x, c.y, 0), Cell(cell.x, cell.y));
                if (c.x >= 0 && c.y >= 0 && c.x < t.w && c.y < t.h) occ[c.x, c.y] = true;
            }
        }

        // bridge / pier decks: open water drawn under them on a layer that doesn't block, planks on top
        if (deck.Count > 0)
        {
            var wm = Materials["water"];
            Tilemap under = MakeTilemap(grid.transform, "DeckWater", wm.order, false);
            var deckRoot = new GameObject("Decks").transform;
            deckRoot.SetParent(root.transform, false);
            foreach (var (c, alongX) in deck)
            {
                under.SetTile(new Vector3Int(c.x, c.y, 0), Cell(wm.bx + 1, wm.by + 1));
                GameObject p = PlaceSprite(deckRoot, alongX ? "plank_v" : "plank_h", c.x + 0.5f, c.y, false);
                if (p != null) p.GetComponentInChildren<SpriteRenderer>().sortingOrder = -13;
                if (c.x >= 0 && c.y >= 0 && c.x < t.w && c.y < t.h) occ[c.x, c.y] = true;
            }
        }

        // shore foam along the top of the sea
        if (shore.Count > 0)
        {
            var foamRoot = new GameObject("ShoreFoam").transform;
            foamRoot.SetParent(root.transform, false);
            foreach (Vector2Int c in shore)
            {
                GameObject f = PlaceSprite(foamRoot, ((c.x & 1) == 0) ? "foam0" : "foam1", c.x + 0.5f, c.y + 1f - 6f / 16f, false);
                if (f != null) f.GetComponentInChildren<SpriteRenderer>().sortingOrder = -14;
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
        var placedRects = new List<(string, Rect)>();
        foreach (var (s, x, y, collide) in t.objs)
        {
            GameObject go = PlaceSprite(objRoot, s, x, y, collide);
            if (go != null) MarkSprite(occ, s, x, y);
            float hw = SpriteSize(s).x * 0.4f;
            // things standing on top of each other (footprints of solid props / buildings)
            if (collide && !s.StartsWith("fence") && !s.StartsWith("tree_"))
            {
                var me = Rect.MinMaxRect(x - hw, y, x + hw, y + 0.7f);
                foreach (var (os, orect) in placedRects) if (orect.Overlaps(me)) LastWarnings.Add($"{s} @({x}, {y}) overlaps {os}");
                placedRects.Add((s, me));
            }
            if (collide && OnStreet(streets, x - hw, x + hw, y, y + 0.6f)) LastWarnings.Add($"{s} @({x}, {y})");
        }

        // little fenced front yards: a fence row a step in front of the house, open at the door
        foreach (var (house, hx, hy) in t.yards)
        {
            float half = SpriteSize(house).x / 2f + 1.1f, fy = hy - 1.6f;
            for (float fx = hx - half + 0.5f; fx <= hx - 1.5f; fx += 1f)
            {
                if (OnStreet(streets, fx - 0.5f, fx + 0.5f, fy, fy + 0.8f)) continue;   // never across a street
                PlaceSprite(objRoot, "fence_h", fx, fy, true);
                MarkRect(occ, fx - 0.5f, fy - 0.3f, fx + 0.5f, fy + 1.3f);
            }
            for (float fx = hx + half - 0.5f; fx >= hx + 1.5f; fx -= 1f)
            {
                if (OnStreet(streets, fx - 0.5f, fx + 0.5f, fy, fy + 0.8f)) continue;
                PlaceSprite(objRoot, "fence_h", fx, fy, true);
                MarkRect(occ, fx - 0.5f, fy - 0.3f, fx + 0.5f, fy + 1.3f);
            }
        }

        // named spots for the town's life (happenings, hangouts, the event stage, the board)
        var spotRoot = new GameObject("Spots").transform;
        spotRoot.SetParent(root.transform, false);
        foreach (var (name, p) in t.spots)
        {
            var s = new GameObject(name).transform;
            s.SetParent(spotRoot, false);
            s.position = new Vector3(p.x, p.y, 0f);
            if (name == "stall") MarkRect(occ, p.x - 2.6f, p.y - 1.2f, p.x + 2.6f, p.y + 3.6f);
            else MarkRect(occ, p.x - 1f, p.y - 1f, p.x + 1f, p.y + 1f);
            if (name == "stall" && OnStreet(streets, p.x - 2.2f, p.x + 2.2f, p.y - 0.4f, p.y + 1.2f)) LastWarnings.Add($"stall spot @({p.x}, {p.y})");
        }

        // the rebuildable home
        if (!string.IsNullOrEmpty(t.home))
        {
            PlaceHome(objRoot, t.home, t.homePos.x, t.homePos.y);
            if (OnStreet(streets, t.homePos.x - 2.2f, t.homePos.x + 2.2f, t.homePos.y - 0.9f, t.homePos.y + 0.6f)) LastWarnings.Add("home (or its debris)");
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

    // ── planned shapes ──────────────────────────────────────────────────────

    private static HashSet<Vector2Int> SetFor(Dictionary<string, HashSet<Vector2Int>> byMat, string mat)
    {
        if (!byMat.TryGetValue(mat, out var set)) byMat[mat] = set = new HashSet<Vector2Int>();
        return set;
    }

    /// <summary>
    /// A polyline with each corner rounded off (a curve of ~1.6 tiles, less on short legs), so
    /// straight runs stay straight and turns are smooth; painted with a round brush.
    /// </summary>
    private static void AddRoad(Dictionary<string, HashSet<Vector2Int>> byMat, string mat, Vector2[] pts, float width)
    {
        var set = SetFor(byMat, mat);
        var p = new List<Vector2> { pts[0] };
        for (int i = 1; i < pts.Length - 1; i++)
        {
            Vector2 prev = pts[i - 1], at = pts[i], next = pts[i + 1];
            float cut0 = Mathf.Min(1.6f, Vector2.Distance(prev, at) * 0.45f), cut1 = Mathf.Min(1.6f, Vector2.Distance(at, next) * 0.45f);
            Vector2 a = at + (prev - at).normalized * cut0, b = at + (next - at).normalized * cut1;
            for (int s = 0; s <= 6; s++)
            {
                float u = s / 6f;
                p.Add((1 - u) * (1 - u) * a + 2 * (1 - u) * u * at + u * u * b);   // quadratic curve through the corner
            }
        }
        p.Add(pts[pts.Length - 1]);
        float r = width / 2f;
        for (int i = 0; i < p.Count - 1; i++)
        {
            float len = Vector2.Distance(p[i], p[i + 1]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
            for (int s = 0; s <= steps; s++)
            {
                Vector2 c = Vector2.Lerp(p[i], p[i + 1], s / (float)steps);
                for (int x = Mathf.FloorToInt(c.x - r - 1); x <= Mathf.CeilToInt(c.x + r + 1); x++)
                    for (int y = Mathf.FloorToInt(c.y - r - 1); y <= Mathf.CeilToInt(c.y + r + 1); y++)
                        if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) <= r) set.Add(new Vector2Int(x, y));
            }
        }
    }

    private static void AddBlob(Dictionary<string, HashSet<Vector2Int>> byMat, string mat, Vector2 c, Vector2 rad, float wobble, int seed)
    {
        var set = SetFor(byMat, mat);
        float s = (seed + c.x * 3.1f + c.y * 1.7f) % 6.28f;
        for (int x = Mathf.FloorToInt(c.x - rad.x * 1.3f); x <= Mathf.CeilToInt(c.x + rad.x * 1.3f); x++)
            for (int y = Mathf.FloorToInt(c.y - rad.y * 1.3f); y <= Mathf.CeilToInt(c.y + rad.y * 1.3f); y++)
            {
                float dx = (x + 0.5f - c.x) / rad.x, dy = (y + 0.5f - c.y) / rad.y;
                float a = Mathf.Atan2(dy, dx);
                float edge = 1f + wobble * (Mathf.Sin(3f * a + s) + 0.5f * Mathf.Sin(5f * a + 2f * s));
                if (dx * dx + dy * dy <= edge * edge) set.Add(new Vector2Int(x, y));
            }
    }

    /// <summary>
    /// The sea along the bottom, far past the map so zooming out shows open water, with a wavy
    /// shoreline and a sand beach above it. The sand also runs a few tiles under the water so
    /// its grassy edge never shows at the waterline.
    /// </summary>
    private static void AddSea(Dictionary<string, HashSet<Vector2Int>> byMat, HashSet<Vector2Int> sea, List<Vector2Int> shore, Town t)
    {
        var water = SetFor(byMat, "water");
        var sand = SetFor(byMat, "sand");
        float s = t.seed * 0.37f;
        for (int x = -Margin - 40; x < t.w + Margin + 40; x++)
        {
            float line = t.seaY + t.seaAmp * (Mathf.Sin(x * 0.17f + s) + 0.6f * Mathf.Sin(x * 0.067f + 2f * s));
            int top = Mathf.RoundToInt(line);
            int beach = Mathf.RoundToInt(t.sandWidth + 1.4f * Mathf.Sin(x * 0.11f + s * 1.3f));
            for (int y = -Margin - 40; y <= top; y++)
            {
                var c = new Vector2Int(x, y);
                water.Add(c);
                sea.Add(c);
            }
            for (int y = top - 3; y <= top + beach; y++) sand.Add(new Vector2Int(x, y));
            if (x >= -Margin && x < t.w + Margin) shore.Add(new Vector2Int(x, top));
        }
    }

    /// <summary>
    /// Makes a shape autotile cleanly: fills one-tile notches, then drops cells that aren't
    /// part of any full 2x2 block (one-tile spurs and diagonal-only joins have no matching tile).
    /// </summary>
    private static void Tidy(HashSet<Vector2Int> set, HashSet<Vector2Int> keep)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            var add = new List<Vector2Int>();
            var bounds = new HashSet<Vector2Int>();
            foreach (Vector2Int c in set)
                foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    bounds.Add(c + d);
            foreach (Vector2Int c in bounds)
            {
                if (set.Contains(c)) continue;
                int n = 0;
                foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    if (set.Contains(c + d)) n++;
                if (n >= 3) add.Add(c);
            }
            set.UnionWith(add);

            var drop = new List<Vector2Int>();
            foreach (Vector2Int c in set)
            {
                if (keep != null && keep.Contains(c)) continue;
                bool inBlock = false;
                for (int ox = -1; ox <= 0 && !inBlock; ox++)
                    for (int oy = -1; oy <= 0 && !inBlock; oy++)
                    {
                        Vector2Int o = c + new Vector2Int(ox, oy);
                        inBlock = set.Contains(o) && set.Contains(o + Vector2Int.right) && set.Contains(o + Vector2Int.up) && set.Contains(o + Vector2Int.one);
                    }
                if (!inBlock) drop.Add(c);
            }
            set.ExceptWith(drop);
            if (add.Count == 0 && drop.Count == 0) break;
        }
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
        // a coast replaces the bottom ring (the sea is the edge there), and the side rings stop
        // at the beach
        bool coast = !float.IsNaN(t.seaY);
        float sideFrom = coast ? t.seaY + t.sandWidth + 1.5f : 0f;
        for (int ring = 0; ring < 4; ring++)
        {
            float step = 2.6f;
            float inset = -0.4f - ring * 2.4f;
            for (float x = inset; x <= t.w - inset; x += step)
            {
                if (!coast) PlaceSprite(parent, Pick(t, rng), x + Jit(rng), inset - 0.2f + Jit(rng), ring == 0);
                PlaceSprite(parent, Pick(t, rng), x + Jit(rng), t.h - inset - 2.4f + Jit(rng), ring == 0);
            }
            for (float y = Mathf.Max(inset + step, sideFrom); y <= t.h - inset - step; y += step)
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

    // layouts: vc_TownBuilder.Plans.cs
}
