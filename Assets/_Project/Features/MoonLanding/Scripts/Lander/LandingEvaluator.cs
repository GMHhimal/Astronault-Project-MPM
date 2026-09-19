using System;
using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Watches the lander's contacts and decides the result of the landing.
    ///  • Foot pads touching down slower than the safe limits → landing.
    ///  • Too fast / body hits the ground / tipping over → crash (LanderDestruction explodes it).
    ///  • Lander must settle and the engine must be cut to confirm a landing (like Apollo "Engine stop").
    /// Also produces flight warnings (descent rate, fuel, attitude) for the HUD and audio.
    /// </summary>
    [RequireComponent(typeof(LanderController))]
    public class LandingEvaluator : MonoBehaviour
    {
        [Header("Colliders (assigned by the level builder)")]
        public Collider[] footColliders;
        public Collider[] bodyColliders;

        [Header("Touchdown limits")]
        public float safeVerticalSpeed = 2.0f;
        public float crashVerticalSpeed = 4.2f;
        public float safeHorizontalSpeed = 1.2f;
        public float crashHorizontalSpeed = 3.5f;
        public float safeTilt = 12f;
        public float tipOverTilt = 42f;
        [Tooltip("Body (not foot pad) hitting anything faster than this = crash.")]
        public float bodyCrashSpeed = 2.2f;
        [Tooltip("Seconds the lander must sit still with the engine off to confirm the landing.")]
        public float settleTime = 1.5f;
        [Tooltip("Apollo landing probes were 1.7 m long -> 'Contact light'.")]
        public float contactLightAltitude = 1.7f;

        [Tooltip("Easy mode: engine is cut automatically once the lander rests on its feet.")]
        public bool autoEngineCut;
        [Tooltip("Lander must also stay this still (m/s) to count as resting.")]
        public float restSpeed = 0.35f;

        [Header("Mission area")]
        public float outOfBoundsRadius = 1900f;
        public float ceilingAltitude = 2500f;

        public bool Armed { get; set; }
        public LandingOutcome Outcome { get; private set; }
        public string OutcomeReason { get; private set; }
        public bool HasTouchedDown { get; private set; }
        public float TouchdownVerticalSpeed { get; private set; }
        public float TouchdownHorizontalSpeed { get; private set; }
        public int HardImpacts { get; private set; }
        public bool ContactLight { get; private set; }
        public bool OnGround { get; private set; }
        public bool AwaitingEngineCut { get; private set; }
        public Vector3 LastImpactPoint { get; private set; }

        public string ActiveWarning { get; private set; }
        public bool MasterAlarm { get; private set; }

        public event Action<LandingOutcome, string> OutcomeDecided;
        /// <summary>point, normal, impact speed, wasFootPad</summary>
        public event Action<Vector3, Vector3, float, bool> Impact;
        public event Action ContactLightOn;
        public event Action FirstTouchdown;

        LanderController _lander;
        Vector3 _preStepVelocity;
        bool _footContact, _bodyContact;
        float _settleTimer, _restingOnBodyTimer;

        void Awake()
        {
            _lander = GetComponent<LanderController>();

            var footMaterial = new PhysicsMaterial("LanderFootPad");
            footMaterial.dynamicFriction = 0.9f;
            footMaterial.staticFriction = 1.1f;
            footMaterial.bounciness = 0f;
            footMaterial.frictionCombine = PhysicsMaterialCombine.Maximum;
            footMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;
            if (footColliders != null)
                foreach (Collider c in footColliders) if (c != null) c.sharedMaterial = footMaterial;
        }

        public void ResetEvaluation()
        {
            Outcome = LandingOutcome.None;
            OutcomeReason = string.Empty;
            HasTouchedDown = false;
            HardImpacts = 0;
            ContactLight = false;
            OnGround = false;
            AwaitingEngineCut = false;
            _settleTimer = 0f;
            _restingOnBodyTimer = 0f;
            ActiveWarning = string.Empty;
            MasterAlarm = false;
        }

        /// <summary>Force an outcome (used for out-of-bounds, abort, etc.).</summary>
        public void Decide(LandingOutcome outcome, string reason)
        {
            if (Outcome != LandingOutcome.None) return;
            Outcome = outcome;
            OutcomeReason = reason;
            Armed = false;
            MasterAlarm = false;
            ActiveWarning = string.Empty;
            if (OutcomeDecided != null) OutcomeDecided(outcome, reason);
        }

        bool IsFoot(Collider c)
        {
            if (footColliders == null) return false;
            for (int i = 0; i < footColliders.Length; i++) if (footColliders[i] == c) return true;
            return false;
        }

        void OnCollisionEnter(Collision collision) { HandleCollision(collision, true); }
        void OnCollisionStay(Collision collision) { HandleCollision(collision, false); }

        void HandleCollision(Collision collision, bool isEnter)
        {
            int count = collision.contactCount;
            if (count == 0) return;

            bool footHit = false, bodyHit = false;
            Vector3 point = Vector3.zero, normal = Vector3.up;
            for (int i = 0; i < count; i++)
            {
                ContactPoint cp = collision.GetContact(i);
                if (IsFoot(cp.thisCollider)) footHit = true; else bodyHit = true;
                point += cp.point;
                normal = cp.normal;
            }
            point /= count;

            if (footHit) _footContact = true;
            if (bodyHit) _bodyContact = true;
            if (!isEnter) return;

            // Velocity BEFORE the physics step that produced this contact.
            float verticalSpeed = Mathf.Max(0f, -_preStepVelocity.y);
            float horizontalSpeed = new Vector3(_preStepVelocity.x, 0f, _preStepVelocity.z).magnitude;
            float impactSpeed = _preStepVelocity.magnitude;
            LastImpactPoint = point;

            if (impactSpeed > 0.8f && Impact != null) Impact(point, normal, impactSpeed, footHit && !bodyHit);

            if (!Armed || Outcome != LandingOutcome.None) return;

            if (bodyHit && impactSpeed > bodyCrashSpeed)
            {
                Decide(LandingOutcome.Crashed, string.Format("The lander's body struck the surface at {0:0.0} m/s.", impactSpeed));
                return;
            }

            if (footHit)
            {
                if (!HasTouchedDown)
                {
                    HasTouchedDown = true;
                    TouchdownVerticalSpeed = verticalSpeed;
                    TouchdownHorizontalSpeed = horizontalSpeed;
                    if (FirstTouchdown != null) FirstTouchdown();
                }

                if (verticalSpeed > crashVerticalSpeed)
                {
                    Decide(LandingOutcome.Crashed, string.Format("Landing gear collapsed: vertical impact {0:0.0} m/s (limit {1:0.0}).", verticalSpeed, crashVerticalSpeed));
                }
                else if (horizontalSpeed > crashHorizontalSpeed)
                {
                    Decide(LandingOutcome.Crashed, string.Format("Lander skidded and broke apart: sideways speed {0:0.0} m/s (limit {1:0.0}).", horizontalSpeed, crashHorizontalSpeed));
                }
                else if (verticalSpeed > safeVerticalSpeed || horizontalSpeed > safeHorizontalSpeed)
                {
                    HardImpacts++;
                    TouchdownVerticalSpeed = Mathf.Max(TouchdownVerticalSpeed, verticalSpeed);
                    TouchdownHorizontalSpeed = Mathf.Max(TouchdownHorizontalSpeed, horizontalSpeed);
                }
            }
        }

        void FixedUpdate()
        {
            bool foot = _footContact, body = _bodyContact;
            _footContact = _bodyContact = false;
            _preStepVelocity = _lander.Body.linearVelocity;

            OnGround = foot || body;
            if (!Armed || Outcome != LandingOutcome.None) { AwaitingEngineCut = false; return; }

            float dt = Time.fixedDeltaTime;

            if (!ContactLight && _lander.Altitude <= contactLightAltitude)
            {
                ContactLight = true;
                if (ContactLightOn != null) ContactLightOn();
            }

            Vector3 p = transform.position;
            if (new Vector2(p.x, p.z).magnitude > outOfBoundsRadius || _lander.Altitude > ceilingAltitude)
            {
                Decide(LandingOutcome.OutOfBounds, "The lander left the mission area. Mission aborted.");
                return;
            }

            if (OnGround && _lander.TiltDegrees > tipOverTilt)
            {
                Decide(LandingOutcome.TippedOver, string.Format("The lander tipped over ({0:0}° tilt).", _lander.TiltDegrees));
                return;
            }

            bool still = _lander.Body.linearVelocity.magnitude < restSpeed && _lander.Body.angularVelocity.magnitude < 0.25f;

            if (body && !foot && still)
            {
                _restingOnBodyTimer += dt;
                if (_restingOnBodyTimer > 1f) { Decide(LandingOutcome.TippedOver, "The lander came to rest on its side."); return; }
            }
            else _restingOnBodyTimer = 0f;

            AwaitingEngineCut = foot && still && _lander.Throttle >= 0.08f;
            if (AwaitingEngineCut && autoEngineCut)
            {
                _lander.CutEngine();          // Easy mode: the computer shuts the engine down for you
                AwaitingEngineCut = false;
            }

            if (foot && still && _lander.Throttle < 0.08f)
            {
                _settleTimer += dt;
                if (_settleTimer >= settleTime) ConfirmLanding();
            }
            else _settleTimer = 0f;

            UpdateWarnings();
        }

        void ConfirmLanding()
        {
            float tilt = _lander.TiltDegrees;
            if (HardImpacts > 0 || TouchdownVerticalSpeed > safeVerticalSpeed || TouchdownHorizontalSpeed > safeHorizontalSpeed)
                Decide(LandingOutcome.HardLanding, string.Format("Hard landing — {0:0.0} m/s vertical. The Lunar Module is damaged but the crew is safe.", TouchdownVerticalSpeed));
            else if (tilt > safeTilt)
                Decide(LandingOutcome.HardLanding, string.Format("Landed on a slope ({0:0}° tilt). Ascent may be difficult.", tilt));
            else
                Decide(LandingOutcome.Success, "Soft touchdown. The Eagle has landed!");
        }

        void UpdateWarnings()
        {
            string warning = string.Empty;
            bool alarm = false;
            float alt = _lander.Altitude;
            float vs = _lander.VerticalSpeed;

            // Allowed sink rate shrinks as the ground gets closer.
            float allowedSink = Mathf.Lerp(safeVerticalSpeed * 1.2f, 14f, Mathf.Clamp01(alt / 250f));
            if (!OnGround && alt < 250f && -vs > allowedSink)
            {
                warning = "DESCENT RATE HIGH";
                alarm = alt < 80f;
            }
            else if (!OnGround && alt < 40f && _lander.TiltDegrees > 20f)
            {
                warning = "ATTITUDE — LEVEL THE LANDER";
                alarm = alt < 15f;
            }
            else if (!OnGround && alt < 30f && _lander.HorizontalSpeed > safeHorizontalSpeed * 2.5f)
            {
                warning = "HORIZONTAL DRIFT";
            }

            if (_lander.FuelFraction <= 0f) { warning = "FUEL DEPLETED"; alarm = !OnGround; }
            else if (_lander.FuelFraction < 0.1f && string.IsNullOrEmpty(warning)) { warning = "LOW FUEL"; alarm = true; }

            ActiveWarning = warning;
            MasterAlarm = alarm;
        }
    }
}
