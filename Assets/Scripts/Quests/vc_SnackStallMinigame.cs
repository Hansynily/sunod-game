using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// E2 Jun - Snack stall (MINIGAME_DESIGN.md, type I Customer Rush). New gameplay only.
/// 8 kids line up, each wanting one snack and how many. Tap the snack once per piece, then
/// Hand over. Each kid has a patience bar (story timer, never a fail). Score = kids served right / 8.
/// Helpers: Hint = the right snack glows; Free step = this kid is served for you;
/// Second chance = a wrong hand-over is taken back; Breather = the kid waits 5 s longer.
/// </summary>
[DisallowMultipleComponent]
public class vc_SnackStallMinigame : vc_MinigameQuest
{
    private const int Kids = 8;
    private static readonly string[] Snacks = { "Turon", "Banana cue", "Fishball", "Juice" };
    private static readonly Color[] SnackCols =
    {
        new Color(0.85f, 0.60f, 0.30f), new Color(0.95f, 0.80f, 0.35f), new Color(0.80f, 0.55f, 0.45f), new Color(0.95f, 0.55f, 0.25f)
    };

    private int _want, _qty, _pick = -1, _count;
    private float _patience, _patienceMax, _pauseUntil;
    private bool _serving, _handOver, _freeServe;
    private int _undoTokens;
    private Label _bubble, _tray;
    private VisualElement _fill;
    private readonly Button[] _snackBtns = new Button[4];

    protected override string Code => "E2";
    protected override string QuestName => "Snack Stall";
    protected override string Intro => "Jun's snack stall is swamped at the fair. Serve the kids what they ask for.";
    protected override string CardTitle => "Run Jun's snack stall";

    protected override IEnumerator Play()
    {
        _undoTokens = 0;
        UI.SetMessage("Tap the snack once for each piece, then Hand over.", vc_MinigameOverlay.Ink);

        _bubble = vc_MinigameOverlay.MakeLabel("", 30);
        _bubble.style.backgroundColor = Color.white;
        _bubble.style.width = 460; _bubble.style.minHeight = 60;
        Border(_bubble, 3, vc_MinigameOverlay.Ink);
        UI.Content.Add(_bubble);

        var bar = Box(UI.Content, 460, 14, new Color(0.3f, 0.25f, 0.27f));
        bar.style.marginTop = 6;
        _fill = Box(bar, 460, 14, vc_MinigameOverlay.Good);

        var snacks = Row(UI.Content, 10);
        for (int i = 0; i < Snacks.Length; i++)
        {
            int k = i;
            var b = SmallButton(snacks, Snacks[i], () => Pick(k), 170);
            b.style.backgroundColor = SnackCols[i];
            _snackBtns[i] = b;
        }
        _tray = vc_MinigameOverlay.MakeLabel("", 24);
        _tray.style.marginTop = 6;
        UI.Content.Add(_tray);
        var acts = Row(UI.Content, 2);
        SmallButton(acts, "Clear", () => { _pick = -1; _count = 0; ShowTray(); }, 140);
        SmallButton(acts, "Hand over", () => { if (_serving && _count > 0) _handOver = true; }, 220);
        BuildHelpers(UI.Content);

        int served = 0;
        for (int kid = 0; kid < Kids; kid++)
        {
            UI.SetStatus($"Kid {kid + 1} of {Kids}   Served: {served}");
            _want = Random.Range(0, Snacks.Length);
            _qty = Random.Range(1, 4);
            _pick = -1; _count = 0; ShowTray();
            _bubble.text = $"\"{_qty} {Snacks[_want]}{(_qty > 1 && _want != 3 ? "s" : "")}, please!\"";
            _patienceMax = Mathf.Max(9f, 15f - kid * 0.6f) + Random.Range(-1f, 1f);
            _patience = _patienceMax;
            _handOver = false; _freeServe = false;
            UI.SetMessage("Tap the snack once for each piece, then Hand over.", vc_MinigameOverlay.Ink);
            _serving = true;
            HelperBar?.Refresh();

            bool ok = false;
            while (true)
            {
                if (_freeServe) { ok = true; break; }
                if (_handOver)
                {
                    _handOver = false;
                    ok = _pick == _want && _count == _qty;
                    if (!ok && _undoTokens > 0)
                    {
                        _undoTokens--;
                        _pick = -1; _count = 0; ShowTray();
                        UI.SetMessage("Oops, wrong order! You take it back. Try again.", vc_MinigameOverlay.Good);
                        continue;
                    }
                    break;
                }
                if (Time.time >= _pauseUntil) _patience -= Time.deltaTime;
                _fill.style.width = 460f * Mathf.Clamp01(_patience / _patienceMax);
                _fill.style.backgroundColor = _patience / _patienceMax < 0.3f ? vc_MinigameOverlay.Bad : vc_MinigameOverlay.Good;
                if (_patience <= 0f) break;
                yield return null;
            }
            _serving = false;
            HelperBar?.Refresh();
            foreach (var b in _snackBtns) Border(b, 0, Gold);

            if (ok) served++;
            UI.SetMessage(ok ? "The kid walks off happily eating!" :
                          _patience <= 0f ? "The kid got tired of waiting and left." : "\"That's not what I asked for...\"",
                          ok ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Bad);
            SunodGame.Core.vc_Sfx.Play(ok ? "step_done" : "clack", 0.5f);
            _bubble.text = ok ? ":)" : ":(";
            yield return new WaitForSeconds(0.9f);
        }

        Score = 100f * served / Kids;
        UI.SetStatus($"Served: {served} of {Kids}");
        UI.SetMessage("SOLD OUT! The fair rush is over.", vc_MinigameOverlay.Good);
        yield return new WaitForSeconds(1.6f);
    }

    private void Pick(int k)
    {
        if (!_serving) return;
        if (_pick != k) { _pick = k; _count = 0; }
        _count = Mathf.Min(_count + 1, 5);
        SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.1f);
        ShowTray();
    }

    private void ShowTray() =>
        _tray.text = _pick < 0 ? "Tray: empty" : $"Tray: {_count} x {Snacks[_pick]}";

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e) => _serving;

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                Border(_snackBtns[_want], 5, Gold);
                UI.SetMessage($"{s.DisplayName}: the glowing snack is what they want.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                _pick = _want; _count = _qty; ShowTray();
                _freeServe = true;
                UI.SetMessage($"{s.DisplayName}: this kid is served for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _undoTokens++;
                UI.SetMessage($"{s.DisplayName}: if you hand over a wrong order, you can take it back.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                _pauseUntil = Time.time + 5f;
                UI.SetMessage($"{s.DisplayName}: the kid happily waits a bit longer.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Every kid got their snack. Jun's stall sold out!" :
        score >= 50f ? "Most kids went home happy. Jun says thanks!" :
        "It was a tough rush, but Jun is glad you helped.";
}
