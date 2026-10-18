using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Walk / idle / sit frames for town people, sliced at runtime from the sheets baked by
/// Tools/bake_npc_sheets.py into Resources/TownLife (walk_&lt;look&gt;, idle_&lt;look&gt;, sit_&lt;look&gt;).
/// A look is "adam_1" (a quest giver's palette) or "town0".."town7" (extra townsfolk).
/// Sheet order: right, up, left, down - 6 frames each. Sit sheets: right 6, left 6 (32px wide).
/// </summary>
public static class vc_NpcSprites
{
    public enum Dir { Right = 0, Up = 1, Left = 2, Down = 3 }

    /// <summary>Same scale as the town builder's NPC frames (22 px per unit, bottom-center pivot).</summary>
    public const float Ppu = 22f;
    public static readonly string[] TownsfolkLooks = { "town0", "town1", "town2", "town3", "town4", "town5", "town6", "town7" };

    private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();

    /// <summary>"npc_adam_1_f0" -> "adam_1" (null if the name isn't a baked NPC frame).</summary>
    public static string LookFromSprite(Sprite s)
    {
        if (s == null || !s.name.StartsWith("npc_")) return null;
        string n = s.name.Substring(4);
        int f = n.LastIndexOf("_f");
        return f > 0 ? n.Substring(0, f) : null;
    }

    public static Sprite[] Walk(string look, Dir dir) => Slice("walk", look, (int)dir * 6, 6, 16);
    public static Sprite[] Idle(string look, Dir dir) => Slice("idle", look, (int)dir * 6, 6, 16);
    /// <summary>Sitting, facing right or left.</summary>
    public static Sprite[] Sit(string look, bool faceRight) => Slice("sit", look, faceRight ? 0 : 6, 6, 32);

    public static Dir DirFor(Vector2 v)
    {
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return v.x >= 0f ? Dir.Right : Dir.Left;
        return v.y >= 0f ? Dir.Up : Dir.Down;
    }

    private static Sprite[] Slice(string kind, string look, int first, int count, int frameWidth)
    {
        if (string.IsNullOrEmpty(look)) return null;
        string key = $"{kind}_{look}_{first}";
        if (Cache.TryGetValue(key, out Sprite[] cached)) return cached;

        Texture2D tex = Resources.Load<Texture2D>($"TownLife/{kind}_{look}");
        Sprite[] frames = null;
        if (tex != null)
        {
            tex.filterMode = FilterMode.Point;
            frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect((first + i) * frameWidth, 0, frameWidth, tex.height);
                if (rect.xMax > tex.width) { frames = null; break; }
                // pivot at the feet, like the baked NPC frames
                frames[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
                frames[i].name = $"{kind}_{look}_{first + i}";
            }
        }
        Cache[key] = frames;
        return frames;
    }
}
