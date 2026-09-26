using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vamp.Core;
using Vamp.Maps;
using Vamp.Match;
using Vamp.UI;

namespace Vamp.Online
{
    /// <summary>
    /// The HOST-AUTHORITATIVE lobby + match state for a player-hosted custom game. Spawned by the host when the lobby
    /// is created and kept alive across scene loads (DontDestroyOnLoad). Holds: members (name, level, team, ready,
    /// K/D), the rules (MatchConfig as JSON), state (Lobby → Loading → InMatch → PostMatch), timer and team scores.
    /// Only the host writes; clients request changes (ready / team) through RPCs.
    /// </summary>
    public sealed class NetSession : NetworkBehaviour
    {
        public static NetSession Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        private readonly NetworkVariable<byte> _state = new NetworkVariable<byte>(0);
        private readonly NetworkVariable<FixedString4096Bytes> _config = new NetworkVariable<FixedString4096Bytes>();
        private readonly NetworkVariable<FixedString64Bytes> _lobbyName = new NetworkVariable<FixedString64Bytes>();
        private readonly NetworkVariable<FixedString32Bytes> _code = new NetworkVariable<FixedString32Bytes>();
        private readonly NetworkVariable<double> _endTime = new NetworkVariable<double>(-1);
        private readonly NetworkVariable<double> _startTime = new NetworkVariable<double>(0);
        private readonly NetworkVariable<int> _scoreA = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> _scoreB = new NetworkVariable<int>(0);
        private readonly NetworkVariable<FixedString64Bytes> _winner = new NetworkVariable<FixedString64Bytes>();
        private NetworkList<NetMember> _members;

        private MatchConfig _cfg;
        private string _cfgJson;
        private readonly List<OnlineMember> _views = new List<OnlineMember>();
        private bool _viewsDirty = true;

        public event Action Changed;

        public NetState State { get { return (NetState)_state.Value; } }
        public string LobbyName { get { return _lobbyName.Value.ToString(); } }
        public string Code { get { return _code.Value.ToString(); } }
        public string Winner { get { return _winner.Value.ToString(); } }
        public int[] TeamScores { get { return new[] { _scoreA.Value, _scoreB.Value }; } }
        public double StartTime { get { return _startTime.Value; } }

        public MatchConfig Config
        {
            get
            {
                string json = _config.Value.ToString();
                if (_cfg == null || json != _cfgJson)
                {
                    _cfgJson = json;
                    _cfg = string.IsNullOrEmpty(json) ? MatchConfig.Defaults(GameMode.FreeForAll) : JsonUtility.FromJson<MatchConfig>(json);
                }
                return _cfg;
            }
        }

        public float TimeRemaining
        {
            get
            {
                if (_endTime.Value < 0 || NetworkManager == null) return -1f;
                return Mathf.Max(0f, (float)(_endTime.Value - NetworkManager.ServerTime.Time));
            }
        }

        public float Elapsed
        {
            get { return NetworkManager == null ? 0f : Mathf.Max(0f, (float)(NetworkManager.ServerTime.Time - _startTime.Value)); }
        }

        private void Awake()
        {
            _members = new NetworkList<NetMember>();
        }

        // ------------------------------------------------------------------ Lifecycle

        public override void OnNetworkSpawn()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _state.OnValueChanged += OnStateChanged;
            _config.OnValueChanged += (a, b) => Raise();
            _lobbyName.OnValueChanged += (a, b) => Raise();
            _scoreA.OnValueChanged += (a, b) => Raise();
            _scoreB.OnValueChanged += (a, b) => Raise();
            _winner.OnValueChanged += (a, b) => Raise();
            _members.OnListChanged += e => Raise();
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
            if (IsServer)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
            Raise();
        }

        public override void OnNetworkDespawn()
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            if (IsServer && NetworkManager != null)
            {
                if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Instance == this) Instance = null;
            HUDController.ExternalMatchInfo = null;
        }

        private void Raise()
        {
            _viewsDirty = true;
            if (Changed != null) Changed();
        }

        private void OnStateChanged(byte previous, byte current)
        {
            Raise();
            if ((NetState)current == NetState.PostMatch)
            {
                InputController.SetCursorLocked(false);
                if (Player.PlayerController.Local != null) Player.PlayerController.Local.Input.GameplayInputEnabled = false;
            }
        }

        // ------------------------------------------------------------------ Views for the UI

        public IReadOnlyList<OnlineMember> MemberViews
        {
            get
            {
                if (!_viewsDirty) return _views;
                _viewsDirty = false;
                _views.Clear();
                ulong local = NetworkManager != null ? NetworkManager.LocalClientId : ulong.MaxValue;
                foreach (var m in _members)
                {
                    _views.Add(new OnlineMember
                    {
                        ClientId = m.ClientId,
                        Name = m.Name.ToString(),
                        Level = m.Level,
                        Team = m.Team,
                        Ready = m.Ready,
                        IsHost = m.ClientId == NetworkManager.ServerClientId,
                        IsLocal = m.ClientId == local,
                        Kills = m.Kills,
                        Deaths = m.Deaths,
                        Score = m.Kills * 100
                    });
                }
                return _views;
            }
        }

        public bool TryGetMember(ulong clientId, out NetMember member)
        {
            foreach (var m in _members) if (m.ClientId == clientId) { member = m; return true; }
            member = default(NetMember);
            return false;
        }

        public int TeamOf(ulong clientId)
        {
            NetMember m;
            return TryGetMember(clientId, out m) ? m.Team : -1;
        }

        public string NameOf(ulong clientId)
        {
            NetMember m;
            return TryGetMember(clientId, out m) ? m.Name.ToString() : "PLAYER";
        }

        // ------------------------------------------------------------------ Host: setup + members

        public void ServerInit(MatchConfig cfg, string lobbyName, string code)
        {
            _config.Value = new FixedString4096Bytes(JsonUtility.ToJson(cfg));
            _lobbyName.Value = new FixedString64Bytes(Trim(lobbyName, 60));
            _code.Value = new FixedString32Bytes(Trim(code, 28));
            _state.Value = (byte)NetState.Lobby;
        }

        public void ServerSetConfig(MatchConfig cfg)
        {
            if (!IsServer || State != NetState.Lobby) return;
            _config.Value = new FixedString4096Bytes(JsonUtility.ToJson(cfg));
            if (!cfg.IsTeamMode) for (int i = 0; i < _members.Count; i++) { var m = _members[i]; m.Team = -1; _members[i] = m; }
            else BalanceTeams(false);
        }

        public void ServerAddMember(ulong clientId, string name, int level)
        {
            if (!IsServer) return;
            for (int i = 0; i < _members.Count; i++) if (_members[i].ClientId == clientId) return;
            _members.Add(new NetMember
            {
                ClientId = clientId,
                Name = new FixedString32Bytes(Trim(string.IsNullOrEmpty(name) ? "PLAYER" : name, 28)),
                Level = Mathf.Clamp(level, 1, 999),
                Team = -1,
                Ready = clientId == NetworkManager.ServerClientId
            });
            if (Config.IsTeamMode) BalanceTeams(false);
            Notice("PLAYER JOINED", name);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                if (_members[i].ClientId != clientId) continue;
                string n = _members[i].Name.ToString();
                _members.RemoveAt(i);
                Notice("PLAYER LEFT", n);
            }
        }

        private void BalanceTeams(bool force)
        {
            int a = 0, b = 0;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Team == 0) a++;
                else if (_members[i].Team == 1) b++;
            }
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                if (!force && (m.Team == 0 || m.Team == 1)) continue;
                m.Team = a <= b ? 0 : 1;
                if (m.Team == 0) a++; else b++;
                _members[i] = m;
            }
        }

        // ------------------------------------------------------------------ Client requests

        [Rpc(SendTo.Server)]
        public void SetReadyRpc(bool ready, RpcParams p = default)
        {
            if (State != NetState.Lobby) return;
            ulong id = p.Receive.SenderClientId;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].ClientId != id) continue;
                var m = _members[i]; m.Ready = ready; _members[i] = m;
            }
        }

        [Rpc(SendTo.Server)]
        public void SetTeamRpc(int team, RpcParams p = default)
        {
            if (State != NetState.Lobby || !Config.IsTeamMode) return;
            ulong id = p.Receive.SenderClientId;
            team = Mathf.Clamp(team, 0, 1);
            int count = 0;
            foreach (var x in _members) if (x.Team == team) count++;
            int max = Mathf.CeilToInt(Mathf.Max(2, Config.maxPlayers) / 2f);
            if (count >= max) return;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].ClientId != id) continue;
                var m = _members[i]; m.Team = team; _members[i] = m;
            }
        }

        // ------------------------------------------------------------------ Host: match flow

        public void ServerStartMatch()
        {
            if (!IsServer || State != NetState.Lobby) return;
            var cfg = Config;
            var map = MapCatalog.Get(cfg.mapId);
            if (map == null) return;
            if (cfg.IsTeamMode) BalanceTeams(false);
            for (int i = 0; i < _members.Count; i++) { var m = _members[i]; m.Kills = 0; m.Deaths = 0; _members[i] = m; }
            _scoreA.Value = 0;
            _scoreB.Value = 0;
            _winner.Value = default(FixedString64Bytes);
            _endTime.Value = -1;
            _state.Value = (byte)NetState.Loading;
            var status = NetworkManager.SceneManager.LoadScene(map.SceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning("[VAMP] Online: could not load " + map.SceneName + ": " + status);
                _state.Value = (byte)NetState.Lobby;
            }
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (!IsServer) return;
            if (sceneName == MapCatalog.MainMenuScene)
            {
                _state.Value = (byte)NetState.Lobby;
                return;
            }
            if (State != NetState.Loading) return;

            var prefab = OnlineBootstrap.PlayerPrefab;
            if (prefab == null) { Debug.LogError("[VAMP] Online: Resources/VampNetPlayer prefab missing (VAMP ▸ Build All Scenes)."); return; }
            var taken = new List<Vector3>();
            foreach (var id in completed)
            {
                NetMember m;
                if (!TryGetMember(id, out m)) continue;
                var spawn = NetSpawns.Choose(Config.IsTeamMode ? m.Team : -1, taken);
                taken.Add(spawn.Position);
                var go = Instantiate(prefab, spawn.Position, Quaternion.Euler(0f, spawn.Yaw, 0f));
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
            }
            foreach (var id in timedOut) NetworkManager.DisconnectClient(id);

            _startTime.Value = NetworkManager.ServerTime.Time;
            _endTime.Value = Config.timeLimitMinutes > 0f ? NetworkManager.ServerTime.Time + Config.timeLimitMinutes * 60.0 : -1;
            _state.Value = (byte)NetState.InMatch;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || State != NetState.InMatch) return;
            if (_endTime.Value >= 0 && NetworkManager.ServerTime.Time >= _endTime.Value) ServerEndMatch();
        }

        /// <summary>Host: a kill happened (killer == victim for suicides / world deaths).</summary>
        public void ServerRecordKill(ulong killer, ulong victim)
        {
            if (!IsServer || State != NetState.InMatch) return;
            var cfg = Config;
            bool suicide = killer == victim;
            int killerTeam = TeamOf(killer), victimTeam = TeamOf(victim);
            bool teamKill = !suicide && cfg.IsTeamMode && killerTeam == victimTeam;
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                if (m.ClientId == victim) m.Deaths++;
                if (m.ClientId == killer && !suicide) m.Kills += teamKill ? -1 : 1;
                _members[i] = m;
            }
            if (cfg.IsTeamMode && !suicide && !teamKill)
            {
                if (killerTeam == 0) _scoreA.Value++;
                else if (killerTeam == 1) _scoreB.Value++;
            }

            if (cfg.scoreLimit > 0)
            {
                if (cfg.IsTeamMode) { if (_scoreA.Value >= cfg.scoreLimit || _scoreB.Value >= cfg.scoreLimit) ServerEndMatch(); }
                else foreach (var m in _members) if (m.Kills >= cfg.scoreLimit) { ServerEndMatch(); break; }
            }
        }

        public void ServerEndMatch()
        {
            if (!IsServer || State != NetState.InMatch) return;
            string winner;
            if (Config.IsTeamMode)
                winner = _scoreA.Value == _scoreB.Value ? "DRAW" : (_scoreA.Value > _scoreB.Value ? "TEAM A WINS" : "TEAM B WINS");
            else
            {
                NetMember best = default(NetMember);
                bool any = false, tie = false;
                foreach (var m in _members)
                {
                    if (!any || m.Kills > best.Kills) { best = m; any = true; tie = false; }
                    else if (m.Kills == best.Kills) tie = true;
                }
                winner = !any ? "MATCH OVER" : tie ? "DRAW" : best.Name + " WINS";
            }
            _winner.Value = new FixedString64Bytes(Trim(winner, 60));
            _state.Value = (byte)NetState.PostMatch;
        }

        public void ServerReturnToLobby()
        {
            if (!IsServer || (State != NetState.PostMatch && State != NetState.InMatch)) return;
            for (int i = 0; i < _members.Count; i++) { var m = _members[i]; m.Ready = m.ClientId == NetworkManager.ServerClientId; _members[i] = m; }
            _state.Value = (byte)NetState.Loading;
            NetworkManager.SceneManager.LoadScene(MapCatalog.MainMenuScene, LoadSceneMode.Single);
        }

        // ------------------------------------------------------------------ Local scene setup (every peer)

        private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == MapCatalog.MainMenuScene || scene.name == MapCatalog.BootScene)
            {
                HUDController.ExternalMatchInfo = null;
                return;
            }
            // A map: HUD, pause menu (time keeps running) and the scoreboard / results overlay.
            var root = new GameObject("[Online Match]");
            root.AddComponent<HUDController>();
            root.AddComponent<MovementDebugOverlay>();
            var pause = root.AddComponent<PauseMenu>();
            pause.KeepTimeRunning = true;
            // Party leader leaving a match brings the whole party back to the party lobby;
            // a member leaving a match leaves the party (everyone in a party shares one session).
            pause.LeaveOverride = () =>
            {
                if (Game.Online == null) return;
                if (Game.Online.IsHost) Game.Online.ReturnToLobby();
                else Game.Online.Leave();
            };
            root.AddComponent<NetMatchUI>();
            HUDController.ExternalMatchInfo = MatchInfo;
            Audio.AudioController.SetMusic("match");
        }

        private bool MatchInfo(out string mode, out string objective, out string score, out float seconds)
        {
            var cfg = Config;
            mode = "ONLINE  ·  " + cfg.ModeLabel;
            float remaining = TimeRemaining;
            seconds = remaining >= 0f ? remaining : Elapsed;
            ulong me = NetworkManager.LocalClientId;
            if (cfg.IsTeamMode)
            {
                int mine = Mathf.Max(0, TeamOf(me));
                int[] s = TeamScores;
                score = "<color=#" + ColorUtility.ToHtmlStringRGB(UIKit.AllyColor()) + ">" + s[mine] + "</color>   —   <color=#"
                        + ColorUtility.ToHtmlStringRGB(UIKit.EnemyColor()) + ">" + s[1 - mine] + "</color>";
                objective = cfg.scoreLimit > 0 ? "FIRST TEAM TO " + cfg.scoreLimit + " KILLS" : "MOST KILLS WINS";
            }
            else
            {
                int mineK = 0, lead = 0;
                foreach (var m in _members)
                {
                    if (m.ClientId == me) mineK = m.Kills;
                    lead = Mathf.Max(lead, m.Kills);
                }
                score = "YOU " + mineK + "   ·   LEADER " + lead;
                objective = cfg.scoreLimit > 0 ? "FIRST TO " + cfg.scoreLimit + " KILLS" : "MOST KILLS WINS";
            }
            if (State == NetState.Loading) objective = "WAITING FOR PLAYERS...";
            if (State == NetState.PostMatch) objective = Winner;
            return true;
        }

        // ------------------------------------------------------------------ Helpers

        private void Notice(string title, string message)
        {
            NoticeRpc(new FixedString64Bytes(Trim(title, 60)), new FixedString64Bytes(Trim(message, 60)));
        }

        [Rpc(SendTo.Everyone)]
        private void NoticeRpc(FixedString64Bytes title, FixedString64Bytes message)
        {
            if (Game.Notifications != null) Game.Notifications.Push(NotificationKind.Info, title.ToString(), message.ToString());
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length > max ? s.Substring(0, max) : s;
        }
    }
}
