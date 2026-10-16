using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Creates and caches materials, wiring up the three generated maps.
    ///
    /// Prefers URP's Lit shader (the right choice for mobile AR) and falls back to the
    /// built-in Standard shader so the generator works in either pipeline.
    /// </summary>
    public static class MatLib2
    {
        public const string Folder = "Assets/OrionAR/Materials";

        static Shader _lit, _unlit;
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static Shader Lit
        {
            get
            {
                if (_lit == null)
                    _lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                return _lit;
            }
        }

        public static Shader Unlit
        {
            get
            {
                if (_unlit == null)
                    _unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                return _unlit;
            }
        }

        public static bool IsURP
        {
            get { return Lit != null && Lit.name.StartsWith("Universal"); }
        }

        /// <summary>
        /// A textured PBR material using a generated map set.
        /// textureBase is the ProcTexture2 base name, e.g. "T_OrionHull".
        /// </summary>
        public static Material Textured(string assetName, string textureBase,
                                        Vector2 tiling, Color tint,
                                        float normalScale = 1f,
                                        float smoothnessScale = 1f,
                                        float metallicScale = 1f)
        {
            var mat = GetOrCreate(assetName, Lit);

            var albedo = ProcTexture2.Load(textureBase + "_Albedo");
            var normal = ProcTexture2.Load(textureBase + "_Normal");
            var mask = ProcTexture2.Load(textureBase + "_Mask");

            SetColor(mat, "_BaseColor", tint);
            SetColor(mat, "_Color", tint);

            if (albedo != null)
            {
                SetTex(mat, "_BaseMap", albedo);
                SetTex(mat, "_MainTex", albedo);
                if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", tiling);
                if (mat.HasProperty("_MainTex")) mat.SetTextureScale("_MainTex", tiling);
            }

            if (normal != null)
            {
                SetTex(mat, "_BumpMap", normal);
                SetFloat(mat, "_BumpScale", normalScale);
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpMap")) mat.SetTextureScale("_BumpMap", tiling);
            }

            if (mask != null && IsURP)
            {
                // URP Lit metallic-gloss layout: R metallic, A smoothness.
                SetTex(mat, "_MetallicGlossMap", mask);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (mat.HasProperty("_MetallicGlossMap")) mat.SetTextureScale("_MetallicGlossMap", tiling);

                SetTex(mat, "_OcclusionMap", mask);
                SetFloat(mat, "_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
                if (mat.HasProperty("_OcclusionMap")) mat.SetTextureScale("_OcclusionMap", tiling);

                // Smoothness comes from the map's alpha, not the albedo's.
                SetFloat(mat, "_SmoothnessTextureChannel", 0f);
            }

            SetFloat(mat, "_Metallic", metallicScale);
            SetFloat(mat, "_Smoothness", smoothnessScale);
            SetFloat(mat, "_Glossiness", smoothnessScale);

            // Highlight emission is driven per-renderer at runtime via a property
            // block, so the keyword must be enabled on the shared material.
            mat.EnableKeyword("_EMISSION");
            SetColor(mat, "_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Plain PBR material with no texture maps.</summary>
        public static Material Solid(string assetName, Color color, float metallic,
                                     float smoothness)
        {
            var mat = GetOrCreate(assetName, Lit);
            SetColor(mat, "_BaseColor", color);
            SetColor(mat, "_Color", color);
            SetFloat(mat, "_Metallic", metallic);
            SetFloat(mat, "_Smoothness", smoothness);
            SetFloat(mat, "_Glossiness", smoothness);
            mat.EnableKeyword("_EMISSION");
            SetColor(mat, "_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Self-lit material for indicator lights, screens and engine glow.</summary>
        public static Material Emissive(string assetName, Color color, float intensity)
        {
            var mat = GetOrCreate(assetName, Lit);
            SetColor(mat, "_BaseColor", color * 0.3f);
            SetColor(mat, "_Color", color * 0.3f);
            SetFloat(mat, "_Metallic", 0f);
            SetFloat(mat, "_Smoothness", 0.6f);
            mat.EnableKeyword("_EMISSION");
            SetColor(mat, "_EmissionColor", color * intensity);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material UnlitColor(string assetName, Color color, bool transparent = false)
        {
            var mat = GetOrCreate(assetName, Unlit);
            SetColor(mat, "_BaseColor", color);
            SetColor(mat, "_Color", color);

            if (transparent) MakeTransparent(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void MakeTransparent(Material mat)
        {
            SetFloat(mat, "_Surface", 1f);
            SetFloat(mat, "_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // ---------------------------------------------------------------

        static Material GetOrCreate(string assetName, Shader shader)
        {
            Material cached;
            if (Cache.TryGetValue(assetName, out cached) && cached != null) return cached;

            AssetPaths.EnsureFolder(Folder);
            string path = Folder + "/" + assetName + ".mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = assetName };
                AssetPaths.CreateAsset(mat, path);
            }
            else
            {
                // Reuse the existing asset so references elsewhere survive a rebuild.
                mat.shader = shader;
            }

            Cache[assetName] = mat;
            return mat;
        }

        public static void ClearCache() { Cache.Clear(); _lit = null; _unlit = null; }

        /// <summary>A material asset on any shader, reused across rebuilds.</summary>
        public static Material WithShader(string assetName, Shader shader)
        {
            return GetOrCreate(assetName, shader);
        }

        static void SetColor(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }
        static void SetFloat(Material m, string p, float f) { if (m.HasProperty(p)) m.SetFloat(p, f); }
        static void SetTex(Material m, string p, Texture t) { if (m.HasProperty(p)) m.SetTexture(p, t); }
    }
}
