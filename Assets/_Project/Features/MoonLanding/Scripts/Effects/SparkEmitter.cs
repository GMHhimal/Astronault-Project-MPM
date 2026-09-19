using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Electric sparks: bursts at impact points, and random short-circuit crackles
    /// across the lander once it is damaged (hard landing or crash).
    /// </summary>
    public class SparkEmitter : MonoBehaviour
    {
        public ParticleSystem sparkPool;
        public Light sparkLight;
        public LanderAudio landerAudio;
        [Tooltip("Renderers whose bounds are used to pick random spark positions.")]
        public Renderer[] modelRenderers;

        [Header("Damage sparking")]
        public bool damaged;
        [Range(0f, 1f)] public float intensity = 0f;
        public Vector2 intervalRange = new Vector2(0.12f, 1.6f);
        [Tooltip("Sparking slowly calms down (per second). 0 = never.")]
        public float intensityDecay = 0.02f;

        float _timer;
        float _lightLevel;

        public void SetDamage(float amount)
        {
            damaged = amount > 0f;
            intensity = Mathf.Clamp01(Mathf.Max(intensity, amount));
        }

        /// <summary>Emit a burst of sparks at a world position.</summary>
        public void Burst(Vector3 position, Vector3 normal, int count, float speedScale)
        {
            if (sparkPool == null) return;
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
            normal.Normalize();

            var ep = new ParticleSystem.EmitParams();
            ep.applyShapeToPosition = false;
            for (int i = 0; i < count; i++)
            {
                ep.position = position;
                Vector3 dir = (normal + Random.insideUnitSphere * 0.95f).normalized;
                ep.velocity = dir * Random.Range(2.5f, 13f) * speedScale;
                ep.startLifetime = Random.Range(0.25f, 0.95f);
                ep.startSize = Random.Range(0.025f, 0.085f);
                sparkPool.Emit(ep, 1);
            }

            if (sparkLight != null)
            {
                sparkLight.transform.position = position + normal * 0.3f;
                _lightLevel = Mathf.Max(_lightLevel, Mathf.Clamp01(count / 25f));
            }
            if (landerAudio != null) landerAudio.PlaySpark(position, Mathf.Clamp01(count / 30f));
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (sparkLight != null)
            {
                _lightLevel = Mathf.MoveTowards(_lightLevel, 0f, dt * 6f);
                sparkLight.intensity = _lightLevel * 5f * Random.Range(0.6f, 1.2f);
                sparkLight.enabled = _lightLevel > 0.01f;
            }

            if (!damaged || intensity <= 0f || dt <= 0f) return;

            intensity = Mathf.Max(0f, intensity - intensityDecay * dt);
            _timer -= dt;
            if (_timer > 0f) return;

            _timer = Mathf.Lerp(intervalRange.y, intervalRange.x, intensity) * Random.Range(0.5f, 1.5f);
            Vector3 point, normal;
            RandomPointOnModel(out point, out normal);
            Burst(point, normal, Random.Range(6, 10 + Mathf.RoundToInt(28f * intensity)), 0.8f);
        }

        void RandomPointOnModel(out Vector3 point, out Vector3 normal)
        {
            if (modelRenderers == null || modelRenderers.Length == 0)
            {
                point = transform.position + Vector3.up * 2f + Random.insideUnitSphere * 1.5f;
                normal = Random.onUnitSphere;
                return;
            }
            Renderer r = modelRenderers[Random.Range(0, modelRenderers.Length)];
            if (r == null) { point = transform.position + Vector3.up * 2f; normal = Vector3.up; return; }
            Bounds b = r.bounds;
            Vector3 offset = Vector3.Scale(b.extents, Random.insideUnitSphere);
            point = b.center + offset * 0.85f;
            normal = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.up;
        }
    }
}
