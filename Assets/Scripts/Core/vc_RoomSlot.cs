using UnityEngine;

namespace SunodGame.Core
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class vc_RoomSlot : MonoBehaviour
    {
        public static vc_RoomSlot Current { get; private set; }

        [SerializeField] public bool acceptsQuest = true;

        private BoxCollider2D _col;
        public Bounds RoomBounds => _col.bounds;

        private void Awake()
        {
            _col = GetComponent<BoxCollider2D>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            Current = this;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Current != null) return;
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
