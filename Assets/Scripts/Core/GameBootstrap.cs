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
            GameObject root = null;
            GameObject GetRoot()
            {
                if (root != null) return root;
                root = FindFirstObjectByType<SessionState>()?.gameObject
                    ?? FindFirstObjectByType<TelemetryManager>()?.gameObject
                    ?? FindFirstObjectByType<AuthManager>()?.gameObject;
                if (root == null)
                {
                    root = new GameObject("[Sunod Singletons]");
                    DontDestroyOnLoad(root);
                }
                return root;
            }

            if (FindFirstObjectByType<SessionState>() == null) GetRoot().AddComponent<SessionState>();
            if (FindFirstObjectByType<TelemetryManager>() == null) GetRoot().AddComponent<TelemetryManager>();
            if (FindFirstObjectByType<AuthManager>() == null) GetRoot().AddComponent<AuthManager>();

            //Debug.Log("[Bootstrap] Singletons initialised.");
        }
    }
}
