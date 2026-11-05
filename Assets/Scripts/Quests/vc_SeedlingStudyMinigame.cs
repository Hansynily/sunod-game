using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// I3 Teacher Joy - Seedling study (MINIGAME_DESIGN.md, type E Observe and Record). New gameplay only.
/// 4 pots beside a cm ruler. For each pot set the measured height (snaps per cm) and tap the leaf
/// colour, then answer "Which pot needs more sunlight?" (the short, yellow one).
/// Score: heights within 1 cm 15% each, leaf colours 5% each, conclusion 20%.
/// Helpers: Hint = one pot's true height shows; Free step = one pot is filled in right;
/// Breather = check one pot's entries; Second chance = one more try at the conclusion.
/// </summary>
[DisallowMultipleComponent]
public class vc_SeedlingStudyMinigame : vc_MinigameQuest
{
    private const int Pots = 4, MaxCm = 12;
    private const float PxPerCm = 12f;
    private static readonly string[] LeafNames = { "Green", "Pale", "Yellow" };
    private static readonly Color[] LeafCols =
    {
        new Color(0.25f, 0.65f, 0.25f), new Color(0.65f, 0.80f, 0.45f), new Color(0.92f, 0.82f, 0.25f)
    };
    private static readonly Color Soil = new Color(0.45f, 0.30f, 0.20f);
    private static readonly Color PotC = new Color(0.75f, 0.42f, 0.28f);

    private int[] _height = new int[Pots], _leaf = new int[Pots], _gotH = new int[Pots], _gotLeaf = new int[Pots];
    private bool[] _locked = new bool[Pots];
    private int _answer;
    private int _conclusionTries;
    private bool _recording;
    private VisualElement[] _markers = new VisualElement[Pots];
    private Label[] _hLabels = new Label[Pots];
    private VisualElement[,] _leafBtns = new VisualElement[Pots, 3];
    private VisualElement[] _cols = new VisualElement[Pots];

    protected override string Code => "I3";
    protected override string QuestName => "Seedling Study";
    protected override string Intro => "Teacher Joy's class is growing seedlings. Measure them and find the one that needs sun.";
    protected override string CardTitle => "Record the seedlings";

    protected override IEnumerator Play()
    {
        // which pot is the sun-starved one: shortest + yellow
        _answer = Random.Range(0, Pots);
        for (int i = 0; i < Pots; i++)
        {
            _height[i] = i == _answer ? Random.Range(2, 5) : Random.Range(5, MaxCm + 1);
            _leaf[i] = i == _answer ? 2 : Random.Range(0, 2);
            _gotH[i] = 0; _gotLeaf[i] = -1; _locked[i] = false;
        }
        _conclusionTries = 1;
        _recording = true;

        UI.SetMessage("Use - / + to line the marker up with the top, then tap the leaf colour.", vc_MinigameOverlay.Ink);
        UI.SetStatus("Measure all 4 pots");

        var row = Row(UI.Content, 2);
        row.style.alignItems = Align.FlexEnd;
        for (int i = 0; i < Pots; i++) BuildPot(row, i);

        bool recorded = false;
        var actions = new VisualElement();
        actions.style.marginLeft = 12; actions.style.marginBottom = 40;
        row.Add(actions);
        var done = SmallButton(actions, "Done", () => recorded = true, 120);
        done.style.minHeight = 70;
        BuildHelpers(UI.Content);

        while (!recorded) yield return null;
        _recording = false;
        actions.Clear();
        actions.style.marginBottom = 0;
        HelperBar?.Refresh();

        // conclusion
        UI.SetMessage("Which pot needs more sunlight? Tap it.", vc_MinigameOverlay.Ink);
        UI.SetStatus("Last question");
        int picked = -1;
        var pick = actions;
        for (int i = 0; i < Pots; i++) { int c = i; SmallButton(pick, $"Pot {i + 1}", () => picked = c, 120); }
        bool right = false;
        while (true)
        {
            picked = -1;
            while (picked < 0) yield return null;
            right = picked == _answer;
            if (right || _conclusionTries <= 1) break;
            _conclusionTries--;
            UI.SetMessage("Not quite. Look at the height and leaf colour again. One more try.", vc_MinigameOverlay.Bad);
            HelperBar?.Refresh();
        }
        pick.style.display = DisplayStyle.None;

        Score = 0f;
        for (int i = 0; i < Pots; i++)
        {
            if (Mathf.Abs(_gotH[i] - _height[i]) <= 1) Score += 15f;
            if (_gotLeaf[i] == _leaf[i]) Score += 5f;
        }
        if (right) Score += 20f;
        UI.SetMessage((right ? $"Yes! Pot {_answer + 1} goes to the window. " : $"It was pot {_answer + 1}. ") +
                      $"Record sheet: {Mathf.RoundToInt(Score)}%.", right ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Bad);
        yield return new WaitForSeconds(2f);
    }

    /// <summary>A cm ruler standing on the soil line, beside the plant (same baseline as the stem).</summary>
    private static void BuildRuler(VisualElement area)
    {
        var ruler = Box(area, 30, MaxCm * PxPerCm + 6, new Color(0.95f, 0.88f, 0.60f));
        ruler.style.position = Position.Absolute; ruler.style.left = 0; ruler.style.bottom = 37;
        for (int cm = 0; cm <= MaxCm; cm++)
        {
            var tick = Box(ruler, cm % 2 == 0 ? 12 : 7, 2, vc_MinigameOverlay.Ink);
            tick.style.position = Position.Absolute; tick.style.right = 0;
            tick.style.bottom = 3 + cm * PxPerCm - 1;
            if (cm % 2 == 0 && cm > 0)
            {
                var l = vc_MinigameOverlay.MakeLabel(cm.ToString(), 11);
                l.style.position = Position.Absolute; l.style.left = 1; l.style.bottom = 3 + cm * PxPerCm - 8;
                ruler.Add(l);
            }
        }
    }

    private void BuildPot(VisualElement row, int i)
    {
        var col = new VisualElement();
        col.style.alignItems = Align.Center;
        col.style.marginLeft = 8; col.style.marginRight = 8;
        row.Add(col);
        _cols[i] = col;

        var area = Box(col, 120, MaxCm * PxPerCm + 50, new Color(0, 0, 0, 0));
        BuildRuler(area);
        // pot + soil
        var pot = Box(area, 70, 40, PotC); pot.style.position = Position.Absolute; pot.style.bottom = 0; pot.style.left = 36;
        var soil = Box(area, 70, 6, Soil); soil.style.position = Position.Absolute; soil.style.bottom = 34; soil.style.left = 36;
        // stem + leaves
        float h = _height[i] * PxPerCm;
        var stem = Box(area, 6, h, new Color(0.30f, 0.55f, 0.25f)); stem.style.position = Position.Absolute; stem.style.bottom = 40; stem.style.left = 68;
        var leafL = Box(area, 18, 10, LeafCols[_leaf[i]]); leafL.style.position = Position.Absolute; leafL.style.bottom = 40 + h - 12; leafL.style.left = 50;
        var leafR = Box(area, 18, 10, LeafCols[_leaf[i]]); leafR.style.position = Position.Absolute; leafR.style.bottom = 40 + h - 18; leafR.style.left = 74;
        var mid = Box(area, 10, 6, LeafCols[_leaf[i]]); mid.style.position = Position.Absolute; mid.style.bottom = 40 + h - 3; mid.style.left = 66;
        // measuring marker (starts at the ruler, runs across the plant)
        var marker = Box(area, 90, 3, new Color(0.85f, 0.20f, 0.20f));
        marker.style.position = Position.Absolute; marker.style.left = 30;
        _markers[i] = marker;

        var name = vc_MinigameOverlay.MakeLabel($"Pot {i + 1}", 16);
        col.Add(name);

        var steps = Row(col, 0);
        int c = i;
        SmallButton(steps, "-", () => SetHeight(c, _gotH[c] - 1), 46);
        _hLabels[i] = vc_MinigameOverlay.MakeLabel("", 18);
        _hLabels[i].style.width = 52;
        steps.Add(_hLabels[i]);
        SmallButton(steps, "+", () => SetHeight(c, _gotH[c] + 1), 46);

        var leaves = Row(col, 0);
        for (int k = 0; k < 3; k++)
        {
            int kk = k;
            // plain colour swatch (the button skin would hide the colour)
            var b = Box(leaves, 34, 30, LeafCols[k]);
            b.style.marginLeft = 4; b.style.marginRight = 4; b.style.marginTop = 4;
            b.tooltip = LeafNames[k];
            b.RegisterCallback<PointerDownEvent>(_ => SetLeaf(c, kk));
            _leafBtns[i, k] = b;
        }
        SetHeight(i, 0);
        SetLeaf(i, -1);
    }

    private void SetHeight(int i, int cm)
    {
        if (!_recording || _locked[i]) return;
        _gotH[i] = Mathf.Clamp(cm, 0, MaxCm);
        _markers[i].style.bottom = 40 + _gotH[i] * PxPerCm - 1;
        _hLabels[i].text = $"{_gotH[i]} cm";
    }

    private void SetLeaf(int i, int k)
    {
        if (!_recording || _locked[i]) return;
        _gotLeaf[i] = k;
        for (int j = 0; j < 3; j++) Border(_leafBtns[i, j], j == k ? 4 : 1, vc_MinigameOverlay.Ink);
    }

    private int FirstWrongPot()
    {
        for (int i = 0; i < Pots; i++)
            if (!_locked[i] && (Mathf.Abs(_gotH[i] - _height[i]) > 1 || _gotLeaf[i] != _leaf[i])) return i;
        return -1;
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e)
    {
        if (e == vc_SkillHelpers.Effect.SecondChance) return _conclusionTries <= 1;
        if (!_recording) return false;
        return FirstWrongPot() >= 0 || e == vc_SkillHelpers.Effect.Breather;
    }

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        int p = FirstWrongPot();
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                if (p < 0) break;
                Border(_cols[p], 3, Gold);
                UI.SetMessage($"{s.DisplayName}: pot {p + 1} is {_height[p]} cm tall.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                if (p < 0) break;
                SetHeight(p, _height[p]); SetLeaf(p, _leaf[p]);
                _locked[p] = true;
                Border(_cols[p], 3, vc_MinigameOverlay.Good);
                UI.SetMessage($"{s.DisplayName}: pot {p + 1} is recorded for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
            {
                int wrong = 0;
                for (int i = 0; i < Pots; i++)
                    if (Mathf.Abs(_gotH[i] - _height[i]) > 1 || _gotLeaf[i] != _leaf[i]) wrong++;
                UI.SetMessage(wrong == 0 ? $"{s.DisplayName}: every pot looks right!" :
                    $"{s.DisplayName}: {wrong} pot{(wrong == 1 ? " needs" : "s need")} another look.", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.SecondChance:
                _conclusionTries = 2;
                UI.SetMessage($"{s.DisplayName}: you get two tries at the last question.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Teacher Joy pins up your record sheet. Perfect notes!" :
        score >= 50f ? "Teacher Joy's class has good notes on their seedlings." :
        "The notes are a bit off, but the class learned something.";
}
