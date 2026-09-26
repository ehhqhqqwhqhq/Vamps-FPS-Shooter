using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vamp.Core;
using Vamp.Maps;
using Vamp.Match;

namespace Vamp.Online
{
    /// <summary>
    /// Player-hosted custom lobbies over Unity Relay (Unity Multiplayer Services "Sessions"):
    /// the host's PC runs the game (NGO host) and friends connect through Relay - no port forwarding.
    /// Public lobbies are listed in the SERVER BROWSER; private ones are joined with the lobby code.
    /// Joining checks the game VERSION so players on old builds are told to update.
    /// </summary>
    public sealed class OnlineService : IOnlineService
    {
        private static readonly IReadOnlyList<OnlineMember> NoMembers = new List<OnlineMember>();
        private const string KeyMode = "mode", KeyMap = "map", KeyVersion = "ver", KeyHost = "host";

        private readonly NetworkManager _nm;
        private ISession _session;
        private bool _busy;
        private bool _leaving;
        private readonly Dictionary<ulong, string> _pendingNames = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, int> _pendingLevels = new Dictionary<ulong, int>();
        private NetSession _hooked;

        public event Action Changed;
        public event Action<string, string> Notice;

        public OnlineService(NetworkManager nm)
        {
            _nm = nm;
            _nm.ConnectionApprovalCallback = Approve;
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        // ------------------------------------------------------------------ State

        private NetSession Net
        {
            get
            {
                var n = NetSession.Instance;
                if (n != _hooked)
                {
                    if (_hooked != null) _hooked.Changed -= Raise;
                    _hooked = n;
                    if (n != null) n.Changed += Raise;
                }
                return n;
            }
        }

        public bool InSession { get { return _session != null || (_nm != null && _nm.IsListening); } }
        public bool IsHost { get { return _nm != null && _nm.IsServer; } }
        public string LobbyCode { get { return _session != null && _session.Code != null ? _session.Code : (Net != null ? Net.Code : ""); } }
        public string LobbyName { get { return Net != null ? Net.LobbyName : (_session != null ? _session.Name : ""); } }
        public MatchConfig Config { get { return Net != null ? Net.Config : null; } }
        public IReadOnlyList<OnlineMember> Members { get { return Net != null ? Net.MemberViews : NoMembers; } }
        public float TimeRemaining { get { return Net != null ? Net.TimeRemaining : -1f; } }
        public int[] TeamScores { get { return Net != null ? Net.TeamScores : new[] { 0, 0 }; } }
        public string LastWinner { get { return Net != null ? Net.Winner : ""; } }

        public OnlineState State
        {
            get
            {
                if (!InSession) return _busy ? OnlineState.Connecting : OnlineState.Offline;
                var n = Net;
                if (n == null) return OnlineState.Connecting;
                switch (n.State)
                {
                    case NetState.Loading: return OnlineState.Loading;
                    case NetState.InMatch: return OnlineState.InMatch;
                    case NetState.PostMatch: return OnlineState.PostMatch;
                    default: return OnlineState.InLobby;
                }
            }
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }

        private void Say(string title, string message)
        {
            if (Notice != null) Notice(title, message);
            if (Game.Notifications != null) Game.Notifications.Push(NotificationKind.Info, title, message);
        }

        // ------------------------------------------------------------------ Sign in (anonymous, per local account)

        internal static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (AuthenticationService.Instance.IsSignedIn) return;
            // One Unity player id per local VAMP account (+ editor) so two copies on one PC can play together.
            string profile = Sanitize(Game.Username) + (Application.isEditor ? "_ed" : "");
            try { AuthenticationService.Instance.SwitchProfile(profile); } catch (Exception) { }
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s ?? "") if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
            string r = sb.Length > 0 ? sb.ToString() : "player";
            return r.Length > 24 ? r.Substring(0, 24) : r;
        }

        private static string Friendly(Exception e)
        {
            string m = e.Message ?? "UNKNOWN ERROR";
            if (m.IndexOf("relay", StringComparison.OrdinalIgnoreCase) >= 0 && m.IndexOf("not", StringComparison.OrdinalIgnoreCase) >= 0)
                m = "RELAY IS NOT ENABLED FOR THIS PROJECT (UNITY CLOUD DASHBOARD)";
            if (m.Length > 140) m = m.Substring(0, 140);
            return m.ToUpperInvariant();
        }

        // ------------------------------------------------------------------ Host

        public async void Host(MatchConfig config, string lobbyName, Action<bool, string> done)
        {
            if (_busy || InSession) { Done(done, false, "ALREADY IN A LOBBY"); return; }
            if (config.mode != GameMode.FreeForAll && config.mode != GameMode.TeamDeathmatch)
            { Done(done, false, "ONLINE LOBBIES SUPPORT FREE FOR ALL AND TEAM DEATHMATCH"); return; }
            _busy = true;
            Raise();
            try
            {
                await SignIn();
                var cfg = config.Clone();
                cfg.bots = 0;
                cfg.isCustom = true;
                cfg.maxPlayers = Mathf.Clamp(cfg.maxPlayers, 2, 12);
                var map = MapCatalog.Get(cfg.mapId);
                string name = string.IsNullOrEmpty(lobbyName) ? Game.Username + "'S LOBBY" : lobbyName;

                _nm.NetworkConfig.ConnectionData = OnlineBootstrap.Payload(Game.Username, LocalLevel());
                var options = new SessionOptions
                {
                    MaxPlayers = 12, // the party can switch modes later; VAMP enforces the mode's player limit itself
                    Name = name,
                    IsPrivate = cfg.isPrivate,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { KeyMode, new SessionProperty(cfg.ModeLabel, VisibilityPropertyOptions.Public) },
                        { KeyMap, new SessionProperty(map != null ? map.DisplayName : cfg.mapId, VisibilityPropertyOptions.Public) },
                        { KeyVersion, new SessionProperty(Application.version, VisibilityPropertyOptions.Public) },
                        { KeyHost, new SessionProperty(Game.Username, VisibilityPropertyOptions.Public) },
                    }
                }.WithRelayNetwork();

                MatchController.SuppressOffline = true;
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                if (!await WaitFor(() => _nm.IsServer, 10f)) throw new Exception("THE HOST COULD NOT START");

                var go = UnityEngine.Object.Instantiate(OnlineBootstrap.SessionPrefab);
                UnityEngine.Object.DontDestroyOnLoad(go);
                var ns = go.GetComponent<NetSession>();
                go.GetComponent<NetworkObject>().Spawn();
                ns.ServerInit(cfg, name, _session.Code); // after Spawn: NetworkVariables must belong to a spawned object
                ns.ServerAddMember(_nm.LocalClientId, Game.Username, LocalLevel());
                _busy = false;
                Raise();
                Done(done, true, _session.Code);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Host failed: " + e);
                _busy = false;
                await Cleanup();
                Done(done, false, Friendly(e));
            }
        }

        // ------------------------------------------------------------------ Join

        public void JoinByCode(string code, Action<bool, string> done)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4) { Done(done, false, "ENTER A LOBBY CODE"); return; }
            Join(() => MultiplayerService.Instance.JoinSessionByCodeAsync(code), done);
        }

        public void JoinById(string lobbyId, Action<bool, string> done)
        {
            Join(() => MultiplayerService.Instance.JoinSessionByIdAsync(lobbyId), done);
        }

        private async void Join(Func<Task<ISession>> joinCall, Action<bool, string> done)
        {
            if (_busy || InSession) { Done(done, false, "ALREADY IN A LOBBY"); return; }
            _busy = true;
            Raise();
            try
            {
                await SignIn();
                _nm.NetworkConfig.ConnectionData = OnlineBootstrap.Payload(Game.Username, LocalLevel());
                MatchController.SuppressOffline = true;
                _session = await joinCall();
                if (!await WaitFor(() => _nm.IsConnectedClient && NetSession.Instance != null, 20f))
                    throw new Exception(!string.IsNullOrEmpty(_nm.DisconnectReason) ? _nm.DisconnectReason : "COULD NOT CONNECT TO THE HOST");
                _busy = false;
                Raise();
                Done(done, true, _session.Code);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Join failed: " + e);
                string reason = !string.IsNullOrEmpty(_nm.DisconnectReason) ? _nm.DisconnectReason : Friendly(e);
                _busy = false;
                await Cleanup();
                Done(done, false, reason);
            }
        }

        // ------------------------------------------------------------------ Browse

        public async void Browse(Action<bool, string, List<OnlineLobbyInfo>> done)
        {
            var list = new List<OnlineLobbyInfo>();
            try
            {
                await SignIn();
                var results = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());
                foreach (var s in results.Sessions)
                {
                    var info = new OnlineLobbyInfo
                    {
                        Id = s.Id,
                        Name = s.Name,
                        MaxPlayers = s.MaxPlayers,
                        Players = s.MaxPlayers - s.AvailableSlots,
                        Locked = s.IsLocked || s.HasPassword
                    };
                    if (s.Properties != null)
                    {
                        SessionProperty p;
                        if (s.Properties.TryGetValue(KeyMode, out p)) info.Mode = p.Value;
                        if (s.Properties.TryGetValue(KeyMap, out p)) info.Map = p.Value;
                        if (s.Properties.TryGetValue(KeyVersion, out p)) info.Version = p.Value;
                        if (s.Properties.TryGetValue(KeyHost, out p)) info.Host = p.Value;
                    }
                    list.Add(info);
                }
                if (done != null) done(true, "", list);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Browse failed: " + e);
                if (done != null) done(false, Friendly(e), list);
            }
        }

        // ------------------------------------------------------------------ Lobby actions

        public void SetReady(bool ready) { if (Net != null) Net.SetReadyRpc(ready); }
        public void SetTeam(int team) { if (Net != null) Net.SetTeamRpc(team); }

        public void SetConfig(MatchConfig config)
        {
            if (!IsHost || Net == null) return;
            if (config.mode != GameMode.FreeForAll && config.mode != GameMode.TeamDeathmatch) return;
            var cfg = config.Clone();
            cfg.bots = 0;
            Net.ServerSetConfig(cfg);
        }

        public void StartMatch() { if (IsHost && Net != null) Net.ServerStartMatch(); }

        public void Kick(ulong clientId)
        {
            if (!IsHost || clientId == _nm.LocalClientId) return;
            _nm.DisconnectClient(clientId, "YOU WERE REMOVED FROM THE PARTY");
        }
        public void ReturnToLobby() { if (IsHost && Net != null) Net.ServerReturnToLobby(); }

        public async void Leave()
        {
            if (_leaving) return;
            _leaving = true;
            await Cleanup();
            _leaving = false;
            ReturnToMenu();
        }

        private async Task Cleanup()
        {
            var s = _session;
            _session = null;
            try
            {
                if (s != null)
                {
                    if (s.IsHost) await s.AsHost().DeleteAsync();
                    else await s.LeaveAsync();
                }
            }
            catch (Exception e) { Debug.LogWarning("[VAMP] Leaving session: " + e.Message); }
            if (_nm != null && _nm.IsListening) _nm.Shutdown();
            _pendingNames.Clear();
            _pendingLevels.Clear();
            MatchController.SuppressOffline = false;
            Player.PlayerController.Local = null;
            UI.HUDController.ExternalMatchInfo = null;
            Raise();
        }

        private static void ReturnToMenu()
        {
            InputController.SetCursorLocked(false);
            Time.timeScale = 1f;
            if (SceneManager.GetActiveScene().name != MapCatalog.MainMenuScene && Game.Scenes != null) Game.Scenes.GoToMainMenu();
        }

        // ------------------------------------------------------------------ Netcode callbacks

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            if (request.ClientNetworkId == _nm.LocalClientId) { response.Approved = true; return; }

            string version, name;
            int level;
            if (!OnlineBootstrap.ParsePayload(request.Payload, out version, out name, out level))
            { response.Approved = false; response.Reason = "INVALID CONNECTION"; return; }
            if (version != Application.version)
            { response.Approved = false; response.Reason = "VERSION MISMATCH - HOST HAS v" + Application.version + ", YOU HAVE v" + version + ". UPDATE FROM THE LAUNCHER."; return; }
            var n = NetSession.Instance;
            if (n != null && n.State != NetState.Lobby)
            { response.Approved = false; response.Reason = "MATCH IN PROGRESS - TRY AGAIN AFTER IT ENDS"; return; }
            if (n != null && n.Config != null && n.MemberViews.Count >= n.Config.maxPlayers)
            { response.Approved = false; response.Reason = "LOBBY IS FULL"; return; }

            _pendingNames[request.ClientNetworkId] = name;
            _pendingLevels[request.ClientNetworkId] = level;
            response.Approved = true;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!_nm.IsServer || clientId == _nm.LocalClientId) { Raise(); return; }
            string name;
            int level;
            if (!_pendingNames.TryGetValue(clientId, out name)) name = "PLAYER";
            if (!_pendingLevels.TryGetValue(clientId, out level)) level = 1;
            var n = NetSession.Instance;
            if (n != null) n.ServerAddMember(clientId, name, level);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_nm.IsServer && clientId != _nm.LocalClientId) { Raise(); return; }
            // We (a client) lost the host, or were kicked / rejected.
            if (_busy || _leaving) return;
            string reason = !string.IsNullOrEmpty(_nm.DisconnectReason) ? _nm.DisconnectReason : "THE HOST ENDED THE SESSION";
            Say("DISCONNECTED", reason);
            Leave();
        }

        // ------------------------------------------------------------------ Helpers

        private static int LocalLevel()
        {
            return Game.Progression != null && Game.Progression.Profile != null ? Game.Progression.Profile.level : 1;
        }

        private static async Task<bool> WaitFor(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) return false;
                await Task.Yield();
            }
            return true;
        }

        private static void Done(Action<bool, string> done, bool ok, string message)
        {
            if (done != null) done(ok, message);
        }
    }
}
