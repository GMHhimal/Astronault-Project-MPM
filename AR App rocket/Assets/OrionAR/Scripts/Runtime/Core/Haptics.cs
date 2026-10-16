using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Light haptic feedback on Android.
    ///
    /// Unity's Handheld.Vibrate() is a blunt ~500 ms buzz with no duration control,
    /// which feels wrong for a UI tick. This calls Android's Vibrator directly so a
    /// button press gets a 12 ms tap rather than a phone-wide rumble.
    /// Silently does nothing in the Editor or on unsupported devices.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static bool _probed;
        static int _apiLevel;

        static AndroidJavaObject Vibrator
        {
            get
            {
                if (!_probed)
                {
                    _probed = true;
                    try
                    {
                        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                            _apiLevel = version.GetStatic<int>("SDK_INT");

                        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                            _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[Haptics] unavailable: " + e.Message);
                        _vibrator = null;
                    }
                }
                return _vibrator;
            }
        }

        static void Buzz(long milliseconds, int amplitude)
        {
            var v = Vibrator;
            if (v == null) return;
            try
            {
                if (_apiLevel >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>(
                               "createOneShot", milliseconds, amplitude))
                        v.Call("vibrate", effect);
                }
                else
                {
                    v.Call("vibrate", milliseconds);
                }
            }
            catch { /* never let feedback break the app */ }
        }
#else
        static void Buzz(long milliseconds, int amplitude) { }
#endif

        /// <summary>Button press.</summary>
        public static void Tick() { if (Enabled) Buzz(12, 60); }

        /// <summary>Something registered - a part studied, an answer accepted.</summary>
        public static void Confirm() { if (Enabled) Buzz(28, 120); }

        /// <summary>Wrong answer, or an action refused.</summary>
        public static void Reject() { if (Enabled) Buzz(55, 180); }

        /// <summary>Major event: placement locked, quiz passed, engine ignition.</summary>
        public static void Impact() { if (Enabled) Buzz(90, 255); }
    }
}
