using System;
using System.Collections.Generic;
using Vamp.Accounts;
using Vamp.Core;

namespace Vamp.Social
{
    /// <summary>
    /// OFFLINE social service. Keeps the local social profile (status, block list) and exposes the full friends /
    /// requests / recent / blocked API so the UI is complete - but anything that needs other players returns
    /// "ONLINE SERVICES UNAVAILABLE" until a backend implementation of <see cref="ISocialService"/> is plugged in.
    /// Username search never exposes private information: results only ever contain public profile fields.
    /// </summary>
    public sealed class OfflineSocialService : ISocialService
    {
        private const string Unavailable = "ONLINE SERVICES UNAVAILABLE (OFFLINE BUILD)";

        private SocialProfileData _data = new SocialProfileData();
        private string _accountId;

        public bool IsOnline { get { return false; } }
        public PresenceStatus Status { get { return _data.status; } }
        public IReadOnlyList<FriendInfo> Friends { get { return _data.friends; } }
        public IReadOnlyList<FriendRequestInfo> Requests { get { return _data.requests; } }
        public IReadOnlyList<RecentPlayerInfo> RecentPlayers { get { return _data.recent; } }
        public IReadOnlyList<string> BlockedNames { get { return _data.blocked_names; } }
        public int PendingRequestCount
        {
            get
            {
                int n = 0;
                foreach (var r in _data.requests) if (!r.outgoing) n++;
                return n;
            }
        }

        public event Action Changed;

        public OfflineSocialService(IAccountService accounts)
        {
            accounts.AccountCreated += a =>
            {
                _accountId = a.id;
                _data = new SocialProfileData { account_id = a.id };
                Save();
            };
            accounts.LoggedIn += a =>
            {
                _accountId = a.id;
                SocialProfileData d;
                _data = JsonStore.TryLoad(Path, out d) ? d : new SocialProfileData { account_id = a.id };
                Fix();
                Raise();
            };
            accounts.LoggedOut += () =>
            {
                _accountId = null;
                _data = new SocialProfileData();
                Raise();
            };
        }

        private string Path { get { return "accounts/" + _accountId + "/social.json"; } }

        private void Fix()
        {
            if (_data.friends == null) _data.friends = new List<FriendInfo>();
            if (_data.requests == null) _data.requests = new List<FriendRequestInfo>();
            if (_data.recent == null) _data.recent = new List<RecentPlayerInfo>();
            if (_data.blocked == null) _data.blocked = new List<string>();
            if (_data.blocked_names == null) _data.blocked_names = new List<string>();
        }

        private void Save()
        {
            if (!string.IsNullOrEmpty(_accountId)) JsonStore.Save(Path, _data);
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }

        public void SetStatus(PresenceStatus status)
        {
            _data.status = status;
            Save();
            Raise();
        }

        public void SetActivity(PresenceActivity activity, string mode, string map) { /* published to presence service when online */ }

        public void SearchUsername(string query, Action<SocialResult, List<FriendInfo>> callback)
        {
            if (callback == null) return;
            string err = AccountRules.Load().ValidateUsername(query);
            if (err != null && err != "THAT USERNAME IS RESERVED")
            {
                callback(SocialResult.Fail(err), new List<FriendInfo>());
                return;
            }
            callback(SocialResult.Fail(Unavailable), new List<FriendInfo>());
        }

        public SocialResult SendFriendRequest(string accountId) { return SocialResult.Fail(Unavailable); }
        public SocialResult AcceptRequest(string accountId) { return SocialResult.Fail(Unavailable); }

        public SocialResult DeclineRequest(string accountId)
        {
            int removed = _data.requests.RemoveAll(r => r.from_account_id == accountId);
            Save();
            Raise();
            return removed > 0 ? SocialResult.Ok() : SocialResult.Fail("REQUEST NOT FOUND");
        }

        public SocialResult RemoveFriend(string accountId)
        {
            int removed = _data.friends.RemoveAll(f => f.account_id == accountId);
            Save();
            Raise();
            return removed > 0 ? SocialResult.Ok("FRIEND REMOVED") : SocialResult.Fail("NOT ON YOUR FRIENDS LIST");
        }

        /// <summary>Blocking works offline: blocked players can't request, invite or message once online.</summary>
        public SocialResult Block(string accountId, string username)
        {
            if (string.IsNullOrEmpty(username)) return SocialResult.Fail("NO PLAYER");
            if (!_data.blocked_names.Contains(username))
            {
                _data.blocked.Add(accountId ?? username);
                _data.blocked_names.Add(username);
            }
            _data.friends.RemoveAll(f => f.account_id == accountId);
            _data.requests.RemoveAll(r => r.from_account_id == accountId);
            Save();
            Raise();
            return SocialResult.Ok(username + " BLOCKED");
        }

        public SocialResult Unblock(string username)
        {
            int idx = _data.blocked_names.IndexOf(username);
            if (idx < 0) return SocialResult.Fail("NOT BLOCKED");
            _data.blocked_names.RemoveAt(idx);
            if (idx < _data.blocked.Count) _data.blocked.RemoveAt(idx);
            Save();
            Raise();
            return SocialResult.Ok(username + " UNBLOCKED");
        }

        public SocialResult Report(string accountId, string reason) { return SocialResult.Fail(Unavailable); }
        public SocialResult InviteToParty(string accountId) { return SocialResult.Fail(Unavailable); }
        public SocialResult JoinFriend(string accountId) { return SocialResult.Fail(Unavailable); }

        public bool IsBlocked(string accountId)
        {
            return !string.IsNullOrEmpty(accountId) && _data.blocked.Contains(accountId);
        }
    }
}
