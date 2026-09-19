using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>Fades a flash light out, with a little flicker.</summary>
    public class LightFlashFade : MonoBehaviour
    {
        public float duration = 1f;
        Light _light;
        float _start, _t;

        void Awake() { _light = GetComponent<Light>(); if (_light != null) _start = _light.intensity; }

        void Update()
        {
            if (_light == null) return;
            _t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_t / duration);
            _light.intensity = _start * k * k * (0.8f + 0.4f * Mathf.PerlinNoise(Time.time * 25f, 0.5f));
            if (k <= 0f) _light.enabled = false;
        }
    }
}
