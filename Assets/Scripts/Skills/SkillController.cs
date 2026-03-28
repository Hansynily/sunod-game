// LEGACY-PARKED: Not good for demo. Only use when on non-demo phase.
using UnityEngine;

public class SkillController : MonoBehaviour
{
    public SkillData[] quickAccessSlots = new SkillData[4];

    public void AssignSkillToSlot(SkillData skill, int index)
    {
        if (index < 0 || index >= quickAccessSlots.Length)
            return;

        quickAccessSlots[index] = skill;
    }

    public void ActivateSlot(int index)
    {
        if (index < 0 || index >= quickAccessSlots.Length)
            return;

        SkillData skill = quickAccessSlots[index];
        if (skill == null)
            return;

        skill.Activate(gameObject);
    }

    public void DeactivateSlot(int index)
    {
        if (index < 0 || index >= quickAccessSlots.Length)
            return;

        SkillData skill = quickAccessSlots[index];
        if (skill == null)
            return;

        skill.Deactivate(gameObject);
    }
}
