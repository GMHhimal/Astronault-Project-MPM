using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Procedural PBR texture generation.
    ///
    /// The upgrade here is normal mapping. Surface detail - panel seams, rivets,
    /// honeycomb cells, crinkled foil, regolith grain - costs nothing in triangles
    /// when it lives in a normal map, and it is what separates a model that reads as
    /// "untextured grey shape" from one that reads as built hardware. Every material
    /// gets three maps:
    ///
    ///   Albedo   base colour, with ambient occlusion multiplied in
    ///   Normal   tangent-space, derived by Sobel filter from a height field
    ///   MaskMap  R = metallic, G = occlusion, A = smoothness (URP Lit mask layout)
    ///
    /// Everything is synthesised from noise, so the project still ships with no
    /// third-party image assets and no licensing to declare.
    /// </summary>
    public static class ProcTexture2
    {
        public const string Folder = "Assets/OrionAR/Textures";

        /// <summary>Per-pixel surface description the generators fill in.</summary>
        public struct Surface
        {
            public Color albedo;
            public float height;      // 0..1, drives the normal map
            public float metallic;    // 0..1
            public float smoothness;  // 0..1
            public float occlusion;   // 0..1, 1 = fully open
        }

        public delegate Surface SurfaceFunc(float u, float v);

        // ================================================================
        // Generation entry point
        // ================================================================

        /// <summary>
        /// Evaluates the surface function across the texture and writes the three maps
        /// as PNG assets, configured with the right import settings.
        /// </summary>
        public static void Generate(string baseName, int size, SurfaceFunc fn,
                                    float normalStrength = 2.2f, bool generateMask = true)
        {
            AssetPaths.EnsureFolder(Folder);

            var albedo = new Color[size * size];
            var height = new float[size * size];
            var mask = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    Surface s = fn(u, v);

                    int i = y * size + x;
                    height[i] = Mathf.Clamp01(s.height);

                    // Bake occlusion into albedo as well as the mask: on mobile the
                    // mask map's AO channel is sometimes skipped by simpler shaders,
                    // and a surface with no AO at all looks plastic.
                    float ao = Mathf.Clamp01(s.occlusion);
                    albedo[i] = new Color(s.albedo.r * (0.55f + 0.45f * ao),
                                          s.albedo.g * (0.55f + 0.45f * ao),
                                          s.albedo.b * (0.55f + 0.45f * ao), 1f);

                    mask[i] = new Color(Mathf.Clamp01(s.metallic), ao, 0f,
                                        Mathf.Clamp01(s.smoothness));
                }
            }

            WritePng(baseName + "_Albedo", size, albedo, srgb: true);

            var normal = HeightToNormal(height, size, normalStrength);
            WritePng(baseName + "_Normal", size, normal, srgb: false, isNormalMap: true);

            if (generateMask)
                WritePng(baseName + "_Mask", size, mask, srgb: false);
        }

        /// <summary>
        /// Sobel filter from height to tangent-space normal.
        ///
        /// Wraps at the edges so tiling surfaces have no visible seam.
        /// </summary>
        static Color[] HeightToNormal(float[] height, int size, float strength)
        {
            var normal = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size, xp = (x + 1) % size;
                    int ym = (y - 1 + size) % size, yp = (y + 1) % size;

                    float tl = height[ym * size + xm], t = height[ym * size + x], tr = height[ym * size + xp];
                    float l  = height[y * size + xm],                             r  = height[y * size + xp];
                    float bl = height[yp * size + xm], b = height[yp * size + x], br = height[yp * size + xp];

                    float dx = (tr + 2f * r + br) - (tl + 2f * l + bl);
                    float dy = (bl + 2f * b + br) - (tl + 2f * t + tr);

                    var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;

                    normal[y * size + x] = new Color(n.x * 0.5f + 0.5f,
                                                     n.y * 0.5f + 0.5f,
                                                     n.z * 0.5f + 0.5f, 1f);
                }
            }
            return normal;
        }

        static void WritePng(string assetName, int size, Color[] pixels,
                             bool srgb, bool isNormalMap = false)
        {
            string path = Folder + "/" + assetName + ".png";

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = isNormalMap ? TextureImporterType.NormalMap
                                              : TextureImporterType.Default;
                imp.sRGBTexture = srgb;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 4;
                imp.maxTextureSize = size;

                // ASTC is the right mobile format: better quality per byte than ETC2
                // and supported by every ARCore-capable device.
                var android = new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = size,
                    format = TextureImporterFormat.ASTC_6x6,
                    textureCompression = TextureImporterCompression.Compressed,
                };
                imp.SetPlatformTextureSettings(android);
                imp.SaveAndReimport();
            }
        }

        public static Texture2D Load(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + assetName + ".png");
        }

        // ================================================================
        // The material library
        // ================================================================

        public static void GenerateAll()
        {
            OrionHull(1024);
            HeatShield(1024);
            ThermalFoil(1024);
            ESMBlanket(1024);
            CabinPanel(1024);
            SolarCells(512);
            BrushedMetal(512);
            LunarRegolith(1024);
            PaintedWhite(512);
            CarbonComposite(512);
            AssetDatabase.Refresh();
        }

        /// <summary>Orion's back shell: thermal tiles, panel seams, fastener rows.</summary>
        public static void OrionHull(int size)
        {
            Generate("T_OrionHull", size, (u, v) =>
            {
                var s = new Surface();

                const float tilesU = 16f, tilesV = 10f;
                float seam = Mathf.Min(Seam(u, tilesU, 0.018f), Seam(v, tilesV, 0.022f));

                int tx = Mathf.FloorToInt(u * tilesU), ty = Mathf.FloorToInt(v * tilesV);
                float tileVar = 0.93f + Hash2(tx, ty) * 0.12f;

                float grime = Fbm(u * 24f, v * 24f, 4, 2.1f, 0.5f);
                float micro = Fbm(u * 180f, v * 180f, 2, 2f, 0.5f);

                // Fastener row along the top of every tile.
                float boltU = Frac(u * tilesU * 4f) - 0.5f;
                float boltV = Frac(v * tilesV) - 0.12f;
                float boltD = Mathf.Sqrt(boltU * boltU * 0.25f + boltV * boltV);
                float bolt = Mathf.SmoothStep(0.016f, 0.03f, boltD);

                var baseCol = new Color(0.78f, 0.79f, 0.81f);
                s.albedo = baseCol * tileVar * (0.94f + grime * 0.12f) *
                           Mathf.Lerp(0.55f, 1f, seam) * Mathf.Lerp(0.72f, 1f, bolt);

                s.height = 0.5f + seam * 0.34f - (1f - bolt) * 0.3f + micro * 0.06f;
                s.metallic = 0.12f + (1f - bolt) * 0.6f;
                s.smoothness = Mathf.Lerp(0.22f, 0.52f, seam) * (0.85f + grime * 0.2f);
                s.occlusion = Mathf.Lerp(0.45f, 1f, seam) * Mathf.Lerp(0.7f, 1f, bolt);
                return s;
            }, normalStrength: 3.0f);
        }

        /// <summary>AVCOAT ablator: honeycomb cells, charred toward the stagnation point.</summary>
        public static void HeatShield(int size)
        {
            Generate("T_HeatShield", size, (u, v) =>
            {
                var s = new Surface();

                float cx = u - 0.5f, cy = v - 0.5f;
                float radial = Mathf.Clamp01(Mathf.Sqrt(cx * cx + cy * cy) * 2f);

                float cell = Honeycomb(u * 64f, v * 64f);
                float charNoise = Fbm(u * 14f, v * 14f, 5, 2.2f, 0.55f);
                float crack = Mathf.SmoothStep(0.62f, 0.72f, Fbm(u * 40f, v * 40f, 3, 2f, 0.5f));

                var fresh = new Color(0.40f, 0.29f, 0.22f);
                var charred = new Color(0.075f, 0.068f, 0.065f);

                float charAmount = Mathf.Clamp01(radial * 0.8f + charNoise * 0.35f);
                s.albedo = Color.Lerp(charred, fresh, charAmount) * (0.8f + cell * 0.32f);

                s.height = 0.45f + cell * 0.4f - crack * 0.18f;
                s.metallic = 0.02f;
                s.smoothness = Mathf.Lerp(0.06f, 0.2f, charAmount) * (1f - crack * 0.5f);
                s.occlusion = Mathf.Lerp(0.5f, 1f, cell) * Mathf.Lerp(0.75f, 1f, 1f - crack);
                return s;
            }, normalStrength: 3.4f);
        }

        /// <summary>Multi-layer insulation: gold, and never flat.</summary>
        public static void ThermalFoil(int size)
        {
            Generate("T_ThermalFoil", size, (u, v) =>
            {
                var s = new Surface();

                // Layered noise at different scales produces the characteristic
                // irregular crumple of MLI blanketing.
                float coarse = Fbm(u * 7f, v * 7f, 3, 2.3f, 0.6f);
                float mid = Fbm(u * 26f + coarse * 2f, v * 26f, 4, 2.1f, 0.55f);
                float fine = Fbm(u * 60f, v * 60f, 2, 2f, 0.5f);
                float crinkle = coarse * 0.55f + mid * 0.35f + fine * 0.10f;

                // Stitch lines holding the blanket down.
                float stitch = Mathf.Min(Seam(u, 6f, 0.008f), Seam(v, 4f, 0.008f));

                var gold = new Color(0.88f, 0.655f, 0.23f);
                s.albedo = gold * (0.62f + crinkle * 0.55f) * Mathf.Lerp(0.6f, 1f, stitch);

                s.height = crinkle * 0.85f + stitch * 0.15f;
                s.metallic = 0.92f;
                s.smoothness = Mathf.Lerp(0.42f, 0.78f, crinkle);
                s.occlusion = Mathf.Lerp(0.55f, 1f, crinkle) * Mathf.Lerp(0.7f, 1f, stitch);
                return s;
            }, normalStrength: 2.6f);
        }

        /// <summary>
        /// The service module's outer thermal blanket, as flown on Artemis I: off-white
        /// beta cloth over MLI, quilted into stitched panels with a soft sheen. The
        /// earlier build wrapped the whole ESM in high-frequency gold crinkle, which read
        /// as noise at phone resolution and is not what the real vehicle looks like.
        /// </summary>
        public static void ESMBlanket(int size)
        {
            Generate("T_ESMBlanket", size, (u, v) =>
            {
                var s = new Surface();

                const float qu = 12f, qv = 8f;           // quilt panels around and along
                float fu = Frac(u * qu) - 0.5f, fv = Frac(v * qv) - 0.5f;

                // Each panel puffs gently outward between its stitch lines.
                float puff = Mathf.Clamp01(1f - (fu * fu + fv * fv) * 3.2f);
                puff = Mathf.SmoothStep(0f, 1f, puff);
                float stitch = Mathf.Min(Seam(u, qu, 0.010f), Seam(v, qv, 0.012f));

                int tx = Mathf.FloorToInt(u * qu), ty = Mathf.FloorToInt(v * qv);
                float panelTint = 0.965f + Hash2(tx, ty) * 0.05f;

                float soft = Fbm(u * 10f, v * 10f, 3, 2.1f, 0.5f);     // broad, low-contrast wrinkles
                float weave = Fbm(u * 260f, v * 260f, 2, 2f, 0.5f);    // fabric tooth

                var cloth = new Color(0.86f, 0.865f, 0.855f);
                s.albedo = cloth * panelTint * (0.93f + soft * 0.08f + weave * 0.03f) *
                           Mathf.Lerp(0.70f, 1f, stitch);

                s.height = 0.35f + puff * 0.35f + soft * 0.12f + weave * 0.02f - (1f - stitch) * 0.2f;
                s.metallic = 0.08f;
                s.smoothness = Mathf.Lerp(0.22f, 0.40f, puff) * (0.9f + soft * 0.15f);
                s.occlusion = Mathf.Lerp(0.62f, 1f, stitch) * Mathf.Lerp(0.85f, 1f, puff);
                return s;
            }, normalStrength: 2.0f);
        }

        /// <summary>Interior wall panels with fasteners and equipment rails.</summary>
        public static void CabinPanel(int size)
        {
            Generate("T_CabinPanel", size, (u, v) =>
            {
                var s = new Surface();

                const float pu = 8f, pv = 6f;
                float seam = Mathf.Min(Seam(u, pu, 0.014f), Seam(v, pv, 0.018f));

                float fu = Frac(u * pu) - 0.5f, fv = Frac(v * pv) - 0.5f;
                float cornerD = Mathf.Sqrt((Mathf.Abs(fu) - 0.42f) * (Mathf.Abs(fu) - 0.42f) +
                                           (Mathf.Abs(fv) - 0.40f) * (Mathf.Abs(fv) - 0.40f));
                float bolt = Mathf.SmoothStep(0.018f, 0.032f, cornerD);

                float grain = Fbm(u * 120f, v * 120f, 3, 2f, 0.5f);
                float scuff = Fbm(u * 18f, v * 18f, 4, 2.2f, 0.55f);

                var baseCol = new Color(0.74f, 0.755f, 0.775f);
                s.albedo = baseCol * (0.93f + grain * 0.1f) *
                           Mathf.Lerp(0.6f, 1f, seam) * Mathf.Lerp(0.68f, 1f, bolt);

                s.height = 0.5f + seam * 0.26f - (1f - bolt) * 0.34f + grain * 0.04f;
                s.metallic = 0.2f + (1f - bolt) * 0.55f;
                s.smoothness = Mathf.Lerp(0.3f, 0.48f, scuff) * Mathf.Lerp(0.7f, 1f, seam);
                s.occlusion = Mathf.Lerp(0.5f, 1f, seam) * Mathf.Lerp(0.65f, 1f, bolt);
                return s;
            }, normalStrength: 2.6f);
        }

        public static void SolarCells(int size)
        {
            Generate("T_SolarCells", size, (u, v) =>
            {
                var s = new Surface();

                float cellU = Seam(u, 12f, 0.030f);
                float cellV = Seam(v, 26f, 0.022f);
                float seam = Mathf.Min(cellU, cellV);

                // Fine busbar fingers across each cell.
                float finger = Seam(v, 26f * 8f, 0.14f);
                float sheen = Fbm(u * 9f, v * 9f, 2, 2f, 0.5f);

                var cell = new Color(0.055f, 0.085f, 0.235f) * (0.85f + sheen * 0.3f);
                var busbar = new Color(0.66f, 0.68f, 0.72f);

                s.albedo = Color.Lerp(busbar, cell * Mathf.Lerp(0.88f, 1f, finger), seam);
                s.height = 0.5f + seam * 0.3f + finger * 0.06f;
                s.metallic = Mathf.Lerp(0.85f, 0.35f, seam);
                s.smoothness = Mathf.Lerp(0.62f, 0.86f, seam);
                s.occlusion = Mathf.Lerp(0.6f, 1f, seam);
                return s;
            }, normalStrength: 2.0f);
        }

        public static void BrushedMetal(int size)
        {
            Generate("T_Metal", size, (u, v) =>
            {
                var s = new Surface();

                // Anisotropic streaking: brushing runs in one direction.
                float brush = Fbm(u * 420f, v * 5f, 3, 2f, 0.5f);
                float blotch = Fbm(u * 16f, v * 16f, 3, 2.2f, 0.5f);

                var baseCol = new Color(0.63f, 0.645f, 0.675f);
                s.albedo = baseCol * (0.9f + brush * 0.16f) * (0.94f + blotch * 0.1f);
                s.height = 0.5f + brush * 0.1f;
                s.metallic = 0.95f;
                s.smoothness = 0.52f + brush * 0.22f;
                s.occlusion = 1f;
                return s;
            }, normalStrength: 1.1f);
        }

        /// <summary>
        /// Lunar regolith.
        ///
        /// Grey, but not a flat grey - the Moon's surface has subtle warm and cool
        /// variation between mare basalt and highland anorthosite, plus the fine
        /// fragmental texture of material pulverised by billions of years of impacts.
        /// </summary>
        public static void LunarRegolith(int size)
        {
            Generate("T_LunarRegolith", size, (u, v) =>
            {
                var s = new Surface();

                float broad = Fbm(u * 4f, v * 4f, 4, 2.1f, 0.55f);
                float mid = Fbm(u * 22f, v * 22f, 5, 2.2f, 0.52f);
                float grain = Fbm(u * 140f, v * 140f, 3, 2f, 0.5f);

                // Scattered small craters.
                float craters = 0f;
                for (int i = 0; i < 5; i++)
                {
                    float cx = Hash2(i * 17, 3) , cy = Hash2(i * 29, 7);
                    float rad = 0.035f + Hash2(i * 41, 11) * 0.05f;
                    float d = Mathf.Sqrt((Frac(u - cx + 0.5f) - 0.5f) * (Frac(u - cx + 0.5f) - 0.5f) +
                                         (Frac(v - cy + 0.5f) - 0.5f) * (Frac(v - cy + 0.5f) - 0.5f));
                    if (d < rad) craters -= Mathf.Cos(d / rad * Mathf.PI * 0.5f) * 0.4f;
                    else if (d < rad * 1.3f) craters += 0.12f;
                }

                var dark = new Color(0.185f, 0.180f, 0.176f);   // mare basalt
                var light = new Color(0.455f, 0.448f, 0.435f);  // highland
                float tone = Mathf.Clamp01(broad * 0.62f + mid * 0.38f);

                s.albedo = Color.Lerp(dark, light, tone) * (0.9f + grain * 0.18f);
                s.height = 0.45f + mid * 0.3f + grain * 0.18f + craters * 0.4f;

                // Regolith is dielectric and extremely rough - almost no specular.
                s.metallic = 0.0f;
                s.smoothness = 0.035f + grain * 0.03f;
                s.occlusion = Mathf.Clamp01(0.65f + mid * 0.35f + craters * 0.5f);
                return s;
            }, normalStrength: 3.2f);
        }

        public static void PaintedWhite(int size)
        {
            Generate("T_PaintedWhite", size, (u, v) =>
            {
                var s = new Surface();
                float panel = Mathf.Min(Seam(u, 5f, 0.012f), Seam(v, 7f, 0.012f));
                float orange = Fbm(u * 110f, v * 110f, 3, 2f, 0.5f);   // orange-peel finish

                s.albedo = new Color(0.90f, 0.905f, 0.91f) * (0.95f + orange * 0.08f) *
                           Mathf.Lerp(0.72f, 1f, panel);
                s.height = 0.5f + panel * 0.2f + orange * 0.05f;
                s.metallic = 0.05f;
                s.smoothness = 0.46f + orange * 0.1f;
                s.occlusion = Mathf.Lerp(0.65f, 1f, panel);
                return s;
            }, normalStrength: 1.8f);
        }

        public static void CarbonComposite(int size)
        {
            Generate("T_Carbon", size, (u, v) =>
            {
                var s = new Surface();

                // Over-under twill weave.
                float wu = Frac(u * 40f), wv = Frac(v * 40f);
                int cu = Mathf.FloorToInt(u * 40f), cv = Mathf.FloorToInt(v * 40f);
                bool over = ((cu + cv) & 1) == 0;
                float strand = over ? Mathf.Sin(wu * Mathf.PI) : Mathf.Sin(wv * Mathf.PI);

                float sheen = Fbm(u * 12f, v * 12f, 2, 2f, 0.5f);

                s.albedo = new Color(0.075f, 0.078f, 0.085f) * (0.75f + strand * 0.5f) *
                           (0.9f + sheen * 0.2f);
                s.height = 0.4f + strand * 0.45f;
                s.metallic = 0.25f;
                s.smoothness = 0.5f + strand * 0.25f;
                s.occlusion = 0.6f + strand * 0.4f;
                return s;
            }, normalStrength: 2.4f);
        }

        // ================================================================
        // Noise
        // ================================================================

        static float Frac(float x) { return x - Mathf.Floor(x); }

        /// <summary>
        /// Tiling fractal Brownian motion.
        ///
        /// Unity's PerlinNoise does not tile, so a texture built from it shows a visible
        /// seam when repeated. Blending the four wrapped corners makes it seamless,
        /// which matters because these textures tile across the whole hull.
        /// </summary>
        public static float Fbm(float x, float y, int octaves, float lacunarity, float gain)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += TilingNoise(x * freq, y * freq, 256f * freq) * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        static float TilingNoise(float x, float y, float period)
        {
            float px = Mathf.Max(1f, period);
            float fx = Frac(x / px), fy = Frac(y / px);

            float n00 = Mathf.PerlinNoise(x, y);
            float n10 = Mathf.PerlinNoise(x - px, y);
            float n01 = Mathf.PerlinNoise(x, y - px);
            float n11 = Mathf.PerlinNoise(x - px, y - px);

            return Mathf.Lerp(Mathf.Lerp(n00, n10, fx), Mathf.Lerp(n01, n11, fx), fy);
        }

        /// <summary>Returns 0 inside a seam line and 1 away from it, antialiased.</summary>
        public static float Seam(float coord, float repeats, float width)
        {
            float f = Frac(coord * repeats);
            float d = Mathf.Min(f, 1f - f);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / Mathf.Max(0.0001f, width)));
        }

        public static float Honeycomb(float x, float y)
        {
            float hy = y * 0.8660254f;                    // sqrt(3)/2, hex row spacing
            float row = Mathf.Floor(hy);
            float offset = (Mathf.Abs(row) % 2f < 1f) ? 0f : 0.5f;
            float fx = Frac(x + offset) - 0.5f;
            float fy = Frac(hy) - 0.5f;
            float d = Mathf.Sqrt(fx * fx + fy * fy);
            return Mathf.SmoothStep(0.28f, 0.46f, d);
        }

        public static float Hash2(int x, int y)
        {
            unchecked
            {
                int n = x * 73856093 ^ y * 19349663;
                n = (n << 13) ^ n;
                int m = n * (n * n * 15731 + 789221) + 1376312589;
                return (m & 0x7fffffff) / 2147483647f;
            }
        }
    }
}
