using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunodGame.Core
{
    public class vc_FloorLoader : MonoBehaviour
    {
        public static vc_FloorLoader Instance { get; private set; }

        [SerializeField] private string startingFloor = "Level1_Scene";

        private static readonly string[] FloorScenes = { "Level1_Scene", "Level2_Scene", "Level3_Scene" };

        private string _currentFloorScene;
        public string CurrentFloorScene => _currentFloorScene;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Dev mode: hit Play directly from a floor scene — it's already loaded.
            foreach (string floor in FloorScenes)
            {
                if (SceneManager.GetSceneByName(floor).isLoaded)
                {
                    _currentFloorScene = floor;
                    OnFloorLoaded(SceneManager.GetSceneByName(floor));
                    return;
                }
            }

            // Normal game flow: Game_Scene was loaded first, now load the starting floor.
            StartCoroutine(LoadFloorRoutine(startingFloor));
        }

        public void LoadFloor(string sceneName)
        {
            StartCoroutine(LoadFloorRoutine(sceneName));
        }

        private IEnumerator LoadFloorRoutine(string sceneName)
        {
            if (!string.IsNullOrEmpty(_currentFloorScene))
                yield return SceneManager.UnloadSceneAsync(_currentFloorScene);

            _currentFloorScene = sceneName;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            OnFloorLoaded(SceneManager.GetSceneByName(sceneName));
        }

        private void OnFloorLoaded(Scene scene)
        {
            TeleportPlayerToSpawn(scene);
            RegisterSpawnPoints(scene);
        }

        private void TeleportPlayerToSpawn(Scene scene)
        {
            GameObject spawnGo = FindInScene(scene, "PlayerSpawnPoint");
            if (spawnGo == null)
            {
                Debug.LogWarning($"[vc_FloorLoader] No PlayerSpawnPoint found in {scene.name}.");
                return;
            }

            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                player.transform.position = spawnGo.transform.position;
        }

        private void RegisterSpawnPoints(Scene scene)
        {
            List<Transform> points = new List<Transform>();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                foreach (vc_SpawnPoint sp in go.GetComponentsInChildren<vc_SpawnPoint>(true))
                    points.Add(sp.transform);
            }

            vc_SkillSpawner spawner = FindFirstObjectByType<vc_SkillSpawner>();
            if (spawner != null)
                spawner.SetSpawnPoints(points);
        }

        private static GameObject FindInScene(Scene scene, string goName)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == goName) return go;
            return null;
        }
    }
}
