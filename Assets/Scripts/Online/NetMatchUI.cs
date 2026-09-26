using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Maps;
using Vamp.Match;
using Vamp.Progression;
using Vamp.UI;

namespace Vamp.Online
{
    /// <summary>
    /// Online match overlay: "waiting for players", the TAB scoreboard (live K/D from the host) and the results screen
    /// (winner, table, XP earned, host: BACK TO LOBBY, everyone: LEAVE).
    /// </summary>
    public sealed class NetMatchUI : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _board;
        private RectTransform _rows;
        private Text _title;
        private Text _sub;
        private Text _reward;
        private RectTransform _buttons;
        private GameObject _waiting;
        private bool _results;
        private bool _xpGiven;
        private string _xpLine = "";
        private float _refresh;

        private void Start()
        {
            _canvas = UIFactory.CreateCanvas("Online Canvas", transform, 150);
            var root = _canvas.transform;

            var wait = UIKit.Label(root, "WAITING FOR PLAYERS...", 30, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Stretch(wait.rectTransform);
            _waiting = wait.gameObject;

            var bg = UIKit.Panel(root, "Board", new Color(0.03f, 0.03f, 0.035f, 0.94f));
            _board = bg.rectTransform;
            _board.anchorMin = _board.anchorMax = new Vector2(0.5f, 0.5f);
            _board.sizeDelta = new Vector2(980f, 640f);
            UIKit.VList(_board, 10f, 30);
            _title = UIKit.Heading(_board, "SCOREBOARD", 44);
            UIKit.Size(_title, 56f);
            _sub = UIKit.Label(_board, "", 18, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIKit.Size(_sub, 26f);
            _reward = UIKit.Label(_board, "", 18, UIKit.Red, TextAnchor.MiddleLeft);
            UIKit.Size(_reward, 26f);
            var head = UIKit.Row(_board, 26f, 10f);
            Col(head, "PLAYER", -1, 1f, UIKit.TextFaint, 14);
            Col(head, "TEAM", 120f, -1, UIKit.TextFaint, 14);
            Col(head, "KILLS", 100f, -1, UIKit.TextFaint, 14);
            Col(head, "DEATHS", 100f, -1, UIKit.TextFaint, 14);
            Col(head, "K/D", 100f, -1, UIKit.TextFaint, 14);
            UIKit.Divider(_board);
            _rows = UIKit.Column(_board, 4f, "Rows");
            UIKit.Size(_rows, -1, -1, -1, 1f);
            _buttons = UIKit.Row(_board, 52f, 12f, "Buttons");
            _board.gameObject.SetActive(false);
        }

        private static Text Col(Transform row, string text, float width, float flex, Color color, int size)
        {
            var l = UIKit.Label(row, text, size, color);
            UIKit.Size(l, -1, width, flex);
            return l;
        }

        private void Update()
        {
            var online = Game.Online;
            if (online == null || !online.InSession) { if (_canvas != null) _canvas.gameObject.SetActive(false); return; }
            _canvas.gameObject.SetActive(true);

            var state = online.State;
            _waiting.SetActive(state == OnlineState.Loading || (state == OnlineState.InMatch && Player.PlayerController.Local == null));

            bool post = state == OnlineState.PostMatch;
            bool tab = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            bool show = post || (tab && state == OnlineState.InMatch);
            if (_board.gameObject.activeSelf != show) _board.gameObject.SetActive(show);

            if (post && !_results) EnterResults(online);
            if (!post && _results) { _results = false; UIKit.Clear(_buttons); }

            if (!show) return;
            _refresh -= Time.unscaledDeltaTime;
            if (_refresh > 0f) return;
            _refresh = 0.25f;
            BuildRows(online);
        }

        private void BuildRows(IOnlineService online)
        {
            var cfg = online.Config;
            bool teams = cfg != null && cfg.IsTeamMode;
            if (_results) { _title.text = online.LastWinner; }
            else _title.text = "SCOREBOARD";
            int[] s = online.TeamScores;
            _sub.text = (cfg != null ? cfg.ModeLabel + "  ·  " + MapCatalog.Get(cfg.mapId).DisplayName : "")
                        + (teams ? "  ·  TEAM A " + s[0] + " — " + s[1] + " TEAM B" : "");
            _reward.text = _results ? _xpLine : "";

            UIKit.Clear(_rows);
            var members = new List<OnlineMember>(online.Members);
            members.Sort((a, b) => b.Kills != a.Kills ? b.Kills.CompareTo(a.Kills) : a.Deaths.CompareTo(b.Deaths));
            foreach (var m in members)
            {
                var row = UIKit.Row(_rows, 34f, 10f);
                var bg = row.gameObject.AddComponent<Image>();
                bg.color = m.IsLocal ? new Color(0.78f, 0.05f, 0.11f, 0.25f) : new Color(1f, 1f, 1f, 0.03f);
                bg.raycastTarget = false;
                Col(row, "  [" + m.Level + "] " + m.Name + (m.IsBot ? "  (BOT)" : m.IsHost ? "  (HOST)" : ""), -1, 1f, m.IsBot ? UIKit.TextDim : UIKit.Text, 18);
                Col(row, teams ? (m.Team == 0 ? "A" : "B") : "-", 120f, -1, teams ? (m.Team == 0 ? UIKit.AllyColor() : UIKit.EnemyColor()) : UIKit.TextDim, 18);
                Col(row, m.Kills.ToString(), 100f, -1, UIKit.Text, 18);
                Col(row, m.Deaths.ToString(), 100f, -1, UIKit.Text, 18);
                Col(row, (m.Kills / (float)Mathf.Max(1, m.Deaths)).ToString("0.00"), 100f, -1, UIKit.TextDim, 18);
            }
        }

        private void EnterResults(IOnlineService online)
        {
            _results = true;
            UIKit.Clear(_buttons);
            var cfg = online.Config;
            bool matchmade = cfg != null && cfg.playlist != Playlist.Custom;
            bool party = false;
            foreach (var m in online.Members) if (!m.IsBot && !m.Matched && !m.IsHost) party = true;

            if (online.IsHost && (!matchmade || party))
            {
                var back = UIKit.Button(_buttons, "BACK TO LOBBY", () => online.ReturnToLobby(), UIKit.ButtonStyle.Primary, 20, 50f);
                UIKit.Size(back, 50f, -1, 1f);
            }
            else if (!online.IsHost && !matchmade)
            {
                var wait = UIKit.Label(_buttons, "WAITING FOR THE HOST...", 16, UIKit.TextDim, TextAnchor.MiddleCenter);
                UIKit.Size(wait, -1, -1, 1f);
            }
            else
            {
                var again = UIKit.Button(_buttons, "FIND ANOTHER MATCH", () =>
                {
                    var playlist = cfg.playlist;
                    var choice = cfg.Clone();
                    choice.bots = 0;
                    online.Leave();
                    Vamp.UI.Menus.MenuController.PendingSearch = new Vamp.UI.Menus.PendingSearchRequest { Playlist = playlist, Config = choice };
                }, UIKit.ButtonStyle.Primary, 20, 50f);
                UIKit.Size(again, 50f, -1, 1f);
            }
            var leave = UIKit.Button(_buttons, matchmade ? "BACK TO MENU" : "LEAVE LOBBY", () => online.Leave(), UIKit.ButtonStyle.Ghost, 18, 50f);
            UIKit.Size(leave, 50f, 240f);
            AwardXp(online);
        }

        /// <summary>
        /// XP only in QUICK MATCH and RANKED, and only for kills on REAL players (bots never count).
        /// Weapon XP (camo unlocks) per weapon from those kills. RANKED: rank points + coins for the ranked shop.
        /// </summary>
        private void AwardXp(IOnlineService online)
        {
            if (_xpGiven || Game.Progression == null || Game.Progression.Profile == null) return;
            _xpGiven = true;
            var cfg = online.Config;
            OnlineMember me = null;
            var list = new List<OnlineMember>(online.Members);
            list.Sort((a, b) => b.Kills.CompareTo(a.Kills));
            int place = 1;
            for (int i = 0; i < list.Count; i++) if (list[i].IsLocal) { me = list[i]; place = i + 1; }
            if (me == null || cfg == null) return;
            if (!cfg.GivesXp)
            {
                _xpLine = "NO XP IN CUSTOM LOBBIES";
                return;
            }

            bool won, draw = false;
            if (cfg.IsTeamMode)
            {
                int[] s = online.TeamScores;
                int mine = Mathf.Clamp(me.Team, 0, 1);
                won = s[mine] > s[1 - mine];
                draw = s[mine] == s[1 - mine];
            }
            else won = place == 1 && (list.Count < 2 || list[0].Kills > list[1].Kills);
            bool realOpponent = false;
            foreach (var m in list)
                if (!m.IsBot && !m.IsLocal && (!cfg.IsTeamMode || m.Team != me.Team)) realOpponent = true;

            int humanKills = me.HumanKills; // counted by the host: kills on real players only
            var report = new MatchReport
            {
                mode = cfg.mode.ToString(),
                map = cfg.mapId,
                completed = true,
                won = won && realOpponent,        // beating only bots isn't a win for XP
                placement = place,
                kills = humanKills,               // bot kills give nothing
                headshots = Mathf.Min(NetMatchTally.TotalHeadshots, humanKills),
                deaths = me.Deaths,
                score = humanKills * 100,
                durationSeconds = Mathf.Max(60f, cfg.timeLimitMinutes * 60f)
            };
            var xp = Game.Progression.ApplyMatch(report);
            var parts = new List<string>();
            if (xp != null) parts.Add("+" + xp.Total + " XP");

            // Weapon levels → camos
            int camos = 0;
            foreach (var kv in NetMatchTally.Kills)
            {
                int heads;
                NetMatchTally.Headshots.TryGetValue(kv.Key, out heads);
                camos += Game.Progression.AddWeaponXp(kv.Key, kv.Value, heads).Count;
            }
            if (NetMatchTally.TotalKills > 0) parts.Add("+" + (NetMatchTally.TotalKills * ProgressionService.WeaponXpPerKill) + " WEAPON XP");
            if (camos > 0) parts.Add(camos + " NEW CAMO" + (camos > 1 ? "S" : ""));

            if (cfg.playlist == Playlist.Ranked)
            {
                var r = Game.Progression.ApplyRanked(won, draw, humanKills);
                parts.Add((r.RpDelta >= 0 ? "+" : "") + r.RpDelta + " RP (" + r.NewTier + " " + r.NewRp + ")");
                parts.Add("+" + r.CoinsEarned + " COINS");
                if (Game.Leaderboard != null) Game.Leaderboard.Submit(r.NewRp);
            }
            Game.Progression.Save();
            _xpLine = string.Join("  ·  ", parts.ToArray());
            if (xp != null && xp.LevelsGained > 0 && Game.Notifications != null)
                Game.Notifications.Push(NotificationKind.Info, "LEVEL UP", "LEVEL " + xp.NewLevel);
        }
    }
}
