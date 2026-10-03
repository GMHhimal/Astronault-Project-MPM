using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Any PNG placed under Assets/_Project/Sprites/ is force-imported as a UI
/// Sprite instead of Unity's default "Texture" type. Without this, every new
/// image you drop into that folder would need its Texture Type manually
/// switched to "Sprite (2D and UI)" in the Inspector before it can be
/// assigned to an Image component - easy for a beginner to forget and a
/// common source of "why is my sprite field empty" confusion.
///
/// The three shared UI chrome sprites (ui_panel, ui_button, ui_ring) also get
/// their 9-slice border set automatically, so Image components using them
/// with Image Type = Sliced scale to any size without stretching the
/// rounded corners.
/// </summary>
public class RocketSpriteImporter : AssetPostprocessor
{
    // Must match the border baked into gen_ui_assets.py's rounded rect radius/stroke.
    private const int NineSliceBorder = 30;

    private void OnPreprocessTexture()
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        if (!normalizedPath.Contains("/_Project/Sprites/")) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;

        string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        if (fileName == "ui_panel" || fileName == "ui_button" || fileName == "ui_ring")
        {
            importer.spriteBorder = new Vector4(NineSliceBorder, NineSliceBorder, NineSliceBorder, NineSliceBorder);
        }
    }
}
