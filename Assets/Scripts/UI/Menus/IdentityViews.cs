using UnityEngine;
using UnityEngine.UI;
using Vamp.Progression;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// Builders for VAMP player identity: ICON (+ FRAME), LEVEL, TITLE, BANNER, USERNAME.
    /// Used in lobby cards, profile, scoreboards, match intro/results and friend lists.
    /// </summary>
    public static class IdentityViews
    {
        /// <summary>Square icon view: shaped background, glyph, frame outline, optional subtle animation.</summary>
        public static RectTransform Icon(Transform parent, string iconId, string frameId, float size, bool locked = false)
        {
            var item = CosmeticCatalog.Get(iconId) ?? CosmeticCatalog.Get("icon_vamp_symbol");
            var frame = CosmeticCatalog.Get(frameId);

            var root = UIKit.Node("Icon", parent);
            root.sizeDelta = new Vector2(size, size);
            UIKit.Size(root, size, size);

            var bg = UIKit.Image(root, "Shape", new Color(0.09f, 0.09f, 0.1f, 1f));
            var brt = bg.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            switch (item.Shape)
            {
                case IconShape.Circle:
                    bg.sprite = UIKit.Circle;
                    brt.sizeDelta = new Vector2(size, size);
                    break;
                case IconShape.Diamond:
                    brt.sizeDelta = new Vector2(size * 0.72f, size * 0.72f);
                    brt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                case IconShape.Shield:
                    brt.sizeDelta = new Vector2(size * 0.82f, size * 0.92f);
                    break;
                default:
                    brt.sizeDelta = new Vector2(size * 0.9f, size * 0.9f);
                    break;
            }

            Color frameColor = frame != null ? frame.Color : CosmeticCatalog.Steel;
            var ol = bg.gameObject.AddComponent<Outline>();
            ol.effectColor = locked ? new Color(1f, 1f, 1f, 0.15f) : frameColor;
            float thick = frame != null && frame.Id != "frame_basic" ? 3f : 1.5f;
            ol.effectDistance = new Vector2(thick, -thick);

            var glyph = UIKit.Label(root, locked ? "?" : item.Glyph, Mathf.RoundToInt(size * (item.Glyph.Length > 2 ? 0.3f : 0.46f)),
                                    locked ? UIKit.TextFaint : item.Color, TextAnchor.MiddleCenter);
            UIKit.Stretch(glyph.rectTransform);
            glyph.horizontalOverflow = HorizontalWrapMode.Overflow;

            if (!locked && (item.Animated || (frame != null && frame.Animated)))
            {
                var anim = root.gameObject.AddComponent<IconPulse>();
                anim.Target = ol;
                anim.Glyph = glyph;
                anim.Base = frameColor;
            }
            return root;
        }

        /// <summary>Compact "[ICON] [32] DarkVamp / PREDATOR" row used in lists.</summary>
        public static RectTransform Row(Transform parent, string iconId, string frameId, int level, int prestige, string username, string subline, Color? sublineColor = null)
        {
            var row = UIKit.Row(parent, 56f, 12f, "IdentityRow");
            Icon(row, iconId, frameId, 48f);
            var lvl = UIKit.Label(row, LevelText(level, prestige), 18, UIKit.Red, TextAnchor.MiddleCenter);
            UIKit.Size(lvl, -1, 56f);
            var col = UIKit.Column(row, 0f);
            UIKit.Size(col, -1, -1, 1f);
            var name = UIKit.Label(col, username, 19, UIKit.Text, TextAnchor.LowerLeft);
            UIKit.Size(name, 28f);
            var sub = UIKit.Label(col, subline ?? "", 13, sublineColor ?? UIKit.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
            UIKit.Size(sub, 20f);
            return row;
        }

        public static string LevelText(int level, int prestige)
        {
            return prestige > 0 ? new string('★', Mathf.Min(3, prestige)) + level : "[" + level + "]";
        }

        /// <summary>
        /// Player card (spec 89): banner background, big icon, "[32] DarkVamp", title, optional ready state.
        /// </summary>
        public static RectTransform Card(Transform parent, PlayerProfileData p, string username, float width = 260f, float height = 320f,
                                         string status = null, bool highlight = false)
        {
            var banner = p != null ? CosmeticCatalog.Get(p.banner) : null;
            Color bannerColor = banner != null ? banner.Color : CosmeticCatalog.Dark;

            var card = UIKit.Panel(parent, "PlayerCard", new Color(0.05f, 0.05f, 0.06f, 0.95f));
            var rt = card.rectTransform;
            UIKit.Size(rt, height, width);
            rt.sizeDelta = new Vector2(width, height);
            if (highlight) card.GetComponent<Outline>().effectColor = UIKit.Red;

            var stripe = UIKit.Image(rt, "Banner", new Color(bannerColor.r, bannerColor.g, bannerColor.b, 0.55f));
            var srt = stripe.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(0f, height * 0.42f);
            var stripeLine = UIKit.Image(stripe.transform, "Line", UIKit.Red);
            var lrt = stripeLine.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(0f, 2f);

            var col = UIKit.StretchNode("Content", rt, 12f, 12f, 24f, 16f);
            UIKit.VList(col, 6f, 0, TextAnchor.UpperCenter);
            var iconRow = UIKit.Row(col, width * 0.45f, 0f, "IconRow");
            iconRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            Icon(iconRow, p != null ? p.profile_icon : null, p != null ? p.icon_frame : null, width * 0.42f);
            UIKit.Spacer(col, 6f);

            int level = p != null ? p.level : 1;
            int prestige = p != null ? p.prestige_level : 0;
            var nameText = UIKit.Label(col, "<color=#C8102E>" + LevelText(level, prestige) + "</color>  " + username, 22, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Size(nameText, 30f);

            var title = p != null ? CosmeticCatalog.Get(p.title) : null;
            string titleText = p != null && p.title_visible && title != null ? title.Name : "";
            var t = UIKit.Label(col, titleText, 15, UIKit.TextDim, TextAnchor.MiddleCenter);
            UIKit.Size(t, 22f);

            if (!string.IsNullOrEmpty(status))
            {
                UIKit.Spacer(col, 0f, true);
                bool ready = status == "READY";
                var s = UIKit.Label(col, status, 16, ready ? UIKit.Good : UIKit.TextFaint, TextAnchor.MiddleCenter);
                UIKit.Size(s, 22f);
            }
            return rt;
        }
    }

}
