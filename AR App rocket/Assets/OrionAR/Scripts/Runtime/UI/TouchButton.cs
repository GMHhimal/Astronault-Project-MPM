using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OrionAR
{
    /// <summary>
    /// Touch-first button.
    ///
    /// Unity's stock Button is built around mouse hover, which does not exist on a
    /// phone. This responds to the states that do exist: press, release inside,
    /// release outside. It also scales down slightly on press, which is the feedback
    /// that makes a touch UI feel physical, and fires a haptic tick.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
                               IPointerClickHandler, IPointerExitHandler
    {
        [Header("Parts")]
        public Image background;
        public Image border;
        public TMP_Text label;

        [Header("Colours")]
        public Color normalColor = Color.white;
        public Color pressedColor = Color.gray;
        public Color disabledColor = new Color(0.25f, 0.29f, 0.36f, 1f);

        [Header("Feel")]
        public float pressScale = 0.96f;
        [Tooltip("Fade the label when the button is disabled. Quiz answers turn this off so the marked answer stays readable.")]
        public bool dimLabelWhenDisabled = true;
        public float animSpeed = 14f;
        public bool haptics = true;

        public UnityEvent onClick = new UnityEvent();

        [Tooltip("Fires after the button is held this long. Leave the event empty to disable.")]
        public float longPressSeconds = 1.2f;
        public UnityEvent onLongPress = new UnityEvent();

        bool _pressed;
        float _pressStart;
        bool _longFired;
        bool _interactable = true;
        Vector3 _baseScale;
        RectTransform _rt;

        public bool Interactable
        {
            get { return _interactable; }
            set
            {
                if (_interactable == value) return;
                _interactable = value;
                _pressed = false;
                Apply(instant: true);
            }
        }

        void Awake()
        {
            _rt = (RectTransform)transform;
            _baseScale = _rt.localScale;
            if (background != null) normalColor = background.color;
        }

        void OnEnable()
        {
            _pressed = false;
            Apply(instant: true);
        }

        void Update()
        {
            if (_rt == null) return;
            if (_pressed && !_longFired && onLongPress.GetPersistentEventCount() + _longListeners > 0 &&
                Time.unscaledTime - _pressStart >= longPressSeconds)
            {
                _longFired = true;
                Haptics.Impact();
                onLongPress.Invoke();
            }

            Vector3 target = (_pressed && _interactable) ? _baseScale * pressScale : _baseScale;
            _rt.localScale = Vector3.Lerp(_rt.localScale, target, 1f - Mathf.Exp(-animSpeed * Time.unscaledDeltaTime));
        }

        void Apply(bool instant = false)
        {
            if (background != null)
            {
                Color c = !_interactable ? disabledColor : (_pressed ? pressedColor : normalColor);
                background.color = c;
            }
            if (label != null && dimLabelWhenDisabled)
                label.alpha = _interactable ? 1f : 0.5f;
            if (border != null)
                border.enabled = _interactable;

            if (instant && _rt != null)
                _rt.localScale = _baseScale;
        }

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        int _longListeners;

        /// <summary>Adds a runtime long-press listener (UnityEvent cannot report its runtime count).</summary>
        public void AddLongPress(UnityAction action)
        {
            onLongPress.AddListener(action);
            _longListeners++;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!_interactable) return;
            _pressed = true;
            _pressStart = Time.unscaledTime;
            _longFired = false;
            Apply();
            if (haptics) Haptics.Tick();
        }

        public void OnPointerUp(PointerEventData e)
        {
            _pressed = false;
            Apply();
        }

        public void OnPointerExit(PointerEventData e)
        {
            // Finger slid off the button: cancel the press rather than firing.
            _pressed = false;
            Apply();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!_interactable) return;
            if (_longFired) { _longFired = false; return; }   // the hold already acted
            onClick.Invoke();
        }
    }
}
