using UnityEditor;
using UnityEngine;

/// <summary>
/// Art rework: a few decor sprites in room prefabs were stretched wider than tall (an oval rug
/// made from the round one, a curtain pulled into a valance), which stretches their pixels.
/// This swaps each of them for a sprite drawn at that shape (Tools/art/make_props.py,
/// STRETCHED) and sets its scale back to 1, so it covers exactly the same area as before.
/// Only plain decor is touched: no children, no colliders, not referenced by quest scripts.
/// Safe to run twice (an object already at scale 1 with the new sprite is skipped).
/// Menu: SUNOD > Art > Apply Stretched Prop Sprites.
/// </summary>
public static class vc_ArtStretchedProps
{
    private const string Sprites = "Assets/Art/Generated/Quests/";

    private struct Swap
    {
        public string prefab, objectName, oldSprite, newSprite;
        public Vector2 scale;
        public Swap(string p, string o, string a, string b, float x, float y) { prefab = p; objectName = o; oldSprite = a; newSprite = b; scale = new Vector2(x, y); }
    }

    private static readonly Swap[] Swaps =
    {
        new Swap("Room_House_HotelLobby", "Decor_Rug_Hall", "rug_round", "rug_oval", 3.2f, 2.2f),
        new Swap("Room_House_OpenHouse", "Decor_Rug_Hall", "rug_round", "rug_oval", 3.2f, 2.2f),
        new Swap("Room_House_WalkLolaHome", "Decor_Rug_Hall", "rug_round", "rug_oval", 3.2f, 2.2f),
        new Swap("Room_House_HotelLobby", "Decor_Carpet", "rug_round", "carpet_runner", 7.5f, 1.6f),
        new Swap("Room_School_OpeningNight", "Decor_Valance", "curtain", "curtain_valance", 8f, 0.6f),
        new Swap("Room_School_OpeningNight", "Prop_CurtainClosed_L", "curtain", "curtain_tall", 2.3f, 3.9f),
        new Swap("Room_School_OpeningNight", "Prop_CurtainClosed_R", "curtain", "curtain_tall", 2.3f, 3.9f),
    };

    [MenuItem("SUNOD/Art/Apply Stretched Prop Sprites")]
    public static void ApplyFromMenu() => Debug.Log(Apply());

    /// <summary>Returns a one-line report. Does nothing in Play mode.</summary>
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "[ArtStretchedProps] Stop Play mode first.";
        AssetDatabase.Refresh();
        int done = 0, skipped = 0, missing = 0;
        foreach (Swap s in Swaps)
        {
            string path = "Assets/Prefabs/Rooms/" + s.prefab + ".prefab";
            Sprite fresh = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + s.newSprite + ".png");
            if (fresh == null || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { missing++; continue; }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.name != s.objectName || sr.sprite == null) continue;
                Transform t = sr.transform;
                bool plain = t.childCount == 0 && t.GetComponent<Collider2D>() == null;
                bool stretched = Mathf.Abs(t.localScale.x - s.scale.x) < 0.02f && Mathf.Abs(t.localScale.y - s.scale.y) < 0.02f;
                if (sr.sprite.name != s.oldSprite || !plain || !stretched) { skipped++; continue; }
                // the new sprite is the old one's size times the old scale, so the area on screen is the same
                sr.sprite = fresh;
                t.localScale = new Vector3(1f, 1f, t.localScale.z);
                changed = true;
                done++;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        return $"[ArtStretchedProps] swapped {done}, already done or not matching {skipped}, missing {missing}";
    }
}
