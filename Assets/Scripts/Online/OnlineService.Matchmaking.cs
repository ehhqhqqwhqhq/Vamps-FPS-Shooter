using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;
using Vamp.Core;
using Vamp.Maps;
using Vamp.Match;

namespace Vamp.Online
{
    /// <summary>
    /// QUICK MATCH / RANKED matchmaking on top of player-hosted sessions (no dedicated servers):
    ///  1. Look for an open lobby of the same queue + game version (and, for quick match, one of your modes) → join it.
    ///  2. None? Host one and wait for searchers to join it.
    ///     QUICK: starts when full, or after 30 s with the empty slots filled by bots.
    ///     RANKED: real players only - 1V1 / 2V2 / 3V3 depending on how many are searching (odd one out re-queues).
    ///  3. While alone, keep looking for an older waiting lobby and merge into it, so two searchers never wait apart.
    /// A party leader searching opens the party lobby to matchmaking instead (the party stays together).
    /// </summary>
    public sealed partial class OnlineService
    {
        public const float QuickFillSeconds = 30f;
        public const float RankedSettleSeconds = 20f;
        private const string ReasonRequeue = "REQUEUE";
        private const string ReasonMatchOver = "MATCH OVER";
        private const string TagQuick = "quick", TagRanked = "ranked", TagNone = "none";

        private sealed class SearchState
        {
            public Playlist Playlist;
            public List<MatchConfig> Choices;
            public float Started;
            public bool HostingSearch;     // our lobby is open to matchmaking
            public bool CreatedForSearch;  // we made the lobby just for this search (not a party)
            public bool JoinedAsClient;    // we joined someone else's lobby
            public bool Starting;
            public float NextMerge;
            public float RankedReadyAt = -1f;
            public MatchConfig HostConfig;
            public string Status = "SEARCHING...";
        }

        private SearchState _search;
        private DateTime _sessionCreatedUtc;
        private bool _mergeBusy;

        public bool Searching { get { return _search != null; } }
        public Playlist SearchPlaylist { get { return _search != null ? _search.Playlist : Playlist.Custom; } }
        public string SearchStatus { get { return _search != null ? _search.Status : ""; } }
        public float SearchSeconds { get { return _search != null ? Time.realtimeSinceStartup - _search.Started : 0f; } }
        public string LobbyStatus { get { return Net != null ? Net.Status : ""; } }

        private static string TagFor(Playlist p) { return p == Playlist.Ranked ? TagRanked : TagQuick; }

        // ------------------------------------------------------------------ Start / cancel

        public void FindMatch(Playlist playlist, List<MatchConfig> choices, Action<bool, string> done)
        {
            if (playlist == Playlist.Custom) { Done(done, false, "PICK QUICK MATCH OR RANKED"); return; }
            if (_search != null) { Done(done, false, "ALREADY SEARCHING"); return; }
            if (_busy) { Done(done, false, "PLEASE WAIT..."); return; }
            if (playlist == Playlist.Quick && (choices == null || choices.Count == 0)) { Done(done, false, "SELECT AT LEAST ONE MODE"); return; }

            _search = new SearchState
            {
                Playlist = playlist,
                Choices = choices != null ? new List<MatchConfig>(choices) : new List<MatchConfig>(),
                Started = Time.realtimeSinceStartup
            };

            if (InSession)
            {
                // Party: the leader opens the party lobby to matchmaking; everyone stays together.
                if (!IsHost) { _search = null; Done(done, false, "ONLY THE PARTY LEADER CAN SEARCH"); return; }
                if (State != OnlineState.InLobby) { _search = null; Done(done, false, "WAIT UNTIL THE PARTY IS BACK IN THE LOBBY"); return; }
                if (playlist == Playlist.Ranked && Net != null && Net.PartyCount > 3) { _search = null; Done(done, false, "RANKED PARTIES CAN HAVE UP TO 3 PLAYERS"); return; }
                BeginHostSearch(false);
                Raise();
                Done(done, true, "");
                return;
            }
            Raise();
            Done(done, true, "");
            SearchLoop();
        }

        public void CancelSearch()
        {
            var s = _search;
            if (s == null) return;
            _search = null;
            if (s.Starting) { Raise(); return; } // too late - the match is loading
            if (s.JoinedAsClient || s.CreatedForSearch) Leave();
            else if (s.HostingSearch) { UpdateQueueProperties(TagNone, null, false); if (Net != null) Net.ServerSetStatus(""); }
            Raise();
        }

        private void StopSearchState()
        {
            _search = null;
            Raise();
        }

        /// <summary>Kicked back to the queue (ranked odd one out / the lobby closed): clean up and search again.</summary>
        private async void Requeue()
        {
            var s = _search;
            var playlist = s != null ? s.Playlist : Playlist.Ranked;
            var choices = s != null ? s.Choices : null;
            _search = null;
            _leaving = true;
            await Cleanup();
            _leaving = false;
            Say("BACK IN THE QUEUE", "LOOKING FOR ANOTHER MATCH...");
            FindMatch(playlist, choices, null);
        }

        // ------------------------------------------------------------------ Search: find a lobby or host one

        private async void SearchLoop()
        {
            var s = _search;
            try
            {
                s.Status = "SEARCHING...";
                await SignIn();
                if (_search != s) return;

                var lobbies = await FindOpenLobbies(s, null);
                foreach (var info in lobbies)
                {
                    if (_search != s) return;
                    s.Status = "JOINING " + (info.Name ?? "LOBBY").ToUpperInvariant() + "...";
                    bool ok = await JoinAsync(info.Id, true);
                    if (_search != s) { if (ok) Leave(); return; }
                    if (!ok) continue;
                    s.JoinedAsClient = true;
                    s.Status = "MATCH FOUND - WAITING FOR PLAYERS";
                    Raise();
                    return;
                }

                // Nobody waiting: host a lobby and let the others find us.
                if (_search != s) return;
                s.Status = "CREATING LOBBY...";
                var cfg = FirstConfig(s);
                bool hosted = await HostAsync(cfg, (s.Playlist == Playlist.Ranked ? "RANKED · " : "QUICK MATCH · ") + Game.Username, TagFor(s.Playlist));
                if (_search != s) { if (hosted) Leave(); return; }
                if (!hosted) { _search = null; Say("MATCHMAKING", "COULD NOT CREATE A LOBBY - TRY AGAIN"); Raise(); return; }
                s.CreatedForSearch = true;
                BeginHostSearch(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Matchmaking failed: " + e);
                if (_search == s) { _search = null; Say("MATCHMAKING FAILED", Friendly(e)); Raise(); }
            }
        }

        private MatchConfig FirstConfig(SearchState s)
        {
            var rng = new System.Random();
            MatchConfig cfg;
            if (s.Playlist == Playlist.Ranked)
            {
                cfg = MatchConfig.Arena(3);
                cfg.maxPlayers = 6;
            }
            else cfg = s.Choices[rng.Next(s.Choices.Count)].Clone();
            cfg.playlist = s.Playlist;
            cfg.isPrivate = false;
            cfg.isCustom = false;
            cfg.mapId = MapCatalog.RandomBattleMap(rng, cfg.teamSize > 0);
            return cfg;
        }

        private void BeginHostSearch(bool created)
        {
            var s = _search;
            if (s == null || Net == null) return;
            MatchConfig cfg;
            if (created) cfg = Net.Config.Clone();
            else
            {
                cfg = FirstConfig(s);
                // The whole party has to fit.
                if (s.Playlist == Playlist.Quick)
                    foreach (var c in s.Choices) if (c.maxPlayers >= Net.PartyCount && (c.teamSize == 0 || c.teamSize >= Net.PartyCount)) { cfg = c.Clone(); break; }
                cfg.playlist = s.Playlist;
                cfg.isPrivate = false;
                cfg.isCustom = false;
                cfg.mapId = MapCatalog.RandomBattleMap(new System.Random(), cfg.teamSize > 0);
                Net.ServerSetConfig(cfg);
            }
            s.HostConfig = cfg;
            s.HostingSearch = true;
            s.Started = Time.realtimeSinceStartup;
            s.NextMerge = Time.realtimeSinceStartup + 6f;
            UpdateQueueProperties(TagFor(s.Playlist), cfg.ModeLabel, false);
            Raise();
        }

        private async void UpdateQueueProperties(string tag, string modeLabel, bool locked)
        {
            if (_session == null || !_session.IsHost) return;
            try
            {
                var host = _session.AsHost();
                host.SetProperty(KeyQueue, new SessionProperty(tag, VisibilityPropertyOptions.Public, PropertyIndex.String1));
                if (modeLabel != null) host.SetProperty(KeyQMode, new SessionProperty(modeLabel, VisibilityPropertyOptions.Public, PropertyIndex.String3));
                if (tag != TagNone) host.IsPrivate = false;
                host.IsLocked = locked;
                await host.SavePropertiesAsync();
            }
            catch (Exception e) { Debug.LogWarning("[VAMP] Matchmaking: updating lobby properties failed: " + e.Message); }
        }

        /// <summary>Open lobbies in the same queue and version, oldest first (quick: only your picked modes).</summary>
        private async Task<List<ISessionInfo>> FindOpenLobbies(SearchState s, DateTime? olderThan)
        {
            var result = new List<ISessionInfo>();
            var options = new QuerySessionsOptions
            {
                Count = 25,
                FilterOptions = new List<FilterOption>
                {
                    new FilterOption(FilterField.StringIndex1, TagFor(s.Playlist), FilterOperation.Equal),
                    new FilterOption(FilterField.StringIndex2, Application.version, FilterOperation.Equal),
                    new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater)
                }
            };
            var q = await MultiplayerService.Instance.QuerySessionsAsync(options);
            string ownId = _session != null ? _session.Id : null;
            var modes = new HashSet<string>();
            foreach (var c in s.Choices) { var x = c.Clone(); x.playlist = s.Playlist; modes.Add(x.ModeLabel); }
            foreach (var info in q.Sessions)
            {
                if (info.Id == ownId || info.IsLocked || info.HasPassword) continue;
                if (olderThan.HasValue && info.Created >= olderThan.Value) continue;
                if (s.Playlist == Playlist.Quick && info.Properties != null)
                {
                    SessionProperty p;
                    if (info.Properties.TryGetValue(KeyQMode, out p) && !modes.Contains(p.Value)) continue;
                }
                result.Add(info);
            }
            result.Sort((a, b) => a.Created.CompareTo(b.Created));
            return result;
        }

        private Task<bool> JoinAsync(string sessionId, bool matched)
        {
            var tcs = new TaskCompletionSource<bool>();
            Join(() => MultiplayerService.Instance.JoinSessionByIdAsync(sessionId), (ok, msg) => tcs.TrySetResult(ok), matched);
            return tcs.Task;
        }

        private Task<bool> HostAsync(MatchConfig cfg, string name, string tag)
        {
            var tcs = new TaskCompletionSource<bool>();
            HostSession(cfg, name, tag, (ok, msg) => tcs.TrySetResult(ok));
            return tcs.Task;
        }

        // ------------------------------------------------------------------ Per frame (OnlineRunner)

        internal void Tick()
        {
            var s = _search;
            if (s == null) return;
            var n = Net;

            if (s.JoinedAsClient)
            {
                if (n == null) return;
                if (n.State != NetState.Lobby) { _search = null; Raise(); return; } // the host started the match
                s.Status = "MATCH FOUND - " + (string.IsNullOrEmpty(n.Status) ? "WAITING FOR PLAYERS" : n.Status);
                return;
            }
            if (!s.HostingSearch || n == null || !IsHost || s.Starting) return;
            if (n.State != NetState.Lobby) { _search = null; Raise(); return; }

            float now = Time.realtimeSinceStartup;
            int humans = n.HumanCount;
            var cfg = s.HostConfig ?? n.Config;

            if (s.Playlist == Playlist.Quick)
            {
                int cap = Mathf.Max(2, cfg.maxPlayers);
                float left = Mathf.Max(0f, QuickFillSeconds - (now - s.Started));
                s.Status = "SEARCHING FOR PLAYERS " + humans + "/" + cap + "  ·  BOTS FILL IN " + Mathf.CeilToInt(left) + "s";
                n.ServerSetStatus(s.Status);
                if (humans >= cap || left <= 0f) StartSearchMatch(s, cfg, cap - Mathf.Min(humans, cap));
            }
            else
            {
                int party = Mathf.Max(1, n.PartyCount);
                if (humans >= 2 && s.RankedReadyAt < 0f) s.RankedReadyAt = now;
                if (humans < 2) s.RankedReadyAt = -1f;
                float left = s.RankedReadyAt < 0f ? -1f : Mathf.Max(0f, RankedSettleSeconds - (now - s.RankedReadyAt));
                s.Status = humans < 2 ? "SEARCHING FOR OPPONENTS..." : "PLAYERS FOUND " + humans + "  ·  STARTING IN " + Mathf.CeilToInt(left) + "s";
                n.ServerSetStatus(s.Status);
                int size = Mathf.Min(3, humans / 2);
                bool fits = size >= party; // the party must fit on one team
                if (fits && size >= 1 && (humans >= 6 || left == 0f)) StartRanked(s, n, size);
            }

            // Alone? Merge into an older waiting lobby so two searchers never wait apart.
            if (s.CreatedForSearch && humans <= 1 && now >= s.NextMerge && !_mergeBusy)
            {
                s.NextMerge = now + 8f;
                TryMerge(s);
            }
        }

        private async void TryMerge(SearchState s)
        {
            _mergeBusy = true;
            try
            {
                var older = await FindOpenLobbies(s, _sessionCreatedUtc);
                if (_search != s || older.Count == 0 || Net == null || Net.HumanCount > 1) return;
                string target = older[0].Id;
                s.Status = "FOUND A LOBBY - JOINING...";
                _search = null;          // don't let the host tick run while we switch lobbies
                _leaving = true;
                await Cleanup();
                _leaving = false;
                _search = s;
                s.HostingSearch = false;
                s.CreatedForSearch = false;
                bool ok = await JoinAsync(target, true);
                if (_search != s) { if (ok) Leave(); return; }
                if (ok) { s.JoinedAsClient = true; s.Status = "MATCH FOUND - WAITING FOR PLAYERS"; Raise(); }
                else SearchLoop();
            }
            catch (Exception e) { Debug.LogWarning("[VAMP] Matchmaking merge: " + e.Message); }
            finally { _mergeBusy = false; }
        }

        private void StartSearchMatch(SearchState s, MatchConfig cfg, int bots)
        {
            s.Starting = true;
            var c = cfg.Clone();
            c.playlist = s.Playlist;
            c.bots = s.Playlist == Playlist.Ranked ? 0 : bots;
            Net.ServerSetConfig(c);
            UpdateQueueProperties(TagNone, null, true);
            Net.ServerSetStatus("MATCH STARTING");
            Net.ServerStartMatch();
            _search = null;
            Raise();
        }

        private void StartRanked(SearchState s, NetSession n, int size)
        {
            // Odd one(s) out go back to the queue (newest matchmaking joiners first).
            int extra = n.HumanCount - size * 2;
            foreach (var id in n.MatchedNewestFirst())
            {
                if (extra <= 0) break;
                _nm.DisconnectClient(id, ReasonRequeue);
                extra--;
            }
            var cfg = MatchConfig.Arena(size);
            cfg.playlist = Playlist.Ranked;
            cfg.isPrivate = false;
            cfg.isCustom = false;
            cfg.bots = 0;
            cfg.mapId = MapCatalog.RandomBattleMap(new System.Random(), true);
            StartSearchMatch(s, cfg, 0);
        }
    }
}
