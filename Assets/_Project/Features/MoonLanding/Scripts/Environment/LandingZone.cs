using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Target landing site: a holographic ring + beacon beam that fades out as the lander gets close
    /// (so it never hides the dust and the touchdown).
    /// </summary>
    public class LandingZone : MonoBehaviour
    {
        [Tooltip("Radius (m) counted as 'inside the landing zone'.")]
        public float radius = 15f;
        public LineRenderer[] rings;
        public Renderer beam;
        public Transform lander;
        public Color hologramColor = new Color(0.35f, 0.9f, 1f, 1f);
        public float beamFadeStartAltitude = 140f;
        public float beamFadeEndAltitude = 30f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _block;

        const float BuiltRadius = 15f; // ring meshes are generated for this radius

        /// <summary>Resize the landing zone at runtime (difficulty presets). Rings are scaled, beacons moved.</summary>
        public void SetRadius(float newRadius)
        {
            radius = Mathf.Max(3f, newRadius);
            float k = radius / BuiltRadius;
            if (rings != null)
                foreach (LineRenderer lr in rings)
                    if (lr != null) lr.transform.localScale = new Vector3(k, k, 1f);

            foreach (Transform child in transform)
            {
                bool post = child.name.StartsWith("BeaconPost_");
                bool lamp = child.name.StartsWith("BeaconLamp_");
                if (!post && !lamp) continue;
                Vector3 offset = child.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < 0.01f) continue;
                Vector3 p = transform.position + offset.normalized * (radius + 1.5f);
                RaycastHit hit;
                float ground = transform.position.y;
                if (Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out hit, 400f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    ground = hit.point.y;
                p.y = ground + (post ? 0.6f : 1.3f);
                child.position = p;
            }
        }

        public float HorizontalDistance(Vector3 worldPosition)
        {
            Vector3 d = worldPosition - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        public bool Contains(Vector3 worldPosition)
        {
            return HorizontalDistance(worldPosition) <= radius;
        }

        void Update()
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.4f);
            float height = lander != null ? lander.position.y - transform.position.y : 999f;
            float beamAlpha = Mathf.InverseLerp(beamFadeEndAltitude, beamFadeStartAltitude, height);

            if (rings != null)
            {
                Color c = hologramColor * pulse;
                c.a = 1f;
                foreach (LineRenderer lr in rings)
                {
                    if (lr == null) continue;
                    lr.startColor = c;
                    lr.endColor = c;
                }
            }

            if (beam != null)
            {
                if (_block == null) _block = new MaterialPropertyBlock();
                Color bc = hologramColor * (1.5f * pulse);
                bc.a = beamAlpha * 0.6f;
                beam.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, bc);
                beam.SetPropertyBlock(_block);
                beam.enabled = beamAlpha > 0.01f;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            const int seg = 48;
            Vector3 prev = transform.position + new Vector3(radius, 0.2f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * radius, 0.2f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
