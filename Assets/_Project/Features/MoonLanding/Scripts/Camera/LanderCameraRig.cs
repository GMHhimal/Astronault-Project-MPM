using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Astronaut.MoonLanding
{
    public enum LanderCameraMode { Chase, Cockpit, Tower, Showcase }

    /// <summary>
    /// Camera for the Moon Landing Challenge.
    ///  Chase   – third-person orbit (right mouse drag / right stick to orbit, scroll to zoom)
    ///  Cockpit – view from the LM window, looking down at the landing site
    ///  Tower   – cinematic telephoto from the landing zone
    ///  Showcase – slow orbit used on the briefing / result screens
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LanderCameraRig : MonoBehaviour
    {
        public LanderController target;
        public Transform cockpitAnchor;
        public Transform towerAnchor;

        [Header("Chase")]
        public float distance = 32f;
        public float minDistance = 10f;
        public float maxDistance = 160f;
        public float focusHeight = 3f;
        public float defaultPitch = 14f;
        public float orbitSensitivity = 0.15f;
        [Tooltip("Degrees per screen pixel for one-finger touch orbit.")]
        public float touchOrbitSensitivity = 0.18f;
        public float pinchZoomSensitivity = 0.004f;
        public float followSharpness = 8f;
        public float headingFollow = 0.8f;

        [Header("Shake")]
        public float engineRumble = 0.06f;
        public float maxShakeOffset = 0.35f;
        public float maxShakeAngle = 1.6f;

        public LanderCameraMode Mode { get; private set; }

        Camera _camera;
        float _yaw, _pitch, _trauma, _userOrbitTimer, _baseFov;
        float _lookYaw, _lookPitch;
        bool _snap = true;
        readonly Dictionary<int, bool> _touchStartedOnUI = new Dictionary<int, bool>();
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        float _lastPinch = -1f;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _baseFov = _camera.fieldOfView;
            _pitch = defaultPitch;
            if (target != null) _yaw = target.transform.eulerAngles.y;
        }

        public void SetMode(LanderCameraMode mode)
        {
            Mode = mode;
            _lookYaw = _lookPitch = 0f;
            _snap = true;
            if (mode != LanderCameraMode.Tower && _camera != null) _camera.fieldOfView = _baseFov;
        }

        public void CycleMode()
        {
            switch (Mode)
            {
                case LanderCameraMode.Chase: SetMode(cockpitAnchor != null ? LanderCameraMode.Cockpit : LanderCameraMode.Tower); break;
                case LanderCameraMode.Cockpit: SetMode(towerAnchor != null ? LanderCameraMode.Tower : LanderCameraMode.Chase); break;
                default: SetMode(LanderCameraMode.Chase); break;
            }
        }

        /// <summary>Add camera shake (0..1). Explosions ≈ 1, hard impacts ≈ 0.4.</summary>
        public void AddShake(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            ReadOrbitInput(dt);

            Vector3 focus = target.transform.position + Vector3.up * focusHeight;

            switch (Mode)
            {
                case LanderCameraMode.Cockpit:
                    if (cockpitAnchor != null)
                    {
                        Quaternion look = cockpitAnchor.rotation * Quaternion.Euler(_lookPitch, _lookYaw, 0f);
                        transform.SetPositionAndRotation(cockpitAnchor.position, look);
                        break;
                    }
                    goto case LanderCameraMode.Chase;

                case LanderCameraMode.Tower:
                    if (towerAnchor != null)
                    {
                        transform.position = towerAnchor.position;
                        Vector3 toTarget = focus - transform.position;
                        transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                        float dist = toTarget.magnitude;
                        float fov = 2f * Mathf.Atan(14f / Mathf.Max(1f, dist)) * Mathf.Rad2Deg;
                        _camera.fieldOfView = Mathf.Clamp(fov, 4f, _baseFov);
                        break;
                    }
                    goto case LanderCameraMode.Chase;

                case LanderCameraMode.Chase:
                default:
                    UpdateOrbit(focus, dt);
                    break;
            }

            ApplyShake(dt);
            _snap = false;
        }

        void ReadOrbitInput(float dt)
        {
            Vector2 look = Vector2.zero;
            float zoom = 0f;

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed || mouse.middleButton.isPressed) look += mouse.delta.ReadValue() * orbitSensitivity;
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) zoom -= Mathf.Sign(scroll);
            }
            ReadTouch(ref look, ref zoom);

            Gamepad gp = Gamepad.current;
            if (gp != null)
            {
                look += gp.rightStick.ReadValue() * 120f * dt;
                if (gp.dpad.up.isPressed) zoom -= 3f * dt;
                if (gp.dpad.down.isPressed) zoom += 3f * dt;
            }

            if (Mode == LanderCameraMode.Cockpit)
            {
                _lookYaw = Mathf.Clamp(_lookYaw + look.x, -70f, 70f);
                _lookPitch = Mathf.Clamp(_lookPitch - look.y, -30f, 50f);
                return;
            }

            if (look.sqrMagnitude > 0.0001f)
            {
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, -10f, 80f);
                _userOrbitTimer = 3f;
            }
            if (zoom != 0f) distance = Mathf.Clamp(distance * (1f + zoom * 0.12f), minDistance, maxDistance);
        }

        /// <summary>One finger on empty screen = orbit, two fingers = pinch zoom. Touches that start on HUD controls are ignored.</summary>
        void ReadTouch(ref Vector2 look, ref float zoom)
        {
            Touchscreen ts = Touchscreen.current;
            if (ts == null) return;

            int count = 0;
            Vector2 delta = Vector2.zero, p0 = Vector2.zero, p1 = Vector2.zero;
            foreach (TouchControl touch in ts.touches)
            {
                int id = touch.touchId.ReadValue();
                if (!touch.press.isPressed)
                {
                    if (_touchStartedOnUI.ContainsKey(id)) _touchStartedOnUI.Remove(id);
                    continue;
                }
                Vector2 pos = touch.position.ReadValue();
                bool onUI;
                if (!_touchStartedOnUI.TryGetValue(id, out onUI))
                {
                    onUI = IsOverUI(pos);
                    _touchStartedOnUI[id] = onUI;
                }
                if (onUI) continue;

                if (count == 0) { p0 = pos; delta = touch.delta.ReadValue(); }
                else if (count == 1) p1 = pos;
                count++;
            }

            if (count == 1)
            {
                look += delta * touchOrbitSensitivity;
                _lastPinch = -1f;
            }
            else if (count >= 2)
            {
                float d = Vector2.Distance(p0, p1);
                if (_lastPinch > 0f) zoom -= (d - _lastPinch) * pinchZoomSensitivity * 10f;
                _lastPinch = d;
            }
            else _lastPinch = -1f;
        }

        bool IsOverUI(Vector2 screenPos)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es);
            data.position = screenPos;
            _uiHits.Clear();
            es.RaycastAll(data, _uiHits);
            return _uiHits.Count > 0;
        }

        void UpdateOrbit(Vector3 focus, float dt)
        {
            if (Mode == LanderCameraMode.Showcase)
            {
                _yaw += 6f * dt;
                _pitch = Mathf.Lerp(_pitch, 12f, dt * 0.5f);
            }
            else
            {
                _userOrbitTimer -= dt;
                if (_userOrbitTimer <= 0f)
                {
                    // Drift back behind the direction of travel so the player always sees where they are going.
                    Vector3 hv = target.HorizontalVelocity;
                    float desiredYaw = hv.magnitude > 1.5f ? Mathf.Atan2(hv.x, hv.z) * Mathf.Rad2Deg : _yaw;
                    _yaw = Mathf.LerpAngle(_yaw, desiredYaw, 1f - Mathf.Exp(-headingFollow * dt));
                }
            }

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = rot * Vector3.back;
            Vector3 desired = focus + back * distance;

            RaycastHit hit;
            if (Physics.SphereCast(focus, 0.6f, back, out hit, distance, target.groundMask, QueryTriggerInteraction.Ignore))
                desired = focus + back * Mathf.Max(minDistance * 0.5f, hit.distance - 0.5f);
            if (Physics.Raycast(desired + Vector3.up * 200f, Vector3.down, out hit, 400f, target.groundMask, QueryTriggerInteraction.Ignore))
                desired.y = Mathf.Max(desired.y, hit.point.y + 1.5f);

            float k = _snap ? 1f : 1f - Mathf.Exp(-followSharpness * dt);
            transform.position = Vector3.Lerp(transform.position, desired, k);
            transform.rotation = Quaternion.LookRotation((focus - transform.position).normalized, Vector3.up);
        }

        void ApplyShake(float dt)
        {
            float rumble = target.Throttle * engineRumble;
            _trauma = Mathf.MoveTowards(_trauma, 0f, dt * 0.9f);
            float s = Mathf.Clamp01(_trauma * _trauma + rumble);
            if (s <= 0.0001f) return;

            float t = Time.unscaledTime * 22f;
            Vector3 offset = new Vector3(
                Mathf.PerlinNoise(t, 1.1f) - 0.5f,
                Mathf.PerlinNoise(t, 2.3f) - 0.5f,
                Mathf.PerlinNoise(t, 3.7f) - 0.5f) * 2f * maxShakeOffset * s;
            Vector3 angles = new Vector3(
                Mathf.PerlinNoise(t, 4.9f) - 0.5f,
                Mathf.PerlinNoise(t, 5.3f) - 0.5f,
                Mathf.PerlinNoise(t, 6.1f) - 0.5f) * 2f * maxShakeAngle * s;

            transform.position += transform.rotation * offset;
            transform.rotation *= Quaternion.Euler(angles);
        }
    }
}
