using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// C6 Manang Fe - Canteen change (MINIGAME_DESIGN.md, type J Sort and Ledger). New gameplay only.
/// 6 students buy food and pay with a bill. Work out paid minus total on the calculator, build the
/// change from peso coins / bills, then Give. Exact change = full marks; score = exact / 6. No timer.
/// Helpers: Hint = the change amount shows; Free step = the change is counted out for you;
/// Second chance = one wrong Give is taken back; Breather = check the change before giving.
/// </summary>
[DisallowMultipleComponent]
public class vc_CanteenChangeMinigame : vc_MinigameQuest
{
    private const int Students = 6;
    private static readonly (string name, int price)[] Menu =
    {
        ("Rice", 15), ("Adobo", 35), ("Pancit", 25), ("Lumpia", 12), ("Juice", 18),
        ("Banana cue", 14), ("Turon", 13), ("Bread", 8), ("Egg", 11), ("Soup", 22)
    };
    private static readonly int[] Money = { 1, 5, 10, 20, 50, 100 };

    private int _total, _paid, _change;
    private readonly List<int> _given = new List<int>();
    private bool _active, _give, _freeGive;
    private int _undoTokens;
    private Label _order, _calcDisplay, _changeLabel;
    private string _calcText = "";

    protected override string Code => "C6";
    protected override string QuestName => "Canteen Change";
    protected override string Intro => "Manang Fe's canteen is busy. Give every student the right change.";
    protected override string CardTitle => "Count the change";

    protected override IEnumerator Play()
    {
        _undoTokens = 0;
        UI.SetMessage("Use the calculator, then tap coins and bills to make the change.", vc_MinigameOverlay.Ink);

        var row = Row(UI.Content, 0);
        row.style.alignItems = Align.FlexStart;
        row.style.flexWrap = Wrap.NoWrap;

        // left: order + change tray
        var left = new VisualElement();
        left.style.width = 430;
        left.style.alignItems = Align.Center;
        row.Add(left);
        _order = vc_MinigameOverlay.MakeLabel("", 22);
        _order.style.backgroundColor = Color.white;
        _order.style.width = 410; _order.style.minHeight = 120;
        _order.style.unityTextAlign = TextAnchor.MiddleLeft;
        _order.style.paddingLeft = 10;
        Border(_order, 3, vc_MinigameOverlay.Ink);
        left.Add(_order);

        var money = Row(left, 6);
        foreach (int m in Money)
        {
            int v = m;
            var b = SmallButton(money, m >= 20 ? $"P{m} bill" : $"P{m}", () => AddMoney(v), m >= 20 ? 120 : 76);
            b.style.backgroundColor = m >= 20 ? new Color(0.65f, 0.85f, 0.60f) : new Color(0.90f, 0.82f, 0.50f);
        }
        var acts = Row(left, 2);
        acts.style.flexWrap = Wrap.NoWrap;
        _changeLabel = vc_MinigameOverlay.MakeLabel("", 22);
        _changeLabel.style.width = 130;
        acts.Add(_changeLabel);
        SmallButton(acts, "Undo", () => { if (_active && _given.Count > 0) { _given.RemoveAt(_given.Count - 1); ShowChange(); } }, 120);
        SmallButton(acts, "Give", () => { if (_active) _give = true; }, 160);

        // right: calculator
        BuildCalculator(row);
        BuildHelpers(UI.Content);

        int exact = 0;
        for (int st = 0; st < Students; st++)
        {
            UI.SetStatus($"Student {st + 1} of {Students}   Exact: {exact}");
            var items = new List<(string, int)>(Menu);
            Shuffle(items);
            int n = Random.Range(1, 4);
            _total = 0;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++) { sb.Append($"{items[i].Item1}  P{items[i].Item2}\n"); _total += items[i].Item2; }
            int[] bills = { 20, 50, 100, 200 };
            _paid = 200;
            foreach (int b in bills) if (b - _total >= 5) { _paid = b; break; }
            _change = _paid - _total;
            sb.Append($"<b>Pays with P{_paid}</b>");
            _order.enableRichText = true;
            _order.text = sb.ToString();
            _given.Clear(); ShowChange();
            _calcText = ""; ShowCalc();
            _give = false; _freeGive = false;
            _active = true;
            HelperBar?.Refresh();

            bool ok;
            while (true)
            {
                while (!_give && !_freeGive) yield return null;
                ok = _freeGive || Sum() == _change;
                _give = false;
                if (!ok && _undoTokens > 0)
                {
                    _undoTokens--;
                    UI.SetMessage("Manang Fe stops you: count that again.", vc_MinigameOverlay.Good);
                    continue;
                }
                break;
            }
            _active = false;
            HelperBar?.Refresh();
            if (ok) exact++;
            UI.SetMessage(ok ? $"Right! P{_change} change. \"Salamat po!\"" : $"Oops, the change should be P{_change}.",
                          ok ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Bad);
            SunodGame.Core.vc_Sfx.Play(ok ? "step_done" : "clack", 0.5f);
            yield return new WaitForSeconds(1.2f);
            UI.SetMessage("", vc_MinigameOverlay.Ink);
        }

        Score = 100f * exact / Students;
        UI.SetStatus($"Exact change: {exact} of {Students}");
        UI.SetMessage("The lunch line is done!", vc_MinigameOverlay.Good);
        yield return new WaitForSeconds(1.5f);
    }

    private int Sum() { int s = 0; foreach (int g in _given) s += g; return s; }

    private void AddMoney(int v)
    {
        if (!_active) return;
        _given.Add(v);
        SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.1f);
        ShowChange();
    }

    private void ShowChange() => _changeLabel.text = $"In hand: P{Sum()}";

    private void BuildCalculator(VisualElement row)
    {
        var calc = new VisualElement();
        calc.style.width = 280;
        calc.style.marginLeft = 16;
        calc.style.backgroundColor = new Color(0.30f, 0.30f, 0.34f);
        calc.style.paddingLeft = 8; calc.style.paddingRight = 8; calc.style.paddingTop = 8; calc.style.paddingBottom = 8;
        row.Add(calc);
        _calcDisplay = vc_MinigameOverlay.MakeLabel("0", 30);
        _calcDisplay.style.backgroundColor = new Color(0.75f, 0.85f, 0.70f);
        _calcDisplay.style.unityTextAlign = TextAnchor.MiddleRight;
        _calcDisplay.style.paddingRight = 8;
        _calcDisplay.style.height = 48;
        calc.Add(_calcDisplay);
        string[] keys = { "7", "8", "9", "-", "4", "5", "6", "+", "1", "2", "3", "=", "C", "0" };
        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        calc.Add(grid);
        foreach (string k in keys)
        {
            string key = k;
            var b = SmallButton(grid, k, () => CalcKey(key), 0);
            b.style.width = Length.Percent(23);
            b.style.minWidth = 0;
            b.style.minHeight = 42; b.style.height = 42;
            b.style.paddingLeft = 0; b.style.paddingRight = 0; b.style.paddingBottom = 4;
            b.style.marginLeft = 1; b.style.marginRight = 1; b.style.marginTop = 2;
        }
    }

    private void CalcKey(string k)
    {
        if (k == "C") _calcText = "";
        else if (k == "=") _calcText = Eval(_calcText).ToString();
        else if (_calcText.Length < 14) _calcText += k;
        ShowCalc();
    }

    private void ShowCalc() => _calcDisplay.text = _calcText.Length == 0 ? "0" : _calcText;

    private static int Eval(string s)
    {
        int total = 0, cur = 0, sign = 1;
        foreach (char ch in s)
        {
            if (char.IsDigit(ch)) cur = cur * 10 + (ch - '0');
            else { total += sign * cur; cur = 0; sign = ch == '-' ? -1 : 1; }
        }
        return total + sign * cur;
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e) => _active;

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                UI.SetMessage($"{s.DisplayName}: the change is P{_change}.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
            {
                _given.Clear();
                int left = _change;
                for (int i = Money.Length - 1; i >= 0; i--) while (left >= Money[i]) { _given.Add(Money[i]); left -= Money[i]; }
                ShowChange();
                _freeGive = true;
                UI.SetMessage($"{s.DisplayName}: the change is counted out for you.", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.SecondChance:
                _undoTokens++;
                UI.SetMessage($"{s.DisplayName}: if you give wrong change, Manang Fe will catch it.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
            {
                int sum = Sum();
                UI.SetMessage(sum == _change ? $"{s.DisplayName}: that's exactly right!" :
                    sum < _change ? $"{s.DisplayName}: not enough yet." : $"{s.DisplayName}: that's too much.",
                    vc_MinigameOverlay.Good);
                break;
            }
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Every peso counted right. Manang Fe is impressed!" :
        score >= 50f ? "Most students got the right change. Good work!" :
        "Counting change is tricky. Manang Fe thanks you for helping.";
}
