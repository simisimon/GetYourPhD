using UnityEditor;
using UnityEngine;

namespace GetYourPhD.EditorTools
{
    /// <summary>Import settings for Resources/Audio: music streams (low memory), sound effects stay decompressed in memory (no latency).</summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var imp = (AudioImporter)assetImporter;
            var s = imp.defaultSampleSettings;
            if (assetPath.Contains("/Music/"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                imp.loadInBackground = true;
            }
            else
            {
                bool longLoop = assetPath.Contains("baby_cry") || assetPath.Contains("washer") || assetPath.Contains("cooking") || assetPath.Contains("applause");
                s.loadType = longLoop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
            }
            imp.defaultSampleSettings = s;
        }
    }
}
