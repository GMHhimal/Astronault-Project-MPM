using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// All lander sound. Note for the viva: space is a vacuum, so what you hear is what the crew
    /// would hear INSIDE the cabin — engine vibration through the structure, thruster valves,
    /// dust hitting the hull, alarms and the radio (with NASA's real Quindar tones 2525/2475 Hz).
    /// </summary>
    public class LanderAudio : MonoBehaviour
    {
        public MoonLandingLibrary library;
        public LanderController lander;
        public LanderEffects effects;
        public LandingEvaluator evaluator;

        [Header("Mix")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float engineVolume = 0.9f;
        [Range(0f, 1f)] public float dustVolume = 0.5f;
        [Range(0f, 1f)] public float ambienceVolume = 0.3f;
        [Range(0f, 1f)] public float alarmVolume = 0.35f;
        [Range(0f, 1f)] public float effectsVolume = 1f;

        AudioSource _engine, _dust, _ambience, _alarm, _hum, _ui, _world;
        bool _engineWasOn;
        float _rcsCooldown;
        bool _lowFuelPlayed, _alarmOn;
        float _humLevel;

        void Awake()
        {
            if (lander == null) lander = GetComponent<LanderController>();
            if (library == null) { Debug.LogWarning("[MoonLanding] LanderAudio has no library assigned."); return; }

            _engine = MakeSource("Audio_Engine", library.engineLoop, true, 0.15f);
            _dust = MakeSource("Audio_Dust", library.dustHissLoop, true, 0.1f);
            _ambience = MakeSource("Audio_Cabin", library.cabinAmbienceLoop, true, 0f);
            _alarm = MakeSource("Audio_Alarm", library.masterAlarmLoop, true, 0f);
            _hum = MakeSource("Audio_ElectricHum", library.electricHumLoop, true, 0.6f);
            _ui = MakeSource("Audio_UI", null, false, 0f);
            _world = MakeSource("Audio_World", null, false, 0.55f);
            _world.transform.SetParent(null, true); // free-moving source for impacts at points
            _world.gameObject.name = "MoonLanding_WorldAudio";
        }

        AudioSource MakeSource(string name, AudioClip clip, bool loop, float spatialBlend)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = spatialBlend;
            s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 10f;
            s.maxDistance = 600f;
            s.volume = 0f;
            if (loop && clip != null)
            {
                s.time = Random.Range(0f, clip.length * 0.9f);
                s.Play();
            }
            return s;
        }

        void OnDestroy()
        {
            if (_world != null) Destroy(_world.gameObject);
        }

        void Update()
        {
            if (library == null || lander == null || _engine == null) return;
            float dt = Time.deltaTime;

            // Engine loop
            float thr = lander.Throttle;
            _engine.volume = masterVolume * engineVolume * Mathf.Pow(thr, 0.7f);
            _engine.pitch = 0.78f + thr * 0.35f + (Mathf.PerlinNoise(Time.time * 2f, 0.1f) - 0.5f) * 0.04f;

            bool on = lander.EngineOn;
            if (on && !_engineWasOn) PlayUI(library.engineIgnite, 0.8f);
            if (!on && _engineWasOn && !lander.IsDestroyed) PlayUI(library.engineShutdown, 0.7f);
            _engineWasOn = on;

            // Dust
            float dust = effects != null ? effects.DustIntensity : 0f;
            _dust.volume = masterVolume * dustVolume * dust;

            // Cabin ambience (power off once destroyed)
            float ambienceTarget = lander.IsDestroyed ? 0f : ambienceVolume;
            _ambience.volume = Mathf.MoveTowards(_ambience.volume, masterVolume * ambienceTarget, dt * 0.5f);

            // RCS valve puffs
            _rcsCooldown -= dt;
            if (effects != null && effects.RcsActivity > 0.15f && _rcsCooldown <= 0f)
            {
                PlayUI(MoonLandingLibrary.Pick(library.rcsPuffs), 0.35f + 0.35f * effects.RcsActivity, Random.Range(0.9f, 1.15f));
                _rcsCooldown = Random.Range(0.09f, 0.16f);
            }

            // Alarms
            bool alarm = evaluator != null && evaluator.MasterAlarm && lander.ControlsEnabled;
            if (alarm != _alarmOn)
            {
                _alarmOn = alarm;
                if (alarm) PlayUI(library.warningBeep, 0.6f);
            }
            _alarm.volume = Mathf.MoveTowards(_alarm.volume, alarm ? masterVolume * alarmVolume : 0f, dt * 4f);

            if (!_lowFuelPlayed && lander.ControlsEnabled && lander.FuelFraction < 0.2f)
            {
                _lowFuelPlayed = true;
                PlayUI(library.lowFuel, 0.8f);
            }

            // Electric hum while damaged
            _humLevel = Mathf.MoveTowards(_humLevel, lander.IsDestroyed ? 0.5f : 0f, dt * 0.5f);
            _hum.volume = masterVolume * effectsVolume * _humLevel;
        }

        // ---------------------------------------------------------------- one-shots
        public void PlayUI(AudioClip clip, float volume) { PlayUI(clip, volume, 1f); }

        public void PlayUI(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || _ui == null) return;
            _ui.pitch = pitch;
            _ui.PlayOneShot(clip, volume * masterVolume);
        }

        public void PlayAt(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null || _world == null) return;
            _world.transform.position = position;
            _world.PlayOneShot(clip, volume * masterVolume * effectsVolume);
        }

        public void PlaySpark(Vector3 position, float strength)
        {
            if (library == null) return;
            PlayAt(MoonLandingLibrary.Pick(library.sparks), position, Mathf.Lerp(0.25f, 0.8f, strength));
        }

        public void PlayImpact(Vector3 position, float speed, bool footPad)
        {
            if (library == null) return;
            float v = Mathf.Clamp01(speed / 5f);
            if (footPad) PlayUI(library.touchdownThud, Mathf.Lerp(0.35f, 1f, v));
            if (!footPad || speed > 2.5f) PlayAt(library.metalImpact, position, Mathf.Lerp(0.3f, 1f, v));
        }

        public void PlayExplosion(Vector3 position)
        {
            if (library == null) return;
            PlayUI(library.explosion, 1f);
            PlayAt(library.metalImpact, position, 0.8f);
            if (_engine != null) _engine.volume = 0f;
        }

        public void PlayRadio(bool intro)
        {
            if (library == null) return;
            PlayUI(intro ? library.quindarIn : library.quindarOut, 0.45f);
        }

        public void PlayVoice(AudioClip clip)
        {
            if (clip == null) return;
            PlayRadio(true);
            PlayUI(clip, 1f);
        }
    }
}
