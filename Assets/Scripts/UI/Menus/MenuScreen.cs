using UnityEngine;

namespace Vamp.UI.Menus
{
    /// <summary>Anything that can host menu screens (main menu, in-match pause menu).</summary>
    public interface IScreenHost
    {
        RectTransform ContentRoot { get; }
        Transform ModalRoot { get; }
        void Push(MenuScreen screen);
        void Replace(MenuScreen screen);
        void Back();
        void ClearTo(MenuScreen screen);
    }

    /// <summary>
    /// One menu page. Screens build their own UI in <see cref="OnBuild"/> and talk to game services
    /// (Game.Settings, Game.Accounts ...) - never directly to gameplay objects.
    /// </summary>
    public abstract class MenuScreen
    {
        public RectTransform Root { get; private set; }
        protected IScreenHost Host { get; private set; }

        /// <summary>Show the persistent top bar (player card / XP / social) with this screen.</summary>
        public virtual bool ShowTopBar { get { return true; } }

        public void Build(IScreenHost host, RectTransform parent)
        {
            Host = host;
            Root = UIKit.StretchNode(GetType().Name, parent);
            OnBuild(Root);
        }

        protected abstract void OnBuild(RectTransform root);

        public virtual void OnShow() { }
        public virtual void OnHide() { }
        public virtual void Tick(float dt) { }

        /// <summary>Return true if the screen consumed Back/Esc itself.</summary>
        public virtual bool OnBack() { return false; }

        public void Destroy()
        {
            OnHide();
            if (Root != null) Object.Destroy(Root.gameObject);
        }

        /// <summary>Standard page layout: big title at top-left, content column below.</summary>
        protected RectTransform Page(RectTransform root, string title, string subtitle = null, float width = 900f, float left = 96f)
        {
            var col = UIKit.Node("Page", root);
            col.anchorMin = new Vector2(0f, 0f);
            col.anchorMax = new Vector2(0f, 1f);
            col.pivot = new Vector2(0f, 1f);
            col.anchoredPosition = new Vector2(left, -150f);
            col.sizeDelta = new Vector2(width, -190f);
            UIKit.VList(col, 10f);
            var h = UIKit.Heading(col, title, 54);
            h.color = UIKit.Text;
            if (!string.IsNullOrEmpty(subtitle)) UIKit.Caption(col, subtitle, 15);
            var accentRow = UIKit.Row(col, 2f, 0f, "TitleAccent");
            var accent = UIKit.Image(accentRow, "Accent", UIKit.Red);
            UIKit.Size(accent, 2f, 80f);
            UIKit.Spacer(col, 8f);
            return col;
        }

        protected void Toast(string title, string message = "", bool error = false)
        {
            if (Core.Game.Notifications != null)
                Core.Game.Notifications.Push(error ? Core.NotificationKind.Error : Core.NotificationKind.Info, title, message);
        }
    }
}
