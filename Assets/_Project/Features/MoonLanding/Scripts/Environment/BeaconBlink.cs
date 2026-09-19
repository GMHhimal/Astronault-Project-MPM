using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>Blinks an emissive beacon (URP Lit material with emission enabled).</summary>
    public class BeaconBlink : MonoBehaviour
    {
        public Color onColor = new Color(0.4f, 1f, 1f) * 6f;
        public float period = 1.2f;
        [Range(0f, 1f)] public float phase;
        [Range(0.05f, 0.95f)] public float duty = 0.25f;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Renderer _renderer;
        MaterialPropertyBlock _block;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (_renderer == null) return;
            float t = Mathf.Repeat(Time.time / period + phase, 1f);
            bool on = t < duty;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(EmissionId, on ? onColor : Color.black);
            _block.SetColor(BaseColorId, on ? Color.white : new Color(0.2f, 0.2f, 0.2f));
            _renderer.SetPropertyBlock(_block);
        }
    }
}
