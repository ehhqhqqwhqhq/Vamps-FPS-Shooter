using System;
using System.Collections.Generic;
using Vamp.Match;

namespace Vamp.Core
{
    public enum OnlineState { Offline, Connecting, InLobby, Loading, InMatch, PostMatch }

    /// <summary>A public lobby as listed in the SERVER BROWSER.</summary>
    public sealed class OnlineLobbyInfo
    {
        public string Id;
        public string Name;
        public string Host;
        public string Mode;
        public string Map;
        public string Version;
        public int Players;
        public int MaxPlayers;
        public bool Locked;
    }

    /// <summary>A player in the current online lobby / match (replicated from the host).</summary>
    public sealed class OnlineMember
    {
        public ulong ClientId;
        public string Name;
        public int Level;
        public int Team;
        public bool Ready;
        public bool IsHost;
        public bool IsLocal;
        public int Kills;
        public int Deaths;
        public int Score;
        /// <summary>Filled-in bot (quick match backfill). Kills on bots never give XP or coins.</summary>
        public bool IsBot;
        /// <summary>Joined through matchmaking (not part of the host's party).</summary>
        public bool Matched;
        /// <summary>Kills on real players this match.</summary>
        public int HumanKills;
    }

    /// <summary>One row of the ranked leaderboard.</summary>
    public sealed class LeaderboardRow
    {
        public int Rank;
        public string Name;
        public int Points;
        public bool IsLocal;
    }

    /// <summary>Online ranked leaderboard (Unity Leaderboards). Set by the online assembly; null offline.</summary>
    public interface ILeaderboardService
    {
        void Submit(int rankPoints);
        /// <summary>ok, error, top rows, the local player's row (null if unranked).</summary>
        void Fetch(int count, Action<bool, string, List<LeaderboardRow>, LeaderboardRow> done);
    }

    /// <summary>
    /// Online multiplayer (player-hosted custom lobbies). Implemented by the Vamp.Online assembly
    /// (Netcode for GameObjects + Unity Relay / Sessions); the menus only ever talk to this interface.
    /// Game.Online is null when the online assembly isn't present.
    /// </summary>
    public interface IOnlineService
    {
        OnlineState State { get; }
        bool IsHost { get; }
        bool InSession { get; }
        string LobbyCode { get; }
        string LobbyName { get; }
        /// <summary>Lobby rules chosen by the host (replicated).</summary>
        MatchConfig Config { get; }
        IReadOnlyList<OnlineMember> Members { get; }
        /// <summary>Seconds left in the match (or -1 when there is no time limit).</summary>
        float TimeRemaining { get; }
        int[] TeamScores { get; }
        string LastWinner { get; }

        event Action Changed;
        /// <summary>Title, message.</summary>
        event Action<string, string> Notice;

        void Host(MatchConfig config, string lobbyName, Action<bool, string> done);
        void JoinByCode(string code, Action<bool, string> done);
        void JoinById(string lobbyId, Action<bool, string> done);
        void Browse(Action<bool, string, List<OnlineLobbyInfo>> done);

        void SetReady(bool ready);
        void SetTeam(int team);
        void SetConfig(MatchConfig config);
        void StartMatch();
        /// <summary>Host only: remove a player from the lobby / party.</summary>
        void Kick(ulong clientId);
        void ReturnToLobby();
        void Leave();

        // ---- Matchmaking (QUICK MATCH / RANKED with real players)
        bool Searching { get; }
        Playlist SearchPlaylist { get; }
        /// <summary>"SEARCHING...", "MATCH FOUND - WAITING FOR PLAYERS", ...</summary>
        string SearchStatus { get; }
        float SearchSeconds { get; }
        /// <summary>Host-replicated status line for party members ("SEARCHING FOR PLAYERS 3/8").</summary>
        string LobbyStatus { get; }
        /// <summary>Quick: one of <paramref name="choices"/> is played (empty slots filled with bots after 30 s).
        /// Ranked: 1V1/2V2/3V3 decided by how many players are searching - real players only.</summary>
        void FindMatch(Playlist playlist, List<MatchConfig> choices, Action<bool, string> done);
        void CancelSearch();
    }
}
