using System;
using System.Collections.Generic;
using UnityEngine;
using SunodGame.Models;

/// <summary>
/// Skill system v2 (SKILL_SYSTEM_DESIGN.md): XP and levels per skill, plus the shared rules
/// every quest reads (presses per stage, slow-path multipliers, recommended level, rare skills).
///
///   Lv1 0 XP · Lv2 15 XP · Lv3 (max) 40 XP. Slow on purpose: max level is a loop 2-3 goal.
///   XP: finish as best fit +2 · same category +1 · other category +1 · used as prep +1 ·
///       duplicate at the night pick +4 · town event +2 (every discovered skill of that kind).
///
/// The XP map is part of the run (saved with every checkpoint as skill_xp). At Go Back the
/// server keeps the XP of the remembered skills only (kept_skill_xp); every other skill starts
/// the next loop at Lv1. Nothing here touches the quest timer's star rule or scoring: a level
/// only makes a skill's path faster inside the quest.
/// </summary>
public static class vc_SkillLevels
{
    public const int MaxLevel = 3;
    public const int Lv2Xp = 15;
    public const int Lv3Xp = 40;

    public const int XpBestFit = 2;
    public const int XpSameCategory = 1;
    public const int XpOtherCategory = 1;
    public const int XpPrep = 1;
    public const int XpDuplicate = 4;
    public const int XpTownEvent = 2;

    private static readonly Dictionary<string, int> Xps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Any XP change (HUD pips, inventory).</summary>
    public static event Action Changed;
    /// <summary>A skill went up a level: (skill, old level, new level).</summary>
    public static event Action<vc_SkillData, int, int> LevelUp;

    /// <summary>Continue: the checkpoint's XP, applied with the owned-skill restore.</summary>
    public static List<SkillXpEntryDto> PendingRestore;

    // ── Levels ────────────────────────────────────────────────────────────

    public static int LevelForXp(int xp) => xp >= Lv3Xp ? 3 : xp >= Lv2Xp ? 2 : 1;

    public static int Xp(vc_SkillData skill) =>
        skill != null && !string.IsNullOrEmpty(skill.skillName) && Xps.TryGetValue(skill.skillName, out int xp) ? xp : 0;

    public static int Level(vc_SkillData skill) => LevelForXp(Xp(skill));

    /// <summary>XP still needed for the next level (0 at max).</summary>
    public static int XpToNext(vc_SkillData skill)
    {
        int xp = Xp(skill);
        return xp >= Lv3Xp ? 0 : (xp >= Lv2Xp ? Lv3Xp : Lv2Xp) - xp;
    }

    /// <summary>0..1 progress inside the current level (1 at max).</summary>
    public static float LevelProgress(vc_SkillData skill)
    {
        int xp = Xp(skill);
        if (xp >= Lv3Xp) return 1f;
        int from = xp >= Lv2Xp ? Lv2Xp : 0, to = xp >= Lv2Xp ? Lv3Xp : Lv2Xp;
        return Mathf.Clamp01((xp - from) / (float)(to - from));
    }

    /// <summary>Adds XP. Returns the new level; fires LevelUp when it changed.</summary>
    public static int AddXp(vc_SkillData skill, int amount)
    {
        if (skill == null || string.IsNullOrEmpty(skill.skillName) || amount <= 0) return Level(skill);
        int before = Level(skill);
        Xps[skill.skillName] = Xp(skill) + amount;
        int after = Level(skill);
        Changed?.Invoke();
        if (after > before) LevelUp?.Invoke(skill, before, after);
        return after;
    }

    /// <summary>Town events: XP to every discovered (owned) skill of one RIASEC kind.</summary>
    public static void AddCategoryXp(string letter, int amount)
    {
        var inv = vc_PlayerInventory.Instance;
        if (inv == null || string.IsNullOrEmpty(letter)) return;
        foreach (vc_SkillData s in inv.GatheredSkills)
            if (s != null && string.Equals(s.riaSecLetter?.Trim(), letter.Trim(), StringComparison.OrdinalIgnoreCase))
                AddXp(s, amount);
    }

    // ── Save / restore ────────────────────────────────────────────────────

    public static void Clear()
    {
        Xps.Clear();
        Changed?.Invoke();
    }

    public static void Load(IEnumerable<SkillXpEntryDto> entries)
    {
        Xps.Clear();
        if (entries != null)
            foreach (SkillXpEntryDto e in entries)
                if (e != null && !string.IsNullOrEmpty(e.skill) && e.xp > 0)
                    Xps[e.skill] = Mathf.Max(Xps.TryGetValue(e.skill, out int had) ? had : 0, e.xp);
        Changed?.Invoke();
    }

    public static List<SkillXpEntryDto> ToEntries()
    {
        var list = new List<SkillXpEntryDto>();
        foreach (var kv in Xps)
            if (kv.Value > 0) list.Add(new SkillXpEntryDto { skill = kv.Key, xp = kv.Value });
        return list;
    }

    /// <summary>
    /// A skill's level for the loop screens (remember cards, wake-up): the live XP, or what the
    /// loop record says it ended / was remembered at (the menu has no live run).
    /// </summary>
    public static int CarriedLevel(vc_SkillData skill)
    {
        int xp = Xp(skill);
        LoopMetaResponse meta = vc_LoopMeta.Current;
        if (skill != null && meta != null)
            foreach (var list in new[] { meta.last_skill_xp, meta.kept_skill_xp })
                if (list != null)
                    foreach (SkillXpEntryDto e in list)
                        if (e != null && string.Equals(e.skill, skill.skillName, StringComparison.OrdinalIgnoreCase))
                            xp = Mathf.Max(xp, e.xp);
        return LevelForXp(xp);
    }

    /// <summary>A fresh run: loop 1 starts empty; loop 2+ starts with the remembered skills' XP.</summary>
    public static void StartRun()
    {
        LoopMetaResponse meta = vc_LoopMeta.Current;
        Load(meta != null ? meta.kept_skill_xp : null);
    }

    // ── Rare skills (night pick) ──────────────────────────────────────────
    // Plan and Survey work in many quests (prep in 12, best fit in 6), so they're a lucky find.

    public static bool IsRare(vc_SkillData skill) => skill != null && (skill.HasTag("plan") || skill.HasTag("survey"));

    // ── Stage rules ───────────────────────────────────────────────────────

    /// <summary>0 = Elementary, 1 = High School, 2 = College (Elementary outside the towns).</summary>
    public static int StageIndex
    {
        get
        {
            string id = vc_TownStage.Current != null ? vc_TownStage.Current.stageId : "";
            if (string.IsNullOrEmpty(id)) return 0;
            id = id.ToLowerInvariant();
            if (id.Contains("college")) return 2;
            if (id.Contains("high")) return 1;
            return 0;
        }
    }

    public static int PressesForStage(int stage) => stage >= 2 ? 4 : stage == 1 ? 5 : 6;

    /// <summary>Slower paths take longer in later stages; the best fit never does.</summary>
    public static float SlowFactorForStage(int stage) => stage >= 2 ? 1.4f : stage == 1 ? 1.2f : 1f;

    private static readonly HashSet<string> Lv3RecommendedCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "R3", "I4", "C7", "A7" };

    /// <summary>Advice only, shown on the request prompt. A lower level still works, just slower.</summary>
    public static int RecommendedLevel(int stage, string questCode)
    {
        if (stage <= 0) return 1;
        if (stage >= 2 && !string.IsNullOrEmpty(questCode) && Lv3RecommendedCodes.Contains(questCode.Trim())) return 3;
        return 2;
    }

    /// <summary>The best level among the player's equipped skills of this kind (for the prompt).</summary>
    public static int EquippedLevel(string letter)
    {
        vc_SkillData s = vc_SkillManager.Instance != null ? vc_SkillManager.Instance.GetSkillInCategory(letter) : null;
        return s != null ? Level(s) : 1;
    }

    // ── Level effect on a quest path (SKILL_SYSTEM_DESIGN.md §1.4) ─────────

    /// <summary>
    /// Seconds a path takes at this level and stage. Best fit: as authored. Same category:
    /// ×stage, Lv2 25% faster, Lv3 = the best fit's time. Other category: ×stage, Lv2 15%
    /// faster, Lv3 30% faster (never instant). Hints don't run.
    /// </summary>
    public static float PathSeconds(vc_SkillPath path, float baseSeconds, int level, int stage, float bestFitSeconds)
    {
        float slow = SlowFactorForStage(stage);
        switch (path)
        {
            case vc_SkillPath.SameCategory:
                if (level >= 3) return Mathf.Min(bestFitSeconds, baseSeconds * slow);
                return baseSeconds * slow * (level == 2 ? 0.75f : 1f);
            case vc_SkillPath.OtherCategory:
                return baseSeconds * slow * (level >= 3 ? 0.7f : level == 2 ? 0.85f : 1f);
            default:
                return baseSeconds;
        }
    }

    /// <summary>"x3" same-category paths: 3 presses, 2 at Lv2, 1 at Lv3.</summary>
    public static int PathPresses(int basePresses, int level) =>
        level >= 3 ? 1 : level == 2 ? Mathf.Max(1, basePresses - 1) : Mathf.Max(1, basePresses);

    public static int XpFor(vc_SkillPath path) => path switch
    {
        vc_SkillPath.BestFit => XpBestFit,
        vc_SkillPath.SameCategory => XpSameCategory,
        vc_SkillPath.OtherCategory => XpOtherCategory,
        vc_SkillPath.Prep => XpPrep,
        _ => 0,
    };
}

/// <summary>How a skill fits the running quest (the paper's Table 1 tiers, plus prep).</summary>
public enum vc_SkillPath { Hint, BestFit, SameCategory, OtherCategory, Prep }
