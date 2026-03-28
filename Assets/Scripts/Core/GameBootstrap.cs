using UnityEngine;
using SunodGame.Core;
using SunodGame.Telemetry;

namespace Sunod
{
    public class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BootstrapBeforeSceneLoad()
        {
            EnsureCoreServices();
        }

        void Awake()
        {
            EnsureCoreServices();
        }

        private static void EnsureCoreServices()
        {
            if (FindFirstObjectByType<TelemetryManager>() != null) return;

            var root = new GameObject("[Sunod Singletons]");
            DontDestroyOnLoad(root);

            root.AddComponent<SessionState>();
            root.AddComponent<TelemetryManager>();
            root.AddComponent<AuthManager>();

            //Debug.Log("[Bootstrap] Singletons initialised.");
        }
    }
}
