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
        if (!other.CompareTag("Player")) return;
        if (skillData == null) return;
        if (vc_PlayerInventory.Instance.HasSkill(skillData)) return;

        if (vc_SkillPickupPopup.Instance == null)
        {
            vc_PlayerInventory.Instance.AddSkill(skillData);
            Destroy(gameObject);
            return;
        }

        vc_SkillPickupPopup.Instance.Show(skillData, () =>
        {
            vc_PlayerInventory.Instance.AddSkill(skillData);
            Destroy(gameObject);
        });
    }
}
