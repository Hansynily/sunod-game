using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// I2 Kuya Migs - Watch the rabbits (MINIGAME_DESIGN.md, type E Observe and Record). New gameplay only.
/// 3 rabbits (white, brown, spotted) eat, hop, groom and freeze on their own. Kuya Migs' notebook
/// shows 6 prompts one at a time ("Tap a rabbit that is grooming", "Tap the rabbit that froze
/// first"...). Tap the right rabbit. Score = correct / 6, minus 5% per wrong tap. About 30 s of
/// watching (story, not a fail: a prompt that runs out just isn't noted).
/// Helpers: Hint = the right rabbit glows; Free step = this note is written for you;
/// Second chance = the next wrong tap costs nothing; Breather = everything holds still 5 s.
/// </summary>
[DisallowMultipleComponent]
public class vc_RabbitWatchMinigame : vc_MinigameQuest
{
    private enum Act { Eat, Hop, Groom, Freeze }
    private const int Prompts = 6;
    private const float MeadowW = 640f, MeadowH = 220f, PromptTime = 6f;
    private static readonly string[] Names = { "white", "brown", "spotted" };
    private static readonly Color[] Fur = { new Color(0.96f, 0.96f, 0.94f), new Color(0.62f, 0.45f, 0.30f), new Color(0.90f, 0.85f, 0.75f) };

    private class Rabbit
    {
        public int Id; public Act Act; public float ActTime; public Vector2 Pos, HopTo;
        public VisualElement El, Body, Grass; public float Phase;
    }

    private readonly List<Rabbit> _rabbits = new List<Rabbit>();
    private VisualElement _shadow;
    private Label _note;
    private int _tapped = -1, _freeTaps;
    private bool _asking, _freePrompt;
    private float _pauseUntil;
    private Act _askAct;
    private bool _askFrozeFirst;
    private int _firstFrozen = -1;
    private float _shadowX = -130f;

    protected override string Code => "I2";
    protected override string QuestName => "Watch the Rabbits";
    protected override string Intro => "Kuya Migs is studying the meadow rabbits. Watch closely and help him take notes.";
    protected override string CardTitle => "Watch the rabbits";

    protected override IEnumerator Play()
    {
        _rabbits.Clear(); _freeTaps = 0; _pauseUntil = 0f;
        UI.SetMessage("Watch what each rabbit does: eating (grass shrinks), hopping, grooming (wiggles), frozen (still).", vc_MinigameOverlay.Ink);

        var meadow = Box(UI.Content, MeadowW, MeadowH, new Color(0.55f, 0.78f, 0.40f));
        meadow.style.overflow = Overflow.Hidden;
        Border(meadow, 3, new Color(0.35f, 0.55f, 0.25f));
        for (int i = 0; i < 3; i++)
        {
            var r = new Rabbit { Id = i, Pos = LanePoint(i) };
            r.El = new VisualElement();
            r.El.style.position = Position.Absolute;
            r.El.style.width = 70; r.El.style.height = 64;
            meadow.Add(r.El);
            r.Grass = Box(r.El, 26, 10, new Color(0.25f, 0.55f, 0.20f));
            r.Grass.style.position = Position.Absolute; r.Grass.style.left = 0; r.Grass.style.top = 50;
            r.Body = Box(r.El, 50, 34, Fur[i]);
            r.Body.style.position = Position.Absolute; r.Body.style.left = 14; r.Body.style.top = 20;
            r.Body.style.borderTopLeftRadius = 16; r.Body.style.borderTopRightRadius = 16;
            r.Body.style.borderBottomLeftRadius = 10; r.Body.style.borderBottomRightRadius = 10;
            Border(r.Body, 2, vc_MinigameOverlay.Ink);
            if (i == 2)
                for (int s = 0; s < 3; s++) { var sp = Box(r.Body, 8, 8, new Color(0.40f, 0.30f, 0.20f)); sp.style.position = Position.Absolute; sp.style.left = 6 + s * 13; sp.style.top = 6 + (s % 2) * 10; }
            for (int e = 0; e < 2; e++)
            {
                var ear = Box(r.El, 8, 20, Fur[i]);
                ear.style.position = Position.Absolute; ear.style.left = 20 + e * 12; ear.style.top = 2;
                Border(ear, 2, vc_MinigameOverlay.Ink);
            }
            int id = i;
            r.El.RegisterCallback<PointerDownEvent>(_ => _tapped = id);
            _rabbits.Add(r);
            SetAct(r, (Act)Random.Range(0, 3));
        }
        _shadow = Box(meadow, 120, 40, new Color(0f, 0f, 0f, 0.25f));
        _shadow.style.position = Position.Absolute; _shadow.style.top = -60;
        _shadow.style.borderTopLeftRadius = 20; _shadow.style.borderTopRightRadius = 20;
        _shadow.style.borderBottomLeftRadius = 20; _shadow.style.borderBottomRightRadius = 20;
        _shadow.pickingMode = PickingMode.Ignore;

        _note = vc_MinigameOverlay.MakeLabel("", 26);
        _note.style.backgroundColor = new Color(1f, 0.98f, 0.85f);
        _note.style.width = 560; _note.style.marginTop = 8; _note.style.minHeight = 44;
        Border(_note, 2, vc_MinigameOverlay.Ink);
        UI.Content.Add(_note);
        BuildHelpers(UI.Content);

        // prompt plan: 4 behaviour prompts + 2 "froze first" prompts (one per bird pass), shuffled
        var plan = new List<int> { 0, 1, 2, 0, 3, 3 }; // Eat, Hop, Groom, Eat, FrozeFirst x2
        plan[3] = Random.Range(0, 3);
        Shuffle(plan);

        int correct = 0, wrong = 0;
        for (int p = 0; p < Prompts; p++)
        {
            UI.SetStatus($"Note {p + 1} of {Prompts}");
            _askFrozeFirst = plan[p] == 3;
            _askAct = _askFrozeFirst ? Act.Freeze : (Act)plan[p];
            _note.text = _askFrozeFirst ? "A bird's shadow! Tap the rabbit that FROZE FIRST." :
                         _askAct == Act.Eat ? "Tap a rabbit that is EATING." :
                         _askAct == Act.Hop ? "Tap a rabbit that is HOPPING." : "Tap a rabbit that is GROOMING.";

            if (_askFrozeFirst) yield return BirdPass();
            else
            {
                // make sure exactly one rabbit (random) starts doing it, the others do something else
                int who = Random.Range(0, 3);
                foreach (var r in _rabbits)
                {
                    if (r.Id == who) SetAct(r, _askAct);
                    else { Act other; do other = (Act)Random.Range(0, 3); while (other == _askAct); SetAct(r, other); }
                }
            }

            _tapped = -1; _freePrompt = false;
            _asking = true;
            HelperBar?.Refresh();
            float t = 0f;
            bool got = false;
            while (t < PromptTime)
            {
                bool paused = Time.time < _pauseUntil;
                if (!paused) { t += Time.deltaTime; Animate(Time.deltaTime); if (_askFrozeFirst && _shadowX < MeadowW + 20f) MoveShadow(Time.deltaTime); }
                if (_freePrompt) { got = true; break; }
                if (_tapped >= 0)
                {
                    int tapped = _tapped; _tapped = -1;
                    if (IsRight(tapped)) { got = true; break; }
                    if (_freeTaps > 0) { _freeTaps--; UI.SetMessage("Not that one, but no harm done. Look again!", vc_MinigameOverlay.Good); }
                    else { wrong++; UI.SetMessage($"The {Names[tapped]} rabbit isn't doing that.", vc_MinigameOverlay.Bad); }
                }
                yield return null;
            }
            _asking = false;
            HelperBar?.Refresh();
            foreach (var r in _rabbits) Border(r.Body, 2, vc_MinigameOverlay.Ink);
            if (got) correct++;
            UI.SetMessage(got ? "Noted! Kuya Migs nods." : "Too late, the moment passed.", got ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
            if (_askFrozeFirst)
            {
                _shadow.style.top = -60; _shadow.style.left = -130;
                foreach (var r in _rabbits) SetAct(r, (Act)Random.Range(0, 3));
            }
            yield return new WaitForSeconds(0.5f);
        }

        Score = 100f * correct / Prompts - 5f * wrong;
        _note.text = $"Notebook: {correct} of {Prompts} notes" + (wrong > 0 ? $", {wrong} slip{(wrong == 1 ? "" : "s")}" : "");
        UI.SetMessage(Score >= 70f ? "\"Great observations!\" says Kuya Migs." : "\"Rabbits are tricky to watch!\" says Kuya Migs.",
                      Score >= 70f ? vc_MinigameOverlay.Good : vc_MinigameOverlay.Ink);
        yield return new WaitForSeconds(1.8f);
    }

    private bool IsRight(int id)
    {
        if (_askFrozeFirst) return id == _firstFrozen;
        return _rabbits[id].Act == _askAct;
    }

    private int RightRabbit()
    {
        if (_askFrozeFirst) return _firstFrozen;
        foreach (var r in _rabbits) if (r.Act == _askAct) return r.Id;
        return 0;
    }

    private IEnumerator BirdPass()
    {
        // the shadow sweeps across; rabbits freeze one after another in a random order
        var order = new List<int> { 0, 1, 2 };
        Shuffle(order);
        _firstFrozen = order[0];
        _shadowX = -130f;
        _shadow.style.top = Random.Range(40f, 120f);
        for (int k = 0; k < 3; k++)
        {
            float delay = k == 0 ? 0.3f : Random.Range(0.6f, 0.9f);
            float t = 0f;
            while (t < delay) { if (Time.time >= _pauseUntil) { t += Time.deltaTime; Animate(Time.deltaTime); MoveShadow(Time.deltaTime); } yield return null; }
            SetAct(_rabbits[order[k]], Act.Freeze);
        }
    }

    private void MoveShadow(float dt)
    {
        _shadowX += 320f * dt;
        if (_shadowX > MeadowW + 20f) _shadowX = -130f;
        _shadow.style.left = _shadowX;
    }

    private void SetAct(Rabbit r, Act a)
    {
        r.Act = a;
        r.ActTime = 0f;
        r.Body.style.rotate = new Rotate(0);
        r.Grass.style.width = 26;
        r.Body.style.backgroundColor = Fur[r.Id];
        if (a == Act.Hop) r.HopTo = LanePoint(r.Id);
        Place(r, 0f);
    }

    private void Animate(float dt)
    {
        foreach (var r in _rabbits)
        {
            r.ActTime += dt; r.Phase += dt;
            float lift = 0f;
            switch (r.Act)
            {
                case Act.Eat:
                    r.Grass.style.width = Mathf.Max(6f, 26f - r.ActTime * 3f);
                    r.Body.style.rotate = new Rotate(Mathf.Sin(r.Phase * 6f) > 0f ? 6f : 0f);
                    break;
                case Act.Hop:
                {
                    Vector2 d = r.HopTo - r.Pos;
                    if (d.magnitude < 6f) r.HopTo = LanePoint(r.Id);
                    r.Pos += d.normalized * 70f * dt;
                    lift = Mathf.Abs(Mathf.Sin(r.Phase * 7f)) * 14f;
                    break;
                }
                case Act.Groom:
                    r.Body.style.rotate = new Rotate(Mathf.Sin(r.Phase * 14f) * 10f);
                    break;
                case Act.Freeze:
                    break;
            }
            Place(r, lift);
        }
    }

    /// <summary>A random spot inside this rabbit's own third of the meadow, so rabbits never overlap.</summary>
    private static Vector2 LanePoint(int id)
    {
        float laneW = MeadowW / 3f;
        return new Vector2(Random.Range(id * laneW + 8f, (id + 1) * laneW - 78f), Random.Range(30f, MeadowH - 80f));
    }

    private static void Place(Rabbit r, float lift)
    {
        r.El.style.left = r.Pos.x;
        r.El.style.top = r.Pos.y - lift;
    }

    protected override bool CanUseHelper(vc_SkillHelpers.Effect e) => _asking;

    protected override void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s)
    {
        switch (e)
        {
            case vc_SkillHelpers.Effect.Hint:
                Border(_rabbits[RightRabbit()].Body, 4, Gold);
                UI.SetMessage($"{s.DisplayName}: the glowing rabbit is the one.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.FreeStep:
                _freePrompt = true;
                UI.SetMessage($"{s.DisplayName}: this note is written for you.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.SecondChance:
                _freeTaps++;
                UI.SetMessage($"{s.DisplayName}: your next wrong tap won't count.", vc_MinigameOverlay.Good);
                break;
            case vc_SkillHelpers.Effect.Breather:
                _pauseUntil = Time.time + 5f;
                UI.SetMessage($"{s.DisplayName}: the meadow holds still for 5 seconds.", vc_MinigameOverlay.Good);
                break;
        }
    }

    protected override string FinishLine(float score) =>
        score >= 90f ? "Kuya Migs' notebook is full of great notes!" :
        score >= 50f ? "Kuya Migs learned a lot about the rabbits today." :
        "The rabbits were sneaky, but Kuya Migs enjoyed watching with you.";
}
