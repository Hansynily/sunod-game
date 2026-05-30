using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunodGame.Core
{
    public class vc_FloorInitializer : MonoBehaviour
    {
        public static vc_FloorInitializer Instance { get; private set; }

        [System.Serializable]
        public class FloorData
        {
            public string sceneName;
            public GameObject slot0Prefab;
            public GameObject hallwayPrefab;
        }

        [SerializeField] private FloorData[] floors;

        private Scene _pendingScene;
        private List<vc_RoomSlot> _pendingSlots;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (vc_PlayerInventory.Instance != null)
                vc_PlayerInventory.Instance.OnSkillAdded -= OnFirstSkillAdded;
        }

        public void InitializeFloor(Scene scene)
        {
            FloorData data = GetFloorData(scene.name);
            if (data == null)
            {
                Debug.LogWarning($"[vc_FloorInitializer] No floor data configured for scene: {scene.name}");
                return;
            }

            List<vc_RoomSlot> slots = new List<vc_RoomSlot>();
            foreach (GameObject go in scene.GetRootGameObjects())
                foreach (vc_RoomSlot slot in go.GetComponentsInChildren<vc_RoomSlot>(true))
                    if (slot.acceptsQuest) slots.Add(slot);

            if (slots.Count == 0)
            {
                Debug.LogWarning($"[vc_FloorInitializer] No vc_RoomSlot found in {scene.name}.");
                return;
            }

            // Place hallway prefabs into all vc_HallwaySlot positions
            if (data.hallwayPrefab != null)
            {
                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    foreach (vc_HallwaySlot hallwaySlot in go.GetComponentsInChildren<vc_HallwaySlot>(true))
                    {
                        if (hallwaySlot.IsLoaded) continue;
                        hallwaySlot.MarkLoaded();
                        GameObject hallway = Instantiate(data.hallwayPrefab, hallwaySlot.transform);
                        hallway.transform.localPosition = Vector3.zero;
                    }
                }
            }

            // Slot 0: always place immediately
            if (data.slot0Prefab != null)
            {
                GameObject room0 = Instantiate(data.slot0Prefab, slots[0].transform);
                room0.transform.localPosition = Vector3.zero;
            }

            if (slots.Count <= 1) return;

            // Slots 1+: if player already has skills (floors 2+), place now.
            // On floor 1 first load, player hasn't hit the skill marker yet — defer
            // until their first skill is picked so the filter has a real inventory to read.
            bool playerHasSkills = vc_PlayerInventory.Instance != null
                && vc_PlayerInventory.Instance.GatheredSkills.Count > 0;

            if (playerHasSkills)
            {
                PlaceQuestInSlot(slots[1]);
            }
            else
            {
                _pendingScene = scene;
                _pendingSlots = slots;
                if (vc_PlayerInventory.Instance != null)
                    vc_PlayerInventory.Instance.OnSkillAdded += OnFirstSkillAdded;
                else
                    Debug.LogWarning("[vc_FloorInitializer] vc_PlayerInventory.Instance is null — quest rooms will not be placed.");
            }
        }

        private void OnFirstSkillAdded(vc_SkillData skill)
        {
            if (vc_PlayerInventory.Instance != null)
                vc_PlayerInventory.Instance.OnSkillAdded -= OnFirstSkillAdded;

            // Place only the first quest slot — hallway markers place each subsequent slot
            // after the player picks a skill in the hallway between quests.
            if (_pendingSlots != null && _pendingSlots.Count > 1)
                PlaceQuestInSlot(_pendingSlots[1]);

            _pendingSlots = null;
        }

        /// <summary>
        /// Places one quest room into a specific slot. Called by hallway skill markers
        /// after the player picks a skill, so each room is placed with the player's
        /// actual inventory at that moment — not all at floor load.
        /// </summary>
        public void PlaceQuestInSlot(vc_RoomSlot slot)
        {
            if (slot == null) return;
            if (slot.transform.childCount > 0) return;

            vc_QuestAvailabilityFilter filter = vc_QuestAvailabilityFilter.Instance
                ?? FindFirstObjectByType<vc_QuestAvailabilityFilter>();
            GameObject[] pool = filter != null
                ? filter.DrawQuestsForFloor(1)
                : System.Array.Empty<GameObject>();

            if (pool.Length == 0)
            {
                Debug.LogWarning($"[vc_FloorInitializer] No eligible quest for slot '{slot.name}'.");
                vc_EndRunUI ui = vc_EndRunUI.Instance ?? FindFirstObjectByType<vc_EndRunUI>();
                ui?.Show();
                return;
            }

            GameObject room = Instantiate(pool[0], slot.transform);
            room.transform.localPosition = Vector3.zero;

            vc_QuestAvailabilityFilter filterPost = vc_QuestAvailabilityFilter.Instance
                ?? FindFirstObjectByType<vc_QuestAvailabilityFilter>();
            filterPost?.InitializeFloor(slot.gameObject.scene);
            Debug.Log($"[vc_FloorInitializer] Placed quest in slot '{slot.name}'.");
        }

        private FloorData GetFloorData(string sceneName)
        {
            if (floors == null) return null;
            foreach (FloorData floor in floors)
                if (floor.sceneName == sceneName) return floor;
            return null;
        }

        private static void Shuffle<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }
}
