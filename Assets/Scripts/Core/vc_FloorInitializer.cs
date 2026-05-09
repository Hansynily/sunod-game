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
            public GameObject[] questPrefabs;  // inserted into slots 1+, slot 0 is always left empty
            public bool shuffle = true;
        }

        [SerializeField] private FloorData[] floors;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
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
                slots.AddRange(go.GetComponentsInChildren<vc_RoomSlot>(true));

            if (slots.Count == 0)
            {
                Debug.LogWarning($"[vc_FloorInitializer] No vc_RoomSlot found in {scene.name}.");
                return;
            }

            // Slot 0 is always left empty (starting/lobby area)
            // Quest prefabs go into slots 1+
            if (data.questPrefabs == null || data.questPrefabs.Length == 0) return;

            GameObject[] pool = new GameObject[data.questPrefabs.Length];
            System.Array.Copy(data.questPrefabs, pool, data.questPrefabs.Length);
            if (data.shuffle) Shuffle(pool);

            int count = Mathf.Min(slots.Count - 1, pool.Length);
            for (int i = 0; i < count; i++)
            {
                if (pool[i] == null) continue;
                GameObject room = Instantiate(pool[i], slots[i + 1].transform);
                room.transform.localPosition = Vector3.zero;
            }
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
