using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Applies lunar physics + rendering settings only while this scene is loaded,
    /// and restores the project defaults afterwards so other team members' scenes are not affected.
    /// Also applies mobile settings (landscape lock, 60 FPS, lighter terrain & shadows) for this scene only.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class LunarEnvironment : MonoBehaviour
    {
        public const float MoonGravity = 1.62f; // m/s^2

        [Tooltip("Surface gravity in m/s^2 (Moon = 1.62, Earth = 9.81)")]
        public float gravity = MoonGravity;

        [Tooltip("Shadow distance on PC while this scene runs (the shared URP asset value is restored on exit).")]
        public float runtimeShadowDistance = 900f;
        [Tooltip("Shadow distance on phones/tablets (smaller = sharper shadows with the 1024 mobile shadow map).")]
        public float mobileShadowDistance = 220f;

        [Header("Mobile")]
        [Tooltip("Force landscape while this scene is open (HUD is designed for landscape).")]
        public bool lockLandscapeOnMobile = true;
        public int mobileTargetFrameRate = 60;
        [Tooltip("Terrain pixel error on mobile (higher = faster, less detail).")]
        public float mobileTerrainPixelError = 5f;

        public ReflectionProbe reflectionProbe;

        Vector3 _previousGravity;
        float _previousShadowDistance = -1f;
        UniversalRenderPipelineAsset _urp;
        ScreenOrientation _previousOrientation;
        int _previousFrameRate;
        bool _mobileApplied;

        public static bool IsMobile
        {
            get { return Application.isMobilePlatform; }
        }

        void Awake()
        {
            _previousGravity = Physics.gravity;
            Physics.gravity = new Vector3(0f, -gravity, 0f);

            _urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float shadow = IsMobile ? mobileShadowDistance : runtimeShadowDistance;
            if (_urp != null && shadow > 0f)
            {
                _previousShadowDistance = _urp.shadowDistance;
                _urp.shadowDistance = shadow;
            }

            if (IsMobile)
            {
                _mobileApplied = true;
                _previousOrientation = Screen.orientation;
                _previousFrameRate = Application.targetFrameRate;
                if (lockLandscapeOnMobile) Screen.orientation = ScreenOrientation.LandscapeLeft;
                Application.targetFrameRate = mobileTargetFrameRate;
                foreach (Terrain t in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                    t.heightmapPixelError = Mathf.Max(t.heightmapPixelError, mobileTerrainPixelError);
            }
        }

        void Start()
        {
            if (reflectionProbe != null) reflectionProbe.RenderProbe();
        }

        void OnDestroy()
        {
            Physics.gravity = _previousGravity;
            if (_urp != null && _previousShadowDistance >= 0f) _urp.shadowDistance = _previousShadowDistance;
            if (_mobileApplied)
            {
                Screen.orientation = _previousOrientation;
                Application.targetFrameRate = _previousFrameRate;
            }
        }
    }
}
