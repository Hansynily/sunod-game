using NUnit.Framework.Interfaces;
using UnityEngine;

public class SkillPickup : MonoBehaviour
{
    public SkillData skill;
    public QuestData quest;
    public int slotIndex = 0;
    public ScheduleBarManager manager; 
    private void OnTriggerEnter2D(Collider2D collision)
    {
        SkillController skillController = collision.GetComponent<SkillController>();
        if (skillController != null && skill != null)
        {
            skillController.AssignSkillToSlot(skill, slotIndex);
            //SkillInventory.Instance.AddSkill(skill);
            manager.AddQuest(quest);
            Destroy(gameObject);
        }
    }
}