using System;
using UnityEngine;

namespace Astronaut.MoonLanding
{
    public enum SasMode { Off, RateDamp, AutoLevel }

    /// <summary>
    /// Physics-based Apollo Lunar Module flight model.
    ///  • Main engine: F = throttle × maxThrust along the lander's up axis.
    ///  • Propellant flow: ṁ = F / (Isp · g0)  → the Rigidbody gets lighter as fuel burns (Tsiolkovsky behaviour).
    ///  • RCS: angular acceleration about pitch / yaw / roll, optional SAS (rate damping or auto-level).
    ///  • Gravity comes from Physics.gravity, set to 1.62 m/s² by LunarEnvironment.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(LanderInput))]
    [DefaultExecutionOrder(-50)]
    public class LanderController : MonoBehaviour
    {
        const float StandardGravity = 9.80665f;

        [Header("Mass & Propulsion (Apollo LM inspired)")]
        [Tooltip("Lander mass without descent propellant (kg).")]
        public float dryMass = 6800f;
        [Tooltip("Descent propellant capacity (kg).")]
        public float fuelCapacity = 850f;
        [Tooltip("Max engine thrust (N). Real Descent Propulsion System ≈ 45 kN; reduced a little for playability.")]
        public float maxThrust = 30000f;
        [Tooltip("Specific impulse (s). Real DPS ≈ 311 s.")]
        public float specificImpulse = 311f;
        [Tooltip("How fast the engine follows the throttle command (throttle units / s).")]
        public float engineSpoolRate = 2.5f;

        [Header("Attitude control (RCS)")]
        [Tooltip("Max angular acceleration in deg/s² for pitch (x), yaw (y), roll (z).")]
        public Vector3 rcsAngularAccelerationDeg = new Vector3(38f, 30f, 38f);
        public SasMode sasMode = SasMode.RateDamp;
        [Tooltip("Rate-damping strength (1/s).")]
        public float sasRateGain = 2.8f;
        [Tooltip("Auto-level spring strength.")]
        public float autoLevelStiffness = 1.6f;
        public float autoLevelDamping = 4f;

        [Header("References")]
        public Transform engineNozzle;
        public Vector3 centerOfMassLocal = new Vector3(0f, 2.3f, 0f);
        [Tooltip("Layers treated as ground for radar altitude (lander colliders live on 'Ignore Raycast').")]
        public LayerMask groundMask = ~(1 << 2);

        public Rigidbody Body { get; private set; }
        public LanderInput Controls { get; private set; }

        public bool ControlsEnabled { get; set; }
        public bool IsDestroyed { get; private set; }

        public float Throttle { get; private set; }          // actual 0..1
        public float ThrottleSetting { get; private set; }   // commanded 0..1
        public float Fuel { get; private set; }
        public float FuelFraction { get { return fuelCapacity > 0f ? Fuel / fuelCapacity : 0f; } }
        public float TotalMass { get { return dryMass + Fuel; } }
        public float ThrustNewtons { get { return Throttle * maxThrust; } }
        public bool EngineOn { get { return Throttle > 0.02f; } }
        /// <summary>Throttle needed to exactly cancel gravity (hover) at the current mass.</summary>
        public float HoverThrottle { get { return Mathf.Clamp01(TotalMass * Mathf.Abs(Physics.gravity.y) / Mathf.Max(1f, maxThrust)); } }
        /// <summary>Thrust-to-weight ratio at full throttle under the current gravity.</summary>
        public float MaxTwr { get { return maxThrust / Mathf.Max(1f, TotalMass * Mathf.Abs(Physics.gravity.y)); } }

        /// <summary>RCS command actually applied this step (local torque axes, -1..1).</summary>
        public Vector3 RcsCommand { get; private set; }

        // Telemetry
        public float Altitude { get; private set; }
        public float VerticalSpeed { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public Vector3 HorizontalVelocity { get; private set; }
        public float TiltDegrees { get; private set; }
        public Vector3 GroundPoint { get; private set; }
        public Vector3 GroundNormal { get; private set; }
        /// <summary>Estimated seconds of hover time left at the current mass.</summary>
        public float HoverSecondsLeft { get; private set; }

        public event Action EngineIgnited;
        public event Action EngineCutoff;
        public event Action FuelDepleted;
        public event Action SasModeChanged;

        bool _fuelEmptyRaised;
        bool _analogWasActive;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Controls = GetComponent<LanderInput>();
            GroundNormal = Vector3.up;
            ResetState(fuelCapacity, 0f);
        }

        /// <summary>Refuel and reset engine state (used when a mission starts or restarts).</summary>
        public void ResetState(float fuel, float startThrottle)
        {
            fuelCapacity = Mathf.Max(fuelCapacity, fuel);
            Fuel = Mathf.Clamp(fuel, 0f, fuelCapacity);
            Throttle = 0f;
            ThrottleSetting = Mathf.Clamp01(startThrottle);
            IsDestroyed = false;
            _fuelEmptyRaised = false;
            RcsCommand = Vector3.zero;

            Body.mass = TotalMass;
            Body.centerOfMass = centerOfMassLocal;
            Body.maxAngularVelocity = 6f;
        }

        /// <summary>Called by LanderDestruction: engine dies, controls stop working.</summary>
        public void MarkDestroyed()
        {
            IsDestroyed = true;
            ControlsEnabled = false;
            ThrottleSetting = 0f;
        }

        public void CutEngine()
        {
            ThrottleSetting = 0f;
        }

        void Update()
        {
            if (!ControlsEnabled || IsDestroyed) return;

            if (Controls.HasLeverThrottle)
            {
                ThrottleSetting = Controls.LeverThrottle;
            }
            else if (Controls.HasAnalogThrottle)
            {
                ThrottleSetting = Controls.AnalogThrottle;
                _analogWasActive = true;
            }
            else
            {
                if (_analogWasActive) { ThrottleSetting = 0f; _analogWasActive = false; }
                ThrottleSetting = Mathf.Clamp01(ThrottleSetting + Controls.ThrottleRate * Controls.throttleKeyRate * Time.deltaTime);
            }

            if (Controls.ConsumeCut()) ThrottleSetting = 0f;
            if (Controls.ConsumeFullThrottle()) ThrottleSetting = 1f;
            if (Controls.ConsumeSasCycle())
            {
                sasMode = (SasMode)(((int)sasMode + 1) % 3);
                if (SasModeChanged != null) SasModeChanged();
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            UpdateTelemetry();

            // ---------------- Main engine ----------------
            float target = 0f;
            if (ControlsEnabled && !IsDestroyed)
            {
                target = Controls.FullThrustHeld ? 1f : ThrottleSetting;
            }
            if (Fuel <= 0f) target = 0f;

            bool wasOn = EngineOn;
            Throttle = Mathf.MoveTowards(Throttle, target, engineSpoolRate * dt * (IsDestroyed ? 4f : 1f));

            float thrust = Throttle * maxThrust;
            if (thrust > 0f && Fuel > 0f)
            {
                float massFlow = thrust / (specificImpulse * StandardGravity); // kg/s
                Fuel = Mathf.Max(0f, Fuel - massFlow * dt);
                Body.AddForce(transform.up * thrust, ForceMode.Force);
            }

            Body.mass = TotalMass;

            if (Fuel <= 0f && !_fuelEmptyRaised)
            {
                _fuelEmptyRaised = true;
                ThrottleSetting = 0f;
                if (FuelDepleted != null) FuelDepleted();
            }

            bool isOn = EngineOn;
            if (isOn && !wasOn && EngineIgnited != null) EngineIgnited();
            if (!isOn && wasOn && EngineCutoff != null) EngineCutoff();

            float weight = TotalMass * Mathf.Abs(Physics.gravity.y);
            float hoverFlow = weight / (specificImpulse * StandardGravity);
            HoverSecondsLeft = hoverFlow > 0f ? Fuel / hoverFlow : 0f;

            // ---------------- RCS / SAS ----------------
            if (IsDestroyed)
            {
                RcsCommand = Vector3.zero;
                return;
            }

            Vector3 command = ControlsEnabled ? Controls.Attitude : Vector3.zero;
            Vector3 localAngularVelocity = transform.InverseTransformDirection(Body.angularVelocity);
            Vector3 maxAccel = rcsAngularAccelerationDeg * Mathf.Deg2Rad;

            // Tilt error towards "straight up", expressed in local axes (used by Auto-Level).
            Vector3 levelError = transform.InverseTransformDirection(Vector3.Cross(transform.up, Vector3.up));

            Vector3 accel = Vector3.zero;
            Vector3 applied = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                float a = 0f;
                if (Mathf.Abs(command[axis]) > 0.05f)
                {
                    a = command[axis] * maxAccel[axis];
                }
                else if (ControlsEnabled && sasMode != SasMode.Off)
                {
                    float desired = -localAngularVelocity[axis] * sasRateGain;
                    if (sasMode == SasMode.AutoLevel && axis != 1)
                    {
                        desired = levelError[axis] * autoLevelStiffness * 4f - localAngularVelocity[axis] * autoLevelDamping;
                    }
                    a = Mathf.Clamp(desired, -maxAccel[axis], maxAccel[axis]);
                    if (Mathf.Abs(a) < maxAccel[axis] * 0.04f) a = 0f; // small deadband, like real thruster minimum impulse
                }
                accel[axis] = a;
                applied[axis] = maxAccel[axis] > 0f ? a / maxAccel[axis] : 0f;
            }

            RcsCommand = applied;
            if (accel != Vector3.zero) Body.AddRelativeTorque(accel, ForceMode.Acceleration);
        }

        void UpdateTelemetry()
        {
            Vector3 v = Body.linearVelocity;
            VerticalSpeed = v.y;
            HorizontalVelocity = new Vector3(v.x, 0f, v.z);
            HorizontalSpeed = HorizontalVelocity.magnitude;
            TiltDegrees = Vector3.Angle(transform.up, Vector3.up);

            const float probeLift = 3f;
            Vector3 origin = transform.position + Vector3.up * probeLift;
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, 50000f, groundMask, QueryTriggerInteraction.Ignore))
            {
                Altitude = Mathf.Max(0f, hit.distance - probeLift);
                GroundPoint = hit.point;
                GroundNormal = hit.normal;
            }
            else
            {
                Altitude = transform.position.y;
                GroundPoint = new Vector3(transform.position.x, 0f, transform.position.z);
                GroundNormal = Vector3.up;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(centerOfMassLocal), 0.25f);
            if (engineNozzle != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.1f);
                Gizmos.DrawLine(engineNozzle.position, engineNozzle.position - transform.up * 4f);
            }
        }
    }
}
