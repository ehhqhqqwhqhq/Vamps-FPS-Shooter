using System;
using System.Collections.Generic;

namespace Vamp.Social
{
    public enum PresenceStatus { Online, Away, DoNotDisturb, Invisible }
    public enum PresenceActivity { Offline, MainMenu, InLobby, Searching, InMatch }

    /// <summary>Lobby state machine from the spec.</summary>
    public enum LobbyState { Offline, MainMenu, Party, Searching, MatchFound, PreMatch, InMatch, PostMatch, ReturnToParty }

    [Serializable]
    public sealed class FriendInfo
    {
        public string account_id;
        public string username;
        public int level;
        public string icon;
        public PresenceStatus status;
        public PresenceActivity activity;
        public string mode;
        public string map;
        public string party_id;
    }

    [Serializable]
    public sealed class FriendRequestInfo
    {
        public string from_account_id;
        public string username;
        public int level;
        public string icon;
        public string sent_at;
        public bool outgoing;
    }

    [Serializable]
    public sealed class RecentPlayerInfo
    {
        public string account_id;
        public string username;
        public int level;
        public string met_at;
        public string mode;
        public string map;
    }

    /// <summary>Local social profile (spec: create social profile on account creation).</summary>
    [Serializable]
    public sealed class SocialProfileData
    {
        public string account_id;
        public PresenceStatus status = PresenceStatus.Online;
        public List<FriendInfo> friends = new List<FriendInfo>();
        public List<FriendRequestInfo> requests = new List<FriendRequestInfo>();
        public List<RecentPlayerInfo> recent = new List<RecentPlayerInfo>();
        public List<string> blocked = new List<string>();
        public List<string> blocked_names = new List<string>();
    }

    public sealed class PartyMember
    {
        public string AccountId;
        public string Username;
        public int Level;
        public string Icon;
        public string Title;
        public bool Ready;
        public bool IsLeader;
        public bool IsLocal;
        public bool Talking;
    }

    public struct SocialResult
    {
        public bool Success;
        public string Message;
        public static SocialResult Ok(string msg = "") { return new SocialResult { Success = true, Message = msg }; }
        public static SocialResult Fail(string msg) { return new SocialResult { Success = false, Message = msg }; }
    }

    public interface ISocialService
    {
        bool IsOnline { get; }
        PresenceStatus Status { get; }
        IReadOnlyList<FriendInfo> Friends { get; }
        IReadOnlyList<FriendRequestInfo> Requests { get; }
        IReadOnlyList<RecentPlayerInfo> RecentPlayers { get; }
        IReadOnlyList<string> BlockedNames { get; }
        int PendingRequestCount { get; }
        event Action Changed;

        void SetStatus(PresenceStatus status);
        void SetActivity(PresenceActivity activity, string mode, string map);
        void SearchUsername(string query, Action<SocialResult, List<FriendInfo>> callback);
        SocialResult SendFriendRequest(string accountId);
        SocialResult AcceptRequest(string accountId);
        SocialResult DeclineRequest(string accountId);
        SocialResult RemoveFriend(string accountId);
        SocialResult Block(string accountId, string username);
        SocialResult Unblock(string username);
        SocialResult Report(string accountId, string reason);
        SocialResult InviteToParty(string accountId);
        SocialResult JoinFriend(string accountId);
        bool IsBlocked(string accountId);
    }

    public interface IPartyService
    {
        IReadOnlyList<PartyMember> Members { get; }
        int MaxSize { get; }
        bool IsLeader { get; }
        bool LocalReady { get; }
        LobbyState State { get; }
        event Action Changed;
        void SetReady(bool ready);
        void SetState(LobbyState state);
        SocialResult Invite(string accountId);
        SocialResult Kick(string accountId);
        SocialResult TransferLeadership(string accountId);
        SocialResult Disband();
        void RefreshLocalMember();
    }
}
