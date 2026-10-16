using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrionAR
{
    /// <summary>
    /// Owns scene flow and anything that must outlive a scene change.
    ///
    /// Also carries results between scenes - the quiz score survives the transition to
    /// the finale, and the finale reports back to the menu - without a save file or a
    /// pile of static variables scattered through the code.
    /// </summary>
    public class AppDirector : MonoBehaviour
    {
        public const string SceneMenu = "01_MainMenu";
        public const string SceneExperience = "02_LunarExplorer";

        public static AppDirector Instance { get; private set; }

        [Header("Session results")]
        public int LastScore;
        public int LastTotal;
        public bool LastRunPassed;
        public int PartsStudied;
        public bool ReturnedFromMission;

        [Header("Settings")]
        public bool SoundEnabled = true;
        public bool HapticsEnabled = true;
        [Tooltip("Set when the device cannot run ARCore, so the experience starts in orbit view instead.")]
        public bool ARUnavailable;

        CanvasGroup _fader;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("~AppDirector");
            Instance = go.AddComponent<AppDirector>();
            DontDestroyOnLoad(go);
            Instance.BuildFader();
            Instance.ApplyPerformanceSettings();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Mobile power and thermal settings.
        ///
        /// An AR session already drives the camera, the IMU and plane detection. Left
        /// uncapped the GPU will render as fast as it can, overheat, and then be
        /// throttled by the OS - which on an exhibition stand means the app gets
        /// visibly worse over the course of the day. Capping at 60 keeps it stable.
        /// </summary>
        void ApplyPerformanceSettings()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            PointerInput.Enable();
        }

        void BuildFader()
        {
            var canvasGO = new GameObject("~TransitionCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;          // above everything, including toasts

            var img = UIKit.Panel("Fade", canvasGO.transform, UITheme.SpaceDeep);
            UIKit.Stretch(img.rectTransform);

            _fader = canvasGO.AddComponent<CanvasGroup>();
            _fader.alpha = 0f;
            _fader.blocksRaycasts = false;
            _fader.interactable = false;
        }

        // ---------------------------------------------------------------
        // Scene flow
        // ---------------------------------------------------------------

        public void GoToMenu()
        {
            StartCoroutine(Transition(SceneMenu));
        }

        public void StartMission()
        {
            LastScore = 0;
            LastTotal = 0;
            LastRunPassed = false;
            PartsStudied = 0;
            ReturnedFromMission = false;
            StartCoroutine(Transition(SceneExperience));
        }

        public void CompleteMission(int score, int total, bool passed)
        {
            LastScore = score;
            LastTotal = total;
            LastRunPassed = passed;
            ReturnedFromMission = true;
            StartCoroutine(Transition(SceneMenu));
        }

        IEnumerator Transition(string sceneName)
        {
            yield return Fade(1f, 0.35f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op != null)
            {
                while (!op.isDone) yield return null;
            }
            else
            {
                Debug.LogError("[AppDirector] Scene '" + sceneName +
                               "' is not in Build Profiles > Scene List.");
            }

            // One frame for the new scene's Awake/Start to run before revealing it.
            yield return null;
            yield return Fade(0f, 0.45f);
        }

        public IEnumerator Fade(float target, float duration)
        {
            if (_fader == null) yield break;

            _fader.blocksRaycasts = target > 0.01f;
            float start = _fader.alpha;
            float t = 0f;

            while (t < duration)
            {
                // Unscaled: the finale slows Time.timeScale in places and the fade
                // must not slow with it.
                t += Time.unscaledDeltaTime;
                _fader.alpha = Mathf.Lerp(start, target, UITheme.EaseInOut(t / duration));
                yield return null;
            }

            _fader.alpha = target;
            _fader.blocksRaycasts = target > 0.01f;
        }
    }
}
