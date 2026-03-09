using UnityEngine;

public class SkillController : MonoBehaviour
{
    public SkillData[] quickAccessSlots = new SkillData[4];

    public void AssignSkillToSlot(SkillData skill, int index)
    {
        quickAccessSlots[index] = skill;
    }

    public void ActivateSlot(int index)
    {
        quickAccessSlots[index].Activate(gameObject);
    }

    public void DeactivateSlot(int index)
    {
        quickAccessSlots[index].Deactivate(gameObject);
    }
}