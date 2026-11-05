using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// R quest (R6), minigame version of vc_BrokenPumpQuest ("New" gameplay, see vc_GameplayMode):
/// "Connect the pipes" (MINIGAME_DESIGN.md, type B).
///   - A 4x4 grid of pipe pieces between the pump (left of the top-left cell) and the tap
///     (right of the bottom-right cell). Tap a piece to turn it 90 degrees.
///   - "Turn on water" tests it once: water runs as far as the pipes connect.
///   - Score = pieces of the right path the water reached (100% if it reaches the tap).
///     90/70/50/30 = 5/4/3/2 stars, else 1. No timer.
///   - Skills are HELPERS here (vc_SkillHelpers): Hint = one wrong pipe glows, Free step = one
///     wrong pipe turns into place and locks, Check first = preview the water without ending,
///     Second chance = if the test isn't perfect, one more go.
/// A new layout every time it's played (shuffled each loop).
/// </summary>
[DisallowMultipleComponent]
public class vc_PipeFixMinigame : vc_FitQuestBase
{
    [Header("Pump art (same as vc_BrokenPumpQuest)")]
    [SerializeField] private SpriteRenderer pump;
    [SerializeField] private Sprite pumpBroken;
    [SerializeField] private Sprite pumpFixed;
    [SerializeField] private Sprite waterDrop;

    [Header("Minigame")]
    [SerializeField] private int gridSize = 4;
    private const int cellPx = 80;

    private const int N = 1, E = 2, S = 4, W = 8;
    private static readonly int[] Dirs = { N, E, S, W };
    private const int Straight = N | S, Corner = N | E, Tee = N | E | S;

    private static readonly Color Slot = new Color(0.30f, 0.25f, 0.27f);
    private static readonly Color Pipe = new Color(0.62f, 0.60f, 0.66f);
    private static readonly Color Water = new Color(0.30f, 0.58f, 0.95f);
    private static readonly Color Gold = new Color(1f, 0.80f, 0.26f);

    private class Cell
    {
        public int Base, Rot, SolutionMask;
        public bool OnPath, Locked;
        public VisualElement El;
        public VisualElement[] Arms = new VisualElement[4];
        public VisualElement Hub;
        public int Mask => RotateMask(Base, Rot);
    }

    private vc_MinigameOverlay _ui;
    private Cell[,] _cells;
    private readonly List<Vector2Int> _path = new List<Vector2Int>();
    private bool _testing, _safetyNet, _done;
    private vc_SkillHelpers.Bar _bar;
    private readonly List<string> _helpersUsed = new List<string>();
    private float _dripClock;

    protected override Vector3 BarWorldPosition => (pump != null ? pump.transform.position : transform.position) + new Vector3(-0.5f, 1.4f, 0f);

    private void Awake()
    {
        _ui = GetComponent<vc_MinigameOverlay>();
        if (_ui == null) _ui = gameObject.AddComponent<vc_MinigameOverlay>();
    }

    protected override void OnBegin()
    {
        if (pump != null && pumpBroken != null) pump.sprite = pumpBroken;
        _helpersUsed.Clear();
        _safetyNet = false;
        _done = false;
        if (vc_QuestTimer.Instance != null)
        {
            vc_QuestTimer.Instance.ExternalHold = true;
            vc_QuestTimer.Instance.SetStarsVisible(false);
        }
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Broken Pump",
            "Aling Rosa's pump is leaking. Turn the pipes so the water reaches her tap.",
            new[] { "Connect the pipes" });
        StartWork(Flow());
    }

    protected override void OnSkill(vc_SkillData d) => Toast("Use your skills in the pipe panel.", 2f);

    protected override void OnWorkCancelled() => _ui?.Close();

    private void Update()
    {
        // a slow drip from the pump while it's broken
        if (!Started || Done || _done || pump == null || waterDrop == null) return;
        _dripClock += Time.deltaTime;
        if (_dripClock >= 0.9f)
        {
            _dripClock = 0f;
            Fx(new[] { waterDrop }, pump.transform.position + new Vector3(0.15f, 0.1f, 0f), 2f, -0.4f, 20);
        }
    }

    // â”€â”€ Flow â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private IEnumerator Flow()
    {
        yield return new WaitForSeconds(0.5f);
        Generate();

        _ui.Open("Fix Aling Rosa's pipes");
        _ui.SetMessage("Tap a pipe to turn it. Connect the pump to the tap.", vc_MinigameOverlay.Ink);

        // grid on the left, test button + skills on the right (fits a landscape phone)
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.FlexStart;
        _ui.Content.Add(row);
        var left = new VisualElement();
        var side = new VisualElement();
        side.style.width = 340;
        side.style.marginLeft = 16;
        side.style.alignItems = Align.Center;
        row.Add(left);
        row.Add(side);
        BuildGrid(left);

        bool test = false;
        Button go = vc_MinigameOverlay.MakeButton("Turn on water", 28, () => { if (!_testing) test = true; });
        side.Add(go);

        _bar = vc_SkillHelpers.BuildBar(side, CanUse, UseHelper);
        UpdateStatus();

        float score;
        while (true)
        {
            test = false;
            while (!test) yield return null;

            _testing = true;
            go.SetEnabled(false);
            _bar.Refresh();
            yield return RunWater(true);
            score = Score(out bool reachedTap);

            if (!reachedTap && _safetyNet)
            {
                _safetyNet = false;
                _ui.SetMessage("Second chance! Fix the pipes and try again.", vc_MinigameOverlay.Good);
                yield return new WaitForSeconds(1.4f);
                ClearWater();
                _testing = false;
                go.SetEnabled(true);
                _bar.Refresh();
                continue;
            }
            _ui.SetMessage(reachedTap ? "The water reaches the tap!" :
                $"The water stops partway ({Mathf.RoundToInt(score)}% of the way).",
                reachedTap ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Bad);
            yield return new WaitForSeconds(1.6f);
            break;
        }

        int stars = score >= 90f ? 5 : score >= 70f ? 4 : score >= 50f ? 3 : score >= 30f ? 2 : 1;
        vc_QuestTimer.Instance?.SetStarOverride(stars);
        Debug.Log($"[Minigame] R6 pipes: {Mathf.RoundToInt(score)}% -> {stars} stars; helpers used: " +
                  (_helpersUsed.Count > 0 ? string.Join(", ", _helpersUsed) : "none"));

        _ui.Close();
        _done = true;
        if (score >= 70f && pump != null && pumpFixed != null)
        {
            pump.sprite = pumpFixed;
            SunodGame.Core.vc_Sfx.Play("step_done", 0.7f);
        }
        yield return new WaitForSeconds(0.6f);
        Finish(score >= 90f ? "No more drip-drip-drip! The water flows to the tap."
             : score >= 50f ? "Most of the water gets through. Much better!"
             : "Only a little water gets through, but it's a start.");
    }

    // â”€â”€ Puzzle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static int RotateMask(int m, int times)
    {
        for (int i = 0; i < (times & 3); i++) m = ((m << 1) | (m >> 3)) & 15;
        return m;
    }

    private static int Opposite(int d) => d == N ? S : d == S ? N : d == E ? W : E;
    private static Vector2Int Step(int d) => d == N ? new Vector2Int(0, -1) : d == S ? new Vector2Int(0, 1) : d == E ? new Vector2Int(1, 0) : new Vector2Int(-1, 0);
    private bool In(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < gridSize && p.y < gridSize;

    private void Generate()
    {
        int n = gridSize;
        _cells = new Cell[n, n];
        _path.Clear();

        // random self-avoiding path from top-left to bottom-right (DFS with random order)
        var visited = new bool[n, n];
        var goal = new Vector2Int(n - 1, n - 1);
        bool Dfs(Vector2Int p)
        {
            visited[p.x, p.y] = true;
            _path.Add(p);
            if (p == goal) return _path.Count >= n * 2 - 1;   // at least a reasonable length
            var order = new List<int>(Dirs);
            for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
            foreach (int d in order)
            {
                Vector2Int q = p + Step(d);
                if (In(q) && !visited[q.x, q.y] && Dfs(q)) return true;
            }
            visited[p.x, p.y] = false;
            _path.RemoveAt(_path.Count - 1);
            return false;
        }
        Dfs(Vector2Int.zero);

        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                int b = Random.value < 0.45f ? Straight : Random.value < 0.75f ? Corner : Tee;
                _cells[x, y] = new Cell { Base = b, Rot = Random.Range(0, 4) };
            }

        // path cells: the shape that links previous -> next
        int wrong = 0;
        for (int i = 0; i < _path.Count; i++)
        {
            Vector2Int p = _path[i];
            int inDir = i == 0 ? W : DirTo(p, _path[i - 1]);
            int outDir = i == _path.Count - 1 ? E : DirTo(p, _path[i + 1]);
            int need = inDir | outDir;
            Cell c = _cells[p.x, p.y];
            c.OnPath = true;
            c.SolutionMask = need;
            c.Base = (need == (N | S) || need == (E | W)) ? Straight : Corner;
            c.Rot = Random.Range(0, 4);
            if (c.Mask != need) wrong++;
        }
        // make sure most of the path starts out wrong
        for (int i = 0; i < _path.Count && wrong < Mathf.CeilToInt(_path.Count * 0.6f); i++)
        {
            Cell c = _cells[_path[i].x, _path[i].y];
            if (c.Mask == c.SolutionMask) { c.Rot = (c.Rot + 1) & 3; wrong++; }
        }
    }

    private static int DirTo(Vector2Int from, Vector2Int to)
    {
        Vector2Int d = to - from;
        return d.y < 0 ? N : d.y > 0 ? S : d.x > 0 ? E : W;
    }

    /// <summary>Cells the water reaches, in order (breadth first from the pump).</summary>
    private List<Vector2Int> Flood(out bool reachedTap)
    {
        var order = new List<Vector2Int>();
        reachedTap = false;
        if ((_cells[0, 0].Mask & W) == 0) return order;
        var seen = new bool[gridSize, gridSize];
        var q = new Queue<Vector2Int>();
        q.Enqueue(Vector2Int.zero);
        seen[0, 0] = true;
        while (q.Count > 0)
        {
            Vector2Int p = q.Dequeue();
            order.Add(p);
            int m = _cells[p.x, p.y].Mask;
            if (p == new Vector2Int(gridSize - 1, gridSize - 1) && (m & E) != 0) reachedTap = true;
            foreach (int d in Dirs)
            {
                if ((m & d) == 0) continue;
                Vector2Int nb = p + Step(d);
                if (!In(nb) || seen[nb.x, nb.y]) continue;
                if ((_cells[nb.x, nb.y].Mask & Opposite(d)) == 0) continue;
                seen[nb.x, nb.y] = true;
                q.Enqueue(nb);
            }
        }
        return order;
    }

    private float Score(out bool reachedTap)
    {
        List<Vector2Int> wet = Flood(out reachedTap);
        if (reachedTap) return 100f;
        int onPath = 0;
        foreach (Vector2Int p in wet) if (_cells[p.x, p.y].OnPath) onPath++;
        return 100f * onPath / Mathf.Max(1, _path.Count);
    }

    // â”€â”€ Grid UI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void BuildGrid(VisualElement parent)
    {
        int n = gridSize, c = cellPx;
        var wrap = new VisualElement();
        wrap.style.width = n * c + 120;
        wrap.style.height = n * c;
        wrap.style.flexShrink = 0;
        parent.Add(wrap);

        var grid = new VisualElement();
        grid.style.position = Position.Absolute;
        grid.style.left = 60; grid.style.top = 0;
        grid.style.width = n * c; grid.style.height = n * c;
        wrap.Add(grid);

        AddEndMarker(wrap, "PUMP", 0, c / 2f - 18f);
        AddEndMarker(wrap, "TAP", 60 + n * c + 4, (n - 1) * c + c / 2f - 18f);

        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                Cell cell = _cells[x, y];
                var el = new VisualElement();
                el.style.position = Position.Absolute;
                el.style.left = x * c + 2; el.style.top = y * c + 2;
                el.style.width = c - 4; el.style.height = c - 4;
                el.style.backgroundColor = Slot;
                SetBorder(el, 0f, Gold);
                int arm = c / 3;
                cell.Hub = Block(el, (c - 4 - arm) / 2f, (c - 4 - arm) / 2f, arm, arm);
                float half = (c - 4) / 2f;
                cell.Arms[0] = Block(el, (c - 4 - arm) / 2f, 0, arm, half);            // N
                cell.Arms[1] = Block(el, half, (c - 4 - arm) / 2f, half, arm);          // E
                cell.Arms[2] = Block(el, (c - 4 - arm) / 2f, half, arm, half);          // S
                cell.Arms[3] = Block(el, 0, (c - 4 - arm) / 2f, half, arm);             // W
                Cell captured = cell;
                el.RegisterCallback<PointerDownEvent>(_ => TapCell(captured));
                grid.Add(el);
                cell.El = el;
                Paint(cell, false);
            }
    }

    private void AddEndMarker(VisualElement wrap, string text, float left, float top)
    {
        var l = vc_MinigameOverlay.MakeLabel(text, 14);
        l.style.position = Position.Absolute;
        l.style.left = left; l.style.top = top;
        l.style.width = 56; l.style.height = 36;
        l.style.backgroundColor = Water;
        l.style.color = Color.white;
        wrap.Add(l);
    }

    private static VisualElement Block(VisualElement parent, float x, float y, float w, float h)
    {
        var b = new VisualElement { pickingMode = PickingMode.Ignore };
        b.style.position = Position.Absolute;
        b.style.left = x; b.style.top = y; b.style.width = w; b.style.height = h;
        parent.Add(b);
        return b;
    }

    private static void SetBorder(VisualElement el, float w, Color c)
    {
        el.style.borderLeftWidth = w; el.style.borderRightWidth = w; el.style.borderTopWidth = w; el.style.borderBottomWidth = w;
        el.style.borderLeftColor = c; el.style.borderRightColor = c; el.style.borderTopColor = c; el.style.borderBottomColor = c;
    }

    private void Paint(Cell cell, bool wet)
    {
        int m = cell.Mask;
        Color col = wet ? Water : Pipe;
        cell.Hub.style.backgroundColor = col;
        for (int i = 0; i < 4; i++)
        {
            bool open = (m & Dirs[i]) != 0;
            cell.Arms[i].style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            cell.Arms[i].style.backgroundColor = col;
        }
        if (cell.Locked) SetBorder(cell.El, 4f, vc_MinigameOverlay.Good);
    }

    private void TapCell(Cell cell)
    {
        if (_testing || cell.Locked) return;
        cell.Rot = (cell.Rot + 1) & 3;
        SunodGame.Core.vc_Sfx.Play("clack", 0.35f, 0.1f);
        Paint(cell, false);
        UpdateStatus();
    }

    private void UpdateStatus() => _ui.SetStatus(_safetyNet ? "One water test + a second chance" : "One water test");

    private void ClearWater()
    {
        foreach (Cell c in _cells) Paint(c, false);
    }

    private IEnumerator RunWater(bool forReal)
    {
        ClearWater();
        List<Vector2Int> wet = Flood(out _);
        if (forReal) SunodGame.Core.vc_Sfx.Play("splash", 0.6f);
        foreach (Vector2Int p in wet)
        {
            Paint(_cells[p.x, p.y], true);
            yield return new WaitForSeconds(0.12f);
        }
    }

    // â”€â”€ Skill helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private Cell FirstWrongPathCell()
    {
        foreach (Vector2Int p in _path)
        {
            Cell c = _cells[p.x, p.y];
            if (!c.Locked && c.Mask != c.SolutionMask) return c;
        }
        return null;
    }

    private bool CanUse(vc_SkillHelpers.Effect e)
    {
        if (_testing) return false;
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
            case vc_SkillHelpers.Effect.FreeStep: return FirstWrongPathCell() != null;
            case vc_SkillHelpers.Effect.SecondChance: return !_safetyNet;
            default: return true;
        }
    }

    private void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        _helpersUsed.Add($"{s.skillName} ({vc_SkillHelpers.EffectName(e)})");
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
            {
                Cell c = FirstWrongPathCell();
                if (c == null) break;
                SetBorder(c.El, 5f, Gold);
                c.El.schedule.Execute(() => { if (!c.Locked) SetBorder(c.El, 0f, Gold); }).StartingIn(3500);
                _ui.SetMessage($"{s.DisplayName}: the glowing pipe is turned wrong.", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.FreeStep:
            {
                Cell c = FirstWrongPathCell();
                if (c == null) break;
                for (int r = 0; r < 4; r++) { c.Rot = r; if (c.Mask == c.SolutionMask) break; }
                c.Locked = true;
                Paint(c, false);
                _ui.SetMessage($"{s.DisplayName}: one pipe set in place for you.", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.SecondChance:
                _safetyNet = true;
                _ui.SetMessage($"{s.DisplayName}: if the water test isn't perfect, you get one more go.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                StartCoroutine(Preview(s));
                break;
        }
        UpdateStatus();
    }

    private IEnumerator Preview(vc_SkillData s)
    {
        _testing = true;
        _bar?.Refresh();
        yield return RunWater(false);
        float sc = Score(out bool tap);
        _ui.SetMessage($"{s.DisplayName}: the water would get {(tap ? "all the way!" : $"{Mathf.RoundToInt(sc)}% of the way.")}",
            vc_MinigameOverlay.Good);
        yield return new WaitForSeconds(1.2f);
        ClearWater();
        _testing = false;
        _bar?.Refresh();
    }
}
