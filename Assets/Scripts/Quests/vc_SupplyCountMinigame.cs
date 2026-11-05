using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// C2 Principal Reyes - Supply room count (MINIGAME_DESIGN.md, type J Sort and Ledger). New gameplay only.
/// A shelf with 4 bins (chalk, paper, markers, glue). Some items hide behind a box at the front of
/// the bin: tap the box to slide it aside. Count each bin and enter it on the hand-held scanner.
/// Per bin: exact = full, off by 1 = half, more = 0. Average of 4. The missing key turns up in a bin.
/// Helpers: Hint = one bin's count shows; Free step = one bin is entered right;
/// Breather = check one bin's number; Second chance = if a count is off, one recount.
/// </summary>
[DisallowMultipleComponent]
public class vc_SupplyCountMinigame : vc_MinigameQuest
{
    private const int Bins = 4, GridN = 6;
    private const float Cell = 22f, BinW = 6 * 22f + 10f;
    private static readonly string[] Names = { "Chalk", "Paper", "Markers", "Glue" };
    private static readonly Color[] ItemCols =
    {
        new Color(0.97f, 0.97f, 0.95f), new Color(0.80f, 0.88f, 0.98f), new Color(0.90f, 0.35f, 0.35f), new Color(0.98f, 0.90f, 0.45f)
    };
    private static readonly Color BinC = new Color(0.55f, 0.40f, 0.30f);
    private static readonly Color CrateC = new Color(0.72f, 0.55f, 0.35f);

    private readonly int[] _count = new int[Bins], _entered = new int[Bins];
    private readonly bool[] _locked = new bool[Bins];
    private readonly Label[] _numLabels = new Label[Bins];
    private readonly VisualElement[] _binEls = new VisualElement[Bins];
    private int _keyBin, _recounts;
    private bool _counting;

    protected override string Code => "C2";
    protected override string QuestName => "Supply Room Count";
    protected override string Intro => "Principal Reyes needs the supply room counted. Check every bin.";
    protected override string CardTitle => "Count the supply bins";

    protected override IEnumerator Play()
    {
        _recounts = 0;
        _keyBin = Random.Range(0, Bins);
        UI.SetMessage("Tap a front box to slide it aside. Count, then set the number with - / +.", vc_MinigameOverlay.Ink);
        UI.SetStatus("Count all 4 bins");

        var shelf = Row(UI.Content, 2);
        shelf.style.flexWrap = Wrap.NoWrap;
        shelf.style.alignItems = Align.FlexStart;
        for (int b = 0; b < Bins; b++) BuildBin(shelf, b);

        bool submit = false;
        var acts = Row(UI.Content, 6);
        SmallButton(acts, "Send to scanner", () => { if (_counting) submit = true; }, 300);
        BuildHelpers(UI.Content);

        _counting = true;
        HelperBar?.Refresh();
        while (true)
        {
            submit = false;
            while (!submit) yield return null;
            bool allRight = true;
            for (int b = 0; b < Bins; b++) if (_entered[b] != _count[b]) allRight = false;
            if (!allRight && _recounts > 0)
            {
                _recounts--;
                UI.SetMessage("The scanner beeps: something is off. Recount and send again.", vc_MinigameOverlay.Bad);
                continue;
            }
            break;
        }
        _counting = false;
        HelperBar?.Refresh();

        Score = 0f;
        for (int b = 0; b < Bins; b++)
        {
            int off = Mathf.Abs(_entered[b] - _count[b]);
            float part = off == 0 ? 25f : off == 1 ? 12.5f : 0f;
            Score += part;
            _numLabels[b].text = $"{_entered[b]} / {_count[b]}";
            _numLabels[b].style.color = off == 0 ? vc_MinigameOverlay.Good : off == 1 ? vc_MinigameOverlay.Ink : vc_MinigameOverlay.Bad;
        }
        UI.SetMessage($"Inventory {Mathf.RoundToInt(Score)}% right. And look: the missing key was in the {Names[_keyBin].ToLower()} bin!",
                      Score >= 70f ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
        yield return new WaitForSeconds(2.2f);
    }

    private void BuildBin(VisualElement shelf, int b)
    {
        var col = new VisualElement();
        col.style.alignItems = Align.Center;
        col.style.marginLeft = 6; col.style.marginRight = 6;
        shelf.Add(col);
        col.Add(vc_MinigameOverlay.MakeLabel(Names[b], 18));

        var bin = Box(col, BinW, GridN * Cell + 10f, BinC);
        _binEls[b] = bin;

        // hidden corner (bottom-left 3x2 cells) is covered by a crate
        int n = Random.Range(5, 13);
        _count[b] = n;
        var cells = new List<Vector2Int>();
        for (int x = 0; x < GridN; x++) for (int y = 0; y < GridN; y++) cells.Add(new Vector2Int(x, y));
        Shuffle(cells);
        // make sure 1-3 items sit behind the crate
        int hidden = Random.Range(1, 4), placedHidden = 0, placed = 0;
        var chosen = new List<Vector2Int>();
        foreach (var c in cells) if (IsHidden(c) && placedHidden < hidden) { chosen.Add(c); placedHidden++; }
        foreach (var c in cells) if (!IsHidden(c) && !IsCoveredWhenOpen(c) && chosen.Count < n) chosen.Add(c);
        foreach (var c in chosen)
        {
            var it = Box(bin, Cell - 6f, Cell - 6f, ItemCols[b]);
            it.style.position = Position.Absolute;
            it.style.left = 5 + c.x * Cell + 3; it.style.top = 5 + c.y * Cell + 3;
            Border(it, 1, vc_MinigameOverlay.Ink);
            placed++;
        }

        var crate = Box(bin, 3 * Cell + 4f, 2 * Cell + 4f, CrateC);
        crate.style.position = Position.Absolute;
        crate.style.left = 3; crate.style.top = 5 + 4 * Cell - 2;
        Border(crate, 2, vc_MinigameOverlay.Ink);
        var crateLbl = vc_MinigameOverlay.MakeLabel("box", 13);
        crateLbl.style.width = Length.Percent(100); crateLbl.style.height = Length.Percent(100);
        crate.Add(crateLbl);
        bool open = false;
        crate.RegisterCallback<PointerDownEvent>(_ =>
        {
            open = !open;
            crate.style.left = open ? BinW - 3 * Cell - 8f : 3f;
            crate.style.top = open ? 5f : 5 + 4 * Cell - 2;
            SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.1f);
        });

        var steps = Row(col, 2);
        steps.style.flexWrap = Wrap.NoWrap;
        int bb = b;
        SmallButton(steps, "-", () => SetEntered(bb, _entered[bb] - 1), 44);
        _numLabels[b] = vc_MinigameOverlay.MakeLabel("0", 22);
        _numLabels[b].style.width = 56;
        steps.Add(_numLabels[b]);
        SmallButton(steps, "+", () => SetEntered(bb, _entered[bb] + 1), 44);
        _entered[b] = 0; _locked[b] = false;
    }

    // bottom-left 3x2 = behind the crate at the start
    private static bool IsHidden(Vector2Int c) => c.x < 3 && c.y >= 4;
    // where the crate slides to (top-right), so nothing is ever hidden once it's moved
    private static bool IsCoveredWhenOpen(Vector2Int c) => c.x >= 3 && c.y <= 1;

    private void SetEntered(int b, int v)
    {
        if (!_counting || _locked[b]) return;
        _entered[b] = Mathf.Clamp(v, 0, 36);
        _numLabels[b].text = _entered[b].ToString();
    }

    private int FirstWrong()
    {
        for (int b = 0; b < Bins; b++) if (!_locked[b] && _entered[b] != _count[b]) return b;
        return -1;
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e)
    {
        if (!_counting) return false;
        if (e == vc_SkillHelpers.Effect.Hint || e == vc_SkillHelpers.Effect.FreeStep) return FirstWrong() >= 0;
        return true;
    }

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        int b = FirstWrong();
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                Border(_binEls[b], 3, Gold);
                UI.SetMessage($"{s.DisplayName}: the {Names[b].ToLower()} bin holds {_count[b]}.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                SetEntered(b, _count[b]);
                _locked[b] = true;
                Border(_binEls[b], 3, vc_MinigameOverlay.Good);
                UI.SetMessage($"{s.DisplayName}: the {Names[b].ToLower()} bin is counted for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
            {
                int wrong = 0;
                for (int i = 0; i < Bins; i++) if (_entered[i] != _count[i]) wrong++;
                UI.SetMessage(wrong == 0 ? $"{s.DisplayName}: every count is right!" :
                    $"{s.DisplayName}: {wrong} bin{(wrong == 1 ? " is" : "s are")} off.", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.SecondChance:
                _recounts++;
                UI.SetMessage($"{s.DisplayName}: if the scanner finds a mistake, you can recount.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Every bin counted right, and the key is found! Principal Reyes is thrilled." :
        score >= 50f ? "The supply list is mostly right, and the key is back." :
        "The counts need another look, but at least the key turned up.";
}
