using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Astronaut.MoonLanding.EditorTools
{
    public static class MoonPaths
    {
        public const string Feature = "Assets/_Project/Features/MoonLanding";
        public const string Textures = Feature + "/Textures/Generated";
        public const string Audio = Feature + "/Audio/SFX";
        public const string Generated = Feature + "/Generated";
        public const string Materials = Generated + "/Materials";
        public const string Meshes = Generated + "/Meshes";
        public const string Terrain = Generated + "/Terrain";
        public const string Scene = "Assets/_Project/Scenes/MoonLanding_Scene.unity";
        public const string LanderModel = "Assets/ThirdParty/Sketchfab/Lunar Model/apollo_11_lunar_module.glb";
        /// <summary>Optional: drop a NASA Blue Marble equirectangular texture here to replace the placeholder Earth.</summary>
        public const string EarthOverride = Feature + "/Textures/T_Earth_BlueMarble.jpg";
    }

    /// <summary>Creates / updates all generated assets for the Moon Landing level.</summary>
    public static class MoonAssetFactory
    {
        public class MaterialSet
        {
            public Material terrain, horizon, rock, sky, earth, beacon, lzPost;
            public Material dust, smoke, fire, spark, flash, hologram, beam;
            public TerrainLayer[] terrainLayers;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ textures
        public static void ConfigureTextures()
        {
            ConfigureTexture("T_RegolithA_Albedo.png", TexKind.Albedo);
            ConfigureTexture("T_RegolithA_Normal.png", TexKind.Normal);
            ConfigureTexture("T_RegolithB_Albedo.png", TexKind.Albedo);
            ConfigureTexture("T_RegolithB_Normal.png", TexKind.Normal);
            ConfigureTexture("T_Horizon_Macro.png", TexKind.Albedo);
            ConfigureTexture("T_Horizon_Detail.png", TexKind.Linear);
            ConfigureTexture("T_Rock_Albedo.png", TexKind.Albedo);
            ConfigureTexture("T_Rock_Normal.png", TexKind.Normal);
            ConfigureTexture("FX_SoftParticle.png", TexKind.Fx);
            ConfigureTexture("FX_SmokePuff.png", TexKind.Fx);
            ConfigureTexture("FX_Spark.png", TexKind.Fx);
            ConfigureTexture("FX_Flare.png", TexKind.Fx);
            ConfigureTexture("FX_BeamGradient.png", TexKind.Fx);
            ConfigureTexture("SKY_Starfield_Equirect.png", TexKind.Sky);
            ConfigureTexture("T_Earth_Placeholder.png", TexKind.Sky);
        }

        enum TexKind { Albedo, Normal, Fx, Sky, Linear }

        static void ConfigureTexture(string file, TexKind kind)
        {
            string path = MoonPaths.Textures + "/" + file;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("[MoonLanding] Missing texture: " + path); return; }

            importer.textureType = kind == TexKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = kind != TexKind.Normal && kind != TexKind.Linear;
            importer.alphaSource = kind == TexKind.Fx ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = kind == TexKind.Fx;
            importer.mipmapEnabled = kind != TexKind.Sky;
            importer.wrapMode = kind == TexKind.Fx ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            if (kind == TexKind.Sky) { importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = TextureWrapMode.Clamp; }
            importer.anisoLevel = (kind == TexKind.Albedo || kind == TexKind.Normal) ? 8 : 1;
            importer.maxTextureSize = kind == TexKind.Sky ? 4096 : 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        static Texture2D Tex(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(MoonPaths.Textures + "/" + file);
        }

        // ------------------------------------------------------------------ materials
        static Material GetOrCreate(string name, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception("Shader not found: " + shaderName + " (is URP installed and active?)");
            string path = MoonPaths.Materials + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            return mat;
        }

        static void SetupLit(Material m, Texture albedo, Texture normal, Color color, float smoothness, float metallic, Vector2 tiling)
        {
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", color);
            m.SetTextureScale("_BaseMap", tiling);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        static void SetupEmissive(Material m, Color emission)
        {
            m.SetColor("_EmissionColor", emission);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
        }

        static void SetupParticle(Material m, Texture tex, Color color, bool additive, bool soft)
        {
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            if (m.HasProperty("_SrcBlendAlpha"))
            {
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            }
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            if (soft && m.HasProperty("_SoftParticlesEnabled"))
            {
                m.SetFloat("_SoftParticlesEnabled", 1f);
                m.SetFloat("_SoftParticlesNearFadeDistance", 0f);
                m.SetFloat("_SoftParticlesFarFadeDistance", 1.5f);
                m.EnableKeyword("_SOFTPARTICLES_ON");
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
        }

        public static MaterialSet CreateMaterials()
        {
            EnsureFolder(MoonPaths.Materials);
            const string lit = "Universal Render Pipeline/Lit";
            const string particle = "Universal Render Pipeline/Particles/Unlit";
            var set = new MaterialSet();

            RemoveObsoleteAssets();
            Texture2D regAlbA = Tex("T_RegolithA_Albedo.png"), regNrmA = Tex("T_RegolithA_Normal.png");
            Texture2D regAlbB = Tex("T_RegolithB_Albedo.png"), regNrmB = Tex("T_RegolithB_Normal.png");
            Texture2D rockAlb = Tex("T_Rock_Albedo.png"), rockNrm = Tex("T_Rock_Normal.png");

            set.terrain = GetOrCreate("MAT_MoonTerrain", "Universal Render Pipeline/Terrain/Lit");
            set.terrain.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(set.terrain);

            set.horizon = GetOrCreate("MAT_HorizonRing", lit);
            // uv = worldXZ / 9  →  one texture tile = 1500 m (craters are pre-shaded for the fixed sun)
            SetupLit(set.horizon, Tex("T_Horizon_Macro.png"), null, Color.white, 0.04f, 0f, Vector2.one * (9f / 1500f));
            set.horizon.SetTexture("_BumpMap", null);
            set.horizon.DisableKeyword("_NORMALMAP");
            // Very large-scale brightness variation (one tile ≈ 23 km) so the 1.5 km base tile never repeats visibly far away.
            // URP Lit "detail albedo ×2": a linear (sRGB off) grey of 0.5 means "no change".
            set.horizon.SetTexture("_DetailAlbedoMap", Tex("T_Horizon_Detail.png"));
            set.horizon.SetTextureScale("_DetailAlbedoMap", Vector2.one * (9f / 23000f));
            set.horizon.SetFloat("_DetailAlbedoMapScale", 1f);
            set.horizon.EnableKeyword("_DETAIL_MULX2");

            set.rock = GetOrCreate("MAT_Rock", lit);
            SetupLit(set.rock, rockAlb, rockNrm, new Color(0.85f, 0.84f, 0.82f), 0.12f, 0f, Vector2.one);

            set.sky = GetOrCreate("MAT_Sky_Starfield", "Skybox/Panoramic");
            set.sky.SetTexture("_MainTex", Tex("SKY_Starfield_Equirect.png"));
            set.sky.SetFloat("_Exposure", 1.0f);
            set.sky.SetFloat("_Mapping", 1f);
            set.sky.SetFloat("_ImageType", 0f);
            set.sky.SetFloat("_Rotation", 0f);
            set.sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));
            EditorUtility.SetDirty(set.sky);

            Texture2D earthTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MoonPaths.EarthOverride);
            if (earthTex == null) earthTex = Tex("T_Earth_Placeholder.png");
            set.earth = GetOrCreate("MAT_Earth", lit);
            SetupLit(set.earth, earthTex, null, Color.white, 0.35f, 0f, Vector2.one);

            set.beacon = GetOrCreate("MAT_Beacon", lit);
            SetupLit(set.beacon, null, null, new Color(0.2f, 0.2f, 0.2f), 0.6f, 0.2f, Vector2.one);
            SetupEmissive(set.beacon, new Color(0.4f, 1f, 1f) * 4f);

            set.lzPost = GetOrCreate("MAT_LZPost", lit);
            SetupLit(set.lzPost, null, null, new Color(0.55f, 0.55f, 0.58f), 0.45f, 0.8f, Vector2.one);

            set.dust = GetOrCreate("MAT_FX_Dust", particle);
            SetupParticle(set.dust, Tex("FX_SmokePuff.png"), new Color(0.62f, 0.6f, 0.57f, 1f), false, true);
            set.smoke = GetOrCreate("MAT_FX_Smoke", particle);
            SetupParticle(set.smoke, Tex("FX_SmokePuff.png"), new Color(0.8f, 0.8f, 0.8f, 1f), false, true);
            set.fire = GetOrCreate("MAT_FX_Fire", particle);
            SetupParticle(set.fire, Tex("FX_SmokePuff.png"), new Color(3.2f, 2.0f, 1.1f, 1f), true, true);
            set.spark = GetOrCreate("MAT_FX_Spark", particle);
            SetupParticle(set.spark, Tex("FX_Spark.png"), new Color(4f, 3.2f, 2.2f, 1f), true, false);
            set.flash = GetOrCreate("MAT_FX_Flash", particle);
            SetupParticle(set.flash, Tex("FX_Flare.png"), new Color(5f, 4.4f, 3.6f, 1f), true, false);
            set.hologram = GetOrCreate("MAT_FX_Hologram", particle);
            SetupParticle(set.hologram, Tex("FX_SoftParticle.png"), new Color(1.6f, 3f, 3.6f, 1f), true, false);
            set.beam = GetOrCreate("MAT_FX_Beam", particle);
            SetupParticle(set.beam, Tex("FX_BeamGradient.png"), new Color(0.6f, 1.6f, 2f, 0.6f), true, false);

            // Terrain layers
            EnsureFolder(MoonPaths.Terrain);
            set.terrainLayers = new[]
            {
                // Non-multiple tile sizes + noise mixing + a baked macro layer (added by MoonTerrainBuilder) = no visible grid
                Layer("TL_RegolithA_Fine", regAlbA, regNrmA, 5.3f, new Vector4(1f, 1f, 1f, 1f), 0.05f, 1.7f),
                Layer("TL_RegolithB_Pebbly", regAlbB, regNrmB, 13.7f, new Vector4(1f, 1f, 1f, 1f), 0.05f, 1.7f),
                Layer("TL_Rocky", rockAlb, rockNrm, 7.9f, new Vector4(1.1f, 1.1f, 1.1f, 1f), 0.1f, 1.5f),
            };

            AssetDatabase.SaveAssets();
            return set;
        }

        static void RemoveObsoleteAssets()
        {
            // v1 assets that caused the visible tiling grid (all generated by this tool, safe to delete)
            string[] obsolete =
            {
                MoonPaths.Terrain + "/TL_Regolith_Fine.terrainlayer",
                MoonPaths.Terrain + "/TL_Regolith_Macro.terrainlayer",
                MoonPaths.Terrain + "/TL_Ejecta_Bright.terrainlayer",
                MoonPaths.Textures + "/T_Regolith_Albedo.png",
                MoonPaths.Textures + "/T_Regolith_Normal.png",
            };
            foreach (string path in obsolete)
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)) && AssetDatabase.LoadMainAssetAtPath(path) != null)
                    AssetDatabase.DeleteAsset(path);
        }

        public static TerrainLayer Layer(string name, Texture2D albedo, Texture2D normal, float tile, Vector4 remapMax, float smoothness, float normalScale)
        {
            string path = MoonPaths.Terrain + "/" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = albedo;
            layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(tile, tile);
            layer.normalScale = normalScale;
            layer.smoothness = smoothness;
            layer.metallic = 0f;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = remapMax;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // ------------------------------------------------------------------ library
        public static MoonLandingLibrary CreateLibrary(MaterialSet mats)
        {
            string path = MoonPaths.Generated + "/MoonLandingLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<MoonLandingLibrary>(path);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<MoonLandingLibrary>();
                AssetDatabase.CreateAsset(lib, path);
            }

            lib.dustMaterial = mats.dust;
            lib.smokeMaterial = mats.smoke;
            lib.fireMaterial = mats.fire;
            lib.sparkMaterial = mats.spark;
            lib.flashMaterial = mats.flash;
            lib.hologramMaterial = mats.hologram;

            lib.displayFont = AssetDatabase.LoadAssetAtPath<Font>(MoonPaths.Feature + "/Fonts/Orbitron-Variable.ttf");
            lib.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(MoonPaths.Feature + "/Fonts/Rajdhani-Bold.ttf");
            if (lib.displayFont == null || lib.bodyFont == null) Debug.LogWarning("[MoonLanding] HUD fonts not found in " + MoonPaths.Feature + "/Fonts — using the default font.");

            lib.engineLoop = Clip("SFX_Engine_Loop");
            lib.engineIgnite = Clip("SFX_Engine_Ignite");
            lib.engineShutdown = Clip("SFX_Engine_Shutdown");
            lib.rcsPuffs = new[] { Clip("SFX_RCS_Puff_1"), Clip("SFX_RCS_Puff_2"), Clip("SFX_RCS_Puff_3") };
            lib.sparks = new[] { Clip("SFX_Spark_1"), Clip("SFX_Spark_2"), Clip("SFX_Spark_3"), Clip("SFX_Spark_4") };
            lib.electricHumLoop = Clip("SFX_Electric_Hum_Loop");
            lib.explosion = Clip("SFX_Explosion_Big");
            lib.touchdownThud = Clip("SFX_Touchdown_Thud");
            lib.metalImpact = Clip("SFX_Metal_Impact");
            lib.cabinAmbienceLoop = Clip("SFX_Cabin_Ambience_Loop");
            lib.dustHissLoop = Clip("SFX_Dust_Hiss_Loop");
            lib.masterAlarmLoop = Clip("SFX_Master_Alarm_Loop");
            lib.warningBeep = Clip("SFX_Warning_Beep");
            lib.lowFuel = Clip("SFX_Low_Fuel");
            lib.quindarIn = Clip("SFX_Quindar_In");
            lib.quindarOut = Clip("SFX_Quindar_Out");
            lib.radioStatic = Clip("SFX_Radio_Static");
            lib.uiClick = Clip("SFX_UI_Click");
            lib.countdownBeep = Clip("SFX_Countdown_Beep");
            lib.countdownGo = Clip("SFX_Countdown_Go");
            lib.missionSuccess = Clip("SFX_Mission_Success");
            lib.missionFailed = Clip("SFX_Mission_Failed");

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        static AudioClip Clip(string name)
        {
            string path = MoonPaths.Audio + "/" + name + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning("[MoonLanding] Missing audio clip: " + path);
            return clip;
        }

        // ------------------------------------------------------------------ post processing
        public static VolumeProfile CreateVolumeProfile()
        {
            string path = MoonPaths.Generated + "/VP_MoonLanding.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.4f);
            color.contrast.Override(18f);
            color.saturation.Override(-55f);   // the Moon is almost colourless

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(2f);      // neutral / very slightly warm like Apollo Hasselblad photos

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);

            var chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0.06f);

            var blur = profile.Add<MotionBlur>(true);
            blur.mode.Override(MotionBlurMode.CameraOnly);
            blur.quality.Override(MotionBlurQuality.Medium);
            blur.intensity.Override(0.12f);

            foreach (VolumeComponent component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
