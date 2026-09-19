using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Drives the lander's visual effects from the physics state:
    ///  • engine plume + engine glow light follow the real throttle,
    ///  • regolith dust is emitted where the exhaust hits the ground (starts below ~35 m, like Apollo),
    ///  • RCS quads puff in the direction that actually produces the commanded torque.
    /// </summary>
    public class LanderEffects : MonoBehaviour
    {
        public LanderController lander;

        [Header("Engine")]
        public ParticleSystem plume;
        public ParticleSystem plumeCore;
        public Light engineLight;
        public float plumeRate = 260f;
        public float coreRate = 90f;
        public float engineLightIntensity = 6f;

        [Header("Dust")]
        public Transform dustEmitter;
        public ParticleSystem dustSheet;
        public ParticleSystem dustHaze;
        public ParticleSystem dustStreaks;
        [Tooltip("Altitude where exhaust starts lifting regolith.")]
        public float dustStartAltitude = 35f;
        public float sheetRate = 420f;
        public float hazeRate = 22f;
        public float streakRate = 180f;

        [Header("RCS")]
        public ParticleSystem[] rcsQuads;
        public float rcsParticlesPerSecond = 70f;
        public float rcsExhaustSpeed = 9f;

        public float DustIntensity { get; private set; }
        public float RcsActivity { get; private set; }

        float[] _rcsAccumulator;
        float _sheetBaseSpeed = 1f;

        void Awake()
        {
            if (lander == null) lander = GetComponent<LanderController>();
            _rcsAccumulator = new float[rcsQuads != null ? rcsQuads.Length : 0];
        }

        void Start()
        {
            PlayIdle(plume); PlayIdle(plumeCore);
            PlayIdle(dustSheet); PlayIdle(dustHaze); PlayIdle(dustStreaks);
            if (rcsQuads != null) foreach (var q in rcsQuads) PlayIdle(q);
            if (engineLight != null) engineLight.intensity = 0f;
        }

        static void PlayIdle(ParticleSystem ps)
        {
            if (ps == null) return;
            MoonVFXFactory.SetRate(ps, 0f);
            ps.Play(true);
        }

        void Update()
        {
            if (lander == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float throttle = lander.Throttle;

            // ---- plume & light
            float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 17f, 0.37f);
            MoonVFXFactory.SetRate(plume, plumeRate * throttle);
            MoonVFXFactory.SetRate(plumeCore, coreRate * throttle);
            if (engineLight != null) engineLight.intensity = engineLightIntensity * throttle * flicker;

            // ---- dust where the exhaust hits the ground
            float targetDust = 0f;
            Transform nozzle = lander.engineNozzle != null ? lander.engineNozzle : transform;
            Vector3 exhaustDir = -transform.up;
            RaycastHit hit;
            if (throttle > 0.02f && Physics.Raycast(nozzle.position, exhaustDir, out hit, dustStartAltitude * 1.6f, lander.groundMask, QueryTriggerInteraction.Ignore))
            {
                float k = Mathf.Clamp01(1f - hit.distance / dustStartAltitude);
                float facing = Mathf.Clamp01(Vector3.Dot(-exhaustDir, hit.normal) * 1.3f);
                targetDust = throttle * Mathf.Pow(k, 1.2f) * facing;
                if (dustEmitter != null)
                {
                    dustEmitter.position = hit.point + hit.normal * 0.25f;
                    dustEmitter.rotation = Quaternion.FromToRotation(Vector3.forward, hit.normal);
                }
            }
            DustIntensity = Mathf.MoveTowards(DustIntensity, targetDust, dt * 2.5f);

            MoonVFXFactory.SetRate(dustSheet, sheetRate * DustIntensity);
            MoonVFXFactory.SetRate(dustHaze, hazeRate * DustIntensity);
            MoonVFXFactory.SetRate(dustStreaks, streakRate * DustIntensity);
            if (dustSheet != null)
            {
                var main = dustSheet.main;
                main.startSpeedMultiplier = _sheetBaseSpeed * (0.45f + DustIntensity);
            }

            // ---- RCS puffs
            UpdateRcs(dt);
        }

        void UpdateRcs(float dt)
        {
            Vector3 command = lander.RcsCommand;
            RcsActivity = Mathf.Clamp01(command.magnitude);
            if (rcsQuads == null || rcsQuads.Length == 0 || lander.Body == null) return;

            Vector3 torqueWorld = transform.TransformDirection(command);
            Vector3 com = lander.Body.worldCenterOfMass;
            Vector3 craftVelocity = lander.Body.linearVelocity;

            for (int i = 0; i < rcsQuads.Length; i++)
            {
                ParticleSystem quad = rcsQuads[i];
                if (quad == null) continue;
                if (RcsActivity < 0.08f) { _rcsAccumulator[i] = 0f; continue; }

                Vector3 r = quad.transform.position - com;
                // Force at r that produces torque τ:  F ∝ τ × r   (because r × (τ × r) is parallel to τ)
                Vector3 force = Vector3.Cross(torqueWorld, r);
                float strength = force.magnitude / Mathf.Max(0.001f, torqueWorld.magnitude * r.magnitude);
                if (strength < 0.25f) continue;

                _rcsAccumulator[i] += rcsParticlesPerSecond * strength * RcsActivity * dt;
                int count = Mathf.FloorToInt(_rcsAccumulator[i]);
                if (count <= 0) continue;
                _rcsAccumulator[i] -= count;

                Vector3 exhaust = -force.normalized; // exhaust leaves opposite to the force on the craft
                var ep = new ParticleSystem.EmitParams();
                ep.applyShapeToPosition = false;
                for (int n = 0; n < count; n++)
                {
                    ep.position = quad.transform.position;
                    ep.velocity = craftVelocity + (exhaust + Random.insideUnitSphere * 0.25f).normalized * rcsExhaustSpeed * Random.Range(0.7f, 1.3f);
                    quad.Emit(ep, 1);
                }
            }
        }
    }
}
