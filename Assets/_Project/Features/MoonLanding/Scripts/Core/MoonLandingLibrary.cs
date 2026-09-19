using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// One asset that holds every material and sound used by the Moon Landing challenge.
    /// Swap a clip here (e.g. a real NASA radio call) and every script picks it up.
    /// </summary>
    [CreateAssetMenu(menuName = "Astronaut Project/Moon Landing/Asset Library", fileName = "MoonLandingLibrary")]
    public class MoonLandingLibrary : ScriptableObject
    {
        [Header("VFX Materials")]
        public Material dustMaterial;      // alpha blended regolith dust
        public Material smokeMaterial;     // alpha blended dark smoke
        public Material fireMaterial;      // additive fireball / plume
        public Material sparkMaterial;     // additive sparks
        public Material flashMaterial;     // additive flare
        public Material hologramMaterial;  // additive landing-zone beam / ring

        [Header("HUD fonts (SIL Open Font License)")]
        public Font displayFont;   // Orbitron – numbers, titles
        public Font bodyFont;      // Rajdhani – labels, text

        [Header("Engine")]
        public AudioClip engineLoop;
        public AudioClip engineIgnite;
        public AudioClip engineShutdown;
        public AudioClip[] rcsPuffs;

        [Header("Damage")]
        public AudioClip[] sparks;
        public AudioClip electricHumLoop;
        public AudioClip explosion;
        public AudioClip touchdownThud;
        public AudioClip metalImpact;

        [Header("Cabin & Radio")]
        public AudioClip cabinAmbienceLoop;
        public AudioClip dustHissLoop;
        public AudioClip masterAlarmLoop;
        public AudioClip warningBeep;
        public AudioClip lowFuel;
        public AudioClip quindarIn;
        public AudioClip quindarOut;
        public AudioClip radioStatic;

        [Header("UI / Mission")]
        public AudioClip uiClick;
        public AudioClip countdownBeep;
        public AudioClip countdownGo;
        public AudioClip missionSuccess;
        public AudioClip missionFailed;

        [Header("Optional voice call-outs (drop real recordings here)")]
        public AudioClip voiceContactLight;
        public AudioClip voiceLanded;

        public static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}
