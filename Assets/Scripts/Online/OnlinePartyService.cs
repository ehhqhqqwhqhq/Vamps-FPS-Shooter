using System;
using System.Collections.Generic;
using UnityEngine;
using Vamp.Accounts;
using Vamp.Core;
using Vamp.Progression;
using Vamp.Social;

namespace Vamp.Online
{
    /// <summary>
    /// PARTY = your online lobby. The party leader hosts it (their PC runs the games); friends join through an
    /// INVITE (or the party's code). Because everyone is in the same session, the whole party loads into every
    /// match the leader starts and comes back to the party together afterwards.
    /// Not in a party → behaves like the offline party of one.
    /// </summary>
    public sealed class OnlinePartyService : IPartyService
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            Game.PartyFactory = (accounts, progression) => new OnlinePartyService(accounts, progression);
        }

        private readonly LocalPartyService _solo;
        private readonly List<PartyMember> _members = new List<PartyMember>();
        private bool _hooked;

        public event Action Changed;

        public OnlinePartyService(IAccountService accounts, ProgressionService progression)
        {
            _solo = new LocalPartyService(accounts, progression);
            _solo.Changed += Raise;
        }

        private IOnlineService O
        {
            get
            {
                var o = Game.Online;
                if (o != null && !_hooked) { _hooked = true; o.Changed += Raise; }
                return o;
            }
        }

        private bool InParty { get { return O != null && O.InSession; } }

        private void Raise()
        {
            if (Changed != null) Changed();
        }

        public IReadOnlyList<PartyMember> Members
        {
            get
            {
                if (!InParty) return _solo.Members;
                _members.Clear();
                foreach (var m in O.Members)
                {
                    _members.Add(new PartyMember
                    {
                        AccountId = m.ClientId.ToString(),
                        Username = m.Name,
                        Level = m.Level,
                        Icon = "icon_vamp_symbol",
                        Title = m.Team == 0 ? "TEAM A" : m.Team == 1 ? "TEAM B" : "",
                        Ready = m.Ready || m.IsHost,
                        IsLeader = m.IsHost,
                        IsLocal = m.IsLocal
                    });
                }
                return _members;
            }
        }

        public int MaxSize { get { return InParty && O.Config != null ? Mathf.Max(2, O.Config.maxPlayers) : 4; } }
        public bool IsLeader { get { return !InParty || O.IsHost; } }

        public bool LocalReady
        {
            get
            {
                if (!InParty) return _solo.LocalReady;
                foreach (var m in O.Members) if (m.IsLocal) return m.Ready || m.IsHost;
                return false;
            }
        }

        public LobbyState State
        {
            get
            {
                if (!InParty) return _solo.State;
                switch (O.State)
                {
                    case OnlineState.Loading: return LobbyState.MatchFound;
                    case OnlineState.InMatch: return LobbyState.InMatch;
                    case OnlineState.PostMatch: return LobbyState.PostMatch;
                    default: return LobbyState.Party;
                }
            }
        }

        public void SetReady(bool ready)
        {
            if (InParty) O.SetReady(ready);
            else _solo.SetReady(ready);
        }

        public void SetState(LobbyState state) { _solo.SetState(state); }

        /// <summary>Invites a friend (by their online account id). Creates the party first if needed.</summary>
        public SocialResult Invite(string accountId)
        {
            return Game.Social != null ? Game.Social.InviteToParty(accountId) : SocialResult.Fail("FRIENDS UNAVAILABLE");
        }

        public SocialResult Kick(string accountId)
        {
            if (!InParty) return SocialResult.Fail("NO OTHER MEMBERS");
            if (!O.IsHost) return SocialResult.Fail("ONLY THE PARTY LEADER CAN REMOVE PLAYERS");
            ulong id;
            if (!ulong.TryParse(accountId, out id)) return SocialResult.Fail("PLAYER NOT FOUND");
            O.Kick(id);
            return SocialResult.Ok("PLAYER REMOVED");
        }

        public SocialResult TransferLeadership(string accountId)
        {
            return SocialResult.Fail("THE PARTY LEADER HOSTS THE GAMES ON THEIR PC - THE NEW LEADER CAN CREATE A NEW PARTY");
        }

        public SocialResult Disband()
        {
            if (!InParty) return _solo.Disband();
            O.Leave();
            return SocialResult.Ok(O.IsHost ? "PARTY CLOSED" : "LEFT THE PARTY");
        }

        public void RefreshLocalMember()
        {
            _solo.RefreshLocalMember();
            Raise();
        }
    }
}
