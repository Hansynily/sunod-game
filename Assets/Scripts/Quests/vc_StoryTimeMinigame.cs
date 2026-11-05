using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// S7 Toto - Story time (MINIGAME_DESIGN.md, type H Talk and Match). New gameplay only. No timer.
/// Toto reads a story page; at each of 5 hard words he stops. Tap its syllable chips in order to
/// help him sound it out. Right first try 20%, second try 10%, else Toto gets it with help (0).
/// Helpers: Hint = the next syllable glows; Free step = the word is sounded out for you;
/// Second chance = one extra try on a word; Breather = check the chips placed so far.
/// </summary>
[DisallowMultipleComponent]
public class vc_StoryTimeMinigame : vc_MinigameQuest
{
    private class Story
    {
        public string Title;
        public string[] Parts;       // text with {0}..{4} for the hard words
        public string[][] Words;     // syllables of each hard word
    }

    private static readonly Story[] Stories =
    {
        new Story
        {
            Title = "The Garden Visit",
            Parts = new[] { "One sunny day a {0} landed on a {1}.", "It met a tiny {2} eating a leaf.",
                            "\"Come to the {3}!\" said the bird.", "They had a {4} under the mango tree." },
            Words = new[] { new[] { "but", "ter", "fly" }, new[] { "flow", "er" }, new[] { "cat", "er", "pil", "lar" },
                            new[] { "gar", "den" }, new[] { "pic", "nic" } }
        },
        new Story
        {
            Title = "Lola's Market Day",
            Parts = new[] { "Lola took a {0} to the market.", "She bought a ripe {1} and a {2}.",
                            "A {3} sold sweet rice cakes.", "They rode home on a {4}." },
            Words = new[] { new[] { "bas", "ket" }, new[] { "pa", "pa", "ya" }, new[] { "wa", "ter", "mel", "on" },
                            new[] { "ven", "dor" }, new[] { "tri", "cy", "cle" } }
        },
        new Story
        {
            Title = "The Rainy Afternoon",
            Parts = new[] { "Dark clouds rolled over the {0}.", "Ben opened his {1} and ran.",
                            "A {2} hid under a big leaf.", "After the rain came a {3} and a {4}." },
            Words = new[] { new[] { "moun", "tain" }, new[] { "um", "brel", "la" }, new[] { "frog", "let" },
                            new[] { "rain", "bow" }, new[] { "sun", "set" } }
        },
    };

    private Story _story;
    private Label _page;
    private VisualElement _chipRow, _answerRow;
    private readonly List<string> _placed = new List<string>();
    private readonly List<Button> _chips = new List<Button>();
    private string[] _current;
    private int _tries, _extraTries;
    private bool _freeWord, _waiting;

    protected override string Code => "S7";
    protected override string QuestName => "Story Time";
    protected override string Intro => "Toto is learning to read. Help him sound out the hard words.";
    protected override string CardTitle => "Read with Toto";

    protected override IEnumerator Play()
    {
        _story = Stories[Random.Range(0, Stories.Length)];
        _extraTries = 0;
        UI.SetTitle($"Read with Toto: {_story.Title}");

        _page = vc_MinigameOverlay.MakeLabel("", 24);
        _page.style.width = 720;
        _page.style.minHeight = 120;
        _page.style.backgroundColor = new Color(1f, 0.97f, 0.88f);
        _page.style.paddingLeft = 14; _page.style.paddingRight = 14; _page.style.paddingTop = 8; _page.style.paddingBottom = 8;
        _page.enableRichText = true;
        UI.Content.Add(_page);

        _answerRow = Row(UI.Content, 10);
        _answerRow.style.minHeight = 56;
        _chipRow = Row(UI.Content, 4);
        _chipRow.style.minHeight = 60;
        BuildHelpers(UI.Content);

        Score = 0f;
        string[] filled = new string[5];
        for (int w = 0; w < 5; w++) filled[w] = "____";

        for (int w = 0; w < 5; w++)
        {
            UI.SetStatus($"Hard word {w + 1} of 5");
            _current = _story.Words[w];
            filled[w] = "<color=#C0392B><b>?</b></color>";
            ShowPage(filled);
            UI.SetMessage("Toto stops and looks at you. Tap the parts in order.", vc_MinigameOverlay.Ink);

            _tries = 0;
            float got = 0f;
            bool solved = false;
            while (!solved)
            {
                _tries++;
                yield return AskWord();
                if (_freeWord) { _freeWord = false; got = _tries == 1 ? 20f : 10f; solved = true; break; }
                if (string.Join("", _placed) == string.Join("", _current))
                {
                    got = _tries == 1 ? 20f : 10f;
                    solved = true;
                }
                else
                {
                    int allowed = 2 + _extraTries;
                    if (_tries >= allowed) break;
                    UI.SetMessage("Hmm, that doesn't sound right. Try again!", vc_MinigameOverlay.Bad);
                    yield return new WaitForSeconds(0.9f);
                }
            }
            if (_tries > 2) _extraTries = Mathf.Max(0, _extraTries - (_tries - 2));

            Score += got;
            string word = string.Join("", _current);
            filled[w] = solved ? $"<color=#3B8C3C><b>{word}</b></color>" : $"<b>{word}</b>";
            ShowPage(filled);
            UI.SetMessage(solved ? $"\"{string.Join("-", _current)}... {word}!\" Toto reads on."
                                 : $"Toto sounds it out with you: {string.Join("-", _current)}.",
                          solved ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
            ClearRows();
            yield return new WaitForSeconds(1.3f);
        }

        UI.SetMessage("Toto reads the whole page by himself!", vc_MinigameOverlay.Good);
        UI.SetStatus($"Reading score {Mathf.RoundToInt(Score)}%");
        yield return new WaitForSeconds(1.8f);
    }

    private static bool Same(List<string> a, string[] b)
    {
        if (a.Count != b.Length) return false;
        for (int i = 0; i < b.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private void ShowPage(string[] filled)
    {
        var sb = new System.Text.StringBuilder();
        foreach (string part in _story.Parts)
        {
            sb.Append(string.Format(part, filled[0], filled[1], filled[2], filled[3], filled[4]));
            sb.Append(' ');
        }
        _page.text = sb.ToString();
    }

    private void ClearRows()
    {
        _answerRow.Clear();
        _chipRow.Clear();
        _chips.Clear();
        _placed.Clear();
    }

    private IEnumerator AskWord()
    {
        ClearRows();
        var order = new List<string>(_current);
        // shuffle until it isn't already in order
        for (int k = 0; k < 6; k++) { Shuffle(order); if (!Same(order, _current)) break; }

        foreach (string syl in order)
        {
            string s = syl;
            Button b = null;
            b = SmallButton(_chipRow, s, () =>
            {
                if (!_waiting) return;
                _placed.Add(s);
                b.SetEnabled(false);
                _chips.Remove(b);
                var placed = vc_MinigameOverlay.MakeLabel(s, 24);
                placed.style.backgroundColor = new Color(0.98f, 0.90f, 0.60f);
                placed.style.marginLeft = 3; placed.style.marginRight = 3;
                placed.style.paddingLeft = 10; placed.style.paddingRight = 10; placed.style.paddingTop = 6; placed.style.paddingBottom = 6;
                _answerRow.Add(placed);
                SunodGame.Core.vc_Sfx.Play("clack", 0.3f, 0.1f);
            }, 110);
            _chips.Add(b);
        }
        _waiting = true;
        HelperBar?.Refresh();
        while (_waiting && !_freeWord && _placed.Count < _current.Length) yield return null;
        _waiting = false;
        HelperBar?.Refresh();
        yield return new WaitForSeconds(0.3f);
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e) => _waiting;

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
            {
                string next = _placed.Count < _current.Length ? _current[_placed.Count] : null;
                bool ok = Same(new List<string>(_placed), Sub(_current, _placed.Count));
                if (!ok) { UI.SetMessage($"{s.DisplayName}: something's out of order already. Finish and try again.", vc_MinigameOverlay.Good); break; }
                foreach (Button b in _chips) if (b.text == next) { Border(b, 4, Gold); break; }
                UI.SetMessage($"{s.DisplayName}: the glowing part comes next.", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.FreeStep:
                _freeWord = true;
                UI.SetMessage($"{s.DisplayName}: you sound the word out together.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _extraTries++;
                UI.SetMessage($"{s.DisplayName}: one extra try on a hard word.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
            {
                bool ok = Same(new List<string>(_placed), Sub(_current, _placed.Count));
                UI.SetMessage(_placed.Count == 0 ? $"{s.DisplayName}: tap a part first, then check." :
                    ok ? $"{s.DisplayName}: so far so good!" : $"{s.DisplayName}: that order won't sound right.",
                    vc_MinigameOverlay.Good);
                break;
            }
        }
    }

    private static string[] Sub(string[] a, int n)
    {
        var r = new string[Mathf.Min(n, a.Length)];
        System.Array.Copy(a, r, r.Length);
        return r;
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Toto reads the whole story out loud. He's so proud!" :
        score >= 50f ? "Toto reads most of the page on his own now." :
        "Toto still needs practice, but he loved reading with you.";
}
