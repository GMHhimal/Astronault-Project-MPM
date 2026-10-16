using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Insets a RectTransform to the device's safe area.
    ///
    /// Modern phones have notches, punch-holes, curved corners and a gesture bar.
    /// Anything drawn under those is either invisible or untappable. This re-reads the
    /// safe area whenever the screen changes (rotation, split screen, a system bar
    /// appearing) rather than only at startup, which is the usual bug.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        [Tooltip("Apply the left/right insets. Turn off for full-bleed backgrounds.")]
        public bool applyHorizontal = true;
        [Tooltip("Apply the top/bottom insets.")]
        public bool applyVertical = true;

        [Tooltip("Extra padding inside the safe area, in pixels.")]
        public float extraPadding = 0f;

        RectTransform _rt;
        Rect _lastSafe = new Rect(0, 0, 0, 0);
        ScreenOrientation _lastOrientation;
        Vector2Int _lastResolution;

        void Awake()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _lastSafe ||
                Screen.orientation != _lastOrientation ||
                Screen.width != _lastResolution.x ||
                Screen.height != _lastResolution.y)
            {
                Apply();
            }
        }

        void Apply()
        {
            if (_rt == null) return;

            _lastSafe = Screen.safeArea;
            _lastOrientation = Screen.orientation;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = _lastSafe;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;

            min.x /= Screen.width;  min.y /= Screen.height;
            max.x /= Screen.width;  max.y /= Screen.height;

            if (!applyHorizontal) { min.x = 0f; max.x = 1f; }
            if (!applyVertical)   { min.y = 0f; max.y = 1f; }

            // Guard against nonsense values from some emulators and foldables.
            if (min.x < 0f || min.y < 0f || max.x > 1f || max.y > 1f ||
                max.x - min.x < 0.2f || max.y - min.y < 0.2f)
            {
                min = Vector2.zero;
                max = Vector2.one;
            }

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = new Vector2(extraPadding, extraPadding);
            _rt.offsetMax = new Vector2(-extraPadding, -extraPadding);
        }
    }
}
