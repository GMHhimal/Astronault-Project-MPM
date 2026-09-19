using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Astronaut.MoonLanding
{
    /// <summary>Vertical throttle lever. While dragged it writes Value (0..1); otherwise it shows DisplayValue.</summary>
    public class ThrottleLever : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform track;
        public RectTransform handle;
        public RectTransform fill;
        public RectTransform hoverMarker;

        public bool IsDragging { get; private set; }
        public float Value { get; private set; }

        public void OnPointerDown(PointerEventData e) { IsDragging = true; OnDrag(e); }

        public void OnDrag(PointerEventData e)
        {
            if (track == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out local)) return;
            Rect r = track.rect;
            Value = Mathf.Clamp01((local.y - r.yMin) / Mathf.Max(1f, r.height));
            // snap to 0 and 100 near the ends
            if (Value < 0.03f) Value = 0f;
            if (Value > 0.97f) Value = 1f;
        }

        public void OnPointerUp(PointerEventData e) { IsDragging = false; }
        void OnDisable() { IsDragging = false; }

        /// <summary>Update the visuals (actual throttle, commanded throttle and hover point).</summary>
        public void Show(float setting, float actual, float hover)
        {
            if (track == null) return;
            float h = track.rect.height;
            if (handle != null) handle.anchoredPosition = new Vector2(0f, Mathf.Clamp01(IsDragging ? Value : setting) * h);
            if (fill != null) fill.anchorMax = new Vector2(1f, Mathf.Clamp01(actual));
            if (hoverMarker != null) hoverMarker.anchoredPosition = new Vector2(0f, Mathf.Clamp01(hover) * h);
        }
    }
}
