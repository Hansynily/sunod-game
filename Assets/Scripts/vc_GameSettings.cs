using UnityEngine;

[CreateAssetMenu(fileName = "vc_GameSettings", menuName = "SUNOD/Game Settings")]
public class vc_GameSettings : ScriptableObject
{
    [Header("Skills")]
    [Tooltip("All vc_SkillData assets in the game. Every skill marker draws from this automatically.")]
    public vc_SkillData[] skills;

    [Header("Quests")]
    [Tooltip("All quest prefabs in the game. Every floor draws from this pool.")]
    public GameObject[] questPrefabs;

    [Header("Quest Availability")]
    [Tooltip("How many exposure-eligible hard-locked quests surface per floor regardless of skill match. Set with advisor.")]
    public int exposureCount = 1;
}
