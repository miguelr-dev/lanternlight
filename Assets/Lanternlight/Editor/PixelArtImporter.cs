using UnityEditor;
using UnityEngine;

namespace Lanternlight.EditorTools
{
    /// <summary>
    /// Import settings for the game's assets, applied automatically when they are imported:
    /// art stays crisp (point filtering, no compression or mipmaps) and audio stays
    /// uncompressed so the looping layers loop without a gap.
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        const string ArtFolder = "Assets/Lanternlight/Resources/Art";
        const string AudioFolder = "Assets/Lanternlight/Resources/Audio";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder))
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default; // sprites are built from these at runtime
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioFolder))
            {
                return;
            }
            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
        }
    }
}
