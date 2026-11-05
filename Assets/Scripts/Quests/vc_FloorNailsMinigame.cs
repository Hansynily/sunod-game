using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// R8 Lolo Ador - Creaky floorboards (MINIGAME_DESIGN.md, type A Timing Tap). New gameplay only.
/// 3 loose boards, 6 nails sticking up. For each nail a power meter bounces: tap Hammer in
/// green = flush, yellow = half way (swing again), red = bent. Flush in 1 swing = full,
/// 2 swings = 60%, bent = 0. Score = average of the 6 nails. No timer.
/// Helpers: Hint = the green zone glows and grows for this nail; Free step = this nail goes in
/// flush; Second chance = a bent nail is pulled out to try again; Breather = the meter stops 5 s.
/// </summary>
[DisallowMultipleComponent]
public class vc_FloorNailsMinigame : vc_MinigameQuest
{
    private const int Nails = 6;
    private const float MeterW = 560f;

    private static readonly Color Wood = new Color(0.62f, 0.42f, 0.26f);
    private static readonly Color WoodDark = new Color(0.45f, 0.30f, 0.18f);
    private static readonly Color Red = new Color(0.85f, 0.30f, 0.25f);
    private static readonly Color Yellow = new Color(0.95f, 0.80f, 0.30f);
    private static readonly Color Green = new Color(0.35f, 0.72f, 0.35f);

    private enum Nail { Up, Half, Flush, Bent }
    private readonly Nail[] _state = new Nail[Nails];
    private readonly float[] _points = new float[Nails];
    private readonly VisualElement[] _nailEls = new VisualElement[Nails];

    private VisualElement _meter, _green, _yellow, _marker;
    private float _pos, _dir = 1f, _speed, _greenW, _yellowW, _greenX, _pauseUntil;
    private int _current, _swings, _pullTokens;
    private bool _swing, _active, _freeNail;

    protected override string Code => "R8";
    protected override string QuestName => "Creaky Floorboards";
    protected override string Intro => "Lolo Ador's floor creaks. Hammer the loose nails down flat.";
    protected override string CardTitle => "Hammer the floorboards";

    protected override IEnumerator Play()
    {
        _pullTokens = 0; _pauseUntil = 0f;
        UI.SetMessage("Tap Hammer when the marker is in the green.", vc_MinigameOverlay.Ink);

        // 3 boards, 2 nails each
        var boards = new VisualElement();
        boards.style.width = 600;
        UI.Content.Add(boards);
        for (int b = 0; b < 3; b++)
        {
            var board = Box(boards, 600, 50, b % 2 == 0 ? Wood : WoodDark);
            board.style.marginTop = 4;
            for (int k = 0; k < 2; k++)
            {
                int i = b * 2 + k;
                var nail = Box(board, 14, 30, new Color(0.70f, 0.70f, 0.74f));
                nail.style.position = Position.Absolute;
                nail.style.left = Random.Range(60f, 240f) + k * 280f;
                nail.style.top = -6;
                _nailEls[i] = nail;
                _state[i] = Nail.Up;
                _points[i] = 0f;
            }
        }

        // power meter: red | yellow | green | yellow | red
        _meter = Box(UI.Content, MeterW, 34, Red);
        _meter.style.marginTop = 14;
        _yellow = Box(_meter, 0, 34, Yellow); _yellow.style.position = Position.Absolute;
        _green = Box(_meter, 0, 34, Green); _green.style.position = Position.Absolute;
        _marker = Box(_meter, 6, 46, vc_MinigameOverlay.Ink); _marker.style.position = Position.Absolute; _marker.style.top = -6;

        var row = Row(UI.Content, 8);
        SmallButton(row, "Hammer", () => { if (_active) _swing = true; }, 220);
        BuildHelpers(UI.Content);

        for (_current = 0; _current < Nails; _current++)
        {
            UI.SetStatus($"Nail {_current + 1} of {Nails}");
            SetZones(Random.Range(46f, 62f));
            _speed = Random.Range(380f, 470f) + _current * 20f;
            _swings = 0;
            Highlight();
            UI.SetMessage("Tap Hammer when the marker is in the green.", vc_MinigameOverlay.Ink);

            while (_state[_current] == Nail.Up || _state[_current] == Nail.Half)
            {
                _swing = false; _freeNail = false;
                _active = true;
                HelperBar?.Refresh();
                while (!_swing && !_freeNail)
                {
                    if (Time.time >= _pauseUntil)
                    {
                        _pos += _dir * _speed * Time.deltaTime;
                        if (_pos < 0f) { _pos = 0f; _dir = 1f; }
                        if (_pos > MeterW) { _pos = MeterW; _dir = -1f; }
                    }
                    _marker.style.left = _pos - 3f;
                    yield return null;
                }
                _active = false;
                _swings++;

                if (_freeNail) { _state[_current] = Nail.Flush; _points[_current] = 1f; }
                else
                {
                    float d = Mathf.Abs(_pos - (_greenX + _greenW / 2f));
                    if (d <= _greenW / 2f)
                    {
                        _state[_current] = Nail.Flush;
                        _points[_current] = _swings == 1 ? 1f : 0.6f;
                    }
                    else if (d <= _greenW / 2f + _yellowW)
                    {
                        if (_state[_current] == Nail.Half) { _state[_current] = Nail.Flush; _points[_current] = 0.6f; }
                        else _state[_current] = Nail.Half;
                    }
                    else
                    {
                        _state[_current] = Nail.Bent;
                        _points[_current] = 0f;
                    }
                }
                SunodGame.Core.vc_Sfx.Play("clack", 0.6f, 0.15f);
                Paint(_current);

                if (_state[_current] == Nail.Bent && _pullTokens > 0)
                {
                    _pullTokens--;
                    _state[_current] = Nail.Up;
                    _swings = 0;
                    yield return new WaitForSeconds(0.5f);
                    Paint(_current);
                    UI.SetMessage("Second chance: the bent nail is pulled out. Try again!", vc_MinigameOverlay.Good);
                    continue;
                }
                UI.SetMessage(_state[_current] == Nail.Flush ? (_points[_current] >= 1f ? "Flush! Perfect." : "Flush, in two swings.")
                            : _state[_current] == Nail.Half ? "Half way. Swing again!" : "Oops, the nail bent.",
                              _state[_current] == Nail.Bent ? vc_MinigameOverlay.Bad : vc_MinigameOverlay.Good);
                yield return new WaitForSeconds(0.45f);
            }
            _green.style.backgroundColor = Green;
            yield return new WaitForSeconds(0.35f);
        }

        Score = 0f;
        int creaks = 0;
        for (int i = 0; i < Nails; i++) { Score += _points[i]; if (_points[i] < 1f) creaks++; }
        Score = 100f * Score / Nails;
        UI.SetMessage(creaks == 0 ? "Lolo Ador walks across... not a single creak!"
                                  : $"Lolo Ador walks across... creak! ({creaks} nail{(creaks == 1 ? "" : "s")} not quite right)",
                      creaks <= 1 ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
        yield return new WaitForSeconds(1.8f);
    }

    private void SetZones(float greenW)
    {
        _greenW = greenW;
        _yellowW = 48f;
        _greenX = Random.Range(_yellowW + 10f, MeterW - _yellowW - greenW - 10f);
        _green.style.left = _greenX; _green.style.width = _greenW;
        _yellow.style.left = _greenX - _yellowW; _yellow.style.width = _greenW + _yellowW * 2f;
    }

    private void Highlight()
    {
        for (int i = 0; i < Nails; i++) Border(_nailEls[i], i == _current ? 3 : 0, Gold);
    }

    private void Paint(int i)
    {
        var n = _nailEls[i];
        switch (_state[i])
        {
            case Nail.Up: n.style.height = 30; n.style.top = -6; n.style.rotate = new Rotate(0); n.style.backgroundColor = new Color(0.70f, 0.70f, 0.74f); break;
            case Nail.Half: n.style.height = 18; n.style.top = 6; break;
            case Nail.Flush: n.style.height = 10; n.style.width = 14; n.style.top = 20; n.style.backgroundColor = new Color(0.50f, 0.50f, 0.55f); break;
            case Nail.Bent: n.style.height = 22; n.style.top = 0; n.style.rotate = new Rotate(35); n.style.backgroundColor = Red; break;
        }
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e) => _active;

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                SetZones(_greenW + 30f);
                _green.style.backgroundColor = new Color(0.55f, 0.95f, 0.45f);
                UI.SetMessage($"{s.DisplayName}: the green spot glows and gets bigger.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                _freeNail = true;
                UI.SetMessage($"{s.DisplayName}: this nail goes in flush for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _pullTokens++;
                UI.SetMessage($"{s.DisplayName}: if a nail bends, you can pull it out and retry.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                _pauseUntil = Time.time + 5f;
                UI.SetMessage($"{s.DisplayName}: the meter holds still for 5 seconds.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "The floor is quiet now. Lolo Ador can nap in peace!" :
        score >= 50f ? "Most of the creaks are gone. Lolo Ador smiles." :
        "A few boards still creak, but it's much better.";
}
