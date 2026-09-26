using System.Collections.Generic;
using UnityEngine;
using Vamp.Combat;
using Vamp.Core;
using Vamp.Match;
using Vamp.Settings;

namespace Vamp.Graphics
{
    public struct BenchmarkResult
    {
        public bool Valid;
        public float Average;
        public float OnePercentLow;
        public float Max;
        public QualityPreset Recommended;
    }

    /// <summary>
    /// PERFORMANCE BENCHMARK: loads VERTEX with 8 bots fighting, a camera flythrough across vertical routes,
    /// explosions, lights, shadows and particles for 20 s, then reports AVERAGE / 1% LOW / MAX FPS and a
    /// recommended preset. Launched from SETTINGS ▸ GRAPHICS.
    /// </summary>
    public static class Benchmark
    {
        public static BenchmarkResult Last;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Last = default(BenchmarkResult); }

        public static void Launch()
        {
            var cfg = MatchConfig.Defaults(GameMode.FreeForAll);
            cfg.benchmark = true;
            cfg.mapId = "vertex";
            cfg.bots = 8;
            cfg.timeLimitMinutes = 0f;
            if (Game.Scenes != null) Game.Scenes.StartMatch(cfg);
        }

        public static QualityPreset Recommend(float avg, float low, QualityPreset current)
        {
            int target = Mathf.Max(60, DisplaySettingsApplier.MonitorRefreshRate());
            int idx = (int)(current == QualityPreset.Custom ? QualityPreset.High : current);
            if (low < 60f || avg < target * 0.7f) idx = Mathf.Max(0, idx - 1);
            else if (low > target * 1.1f && avg > target * 1.4f) idx = Mathf.Min((int)QualityPreset.Ultra, idx + 1);
            return (QualityPreset)idx;
        }
    }

}
