using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A4 Bea - Class song (MINIGAME_DESIGN.md, type G Make to a Brief). New gameplay only. No timer.
/// A 4-line verse (AABB) with the last word of each line blank; dots under the blank show its beats.
/// Pick 1 of 3 words per line. Per line: rhymes with its pair 15% + fits the beats 10%.
/// Helpers: Hint = the best word glows; Free step = the line is filled right;
/// Second chance = re-pick the previous line; Breather = check a word before committing.
/// </summary>
[DisallowMultipleComponent]
public class vc_ClassSongMinigame : vc_MinigameQuest
{
    private struct Opt { public string W, Rhyme; public int Syl; public Opt(string w, string r, int s) { W = w; Rhyme = r; Syl = s; } }
    private struct Line { public string Text, Rhyme; public int Beats; public Opt[] Opts; }

    private static Line L(string text, string rhyme, int beats, params Opt[] o) => new Line { Text = text, Rhyme = rhyme, Beats = beats, Opts = o };
    private static Opt O(string w, string r, int s) => new Opt(w, r, s);

    private static readonly (string title, Line[] lines)[] Verses =
    {
        ("Off to School", new[]
        {
            L("Every morning we go out to", "ay", 1, O("play", "ay", 1), O("today", "ay", 2), O("sing", "ing", 1)),
            L("Under the sun on a bright school", "ay", 1, O("day", "ay", 1), O("holiday", "ay", 3), O("time", "ime", 1)),
            L("We share our snacks and we learn to", "eed", 1, O("read", "eed", 1), O("succeed", "eed", 2), O("write", "ite", 1)),
            L("Our teacher helps with all that we", "eed", 1, O("need", "eed", 1), O("indeed", "eed", 2), O("want", "ant", 1)),
        }),
        ("My Friends and I", new[]
        {
            L("My friends and I like to run and", "ide", 1, O("hide", "ide", 1), O("outside", "ide", 2), O("jump", "ump", 1)),
            L("We laugh so loud on the shiny", "ide", 1, O("slide", "ide", 1), O("worldwide", "ide", 2), O("swing", "ing", 1)),
            L("When it gets dark we look at the", "oon", 1, O("moon", "oon", 1), O("afternoon", "oon", 3), O("stars", "ars", 1)),
            L("We wave goodbye and we'll meet", "oon", 1, O("soon", "oon", 1), O("balloon", "oon", 2), O("late", "ate", 1)),
        }),
        ("Our Little Town", new[]
        {
            L("The river shines and the birds all", "ing", 1, O("sing", "ing", 1), O("everything", "ing", 3), O("fly", "y", 1)),
            L("The church bells start to happily", "ing", 1, O("ring", "ing", 1), O("evening", "ing", 2), O("chime", "ime", 1)),
            L("We plant some seeds and we watch them", "ow", 1, O("grow", "ow", 1), O("below", "ow", 2), O("rise", "ise", 1)),
            L("Our little town is the best I", "ow", 1, O("know", "ow", 1), O("although", "ow", 2), O("see", "ee", 1)),
        }),
    };

    private Line[] _lines;
    private readonly int[] _choice = { -1, -1, -1, -1 };
    private Label[] _lineLabels = new Label[4];
    private VisualElement _opts;
    private int _cur;
    private bool _picking, _checkFirst, _redoPrev, _freeLine;
    private int _picked;
    private readonly List<Button> _optBtns = new List<Button>();

    protected override string Code => "A4";
    protected override string QuestName => "Class Song";
    protected override string Intro => "Bea's class needs a song. Pick the words that rhyme and fit the beat.";
    protected override string CardTitle => "Finish the class song";

    protected override IEnumerator Play()
    {
        var verse = Verses[Random.Range(0, Verses.Length)];
        _lines = verse.lines;
        for (int i = 0; i < 4; i++) _choice[i] = -1;
        UI.SetTitle($"Class song: \"{verse.title}\"");
        UI.SetMessage("Pick a last word for each line. Lines 1+2 and 3+4 should rhyme. Dots = beats.", vc_MinigameOverlay.Ink);

        var sheet = new VisualElement();
        sheet.style.backgroundColor = new Color(1f, 0.98f, 0.90f);
        sheet.style.width = 700;
        sheet.style.paddingLeft = 14; sheet.style.paddingTop = 6; sheet.style.paddingBottom = 6;
        UI.Content.Add(sheet);
        for (int i = 0; i < 4; i++)
        {
            _lineLabels[i] = vc_MinigameOverlay.MakeLabel("", 24);
            _lineLabels[i].enableRichText = true;
            _lineLabels[i].style.unityTextAlign = TextAnchor.MiddleLeft;
            sheet.Add(_lineLabels[i]);
        }
        _opts = Row(UI.Content, 10);
        BuildHelpers(UI.Content);

        _cur = 0;
        while (_cur < 4)
        {
            UI.SetStatus($"Line {_cur + 1} of 4");
            Render();
            BuildOptions();
            _picked = -1; _redoPrev = false; _freeLine = false;
            _picking = true;
            HelperBar?.Refresh();
            while (_picked < 0 && !_redoPrev && !_freeLine) yield return null;
            _picking = false;

            if (_redoPrev)
            {
                _cur = Mathf.Max(0, _cur - 1);
                _choice[_cur] = -1;
                UI.SetMessage("Pick that line's word again.", vc_MinigameOverlay.Good);
                continue;
            }
            if (_freeLine) _picked = BestIndex(_cur);
            _choice[_cur] = _picked;
            SunodGame.Core.vc_Sfx.Play("clack", 0.4f, 0.1f);
            _cur++;
        }
        _opts.Clear();
        Render();

        Score = 0f;
        for (int i = 0; i < 4; i++)
        {
            Opt o = _lines[i].Opts[_choice[i]];
            if (o.Rhyme == _lines[i].Rhyme) Score += 15f;
            if (o.Syl == _lines[i].Beats) Score += 10f;
        }
        UI.SetStatus("Bea sings your song!");
        UI.SetMessage(Score >= 90f ? "It rhymes and flows perfectly. The class sings along!" :
                      Score >= 50f ? "Nice song! A couple of words trip over the beat." :
                      "The rhymes are a bit off, but Bea sings it proudly anyway.",
                      Score >= 70f ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
        yield return new WaitForSeconds(2.4f);
    }

    private int BestIndex(int line)
    {
        for (int k = 0; k < 3; k++) { Opt o = _lines[line].Opts[k]; if (o.Rhyme == _lines[line].Rhyme && o.Syl == _lines[line].Beats) return k; }
        return 0;
    }

    private void Render()
    {
        for (int i = 0; i < 4; i++)
        {
            string dots = new string('o', _lines[i].Beats).Replace("o", "• ").Trim();
            // while picking a chosen word is green; once the song is done, green = rhymes and fits, red = not
            bool good = _choice[i] >= 0 && (_cur < 4 || _choice[i] == BestIndex(i));
            string word = _choice[i] >= 0 ? $"<color={(good ? "#3B8C3C" : "#C0392B")}><b>{_lines[i].Opts[_choice[i]].W}</b></color>"
                        : i == _cur ? $"<color=#C0392B>____</color>  <size=16>{dots}</size>" : "____";
            string mark = i == _cur && _choice[i] < 0 ? "> " : "   ";
            _lineLabels[i].text = $"{mark}{_lines[i].Text} {word}";
        }
    }

    private void BuildOptions()
    {
        _opts.Clear();
        _optBtns.Clear();
        var order = new List<int> { 0, 1, 2 };
        Shuffle(order);
        foreach (int k in order)
        {
            int kk = k;
            var b = SmallButton(_opts, _lines[_cur].Opts[k].W, () => Choose(kk), 200);
            _optBtns.Add(b);
        }
    }

    private void Choose(int k)
    {
        if (!_picking) return;
        if (_checkFirst)
        {
            _checkFirst = false;
            Opt o = _lines[_cur].Opts[k];
            bool rh = o.Rhyme == _lines[_cur].Rhyme, fit = o.Syl == _lines[_cur].Beats;
            UI.SetMessage($"\"{o.W}\": " + (rh && fit ? "rhymes and fits the beat. Tap it again to keep it."
                         : rh ? "rhymes, but has too many beats." : fit ? "fits the beat, but doesn't rhyme." : "doesn't rhyme or fit."),
                         vc_MinigameOverlay.Good);
            HelperBar?.Refresh();
            return;
        }
        _picked = k;
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e)
    {
        if (!_picking) return false;
        if (e == vc_SkillHelpers.Effect.SecondChance) return _cur > 0;
        if (e == vc_SkillHelpers.Effect.Breather) return !_checkFirst;
        return true;
    }

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
            {
                string best = _lines[_cur].Opts[BestIndex(_cur)].W;
                foreach (var b in _optBtns) if (b.text == best) Border(b, 4, Gold);
                UI.SetMessage($"{s.DisplayName}: the glowing word fits best.", vc_MinigameOverlay.Good);
                break;
            }
            case vc_SkillHelpers.Effect.FreeStep:
                _freeLine = true;
                UI.SetMessage($"{s.DisplayName}: this line is written for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _redoPrev = true;
                UI.SetMessage($"{s.DisplayName}: go back and change the last word.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                _checkFirst = true;
                UI.SetMessage($"{s.DisplayName}: tap a word to check it before you keep it.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Bea's class sings your song at the assembly!" :
        score >= 50f ? "Bea's class has a song now. Not bad at all!" :
        "The song is a little bumpy, but the class loves it.";
}
