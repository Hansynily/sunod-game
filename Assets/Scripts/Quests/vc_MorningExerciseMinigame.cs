using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// S4 Tita Lorna - Morning exercise (MINIGAME_DESIGN.md, type F Copy and Cue). New gameplay only.
/// 1) Show her: a routine card of 4 poses; on a slow "1-2-3-4" count tap the pose for each step.
/// 2) Help her: she copies it but gets 2 poses wrong. Tap the pose she got wrong, then pick the
///    right one. Score: poses shown on the count 60% (15 each) + mistakes fixed 40% (20 each).
/// The count is slow and forgiving; nothing else is timed.
/// Helpers: Hint = the right pose / a wrong card glows; Free step = this step is done for you;
/// Second chance = a missed count step is repeated / a wrong fix can be retried;
/// Breather = the count pauses 5 s, or check a card before choosing.
/// </summary>
[DisallowMultipleComponent]
public class vc_MorningExerciseMinigame : vc_MinigameQuest
{
    private static readonly string[] Poses = { "Arms up", "Touch toes", "Side stretch", "March", "Jumping jack", "Squat" };
    private const float BeatTime = 2.4f;

    private int[] _routine, _herCopy;
    private readonly List<Button> _poseBtns = new List<Button>();
    private readonly List<VisualElement> _cards = new List<VisualElement>();
    private Label _you, _her;
    private int _tapped = -1, _cardTapped = -1, _retries;
    private bool _showing, _helping, _free, _checkCard;
    private float _pauseUntil;
    private int _phaseStep;

    protected override string Code => "S4";
    protected override string QuestName => "Morning Exercise";
    protected override string Intro => "Tita Lorna wants to learn a morning routine. Show her, then help her get it right.";
    protected override string CardTitle => "Teach the morning routine";

    protected override IEnumerator Play()
    {
        _retries = 0; _pauseUntil = 0f;
        var pool = new List<int> { 0, 1, 2, 3, 4, 5 };
        Shuffle(pool);
        _routine = pool.GetRange(0, 4).ToArray();

        // routine card
        var card = Row(UI.Content, 0);
        card.style.flexWrap = Wrap.NoWrap;
        for (int i = 0; i < 4; i++)
        {
            var c = Box(card, 150, 60, new Color(1f, 0.97f, 0.88f));
            c.style.marginLeft = 4; c.style.marginRight = 4;
            Border(c, 2, vc_MinigameOverlay.Ink);
            var l = vc_MinigameOverlay.MakeLabel($"{i + 1}. {Poses[_routine[i]]}", 18);
            l.style.width = Length.Percent(100); l.style.height = Length.Percent(100);
            c.Add(l);
            _cards.Add(c);
        }
        var people = Row(UI.Content, 6);
        _you = vc_MinigameOverlay.MakeLabel("You: ready", 22);
        _you.style.width = 300;
        _her = vc_MinigameOverlay.MakeLabel("Tita Lorna: watching", 22);
        _her.style.width = 340;
        people.Add(_you); people.Add(_her);

        var buttons = Row(UI.Content, 6);
        for (int p = 0; p < Poses.Length; p++)
        {
            int pp = p;
            _poseBtns.Add(SmallButton(buttons, Poses[p], () => TapPose(pp), 170));
        }
        BuildHelpers(UI.Content);

        // ── 1) Show her ──
        float showPts = 0f;
        UI.SetMessage("Show her! Tap each pose on its count.", vc_MinigameOverlay.Ink);
        for (_phaseStep = 0; _phaseStep < 4; _phaseStep++)
        {
            for (int i = 0; i < 4; i++) Border(_cards[i], i == _phaseStep ? 4 : 2, i == _phaseStep ? Gold : vc_MinigameOverlay.Ink);
            for (int count = 3; count >= 1; count--) { UI.SetStatus($"Step {_phaseStep + 1} of 4 ... {count}"); yield return Wait(0.35f); }
            UI.SetStatus($"Step {_phaseStep + 1} of 4 ... GO!");
            _tapped = -1; _free = false;
            _showing = true;
            HelperBar?.Refresh();
            float t = 0f;
            while (t < BeatTime && _tapped < 0 && !_free)
            {
                if (Time.time >= _pauseUntil) t += Time.deltaTime;
                yield return null;
            }
            _showing = false;
            ClearGlow();
            bool ok = _free || _tapped == _routine[_phaseStep];
            if (!ok && _retries > 0)
            {
                _retries--;
                UI.SetMessage(_tapped < 0 ? "Missed the count. Second chance, again!" : "Wrong pose. Second chance, again!", vc_MinigameOverlay.Good);
                _phaseStep--;
                yield return Wait(0.8f);
                continue;
            }
            if (ok) showPts += 15f;
            _you.text = ok ? $"You: {Poses[_routine[_phaseStep]]}!" : "You: (oops)";
            UI.SetMessage(ok ? "Nice!" : _tapped < 0 ? "Too slow for that count." : "That wasn't the pose on the card.",
                          ok ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Bad);
            yield return Wait(0.6f);
        }

        // ── 2) Help her ──
        _herCopy = (int[])_routine.Clone();
        var wrongIdx = new List<int> { 0, 1, 2, 3 };
        Shuffle(wrongIdx);
        var mistakes = new List<int> { wrongIdx[0], wrongIdx[1] };
        foreach (int m in mistakes)
        {
            int bad;
            do bad = Random.Range(0, Poses.Length); while (bad == _routine[m]);
            _herCopy[m] = bad;
        }
        for (int i = 0; i < 4; i++)
        {
            int ii = i;
            var l = _cards[i].Q<Label>();
            l.text = $"{i + 1}. {Poses[_herCopy[i]]}";
            _cards[i].style.backgroundColor = new Color(0.95f, 0.90f, 1f);
            _cards[i].RegisterCallback<PointerDownEvent>(_ => TapCard(ii));
            Border(_cards[i], 2, vc_MinigameOverlay.Ink);
        }
        _you.text = "You: watching";
        _her.text = "Tita Lorna: \"Like this?\"";
        UI.SetMessage("Her copy is on the cards. Tap a pose she got wrong, then the right pose.", vc_MinigameOverlay.Ink);

        float fixPts = 0f;
        var fixedSet = new HashSet<int>();
        int handled = 0;
        while (handled < 2)
        {
            UI.SetStatus($"Mistakes to fix: {2 - handled}");
            _cardTapped = -1; _tapped = -1; _free = false;
            _helping = true;
            HelperBar?.Refresh();
            while (_cardTapped < 0 && !_free) yield return null;
            if (_free)
            {
                foreach (int m in mistakes) if (!fixedSet.Contains(m)) { _cardTapped = m; _tapped = _routine[m]; break; }
            }
            else
            {
                if (!mistakes.Contains(_cardTapped) || fixedSet.Contains(_cardTapped))
                {
                    _helping = false;
                    UI.SetMessage($"Step {_cardTapped + 1} looks right already!", vc_MinigameOverlay.Bad);
                    fixPts -= 5f;
                    yield return Wait(0.7f);
                    UI.SetMessage("Tap a pose she got wrong, then the right pose.", vc_MinigameOverlay.Ink);
                    continue;
                }
                Border(_cards[_cardTapped], 4, Gold);
                UI.SetMessage($"Yes, step {_cardTapped + 1} is wrong. Which pose should it be?", vc_MinigameOverlay.Good);
                while (_tapped < 0 && !_free) yield return null;
                if (_free) _tapped = _routine[_cardTapped];
            }
            _helping = false;
            int step = _cardTapped;
            bool right = _tapped == _routine[step];
            if (!right && _retries > 0)
            {
                _retries--;
                UI.SetMessage("Not that one. Second chance, try another pose.", vc_MinigameOverlay.Good);
                Border(_cards[step], 2, vc_MinigameOverlay.Ink);
                yield return Wait(0.6f);
                continue;
            }
            fixedSet.Add(step);
            handled++;
            if (right)
            {
                fixPts += 20f;
                _herCopy[step] = _routine[step];
                _cards[step].Q<Label>().text = $"{step + 1}. {Poses[_routine[step]]}";
                _cards[step].style.backgroundColor = new Color(0.85f, 0.95f, 0.80f);
                _her.text = "Tita Lorna: \"Ah, like that!\" :)";
            }
            else _her.text = "Tita Lorna: \"Hmm, I'm still not sure...\"";
            Border(_cards[step], 2, vc_MinigameOverlay.Ink);
            UI.SetMessage(right ? "She fixes it and smiles." : "That wasn't the pose on the routine.",
                          right ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Bad);
            yield return Wait(0.8f);
        }

        Score = showPts + Mathf.Max(0f, fixPts);
        UI.SetStatus("Together: 1 - 2 - 3 - 4!");
        _you.text = "You: stretching";
        _her.text = Score >= 70f ? "Tita Lorna: stretching perfectly!" : "Tita Lorna: doing her best!";
        UI.SetMessage(Score >= 90f ? "You both do the routine in perfect time!" : "You do the routine together.", vc_MinigameOverlay.Good);
        yield return Wait(1.8f);
    }

    private IEnumerator Wait(float s)
    {
        float t = 0f;
        while (t < s) { if (Time.time >= _pauseUntil) t += Time.deltaTime; yield return null; }
    }

    private void TapPose(int p)
    {
        if (_showing) { _tapped = p; SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.1f); }
        else if (_helping && _cardTapped >= 0) { _tapped = p; SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.1f); }
    }

    private void TapCard(int i)
    {
        if (!_helping || _cardTapped >= 0) return;
        if (_checkCard)
        {
            _checkCard = false;
            bool wrong = _herCopy[i] != _routine[i];
            UI.SetMessage($"Step {i + 1}: " + (wrong ? "she got this one wrong." : "this one is right."), vc_MinigameOverlay.Good);
            HelperBar?.Refresh();
            return;
        }
        _cardTapped = i;
    }

    private void ClearGlow() { foreach (var b in _poseBtns) Border(b, 0, Gold); }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e)
    {
        if (!_showing && !_helping) return false;
        if (e == vc_SkillHelpers.Effect.Breather && _helping) return !_checkCard && _cardTapped < 0;
        return true;
    }

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                if (_showing) { Border(_poseBtns[_routine[_phaseStep]], 4, Gold); UI.SetMessage($"{s.DisplayName}: the glowing pose is next.", vc_MinigameOverlay.Good); }
                else if (_cardTapped >= 0) { Border(_poseBtns[_routine[_cardTapped]], 4, Gold); UI.SetMessage($"{s.DisplayName}: the glowing pose is the right one.", vc_MinigameOverlay.Good); }
                else
                {
                    for (int i = 0; i < 4; i++) if (_herCopy[i] != _routine[i]) { Border(_cards[i], 4, Gold); break; }
                    UI.SetMessage($"{s.DisplayName}: the glowing card is one she got wrong.", vc_MinigameOverlay.Good);
                }
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                _free = true;
                UI.SetMessage($"{s.DisplayName}: this step is done for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _retries++;
                UI.SetMessage($"{s.DisplayName}: if you slip up, you can try that step again.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                if (_showing) { _pauseUntil = Time.time + 5f; UI.SetMessage($"{s.DisplayName}: the count pauses for 5 seconds.", vc_MinigameOverlay.Good); }
                else { _checkCard = true; UI.SetMessage($"{s.DisplayName}: tap a card to check it first.", vc_MinigameOverlay.Good); }
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Tita Lorna has a perfect routine now. She'll do it every morning!" :
        score >= 50f ? "Tita Lorna learned most of the routine. She feels great!" :
        "The routine was a bit mixed up, but Tita Lorna enjoyed it.";
}
