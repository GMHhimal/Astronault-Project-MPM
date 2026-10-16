using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace OrionAR
{
    /// <summary>
    /// Finds a surface, shows a reticle on it, and places the spacecraft where the
    /// user taps.
    ///
    /// Placement is the part of an AR app most likely to fail in front of an audience.
    /// An exhibition hall is close to worst case for ARCore: polished or plain floors
    /// with no features to track, moving crowds, and uneven lighting. So this is built
    /// to degrade rather than stall - it coaches the user while scanning, times out to
    /// an explicit fallback rather than hanging, and can place without any plane at all
    /// if detection never succeeds.
    /// </summary>
    public class ARPlacementController : MonoBehaviour
    {
        public enum State { Initialising, Scanning, ReadyToPlace, Placed, Unavailable }

        [Header("AR")]
        public ARSession arSession;
        public ARPlaneManager planeManager;
        public ARRaycastManager raycastManager;
        public ARAnchorManager anchorManager;
        public Camera arCamera;

        [Header("Scene")]
        [Tooltip("Root of everything that gets placed: spacecraft, lunar surface, lighting.")]
        public Transform contentRoot;
        public GameObject placementReticle;

        [Header("Behaviour")]
        [Tooltip("Seconds of scanning before offering to place without a detected plane.")]
        public float scanTimeout = 18f;
        [Tooltip("Distance in front of the camera used when placing without a plane.")]
        public float fallbackDistance = 1.4f;

        public State Current { get; private set; } = State.Initialising;
        public bool HasPlaced { get { return Current == State.Placed; } }

        /// <summary>Fires with true once content is placed and the tour can begin.</summary>
        public event System.Action<State> StateChanged;

        readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();
        float _scanStarted;
        bool _reticleValid;
        Pose _reticlePose;
        ARAnchor _anchor;

        void Awake()
        {
            if (arCamera == null && Camera.main != null) arCamera = Camera.main;
            if (contentRoot != null) contentRoot.gameObject.SetActive(false);
            ShowReticle(false);
        }

        IEnumerator Start()
        {
            SetState(State.Initialising);

            // CheckAvailability must run before the session is enabled, and can take a
            // moment on first launch while Google Play Services for AR updates.
            if ((ARSession.state == ARSessionState.None ||
                 ARSession.state == ARSessionState.CheckingAvailability))
            {
                yield return ARSession.CheckAvailability();
            }

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                yield return ARSession.Install();
            }

            if (ARSession.state == ARSessionState.Unsupported)
            {
                Debug.LogWarning("[AR] This device does not support ARCore. Falling back to orbit view.");
                if (AppDirector.Instance != null) AppDirector.Instance.ARUnavailable = true;
                SetState(State.Unavailable);
                yield break;
            }

            if (arSession != null) arSession.enabled = true;
            if (planeManager != null)
            {
                planeManager.enabled = true;
                planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            }

            _scanStarted = Time.time;
            SetState(State.Scanning);
        }

        void Update()
        {
            if (Current == State.Placed || Current == State.Unavailable) return;
            if (Current == State.Initialising) return;

            UpdateReticle();

            if (Current == State.Scanning)
            {
                bool anyPlane = planeManager != null && planeManager.trackables.count > 0;
                bool timedOut = Time.time - _scanStarted > scanTimeout;

                if (anyPlane || timedOut) SetState(State.ReadyToPlace);
            }
        }

        void UpdateReticle()
        {
            _reticleValid = false;
            if (arCamera == null) return;

            var screenCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);

            if (raycastManager != null &&
                raycastManager.Raycast(screenCentre, _hits, TrackableType.PlaneWithinPolygon))
            {
                _reticlePose = _hits[0].pose;
                _reticleValid = true;
            }
            else if (Current == State.ReadyToPlace)
            {
                // No plane under the crosshair. Offer a point floating in front of the
                // camera so the user is never completely blocked.
                Vector3 forward = arCamera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();

                Vector3 pos = arCamera.transform.position + forward * fallbackDistance
                              + Vector3.down * 0.35f;
                _reticlePose = new Pose(pos, Quaternion.LookRotation(forward, Vector3.up));
                _reticleValid = true;
            }

            ShowReticle(_reticleValid && Current == State.ReadyToPlace);

            if (_reticleValid && placementReticle != null)
            {
                placementReticle.transform.position = _reticlePose.position;
                placementReticle.transform.rotation =
                    Quaternion.Euler(0f, _reticlePose.rotation.eulerAngles.y, 0f);
            }
        }

        void ShowReticle(bool on)
        {
            if (placementReticle != null && placementReticle.activeSelf != on)
                placementReticle.SetActive(on);
        }

        /// <summary>Called by the UI's Place button, or by a tap on the reticle.</summary>
        public bool TryPlace()
        {
            if (Current != State.ReadyToPlace || !_reticleValid || contentRoot == null)
                return false;

            contentRoot.gameObject.SetActive(true);
            contentRoot.position = _reticlePose.position;

            // Face the model toward the user, keeping it upright.
            if (arCamera != null)
            {
                Vector3 toCamera = arCamera.transform.position - _reticlePose.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.0001f)
                    contentRoot.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }

            AttachAnchor();

            ShowReticle(false);
            if (planeManager != null)
            {
                // Stop adding planes and hide the ones already found: visible plane
                // meshes are useful while scanning and visual noise afterwards.
                planeManager.requestedDetectionMode = PlaneDetectionMode.None;
                SetPlanesVisible(false);
            }

            Haptics.Impact();
            AudioDirector.Play("ui_place");
            SetState(State.Placed);
            return true;
        }

        /// <summary>
        /// Anchors the content so ARCore keeps it registered to the real world.
        /// Without an anchor the model drifts as tracking refines its map - the most
        /// common complaint about hand-rolled AR placement.
        /// </summary>
        void AttachAnchor()
        {
            if (anchorManager == null || contentRoot == null) return;

            try
            {
                if (_anchor != null) Destroy(_anchor.gameObject);

                var anchorGO = new GameObject("ContentAnchor");
                anchorGO.transform.SetPositionAndRotation(contentRoot.position, contentRoot.rotation);
                _anchor = anchorGO.AddComponent<ARAnchor>();
                contentRoot.SetParent(_anchor.transform, worldPositionStays: true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AR] Could not create anchor, content may drift: " + e.Message);
            }
        }

        /// <summary>Lets the user pick the spot again.</summary>
        public void Reposition()
        {
            if (contentRoot != null)
            {
                if (_anchor != null) contentRoot.SetParent(null, true);
                contentRoot.gameObject.SetActive(false);
            }
            if (_anchor != null) { Destroy(_anchor.gameObject); _anchor = null; }

            if (planeManager != null)
            {
                planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                SetPlanesVisible(true);
            }

            _scanStarted = Time.time;
            SetState(State.Scanning);
        }

        public void SetPlanesVisible(bool visible)
        {
            if (planeManager == null) return;
            foreach (var plane in planeManager.trackables)
            {
                if (plane == null) continue;
                var r = plane.GetComponentInChildren<Renderer>();
                if (r != null) r.enabled = visible;
                var lr = plane.GetComponentInChildren<LineRenderer>();
                if (lr != null) lr.enabled = visible;
            }
        }

        void SetState(State s)
        {
            if (Current == s) return;
            Current = s;
            if (StateChanged != null) StateChanged(s);
        }

        /// <summary>Guidance text for the current state, shown in the coach bar.</summary>
        public string CoachMessage
        {
            get
            {
                switch (Current)
                {
                    case State.Initialising: return "Starting camera…";
                    case State.Scanning:
                        return "Point your phone at the floor or a table and move slowly";
                    case State.ReadyToPlace:
                        return "Tap to place Orion";
                    case State.Unavailable:
                        return "AR is not available on this device — using orbit view";
                    default: return string.Empty;
                }
            }
        }
    }
}
