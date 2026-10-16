using UnityEditor;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Textured Earth and Moon spheres with a thin atmosphere or limb glow.
    ///
    /// Replaces the flat emissive blue ball that stood in for Earth. The maps are the
    /// equirectangular NASA-derived Blue Marble Earth and a lunar albedo mosaic,
    /// lit by the scene's sun, so Earth shows a real terminator and continents.
    /// </summary>
    public static class PlanetBuilder
    {
        public const string Folder = "Assets/OrionAR/Textures/Planets";
        public const string EarthMap = Folder + "/Earth_BlueMarble_2048.jpg";
        public const string MoonMap = Folder + "/Moon_1024.jpg";

        static Mesh _sphere, _shell;

        public static void ResetCache() { _sphere = null; _shell = null; }

        static Mesh SphereMesh()
        {
            if (_sphere != null) return _sphere;
            _sphere = ProcMesh2.Sphere("PlanetSphere", 1f, 96, 48);
            AssetPaths.CreateAsset(_sphere, OrionBuilder.MeshFolder + "/PlanetSphere.asset");
            return _sphere;
        }

        static Mesh ShellMesh()
        {
            if (_shell != null) return _shell;
            _shell = ProcMesh2.Sphere("PlanetShell", 1.035f, 64, 32);
            AssetPaths.CreateAsset(_shell, OrionBuilder.MeshFolder + "/PlanetShell.asset");
            return _shell;
        }

        static Texture2D LoadMap(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && (ti.wrapModeU != TextureWrapMode.Repeat || ti.mipmapEnabled == false || ti.anisoLevel < 4))
            {
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.sRGBTexture = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material SurfaceMaterial(string name, string mapPath, float smoothness)
        {
            var mat = MatLib2.Solid(name, Color.white, 0f, smoothness);
            var tex = LoadMap(mapPath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                // The sphere's longitude runs clockwise seen from above; flip U so the
                // continents are not mirrored.
                mat.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
                mat.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            }
            if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material RimMaterial(string name, Color color, float intensity, float power)
        {
            var shader = Shader.Find("OrionAR/Atmosphere");
            var mat = MatLib2.WithShader(name, shader != null ? shader : MatLib2.Unlit);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Intensity")) mat.SetFloat("_Intensity", intensity);
            if (mat.HasProperty("_Power")) mat.SetFloat("_Power", power);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static GameObject Earth(string name, Transform parent)
        {
            return Build(name, parent, SurfaceMaterial("M_EarthSurface", EarthMap, 0.35f),
                         RimMaterial("M_EarthAtmosphere", new Color(0.32f, 0.58f, 1f), 1.6f, 3.0f));
        }

        public static GameObject Moon(string name, Transform parent)
        {
            return Build(name, parent, SurfaceMaterial("M_MoonSurface", MoonMap, 0.05f),
                         RimMaterial("M_MoonRim", new Color(0.55f, 0.6f, 0.7f), 0.35f, 4.5f));
        }

        static GameObject Build(string name, Transform parent, Material surface, Material rim)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            go.AddComponent<MeshFilter>().sharedMesh = SphereMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = surface;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var shell = new GameObject("Atmosphere");
            shell.transform.SetParent(go.transform, false);
            shell.AddComponent<MeshFilter>().sharedMesh = ShellMesh();
            var smr = shell.AddComponent<MeshRenderer>();
            smr.sharedMaterial = rim;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = false;

            return go;
        }
    }
}
