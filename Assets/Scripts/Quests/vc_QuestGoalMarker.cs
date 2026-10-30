using UnityEngine;

/// <summary>
/// "Bring them here" marker for walk-along quest paths (Guide): a bobbing arrow over the goal
/// plus a small arrow next to the player pointing at it. Spawned and removed by
/// vc_FitQuestBase.ShowGoal / HideGoal. The arrows are drawn in code (pixel art, no asset).
/// </summary>
public class vc_QuestGoalMarker : MonoBehaviour
{
    private Transform _target;
    private Vector3 _targetPoint;
    private Transform _player;
    private SpriteRenderer _over, _pointer;
    private float _hideWithin = 1.6f;

    private static Sprite _down, _right;

    // 9x9 down arrow / 7x7 right arrow: '#' outline, 'o' fill
    private static readonly string[] DownArt =
    {
        "..#####..",
        "..#ooo#..",
        "..#ooo#..",
        "###ooo###",
        "#ooooooo#",
        ".#ooooo#.",
        "..#ooo#..",
        "...#o#...",
        "....#....",
    };

    private static readonly string[] RightArt =
    {
        "...#...",
        "...##..",
        "####o#.",
        "#ooooo#",
        "####o#.",
        "...##..",
        "...#...",
    };

    public static vc_QuestGoalMarker Create(Transform parent, Transform target, Vector3 point, string sortingLayer)
    {
        var go = new GameObject("GoalMarker");
        go.transform.SetParent(parent, true);
        var m = go.AddComponent<vc_QuestGoalMarker>();
        m._target = target;
        m._targetPoint = point;
        m._over = m.MakeRenderer("GoalArrow", Down(), sortingLayer, 40);
        m._pointer = m.MakeRenderer("GoalPointer", Right(), sortingLayer, 40);
        m._pointer.transform.localScale = new Vector3(2f, 2f, 1f);   // whole-pixel 2x so it reads at a glance
        m._over.transform.localScale = new Vector3(2f, 2f, 1f);
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) m._player = pc.transform;
        m.LateUpdate();
        return m;
    }

    private SpriteRenderer MakeRenderer(string name, Sprite s, string layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingLayerName = layer;
        sr.sortingOrder = order;
        return sr;
    }

    private Vector3 Goal()
    {
        if (_target == null) return _targetPoint;
        var sr = _target.GetComponent<SpriteRenderer>();
        return sr != null ? new Vector3(sr.bounds.center.x, sr.bounds.max.y, 0f) : _target.position + new Vector3(0f, 1f, 0f);
    }

    private void LateUpdate()
    {
        Vector3 goal = Goal();
        float bob = Mathf.Round(Mathf.Sin(Time.time * 5f) * 2f) / 16f;   // whole-pixel bob
        if (_over != null) _over.transform.position = Snap(goal + new Vector3(0f, 0.45f + bob, 0f));

        if (_pointer == null) return;
        if (_player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }
        if (_player == null) { _pointer.enabled = false; return; }

        Vector2 dir = (Vector2)(goal - _player.position);
        bool near = dir.magnitude < _hideWithin;
        _pointer.enabled = !near;
        if (near) return;

        // snap the angle to 45-degree steps so the pixel arrow stays crisp
        float ang = Mathf.Round(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg / 45f) * 45f;
        Vector3 off = Quaternion.Euler(0f, 0f, ang) * Vector3.right * (1.1f + Mathf.Abs(bob));
        _pointer.transform.position = Snap(_player.position + new Vector3(0f, 0.45f, 0f) + off);
        _pointer.transform.rotation = Quaternion.Euler(0f, 0f, ang);
    }

    private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 16f) / 16f, Mathf.Round(p.y * 16f) / 16f, 0f);

    private static Sprite Down() => _down != null ? _down : (_down = Build(DownArt, "goal_down"));
    private static Sprite Right() => _right != null ? _right : (_right = Build(RightArt, "goal_right"));

    private static Sprite Build(string[] art, string name)
    {
        int h = art.Length, w = art[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
        Color32 outline = new Color32(46, 34, 40, 255), fill = new Color32(255, 204, 66, 255), clear = new Color32(0, 0, 0, 0);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = art[h - 1 - y][x];
                tex.SetPixel(x, y, c == '#' ? outline : c == 'o' ? fill : clear);
            }
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        s.name = name;
        return s;
    }
}
