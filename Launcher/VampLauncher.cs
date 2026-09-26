// VAMP Launcher - checks GitHub Releases for the newest VAMP build, downloads + installs it, then starts the game.
// Built by Unity (VAMP ▸ Release ▸ Build Launcher) with the C# compiler that ships with Windows (.NET Framework 4.x),
// so it is written in C# 5 (no string interpolation / ?. / expression-bodied members).
//
// Folder layout (next to "VAMP Launcher.exe"):
//   launcher.json      { "owner": "...", "repo": "...", "assetName": "VAMP-win64.zip", "gameExe": "VAMP.exe" }
//   Game\              the installed game (replaced on update)
//   Game\version.txt   installed version tag

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace VampLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2 (GitHub requires it)
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }
    }

    internal sealed class LauncherConfig
    {
        public string Owner = "";
        public string Repo = "";
        public string AssetName = "VAMP-win64.zip";
        public string GameExe = "VAMP.exe";

        public static LauncherConfig Load(string path)
        {
            var c = new LauncherConfig();
            if (!File.Exists(path)) return c;
            string json = File.ReadAllText(path);
            c.Owner = Field(json, "owner", c.Owner);
            c.Repo = Field(json, "repo", c.Repo);
            c.AssetName = Field(json, "assetName", c.AssetName);
            c.GameExe = Field(json, "gameExe", c.GameExe);
            return c;
        }

        private static string Field(string json, string key, string fallback)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : fallback;
        }
    }

    internal sealed class LauncherForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(10, 10, 12);
        private static readonly Color Red = Color.FromArgb(200, 16, 46);
        private static readonly Color Dim = Color.FromArgb(140, 140, 145);

        private readonly string _root;
        private readonly string _gameDir;
        private readonly string _versionFile;
        private readonly LauncherConfig _cfg;

        private readonly Label _status;
        private readonly Label _version;
        private readonly ProgressBar _bar;
        private readonly Button _play;
        private readonly LinkLabel _retry;

        private string _latestTag;
        private string _downloadUrl;
        private string _notes = "";

        public LauncherForm()
        {
            _root = AppDomain.CurrentDomain.BaseDirectory;
            _gameDir = Path.Combine(_root, "Game");
            _versionFile = Path.Combine(_gameDir, "version.txt");
            _cfg = LauncherConfig.Load(Path.Combine(_root, "launcher.json"));

            Text = "VAMP Launcher";
            ClientSize = new Size(760, 420);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);

            var title = new Label { Text = "VAMP", Font = new Font("Segoe UI", 54f, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(40, 30), BackColor = Bg };
            var tag = new Label { Text = "MOVE FAST. AIM FASTER. NEVER STOP MOVING.", Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Red, AutoSize = true, Location = new Point(46, 128), BackColor = Bg };
            _version = new Label { Text = "", ForeColor = Dim, AutoSize = true, Location = new Point(46, 158), BackColor = Bg };
            _status = new Label { Text = "CHECKING FOR UPDATES...", Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Color.White, AutoSize = false, Size = new Size(680, 48), Location = new Point(40, 250), BackColor = Bg };
            _bar = new ProgressBar { Location = new Point(40, 305), Size = new Size(680, 10), Style = ProgressBarStyle.Continuous, Maximum = 1000 };
            _play = new Button
            {
                Text = "PLAY", Font = new Font("Segoe UI", 16f, FontStyle.Bold), BackColor = Red, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Size = new Size(220, 56), Location = new Point(500, 335), Enabled = false
            };
            _play.FlatAppearance.BorderSize = 0;
            _play.Click += delegate { Launch(); };
            _retry = new LinkLabel { Text = "CHECK AGAIN", AutoSize = true, Location = new Point(40, 355), LinkColor = Dim, ActiveLinkColor = Color.White, BackColor = Bg, Visible = false };
            _retry.LinkClicked += delegate { CheckForUpdate(); };

            Controls.Add(title);
            Controls.Add(tag);
            Controls.Add(_version);
            Controls.Add(_status);
            Controls.Add(_bar);
            Controls.Add(_play);
            Controls.Add(_retry);

            Shown += delegate { CheckForUpdate(); };
        }

        private string InstalledVersion
        {
            get { return File.Exists(_versionFile) ? File.ReadAllText(_versionFile).Trim() : ""; }
        }

        private bool GameInstalled
        {
            get { return File.Exists(Path.Combine(_gameDir, _cfg.GameExe)); }
        }

        private void SetStatus(string text)
        {
            _status.Text = text;
            _version.Text = "INSTALLED: " + (GameInstalled && InstalledVersion.Length > 0 ? InstalledVersion : "NONE")
                            + (string.IsNullOrEmpty(_latestTag) ? "" : "     LATEST: " + _latestTag);
        }

        // ------------------------------------------------------------------ Update check

        private void CheckForUpdate()
        {
            _retry.Visible = false;
            _play.Enabled = false;
            _bar.Value = 0;
            if (_cfg.Owner.Length == 0 || _cfg.Repo.Length == 0)
            {
                SetStatus("launcher.json IS MISSING THE GITHUB OWNER / REPO.");
                _play.Enabled = GameInstalled;
                return;
            }
            SetStatus("CHECKING FOR UPDATES...");
            var wc = NewClient();
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            wc.DownloadStringCompleted += OnReleaseInfo;
            wc.DownloadStringAsync(new Uri("https://api.github.com/repos/" + _cfg.Owner + "/" + _cfg.Repo + "/releases/latest"));
        }

        private void OnReleaseInfo(object sender, DownloadStringCompletedEventArgs e)
        {
            ((WebClient)sender).Dispose();
            if (e.Error != null)
            {
                Offline("COULDN'T REACH THE UPDATE SERVER.");
                return;
            }
            string json = e.Result;
            var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            var url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*" + Regex.Escape(_cfg.AssetName) + ")\"");
            var body = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!tag.Success || !url.Success)
            {
                Offline("THE LATEST RELEASE HAS NO " + _cfg.AssetName + ".");
                return;
            }
            _latestTag = tag.Groups[1].Value;
            _downloadUrl = url.Groups[1].Value;
            _notes = body.Success ? Regex.Unescape(body.Groups[1].Value) : "";

            if (GameInstalled && InstalledVersion == _latestTag)
            {
                SetStatus("UP TO DATE - READY TO PLAY.");
                _bar.Value = _bar.Maximum;
                _play.Enabled = true;
                return;
            }
            Download();
        }

        private void Offline(string why)
        {
            _retry.Visible = true;
            if (GameInstalled)
            {
                SetStatus(why + " YOU CAN STILL PLAY THE INSTALLED VERSION (ONLINE LOBBIES NEED THE SAME VERSION AS THE HOST).");
                _play.Enabled = true;
            }
            else SetStatus(why + " CONNECT TO THE INTERNET AND CLICK CHECK AGAIN.");
        }

        // ------------------------------------------------------------------ Download + install

        private string _zipPath;

        private void Download()
        {
            SetStatus((GameInstalled ? "UPDATING TO " : "INSTALLING ") + _latestTag + "...");
            _zipPath = Path.Combine(Path.GetTempPath(), "vamp_update_" + Guid.NewGuid().ToString("N") + ".zip");
            var wc = NewClient();
            wc.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs a)
            {
                if (a.TotalBytesToReceive > 0) _bar.Value = (int)Math.Min(_bar.Maximum, a.BytesReceived * _bar.Maximum / a.TotalBytesToReceive);
                _status.Text = "DOWNLOADING " + _latestTag + "   " + (a.BytesReceived / 1048576) + " / " + Math.Max(1, a.TotalBytesToReceive / 1048576) + " MB";
            };
            wc.DownloadFileCompleted += OnDownloaded;
            wc.DownloadFileAsync(new Uri(_downloadUrl), _zipPath);
        }

        private void OnDownloaded(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            ((WebClient)sender).Dispose();
            if (e.Error != null)
            {
                TryDelete(_zipPath);
                Offline("DOWNLOAD FAILED.");
                return;
            }
            SetStatus("INSTALLING " + _latestTag + "...");
            Application.DoEvents();
            string staging = Path.Combine(_root, "Game_new");
            string old = Path.Combine(_root, "Game_old");
            try
            {
                TryDeleteDir(staging);
                ZipFile.ExtractToDirectory(_zipPath, staging);
                // If the zip has a single top-level folder, use its contents.
                string exe = FindExe(staging);
                if (exe == null) throw new Exception("THE DOWNLOAD DOESN'T CONTAIN " + _cfg.GameExe);
                string content = Path.GetDirectoryName(exe);
                TryDeleteDir(old);
                // Fast path: swap folders. OneDrive / antivirus often lock freshly extracted files for a few
                // seconds, so fall back to copying over the old install (with retries) if the rename fails.
                bool swapped = false;
                try
                {
                    if (Directory.Exists(_gameDir)) Directory.Move(_gameDir, old);
                    Directory.Move(content, _gameDir);
                    swapped = true;
                }
                catch (Exception)
                {
                    if (!Directory.Exists(_gameDir) && Directory.Exists(old)) Directory.Move(old, _gameDir);
                }
                if (!swapped) CopyWithRetry(content, _gameDir);
                File.WriteAllText(_versionFile, _latestTag);
                TryDeleteDir(staging);
                TryDeleteDir(old);
                _bar.Value = _bar.Maximum;
                SetStatus("UPDATED TO " + _latestTag + " - READY TO PLAY." + (_notes.Length > 0 ? "\n" + FirstLine(_notes) : ""));
                _play.Enabled = true;
            }
            catch (Exception ex)
            {
                // Roll back if the swap failed half way.
                if (!Directory.Exists(_gameDir) && Directory.Exists(old)) Directory.Move(old, _gameDir);
                Offline("INSTALL FAILED: " + ex.Message.ToUpperInvariant() + " (IS VAMP STILL RUNNING?)");
            }
            finally { TryDelete(_zipPath); }
        }

        private string FindExe(string dir)
        {
            string direct = Path.Combine(dir, _cfg.GameExe);
            if (File.Exists(direct)) return direct;
            foreach (var sub in Directory.GetDirectories(dir))
            {
                string p = Path.Combine(sub, _cfg.GameExe);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static void CopyWithRetry(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(to, dir.Substring(from.Length).TrimStart('\\', '/')));
            foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string dest = Path.Combine(to, file.Substring(from.Length).TrimStart('\\', '/'));
                for (int attempt = 0; ; attempt++)
                {
                    try { File.Copy(file, dest, true); break; }
                    catch (IOException)
                    {
                        if (attempt >= 20) throw;
                        System.Threading.Thread.Sleep(500);
                        Application.DoEvents();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        if (attempt >= 20) throw;
                        System.Threading.Thread.Sleep(500);
                        Application.DoEvents();
                    }
                }
            }
        }

        private static void TryDeleteDir(string path)
        {
            for (int i = 0; i < 5; i++)
            {
                try { if (Directory.Exists(path)) Directory.Delete(path, true); return; }
                catch (Exception) { System.Threading.Thread.Sleep(400); }
            }
        }

        private static string FirstLine(string s)
        {
            int i = s.IndexOf('\n');
            string line = i >= 0 ? s.Substring(0, i) : s;
            return line.Length > 90 ? line.Substring(0, 90) : line;
        }

        private static void TryDelete(string path)
        {
            try { if (path != null && File.Exists(path)) File.Delete(path); } catch (Exception) { }
        }

        private static WebClient NewClient()
        {
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "VAMP-Launcher";
            return wc;
        }

        // ------------------------------------------------------------------ Play

        private void Launch()
        {
            string exe = Path.Combine(_gameDir, _cfg.GameExe);
            if (!File.Exists(exe)) { SetStatus("VAMP ISN'T INSTALLED YET."); return; }
            try
            {
                Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = _gameDir, UseShellExecute = true });
                Close();
            }
            catch (Exception ex) { SetStatus("COULDN'T START VAMP: " + ex.Message); }
        }
    }
}
