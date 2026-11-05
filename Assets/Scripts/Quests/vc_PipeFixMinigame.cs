using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// R6 Aling Rosa - Leaking faucet (MINIGAME_DESIGN.md, type B Connect Path), "New" gameplay only;
/// the old vc_BrokenPumpQuest is untouched. FLOWING WATER version:
///   - A 4x4 grid of pipe pieces between the valve (left of the top-left cell) and the faucet
///     (right of the bottom-right cell). Some pieces are turned wrong.
///   - 3-2-1, then the water starts and keeps moving, slowly filling one piece at a time.
///   - Tap pieces to turn them 90 degrees, ahead of the water. A piece the water is in can't turn.
///   - If the water reaches a piece that doesn't connect, it leaks there and stops. If it reaches
///     the faucet, done. The stop is a leak, never a fail screen.
///   - Score = pieces of the right path the water filled (100% if it reaches the faucet).
/// Helpers: Hint = the next wrong piece ahead glows; Free step = one piece turns into place;
/// Breather = the water pauses 5 s; Second chance = one leak gets plugged for a few seconds so you
/// can turn the piece and the water keeps going.
/// A new random layout every time it's played (shuffled each loop).
/// </summary>
[DisallowMultipleComponent]
public class vc_PipeFixMinigame : vc_MinigameQuest
{
    [Header("Pump art (same as vc_BrokenPumpQuest)")]
    [SerializeField] private SpriteRenderer pump;
    [SerializeField] private Sprite pumpBroken;
    [SerializeField] private Sprite pumpFixed;
    [SerializeField] private Sprite waterDrop;

    [Header("Minigame")]
    [SerializeField] private int gridSize = 4;
    private const float fillSeconds = 2.2f;   // seconds per pipe piece (Elementary = slow)
    private const float headStart = 3f;       // extra seconds before the first piece fills
    private const int cellPx = 80;

    private const int N = 1, E = 2, S = 4, W = 8;
    private static readonly int[] Dirs = { N, E, S, W };
    private const int Straight = N | S, Corner = N | E, Tee = N | E | S;

    private static readonly Color Slot = new Color(0.30f, 0.25f, 0.27f);
    private static readonly Color Pipe = new Color(0.62f, 0.60f, 0.66f);
    private static readonly Color Water = new Color(0.30f, 0.58f, 0.95f);

    private class Cell
    {
        public int Base, Rot, SolutionMask;
        public bool OnPath, Locked, Wet;
        public float Fill;
        public VisualElement El;
        public VisualElement[] Arms = new VisualElement[4];
        public VisualElement Hub;
        public int Mask => RotateMask(Base, Rot);
    }

    private Cell[,] _cells;
    private readonly List<Vector2Int> _path = new List<Vector2Int>();
    private bool _flowing, _done;
    private int _plugs;
    private float _pauseUntil;
    private Label _leak;
    private float _dripClock;

    protected override string Code => "R6";
    protected override string QuestName => "Broken Pump";
    protected override string Intro => "Aling Rosa's pipes are leaking and the water is on! Turn the pipes before the water gets there.";
    protected override string CardTitle => "Race the water to the faucet";

    protected override Vector3 BarWorldPosition => (pump != null ? pump.transform.position : transform.position) + new Vector3(-0.5f, 1.4f, 0f);

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

    protected override IEnumerator Play()
    {
        if (pump != null && pumpBroken != null) pump.sprite = pumpBroken;
        _done = false; _plugs = 0; _pauseUntil = 0f; _flowing = false;
        Generate();

        UI.SetMessage("Tap a pipe to turn it. Get the path ready before the water arrives!", vc_MinigameOverlay.Ink);
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.FlexStart;
        UI.Content.Add(row);
        var left = new VisualElement();
        var side = new VisualElement();
        side.style.width = 340; side.style.marginLeft = 16; side.style.alignItems = Align.Center;
        row.Add(left); row.Add(side);
        BuildGrid(left);
        BuildHelpers(side);
        HelperBar?.Refresh();

        // 3-2-1 (pipes can already be turned)
        _flowing = true;
        HelperBar?.Refresh();
        for (int c = 3; c >= 1; c--) { UI.SetStatus($"Water starts in {c}..."); yield return new WaitForSeconds(1f); }
        UI.SetStatus("The water is on!");
        SunodGame.Core.vc_Sfx.Play("splash", 0.5f);

        // flow: enter (0,0) from the valve side (W)
        Vector2Int at = Vector2Int.zero;
        int from = W;
        bool reachedTap = false;
        var visited = new HashSet<Vector2Int>();
        float wait = headStart;
        while (true)
        {
            while (wait > 0f) { if (Time.time >= _pauseUntil) wait -= Time.deltaTime; yield return null; }

            // can the water get into this cell?
            Cell cell = InGrid(at) ? _cells[at.x, at.y] : null;
            bool ok = cell != null && !visited.Contains(at) && (cell.Mask & from) != 0;
            if (!ok)
            {
                if (_plugs > 0)
                {
                    _plugs--;
                    ShowLeak(at, from, true);
                    UI.SetMessage("Second chance! The leak is plugged for 4 seconds. Turn that pipe!", vc_MinigameOverlay.Good);
                    float hold = 4f;
                    while (hold > 0f) { if (Time.time >= _pauseUntil) hold -= Time.deltaTime; yield return null; }
                    HideLeak();
                    continue; // try again with whatever the player turned
                }
                ShowLeak(at, from, false);
                SunodGame.Core.vc_Sfx.Play("splash", 0.7f);
                break;
            }

            // fill this piece
            cell.Wet = true;
            visited.Add(at);
            float t = 0f;
            while (t < fillSeconds)
            {
                if (Time.time >= _pauseUntil) t += Time.deltaTime;
                cell.Fill = Mathf.Clamp01(t / fillSeconds);
                Paint(cell);
                yield return null;
            }
            cell.Fill = 1f;
            HelperBar?.Refresh(); // e.g. Hint / Free step run out once the path ahead is right
            Paint(cell);

            int exit = ExitOf(cell.Mask, from, at, visited);
            if (at == new Vector2Int(gridSize - 1, gridSize - 1) && exit == E) { reachedTap = true; break; }
            if (exit == 0) { ShowLeak(at, Opposite(from), false); break; } // dead end: leaks out of this piece
            at += Step(exit);
            from = Opposite(exit);
            wait = 0f;
        }
        _flowing = false;
        HelperBar?.Refresh();

        int onPath = 0;
        foreach (Vector2Int p in _path) if (_cells[p.x, p.y].Wet) onPath++;
        Score = reachedTap ? 100f : 100f * onPath / Mathf.Max(1, _path.Count);

        UI.SetMessage(reachedTap ? "The water reaches the faucet!" : $"Leak! The water got {Mathf.RoundToInt(Score)}% of the way.",
                      reachedTap ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Bad);
        yield return new WaitForSeconds(1.8f);

        _done = true;
        if (Score >= 70f && pump != null && pumpFixed != null)
        {
            pump.sprite = pumpFixed;
            SunodGame.Core.vc_Sfx.Play("step_done", 0.7f);
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "No more drip-drip-drip! The water flows to the faucet." :
        score >= 50f ? "Most of the water gets through. Aling Rosa's faucet drips less." :
        "Only a little water gets through, but it's a start.";

    // ── Puzzle ───────────────────────────────────────────────────────────

    private static int RotateMask(int m, int times)
    {
        for (int i = 0; i < (times & 3); i++) m = ((m << 1) | (m >> 3)) & 15;
        return m;
    }

    private static int Opposite(int d) => d == N ? S : d == S ? N : d == E ? W : E;
    private static Vector2Int Step(int d) => d == N ? new Vector2Int(0, -1) : d == S ? new Vector2Int(0, 1) : d == E ? new Vector2Int(1, 0) : new Vector2Int(-1, 0);
    private bool InGrid(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < gridSize && p.y < gridSize;

    /// <summary>Where the water leaves a piece it entered from 'from'. For a T piece it prefers
    /// an exit that leads somewhere connected; 0 = nowhere to go.</summary>
    private int ExitOf(int mask, int from, Vector2Int at, HashSet<Vector2Int> visited)
    {
        if (at == new Vector2Int(gridSize - 1, gridSize - 1) && from != E && (mask & E) != 0) return E; // the faucet
        int best = 0;
        foreach (int d in Dirs)
        {
            if (d == from || (mask & d) == 0) continue;
            Vector2Int nb = at + Step(d);
            bool connects = InGrid(nb) && !visited.Contains(nb) && (_cells[nb.x, nb.y].Mask & Opposite(d)) != 0;
            if (connects) return d;
            if (best == 0) best = d;
        }
        return best;
    }

    private void Generate()
    {
        int n = gridSize;
        _cells = new Cell[n, n];
        _path.Clear();

        var visited = new bool[n, n];
        var goal = new Vector2Int(n - 1, n - 1);
        bool Dfs(Vector2Int p)
        {
            visited[p.x, p.y] = true;
            _path.Add(p);
            if (p == goal) return _path.Count >= n * 2 - 1;
            var order = new List<int>(Dirs);
            Shuffle(order);
            foreach (int d in order)
            {
                Vector2Int q = p + Step(d);
                if (InGrid(q) && !visited[q.x, q.y] && Dfs(q)) return true;
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

    // ── Grid UI ──────────────────────────────────────────────────────────

    private VisualElement _gridEl;

    private void BuildGrid(VisualElement parent)
    {
        int n = gridSize, c = cellPx;
        var wrap = new VisualElement();
        wrap.style.width = n * c + 120;
        wrap.style.height = n * c;
        wrap.style.flexShrink = 0;
        parent.Add(wrap);

        _gridEl = new VisualElement();
        _gridEl.style.position = Position.Absolute;
        _gridEl.style.left = 60; _gridEl.style.top = 0;
        _gridEl.style.width = n * c; _gridEl.style.height = n * c;
        wrap.Add(_gridEl);

        AddEndMarker(wrap, "VALVE", 0, c / 2f - 18f);
        AddEndMarker(wrap, "FAUCET", 60 + n * c + 4, (n - 1) * c + c / 2f - 18f);

        _leak = vc_MinigameOverlay.MakeLabel("LEAK!", 16);
        _leak.style.position = Position.Absolute;
        _leak.style.width = 64; _leak.style.height = 26;
        _leak.style.backgroundColor = vc_MinigameOverlay.Bad;
        _leak.style.color = Color.white;
        _leak.style.display = DisplayStyle.None;

        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                Cell cell = _cells[x, y];
                var el = new VisualElement();
                el.style.position = Position.Absolute;
                el.style.left = x * c + 2; el.style.top = y * c + 2;
                el.style.width = c - 4; el.style.height = c - 4;
                el.style.backgroundColor = Slot;
                Border(el, 0f, Gold);
                int arm = c / 3;
                cell.Hub = Block(el, (c - 4 - arm) / 2f, (c - 4 - arm) / 2f, arm, arm);
                float half = (c - 4) / 2f;
                cell.Arms[0] = Block(el, (c - 4 - arm) / 2f, 0, arm, half);
                cell.Arms[1] = Block(el, half, (c - 4 - arm) / 2f, half, arm);
                cell.Arms[2] = Block(el, (c - 4 - arm) / 2f, half, arm, half);
                cell.Arms[3] = Block(el, 0, (c - 4 - arm) / 2f, half, arm);
                Cell captured = cell;
                el.RegisterCallback<PointerDownEvent>(_ => TapCell(captured));
                _gridEl.Add(el);
                cell.El = el;
                Paint(cell);
            }
        _gridEl.Add(_leak);
    }

    private void AddEndMarker(VisualElement wrap, string text, float left, float top)
    {
        var l = vc_MinigameOverlay.MakeLabel(text, 13);
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

    private void Paint(Cell cell)
    {
        int m = cell.Mask;
        Color col = cell.Wet ? Color.Lerp(Pipe, Water, 0.35f + 0.65f * cell.Fill) : Pipe;
        cell.Hub.style.backgroundColor = col;
        for (int i = 0; i < 4; i++)
        {
            cell.Arms[i].style.display = (m & Dirs[i]) != 0 ? DisplayStyle.Flex : DisplayStyle.None;
            cell.Arms[i].style.backgroundColor = col;
        }
        if (cell.Locked) Border(cell.El, 4f, vc_MinigameOverlay.Good);
        else if (cell.Wet) Border(cell.El, 3f, Water);
    }

    private void TapCell(Cell cell)
    {
        if (_done || cell.Locked || cell.Wet) return;
        cell.Rot = (cell.Rot + 1) & 3;
        SunodGame.Core.vc_Sfx.Play("clack", 0.35f, 0.1f);
        Paint(cell);
    }

    private void ShowLeak(Vector2Int at, int from, bool plugged)
    {
        // the leak sits on the edge the water was trying to cross
        Vector2Int src = at + Step(from);
        Vector2 a = new Vector2(src.x + 0.5f, src.y + 0.5f), b = new Vector2(at.x + 0.5f, at.y + 0.5f);
        Vector2 mid = (a + b) * 0.5f * cellPx;
        _leak.text = plugged ? "PLUGGED" : "LEAK!";
        _leak.style.backgroundColor = plugged ? new Color(0.85f, 0.60f, 0.15f) : vc_MinigameOverlay.Bad;
        _leak.style.left = Mathf.Clamp(mid.x - 32f, -50f, gridSize * cellPx - 14f);
        _leak.style.top = Mathf.Clamp(mid.y - 13f, 0f, gridSize * cellPx - 26f);
        _leak.style.display = DisplayStyle.Flex;
        _leak.BringToFront();
    }

    private void HideLeak() => _leak.style.display = DisplayStyle.None;

    // ── Skill helpers ────────────────────────────────────────────────────

    private Cell FirstWrongDryPathCell()
    {
        foreach (Vector2Int p in _path)
        {
            Cell c = _cells[p.x, p.y];
            if (!c.Locked && !c.Wet && c.Mask != c.SolutionMask) return c;
        }
        return null;
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e)
    {
        if (!_flowing) return false;
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
            case vc_SkillHelpers.Effect.FreeStep: return FirstWrongDryPathCell() != null;
            case vc_SkillHelpers.Effect.Breather: return Time.time >= _pauseUntil;
            default: return true;
        }
    }

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
            {
                Cell c = FirstWrongDryPathCell();
                Border(c.El, 5f, Gold);
                c.El.schedule.Execute(() => { if (!c.Locked && !c.Wet) Border(c.El, 0f, Gold); }).StartingIn(3500);
                UI.SetMessage($"{s.DisplayName}: the glowing pipe ahead is turned wrong.", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.FreeStep:
            {
                Cell c = FirstWrongDryPathCell();
                for (int r = 0; r < 4; r++) { c.Rot = r; if (c.Mask == c.SolutionMask) break; }
                c.Locked = true;
                Paint(c);
                UI.SetMessage($"{s.DisplayName}: one pipe set in place for you.", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.SecondChance:
                _plugs++;
                UI.SetMessage($"{s.DisplayName}: if the water leaks, it gets plugged so you can fix it.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                _pauseUntil = Time.time + 5f;
                UI.SetMessage($"{s.DisplayName}: the water holds still for 5 seconds.", vc_MinigameOverlay.Good);
                break;
        }
    }
}
