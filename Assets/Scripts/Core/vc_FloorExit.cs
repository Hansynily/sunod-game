using UnityEngine;

namespace SunodGame.Core
{
    public class vc_FloorExit : MonoBehaviour
    {
        [SerializeField] private string nextFloorScene;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (string.IsNullOrEmpty(nextFloorScene))
            {
                Debug.LogWarning("[vc_FloorExit] nextFloorScene is not assigned.");
                return;
            }

            vc_FloorLoader.Instance.LoadFloor(nextFloorScene);
        }
    }
}
