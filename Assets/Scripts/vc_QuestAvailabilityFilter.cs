using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class vc_QuestAvailabilityFilter : MonoBehaviour
{
    public static vc_QuestAvailabilityFilter Instance { get; private set; }

    [SerializeField] private vc_GameSettings gameSettings;

    private readonly HashSet<vc_QuestRoom> _sessionExposureRooms = new HashSet<vc_QuestRoom>();
    private readonly HashSet<GameObject> _usedPrefabs = new HashSet<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Clears the used-quest tracking. Call at the start of a new game run.
    /// </summary>
    public void ClearRun()
    {
        _usedPrefabs.Clear();
    }

    /// <summary>
    /// Draws prefabs from the global registry for a floor's quest slots.
    /// Separates into skill-matched pool and exposure pool, combines, shuffles.
    /// Called by vc_FloorInitializer before instantiating rooms.
    /// </summary>
    public GameObject[] DrawQuestsForFloor(int slotCount)
    {
        if (gameSettings == null || gameSettings.questPrefabs == null)
        {
            Debug.LogWarning("[vc_QuestAvailabilityFilter] No game settings assigned. No quests will be placed.");
            return System.Array.Empty<GameObject>();
        }

        List<GameObject> matched = new List<GameObject>();
        List<GameObject> exposureCandidates = new List<GameObject>();

        foreach (GameObject prefab in gameSettings.questPrefabs)
        {
            if (prefab == null) continue;
            if (_usedPrefabs.Contains(prefab)) continue;

            vc_QuestRoom qr = prefab.GetComponentInChildren<vc_QuestRoom>(true);
            if (qr == null)
            {
                matched.Add(prefab);
                continue;
            }

            bool canSolve = CanPlayerSolve(qr);
            if (canSolve)
                matched.Add(prefab);
            else if (qr.IsExposureEligible)
                exposureCandidates.Add(prefab);
            // Can't solve + not exposure eligible = never surfaces this floor
        }

        ShuffleList(matched);
        ShuffleList(exposureCandidates);

        int exposurePick = gameSettings != null
            ? Mathf.Min(gameSettings.exposureCount, exposureCandidates.Count)
            : 0;

        List<GameObject> selected = new List<GameObject>();

        int matchedCount = Mathf.Min(slotCount - exposurePick, matched.Count);
        for (int i = 0; i < matchedCount; i++)
            selected.Add(matched[i]);

        for (int i = 0; i < exposurePick; i++)
            selected.Add(exposureCandidates[i]);

        ShuffleList(selected);

        foreach (GameObject prefab in selected)
            _usedPrefabs.Add(prefab);

        Debug.Log($"[vc_QuestAvailabilityFilter] Drew {selected.Count} quests for floor ({matchedCount} matched, {exposurePick} exposure).");
        return selected.ToArray();
    }

    private static bool CanPlayerSolve(vc_QuestRoom qr)
    {
        if (vc_PlayerInventory.Instance == null) return false;

        // AND logic: player must have ALL combo tags for this quest to surface.
        if (qr.RequiredComboTags != null && qr.RequiredComboTags.Length > 0)
        {
            foreach (string tag in qr.RequiredComboTags)
            {
                if (!vc_PlayerInventory.Instance.HasSkillByTag(tag))
                    return false;
            }
            return true;
        }

        // OR logic: player has any one of the listed tags.
        if (qr.SolvableWithTags != null && qr.SolvableWithTags.Length > 0)
        {
            foreach (string tag in qr.SolvableWithTags)
            {
                if (vc_PlayerInventory.Instance.HasSkillByTag(tag))
                    return true;
            }
            return false;
        }

        // Nothing declared = universally solvable. Safe default while quests are being authored.
        return true;
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    /// <summary>
    /// Call once per floor load, after room prefabs are instantiated.
    /// Scans the scene for hard-locked exposure-eligible rooms the player
    /// can't normally access, then randomly surfaces up to exposureCount of them.
    /// </summary>
    public void InitializeFloor(Scene scene)
    {
        _sessionExposureRooms.Clear();

        if (gameSettings == null)
        {
            Debug.LogWarning("[vc_QuestAvailabilityFilter] No game settings assigned. Exposure pool disabled.");
            return;
        }

        List<vc_QuestRoom> allRooms = new List<vc_QuestRoom>();
        foreach (GameObject go in scene.GetRootGameObjects())
            foreach (vc_QuestRoom room in go.GetComponentsInChildren<vc_QuestRoom>(true))
                allRooms.Add(room);

        // Build exposure candidates: hard-locked, marked eligible, and player lacks the skill
        List<vc_QuestRoom> candidates = new List<vc_QuestRoom>();
        foreach (vc_QuestRoom room in allRooms)
        {
            if (room.LockType != vc_QuestRoom.QuestLockType.Hard) continue;
            if (!room.IsExposureEligible) continue;
            bool playerHasSkill = vc_PlayerInventory.Instance != null
                && vc_PlayerInventory.Instance.HasSkillByTag(room.RequiredSkillTag);
            if (!playerHasSkill)
                candidates.Add(room);
        }

        // Fisher-Yates shuffle
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            vc_QuestRoom temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        int pickCount = Mathf.Min(gameSettings.exposureCount, candidates.Count);
        for (int i = 0; i < pickCount; i++)
            _sessionExposureRooms.Add(candidates[i]);

        Debug.Log($"[vc_QuestAvailabilityFilter] Floor '{scene.name}': {allRooms.Count} rooms, {candidates.Count} exposure candidates, {pickCount} surfaced.");
    }

    /// <summary>
    /// Returns true if the player can attempt this quest.
    /// None and Soft are always available.
    /// Hard requires the player to own the required skill OR the room was drawn into the exposure set.
    /// </summary>
    public bool IsAvailable(vc_QuestRoom room)
    {
        if (room == null) return false;
        if (room.LockType != vc_QuestRoom.QuestLockType.Hard) return true;
        if (vc_PlayerInventory.Instance != null
            && vc_PlayerInventory.Instance.HasSkillByTag(room.RequiredSkillTag)) return true;
        return _sessionExposureRooms.Contains(room);
    }
}
