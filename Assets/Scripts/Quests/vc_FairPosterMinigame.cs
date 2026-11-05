using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A3 Ms. Dela Cruz - Fair poster (MINIGAME_DESIGN.md, type G Make to a Brief). New gameplay only.
/// A blank poster and a brief ("show the event's name, the date, a picture of it, easy to read from
/// far"). Tap tray pieces to add them, drag them around the poster, tap one then Remove to take it off.
/// Checklist, 20% each: has the name; has the date; has a fitting picture; nothing off the edge or
/// overlapping; title is the large size. Colours and layout are free (never scored). No timer.
/// Helpers: Hint = names one unmet checklist item; Free step = one unmet item is done for you;
/// Second chance = undo the last change; Breather = check the checklist before pinning it up.
/// </summary>
[DisallowMultipleComponent]
public class vc_FairPosterMinigame : vc_MinigameQuest
{
    private const float PW = 420f, PH = 270f;

    private class Brief { public string Name, Date, RightPic; }
    private static readonly Brief[] Briefs =
    {
        new Brief { Name = "School Fair", Date = "Sat, March 8", RightPic = "Ferris wheel" },
        new Brief { Name = "Science Day", Date = "Fri, Aug 22", RightPic = "Volcano model" },
        new Brief { Name = "Sports Fest", Date = "Mon, Oct 13", RightPic = "Running kids" },
    };
    private static readonly string[] Pictures = { "Ferris wheel", "Volcano model", "Running kids", "Balloons", "A random cat" };
    private static readonly Color[] Bgs = { new Color(1f, 0.95f, 0.75f), new Color(0.80f, 0.90f, 1f), new Color(1f, 0.85f, 0.85f), new Color(0.85f, 0.95f, 0.80f) };

    private enum Kind { Title, Date, Pic }
    private class Piece { public Kind Kind; public int Size; public string Text; public Rect R; public VisualElement El; }

    private Brief _brief;
    private VisualElement _poster;
    private readonly List<Piece> _pieces = new List<Piece>();
    private readonly Stack<List<(Kind, int, string, Rect)>> _undo = new Stack<List<(Kind, int, string, Rect)>>();
    private Piece _selected, _dragging;
    private Vector2 _dragOffset;
    private bool _dragMoved, _editing;
    private int _bg;

    protected override string Code => "A3";
    protected override string QuestName => "Fair Poster";
    protected override string Intro => "Ms. Dela Cruz needs a poster for the school event. Make one that fits the brief.";
    protected override string CardTitle => "Design the poster";

    protected override IEnumerator Play()
    {
        _brief = Briefs[Random.Range(0, Briefs.Length)];
        _pieces.Clear(); _undo.Clear(); _selected = null; _bg = 0;
        UI.SetTitle($"Poster: {_brief.Name}");
        UI.SetMessage($"Brief: name, date ({_brief.Date}), a fitting picture, easy to read.", vc_MinigameOverlay.Ink);

        var row = Row(UI.Content, 0);
        row.style.flexWrap = Wrap.NoWrap;
        row.style.alignItems = Align.FlexStart;

        _poster = Box(row, PW, PH, Bgs[0]);
        Border(_poster, 3, vc_MinigameOverlay.Ink);
        _poster.style.overflow = Overflow.Hidden;
        _poster.RegisterCallback<PointerMoveEvent>(OnMove);
        _poster.RegisterCallback<PointerUpEvent>(OnUp);
        _poster.RegisterCallback<PointerLeaveEvent>(_ => _dragging = null);

        var tray = new VisualElement();
        tray.style.width = 400; tray.style.marginLeft = 12;
        row.Add(tray);
        tray.Add(vc_MinigameOverlay.MakeLabel("Title stickers", 15));
        var titles = Row(tray, 0);
        string[] sz = { "S", "M", "L" };
        for (int s = 0; s < 3; s++) { int ss = s; Tray(titles, $"Title {sz[s]}", () => Add(Kind.Title, ss, _brief.Name), 96); }
        tray.Add(vc_MinigameOverlay.MakeLabel("Date and pictures", 15));
        var others = Row(tray, 0);
        Tray(others, "Date", () => Add(Kind.Date, 0, _brief.Date), 96);
        var pics = new List<string>(Pictures);
        pics.Remove(_brief.RightPic == "Ferris wheel" ? "Volcano model" : _brief.RightPic == "Volcano model" ? "Running kids" : "Ferris wheel");
        Shuffle(pics);
        foreach (string p in pics) { string pp = p; Tray(others, p, () => Add(Kind.Pic, 0, pp), 124); }
        tray.Add(vc_MinigameOverlay.MakeLabel("Background, then finish", 15));
        var bgs = Row(tray, 0);
        for (int b = 0; b < Bgs.Length; b++)
        {
            int bb = b;
            var sw = Box(bgs, 30, 30, Bgs[b]);
            sw.style.marginLeft = 2; sw.style.marginRight = 2;
            Border(sw, 2, vc_MinigameOverlay.Ink);
            sw.RegisterCallback<PointerDownEvent>(_ => { if (_editing) { _bg = bb; _poster.style.backgroundColor = Bgs[bb]; } });
        }
        var acts = bgs;
        Tray(acts, "Remove", RemoveSelected, 96);
        bool pin = false;
        Tray(acts, "Pin it up", () => { if (_editing) pin = true; }, 104);
        BuildHelpers(UI.Content);

        _editing = true;
        HelperBar?.Refresh();
        while (!pin) yield return null;
        _editing = false;
        HelperBar?.Refresh();
        Select(null);

        var checks = Check();
        Score = 0f;
        foreach (var c in checks) if (c.ok) Score += 20f;
        var sb = new System.Text.StringBuilder();
        foreach (var c in checks) sb.Append(c.ok ? "[x] " : "[ ] ").Append(c.name).Append("   ");
        UI.SetStatus(sb.ToString());
        UI.SetMessage(Score >= 90f ? "Ms. Dela Cruz pins it on the school wall. Everyone can read it!" :
                      Score >= 50f ? "Nice poster! Ms. Dela Cruz pins it up." : "It's up on the wall, though it's a little hard to read.",
                      Score >= 70f ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
        yield return new WaitForSeconds(2.6f);
    }

    private void Tray(VisualElement parent, string text, System.Action act, float w)
    {
        var b = SmallButton(parent, text, act, w);
        b.style.fontSize = 17; b.style.minHeight = 42; b.style.paddingBottom = 4;
    }

    // ── pieces ───────────────────────────────────────────────────────────

    private static Vector2 SizeOf(Kind k, int size) =>
        k == Kind.Title ? (size == 0 ? new Vector2(150, 30) : size == 1 ? new Vector2(230, 42) : new Vector2(330, 58))
      : k == Kind.Date ? new Vector2(170, 34) : new Vector2(130, 96);

    private void Add(Kind k, int size, string text)
    {
        if (!_editing) return;
        Snapshot();
        Vector2 s = SizeOf(k, size);
        // drop it somewhere free if possible
        Rect r = new Rect((PW - s.x) / 2f, (PH - s.y) / 2f, s.x, s.y);
        for (int tries = 0; tries < 40; tries++)
        {
            var cand = new Rect(Random.Range(6f, PW - s.x - 6f), Random.Range(6f, PH - s.y - 6f), s.x, s.y);
            bool free = true;
            foreach (var p in _pieces) if (p.R.Overlaps(cand)) { free = false; break; }
            if (free) { r = cand; break; }
        }
        Spawn(k, size, text, r);
        SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.1f);
    }

    private Piece Spawn(Kind k, int size, string text, Rect r)
    {
        var p = new Piece { Kind = k, Size = size, Text = text, R = r };
        p.El = new VisualElement();
        p.El.style.position = Position.Absolute;
        p.El.style.backgroundColor = k == Kind.Pic ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.0f);
        var l = vc_MinigameOverlay.MakeLabel(k == Kind.Pic ? $"[{text}]" : text,
            k == Kind.Title ? (size == 0 ? 16 : size == 1 ? 26 : 38) : k == Kind.Date ? 20 : 15);
        l.style.width = Length.Percent(100); l.style.height = Length.Percent(100);
        if (k == Kind.Title) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        p.El.Add(l);
        Border(p.El, 1, new Color(0f, 0f, 0f, 0.25f));
        p.El.RegisterCallback<PointerDownEvent>(e =>
        {
            if (!_editing) return;
            _dragging = p; _dragMoved = false;
            Vector2 local = _poster.WorldToLocal(e.position);
            _dragOffset = local - p.R.position;
            Snapshot();
            e.StopPropagation();
        });
        _poster.Add(p.El);
        _pieces.Add(p);
        Layout(p);
        return p;
    }

    private void Layout(Piece p)
    {
        p.El.style.left = p.R.x; p.El.style.top = p.R.y;
        p.El.style.width = p.R.width; p.El.style.height = p.R.height;
    }

    private void OnMove(PointerMoveEvent e)
    {
        if (_dragging == null) return;
        Vector2 local = _poster.WorldToLocal(e.position);
        Vector2 pos = local - _dragOffset;
        // allowed to hang off the edge a bit (that's one of the checks)
        pos.x = Mathf.Clamp(pos.x, -_dragging.R.width * 0.5f, PW - _dragging.R.width * 0.5f);
        pos.y = Mathf.Clamp(pos.y, -_dragging.R.height * 0.5f, PH - _dragging.R.height * 0.5f);
        if ((pos - _dragging.R.position).sqrMagnitude > 4f) _dragMoved = true;
        _dragging.R = new Rect(pos, _dragging.R.size);
        Layout(_dragging);
    }

    private void OnUp(PointerUpEvent e)
    {
        if (_dragging == null) return;
        if (!_dragMoved) { _undo.Pop(); Select(_selected == _dragging ? null : _dragging); }
        _dragging = null;
    }

    private void Select(Piece p)
    {
        _selected = p;
        foreach (var q in _pieces) Border(q.El, q == p ? 3 : 1, q == p ? Gold : new Color(0f, 0f, 0f, 0.25f));
    }

    private void RemoveSelected()
    {
        if (!_editing || _selected == null) return;
        Snapshot();
        _selected.El.RemoveFromHierarchy();
        _pieces.Remove(_selected);
        _selected = null;
    }

    private void Snapshot()
    {
        var snap = new List<(Kind, int, string, Rect)>();
        foreach (var p in _pieces) snap.Add((p.Kind, p.Size, p.Text, p.R));
        _undo.Push(snap);
    }

    private void Restore(List<(Kind, int, string, Rect)> snap)
    {
        foreach (var p in _pieces) p.El.RemoveFromHierarchy();
        _pieces.Clear();
        _selected = null;
        foreach (var (k, s, t, r) in snap) Spawn(k, s, t, r);
    }

    // ── checklist ────────────────────────────────────────────────────────

    private List<(string name, bool ok)> Check()
    {
        bool name = false, date = false, pic = false, large = false, tidy = true;
        foreach (var p in _pieces)
        {
            if (p.Kind == Kind.Title) { name = true; if (p.Size == 2) large = true; }
            if (p.Kind == Kind.Date) date = true;
            if (p.Kind == Kind.Pic && p.Text == _brief.RightPic) pic = true;
            if (p.R.xMin < 0f || p.R.yMin < 0f || p.R.xMax > PW || p.R.yMax > PH) tidy = false;
        }
        for (int i = 0; i < _pieces.Count; i++)
            for (int j = i + 1; j < _pieces.Count; j++)
                if (_pieces[i].R.Overlaps(_pieces[j].R)) tidy = false;
        return new List<(string, bool)>
        {
            ("Name", name), ("Date", date), ("Picture", pic), ("Neat", tidy), ("Big title", large)
        };
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e)
    {
        if (!_editing) return false;
        if (e == vc_SkillHelpers.Effect.SecondChance) return _undo.Count > 0;
        if (e == vc_SkillHelpers.Effect.Hint || e == vc_SkillHelpers.Effect.FreeStep) return Check().Exists(c => !c.ok);
        return true;
    }

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
            {
                var miss = Check().Find(c => !c.ok).name;
                string why = miss == "Name" ? "it needs the event's name." : miss == "Date" ? "it needs the date." :
                             miss == "Picture" ? $"it needs a picture that fits the {_brief.Name}." :
                             miss == "Neat" ? "something hangs off the edge or overlaps." : "the title should be the large size.";
                UI.SetMessage($"{s.DisplayName}: {why}", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.FreeStep:
                FixOne();
                UI.SetMessage($"{s.DisplayName}: one part of the poster is fixed for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                Restore(_undo.Pop());
                UI.SetMessage($"{s.DisplayName}: your last change is undone.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
            {
                int ok = Check().FindAll(c => c.ok).Count;
                UI.SetMessage($"{s.DisplayName}: {ok} of 5 checks pass so far.", vc_MinigameOverlay.Good);
                break;
            }
        }
    }

    /// <summary>Lays the poster out in tidy slots and adds the first missing piece.</summary>
    private void FixOne()
    {
        Snapshot();
        var c = Check();
        if (!c[4].ok)
        {
            foreach (var p in _pieces.FindAll(p => p.Kind == Kind.Title)) { p.El.RemoveFromHierarchy(); _pieces.Remove(p); }
            Spawn(Kind.Title, 2, _brief.Name, new Rect((PW - 330f) / 2f, 10f, 330f, 58f));
        }
        else if (!c[1].ok) Spawn(Kind.Date, 0, _brief.Date, new Rect((PW - 170f) / 2f, PH - 44f, 170f, 34f));
        else if (!c[2].ok) Spawn(Kind.Pic, 0, _brief.RightPic, new Rect((PW - 130f) / 2f, 76f, 130f, 96f));
        // always leave a tidy layout: title on top, date at the bottom, pictures in a row between
        // (so fixing one check never breaks "Neat")
        float px = 8f, ty = 10f;
        foreach (var p in _pieces)
        {
            Vector2 s = p.R.size;
            if (p.Kind == Kind.Title) { p.R = new Rect((PW - s.x) / 2f, ty, s.x, s.y); ty += s.y + 4f; }
            else if (p.Kind == Kind.Date) p.R = new Rect((PW - s.x) / 2f, PH - s.y - 8f, s.x, s.y);
            else { p.R = new Rect(px, 76f, s.x, s.y); px += s.x + 6f; }
            Layout(p);
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "The poster is perfect. Everyone will know about the event!" :
        score >= 50f ? "The poster is up. Ms. Dela Cruz says thank you!" :
        "The poster is up, even if it's a bit messy.";
}
