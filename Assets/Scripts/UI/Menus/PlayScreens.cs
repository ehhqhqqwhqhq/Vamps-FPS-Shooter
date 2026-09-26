using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Maps;
using Vamp.Match;
using Vamp.Matchmaking;

namespace Vamp.UI.Menus
{
    /// <summary>PLAY: QUICK PLAY · RANKED · CUSTOM GAME · PRIVATE MATCH · SERVER BROWSER · TRAINING.</summary>
    public sealed class PlayScreen : MenuScreen
    {
        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "PLAY", "OFFLINE BUILD · MATCHES ARE PLAYED AGAINST BOTS", 560f);
            UIKit.Button(col, "QUICK PLAY", () => Host.Push(new QuickPlayScreen()), UIKit.ButtonStyle.Menu, 32, 58f);
            var ranked = UIKit.Button(col, "RANKED  ·  REQUIRES ONLINE", null, UIKit.ButtonStyle.Menu, 32, 58f);
            ranked.interactable = false;
            UIKit.Button(col, "CUSTOM GAME", () => Host.Push(new CustomGameScreen(false)), UIKit.ButtonStyle.Menu, 32, 58f);
            UIKit.Button(col, "PRIVATE MATCH", () => Host.Push(new CustomGameScreen(true)), UIKit.ButtonStyle.Menu, 32, 58f);
            UIKit.Button(col, "SERVER BROWSER", () => Host.Push(new ServerBrowserScreen()), UIKit.ButtonStyle.Menu, 32, 58f);
            UIKit.Button(col, "TRAINING", () =>
            {
                var cfg = MatchConfig.Defaults(GameMode.Training);
                Game.Scenes.StartMatch(cfg);
            }, UIKit.ButtonStyle.Menu, 32, 58f);
            UIKit.Spacer(col, 10f);
            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);
        }
    }

    /// <summary>QUICK PLAY: preferred modes, region, max ping → SEARCHING... PLAYERS FOUND 8/10 · PING.</summary>
    public sealed class QuickPlayScreen : MenuScreen
    {
        private readonly QuickPlayPreferences _prefs = new QuickPlayPreferences();
        private RectTransform _searchPanel;
        private Text _searchTitle, _found, _ping, _time;
        private Button _findButton;
        private float _autoStart = -1f;

        private static readonly GameMode[] QuickModes = { GameMode.FreeForAll, GameMode.TeamDeathmatch, GameMode.GunGame, GameMode.Elimination };
        private static readonly int[] Pings = { 40, 60, 80, 120, 200 };

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "QUICK PLAY", "FIND A MATCH AUTOMATICALLY", 720f);
            UIKit.Caption(col, "PREFERRED MODES", 14);
            foreach (var mode in QuickModes)
            {
                var m = mode;
                UIKit.Toggle(col, MatchConfig.ModeName(m), _prefs.modes.Contains(m), on =>
                {
                    if (on && !_prefs.modes.Contains(m)) _prefs.modes.Add(m);
                    if (!on) _prefs.modes.Remove(m);
                });
            }
            UIKit.Selector(col, "REGION", MatchmakingService.Regions, 0, i => _prefs.region = MatchmakingService.Regions[i]);
            var pingNames = new List<string>();
            foreach (var p in Pings) pingNames.Add(p + " MS");
            UIKit.Selector(col, "MAXIMUM PING", pingNames, 2, i => _prefs.maxPing = Pings[i]);
            UIKit.Spacer(col, 10f);
            _findButton = UIKit.Button(col, "FIND MATCH", Find, UIKit.ButtonStyle.Primary, 24, 58f);
            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);

            // Searching overlay (bottom-center)
            var panel = UIKit.Panel(root, "Searching", new Color(0.04f, 0.04f, 0.05f, 0.95f));
            _searchPanel = panel.rectTransform;
            _searchPanel.anchorMin = _searchPanel.anchorMax = new Vector2(0.5f, 0f);
            _searchPanel.pivot = new Vector2(0.5f, 0f);
            _searchPanel.anchoredPosition = new Vector2(80f, 60f);
            _searchPanel.sizeDelta = new Vector2(520f, 210f);
            UIKit.VList(_searchPanel, 6f, 24, TextAnchor.UpperCenter);
            _searchTitle = UIKit.Label(_searchPanel, "SEARCHING...", 34, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Size(_searchTitle, 44f);
            _found = UIKit.Label(_searchPanel, "", 20, UIKit.TextDim, TextAnchor.MiddleCenter);
            UIKit.Size(_found, 28f);
            _ping = UIKit.Label(_searchPanel, "", 16, UIKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Normal);
            UIKit.Size(_ping, 24f);
            _time = UIKit.Label(_searchPanel, "", 14, UIKit.TextFaint, TextAnchor.MiddleCenter, FontStyle.Normal);
            UIKit.Size(_time, 20f);
            UIKit.Button(_searchPanel, "CANCEL", Cancel, UIKit.ButtonStyle.Ghost, 16, 38f);
            _searchPanel.gameObject.SetActive(false);
        }

        private void Find()
        {
            if (_prefs.modes.Count == 0)
            {
                Toast("SELECT AT LEAST ONE MODE", "", true);
                return;
            }
            Game.Matchmaking.StartQuickPlay(_prefs);
            _searchPanel.gameObject.SetActive(true);
            _findButton.interactable = false;
            _autoStart = -1f;
        }

        private void Cancel()
        {
            Game.Matchmaking.Cancel();
            _searchPanel.gameObject.SetActive(false);
            _findButton.interactable = true;
            _autoStart = -1f;
        }

        public override void Tick(float dt)
        {
            var mm = Game.Matchmaking;
            if (mm == null || _searchPanel == null || !_searchPanel.gameObject.activeSelf) return;
            _found.text = "PLAYERS FOUND: " + mm.PlayersFound + "/" + mm.PlayersNeeded;
            _ping.text = "PING: LOCAL  ·  REGION: " + _prefs.region;
            _time.text = "OFFLINE: EMPTY SLOTS ARE FILLED WITH BOTS  ·  " + Mathf.FloorToInt(mm.SearchTime) + "s";

            if (mm.State == MatchmakingState.MatchFound)
            {
                if (_autoStart < 0f)
                {
                    _autoStart = 2f;
                    _searchTitle.text = "MATCH FOUND";
                    _searchTitle.color = UIKit.Red;
                    Audio.AudioController.PlayUI(Audio.SfxId.CountdownGo);
                }
                _autoStart -= dt;
                _time.text = MatchConfig.ModeName(mm.FoundMatch.mode) + "  ·  " + MapCatalog.Get(mm.FoundMatch.mapId).DisplayName;
                if (_autoStart <= 0f)
                {
                    _autoStart = 999f;
                    Game.Scenes.StartMatch(mm.FoundMatch);
                }
            }
            else
            {
                _searchTitle.text = "SEARCHING" + new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);
                _searchTitle.color = UIKit.Text;
            }
        }

        public override bool OnBack()
        {
            if (Game.Matchmaking != null && Game.Matchmaking.State == MatchmakingState.Searching) { Cancel(); return true; }
            return false;
        }

        public override void OnHide()
        {
            if (Game.Matchmaking != null && Game.Matchmaking.State == MatchmakingState.Searching) Game.Matchmaking.Cancel();
        }
    }

    /// <summary>
    /// CUSTOM GAME / PRIVATE MATCH host options: mode, map, max players, bots, difficulty, time limit, score limit,
    /// weapon restrictions, respawns, friendly fire, gravity, movement settings, private/public, lobby code.
    /// </summary>
    public sealed class CustomGameScreen : MenuScreen
    {
        private readonly bool _private;
        private MatchConfig _cfg;
        private RectTransform _options;
        private Text _codeText;

        private static readonly GameMode[] Modes = { GameMode.FreeForAll, GameMode.TeamDeathmatch, GameMode.GunGame, GameMode.MovementRace, GameMode.Elimination, GameMode.Training };
        private static readonly float[] Times = { 0f, 3f, 5f, 8f, 10f, 15f, 20f };
        private static readonly int[] Scores = { 0, 10, 15, 20, 25, 30, 40, 50, 75 };
        private static readonly float[] Gravity = { 0.5f, 0.75f, 1f, 1.25f, 1.5f };
        private static readonly float[] Speed = { 0.75f, 1f, 1.25f, 1.5f };

        public CustomGameScreen(bool privateMatch)
        {
            _private = privateMatch;
            _cfg = MatchConfig.Defaults(GameMode.FreeForAll);
            _cfg.isCustom = true;
            _cfg.isPrivate = privateMatch;
            if (privateMatch) _cfg.lobbyCode = "VAMP-" + UnityEngine.Random.Range(1000, 10000);
        }

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, _private ? "PRIVATE MATCH" : "CUSTOM GAME",
                _private ? "INVITE-ONLY LOBBY · SHARE THE CODE WITH FRIENDS WHEN ONLINE" : "HOST YOUR OWN RULES", 820f);
            if (_private)
            {
                var codeRow = UIKit.Row(col, 40f, 12f);
                _codeText = UIKit.Label(codeRow, "LOBBY CODE:  <color=#C8102E>" + _cfg.lobbyCode + "</color>", 24, UIKit.Text);
                UIKit.Size(_codeText, -1, -1, 1f);
                var join = UIKit.Button(codeRow, "JOIN WITH CODE", () => Host.Push(new ServerBrowserScreen()), UIKit.ButtonStyle.Ghost, 14, 38f);
                UIKit.Size(join, 38f, 200f);
            }

            ScrollRect scroll;
            _options = UIKit.ScrollList(col, out scroll, 2f);
            UIKit.Size(scroll, -1, -1, -1, 1f);
            BuildOptions();

            var buttons = UIKit.Row(col, 56f, 12f);
            var start = UIKit.Button(buttons, "PLAY VS BOTS", () => Game.Scenes.StartMatch(_cfg.Clone()), UIKit.ButtonStyle.Primary, 22, 56f);
            UIKit.Size(start, 56f, -1, 1f);
            if (Game.Online != null)
            {
                Button host = null;
                host = UIKit.Button(buttons, "HOST ONLINE LOBBY", () =>
                {
                    if (_cfg.mode != GameMode.FreeForAll && _cfg.mode != GameMode.TeamDeathmatch)
                    {
                        Toast("ONLINE LOBBIES", "CHOOSE FREE FOR ALL OR TEAM DEATHMATCH", true);
                        return;
                    }
                    host.interactable = false;
                    UIKit.ButtonText(host).text = "CREATING LOBBY...";
                    Game.Online.Host(_cfg.Clone(), Game.Username + "'S LOBBY", (ok, msg) =>
                    {
                        if (Root == null) return;
                        host.interactable = true;
                        UIKit.ButtonText(host).text = "HOST ONLINE LOBBY";
                        if (ok) Host.Push(new OnlineLobbyScreen());
                        else Toast("COULDN'T CREATE LOBBY", msg, true);
                    });
                }, UIKit.ButtonStyle.Box, 20, 56f);
                UIKit.Size(host, 56f, -1, 1f);
            }
            var back = UIKit.Button(buttons, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 56f);
            UIKit.Size(back, 56f, 200f);
        }

        private void BuildOptions()
        {
            UIKit.Clear(_options);
            var o = _options;
            var modeNames = new List<string>();
            foreach (var m in Modes) modeNames.Add(MatchConfig.ModeName(m));
            UIKit.Selector(o, "MODE", modeNames, Array.IndexOf(Modes, _cfg.mode), i =>
            {
                string code = _cfg.lobbyCode;
                var restricted = _cfg.restrictedWeapons;
                _cfg = MatchConfig.Defaults(Modes[i]);
                _cfg.isCustom = true;
                _cfg.isPrivate = _private;
                _cfg.lobbyCode = code;
                _cfg.restrictedWeapons = restricted;
                BuildOptions();
            });
            UIKit.Caption(o, MatchConfig.ModeDescription(_cfg.mode), 13);

            var mapNames = new List<string>();
            int mapIdx = 0;
            for (int i = 0; i < MapCatalog.Maps.Count; i++)
            {
                mapNames.Add(MapCatalog.Maps[i].DisplayName);
                if (MapCatalog.Maps[i].Id == _cfg.mapId) mapIdx = i;
            }
            UIKit.Selector(o, "MAP", mapNames, mapIdx, i => _cfg.mapId = MapCatalog.Maps[i].Id);

            if (_cfg.mode != GameMode.Training && _cfg.mode != GameMode.MovementRace)
            {
                UIKit.SliderRow(o, "MAX PLAYERS", 2, 12, _cfg.maxPlayers, "0", v =>
                {
                    _cfg.maxPlayers = Mathf.RoundToInt(v);
                    _cfg.bots = Mathf.Min(_cfg.bots, _cfg.maxPlayers - 1);
                }, true);
                UIKit.SliderRow(o, "BOTS", 0, 11, _cfg.bots, "0", v => _cfg.bots = Mathf.Min(Mathf.RoundToInt(v), _cfg.maxPlayers - 1), true);
                UIKit.Selector(o, "BOT DIFFICULTY", new[] { "EASY", "NORMAL", "HARD", "BRUTAL" }, (int)_cfg.botDifficulty, i => _cfg.botDifficulty = (BotDifficulty)i);
            }
            else if (_cfg.mode == GameMode.Training)
            {
                UIKit.SliderRow(o, "PRACTICE BOTS", 0, 8, _cfg.bots, "0", v => _cfg.bots = Mathf.RoundToInt(v), true);
            }

            var timeNames = new List<string>();
            foreach (var t in Times) timeNames.Add(t <= 0f ? "NONE" : t + " MIN");
            UIKit.Selector(o, "TIME LIMIT", timeNames, Nearest(Times, _cfg.timeLimitMinutes), i => _cfg.timeLimitMinutes = Times[i]);

            if (_cfg.mode == GameMode.FreeForAll || _cfg.mode == GameMode.TeamDeathmatch)
            {
                var scoreNames = new List<string>();
                foreach (var s in Scores) scoreNames.Add(s == 0 ? "NONE" : s + " KILLS");
                UIKit.Selector(o, "SCORE LIMIT", scoreNames, Nearest(Scores, _cfg.scoreLimit), i => _cfg.scoreLimit = Scores[i]);
            }
            if (_cfg.mode == GameMode.Elimination)
                UIKit.SliderRow(o, "ROUNDS TO WIN", 1, 7, _cfg.scoreLimit, "0", v => _cfg.scoreLimit = Mathf.RoundToInt(v), true);

            if (_cfg.mode != GameMode.Elimination && _cfg.mode != GameMode.MovementRace)
                UIKit.Toggle(o, "RESPAWNS", _cfg.respawns, v => _cfg.respawns = v);
            if (_cfg.IsTeamMode) UIKit.Toggle(o, "FRIENDLY FIRE", _cfg.friendlyFire, v => _cfg.friendlyFire = v);

            UIKit.Selector(o, "GRAVITY", Names(Gravity), Nearest(Gravity, _cfg.gravityMultiplier), i => _cfg.gravityMultiplier = Gravity[i]);
            UIKit.Selector(o, "MOVEMENT SPEED", Names(Speed), Nearest(Speed, _cfg.movementSpeedMultiplier), i => _cfg.movementSpeedMultiplier = Speed[i]);
            UIKit.Toggle(o, "PRIVATE (INVITE ONLY)", _cfg.isPrivate, v => _cfg.isPrivate = v);

            if (_cfg.mode != GameMode.GunGame && _cfg.mode != GameMode.MovementRace && Game.Weapons != null)
            {
                UIKit.Spacer(o, 6f);
                UIKit.Caption(o, "WEAPON RESTRICTIONS", 14);
                foreach (var w in Game.Weapons.weapons)
                {
                    if (w == null) continue;
                    string id = w.id;
                    UIKit.Toggle(o, "ALLOW " + w.displayName, !_cfg.restrictedWeapons.Contains(id), allowed =>
                    {
                        if (!allowed && !_cfg.restrictedWeapons.Contains(id)) _cfg.restrictedWeapons.Add(id);
                        if (allowed) _cfg.restrictedWeapons.Remove(id);
                    });
                }
            }
        }

        private static List<string> Names(float[] values)
        {
            var l = new List<string>();
            foreach (var v in values) l.Add(Mathf.RoundToInt(v * 100f) + "%");
            return l;
        }

        private static int Nearest(float[] values, float v)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++) if (Mathf.Abs(values[i] - v) < Mathf.Abs(values[best] - v)) best = i;
            return best;
        }

        private static int Nearest(int[] values, int v)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++) if (Mathf.Abs(values[i] - v) < Mathf.Abs(values[best] - v)) best = i;
            return best;
        }
    }

    /// <summary>
    /// SERVER BROWSER: public player-hosted lobbies (Unity Relay / Sessions) + JOIN WITH CODE for private ones.
    /// Lobbies on a different game version are shown but can't be joined (update from the launcher).
    /// </summary>
    public sealed class ServerBrowserScreen : MenuScreen
    {
        private RectTransform _list;
        private InputField _code;
        private Text _status;
        private bool _busy;

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "SERVER BROWSER", "PLAYER-HOSTED LOBBIES", 1100f);
            if (Game.Online == null)
            {
                var off = UIKit.Label(col, "ONLINE PLAY ISN'T AVAILABLE IN THIS BUILD.", 18, UIKit.TextFaint, TextAnchor.MiddleCenter, FontStyle.Normal);
                UIKit.Size(off, 120f);
                UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 16, 46f);
                return;
            }

            var codeRow = UIKit.Row(col, 48f, 12f);
            _code = UIKit.Input(codeRow, "LOBBY CODE (FROM YOUR FRIEND)", false, 12, 46f);
            UIKit.Size(_code, 46f, 420f);
            UIKit.OnEnter(_code, JoinCode);
            var join = UIKit.Button(codeRow, "JOIN WITH CODE", JoinCode, UIKit.ButtonStyle.Primary, 18, 46f);
            UIKit.Size(join, 46f, 240f);
            UIKit.Spacer(codeRow, 0f, true);

            var header = UIKit.Row(col, 30f, 10f);
            Head(header, "LOBBY", -1, 1.6f);
            Head(header, "HOST", -1, 1f);
            Head(header, "MODE", -1, 1f);
            Head(header, "MAP", -1, 1f);
            Head(header, "PLAYERS", 110f, -1);
            Head(header, "", 150f, -1);
            UIKit.Divider(col);

            ScrollRect scroll;
            _list = UIKit.ScrollList(col, out scroll, 4f);
            UIKit.Size(scroll, -1, -1, -1, 1f);
            _status = UIKit.Label(col, "", 15, UIKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Normal);
            UIKit.Size(_status, 28f);

            var buttons = UIKit.Row(col, 48f, 10f);
            var refresh = UIKit.Button(buttons, "REFRESH", Refresh, UIKit.ButtonStyle.Box, 16, 46f);
            UIKit.Size(refresh, 46f, 200f);
            UIKit.Spacer(buttons, 0f, true);
            var back = UIKit.Button(buttons, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 16, 46f);
            UIKit.Size(back, 46f, 200f);
            Refresh();
        }

        private static void Head(Transform row, string text, float width, float flex)
        {
            var l = UIKit.Label(row, text, 13, UIKit.TextDim);
            UIKit.Size(l, -1, width, flex);
        }

        private void Refresh()
        {
            if (_busy || Game.Online == null) return;
            _busy = true;
            _status.text = "SEARCHING...";
            Game.Online.Browse((ok, msg, lobbies) =>
            {
                _busy = false;
                if (Root == null) return;
                UIKit.Clear(_list);
                if (!ok) { _status.text = "COULDN'T REACH ONLINE SERVICES: " + msg; return; }
                _status.text = lobbies.Count == 0 ? "NO PUBLIC LOBBIES RIGHT NOW. HOST ONE FROM PLAY ▸ CUSTOM GAME." : lobbies.Count + " LOBBIES";
                foreach (var l in lobbies) AddRow(l);
            });
        }

        private void AddRow(OnlineLobbyInfo l)
        {
            var row = UIKit.Row(_list, 44f, 10f);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.04f);
            bg.raycastTarget = false;
            bool sameVersion = string.IsNullOrEmpty(l.Version) || l.Version == Application.version;
            Cell(row, "  " + l.Name, -1, 1.6f, UIKit.Text);
            Cell(row, l.Host ?? "-", -1, 1f, UIKit.TextDim);
            Cell(row, l.Mode ?? "-", -1, 1f, UIKit.TextDim);
            Cell(row, l.Map ?? "-", -1, 1f, UIKit.TextDim);
            Cell(row, l.Players + " / " + l.MaxPlayers, 110f, -1, UIKit.Text);
            bool full = l.Players >= l.MaxPlayers;
            string label = !sameVersion ? "v" + l.Version : full ? "FULL" : l.Locked ? "LOCKED" : "JOIN";
            string id = l.Id;
            var join = UIKit.Button(row, label, () => Join(id), UIKit.ButtonStyle.Primary, 15, 38f);
            UIKit.Size(join, 38f, 150f);
            join.interactable = sameVersion && !full && !l.Locked;
        }

        private static void Cell(Transform row, string text, float width, float flex, Color c)
        {
            var t = UIKit.Label(row, text, 16, c);
            UIKit.Size(t, -1, width, flex);
        }

        private void JoinCode()
        {
            if (_busy || _code == null) return;
            _busy = true;
            _status.text = "JOINING " + _code.text.ToUpperInvariant() + "...";
            Game.Online.JoinByCode(_code.text, OnJoined);
        }

        private void Join(string id)
        {
            if (_busy) return;
            _busy = true;
            _status.text = "JOINING...";
            Game.Online.JoinById(id, OnJoined);
        }

        private void OnJoined(bool ok, string msg)
        {
            _busy = false;
            if (Root == null) return;
            if (ok) { Host.Push(new OnlineLobbyScreen()); return; }
            _status.text = "COULDN'T JOIN: " + msg;
            Toast("COULDN'T JOIN", msg, true);
        }
    }
}
