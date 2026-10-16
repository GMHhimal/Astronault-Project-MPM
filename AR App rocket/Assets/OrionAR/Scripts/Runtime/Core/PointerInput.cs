using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace OrionAR
{
    /// <summary>
    /// One place that reads fingers and the mouse, through the Input System.
    ///
    /// The project's Active Input Handling is "Input System Package", under which the
    /// old UnityEngine.Input class throws on every call. The previous build read
    /// Input.touchCount directly, so on a phone nothing could be tapped at all and the
    /// tour could never advance. Everything now reads touches from here instead.
    ///
    /// In the Editor the mouse stands in for one finger, and the scroll wheel for a
    /// pinch, so the whole flow is testable without a device.
    /// </summary>
    public static class PointerInput
    {
        public enum Phase { None, Began, Moved, Stationary, Ended, Canceled }

        public struct Pointer
        {
            public int id;          // finger index, or -1 for the mouse
            public Vector2 position;
            public Phase phase;
        }

        static bool _enabled;
        static bool _pendingMouseEnd;
        static Vector2 _pendingMousePos;
        static readonly List<Pointer> _buffer = new List<Pointer>(4);
        static int _bufferFrame = -1;

        /// <summary>Must be called once before reading touches. Idempotent.</summary>
        public static void Enable()
        {
            if (_enabled) return;
            EnhancedTouchSupport.Enable();
            _enabled = true;
        }

        /// <summary>Active fingers this frame (mouse counts as one while a button is held).</summary>
        public static List<Pointer> Active
        {
            get
            {
                if (_bufferFrame == Time.frameCount) return _buffer;
                _bufferFrame = Time.frameCount;
                _buffer.Clear();
                Enable();

                foreach (var t in ETouch.activeTouches)
                {
                    _buffer.Add(new Pointer
                    {
                        id = t.finger.index,
                        position = t.screenPosition,
                        phase = Convert(t.phase),
                    });
                }

                if (_buffer.Count == 0)
                {
                    var mouse = Mouse.current;
                    if (mouse != null)
                    {
                        var b = mouse.leftButton;
                        Vector2 pos = mouse.position.ReadValue();
                        Phase p = Phase.None;

                        if (_pendingMouseEnd)
                        {
                            // A click that went down and up inside one frame: report the
                            // release now, so listeners see Began then Ended.
                            _pendingMouseEnd = false;
                            p = Phase.Ended;
                            pos = _pendingMousePos;
                        }
                        else if (b.wasPressedThisFrame)
                        {
                            p = Phase.Began;
                            if (b.wasReleasedThisFrame || !b.isPressed)
                            {
                                _pendingMouseEnd = true;
                                _pendingMousePos = pos;
                            }
                        }
                        else if (b.wasReleasedThisFrame) p = Phase.Ended;
                        else if (b.isPressed) p = mouse.delta.ReadValue().sqrMagnitude > 0f ? Phase.Moved : Phase.Stationary;

                        if (p != Phase.None)
                            _buffer.Add(new Pointer { id = -1, position = pos, phase = p });
                    }
                }
                return _buffer;
            }
        }

        public static int Count { get { return Active.Count; } }

        /// <summary>Scroll wheel as a zoom factor per frame (1 = no change). Editor and desktop only.</summary>
        public static float ScrollZoomFactor
        {
            get
            {
                var mouse = Mouse.current;
                if (mouse == null) return 1f;
                float y = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(y) < 0.01f) return 1f;
                // Windows reports 120 per notch; normalise so one notch is about 8%.
                float notches = y / 120f;
                if (Mathf.Abs(notches) < 0.05f) notches = Mathf.Sign(y);   // trackpads report small values
                return Mathf.Pow(1.08f, notches);
            }
        }

        static Phase Convert(ETouchPhase p)
        {
            switch (p)
            {
                case ETouchPhase.Began: return Phase.Began;
                case ETouchPhase.Moved: return Phase.Moved;
                case ETouchPhase.Stationary: return Phase.Stationary;
                case ETouchPhase.Ended: return Phase.Ended;
                case ETouchPhase.Canceled: return Phase.Canceled;
                default: return Phase.None;
            }
        }
    }
}
