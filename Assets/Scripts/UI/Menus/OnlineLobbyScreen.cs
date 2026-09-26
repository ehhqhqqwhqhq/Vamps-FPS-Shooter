using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Maps;
using Vamp.Match;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// ONLINE LOBBY (player-hosted). Everyone: lobby code + COPY, player list (level, team, ready), READY / TEAM, LEAVE.
    /// Host: edit the rules (mode, map, time, score, max players, friendly fire) and START MATCH.
    /// </summary>
    public sealed class OnlineLobbyScreen : MenuScreen
    {
        private static readonly GameMode[] Modes = { GameMode.FreeForAll, GameMode.TeamDeathmatch };
        private static readonly float[] Times = { 0f, 3f, 5f, 8f, 10f, 15f, 20f };
        private static readonly int[] Scores = { 0, 10, 15, 20, 25, 30, 40, 50 };

        private RectTransform _members;
        private RectTransform _rules;
        private Text _code;
        private Text _summary;
        private Button _start;
        private Button _ready;
        private bool _dirty = true;
        private float _poll;
        private string _rulesKey = "";

        private IOnlineService O { get { return Game.Online; } }

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "PARTY", O != null ? O.LobbyName + "  ·  THE PARTY STAYS TOGETHER THROUGH EVERY MATCH" : "", 1180f);

            var codeRow = UIKit.Row(col, 44f, 12f);
            _code = UIKit.Label(codeRow, "", 26, UIKit.Text);
            _code.supportRichText = true;
            UIKit.Size(_code, -1, -1, 1f);
            var invite = UIKit.Button(codeRow, "INVITE FRIENDS", () => Host.Push(new FriendsScreen(0)), UIKit.ButtonStyle.Primary, 15, 40f);
            UIKit.Size(invite, 40f, 200f);
            var copy = UIKit.Button(codeRow, "COPY CODE", () =>
            {
                if (O == null) return;
                GUIUtility.systemCopyBuffer = O.LobbyCode;
                Toast("CODE COPIED", O.LobbyCode);
            }, UIKit.ButtonStyle.Box, 15, 40f);
            UIKit.Size(copy, 40f, 200f);
            _summary = UIKit.Label(col, "", 16, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIKit.Size(_summary, 26f);

            var body = UIKit.Row(col, 520f, 30f, "Body");
            var left = UIKit.Column(body, 6f, "Players");
            UIKit.Size(left, -1, -1, 1.3f);
            UIKit.Size(UIKit.Label(left, "PLAYERS", 14, UIKit.Red), 22f);
            UIKit.Divider(left);
            _members = UIKit.Column(left, 4f, "List");

            var right = UIKit.Column(body, 4f, "Rules");
            UIKit.Size(right, -1, -1, 1f);
            UIKit.Size(UIKit.Label(right, "RULES", 14, UIKit.Red), 22f);
            UIKit.Divider(right);
            _rules = UIKit.Column(right, 2f, "RulesList");

            UIKit.Spacer(col, 0f, true);
            var buttons = UIKit.Row(col, 56f, 12f);
            _start = UIKit.Button(buttons, "START MATCH", () => { if (O != null) O.StartMatch(); }, UIKit.ButtonStyle.Primary, 22, 56f);
            UIKit.Size(_start, 56f, -1, 1f);
            _ready = UIKit.Button(buttons, "READY", () =>
            {
                if (O == null) return;
                bool now = false;
                foreach (var m in O.Members) if (m.IsLocal) now = m.Ready;
                O.SetReady(!now);
            }, UIKit.ButtonStyle.Primary, 22, 56f);
            UIKit.Size(_ready, 56f, -1, 1f);
            var leave = UIKit.Button(buttons, "LEAVE PARTY", () =>
                UIKit.Modal(Host.ModalRoot, "LEAVE PARTY?", O != null && O.IsHost ? "YOU ARE THE PARTY LEADER - THE PARTY CLOSES FOR EVERYONE." : "",
                    ("LEAVE", () => { if (O != null) O.Leave(); Host.ClearTo(new HomeScreen()); }, UIKit.ButtonStyle.Danger),
                    ("STAY", null, UIKit.ButtonStyle.Box)),
                UIKit.ButtonStyle.Ghost, 18, 56f);
            UIKit.Size(leave, 56f, 240f);
            Refresh();
        }

        public override void OnShow()
        {
            if (O != null) O.Changed += MarkDirty;
            _dirty = true;
        }

        public override void OnHide()
        {
            if (O != null) O.Changed -= MarkDirty;
        }

        public override bool OnBack()
        {
            UIKit.Modal(Host.ModalRoot, "LEAVE PARTY?", "",
                ("LEAVE", () => { if (O != null) O.Leave(); Host.ClearTo(new HomeScreen()); }, UIKit.ButtonStyle.Danger),
                ("STAY", null, UIKit.ButtonStyle.Box));
            return true;
        }

        private void MarkDirty() { _dirty = true; }

        public override void Tick(float dt)
        {
            if (O == null || !O.InSession)
            {
                Host.ClearTo(new HomeScreen());
                return;
            }
            _poll -= dt;
            if (_poll <= 0f) { _poll = 0.5f; _dirty = true; }
            if (_dirty) Refresh();
        }

        private void Refresh()
        {
            _dirty = false;
            if (O == null || _members == null) return;
            var cfg = O.Config;
            bool host = O.IsHost;
            _code.text = "LOBBY CODE:  <color=#C8102E>" + (string.IsNullOrEmpty(O.LobbyCode) ? "..." : O.LobbyCode) + "</color>";
            if (cfg != null)
            {
                var map = MapCatalog.Get(cfg.mapId);
                _summary.text = cfg.ModeLabel + "  ·  " + (map != null ? map.DisplayName : cfg.mapId)
                                + "  ·  " + (cfg.timeLimitMinutes > 0f ? cfg.timeLimitMinutes + " MIN" : "NO TIME LIMIT")
                                + "  ·  " + (cfg.scoreLimit > 0 ? cfg.scoreLimit + " KILLS" : "NO SCORE LIMIT")
                                + "  ·  v" + Application.version + (O.State == OnlineState.Connecting ? "  ·  CONNECTING..." : "");
            }

            // Players
            UIKit.Clear(_members);
            int ready = 0, total = 0;
            bool localReady = false;
            foreach (var m in O.Members)
            {
                total++;
                if (m.Ready || m.IsHost) ready++;
                if (m.IsLocal) localReady = m.Ready;
                var row = UIKit.Row(_members, 42f, 10f);
                var bg = row.gameObject.AddComponent<Image>();
                bg.color = m.IsLocal ? new Color(0.78f, 0.05f, 0.11f, 0.22f) : new Color(1f, 1f, 1f, 0.04f);
                bg.raycastTarget = false;
                var name = UIKit.Label(row, "  [" + m.Level + "]  " + m.Name + (m.IsHost ? "   ♛ LEADER" : ""), 18, UIKit.Text);
                UIKit.Size(name, -1, -1, 1f);
                if (cfg != null && cfg.IsTeamMode)
                {
                    string t = m.Team == 1 ? "TEAM B" : "TEAM A";
                    if (m.IsLocal)
                    {
                        int other = m.Team == 1 ? 0 : 1;
                        var tb = UIKit.Button(row, t + "  ⇄", () => O.SetTeam(other), UIKit.ButtonStyle.Box, 14, 34f);
                        UIKit.Size(tb, 34f, 130f);
                    }
                    else UIKit.Size(UIKit.Label(row, t, 15, m.Team == 1 ? UIKit.EnemyColor() : UIKit.AllyColor(), TextAnchor.MiddleCenter), -1, 130f);
                }
                var st = UIKit.Label(row, m.IsHost ? "HOST" : (m.Ready ? "READY" : "NOT READY"), 15, m.Ready || m.IsHost ? UIKit.Good : UIKit.TextFaint, TextAnchor.MiddleCenter);
                UIKit.Size(st, -1, 120f);
                if (host && !m.IsLocal)
                {
                    ulong kickId = m.ClientId;
                    string kickName = m.Name;
                    var kick = UIKit.Button(row, "KICK", () => UIKit.Modal(Host.ModalRoot, "REMOVE " + kickName + "?", "",
                        ("REMOVE", () => O.Kick(kickId), UIKit.ButtonStyle.Danger), ("CANCEL", null, UIKit.ButtonStyle.Box)), UIKit.ButtonStyle.Ghost, 12, 32f);
                    UIKit.Size(kick, 32f, 70f);
                }
            }
            int max = cfg != null ? cfg.maxPlayers : 8;
            for (int i = total; i < max && i < 12; i++)
                UIKit.Size(UIKit.Label(_members, "   OPEN SLOT", 15, UIKit.TextFaint, TextAnchor.MiddleLeft, FontStyle.Normal), 28f);

            _start.gameObject.SetActive(host);
            _ready.gameObject.SetActive(!host);
            if (host) UIKit.ButtonText(_start).text = "START MATCH  (" + ready + "/" + total + " READY)";
            else UIKit.ButtonText(_ready).text = localReady ? "NOT READY" : "READY";

            BuildRules(cfg, host);
        }

        private void BuildRules(MatchConfig cfg, bool host)
        {
            if (cfg == null) return;
            string key = (host ? "H" : "C") + JsonUtility.ToJson(cfg);
            if (key == _rulesKey) return;
            _rulesKey = key;
            UIKit.Clear(_rules);
            if (!host)
            {
                Line("MODE", cfg.ModeLabel);
                var map = MapCatalog.Get(cfg.mapId);
                Line("MAP", map != null ? map.DisplayName : cfg.mapId);
                Line("TIME LIMIT", cfg.timeLimitMinutes > 0f ? cfg.timeLimitMinutes + " MIN" : "NONE");
                Line("SCORE LIMIT", cfg.scoreLimit > 0 ? cfg.scoreLimit + " KILLS" : "NONE");
                Line("MAX PLAYERS", cfg.maxPlayers.ToString());
                Line("RESPAWNS", cfg.respawns ? "ON" : "OFF");
                if (cfg.IsTeamMode) Line("FRIENDLY FIRE", cfg.friendlyFire ? "ON" : "OFF");
                Line("GRAVITY", Mathf.RoundToInt(cfg.gravityMultiplier * 100f) + "%");
                Line("MOVEMENT SPEED", Mathf.RoundToInt(cfg.movementSpeedMultiplier * 100f) + "%");
                UIKit.Spacer(_rules, 8f);
                UIKit.Caption(_rules, "ONLY THE HOST CAN CHANGE THE RULES.", 13);
                return;
            }

            var modeNames = new List<string>();
            foreach (var m in MatchConfig.OnlineChoices) modeNames.Add(m.Label);
            UIKit.Selector(_rules, "MODE", modeNames, MatchConfig.IndexOf(MatchConfig.OnlineChoices, cfg), i =>
            {
                var c = MatchConfig.OnlineChoices[i].Create();
                if (c.maxPlayers < O.Members.Count)
                {
                    Toast("TOO MANY PLAYERS", c.ModeLabel + " IS FOR " + c.maxPlayers + " PLAYERS - YOUR PARTY HAS " + O.Members.Count, true);
                    _rulesKey = "";
                    _dirty = true;
                    return;
                }
                c.isPrivate = cfg.isPrivate;
                c.isCustom = true;
                if (c.teamSize == 0) c.mapId = cfg.mapId;
                O.SetConfig(c);
            }, 200f);
            var mapNames = new List<string>();
            int mapIdx = 0;
            for (int i = 0; i < MapCatalog.Maps.Count; i++)
            {
                mapNames.Add(MapCatalog.Maps[i].DisplayName);
                if (MapCatalog.Maps[i].Id == cfg.mapId) mapIdx = i;
            }
            UIKit.Selector(_rules, "MAP", mapNames, mapIdx, i => { var c = cfg.Clone(); c.mapId = MapCatalog.Maps[i].Id; O.SetConfig(c); }, 200f);
            var times = new List<string>();
            foreach (var t in Times) times.Add(t <= 0f ? "NONE" : t + " MIN");
            UIKit.Selector(_rules, "TIME LIMIT", times, Nearest(Times, cfg.timeLimitMinutes), i => { var c = cfg.Clone(); c.timeLimitMinutes = Times[i]; O.SetConfig(c); }, 200f);
            var scores = new List<string>();
            foreach (var s in Scores) scores.Add(s == 0 ? "NONE" : s + " KILLS");
            UIKit.Selector(_rules, "SCORE LIMIT", scores, NearestI(Scores, cfg.scoreLimit), i => { var c = cfg.Clone(); c.scoreLimit = Scores[i]; O.SetConfig(c); }, 200f);
            UIKit.Toggle(_rules, "RESPAWNS", cfg.respawns, v => { var c = cfg.Clone(); c.respawns = v; O.SetConfig(c); });
            if (cfg.IsTeamMode) UIKit.Toggle(_rules, "FRIENDLY FIRE", cfg.friendlyFire, v => { var c = cfg.Clone(); c.friendlyFire = v; O.SetConfig(c); });
            UIKit.Spacer(_rules, 8f);
            UIKit.Caption(_rules, "INVITE FRIENDS OR SHARE THE CODE (PLAY ▸ SERVER BROWSER ▸ JOIN WITH CODE). EVERYONE HERE PLAYS EVERY MATCH TOGETHER.", 13);
        }

        private void Line(string label, string value)
        {
            var row = UIKit.Row(_rules, 32f, 10f);
            UIKit.Size(UIKit.Label(row, label, 16, UIKit.TextDim), -1, 200f);
            UIKit.Size(UIKit.Label(row, value, 16, UIKit.Text), -1, -1, 1f);
        }

        private static int Nearest(float[] values, float v)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++) if (Mathf.Abs(values[i] - v) < Mathf.Abs(values[best] - v)) best = i;
            return best;
        }

        private static int NearestI(int[] values, int v)
        {
            int best = 0;
            for (int i = 1; i < values.Length; i++) if (Mathf.Abs(values[i] - v) < Mathf.Abs(values[best] - v)) best = i;
            return best;
        }
    }
}
