using UnityEditor;
using UnityEngine;

/// <summary>
/// SUNOD > Minigames > Wire Elementary Minigames: adds each minigame script next to its room's
/// old quest logic and sets vc_QuestRoom.minigameLogic, so "New gameplay" plays the minigame.
/// Safe to run again (skips rooms already wired).
/// </summary>
public static class vc_MinigameWiring
{
    private static readonly (string prefab, System.Type type)[] Rooms =
    {
        ("Assets/Prefabs/Rooms/Room_River_RiverCrossing.prefab", typeof(vc_RiverStonesMinigame)),
        ("Assets/Prefabs/Rooms/Room_Forest_SeedlingStudy.prefab", typeof(vc_SeedlingStudyMinigame)),
        ("Assets/Prefabs/Rooms/Room_School_StoryTime.prefab", typeof(vc_StoryTimeMinigame)),
        ("Assets/Prefabs/Rooms/Room_School_SnackStall.prefab", typeof(vc_SnackStallMinigame)),
        ("Assets/Prefabs/Rooms/Room_School_CanteenChange.prefab", typeof(vc_CanteenChangeMinigame)),
        ("Assets/Prefabs/Rooms/Room_House_CreakyFloorboards.prefab", typeof(vc_FloorNailsMinigame)),
        ("Assets/Prefabs/Rooms/Room_House_MissingKeyQuest.prefab", typeof(vc_SupplyCountMinigame)),
        ("Assets/Prefabs/Rooms/Room_School_ClassSong.prefab", typeof(vc_ClassSongMinigame)),
        ("Assets/Prefabs/Rooms/Room_House_ToyStore.prefab", typeof(vc_ToyStoreMinigame)),
        ("Assets/Prefabs/Rooms/Room_River_FishCount.prefab", typeof(vc_FishCountMinigame)),
        ("Assets/Prefabs/Rooms/Room_Forest_MorningExercise.prefab", typeof(vc_MorningExerciseMinigame)),
        ("Assets/Prefabs/Rooms/Room_Forest_WatchTheRabbits.prefab", typeof(vc_RabbitWatchMinigame)),
        ("Assets/Prefabs/Rooms/Room_School_FairPoster.prefab", typeof(vc_FairPosterMinigame)),
        ("Assets/Prefabs/Rooms/Room_School_LostFriendQuest.prefab", typeof(vc_PlaygroundWatchMinigame)),
    };

    [MenuItem("SUNOD/Minigames/Wire Elementary Minigames")]
    public static void Wire()
    {
        foreach (var (path, type) in Rooms)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var room = root.GetComponentInChildren<vc_QuestRoom>(true);
                if (room == null) { Debug.LogError($"[Wiring] no vc_QuestRoom in {path}"); continue; }
                var so = new SerializedObject(room);
                var logic = so.FindProperty("questLogic").objectReferenceValue as MonoBehaviour;
                var mini = so.FindProperty("minigameLogic");
                GameObject host = logic != null ? logic.gameObject : room.gameObject;

                var comp = host.GetComponent(type) as MonoBehaviour;
                if (comp == null) comp = host.AddComponent(type) as MonoBehaviour;
                if (host.GetComponent<vc_MinigameOverlay>() == null) host.AddComponent<vc_MinigameOverlay>();
                comp.enabled = false; // vc_QuestRoom turns it on in New gameplay
                mini.objectReferenceValue = comp;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Wiring] {path}: {type.Name} on '{host.name}' (old logic: {(logic != null ? logic.GetType().Name : "none")})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
    }
}
