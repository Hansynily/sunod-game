using System;

/// <summary>
/// One quest's entry in skill system v2 (QUEST_REDESIGN.md): which skill is the best fit,
/// the prep skill and what it unlocks, the NPC's preferred skill and their surprised lines.
/// A quest script returns one from vc_FitQuestBase.SkillProfile. Quests without one still get
/// the press limit and XP, but no prep, preference or level speed-ups.
/// </summary>
[Serializable]
public class vc_QuestSkillProfile
{
    /// <summary>NPC who asked (for their lines).</summary>
    public string npcName;

    /// <summary>This quest's press limit (longest real solution + the stage's spare). 0 = stage default.</summary>
    public int presses;

    /// <summary>Tag of the best-fit skill (fast, never slowed by stage or level).</summary>
    public string bestFitTag;
    /// <summary>The best fit's time: what a Lv3 same-category skill or a prepped skill takes.</summary>
    public float bestFitSeconds = 1f;

    /// <summary>Tag of the prep skill (another category). Pressed first, it makes one skill instant.</summary>
    public string prepTag;
    /// <summary>Tag of the same-category skill the prep makes instant.</summary>
    public string prepUnlocksTag;
    /// <summary>Toast while the prep plays, e.g. "You sweep the pipe with a magnifier...".</summary>
    public string prepLine;

    /// <summary>Tag of the NPC's preferred skill (same category, never the best fit). Bonus only.</summary>
    public string preferredTag;
    /// <summary>What the NPC says when it was done their way.</summary>
    public string preferenceLine;

    /// <summary>Surprised lines when a same-category skill solves it at best-fit speed: (tag, line).</summary>
    public (string tag, string line)[] fastLines = new (string, string)[0];

    public string FastLineFor(vc_SkillData skill)
    {
        if (skill == null || fastLines == null) return null;
        foreach (var (tag, line) in fastLines)
            if (skill.HasTag(tag)) return line;
        return null;
    }
}
