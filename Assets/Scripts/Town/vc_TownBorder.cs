using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the player inside the playable world, and fills what the camera sees past it.
/// Built at runtime from what's already in the scene, so rebuilding a town never undoes it.
///
///  - Town / tutorial edge: the forest ring's trees were only solid at the trunk, so the
///    player could slip between them and walk around under the canopies. Every edge tree is
///    now solid over its whole footprint, and the gaps between neighbours are closed too.
///  - Quest rooms: invisible walls around the 16x16 room (some rooms have no wall colliders,
///    some have side openings left over from the old hallways). Only the player is held in.
///  - Past the map: a big tiled far-forest canopy behind everything, and the camera clears to
///    the same dark green, so zooming out never shows the blue void.
/// </summary>
public static class vc_TownBorder
{
    private const string BlockName = "BorderBlock";
    private const string ForestName = "FarForest";
    public const string FarForestResource = "Town/far_forest";

    /// <summary>The far forest's darkest canopy colour (camera clear colour).</summary>
    public static readonly Color VoidColor = new Color32(24, 44, 33, 255);

    private enum Edge { Bottom, Top, Left, Right }

    private struct Foot
    {
        public Edge edge;
        public Rect rect;
        public float Along0 => edge == Edge.Bottom || edge == Edge.Top ? rect.xMin : rect.yMin;
        public float Along1 => edge == Edge.Bottom || edge == Edge.Top ? rect.xMax : rect.yMax;
        public float Across0 => edge == Edge.Bottom || edge == Edge.Top ? rect.yMin : rect.xMin;
        public float Across1 => edge == Edge.Bottom || edge == Edge.Top ? rect.yMax : rect.xMax;
    }

    /// <summary>
    /// Solid edge forest + far-forest backdrop for a town (or the tutorial grounds) root that
    /// has "Nature" and "Bounds" children. Safe to call more than once.
    /// </summary>
    public static void Apply(Transform town, Rect? extraArea = null)
    {
        if (town == null || town.Find(BlockName) != null) return;
        Rect rect = vc_TownGrid.TownRect(town);   // the playable rect, inside the edge walls

        var block = new GameObject(BlockName).transform;
        block.SetParent(town, false);

        var keepClear = KeepClearPoints(town);
        var feet = new List<Foot>();
        Transform nature = town.Find("Nature");
        if (nature != null)
            foreach (Transform tree in nature)
            {
                if (!tree.name.StartsWith("tree_")) continue;
                Vector3 p = tree.position;
                // only the ring at the playable edge matters; the outer rings are behind walls
                if (p.x < rect.xMin - 3f || p.x > rect.xMax + 3f || p.y < rect.yMin - 3f || p.y > rect.yMax + 3f) continue;
                float dl = p.x - rect.xMin, dr = rect.xMax - p.x, db = p.y - rect.yMin, dt = rect.yMax - p.y;
                Edge edge; float d;
                if (dl <= dr && dl <= db && dl <= dt) { edge = Edge.Left; d = dl; }
                else if (dr <= db && dr <= dt) { edge = Edge.Right; d = dr; }
                else if (db <= dt) { edge = Edge.Bottom; d = db; }
                else { edge = Edge.Top; d = dt; }
                // edge trees stand at the line (top row a couple of tiles in); cluster trees don't
                if (d > (edge == Edge.Top ? 3.2f : 1.5f)) continue;

                if (!VisualRect(tree, out Rect v)) continue;
                // whole footprint, minus a little at the top so the player's head can tuck under
                Rect foot = Rect.MinMaxRect(v.xMin + 0.1f, v.yMin, v.xMax - 0.1f, Mathf.Max(v.yMin + 0.6f, v.yMax - 0.75f));
                feet.Add(new Foot { edge = edge, rect = foot });
            }

        // each tree's footprint, then the gap to its neighbour along the same edge
        int added = 0, skipped = 0;
        foreach (Edge e in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
        {
            var row = feet.FindAll(f => f.edge == e);
            row.Sort((a, b) => a.Along0.CompareTo(b.Along0));
            for (int i = 0; i < row.Count; i++)
            {
                if (AddBox(block, row[i].rect, keepClear)) added++; else skipped++;
                if (i + 1 >= row.Count) continue;
                Foot a = row[i], b = row[i + 1];
                if (b.Along0 <= a.Along1) continue;
                float c0 = Mathf.Max(a.Across0, b.Across0), c1 = Mathf.Min(a.Across1, b.Across1);
                if (c1 - c0 < 0.4f) { c0 = Mathf.Min(a.Across0, b.Across0); c1 = Mathf.Max(a.Across1, b.Across1); }
                Rect gap = e == Edge.Bottom || e == Edge.Top
                    ? Rect.MinMaxRect(a.Along1 - 0.05f, c0, b.Along0 + 0.05f, c1)
                    : Rect.MinMaxRect(c0, a.Along1 - 0.05f, c1, b.Along0 + 0.05f);
                if (AddBox(block, gap, keepClear)) added++; else skipped++;
            }
        }
        if (skipped > 0) Debug.Log($"[vc_TownBorder] {town.name}: {added} edge blocks, {skipped} left open (an NPC, prop or spawn stands there)");

        // backdrop: the town, its margin, and the quest area far below it
        Rect area = rect;
        if (extraArea.HasValue)
        {
            Rect x = extraArea.Value;
            area = Rect.MinMaxRect(Mathf.Min(area.xMin, x.xMin), Mathf.Min(area.yMin, x.yMin), Mathf.Max(area.xMax, x.xMax), Mathf.Max(area.yMax, x.yMax));
        }
        AddFarForest(town, area, 70f);
        PaintVoid(Camera.main);
        vc_TownGrid.Forget(town);   // a grid built before this would miss the new blocks
    }

    /// <summary>
    /// Invisible walls just inside the edge of a quest room (rooms are size x size, centred on
    /// their root). Only the player is held in: anything with its own moving body in the room
    /// (quest NPCs, pets) ignores them.
    /// </summary>
    public static void WallRoom(GameObject room, float size = 16f)
    {
        if (room == null || room.transform.Find("RoomWalls") != null) return;
        var walls = new GameObject("RoomWalls").transform;
        walls.SetParent(room.transform, false);
        float half = size / 2f, inner = half - 1f, t = 2f;
        var made = new[]
        {
            Wall(walls, new Vector2(0f, -inner - t / 2f), new Vector2(size + 2f * t, t)),
            Wall(walls, new Vector2(0f, inner + t / 2f), new Vector2(size + 2f * t, t)),
            Wall(walls, new Vector2(-inner - t / 2f, 0f), new Vector2(t, size + 2f * t)),
            Wall(walls, new Vector2(inner + t / 2f, 0f), new Vector2(t, size + 2f * t)),
        };
        foreach (Collider2D c in room.GetComponentsInChildren<Collider2D>(true))
        {
            if (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static) continue;
            if (c.GetComponentInParent<PlayerController>() != null) continue;
            foreach (Collider2D w in made) Physics2D.IgnoreCollision(w, c, true);
        }
    }

    /// <summary>The camera clears to the far forest's dark green instead of the blue skybox.</summary>
    public static void PaintVoid(Camera cam)
    {
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = VoidColor;
    }

    /// <summary>A big tiled canopy behind everything, covering 'area' plus 'pad' on each side.</summary>
    public static void AddFarForest(Transform parent, Rect area, float pad)
    {
        if (parent == null || parent.Find(ForestName) != null) return;
        Sprite tile = Resources.Load<Sprite>(FarForestResource);
        if (tile == null) { Debug.LogWarning($"[vc_TownBorder] Missing Resources/{FarForestResource}.png"); return; }

        var go = new GameObject(ForestName, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        float tw = tile.rect.width / tile.pixelsPerUnit, th = tile.rect.height / tile.pixelsPerUnit;
        // snap to whole tiles so the pattern sits on the pixel grid
        float x0 = Mathf.Floor((area.xMin - pad) / tw) * tw, y0 = Mathf.Floor((area.yMin - pad) / th) * th;
        float x1 = Mathf.Ceil((area.xMax + pad) / tw) * tw, y1 = Mathf.Ceil((area.yMax + pad) / th) * th;
        go.transform.position = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 0f);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = tile;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(x1 - x0, y1 - y0);
        sr.sortingLayerName = "Default";
        sr.sortingOrder = -40;   // under the ground tilemap (-22)
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static Collider2D Wall(Transform parent, Vector2 localCenter, Vector2 size)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        var c = go.AddComponent<BoxCollider2D>();
        c.size = size;
        return c;
    }

    private static bool AddBox(Transform parent, Rect r, List<Vector2> keepClear)
    {
        if (r.width < 0.05f || r.height < 0.05f) return true;
        Rect grown = Rect.MinMaxRect(r.xMin - 0.3f, r.yMin - 0.3f, r.xMax + 0.3f, r.yMax + 0.3f);
        foreach (Vector2 p in keepClear) if (grown.Contains(p)) return false;
        var go = new GameObject("Block");
        go.transform.SetParent(parent, false);
        go.transform.position = r.center;
        go.AddComponent<BoxCollider2D>().size = r.size;
        return true;
    }

    /// <summary>Spots that must stay walkable: NPCs, the spawn point, buildings' doors.</summary>
    private static List<Vector2> KeepClearPoints(Transform town)
    {
        var pts = new List<Vector2>();
        foreach (vc_TownNPC n in town.GetComponentsInChildren<vc_TownNPC>(true)) pts.Add(n.transform.position);
        foreach (vc_TownHome h in town.GetComponentsInChildren<vc_TownHome>(true)) pts.Add(h.WakePosition);
        Transform spawn = town.Find("PlayerSpawn");
        if (spawn != null) pts.Add(spawn.position);
        Transform objects = town.Find("Objects");
        if (objects != null) foreach (Transform o in objects) pts.Add(o.position + Vector3.down * 0.8f);
        return pts;
    }

    /// <summary>World rect of a tree's base + top sprites.</summary>
    private static bool VisualRect(Transform tree, out Rect rect)
    {
        rect = default;
        bool any = false;
        Bounds b = default;
        foreach (SpriteRenderer sr in tree.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            if (!any) { b = sr.bounds; any = true; } else b.Encapsulate(sr.bounds);
        }
        if (any) rect = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
        return any;
    }
}
