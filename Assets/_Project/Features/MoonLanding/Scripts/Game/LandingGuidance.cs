using UnityEngine;

namespace Astronaut.MoonLanding
{
    public enum GuidanceLevel { None, Hints, Full }
    public enum GuidanceSeverity { Info, Good, Caution, Urgent }

    /// <summary>What the flight computer recommends right now (read by the HUD).</summary>
    public struct GuidanceAdvice
    {
        public string primary;
        public string secondary;
        public GuidanceSeverity severity;
        public bool highlightThrust;      // press / hold THRUST, or raise the lever
        public bool highlightRelease;     // release THRUST / lower the lever
        public bool highlightCut;         // press CUT
        public Vector2 stickDirection;    // suggested joystick direction (x = roll right, y = pitch forward), zero = none
        public float targetSinkRate;      // m/s, for the HUD sink-rate gauge
        public bool HasAdvice { get { return !string.IsNullOrEmpty(primary); } }
    }

    /// <summary>
    /// A simple "flight computer" that watches the lander and tells a beginner what to do,
    /// e.g. "RELEASE THE THRUST NOW", "HOLD THRUST", "TILT BACK TO STOP DRIFTING", "CUT THE ENGINE".
    ///
    /// Vertical guidance follows a safe descent profile: target sink rate ≈ 0.5 m/s + 9 % of altitude (max 10 m/s),
    /// so the lander slows down smoothly and touches down at about 1 m/s even with a slow reaction time.
    /// Horizontal guidance aims for a gentle velocity towards the landing zone that falls to zero near the ground.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class LandingGuidance : MonoBehaviour
    {
        public LanderController lander;
        public LandingEvaluator evaluator;
        public LandingZone landingZone;
        public GuidanceLevel level = GuidanceLevel.Full;

        [Header("Descent profile")]
        public float touchdownSinkRate = 0.5f;
        public float sinkPerMetre = 0.09f;
        public float maxTargetSinkRate = 10f;
        public float tolerance = 1.1f;
        [Tooltip("Tighter tolerance below 15 m so beginners touch down softly (tested in simulation).")]
        public float nearGroundTolerance = 0.5f;

        [Header("Horizontal")]
        public float approachGain = 0.05f;
        public float maxApproachSpeed = 9f;
        public float driftTolerance = 1.4f;

        public GuidanceAdvice Current { get; private set; }

        string _lastPrimary;
        float _holdTimer;

        public float TargetSinkRate(float altitude)
        {
            return Mathf.Clamp(touchdownSinkRate + altitude * sinkPerMetre, touchdownSinkRate, maxTargetSinkRate);
        }

        void Update()
        {
            if (lander == null || level == GuidanceLevel.None || !lander.ControlsEnabled)
            {
                Current = new GuidanceAdvice();
                return;
            }

            GuidanceAdvice advice = Evaluate();

            // Avoid flicker: keep the previous message at least 0.6 s unless the new one is more urgent.
            _holdTimer -= Time.deltaTime;
            if (_holdTimer > 0f && advice.primary != _lastPrimary && advice.severity <= Current.severity && Current.HasAdvice)
            {
                GuidanceAdvice kept = Current;
                kept.targetSinkRate = advice.targetSinkRate;
                Current = kept;
                return;
            }
            if (advice.primary != _lastPrimary) { _holdTimer = 0.6f; _lastPrimary = advice.primary; }

            if (level == GuidanceLevel.Hints)
            {
                // Normal mode: only critical advice, no button highlighting.
                if (advice.severity < GuidanceSeverity.Urgent) { advice.primary = null; advice.secondary = null; }
                advice.stickDirection = Vector2.zero;
                advice.highlightThrust = advice.highlightRelease = false;
            }
            Current = advice;
        }

        GuidanceAdvice Evaluate()
        {
            var a = new GuidanceAdvice();
            float alt = lander.Altitude;
            float sink = -lander.VerticalSpeed;
            float target = TargetSinkRate(alt);
            a.targetSinkRate = target;

            bool onGround = evaluator != null && evaluator.OnGround;

            // ---- 1. on the surface
            if (onGround)
            {
                if (lander.Throttle > 0.08f && (evaluator == null || !evaluator.autoEngineCut))
                {
                    a.primary = "CUT THE ENGINE NOW";
                    a.secondary = "Press CUT to confirm the landing";
                    a.severity = GuidanceSeverity.Urgent;
                    a.highlightCut = true;
                    a.highlightRelease = true;
                }
                else
                {
                    a.primary = "TOUCHDOWN — HOLD STILL";
                    a.severity = GuidanceSeverity.Good;
                }
                return a;
            }

            if (lander.Fuel <= 0f)
            {
                a.primary = "OUT OF FUEL";
                a.secondary = "Keep the lander level";
                a.severity = GuidanceSeverity.Urgent;
                return a;
            }

            // ---- 2. attitude: must be nearly upright close to the ground
            float tiltLimit = alt < 15f ? 10f : (alt < 60f ? 22f : 35f);
            if (lander.TiltDegrees > tiltLimit)
            {
                a.primary = "LEVEL THE LANDER";
                a.secondary = lander.sasMode == SasMode.AutoLevel ? "Let go of the stick — auto-level is on" : "Tilt the opposite way";
                a.severity = alt < 30f ? GuidanceSeverity.Urgent : GuidanceSeverity.Caution;
                a.stickDirection = LevelingStick();
                return a;
            }

            // ---- 3. vertical speed vs. the safe descent profile
            float error = sink - target;   // + = falling too fast
            float hover = lander.HoverThrottle;
            float tol = alt < 15f ? nearGroundTolerance : tolerance;
            bool rising = lander.VerticalSpeed > 0.6f;

            if (error > tol * 3.5f || (alt < 25f && error > tol * 1.8f))
            {
                a.primary = "FULL THRUST NOW!";
                a.secondary = string.Format("Falling {0:0.0} m/s — slow to {1:0.0} m/s", sink, target);
                a.severity = GuidanceSeverity.Urgent;
                a.highlightThrust = true;
            }
            else if (error > tol)
            {
                a.primary = lander.Throttle < hover ? "HOLD THRUST" : "MORE THRUST";
                a.secondary = string.Format("Descending too fast ({0:0.0} m/s)", sink);
                a.severity = GuidanceSeverity.Caution;
                a.highlightThrust = true;
            }
            else if (rising || error < -tol * 1.3f)
            {
                a.primary = "RELEASE THE THRUST NOW";
                a.secondary = rising ? "You are going up — let the Moon pull you down" : "Too slow — let the lander fall a little";
                a.severity = rising ? GuidanceSeverity.Urgent : GuidanceSeverity.Caution;
                a.highlightRelease = true;
            }

            // ---- 4. horizontal: drift towards the landing zone, stop near the ground
            Vector3 desired = Vector3.zero;
            if (landingZone != null && alt > 12f)
            {
                Vector3 toZone = landingZone.transform.position - lander.transform.position;
                toZone.y = 0f;
                float dist = toZone.magnitude;
                float speedLimit = Mathf.Min(maxApproachSpeed, alt * 0.12f + 0.5f);
                if (dist > 1f) desired = toZone / dist * Mathf.Min(dist * approachGain, speedLimit);
            }
            Vector3 velocityError = desired - lander.HorizontalVelocity;
            float allowed = alt < 15f ? 0.7f : driftTolerance;

            string horizontalText = null;
            Vector2 stick = Vector2.zero;
            if (velocityError.magnitude > allowed)
            {
                // To accelerate along velocityError the thrust must lean that way.
                Vector3 local = lander.transform.InverseTransformDirection(velocityError);
                if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
                {
                    stick = new Vector2(0f, Mathf.Sign(local.z));
                    horizontalText = local.z > 0f ? "TILT FORWARD" : "TILT BACK";
                }
                else
                {
                    stick = new Vector2(Mathf.Sign(local.x), 0f);
                    horizontalText = local.x > 0f ? "TILT RIGHT" : "TILT LEFT";
                }
                horizontalText += desired.sqrMagnitude < 0.01f || Vector3.Dot(desired, lander.HorizontalVelocity) < 0f
                    ? " TO STOP DRIFTING" : " TOWARDS THE LANDING ZONE";
            }

            if (!a.HasAdvice && horizontalText != null)
            {
                a.primary = horizontalText;
                a.secondary = string.Format("Sideways speed {0:0.0} m/s", lander.HorizontalSpeed);
                a.severity = alt < 20f && lander.HorizontalSpeed > 2.5f ? GuidanceSeverity.Urgent : GuidanceSeverity.Caution;
                a.stickDirection = stick;
            }
            else if (a.HasAdvice && horizontalText != null)
            {
                a.secondary = horizontalText;
                a.stickDirection = stick;
            }

            if (!a.HasAdvice)
            {
                a.primary = alt < 3f ? "ALMOST THERE — STEADY" : "GOOD — HOLD THIS DESCENT";
                a.secondary = string.Format("Target {0:0.0} m/s down", target);
                a.severity = GuidanceSeverity.Good;
            }
            return a;
        }

        Vector2 LevelingStick()
        {
            // The top leans along the horizontal part of transform.up; push the stick the opposite way.
            Vector3 lean = lander.transform.up;
            lean.y = 0f;
            Vector3 local = lander.transform.InverseTransformDirection(-lean);
            Vector2 s = new Vector2(local.x, local.z);
            return s.sqrMagnitude > 0.0001f ? s.normalized : Vector2.zero;
        }
    }
}
