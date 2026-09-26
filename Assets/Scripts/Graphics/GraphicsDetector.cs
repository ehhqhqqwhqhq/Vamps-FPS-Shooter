using UnityEngine;
using Vamp.Settings;

namespace Vamp.Graphics
{
    /// <summary>First-launch hardware heuristic: recommends a preset and an FPS estimate. The player always has control.</summary>
    public static class GraphicsDetector
    {
        public struct Recommendation
        {
            public QualityPreset Preset;
            public string EstimatedTarget;
            public string Gpu;
            public int VramMb;
        }

        public static Recommendation Recommend()
        {
            int vram = SystemInfo.graphicsMemorySize;
            int cores = SystemInfo.processorCount;
            int ram = SystemInfo.systemMemorySize;

            QualityPreset p;
            if (vram >= 10000 && cores >= 8) p = QualityPreset.Ultra;
            else if (vram >= 6000 && cores >= 6) p = QualityPreset.High;
            else if (vram >= 3500 && cores >= 4) p = QualityPreset.Medium;
            else if (vram >= 1800) p = QualityPreset.Low;
            else p = QualityPreset.VeryLow;
            if (ram < 8000 && p > QualityPreset.Medium) p = QualityPreset.Medium;

            string estimate = p == QualityPreset.Ultra ? "144+ FPS" : p == QualityPreset.High ? "120+ FPS"
                            : p == QualityPreset.Medium ? "90+ FPS" : "60+ FPS";

            return new Recommendation { Preset = p, EstimatedTarget = estimate, Gpu = SystemInfo.graphicsDeviceName, VramMb = vram };
        }

        public static string PresetLabel(QualityPreset p)
        {
            return p == QualityPreset.VeryLow ? "VERY LOW" : p.ToString().ToUpperInvariant();
        }
    }
}
