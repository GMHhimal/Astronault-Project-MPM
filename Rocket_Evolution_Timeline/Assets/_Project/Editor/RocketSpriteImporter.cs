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

    // v22 (client: a moving starfield background, "stars moving downwards").
    // star_scroll_overlay.png (512x512, seamlessly tileable) is shown via
    // Image.Type.Tiled, which renders it at (texture pixel size / Pixels Per
    // Unit) UI units per tile. ScrollStarTileSize is the on-screen size (in UI
    // units, same space as every other Anchor() call in
    // RocketTimelineSceneBuilder.cs) one tile should render at -
    // BuildScrollingStarfield relies on this being an exact divisor of its
    // panel height (1080) so every panel ends on a whole tile, never a
    // partial one - see that method's own comment for why a partial tile
    // would make the seam between the two scrolling panels visible.
    private const float ScrollStarTextureSize = 512f;
    private const float ScrollStarTileSize = 360f;

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

        if (fileName == "star_scroll_overlay")
        {
            // Pixels Per Unit set so one tile renders at exactly
            // ScrollStarTileSize UI units (tileSize = texturePx / PPU).
            importer.spritePixelsPerUnit = ScrollStarTextureSize / ScrollStarTileSize;

            // v22.1 fix: spriteMeshType and spriteGenerateFallbackPhysicsShape
            // aren't direct TextureImporter properties (that was a compile
            // error, CS1061) - they live on the TextureImporterSettings
            // struct instead, read out, changed, then written back.
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            // Image.Type.Tiled needs the full rectangular quad, not a mesh
            // trimmed to the opaque pixels (which is Unity's default for a
            // transparent-background sprite like this one) - Tight mesh would
            // clip each tile to its own star silhouettes instead of repeating
            // the whole square, breaking the tiling entirely.
            settings.spriteMeshType = SpriteMeshType.FullRect;
            // This is a background decoration no player ever taps - skip the
            // (by-default-enabled) physics-shape generation Sprite import
            // does for every sprite, since nothing here needs a collider.
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
