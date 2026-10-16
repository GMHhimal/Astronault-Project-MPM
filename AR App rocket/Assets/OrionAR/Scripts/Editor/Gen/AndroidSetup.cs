using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Player settings an ARCore phone app needs. Applied by Build Everything and
    /// available on its own from the menu.
    ///
    ///   Portrait only     every layout is designed for a portrait phone
    ///   Android 7.1+      Unity 6 minimum (API 25); ARCore needs 24
    ///   IL2CPP + ARM64    Google Play requires 64-bit; ARCore ships ARM64 libraries
    ///   OpenGL ES 3       the graphics API ARCore's camera background is built on
    ///   Linear colour     correct PBR lighting on the spacecraft materials
    /// </summary>
    public static class AndroidSetup
    {
        [MenuItem("Orion AR/Apply Android Player Settings", priority = 42)]
        static void ApplyMenu() { Apply(verbose: true); }

        public static void Apply(bool verbose)
        {
            PlayerSettings.companyName = "Hasitha MPM";
            PlayerSettings.productName = "Orion AR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.hasitha.orionar");

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.Android.renderOutsideSafeArea = false;

            int added = AddARBackgroundFeature();

            if (verbose || added > 0)
                Debug.Log("[Orion AR] Android player settings applied: portrait, API 25+, " +
                          "IL2CPP ARM64, OpenGL ES 3, linear colour." +
                          (added > 0 ? " Added the AR background feature to " + added + " URP renderer(s)." : ""));
        }

        /// <summary>
        /// AR Foundation draws the camera feed through a URP renderer feature. Without
        /// it on the renderer the phone uses, the spacecraft renders over solid black
        /// instead of the room - the classic "AR works but there is no camera" fault.
        /// </summary>
        public static int AddARBackgroundFeature()
        {
            System.Type featureType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in SafeTypes(asm))
                {
                    if (t.Name == "ARBackgroundRendererFeature" &&
                        typeof(UnityEngine.Rendering.Universal.ScriptableRendererFeature).IsAssignableFrom(t))
                    { featureType = t; break; }
                }
                if (featureType != null) break;
            }
            if (featureType == null)
            {
                Debug.LogWarning("[Orion AR] ARBackgroundRendererFeature not found - is AR Foundation installed?");
                return 0;
            }

            int added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(path);
                if (data == null) continue;

                bool has = false;
                foreach (var f in data.rendererFeatures)
                    if (f != null && f.GetType() == featureType) { has = true; break; }
                if (has) continue;

                var feature = (UnityEngine.Rendering.Universal.ScriptableRendererFeature)
                    ScriptableObject.CreateInstance(featureType);
                feature.name = "ARBackgroundRendererFeature";
                AssetDatabase.AddObjectToAsset(feature, data);

                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
                {
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                }

                // The camera background is not drawn on the deferred path.
                var mode = so.FindProperty("m_RenderingMode");
                if (mode != null && mode.intValue == 1) mode.intValue = 0;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                added++;
            }

            if (added > 0) AssetDatabase.SaveAssets();
            return added;
        }

        static System.Type[] SafeTypes(System.Reflection.Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e)
            {
                var list = new System.Collections.Generic.List<System.Type>();
                foreach (var t in e.Types) if (t != null) list.Add(t);
                return list.ToArray();
            }
            catch { return new System.Type[0]; }
        }
    }
}
