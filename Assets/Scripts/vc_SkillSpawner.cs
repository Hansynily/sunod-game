using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_SkillSpawner : MonoBehaviour
{
    [SerializeField] private GameObject pickupPrefab;
    [SerializeField] private vc_SkillData[] guaranteedPool;
    [SerializeField] private vc_SkillData[] randomPool;
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        if (pickupPrefab == null)
        {
            Debug.LogWarning($"[{nameof(vc_SkillSpawner)}] Pickup prefab is not assigned on {name}.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"[{nameof(vc_SkillSpawner)}] No spawn points assigned on {name}.");
            return;
        }

        List<vc_SkillData> toSpawn = new List<vc_SkillData>();

        if (guaranteedPool != null)
        {
            foreach (vc_SkillData skill in guaranteedPool)
            {
                if (skill == null || toSpawn.Contains(skill))
                {
                    continue;
                }

                toSpawn.Add(skill);
                if (toSpawn.Count >= spawnPoints.Length)
                {
                    break;
                }
            }
        }

        if (toSpawn.Count < spawnPoints.Length && randomPool != null)
        {
            List<vc_SkillData> randomCandidates = new List<vc_SkillData>();
            foreach (vc_SkillData skill in randomPool)
            {
                if (skill == null || toSpawn.Contains(skill))
                {
                    continue;
                }

                randomCandidates.Add(skill);
            }

            while (toSpawn.Count < spawnPoints.Length && randomCandidates.Count > 0)
            {
                int idx = Random.Range(0, randomCandidates.Count);
                toSpawn.Add(randomCandidates[idx]);
                randomCandidates.RemoveAt(idx);
            }
        }

        for (int i = 0; i < toSpawn.Count; i++)
        {
            if (spawnPoints[i] == null)
            {
                continue;
            }

            GameObject go = Instantiate(pickupPrefab, spawnPoints[i].position, Quaternion.identity);
            vc_SkillPickup pickup = go.GetComponent<vc_SkillPickup>();
            if (pickup != null)
            {
                pickup.SkillData = toSpawn[i];
            }
        }
    }
}
