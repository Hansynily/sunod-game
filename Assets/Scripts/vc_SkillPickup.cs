using UnityEngine;

[DisallowMultipleComponent]
public class vc_SkillPickup : MonoBehaviour
{
    [SerializeField] private vc_SkillData skillData;

    public vc_SkillData SkillData
    {
        get => skillData;
        set => skillData = value;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag != "Player")
        {
            return;
        }

        if (skillData == null)
        {
            return;
        }

        vc_PlayerInventory.Instance.AddSkill(skillData);

        vc_FloatingMessage msg = FindFirstObjectByType<vc_FloatingMessage>();
        if (msg != null)
        {
            msg.Show("Skill " + skillData.skillName + " added to inventory");
        }

        Destroy(gameObject);
    }
}
