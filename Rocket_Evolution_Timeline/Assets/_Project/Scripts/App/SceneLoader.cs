using UnityEngine;
using UnityEngine.SceneManagement;

namespace RocketTimeline.App
{
    /// <summary>
    /// Tiny wrapper around SceneManager so UI buttons can call a named method
    /// (Unity's Button.onClick can't call static methods or take arguments
    /// through the Inspector easily). One of these lives in both the Main
    /// Menu scene and the Game scene.
    ///
    /// Both scene names below must be registered in
    /// File > Build Profiles > Scene List (the scene builder does this for
    /// you automatically when you run "Rocket Timeline > Build Everything").
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public const string MainMenuSceneName = "MainMenu";
        public const string GameSceneName = "RocketTimelineGame";

        public void LoadMainMenu() => LoadScene(MainMenuSceneName);
        public void LoadGame() => LoadScene(GameSceneName);

        public void LoadScene(string sceneName)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not in Build Settings. " +
                                "Run 'Rocket Timeline > Build Everything' again, or add it manually " +
                                "under File > Build Profiles > Scene List.");
            }
        }
    }
}
