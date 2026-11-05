using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Skills as minigame HELPERS ("New" gameplay, see MINIGAME_DESIGN.md "Skills in minigames").
/// Collecting skills is unchanged; inside a minigame each equipped skill gives one of 4 effects,
/// and every effect works in every minigame (so picking skills of one letter never raises that
/// letter's score by itself):
///   Hint          - shows one right answer
///   SecondChance  - undo / another try after a mistake
///   FreeStep      - one step done perfectly for you
///   Breather      - pause what's moving, or check one answer before committing
/// Uses: each equipped skill once per in-game day; skills remembered from past loops twice.
/// Every use is added to the quest's skill usage counts (telemetry).
/// </summary>
public static class vc_SkillHelpers
{
    public enum Effect { Hint, SecondChance, FreeStep, Breather }

    private struct Entry
    {
        public Effect Effect;
        public string Line;
        public Entry(Effect e, string line) { Effect = e; Line = line; }
    }

    // Keyed by vc_SkillData.skillName (the Filipino name, as the loop save uses).
    private static readonly Dictionary<string, Entry> Map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
    {
        { "AYOS",   new Entry(Effect.SecondChance, "Fix your last mistake.") },
        { "BUO",    new Entry(Effect.FreeStep,     "Build one piece right for you.") },
        { "TULAK",  new Entry(Effect.Breather,     "Hold it right there - a moment to check.") },
        { "SURI",   new Entry(Effect.Hint,         "Look around. One right answer lights up.") },
        { "LUTAS",  new Entry(Effect.Hint,         "Solve part of it - one right answer shows.") },
        { "BALAK",  new Entry(Effect.Breather,     "Plan ahead - check one answer first.") },
        { "PINTA",  new Entry(Effect.Hint,         "Colour it in. One right answer shows.") },
        { "HULMA",  new Entry(Effect.SecondChance, "Reshape it - undo a mistake.") },
        { "KATHA",  new Entry(Effect.FreeStep,     "Craft one step perfectly.") },
        { "GABAY",  new Entry(Effect.Hint,         "A guiding hand points the way.") },
        { "LUNAS",  new Entry(Effect.SecondChance, "Heal a mistake - one more try.") },
        { "TURO",   new Entry(Effect.FreeStep,     "Someone shows you how. One step done.") },
        { "SENSUS", new Entry(Effect.Hint,         "Map it out. One right answer shows.") },
        { "UTOS",   new Entry(Effect.FreeStep,     "Give the order - one step done for you.") },
        { "HIMOK",  new Entry(Effect.Breather,     "Talk them into waiting - check first.") },
        { "SIKAT",  new Entry(Effect.Breather,     "Charm buys you a moment - check first.") },
        { "TAWAG",  new Entry(Effect.Breather,     "Call for a pause - check first.") },
        { "SILIP",  new Entry(Effect.Hint,         "Peek closer. One right answer shows.") },
        { "IPON",   new Entry(Effect.SecondChance, "Gather yourself - one more try.") },
        { "DAAN",   new Entry(Effect.FreeStep,     "Direct it - one step done for you.") },
    };

    public static Effect EffectOf(vc_SkillData s)
    {
        if (s != null && Map.TryGetValue(s.skillName ?? "", out Entry e)) return e.Effect;
        return Effect.Hint;
    }

    public static string LineOf(vc_SkillData s)
    {
        if (s != null && Map.TryGetValue(s.skillName ?? "", out Entry e)) return e.Line;
        return "Shows one right answer.";
    }

    public static string EffectName(Effect e)
    {
        switch (e)
        {
            case Effect.SecondChance: return "Second chance";
            case Effect.FreeStep: return "Free step";
            case Effect.Breather: return "Check first";
            default: return "Hint";
        }
    }

    // ── Daily uses ───────────────────────────────────────────────────────

    private static string _dayKey = "";
    private static readonly Dictionary<string, int> _used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private static string CurrentDayKey()
    {
        vc_TownStage stage = UnityEngine.Object.FindFirstObjectByType<vc_TownStage>();
        string day = stage != null ? $"{stage.stageId}:{stage.Day}" : "no-town";
        return $"{vc_LoopMeta.Loop}:{day}";
    }

    private static void RollDay()
    {
        string k = CurrentDayKey();
        if (k == _dayKey) return;
        _dayKey = k;
        _used.Clear();
    }

    public static bool IsRemembered(vc_SkillData s)
    {
        if (s == null) return false;
        foreach (string n in vc_LoopMeta.KeptSkills)
            if (string.Equals(n, s.skillName, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static int MaxUses(vc_SkillData s) => IsRemembered(s) ? 2 : 1;

    public static int UsesLeft(vc_SkillData s)
    {
        if (s == null) return 0;
        RollDay();
        _used.TryGetValue(s.skillName ?? "", out int u);
        return Mathf.Max(0, MaxUses(s) - u);
    }

    /// <summary>Spends one use and logs it in the quest's skill usage counts.</summary>
    public static bool Spend(int slot, vc_SkillData s)
    {
        if (UsesLeft(s) <= 0) return false;
        string k = s.skillName ?? "";
        _used.TryGetValue(k, out int u);
        _used[k] = u + 1;
        vc_SkillManager.Instance?.RecordHelperUse(slot);
        return true;
    }

    // ── Shared helper bar (inside a minigame card) ───────────────────────

    public class Bar
    {
        public VisualElement Root;
        private readonly List<(Button b, int slot, vc_SkillData s)> _buttons = new List<(Button, int, vc_SkillData)>();
        private readonly Func<Effect, bool> _canUse;

        public Bar(Func<Effect, bool> canUse) { _canUse = canUse; }

        public void Refresh()
        {
            foreach (var (b, slot, s) in _buttons)
            {
                int left = UsesLeft(s);
                bool ok = left > 0 && (_canUse == null || _canUse(EffectOf(s)));
                b.SetEnabled(ok);
                var uses = b.Q<Label>("uses");
                if (uses != null) uses.text = left > 0 ? $"{EffectName(EffectOf(s))} x{left}" : "used today";
            }
        }

        public void Add(Button b, int slot, vc_SkillData s) => _buttons.Add((b, slot, s));
    }

    /// <summary>
    /// Builds a row of the player's equipped skills. Tapping one spends a use and calls
    /// onUse(effect, skill). canUse decides if an effect makes sense right now.
    /// </summary>
    public static Bar BuildBar(VisualElement parent, Func<Effect, bool> canUse, Action<Effect, vc_SkillData> onUse)
    {
        var bar = new Bar(canUse);
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.Center;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginTop = 10;
        bar.Root = row;
        parent.Add(row);

        var title = vc_MinigameOverlay.MakeLabel("Your skills (tap to use):", 14);
        title.style.width = Length.Percent(100);
        title.style.marginBottom = 2;
        row.Add(title);

        vc_SkillManager sm = vc_SkillManager.Instance;
        int count = 0;
        for (int slot = 0; slot < vc_SkillManager.SlotCount; slot++)
        {
            vc_SkillData s = sm != null ? sm.GetSkillInSlot(slot) : null;
            if (s == null) continue;
            count++;
            int capturedSlot = slot;
            vc_SkillData captured = s;
            Button b = null;
            b = new Button(() =>
            {
                Effect e = EffectOf(captured);
                if (canUse != null && !canUse(e)) return;
                if (!Spend(capturedSlot, captured)) return;
                SunodGame.Core.vc_Sfx.Play("idea", 0.6f);
                onUse?.Invoke(e, captured);
                bar.Refresh();
            });
            b.AddToClassList("sl-button");
            b.style.width = 106; b.style.minHeight = 58;
            b.style.marginLeft = 3; b.style.marginRight = 3; b.style.marginTop = 4;
            b.style.paddingTop = 4; b.style.paddingBottom = 8;
            b.style.paddingLeft = 2; b.style.paddingRight = 2;
            b.style.flexDirection = FlexDirection.Column;
            b.style.alignItems = Align.Center;
            b.tooltip = LineOf(s);

            var name = vc_MinigameOverlay.MakeLabel(s.DisplayName, 14);
            if (IsRemembered(s)) name.style.color = new Color(0.55f, 0.30f, 0.85f);
            b.Add(name);
            var uses = vc_MinigameOverlay.MakeLabel("", 14);
            uses.style.opacity = 0.8f;
            uses.name = "uses";
            b.Add(uses);

            row.Add(b);
            bar.Add(b, slot, s);
        }
        if (count == 0) title.text = "No skills equipped.";
        bar.Refresh();
        return bar;
    }
}
