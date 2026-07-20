using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class vc_CheatUnlockAll : MonoBehaviour
{
    [Tooltip("Preferred source: the master skill registry. When assigned, EVERY skill in " +
             "vc_GameSettings.skills is granted, so newly added skills are picked up with no " +
             "extra wiring. Leave the manual list below empty when this is set.")]
    [SerializeField] private vc_GameSettings gameSettings;

    [Tooltip("Fallback list, only used when Game Settings is not assigned. Hand-maintained, so " +
             "it goes stale when you add new skills - prefer assigning Game Settings instead.")]
    [SerializeField] private vc_SkillData[] allSkills;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(UnlockAll);
    }

    public void UnlockAll()
    {
        if (vc_PlayerInventory.Instance == null) return;

        // Prefer the assigned master list; if none was wired, borrow the SAME GameSettings the
        // quest system uses (always present in the play scene) so the cheat needs zero Inspector
        // setup and always reflects every skill in the master list. Manual array is last resort.
        vc_GameSettings source = gameSettings;
        if (source == null || source.skills == null || source.skills.Length == 0)
        {
            vc_QuestAvailabilityFilter filter = vc_QuestAvailabilityFilter.Instance
                ?? FindFirstObjectByType<vc_QuestAvailabilityFilter>(FindObjectsInactive.Include);
            if (filter != null && filter.GameSettings != null)
                source = filter.GameSettings;
        }

        vc_SkillData[] skills = (source != null && source.skills != null && source.skills.Length > 0)
            ? source.skills
            : allSkills;

        if (skills == null) return;

        int added = 0;
        foreach (vc_SkillData skill in skills)
        {
            if (skill != null)
            {
                vc_PlayerInventory.Instance.AddSkill(skill);
                added++;
            }
        }

        Debug.Log($"[CheatUnlockAll] Added {added} skills to inventory.");
    }
}
