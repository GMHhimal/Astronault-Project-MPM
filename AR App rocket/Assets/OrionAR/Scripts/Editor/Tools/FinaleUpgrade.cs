using OrionAR.Gen;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OrionAR.Tools
{
    /// <summary>
    /// Wires the version 2 finale into the existing AR scene without a full rebuild,
    /// so nothing else in the scene is regenerated or lost.
    /// </summary>
    public static class FinaleUpgrade
    {
        const string ARScenePath = "Assets/OrionAR/Scenes/02_LunarExplorer.unity";

        [MenuItem("Orion AR/Upgrade Finale (keeps scene)", priority = 42)]
        public static void Upgrade()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Orion AR", "Stop Play mode first.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(ARScenePath, OpenSceneMode.Single);
            var finale = Object.FindFirstObjectByType<TEISequence>(FindObjectsInactive.Include);
            if (finale == null)
            {
                EditorUtility.DisplayDialog("Orion AR",
                    "No TEISequence found in 02_LunarExplorer. Run Orion AR > Build Everything.", "OK");
                return;
            }

            finale.moonGlobeMaterial = PlanetBuilder.SurfaceMaterial("M_MoonSurface", PlanetBuilder.MoonMap, 0.05f);
            finale.moonRimMaterial = PlanetBuilder.RimMaterial("M_MoonRim", new Color(0.55f, 0.6f, 0.7f), 0.35f, 4.5f);
            finale.coastSeconds = 14f;

            EditorUtility.SetDirty(finale);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Orion AR] Finale upgraded: lunar globe material " +
                      (finale.moonGlobeMaterial != null ? finale.moonGlobeMaterial.name : "MISSING") +
                      ". Open 01_MainMenu and press Play.");
        }
    }
}

namespace OrionAR.Tools
{
    /// <summary>
    /// Turns the right way out every sphere-based mesh that the old generator built
    /// inside out: the Earth and Moon globes, the atmosphere shell, the lunar boulders
    /// and the spherical tanks on the service module.
    ///
    /// The mesh assets are fixed in place, so every scene reference stays valid and
    /// nothing has to be rebuilt. Each mesh is tested before it is touched, so running
    /// this twice does no harm.
    /// </summary>
    public static class MeshRepair
    {
        static readonly string[] Prefixes =
        {
            "PlanetSphere", "PlanetShell", "BoulderSource", "LunarBoulder",
            "Orion_TankBulge", "Orion_Pressurant",
        };

        [MenuItem("Orion AR/Repair Inside-Out Spheres", priority = 43)]
        public static void Repair()
        {
            int fixedCount = 0, okCount = 0;
            var log = new System.Text.StringBuilder("[Orion AR] Sphere mesh repair\n");

            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/OrionAR/Meshes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null || !Matches(mesh.name, path)) continue;

                if (FacesOutward(mesh)) { okCount++; continue; }

                Flip(mesh);
                EditorUtility.SetDirty(mesh);
                fixedCount++;
                log.AppendLine("  flipped  " + path);
            }

            AssetDatabase.SaveAssets();
            log.AppendLine("  " + fixedCount + " fixed, " + okCount + " already correct.");
            Debug.Log(log.ToString());
        }

        static bool Matches(string meshName, string path)
        {
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (var p in Prefixes)
                if (meshName.StartsWith(p) || file.StartsWith(p)) return true;
            return false;
        }

        /// <summary>
        /// Sums each face normal against the direction from the mesh centre. Unity's
        /// face normal is cross(b - a, c - a), pointing toward a viewer who sees the
        /// face; positive means the faces look outward.
        /// </summary>
        static bool FacesOutward(Mesh mesh)
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            Vector3 centre = mesh.bounds.center;
            double sum = 0;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                sum += Vector3.Dot(n, (a + b + c) / 3f - centre);
            }
            return sum >= 0;
        }

        static void Flip(Mesh mesh)
        {
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                var tris = mesh.GetTriangles(sm);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int tmp = tris[i + 1];
                    tris[i + 1] = tris[i + 2];
                    tris[i + 2] = tmp;
                }
                mesh.SetTriangles(tris, sm);
            }

            // Smoothed normals were averaged from the inward faces, so they point in.
            var normals = mesh.normals;
            if (normals != null && normals.Length == mesh.vertexCount)
            {
                for (int i = 0; i < normals.Length; i++) normals[i] = -normals[i];
                mesh.normals = normals;
            }
            else mesh.RecalculateNormals();

            if (mesh.uv != null && mesh.uv.Length == mesh.vertexCount) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }
    }
}
