using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Social;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// Persistent right-side social panel: status, party (with ready state), friends grouped by
    /// ONLINE / IN LOBBY / IN MATCH / AWAY / OFFLINE, pending requests badge and quick actions.
    /// </summary>
    public sealed class SocialPanel
    {
        private readonly RectTransform _root;
        private readonly RectTransform _party;
        private readonly RectTransform _friends;
        private readonly Text _requestsBadge;
        private readonly Text _mode;
        private readonly OptionSelector _status;
        private readonly MenuController _menu;

        private static readonly string[] StatusNames = { "ONLINE", "AWAY", "DO NOT DISTURB", "INVISIBLE" };

        public SocialPanel(Transform canvas, MenuController menu)
        {
            _menu = menu;
            var panel = UIKit.Panel(canvas, "SocialPanel", new Color(0.03f, 0.03f, 0.035f, 0.85f));
            _root = panel.rectTransform;
            _root.anchorMin = new Vector2(1f, 0f);
            _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(1f, 0.5f);
            _root.offsetMin = new Vector2(-340f, 28f);
            _root.offsetMax = new Vector2(-28f, -28f);
            UIKit.VList(_root, 8f, 16);

            var head = UIKit.Row(_root, 30f, 8f);
            UIKit.Label(head, "SOCIAL", 20, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Spacer(head, 0f, true);
            _mode = UIKit.Label(head, "OFFLINE MODE", 12, UIKit.Red, TextAnchor.MiddleRight);
            UIKit.Size(_mode, -1, 110f);

            _status = UIKit.Selector(_root, "STATUS", StatusNames, 0, i =>
            {
                if (Game.Social != null) Game.Social.SetStatus((PresenceStatus)i);
            }, 80f);

            UIKit.Divider(_root);
            UIKit.Caption(_root, "PARTY", 13);
            _party = UIKit.Column(_root, 6f, "Party");

            var readyRow = UIKit.Row(_root, 40f, 8f);
            var ready = UIKit.Button(readyRow, "READY", () =>
            {
                if (Game.Party == null) return;
                Game.Party.SetReady(!Game.Party.LocalReady);
            }, UIKit.ButtonStyle.Ghost, 15, 38f);
            UIKit.Size(ready, 38f, -1, 1f);
            var invite = UIKit.Button(readyRow, "INVITE", () => menu.Push(new FriendsScreen(0)), UIKit.ButtonStyle.Ghost, 15, 38f);
            UIKit.Size(invite, 38f, -1, 1f);

            UIKit.Divider(_root);
            var fhead = UIKit.Row(_root, 26f, 6f);
            UIKit.Label(fhead, "FRIENDS", 13, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIKit.Spacer(fhead, 0f, true);
            _requestsBadge = UIKit.Label(fhead, "", 13, UIKit.Red, TextAnchor.MiddleRight);
            UIKit.Size(_requestsBadge, -1, 120f);

            ScrollRect scroll;
            _friends = UIKit.ScrollList(_root, out scroll, 4f);
            UIKit.Size(scroll, -1, -1, -1, 1f);

            var actions = UIKit.Row(_root, 40f, 8f);
            var friendsBtn = UIKit.Button(actions, "FRIENDS", () => menu.Push(new FriendsScreen(0)), UIKit.ButtonStyle.Box, 15, 38f);
            UIKit.Size(friendsBtn, 38f, -1, 1f);
            var add = UIKit.Button(actions, "ADD FRIEND", () => menu.Push(new FriendsScreen(4)), UIKit.ButtonStyle.Box, 15, 38f);
            UIKit.Size(add, 38f, -1, 1f);

            if (Game.Social != null) Game.Social.Changed += Refresh;
            if (Game.Party != null) Game.Party.Changed += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            if (Game.Social != null) Game.Social.Changed -= Refresh;
            if (Game.Party != null) Game.Party.Changed -= Refresh;
        }

        public void SetVisible(bool v)
        {
            if (_root.gameObject.activeSelf != v) _root.gameObject.SetActive(v);
        }

        public void Refresh()
        {
            if (_root == null) return;
            if (Game.Social != null) _status.SetIndexWithoutNotify((int)Game.Social.Status);

            UIKit.Clear(_party);
            if (Game.Party != null)
            {
                foreach (var m in Game.Party.Members)
                {
                    var row = IdentityViews.Row(_party, m.Icon, Game.Progression != null && Game.Progression.Profile != null ? Game.Progression.Profile.icon_frame : null,
                        m.Level, Game.Progression != null && Game.Progression.Profile != null ? Game.Progression.Profile.prestige_level : 0,
                        m.Username + (m.IsLeader ? "  ♛" : ""), m.Ready ? "READY" : "NOT READY", m.Ready ? UIKit.Good : UIKit.TextFaint);
                    UIKit.Size(row, 56f);
                }
                int free = Game.Party.MaxSize - Game.Party.Members.Count;
                if (free > 0) UIKit.Size(UIKit.Label(_party, free + " OPEN SLOT" + (free > 1 ? "S" : ""), 13, UIKit.TextFaint), 20f);
            }

            UIKit.Clear(_friends);
            var social = Game.Social;
            if (social == null) return;
            if (_mode != null)
            {
                _mode.text = social.IsOnline ? "ONLINE" : "OFFLINE MODE";
                _mode.color = social.IsOnline ? UIKit.Good : UIKit.Red;
            }
            int pending = social.PendingRequestCount;
            _requestsBadge.text = pending > 0 ? pending + " REQUEST" + (pending > 1 ? "S" : "") : "";

            if (social.Friends.Count == 0)
            {
                var msg = UIKit.Label(_friends, social.IsOnline ? "NO FRIENDS YET.\nADD FRIEND ▸ TYPE THEIR TAG (NAME#1234)."
                                                                : "CONNECTING TO ONLINE SERVICES...\nFRIENDS NEED AN INTERNET CONNECTION.",
                    13, UIKit.TextFaint, TextAnchor.UpperLeft, FontStyle.Normal);
                UIKit.Size(msg, 70f);
                return;
            }

            var groups = new List<KeyValuePair<string, List<FriendInfo>>>
            {
                new KeyValuePair<string, List<FriendInfo>>("IN LOBBY", new List<FriendInfo>()),
                new KeyValuePair<string, List<FriendInfo>>("IN MATCH", new List<FriendInfo>()),
                new KeyValuePair<string, List<FriendInfo>>("ONLINE", new List<FriendInfo>()),
                new KeyValuePair<string, List<FriendInfo>>("AWAY", new List<FriendInfo>()),
                new KeyValuePair<string, List<FriendInfo>>("OFFLINE", new List<FriendInfo>()),
            };
            foreach (var f in social.Friends)
            {
                int g = f.activity == PresenceActivity.Offline || f.status == PresenceStatus.Invisible ? 4
                      : f.activity == PresenceActivity.InLobby ? 0 : f.activity == PresenceActivity.InMatch ? 1
                      : f.status == PresenceStatus.Away || f.status == PresenceStatus.DoNotDisturb ? 3 : 2;
                groups[g].Value.Add(f);
            }
            foreach (var g in groups)
            {
                if (g.Value.Count == 0) continue;
                UIKit.Size(UIKit.Label(_friends, g.Key + " — " + g.Value.Count, 12, UIKit.TextDim), 18f);
                foreach (var f in g.Value)
                    UIKit.Size(IdentityViews.Row(_friends, f.icon, null, f.level, 0, f.username, g.Key), 56f);
            }
        }
    }
}
