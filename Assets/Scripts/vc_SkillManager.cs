using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_SkillManager : MonoBehaviour
{
    [SerializeField] private vc_SkillSlot[] skillSlots = new vc_SkillSlot[4];

    private int[] usageCount;

    public event Action<int, vc_PlayerSkill> SkillUsed;

    public static vc_SkillManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

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

    public void AssignSkillToSlot(int slotIndex, vc_SkillData data)
    {
        if (skillSlots == null || slotIndex < 0 || slotIndex >= skillSlots.Length || skillSlots[slotIndex] == null)
        {
            return;
        }

        vc_PlayerSkill skill = ResolveSkill(data);
        if (skill == null)
        {
            return;
        }

        skillSlots[slotIndex].AssignSkill(skill);
    }

    public void ResetUsageCounts()
    {
        int slotCount = skillSlots != null ? skillSlots.Length : 0;
        if (usageCount == null || usageCount.Length != slotCount)
        {
            usageCount = new int[slotCount];
            return;
        }

        Array.Clear(usageCount, 0, usageCount.Length);
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

    private static readonly Dictionary<string, Type> SkillTypeCache = new Dictionary<string, Type>();

    private vc_PlayerSkill ResolveSkill(vc_SkillData skillData)
    {
        if (skillData == null)
            return null;

        vc_PlayerSkill[] existing = GetComponentsInChildren<vc_PlayerSkill>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].SkillData == skillData)
                return existing[i];
        }

        string typeName = $"vc_{skillData.skillName}Skill";
        if (!SkillTypeCache.TryGetValue(typeName, out Type skillType))
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                skillType = assembly.GetType(typeName);
                if (skillType != null)
                    break;
            }
            SkillTypeCache[typeName] = skillType;
        }

        if (skillType == null || !typeof(vc_PlayerSkill).IsAssignableFrom(skillType))
        {
            Debug.LogWarning($"[vc_SkillManager] No skill class found matching '{typeName}'. Skill name must match class name convention vc_{{Name}}Skill.");
            return null;
        }

        GameObject skillObject = new GameObject(skillData.skillName);
        skillObject.transform.SetParent(transform);
        vc_PlayerSkill newSkill = (vc_PlayerSkill)skillObject.AddComponent(skillType);
        newSkill.Initialize(skillData);
        return newSkill;
    }
}
