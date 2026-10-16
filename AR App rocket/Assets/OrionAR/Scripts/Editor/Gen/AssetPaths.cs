using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Folder and asset creation that the AssetDatabase actually knows about.
    ///
    /// Directory.CreateDirectory makes a folder on disk, but Unity's asset database does
    /// not notice it until a refresh, and AssetDatabase.CreateAsset into an unregistered
    /// folder fails part-way through the build. Everything the generators write goes
    /// through here instead.
    /// </summary>
    public static class AssetPaths
    {
        public static bool EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;

            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetPath)) return true;

            var parts = assetPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                Debug.LogError("[AssetPaths] Path must start with 'Assets/': " + assetPath);
                return false;
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    // On disk but unknown to the database: import it rather than
                    // creating a second "Folder 1" next to it.
                    string diskPath = Path.Combine(Directory.GetCurrentDirectory(), next);
                    if (Directory.Exists(diskPath))
                    {
                        AssetDatabase.ImportAsset(next, ImportAssetOptions.ForceUpdate);
                        AssetDatabase.Refresh();
                    }

                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        string guid = AssetDatabase.CreateFolder(current, parts[i]);
                        if (string.IsNullOrEmpty(guid))
                        {
                            Debug.LogError("[AssetPaths] Could not create folder: " + next);
                            return false;
                        }
                    }
                }

                current = next;
            }

            return AssetDatabase.IsValidFolder(assetPath);
        }

        /// <summary>
        /// Creates an asset, registering its folder first and replacing anything already
        /// at that path. Calling it on an object that is already an asset is a no-op, so
        /// a mesh shared by several GameObjects is only written once.
        /// </summary>
        public static bool CreateAsset(Object asset, string assetPath)
        {
            if (asset == null) return false;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset))) return true;

            string folder = Path.GetDirectoryName(assetPath);
            if (folder != null) folder = folder.Replace('\\', '/');
            if (!EnsureFolder(folder)) return false;

            var existing = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (existing != null) AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(asset, assetPath);
            return true;
        }

        /// <summary>Deletes every asset in a folder, keeping the folder itself.</summary>
        public static void ClearFolder(string assetPath)
        {
            if (!AssetDatabase.IsValidFolder(assetPath)) return;
            var guids = AssetDatabase.FindAssets("", new[] { assetPath });
            var paths = new System.Collections.Generic.List<string>();
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!AssetDatabase.IsValidFolder(p)) paths.Add(p);
            }
            if (paths.Count > 0)
            {
                var failed = new System.Collections.Generic.List<string>();
                AssetDatabase.DeleteAssets(paths.ToArray(), failed);
            }
        }

        public static void EnsureAllProjectFolders()
        {
            string[] folders =
            {
                "Assets/OrionAR",
                "Assets/OrionAR/Data",
                "Assets/OrionAR/Data/Parts",
                "Assets/OrionAR/Scenes",
                "Assets/OrionAR/Materials",
                "Assets/OrionAR/Meshes",
                "Assets/OrionAR/Textures",
                "Assets/OrionAR/Audio",
                "Assets/OrionAR/Prefabs",
                "Assets/OrionAR/Fonts",
            };

            foreach (var f in folders) EnsureFolder(f);
            AssetDatabase.Refresh();
        }
    }
}
