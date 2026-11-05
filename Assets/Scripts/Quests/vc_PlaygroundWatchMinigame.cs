using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// S6 Ate Mia - Playground watch (MINIGAME_DESIGN.md, type D Spot and Inspect). New gameplay only.
/// The playground from above: 5 kids play during recess (about 45 s). Now and then a kid needs you
/// (climbing too high, running to the gate, sitting alone, two kids arguing) and a small sign shows.
/// Tap that kid within a few seconds to step in. Tapping a kid who's fine costs a little.
/// Score = kids helped / 8 moments, minus 5% per wrong tap. Recess is the story timer, not a fail.
/// Helpers: Hint = the next kid who'll need you glows early; Free step = this moment is handled;
/// Second chance = a missed moment stays a bit longer; Breather = recess pauses 5 s.
/// </summary>
[DisallowMultipleComponent]
public class vc_PlaygroundWatchMinigame : vc_MinigameQuest
{
    private const int KidsN = 5, Moments = 8;
    private const float AreaW = 660f, AreaH = 250f, SignTime = 3.6f;
    private static readonly Color[] Shirts =
    {
        new Color(0.90f, 0.35f, 0.30f), new Color(0.35f, 0.55f, 0.95f), new Color(0.98f, 0.80f, 0.30f),
        new Color(0.45f, 0.75f, 0.40f), new Color(0.75f, 0.45f, 0.85f)
    };
    private static readonly (string sign, string what, Color col)[] Needs =
    {
        ("!", "climbing too high", new Color(0.95f, 0.30f, 0.25f)),
        (">>", "running to the gate", new Color(0.95f, 0.55f, 0.15f)),
        ("...", "sitting alone", new Color(0.45f, 0.55f, 0.85f)),
        ("#!", "arguing", new Color(0.85f, 0.25f, 0.55f)),
    };

    private class Kid { public int Id; public Vector2 Pos, To; public VisualElement El; public Label Sign; public bool Needs; }

    private readonly List<Kid> _kids = new List<Kid>();
    private int _tapped = -1, _extraTokens;
    private bool _playing, _free;
    private float _pauseUntil;
    private int _nextKid = -1;

    protected override string Code => "S6";
    protected override string QuestName => "Playground Watch";
    protected override string Intro => "Ate Mia is watching the kids at recess. Help her keep everyone safe and happy.";
    protected override string CardTitle => "Watch the playground";

    protected override IEnumerator Play()
    {
        _kids.Clear(); _extraTokens = 0; _pauseUntil = 0f; _free = false;
        UI.SetMessage("When a sign pops up over a kid, tap that kid to step in. Leave the others be!", vc_MinigameOverlay.Ink);

        var area = Box(UI.Content, AreaW, AreaH, new Color(0.62f, 0.80f, 0.45f));
        area.style.overflow = Overflow.Hidden;
        Border(area, 3, new Color(0.45f, 0.35f, 0.25f));
        // props: slide, swings, sandbox, gate
        Prop(area, "slide", 40, 30, 90, 50, new Color(0.85f, 0.55f, 0.30f));
        Prop(area, "swings", 270, 20, 120, 40, new Color(0.60f, 0.50f, 0.40f));
        Prop(area, "sand", 480, 150, 120, 70, new Color(0.92f, 0.85f, 0.60f));
        Prop(area, "gate", AreaW - 26, 90, 20, 70, new Color(0.45f, 0.35f, 0.25f));

        for (int i = 0; i < KidsN; i++)
        {
            var k = new Kid { Id = i, Pos = RandPos(), To = RandPos() };
            k.El = new VisualElement();
            k.El.style.position = Position.Absolute;
            k.El.style.width = 44; k.El.style.height = 60;
            var head = Box(k.El, 22, 22, new Color(0.95f, 0.80f, 0.65f));
            head.style.position = Position.Absolute; head.style.left = 11; head.style.top = 14;
            head.style.borderTopLeftRadius = 11; head.style.borderTopRightRadius = 11; head.style.borderBottomLeftRadius = 11; head.style.borderBottomRightRadius = 11;
            var body = Box(k.El, 30, 22, Shirts[i]);
            body.style.position = Position.Absolute; body.style.left = 7; body.style.top = 36;
            body.style.borderTopLeftRadius = 8; body.style.borderTopRightRadius = 8;
            k.Sign = vc_MinigameOverlay.MakeLabel("", 18);
            k.Sign.style.position = Position.Absolute; k.Sign.style.left = 4; k.Sign.style.top = -6;
            k.Sign.style.width = 36; k.Sign.style.height = 20;
            k.Sign.style.color = Color.white;
            k.Sign.style.display = DisplayStyle.None;
            k.El.Add(k.Sign);
            int id = i;
            k.El.RegisterCallback<PointerDownEvent>(_ => _tapped = id);
            area.Add(k.El);
            _kids.Add(k);
        }
        BuildHelpers(UI.Content);

        int helped = 0, wrong = 0;
        _playing = true;
        HelperBar?.Refresh();
        var order = new List<int>();
        for (int m = 0; m < Moments; m++) order.Add(m % 4);
        Shuffle(order);
        _nextKid = Random.Range(0, KidsN);

        for (int m = 0; m < Moments; m++)
        {
            UI.SetStatus($"Recess... moments handled: {helped} of {m}");
            // calm play between moments
            float gap = Random.Range(1.6f, 2.8f);
            float t = 0f;
            while (t < gap)
            {
                if (Tick()) t += Time.deltaTime;
                if (_tapped >= 0) { wrong++; Ouch(_tapped); _tapped = -1; }
                yield return null;
            }

            var need = Needs[order[m]];
            Kid kid = _kids[_nextKid];
            Kid buddy = null;
            if (order[m] == 3) { buddy = _kids[(_nextKid + 1 + Random.Range(0, KidsN - 1)) % KidsN]; buddy.Pos = kid.Pos + new Vector2(kid.Pos.x > AreaW / 2 ? -46f : 46f, 0f); }
            if (order[m] == 0) kid.Pos = new Vector2(70f, 20f);        // up on the slide
            if (order[m] == 1) kid.To = new Vector2(AreaW - 70f, 105f); // heads for the gate
            ShowSign(kid, need.sign, need.col);
            if (buddy != null) ShowSign(buddy, need.sign, need.col);

            float left = SignTime;
            bool done = false;
            while (left > 0f && !done)
            {
                if (Tick(order[m] == 2 ? kid : null)) left -= Time.deltaTime;
                if (_free) { done = true; _free = false; }
                if (_tapped >= 0)
                {
                    if (_tapped == kid.Id || (buddy != null && _tapped == buddy.Id)) done = true;
                    else { wrong++; Ouch(_tapped); }
                    _tapped = -1;
                }
                if (left <= 0f && !done && _extraTokens > 0) { _extraTokens--; left = 2.5f; UI.SetMessage("Second chance: the kid still needs you!", vc_MinigameOverlay.Good); }
                yield return null;
            }
            HideSign(kid); if (buddy != null) HideSign(buddy);
            Border(kid.El, 0, Gold);
            if (done) helped++;
            UI.SetMessage(done ? $"You step in. The kid who was {need.what} is fine now." : $"Ate Mia handles the kid who was {need.what}.",
                          done ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
            if (order[m] == 1) kid.To = RandPos();
            int nk; do nk = Random.Range(0, KidsN); while (nk == _nextKid && KidsN > 1);
            _nextKid = nk;
        }
        _playing = false;
        HelperBar?.Refresh();

        Score = 100f * helped / Moments - 5f * wrong;
        UI.SetStatus($"Kids helped: {helped} of {Moments}" + (wrong > 0 ? $"   Wrong taps: {wrong}" : ""));
        UI.SetMessage("The bell rings. Recess is over!", vc_MinigameOverlay.Good);
        yield return new WaitForSeconds(1.8f);
    }

    private void Prop(VisualElement area, string name, float x, float y, float w, float h, Color c)
    {
        var p = Box(area, w, h, c);
        p.style.position = Position.Absolute; p.style.left = x; p.style.top = y;
        p.pickingMode = PickingMode.Ignore;
        var l = vc_MinigameOverlay.MakeLabel(name, 13);
        l.style.width = Length.Percent(100); l.style.height = Length.Percent(100);
        p.Add(l);
    }

    private static Vector2 RandPos() => new Vector2(Random.Range(20f, AreaW - 100f), Random.Range(20f, AreaH - 70f));

    /// <summary>Moves the kids one frame. Returns false while paused.</summary>
    private bool Tick(Kid sitting = null)
    {
        if (Time.time < _pauseUntil) return false;
        foreach (var k in _kids)
        {
            if (k != sitting)
            {
                Vector2 d = k.To - k.Pos;
                if (d.magnitude < 5f) k.To = RandPos();
                else k.Pos += d.normalized * 60f * Time.deltaTime;
            }
            k.El.style.left = k.Pos.x; k.El.style.top = k.Pos.y;
        }
        return true;
    }

    private void ShowSign(Kid k, string s, Color c)
    {
        k.Needs = true;
        k.Sign.text = s;
        k.Sign.style.backgroundColor = c;
        k.Sign.style.display = DisplayStyle.Flex;
        SunodGame.Core.vc_Sfx.Play("idea", 0.35f);
    }

    private void HideSign(Kid k) { k.Needs = false; k.Sign.style.display = DisplayStyle.None; }

    private void Ouch(int id) => UI.SetMessage("That kid is playing happily. Leave them be!", vc_MinigameOverlay.Bad);

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e) => _playing;

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                Border(_kids[_nextKid].El, 3, Gold);
                UI.SetMessage($"{s.DisplayName}: keep an eye on the glowing kid.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                _free = true;
                UI.SetMessage($"{s.DisplayName}: the next moment is handled for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _extraTokens++;
                UI.SetMessage($"{s.DisplayName}: if you miss a moment, it stays a little longer.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                _pauseUntil = Time.time + 5f;
                UI.SetMessage($"{s.DisplayName}: recess pauses for 5 seconds.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Every kid was safe and happy. Ate Mia gives you a big thumbs up!" :
        score >= 50f ? "Recess went well. Ate Mia is glad you were there." :
        "Recess was busy, but Ate Mia thanks you for helping.";
}
