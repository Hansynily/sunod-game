using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_PlayerInventory : MonoBehaviour
{
    public static vc_PlayerInventory Instance { get; private set; }

    private readonly List<vc_SkillData> _gatheredSkills = new List<vc_SkillData>();

    public IReadOnlyList<vc_SkillData> GatheredSkills => _gatheredSkills;

    public event Action<vc_SkillData> OnSkillAdded;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    public void AddSkill(vc_SkillData skillData)
    {
        if (skillData == null || _gatheredSkills.Contains(skillData))
        {
            return;
        }

        _gatheredSkills.Add(skillData);
        OnSkillAdded?.Invoke(skillData);
    }

    public bool HasSkill(vc_SkillData skillData)
    {
        return _gatheredSkills.Contains(skillData);
    }

    public void ClearInventory()
    {
        _gatheredSkills.Clear();
    }
}
