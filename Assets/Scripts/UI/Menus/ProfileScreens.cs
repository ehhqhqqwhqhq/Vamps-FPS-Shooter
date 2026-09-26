using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Progression;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// PROFILE: player card, username, level/XP, prestige, K/D and headline stats, favourites.
    /// Account actions: LOG OUT, DELETE ACCOUNT (type your username to confirm).
    /// </summary>
    public sealed class ProfileScreen : MenuScreen
    {
        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "PROFILE", null, 1200f);
            var prog = Game.Progression;
            if (prog == null || !prog.IsLoaded)
            {
                UIKit.Caption(col, "NOT LOGGED IN");
                return;
            }
            var p = prog.Profile;
            var s = prog.Stats;

            var body = UIKit.Row(col, 560f, 40f, "Body");
            var left = UIKit.Column(body, 10f);
            UIKit.Size(left, -1, 280f);
            var cardRow = UIKit.Row(left, 330f, 0f);
            cardRow.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            IdentityViews.Card(cardRow, p, Game.Username, 270f, 330f);
            string xp = prog.IsMaxLevel ? "MAX LEVEL" : p.xp.ToString("N0") + " / " + prog.XpToNext.ToString("N0") + " XP";
            UIKit.Caption(left, "LEVEL " + p.level + (p.prestige_level > 0 ? "  ·  PRESTIGE " + CosmeticCatalog.ToRoman(p.prestige_level) : "") + "  ·  " + xp, 14);
            UIKit.Caption(left, "TOTAL XP  " + p.total_xp.ToString("N0"), 13);
            if (prog.CanPrestige)
            {
                UIKit.Button(left, "PRESTIGE", () =>
                    UIKit.Modal(Host.ModalRoot, "PRESTIGE?", "RETURN TO LEVEL 1 WITH A PRESTIGE STAR AND EXCLUSIVE COSMETICS. PRESTIGE IS OPTIONAL AND GIVES NO GAMEPLAY ADVANTAGE.",
                        ("PRESTIGE", () => { Game.Progression.Prestige(); Host.Replace(new ProfileScreen()); }, UIKit.ButtonStyle.Primary),
                        ("CANCEL", null, UIKit.ButtonStyle.Box)), UIKit.ButtonStyle.Primary, 18, 48f);
            }
            UIKit.Spacer(left, 0f, true);
            UIKit.Button(left, "LOG OUT", () =>
            {
                Game.Accounts.Logout();
                Host.ClearTo(new WelcomeScreen());
            }, UIKit.ButtonStyle.Box, 16, 44f);
            UIKit.Button(left, "DELETE ACCOUNT", () => DeleteAccountPopup(Host), UIKit.ButtonStyle.Danger, 16, 44f);

            var right = UIKit.Column(body, 6f);
            UIKit.Size(right, -1, -1, 1f);
            var stats = new List<KeyValuePair<string, string>>
            {
                KV("USERNAME", Game.Username),
                KV("KILLS", s.kills.ToString("N0")),
                KV("DEATHS", s.deaths.ToString("N0")),
                KV("K/D", s.KD.ToString("0.00")),
                KV("WINS", s.wins.ToString("N0")),
                KV("LOSSES", s.losses.ToString("N0")),
                KV("MATCHES", s.matches.ToString("N0")),
                KV("PLAYTIME", FormatTime(s.playtime)),
                KV("BEST SPEED", s.best_speed.ToString("0.0") + " M/S"),
                KV("LONGEST KILL", s.longest_kill.ToString("0") + " M"),
                KV("FAVORITE WEAPON", Game.Weapons != null && !string.IsNullOrEmpty(p.favorite_weapon) ? Game.Weapons.DisplayName(p.favorite_weapon) : "-"),
                KV("FAVORITE MAP", string.IsNullOrEmpty(p.favorite_map) ? "-" : Maps.MapCatalog.Get(p.favorite_map).DisplayName),
            };
            foreach (var kv in stats) StatRow(right, kv.Key, kv.Value);

            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);
        }

        public static KeyValuePair<string, string> KV(string k, string v) { return new KeyValuePair<string, string>(k, v); }

        public static void StatRow(Transform parent, string key, string value)
        {
            var row = UIKit.Row(parent, 34f, 10f);
            var k = UIKit.Label(row, key, 15, UIKit.TextDim);
            UIKit.Size(k, -1, 260f);
            var v = UIKit.Label(row, value, 19, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Size(v, -1, -1, 1f);
            UIKit.Divider(parent, new Color(1f, 1f, 1f, 0.05f));
        }

        public static string FormatTime(double seconds)
        {
            int total = Mathf.FloorToInt((float)seconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            return h > 0 ? h + "H " + m + "M" : m + "M " + (total % 60) + "S";
        }

        public static void DeleteAccountPopup(IScreenHost host)
        {
            var go = UIKit.Modal(host.ModalRoot, "DELETE ACCOUNT", "THIS PERMANENTLY DELETES YOUR ACCOUNT, PROGRESS AND COSMETICS.\nTYPE YOUR USERNAME TO DELETE YOUR ACCOUNT.");
            var panel = go.transform.Find("ModalPanel");
            var field = UIKit.Input(panel, Game.Username, false, 16);
            field.transform.SetSiblingIndex(panel.childCount - 3);
            var row = panel.Find("Buttons");
            var del = UIKit.Button(row, "DELETE", () =>
            {
                var r = Game.Accounts.DeleteAccount(field.text);
                if (!r.Success)
                {
                    Game.Notifications.Push(NotificationKind.Error, "NOT DELETED", r.Error);
                    return;
                }
                Object.Destroy(go);
                Game.Notifications.Push(NotificationKind.Info, "ACCOUNT DELETED", "");
                host.ClearTo(new WelcomeScreen());
            }, UIKit.ButtonStyle.Danger, 18, 48f);
            UIKit.Size(del, 48f, -1, 1f);
            var cancel = UIKit.Button(row, "CANCEL", () => Object.Destroy(go), UIKit.ButtonStyle.Box, 18, 48f);
            UIKit.Size(cancel, 48f, -1, 1f);
        }
    }

    /// <summary>
    /// CAREER: totals, K/D, headshots, best speed, longest kill, favourite weapons (ranked), favourite maps,
    /// movement statistics, daily/weekly challenges with progress, milestones.
    /// </summary>
    public sealed class CareerScreen : MenuScreen
    {
        private int _tab;
        private RectTransform _content;

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "CAREER", null, 1150f);
            UIKit.Tabs(col, new[] { "OVERVIEW", "WEAPONS & MAPS", "MOVEMENT", "CHALLENGES", "MILESTONES" }, 0, i => { _tab = i; Refresh(); }, 15);
            ScrollRect scroll;
            _content = UIKit.ScrollList(col, out scroll, 4f);
            UIKit.Size(scroll, -1, -1, -1, 1f);
            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);
            Refresh();
        }

        private void Refresh()
        {
            UIKit.Clear(_content);
            var prog = Game.Progression;
            if (prog == null || !prog.IsLoaded) return;
            var s = prog.Stats;
            switch (_tab)
            {
                case 0:
                    Row("TOTAL MATCHES", s.matches.ToString("N0"));
                    Row("WINS", s.wins.ToString("N0"));
                    Row("LOSSES", s.losses.ToString("N0"));
                    Row("WIN RATE", s.matches > 0 ? (100f * s.wins / s.matches).ToString("0") + "%" : "-");
                    Row("KILLS", s.kills.ToString("N0"));
                    Row("DEATHS", s.deaths.ToString("N0"));
                    Row("K/D", s.KD.ToString("0.00"));
                    Row("ASSISTS", s.assists.ToString("N0"));
                    Row("HEADSHOTS", s.headshots.ToString("N0") + (s.kills > 0 ? "  (" + (100f * s.headshots / Mathf.Max(1, s.kills)).ToString("0") + "%)" : ""));
                    Row("BEST SPEED", s.best_speed.ToString("0.0") + " M/S");
                    Row("LONGEST KILL", s.longest_kill.ToString("0") + " M");
                    Row("PLAYTIME", ProfileScreen.FormatTime(s.playtime));
                    break;
                case 1:
                    UIKit.Caption(_content, "FAVORITE WEAPONS", 14);
                    var weapons = new List<IdCount>(s.weapon_kills);
                    weapons.Sort((a, b) => b.count.CompareTo(a.count));
                    if (weapons.Count == 0) UIKit.Caption(_content, "PLAY A MATCH TO START TRACKING.", 13);
                    foreach (var w in weapons) Row(Game.Weapons != null ? Game.Weapons.DisplayName(w.id) : w.id, w.count + " KILLS");
                    UIKit.Spacer(_content, 10f);
                    UIKit.Caption(_content, "FAVORITE MAPS", 14);
                    var maps = new List<IdCount>(s.map_plays);
                    maps.Sort((a, b) => b.count.CompareTo(a.count));
                    foreach (var m in maps) Row(Maps.MapCatalog.Get(m.id).DisplayName, m.count + " MATCHES");
                    break;
                case 2:
                    Row("WALL RUNS", s.wall_runs.ToString("N0"));
                    Row("WALL JUMPS", s.wall_jumps.ToString("N0"));
                    Row("SLIDES", s.slides.ToString("N0"));
                    Row("DASHES", s.dashes.ToString("N0"));
                    Row("ROCKET JUMPS", s.rocket_jumps.ToString("N0"));
                    Row("AIRBORNE KILLS", s.airborne_kills.ToString("N0"));
                    Row("HIGH-SPEED KILLS", s.high_speed_kills.ToString("N0"));
                    Row("DISTANCE TRAVELLED", (s.distance_travelled / 1000.0).ToString("0.0") + " KM");
                    Row("BEST RACE TIME", s.best_race_time > 0f ? s.best_race_time.ToString("0.00") + " S" : "-");
                    Row("BEST SPEED", s.best_speed.ToString("0.0") + " M/S");
                    break;
                case 3:
                    foreach (var c in prog.ActiveChallenges())
                    {
                        var row = UIKit.Row(_content, 44f, 12f);
                        var tag = UIKit.Label(row, c.def.Weekly ? "WEEKLY" : "DAILY", 12, c.def.Weekly ? UIKit.Red : UIKit.TextDim);
                        UIKit.Size(tag, -1, 80f);
                        var name = UIKit.Label(row, c.def.Description, 17, c.state.completed ? UIKit.Good : UIKit.Text);
                        UIKit.Size(name, -1, -1, 1f);
                        var bar = UIKit.Image(row, "Bar", new Color(1f, 1f, 1f, 0.1f));
                        UIKit.Size(bar, 6f, 200f);
                        var fill = UIKit.Image(bar.transform, "Fill", c.state.completed ? UIKit.Good : UIKit.Red);
                        fill.rectTransform.anchorMin = Vector2.zero;
                        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)c.state.progress / c.def.Target), 1f);
                        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                        var p = UIKit.Label(row, c.state.progress + "/" + c.def.Target, 15, UIKit.TextDim, TextAnchor.MiddleRight);
                        UIKit.Size(p, -1, 90f);
                        var reward = UIKit.Label(row, "+" + c.def.XpReward + " XP" + (string.IsNullOrEmpty(c.def.RewardItem) ? "" : " + ICON"), 13, UIKit.TextDim, TextAnchor.MiddleRight);
                        UIKit.Size(reward, -1, 130f);
                    }
                    break;
                default:
                    if (s.milestones.Count == 0) UIKit.Caption(_content, "NO MILESTONES YET.", 13);
                    foreach (var m in s.milestones) Row("✓ " + m, "");
                    break;
            }
        }

        private void Row(string k, string v) { ProfileScreen.StatRow(_content, k, v); }
    }
}
