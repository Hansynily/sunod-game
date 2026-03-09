using UnityEngine;
using SunodGame.Core;
using SunodGame.Telemetry;

namespace Sunod
{
    public class GameBootstrap : MonoBehaviour
    {
        void Awake()
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
