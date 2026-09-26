using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Social;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// FRIENDS · REQUESTS · RECENT PLAYERS · BLOCKED · ADD FRIEND.
    /// Actions per friend: invite, join, view profile, message, remove, block, report.
    /// Offline: lists are local and online-only actions explain why they're unavailable.
    /// </summary>
    public sealed class FriendsScreen : MenuScreen
    {
        private int _tab;
        private RectTransform _content;
        private static readonly string[] TabNames = { "FRIENDS", "REQUESTS", "RECENT PLAYERS", "BLOCKED", "ADD FRIEND" };

        public FriendsScreen(int tab) { _tab = tab; }

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "FRIENDS", Game.Social != null && Game.Social.IsOnline ? "YOUR TAG: " + Game.Social.FriendTag : "NOT CONNECTED TO ONLINE SERVICES", 1000f);
            UIKit.Tabs(col, TabNames, _tab, i => { _tab = i; Refresh(); }, 15);
            ScrollRect scroll;
            _content = UIKit.ScrollList(col, out scroll, 6f);
            UIKit.Size(scroll, -1, -1, -1, 1f);
            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);
            Refresh();
        }

        public override void OnShow()
        {
            if (Game.Social != null) Game.Social.Changed += Refresh;
        }

        public override void OnHide()
        {
            if (Game.Social != null) Game.Social.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_content == null) return;
            UIKit.Clear(_content);
            var s = Game.Social;
            if (s == null) return;
            switch (_tab)
            {
                case 0:
                    if (s.Friends.Count == 0) Empty(s.IsOnline ? "NO FRIENDS YET. USE ADD FRIEND AND TYPE A FRIEND TAG (NAME#1234)." : "NO FRIENDS YET.");
                    foreach (var f in s.Friends) FriendRow(f);
                    break;
                case 1:
                    if (s.Requests.Count == 0) Empty("NO PENDING FRIEND REQUESTS.");
                    foreach (var r in s.Requests) RequestRow(r);
                    break;
                case 2:
                    if (s.RecentPlayers.Count == 0) Empty("PLAYERS FROM YOUR RECENT ONLINE MATCHES WILL APPEAR HERE.");
                    foreach (var r in s.RecentPlayers) RecentRow(r);
                    break;
                case 3:
                    if (s.BlockedNames.Count == 0) Empty("YOU HAVEN'T BLOCKED ANYONE. BLOCKED PLAYERS CAN'T FRIEND REQUEST, INVITE OR MESSAGE YOU.");
                    foreach (var name in s.BlockedNames)
                    {
                        var row = UIKit.Row(_content, 48f, 10f);
                        var l = UIKit.Label(row, name, 18, UIKit.Text);
                        UIKit.Size(l, -1, -1, 1f);
                        string n = name;
                        Action(row, "UNBLOCK", () => Result(Game.Social.Unblock(n)));
                    }
                    break;
                default:
                    AddFriend();
                    break;
            }
        }

        private void Empty(string text)
        {
            var l = UIKit.Label(_content, text, 15, UIKit.TextFaint, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIKit.Size(l, 60f);
        }

        private void FriendRow(FriendInfo f)
        {
            var row = UIKit.Row(_content, 60f, 8f);
            var id = IdentityViews.Row(row, f.icon, null, f.level, 0, f.username, f.status + " · " + f.activity + (string.IsNullOrEmpty(f.map) ? "" : " · " + f.mode + " · " + f.map));
            UIKit.Size(id, 56f, -1, 1f);
            string aid = f.account_id, name = f.username;
            Action(row, "INVITE", () => Result(Game.Social.InviteToParty(aid)));
            Action(row, "JOIN", () => Result(Game.Social.JoinFriend(aid)));
            Action(row, "MESSAGE", () => Result(SocialResult.Fail("CHAT NEEDS ONLINE SERVICES")));
            Action(row, "REMOVE", () => UIKit.Modal(Host.ModalRoot, "REMOVE FRIEND?", name, ("REMOVE", () => Result(Game.Social.RemoveFriend(aid)), UIKit.ButtonStyle.Danger), ("CANCEL", null, UIKit.ButtonStyle.Box)));
            Action(row, "BLOCK", () => Result(Game.Social.Block(aid, name)));
            Action(row, "REPORT", () => Result(Game.Social.Report(aid, "player report")));
        }

        private void RequestRow(FriendRequestInfo r)
        {
            var row = UIKit.Row(_content, 60f, 8f);
            var id = IdentityViews.Row(row, r.icon, null, r.level, 0, r.username, r.outgoing ? "SENT" : "WANTS TO BE FRIENDS");
            UIKit.Size(id, 56f, -1, 1f);
            string aid = r.from_account_id, name = r.username;
            if (!r.outgoing) Action(row, "ACCEPT", () => Result(Game.Social.AcceptRequest(aid)));
            Action(row, "DECLINE", () => Result(Game.Social.DeclineRequest(aid)));
            Action(row, "BLOCK", () => Result(Game.Social.Block(aid, name)));
            Action(row, "REPORT", () => Result(Game.Social.Report(aid, "request")));
        }

        private void RecentRow(RecentPlayerInfo r)
        {
            var row = UIKit.Row(_content, 60f, 8f);
            var id = IdentityViews.Row(row, null, null, r.level, 0, r.username, r.mode + " · " + r.map);
            UIKit.Size(id, 56f, -1, 1f);
            string aid = r.account_id, name = r.username;
            Action(row, "ADD FRIEND", () => Result(Game.Social.SendFriendRequest(aid)));
            Action(row, "INVITE", () => Result(Game.Social.InviteToParty(aid)));
            Action(row, "BLOCK", () => Result(Game.Social.Block(aid, name)));
            Action(row, "REPORT", () => Result(Game.Social.Report(aid, "recent")));
        }

        private void AddFriend()
        {
            UIKit.Heading(_content, "ADD FRIEND", 30);
            var s = Game.Social;
            if (s != null && s.IsOnline && !string.IsNullOrEmpty(s.FriendTag))
            {
                var tagRow = UIKit.Row(_content, 40f, 10f);
                var tag = UIKit.Label(tagRow, "YOUR FRIEND TAG:  <color=#C8102E>" + s.FriendTag + "</color>", 20, UIKit.Text);
                tag.supportRichText = true;
                UIKit.Size(tag, -1, -1, 1f);
                string t = s.FriendTag;
                var copy = UIKit.Button(tagRow, "COPY", () => { GUIUtility.systemCopyBuffer = t; Toast("COPIED", t); }, UIKit.ButtonStyle.Box, 14, 36f);
                UIKit.Size(copy, 36f, 110f);
                UIKit.Caption(_content, "SHARE YOUR TAG. TO ADD SOMEONE, TYPE THEIR FULL TAG INCLUDING THE # NUMBER (E.G. VAMPP#1234).", 13);
            }
            else UIKit.Caption(_content, "SEARCH BY EXACT USERNAME. ONLY PUBLIC PROFILE INFORMATION IS EVER SHOWN.", 13);
            var row = UIKit.Row(_content, 46f, 10f);
            var field = UIKit.Input(row, s != null && s.IsOnline ? "Friend tag, e.g. Name#1234" : "Search username...", false, 40);
            UIKit.Size(field, 46f, -1, 1f);
            var results = UIKit.Column(_content, 6f, "Results");
            System.Action search = () =>
            {
                UIKit.Clear(results);
                Game.Social.SearchUsername(field.text, (res, list) =>
                {
                    if (!res.Success)
                    {
                        UIKit.Size(UIKit.Label(results, res.Message, 15, UIKit.Red), 30f);
                        return;
                    }
                    if (list.Count == 0) UIKit.Size(UIKit.Label(results, "NO PLAYER FOUND", 15, UIKit.TextDim), 30f);
                    foreach (var f in list)
                    {
                        var r = UIKit.Row(results, 60f, 8f);
                        UIKit.Size(IdentityViews.Row(r, f.icon, null, f.level, 0, f.username, ""), 56f, -1, 1f);
                        string aid = f.account_id;
                        Action(r, "SEND REQUEST", () => Result(Game.Social.SendFriendRequest(aid)));
                    }
                });
            };
            var b = UIKit.Button(row, "SEARCH", search, UIKit.ButtonStyle.Primary, 16, 46f);
            UIKit.Size(b, 46f, 140f);
            UIKit.OnEnter(field, search);
        }

        private static void Action(Transform row, string label, System.Action a)
        {
            var b = UIKit.Button(row, label, a, UIKit.ButtonStyle.Ghost, 12, 36f);
            UIKit.Size(b, 36f, label.Length * 9f + 26f);
        }

        private void Result(SocialResult r)
        {
            Toast(r.Success ? (string.IsNullOrEmpty(r.Message) ? "DONE" : r.Message) : "UNAVAILABLE", r.Success ? "" : r.Message, !r.Success);
        }
    }

    /// <summary>First launch: GRAPHICS DETECTED · RECOMMENDED PRESET · ESTIMATED TARGET · [APPLY] [CUSTOMIZE].</summary>
    public static class GraphicsDetectPopup
    {
        public static void Show(IScreenHost host)
        {
            var rec = Graphics.GraphicsDetector.Recommend();
            string label = Graphics.GraphicsDetector.PresetLabel(rec.Preset);
            UIKit.Modal(host.ModalRoot, "GRAPHICS DETECTED",
                rec.Gpu + "  ·  " + (rec.VramMb / 1024f).ToString("0.#") + " GB VRAM\n\nRECOMMENDED PRESET\n" + label + "\n\nESTIMATED TARGET\n" + rec.EstimatedTarget,
                ("APPLY", () =>
                {
                    var s = Game.Settings;
                    s.SetGraphicsPreset(rec.Preset);
                    s.Current.firstLaunchDone = true;
                    s.Apply();
                    s.Save();
                }, UIKit.ButtonStyle.Primary),
                ("CUSTOMIZE", () =>
                {
                    Game.Settings.Current.firstLaunchDone = true;
                    Game.Settings.Save();
                    host.Push(new SettingsScreen(3));
                }, UIKit.ButtonStyle.Box));
        }
    }
}
