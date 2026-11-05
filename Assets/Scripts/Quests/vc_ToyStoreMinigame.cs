using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// E8 Mr. Tan - Toy store rush (MINIGAME_DESIGN.md, type I Customer Rush). New gameplay only.
/// A shelf with 5 toys (limited stock) and a stock box. Customers ask for a toy: tap it on the
/// shelf to sell it. When a toy runs out, tap the stock box, then the empty spot to restock.
/// 10 customers, each with a patience bar (story timer, never a fail). Score = got their toy / 10.
/// Helpers: Hint = the wanted toy glows; Free step = this customer is served (restocked if needed);
/// Second chance = a customer who'd leave comes back once; Breather = the customer waits 5 s more.
/// </summary>
[DisallowMultipleComponent]
public class vc_ToyStoreMinigame : vc_MinigameQuest
{
    private const int Customers = 10, Toys = 5, Restock = 3;
    private static readonly string[] Names = { "Robot", "Doll", "Yo-yo", "Kite", "Toy car" };
    private static readonly Color[] Cols =
    {
        new Color(0.60f, 0.70f, 0.85f), new Color(0.95f, 0.65f, 0.75f), new Color(0.90f, 0.45f, 0.35f),
        new Color(0.55f, 0.80f, 0.55f), new Color(0.95f, 0.80f, 0.35f)
    };

    private readonly int[] _stock = new int[Toys];
    private readonly VisualElement[] _slots = new VisualElement[Toys];
    private readonly Label[] _slotLabels = new Label[Toys];
    private VisualElement _box, _fill;
    private Label _bubble;
    private int _want, _comebacks;
    private bool _serving, _sold, _freeServe, _holdingStock;
    private float _patience, _patienceMax, _pauseUntil;

    protected override string Code => "E8";
    protected override string QuestName => "Toy Store Rush";
    protected override string Intro => "Mr. Tan's toy store is packed. Sell each customer their toy and keep the shelf stocked.";
    protected override string CardTitle => "Run Mr. Tan's toy store";

    protected override IEnumerator Play()
    {
        _comebacks = 0; _pauseUntil = 0f; _holdingStock = false;
        UI.SetMessage("Tap the toy they want. Empty spot? Tap the stock box, then the spot.", vc_MinigameOverlay.Ink);

        _bubble = vc_MinigameOverlay.MakeLabel("", 28);
        _bubble.style.backgroundColor = Color.white;
        _bubble.style.width = 460; _bubble.style.minHeight = 54;
        Border(_bubble, 3, vc_MinigameOverlay.Ink);
        UI.Content.Add(_bubble);
        var bar = Box(UI.Content, 460, 12, new Color(0.3f, 0.25f, 0.27f));
        bar.style.marginTop = 4;
        _fill = Box(bar, 460, 12, vc_MinigameOverlay.Good);

        var shop = Row(UI.Content, 10);
        shop.style.flexWrap = Wrap.NoWrap;
        var shelf = Box(shop, Toys * 118f + 10f, 120, new Color(0.55f, 0.40f, 0.30f));
        shelf.style.flexDirection = FlexDirection.Row;
        shelf.style.paddingLeft = 5; shelf.style.paddingTop = 8;
        for (int t = 0; t < Toys; t++)
        {
            _stock[t] = Random.Range(1, 4);
            int tt = t;
            var slot = Box(shelf, 110, 100, Cols[t]);
            slot.style.marginRight = 8;
            Border(slot, 2, vc_MinigameOverlay.Ink);
            var l = vc_MinigameOverlay.MakeLabel("", 20);
            l.style.width = Length.Percent(100); l.style.height = Length.Percent(100);
            slot.Add(l);
            slot.RegisterCallback<PointerDownEvent>(_ => TapSlot(tt));
            _slots[t] = slot; _slotLabels[t] = l;
            PaintSlot(t);
        }
        _box = Box(shop, 110, 100, new Color(0.72f, 0.55f, 0.35f));
        _box.style.marginLeft = 14;
        Border(_box, 3, vc_MinigameOverlay.Ink);
        var bl = vc_MinigameOverlay.MakeLabel("STOCK\nBOX", 20);
        bl.style.width = Length.Percent(100); bl.style.height = Length.Percent(100);
        _box.Add(bl);
        _box.RegisterCallback<PointerDownEvent>(_ => TapBox());
        BuildHelpers(UI.Content);

        int served = 0;
        for (int c = 0; c < Customers; c++)
        {
            UI.SetStatus($"Customer {c + 1} of {Customers}   Happy: {served}");
            // more likely to ask for a toy that's running low, so restocking matters
            _want = Random.value < 0.4f ? LowestStock() : Random.Range(0, Toys);
            _bubble.text = $"\"Do you have a {Names[_want].ToLower()}?\"";
            _patienceMax = Mathf.Max(7f, 11f - c * 0.3f);
            _patience = _patienceMax;
            _sold = false; _freeServe = false;
            _serving = true;
            HelperBar?.Refresh();

            while (!_sold && !_freeServe)
            {
                if (Time.time >= _pauseUntil) _patience -= Time.deltaTime;
                _fill.style.width = 460f * Mathf.Clamp01(_patience / _patienceMax);
                _fill.style.backgroundColor = _patience / _patienceMax < 0.3f ? vc_MinigameOverlay.Bad : vc_MinigameOverlay.Good;
                if (_patience <= 0f)
                {
                    if (_comebacks > 0)
                    {
                        _comebacks--;
                        _patience = _patienceMax * 0.6f;
                        UI.SetMessage("The customer was about to leave, but waits a little longer!", vc_MinigameOverlay.Good);
                    }
                    else break;
                }
                yield return null;
            }
            _serving = false;
            HelperBar?.Refresh();
            foreach (var s in _slots) Border(s, 2, vc_MinigameOverlay.Ink);

            bool ok = _sold || _freeServe;
            if (_freeServe && !_sold) { if (_stock[_want] == 0) _stock[_want] = Restock; _stock[_want]--; PaintSlot(_want); }
            if (ok) served++;
            UI.SetMessage(ok ? $"Sold! The kid hugs the {Names[_want].ToLower()}." : "The customer got tired of waiting and left.",
                          ok ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Bad);
            _bubble.text = ok ? ":)" : ":(";
            SunodGame.Core.vc_Sfx.Play(ok ? "step_done" : "clack", 0.5f);
            yield return new WaitForSeconds(0.8f);
            UI.SetMessage("Tap the toy they want. Empty spot? Tap the stock box, then the spot.", vc_MinigameOverlay.Ink);
        }

        Score = 100f * served / Customers;
        UI.SetStatus($"Happy customers: {served} of {Customers}");
        UI.SetMessage("Closing time! The rush is over.", vc_MinigameOverlay.Good);
        yield return new WaitForSeconds(1.6f);
    }

    private int LowestStock()
    {
        int best = 0;
        for (int t = 1; t < Toys; t++) if (_stock[t] < _stock[best]) best = t;
        return best;
    }

    private void TapBox()
    {
        if (!_serving) return;
        _holdingStock = !_holdingStock;
        Border(_box, _holdingStock ? 5 : 3, _holdingStock ? Gold : vc_MinigameOverlay.Ink);
        SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.1f);
    }

    private void TapSlot(int t)
    {
        if (!_serving) return;
        if (_holdingStock)
        {
            _holdingStock = false;
            Border(_box, 3, vc_MinigameOverlay.Ink);
            if (_stock[t] == 0) { _stock[t] = Restock; PaintSlot(t); SunodGame.Core.vc_Sfx.Play("clack", 0.5f); }
            else UI.SetMessage("That spot isn't empty yet.", vc_MinigameOverlay.Ink);
            return;
        }
        if (_stock[t] == 0) { UI.SetMessage("That spot is empty. Restock it from the box first.", vc_MinigameOverlay.Bad); return; }
        if (t != _want)
        {
            _patience -= 1.5f;
            UI.SetMessage($"\"No, I wanted a {Names[_want].ToLower()}...\"", vc_MinigameOverlay.Bad);
            return;
        }
        _stock[t]--;
        PaintSlot(t);
        _sold = true;
    }

    private void PaintSlot(int t)
    {
        bool empty = _stock[t] == 0;
        _slots[t].style.backgroundColor = empty ? new Color(0.35f, 0.27f, 0.22f) : Cols[t];
        _slotLabels[t].text = empty ? "(empty)" : $"{Names[t]}\nx{_stock[t]}";
        _slotLabels[t].style.color = empty ? new Color(0.9f, 0.85f, 0.8f) : vc_MinigameOverlay.Ink;
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e) => _serving;

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                Border(_slots[_want], 5, Gold);
                UI.SetMessage($"{s.DisplayName}: the glowing spot is the one they want" + (_stock[_want] == 0 ? " (restock it first)." : "."), vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                _freeServe = true;
                UI.SetMessage($"{s.DisplayName}: this customer is served for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _comebacks++;
                UI.SetMessage($"{s.DisplayName}: if a customer runs out of patience, they'll wait a bit more.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                _pauseUntil = Time.time + 5f;
                UI.SetMessage($"{s.DisplayName}: the customer happily waits 5 more seconds.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Every customer left with a toy. Mr. Tan's best day ever!" :
        score >= 50f ? "Most customers went home happy. Mr. Tan thanks you!" :
        "It was a wild rush, but Mr. Tan is glad you helped.";
}
