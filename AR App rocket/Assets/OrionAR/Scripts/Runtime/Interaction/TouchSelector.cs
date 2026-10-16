using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OrionAR
{
    /// <summary>
    /// Turns a screen tap into a part selection.
    ///
    /// Three things make touch selection on a phone different from a mouse click, and
    /// all three are handled here:
    ///
    ///   A tap is not a click. A finger moves while it is down. Anything that travels
    ///   more than a few millimetres is a drag meant for the scale controller, not a
    ///   tap, so movement and duration are both checked before a selection fires.
    ///
    ///   Fingers are imprecise. A ray through a single pixel misses small components
    ///   constantly. When the exact ray hits nothing this widens to a sphere cast,
    ///   which is the difference between "this app is broken" and "this app works".
    ///
    ///   UI comes first. A tap landing on a button must never also hit the model
    ///   behind it.
    /// </summary>
    public class TouchSelector : MonoBehaviour
    {
        [Header("References")]
        public Camera rayCamera;
        public ModelScaleController scaleController;

        [Header("Raycast")]
        public LayerMask selectableMask = ~0;
        public float maxDistance = 60f;
        [Tooltip("Forgiveness radius in screen-relative units. Larger is easier to hit but less precise.")]
        public float assistRadiusPixels = 44f;

        [Header("Tap recognition")]
        [Tooltip("Maximum finger travel, as a fraction of screen height, still counted as a tap.")]
        public float tapMoveTolerance = 0.025f;
        public float tapMaxDuration = 0.6f;

        public SelectablePart Current { get; private set; }

        /// <summary>Fires on a confirmed tap. Null means the user tapped empty space.</summary>
        public event System.Action<SelectablePart> Selected;

        public bool InputEnabled { get; set; } = true;

        Vector2 _startPos;
        float _startTime;
        bool _tracking;
        bool _startedOverUI;

        void Awake()
        {
            if (rayCamera == null && Camera.main != null) rayCamera = Camera.main;
            PointerInput.Enable();
        }

        void Update()
        {
            if (!InputEnabled || rayCamera == null) return;

            var pointers = PointerInput.Active;

            // Multi-touch belongs to the scale controller.
            if (pointers.Count >= 2) { _tracking = false; return; }
            if (pointers.Count == 0) return;

            var t = pointers[0];
            switch (t.phase)
            {
                case PointerInput.Phase.Began: BeginTap(t.position, t.id); break;
                case PointerInput.Phase.Ended: EndTap(t.position); break;
                case PointerInput.Phase.Canceled: _tracking = false; break;
            }
        }

        void BeginTap(Vector2 pos, int fingerId)
        {
            _tracking = true;
            _startPos = pos;
            _startTime = Time.time;
            _startedOverUI = IsOverUI(pos, fingerId);
        }

        void EndTap(Vector2 pos)
        {
            if (!_tracking) return;
            _tracking = false;

            if (_startedOverUI) { TapLog("ignored: started over UI (" + _uiHitName + ")"); return; }
            if (scaleController != null && scaleController.GestureActive) { TapLog("ignored: gesture active"); return; }

            if (Time.time - _startTime > tapMaxDuration) { TapLog("ignored: held too long"); return; }

            float travel = Vector2.Distance(pos, _startPos) / Mathf.Max(1f, Screen.height);
            if (travel > tapMoveTolerance) { TapLog("ignored: moved " + travel.ToString("0.000")); return; }

            var part = Pick(pos);
            TapLog("tap at " + pos + " -> " + (part != null ? part.PartId : "nothing"));
            SetCurrent(part);

            if (Selected != null) Selected(part);
        }

        static readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        static string _uiHitName = "";

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        static void TapLog(string msg) { Debug.Log("[TouchSelector] " + msg); }

        static bool IsOverUI(Vector2 position, int fingerId)
        {
            if (EventSystem.current == null) return false;
            // A UI raycast at the exact position is reliable with both the touch and
            // mouse paths of InputSystemUIInputModule, unlike pointer-id lookups which
            // differ between them.
            var data = new PointerEventData(EventSystem.current) { position = position };
            _uiHits.Clear();
            EventSystem.current.RaycastAll(data, _uiHits);
            _uiHitName = _uiHits.Count > 0 ? _uiHits[0].gameObject.name : "";
            return _uiHits.Count > 0;
        }

        /// <summary>
        /// Exact ray first; if it misses, a widening sphere cast picks the part whose
        /// hit point is nearest the tap in screen space.
        /// </summary>
        public SelectablePart Pick(Vector2 screenPos)
        {
            Ray ray = rayCamera.ScreenPointToRay(screenPos);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, maxDistance, selectableMask,
                                QueryTriggerInteraction.Ignore))
            {
                var exact = hit.collider.GetComponentInParent<SelectablePart>();
                if (exact != null) return exact;
                TapLog("ray hit non-part collider: " + hit.collider.name);
            }
            else TapLog("ray hit nothing (camera " + rayCamera.name + ", ray " + ray + ")");

            // Convert the pixel forgiveness radius into world units at roughly the
            // distance of the model, so the assist feels the same at any scale.
            float referenceDistance = hit.collider != null ? hit.distance : 1.5f;
            float worldRadius = ScreenRadiusToWorld(assistRadiusPixels, referenceDistance);

            var hits = Physics.SphereCastAll(ray, worldRadius, maxDistance, selectableMask,
                                             QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return null;

            SelectablePart best = null;
            float bestScore = float.MaxValue;

            foreach (var h in hits)
            {
                var part = h.collider.GetComponentInParent<SelectablePart>();
                if (part == null) continue;

                Vector3 sp = rayCamera.WorldToScreenPoint(part.AnchorPoint);
                if (sp.z <= 0f) continue;

                // Prefer the part whose anchor is closest to the finger, breaking ties
                // toward whichever is nearer the camera.
                float score = Vector2.Distance(new Vector2(sp.x, sp.y), screenPos)
                              + h.distance * 2f;
                if (score < bestScore) { bestScore = score; best = part; }
            }

            return best;
        }

        float ScreenRadiusToWorld(float pixels, float distance)
        {
            if (rayCamera == null) return 0.05f;
            float frustumHeight = 2f * distance * Mathf.Tan(rayCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return frustumHeight * (pixels / Mathf.Max(1f, Screen.height));
        }

        public void SetCurrent(SelectablePart part)
        {
            if (Current == part) return;

            if (Current != null) Current.SetSelected(false);
            Current = part;
            if (Current != null)
            {
                Current.SetSelected(true);
                Current.RaiseTapped();
                Haptics.Confirm();
            }
        }

        public void ClearSelection()
        {
            SetCurrent(null);
        }
    }
}
