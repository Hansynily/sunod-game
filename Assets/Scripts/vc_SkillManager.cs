using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_SkillManager : MonoBehaviour
{
    public const int SlotCount = 6;

    private static readonly string[] CategoryLetters = { "R", "I", "A", "S", "E", "C" };
    private static readonly string[] CategoryNames = { "Realistic", "Investigative", "Artistic", "Social", "Enterprising", "Conventional" };

    public static int SlotIndexForLetter(string letter)
    {
        if (string.IsNullOrEmpty(letter)) return -1;
        string trimmed = letter.Trim().ToUpperInvariant();
        for (int i = 0; i < CategoryLetters.Length; i++)
        {
            if (CategoryLetters[i] == trimmed) return i;
        }
        return -1;
    }

    public static string CategoryNameForSlot(int index)
    {
        if (index < 0 || index >= CategoryNames.Length) return string.Empty;
        return CategoryNames[index];
    }

    public static string CategoryLetterForSlot(int index)
    {
        if (index < 0 || index >= CategoryLetters.Length) return string.Empty;
        return CategoryLetters[index];
    }

    [SerializeField] private vc_SkillSlot[] skillSlots = new vc_SkillSlot[SlotCount];

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
        EnsureSlotsResolved();

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

    public bool IsHoldingTag(string tag)
    {
        int count = skillSlots != null ? skillSlots.Length : 0;
        for (int i = 0; i < count; i++)
        {
            vc_PlayerSkill skill = GetSlotSkill(i);
            if (skill != null && IsSlotHeld(i) && skill.SkillData != null && skill.SkillData.HasTag(tag))
                return true;
        }
        return false;
    }

    public void SetSkillsInteractable(bool value)
    {
        if (skillSlots == null) return;

        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (skillSlots[i] == null) continue;

            UnityEngine.UI.Button button = skillSlots[i].GetComponent<UnityEngine.UI.Button>();
            if (button != null)
                button.interactable = value;
        }
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

    public bool AssignSkillToSlot(int slotIndex, vc_SkillData data)
    {
        EnsureSlotsResolved();

        if (skillSlots == null || slotIndex < 0 || slotIndex >= skillSlots.Length || skillSlots[slotIndex] == null)
        {
            Debug.LogWarning($"[SkillManager] AssignSkillToSlot({slotIndex}) - slot check failed. skillSlots null={skillSlots == null}, length={skillSlots?.Length}");
            return false;
        }

        if (data != null && SlotIndexForLetter(data.riaSecLetter) != slotIndex)
        {
            Debug.LogWarning($"[SkillManager] AssignSkillToSlot({slotIndex}) - '{data.skillName}' is category '{data.riaSecLetter}', doesn't belong in slot {slotIndex}");
            return false;
        }

        vc_PlayerSkill skill = ResolveSkill(data);
        if (skill == null)
        {
            Debug.LogWarning($"[SkillManager] AssignSkillToSlot({slotIndex}) - ResolveSkill returned null for '{data?.skillName}'");
            return false;
        }

        Debug.Log($"[SkillManager] Assigning '{data.skillName}' to slot {slotIndex}");
        skillSlots[slotIndex].AssignSkill(skill);
        return true;
    }

    public bool EquipToCategorySlot(vc_SkillData data)
    {
        if (data == null) return false;
        int slotIndex = SlotIndexForLetter(data.riaSecLetter);
        if (slotIndex < 0) return false;
        return AssignSkillToSlot(slotIndex, data);
    }

    public vc_SkillData GetSkillInCategory(string letter)
    {
        int slotIndex = SlotIndexForLetter(letter);
        if (slotIndex < 0) return null;
        return GetSkillInSlot(slotIndex);
    }

    public void OfferEquip(vc_SkillData data)
    {
        if (data == null) return;

        int slot = SlotIndexForLetter(data.riaSecLetter);
        if (slot < 0) return;

        vc_SkillData current = GetSkillInSlot(slot);
        string categoryName = CategoryNameForSlot(slot);

        if (current == null)
        {
            EquipToCategorySlot(data);
            vc_FloatingMessage.Instance?.Show($"{data.skillName} added to your {categoryName} slot.");
            return;
        }

        if (current == data)
        {
            vc_DialogPanel.Instance?.ShowMessage("Already Equipped",
                $"{data.skillName} is already in your {categoryName} slot.", null);
            return;
        }

        vc_DialogPanel.Instance?.ShowChoice(
            "Swap Skill?",
            $"Your {categoryName} slot has {current.skillName}. Swap it for {data.skillName}? The other one stays in your inventory.",
            data.icon,
            "Swap",
            "Keep",
            () => EquipToCategorySlot(data),
            null);
    }

    public void RegisterSlot(vc_SkillSlot slot)
    {
        if (slot == null) return;
        int idx = slot.SlotIndex;
        if (skillSlots == null || idx < 0 || idx >= skillSlots.Length) return;
        if (skillSlots[idx] == slot) return;

        if (skillSlots[idx] != null)
            skillSlots[idx].SkillPressed -= HandleSkillPressed;

        skillSlots[idx] = slot;
        slot.RefreshDisplay();
        slot.SkillPressed += HandleSkillPressed;
    }

    private void EnsureSlotsResolved()
    {
        if (skillSlots == null) return;

        bool anyMissing = false;
        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (skillSlots[i] == null) { anyMissing = true; break; }
        }

        if (!anyMissing) return;

        vc_SkillSlot[] found = FindObjectsByType<vc_SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            RegisterSlot(found[i]);
        }
    }

    public void ResetForNewRun()
    {
        vc_PlayerSkill[] carriedSkills = GetComponentsInChildren<vc_PlayerSkill>(true);
        for (int i = 0; i < carriedSkills.Length; i++)
        {
            if (carriedSkills[i] != null)
                Destroy(carriedSkills[i].gameObject);
        }

        if (skillSlots != null)
        {
            for (int i = 0; i < skillSlots.Length; i++)
                skillSlots[i] = null;
        }

        ResetUsageCounts();
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
            skillType = typeof(vc_GenericSkill);
            SkillTypeCache[typeName] = skillType;
        }

        GameObject skillObject = new GameObject(skillData.skillName);
        skillObject.transform.SetParent(transform);
        vc_PlayerSkill newSkill = (vc_PlayerSkill)skillObject.AddComponent(skillType);
        newSkill.Initialize(skillData);
        return newSkill;
    }
}
