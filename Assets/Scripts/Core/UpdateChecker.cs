using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Vamp.Core
{
    /// <summary>
    /// Where releases are published (written by VAMP ▸ Release into Resources/VampRelease.json).
    /// The launcher reads the same GitHub repository.
    /// </summary>
    [Serializable]
    public sealed class ReleaseConfig
    {
        public string owner = "";
        public string repo = "";
        public string assetName = "VAMP-win64.zip";

        public bool Valid { get { return !string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo); } }

        public static ReleaseConfig Load()
        {
            var t = Resources.Load<TextAsset>("VampRelease");
            if (t == null) return new ReleaseConfig();
            try { return JsonUtility.FromJson<ReleaseConfig>(t.text) ?? new ReleaseConfig(); }
            catch (Exception) { return new ReleaseConfig(); }
        }
    }

    /// <summary>
    /// Once per session (main menu): asks GitHub for the latest release. If it's newer than this build, shows
    /// "UPDATE AVAILABLE" - players update by restarting VAMP from the launcher (which downloads it).
    /// Online lobbies also refuse players on a different version, so everyone stays in sync.
    /// </summary>
    public static class UpdateChecker
    {
        public static string LatestVersion { get; private set; }
        public static bool UpdateAvailable { get; private set; }
        private static bool _checked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _checked = false; LatestVersion = null; UpdateAvailable = false; }

        public static void CheckOnce()
        {
            if (_checked) return;
            _checked = true;
            var cfg = ReleaseConfig.Load();
            if (!cfg.Valid || GameRoot.Instance == null) return;
            GameRoot.Instance.StartCoroutine(Check(cfg));
        }

        private static IEnumerator Check(ReleaseConfig cfg)
        {
            string url = "https://api.github.com/repos/" + cfg.owner + "/" + cfg.repo + "/releases/latest";
            using (var req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("User-Agent", "VAMP-Game");
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                var m = Regex.Match(req.downloadHandler.text, @"""tag_name""\s*:\s*""v?([^""]+)""");
                if (!m.Success) yield break;
                LatestVersion = m.Groups[1].Value;
                UpdateAvailable = IsNewer(LatestVersion, Application.version);
                if (UpdateAvailable && Game.Notifications != null)
                    Game.Notifications.Push(NotificationKind.Info, "UPDATE AVAILABLE - v" + LatestVersion,
                        "YOU HAVE v" + Application.version + ". CLOSE VAMP AND OPEN THE VAMP LAUNCHER TO UPDATE.");
            }
        }

        /// <summary>Compares dotted versions (1.2.10 &gt; 1.2.9).</summary>
        public static bool IsNewer(string candidate, string current)
        {
            var a = (candidate ?? "0").Split('.');
            var b = (current ?? "0").Split('.');
            for (int i = 0; i < Mathf.Max(a.Length, b.Length); i++)
            {
                int x = 0, y = 0;
                if (i < a.Length) int.TryParse(Regex.Replace(a[i], "[^0-9]", ""), out x);
                if (i < b.Length) int.TryParse(Regex.Replace(b[i], "[^0-9]", ""), out y);
                if (x != y) return x > y;
            }
            return false;
        }
    }
}
