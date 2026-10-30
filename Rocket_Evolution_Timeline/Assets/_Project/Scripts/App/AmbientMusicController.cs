using UnityEngine;

namespace RocketTimeline.App
{
    /// <summary>
    /// Keeps the looping ambient background track playing continuously across
    /// scene loads, instead of restarting (and audibly cutting) every time the
    /// player goes Main Menu -> Game or back.
    ///
    /// RocketTimelineSceneBuilder.BuildAmbientMusic() bakes one of these - with
    /// its own AudioSource already configured to loop - into BOTH scenes, the
    /// same way it bakes in a Camera and an EventSystem per scene. That means
    /// when the SECOND scene loads at runtime, Unity instantiates its own copy
    /// of this object too, which would otherwise start a second, overlapping
    /// playback on top of the first. Awake() below prevents that: the first
    /// instance created this session claims the static `instance` slot and
    /// calls DontDestroyOnLoad on itself, so it survives the scene load and
    /// keeps playing uninterrupted; every instance created after that (i.e.
    /// every scene's baked-in copy loaded from then on) finds the slot already
    /// taken and destroys itself on arrival instead.
    /// </summary>
    public class AmbientMusicController : MonoBehaviour
    {
        private static AmbientMusicController instance;

        public AudioSource audioSource;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
