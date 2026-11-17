using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Visual only. Turns an indoor quest room into a real building interior when it spawns:
///   - the forest around it is hidden behind a dark surround,
///   - the garden, street lamps and benches around the old paved pad are removed,
///   - the floor is relaid: planks (homes, shops) or tiles (school, framed rooms), with an
///     area rug in the room's own colour where the quest takes place,
///   - a back wall (plaster, wainscot, windows) and wall tops are drawn around the floor.
/// Nothing here adds or removes colliders, so walking is unchanged.
/// Outdoor rooms (forest, river, fair stalls) keep their ground and get a plank stage, sun
/// patches instead (DressOutdoor).
/// Art: Resources/RoomShell (Tools/art/make_room_shell.py).
/// </summary>
public class vc_RoomDressing : MonoBehaviour
{
    private const string ShellName = "ArtShell";
    private static readonly Color VoidColor = new Color32(28, 23, 38, 255);

    // Rooms under House_/School_ that are really outdoors.
    private static readonly string[] OutdoorRooms =
    {
        "Room_School_CareerFair", "Room_School_SnackStall", "Room_School_ParadeDrum",
        "Room_School_StartingArea", "Room_School_LostFriendQuest",
    };

    /// <summary>True while the player is inside an indoor quest room. The town ambience
    /// (birds and their shadows, cloud shadows, leaves, butterflies, dew) checks this and stays outside.</summary>
    public static bool Indoors { get; private set; }

    private GameObject _hiddenBackdrop;
    private bool _indoor;

    public static bool IsIndoor(string roomName)
    {
        if (string.IsNullOrEmpty(roomName)) return false;
        roomName = roomName.Replace("(Clone)", "").Trim();
        if (!roomName.StartsWith("Room_House_") && !roomName.StartsWith("Room_School_")) return false;
        foreach (string o in OutdoorRooms) if (roomName.StartsWith(o)) return false;
        return true;
    }

    /// <summary>Dress a freshly spawned room. Safe to call twice.</summary>
    public static void Dress(GameObject room)
    {
        if (room == null || room.transform.Find(ShellName) != null) return;
        if (!IsIndoor(room.name)) { DressOutdoor(room); return; }

        Tilemap ground = FindGround(room);
        if (ground == null) return;
        Sprite cap = Load("cap"), white = Load("white");
        if (cap == null || white == null) { Debug.LogWarning("[vc_RoomDressing] Missing Resources/RoomShell art."); return; }
        bool school = room.name.StartsWith("Room_School_");

        var shell = new GameObject(ShellName);
        shell.transform.SetParent(room.transform, false);
        var dressing = shell.AddComponent<vc_RoomDressing>();
        dressing._indoor = true;
        Indoors = true;

        // 1. Read the old layout. Built rooms pave a pad and leave grass around it (the grass
        //    patches use an auto-tile); a room with no auto-tile is a framed room whose whole
        //    square is floor. Grass = a green tile whose type reaches the edge of the room's
        //    square (or is the auto-tile). Green carpets never reach the edge, so they stay.
        ground.CompressBounds();
        BoundsInt gb = ground.cellBounds;
        bool paved = false;
        var edgeTypes = new HashSet<TileBase>();
        foreach (Vector3Int p in gb.allPositionsWithin)
        {
            TileBase t = ground.GetTile(p);
            if (t == null) continue;
            if (t.name.Contains("FangAuto")) { paved = true; edgeTypes.Add(t); }
            else if (p.x == gb.xMin || p.x == gb.xMax - 1 || p.y == gb.yMin || p.y == gb.yMax - 1) edgeTypes.Add(t);
        }
        var floor = new HashSet<Vector3Int>();
        var grass = new List<Vector3Int>();
        var pixels = new Dictionary<Texture2D, Color32[]>();
        var typeCount = new Dictionary<TileBase, int>();       // tile types that never reach the edge
        foreach (Vector3Int p in gb.allPositionsWithin)
        {
            TileBase t = ground.GetTile(p);
            if (t == null) continue;
            if (paved && edgeTypes.Contains(t) && Look(ground.GetSprite(p), pixels, out _)) { grass.Add(p); continue; }
            floor.Add(p);
            if (paved && edgeTypes.Contains(t)) continue;
            typeCount[t] = (typeCount.TryGetValue(t, out int n) ? n : 0) + 1;
        }
        if (floor.Count == 0) { DestroyImmediate(shell); return; }

        // The room's own colour comes from its most used inner floor tile; in a paved room that
        // tile also marks the rectangle where the quest happens (the rug).
        TileBase main = null;
        int best = 0;
        foreach (var kv in typeCount) if (kv.Value > best) { best = kv.Value; main = kv.Key; }
        Color roomColor = Color.white;
        var rug = new HashSet<Vector3Int>();
        if (main != null)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            Sprite sample = null;
            foreach (Vector3Int p in floor)
            {
                if (ground.GetTile(p) != main) continue;
                if (sample == null) sample = ground.GetSprite(p);
                x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
            }
            Look(sample, pixels, out roomColor);
            if (paved && best >= 8)
                foreach (Vector3Int p in floor)
                    if (p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1) rug.Add(p);
        }

        // 2. Relay the floor.
        foreach (Vector3Int p in grass) ground.SetTile(p, null);
        Tile[] planks = { MakeTile(Load("wood_a")), MakeTile(Load("wood_b")) };
        Tile[] tiles = { MakeTile(Load("tile_a")), MakeTile(Load("tile_b")) };
        var rugTiles = new Dictionary<string, Tile>();
        Color rugTint = Color.Lerp(Color.white, roomColor, 0.9f);
        Color tileTint = paved ? new Color(0.98f, 0.97f, 0.92f) : Color.Lerp(Color.white, roomColor, 0.7f);
        foreach (Vector3Int p in floor)
        {
            if (rug.Contains(p))
            {
                string part = (rug.Contains(p + Vector3Int.up) ? "" : "t") + (rug.Contains(p + Vector3Int.down) ? "" : "b")
                            + (rug.Contains(p + Vector3Int.left) ? "" : "l") + (rug.Contains(p + Vector3Int.right) ? "" : "r");
                if (part == "") part = "c";
                if (!rugTiles.TryGetValue(part, out Tile rt))
                {
                    Sprite s = Load("rug_" + part);
                    rt = MakeTile(s != null ? s : Load("rug_c"));
                    rugTiles[part] = rt;
                }
                Put(ground, p, rt, rugTint);
                continue;
            }
            int v = ((p.x * 7 + p.y * 13) & 3) == 0 ? 1 : 0;
            bool wood = paved && !school;
            Put(ground, p, wood ? planks[v] : tiles[v], wood ? Color.white : tileTint);
        }

        // 2b. Each room's themed floor patch (the "Theme" maps) is redrawn in the same style, in
        //     its own colour: an area rug in a paved room, coloured floor tiles in a framed one.
        foreach (Tilemap tm in room.GetComponentsInChildren<Tilemap>(true))
        {
            if (tm == ground || tm.GetComponent<Collider2D>() != null) continue;
            Transform themed = tm.transform;
            while (themed != null && themed != room.transform && themed.name != "Theme") themed = themed.parent;
            if (themed == null || themed.name != "Theme") continue;
            tm.CompressBounds();
            var cells = new HashSet<Vector3Int>();
            var spriteCount = new Dictionary<Sprite, int>();
            foreach (Vector3Int p in tm.cellBounds.allPositionsWithin)
            {
                Sprite sp = tm.GetSprite(p);
                if (sp == null) continue;
                cells.Add(p);
                spriteCount[sp] = (spriteCount.TryGetValue(sp, out int c) ? c : 0) + 1;
            }
            if (cells.Count < 6) continue;
            Sprite common = null;
            int most = 0;
            foreach (var kv in spriteCount) if (kv.Value > most) { most = kv.Value; common = kv.Key; }
            Look(common, pixels, out Color themeColor);
            Color tint = Color.Lerp(Color.white, themeColor, paved ? 0.92f : 0.8f);
            foreach (Vector3Int p in cells)
            {
                if (!paved) { Put(tm, p, tiles[((p.x * 7 + p.y * 13) & 3) == 0 ? 1 : 0], tint); continue; }
                string part = (cells.Contains(p + Vector3Int.up) ? "" : "t") + (cells.Contains(p + Vector3Int.down) ? "" : "b")
                            + (cells.Contains(p + Vector3Int.left) ? "" : "l") + (cells.Contains(p + Vector3Int.right) ? "" : "r");
                if (part == "") part = "c";
                if (!rugTiles.TryGetValue(part, out Tile rt))
                {
                    Sprite sp = Load("rug_" + part);
                    rt = MakeTile(sp != null ? sp : Load("rug_c"));
                    rugTiles[part] = rt;
                }
                Put(tm, p, rt, tint);
            }
            if (!paved) tileTint = Color.Lerp(Color.white, themeColor, 0.55f);   // the border strip matches, a shade lighter
        }
        if (!paved)
            foreach (Vector3Int p in floor) ground.SetColor(p, tileTint);

        // 3. Outdoor dressing (bushes, street lamps, park benches) goes. Solid maps are never touched.
        if (paved)
            foreach (Tilemap tm in room.GetComponentsInChildren<Tilemap>(true))
            {
                if (tm == ground || tm.GetComponent<Collider2D>() != null) continue;
                if (tm.layoutGrid != ground.layoutGrid) continue;
                Transform t = tm.transform;
                bool themed = false;
                while (t != null && t != room.transform) { if (t.name == "Theme") themed = true; t = t.parent; }
                if (!themed) tm.ClearAllTiles();
            }

        // 4. The surround: a dark sheet over the town ground, and the forest backdrop switched off.
        var groundRenderer = ground.GetComponent<TilemapRenderer>();
        var sheet = new GameObject("Surround", typeof(SpriteRenderer));
        sheet.transform.SetParent(shell.transform, false);
        sheet.transform.localScale = new Vector3(90f / white.bounds.size.x, 90f / white.bounds.size.y, 1f);
        var sr = sheet.GetComponent<SpriteRenderer>();
        sr.sprite = white;
        sr.color = VoidColor;
        sr.sortingLayerID = groundRenderer.sortingLayerID;
        sr.sortingOrder = groundRenderer.sortingOrder - 10;
        sr.sharedMaterial = groundRenderer.sharedMaterial;

        Transform town = room.transform.parent != null ? room.transform.parent.parent : null;
        Transform backdrop = town != null ? town.Find("QuestAreaBackdrop") : null;
        if (backdrop != null && backdrop.gameObject.activeSelf)
        {
            backdrop.gameObject.SetActive(false);
            dressing._hiddenBackdrop = backdrop.gameObject;
        }

        // 5. Walls around the floor's outline.
        string theme = school ? "school" : "house";
        Tile top = MakeTile(Load(theme + "_top"));
        Tile window = MakeTile(Load(theme + "_window"));
        Tile bot = MakeTile(Load(theme + "_bot"));
        Tile capTile = MakeTile(cap);
        Tile shade = MakeTile(Load("shade"));
        Tilemap walls = NewMap("Walls", ground, groundRenderer, 1);
        Tilemap shades = NewMap("WallShade", ground, groundRenderer, 1);

        var wall = new HashSet<Vector3Int>();
        foreach (Vector3Int p in floor)
        {
            Vector3Int up1 = p + Vector3Int.up, up2 = up1 + Vector3Int.up;
            if (floor.Contains(up1)) continue;
            walls.SetTile(up1, bot);
            wall.Add(up1);
            shades.SetTile(p, shade);
            if (floor.Contains(up2)) continue;
            // a window every fourth column, never on the end columns of a wall run
            bool run = !floor.Contains(up1 + Vector3Int.left) && floor.Contains(p + Vector3Int.left)
                    && !floor.Contains(up1 + Vector3Int.right) && floor.Contains(p + Vector3Int.right);
            walls.SetTile(up2, run && Mathf.Abs(p.x) % 4 == 2 ? window : top);
            wall.Add(up2);
        }
        var solid = new HashSet<Vector3Int>(floor);
        solid.UnionWith(wall);
        foreach (Vector3Int p in solid)
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector3Int q = new Vector3Int(p.x + dx, p.y + dy, p.z);
                    if (!solid.Contains(q) && !walls.HasTile(q)) walls.SetTile(q, capTile);
                }
    }

    // -- outdoor rooms -----------------------------------------------------

    private class Dapple { public Transform t; public Vector3 home; public float phase; }
    private readonly List<Dapple> _dapples = new List<Dapple>();

    /// <summary>
    /// Forest, river and fair rooms: the themed floor patch becomes a raised plank stage, soft
    /// patches of morning sun drift over the ground.
    /// </summary>
    private static void DressOutdoor(GameObject room)
    {
        Tilemap ground = FindGround(room);
        if (ground == null) return;
        var shell = new GameObject(ShellName);
        shell.transform.SetParent(room.transform, false);
        var dressing = shell.AddComponent<vc_RoomDressing>();
        var groundRenderer = ground.GetComponent<TilemapRenderer>();

        // the stage
        var deck = new Dictionary<string, Tile>();
        foreach (Tilemap tm in room.GetComponentsInChildren<Tilemap>(true))
        {
            if (tm == ground || tm.GetComponent<Collider2D>() != null || !UnderTheme(tm.transform, room.transform)) continue;
            tm.CompressBounds();
            var cells = new HashSet<Vector3Int>();
            foreach (Vector3Int p in tm.cellBounds.allPositionsWithin) if (tm.HasTile(p)) cells.Add(p);
            if (cells.Count < 12) continue;
            tm.color = Color.white;                    // the old patch was tinted
            foreach (Vector3Int p in cells)
            {
                string part = (cells.Contains(p + Vector3Int.up) ? "" : "t") + (cells.Contains(p + Vector3Int.down) ? "" : "b")
                            + (cells.Contains(p + Vector3Int.left) ? "" : "l") + (cells.Contains(p + Vector3Int.right) ? "" : "r");
                if (part == "") part = "c";
                if (!deck.TryGetValue(part, out Tile t))
                {
                    Sprite sp = Load("deck_" + part);
                    t = MakeTile(sp != null ? sp : Load("deck_c"));
                    deck[part] = t;
                }
                Put(tm, p, t, Color.white);
            }
        }

        // sun dapples
        Sprite dapple = Load("dapple");
        if (dapple != null)
        {
            var rng = new System.Random(room.name.Length * 31 + 7);
            for (int i = 0; i < 9; i++)
            {
                var go = new GameObject("SunPatch", typeof(SpriteRenderer));
                go.transform.SetParent(shell.transform, false);
                var home = new Vector3((float)(rng.NextDouble() * 13.0 - 6.5), (float)(rng.NextDouble() * 12.0 - 6.0), 0f);
                home.z = -0.01f;                       // just above the ground maps of the same order
                go.transform.localPosition = home;
                float sc = 1f + (float)rng.NextDouble() * 1.2f;
                go.transform.localScale = new Vector3(sc, sc, 1f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = dapple;
                sr.color = new Color(1f, 0.96f, 0.72f, 0.13f);
                sr.sortingLayerID = groundRenderer.sortingLayerID;
                sr.sortingOrder = FloorTopOrder(room, ground);
                sr.sharedMaterial = groundRenderer.sharedMaterial;
                dressing._dapples.Add(new Dapple { t = go.transform, home = home, phase = (float)rng.NextDouble() * 6.28f });
            }
        }
    }

    private void Update()
    {
        // leaves moving overhead: the sun patches sway a little, in whole pixels
        for (int i = 0; i < _dapples.Count; i++)
        {
            Dapple d = _dapples[i];
            if (d.t == null) continue;
            float x = d.home.x + Mathf.Sin(Time.time * 0.5f + d.phase) * 0.25f;
            float y = d.home.y + Mathf.Cos(Time.time * 0.37f + d.phase * 1.7f) * 0.12f;
            d.t.localPosition = new Vector3(Mathf.Round(x * 16f) / 16f, Mathf.Round(y * 16f) / 16f, d.home.z);
        }
    }

    private static bool UnderTheme(Transform t, Transform room)
    {
        while (t != null && t != room) { if (t.name == "Theme") return true; t = t.parent; }
        return false;
    }

    /// <summary>The highest sorting order used by the room floor maps (shadows sit right on top of it).</summary>
    private static int FloorTopOrder(GameObject room, Tilemap ground)
    {
        var gr = ground.GetComponent<TilemapRenderer>();
        int top = gr.sortingOrder;
        foreach (TilemapRenderer r in room.GetComponentsInChildren<TilemapRenderer>(true))
            if (r.sortingLayerID == gr.sortingLayerID && r.GetComponent<Collider2D>() == null) top = Mathf.Max(top, r.sortingOrder);
        return top;
    }

    private static void Put(Tilemap map, Vector3Int p, Tile tile, Color tint)
    {
        map.SetTile(p, tile);
        map.SetTileFlags(p, TileFlags.None);
        map.SetTransformMatrix(p, Matrix4x4.identity);   // old cells were sometimes rotated or flipped
        map.SetColor(p, tint);
    }

    private static Sprite Load(string name) => Resources.Load<Sprite>("RoomShell/" + name);

    private static Tilemap NewMap(string name, Tilemap ground, TilemapRenderer like, int orderOffset)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(ground.transform.parent, false);
        go.transform.localPosition = ground.transform.localPosition;
        var r = go.GetComponent<TilemapRenderer>();
        r.sortingLayerID = like.sortingLayerID;
        r.sortingOrder = like.sortingOrder + orderOffset;
        r.sharedMaterial = like.sharedMaterial;
        return go.GetComponent<Tilemap>();
    }

    private static Tilemap FindGround(GameObject room)
    {
        Tilemap best = null;
        foreach (Tilemap tm in room.GetComponentsInChildren<Tilemap>(true))
        {
            if (tm.GetComponent<Collider2D>() != null) continue;
            if (tm.name == "Tilemap") return tm;
            if (best == null) best = tm;
        }
        return best;
    }

    /// <summary>
    /// Reads a sprite's average colour (works on non-readable textures) and returns true when
    /// most of it is grass-green.
    /// </summary>
    private static bool Look(Sprite s, Dictionary<Texture2D, Color32[]> cache, out Color average)
    {
        average = Color.white;
        if (s == null || s.texture == null) return false;
        Texture2D tex = s.texture;
        if (!cache.TryGetValue(tex, out Color32[] px))
        {
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            px = copy.GetPixels32();
            DestroyImmediate(copy);
            cache[tex] = px;
        }
        Rect r = s.textureRect;
        int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height, green = 0, seen = 0;
        float sr = 0f, sg = 0f, sb = 0f;
        for (int y = y0; y < y0 + h; y += 2)
            for (int x = x0; x < x0 + w; x += 2)
            {
                Color32 c = px[y * tex.width + x];
                if (c.a < 128) continue;
                seen++;
                sr += c.r; sg += c.g; sb += c.b;
                if (c.g > c.r * 1.15f && c.g > c.b * 1.25f) green++;
            }
        if (seen == 0) return false;
        average = new Color(sr / seen / 255f, sg / seen / 255f, sb / seen / 255f, 1f);
        return green * 2 > seen;
    }

    private static Tile MakeTile(Sprite s)
    {
        var t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = s;
        t.colliderType = Tile.ColliderType.None;
        t.flags = TileFlags.None;      // so a cell can be tinted
        return t;
    }

    private void OnDestroy()
    {
        if (_hiddenBackdrop != null) _hiddenBackdrop.SetActive(true);
        if (_indoor) Indoors = false;
    }
}
