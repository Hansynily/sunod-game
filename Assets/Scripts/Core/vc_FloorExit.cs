using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunodGame.Core
{
    public class vc_FloorExit : MonoBehaviour
    {
        private static readonly System.Collections.Generic.HashSet<string> FloorScenes =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { "Level1_Scene", "Level2_Scene", "Level3_Scene" };

        [SerializeField] private string nextFloorScene;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (string.IsNullOrEmpty(nextFloorScene))
            {
                Debug.LogWarning("[vc_FloorExit] nextFloorScene is not assigned.");
                return;
            }

            if (!FloorScenes.Contains(nextFloorScene))
            {
                StartCoroutine(SubmitAndLoad());
                return;
            }

            vc_FloorLoader.Instance.LoadFloor(nextFloorScene);
        }

        private IEnumerator SubmitAndLoad()
        {
            vc_SessionTelemetry telemetry = vc_SessionTelemetry.Instance;
            if (telemetry == null)
            {
                Debug.LogWarning("[vc_FloorExit] No SessionTelemetry found — loading scene directly.");
                SceneManager.LoadScene(nextFloorScene);
                yield break;
            }

            yield return telemetry.SubmitRunSummary(
                null,
                error =>
                {
                    if (!string.IsNullOrWhiteSpace(error))
                        Debug.LogWarning($"[vc_FloorExit] SubmitRunSummary failed: {error}");
                });

            yield return telemetry.SubmitAndPredict(cluster =>
            {
                Debug.Log($"[vc_FloorExit] Prediction complete. Cluster={cluster}");
            });

            SceneManager.LoadScene(nextFloorScene);
        }
    }
}
