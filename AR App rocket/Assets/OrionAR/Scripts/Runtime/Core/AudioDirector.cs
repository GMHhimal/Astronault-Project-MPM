using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Central sound bus.
    ///
    /// Three channels, because they need independent control: UI (short, always
    /// audible), ambience (looping bed, ducked under speech-like moments) and a
    /// dedicated engine channel the finale drives continuously.
    ///
    /// Clips are looked up by name from a registry filled by the scene generator, so
    /// gameplay code says AudioDirector.Play("ui_confirm") rather than holding
    /// inspector references in a dozen scripts.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [Header("Mix")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = 0.75f;
        [Range(0f, 1f)] public float ambienceVolume = 0.35f;
        [Range(0f, 1f)] public float engineVolume = 0.8f;

        [Header("Library")]
        public List<NamedClip> clips = new List<NamedClip>();

        [System.Serializable]
        public class NamedClip
        {
            public string key;
            public AudioClip clip;
        }

        readonly Dictionary<string, AudioClip> _lookup = new Dictionary<string, AudioClip>();

        AudioSource _uiA, _uiB;     // two sources so rapid taps do not cut each other off
        AudioSource _ambience;
        AudioSource _engine;
        bool _useA;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _uiA = NewSource("UI_A", loop: false);
            _uiB = NewSource("UI_B", loop: false);
            _ambience = NewSource("Ambience", loop: true);
            _engine = NewSource("Engine", loop: true);

            RebuildLookup();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;        // 2D: this is a handheld screen, not a room
            src.volume = 0f;
            return src;
        }

        public void RebuildLookup()
        {
            _lookup.Clear();
            foreach (var c in clips)
            {
                if (c == null || string.IsNullOrEmpty(c.key) || c.clip == null) continue;
                _lookup[c.key] = c.clip;
            }
        }

        bool SoundOn
        {
            get { return AppDirector.Instance == null || AppDirector.Instance.SoundEnabled; }
        }

        public AudioClip Find(string key)
        {
            AudioClip clip;
            return _lookup.TryGetValue(key, out clip) ? clip : null;
        }

        // ---------------------------------------------------------------
        // Playback
        // ---------------------------------------------------------------

        /// <summary>Fire a one-shot UI or event sound.</summary>
        public static void Play(string key, float volumeScale = 1f, float pitch = 1f)
        {
            if (Instance == null) return;
            Instance.PlayInternal(key, volumeScale, pitch);
        }

        void PlayInternal(string key, float volumeScale, float pitch)
        {
            if (!SoundOn) return;
            var clip = Find(key);
            if (clip == null) return;

            var src = _useA ? _uiA : _uiB;
            _useA = !_useA;

            src.pitch = pitch;
            src.volume = masterVolume * uiVolume;
            src.PlayOneShot(clip, volumeScale);
        }

        /// <summary>Start or cross-fade the looping ambience bed.</summary>
        public static void SetAmbience(string key, float fadeSeconds = 1.5f)
        {
            if (Instance == null) return;
            Instance.StartCoroutine(Instance.AmbienceRoutine(key, fadeSeconds));
        }

        IEnumerator AmbienceRoutine(string key, float fade)
        {
            var clip = Find(key);
            if (clip == null || !SoundOn)
            {
                yield return FadeSource(_ambience, 0f, fade);
                _ambience.Stop();
                yield break;
            }

            if (_ambience.clip != clip)
            {
                if (_ambience.isPlaying) yield return FadeSource(_ambience, 0f, fade * 0.5f);
                _ambience.clip = clip;
                _ambience.Play();
            }

            yield return FadeSource(_ambience, masterVolume * ambienceVolume, fade);
        }

        /// <summary>Engine channel: the finale ramps this with throttle.</summary>
        public static void SetEngine(string key, float level, float pitch = 1f)
        {
            if (Instance == null) return;
            Instance.EngineInternal(key, level, pitch);
        }

        void EngineInternal(string key, float level, float pitch)
        {
            if (!SoundOn) { _engine.volume = 0f; return; }

            var clip = Find(key);
            if (clip == null) return;

            if (_engine.clip != clip)
            {
                _engine.clip = clip;
                _engine.Play();
            }
            if (!_engine.isPlaying) _engine.Play();

            _engine.pitch = pitch;
            _engine.volume = masterVolume * engineVolume * Mathf.Clamp01(level);
        }

        public static void StopEngine(float fadeSeconds = 1f)
        {
            if (Instance == null) return;
            Instance.StartCoroutine(Instance.StopEngineRoutine(fadeSeconds));
        }

        IEnumerator StopEngineRoutine(float fade)
        {
            yield return FadeSource(_engine, 0f, fade);
            _engine.Stop();
        }

        IEnumerator FadeSource(AudioSource src, float target, float duration)
        {
            if (src == null) yield break;
            float start = src.volume;
            if (duration <= 0f) { src.volume = target; yield break; }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            src.volume = target;
        }

        public static void MuteAll(bool muted)
        {
            if (Instance == null) return;
            AudioListener.volume = muted ? 0f : 1f;
        }
    }
}
