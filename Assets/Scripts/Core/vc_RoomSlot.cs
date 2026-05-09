using UnityEngine;

namespace SunodGame.Core
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class vc_RoomSlot : MonoBehaviour
    {
        public static vc_RoomSlot Current { get; private set; }

        [SerializeField] public bool acceptsQuest = true;
        public Bounds RoomBounds { get; private set; }

        private void Awake()
        {
            RoomBounds = GetComponent<BoxCollider2D>().bounds;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            Current = this;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (Current == this) Current = null;
        }
    }
}
