using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Astronaut.MoonLanding
{
    /// <summary>On-screen button that reports "held" state (mouse or multi-touch), for THRUST / yaw controls.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Image background;
        public Image glow;
        public Color normalColor = new Color(0.05f, 0.12f, 0.18f, 0.75f);
        public Color heldColor = new Color(0.25f, 0.75f, 1f, 0.9f);

        public bool IsHeld { get; private set; }
        public Action<bool> onHeldChanged;

        public void OnPointerDown(PointerEventData eventData) { SetHeld(true); }
        public void OnPointerUp(PointerEventData eventData) { SetHeld(false); }
        public void OnPointerExit(PointerEventData eventData) { SetHeld(false); }

        void OnDisable() { SetHeld(false); }

        void SetHeld(bool held)
        {
            if (IsHeld == held) return;
            IsHeld = held;
            if (background != null)
            {
                background.color = held ? heldColor : normalColor;
                background.rectTransform.localScale = Vector3.one * (held ? 0.94f : 1f);
            }
            if (onHeldChanged != null) onHeldChanged(held);
        }
    }
}
