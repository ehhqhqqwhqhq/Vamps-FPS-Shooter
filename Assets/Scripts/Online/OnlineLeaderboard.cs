using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using UnityEngine;
using Vamp.Core;

namespace Vamp.Online
{
    /// <summary>
    /// Ranked leaderboard on Unity Leaderboards. The leaderboard "vamp_ranked" is defined in
    /// Assets/Online/vamp_ranked.lb (deploy it once from Window ▸ Services ▸ Deployment).
    /// Scores are the player's rank points (keep-latest, so losing RP moves you down too).
    /// </summary>
    public sealed class OnlineLeaderboard : ILeaderboardService
    {
        public const string Id = "vamp_ranked";
        private int _lastSubmitted = -1;

        [Serializable]
        private sealed class Meta
        {
            public string name;
        }

        public async void Submit(int rankPoints)
        {
            if (rankPoints == _lastSubmitted) return;
            try
            {
                await OnlineService.SignIn();
                var options = new AddPlayerScoreOptions { Metadata = new Meta { name = Game.Username } };
                await LeaderboardsService.Instance.AddPlayerScoreAsync(Id, rankPoints, options);
                _lastSubmitted = rankPoints;
            }
            catch (Exception e) { Debug.LogWarning("[VAMP] Leaderboard submit failed: " + e.Message); }
        }

        public async void Fetch(int count, Action<bool, string, List<LeaderboardRow>, LeaderboardRow> done)
        {
            var rows = new List<LeaderboardRow>();
            LeaderboardRow mine = null;
            try
            {
                await OnlineService.SignIn();
                string me = AuthenticationService.Instance.PlayerId;
                var page = await LeaderboardsService.Instance.GetScoresAsync(Id, new GetScoresOptions { Limit = count, IncludeMetadata = true });
                foreach (var e in page.Results)
                {
                    var row = new LeaderboardRow
                    {
                        Rank = e.Rank + 1,
                        Name = NameOf(e.Metadata, e.PlayerName),
                        Points = (int)e.Score,
                        IsLocal = e.PlayerId == me
                    };
                    if (row.IsLocal) mine = row;
                    rows.Add(row);
                }
                if (mine == null)
                {
                    try
                    {
                        var e = await LeaderboardsService.Instance.GetPlayerScoreAsync(Id);
                        mine = new LeaderboardRow { Rank = e.Rank + 1, Name = Game.Username, Points = (int)e.Score, IsLocal = true };
                    }
                    catch (Exception) { /* not on the board yet */ }
                }
                if (done != null) done(true, "", rows, mine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Leaderboard fetch failed: " + e.Message);
                string msg = e.Message ?? "ERROR";
                if (msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 || msg.IndexOf("404", StringComparison.Ordinal) >= 0)
                    msg = "THE LEADERBOARD ISN'T SET UP YET (DEPLOY vamp_ranked.lb)";
                if (msg.Length > 120) msg = msg.Substring(0, 120);
                if (done != null) done(false, msg.ToUpperInvariant(), rows, null);
            }
        }

        private static string NameOf(string metadata, string fallback)
        {
            if (!string.IsNullOrEmpty(metadata))
            {
                try
                {
                    var m = JsonUtility.FromJson<Meta>(metadata);
                    if (m != null && !string.IsNullOrEmpty(m.name)) return m.name;
                }
                catch (Exception) { }
            }
            if (string.IsNullOrEmpty(fallback)) return "PLAYER";
            int hash = fallback.IndexOf('#');
            return hash > 0 ? fallback.Substring(0, hash) : fallback;
        }
    }
}
