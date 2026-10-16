using System.Collections.Generic;
using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// A component of the spacecraft the user can tap.
    ///
    /// Handles its own highlight states. Three are needed and they must be visually
    /// distinct at arm's length on a phone screen in a bright exhibition hall:
    ///
    ///   Idle      no emission
    ///   Hinted    slow cyan pulse - "there is something here"
    ///   Selected  steady warm glow plus a slight scale lift
    ///   Studied   dim steady tint - "you have already read this"
    ///
    /// Emission is written through a MaterialPropertyBlock so every part can glow
    /// independently without duplicating materials, which would multiply draw calls.
    /// </summary>
    [DisallowMultipleComponent]
    public class SelectablePart : MonoBehaviour
    {
        public PartInfo info;

        [Tooltip("Where the callout line points. Defaults to the renderer bounds centre.")]
        public Transform labelAnchor;

        [Header("Highlight colours")]
        public Color hintColor = new Color(0.25f, 0.85f, 0.95f);
        public Color selectColor = new Color(1f, 0.78f, 0.38f);
        public Color studiedColor = new Color(0.16f, 0.55f, 0.42f);

        [Header("Feel")]
        public float pulseSpeed = 1.9f;
        public float selectedLift = 1.012f;
        public float fadeSpeed = 9f;

        public static event System.Action<SelectablePart> Tapped;

        public bool Studied { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsHinted { get; private set; }
        public string PartId { get { return info != null ? info.partId : name; } }

        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        Vector3 _baseScale;
        Color _currentEmission;
        Color _targetEmission;
        Bounds _localBounds;
        bool _boundsValid;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: false);
            _mpb = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            _currentEmission = Color.black;
            _targetEmission = Color.black;

            ComputeLocalBounds();

            if (labelAnchor == null)
            {
                var anchor = new GameObject("LabelAnchor");
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = _boundsValid ? _localBounds.center : Vector3.zero;
                labelAnchor = anchor.transform;
            }

            ApplyEmission(Color.black);
        }

        void ComputeLocalBounds()
        {
            if (_renderers == null || _renderers.Length == 0) return;

            bool any = false;
            Bounds b = new Bounds();
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                // Convert world bounds into this transform's local space so the
                // anchor stays correct when the model is rescaled in AR.
                Bounds wb = r.bounds;
                Vector3 c = transform.InverseTransformPoint(wb.center);
                Vector3 e = transform.InverseTransformVector(wb.extents);
                e = new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z));

                if (!any) { b = new Bounds(c, e * 2f); any = true; }
                else b.Encapsulate(new Bounds(c, e * 2f));
            }
            _localBounds = b;
            _boundsValid = any;
        }

        void Update()
        {
            Color target = _targetEmission;

            if (IsHinted && !IsSelected)
            {
                float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * pulseSpeed));
                target = hintColor * pulse * 0.9f;
            }

            _currentEmission = Color.Lerp(_currentEmission, target,
                                          1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));
            ApplyEmission(_currentEmission);

            Vector3 scaleTarget = IsSelected ? _baseScale * selectedLift : _baseScale;
            transform.localScale = Vector3.Lerp(transform.localScale, scaleTarget,
                                                1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        void ApplyEmission(Color c)
        {
            if (_renderers == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();   // survives a domain reload in Play mode
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        // ---------------------------------------------------------------
        // State
        // ---------------------------------------------------------------

        public void SetHinted(bool on)
        {
            IsHinted = on;
            RefreshTarget();
        }

        public void SetSelected(bool on)
        {
            IsSelected = on;
            RefreshTarget();
        }

        public void MarkStudied()
        {
            Studied = true;
            RefreshTarget();
        }

        public void ResetState()
        {
            Studied = false;
            IsSelected = false;
            IsHinted = false;
            RefreshTarget();
        }

        void RefreshTarget()
        {
            if (IsSelected) _targetEmission = selectColor * 0.85f;
            else if (IsHinted) _targetEmission = hintColor * 0.5f;
            else if (Studied) _targetEmission = studiedColor * 0.22f;
            else _targetEmission = Color.black;
        }

        /// <summary>World-space point the info callout should point at.</summary>
        public Vector3 AnchorPoint
        {
            get { return labelAnchor != null ? labelAnchor.position : transform.position; }
        }

        /// <summary>Approximate world radius, used to frame the camera on this part.</summary>
        public float WorldRadius
        {
            get
            {
                if (!_boundsValid) return 0.25f;
                Vector3 s = transform.lossyScale;
                float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                return _localBounds.extents.magnitude * maxScale;
            }
        }

        public void RaiseTapped()
        {
            if (Tapped != null) Tapped(this);
        }

        // ---------------------------------------------------------------
        // Registry
        // ---------------------------------------------------------------

        static readonly List<SelectablePart> Registry = new List<SelectablePart>();

        void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        void OnDisable()
        {
            Registry.Remove(this);
        }

        /// <summary>Every part currently in the scene, sorted by tour order.</summary>
        public static List<SelectablePart> AllSorted()
        {
            var list = new List<SelectablePart>();
            foreach (var p in Registry)
                if (p != null && p.info != null) list.Add(p);

            list.Sort((a, b) => a.info.order.CompareTo(b.info.order));
            return list;
        }

        public static SelectablePart FindById(string id)
        {
            foreach (var p in Registry)
                if (p != null && p.PartId == id) return p;
            return null;
        }
    }
}
