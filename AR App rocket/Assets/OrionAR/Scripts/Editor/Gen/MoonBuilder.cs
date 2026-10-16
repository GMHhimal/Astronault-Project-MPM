using UnityEditor;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Builds the lunar setting.
    ///
    /// The Moon is not Mars with the colour drained out, and getting the difference
    /// right is most of the work. There is no atmosphere, so:
    ///
    ///   No fog, and no aerial perspective. Distant terrain is exactly as sharp and as
    ///   contrasty as near terrain, which is why lunar photographs make distances
    ///   impossible to judge.
    ///
    ///   Almost no ambient light. On Earth the sky is a huge soft fill; on the Moon
    ///   the only fill is weak bounce from the regolith. Shadows go nearly black, and
    ///   that brutal contrast is the single most recognisable feature of lunar imagery.
    ///   Most renders get this wrong by keeping Earth-like ambient.
    ///
    ///   A hard white sun. No atmospheric scattering to warm or soften it.
    ///
    ///   Sharp crater rims. No wind or water means no erosion, so features stay crisp
    ///   for billions of years.
    ///
    /// Earth hangs in the sky, which is also a useful orientation cue for the finale.
    /// </summary>
    public static class MoonBuilder
    {
        public static GameObject BuildSurface(float size = 260f, int resolution = 180, int seed = 1969)
        {
            var root = new GameObject("LunarSurface");

            var regolith = MatLib2.Textured("M_LunarRegolith", "T_LunarRegolith",
                                            new Vector2(14f, 14f), Color.white,
                                            normalScale: 1.6f);
            var rockMat = MatLib2.Solid("M_LunarRock", new Color(0.26f, 0.255f, 0.248f), 0f, 0.06f);

            BuildTerrain(root.transform, regolith, size, resolution, seed);
            ScatterRocks(root.transform, rockMat, size, seed);
            ApplyLunarEnvironment();
            BuildSun(root.transform);
            BuildEarth(root.transform);

            return root;
        }

        // ---------------------------------------------------------------

        static void BuildTerrain(Transform parent, Material mat, float size, int resolution, int seed)
        {
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 1000f;
            float oz = (float)rng.NextDouble() * 1000f;

            System.Func<float, float, float> height = (x, z) =>
            {
                // Flat pad under the spacecraft so the placement reads as deliberate.
                float distFromPad = Mathf.Sqrt(x * x + z * z);
                float flatten = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distFromPad - 9f) / 14f));

                float rolling = (Mathf.PerlinNoise((x + ox) * 0.009f, (z + oz) * 0.009f) - 0.5f) * 14f;
                float undulation = (Mathf.PerlinNoise((x + ox) * 0.035f, (z + oz) * 0.035f) - 0.5f) * 2.4f;
                float grit = (Mathf.PerlinNoise((x + ox) * 0.3f, (z + oz) * 0.3f) - 0.5f) * 0.22f;

                // Craters at several scales. Rims stay sharp - there is no erosion.
                float craters = 0f;
                craters += Crater(x, z, 58f, -40f, 26f, 5.5f);
                craters += Crater(x, z, -72f, 52f, 34f, 7.0f);
                craters += Crater(x, z, 30f, 88f, 18f, 3.4f);
                craters += Crater(x, z, -95f, -70f, 42f, 8.5f);
                craters += Crater(x, z, 100f, 20f, 15f, 2.8f);

                var smallRng = new System.Random(seed + 11);
                for (int i = 0; i < 14; i++)
                {
                    float cx = ((float)smallRng.NextDouble() - 0.5f) * size * 0.85f;
                    float cz = ((float)smallRng.NextDouble() - 0.5f) * size * 0.85f;
                    float r = 3.5f + (float)smallRng.NextDouble() * 7f;
                    craters += Crater(x, z, cx, cz, r, r * 0.17f);
                }

                return (rolling + undulation + craters) * flatten + grit;
            };

            var mesh = ProcMesh2.HeightGrid("LunarTerrain", resolution, size, height, size / 10f);
            string path = OrionBuilder.MeshFolder + "/LunarTerrain.asset";
            AssetPaths.CreateAsset(mesh, path);

            var go = new GameObject("Terrain");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.receiveShadows = true;

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;

            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        }

        /// <summary>
        /// A crater: parabolic bowl, sharp raised rim, shallow ejecta apron.
        /// The rim is the part that must stay crisp.
        /// </summary>
        static float Crater(float x, float z, float cx, float cz, float radius, float depth)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / radius;
            if (d > 1.8f) return 0f;

            if (d < 0.92f)
            {
                // Bowl floor.
                return -depth * Mathf.Cos(d / 0.92f * Mathf.PI * 0.5f);
            }
            if (d < 1.08f)
            {
                // Rim: narrow and sharp.
                float t = (d - 0.92f) / 0.16f;
                return depth * 0.42f * Mathf.Sin(t * Mathf.PI);
            }
            // Ejecta apron, falling away gently.
            float e = (d - 1.08f) / 0.72f;
            return depth * 0.13f * (1f - e) * (1f - e);
        }

        static void ScatterRocks(Transform parent, Material mat, float size, int seed)
        {
            Physics.SyncTransforms();     // editor raycasts need the colliders registered

            var rocks = new GameObject("Boulders");
            rocks.transform.SetParent(parent, false);

            // A dozen unique boulder meshes reused across the field: far better
            // batching than a hundred unique meshes, and visually indistinguishable.
            const int variants = 12;
            var meshes = new Mesh[variants];
            var sphere = ProcMesh2.Sphere("BoulderSource", 1f, 20, 12);

            for (int v = 0; v < variants; v++)
            {
                meshes[v] = Deform(sphere, 0.42f, seed + v * 37, "LunarBoulder_" + v);
                string path = OrionBuilder.MeshFolder + "/LunarBoulder_" + v + ".asset";
                AssetPaths.CreateAsset(meshes[v], path);
            }
            Object.DestroyImmediate(sphere);

            var rng = new System.Random(seed + 5);
            const int count = 110;

            for (int i = 0; i < count; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = 11f + (float)rng.NextDouble() * (size * 0.44f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                var go = new GameObject("Boulder_" + i);
                go.transform.SetParent(rocks.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[rng.Next(variants)];
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;

                float scale = 0.25f + (float)rng.NextDouble() * 1.9f;
                go.transform.localScale = new Vector3(scale,
                                                      scale * (0.6f + (float)rng.NextDouble() * 0.5f),
                                                      scale);
                go.transform.localRotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 24f,
                    (float)rng.NextDouble() * 360f,
                    (float)rng.NextDouble() * 24f);

                RaycastHit hit;
                float y = Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out hit, 600f)
                    ? hit.point.y : 0f;
                go.transform.position = new Vector3(x, y - scale * 0.26f, z);

                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        static Mesh Deform(Mesh source, float irregularity, int seed, string name)
        {
            var rng = new System.Random(seed);
            var offset = new Vector3((float)rng.NextDouble() * 100f,
                                     (float)rng.NextDouble() * 100f,
                                     (float)rng.NextDouble() * 100f);

            var verts = source.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 n = verts[i].normalized;
                Vector3 p = n * 2.6f + offset;
                float d = 1f
                    + (Mathf.PerlinNoise(p.x, p.y) - 0.5f) * irregularity
                    + (Mathf.PerlinNoise(p.z, p.x) - 0.5f) * irregularity * 0.55f
                    + (Mathf.PerlinNoise(p.y * 3f, p.z * 3f) - 0.5f) * irregularity * 0.25f;
                verts[i] = verts[i] * d;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.uv = source.uv;
            mesh.triangles = source.triangles;
            // Angular rather than smooth: lunar rocks are fractured, not rounded.
            ProcMesh2.ApplySmoothing(mesh, 42f, null);
            return mesh;
        }

        // ---------------------------------------------------------------
        // Environment
        // ---------------------------------------------------------------

        public static void ApplyLunarEnvironment()
        {
            // No atmosphere: no fog, no scattering, no sky light.
            RenderSettings.fog = false;
            RenderSettings.skybox = null;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            // Sky contributes essentially nothing. The small ground term is bounce
            // from the regolith, which is the only fill light that exists up there.
            // Kept dark, but not zero: pure lunar ambient crushes every shadowed
            // surface to black on a phone screen in a bright exhibition hall.
            RenderSettings.ambientSkyColor = new Color(0.045f, 0.052f, 0.075f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.07f, 0.078f);
            RenderSettings.ambientGroundColor = new Color(0.13f, 0.125f, 0.12f);

            RenderSettings.reflectionIntensity = 0.35f;
        }

        static void BuildSun(Transform parent)
        {
            var existing = Object.FindFirstObjectByType<Light>();
            if (existing != null && existing.type == LightType.Directional)
                Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;

            // Unfiltered sunlight: bright and essentially white. Low elevation gives
            // the long shadows that convey the scale of the terrain.
            light.intensity = 1.55f;
            light.color = new Color(1f, 0.985f, 0.96f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;            // no atmosphere to soften shadows
            light.shadowBias = 0.012f;
            light.shadowNormalBias = 0.2f;

            // Local to the content root, whose +Z faces the user once placed. A sun
            // behind the viewer's shoulder lights the side they are looking at; the
            // earlier angle lit the far side, so the model always looked black.
            go.transform.localRotation = Quaternion.Euler(28f, 205f, 0f);
            RenderSettings.sun = light;

            // Earthshine. The Earth's day side reflects sunlight onto the Moon - it is
            // what lights the lunar night - so a faint cool fill from the other flank
            // is physically honest, and it keeps shadowed faces readable on a phone.
            var fillGO = new GameObject("Earthshine");
            fillGO.transform.SetParent(parent, false);
            fillGO.transform.localRotation = Quaternion.Euler(10f, 145f, 0f);
            var fill = fillGO.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.38f;
            fill.color = new Color(0.62f, 0.74f, 1f);
            fill.shadows = LightShadows.None;
        }

        /// <summary>
        /// Earth in the lunar sky.
        ///
        /// From the Moon, Earth subtends about 2 degrees - roughly four times the
        /// angular size of the Moon seen from Earth - and stays nearly fixed in the
        /// sky because the Moon is tidally locked.
        /// </summary>
        static void BuildEarth(Transform parent)
        {
            var go = PlanetBuilder.Earth("Earth", parent);
            go.transform.localPosition = new Vector3(-62f, 58f, 115f);
            go.transform.localScale = Vector3.one * 7.5f;
            go.transform.localRotation = Quaternion.Euler(-18f, 140f, 12f);
        }

        /// <summary>Starfield for the lunar sky and the orbital backdrop.</summary>
        public static GameObject BuildStarfield(Transform parent, float radius = 400f)
        {
            var go = new GameObject("Starfield");
            if (parent != null) go.transform.SetParent(parent, false);

            var field = go.AddComponent<Starfield>();
            field.radius = radius;
            field.starCount = 1500;

            var mesh = field.Generate();
            string path = OrionBuilder.MeshFolder + "/Starfield.asset";
            AssetPaths.CreateAsset(mesh, path);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            // Vertex colours carry per-star tint and brightness, which URP's Unlit
            // ignores; the custom shader also draws the quads from inside the sphere.
            var starShader = Shader.Find("OrionAR/StarPoint");
            var mat = starShader != null
                ? MatLib2.WithShader("M_Starfield", starShader)
                : MatLib2.UnlitColor("M_Starfield", Color.white, transparent: true);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            return go;
        }
    }
}
