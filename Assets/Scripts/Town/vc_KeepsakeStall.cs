using UnityEngine;

/// <summary>
/// The Keepsake Shop stall, in every town, a few steps from the player's home. Walk up to it
/// and the town's Talk button turns into "Open shop" (vc_TownStage.UpdateTalkButton). A gold
/// bag bobs over it; it turns into a "!" when something new is unlocked. Placed at runtime by
/// vc_TownStage (no scene setup).
/// </summary>
public class vc_KeepsakeStall : MonoBehaviour
{

    /// <summary>Just in front of the counter (where the debug tools put the player).</summary>
    public Vector2 Front => (Vector2)transform.position + new Vector2(0f, -0.4f);

    /// <summary>How far a point is from the stall itself (0 when touching it), from any side.</summary>
    public float DistanceTo(Vector2 p)
    {
        Bounds b = default;
        bool any = false;
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>())
        {
            if (r == _sign || r.sprite == null) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return Vector2.Distance(p, Front);
        Vector3 c = b.ClosestPoint(new Vector3(p.x, p.y, b.center.z));
        return Vector2.Distance(p, c);
    }

    private SpriteRenderer _sign;
    private Sprite _bag, _new;
    private Vector3 _signBase;

    /// <summary>Puts the stall near the town's home (on a free spot) and returns it.</summary>
    public static vc_KeepsakeStall Place(vc_TownStage stage, Vector3 near, Sprite newMarker)
    {
        GameObject prefab = Resources.Load<GameObject>("Loop/keepsake_stall");
        if (prefab == null) return null;

        // Spots picked by eye per town (open grass near home, off the paths). Other towns: the
        // first nearby candidate whose whole footprint is walkable.
        vc_TownGrid grid = vc_TownGrid.For(stage.transform);
        Vector3 want = near + new Vector3(4.8f, -1.2f, 0f);
        Vector2? picked = stage.stageId switch
        {
            "elementary" => new Vector2(5.5f, 1.5f),
            "highschool" => new Vector2(5.7f, -0.9f),
            "college" => new Vector2(4.3f, 1.4f),
            _ => (Vector2?)null,
        };
        // planned towns (2026-11-07): the builder leaves a clear "stall" spot near home, off the streets
        Transform planned = stage.transform.Find("Spots")?.Find("stall");
        if (planned != null) want = planned.position;
        else if (picked.HasValue) want = near + (Vector3)picked.Value;
        else if (grid != null)
        {
            Vector2[] offsets =
            {
                new Vector2(4.8f, -1.2f), new Vector2(-4.8f, -1.2f), new Vector2(0f, -3.2f),
                new Vector2(5.5f, 1.5f), new Vector2(-5.5f, 1.5f), new Vector2(3f, -3.4f), new Vector2(-3f, -3.4f),
            };
            foreach (Vector2 o in offsets)
            {
                Vector3 c = near + (Vector3)o;
                if (FootprintClear(grid, c)) { want = c; break; }
            }
        }

        GameObject go = Instantiate(prefab, stage.transform);
        go.name = "KeepsakeStall";
        go.transform.position = new Vector3(Mathf.Round(want.x * 16f) / 16f, Mathf.Round(want.y * 16f) / 16f, near.z);
        var stall = go.AddComponent<vc_KeepsakeStall>();
        stall.Build(newMarker);
        // the town's walk map is built from colliders: rebuild it so paths go around the stall
        vc_TownGrid.Forget(stage.transform);
        return stall;
    }

    private static bool FootprintClear(vc_TownGrid grid, Vector3 bottomCenter)
    {
        for (float x = -2f; x <= 2f; x += 0.5f)
            for (float y = -1f; y <= 2.2f; y += 0.5f)
                if (!grid.IsWalkable(bottomCenter + new Vector3(x, y, 0f))) return false;
        return true;
    }

    private void Build(Sprite newMarker)
    {
        Texture2D tex = Resources.Load<Texture2D>("Loop/stall_sign");
        if (tex != null)
        {
            tex.filterMode = FilterMode.Point;
            _bag = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 16f);
        }
        _new = newMarker != null ? newMarker : _bag;

        var sign = new GameObject("Sign");
        sign.transform.SetParent(transform, false);
        _signBase = new Vector3(0f, 3.1f, 0f);
        sign.transform.localPosition = _signBase;
        _sign = sign.AddComponent<SpriteRenderer>();
        _sign.sprite = _bag;
        // above every part of the stall (the roof is its own, higher sprite)
        int top = int.MinValue;
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>())
            if (r != _sign && r.sortingOrder > top) { top = r.sortingOrder; _sign.sortingLayerID = r.sortingLayerID; }
        _sign.sortingOrder = (top == int.MinValue ? 0 : top) + 50;
    }

    private void Update()
    {
        if (_sign != null)
        {
            _sign.sprite = vc_LoopMeta.HasNewInShop() ? _new : _bag;
            float bob = Mathf.Round(Mathf.Sin(Time.time * 3f) * 1.5f) / 16f;
            _sign.transform.localPosition = _signBase + new Vector3(0f, bob, 0f);
        }
    }
}
