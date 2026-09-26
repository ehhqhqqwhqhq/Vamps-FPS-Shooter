using System;
using System.IO;
using UnityEngine;

namespace Vamp.Core
{
    /// <summary>
    /// Small, safe JSON persistence under persistentDataPath/VAMP. Writes go to a temp file first and are then
    /// swapped in, so a crash mid-save can't corrupt existing data. Corrupt files are moved aside and treated
    /// as missing (the game falls back to defaults instead of crashing).
    /// This is the OFFLINE store. When a backend exists, account/progression data moves server-side and this
    /// remains only for device-local things (graphics settings, cached session).
    /// </summary>
    public static class JsonStore
    {
        public static string Root
        {
            get
            {
                string root = Path.Combine(Application.persistentDataPath, "VAMP");
                if (!_migrated) { _migrated = true; MigrateLegacy(root); }
                return root;
            }
        }

        private static bool _migrated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _migrated = false; }

        /// <summary>
        /// The project was renamed (DefaultCompany/vamp → VAMP/VAMP), which moves persistentDataPath. Copy existing
        /// accounts, progress and settings across once so nobody loses their data.
        /// </summary>
        private static void MigrateLegacy(string root)
        {
            try
            {
                if (Directory.Exists(root)) return;
                var companyDir = Directory.GetParent(Application.persistentDataPath);
                if (companyDir == null || companyDir.Parent == null) return;
                string legacy = Path.Combine(companyDir.Parent.FullName, "DefaultCompany", "vamp", "VAMP");
                if (!Directory.Exists(legacy) || string.Equals(Path.GetFullPath(legacy), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) return;
                CopyDir(legacy, root);
                Debug.Log("[VAMP] Moved saved data from " + legacy);
            }
            catch (Exception e) { Debug.LogWarning("[VAMP] Could not migrate old save data: " + e.Message); }
        }

        private static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), false);
            foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
        }

        public static string FullPath(string relativePath)
        {
            return Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static bool Exists(string relativePath)
        {
            return File.Exists(FullPath(relativePath));
        }

        public static bool TryLoad<T>(string relativePath, out T value) where T : class
        {
            value = null;
            string path = FullPath(relativePath);
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                value = JsonUtility.FromJson<T>(json);
                return value != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Could not read " + relativePath + " (" + e.Message + "). Using defaults.");
                try { File.Move(path, path + ".corrupt-" + DateTime.UtcNow.Ticks); } catch { /* ignore */ }
                value = null;
                return false;
            }
        }

        public static bool Save<T>(string relativePath, T value)
        {
            string path = FullPath(relativePath);
            string tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(tmp, JsonUtility.ToJson(value, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[VAMP] Could not save " + relativePath + ": " + e.Message);
                return false;
            }
        }

        public static void Delete(string relativePath)
        {
            try
            {
                string path = FullPath(relativePath);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Could not delete " + relativePath + ": " + e.Message);
            }
        }

        public static void DeleteFolder(string relativePath)
        {
            try
            {
                string path = FullPath(relativePath);
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Could not delete folder " + relativePath + ": " + e.Message);
            }
        }
    }
}
