using UnityEngine;

[DisallowMultipleComponent]
public class vc_SkillSpawner : MonoBehaviour
{
    [SerializeField] private GameObject pickupPrefab;
    [SerializeField] private vc_SkillData[] skillPool;
    [SerializeField] private Transform[] spawnPoints;

    private bool _hasSpawned;

    private void Start()
    {
        SpawnPickups();
    }

    public void SpawnPickups()
    {
        if (_hasSpawned)
        {
            return;
        }

        if (pickupPrefab == null)
        {
            Debug.LogWarning($"[{nameof(vc_SkillSpawner)}] Pickup prefab is not assigned on {name}.");
            return;
        }

        if (skillPool == null || skillPool.Length == 0)
        {
            Debug.LogWarning($"[{nameof(vc_SkillSpawner)}] Skill pool is empty on {name}.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"[{nameof(vc_SkillSpawner)}] No spawn points assigned on {name}.");
            return;
        }

        vc_SkillData[] shuffled = ShuffleSkillPool(skillPool);
        int spawnCount = Mathf.Min(shuffled.Length, spawnPoints.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            Transform spawnPoint = spawnPoints[i];
            if (spawnPoint == null)
            {
                continue;
            }

            vc_SkillData chosen = shuffled[i];
            if (chosen == null)
            {
                continue;
            }

            GameObject instance = Instantiate(pickupPrefab, spawnPoint.position, Quaternion.identity, spawnPoint);
            vc_SkillPickup pickup = instance.GetComponent<vc_SkillPickup>();

            if (pickup == null)
            {
                Debug.LogWarning($"[{nameof(vc_SkillSpawner)}] Pickup prefab '{pickupPrefab.name}' is missing a vc_SkillPickup component. Destroying instance.");
                Destroy(instance);
                continue;
            }

            pickup.SkillData = chosen;
        }

        _hasSpawned = true;
    }

    private static vc_SkillData[] ShuffleSkillPool(vc_SkillData[] source)
    {
        vc_SkillData[] copy = new vc_SkillData[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            copy[i] = source[i];
        }

        for (int i = copy.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }
}
