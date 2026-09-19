using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Astronaut.MoonLanding
{
    /// <summary>On-screen analog stick (touch or mouse). Value: x = right, y = up, each -1..1.</summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public Image knobImage;
        public Image ringImage;
        public float deadZone = 0.12f;
        public Color idleColor = new Color(0.4f, 0.85f, 1f, 0.55f);
        public Color activeColor = new Color(0.55f, 0.95f, 1f, 1f);

        public Vector2 Value { get; private set; }
        public bool IsHeld { get; private set; }

        RectTransform _rect;

        void Awake() { _rect = (RectTransform)transform; }

        public void OnPointerDown(PointerEventData e) { IsHeld = true; OnDrag(e); }

        public void OnDrag(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, e.position, e.pressEventCamera, out local)) return;
            float radius = _rect.rect.width * 0.5f;
            Vector2 v = local / Mathf.Max(1f, radius);
            if (v.magnitude > 1f) v = v.normalized;
            if (knob != null) knob.anchoredPosition = v * radius * 0.62f;
            float m = v.magnitude;
            Value = m < deadZone ? Vector2.zero : v.normalized * Mathf.InverseLerp(deadZone, 1f, m);
        }

        public void OnPointerUp(PointerEventData e) { Release(); }

        void OnDisable() { Release(); }

        void Release()
        {
            IsHeld = false;
            Value = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
        }

        void Update()
        {
            if (knobImage != null) knobImage.color = IsHeld ? activeColor : idleColor;
        }
    }
}
