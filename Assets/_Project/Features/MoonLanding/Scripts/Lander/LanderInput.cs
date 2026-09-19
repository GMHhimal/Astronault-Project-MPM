using UnityEngine;
using UnityEngine.InputSystem;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Reads Keyboard, Gamepad and the on-screen HUD buttons (new Input System only,
    /// because the project has "Active Input Handling = Input System Package").
    ///
    /// Keyboard:  W/S pitch · A/D roll · Q/E yaw · Shift/Ctrl throttle · Space full thrust (hold)
    ///            Z full throttle · X cut engine · F SAS mode · C camera · H hide buttons · Esc pause · R restart
    /// Gamepad:   Left stick pitch/roll · LB/RB yaw · RT throttle (analog) · LT throttle down
    ///            X cut · Y SAS · B camera · A confirm · Start pause · Select restart
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LanderInput : MonoBehaviour
    {
        [Tooltip("How fast Shift/Ctrl change the throttle (per second).")]
        public float throttleKeyRate = 0.55f;

        [Tooltip("How fast attitude commands ramp in (keyboard smoothing).")]
        public float attitudeRamp = 7f;

        // --- Written by the on-screen HUD buttons ---------------------------------
        [System.NonSerialized] public bool uiThrust;
        [System.NonSerialized] public bool uiThrottleUp;
        [System.NonSerialized] public bool uiThrottleDown;
        [System.NonSerialized] public bool uiPitchForward;
        [System.NonSerialized] public bool uiPitchBack;
        [System.NonSerialized] public bool uiRollLeft;
        [System.NonSerialized] public bool uiRollRight;
        [System.NonSerialized] public bool uiYawLeft;
        [System.NonSerialized] public bool uiYawRight;
        /// <summary>Virtual joystick (x = roll right, y = pitch forward), -1..1.</summary>
        [System.NonSerialized] public Vector2 uiStick;
        /// <summary>Throttle lever value 0..1 while the player is dragging it, otherwise -1.</summary>
        [System.NonSerialized] public float uiThrottleLever = -1f;

        /// <summary>Torque command in lander local axes: x = pitch, y = yaw, z = roll (each -1..1).</summary>
        public Vector3 Attitude { get; private set; }

        /// <summary>-1..1 throttle change request (keyboard / buttons).</summary>
        public float ThrottleRate { get; private set; }

        /// <summary>Space / THRUST button held: engine forced to 100%.</summary>
        public bool FullThrustHeld { get; private set; }

        /// <summary>True while a gamepad trigger directly drives the throttle.</summary>
        public bool HasAnalogThrottle { get; private set; }
        public float AnalogThrottle { get; private set; }

        /// <summary>True while the on-screen throttle lever is being dragged.</summary>
        public bool HasLeverThrottle { get { return uiThrottleLever >= 0f; } }
        public float LeverThrottle { get { return Mathf.Clamp01(uiThrottleLever); } }

        bool _cut, _full, _sas, _camera, _pause, _confirm, _restart, _toggleButtons;

        public bool ConsumeCut() { bool v = _cut; _cut = false; return v; }
        public bool ConsumeFullThrottle() { bool v = _full; _full = false; return v; }
        public bool ConsumeSasCycle() { bool v = _sas; _sas = false; return v; }
        public bool ConsumeCamera() { bool v = _camera; _camera = false; return v; }
        public bool ConsumePause() { bool v = _pause; _pause = false; return v; }
        public bool ConsumeConfirm() { bool v = _confirm; _confirm = false; return v; }
        public bool ConsumeRestart() { bool v = _restart; _restart = false; return v; }
        public bool ConsumeToggleButtons() { bool v = _toggleButtons; _toggleButtons = false; return v; }

        // Buttons can also trigger the one-shot actions
        // UI buttons fire during the EventSystem update, so they are queued and delivered next frame.
        bool _pendingCut, _pendingSas, _pendingCamera, _pendingPause, _pendingFull;
        public void PressCut() { _pendingCut = true; }
        public void PressFullThrottle() { _pendingFull = true; }
        public void PressSas() { _pendingSas = true; }
        public void PressCamera() { _pendingCamera = true; }
        public void PressPause() { _pendingPause = true; }

        void Update()
        {
            if (_pendingCut) { _cut = true; _pendingCut = false; }
            if (_pendingFull) { _full = true; _pendingFull = false; }
            if (_pendingSas) { _sas = true; _pendingSas = false; }
            if (_pendingCamera) { _camera = true; _pendingCamera = false; }
            if (_pendingPause) { _pause = true; _pendingPause = false; }

            float pitch = 0f, yaw = 0f, rollRight = 0f, rate = 0f;
            bool full = uiThrust;
            bool analog = false;
            float analogValue = 0f;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) pitch += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) pitch -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rollRight += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rollRight -= 1f;
                if (kb.eKey.isPressed) yaw += 1f;
                if (kb.qKey.isPressed) yaw -= 1f;

                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) rate += 1f;
                if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) rate -= 1f;
                if (kb.spaceKey.isPressed) full = true;

                if (kb.xKey.wasPressedThisFrame) _cut = true;
                if (kb.zKey.wasPressedThisFrame) _full = true;
                if (kb.fKey.wasPressedThisFrame) _sas = true;
                if (kb.cKey.wasPressedThisFrame) _camera = true;
                if (kb.hKey.wasPressedThisFrame) _toggleButtons = true;
                if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) _pause = true;
                if (kb.rKey.wasPressedThisFrame) _restart = true;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                    _confirm = true;
            }

            Gamepad gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                pitch += stick.y;
                rollRight += stick.x;
                if (gp.rightShoulder.isPressed) yaw += 1f;
                if (gp.leftShoulder.isPressed) yaw -= 1f;

                float rt = gp.rightTrigger.ReadValue();
                if (rt > 0.05f) { analog = true; analogValue = rt; }
                rate -= gp.leftTrigger.ReadValue();

                if (gp.buttonWest.wasPressedThisFrame) _cut = true;
                if (gp.buttonNorth.wasPressedThisFrame) _sas = true;
                if (gp.buttonEast.wasPressedThisFrame) _camera = true;
                if (gp.startButton.wasPressedThisFrame) _pause = true;
                if (gp.selectButton.wasPressedThisFrame) _restart = true;
                if (gp.buttonSouth.wasPressedThisFrame) _confirm = true;
            }

            pitch += uiStick.y;
            rollRight += uiStick.x;
            if (uiPitchForward) pitch += 1f;
            if (uiPitchBack) pitch -= 1f;
            if (uiRollRight) rollRight += 1f;
            if (uiRollLeft) rollRight -= 1f;
            if (uiYawRight) yaw += 1f;
            if (uiYawLeft) yaw -= 1f;
            if (uiThrottleUp) rate += 1f;
            if (uiThrottleDown) rate -= 1f;

            // Unity axes: +X torque tips the top forward, -Z torque tips the top to the right, +Y turns right.
            Vector3 target = new Vector3(Mathf.Clamp(pitch, -1f, 1f), Mathf.Clamp(yaw, -1f, 1f), -Mathf.Clamp(rollRight, -1f, 1f));
            Attitude = Vector3.MoveTowards(Attitude, target, attitudeRamp * Time.unscaledDeltaTime);
            if (target == Vector3.zero && Attitude.sqrMagnitude < 0.0004f) Attitude = Vector3.zero;

            ThrottleRate = Mathf.Clamp(rate, -1f, 1f);
            FullThrustHeld = full;
            HasAnalogThrottle = analog;
            AnalogThrottle = analogValue;
        }

        void LateUpdate()
        {
            // One-shot presses live for exactly one frame.
            _cut = _full = _sas = _camera = _pause = _confirm = _restart = _toggleButtons = false;
        }

        void OnDisable()
        {
            Attitude = Vector3.zero;
            FullThrustHeld = false;
            HasAnalogThrottle = false;
        }
    }
}
