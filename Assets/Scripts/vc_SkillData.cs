using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "SUNOD/Skill")]
public class vc_SkillData : ScriptableObject
{
    [Tooltip("Stable id (the Filipino name). Saves, the run-state and telemetry use this - never rename it.")]
    public string skillName;
    [Tooltip("English name shown by default (Settings can switch to the Filipino name).")]
    public string englishName;
    public string riaSecLetter;
    public string buttonLabel;

    [TextArea(2, 4)]
    public string description;

    public Sprite icon;

    public string[] capabilityTags;

    public int level = 1;

    /// <summary>The name players see: English by default, Filipino when the option is on.</summary>
    public string DisplayName =>
        vc_PlayerOptions.UseBaybayinNames || string.IsNullOrWhiteSpace(englishName) ? skillName : englishName;

    public bool HasTag(string tag)
    {
        if (capabilityTags == null || string.IsNullOrEmpty(tag)) return false;
        for (int i = 0; i < capabilityTags.Length; i++)
            if (string.Equals(capabilityTags[i], tag, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
