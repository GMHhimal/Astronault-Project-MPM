using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrionAR.Gen
{
    /// <summary>
    /// Saves UIKit's procedurally drawn sprites as PNG assets while the scenes are
    /// generated, so the Image components reference something that survives a save.
    ///
    /// Before this existed, every rounded panel, pill, button and the progress ring was
    /// drawn into a Texture2D that lived only in memory: the scene saved a reference to
    /// nothing, and on reopening, every panel was a hard-cornered rectangle and the
    /// progress ring a solid square.
    /// </summary>
    public static class UISpriteStore
    {
        public const string Folder = "Assets/OrionAR/UI";

        public static void Begin()
        {
            AssetPaths.EnsureFolder(Folder);
            UIKit.ClearSpriteCaches();
            UIKit.PersistSprite = Save;
        }

        public static void End()
        {
            UIKit.PersistSprite = null;
            UIKit.ClearSpriteCaches();
        }

        static Sprite Save(Sprite sprite, string key)
        {
            if (sprite == null || sprite.texture == null) return sprite;

            string assetPath = Folder + "/" + key + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (existing != null) return existing;

            byte[] png = sprite.texture.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.spritePixelsPerUnit = sprite.pixelsPerUnit;

                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteBorder = sprite.border;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
            }

            var saved = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            return saved != null ? saved : sprite;
        }
    }
}
