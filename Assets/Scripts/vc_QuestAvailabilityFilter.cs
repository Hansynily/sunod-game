using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class vc_QuestAvailabilityFilter : MonoBehaviour
{
    public static vc_QuestAvailabilityFilter Instance { get; private set; }

    [SerializeField] private vc_GameSettings gameSettings;

    /// <summary>The master GameSettings assigned in the scene. Read-only accessor so other
    /// systems (e.g. the +ALL skill cheat) can reuse the one master skill list.</summary>
    public vc_GameSettings GameSettings => gameSettings;

    private readonly HashSet<vc_QuestRoom> _sessionExposureRooms = new HashSet<vc_QuestRoom>();
    private readonly HashSet<GameObject> _usedPrefabs = new HashSet<GameObject>();

    /// <summary>
    /// Set by the main menu's Continue button before loading the floor scene - this
    /// component is scene-scoped (no DontDestroyOnLoad), so a static handoff is how a
    /// freshly-created instance learns which quests the resumed run already completed.
    /// Consumed and cleared in Awake().
    /// </summary>
    public static List<string> PendingCompletedQuestIds;

    /// <summary>
    /// Skill NAMES (vc_SkillData.skillName) the resumed run owned - set by Continue,
    /// resolved to assets via gameSettings and re-equipped. Applied idempotently the
    /// first time it's needed (Start or the first quest draw, whichever comes first),
    /// so the player's inventory is populated BEFORE quest availability is computed.
    /// </summary>
    public static List<string> PendingOwnedSkillNames;

    private bool _skillRestoreApplied;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (PendingCompletedQuestIds != null)
        {
            SeedCompletedQuestIds(PendingCompletedQuestIds);
            PendingCompletedQuestIds = null;
        }

        // Always exclude quests the local save already records as completed, so floor
        // transitions and resumed runs never re-offer a finished quest. Fresh runs delete
        // the save before loading, so there is nothing stale to seed there.
        vc_SaveManager.SaveData save = vc_SaveManager.Load();
        if (save != null && save.completedQuestIds != null)
            SeedCompletedQuestIds(save.completedQuestIds);
    }

    private void Start()
    {
        // Belt-and-suspenders: apply here too. DrawQuestsForFloor also calls this, and the
        // done-flag makes whichever runs first (with the singletons ready) the one that wins.
        ApplyPendingSkillRestore();
    }

    /// <summary>
    /// Resolves the resumed run's owned skill names to vc_SkillData assets (via the master
    /// list in gameSettings) and re-equips them into the player's inventory and skill slots.
    /// Idempotent and heavily null-guarded - if a dependency isn't ready it logs and leaves
    /// the pending list in place for the next call rather than crashing or half-applying.
    /// </summary>
    public void ApplyPendingSkillRestore()
    {
        if (_skillRestoreApplied) return;
        if (PendingOwnedSkillNames == null) return; // nothing to restore (normal fresh run)

        if (gameSettings == null || gameSettings.skills == null)
        {
            Debug.LogWarning("[vc_QuestAvailabilityFilter] Skill restore skipped - no gameSettings/skills master list assigned.");
            return;
        }

        // Both singletons must exist to restore meaningfully; if not yet, defer to the next call.
        if (vc_PlayerInventory.Instance == null)
        {
            Debug.Log("[vc_QuestAvailabilityFilter] Deferring skill restore - PlayerInventory not ready yet.");
            return;
        }

        List<string> names = PendingOwnedSkillNames;
        int equippedSlot = 0;
        foreach (string skillName in names)
        {
            if (string.IsNullOrWhiteSpace(skillName)) continue;

            vc_SkillData resolved = System.Array.Find(gameSettings.skills,
                s => s != null && string.Equals(s.skillName, skillName, System.StringComparison.OrdinalIgnoreCase));

            if (resolved == null)
            {
                Debug.LogWarning($"[vc_QuestAvailabilityFilter] Owned skill '{skillName}' not found in the master list - skipped.");
                continue;
            }

            vc_PlayerInventory.Instance.AddSkill(resolved);      // ownership (drives quest availability)
            vc_SkillManager.Instance?.AssignSkillToSlot(equippedSlot, resolved); // usable equip
            equippedSlot++;
        }

        _skillRestoreApplied = true;
        PendingOwnedSkillNames = null;
        Debug.Log($"[vc_QuestAvailabilityFilter] Restored {equippedSlot} skill(s) for the resumed run.");
    }

    /// <summary>
    /// Marks quests already completed in a resumed run as "used" so DrawQuestsForFloor
    /// won't offer them again. Matches by vc_QuestRoom.QuestId against the registry's
    /// configured prefabs - additive, does not change fresh-run behavior.
    /// </summary>
    public void SeedCompletedQuestIds(IEnumerable<string> completedQuestIds)
    {
        if (gameSettings == null || gameSettings.questPrefabs == null || completedQuestIds == null)
            return;

        HashSet<string> idSet = new HashSet<string>(completedQuestIds);
        if (idSet.Count == 0) return;

        foreach (GameObject prefab in gameSettings.questPrefabs)
        {
            if (prefab == null) continue;
            vc_QuestRoom qr = prefab.GetComponentInChildren<vc_QuestRoom>(true);
            if (qr != null && idSet.Contains(qr.QuestId))
                _usedPrefabs.Add(prefab);
        }
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
        // Ensure a resumed run's owned skills are re-equipped BEFORE availability is computed -
        // CanPlayerSolve reads the inventory, so the restore must land first.
        ApplyPendingSkillRestore();

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

        List<GameObject> selected = new List<GameObject>();

        // Solvable quests fill slots first. Exposure quests only take slots the solvable
        // pool could not fill, and the total never exceeds slotCount, so no prefab is ever
        // marked used without also being returned to the caller.
        int matchedCount = Mathf.Min(slotCount, matched.Count);
        for (int i = 0; i < matchedCount; i++)
            selected.Add(matched[i]);

        int exposureBudget = Mathf.Min(gameSettings != null ? gameSettings.exposureCount : 0, exposureCandidates.Count);
        int exposurePick = Mathf.Clamp(slotCount - matchedCount, 0, exposureBudget);
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
