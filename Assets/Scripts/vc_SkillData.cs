using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "SUNOD/Skill")]
public class vc_SkillData : ScriptableObject
{
    public string skillName;
    public string riaSecLetter;
    public string buttonLabel;

    [TextArea(2, 4)]
    public string description;

    public Sprite icon;

    public string[] capabilityTags;

    public bool HasTag(string tag)
    {
        if (capabilityTags == null || string.IsNullOrEmpty(tag)) return false;
        for (int i = 0; i < capabilityTags.Length; i++)
            if (string.Equals(capabilityTags[i], tag, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
