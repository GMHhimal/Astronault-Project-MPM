using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Astronaut.MoonLanding.EditorTools
{
    /// <summary>
    /// Menu:  Astronaut Project ▸ Moon Landing ▸ Build Level
    /// Builds the complete, playable Moon Landing Challenge inside MoonLanding_Scene.
    /// Safe to run again: everything under "MoonLanding_Level" is regenerated, your Lander model is kept.
    /// </summary>
    public static class MoonLandingLevelBuilder
    {
        const string RootName = "MoonLanding_Level";
        const float LanderScale = 0.05f;           // glb is ~134 units tall → ≈ 6.7 m (real LM ≈ 7 m)
        const int Seed = 1969;
        static readonly Vector3 Approach = Vector3.forward;
        const int IgnoreRaycastLayer = 2;

        [MenuItem("Astronaut Project/Moon Landing/Build Level", false, 1)]
        public static void BuildLevel()
        {
            if (!EditorUtility.DisplayDialog("Build Moon Landing Level",
                "This will (re)generate the Moon Landing level inside MoonLanding_Scene:\n\n" +
                "• terrain, craters, rocks, sky, Earth, lighting & post-processing\n" +
                "• lander physics rig, VFX, audio, camera, HUD and game logic\n\n" +
                "Objects under '" + RootName + "' will be replaced. Continue?", "Build", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (scene.path != MoonPaths.Scene)
                {
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MoonPaths.Scene) != null)
                        scene = EditorSceneManager.OpenScene(MoonPaths.Scene, OpenSceneMode.Single);
                    else
                        Debug.LogWarning("[MoonLanding] " + MoonPaths.Scene + " not found — building into the open scene.");
                }

                Progress("Importing generated textures", 0.02f);
                AssetDatabase.Refresh();
                MoonAssetFactory.EnsureFolder(MoonPaths.Generated);
                MoonAssetFactory.ConfigureTextures();

                Progress("Creating materials", 0.08f);
                MoonAssetFactory.MaterialSet mats = MoonAssetFactory.CreateMaterials();
                MoonLandingLibrary library = MoonAssetFactory.CreateLibrary(mats);
                VolumeProfile profile = MoonAssetFactory.CreateVolumeProfile();

                Progress("Cleaning previous build", 0.12f);
                GameObject landerModel = RescueLanderModel();
                GameObject old = GameObject.Find(RootName);
                if (old != null) Object.DestroyImmediate(old);
                DisableLegacyObjects();

                var root = new GameObject(RootName).transform;
                var environment = new GameObject("Environment").transform; environment.SetParent(root, false);
                var gameplay = new GameObject("Gameplay").transform; gameplay.SetParent(root, false);
                var systems = new GameObject("Systems").transform; systems.SetParent(root, false);

                Progress("Generating lunar terrain (craters)…", 0.2f);
                MoonTerrainBuilder.Result terrain = MoonTerrainBuilder.Build(environment, mats, Approach, Seed);

                Progress("Scattering rocks and boulders", 0.45f);
                MoonRockBuilder.Build(environment, terrain.terrain, mats.rock, terrain.westCrater, Seed);

                Progress("Lighting, sky and Earth", 0.6f);
                Camera cam = SetupCamera();
                ReflectionProbe probe = SetupLighting(environment, mats, profile, cam);

                Progress("Landing zone", 0.66f);
                LandingZone zone = BuildLandingZone(gameplay, mats, library, terrain.terrain);

                Progress("Building lander rig", 0.72f);
                LanderRefs lander = BuildLander(gameplay, library, landerModel);
                zone.lander = lander.controller.transform;

                Progress("Camera, HUD and game logic", 0.9f);
                var rig = cam.GetComponent<LanderCameraRig>();
                if (rig == null) rig = cam.gameObject.AddComponent<LanderCameraRig>();
                rig.target = lander.controller;
                rig.cockpitAnchor = lander.cockpitAnchor;
                rig.towerAnchor = zone.transform.Find("TowerCameraAnchor");

                var systemsGo = systems.gameObject;
                var env = systemsGo.AddComponent<LunarEnvironment>();
                env.reflectionProbe = probe;

                var guidance = systemsGo.AddComponent<LandingGuidance>();
                guidance.lander = lander.controller;
                guidance.evaluator = lander.evaluator;
                guidance.landingZone = zone;

                var score = systemsGo.AddComponent<MoonLandingScore>();
                score.lander = lander.controller;
                score.evaluator = lander.evaluator;
                score.landingZone = zone;
                score.guidance = guidance;

                var gm = systemsGo.AddComponent<MoonLandingGameManager>();
                gm.guidance = guidance;
                gm.score = score;
                gm.lander = lander.controller;
                gm.evaluator = lander.evaluator;
                gm.destruction = lander.destruction;
                gm.landerAudio = lander.audio;
                gm.sparks = lander.sparks;
                gm.cameraRig = rig;
                gm.landingZone = zone;
                gm.library = library;
                gm.approachDirection = Approach;
                gm.difficulties = MoonLandingGameManager.DefaultPresets();

                var hud = systemsGo.AddComponent<LanderHUD>();
                hud.game = gm;
                hud.viewCamera = cam;

                // place the lander at the PILOT start so the scene view looks right
                DifficultyPreset p = gm.difficulties[1];
                Vector3 start = -Approach * p.startDistance;
                start.y = MoonTerrainBuilder.GroundHeight(terrain.terrain, start) + p.startAltitude;
                lander.controller.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Approach));
                cam.transform.position = start + new Vector3(0f, 8f, -32f);
                cam.transform.LookAt(start + Vector3.up * 3f);

                AddSceneToBuildSettings(scene.path);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                Selection.activeGameObject = lander.controller.gameObject;
                Debug.Log("[MoonLanding] Level built successfully. Press Play! (Menu: Astronaut Project ▸ Moon Landing ▸ Build Level)");
                EditorUtility.DisplayDialog("Moon Landing", "Level built and scene saved.\n\nPress Play to fly.", "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Moon Landing – build failed", e.Message + "\n\nSee the Console for details.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Astronaut Project/Moon Landing/Open Scene", false, 20)]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(MoonPaths.Scene, OpenSceneMode.Single);
        }

        const string FlipPrefKey = "Astronaut.MoonLanding.ForceFlipLander";

        [MenuItem("Astronaut Project/Moon Landing/Options/Force Flip Lander Model", false, 60)]
        static void ToggleFlip()
        {
            EditorPrefs.SetBool(FlipPrefKey, !EditorPrefs.GetBool(FlipPrefKey, false));
            Debug.Log("[MoonLanding] Force flip lander = " + EditorPrefs.GetBool(FlipPrefKey, false) + ". Run Build Level again.");
        }

        [MenuItem("Astronaut Project/Moon Landing/Options/Force Flip Lander Model", true)]
        static bool ToggleFlipValidate()
        {
            Menu.SetChecked("Astronaut Project/Moon Landing/Options/Force Flip Lander Model", EditorPrefs.GetBool(FlipPrefKey, false));
            return true;
        }

        [MenuItem("Astronaut Project/Moon Landing/Reset Best Score", false, 40)]
        public static void ResetBestScore()
        {
            PlayerPrefs.DeleteKey(MoonLandingEvents.BestScoreKey);
            PlayerPrefs.DeleteKey(MoonLandingEvents.CompletedKey);
            Debug.Log("[MoonLanding] Best score reset.");
        }

        static void Progress(string msg, float t)
        {
            EditorUtility.DisplayProgressBar("Building Moon Landing Level", msg, t);
        }

        // ================================================================= scene housekeeping
        static IEnumerable<GameObject> AllSceneObjects()
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject);
        }

        /// <summary>Keeps the user's Lander prefab instance alive across rebuilds.</summary>
        static GameObject RescueLanderModel()
        {
            GameObject model = AllSceneObjects().FirstOrDefault(g => g.name == "Lander" && g.GetComponentInChildren<Renderer>(true) != null
                                                                   && g.GetComponent<LanderController>() == null);
            if (model != null)
            {
                model.transform.SetParent(null, true);
                return model;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoonPaths.LanderModel);
            if (prefab == null)
                throw new System.Exception("Lander model not found at " + MoonPaths.LanderModel +
                                           ". Make sure Git LFS files are pulled and glTFast is installed.");
            model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Lander";
            return model;
        }

        static void DisableLegacyObjects()
        {
            foreach (GameObject g in AllSceneObjects().ToList())
            {
                if (g == null) continue;
                bool legacyMoonSphere = g.name == "Moon" && g.transform.parent != null && g.transform.parent.name == "Environment"
                                        && g.transform.root.name != RootName;
                bool legacySurface = g.name == "LandingSurface";
                if (legacyMoonSphere || legacySurface)
                {
                    g.SetActive(false);
                    Debug.Log("[MoonLanding] Disabled legacy object '" + g.name + "' (kept in scene, not deleted).");
                }
            }
        }

        static void AddSceneToBuildSettings(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[MoonLanding] Added " + path + " to Build Profiles scene list.");
        }

        // ================================================================= camera & lighting
        static Camera SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100000f; // horizon ring reaches 95 km (beyond the lunar horizon)
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;
            data.stopNaN = true;
            return cam;
        }

        static ReflectionProbe SetupLighting(Transform environment, MoonAssetFactory.MaterialSet mats, VolumeProfile profile, Camera cam)
        {
            var lighting = new GameObject("Lighting").transform;
            lighting.SetParent(environment, false);

            // --- Sun: harsh, low (Apollo 11 landed with the sun ~10° above the horizon), no atmosphere
            Light sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional && l.transform.root.name != RootName);
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.gameObject.name = "Sun";
            sun.transform.SetParent(lighting, true);
            sun.transform.rotation = Quaternion.Euler(17f, 55f, 0f);
            sun.color = new Color(1f, 0.985f, 0.96f);
            sun.intensity = 2.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sun.gameObject.SetActive(true);

            // --- Regolith bounce light (cheap stand-in for GI: sunlit soil lights the lander's underside)
            var bounce = new GameObject("RegolithBounce").AddComponent<Light>();
            bounce.transform.SetParent(lighting, false);
            bounce.type = LightType.Directional;
            bounce.transform.rotation = Quaternion.Euler(-65f, 55f + 180f, 0f);
            bounce.color = new Color(0.78f, 0.74f, 0.69f);
            bounce.intensity = 0.14f;
            bounce.shadows = LightShadows.None;

            RenderSettings.sun = sun;
            RenderSettings.skybox = mats.sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.010f, 0.012f, 0.018f);
            RenderSettings.ambientEquatorColor = new Color(0.028f, 0.028f, 0.03f);
            RenderSettings.ambientGroundColor = new Color(0.075f, 0.072f, 0.068f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.6f;

            // --- Reflection probe around the landing site (gold foil & metal need something to reflect)
            var probeGo = new GameObject("LandingSiteReflectionProbe");
            probeGo.transform.SetParent(lighting, false);
            probeGo.transform.position = new Vector3(0f, 6f, 0f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(1200f, 400f, 1200f);
            probe.resolution = 256;
            probe.hdr = true;
            probe.farClipPlane = 3000f;

            // --- Post processing
            var volumeGo = new GameObject("PostProcessVolume");
            volumeGo.transform.SetParent(lighting, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            // --- Earth (lit by the real sun → correct phase)
            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earth.name = "Earth";
            earth.transform.SetParent(environment, false);
            Object.DestroyImmediate(earth.GetComponent<Collider>());
            earth.transform.localScale = Vector3.one * 780f;
            earth.transform.rotation = Quaternion.Euler(-23.4f, 0f, 0f);
            var er = earth.GetComponent<MeshRenderer>();
            er.sharedMaterial = mats.earth;
            er.shadowCastingMode = ShadowCastingMode.Off;
            er.receiveShadows = false;
            var follower = earth.AddComponent<SkyBodyFollower>();
            follower.viewer = cam.transform;
            follower.direction = Quaternion.Euler(-34f, 18f, 0f) * Approach;
            follower.distance = 14000f;
            earth.transform.position = follower.direction.normalized * follower.distance;

            return probe;
        }

        // ================================================================= landing zone
        static LandingZone BuildLandingZone(Transform parent, MoonAssetFactory.MaterialSet mats, MoonLandingLibrary lib, Terrain terrain)
        {
            Vector3 center = Vector3.zero;
            center.y = MoonTerrainBuilder.GroundHeight(terrain, center);

            var go = new GameObject("LandingZone");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var zone = go.AddComponent<LandingZone>();
            zone.radius = 15f;

            var rings = new List<LineRenderer>
            {
                Ring(go.transform, "Ring_Outer", zone.radius, 0.35f, mats.hologram),
                Ring(go.transform, "Ring_Inner", zone.radius * 0.35f, 0.18f, mats.hologram),
            };
            rings.Add(Line(go.transform, "Cross_A", new Vector3(-zone.radius * 0.2f, 0f, 0f), new Vector3(zone.radius * 0.2f, 0f, 0f), 0.14f, mats.hologram));
            rings.Add(Line(go.transform, "Cross_B", new Vector3(0f, 0f, -zone.radius * 0.2f), new Vector3(0f, 0f, zone.radius * 0.2f), 0.14f, mats.hologram));
            zone.rings = rings.ToArray();

            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 pos = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (zone.radius + 1.5f);
                pos.y = MoonTerrainBuilder.GroundHeight(terrain, pos);

                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "BeaconPost_" + i;
                post.transform.SetParent(go.transform, true);
                post.transform.position = pos + Vector3.up * 0.6f;
                post.transform.localScale = new Vector3(0.12f, 0.6f, 0.12f);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.GetComponent<MeshRenderer>().sharedMaterial = mats.lzPost;

                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = "BeaconLamp_" + i;
                lamp.transform.SetParent(go.transform, true);
                lamp.transform.position = pos + Vector3.up * 1.3f;
                lamp.transform.localScale = Vector3.one * 0.28f;
                Object.DestroyImmediate(lamp.GetComponent<Collider>());
                var lr = lamp.GetComponent<MeshRenderer>();
                lr.sharedMaterial = mats.beacon;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                var blink = lamp.AddComponent<BeaconBlink>();
                blink.phase = i * 0.25f;
            }

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "BeaconBeam";
            beam.transform.SetParent(go.transform, false);
            beam.transform.localPosition = new Vector3(0f, 90f, 0f);
            beam.transform.localScale = new Vector3(2.4f, 90f, 2.4f);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            var br = beam.GetComponent<MeshRenderer>();
            br.sharedMaterial = mats.beam;
            br.shadowCastingMode = ShadowCastingMode.Off;
            br.receiveShadows = false;
            zone.beam = br;

            var tower = new GameObject("TowerCameraAnchor");
            tower.transform.SetParent(go.transform, false);
            Vector3 towerPos = center + (Quaternion.Euler(0f, 70f, 0f) * -Approach) * 70f;
            towerPos.y = MoonTerrainBuilder.GroundHeight(terrain, towerPos) + 2.2f;
            tower.transform.position = towerPos;

            return zone;
        }

        static LineRenderer Ring(Transform parent, string name, float radius, float width, Material mat)
        {
            const int segments = 96;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.alignment = LineAlignment.TransformZ;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            SetupLine(lr, width, mat);
            return lr;
        }

        static LineRenderer Line(Transform parent, string name, Vector3 a, Vector3 b, float width, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(a.x, a.z, 0f));
            lr.SetPosition(1, new Vector3(b.x, b.z, 0f));
            SetupLine(lr, width, mat);
            return lr;
        }

        static void SetupLine(LineRenderer lr, float width, Material mat)
        {
            lr.widthMultiplier = width;
            lr.sharedMaterial = mat;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            lr.startColor = new Color(0.35f, 0.9f, 1f);
            lr.endColor = new Color(0.35f, 0.9f, 1f);
        }

        // ================================================================= lander
        class LanderRefs
        {
            public LanderController controller;
            public LandingEvaluator evaluator;
            public LanderDestruction destruction;
            public LanderAudio audio;
            public SparkEmitter sparks;
            public Transform cockpitAnchor;
        }

        struct ModelStats
        {
            public Bounds bounds;
            public float bottomSpread, topSpread;
            public List<Vector3> bottomPoints;
            public bool fromVertices;
        }

        static LanderRefs BuildLander(Transform parent, MoonLandingLibrary lib, GameObject model)
        {
            var rigGo = new GameObject("LanderRig");
            rigGo.transform.SetParent(parent, false);
            Transform rig = rigGo.transform;

            model.transform.SetParent(rig, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity; // glTF is Y-up: the model is upright without extra rotation
            model.transform.localScale = Vector3.one * LanderScale;

            ModelStats stats = Measure(rig, model);
            bool autoFlip = stats.fromVertices && stats.topSpread > stats.bottomSpread * 1.15f;
            if (autoFlip != EditorPrefs.GetBool(FlipPrefKey, false))
            {
                // legs are on top → flip upright
                model.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                stats = Measure(rig, model);
            }
            model.transform.localPosition = new Vector3(-stats.bounds.center.x, -stats.bounds.min.y, -stats.bounds.center.z);
            stats = Measure(rig, model);

            SetLayer(model, IgnoreRaycastLayer);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            float H = stats.bounds.size.y;
            float W = stats.bounds.size.x;
            float D = stats.bounds.size.z;

            // ---- colliders
            var colRoot = Child(rig, "Colliders", Vector3.zero);
            colRoot.gameObject.layer = IgnoreRaycastLayer;
            Vector3[] pads = FindFootPads(stats);
            float padRadius = Mathf.Clamp(H * 0.06f, 0.25f, 0.6f);
            var feet = new List<Collider>();
            for (int i = 0; i < pads.Length; i++)
            {
                Transform t = Child(colRoot, "FootPad_" + i, new Vector3(pads[i].x, padRadius, pads[i].z));
                var sc = t.gameObject.AddComponent<SphereCollider>();
                sc.radius = padRadius;
                feet.Add(sc);
            }
            var bodies = new List<Collider>();
            bodies.Add(Box(colRoot, "DescentStage", new Vector3(0f, 0.30f * H, 0f), new Vector3(0.46f * W, 0.26f * H, 0.46f * D)));
            bodies.Add(Box(colRoot, "AscentStage", new Vector3(0f, 0.63f * H, 0f), new Vector3(0.40f * W, 0.36f * H, 0.40f * D)));
            SetLayer(colRoot.gameObject, IgnoreRaycastLayer);

            // ---- rigidbody
            var rb = rigGo.AddComponent<Rigidbody>();
            rb.mass = 7500f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.useGravity = true;
            rb.isKinematic = true; // released by the game manager at mission start

            // ---- anchors
            Transform nozzle = Child(rig, "EngineNozzle", new Vector3(0f, 0.10f * H, 0f));
            nozzle.localRotation = Quaternion.Euler(90f, 0f, 0f); // +Z points down the nozzle
            Transform cockpit = Child(rig, "CockpitCameraAnchor", new Vector3(0f, 0.80f * H, 0.24f * D));
            cockpit.localRotation = Quaternion.Euler(24f, 0f, 0f);

            // ---- components
            rigGo.AddComponent<LanderInput>();
            var controller = rigGo.AddComponent<LanderController>();
            controller.engineNozzle = nozzle;
            controller.centerOfMassLocal = new Vector3(0f, 0.35f * H, 0f);

            var evaluator = rigGo.AddComponent<LandingEvaluator>();
            evaluator.footColliders = feet.ToArray();
            evaluator.bodyColliders = bodies.ToArray();

            // ---- VFX
            Transform vfx = Child(rig, "VFX", Vector3.zero);
            var effects = rigGo.AddComponent<LanderEffects>();
            effects.lander = controller;
            effects.plume = MoonVFXFactory.CreatePlume(lib, nozzle);
            effects.plumeCore = MoonVFXFactory.CreatePlumeCore(lib, nozzle);

            var engineLight = new GameObject("EngineLight").AddComponent<Light>();
            engineLight.transform.SetParent(nozzle, false);
            engineLight.transform.localPosition = new Vector3(0f, 0f, 2f);
            engineLight.type = LightType.Point;
            engineLight.color = new Color(1f, 0.62f, 0.35f);
            engineLight.range = 30f;
            engineLight.intensity = 0f;
            engineLight.shadows = LightShadows.None;
            effects.engineLight = engineLight;

            Transform dust = Child(vfx, "DustEmitter", new Vector3(0f, -10f, 0f));
            ParticleSystem sheet, haze, streaks;
            MoonVFXFactory.CreateDust(lib, dust, out sheet, out haze, out streaks);
            effects.dustEmitter = dust;
            effects.dustSheet = sheet;
            effects.dustHaze = haze;
            effects.dustStreaks = streaks;

            var quads = new List<ParticleSystem>();
            float qr = 0.22f;
            quads.Add(MoonVFXFactory.CreateRcsQuad(lib, vfx, "RCS_Quad_1", new Vector3(qr * W, 0.66f * H, qr * D)));
            quads.Add(MoonVFXFactory.CreateRcsQuad(lib, vfx, "RCS_Quad_2", new Vector3(-qr * W, 0.66f * H, qr * D)));
            quads.Add(MoonVFXFactory.CreateRcsQuad(lib, vfx, "RCS_Quad_3", new Vector3(-qr * W, 0.66f * H, -qr * D)));
            quads.Add(MoonVFXFactory.CreateRcsQuad(lib, vfx, "RCS_Quad_4", new Vector3(qr * W, 0.66f * H, -qr * D)));
            effects.rcsQuads = quads.ToArray();

            Renderer[] modelRenderers = model.GetComponentsInChildren<Renderer>(true);

            var sparks = rigGo.AddComponent<SparkEmitter>();
            sparks.sparkPool = MoonVFXFactory.CreateSparkPool(lib, vfx);
            sparks.modelRenderers = modelRenderers;
            var sparkLight = new GameObject("SparkLight").AddComponent<Light>();
            sparkLight.transform.SetParent(vfx, false);
            sparkLight.type = LightType.Point;
            sparkLight.color = new Color(0.7f, 0.85f, 1f);
            sparkLight.range = 12f;
            sparkLight.intensity = 0f;
            sparkLight.shadows = LightShadows.None;
            sparkLight.enabled = false;
            sparks.sparkLight = sparkLight;

            var audio = rigGo.AddComponent<LanderAudio>();
            audio.library = lib;
            audio.lander = controller;
            audio.effects = effects;
            audio.evaluator = evaluator;
            sparks.landerAudio = audio;

            var destruction = rigGo.AddComponent<LanderDestruction>();
            destruction.library = lib;
            destruction.modelRenderers = modelRenderers;
            destruction.sparks = sparks;
            destruction.landerAudio = audio;

            SetLayer(vfx.gameObject, IgnoreRaycastLayer);
            rigGo.layer = IgnoreRaycastLayer;

            Debug.Log(string.Format("[MoonLanding] Lander rig: height {0:0.00} m, width {1:0.00} m, {2} foot pads ({3}).",
                H, W, pads.Length, stats.fromVertices ? "from mesh vertices" : "estimated from bounds"));

            return new LanderRefs
            {
                controller = controller,
                evaluator = evaluator,
                destruction = destruction,
                audio = audio,
                sparks = sparks,
                cockpitAnchor = cockpit,
            };
        }

        static Transform Child(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static BoxCollider Box(Transform parent, string name, Vector3 center, Vector3 size)
        {
            Transform t = Child(parent, name, Vector3.zero);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            return box;
        }

        static void SetLayer(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static ModelStats Measure(Transform rig, GameObject model)
        {
            var stats = new ModelStats { bottomPoints = new List<Vector3>() };
            var points = new List<Vector3>();

            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                Vector3[] verts;
                try { verts = mesh.vertices; }
                catch { continue; }
                if (verts == null || verts.Length == 0) continue;
                int step = Mathf.Max(1, verts.Length / 30000);
                for (int i = 0; i < verts.Length; i += step)
                    points.Add(rig.InverseTransformPoint(mf.transform.TransformPoint(verts[i])));
            }

            if (points.Count > 100)
            {
                stats.fromVertices = true;
                var b = new Bounds(points[0], Vector3.zero);
                foreach (Vector3 p in points) b.Encapsulate(p);
                stats.bounds = b;
                float h = b.size.y;
                Vector3 c = b.center;
                foreach (Vector3 p in points)
                {
                    float r = new Vector2(p.x - c.x, p.z - c.z).magnitude;
                    if (p.y < b.min.y + h * 0.08f) stats.bottomSpread = Mathf.Max(stats.bottomSpread, r);
                    if (p.y > b.max.y - h * 0.08f) stats.topSpread = Mathf.Max(stats.topSpread, r);
                    if (p.y < b.min.y + h * 0.04f) stats.bottomPoints.Add(p);
                }
                return stats;
            }

            // Fallback: renderer bounds (mesh not readable)
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new System.Exception("Lander model has no renderers.");
            bool first = true;
            var bounds = new Bounds();
            foreach (Renderer r in renderers)
            {
                Bounds wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 local = rig.InverseTransformPoint(corner);
                    if (first) { bounds = new Bounds(local, Vector3.zero); first = false; }
                    else bounds.Encapsulate(local);
                }
            }
            stats.bounds = bounds;
            return stats;
        }

        /// <summary>Clusters the lowest vertices into 4 foot pads (works for any yaw of the model).</summary>
        static Vector3[] FindFootPads(ModelStats stats)
        {
            Bounds b = stats.bounds;
            Vector3 c = b.center;
            if (stats.fromVertices && stats.bottomPoints.Count >= 8)
            {
                Vector3 far = stats.bottomPoints.OrderByDescending(p => new Vector2(p.x - c.x, p.z - c.z).sqrMagnitude).First();
                float baseAngle = Mathf.Atan2(far.z - c.z, far.x - c.x);
                var sums = new Vector3[4];
                var counts = new int[4];
                foreach (Vector3 p in stats.bottomPoints)
                {
                    float a = Mathf.Atan2(p.z - c.z, p.x - c.x) - baseAngle;
                    int k = Mathf.RoundToInt(Mathf.Repeat(a, Mathf.PI * 2f) / (Mathf.PI * 0.5f)) % 4;
                    sums[k] += p;
                    counts[k]++;
                }
                if (counts.All(n => n > 0))
                {
                    var pads = new Vector3[4];
                    for (int i = 0; i < 4; i++) { pads[i] = sums[i] / counts[i]; pads[i].y = b.min.y; }
                    return pads;
                }
            }
            // Fallback: Apollo LM legs sit on the diagonals
            float ex = b.extents.x * 0.85f, ez = b.extents.z * 0.85f;
            return new[]
            {
                new Vector3(c.x + ex, b.min.y, c.z + ez), new Vector3(c.x - ex, b.min.y, c.z + ez),
                new Vector3(c.x - ex, b.min.y, c.z - ez), new Vector3(c.x + ex, b.min.y, c.z - ez),
            };
        }
    }
}
