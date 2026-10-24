using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the tutorial's grounds in the same style as the towns (same tiles, props, trees,
/// forest border and ambience), so the tutorial looks like a small corner of the real game:
///   - your home on the left, a walkway down to the road;
///   - the road leads east to a stone plaza where the 6 skill pedestals stand in a ring;
///   - a path north from the plaza to a little park: the kite tree, Tala, a swing and a slide;
///   - the janitor (who helps with some skills) stands by the small hall.
/// The tutorial's own objects (markers, Tala, kite tree, skill zone, triggers) are kept and
/// only moved, so every reference and script stays as it was. The old ground is switched off,
/// not deleted. Safe to run again: it replaces its own "TutorialGrounds" root.
/// </summary>
public static partial class vc_TownBuilder
{
    private const string TutorialScene = "Assets/Scenes/Level0_Tutorial.unity";

    [MenuItem("SUNOD/Tutorial/Rebuild Tutorial Grounds")]
    public static void BuildTutorialGrounds()
    {
        ImportSprites();
        var scene = EditorSceneManager.OpenScene(TutorialScene, OpenSceneMode.Single);

        var t = new Town { id = "tutorial", title = "Tutorial", w = 30, h = 21, seed = 4242, spawn = new Vector2(5f, 7.6f) };
        // road from home to the plaza, the plaza, the path up to the park
        t.Path("dirt", 4, 5, 3, 4);      // home walkway
        t.Path("dirt", 2, 5, 14, 2);     // road
        t.Path("plaza", 15, 3, 9, 7);    // skill plaza
        t.Path("plaza", 18, 10, 3, 5);   // stone walk up to the park (same tiles as the plaza, so they join)
        t.Obj("house_red", 5f, 9f);
        t.Obj("garden_bed", 8.6f, 9.2f, false);
        t.Obj("hall_small", 11f, 12.4f);
        t.Obj("house_peach", 26.5f, 12.6f);
        t.Obj("sign_park", 22.6f, 13.1f);
        t.Obj("swing", 25.2f, 16.4f);
        t.Obj("slide", 28f, 16.4f);
        t.Obj("bench", 16.6f, 10.5f);
        t.Obj("bench", 22.4f, 10.5f);
        t.Obj("lamp_white", 14.4f, 8.6f);
        t.Obj("lamp_white", 24.6f, 8.6f);
        t.Obj("lamp_blue", 14.4f, 3.2f);
        t.Trees(1, 13, 7, 6, 4, "tree_green", "tree_round", "tree_pink");
        t.Trees(1, 0, 12, 3, 3, "tree_green", "tree_pine");
        t.Trees(25, 0, 4, 3, 2, "tree_round", "tree_pine");

        // places that must stay clear for the tutorial's own pieces
        var keepClear = new List<Rect>
        {
            Rect.MinMaxRect(15f, 3f, 24f, 10f),     // plaza + pedestals
            Rect.MinMaxRect(15.5f, 13.5f, 23.5f, 20f),  // kite tree, Tala, the skill zone
            Rect.MinMaxRect(10f, 10f, 13.5f, 12.2f),    // janitor
            Rect.MinMaxRect(3f, 6f, 7f, 9f),            // spawn
        };

        GameObject old = null;
        foreach (GameObject r in scene.GetRootGameObjects()) if (r.name == "TutorialGrounds") old = r;
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("TutorialGrounds");
        SceneManager_MoveToScene(root, scene);
        BuildGroundsInto(root.transform, t, keepClear);
        root.AddComponent<vc_TownAmbience>();
        root.AddComponent<vc_TownAtmosphere>();
        root.AddComponent<vc_TownCritters>();

        // the old ground (dark path pack) is switched off, not deleted
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "Grid (1)" || r.name == "Grid") r.SetActive(false);

        PlaceTutorialPieces(scene, t);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[vc_TownBuilder] Tutorial grounds rebuilt.");
    }

    private static void SceneManager_MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    /// <summary>Grass, autotiled paths, patches, props, trees, forest border, flora and bounds (the town recipe).</summary>
    private static void BuildGroundsInto(Transform root, Town t, List<Rect> keepClear)
    {
        var rng = new System.Random(t.seed);
        var occ = new bool[t.w, t.h];
        foreach (Rect r in keepClear) MarkRect(occ, r.xMin, r.yMin, r.xMax, r.yMax);

        var grid = new GameObject("Grid", typeof(Grid));
        grid.transform.SetParent(root, false);
        Tilemap ground = MakeTilemap(grid.transform, "Ground", -22, false);
        TileBase grass = Cell(GrassCell.x, GrassCell.y);
        for (int x = -Margin; x < t.w + Margin; x++)
            for (int y = -Margin; y < t.h + Margin; y++)
                ground.SetTile(new Vector3Int(x, y, 0), grass);

        var byMat = new Dictionary<string, HashSet<Vector2Int>>();
        foreach (var (mat, r) in t.paths) AddRect(byMat, mat, r);
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

        var patchRoot = new GameObject("GrassPatches").transform;
        patchRoot.SetParent(root, false);
        string[] patches = { "grass_dark1", "grass_dark2", "grass_dark3", "grass_light1", "grass_light2", "grass_light3" };
        for (int x = -Margin + 2; x < t.w + Margin - 2; x += 2)
            for (int y = -Margin + 2; y < t.h + Margin - 2; y += 2)
            {
                if (rng.NextDouble() > 0.24) continue;
                var c = new Vector2Int(x, y);
                bool onPath = false;
                foreach (var set in byMat.Values) if (set.Contains(c)) { onPath = true; break; }
                if (onPath) continue;
                GameObject p = PlaceSprite(patchRoot, patches[rng.Next(patches.Length)], x + (float)rng.NextDouble(), y + (float)rng.NextDouble(), false);
                if (p != null) p.GetComponentInChildren<SpriteRenderer>().sortingOrder = -21;
            }

        var objRoot = new GameObject("Objects").transform;
        objRoot.SetParent(root, false);
        foreach (var (s, x, y, collide) in t.objs)
            if (PlaceSprite(objRoot, s, x, y, collide) != null) MarkSprite(occ, s, x, y);

        var natureRoot = new GameObject("Nature").transform;
        natureRoot.SetParent(root, false);
        foreach (var (area, count, kinds) in t.trees)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 40 && placed < count; attempt++)
            {
                string kind = kinds[rng.Next(kinds.Length)];
                float x = Mathf.Round((area.x + (float)rng.NextDouble() * area.width) * 2f) / 2f;
                float y = Mathf.Round((area.y + (float)rng.NextDouble() * area.height) * 2f) / 2f;
                if (!SpriteFits(occ, kind, x, y)) continue;
                PlaceSprite(natureRoot, kind, x, y, true);
                MarkSprite(occ, kind, x, y);
                placed++;
            }
        }
        BuildBorder(natureRoot, t, rng);

        var floraRoot = new GameObject("Flora").transform;
        floraRoot.SetParent(root, false);
        string[] tufts = { "tuft1", "tuft2", "tuft3", "tuft4", "tuft5", "tuft6", "tuft7", "tuft8", "tuft_wide1", "tuft_wide2", "tuft_wide3", "tuft_wide4" };
        for (int x = -Margin + 1; x < t.w + Margin - 1; x++)
            for (int y = -Margin + 1; y < t.h + Margin - 1; y++)
            {
                bool inTown = x >= 0 && y >= 0 && x < t.w && y < t.h;
                if (inTown && occ[x, y]) continue;
                var c = new Vector2Int(x, y);
                bool onPath = false;
                foreach (var set in byMat.Values) if (set.Contains(c)) { onPath = true; break; }
                if (onPath) continue;
                if (rng.NextDouble() < 0.45)
                    PlaceSprite(floraRoot, tufts[rng.Next(tufts.Length)], x + 0.15f + (float)rng.NextDouble() * 0.7f, y + (float)rng.NextDouble() * 0.8f, false);
                if (rng.NextDouble() < t.decorDensity)
                {
                    PlaceSprite(floraRoot, t.decor[rng.Next(t.decor.Length)], x + 0.5f, y + 0.15f, false);
                    if (inTown) occ[x, y] = true;
                }
            }

        var bounds = new GameObject("Bounds").transform;
        bounds.SetParent(root, false);
        AddWall(bounds, new Vector2(t.w / 2f, -0.5f), new Vector2(t.w + 2, 1));
        AddWall(bounds, new Vector2(t.w / 2f, t.h + 0.5f), new Vector2(t.w + 2, 1));
        AddWall(bounds, new Vector2(-0.5f, t.h / 2f), new Vector2(1, t.h + 2));
        AddWall(bounds, new Vector2(t.w + 0.5f, t.h / 2f), new Vector2(1, t.h + 2));
    }

    /// <summary>Moves the tutorial's own objects into the new layout (references untouched).</summary>
    private static void PlaceTutorialPieces(UnityEngine.SceneManagement.Scene scene, Town t)
    {
        Transform Find(string path)
        {
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                if (r.name == path) return r.transform;
                Transform c = r.transform.Find(path);
                if (c != null) return c;
            }
            return null;
        }
        void Put(string path, float x, float y)
        {
            Transform tr = Find(path);
            if (tr != null) tr.position = new Vector3(x, y, tr.position.z);
            else Debug.LogWarning("[vc_TownBuilder] Tutorial piece not found: " + path);
        }

        // player starts in front of home
        Put("Player", t.spawn.x, t.spawn.y);

        // the six skill pedestals in a ring around the plaza (R at the top, then clockwise)
        var center = new Vector2(19.5f, 6.5f);
        string[] letters = { "R", "I", "A", "S", "E", "C" };
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Deg2Rad * (90f - i * 60f);
            Put("Marker_" + letters[i], center.x + Mathf.Cos(a) * 2.6f, center.y + Mathf.Sin(a) * 2.1f);
        }

        // walking onto the plaza starts the skill lesson: a tall trigger across the road
        Transform area = Find("Trigger_SkillArea");
        if (area != null)
        {
            area.position = new Vector3(14.6f, 6f, 0f);
            var box = area.GetComponent<BoxCollider2D>();
            if (box != null) { box.size = new Vector2(1f, 3f); box.offset = Vector2.zero; }
        }

        // the park: kite tree, kite in its branches, Tala looking up at it, the skill zone below
        Put("KiteTree", 19.5f, 16.8f);
        Put("Kite", 18.8f, 17.9f);
        Put("Tala", 17.3f, 16.5f);
        Put("Prefab_SkillZone", 19.5f, 14.6f);
        Put("KiteQuestProp", 19.5f, 15.5f);

        // the janitor waits by the hall, and looks like a townsperson now
        Put("JanitorNPC", 11.8f, 11.2f);
        Transform janitor = Find("JanitorNPC");
        var jr = janitor != null ? janitor.GetComponent<SpriteRenderer>() : null;
        Sprite look = AssetDatabase.LoadAssetAtPath<Sprite>($"{NpcDir}/npc_bob_2_f0.png");
        Transform tala = Find("Tala");
        if (jr != null && look != null)
        {
            jr.sprite = look;
            janitor.localScale = tala != null ? tala.localScale : new Vector3(0.85f, 0.85f, 1f);
            var talaR = tala != null ? tala.GetComponent<SpriteRenderer>() : null;
            if (talaR != null) { jr.sortingLayerID = talaR.sortingLayerID; jr.sortingOrder = talaR.sortingOrder; }
        }

        // Tala gets the same "!" as a town NPC who needs help (the flow controller shows/hides it)
        if (tala != null)
        {
            Transform mark = tala.Find("QuestMarker");
            if (mark == null) { mark = new GameObject("QuestMarker", typeof(SpriteRenderer)).transform; mark.SetParent(tala, false); }
            var mr = mark.GetComponent<SpriteRenderer>();
            mr.sprite = LoadSprite("marker_quest");
            mr.sortingLayerName = "Decor";
            mr.sortingOrder = 10;
            mark.localPosition = new Vector3(0f, 1.9f, 0f);
            mark.localScale = new Vector3(1f / Mathf.Max(0.01f, tala.localScale.x), 1f / Mathf.Max(0.01f, tala.localScale.y), 1f);
            mark.gameObject.SetActive(false);
        }
    }
}
