using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_SkillManager : MonoBehaviour
{
    [SerializeField] private vc_SkillSlot[] skillSlots = new vc_SkillSlot[4];

    private int[] usageCount = new int[4];

    public event Action<int, vc_PlayerSkill> SkillUsed;

    private void Awake()
    {
        usageCount = new int[skillSlots != null ? skillSlots.Length : 0];

        if (skillSlots == null)
        {
            return;
        }

        for (int i = 0; i < skillSlots.Length; i++)
        {
            vc_SkillSlot skillSlot = skillSlots[i];
            if (skillSlot == null)
            {
                continue;
            }

            skillSlot.RefreshDisplay();
            skillSlot.SkillPressed += HandleSkillPressed;
        }
    }

    private void OnDestroy()
    {
        if (skillSlots == null)
        {
            return;
        }

        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (skillSlots[i] == null)
            {
                continue;
            }

            skillSlots[i].SkillPressed -= HandleSkillPressed;
        }
    }

    public Dictionary<string, int> GetUsageSummary()
    {
        Dictionary<string, int> summary = new Dictionary<string, int>();

        if (skillSlots == null)
        {
            return summary;
        }

        for (int i = 0; i < skillSlots.Length; i++)
        {
            vc_SkillSlot skillSlot = skillSlots[i];
            if (skillSlot == null || skillSlot.AssignedSkillData == null || i >= usageCount.Length)
            {
                continue;
            }

            string letter = skillSlot.AssignedSkillData.riaSecLetter;
            if (!summary.ContainsKey(letter))
            {
                summary[letter] = 0;
            }

            summary[letter] += usageCount[i];
        }

        return summary;
    }

    public vc_SkillSlot GetSkillSlot(int index)
    {
        if (skillSlots == null || index < 0 || index >= skillSlots.Length)
        {
            return null;
        }

        return skillSlots[index];
    }

    public vc_SkillData GetSkillInSlot(int index)
    {
        vc_SkillSlot skillSlot = GetSkillSlot(index);
        return skillSlot != null ? skillSlot.AssignedSkillData : null;
    }

    public bool IsSlotHeld(int index)
    {
        vc_SkillSlot skillSlot = GetSkillSlot(index);
        return skillSlot != null && skillSlot.IsHeld;
    }

    public vc_PlayerSkill GetSlotSkill(int index)
    {
        vc_SkillSlot skillSlot = GetSkillSlot(index);
        return skillSlot != null ? skillSlot.AssignedSkill : null;
    }

    public void LoadSkills(vc_SkillData[] newSkills)
    {
        if (skillSlots == null || newSkills == null)
        {
            return;
        }

        int count = Mathf.Min(skillSlots.Length, newSkills.Length);
        for (int i = 0; i < count; i++)
        {
            if (skillSlots[i] != null)
            {
                skillSlots[i].AssignSkill(ResolveSkill(newSkills[i]));
            }
        }
    }

    private void HandleSkillPressed(vc_SkillSlot skillSlot)
    {
        if (skillSlot == null || skillSlot.AssignedSkillData == null)
        {
            return;
        }

        int index = skillSlot.SlotIndex;
        if (index < 0 || index >= usageCount.Length)
        {
            return;
        }

        vc_PlayerSkill assignedSkill = skillSlot.AssignedSkill;
        vc_SkillData assignedSkillData = skillSlot.AssignedSkillData;
        usageCount[index]++;
        Debug.Log($"{assignedSkillData.skillName} used - RIASEC: {assignedSkillData.riaSecLetter}");
        SkillUsed?.Invoke(index, assignedSkill);
    }

    private vc_PlayerSkill ResolveSkill(vc_SkillData skillData)
    {
        if (skillData == null)
        {
            return null;
        }

        vc_PlayerSkill[] availableSkills = GetComponents<vc_PlayerSkill>();
        for (int i = 0; i < availableSkills.Length; i++)
        {
            if (availableSkills[i] != null && availableSkills[i].SkillData == skillData)
            {
                return availableSkills[i];
            }
        }

        availableSkills = GetComponentsInChildren<vc_PlayerSkill>(true);
        for (int i = 0; i < availableSkills.Length; i++)
        {
            if (availableSkills[i] != null && availableSkills[i].SkillData == skillData)
            {
                return availableSkills[i];
            }
        }

        return null;
    }
}
