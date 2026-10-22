using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Walkability grid + A* paths for one town, so people walk around buildings, trees and the
/// pond instead of bumping into them. Built once per town from what the town builder made:
/// the Bounds walls (town edge), the water tilemap and every solid collider. Moving things
/// (the player, NPC bodies) and triggers are ignored. Cells are half a unit.
/// Used by roaming townsfolk (vc_TownLife) and by the walk to bed (vc_TownStage).
/// </summary>
public class vc_TownGrid
{
    public const float Cell = 0.5f;

    private readonly Rect _rect;
    private readonly int _w, _h;
    private readonly bool[] _walk;

    private static readonly Dictionary<int, vc_TownGrid> Cache = new Dictionary<int, vc_TownGrid>();

    /// <summary>The grid for this town root (built on first use, kept until the town unloads).</summary>
    public static vc_TownGrid For(Transform town)
    {
        if (town == null) return null;
        int key = town.GetInstanceID();
        if (Cache.TryGetValue(key, out vc_TownGrid g)) return g;
        g = new vc_TownGrid(town);
        Cache[key] = g;
        return g;
    }

    public static void Forget(Transform town) { if (town != null) Cache.Remove(town.GetInstanceID()); }

    private vc_TownGrid(Transform town)
    {
        _rect = TownRect(town);
        _w = Mathf.CeilToInt(_rect.width / Cell);
        _h = Mathf.CeilToInt(_rect.height / Cell);
        _walk = new bool[_w * _h];

        Tilemap water = town.Find("Grid")?.Find("water")?.GetComponent<Tilemap>();
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        filter.useLayerMask = true;
        var hits = new Collider2D[6];
        Physics2D.SyncTransforms();

        // The player's body collider stands on the feet point and reaches half a unit up
        // (0.23 x 0.5, offset +0.25). Check that shape with a margin, so paths keep a little
        // distance from walls and posts and never clip a corner.
        Vector2 body = new Vector2(0.5f, 0.66f), bodyOffset = new Vector2(0f, 0.25f);
        for (int y = 0; y < _h; y++)
        for (int x = 0; x < _w; x++)
        {
            Vector2 c = CellCenter(x, y);
            bool ok = water == null || !water.HasTile(water.WorldToCell(c));
            if (ok)
            {
                int n = Physics2D.OverlapBox(c + bodyOffset, body, 0f, filter, hits);
                for (int i = 0; i < n && ok; i++) if (IsStatic(hits[i])) ok = false;
            }
            _walk[y * _w + x] = ok;
        }
    }

    /// <summary>Things that never move block paths; the player and NPCs (dynamic bodies) don't.</summary>
    private static bool IsStatic(Collider2D c)
    {
        if (c == null) return false;
        Rigidbody2D rb = c.attachedRigidbody;
        return rb == null || rb.bodyType == RigidbodyType2D.Static;
    }

    public static Rect TownRect(Transform town)
    {
        Transform walls = town.Find("Bounds");
        var rect = new Rect(0f, 0f, 48f, 36f);
        if (walls != null && walls.childCount > 0)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (Transform w in walls)
            {
                x0 = Mathf.Min(x0, w.position.x); x1 = Mathf.Max(x1, w.position.x);
                y0 = Mathf.Min(y0, w.position.y); y1 = Mathf.Max(y1, w.position.y);
            }
            rect = Rect.MinMaxRect(x0 + 1f, y0 + 1f, x1 - 1f, y1 - 1f);
        }
        return rect;
    }

    // ── cells ─────────────────────────────────────────────────────────────

    private Vector2 CellCenter(int x, int y) => new Vector2(_rect.xMin + (x + 0.5f) * Cell, _rect.yMin + (y + 0.5f) * Cell);

    private bool ToCell(Vector2 p, out int x, out int y)
    {
        x = Mathf.FloorToInt((p.x - _rect.xMin) / Cell);
        y = Mathf.FloorToInt((p.y - _rect.yMin) / Cell);
        return x >= 0 && y >= 0 && x < _w && y < _h;
    }

    private bool Walk(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h && _walk[y * _w + x];

    public bool IsWalkable(Vector2 p) => ToCell(p, out int x, out int y) && Walk(x, y);

    /// <summary>The walkable cell centre closest to p (searching outward), or null.</summary>
    public Vector2? NearestWalkable(Vector2 p, int maxRings = 8)
    {
        ToCell(p, out int cx, out int cy);
        cx = Mathf.Clamp(cx, 0, _w - 1); cy = Mathf.Clamp(cy, 0, _h - 1);
        if (Walk(cx, cy)) return CellCenter(cx, cy);
        for (int r = 1; r <= maxRings; r++)
        {
            float best = float.MaxValue; Vector2? found = null;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                if (!Walk(cx + dx, cy + dy)) continue;
                Vector2 c = CellCenter(cx + dx, cy + dy);
                float d = (c - p).sqrMagnitude;
                if (d < best) { best = d; found = c; }
            }
            if (found.HasValue) return found;
        }
        return null;
    }

    /// <summary>A random walkable spot within range of p (or anywhere if range &lt;= 0).</summary>
    public Vector2? RandomWalkable(Vector2 near, float range, System.Func<Vector2, bool> accept = null, int tries = 40)
    {
        for (int i = 0; i < tries; i++)
        {
            Vector2 p = range > 0f
                ? near + new Vector2(Random.Range(-range, range), Random.Range(-range, range))
                : new Vector2(Random.Range(_rect.xMin, _rect.xMax), Random.Range(_rect.yMin, _rect.yMax));
            if (!IsWalkable(p)) continue;
            if (accept != null && !accept(p)) continue;
            return p;
        }
        return null;
    }

    // ── A* ────────────────────────────────────────────────────────────────

    private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

    /// <summary>
    /// Waypoints from 'from' to 'to' around everything solid (empty list if unreachable).
    /// The path is straightened wherever there's a clear line, so it doesn't zig-zag cell
    /// by cell. The last point is exactly 'to' when 'to' is walkable.
    /// </summary>
    public List<Vector3> FindPath(Vector3 from, Vector3 to, int maxExpanded = 9000)
    {
        var result = new List<Vector3>();
        Vector2? s = NearestWalkable(from, 4), g = NearestWalkable(to, 6);
        if (!s.HasValue || !g.HasValue) return result;
        ToCell(s.Value, out int sx, out int sy);
        ToCell(g.Value, out int gx, out int gy);
        int start = sy * _w + sx, goal = gy * _w + gx;

        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float> { [start] = 0f };
        var open = new MinHeap();
        open.Push(start, Heuristic(sx, sy, gx, gy));
        var closed = new HashSet<int>();
        int expanded = 0;

        while (open.Count > 0 && expanded < maxExpanded)
        {
            int cur = open.Pop();
            if (cur == goal) break;
            if (!closed.Add(cur)) continue;
            expanded++;
            int cx = cur % _w, cy = cur / _w;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + Dx[k], ny = cy + Dy[k];
                if (!Walk(nx, ny)) continue;
                // diagonal moves never cut a corner
                if (k >= 4 && (!Walk(cx + Dx[k], cy) || !Walk(cx, cy + Dy[k]))) continue;
                int n = ny * _w + nx;
                if (closed.Contains(n)) continue;
                float cost = gScore[cur] + (k >= 4 ? 1.4142f : 1f);
                if (gScore.TryGetValue(n, out float old) && old <= cost) continue;
                gScore[n] = cost;
                cameFrom[n] = cur;
                open.Push(n, cost + Heuristic(nx, ny, gx, gy));
            }
        }
        if (!cameFrom.ContainsKey(goal) && goal != start) return result;

        var cells = new List<Vector2>();
        for (int c = goal; ; c = cameFrom[c])
        {
            cells.Add(CellCenter(c % _w, c / _w));
            if (c == start || !cameFrom.ContainsKey(c)) break;
        }
        cells.Reverse();

        // string-pull: keep only the corners we can't see past
        Vector2 anchor = from;
        for (int i = 1; i < cells.Count; i++)
        {
            if (!LineClear(anchor, cells[i]))
            {
                result.Add(cells[i - 1]);
                anchor = cells[i - 1];
            }
        }
        result.Add(IsWalkable(to) && LineClear(anchor, to) ? (Vector2)to : g.Value);
        for (int i = 0; i < result.Count; i++) result[i] = new Vector3(result[i].x, result[i].y, from.z);
        return result;
    }

    private static float Heuristic(int x, int y, int gx, int gy)
    {
        int dx = Mathf.Abs(x - gx), dy = Mathf.Abs(y - gy);
        return (dx + dy) + (1.4142f - 2f) * Mathf.Min(dx, dy);
    }

    /// <summary>Is the straight line between two points walkable all the way (with body width)?</summary>
    public bool LineClear(Vector2 a, Vector2 b)
    {
        float d = Vector2.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(d / (Cell * 0.5f)));
        Vector2 side = d > 0.001f ? Vector2.Perpendicular((b - a) / d) * 0.18f : Vector2.zero;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            if (!IsWalkable(p) || !IsWalkable(p + side) || !IsWalkable(p - side)) return false;
        }
        return true;
    }

    private class MinHeap
    {
        private readonly List<(int id, float f)> _h = new List<(int, float)>();
        public int Count => _h.Count;
        public void Push(int id, float f)
        {
            _h.Add((id, f));
            int i = _h.Count - 1;
            while (i > 0) { int p = (i - 1) / 2; if (_h[p].f <= _h[i].f) break; (_h[p], _h[i]) = (_h[i], _h[p]); i = p; }
        }
        public int Pop()
        {
            int top = _h[0].id;
            _h[0] = _h[_h.Count - 1];
            _h.RemoveAt(_h.Count - 1);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < _h.Count && _h[l].f < _h[m].f) m = l;
                if (r < _h.Count && _h[r].f < _h[m].f) m = r;
                if (m == i) break;
                (_h[m], _h[i]) = (_h[i], _h[m]); i = m;
            }
            return top;
        }
    }
}
