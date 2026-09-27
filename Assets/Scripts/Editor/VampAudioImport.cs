using UnityEditor;
using UnityEngine;

namespace Vamp.EditorTools
{
    /// <summary>Soundtrack files (Resources/Music) stream from disk instead of sitting decompressed in memory.</summary>
    public sealed class VampAudioImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Music/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            s.preloadAudioData = false;
            ai.defaultSampleSettings = s;
            ai.loadInBackground = true;
        }
    }
}
