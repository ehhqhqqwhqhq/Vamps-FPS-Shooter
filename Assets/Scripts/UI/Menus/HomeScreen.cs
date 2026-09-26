using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Progression;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// Main menu: VAMP · PLAY · LOADOUT · CUSTOMIZE · PROFILE · SETTINGS · CAREER · QUIT,
    /// plus the lobby card(s) for your party and today's challenges.
    /// </summary>
    public sealed class HomeScreen : MenuScreen
    {
        private RectTransform _cards;
        private RectTransform _challenges;

        protected override void OnBuild(RectTransform root)
        {
            var col = UIKit.Node("Menu", root);
            col.anchorMin = new Vector2(0f, 0f); col.anchorMax = new Vector2(0f, 1f);
            col.pivot = new Vector2(0f, 0.5f);
            col.anchoredPosition = new Vector2(96f, 0f);
            col.sizeDelta = new Vector2(520f, -140f);
            UIKit.VList(col, 4f, 0, TextAnchor.MiddleLeft);

            var logo = UIKit.Label(col, "VAMP", 132, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Size(logo, 150f);
            var tag = UIKit.Label(col, "MOVE FAST. AIM FASTER. NEVER STOP MOVING.", 15, UIKit.Red, TextAnchor.MiddleLeft);
            UIKit.Size(tag, 26f);
            UIKit.Spacer(col, 26f);

            UIKit.Button(col, "PLAY", () => Host.Push(new PlayScreen()), UIKit.ButtonStyle.Menu, 40, 64f);
            UIKit.Button(col, "LOADOUT", () => Host.Push(new LoadoutScreen()), UIKit.ButtonStyle.Menu, 28, 50f);
            UIKit.Button(col, "CUSTOMIZE", () => Host.Push(new CustomizeScreen()), UIKit.ButtonStyle.Menu, 28, 50f);
            UIKit.Button(col, "SHOP", () => Host.Push(new ShopScreen()), UIKit.ButtonStyle.Menu, 28, 50f);
            UIKit.Button(col, "PROFILE", () => Host.Push(new ProfileScreen()), UIKit.ButtonStyle.Menu, 28, 50f);
            UIKit.Button(col, "SETTINGS", () => Host.Push(new SettingsScreen()), UIKit.ButtonStyle.Menu, 28, 50f);
            UIKit.Button(col, "CAREER", () => Host.Push(new CareerScreen()), UIKit.ButtonStyle.Menu, 28, 50f);
            UIKit.Button(col, "QUIT", () =>
                UIKit.Modal(Host.ModalRoot, "QUIT VAMP?", "", ("QUIT", WelcomeScreen.Quit, UIKit.ButtonStyle.Danger),
                                                                ("CANCEL", null, UIKit.ButtonStyle.Box)),
                UIKit.ButtonStyle.Menu, 28, 50f);

            // Lobby cards (your party) - center bottom
            _cards = UIKit.Node("LobbyCards", root);
            _cards.anchorMin = _cards.anchorMax = new Vector2(0.52f, 0f);
            _cards.pivot = new Vector2(0.5f, 0f);
            _cards.anchoredPosition = new Vector2(0f, 60f);
            _cards.sizeDelta = new Vector2(620f, 330f);
            var h = UIKit.HList(_cards, 16f, 0, TextAnchor.LowerCenter);
            h.childControlHeight = false;
            h.childControlWidth = false;

            // Daily challenges - under the top bar
            _challenges = UIKit.Node("Challenges", root);
            _challenges.anchorMin = _challenges.anchorMax = new Vector2(1f, 1f);
            _challenges.pivot = new Vector2(1f, 1f);
            _challenges.anchoredPosition = new Vector2(-380f, -130f);
            _challenges.sizeDelta = new Vector2(460f, 200f);
            UIKit.VList(_challenges, 4f);
        }

        public override void OnShow()
        {
            Refresh();
            if (Game.Progression != null) Game.Progression.ProfileChanged += Refresh;
            if (Game.Party != null) Game.Party.Changed += Refresh;
        }

        public override void OnHide()
        {
            if (Game.Progression != null) Game.Progression.ProfileChanged -= Refresh;
            if (Game.Party != null) Game.Party.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_cards == null) return;
            UIKit.Clear(_cards);
            if (Game.Party != null && Game.Progression != null)
            {
                foreach (var m in Game.Party.Members)
                    IdentityViews.Card(_cards, m.IsLocal ? Game.Progression.Profile : null, m.Username, 250f, 320f, m.Ready ? "READY" : "NOT READY", m.IsLocal);
            }

            UIKit.Clear(_challenges);
            if (Game.Progression == null || !Game.Progression.IsLoaded) return;
            UIKit.Size(UIKit.Label(_challenges, "DAILY CHALLENGES", 13, UIKit.TextDim), 20f);
            int shown = 0;
            foreach (var c in Game.Progression.ActiveChallenges())
            {
                if (c.def.Weekly || shown >= 3) continue;
                shown++;
                var row = UIKit.Row(_challenges, 22f, 8f);
                var name = UIKit.Label(row, (c.state.completed ? "✓ " : "") + c.def.Description, 14, c.state.completed ? UIKit.Good : UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Normal);
                UIKit.Size(name, -1, -1, 1f);
                var prog = UIKit.Label(row, c.state.progress + " / " + c.def.Target, 14, UIKit.TextDim, TextAnchor.MiddleRight);
                UIKit.Size(prog, -1, 90f);
            }
        }

        public override bool OnBack()
        {
            return true; // nothing behind the home screen
        }
    }
}
