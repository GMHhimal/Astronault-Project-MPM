using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Astronaut.MoonLanding.EditorTools
{
    /// <summary>
    /// Procedural lunar mare terrain, v2 (2 km × 2 km, ~0.98 m heightmap cells):
    ///  • ~14 000 crater attempts following the lunar size-frequency law (many small, few large),
    ///    with irregular rims, flat floors on old craters, bright ejecta and ray systems on fresh ones,
    ///  • a "West crater"-like 190 m crater + boulder field on the approach path (Apollo 11 homage),
    ///  • a flat landing zone at the world origin,
    ///  • a baked 2048² macro albedo map (mare tone patches, ejecta, rays, darker crater floors)
    ///    blended with two anti-tiling regolith detail layers → no visible texture grid,
    ///  • a curved horizon ring out to 30 km.
    /// </summary>
    public static class MoonTerrainBuilder
    {
        public const int Resolution = 2049;
        public const float Size = 2000f;
        public const float HeightRange = 240f;
        public const float BaseHeight = 100f;
        const float Half = Size * 0.5f;
        const float MoonRadius = 1737100f;
        const int MacroResolution = 2048;
        const int SplatResolution = 1024;
        /// <summary>Terrain beyond this radius is cut away; the horizon ring starts just inside it.</summary>
        public const float HoleRadius = 997f;
        /// <summary>Outer radius of the horizon ring. Must be beyond the geometric horizon at max altitude
        /// (d = √(2·R·h): 2.5 km ceiling → 93 km), otherwise the edge of the world becomes visible.</summary>
        public const float HorizonOuterRadius = 95000f;

        public struct Crater
        {
            public float x, z, radius, depth, rim, freshness, phase1, phase2;
        }

        public class Result
        {
            public Terrain terrain;
            public Crater westCrater;
            public List<Crater> craters = new List<Crater>();
        }

        static float Smooth01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        static float Noise(float wx, float wz, float scale, float ox, float oz)
        {
            return Mathf.PerlinNoise(wx / scale + ox, wz / scale + oz) - 0.5f;
        }

        public static Result Build(Transform parent, MoonAssetFactory.MaterialSet mats, Vector3 approachDirection, int seed)
        {
            var result = new Result();
            var rnd = new System.Random(seed);
            float cell = Size / (Resolution - 1);
            var h = new float[Resolution, Resolution];
            var ejecta = new float[Resolution, Resolution];
            var rays = new float[Resolution, Resolution];

            // ---- 1. mare undulation (5 octaves)
            float ox = (float)rnd.NextDouble() * 500f, oz = (float)rnd.NextDouble() * 500f;
            for (int z = 0; z < Resolution; z++)
            {
                float wz = z * cell - Half;
                for (int x = 0; x < Resolution; x++)
                {
                    float wx = x * cell - Half;
                    h[z, x] =
                        Noise(wx, wz, 700f, ox, oz) * 16f +
                        Noise(wx, wz, 210f, ox * 2f, oz * 2f) * 5f +
                        Noise(wx, wz, 55f, ox * 3f, oz * 3f) * 2.2f +
                        Noise(wx, wz, 22f, ox * 4f, oz * 4f) * 0.9f +
                        Noise(wx, wz, 7f, ox * 5f, oz * 5f) * 0.18f;
                }
            }
            EditorUtility.DisplayProgressBar("Building Moon Landing Level", "Terrain: stamping craters…", 0.25f);

            // ---- 2. craters
            Vector3 dir = approachDirection; dir.y = 0f; dir.Normalize();
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);

            Vector3 westCenter = -dir * 270f + side * 45f;
            result.westCrater = MakeCrater(rnd, westCenter.x, westCenter.z, 95f, 0.16f, 1f);
            result.craters.Add(result.westCrater);

            for (int i = 0; i < 7; i++)
            {
                Vector2 p = RandomPoint(rnd, 380f, 850f);
                float d = Lerp(90f, 260f, (float)rnd.NextDouble());
                result.craters.Add(MakeCrater(rnd, p.x, p.y, d * 0.5f, Lerp(0.08f, 0.14f, (float)rnd.NextDouble()), Lerp(0.2f, 0.6f, (float)rnd.NextDouble())));
            }
            // size-frequency law N(>D) ∝ D^-2, D from 3 m to 90 m
            for (int i = 0; i < 14000; i++)
            {
                float u = (float)rnd.NextDouble();
                float diameter = Mathf.Min(90f, 3f * Mathf.Pow(1f - u, -0.5f));
                Vector2 p = RandomPoint(rnd, 0f, 985f);
                float distToLz = p.magnitude;
                if (distToLz < 45f + diameter) continue;
                if (distToLz < 110f && diameter > 14f) continue;
                float fresh = Mathf.Pow((float)rnd.NextDouble(), 2.2f);
                result.craters.Add(MakeCrater(rnd, p.x, p.y, diameter * 0.5f, Lerp(0.07f, 0.19f, fresh), fresh));
            }

            foreach (Crater c in result.craters) Stamp(h, ejecta, rays, c, cell);

            // ---- 3. flatten landing zone + fade the outer border to base level (meets the horizon ring)
            for (int z = 0; z < Resolution; z++)
            {
                float wz = z * cell - Half;
                for (int x = 0; x < Resolution; x++)
                {
                    float wx = x * cell - Half;
                    float r = Mathf.Sqrt(wx * wx + wz * wz);
                    float lz = 1f - Smooth01(35f, 120f, r);
                    float edge = Smooth01(900f, 995f, r);
                    float k = Mathf.Max(lz, edge);
                    float micro = Noise(wx, wz, 9f, 17f, 3f) * 0.25f * lz;
                    // real lunar curvature (surface drops r²/2R) so the terrain meets the curved horizon without a step
                    h[z, x] = Lerp(h[z, x], micro, k) - r * r / (2f * MoonRadius);
                    ejecta[z, x] *= 1f - edge;
                    rays[z, x] *= 1f - edge;
                }
            }

            EditorUtility.DisplayProgressBar("Building Moon Landing Level", "Terrain: baking macro albedo…", 0.33f);

            // ---- 4. macro albedo texture + layer
            Texture2D macro = BakeMacroAlbedo(h, ejecta, rays, cell, ox, oz);
            TerrainLayer macroLayer = MoonAssetFactory.Layer("TL_Macro_Albedo", macro, null, Size, new Vector4(1f, 1f, 1f, 1f), 0.04f, 0f);

            // ---- 5. TerrainData asset
            MoonAssetFactory.EnsureFolder(MoonPaths.Terrain);
            string dataPath = MoonPaths.Terrain + "/TD_MoonSurface.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath) != null) AssetDatabase.DeleteAsset(dataPath);

            var data = new TerrainData();
            data.heightmapResolution = Resolution;
            data.size = new Vector3(Size, HeightRange, Size); // must be set AFTER heightmapResolution
            data.alphamapResolution = SplatResolution;
            data.baseMapResolution = 2048;

            var heights = new float[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
                for (int x = 0; x < Resolution; x++)
                    heights[z, x] = Mathf.Clamp01((BaseHeight + h[z, x]) / HeightRange);
            data.SetHeights(0, 0, heights);

            // Cut the square corners away (terrain holes): the playable world is a circle that blends
            // into the curved horizon ring, so no straight "table edge" is ever visible.
            int holeRes = Resolution - 1;
            var surface = new bool[holeRes, holeRes];
            float holeCell = Size / holeRes;
            for (int z = 0; z < holeRes; z++)
            {
                float wz = (z + 0.5f) * holeCell - Half;
                for (int x = 0; x < holeRes; x++)
                {
                    float wx = (x + 0.5f) * holeCell - Half;
                    surface[z, x] = wx * wx + wz * wz < HoleRadius * HoleRadius;   // true = solid ground
                }
            }
            data.SetHoles(0, 0, surface);

            var layers = new List<TerrainLayer>(mats.terrainLayers);
            layers.Add(macroLayer);
            data.terrainLayers = layers.ToArray();
            data.SetAlphamaps(0, 0, BuildSplat(h, cell, ox, oz));

            AssetDatabase.CreateAsset(data, dataPath);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "MoonTerrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-Half, -BaseHeight, -Half);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

            Terrain terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = mats.terrain;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = 1500f;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            terrain.allowAutoConnect = false;
            result.terrain = terrain;

            BuildHorizonRing(parent, mats.horizon, seed);
            return result;
        }

        static Vector2 RandomPoint(System.Random rnd, float minR, float maxR)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Lerp(minR * minR, maxR * maxR, (float)rnd.NextDouble()));
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        static Crater MakeCrater(System.Random rnd, float x, float z, float radius, float depthRatio, float freshness)
        {
            float diameter = radius * 2f;
            return new Crater
            {
                x = x, z = z, radius = radius,
                depth = diameter * depthRatio,
                rim = diameter * Lerp(0.015f, 0.045f, freshness),
                freshness = freshness,
                phase1 = (float)rnd.NextDouble() * 6.2832f,
                phase2 = (float)rnd.NextDouble() * 6.2832f,
            };
        }

        static void Stamp(float[,] h, float[,] ejecta, float[,] rays, Crater c, float cell)
        {
            float reach = 2.6f;
            bool hasRays = c.freshness > 0.6f && c.radius > 10f;
            float ext = c.radius * (hasRays ? 8f : reach) * 1.1f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - ext + Half) / cell));
            int x1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt((c.x + ext + Half) / cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((c.z - ext + Half) / cell));
            int z1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt((c.z + ext + Half) / cell));
            float floorLimit = -c.depth * Lerp(0.72f, 1f, c.freshness);

            for (int z = z0; z <= z1; z++)
            {
                float dz = z * cell - Half - c.z;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x * cell - Half - c.x;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist > ext) continue;
                    float theta = Mathf.Atan2(dz, dx);
                    // irregular, slightly polygonal rim like real impact craters
                    float irregular = 1f + 0.03f * Mathf.Sin(3f * theta + c.phase1) + 0.02f * Mathf.Sin(5f * theta + c.phase2)
                                         + 0.012f * Mathf.Sin(9f * theta + c.phase1 * 2f);
                    float d = dist / (c.radius * irregular);

                    if (d < reach)
                    {
                        float delta;
                        if (d < 1f)
                        {
                            float bowl = Mathf.Max(-c.depth * (1f - d * d), floorLimit);
                            delta = bowl + c.rim * Smooth01(0.55f, 1f, d);
                        }
                        else
                        {
                            delta = c.rim * Mathf.Pow(d, -4f) * Mathf.Clamp01((reach - d) / 0.6f);
                        }
                        h[z, x] += delta;

                        float e = c.freshness * Mathf.Clamp01(1.5f - d * 0.55f) * (d > 0.8f ? 1f : 0.35f);
                        if (e > ejecta[z, x]) ejecta[z, x] = e;
                    }

                    if (hasRays && d > 1f && d < 8f)
                    {
                        float s = Mathf.Sin(theta * 11f + c.phase1) * Mathf.Sin(theta * 7f + c.phase2) * Mathf.Sin(theta * 23f + c.phase1 * 3f);
                        s = Mathf.Max(0f, s);
                        float ray = s * s * Mathf.Clamp01(1f - (d - 1f) / 7f) * (c.freshness - 0.6f) * 2.5f;
                        if (ray > rays[z, x]) rays[z, x] = ray;
                    }
                }
            }
        }

        static float[,] BoxBlur(float[,] src, int radius)
        {
            int n = src.GetLength(0);
            var tmp = new float[n, n];
            var dst = new float[n, n];
            float inv = 1f / (radius * 2 + 1);
            for (int z = 0; z < n; z++)
            {
                float sum = 0f;
                for (int x = -radius; x <= radius; x++) sum += src[z, Mathf.Clamp(x, 0, n - 1)];
                for (int x = 0; x < n; x++)
                {
                    tmp[z, x] = sum * inv;
                    sum += src[z, Mathf.Min(n - 1, x + radius + 1)] - src[z, Mathf.Max(0, x - radius)];
                }
            }
            for (int x = 0; x < n; x++)
            {
                float sum = 0f;
                for (int z = -radius; z <= radius; z++) sum += tmp[Mathf.Clamp(z, 0, n - 1), x];
                for (int z = 0; z < n; z++)
                {
                    dst[z, x] = sum * inv;
                    sum += tmp[Mathf.Min(n - 1, z + radius + 1), x] - tmp[Mathf.Max(0, z - radius), x];
                }
            }
            return dst;
        }

        static Texture2D BakeMacroAlbedo(float[,] h, float[,] ejecta, float[,] rays, float cell, float ox, float oz)
        {
            float[,] blurred = BoxBlur(h, 6);
            int m = MacroResolution;
            var pixels = new Color32[m * m];
            float texel = Size / m;

            for (int pz = 0; pz < m; pz++)
            {
                float wz = (pz + 0.5f) * texel - Half;
                int hz = Mathf.Clamp(Mathf.RoundToInt((wz + Half) / cell), 1, Resolution - 2);
                for (int px = 0; px < m; px++)
                {
                    float wx = (px + 0.5f) * texel - Half;
                    int hx = Mathf.Clamp(Mathf.RoundToInt((wx + Half) / cell), 1, Resolution - 2);

                    float gx = (h[hz, hx + 1] - h[hz, hx - 1]) / (2f * cell);
                    float gz = (h[hz + 1, hx] - h[hz - 1, hx]) / (2f * cell);
                    float slope = Mathf.Sqrt(gx * gx + gz * gz);
                    float cavity = Mathf.Clamp(h[hz, hx] - blurred[hz, hx], -3f, 1f);

                    float a = 0.40f
                              + 0.07f * Noise(wx, wz, 600f, ox + 11f, oz)       // mare tone patches
                              + 0.04f * Noise(wx, wz, 150f, ox + 12f, oz)
                              + 0.025f * Noise(wx, wz, 18f, ox + 13f, oz)
                              + ejecta[hz, hx] * 0.07f                          // fresh bright ejecta
                              + rays[hz, hx] * 0.06f                            // ray systems
                              + cavity * 0.012f                                 // darker crater floors
                              + Mathf.Clamp(slope - 0.25f, 0f, 0.6f) * 0.08f;   // fresh exposed slopes
                    a = Mathf.Clamp(a, 0.25f, 0.62f);
                    byte r = (byte)(a * 255f), g = (byte)(a * 0.99f * 255f), b = (byte)(a * 0.975f * 255f);
                    pixels[pz * m + px] = new Color32(r, g, b, 255);
                }
            }

            var tex = new Texture2D(m, m, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            MoonAssetFactory.EnsureFolder(MoonPaths.Terrain);
            string path = MoonPaths.Terrain + "/T_Terrain_MacroAlbedo.png";
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.anisoLevel = 8;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Layers: 0 regolith A (fine), 1 regolith B (pebbly), 2 rocky slopes, 3 macro albedo (constant share).
        /// The two detail layers use non-multiple tile sizes and are mixed by noise, so repetition is invisible.
        /// </summary>
        static float[,,] BuildSplat(float[,] h, float cell, float ox, float oz)
        {
            int res = SplatResolution;
            var map = new float[res, res, 4];
            const float macroShare = 0.5f;
            float step = Size / (res - 1);

            for (int z = 0; z < res; z++)
            {
                float wz = z * step - Half;
                int hz = Mathf.Clamp(Mathf.RoundToInt(z * (Resolution - 1) / (float)(res - 1)), 1, Resolution - 2);
                for (int x = 0; x < res; x++)
                {
                    float wx = x * step - Half;
                    int hx = Mathf.Clamp(Mathf.RoundToInt(x * (Resolution - 1) / (float)(res - 1)), 1, Resolution - 2);
                    float gx = (h[hz, hx + 1] - h[hz, hx - 1]) / (2f * cell);
                    float gz = (h[hz + 1, hx] - h[hz - 1, hx]) / (2f * cell);
                    float slope = Mathf.Sqrt(gx * gx + gz * gz);

                    float rocky = Smooth01(0.30f, 0.8f, slope) * 0.8f;
                    float mixAB = Smooth01(-0.18f, 0.18f, Noise(wx, wz, 38f, ox + 21f, oz) + 0.5f * Noise(wx, wz, 9f, ox + 22f, oz));
                    float detail = 1f - macroShare;
                    map[z, x, 0] = detail * (1f - rocky) * mixAB;
                    map[z, x, 1] = detail * (1f - rocky) * (1f - mixAB);
                    map[z, x, 2] = detail * rocky;
                    map[z, x, 3] = macroShare;
                }
            }
            return map;
        }

        static void BuildHorizonRing(Transform parent, Material material, int seed)
        {
            const int rings = 64, segments = 200;
            const float inner = 990f;
            float outer = HorizonOuterRadius;
            var vertices = new Vector3[(rings + 1) * segments];
            var uvs = new Vector2[vertices.Length];
            float ox = seed * 0.137f % 100f;

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float radius = inner * Mathf.Pow(outer / inner, t);
                for (int s = 0; s < segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    float x = Mathf.Cos(a) * radius, z = Mathf.Sin(a) * radius;
                    float hills = Smooth01(1500f, 4500f, radius) * (Mathf.PerlinNoise(x / 2500f + ox, z / 2500f) - 0.45f) * 90f
                                + Smooth01(1100f, 2200f, radius) * (Mathf.PerlinNoise(x / 420f + ox, z / 420f) - 0.5f) * 10f;
                    float curvature = -(radius * radius) / (2f * MoonRadius);
                    // hills fade out towards the far edge so nothing pokes above the true horizon
                    hills *= 1f - Smooth01(40000f, outer, radius);
                    vertices[r * segments + s] = new Vector3(x, -0.05f + curvature + hills, z);
                    uvs[r * segments + s] = new Vector2(x, z) / 9f;
                }
            }

            var tris = new List<int>(rings * segments * 6);
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * segments + s;
                    int b = r * segments + (s + 1) % segments;
                    int c = (r + 1) * segments + s;
                    int d = (r + 1) * segments + (s + 1) % segments;
                    // winding chosen so the surface faces up (verified)
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }

            var mesh = new Mesh { name = "M_HorizonRing" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            MoonAssetFactory.EnsureFolder(MoonPaths.Meshes);
            string path = MoonPaths.Meshes + "/M_HorizonRing.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject("HorizonRing");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            var collider = go.AddComponent<MeshCollider>();   // the lander can fly (and crash) beyond the detailed terrain
            collider.sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        public static float GroundHeight(Terrain terrain, Vector3 world)
        {
            return terrain.SampleHeight(world) + terrain.transform.position.y;
        }

        public static Vector3 GroundNormal(Terrain terrain, Vector3 world)
        {
            Vector3 local = world - terrain.transform.position;
            return terrain.terrainData.GetInterpolatedNormal(local.x / Size, local.z / Size);
        }
    }
}
