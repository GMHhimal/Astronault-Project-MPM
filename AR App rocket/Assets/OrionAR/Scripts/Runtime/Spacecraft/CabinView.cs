using System.Collections.Generic;
using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Peels the crew module's outer hull away so the cabin can be seen and tapped.
    ///
    /// Nine of the seventeen systems are inside the pressure vessel. With the hull in
    /// place a finger can never reach them - the back shell's collider takes every tap -
    /// which is why the earlier build could not get past the exterior parts of the tour.
    ///
    /// The inner pressure-vessel wall is lathed with inward-facing normals, so once the
    /// hull is hidden the near wall is back-face culled and the far wall stays visible
    /// behind the seats. That gives a natural cutaway without a custom clipping shader,
    /// which keeps it cheap on a phone GPU.
    /// </summary>
    public class CabinView : MonoBehaviour
    {
        [Tooltip("Exterior crew-module pieces hidden while the cabin is open.")]
        public List<GameObject> hullPieces = new List<GameObject>();

        [Tooltip("Optional soft fill light switched on with the cabin view.")]
        public Light cabinFill;

        public bool IsOpen { get; private set; }
        public event System.Action<bool> Changed;

        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly List<Collider> _colliders = new List<Collider>();
        bool _cached;

        void Cache()
        {
            if (_cached) return;
            _cached = true;
            foreach (var go in hullPieces)
            {
                if (go == null) continue;
                _renderers.AddRange(go.GetComponentsInChildren<Renderer>(true));
                _colliders.AddRange(go.GetComponentsInChildren<Collider>(true));
            }
        }

        void Awake()
        {
            Cache();
            Apply(false, silent: true);
        }

        public void Toggle() { Set(!IsOpen); }

        public void Set(bool open)
        {
            if (open == IsOpen) return;
            Apply(open, silent: false);
        }

        void Apply(bool open, bool silent)
        {
            Cache();
            IsOpen = open;

            foreach (var r in _renderers) if (r != null) r.enabled = !open;
            foreach (var c in _colliders) if (c != null) c.enabled = !open;
            if (cabinFill != null) cabinFill.enabled = open;

            if (silent) return;
            AudioDirector.Play(open ? "ui_open" : "ui_close");
            Haptics.Tick();
            if (Changed != null) Changed(open);
        }
    }
}
