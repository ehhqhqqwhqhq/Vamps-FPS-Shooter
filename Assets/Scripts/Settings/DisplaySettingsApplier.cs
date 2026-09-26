using System.Collections.Generic;
using UnityEngine;

namespace Vamp.Settings
{
    /// <summary>
    /// Detects what the current monitor supports and applies display mode / resolution / refresh / v-sync.
    /// Only valid combinations are ever offered or applied; anything else falls back to the native mode.
    /// (Resolution changes are ignored by Unity inside the Editor Game view - they apply in builds.)
    /// </summary>
    public static class DisplaySettingsApplier
    {
        public struct ResolutionOption
        {
            public int Width;
            public int Height;
            public string Label { get { return Width + " × " + Height; } }
        }

        public static List<ResolutionOption> GetResolutions()
        {
            var list = new List<ResolutionOption>();
            var seen = new HashSet<long>();
            var all = Screen.resolutions;
            for (int i = all.Length - 1; i >= 0; i--)
            {
                long key = ((long)all[i].width << 32) | (uint)all[i].height;
                if (seen.Add(key)) list.Add(new ResolutionOption { Width = all[i].width, Height = all[i].height });
            }
            if (list.Count == 0)
            {
                var cur = Screen.currentResolution;
                list.Add(new ResolutionOption { Width = cur.width, Height = cur.height });
            }
            list.Sort((a, b) => (b.Width * b.Height).CompareTo(a.Width * a.Height));
            return list;
        }

        public static List<int> GetRefreshRates(int width, int height)
        {
            var rates = new List<int>();
            foreach (var r in Screen.resolutions)
            {
                if (r.width != width || r.height != height) continue;
                int hz = Mathf.RoundToInt((float)r.refreshRateRatio.value);
                if (hz > 0 && !rates.Contains(hz)) rates.Add(hz);
            }
            if (rates.Count == 0) rates.Add(MonitorRefreshRate());
            rates.Sort((a, b) => b.CompareTo(a));
            return rates;
        }

        public static int MonitorRefreshRate()
        {
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            return hz > 0 ? hz : 60;
        }

        public static FullScreenMode ToUnity(DisplayModeOption mode)
        {
            switch (mode)
            {
                case DisplayModeOption.Borderless: return FullScreenMode.FullScreenWindow;
                case DisplayModeOption.Windowed: return FullScreenMode.Windowed;
                default: return FullScreenMode.ExclusiveFullScreen;
            }
        }

        /// <summary>Resolves "0 = native / highest" and invalid values into a real supported mode.</summary>
        public static void Resolve(DisplaySettingsData d, out int width, out int height, out int hz)
        {
            var options = GetResolutions();
            width = d.width;
            height = d.height;
            bool valid = false;
            foreach (var o in options)
                if (o.Width == width && o.Height == height) { valid = true; break; }
            if (!valid)
            {
                width = options[0].Width;
                height = options[0].Height;
            }

            var rates = GetRefreshRates(width, height);
            hz = rates.Contains(d.refreshRate) ? d.refreshRate : rates[0];
        }

        public static void Apply(DisplaySettingsData d)
        {
            int w, h, hz;
            Resolve(d, out w, out h, out hz);

            RefreshRate target = Screen.currentResolution.refreshRateRatio;
            foreach (var r in Screen.resolutions)
            {
                if (r.width == w && r.height == h && Mathf.RoundToInt((float)r.refreshRateRatio.value) == hz)
                {
                    target = r.refreshRateRatio;
                    break;
                }
            }

            try
            {
                Screen.SetResolution(w, h, ToUnity(d.displayMode), target);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[VAMP] Display change failed, keeping current mode: " + e.Message);
            }

            QualitySettings.vSyncCount = d.vSync ? 1 : 0;
        }
    }
}
