using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Notifications;
using UnityEngine;
using Vamp.Accounts;
using Vamp.Core;
using Vamp.Social;
using Vamp.UI.Menus;

namespace Vamp.Online
{
    /// <summary>What a player broadcasts to friends (Unity Friends presence activity).</summary>
    [Serializable]
    public sealed class VampActivity
    {
        public string state = "menu";   // menu | lobby | match
        public string code = "";        // lobby code while in a lobby (lets friends JOIN)
        public string mode = "";
        public string map = "";
        public int level = 1;
        public string icon = "icon_vamp_symbol";
    }

    /// <summary>Lobby invite sent friend → friend as a Friends message.</summary>
    [Serializable]
    public sealed class VampInvite
    {
        public string code = "";
        public string from = "";
        public string mode = "";
        public string map = "";
    }

    /// <summary>
    /// ONLINE friends (Unity Gaming Services "Friends"): friend tags (Name#1234), requests, accept / decline, remove,
    /// block, presence (online / in lobby / in match), lobby INVITE and JOIN. Signs in with Unity Authentication
    /// when a local VAMP account logs in. Falls back to the offline service (local block list etc.) while not
    /// connected, so the menus always work.
    /// </summary>
    public sealed class OnlineSocialService : ISocialService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            Game.SocialFactory = accounts => new OnlineSocialService(accounts);
        }

        private readonly OfflineSocialService _offline;
        private readonly IAccountService _accounts;
        private bool _online;
        private bool _connecting;
        private bool _hookedOnline;
        private bool _hookedFriends;
        private string _tag = "";
        private PresenceStatus _status = PresenceStatus.Online;
        private float _nextPresence;
        private string _lastPresenceKey = "";

        private readonly List<FriendInfo> _friends = new List<FriendInfo>();
        private readonly List<FriendRequestInfo> _requests = new List<FriendRequestInfo>();
        private readonly List<string> _blockedNames = new List<string>();
        private readonly Dictionary<string, string> _blockedIds = new Dictionary<string, string>(); // name → id
        private readonly Dictionary<string, VampActivity> _activities = new Dictionary<string, VampActivity>();
        private readonly List<VampInvite> _invites = new List<VampInvite>();

        public event Action Changed;

        public OnlineSocialService(IAccountService accounts)
        {
            _accounts = accounts;
            _offline = new OfflineSocialService(accounts);
            _offline.Changed += Raise;
            accounts.LoggedIn += a => Connect();
            accounts.LoggedOut += Disconnect;
        }

        // ------------------------------------------------------------------ ISocialService: state

        public bool IsOnline { get { return _online; } }
        public string FriendTag { get { return _tag; } }
        public PresenceStatus Status { get { return _online ? _status : _offline.Status; } }
        public IReadOnlyList<FriendInfo> Friends { get { return _online ? (IReadOnlyList<FriendInfo>)_friends : _offline.Friends; } }
        public IReadOnlyList<FriendRequestInfo> Requests { get { return _online ? (IReadOnlyList<FriendRequestInfo>)_requests : _offline.Requests; } }
        public IReadOnlyList<RecentPlayerInfo> RecentPlayers { get { return _offline.RecentPlayers; } }
        public IReadOnlyList<string> BlockedNames { get { return _online ? (IReadOnlyList<string>)_blockedNames : _offline.BlockedNames; } }

        public int PendingRequestCount
        {
            get
            {
                if (!_online) return _offline.PendingRequestCount;
                int n = 0;
                foreach (var r in _requests) if (!r.outgoing) n++;
                return n;
            }
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }

        private static void Say(string title, string msg = "")
        {
            if (Game.Notifications != null) Game.Notifications.Push(NotificationKind.Info, title, msg);
        }

        // ------------------------------------------------------------------ Connect

        private async void Connect()
        {
            if (_connecting) return;
            _connecting = true;
            try
            {
                // Remember-me logs in during startup, before every Unity service module has registered.
                // Initialising Unity Services that early leaves Friends out, so wait for the first frames.
                while (Time.frameCount < 5) await Task.Yield();
                await OnlineService.SignIn();
                // Friend tag = your VAMP username + the #number Unity adds to keep it unique.
                string current = AuthenticationService.Instance.PlayerName;
                string wanted = Sanitize(Game.Username);
                if (string.IsNullOrEmpty(current) || current.Split('#')[0] != wanted)
                    current = await AuthenticationService.Instance.UpdatePlayerNameAsync(wanted);
                _tag = current ?? wanted;

                await FriendsService.Instance.InitializeAsync();
                if (!_hookedFriends)
                {
                    _hookedFriends = true;
                    FriendsService.Instance.RelationshipAdded += e => Rebuild();
                    FriendsService.Instance.RelationshipDeleted += e => Rebuild();
                    FriendsService.Instance.PresenceUpdated += e => Rebuild();
                    FriendsService.Instance.MessageReceived += OnMessage;
                }
                _online = true;
                Rebuild();
                PushPresence(true);
                if (Game.Online != null && !_hookedOnline) { _hookedOnline = true; Game.Online.Changed += () => PushPresence(false); }
                Say("ONLINE", "FRIEND TAG: " + _tag);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VAMP] Friends unavailable: " + e.Message);
                _online = false;
            }
            finally
            {
                _connecting = false;
                Raise();
            }
        }

        private void Disconnect()
        {
            _online = false;
            _tag = "";
            _friends.Clear();
            _requests.Clear();
            _blockedNames.Clear();
            _blockedIds.Clear();
            try { if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(); } catch (Exception) { }
            Raise();
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s ?? "") if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "Player";
        }

        // ------------------------------------------------------------------ Mirror Unity Friends into VAMP's social model

        private void Rebuild()
        {
            if (!_online) return;
            var fs = FriendsService.Instance;
            _friends.Clear();
            foreach (var rel in fs.Friends)
            {
                var m = rel.Member;
                if (m == null) continue;
                var act = ReadActivity(m);
                var avail = m.Presence != null ? m.Presence.Availability : Availability.Unknown;
                _friends.Add(new FriendInfo
                {
                    account_id = m.Id,
                    username = NameOf(m),
                    level = act != null ? act.level : 1,
                    icon = act != null ? act.icon : "icon_vamp_symbol",
                    status = avail == Availability.Away ? PresenceStatus.Away : avail == Availability.Busy ? PresenceStatus.DoNotDisturb
                           : avail == Availability.Invisible ? PresenceStatus.Invisible : PresenceStatus.Online,
                    activity = avail == Availability.Offline || avail == Availability.Unknown || avail == Availability.Invisible ? PresenceActivity.Offline
                             : act == null ? PresenceActivity.MainMenu
                             : act.state == "match" ? PresenceActivity.InMatch : act.state == "lobby" ? PresenceActivity.InLobby : PresenceActivity.MainMenu,
                    mode = act != null ? act.mode : "",
                    map = act != null ? act.map : "",
                    party_id = act != null ? act.code : ""
                });
                if (act != null) _activities[m.Id] = act;
            }
            _requests.Clear();
            foreach (var inv in _invites)
                _requests.Add(new FriendRequestInfo { from_account_id = "invite:" + inv.code, username = inv.from + " INVITED YOU  ·  " + inv.mode + " · " + inv.map, level = 1, icon = "icon_vamp_symbol" });
            foreach (var rel in fs.IncomingFriendRequests)
                if (rel.Member != null) _requests.Add(new FriendRequestInfo { from_account_id = rel.Member.Id, username = NameOf(rel.Member), level = 1, icon = "icon_vamp_symbol" });
            foreach (var rel in fs.OutgoingFriendRequests)
                if (rel.Member != null) _requests.Add(new FriendRequestInfo { from_account_id = rel.Member.Id, username = NameOf(rel.Member), level = 1, icon = "icon_vamp_symbol", outgoing = true });
            _blockedNames.Clear();
            _blockedIds.Clear();
            foreach (var rel in fs.Blocks)
            {
                if (rel.Member == null) continue;
                string n = NameOf(rel.Member);
                _blockedNames.Add(n);
                _blockedIds[n] = rel.Member.Id;
            }
            Raise();
        }

        private static string NameOf(Member m)
        {
            return m.Profile != null && !string.IsNullOrEmpty(m.Profile.Name) ? m.Profile.Name : "PLAYER";
        }

        private static VampActivity ReadActivity(Member m)
        {
            try { return m.Presence != null ? m.Presence.GetActivity<VampActivity>() : null; }
            catch (Exception) { return null; }
        }

        // ------------------------------------------------------------------ Presence

        /// <summary>Tells friends what you're doing; includes the lobby code so they can JOIN.</summary>
        private async void PushPresence(bool force)
        {
            if (!_online) return;
            if (!force && Time.realtimeSinceStartup < _nextPresence) return;
            var o = Game.Online;
            var act = new VampActivity
            {
                state = o == null || !o.InSession ? "menu" : o.State == OnlineState.InMatch || o.State == OnlineState.Loading ? "match" : "lobby",
                code = o != null && o.InSession ? o.LobbyCode : "",
                mode = o != null && o.Config != null ? Match.MatchConfig.ModeName(o.Config.mode) : "",
                map = o != null && o.Config != null ? o.Config.mapId.ToUpperInvariant() : "",
                level = Game.Progression != null && Game.Progression.Profile != null ? Game.Progression.Profile.level : 1,
                icon = Game.Progression != null && Game.Progression.Profile != null ? Game.Progression.Profile.profile_icon : "icon_vamp_symbol"
            };
            string key = _status + JsonUtility.ToJson(act);
            if (!force && key == _lastPresenceKey) return;
            _lastPresenceKey = key;
            _nextPresence = Time.realtimeSinceStartup + 2f;
            var avail = _status == PresenceStatus.Away ? Availability.Away : _status == PresenceStatus.DoNotDisturb ? Availability.Busy
                      : _status == PresenceStatus.Invisible ? Availability.Invisible : Availability.Online;
            try { await FriendsService.Instance.SetPresenceAsync(avail, act); }
            catch (Exception e) { Debug.LogWarning("[VAMP] Presence: " + e.Message); }
        }

        public void SetStatus(PresenceStatus status)
        {
            _status = status;
            _offline.SetStatus(status);
            PushPresence(true);
            Raise();
        }

        public void SetActivity(PresenceActivity activity, string mode, string map) { PushPresence(false); }

        // ------------------------------------------------------------------ Requests

        public void SearchUsername(string query, Action<SocialResult, List<FriendInfo>> callback)
        {
            if (callback == null) return;
            if (!_online) { _offline.SearchUsername(query, callback); return; }
            query = (query ?? "").Trim();
            if (query.Length < 3) { callback(SocialResult.Fail("TYPE A FRIEND TAG, E.G. NAME#1234"), new List<FriendInfo>()); return; }
            if (!query.Contains("#")) { callback(SocialResult.Fail("INCLUDE THE # NUMBER - ASK YOUR FRIEND FOR THEIR TAG (FRIENDS ▸ ADD FRIEND)"), new List<FriendInfo>()); return; }
            if (string.Equals(query, _tag, StringComparison.OrdinalIgnoreCase)) { callback(SocialResult.Fail("THAT'S YOUR OWN TAG"), new List<FriendInfo>()); return; }
            // Unity Friends adds by exact tag (no public search), so the "result" is the tag itself.
            callback(SocialResult.Ok(), new List<FriendInfo> { new FriendInfo { account_id = "name:" + query, username = query, level = 1, icon = "icon_vamp_symbol" } });
        }

        public SocialResult SendFriendRequest(string accountId)
        {
            if (!_online) return _offline.SendFriendRequest(accountId);
            Run(async () =>
            {
                if (accountId.StartsWith("name:")) await FriendsService.Instance.AddFriendByNameAsync(accountId.Substring(5));
                else await FriendsService.Instance.AddFriendAsync(accountId);
            }, "FRIEND REQUEST SENT", "COULDN'T SEND REQUEST");
            return SocialResult.Ok("SENDING REQUEST...");
        }

        public SocialResult AcceptRequest(string accountId)
        {
            if (!_online) return _offline.AcceptRequest(accountId);
            if (accountId.StartsWith("invite:")) return JoinCode(accountId.Substring(7));
            Run(() => FriendsService.Instance.AddFriendAsync(accountId), "FRIEND ADDED", "COULDN'T ACCEPT");
            return SocialResult.Ok("ACCEPTING...");
        }

        public SocialResult DeclineRequest(string accountId)
        {
            if (!_online) return _offline.DeclineRequest(accountId);
            if (accountId.StartsWith("invite:"))
            {
                string code = accountId.Substring(7);
                _invites.RemoveAll(i => i.code == code);
                Rebuild();
                return SocialResult.Ok("INVITE DISMISSED");
            }
            bool outgoing = _requests.Exists(r => r.from_account_id == accountId && r.outgoing);
            Run(() => outgoing ? FriendsService.Instance.DeleteOutgoingFriendRequestAsync(accountId)
                               : FriendsService.Instance.DeleteIncomingFriendRequestAsync(accountId), "REQUEST REMOVED", "COULDN'T DECLINE");
            return SocialResult.Ok();
        }

        public SocialResult RemoveFriend(string accountId)
        {
            if (!_online) return _offline.RemoveFriend(accountId);
            Run(() => FriendsService.Instance.DeleteFriendAsync(accountId), "FRIEND REMOVED", "COULDN'T REMOVE");
            return SocialResult.Ok();
        }

        public SocialResult Block(string accountId, string username)
        {
            _offline.Block(accountId, username);
            if (!_online || string.IsNullOrEmpty(accountId) || accountId.Contains(":")) return SocialResult.Ok(username + " BLOCKED");
            Run(() => FriendsService.Instance.AddBlockAsync(accountId), username + " BLOCKED", "COULDN'T BLOCK");
            return SocialResult.Ok();
        }

        public SocialResult Unblock(string username)
        {
            _offline.Unblock(username);
            string id;
            if (!_online || !_blockedIds.TryGetValue(username, out id)) return SocialResult.Ok(username + " UNBLOCKED");
            Run(() => FriendsService.Instance.DeleteBlockAsync(id), username + " UNBLOCKED", "COULDN'T UNBLOCK");
            return SocialResult.Ok();
        }

        public SocialResult Report(string accountId, string reason)
        {
            return SocialResult.Fail("REPORTS AREN'T AVAILABLE YET - BLOCK THE PLAYER INSTEAD");
        }

        public bool IsBlocked(string accountId)
        {
            return _offline.IsBlocked(accountId) || _blockedIds.ContainsValue(accountId);
        }

        // ------------------------------------------------------------------ Invite / join

        public SocialResult InviteToParty(string accountId)
        {
            if (!_online) return SocialResult.Fail("NOT CONNECTED TO ONLINE SERVICES");
            var o = Game.Online;
            if (o == null || !o.InSession || string.IsNullOrEmpty(o.LobbyCode))
                return SocialResult.Fail("HOST A LOBBY FIRST: PLAY ▸ CUSTOM GAME ▸ HOST ONLINE LOBBY");
            var inv = new VampInvite
            {
                code = o.LobbyCode,
                from = _tag,
                mode = o.Config != null ? Match.MatchConfig.ModeName(o.Config.mode) : "",
                map = o.Config != null ? o.Config.mapId.ToUpperInvariant() : ""
            };
            Run(() => FriendsService.Instance.MessageAsync(accountId, inv), "INVITE SENT", "COULDN'T INVITE");
            return SocialResult.Ok("SENDING INVITE...");
        }

        public SocialResult JoinFriend(string accountId)
        {
            VampActivity act;
            if (!_activities.TryGetValue(accountId, out act) || string.IsNullOrEmpty(act.code))
                return SocialResult.Fail("THAT FRIEND ISN'T IN A LOBBY");
            if (act.state == "match") return SocialResult.Fail("THEY'RE IN A MATCH - JOIN WHEN IT ENDS");
            return JoinCode(act.code);
        }

        private SocialResult JoinCode(string code)
        {
            var o = Game.Online;
            if (o == null) return SocialResult.Fail("ONLINE PLAY UNAVAILABLE");
            if (o.InSession) return SocialResult.Fail("LEAVE YOUR CURRENT LOBBY FIRST");
            o.JoinByCode(code, (ok, msg) =>
            {
                if (ok)
                {
                    _invites.RemoveAll(i => i.code == code);
                    Rebuild();
                    if (MenuController.Instance != null) MenuController.Instance.Push(new OnlineLobbyScreen());
                }
                else Say("COULDN'T JOIN", msg);
            });
            return SocialResult.Ok("JOINING " + code + "...");
        }

        private void OnMessage(IMessageReceivedEvent e)
        {
            try
            {
                var inv = e.GetAs<VampInvite>();
                if (inv == null || string.IsNullOrEmpty(inv.code)) return;
                _invites.RemoveAll(i => i.code == inv.code);
                _invites.Add(inv);
                Say("LOBBY INVITE FROM " + inv.from, "OPEN FRIENDS ▸ REQUESTS AND PRESS ACCEPT TO JOIN");
                Rebuild();
            }
            catch (Exception ex) { Debug.LogWarning("[VAMP] Bad friend message: " + ex.Message); }
        }

        // ------------------------------------------------------------------ Helpers

        private async void Run(Func<Task> call, string ok, string fail)
        {
            try
            {
                await call();
                Say(ok);
                try { await FriendsService.Instance.ForceRelationshipsRefreshAsync(); } catch (Exception) { }
                Rebuild();
            }
            catch (Exception e)
            {
                string m = e.Message ?? "";
                if (m.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) m = "NO PLAYER WITH THAT TAG";
                Say(fail, m.Length > 120 ? m.Substring(0, 120).ToUpperInvariant() : m.ToUpperInvariant());
            }
        }
    }
}
