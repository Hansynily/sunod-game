using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunodGame.Core
{
    public static class vc_SceneBootstrapper
    {
        private static readonly string[] FloorScenes = { "Level1_Scene", "Level2_Scene", "Level3_Scene" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureGameSceneLoaded()
        {
            string activeScene = SceneManager.GetActiveScene().name;

            bool isFloorScene = System.Array.IndexOf(FloorScenes, activeScene) >= 0;
            if (!isFloorScene) return;

            if (SceneManager.GetSceneByName(SceneLoader.SCENE_GAME).isLoaded) return;

            SceneManager.LoadSceneAsync(SceneLoader.SCENE_GAME, LoadSceneMode.Additive);
        }
    }
}
