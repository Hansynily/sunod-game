using System.Collections.Generic;
using UnityEngine;
using SunodGame.Core;

/// <summary>
/// Physical hallway pickup that opens the vc_SkillAwardPopup with a weighted skill draw.
/// Place in hallway between rooms. Assign skillPool (all vc_SkillData assets) and
/// nextRoomSlot (the RoomSlot after this hallway) in the Inspector.
/// Destroys itself after the player makes a selection.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class vc_SkillSelectionMarker : MonoBehaviour
{
    [SerializeField] private vc_GameSettings gameSettings;
    [SerializeField] private vc_SkillData[] skillPool;
    [SerializeField] private vc_RoomSlot nextRoomSlot;

    [Header("Starting Kit Mode")]
    [Tooltip("When enabled, runs multiple pick rounds to seed the player's starting inventory. Used in Room_School_StartingArea.")]
    [SerializeField] private bool startingKitMode = false;
    [SerializeField] private int picksRequired = 1;

    private int _picksDone = 0;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (vc_SkillAwardPopup.Instance == null)
        {
            Debug.LogWarning("[vc_SkillSelectionMarker] vc_SkillAwardPopup.Instance is null.");
            return;
        }

        // Starting kit mode ignores nextRoomSlot weighting — all skills are equal weight at game start.
        string nextRiasec = string.Empty;
        if (!startingKitMode && nextRoomSlot != null)
        {
            vc_QuestRoom qr = nextRoomSlot.GetComponentInChildren<vc_QuestRoom>();
            if (qr != null) nextRiasec = qr.PrimaryRiasec;
        }

        vc_SkillData[] picks = BuildWeightedPicks(nextRiasec, 3);
        if (picks.Length == 0)
        {
            Debug.LogWarning("[vc_SkillSelectionMarker] No skills available to offer.");
            return;
        }

        _picksDone = 0;
        gameObject.SetActive(false);
        vc_SkillAwardPopup.Instance.Show(picks, OnSkillChosen);
    }

    private void OnSkillChosen(vc_SkillData chosen)
    {
        if (chosen != null)
            vc_PlayerInventory.Instance?.AddSkill(chosen);

        _picksDone++;

        if (startingKitMode && _picksDone < picksRequired)
        {
            if (vc_SkillAwardPopup.Instance == null)
            {
                Debug.LogWarning("[vc_SkillSelectionMarker] vc_SkillAwardPopup.Instance is null during starting kit chain.");
                Destroy(gameObject);
                return;
            }

            // BuildWeightedPicks already excludes skills the player owns, so each round offers fresh choices.
            vc_SkillData[] picks = BuildWeightedPicks(string.Empty, 3);
            if (picks.Length > 0)
            {
                vc_SkillAwardPopup.Instance.Show(picks, OnSkillChosen);
                return;
            }
        }

        if (nextRoomSlot != null)
        {
            vc_QuestAvailabilityFilter filter = vc_QuestAvailabilityFilter.Instance
                ?? FindFirstObjectByType<vc_QuestAvailabilityFilter>();
            if (filter != null)
            {
                GameObject[] pool = filter.DrawQuestsForFloor(1);
                if (pool.Length > 0)
                {
                    GameObject room = Instantiate(pool[0], nextRoomSlot.transform);
                    room.transform.localPosition = Vector3.zero;
                    filter.InitializeFloor(nextRoomSlot.gameObject.scene);
                }
            }
        }

        Destroy(gameObject);
    }

    private vc_SkillData[] BuildWeightedPicks(string nextRiasec, int count)
    {
        vc_SkillData[] source = (skillPool != null && skillPool.Length > 0)
            ? skillPool
            : (gameSettings != null ? gameSettings.skills : null);

        if (source == null || source.Length == 0)
        {
            Debug.LogWarning("[vc_SkillSelectionMarker] No skill pool or registry assigned.");
            return System.Array.Empty<vc_SkillData>();
        }

        var pool = new List<(vc_SkillData skill, int weight)>();
        foreach (vc_SkillData skill in source)
        {
            if (skill == null) continue;
            if (vc_PlayerInventory.Instance != null && vc_PlayerInventory.Instance.HasSkill(skill)) continue;

            int w = (!string.IsNullOrEmpty(nextRiasec) &&
                     string.Equals(skill.riaSecLetter, nextRiasec, System.StringComparison.OrdinalIgnoreCase)) ? 3 : 1;
            pool.Add((skill, w));
        }

        var picks = new List<vc_SkillData>();
        while (picks.Count < count && pool.Count > 0)
        {
            int total = 0;
            foreach (var entry in pool) total += entry.weight;

            int rand = Random.Range(0, total);
            int cumulative = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += pool[i].weight;
                if (rand < cumulative)
                {
                    picks.Add(pool[i].skill);
                    pool.RemoveAt(i);
                    break;
                }
            }
        }

        return picks.ToArray();
    }
}
