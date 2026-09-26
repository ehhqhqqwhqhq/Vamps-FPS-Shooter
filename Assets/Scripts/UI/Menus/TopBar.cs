using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Progression;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// Persistent main-menu header: player identity (icon, level, username, title) and the XP bar
    /// ("LEVEL 32 · DarkVamp · ██████░░ 6,420 / 8,000 XP"). Clicking it opens PROFILE.
    /// </summary>
    public sealed class TopBar
    {
        private readonly RectTransform _root;
        private readonly RectTransform _iconSlot;
        private readonly Text _name;
        private readonly Text _level;
        private readonly Text _xp;
        private readonly Image _fill;
        private readonly MenuController _menu;
        private float _shownFill;
        private float _targetFill;

        public TopBar(Transform canvas, MenuController menu)
        {
            _menu = menu;
            _root = UIKit.Node("TopBar", canvas);
            _root.anchorMin = _root.anchorMax = new Vector2(1f, 1f);
            _root.pivot = new Vector2(1f, 1f);
            _root.anchoredPosition = new Vector2(-380f, -28f);
            _root.sizeDelta = new Vector2(460f, 84f);

            var btn = _root.gameObject.AddComponent<Button>();
            var bg = _root.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.35f);
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => menu.Push(new ProfileScreen()));

            UIKit.HList(_root, 12f, 10);
            _iconSlot = UIKit.Node("IconSlot", _root);
            UIKit.Size(_iconSlot, 64f, 64f);

            var col = UIKit.Column(_root, 2f);
            UIKit.Size(col, -1, -1, 1f);
            var line = UIKit.Row(col, 30f, 10f);
            _level = UIKit.Label(line, "LEVEL 1", 16, UIKit.Red, TextAnchor.MiddleLeft);
            UIKit.Size(_level, -1, 110f);
            _name = UIKit.Label(line, "", 20, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Size(_name, -1, -1, 1f);

            var barRow = UIKit.Node("Bar", col);
            UIKit.Size(barRow, 6f);
            var barBg = UIKit.Image(barRow, "Bg", new Color(1f, 1f, 1f, 0.12f));
            UIKit.Stretch(barBg.rectTransform);
            _fill = UIKit.Image(barRow, "Fill", UIKit.Red);
            var frt = _fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = new Vector2(0f, 0f);

            _xp = UIKit.Label(col, "", 13, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIKit.Size(_xp, 20f);

            if (Game.Progression != null) Game.Progression.ProfileChanged += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            if (Game.Progression != null) Game.Progression.ProfileChanged -= Refresh;
        }

        public void SetVisible(bool v)
        {
            if (_root.gameObject.activeSelf != v)
            {
                _root.gameObject.SetActive(v);
                if (v) Refresh();
            }
        }

        public void Refresh()
        {
            var prog = Game.Progression;
            if (prog == null || prog.Profile == null) return;
            var p = prog.Profile;
            UIKit.Clear(_iconSlot);
            IdentityViews.Icon(_iconSlot, p.profile_icon, p.icon_frame, 64f);
            _name.text = Game.Username;
            _level.text = (p.prestige_level > 0 ? new string('★', Mathf.Min(3, p.prestige_level)) + " " : "") + "LEVEL " + p.level;
            int need = prog.XpToNext;
            _xp.text = prog.IsMaxLevel ? "MAX LEVEL" + (prog.CanPrestige ? "  ·  PRESTIGE AVAILABLE" : "")
                                       : p.xp.ToString("N0") + " / " + need.ToString("N0") + " XP";
            _targetFill = prog.IsMaxLevel ? 1f : Mathf.Clamp01((float)p.xp / need);
        }

        public void Tick(float dt)
        {
            _shownFill = Mathf.MoveTowards(_shownFill, _targetFill, dt * 1.5f);
            var parent = (RectTransform)_fill.rectTransform.parent;
            _fill.rectTransform.sizeDelta = new Vector2(parent.rect.width * _shownFill, 0f);
        }
    }
}
