using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Vamp.Core;
using Debug = UnityEngine.Debug;

namespace Vamp.EditorTools
{
    /// <summary>
    /// VAMP ▸ Release - ship the game as a Windows .exe and push updates.
    ///   1. BUILD GAME       → Builds/VAMP/VAMP.exe (+ zipped as Builds/VAMP-win64.zip)
    ///   2. BUILD LAUNCHER   → Builds/Launcher/VAMP Launcher.exe (+ launcher.json), zipped as Builds/VAMP-Launcher.zip.
    ///                          Compiled with the C# compiler that ships with Windows - no extra installs.
    ///   3. PUBLISH          → creates GitHub release "v{version}" with both zips. Launchers pick it up automatically.
    /// Your GitHub token is stored only in this PC's EditorPrefs (never in the project or the build).
    /// </summary>
    public sealed class VampReleaseWindow : EditorWindow
    {
        private const string PrefOwner = "VAMP.Release.Owner", PrefRepo = "VAMP.Release.Repo", PrefToken = "VAMP.Release.Token";
        private const string GameZip = "VAMP-win64.zip", LauncherZip = "VAMP-Launcher.zip";

        private string _owner, _repo, _token, _version, _notes = "";
        private string _log = "";
        private bool _busy;
        private Vector2 _scroll;

        private static string ProjectRoot { get { return Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName); } }
        private static string BuildsDir { get { return Path.Combine(ProjectRoot, "Builds"); } }
        private static string GameDir { get { return Path.Combine(BuildsDir, "VAMP"); } }
        private static string LauncherDir { get { return Path.Combine(BuildsDir, "Launcher"); } }

        [MenuItem("VAMP/Release (Build + Publish Update)", priority = 40)]
        public static void Open()
        {
            var w = GetWindow<VampReleaseWindow>(true, "VAMP Release", true);
            w.minSize = new Vector2(560f, 560f);
        }

        private void OnEnable()
        {
            _owner = EditorPrefs.GetString(PrefOwner, "");
            _repo = EditorPrefs.GetString(PrefRepo, "vamp");
            _token = EditorPrefs.GetString(PrefToken, "");
            _version = PlayerSettings.bundleVersion;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VAMP RELEASE", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Players install VAMP with the launcher once. Every release you publish here is downloaded by their launcher automatically. Online lobbies only accept players on the same version as the host.", MessageType.Info);

            using (new EditorGUI.DisabledScope(_busy))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("GitHub repository (public, holds the releases)", EditorStyles.miniBoldLabel);
                _owner = EditorGUILayout.TextField("Owner (username)", _owner);
                _repo = EditorGUILayout.TextField("Repository", _repo);
                _token = EditorGUILayout.PasswordField(new GUIContent("Access token", "Fine-grained token with Contents: Read and write on this repo. Stored only on this PC."), _token);
                if (GUI.changed) SavePrefs();

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                _version = EditorGUILayout.TextField("Version", _version);
                if (GUILayout.Button("+0.0.1", GUILayout.Width(70f))) _version = Bump(_version);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Patch notes");
                _notes = EditorGUILayout.TextArea(_notes, GUILayout.MinHeight(60f));

                EditorGUILayout.Space();
                if (GUILayout.Button("1. BUILD GAME (.exe)", GUILayout.Height(30f))) BuildGame();
                if (GUILayout.Button("2. BUILD LAUNCHER", GUILayout.Height(30f))) BuildLauncher();
                if (GUILayout.Button("3. PUBLISH v" + _version + " TO GITHUB", GUILayout.Height(30f))) Publish();
                EditorGUILayout.Space();
                if (GUILayout.Button("UPLOAD SOURCE CODE TO GITHUB", GUILayout.Height(30f))) UploadSource();
                if (GUILayout.Button("BUILD + PUBLISH (1 → 2 → 3)", GUILayout.Height(40f)))
                {
                    if (BuildGame() && BuildLauncher()) Publish();
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open Builds folder")) { Directory.CreateDirectory(BuildsDir); EditorUtility.RevealInFinder(BuildsDir); }
                if (GUILayout.Button("Run built game")) RunBuilt();
                if (GUILayout.Button("Run launcher")) RunLauncher();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log", EditorStyles.miniBoldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_log, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefOwner, (_owner ?? "").Trim());
            EditorPrefs.SetString(PrefRepo, (_repo ?? "").Trim());
            EditorPrefs.SetString(PrefToken, (_token ?? "").Trim());
        }

        private void Log(string line)
        {
            _log = DateTime.Now.ToString("HH:mm:ss") + "  " + line + "\n" + _log;
            Debug.Log("[VAMP Release] " + line);
            Repaint();
        }

        private static string Bump(string v)
        {
            var parts = new List<string>((v ?? "0.1.0").Split('.'));
            while (parts.Count < 3) parts.Add("0");
            int p;
            int.TryParse(parts[2], out p);
            parts[2] = (p + 1).ToString();
            return string.Join(".", parts.ToArray());
        }

        // ------------------------------------------------------------------ 1. Game

        private void WriteReleaseConfig()
        {
            var cfg = new ReleaseConfig { owner = (_owner ?? "").Trim(), repo = (_repo ?? "").Trim(), assetName = GameZip };
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources"));
            File.WriteAllText(Path.Combine(Application.dataPath, "Resources", "VampRelease.json"), JsonUtility.ToJson(cfg, true));
            AssetDatabase.ImportAsset("Assets/Resources/VampRelease.json");
        }

        public bool BuildGame()
        {
            SavePrefs();
            if (!Regex.IsMatch(_version ?? "", "^\\d+\\.\\d+\\.\\d+$")) { Log("Version must look like 1.2.3"); return false; }
            PlayerSettings.bundleVersion = _version;
            PlayerSettings.productName = "VAMP";
            PlayerSettings.companyName = "VAMP";
            PlayerSettings.runInBackground = true;   // hosts keep running when alt-tabbed
            PlayerSettings.visibleInBackground = true;
            WriteReleaseConfig();

            var scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            if (scenes.Count == 0) { Log("No scenes in Build Settings - run VAMP ▸ Build All Scenes first."); return false; }

            CloseRunningBuild();
            try
            {
                if (Directory.Exists(GameDir)) Directory.Delete(GameDir, true);
            }
            catch (Exception e)
            {
                Log("Couldn't clear the old build (" + e.Message + "). Close VAMP.exe and try again.");
                return false;
            }
            Directory.CreateDirectory(GameDir);
            Log("Building VAMP v" + _version + " (" + scenes.Count + " scenes)...");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Path.Combine(GameDir, "VAMP.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Log("BUILD FAILED: " + report.summary.result + " (" + report.summary.totalErrors + " errors - see Console)");
                return false;
            }
            // Burst debug info must not ship.
            foreach (var d in Directory.GetDirectories(GameDir, "*DoNotShip*")) Directory.Delete(d, true);
            foreach (var d in Directory.GetDirectories(GameDir, "*ButDontShip*")) Directory.Delete(d, true);
            string zip = Path.Combine(BuildsDir, GameZip);
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(GameDir, zip, System.IO.Compression.CompressionLevel.Optimal, false);
            Log("Game built: " + GameDir + "  (" + (new FileInfo(zip).Length / 1048576) + " MB zip)");
            return true;
        }

        /// <summary>A running copy of the built game locks its files - close it before rebuilding.</summary>
        private void CloseRunningBuild() { CloseProcesses("VAMP", GameDir); }

        private void CloseProcesses(string processName, string folder)
        {
            string dir = Path.GetFullPath(folder).TrimEnd('\\', '/');
            foreach (var p in Process.GetProcessesByName(processName))
            {
                try
                {
                    string exe = p.MainModule != null ? Path.GetFullPath(p.MainModule.FileName) : "";
                    if (!exe.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) continue;
                    Log("Closing the running " + processName + ".exe so it can be rebuilt...");
                    if (!p.CloseMainWindow() || !p.WaitForExit(4000)) p.Kill();
                    p.WaitForExit(4000);
                }
                catch (Exception) { }
            }
        }

        private static void RunLauncher()
        {
            string exe = Path.Combine(LauncherDir, "VAMP Launcher.exe");
            if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = LauncherDir, UseShellExecute = true });
            else EditorUtility.DisplayDialog("VAMP", "Build the launcher first.", "OK");
        }

        private static void RunBuilt()
        {
            string exe = Path.Combine(GameDir, "VAMP.exe");
            if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = GameDir, UseShellExecute = true });
            else EditorUtility.DisplayDialog("VAMP", "Build the game first.", "OK");
        }

        // ------------------------------------------------------------------ 2. Launcher

        public bool BuildLauncher()
        {
            SavePrefs();
            string src = Path.GetFullPath(Path.Combine(ProjectRoot, "Launcher", "VampLauncher.cs"));
            if (!File.Exists(src)) { Log("Missing " + src); return false; }
            string fw = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319"));
            string csc = Path.Combine(fw, "csc.exe");
            if (!File.Exists(csc)) { Log("Windows C# compiler not found at " + csc + " (.NET Framework 4.8 is part of Windows 10/11)."); return false; }

            Directory.CreateDirectory(LauncherDir);
            CloseProcesses("VAMP Launcher", LauncherDir);
            string exe = Path.GetFullPath(Path.Combine(LauncherDir, "VAMP Launcher.exe"));
            string args = "/nologo /target:winexe /optimize+ /out:\"" + exe + "\""
                          + " /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll"
                          + " /r:\"" + Path.Combine(fw, "System.IO.Compression.dll") + "\""
                          + " /r:\"" + Path.Combine(fw, "System.IO.Compression.FileSystem.dll") + "\""
                          + " \"" + src + "\"";
            var psi = new ProcessStartInfo(csc, args)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) { Log("LAUNCHER COMPILE FAILED:\n" + output); return false; }
            }

            string json = "{\n  \"owner\": \"" + (_owner ?? "").Trim() + "\",\n  \"repo\": \"" + (_repo ?? "").Trim() + "\",\n  \"assetName\": \"" + GameZip + "\",\n  \"gameExe\": \"VAMP.exe\"\n}\n";
            File.WriteAllText(Path.Combine(LauncherDir, "launcher.json"), json);
            File.WriteAllText(Path.Combine(LauncherDir, "README.txt"),
                "VAMP\r\n\r\nRun \"VAMP Launcher.exe\". It downloads the latest VAMP into the Game folder and keeps it updated.\r\n" +
                "Keep launcher.json next to the launcher.\r\n");
            string zip = Path.Combine(BuildsDir, LauncherZip);
            if (File.Exists(zip)) File.Delete(zip);
            // Zip only the launcher files (not a downloaded Game folder).
            using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                foreach (var f in new[] { "VAMP Launcher.exe", "launcher.json", "README.txt" })
                    z.CreateEntryFromFile(Path.Combine(LauncherDir, f), "VAMP/" + f);
            }
            Log("Launcher built: " + exe + "  → share " + LauncherZip + " with players (once).");
            return true;
        }

        // ------------------------------------------------------------------ 3. Publish

        private async void Publish()
        {
            SavePrefs();
            string owner = (_owner ?? "").Trim(), repo = (_repo ?? "").Trim(), token = (_token ?? "").Trim();
            if (owner.Length == 0 || repo.Length == 0 || token.Length == 0) { Log("Fill in owner, repository and access token."); return; }
            string gameZip = Path.Combine(BuildsDir, GameZip);
            if (!File.Exists(gameZip)) { Log("Build the game first."); return; }
            if (PlayerSettings.bundleVersion != _version) { Log("Version changed since the last build - build the game again first."); return; }

            _busy = true;
            try
            {
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromMinutes(30);
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("VAMP-Release-Tool");
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                    string tag = "v" + _version;
                    Log("Creating release " + tag + " on " + owner + "/" + repo + "...");
                    string body = "{\"tag_name\":" + Json(tag) + ",\"name\":" + Json("VAMP " + tag) + ",\"body\":" + Json(_notes ?? "") + ",\"draft\":false,\"prerelease\":false}";
                    string api = "https://api.github.com/repos/" + owner + "/" + repo;
                    var res = await http.PostAsync(api + "/releases", new StringContent(body, Encoding.UTF8, "application/json"));
                    string text = await res.Content.ReadAsStringAsync();
                    if ((int)res.StatusCode == 422 && text.IndexOf("empty", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // A brand-new repo has no commits, and releases need one to tag. Add a README first.
                        Log("Repository is empty - adding a README so releases can be tagged...");
                        string readme = "# VAMP\n\nFast movement arena FPS.\n\n## Play\nDownload **VAMP-Launcher.zip** from the latest release, unzip it and run **VAMP Launcher.exe**. "
                                        + "It installs VAMP and keeps it updated.\n";
                        string put = "{\"message\":\"Initial commit\",\"content\":" + Json(Convert.ToBase64String(Encoding.UTF8.GetBytes(readme))) + "}";
                        var init = await http.PutAsync(api + "/contents/README.md", new StringContent(put, Encoding.UTF8, "application/json"));
                        if (!init.IsSuccessStatusCode) { Log("Couldn't create the README (" + (int)init.StatusCode + "): " + Short(await init.Content.ReadAsStringAsync())); return; }
                        res = await http.PostAsync(api + "/releases", new StringContent(body, Encoding.UTF8, "application/json"));
                        text = await res.Content.ReadAsStringAsync();
                    }
                    if (!res.IsSuccessStatusCode)
                    {
                        Log("GitHub refused the release (" + (int)res.StatusCode + "): " + Short(text) +
                            ((int)res.StatusCode == 422 ? "  - a release with this version probably exists; bump the version." : ""));
                        return;
                    }
                    var id = Regex.Match(text, "\"id\"\\s*:\\s*(\\d+)");
                    if (!id.Success) { Log("Unexpected GitHub response."); return; }

                    await Upload(http, owner, repo, id.Groups[1].Value, gameZip, GameZip);
                    string launcherZip = Path.Combine(BuildsDir, LauncherZip);
                    if (File.Exists(launcherZip)) await Upload(http, owner, repo, id.Groups[1].Value, launcherZip, LauncherZip);
                    Log("PUBLISHED " + tag + ". Launchers will install it next time they open.  https://github.com/" + owner + "/" + repo + "/releases/tag/" + tag);
                }
            }
            catch (Exception e) { Log("Publish failed: " + e.Message); }
            finally
            {
                _busy = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        // ------------------------------------------------------------------ Source code → GitHub

        private static readonly string[] SourceRoots = { "Assets", "Packages", "ProjectSettings", "Launcher" };
        private static readonly string[] SkipNames = { "TestAccounts.txt", ".DS_Store", "Thumbs.db" };

        private const string GitIgnore =
            "# Unity\n/[Ll]ibrary/\n/[Tt]emp/\n/[Oo]bj/\n/[Bb]uild/\n/[Bb]uilds/\n/[Ll]ogs/\n/[Uu]ser[Ss]ettings/\n/[Mm]emoryCaptures/\n/[Rr]ecordings/\n" +
            "*.csproj\n*.sln\n*.suo\n*.user\n*.userprefs\n*.pidb\n*.booproj\n*.svd\n*.pdb\n*.mdb\n*.opendb\n*.VC.db\n.vs/\n.idea/\n.vscode/\n" +
            "crashlytics-build.properties\n/[Aa]ssets/[Ss]treamingAssets/aa/*\nTestAccounts.txt\n";

        private const string SourceReadme =
            "# VAMP\n\nFast-paced movement arena FPS made in Unity 6 (URP).\n\n" +
            "## Play\nDownload **VAMP-Launcher.zip** from the [latest release](../../releases/latest), unzip it and run **VAMP Launcher.exe**. " +
            "It installs VAMP and keeps it updated.\n\n" +
            "## Develop\n1. Open this folder in Unity 6000.6.3f1 (Unity Hub ▸ Add ▸ this folder).\n" +
            "2. Run **VAMP ▸ Build All Scenes**, open `Assets/Scenes/Boot/Boot.unity` and press Play.\n" +
            "3. Ship updates with **VAMP ▸ Release** (builds the .exe and publishes a GitHub release that launchers download).\n\n" +
            "Online custom lobbies use Unity Relay / Multiplayer Services (link the project to your Unity Cloud project).\n";

        /// <summary>
        /// Pushes the Unity project (Assets, Packages, ProjectSettings, Launcher + README/.gitignore) to the repo's default
        /// branch as one commit, through the GitHub API - no git install needed. Only changed files are uploaded.
        /// Library / Temp / Builds / Logs / UserSettings are never uploaded.
        /// </summary>
        private async void UploadSource()
        {
            SavePrefs();
            string owner = (_owner ?? "").Trim(), repo = (_repo ?? "").Trim(), token = (_token ?? "").Trim();
            if (owner.Length == 0 || repo.Length == 0 || token.Length == 0) { Log("Fill in owner, repository and access token."); return; }
            AssetDatabase.SaveAssets();

            _busy = true;
            try
            {
                File.WriteAllText(Path.Combine(ProjectRoot, ".gitignore"), GitIgnore);
                File.WriteAllText(Path.Combine(ProjectRoot, "README.md"), SourceReadme);

                // Collect files
                var files = new List<string> { ".gitignore", "README.md" };
                foreach (var root in SourceRoots)
                {
                    string dir = Path.Combine(ProjectRoot, root);
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        string rel = f.Substring(ProjectRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
                        if (Array.IndexOf(SkipNames, Path.GetFileName(f)) >= 0) continue;
                        if (new FileInfo(f).Length > 95L * 1024 * 1024) { Log("Skipping " + rel + " (over GitHub's 100 MB file limit)"); continue; }
                        files.Add(rel);
                    }
                }
                Log("Uploading source: " + files.Count + " files...");

                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromMinutes(10);
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("VAMP-Release-Tool");
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                    string api = "https://api.github.com/repos/" + owner + "/" + repo;

                    // Default branch + current commit
                    string repoJson = await GetOk(http, api);
                    var br = Regex.Match(repoJson, "\"default_branch\"\\s*:\\s*\"([^\"]+)\"");
                    string branch = br.Success ? br.Groups[1].Value : "main";
                    string refJson = await GetOk(http, api + "/git/ref/heads/" + branch);
                    string parent = Regex.Match(refJson, "\"sha\"\\s*:\\s*\"([0-9a-f]{40})\"").Groups[1].Value;
                    string commitJson = await GetOk(http, api + "/git/commits/" + parent);
                    string baseTree = Regex.Match(commitJson, "\"tree\"\\s*:\\s*\\{\\s*\"sha\"\\s*:\\s*\"([0-9a-f]{40})\"").Groups[1].Value;

                    // What's already there (path → blob sha) so unchanged files are skipped
                    var existing = new Dictionary<string, string>();
                    if (baseTree.Length > 0)
                    {
                        string treeJson = await GetOk(http, api + "/git/trees/" + baseTree + "?recursive=1");
                        foreach (System.Text.RegularExpressions.Match m in Regex.Matches(treeJson, "\"path\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"[^}]*?\"type\"\\s*:\\s*\"blob\"[^}]*?\"sha\"\\s*:\\s*\"([0-9a-f]{40})\""))
                            existing[Regex.Unescape(m.Groups[1].Value)] = m.Groups[2].Value;
                    }

                    // Blobs already sent in an earlier (possibly interrupted) run - GitHub keeps them.
                    string cachePath = Path.Combine(ProjectRoot, "Library", "VampGitBlobs_" + owner + "_" + repo + ".txt");
                    var sent = new HashSet<string>();
                    if (File.Exists(cachePath)) foreach (var line in File.ReadAllLines(cachePath)) if (line.Length == 40) sent.Add(line);

                    // Upload changed blobs
                    var entries = new StringBuilder();
                    int uploaded = 0, done = 0;
                    foreach (var rel in files)
                    {
                        byte[] data = File.ReadAllBytes(Path.Combine(ProjectRoot, rel));
                        string sha = GitBlobSha(data);
                        string known;
                        if ((!existing.TryGetValue(rel, out known) || known != sha) && !sent.Contains(sha))
                        {
                            string blobBody = "{\"encoding\":\"base64\",\"content\":\"" + Convert.ToBase64String(data) + "\"}";
                            await PostWithRetry(http, api + "/git/blobs", blobBody, rel);
                            sent.Add(sha);
                            File.AppendAllText(cachePath, sha + "\n");
                            uploaded++;
                            await Task.Delay(900); // GitHub allows ~80 content-creating requests a minute
                        }
                        if (entries.Length > 0) entries.Append(',');
                        entries.Append("{\"path\":").Append(Json(rel)).Append(",\"mode\":\"100644\",\"type\":\"blob\",\"sha\":\"").Append(sha).Append("\"}");
                        done++;
                        if (done % 25 == 0) EditorUtility.DisplayProgressBar("VAMP", "Uploading source " + done + "/" + files.Count + " (" + uploaded + " changed)", done / (float)files.Count);
                    }

                    // Tree (full snapshot of the project) → commit → move the branch
                    var tree = await http.PostAsync(api + "/git/trees", new StringContent("{\"tree\":[" + entries + "]}", Encoding.UTF8, "application/json"));
                    string treeText = await tree.Content.ReadAsStringAsync();
                    if (!tree.IsSuccessStatusCode) throw new Exception("tree failed (" + (int)tree.StatusCode + "): " + Short(treeText));
                    string newTree = Regex.Match(treeText, "\"sha\"\\s*:\\s*\"([0-9a-f]{40})\"").Groups[1].Value;
                    if (newTree == baseTree) { Log("Source code is already up to date on GitHub."); return; }

                    string msg = "VAMP v" + PlayerSettings.bundleVersion + " source" + (string.IsNullOrEmpty(_notes) ? "" : "\n\n" + _notes);
                    string commitBody = "{\"message\":" + Json(msg) + ",\"tree\":\"" + newTree + "\",\"parents\":[\"" + parent + "\"]}";
                    var commit = await http.PostAsync(api + "/git/commits", new StringContent(commitBody, Encoding.UTF8, "application/json"));
                    string commitText = await commit.Content.ReadAsStringAsync();
                    if (!commit.IsSuccessStatusCode) throw new Exception("commit failed (" + (int)commit.StatusCode + "): " + Short(commitText));
                    string newCommit = Regex.Match(commitText, "\"sha\"\\s*:\\s*\"([0-9a-f]{40})\"").Groups[1].Value;

                    var patch = new HttpRequestMessage(new HttpMethod("PATCH"), api + "/git/refs/heads/" + branch)
                    { Content = new StringContent("{\"sha\":\"" + newCommit + "\"}", Encoding.UTF8, "application/json") };
                    var moved = await http.SendAsync(patch);
                    if (!moved.IsSuccessStatusCode) throw new Exception("updating " + branch + " failed (" + (int)moved.StatusCode + "): " + Short(await moved.Content.ReadAsStringAsync()));
                    Log("SOURCE UPLOADED: " + files.Count + " files (" + uploaded + " changed) → https://github.com/" + owner + "/" + repo + "/tree/" + branch);
                }
            }
            catch (Exception e) { Log("Source upload failed: " + e.Message); }
            finally
            {
                _busy = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        /// <summary>POST that waits out GitHub's secondary rate limit (403/429) instead of failing.</summary>
        private async Task PostWithRetry(HttpClient http, string url, string body, string label)
        {
            for (int attempt = 0; ; attempt++)
            {
                var res = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
                string txt = await res.Content.ReadAsStringAsync();
                if (res.IsSuccessStatusCode) return;
                int code = (int)res.StatusCode;
                bool limited = (code == 403 || code == 429) && txt.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!limited || attempt >= 15) throw new Exception(label + " failed (" + code + "): " + Short(txt));
                int wait = 60;
                IEnumerable<string> ra;
                if (res.Headers.TryGetValues("Retry-After", out ra)) foreach (var v in ra) { int x; if (int.TryParse(v, out x)) wait = Mathf.Clamp(x, 5, 300); }
                Log("GitHub rate limit - waiting " + wait + " s, then continuing (attempt " + (attempt + 1) + ")...");
                EditorUtility.DisplayProgressBar("VAMP", "GitHub rate limit - waiting " + wait + " s...", 0.5f);
                await Task.Delay(wait * 1000);
            }
        }

        private static async Task<string> GetOk(HttpClient http, string url)
        {
            var res = await http.GetAsync(url);
            string text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new Exception("GET " + url + " failed (" + (int)res.StatusCode + "): " + Short(text));
            return text;
        }

        /// <summary>Git's blob id: SHA-1 of "blob {size}\0" + content.</summary>
        private static string GitBlobSha(byte[] data)
        {
            byte[] header = Encoding.ASCII.GetBytes("blob " + data.Length + "\0");
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                sha1.TransformBlock(header, 0, header.Length, null, 0);
                sha1.TransformFinalBlock(data, 0, data.Length);
                var sb = new StringBuilder(40);
                foreach (byte b in sha1.Hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private async Task Upload(HttpClient http, string owner, string repo, string releaseId, string path, string name)
        {
            Log("Uploading " + name + " (" + (new FileInfo(path).Length / 1048576) + " MB)...");
            EditorUtility.DisplayProgressBar("VAMP Release", "Uploading " + name + "...", 0.5f);
            using (var stream = File.OpenRead(path))
            {
                var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                var res = await http.PostAsync("https://uploads.github.com/repos/" + owner + "/" + repo + "/releases/" + releaseId + "/assets?name=" + Uri.EscapeDataString(name), content);
                if (!res.IsSuccessStatusCode) throw new Exception("upload of " + name + " failed (" + (int)res.StatusCode + "): " + Short(await res.Content.ReadAsStringAsync()));
            }
            Log("Uploaded " + name + ".");
        }

        private static string Json(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }

        private static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > 300 ? s.Substring(0, 300) : s;
        }
    }
}
