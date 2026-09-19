using System;
using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>All the ways a landing attempt can end.</summary>
    public enum LandingOutcome { None, Success, HardLanding, Crashed, TippedOver, OutOfBounds, Aborted, MissedTarget }

    /// <summary>Data produced when the Moon Landing Challenge finishes (used by the HUD and by other team scenes).</summary>
    [Serializable]
    public struct MoonLandingResult
    {
        public LandingOutcome outcome;
        public string reason;
        public string difficulty;
        public float touchdownVerticalSpeed;
        public float touchdownHorizontalSpeed;
        public float finalTiltDeg;
        public float distanceToTarget;
        public float fuelRemainingKg;
        public float fuelFraction;
        public float missionTime;
        public int score;
        public int stars;
        public bool newBest;
        /// <summary>Points earned during the flight (awarded even if the landing fails).</summary>
        public int flightPoints;

        public bool IsSuccess => outcome == LandingOutcome.Success || outcome == LandingOutcome.HardLanding;
    }

    /// <summary>
    /// Static hooks so the main HUD / other challenges can react to this challenge
    /// without holding a direct reference to anything inside the Moon Landing scene.
    /// Example (in another scene):  MoonLandingEvents.ChallengeFinished += r => Debug.Log(r.score);
    /// </summary>
    public static class MoonLandingEvents
    {
        public const string BestScoreKey = "Astronaut.MoonLanding.BestScore";
        public const string CompletedKey = "Astronaut.MoonLanding.Completed";
        public const string DifficultyKey = "Astronaut.MoonLanding.Difficulty";

        /// <summary>Raised once per attempt when the result is known.</summary>
        public static event Action<MoonLandingResult> ChallengeFinished;

        public static int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);
        public static bool HasCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

        internal static void RaiseFinished(MoonLandingResult result)
        {
            if (ChallengeFinished != null) ChallengeFinished(result);
        }
    }
}
