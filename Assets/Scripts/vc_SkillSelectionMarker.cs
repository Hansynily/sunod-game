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
    [SerializeField] private vc_SkillData[] skillPool;
    [SerializeField] private vc_RoomSlot nextRoomSlot;

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

        string nextRiasec = string.Empty;
        if (nextRoomSlot != null)
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

        gameObject.SetActive(false);
        vc_SkillAwardPopup.Instance.Show(picks, OnSkillChosen);
    }

    private void OnSkillChosen(vc_SkillData chosen)
    {
        if (chosen != null)
            vc_PlayerInventory.Instance?.AddSkill(chosen);

        Destroy(gameObject);
    }

    private vc_SkillData[] BuildWeightedPicks(string nextRiasec, int count)
    {
        if (skillPool == null || skillPool.Length == 0)
            return System.Array.Empty<vc_SkillData>();

        var pool = new List<(vc_SkillData skill, int weight)>();
        foreach (vc_SkillData skill in skillPool)
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
