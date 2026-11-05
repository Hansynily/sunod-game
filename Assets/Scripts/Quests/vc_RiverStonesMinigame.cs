using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// R2 Mang Ben - River crossing stones (MINIGAME_DESIGN.md, type A Timing Tap). New gameplay only.
/// A stone slides left/right over the river; tap Drop when it is over the faint outline.
/// 5 spots, the slide gets faster each time. Centred = 20, near = 10, splash = 0.
/// Helpers: Hint = centre line glows; Free step = next stone lands perfectly;
/// Second chance = redo the last stone; Breather = the stone stops for 5 s.
/// </summary>
[DisallowMultipleComponent]
public class vc_RiverStonesMinigame : vc_MinigameQuest
{
    private const float TrackW = 640f, StoneW = 56f, Spots = 5;
    private const float CentreTol = 10f, NearTol = 34f;

    private static readonly Color River = new Color(0.30f, 0.55f, 0.85f);
    private static readonly Color Bank = new Color(0.55f, 0.75f, 0.40f);
    private static readonly Color StoneC = new Color(0.55f, 0.53f, 0.56f);

    private VisualElement _stone, _target, _centreLine;
    private Label _result;
    private float _x, _dir = 1f, _speed, _targetX, _pauseUntil;
    private bool _drop, _freeNext, _canRedo, _redo, _active;
    private float[] _points = new float[5];
    private int _index;

    protected override string Code => "R2";
    protected override string QuestName => "River Crossing";
    protected override string Intro => "Mang Ben needs stepping stones. Drop each stone on a shallow spot.";
    protected override string CardTitle => "Lay the river stones";

    protected override IEnumerator Play()
    {
        UI.SetMessage("Tap Drop when the stone is over the outline.", vc_MinigameOverlay.Ink);

        var scene = Box(UI.Content, TrackW, 200, River);
        scene.style.marginTop = 4;
        var bankL = Box(scene, 30, 200, Bank); bankL.style.position = Position.Absolute; bankL.style.left = 0;
        var bankR = Box(scene, 30, 200, Bank); bankR.style.position = Position.Absolute; bankR.style.right = 0;

        _target = Box(scene, StoneW + 8, 30, new Color(1f, 1f, 1f, 0.18f));
        _target.style.position = Position.Absolute; _target.style.top = 140;
        Border(_target, 3, new Color(1f, 1f, 1f, 0.7f));
        _centreLine = Box(_target, 4, 30, Gold);
        _centreLine.style.position = Position.Absolute; _centreLine.style.left = (StoneW + 8) / 2f - 5f;
        _centreLine.style.display = DisplayStyle.None;

        _stone = Box(scene, StoneW, 26, StoneC);
        _stone.style.position = Position.Absolute; _stone.style.top = 30;
        Border(_stone, 3, new Color(0.35f, 0.33f, 0.36f));

        _result = vc_MinigameOverlay.MakeLabel("", 24);
        _result.style.position = Position.Absolute; _result.style.top = 70; _result.style.left = 0; _result.style.right = 0;
        _result.style.color = Color.white;
        scene.Add(_result);

        var row = Row(UI.Content);
        SmallButton(row, "Drop", () => { if (_active) _drop = true; }, 200);
        var redo = SmallButton(row, "Redo last stone", () => { if (_canRedo && !_active) _redo = true; }, 240);
        redo.style.display = DisplayStyle.None;
        BuildHelpers(UI.Content);

        _index = 0;
        while (_index < Spots)
        {
            UI.SetStatus($"Stone {_index + 1} of {Spots}");
            _targetX = Random.Range(60f, TrackW - 60f - StoneW);
            _target.style.left = _targetX - 4f;
            _speed = 230f + _index * 45f + Random.Range(-20f, 20f);
            _x = Random.Range(30f, TrackW - 30f - StoneW);
            _stone.style.top = 30;
            _result.text = "";
            _drop = false;
            _active = true;
            HelperBar?.Refresh();

            if (_freeNext)
            {
                _freeNext = false;
                _x = _targetX;
                _drop = true;
            }
            while (!_drop)
            {
                if (Time.time >= _pauseUntil)
                {
                    _x += _dir * _speed * Time.deltaTime;
                    if (_x < 30f) { _x = 30f; _dir = 1f; }
                    if (_x > TrackW - 30f - StoneW) { _x = TrackW - 30f - StoneW; _dir = -1f; }
                }
                _stone.style.left = _x;
                yield return null;
            }
            _active = false;
            _stone.style.left = _x;
            _stone.style.top = 140;

            float off = Mathf.Abs(_x - _targetX);
            float pts = off <= CentreTol ? 20f : off <= NearTol ? 10f : 0f;
            _points[_index] = pts;
            _result.text = pts >= 20f ? "Solid!" : pts > 0f ? "Wobbly..." : "Splash!";
            SunodGame.Core.vc_Sfx.Play(pts > 0f ? "clack" : "splash", 0.6f);
            _centreLine.style.display = DisplayStyle.None;

            _canRedo = false;
            HelperBar?.Refresh();
            if (pts < 20f && _redoTokens > 0)
            {
                _redoTokens--;
                _canRedo = true;
                redo.style.display = DisplayStyle.Flex;
                UI.SetMessage("Second chance: tap Redo, or wait to keep it.", vc_MinigameOverlay.Good);
                float until = Time.time + 3f;
                _redo = false;
                while (Time.time < until && !_redo) yield return null;
                redo.style.display = DisplayStyle.None;
                _canRedo = false;
                if (_redo) { UI.SetMessage("Try that stone again.", vc_MinigameOverlay.Ink); continue; }
            }
            yield return new WaitForSeconds(0.9f);
            _index++;
        }

        Score = 0f;
        foreach (float p in _points) Score += p;
        UI.SetMessage($"Mang Ben walks across... {Mathf.RoundToInt(Score)}% steady.", Score >= 70f ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
        yield return new WaitForSeconds(1.6f);
    }

    private int _redoTokens;

    protected override void OnBegin()
    {
        _redoTokens = 0; _freeNext = false; _pauseUntil = 0f;
        _points = new float[5];
        base.OnBegin();
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e)
    {
        if (!_active) return false;
        if (e == vc_SkillHelpers.Effect.FreeStep) return !_freeNext;
        return true;
    }

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                _centreLine.style.display = DisplayStyle.Flex;
                UI.SetMessage($"{s.DisplayName}: drop when the stone's middle meets the gold line.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                _x = _targetX; _drop = true;
                UI.SetMessage($"{s.DisplayName}: this stone goes right on the spot.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _redoTokens++;
                UI.SetMessage($"{s.DisplayName}: if a stone lands badly, you can redo it.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                _pauseUntil = Time.time + 5f;
                UI.SetMessage($"{s.DisplayName}: the stone holds still for 5 seconds.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Mang Ben crosses without a wobble!" :
        score >= 50f ? "Mang Ben makes it across, a little wobbly." :
        "Mang Ben hops over a few gaps, but he gets across.";
}
