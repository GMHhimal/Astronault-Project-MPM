using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto-configures the ambient background music clip's import settings the
/// moment it's in Assets/_Project/Audio/Music/ - same idea as
/// RocketSpriteImporter.cs for sprites, so nothing needs manual Inspector
/// tweaking after "Rocket Timeline > Build Everything". Streaming load (a
/// ~1-minute ambient loop doesn't need to sit fully decompressed in memory
/// for the whole game), Vorbis compression (loops cleanly in Unity, unlike
/// MP3's encoder padding/delay, which can leave an audible gap or click
/// every time a looped MP3 wraps), and Preload Audio Data on (so there's no
/// startup stutter the first time it plays).
/// </summary>
public class AmbientMusicImporter : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        if (!normalizedPath.Contains("/_Project/Audio/Music/")) return;

        AudioImporter importer = (AudioImporter)assetImporter;

        // v20.1 fix: AudioImporter.preloadAudioData (set directly on the
        // importer) is obsolete in Unity 6 - preload is now a per-platform
        // field on AudioImporterSampleSettings itself, set alongside
        // loadType/compressionFormat/quality below instead.
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
    }
}
