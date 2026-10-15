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
/// The shared UI chrome sprites (ui_panel, ui_button, ui_button_ghost,
/// ui_ring) also get their 9-slice border set automatically, so Image
/// components using them with Image Type = Sliced scale to any size without
/// stretching the rounded corners.
/// </summary>
public class RocketSpriteImporter : AssetPostprocessor
{
    // v14: ui_button_ghost added alongside ui_button (gen_ui_assets.py's
    // v14 "Sleek Glass Dashboard" pass split the old single chamfered
    // ui_button into a bright filled "solid" texture reserved for Play, and
    // a dark-glass-with-border "ghost" texture used for every other button
    // in the app - see that file's own module docstring). Each border below
    // must stay >= that sprite's (radius + margin) baked in gen_ui_assets.py,
    // or 9-slice stretching will cut the rounded corner mid-curve - see that
    // file's comment just above its final make_card/make_solid_button/
    // make_ring calls, which documents the exact numbers these need to stay
    // ahead of. ui_button_ghost is the tight case: every "secondary" button
    // in the app was floored at 68px tall in this pass specifically to give
    // its smaller radius/border room to work.
    private const int ButtonBorder = 52;
    private const int ButtonGhostBorder = 30;
    private const int PanelBorder = 52;
    private const int RingBorder = 54;

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
        int border = fileName switch
        {
            "ui_button" => ButtonBorder,
            "ui_button_ghost" => ButtonGhostBorder,
            "ui_panel" => PanelBorder,
            "ui_ring" => RingBorder,
            _ => -1,
        };
        if (border > 0)
        {
            importer.spriteBorder = new Vector4(border, border, border, border);
        }
    }
}
