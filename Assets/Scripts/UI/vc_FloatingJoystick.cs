using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace SunodGame.UI
{
    /// <summary>
    /// Stardew-style joystick with a forgiving touch area. The stick never moves from its
    /// spot; a large invisible zone around it accepts touches, and the direction is taken
    /// from the stick's center to the finger - so a thumb landing off the stick (or sliding
    /// past its rim) still steers. Output is analog (partial tilt = slower walk) and feeds
    /// the same &lt;Gamepad&gt;/leftStick binding PlayerController already reads.
    /// Attach to the zone object (needs a raycast-target Graphic, alpha can be 0).
    /// </summary>
    public class vc_FloatingJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [InputControl(layout = "Vector2")]
        [SerializeField] private string _controlPath = "<Gamepad>/leftStick";

        [SerializeField] private RectTransform stickBase;
        [SerializeField] private RectTransform knob;

        [Tooltip("Knob travel from the base center, in canvas units. Fingers beyond it = full tilt.")]
        [SerializeField] private float radius = 16f;
        [Tooltip("Ignore tiny tilts below this fraction of full tilt.")]
        [Range(0f, 0.5f)] [SerializeField] private float deadZone = 0.12f;
        [SerializeField] private float idleAlpha = 0.55f;

        private RectTransform _area;      // stickBase's parent (coordinate space)
        private Vector2 _home;            // fixed stick center (anchoredPosition)
        private int _pointerId = int.MinValue;
        private CanvasGroup _baseGroup;

        protected override string controlPathInternal
        {
            get => _controlPath;
            set => _controlPath = value;
        }

        private bool Active => _pointerId != int.MinValue;

        private void Awake()
        {
            if (stickBase == null) return;
            _area = stickBase.parent as RectTransform;
            _home = stickBase.anchoredPosition;

            _baseGroup = stickBase.GetComponent<CanvasGroup>();
            if (_baseGroup == null) _baseGroup = stickBase.gameObject.AddComponent<CanvasGroup>();
            _baseGroup.blocksRaycasts = false;   // the zone owns all touches
            _baseGroup.alpha = idleAlpha;
        }

        protected override void OnDisable()
        {
            Release();
            base.OnDisable();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Active || stickBase == null) return;
            _pointerId = eventData.pointerId;
            _baseGroup.alpha = 1f;
            UpdateStick(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            UpdateStick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Release();
        }

        private void UpdateStick(PointerEventData eventData)
        {
            if (!TryLocal(eventData, out Vector2 local)) return;

            // Direction from the fixed stick center to the finger; beyond the rim = full tilt.
            Vector2 offset = local - _home;
            if (offset.magnitude > radius)
                offset = offset.normalized * radius;

            if (knob != null) knob.anchoredPosition = offset;

            Vector2 tilt = offset / Mathf.Max(0.01f, radius);
            float mag = tilt.magnitude;
            Vector2 value = mag <= deadZone
                ? Vector2.zero
                : tilt.normalized * Mathf.InverseLerp(deadZone, 1f, mag);

            SendValueToControl(value);
        }

        private void Release()
        {
            _pointerId = int.MinValue;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (_baseGroup != null) _baseGroup.alpha = idleAlpha;
            SendValueToControl(Vector2.zero);
        }

        private bool TryLocal(PointerEventData eventData, out Vector2 local)
        {
            local = Vector2.zero;
            if (_area == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _area, eventData.position, eventData.pressEventCamera, out Vector2 inRect))
                return false;

            // Convert rect-local (pivot-relative) to anchoredPosition space of stickBase.
            Vector2 anchorRef = Vector2.Scale(_area.rect.size, stickBase.anchorMin - _area.pivot);
            local = inRect - anchorRef;
            return true;
        }
    }
}
