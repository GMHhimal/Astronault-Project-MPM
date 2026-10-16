using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Scale and rotation of the placed model.
    ///
    /// The experience has to work both as a tabletop miniature and at the real 11 m
    /// height, so scale is a first-class control rather than a fixed constant. Four
    /// named presets cover the useful range, and a continuous pinch gesture sits on
    /// top for anyone who wants something in between.
    ///
    /// Scale 1.0 is true size: the Orion stack with its service module is about 11 m
    /// from nozzle to docking adapter.
    /// </summary>
    public class ModelScaleController : MonoBehaviour
    {
        public enum Preset { Tabletop, Desk, Room, LifeSize }

        [Header("Target")]
        public Transform target;
        public Camera arCamera;

        [Header("Limits")]
        public float minScale = 0.035f;
        public float maxScale = 1.0f;

        [Header("Feel")]
        public float smoothing = 12f;
        public float rotateSensitivity = 0.28f;
        [Tooltip("Pinch below this many pixels of movement is treated as noise.")]
        public float pinchDeadzone = 6f;

        public float CurrentScale { get; private set; } = 0.05f;
        public event System.Action<float> ScaleChanged;

        float _targetScale = 0.05f;
        float _targetYaw;
        float _currentYaw;

        float _lastPinchDistance;
        bool _pinching;
        bool _dragging;
        Vector2 _lastDragPos;

        /// <summary>True while a multi-touch gesture owns the input, so taps are ignored.</summary>
        public bool GestureActive { get { return _pinching || _dragging; } }

        void Awake()
        {
            PointerInput.Enable();
            if (arCamera == null && Camera.main != null) arCamera = Camera.main;
            if (target != null) _currentYaw = target.eulerAngles.y;
            _targetYaw = _currentYaw;
        }

        void Update()
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;

            HandleGestures();

            CurrentScale = Mathf.Lerp(CurrentScale, _targetScale,
                                      1f - Mathf.Exp(-smoothing * Time.deltaTime));
            target.localScale = Vector3.one * CurrentScale;

            _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw,
                                          1f - Mathf.Exp(-smoothing * Time.deltaTime));
            var e = target.eulerAngles;
            target.rotation = Quaternion.Euler(e.x, _currentYaw, e.z);
        }

        void HandleGestures()
        {
            var pointers = PointerInput.Active;
            int touches = pointers.Count;

            // Scroll wheel zooms in the Editor, standing in for a pinch.
            float scroll = PointerInput.ScrollZoomFactor;
            if (!Mathf.Approximately(scroll, 1f) && !PointerOverUI())
                SetScale(_targetScale * scroll, notify: true);

            if (touches >= 2)
            {
                _dragging = false;
                float distance = Vector2.Distance(pointers[0].position, pointers[1].position);

                if (!_pinching)
                {
                    _pinching = true;
                    _lastPinchDistance = distance;
                }
                else if (Mathf.Abs(distance - _lastPinchDistance) > pinchDeadzone)
                {
                    float factor = distance / Mathf.Max(1f, _lastPinchDistance);
                    SetScale(_targetScale * factor, notify: true);
                    _lastPinchDistance = distance;
                }
                return;
            }

            _pinching = false;

            // One finger dragging on empty space spins the model. The touch selector
            // claims taps, so this only sees drags that did not become a tap.
            if (touches == 1)
            {
                var t = pointers[0];
                if (t.phase == PointerInput.Phase.Began)
                {
                    _dragging = false;
                    _dragBlocked = PointerOverUI();
                    _lastDragPos = t.position;
                }
                else if (t.phase == PointerInput.Phase.Moved && !_dragBlocked)
                {
                    float dx = t.position.x - _lastDragPos.x;
                    if (!_dragging && Mathf.Abs(dx) > 12f) _dragging = true;
                    if (_dragging)
                    {
                        _targetYaw -= dx * rotateSensitivity;
                        _lastDragPos = t.position;
                    }
                }
                else if (t.phase == PointerInput.Phase.Ended || t.phase == PointerInput.Phase.Canceled)
                {
                    _dragging = false;
                    _dragBlocked = false;
                }
            }
            else
            {
                _dragging = false;
            }
        }

        bool _dragBlocked;

        /// <summary>Drags that start on a slider or sheet must not also spin the model.</summary>
        static bool PointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var pointers = PointerInput.Active;
            Vector2 pos;
            if (pointers.Count > 0) pos = pointers[0].position;
            else if (UnityEngine.InputSystem.Mouse.current != null)
                pos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            else return false;

            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = pos };
            _uiHits.Clear();
            es.RaycastAll(data, _uiHits);
            return _uiHits.Count > 0;
        }

        static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _uiHits =
            new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();

        // ---------------------------------------------------------------

        public void SetScale(float scale, bool notify = true)
        {
            _targetScale = Mathf.Clamp(scale, minScale, maxScale);
            if (notify && ScaleChanged != null) ScaleChanged(_targetScale);
        }

        /// <summary>Normalised 0..1 position for the UI slider, logarithmic so the
        /// small end of the range gets as much travel as the large end.</summary>
        public float NormalisedScale
        {
            get
            {
                float t = Mathf.Log(_targetScale / minScale) / Mathf.Log(maxScale / minScale);
                return Mathf.Clamp01(t);
            }
        }

        public void SetNormalisedScale(float t)
        {
            t = Mathf.Clamp01(t);
            SetScale(minScale * Mathf.Pow(maxScale / minScale, t), notify: false);
        }

        public void ApplyPreset(Preset preset)
        {
            switch (preset)
            {
                case Preset.Tabletop: SetScale(0.05f); break;     // ~55 cm tall
                case Preset.Desk:     SetScale(0.11f); break;     // ~1.2 m
                case Preset.Room:     SetScale(0.32f); break;     // ~3.5 m
                case Preset.LifeSize: SetScale(1.0f); break;      // ~11 m, true size
            }
            Haptics.Tick();
            AudioDirector.Play("ui_tap");
        }

        public void ResetRotation()
        {
            if (arCamera == null || target == null) return;
            Vector3 toCamera = arCamera.transform.position - target.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
                _targetYaw = Quaternion.LookRotation(toCamera.normalized, Vector3.up).eulerAngles.y;
        }

        /// <summary>Human-readable size, so the user understands what the scale means.</summary>
        public string ScaleLabel(float trueHeightMetres = 11.0f)
        {
            float h = trueHeightMetres * _targetScale;
            if (h < 1f) return Mathf.RoundToInt(h * 100f) + " cm tall";
            return h.ToString("0.0") + " m tall";
        }
    }
}
