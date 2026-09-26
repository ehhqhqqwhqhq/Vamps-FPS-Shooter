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
        void ReturnToLobby();
        void Leave();
    }
}
