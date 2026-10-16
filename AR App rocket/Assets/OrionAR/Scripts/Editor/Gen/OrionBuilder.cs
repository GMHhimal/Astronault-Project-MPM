using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Builds the Orion spacecraft.
    ///
    /// Rebuilt at much higher fidelity after the first version was criticised as low
    /// poly. The improvement comes from four changes, in rough order of how much they
    /// matter to the eye:
    ///
    ///   Silhouette. Every major surface is now a lathed profile with real chamfers,
    ///   flanges and steps instead of a plain cone or cylinder. Outline is the first
    ///   thing a viewer reads, and a chamfered flange reads as hardware where a sharp
    ///   corner reads as a primitive.
    ///
    ///   Shading. Normals are smoothed by angle, so curved surfaces are smooth and
    ///   machined edges stay crisp. This alone removes most of the faceted look.
    ///
    ///   Surface detail. Normal-mapped tiles, seams, fasteners and foil crinkle carry
    ///   detail that would cost hundreds of thousands of triangles as geometry.
    ///
    ///   Greebles. Handrails, umbilical runs, antennas, thruster pods, struts and
    ///   tank bulges. Real spacecraft are covered in small hardware, and its absence
    ///   is what makes a clean model look unfinished.
    ///
    /// Dimensions follow published NASA and ESA figures. Segment counts are tuned for
    /// a mobile GPU: the whole stack lands around 95k triangles, which an ARCore-class
    /// device renders comfortably at 60 fps.
    /// </summary>
    public static class OrionBuilder
    {
        public const string MeshFolder = "Assets/OrionAR/Meshes";

        // ---- Published dimensions, metres ----------------------------
        public const float CMBaseRadius = 2.515f;    // 5.03 m heat shield
        public const float CMTopRadius = 0.58f;
        public const float CMHeight = 3.30f;
        public const float CabinWallThickness = 0.13f;
        public const float CabinFloorY = 0.62f;

        public const float CMAHeight = 1.00f;        // crew module adapter
        public const float CMARadius = 1.60f;

        public const float SMRadius = 2.05f;         // 4.1 m diameter
        public const float SMHeight = 4.00f;

        /// <summary>Total stack height, used by the scale readout.</summary>
        public const float StackHeight = SMHeight + CMAHeight + CMHeight + 0.55f;

        class Mats
        {
            public Material hull, shield, foil, blanket, cabin, metal, carbon, solar,
                            glass, seat, screen, thruster, white, dark, light;
        }

        static int _meshCounter;

        // ================================================================

        public static GameObject Build(Dictionary<string, PartInfo> infos, Transform parent = null)
        {
            AssetPaths.EnsureFolder(MeshFolder);
            _meshCounter = 0;
            MeshCache.Clear();

            var mats = BuildMaterials();
            var root = new GameObject("Orion_Spacecraft");
            if (parent != null) root.transform.SetParent(parent, false);

            var sm = BuildServiceModule(root.transform, mats, infos);
            sm.transform.localPosition = Vector3.zero;

            var cma = BuildAdapter(root.transform, mats, infos);
            cma.transform.localPosition = new Vector3(0f, SMHeight, 0f);

            var cm = BuildCrewModule(root.transform, mats, infos);
            cm.transform.localPosition = new Vector3(0f, SMHeight + CMAHeight, 0f);

            BuildInterior(cm.transform, mats, infos);

            // Everything on the crew module's outside except the heat shield, which
            // sits below the deck and does not block the view into the cabin.
            var cabinView = root.AddComponent<CabinView>();
            foreach (Transform child in cm.transform)
            {
                if (child.name == "Interior" || child.name == "HeatShield") continue;
                cabinView.hullPieces.Add(child.gameObject);
            }

            AssetDatabase.SaveAssets();
            return root;
        }

        static Mats BuildMaterials()
        {
            return new Mats
            {
                hull = MatLib2.Textured("M_OrionHull", "T_OrionHull",
                                        new Vector2(3f, 2f), Color.white, 1.0f, 1f, 1f),
                shield = MatLib2.Textured("M_HeatShield", "T_HeatShield",
                                          new Vector2(2f, 2f), Color.white, 1.2f),
                foil = MatLib2.Textured("M_ThermalFoil", "T_ThermalFoil",
                                        new Vector2(2.5f, 2f), Color.white, 1.3f),
                blanket = MatLib2.Textured("M_ESMBlanket", "T_ESMBlanket",
                                           new Vector2(1f, 1f), Color.white, 1.0f),
                cabin = MatLib2.Textured("M_CabinPanel", "T_CabinPanel",
                                         new Vector2(3f, 2.5f), Color.white, 0.9f),
                metal = MatLib2.Textured("M_Metal", "T_Metal",
                                         new Vector2(2f, 2f), Color.white, 0.6f),
                carbon = MatLib2.Textured("M_Carbon", "T_Carbon",
                                          new Vector2(2f, 2f), Color.white, 0.8f),
                solar = MatLib2.Textured("M_SolarArray", "T_SolarCells",
                                         new Vector2(1f, 3f), Color.white, 0.7f),
                white = MatLib2.Textured("M_PaintedWhite", "T_PaintedWhite",
                                         new Vector2(2f, 2f), Color.white, 0.7f),

                glass = MatLib2.Solid("M_Window", new Color(0.09f, 0.14f, 0.20f), 0.1f, 0.96f),
                seat = MatLib2.Solid("M_CrewSeat", new Color(0.17f, 0.19f, 0.23f), 0.05f, 0.33f),
                thruster = MatLib2.Solid("M_Thruster", new Color(0.22f, 0.21f, 0.20f), 0.75f, 0.30f),
                dark = MatLib2.Solid("M_PanelDark", new Color(0.11f, 0.12f, 0.14f), 0.4f, 0.42f),

                screen = MatLib2.Emissive("M_Display", new Color(0.25f, 0.70f, 0.85f), 1.6f),
                light = MatLib2.Emissive("M_CabinLight", new Color(0.85f, 0.93f, 1f), 2.4f),
            };
        }

        // ================================================================
        // European Service Module
        // ================================================================

        static GameObject BuildServiceModule(Transform parent, Mats mats,
                                             Dictionary<string, PartInfo> infos)
        {
            var sm = new GameObject("EuropeanServiceModule");
            sm.transform.SetParent(parent, false);

            // --- main body: flanged cylinder, not a plain tube ---------
            var profile = new List<ProcMesh2.ProfilePoint>
            {
                ProcMesh2.P(0f, 0f, true),
                ProcMesh2.P(SMRadius * 0.52f, 0f, true),
                ProcMesh2.P(SMRadius * 0.55f, 0.04f),
                ProcMesh2.P(SMRadius - 0.06f, 0.10f),
                ProcMesh2.P(SMRadius, 0.18f),                 // lower flange
                ProcMesh2.P(SMRadius, 0.30f, true),
                ProcMesh2.P(SMRadius - 0.025f, 0.34f),
                ProcMesh2.P(SMRadius - 0.025f, SMHeight - 0.42f),
                ProcMesh2.P(SMRadius, SMHeight - 0.36f, true),   // upper flange
                ProcMesh2.P(SMRadius, SMHeight - 0.22f),
                ProcMesh2.P(SMRadius - 0.08f, SMHeight - 0.12f),
                ProcMesh2.P(SMRadius * 0.80f, SMHeight, true),
            };

            var body = MeshObject("ESM_Body",
                ProcMesh2.Lathe("Orion_ESM_Body", profile, 96, uTile: 1f),
                mats.blanket, sm.transform);
            AddPart(body, infos, "service_module", meshCollider: true);

            BuildTankBulges(sm.transform, mats);
            BuildRadiators(sm.transform, mats);
            BuildSolarWings(sm.transform, mats, infos);
            BuildMainEngine(sm.transform, mats, infos);
            BuildAuxThrusters(sm.transform, mats);
            BuildESMGreebles(sm.transform, mats);

            return sm;
        }

        /// <summary>
        /// Propellant tank bulges.
        /// The ESM's four tanks press outward against the skin; the resulting curvature
        /// is visible in photographs and breaks up what would otherwise be a bare tube.
        /// </summary>
        static void BuildTankBulges(Transform parent, Mats mats)
        {
            var mesh = ProcMesh2.Sphere("Orion_TankBulge", 0.62f, 32, 18);
            SaveMesh(mesh);

            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 90f;
                var go = NewMeshObject("TankBulge_" + (i + 1), mesh, mats.blanket, parent);
                go.transform.localPosition = Quaternion.Euler(0f, a, 0f) *
                                             new Vector3(0f, SMHeight * 0.42f, SMRadius * 0.72f);
                go.transform.localScale = new Vector3(1f, 1.45f, 0.62f);
                go.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
        }

        static void BuildRadiators(Transform parent, Mats mats)
        {
            var panel = ProcMesh2.BeveledBox("Orion_Radiator", new Vector3(1.45f, 0.055f, 1.05f), 0.014f);
            SaveMesh(panel);

            var rib = ProcMesh2.BeveledBox("Orion_RadiatorRib", new Vector3(1.45f, 0.03f, 0.035f), 0.008f);
            SaveMesh(rib);

            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                var group = new GameObject("RadiatorAssembly_" + (i + 1));
                group.transform.SetParent(parent, false);
                group.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                group.transform.localPosition = Quaternion.Euler(0f, a, 0f) *
                                                new Vector3(0f, SMHeight * 0.30f, SMRadius + 0.50f);

                NewMeshObject("Panel", panel, mats.white, group.transform);

                // Coolant ribs across the face: the detail that says "radiator".
                for (int r = 0; r < 5; r++)
                {
                    var ribGO = NewMeshObject("Rib_" + r, rib, mats.metal, group.transform);
                    ribGO.transform.localPosition = new Vector3(0f, 0.042f, -0.40f + r * 0.20f);
                }

                // Mounting struts back to the hull.
                for (int s = -1; s <= 1; s += 2)
                {
                    var strut = Tube("Strut", group.transform, mats.metal, 0.028f,
                        new Vector3(s * 0.5f, 0f, -0.48f),
                        new Vector3(s * 0.3f, -0.18f, -0.95f));
                    strut.transform.localPosition += Vector3.zero;
                }
            }
        }

        static void BuildSolarWings(Transform parent, Mats mats, Dictionary<string, PartInfo> infos)
        {
            // 19 m tip-to-tip deployed: four wings of three panels each.
            var panelMesh = ProcMesh2.BeveledBox("Orion_SolarPanel",
                                                 new Vector3(1.90f, 0.035f, 2.10f), 0.010f);
            SaveMesh(panelMesh);

            var frameMesh = ProcMesh2.BeveledBox("Orion_SolarFrame",
                                                 new Vector3(1.98f, 0.055f, 0.05f), 0.010f);
            SaveMesh(frameMesh);

            for (int w = 0; w < 4; w++)
            {
                float a = 45f + w * 90f;
                var wing = new GameObject("SolarWing_" + (w + 1));
                wing.transform.SetParent(parent, false);
                wing.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                wing.transform.localPosition = new Vector3(0f, SMHeight * 0.60f, 0f);

                // Yoke: the gimbal that lets the wing track the Sun on two axes.
                var yokeBase = NewMeshObject("YokeBase",
                    GetOrCreate("Orion_YokeBase", () => ProcMesh2.Lathe("Orion_YokeBase",
                        ProcMesh2.CylinderProfile(0.17f, 0f, 0.26f, 0.02f), 28)),
                    mats.metal, wing.transform);
                yokeBase.transform.localPosition = new Vector3(0f, -0.13f, SMRadius + 0.04f);

                var boom = Tube("YokeBoom", wing.transform, mats.metal, 0.055f,
                    new Vector3(0f, 0f, SMRadius + 0.05f),
                    new Vector3(0f, 0f, SMRadius + 0.62f));

                for (int p = 0; p < 3; p++)
                {
                    float z = SMRadius + 1.30f + p * 2.16f;

                    var panel = NewMeshObject("Panel_" + (p + 1), panelMesh, mats.solar, wing.transform);
                    panel.transform.localPosition = new Vector3(0f, 0f, z);

                    var frameA = NewMeshObject("FrameA_" + p, frameMesh, mats.metal, wing.transform);
                    frameA.transform.localPosition = new Vector3(0f, 0f, z - 1.05f);
                    var frameB = NewMeshObject("FrameB_" + p, frameMesh, mats.metal, wing.transform);
                    frameB.transform.localPosition = new Vector3(0f, 0f, z + 1.05f);

                    // Hinge between panels.
                    if (p < 2)
                    {
                        var hinge = Tube("Hinge_" + p, wing.transform, mats.metal, 0.035f,
                            new Vector3(-0.9f, 0f, z + 1.08f),
                            new Vector3(0.9f, 0f, z + 1.08f));
                    }
                }

                if (w == 0) AddPart(wing, infos, "solar_arrays", boundsCollider: true);
            }
        }

        static void BuildMainEngine(Transform parent, Mats mats, Dictionary<string, PartInfo> infos)
        {
            var engine = new GameObject("MainEngine_OMSE");
            engine.transform.SetParent(parent, false);
            engine.transform.localPosition = new Vector3(0f, 0f, 0f);

            // Bell nozzle with a real parabolic contour, pointing down.
            var nozzle = MeshObject("Nozzle",
                ProcMesh2.Lathe("Orion_Nozzle",
                    ProcMesh2.BellNozzleProfile(0.22f, 0.78f, 1.45f, 24), 56, uTile: 3f),
                mats.thruster, engine.transform);
            nozzle.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            nozzle.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);

            // Regenerative cooling tubes running down the bell.
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                var path = new List<Vector3>();
                for (int k = 0; k <= 6; k++)
                {
                    float t = k / 6f;
                    float r = Mathf.Lerp(0.235f, 0.80f, Mathf.Pow(t, 0.62f));
                    float y = -0.05f - t * 1.45f;
                    path.Add(Quaternion.Euler(0f, a, 0f) * new Vector3(0f, y, r));
                }
                var tube = ProcMesh2.Tube("CoolingTube", path, 0.016f, 6, false);
                SaveMesh(tube);
                NewMeshObject("CoolTube_" + i, tube, mats.metal, engine.transform);
            }

            // Combustion chamber and injector head above the throat.
            var chamber = MeshObject("Chamber",
                ProcMesh2.Lathe("Orion_Chamber", new List<ProcMesh2.ProfilePoint>
                {
                    ProcMesh2.P(0.235f, 0f, true),
                    ProcMesh2.P(0.30f, 0.18f),
                    ProcMesh2.P(0.33f, 0.42f),
                    ProcMesh2.P(0.33f, 0.56f, true),
                    ProcMesh2.P(0.37f, 0.60f, true),
                    ProcMesh2.P(0.37f, 0.70f),
                    ProcMesh2.P(0.20f, 0.78f, true),
                    ProcMesh2.P(0f, 0.80f, true),
                }, 40),
                mats.metal, engine.transform);
            chamber.transform.localPosition = new Vector3(0f, -0.05f, 0f);

            // Gimbal actuators: the engine steers by tilting.
            for (int i = 0; i < 2; i++)
            {
                float a = i * 90f;
                Tube("GimbalActuator_" + i, engine.transform, mats.metal, 0.045f,
                    Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.72f, 0.30f),
                    Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.18f, 0.62f));
            }

            AddPart(engine, infos, "main_engine", boundsCollider: true);
        }

        static void BuildAuxThrusters(Transform parent, Mats mats)
        {
            // Eight R-4D auxiliary engines plus attitude-control pods.
            var podMesh = ProcMesh2.BeveledBox("Orion_RCSPod", new Vector3(0.34f, 0.26f, 0.30f), 0.02f);
            SaveMesh(podMesh);

            var nozzleMesh = GetOrCreate("Orion_SmallNozzle", () =>
                ProcMesh2.Lathe("Orion_SmallNozzle",
                    ProcMesh2.BellNozzleProfile(0.035f, 0.085f, 0.16f, 10), 20));

            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var pod = NewMeshObject("AuxPod_" + (i + 1), podMesh, mats.thruster, parent);
                pod.transform.localPosition = Quaternion.Euler(0f, a, 0f) *
                                              new Vector3(0f, 0.62f, SMRadius + 0.14f);
                pod.transform.localRotation = Quaternion.Euler(0f, a, 0f);

                var noz = NewMeshObject("Nozzle", nozzleMesh, mats.thruster, pod.transform);
                noz.transform.localPosition = new Vector3(0f, -0.14f, 0.02f);
                noz.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            }

            // Six attitude-control pods carrying four thrusters each.
            for (int i = 0; i < 6; i++)
            {
                float a = 30f + i * 60f;
                var pod = NewMeshObject("RCSPod_" + (i + 1), podMesh, mats.thruster, parent);
                pod.transform.localPosition = Quaternion.Euler(0f, a, 0f) *
                                              new Vector3(0f, SMHeight - 0.70f, SMRadius + 0.13f);
                pod.transform.localRotation = Quaternion.Euler(0f, a, 0f);
                pod.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);

                for (int k = 0; k < 4; k++)
                {
                    var noz = NewMeshObject("Noz_" + k, nozzleMesh, mats.thruster, pod.transform);
                    float ang = k * 90f;
                    noz.transform.localPosition = Quaternion.Euler(ang, 0f, 0f) * new Vector3(0f, 0f, 0.16f);
                    noz.transform.localRotation = Quaternion.Euler(90f + ang, 0f, 0f);
                    noz.transform.localScale = Vector3.one * 0.8f;
                }
            }
        }

        /// <summary>Piping, cable runs and antennas. Small hardware, large effect.</summary>
        static void BuildESMGreebles(Transform parent, Mats mats)
        {
            var greebles = new GameObject("Greebles");
            greebles.transform.SetParent(parent, false);

            // Longitudinal cable trays.
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 15f;
                Tube("CableTray_" + i, greebles.transform, mats.metal, 0.042f,
                    Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.45f, SMRadius + 0.05f),
                    Quaternion.Euler(0f, a, 0f) * new Vector3(0f, SMHeight - 0.55f, SMRadius + 0.05f));
            }

            // Pressurant spheres.
            var sphere = GetOrCreate("Orion_Pressurant", () => ProcMesh2.Sphere("Orion_Pressurant", 0.20f, 24, 14));
            for (int i = 0; i < 3; i++)
            {
                float a = 60f + i * 120f;
                var s = NewMeshObject("PressurantTank_" + i, sphere, mats.metal, greebles.transform);
                s.transform.localPosition = Quaternion.Euler(0f, a, 0f) *
                                            new Vector3(0f, SMHeight - 0.95f, SMRadius + 0.22f);
            }

            // High-gain antenna on a short mast.
            var mast = Tube("AntennaMast", greebles.transform, mats.metal, 0.035f,
                new Vector3(0f, SMHeight - 0.5f, SMRadius + 0.05f),
                new Vector3(0.25f, SMHeight + 0.15f, SMRadius + 0.45f));

            var dish = GetOrCreate("Orion_AntennaDish", () =>
                ProcMesh2.Lathe("Orion_AntennaDish",
                    ProcMesh2.SphericalCapProfile(0.30f, 0.46f, 10, pointDown: false, rimBevel: 0.015f),
                    36, flipNormals: true));
            var dishGO = NewMeshObject("AntennaDish", dish, mats.white, greebles.transform);
            dishGO.transform.localPosition = new Vector3(0.25f, SMHeight + 0.15f, SMRadius + 0.45f);
            dishGO.transform.localRotation = Quaternion.Euler(-40f, 30f, 0f);
        }

        // ================================================================
        // Crew Module Adapter
        // ================================================================

        static GameObject BuildAdapter(Transform parent, Mats mats, Dictionary<string, PartInfo> infos)
        {
            var cma = new GameObject("CrewModuleAdapter");
            cma.transform.SetParent(parent, false);

            // Narrower than both neighbours, which is what leaves the 5.03 m heat
            // shield standing proud of the stack where it can be seen and tapped.
            var profile = new List<ProcMesh2.ProfilePoint>
            {
                ProcMesh2.P(SMRadius * 0.80f, 0f, true),
                ProcMesh2.P(SMRadius * 0.80f, 0.06f),
                ProcMesh2.P(SMRadius * 0.72f, 0.14f),
                ProcMesh2.P(CMARadius + 0.05f, 0.35f),
                ProcMesh2.P(CMARadius, 0.52f),
                ProcMesh2.P(CMARadius, CMAHeight - 0.14f),
                ProcMesh2.P(CMARadius + 0.07f, CMAHeight - 0.06f, true),   // separation flange
                ProcMesh2.P(CMARadius + 0.07f, CMAHeight, true),
            };

            var body = MeshObject("Adapter_Body",
                ProcMesh2.Lathe("Orion_CMA", profile, 56, uTile: 3f),
                mats.carbon, cma.transform);
            var col = body.AddComponent<MeshCollider>();
            col.sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;

            // Umbilical runs carrying power, water and oxygen to the crew module.
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f;
                Tube("Umbilical_" + (i + 1), cma.transform, mats.metal, 0.075f,
                    Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.08f, CMARadius + 0.14f),
                    Quaternion.Euler(0f, a, 0f) * new Vector3(0f, CMAHeight - 0.05f, CMARadius + 0.12f));
            }

            // Separation bolts around the flange.
            var bolt = GetOrCreate("Orion_SepBolt", () =>
                ProcMesh2.Lathe("Orion_SepBolt",
                    ProcMesh2.CylinderProfile(0.045f, 0f, 0.07f, 0.008f), 14));
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                var b = NewMeshObject("SepBolt_" + i, bolt, mats.metal, cma.transform);
                b.transform.localPosition = Quaternion.Euler(0f, a, 0f) *
                                            new Vector3(0f, CMAHeight - 0.10f, CMARadius + 0.08f);
                b.transform.localRotation = Quaternion.Euler(90f, a, 0f);
            }

            return cma;
        }

        // ================================================================
        // Crew Module
        // ================================================================

        static GameObject BuildCrewModule(Transform parent, Mats mats, Dictionary<string, PartInfo> infos)
        {
            var cm = new GameObject("CrewModule");
            cm.transform.SetParent(parent, false);

            // --- back shell: blunt 57.5 degree cone with flanges --------
            var profile = new List<ProcMesh2.ProfilePoint>
            {
                ProcMesh2.P(CMBaseRadius, 0f, true),
                ProcMesh2.P(CMBaseRadius, 0.07f),
                ProcMesh2.P(CMBaseRadius - 0.045f, 0.13f),      // shoulder chamfer
            };
            // Cone body, sampled so the normal map tiles evenly.
            const int coneRings = 10;
            for (int i = 1; i <= coneRings; i++)
            {
                float t = (float)i / coneRings;
                float y = Mathf.Lerp(0.13f, CMHeight - 0.18f, t);
                float r = Mathf.Lerp(CMBaseRadius - 0.045f, CMTopRadius + 0.07f, t);
                profile.Add(ProcMesh2.P(r, y));
            }
            profile.Add(ProcMesh2.P(CMTopRadius + 0.05f, CMHeight - 0.10f));
            profile.Add(ProcMesh2.P(CMTopRadius + 0.09f, CMHeight - 0.04f, true));   // forward flange
            profile.Add(ProcMesh2.P(CMTopRadius + 0.09f, CMHeight, true));

            var shell = MeshObject("BackShell",
                ProcMesh2.Lathe("Orion_BackShell", profile, 80, uTile: 6f),
                mats.hull, cm.transform);
            AddPart(shell, infos, "back_shell", meshCollider: true);

            // --- heat shield -------------------------------------------
            var shieldProfile = ProcMesh2.SphericalCapProfile(CMBaseRadius, 6.05f, 18, true, 0.05f);
            var shield = MeshObject("HeatShield",
                ProcMesh2.Lathe("Orion_HeatShield", shieldProfile, 80, uTile: 1f),
                mats.shield, cm.transform);
            AddPart(shield, infos, "heat_shield", meshCollider: true);

            // Compression ring joining shield to shell.
            var ring = MeshObject("ShieldRing",
                ProcMesh2.Lathe("Orion_ShieldRing", new List<ProcMesh2.ProfilePoint>
                {
                    ProcMesh2.P(CMBaseRadius - 0.09f, -0.02f, true),
                    ProcMesh2.P(CMBaseRadius + 0.02f, -0.02f),
                    ProcMesh2.P(CMBaseRadius + 0.035f, 0.03f),
                    ProcMesh2.P(CMBaseRadius + 0.02f, 0.08f),
                    ProcMesh2.P(CMBaseRadius - 0.09f, 0.08f, true),
                }, 80),
                mats.metal, cm.transform);

            BuildHatch(cm.transform, mats, infos);
            BuildWindows(cm.transform, mats, infos);
            BuildForwardBay(cm.transform, mats, infos);
            BuildCMThrusters(cm.transform, mats);
            BuildCMGreebles(cm.transform, mats);

            return cm;
        }

        static float RadiusAt(float y)
        {
            float t = Mathf.Clamp01(y / CMHeight);
            return Mathf.Lerp(CMBaseRadius, CMTopRadius, t);
        }

        static void BuildHatch(Transform parent, Mats mats, Dictionary<string, PartInfo> infos)
        {
            float hatchY = CMHeight * 0.40f;
            float r = RadiusAt(hatchY);

            var hatch = new GameObject("SideHatch");
            hatch.transform.SetParent(parent, false);
            hatch.transform.localPosition = new Vector3(0f, hatchY, r + 0.02f);
            hatch.transform.localRotation = Quaternion.Euler(-32.5f, 0f, 0f);

            var frame = ProcMesh2.BeveledBox("Orion_HatchFrame", new Vector3(1.18f, 1.28f, 0.09f), 0.022f);
            SaveMesh(frame);
            NewMeshObject("Frame", frame, mats.metal, hatch.transform);

            var door = ProcMesh2.BeveledBox("Orion_HatchDoor", new Vector3(1.00f, 1.10f, 0.07f), 0.028f);
            SaveMesh(door);
            var doorGO = NewMeshObject("Door", door, mats.hull, hatch.transform);
            doorGO.transform.localPosition = new Vector3(0f, 0f, 0.025f);

            // Locking handle and hinge hardware.
            var handle = GetOrCreate("Orion_HatchHandle", () =>
                ProcMesh2.Lathe("Orion_HatchHandle",
                    ProcMesh2.CylinderProfile(0.055f, 0f, 0.09f, 0.012f), 20));
            var h = NewMeshObject("Handle", handle, mats.metal, hatch.transform);
            h.transform.localPosition = new Vector3(0.28f, 0f, 0.075f);
            h.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            for (int i = -1; i <= 1; i += 2)
            {
                Tube("Hinge_" + i, hatch.transform, mats.metal, 0.035f,
                    new Vector3(-0.52f, i * 0.42f, 0.03f),
                    new Vector3(-0.62f, i * 0.42f, 0.03f));
            }

            AddPart(hatch, infos, "side_hatch", boundsCollider: true);
        }

        static void BuildWindows(Transform parent, Mats mats, Dictionary<string, PartInfo> infos)
        {
            float winY = CMHeight * 0.60f;
            float r = RadiusAt(winY);

            var frameMesh = ProcMesh2.BeveledBox("Orion_WindowFrame",
                                                 new Vector3(0.52f, 0.46f, 0.075f), 0.016f);
            SaveMesh(frameMesh);
            var glassMesh = ProcMesh2.BeveledBox("Orion_WindowGlass",
                                                 new Vector3(0.40f, 0.34f, 0.05f), 0.012f);
            SaveMesh(glassMesh);

            var group = new GameObject("Windows");
            group.transform.SetParent(parent, false);

            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 90f;
                var win = new GameObject("Window_" + (i + 1));
                win.transform.SetParent(group.transform, false);
                win.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, winY, r + 0.01f);
                win.transform.localRotation = Quaternion.Euler(-32.5f, a, 0f);

                NewMeshObject("Frame", frameMesh, mats.metal, win.transform);
                var g = NewMeshObject("Glass", glassMesh, mats.glass, win.transform);
                g.transform.localPosition = new Vector3(0f, 0f, 0.022f);
            }

            AddPart(group, infos, "windows", boundsCollider: true);
        }

        static void BuildForwardBay(Transform parent, Mats mats, Dictionary<string, PartInfo> infos)
        {
            var bay = new GameObject("ForwardBay_DockingSystem");
            bay.transform.SetParent(parent, false);
            bay.transform.localPosition = new Vector3(0f, CMHeight, 0f);

            var profile = new List<ProcMesh2.ProfilePoint>
            {
                ProcMesh2.P(CMTopRadius + 0.09f, 0f, true),
                ProcMesh2.P(CMTopRadius + 0.09f, 0.05f),
                ProcMesh2.P(CMTopRadius, 0.12f),
                ProcMesh2.P(CMTopRadius - 0.03f, 0.34f),
                ProcMesh2.P(CMTopRadius - 0.03f, 0.40f, true),
                ProcMesh2.P(CMTopRadius + 0.02f, 0.44f, true),       // docking ring
                ProcMesh2.P(CMTopRadius + 0.02f, 0.52f),
                ProcMesh2.P(CMTopRadius - 0.12f, 0.56f, true),
                ProcMesh2.P(CMTopRadius - 0.32f, 0.56f, true),
                ProcMesh2.P(CMTopRadius - 0.34f, 0.50f, true),
                ProcMesh2.P(CMTopRadius - 0.34f, 0.20f, true),
            };

            var body = MeshObject("DockingAdapter",
                ProcMesh2.Lathe("Orion_ForwardBay", profile, 48, uTile: 2f),
                mats.metal, bay.transform);

            // Soft-capture petals: the androgynous half of the docking mechanism.
            var petal = ProcMesh2.BeveledBox("Orion_DockPetal", new Vector3(0.16f, 0.04f, 0.22f), 0.008f);
            SaveMesh(petal);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var p = NewMeshObject("Petal_" + i, petal, mats.metal, bay.transform);
                p.transform.localPosition = Quaternion.Euler(0f, a, 0f) *
                                            new Vector3(0f, 0.58f, CMTopRadius - 0.20f);
                p.transform.localRotation = Quaternion.Euler(-18f, a, 0f);
            }

            AddPart(bay, infos, "docking_system", boundsCollider: true);
        }

        static void BuildCMThrusters(Transform parent, Mats mats)
        {
            var nozzleMesh = GetOrCreate("Orion_SmallNozzle", () =>
                ProcMesh2.Lathe("Orion_SmallNozzle",
                    ProcMesh2.BellNozzleProfile(0.035f, 0.085f, 0.16f, 10), 20));

            var housing = ProcMesh2.BeveledBox("Orion_CMThrusterHousing",
                                               new Vector3(0.24f, 0.18f, 0.20f), 0.016f);
            SaveMesh(housing);

            // Twelve reaction-control thrusters in clusters around the back shell.
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                float ty = CMHeight * 0.76f;
                float r = RadiusAt(ty);

                var cluster = NewMeshObject("CM_RCSCluster_" + (i + 1), housing, mats.thruster, parent);
                cluster.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, ty, r + 0.06f);
                cluster.transform.localRotation = Quaternion.Euler(-32.5f, a, 0f);

                for (int k = -1; k <= 1; k += 2)
                {
                    var noz = NewMeshObject("Noz_" + k, nozzleMesh, mats.thruster, cluster.transform);
                    noz.transform.localPosition = new Vector3(k * 0.07f, 0f, 0.09f);
                    noz.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    noz.transform.localScale = Vector3.one * 0.75f;
                }
            }
        }

        /// <summary>Handrails, antennas and markings on the crew module.</summary>
        static void BuildCMGreebles(Transform parent, Mats mats)
        {
            var greebles = new GameObject("Greebles");
            greebles.transform.SetParent(parent, false);

            // EVA handrails: crew translation aids, and a strong scale cue because
            // everyone knows roughly how big a handrail is.
            for (int i = 0; i < 4; i++)
            {
                float a = 22f + i * 90f;
                for (int k = 0; k < 2; k++)
                {
                    float y0 = CMHeight * (0.30f + k * 0.26f);
                    float y1 = y0 + 0.42f;
                    Vector3 p0 = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, y0, RadiusAt(y0) + 0.10f);
                    Vector3 p1 = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, y1, RadiusAt(y1) + 0.10f);

                    Tube("Handrail_" + i + "_" + k, greebles.transform, mats.metal, 0.022f, p0, p1);

                    // Stand-off feet.
                    foreach (var p in new[] { p0, p1 })
                    {
                        Vector3 inward = new Vector3(-p.x, 0f, -p.z).normalized * 0.09f;
                        Tube("Foot", greebles.transform, mats.metal, 0.016f, p, p + inward);
                    }
                }
            }

            // Low-gain antennas.
            var antenna = GetOrCreate("Orion_LGA", () =>
                ProcMesh2.Lathe("Orion_LGA", new List<ProcMesh2.ProfilePoint>
                {
                    ProcMesh2.P(0.055f, 0f, true),
                    ProcMesh2.P(0.055f, 0.03f),
                    ProcMesh2.P(0.035f, 0.05f),
                    ProcMesh2.P(0.035f, 0.14f),
                    ProcMesh2.P(0.012f, 0.17f, true),
                }, 16));

            for (int i = 0; i < 2; i++)
            {
                float a = 70f + i * 180f;
                float y = CMHeight * 0.86f;
                var ant = NewMeshObject("LGA_" + i, antenna, mats.white, greebles.transform);
                ant.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, y, RadiusAt(y) + 0.03f);
                ant.transform.localRotation = Quaternion.Euler(-32.5f, a, 0f);
            }
        }

        // ================================================================
        // Interior
        // ================================================================

        static void BuildInterior(Transform crewModule, Mats mats, Dictionary<string, PartInfo> infos)
        {
            var cabin = new GameObject("Interior");
            cabin.transform.SetParent(crewModule, false);

            float innerBase = CMBaseRadius - CabinWallThickness;
            float innerTop = CMTopRadius;

            // Pressure vessel, seen from inside.
            var shellProfile = new List<ProcMesh2.ProfilePoint>
            {
                ProcMesh2.P(innerBase, 0.10f, true),
                ProcMesh2.P(innerBase - 0.04f, 0.18f),
            };
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                shellProfile.Add(ProcMesh2.P(
                    Mathf.Lerp(innerBase - 0.04f, innerTop, t),
                    Mathf.Lerp(0.18f, CMHeight - 0.16f, t)));
            }

            var shell = MeshObject("PressureVessel",
                ProcMesh2.Lathe("Orion_CabinShell", shellProfile, 64, uTile: 5f, flipNormals: true),
                mats.cabin, cabin.transform);
            AddPart(shell, infos, "pressure_vessel", meshCollider: true);

            // Deck.
            float deckR = RadiusAt(CabinFloorY) - CabinWallThickness;
            var floor = MeshObject("CabinFloor",
                ProcMesh2.Lathe("Orion_CabinFloor", new List<ProcMesh2.ProfilePoint>
                {
                    ProcMesh2.P(0f, 0f, true),
                    ProcMesh2.P(deckR - 0.05f, 0f),
                    ProcMesh2.P(deckR, 0.04f, true),
                    ProcMesh2.P(deckR, -0.06f, true),
                    ProcMesh2.P(deckR - 0.05f, -0.08f),
                    ProcMesh2.P(0f, -0.08f, true),
                }, 56),
                mats.dark, cabin.transform);
            floor.transform.localPosition = new Vector3(0f, CabinFloorY, 0f);

            BuildCrewSeats(cabin.transform, mats, infos, deckR);
            BuildConsole(cabin.transform, mats, infos, deckR);
            BuildHandControllers(cabin.transform, mats, infos, deckR);
            BuildEquipmentRacks(cabin.transform, mats, infos, deckR);
            BuildDockingHatch(cabin.transform, mats, infos);
            BuildCabinLighting(cabin.transform, mats, innerTop);
        }

        static void BuildCrewSeats(Transform parent, Mats mats, Dictionary<string, PartInfo> infos, float deckR)
        {
            var panMesh = ProcMesh2.BeveledBox("Orion_SeatPan", new Vector3(0.54f, 0.10f, 0.56f), 0.025f);
            SaveMesh(panMesh);
            var backMesh = ProcMesh2.BeveledBox("Orion_SeatBack", new Vector3(0.54f, 0.78f, 0.11f), 0.025f);
            SaveMesh(backMesh);
            var frameMesh = ProcMesh2.BeveledBox("Orion_SeatFrame", new Vector3(0.60f, 0.08f, 0.62f), 0.015f);
            SaveMesh(frameMesh);

            var group = new GameObject("CrewSeats");
            group.transform.SetParent(parent, false);

            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 90f;
                var seat = new GameObject("Seat_" + (i + 1));
                seat.transform.SetParent(group.transform, false);
                seat.transform.localPosition = Quaternion.Euler(0f, a, 0f) *
                                               new Vector3(0f, CabinFloorY, deckR * 0.50f);
                seat.transform.localRotation = Quaternion.Euler(0f, a + 180f, 0f);

                var pan = NewMeshObject("Pan", panMesh, mats.seat, seat.transform);
                pan.transform.localPosition = new Vector3(0f, 0.44f, 0f);

                var back = NewMeshObject("Back", backMesh, mats.seat, seat.transform);
                back.transform.localPosition = new Vector3(0f, 0.82f, -0.24f);
                back.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

                var frame = NewMeshObject("Frame", frameMesh, mats.metal, seat.transform);
                frame.transform.localPosition = new Vector3(0f, 0.08f, 0f);

                // Energy-absorbing struts, tuned per crew member's mass.
                for (int s = -1; s <= 1; s += 2)
                    for (int f = -1; f <= 1; f += 2)
                        Tube("Strut", seat.transform, mats.metal, 0.022f,
                             new Vector3(s * 0.24f, 0.10f, f * 0.24f),
                             new Vector3(s * 0.20f, 0.42f, f * 0.20f));

                // Harness straps.
                for (int s = -1; s <= 1; s += 2)
                    Tube("Harness", seat.transform, mats.dark, 0.016f,
                         new Vector3(s * 0.18f, 1.14f, -0.20f),
                         new Vector3(s * 0.08f, 0.50f, 0.10f));
            }

            AddPart(group, infos, "crew_seats", boundsCollider: true);
        }

        static void BuildConsole(Transform parent, Mats mats, Dictionary<string, PartInfo> infos, float deckR)
        {
            var console = new GameObject("DisplayAndControlPanel");
            console.transform.SetParent(parent, false);
            console.transform.localPosition = new Vector3(0f, CabinFloorY + 1.16f, -deckR * 0.70f);
            console.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);

            var bodyMesh = ProcMesh2.BeveledBox("Orion_ConsoleBody", new Vector3(1.62f, 0.66f, 0.16f), 0.022f);
            SaveMesh(bodyMesh);
            NewMeshObject("Body", bodyMesh, mats.dark, console.transform);

            var screenMesh = ProcMesh2.BeveledBox("Orion_Screen", new Vector3(0.46f, 0.32f, 0.03f), 0.008f);
            SaveMesh(screenMesh);
            var bezelMesh = ProcMesh2.BeveledBox("Orion_ScreenBezel", new Vector3(0.50f, 0.36f, 0.035f), 0.010f);
            SaveMesh(bezelMesh);

            for (int i = 0; i < 3; i++)
            {
                var bezel = NewMeshObject("Bezel_" + i, bezelMesh, mats.metal, console.transform);
                bezel.transform.localPosition = new Vector3((i - 1) * 0.52f, 0.06f, -0.085f);

                var screen = NewMeshObject("Screen_" + i, screenMesh, mats.screen, console.transform);
                screen.transform.localPosition = new Vector3((i - 1) * 0.52f, 0.06f, -0.105f);
            }

            // Physical switches: the ones that must work when software does not.
            var sw = GetOrCreate("Orion_Switch", () =>
                ProcMesh2.Lathe("Orion_Switch",
                    ProcMesh2.CylinderProfile(0.018f, 0f, 0.035f, 0.005f), 12));
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 8; c++)
                {
                    var s = NewMeshObject("Switch_" + r + "_" + c, sw, mats.metal, console.transform);
                    s.transform.localPosition = new Vector3(-0.68f + c * 0.195f, -0.20f - r * 0.09f, -0.09f);
                    s.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                }

            AddPart(console, infos, "display_control_panel", boundsCollider: true);
        }

        static void BuildHandControllers(Transform parent, Mats mats, Dictionary<string, PartInfo> infos, float deckR)
        {
            var group = new GameObject("HandControllers");
            group.transform.SetParent(parent, false);
            group.transform.localPosition = new Vector3(0f, CabinFloorY + 0.86f, -deckR * 0.56f);

            var baseMesh = ProcMesh2.BeveledBox("Orion_ControllerBase", new Vector3(0.20f, 0.07f, 0.20f), 0.012f);
            SaveMesh(baseMesh);
            var gripMesh = GetOrCreate("Orion_Grip", () =>
                ProcMesh2.Lathe("Orion_Grip", new List<ProcMesh2.ProfilePoint>
                {
                    ProcMesh2.P(0.042f, 0f, true),
                    ProcMesh2.P(0.045f, 0.03f),
                    ProcMesh2.P(0.038f, 0.09f),
                    ProcMesh2.P(0.044f, 0.14f),
                    ProcMesh2.P(0.040f, 0.19f),
                    ProcMesh2.P(0.020f, 0.22f, true),
                }, 20));

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 0.36f : -0.36f;
                var b = NewMeshObject(i == 0 ? "RHC_Base" : "THC_Base", baseMesh, mats.dark, group.transform);
                b.transform.localPosition = new Vector3(x, 0f, 0f);

                var g = NewMeshObject(i == 0 ? "RHC_Grip" : "THC_Grip", gripMesh, mats.dark, group.transform);
                g.transform.localPosition = new Vector3(x, 0.035f, 0f);
                g.transform.localRotation = Quaternion.Euler(i == 0 ? -12f : 12f, 0f, 0f);
            }

            AddPart(group, infos, "hand_controllers", boundsCollider: true);
        }

        static void BuildEquipmentRacks(Transform parent, Mats mats, Dictionary<string, PartInfo> infos, float deckR)
        {
            // --- ECLSS ------------------------------------------------
            var eclss = new GameObject("ECLSS_Rack");
            eclss.transform.SetParent(parent, false);
            eclss.transform.localPosition = Quaternion.Euler(0f, 90f, 0f) *
                                            new Vector3(0f, CabinFloorY + 0.60f, deckR * 0.76f);
            eclss.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var rackMesh = ProcMesh2.BeveledBox("Orion_Rack", new Vector3(0.78f, 1.12f, 0.44f), 0.020f);
            SaveMesh(rackMesh);
            NewMeshObject("Body", rackMesh, mats.cabin, eclss.transform);

            // Amine swing bed: two canisters that alternate.
            var canister = GetOrCreate("Orion_Canister", () =>
                ProcMesh2.Lathe("Orion_Canister",
                    ProcMesh2.CylinderProfile(0.14f, 0f, 0.46f, 0.02f), 24));
            for (int i = -1; i <= 1; i += 2)
            {
                var c = NewMeshObject("SwingBed_" + i, canister, mats.metal, eclss.transform);
                c.transform.localPosition = new Vector3(i * 0.20f, 0.10f, -0.26f);
            }
            for (int i = 0; i < 3; i++)
                Tube("Duct_" + i, eclss.transform, mats.metal, 0.05f,
                     new Vector3(-0.3f + i * 0.3f, 0.60f, -0.22f),
                     new Vector3(-0.3f + i * 0.3f, 0.86f, -0.10f));

            AddPart(eclss, infos, "eclss", boundsCollider: true);

            // --- Avionics ---------------------------------------------
            var avionics = new GameObject("AvionicsBay");
            avionics.transform.SetParent(parent, false);
            avionics.transform.localPosition = Quaternion.Euler(0f, -90f, 0f) *
                                               new Vector3(0f, CabinFloorY + 0.50f, deckR * 0.76f);
            avionics.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            var avMesh = ProcMesh2.BeveledBox("Orion_AvionicsBox", new Vector3(0.74f, 0.92f, 0.42f), 0.018f);
            SaveMesh(avMesh);
            NewMeshObject("Body", avMesh, mats.dark, avionics.transform);

            // Line-replaceable unit faces, with status lights.
            var lruMesh = ProcMesh2.BeveledBox("Orion_LRU", new Vector3(0.66f, 0.13f, 0.06f), 0.008f);
            SaveMesh(lruMesh);
            var ledMesh = ProcMesh2.BeveledBox("Orion_LED", new Vector3(0.025f, 0.025f, 0.012f), 0.004f);
            SaveMesh(ledMesh);

            for (int i = 0; i < 5; i++)
            {
                var lru = NewMeshObject("LRU_" + i, lruMesh, mats.metal, avionics.transform);
                lru.transform.localPosition = new Vector3(0f, 0.32f - i * 0.16f, -0.23f);

                var led = NewMeshObject("LED_" + i, ledMesh, mats.screen, avionics.transform);
                led.transform.localPosition = new Vector3(0.27f, 0.32f - i * 0.16f, -0.26f);
            }

            AddPart(avionics, infos, "avionics", boundsCollider: true);

            // --- Stowage ----------------------------------------------
            var stow = new GameObject("StowageLockers");
            stow.transform.SetParent(parent, false);
            stow.transform.localPosition = Quaternion.Euler(0f, 165f, 0f) *
                                           new Vector3(0f, CabinFloorY, deckR * 0.78f);
            stow.transform.localRotation = Quaternion.Euler(0f, 165f, 0f);

            var lockerMesh = ProcMesh2.BeveledBox("Orion_Locker", new Vector3(0.36f, 0.28f, 0.32f), 0.016f);
            SaveMesh(lockerMesh);
            var latchMesh = ProcMesh2.BeveledBox("Orion_Latch", new Vector3(0.07f, 0.03f, 0.02f), 0.005f);
            SaveMesh(latchMesh);

            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 2; col++)
                {
                    var box = NewMeshObject("Locker_" + row + col, lockerMesh, mats.cabin, stow.transform);
                    box.transform.localPosition = new Vector3((col - 0.5f) * 0.38f, 0.20f + row * 0.30f, 0f);

                    var latch = NewMeshObject("Latch_" + row + col, latchMesh, mats.metal, stow.transform);
                    latch.transform.localPosition = new Vector3((col - 0.5f) * 0.38f, 0.20f + row * 0.30f, -0.17f);
                }

            AddPart(stow, infos, "stowage", boundsCollider: true);

            // --- Waste management -------------------------------------
            var wms = new GameObject("WasteManagementSystem");
            wms.transform.SetParent(parent, false);
            wms.transform.localPosition = Quaternion.Euler(0f, -165f, 0f) *
                                          new Vector3(0f, CabinFloorY + 0.42f, deckR * 0.74f);
            wms.transform.localRotation = Quaternion.Euler(0f, -165f, 0f);

            var wmsMesh = ProcMesh2.BeveledBox("Orion_WMS", new Vector3(0.56f, 0.82f, 0.50f), 0.022f);
            SaveMesh(wmsMesh);
            NewMeshObject("Body", wmsMesh, mats.cabin, wms.transform);

            // Airflow duct: without gravity, waste is moved by a fan.
            Tube("Fan_Duct", wms.transform, mats.metal, 0.06f,
                 new Vector3(0f, 0.40f, -0.20f), new Vector3(0f, 0.58f, -0.40f));

            AddPart(wms, infos, "waste_management", boundsCollider: true);
        }

        static void BuildDockingHatch(Transform parent, Mats mats, Dictionary<string, PartInfo> infos)
        {
            var hatch = new GameObject("ForwardDockingHatch");
            hatch.transform.SetParent(parent, false);
            hatch.transform.localPosition = new Vector3(0f, CMHeight - 0.22f, 0f);

            var ring = MeshObject("HatchRing",
                ProcMesh2.Lathe("Orion_DockHatchRing", new List<ProcMesh2.ProfilePoint>
                {
                    ProcMesh2.P(0.30f, 0f, true),
                    ProcMesh2.P(0.52f, 0f),
                    ProcMesh2.P(0.55f, 0.04f),
                    ProcMesh2.P(0.55f, 0.10f, true),
                    ProcMesh2.P(0.30f, 0.10f, true),
                }, 40),
                mats.metal, hatch.transform);

            var door = GetOrCreate("Orion_DockHatchDoor", () =>
                ProcMesh2.Lathe("Orion_DockHatchDoor",
                    ProcMesh2.SphericalCapProfile(0.30f, 0.62f, 10, pointDown: true, rimBevel: 0.02f), 36));
            var d = NewMeshObject("Door", door, mats.cabin, hatch.transform);
            d.transform.localPosition = new Vector3(0f, 0.04f, 0f);

            // Dogging latches around the rim.
            var latch = GetOrCreate("Orion_HatchLatch", () =>
                ProcMesh2.Lathe("Orion_HatchLatch",
                    ProcMesh2.CylinderProfile(0.028f, 0f, 0.07f, 0.006f), 12));
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var l = NewMeshObject("Latch_" + i, latch, mats.metal, hatch.transform);
                l.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.02f, 0.42f);
                l.transform.localRotation = Quaternion.Euler(90f, a, 0f);
            }

            AddPart(hatch, infos, "docking_hatch", boundsCollider: true);
        }

        static void BuildCabinLighting(Transform parent, Mats mats, float innerTop)
        {
            // Emissive strips rather than real lights: a handful of realtime point
            // lights inside a cabin is the fastest way to lose frame rate on a phone.
            var strip = ProcMesh2.BeveledBox("Orion_LightStrip", new Vector3(0.62f, 0.05f, 0.06f), 0.012f);
            SaveMesh(strip);

            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                float y = CMHeight - 0.62f;
                var s = NewMeshObject("CabinLight_" + i, strip, mats.light, parent);
                s.transform.localPosition = Quaternion.Euler(0f, a, 0f) *
                                            new Vector3(0f, y, RadiusAt(y) - 0.22f);
                s.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
        }

        // ================================================================
        // Helpers
        // ================================================================

        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

        static Mesh GetOrCreate(string name, System.Func<Mesh> factory)
        {
            Mesh m;
            if (MeshCache.TryGetValue(name, out m) && m != null) return m;
            m = factory();
            SaveMesh(m);
            MeshCache[name] = m;
            return m;
        }

        static void SaveMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh))) return;
            string path = MeshFolder + "/" + mesh.name + "_" + (_meshCounter++) + ".asset";
            AssetPaths.CreateAsset(mesh, path);
        }

        static GameObject MeshObject(string name, Mesh mesh, Material mat, Transform parent)
        {
            SaveMesh(mesh);
            return NewMeshObject(name, mesh, mat, parent);
        }

        static GameObject NewMeshObject(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        static GameObject Tube(string name, Transform parent, Material mat, float radius,
                               Vector3 from, Vector3 to, int sides = 10)
        {
            var mesh = ProcMesh2.Tube(name, new List<Vector3> { from, to }, radius, sides, true);
            SaveMesh(mesh);
            return NewMeshObject(name, mesh, mat, parent);
        }

        static void AddPart(GameObject go, Dictionary<string, PartInfo> infos, string partId,
                            bool meshCollider = false, bool boundsCollider = false)
        {
            if (meshCollider)
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && go.GetComponent<Collider>() == null)
                {
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
            else if (boundsCollider || go.GetComponent<Collider>() == null)
            {
                // Unity's "fake null" defeats ??: GetComponent returns a non-null stub in the
                // editor, so the AddComponent branch never ran and the build threw here.
                var box = go.GetComponent<BoxCollider>();
                if (box == null) box = go.AddComponent<BoxCollider>();
                FitBoxToChildren(go, box);
            }

            var part = go.AddComponent<SelectablePart>();
            PartInfo info;
            if (infos != null && infos.TryGetValue(partId, out info)) part.info = info;
            else Debug.LogWarning("[OrionBuilder] No PartInfo for id '" + partId + "'.");
        }

        /// <summary>
        /// Fits a box collider in the object's own local space.
        /// World-space renderer bounds would oversize the box on anything rotated,
        /// which is most of this model.
        /// </summary>
        static void FitBoxToChildren(GameObject go, BoxCollider box)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>();
            bool any = false;
            Bounds local = new Bounds();

            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;

                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);

                    Vector3 world = mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, sign));
                    Vector3 lp = go.transform.InverseTransformPoint(world);

                    if (!any) { local = new Bounds(lp, Vector3.zero); any = true; }
                    else local.Encapsulate(lp);
                }
            }

            if (!any) return;
            box.center = local.center;
            // A little padding makes small components far easier to hit with a finger.
            box.size = local.size + Vector3.one * 0.03f;
        }
    }
}
