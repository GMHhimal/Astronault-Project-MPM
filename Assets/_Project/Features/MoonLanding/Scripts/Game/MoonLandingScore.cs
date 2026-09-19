using System;
using System.Collections.Generic;
using UnityEngine;

namespace Astronaut.MoonLanding
{
    [Serializable]
    public struct ScoreLine
    {
        public string label;
        public int points;
        public ScoreLine(string label, int points) { this.label = label; this.points = points; }
    }

    /// <summary>
    /// Scoring for the Moon Landing Challenge.
    ///
    /// DURING the flight (kept even if you crash):
    ///   • Approach     – points for closing distance to the landing zone
    ///   • Smooth flying – points every second the descent rate follows the safe profile
    ///   • Milestones   – bonuses for passing 100 m / 50 m / 20 m / contact light at a safe speed
    ///
    /// AT THE END:
    ///   • Landed: landing bonus + softness + precision + fuel saved + level attitude
    ///   • Crashed / tipped: flight points + closest-approach bonus (no landing bonus)
    ///   • Everything × difficulty multiplier
    /// </summary>
    public class MoonLandingScore : MonoBehaviour
    {
        public LanderController lander;
        public LandingEvaluator evaluator;
        public LandingZone landingZone;
        public LandingGuidance guidance;

        [Header("Flight points")]
        public int maxApproachPoints = 600;
        public float approachPointsPerMetre = 1.2f;
        public int maxSmoothPoints = 900;
        public float smoothPointsPerSecond = 12f;

        /// <summary>points, label — for floating "+100 SMOOTH DESCENT" popups.</summary>
        public event Action<int, string> PointsAwarded;

        public bool Running { get; set; }
        public float Multiplier { get; set; }
        /// <summary>Live score shown in the HUD (flight points × multiplier).</summary>
        public int LiveScore { get { return Mathf.RoundToInt(FlightPoints * Mathf.Max(1f, Multiplier)); } }
        public float FlightPoints { get { return _approach + _smooth + _milestones; } }
        public float ClosestDistance { get; private set; }

        float _approach, _smooth, _milestones;
        float _lastDistance = -1f;
        float _smoothChunk;
        readonly bool[] _milestoneDone = new bool[4];
        static readonly float[] MilestoneAltitudes = { 100f, 50f, 20f, 1.7f };
        static readonly int[] MilestonePoints = { 50, 75, 100, 150 };
        static readonly string[] MilestoneNames = { "100 M", "50 M", "20 M", "CONTACT LIGHT" };

        public void ResetScore(float multiplier)
        {
            Multiplier = multiplier;
            _approach = _smooth = _milestones = 0f;
            _smoothChunk = 0f;
            _lastDistance = -1f;
            ClosestDistance = float.MaxValue;
            for (int i = 0; i < _milestoneDone.Length; i++) _milestoneDone[i] = false;
            Running = false;
        }

        void Update()
        {
            if (!Running || lander == null || !lander.ControlsEnabled) return;
            float dt = Time.deltaTime;
            float alt = lander.Altitude;
            float sink = -lander.VerticalSpeed;

            // Approach
            if (landingZone != null)
            {
                float d = landingZone.HorizontalDistance(lander.transform.position);
                ClosestDistance = Mathf.Min(ClosestDistance, d);
                if (_lastDistance >= 0f && d < _lastDistance && _approach < maxApproachPoints)
                    _approach = Mathf.Min(maxApproachPoints, _approach + (_lastDistance - d) * approachPointsPerMetre);
                _lastDistance = d;
            }

            // Smooth flying: follow the safe descent profile
            float target = guidance != null ? guidance.TargetSinkRate(alt) : Mathf.Clamp(0.5f + alt * 0.09f, 0.5f, 10f);
            bool smooth = sink > -0.3f && Mathf.Abs(sink - target) < 1.6f && lander.TiltDegrees < 30f && alt < 200f;
            if (smooth && _smooth < maxSmoothPoints)
            {
                float add = smoothPointsPerSecond * dt;
                _smooth = Mathf.Min(maxSmoothPoints, _smooth + add);
                _smoothChunk += add;
                if (_smoothChunk >= 50f)
                {
                    _smoothChunk -= 50f;
                    Award(50, "SMOOTH DESCENT", false);
                }
            }

            // Milestones
            for (int i = 0; i < MilestoneAltitudes.Length; i++)
            {
                if (_milestoneDone[i] || alt > MilestoneAltitudes[i]) continue;
                _milestoneDone[i] = true;
                float safeSink = Mathf.Max(evaluator != null ? evaluator.safeVerticalSpeed * 1.4f : 2.8f, MilestoneAltitudes[i] * 0.16f);
                if (sink <= safeSink)
                    Award(MilestonePoints[i], MilestoneNames[i] + " SAFE", true);
            }
        }

        void Award(int points, string label, bool addToTotal)
        {
            if (addToTotal) _milestones += points;
            if (PointsAwarded != null) PointsAwarded(Mathf.RoundToInt(points * Mathf.Max(1f, Multiplier)), label);
        }

        /// <summary>Builds the final breakdown. Returns total score (already multiplied).</summary>
        public int BuildFinal(MoonLandingResult r, DifficultyPreset preset, List<ScoreLine> lines)
        {
            lines.Clear();
            int flight = Mathf.RoundToInt(FlightPoints);
            if (_approach > 0f) lines.Add(new ScoreLine("Approach to landing zone", Mathf.RoundToInt(_approach)));
            if (_smooth > 0f) lines.Add(new ScoreLine("Smooth descent", Mathf.RoundToInt(_smooth)));
            if (_milestones > 0f) lines.Add(new ScoreLine("Safe altitude milestones", Mathf.RoundToInt(_milestones)));

            float raw = flight;
            float zoneRadius = landingZone != null ? landingZone.radius : 15f;
            float crashV = evaluator != null ? evaluator.crashVerticalSpeed : 4.2f;
            float tipTilt = evaluator != null ? evaluator.tipOverTilt : 42f;

            switch (r.outcome)
            {
                case LandingOutcome.Success:
                case LandingOutcome.HardLanding:
                case LandingOutcome.MissedTarget:
                {
                    bool soft = r.outcome == LandingOutcome.Success;
                    int landing = soft ? 1500 : (r.outcome == LandingOutcome.HardLanding ? 700 : 500);
                    lines.Add(new ScoreLine(soft ? "Safe landing bonus" : (r.outcome == LandingOutcome.HardLanding ? "Hard landing bonus" : "Landed outside the zone"), landing));
                    int softness = Mathf.RoundToInt(Mathf.Clamp01(1f - r.touchdownVerticalSpeed / crashV) * 1000f);
                    lines.Add(new ScoreLine(string.Format("Soft touchdown ({0:0.0} m/s)", r.touchdownVerticalSpeed), softness));
                    float precision01 = Mathf.Clamp01(1f - Mathf.Max(0f, r.distanceToTarget - zoneRadius) / 150f);
                    if (r.distanceToTarget <= zoneRadius) precision01 = Mathf.Lerp(0.8f, 1f, 1f - r.distanceToTarget / zoneRadius);
                    int precision = Mathf.RoundToInt(precision01 * 1000f);
                    lines.Add(new ScoreLine(string.Format("Precision ({0:0} m from centre)", r.distanceToTarget), precision));
                    int fuel = Mathf.RoundToInt(r.fuelFraction * 800f);
                    lines.Add(new ScoreLine(string.Format("Fuel saved ({0:0}%)", r.fuelFraction * 100f), fuel));
                    int level = Mathf.RoundToInt(Mathf.Clamp01(1f - r.finalTiltDeg / tipTilt) * 400f);
                    lines.Add(new ScoreLine(string.Format("Level stance ({0:0}°)", r.finalTiltDeg), level));
                    raw += landing + softness + precision + fuel + level;
                    break;
                }
                case LandingOutcome.Crashed:
                case LandingOutcome.TippedOver:
                {
                    float closest = Mathf.Min(ClosestDistance, r.distanceToTarget);
                    int approachBonus = Mathf.RoundToInt(Mathf.Clamp01(1f - closest / 300f) * 300f);
                    if (approachBonus > 0) lines.Add(new ScoreLine(string.Format("Closest approach ({0:0} m)", closest), approachBonus));
                    int survival = Mathf.RoundToInt(Mathf.Min(r.missionTime, 120f) * 2f);
                    if (survival > 0) lines.Add(new ScoreLine(string.Format("Flight time ({0:0} s)", r.missionTime), survival));
                    raw += approachBonus + survival;
                    break;
                }
                default: // out of bounds / aborted
                    raw *= 0.5f;
                    lines.Add(new ScoreLine("Mission aborted (×0.5)", -Mathf.RoundToInt(flight * 0.5f)));
                    break;
            }

            raw = Mathf.Max(0f, raw);
            float multiplier = preset != null ? preset.scoreMultiplier : 1f;
            int total = Mathf.RoundToInt(raw * multiplier);
            if (multiplier != 1f)
                lines.Add(new ScoreLine(string.Format("Difficulty bonus ×{0:0.0#}", multiplier), total - Mathf.RoundToInt(raw)));
            return total;
        }
    }
}
