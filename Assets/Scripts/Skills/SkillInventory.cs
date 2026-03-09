using System.Collections.Generic;
using UnityEngine;

public class SkillInventory : MonoBehaviour
{
    public static SkillInventory Instance;

    private Dictionary<HollandCode, List<SkillData>> skills =
        new Dictionary<HollandCode, List<SkillData>>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        foreach (HollandCode category in System.Enum.GetValues(typeof(HollandCode)))
        {
            skills[category] = new List<SkillData>();
        }
    }
    public void AddSkill(SkillData skill)
    {
        if (!skills[skill.hollandCode].Contains(skill))
        {
            skills[skill.hollandCode].Add(skill);
            Debug.Log("Added skill: " + skill.skillName);
        }
    }

    public List<SkillData> GetSkills(HollandCode category)
    {
        return skills[category];
    }
}
