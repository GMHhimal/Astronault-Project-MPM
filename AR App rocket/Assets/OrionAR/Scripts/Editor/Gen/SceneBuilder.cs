using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace OrionAR.Gen
{
    /// <summary>
    /// Assembles both scenes and every screen from code.
    ///
    /// Generating the scenes rather than hand-placing them means the whole project is
    /// reproducible from source: a supervisor can rebuild it with one menu item, and
    /// the scene file is never the single point of failure in the submission.
    /// </summary>
    public static class SceneBuilder
    {
        public const string SceneFolder = "Assets/OrionAR/Scenes";
        const string MenuScenePath = SceneFolder + "/01_MainMenu.unity";
        const string ARScenePath = SceneFolder + "/02_LunarExplorer.unity";

        // ================================================================
        // Menu items
        // ================================================================

        [MenuItem("Orion AR/Build Everything", priority = 0)]
        public static void BuildEverything()
        {
            // Scenes cannot be created or saved while the editor is playing; the failure
            // leaves a half-built Untitled scene that looks like a broken app.
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Orion AR",
                    "Stop Play mode before building.\n\nUnity cannot create or save scenes " +
                    "while the editor is playing.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!EditorUtility.DisplayDialog("Build Orion AR",
                    "This regenerates all textures, meshes, audio, content assets and both " +
                    "scenes.\n\nAny hand edits to those will be replaced.\n\nContinue?",
                    "Build", "Cancel"))
                return;

            RunBuild();
        }

        /// <summary>The build itself, without dialogs, so it can also run from batch mode.</summary>
        public static bool RunBuild()
        {
            string stage = "start";
            try
            {
                MatLib2.ClearCache();

                PlanetBuilder.ResetCache();

                stage = "creating asset folders";
                Report(stage, 0.02f);
                AssetPaths.EnsureAllProjectFolders();
                AssetPaths.ClearFolder(OrionBuilder.MeshFolder);

                stage = "generating textures";
                Report(stage, 0.06f);
                ProcTexture2.GenerateAll();

                stage = "generating audio";
                Report(stage, 0.22f);
                AudioGen.GenerateAll();

                stage = "building fonts";
                Report(stage, 0.28f);
                FontBuilder.BuildAll();

                stage = "configuring Android player settings";
                AndroidSetup.Apply(verbose: false);

                stage = "authoring content";
                Report(stage, 0.32f);
                var parts = ContentBuilder.BuildParts();
                var quiz = ContentBuilder.BuildQuiz();
                if (parts == null || parts.Count == 0)
                    throw new System.Exception("No PartInfo assets were created.");

                stage = "preparing UI sprites";
                AssetPaths.ClearFolder(UISpriteStore.Folder);
                UISpriteStore.Begin();

                stage = "building AR scene";
                Report(stage, 0.40f);
                BuildARScene(parts, quiz);

                stage = "building menu scene";
                Report(stage, 0.85f);
                BuildMenuScene(parts);

                stage = "saving assets";
                Report(stage, 0.95f);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                stage = "setting scene list";
                OrionAR.Gen.BuildVerifier.SetSceneList();
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("[Orion AR] BUILD FAILED while " + stage + ".\n\n" +
                               e.GetType().Name + ": " + e.Message + "\n\n" + e.StackTrace);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Orion AR \u2014 Build Failed",
                        "The build failed while " + stage + ".\n\n" + e.Message +
                        "\n\nSee the Console for the full stack trace.", "OK");
                return false;
            }
            finally
            {
                UISpriteStore.End();
                EditorUtility.ClearProgressBar();
            }

            if (TMP_Settings.defaultFontAsset == null)
                Debug.LogWarning("[Orion AR] No TMP font found. Run Window > TextMeshPro > " +
                                 "Import TMP Essential Resources, then rebuild.");

            Debug.Log("[Orion AR] BUILD COMPLETE. Scenes: " + MenuScenePath + ", " + ARScenePath +
                      ". Scene list set with 01_MainMenu first.");

            EditorSceneManager.OpenScene(MenuScenePath);
            return true;
        }

        static void Report(string stage, float progress)
        {
            Debug.Log("[Orion AR] " + stage + "\u2026");
            EditorUtility.DisplayProgressBar("Orion AR",
                char.ToUpper(stage[0]) + stage.Substring(1) + "\u2026", progress);
        }

        [MenuItem("Orion AR/Regenerate Content Only", priority = 20)]
        public static void ContentOnly()
        {
            ContentBuilder.BuildParts();
            ContentBuilder.BuildQuiz();
            AssetDatabase.SaveAssets();
            Debug.Log("[Orion AR] Content assets regenerated.");
        }

        [MenuItem("Orion AR/Regenerate Textures Only", priority = 21)]
        public static void TexturesOnly()
        {
            ProcTexture2.GenerateAll();
            Debug.Log("[Orion AR] Textures regenerated.");
        }

        [MenuItem("Orion AR/Regenerate Audio Only", priority = 22)]
        public static void AudioOnly()
        {
            AudioGen.GenerateAll();
        }

        // ================================================================
        // AR scene
        // ================================================================

        static void BuildARScene(Dictionary<string, PartInfo> parts, QuizData quiz)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- AR session ------------------------------------------
            var sessionGO = new GameObject("AR Session");
            var session = sessionGO.AddComponent<UnityEngine.XR.ARFoundation.ARSession>();
            sessionGO.AddComponent<UnityEngine.XR.ARFoundation.ARInputManager>();

            var originGO = new GameObject("XR Origin");
            var origin = originGO.AddComponent<Unity.XR.CoreUtils.XROrigin>();

            var offsetGO = new GameObject("Camera Offset");
            offsetGO.transform.SetParent(originGO.transform, false);

            var camGO = new GameObject("AR Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(offsetGO.transform, false);

            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 900f;
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
            camGO.AddComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
            AddPoseDriver(camGO);

            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offsetGO;

            var planeManager = originGO.AddComponent<UnityEngine.XR.ARFoundation.ARPlaneManager>();
            var raycastManager = originGO.AddComponent<UnityEngine.XR.ARFoundation.ARRaycastManager>();
            var anchorManager = originGO.AddComponent<UnityEngine.XR.ARFoundation.ARAnchorManager>();

            // ---- Content root ----------------------------------------
            var contentRoot = new GameObject("ContentRoot");
            contentRoot.transform.localScale = Vector3.one * 0.05f;

            var orion = OrionBuilder.Build(parts, contentRoot.transform);
            orion.transform.localPosition = Vector3.zero;

            var moon = MoonBuilder.BuildSurface(260f, 170, 1969);
            moon.transform.SetParent(contentRoot.transform, false);
            moon.transform.localPosition = new Vector3(0f, -16f, 0f);

            // The sun and Earthshine must not live under the surface: the finale
            // shrinks the surface away, and the spacecraft went black with it.
            foreach (var lightName in new[] { "Sun", "Earthshine" })
            {
                var lt = moon.transform.Find(lightName);
                if (lt != null) lt.SetParent(contentRoot.transform, false);
            }

            var stars = MoonBuilder.BuildStarfield(contentRoot.transform, 400f);

            // ---- Placement reticle -----------------------------------
            var reticle = BuildReticle();

            // ---- UI ---------------------------------------------------
            var canvasGO = NewCanvas("UI Canvas", out var canvas, out var scaler);
            var ui = SceneBuilderUI.BuildARUI(canvasGO.transform, quiz);

            // ---- Audio ------------------------------------------------
            var audioGO = new GameObject("AudioDirector");
            var audio = audioGO.AddComponent<AudioDirector>();
            audio.clips = new List<AudioDirector.NamedClip>();
            foreach (var key in AudioGen.AllKeys())
            {
                var clip = AudioGen.Load(key);
                if (clip != null)
                    audio.clips.Add(new AudioDirector.NamedClip { key = key, clip = clip });
            }

            var narrationGO = new GameObject("Narration");
            narrationGO.transform.SetParent(audioGO.transform, false);
            var narration = narrationGO.AddComponent<AudioSource>();
            narration.playOnAwake = false;
            narration.spatialBlend = 0f;

            // ---- Systems ----------------------------------------------
            var systemsGO = new GameObject("Systems");

            var placement = systemsGO.AddComponent<ARPlacementController>();
            placement.arSession = session;
            placement.planeManager = planeManager;
            placement.raycastManager = raycastManager;
            placement.anchorManager = anchorManager;
            placement.arCamera = cam;
            placement.contentRoot = contentRoot.transform;
            placement.placementReticle = reticle;

            var scaleCtrl = systemsGO.AddComponent<ModelScaleController>();
            scaleCtrl.target = contentRoot.transform;
            scaleCtrl.arCamera = cam;

            var selector = systemsGO.AddComponent<TouchSelector>();
            selector.rayCamera = cam;
            selector.scaleController = scaleCtrl;

            var tutorial = systemsGO.AddComponent<TutorialDirector>();
            tutorial.narrationSource = narration;

            var finaleGO = new GameObject("Finale");
            var finale = BuildFinale(finaleGO, contentRoot.transform, orion, moon, cam, ui);

            var director = systemsGO.AddComponent<ExperienceDirector>();
            director.placement = placement;
            director.scaleController = scaleCtrl;
            director.selector = selector;
            director.tutorial = tutorial;
            director.quiz = ui.quiz;
            director.finale = finale;
            director.infoSheet = ui.sheet;
            director.checklist = ui.checklist;
            director.progressRing = ui.ring;
            director.explorerHud = ui.explorerGroup;
            director.placementHud = ui.placementHud;
            director.coachText = ui.coachText;
            director.scaleLabel = ui.scaleLabel;
            director.scaleSlider = ui.scaleSlider;
            director.placeButton = ui.placeButton;
            director.checklistButton = ui.checklistButton;
            director.repositionButton = ui.repositionButton;
            director.continueButton = ui.continueButton;
            director.soundButton = ui.soundButton;
            director.scaleBar = ui.scaleBar;
            director.contentRoot = contentRoot.transform;
            director.lunarSurface = moon;
            director.orbitBackdrop = stars;

            // Buttons are wired by the director at runtime: listeners added here in the
            // Editor are not serialised and silently vanish when the scene is saved.
            director.presetButtons = ui.presetButtons;
            director.cabinButton = ui.cabinButton;
            director.cabinButtonLabel = ui.cabinButtonLabel;
            director.ringButton = ui.ringButton;
            director.cabinView = orion.GetComponent<CabinView>();

            AssetPaths.EnsureFolder(SceneFolder);
            if (!EditorSceneManager.SaveScene(scene, ARScenePath))
                throw new System.Exception("Could not save " + ARScenePath);
        }

        /// <summary>
        /// Drives the AR camera from the device pose.
        ///
        /// Bindings are set explicitly rather than left to defaults: a TrackedPoseDriver
        /// added from code has no actions assigned, and the symptom is a camera that
        /// renders the passthrough feed but never moves - which looks like broken
        /// tracking rather than a missing binding.
        /// </summary>
        static void AddPoseDriver(GameObject camGO)
        {
            var tpd = camGO.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var position = new InputAction("Camera Position", InputActionType.Value,
                                           expectedControlType: "Vector3");
            position.AddBinding("<XRHMD>/centerEyePosition");
            position.AddBinding("<HandheldARInputDevice>/devicePosition");

            var rotation = new InputAction("Camera Rotation", InputActionType.Value,
                                           expectedControlType: "Quaternion");
            rotation.AddBinding("<XRHMD>/centerEyeRotation");
            rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");

            var trackingState = new InputAction("Tracking State", InputActionType.Value,
                                                expectedControlType: "Integer");
            trackingState.AddBinding("<XRHMD>/trackingState");
            trackingState.AddBinding("<HandheldARInputDevice>/trackingState");

            tpd.positionInput = new InputActionProperty(position);
            tpd.rotationInput = new InputActionProperty(rotation);
            tpd.trackingStateInput = new InputActionProperty(trackingState);
        }

        static GameObject BuildReticle()
        {
            var root = new GameObject("PlacementReticle");

            var ringMesh = ProcMesh2.Lathe("ReticleRing", new List<ProcMesh2.ProfilePoint>
            {
                ProcMesh2.P(0.22f, 0f, true),
                ProcMesh2.P(0.26f, 0f),
                ProcMesh2.P(0.26f, 0.004f),
                ProcMesh2.P(0.22f, 0.004f, true),
            }, 48);
            AssetPaths.CreateAsset(ringMesh, OrionBuilder.MeshFolder + "/ReticleRing.asset");

            var mat = MatLib2.Emissive("M_Reticle", new Color(0.25f, 0.85f, 0.92f), 2.2f);

            var ring = new GameObject("Ring");
            ring.transform.SetParent(root.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            var mr = ring.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Cross-hairs through the centre give the eye something to register on
            // featureless flooring, where a bare ring is hard to judge.
            for (int i = 0; i < 2; i++)
            {
                var barMesh = ProcMesh2.BeveledBox("ReticleBar",
                    i == 0 ? new Vector3(0.10f, 0.004f, 0.012f) : new Vector3(0.012f, 0.004f, 0.10f),
                    0.002f);
                AssetPaths.CreateAsset(barMesh, OrionBuilder.MeshFolder + "/ReticleBar_" + i + ".asset");

                var bar = new GameObject("Bar_" + i);
                bar.transform.SetParent(root.transform, false);
                bar.AddComponent<MeshFilter>().sharedMesh = barMesh;
                var bmr = bar.AddComponent<MeshRenderer>();
                bmr.sharedMaterial = mat;
                bmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            root.SetActive(false);
            return root;
        }

        static TEISequence BuildFinale(GameObject root, Transform contentRoot, GameObject orion,
                                       GameObject moon, Camera cam, ARUIRefs ui)
        {
            var finale = root.AddComponent<TEISequence>();
            finale.spacecraft = orion.transform;
            finale.moon = moon.transform;
            finale.sequenceCamera = cam;

            // Earth, revealed during the coast: the textured Blue Marble globe.
            var earth = PlanetBuilder.Earth("FinaleEarth", contentRoot);
            // Above and beyond the spacecraft as the user sees it (the content root's +Z
            // faces the viewer), so Orion climbs toward it. The earlier position was on
            // the viewer's side and ended up behind the camera.
            earth.transform.localPosition = new Vector3(-8f, 17f, -34f);
            earth.transform.localScale = Vector3.one * 5f;
            earth.transform.localRotation = Quaternion.Euler(-12f, 200f, 8f);
            earth.SetActive(false);
            finale.earth = earth.transform;

            // The finale flies away from a lunar globe built from the Earth's sphere.
            finale.moonGlobeMaterial = PlanetBuilder.SurfaceMaterial("M_MoonSurface", PlanetBuilder.MoonMap, 0.05f);
            finale.moonRimMaterial = PlanetBuilder.RimMaterial("M_MoonRim", new Color(0.55f, 0.6f, 0.7f), 0.35f, 4.5f);

            // Engine plume.
            var plume = BuildPlume(orion.transform);
            finale.enginePlume = new[] { plume };

            var glowGO = new GameObject("EngineGlow");
            glowGO.transform.SetParent(orion.transform, false);
            glowGO.transform.localPosition = new Vector3(0f, -1.6f, 0f);
            var glow = glowGO.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 14f;
            glow.color = new Color(1f, 0.62f, 0.28f);
            glow.intensity = 0f;
            finale.engineGlow = glow;

            finale.hudRoot = ui.finaleHud;
            finale.hudGroup = ui.finaleGroup;
            finale.phaseLabel = ui.finalePhase;
            finale.countdownText = ui.finaleCountdown;
            finale.deltaVText = ui.finaleDeltaV;
            finale.accelText = ui.finaleAccel;
            finale.massText = ui.finaleMass;
            finale.burnTimeText = ui.finaleBurnTime;
            finale.timeWarpText = ui.finaleTimeWarp;
            finale.burnProgressBar = ui.finaleProgress;
            finale.completeCard = ui.completeCard;
            finale.completeHeadline = ui.completeHeadline;
            finale.completeScore = ui.completeScore;
            finale.completeBody = ui.completeBody;
            finale.completeButton = ui.completeButton;

            return finale;
        }

        static ParticleSystem BuildPlume(Transform parent)
        {
            var go = new GameObject("EnginePlume");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -1.5f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startSpeed = 14f;
            main.startLifetime = 0.55f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.55f, 0.9f), new Color(1f, 0.55f, 0.2f, 0.75f));
            main.maxParticles = 160;        // kept low: this runs on a phone
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime = 110f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.4f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1.6f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f),
                        new GradientColorKey(new Color(1f, 0.45f, 0.15f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var pr = go.GetComponent<ParticleSystemRenderer>();
            // Soft additive points tinted by particle colour. The plain unlit material
            // drew every particle as a hard white square.
            var plumeShader = Shader.Find("OrionAR/StarPoint");
            if (plumeShader != null)
            {
                var plumeMat = MatLib2.WithShader("M_Plume", plumeShader);
                plumeMat.SetFloat("_Intensity", 1.25f);
                EditorUtility.SetDirty(plumeMat);
                pr.sharedMaterial = plumeMat;
            }
            else pr.sharedMaterial = MatLib2.UnlitColor("M_Plume", Color.white, transparent: true);
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Stop();
            return ps;
        }

        // ================================================================
        // Menu scene
        // ================================================================

        static void BuildMenuScene(Dictionary<string, PartInfo> parts)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UITheme.SpaceDeep;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1200f;
            cam.fieldOfView = 42f;
            camGO.transform.position = new Vector3(0f, 1.1f, -9.5f);
            camGO.transform.rotation = Quaternion.Euler(6f, 0f, 0f);
            camGO.AddComponent<AudioListener>();

            MoonBuilder.ApplyLunarEnvironment();
            var stars = MoonBuilder.BuildStarfield(null, 500f);

            // Hero model, slowly rotating.
            var heroPivot = new GameObject("HeroPivot");
            var heroBob = new GameObject("HeroBob");
            heroBob.transform.SetParent(heroPivot.transform, false);

            var hero = OrionBuilder.Build(parts, heroBob.transform);
            hero.transform.localPosition = new Vector3(0f, -4.6f, 0f);
            // Framed for a portrait phone: the stack fills the middle ~45% of the screen,
            // clear of the title block above and the buttons below.
            heroPivot.transform.position = new Vector3(0f, 0.45f, 0f);
            heroPivot.transform.localScale = Vector3.one * 0.34f;

            // Astronomy backdrop: the Moon rising in the lower corner, Earth small and
            // far above it - the journey the app ends with.
            var menuMoon = PlanetBuilder.Moon("BackdropMoon", null);
            menuMoon.transform.position = new Vector3(3.2f, -6.4f, 16f);
            menuMoon.transform.localScale = Vector3.one * 5.2f;
            menuMoon.transform.rotation = Quaternion.Euler(8f, 200f, 0f);
            menuMoon.AddComponent<SlowSpin>().degreesPerSecond = 0.6f;

            var menuEarth = PlanetBuilder.Earth("BackdropEarth", null);
            menuEarth.transform.position = new Vector3(-3.1f, 2.6f, 30f);
            menuEarth.transform.localScale = Vector3.one * 0.85f;
            menuEarth.transform.rotation = Quaternion.Euler(-20f, 160f, 10f);
            menuEarth.AddComponent<SlowSpin>().degreesPerSecond = 3f;

            // Strip interaction from the hero: the menu model is scenery.
            foreach (var p in hero.GetComponentsInChildren<SelectablePart>()) Object.DestroyImmediate(p);
            foreach (var c in hero.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var sunGO = new GameObject("KeyLight");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.98f, 0.95f);
            sun.shadows = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(18f, 36f, 0f);

            // Rim light: separates the silhouette from a near-black background, which
            // is the difference between a dramatic hero shot and a dark smudge.
            var rimGO = new GameObject("RimLight");
            var rim = rimGO.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.7f;
            rim.color = new Color(0.45f, 0.72f, 1f);
            rim.shadows = LightShadows.None;
            rimGO.transform.rotation = Quaternion.Euler(10f, -140f, 0f);

            var audioGO = new GameObject("AudioDirector");
            var audio = audioGO.AddComponent<AudioDirector>();
            audio.clips = new List<AudioDirector.NamedClip>();
            foreach (var key in AudioGen.AllKeys())
            {
                var clip = AudioGen.Load(key);
                if (clip != null)
                    audio.clips.Add(new AudioDirector.NamedClip { key = key, clip = clip });
            }

            var canvasGO = NewCanvas("UI Canvas", out _, out _);
            var menu = SceneBuilderUI.BuildMenuUI(canvasGO.transform);
            menu.heroModel = heroPivot.transform;
            menu.heroBob = heroBob.transform;

            AssetPaths.EnsureFolder(SceneFolder);
            if (!EditorSceneManager.SaveScene(scene, MenuScenePath))
                throw new System.Exception("Could not save " + MenuScenePath);
        }

        // ================================================================
        // Canvas scaffolding
        // ================================================================

        static GameObject NewCanvas(string name, out Canvas canvas, out CanvasScaler scaler)
        {
            var go = new GameObject(name, typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UITheme.ReferenceResolution;
            // Match height rather than width: phones vary far more in aspect ratio
            // than in usable vertical space, and this keeps type size consistent.
            // Match width: the layouts are authored for a 412 dp wide portrait phone,
            // and height is the dimension that varies (732 dp at 16:9, 915 at 20:9).
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                // The Input System UI module: the project uses the Input System only,
                // and StandaloneInputModule throws every frame under that setting, so
                // no button in the app could ever be pressed.
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                var module = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            var fonts = go.AddComponent<UIFonts>();
            fonts.display = UIKit.DisplayFont;
            fonts.heading = UIKit.HeadingFont;
            fonts.body = UIKit.BodyFont;
            fonts.icons = UIKit.IconFont;

            return go;
        }

        // ================================================================
        // UI construction is in SceneBuilderUI.cs
        // ================================================================
    }
}
