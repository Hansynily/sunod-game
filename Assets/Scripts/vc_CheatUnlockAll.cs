using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class vc_CheatUnlockAll : MonoBehaviour
{
    [SerializeField] private vc_SkillData[] allSkills;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(UnlockAll);
    }

    public void UnlockAll()
    {
        if (vc_PlayerInventory.Instance == null || allSkills == null) return;

        foreach (vc_SkillData skill in allSkills)
        {
            if (skill != null)
                vc_PlayerInventory.Instance.AddSkill(skill);
        }

        Debug.Log($"[CheatUnlockAll] Added {allSkills.Length} skills to inventory.");
    }
}
