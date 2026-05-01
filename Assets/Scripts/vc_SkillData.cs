using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "SUNOD/Skill")]
public class vc_SkillData : ScriptableObject
{
    public string skillName;
    public string riaSecLetter;
    public string buttonLabel;

    [TextArea(2, 4)]
    public string description;
}
