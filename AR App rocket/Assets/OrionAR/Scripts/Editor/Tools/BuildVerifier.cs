using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Checks that the generated scenes actually contain everything they should.
    /// A half-built scene does not look broken - it opens, plays and shows a black
    /// screen - so this prints an explicit pass/fail table instead.
    /// </summary>
    public static class BuildVerifier
    {
        public const string MenuScene = "Assets/OrionAR/Scenes/01_MainMenu.unity";
        public const string ARScene = "Assets/OrionAR/Scenes/02_LunarExplorer.unity";

        [MenuItem("Orion AR/Verify Build", priority = 40)]
        public static void VerifyMenu() { Verify(); }

        public static bool Verify()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Orion AR] Stop Play mode before verifying.");
                return false;
            }

            var report = new StringBuilder();
            report.AppendLine("[Orion AR] Build verification");
            bool ok = true;

            ok &= CheckAssets(report);
            ok &= CheckScene(report, ARScene, true);
            ok &= CheckScene(report, MenuScene, false);
            ok &= CheckBuildSettings(report);

            report.AppendLine(ok
                ? "RESULT: PASS - everything present. Open 01_MainMenu and press Play."
                : "RESULT: FAIL - see the lines marked MISSING / SHORT / UNWIRED.");

            if (ok) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
            return ok;
        }

        static bool CheckAssets(StringBuilder r)
        {
            r.AppendLine("ASSETS");
            bool ok = true;
            ok &= Count(r, "PartInfo", "t:PartInfo", "Assets/OrionAR/Data", 17);
            ok &= Count(r, "QuizData", "t:QuizData", "Assets/OrionAR/Data", 1);
            ok &= Count(r, "Textures", "t:Texture2D", "Assets/OrionAR/Textures", 20);
            ok &= Count(r, "AudioClips", "t:AudioClip", "Assets/OrionAR/Audio", 19);
            ok &= Count(r, "Meshes", "t:Mesh", "Assets/OrionAR/Meshes", 50);
            ok &= Count(r, "Materials", "t:Material", "Assets/OrionAR/Materials", 10);
            return ok;
        }

        static bool Count(StringBuilder r, string label, string filter, string folder, int expected)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                r.AppendLine("  MISSING  " + label + " (no folder " + folder + ")");
                return false;
            }
            int n = AssetDatabase.FindAssets(filter, new[] { folder }).Length;
            bool ok = n >= expected;
            r.AppendLine((ok ? "  OK       " : "  SHORT    ") + label + ": " + n + " (need " + expected + ")");
            return ok;
        }

        static bool CheckScene(StringBuilder r, string path, bool isAR)
        {
            r.AppendLine("SCENE " + path);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                r.AppendLine("  MISSING  scene file - the build did not finish");
                return false;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            bool ok = true;
            try
            {
                var roots = new List<GameObject>(scene.GetRootGameObjects());
                var names = new HashSet<string>();
                foreach (var g in roots) names.Add(g.name);

                string[] required = isAR
                    ? new[] { "AR Session", "XR Origin", "ContentRoot", "UI Canvas", "Systems" }
                    : new[] { "UI Canvas" };

                foreach (var n in required)
                {
                    bool f = names.Contains(n);
                    r.AppendLine((f ? "  OK       " : "  MISSING  ") + n);
                    ok &= f;
                }

                if (isAR)
                {
                    int parts = 0;
                    var ids = new HashSet<string>();
                    foreach (var g in roots)
                        foreach (var sp in g.GetComponentsInChildren<SelectablePart>(true))
                        {
                            parts++;
                            if (sp.info != null) ids.Add(sp.info.partId);
                        }
                    bool pOk = parts >= 17 && ids.Count >= 17;
                    r.AppendLine((pOk ? "  OK       " : "  SHORT    ") + "SelectableParts: " + parts +
                                 " components, " + ids.Count + " distinct parts (need 17)");
                    ok &= pOk;

                    ExperienceDirector director = null;
                    foreach (var g in roots)
                    {
                        director = g.GetComponentInChildren<ExperienceDirector>(true);
                        if (director != null) break;
                    }
                    if (director == null) { r.AppendLine("  MISSING  ExperienceDirector"); ok = false; }
                    else
                    {
                        var so = new SerializedObject(director);
                        var it = so.GetIterator();
                        bool enter = true;
                        while (it.NextVisible(enter))
                        {
                            enter = false;
                            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                            if (it.name == "m_Script") continue;
                            bool set = it.objectReferenceValue != null;
                            if (!set) { r.AppendLine("  UNWIRED  ExperienceDirector." + it.name); ok = false; }
                        }
                    }
                }
            }
            finally
            {
                if (UnityEngine.SceneManagement.SceneManager.loadedSceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
            }
            return ok;
        }

        static bool CheckBuildSettings(StringBuilder r)
        {
            r.AppendLine("SCENE LIST");
            var scenes = EditorBuildSettings.scenes;
            bool menuFirst = scenes.Length > 0 && scenes[0].path == MenuScene && scenes[0].enabled;
            bool hasAR = false;
            foreach (var s in scenes) if (s.path == ARScene && s.enabled) hasAR = true;
            r.AppendLine((menuFirst ? "  OK       " : "  MISSING  ") + "01_MainMenu at index 0");
            r.AppendLine((hasAR ? "  OK       " : "  MISSING  ") + "02_LunarExplorer");
            return menuFirst && hasAR;
        }

        [MenuItem("Orion AR/Add Scenes to Build Settings", priority = 41)]
        public static void SetSceneList()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) == null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ARScene) == null)
            {
                Debug.LogWarning("[Orion AR] Scenes do not exist yet. Run Orion AR > Build Everything first.");
                return;
            }

            // Only the two app scenes: the template SampleScene must not ship.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScene, true),
                new EditorBuildSettingsScene(ARScene, true),
            };
            Debug.Log("[Orion AR] Scene list set: 01_MainMenu (0), 02_LunarExplorer (1).");
        }
    }
}
