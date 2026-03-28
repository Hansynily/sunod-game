using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunodGame.Demo
{
    public partial class ChallengeSessionController
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _sceneHookRegistered = false;
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            if (_sceneHookRegistered) return;

            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneHookRegistered = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapCurrentScene()
        {
            EnsureManagerForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureManagerForScene(scene);
        }

        internal static bool UsesChallengeSessionShell(Scene scene)
        {
            if (!scene.IsValid() || scene.name != DemoSceneName)
                return false;

            return FindSceneObjectByName(ChallengeHudName, scene) != null &&
                   FindSceneObjectByName(ChallengeRootNames[0], scene) != null;
        }

        private static void EnsureManagerForScene(Scene scene)
        {
            if (!UsesChallengeSessionShell(scene)) return;
            if (FindAnyObjectByType<ChallengeSessionController>() != null) return;

            var root = new GameObject("[Challenge Session Controller]");
            root.AddComponent<ChallengeSessionController>();
        }

        private void Awake()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!UsesChallengeSessionShell(activeScene))
            {
                Destroy(gameObject);
                return;
            }

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
    }
}
