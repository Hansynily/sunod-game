using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// I6 Nico - Fish count (MINIGAME_DESIGN.md, type E Observe and Record). New gameplay only.
/// A pond with 3 kinds of fish swimming around for 20 s, some slipping under lily pads. Keep a
/// count of each kind with + / -. Then Nico lifts the net and the real counts show.
/// Score per kind: 100% minus 15% per fish off; average of 3. The 20 s is story, not a fail.
/// Helpers: Hint = one kind's real count; Free step = one counter set right;
/// Second chance = Nico waits 8 s more before lifting the net; Breather = the fish freeze 5 s.
/// </summary>
[DisallowMultipleComponent]
public class vc_FishCountMinigame : vc_MinigameQuest
{
    private const float PondW = 640f, PondH = 230f, Watch = 20f;
    private static readonly string[] Kinds = { "Red", "Blue", "Yellow" };
    private static readonly Color[] Cols =
    {
        new Color(0.90f, 0.35f, 0.30f), new Color(0.35f, 0.50f, 0.95f), new Color(0.98f, 0.85f, 0.30f)
    };

    private class Fish { public int Kind; public Vector2 Pos, Vel; public VisualElement El; public float Turn; }

    private readonly List<Fish> _fish = new List<Fish>();
    private readonly int[] _real = new int[3], _counted = new int[3];
    private readonly bool[] _locked = new bool[3];
    private readonly Label[] _countLabels = new Label[3];
    private readonly VisualElement[] _counterCols = new VisualElement[3];
    private bool _watching;
    private float _timeLeft, _freezeUntil;

    protected override string Code => "I6";
    protected override string QuestName => "Fish Count";
    protected override string Intro => "Nico is counting the fish in the pond. Count each kind before he lifts the net.";
    protected override string CardTitle => "Count the pond fish";

    protected override IEnumerator Play()
    {
        _fish.Clear(); _freezeUntil = 0f;
        UI.SetMessage("Count each kind of fish with + and -. Some hide under the lily pads!", vc_MinigameOverlay.Ink);

        var pond = Box(UI.Content, PondW, PondH, new Color(0.25f, 0.52f, 0.62f));
        pond.style.overflow = Overflow.Hidden;
        Border(pond, 4, new Color(0.45f, 0.65f, 0.35f));

        for (int k = 0; k < 3; k++)
        {
            _real[k] = Random.Range(2, 7);
            _counted[k] = 0; _locked[k] = false;
            for (int i = 0; i < _real[k]; i++)
            {
                var f = new Fish
                {
                    Kind = k,
                    Pos = new Vector2(Random.Range(20f, PondW - 50f), Random.Range(15f, PondH - 30f)),
                    Vel = Random.insideUnitCircle.normalized * Random.Range(30f, 60f),
                    Turn = Random.Range(1f, 3f)
                };
                f.El = Box(pond, 30, 14, Cols[k]);
                f.El.style.position = Position.Absolute;
                f.El.style.borderTopLeftRadius = 7; f.El.style.borderBottomLeftRadius = 7;
                f.El.style.borderTopRightRadius = 3; f.El.style.borderBottomRightRadius = 3;
                var eye = Box(f.El, 4, 4, vc_MinigameOverlay.Ink);
                eye.style.position = Position.Absolute; eye.style.left = 5; eye.style.top = 4;
                _fish.Add(f);
            }
        }
        // lily pads on top of the fish
        for (int p = 0; p < 4; p++)
        {
            var pad = Box(pond, Random.Range(70f, 95f), Random.Range(50f, 65f), new Color(0.35f, 0.65f, 0.30f));
            pad.style.position = Position.Absolute;
            pad.style.left = 30f + p * (PondW - 120f) / 3f + Random.Range(-15f, 15f);
            pad.style.top = Random.Range(10f, PondH - 75f);
            pad.style.borderTopLeftRadius = 30; pad.style.borderTopRightRadius = 30;
            pad.style.borderBottomLeftRadius = 30; pad.style.borderBottomRightRadius = 30;
        }

        var counters = Row(UI.Content, 6);
        counters.style.flexWrap = Wrap.NoWrap;
        for (int k = 0; k < 3; k++)
        {
            int kk = k;
            var col = new VisualElement();
            col.style.flexDirection = FlexDirection.Row;
            col.style.alignItems = Align.Center;
            col.style.marginLeft = 10; col.style.marginRight = 10;
            counters.Add(col);
            _counterCols[k] = col;
            var swatch = Box(col, 26, 14, Cols[k]);
            swatch.style.marginRight = 4;
            SmallButton(col, "-", () => SetCount(kk, _counted[kk] - 1), 44);
            _countLabels[k] = vc_MinigameOverlay.MakeLabel("0", 24);
            _countLabels[k].style.width = 40;
            col.Add(_countLabels[k]);
            SmallButton(col, "+", () => SetCount(kk, _counted[kk] + 1), 44);
        }
        BuildHelpers(UI.Content);

        _timeLeft = Watch;
        _watching = true;
        HelperBar?.Refresh();
        while (_timeLeft > 0f)
        {
            bool frozen = Time.time < _freezeUntil;
            if (!frozen) _timeLeft -= Time.deltaTime;
            UI.SetStatus(frozen ? "Fish frozen!" : $"Nico lifts the net in {Mathf.CeilToInt(_timeLeft)} s");
            if (!frozen) MoveFish(Time.deltaTime);
            yield return null;
        }
        _watching = false;
        HelperBar?.Refresh();

        Score = 0f;
        var sb = new System.Text.StringBuilder("Nico lifts the net: ");
        for (int k = 0; k < 3; k++)
        {
            int off = Mathf.Abs(_counted[k] - _real[k]);
            Score += Mathf.Max(0f, 100f - 15f * off) / 3f;
            sb.Append($"{Kinds[k]} {_real[k]}{(k < 2 ? ", " : "")}");
            _countLabels[k].text = $"{_counted[k]}/{_real[k]}";
            _countLabels[k].style.color = off == 0 ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Bad;
        }
        UI.SetStatus($"Count accuracy {Mathf.RoundToInt(Score)}%");
        UI.SetMessage(sb.ToString(), Score >= 70f ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
        yield return new WaitForSeconds(2.4f);
    }

    private void MoveFish(float dt)
    {
        foreach (var f in _fish)
        {
            f.Turn -= dt;
            if (f.Turn <= 0f)
            {
                f.Turn = Random.Range(1f, 3f);
                f.Vel = Quaternion.Euler(0, 0, Random.Range(-70f, 70f)) * f.Vel;
            }
            f.Pos += f.Vel * dt;
            if (f.Pos.x < 4f || f.Pos.x > PondW - 40f) { f.Vel.x = -f.Vel.x; f.Pos.x = Mathf.Clamp(f.Pos.x, 4f, PondW - 40f); }
            if (f.Pos.y < 4f || f.Pos.y > PondH - 24f) { f.Vel.y = -f.Vel.y; f.Pos.y = Mathf.Clamp(f.Pos.y, 4f, PondH - 24f); }
            f.El.style.left = f.Pos.x;
            f.El.style.top = f.Pos.y;
            f.El.style.scale = new Scale(new Vector3(f.Vel.x >= 0f ? -1f : 1f, 1f, 1f)); // face the way it swims
        }
    }

    private void SetCount(int k, int v)
    {
        if (!_watching || _locked[k]) return;
        _counted[k] = Mathf.Clamp(v, 0, 20);
        _countLabels[k].text = _counted[k].ToString();
    }

    private int FirstWrong()
    {
        for (int k = 0; k < 3; k++) if (!_locked[k] && _counted[k] != _real[k]) return k;
        return -1;
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e)
    {
        if (!_watching) return false;
        if (e == vc_SkillHelpers.Effect.Hint || e == vc_SkillHelpers.Effect.FreeStep) return FirstWrong() >= 0;
        return true;
    }

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        int k = FirstWrong();
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                Border(_counterCols[k], 3, Gold);
                UI.SetMessage($"{s.DisplayName}: there are {_real[k]} {Kinds[k].ToLower()} fish.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                SetCount(k, _real[k]);
                _locked[k] = true;
                Border(_counterCols[k], 3, vc_MinigameOverlay.Good);
                UI.SetMessage($"{s.DisplayName}: the {Kinds[k].ToLower()} fish are counted for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _timeLeft += 8f;
                UI.SetMessage($"{s.DisplayName}: Nico waits 8 more seconds.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                _freezeUntil = Time.time + 5f;
                UI.SetMessage($"{s.DisplayName}: the fish hold still for 5 seconds.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Your count matches Nico's net exactly. Great eyes!" :
        score >= 50f ? "Close count! Nico writes it in his notebook." :
        "Fish are slippery to count, but Nico had fun with you.";
}
