using System;
using System.Collections.Generic;
using Vamp.Accounts;
using Vamp.Core;
using Vamp.Progression;

namespace Vamp.Social
{
    /// <summary>
    /// Party of one while offline (you are the leader). Implements the full party API and lobby state machine
    /// so menus, ready state and the "return to party / rematch" loop already work; invites need online services.
    /// </summary>
    public sealed class LocalPartyService : IPartyService
    {
        private readonly List<PartyMember> _members = new List<PartyMember>();
        private readonly IAccountService _accounts;
        private readonly ProgressionService _progression;

        public IReadOnlyList<PartyMember> Members { get { return _members; } }
        public int MaxSize { get { return 4; } }
        public bool IsLeader { get { return true; } }
        public bool LocalReady { get; private set; }
        public LobbyState State { get; private set; }
        public event Action Changed;

        public LocalPartyService(IAccountService accounts, ProgressionService progression)
        {
            _accounts = accounts;
            _progression = progression;
            State = LobbyState.Offline;
            accounts.LoggedIn += a => { SetState(LobbyState.MainMenu); RefreshLocalMember(); };
            accounts.LoggedOut += () => { _members.Clear(); SetState(LobbyState.Offline); };
            progression.ProfileChanged += RefreshLocalMember;
        }

        public void RefreshLocalMember()
        {
            _members.Clear();
            var a = _accounts.Current;
            if (a != null)
            {
                var p = _progression.Profile;
                var title = p != null ? CosmeticCatalog.Get(p.title) : null;
                _members.Add(new PartyMember
                {
                    AccountId = a.id,
                    Username = a.username,
                    Level = p != null ? p.level : 1,
                    Icon = p != null ? p.profile_icon : "icon_vamp_symbol",
                    Title = p != null && p.title_visible && title != null ? title.Name : "",
                    Ready = LocalReady,
                    IsLeader = true,
                    IsLocal = true
                });
            }
            if (Changed != null) Changed();
        }

        public void SetReady(bool ready)
        {
            LocalReady = ready;
            RefreshLocalMember();
        }

        public void SetState(LobbyState state)
        {
            State = state;
            if (Changed != null) Changed();
        }

        public SocialResult Invite(string accountId) { return SocialResult.Fail("ONLINE SERVICES UNAVAILABLE (OFFLINE BUILD)"); }
        public SocialResult Kick(string accountId) { return SocialResult.Fail("NO OTHER MEMBERS"); }
        public SocialResult TransferLeadership(string accountId) { return SocialResult.Fail("NO OTHER MEMBERS"); }
        public SocialResult Disband() { SetReady(false); return SocialResult.Ok("PARTY RESET"); }
    }
}
