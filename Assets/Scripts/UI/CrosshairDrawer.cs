using UnityEngine;
using UnityEngine.UI;
using Vamp.Settings;

namespace Vamp.UI
{
    /// <summary>Draws a crosshair (shape, size, thickness, gap, opacity, outline, center dot, dynamic gap) from settings.</summary>
    public static class CrosshairDrawer
    {
        public const float PixelsPerSpreadDegree = 6f;

        public static void Draw(RectTransform parent, CrosshairSettings c, float spreadDeg, float scale = 1f)
        {
            bool lines = c.type != CrosshairType.Dot;
            bool dot = c.type != CrosshairType.Cross || c.centerDot;
            float gap = (c.gap + (c.dynamic ? spreadDeg * PixelsPerSpreadDegree : 0f)) * scale;
            float len = c.size * scale;
            float th = c.thickness * scale;
            float o = c.outline ? c.outlineThickness * scale : 0f;
            var col = c.color;
            col.a = c.opacity;
            var outCol = new Color(0f, 0f, 0f, c.opacity);

            if (lines)
            {
                Part(parent, new Vector2(0f, gap + len * 0.5f), new Vector2(th, len), col, o, outCol);
                Part(parent, new Vector2(0f, -(gap + len * 0.5f)), new Vector2(th, len), col, o, outCol);
                Part(parent, new Vector2(-(gap + len * 0.5f), 0f), new Vector2(len, th), col, o, outCol);
                Part(parent, new Vector2(gap + len * 0.5f, 0f), new Vector2(len, th), col, o, outCol);
            }
            if (dot)
            {
                float d = c.type == CrosshairType.Dot ? Mathf.Max(th, c.size * 0.5f * scale) : th;
                Part(parent, Vector2.zero, new Vector2(d, d), col, o, outCol);
            }
        }

        private static void Part(RectTransform parent, Vector2 pos, Vector2 size, Color col, float outline, Color outCol)
        {
            if (outline > 0f)
            {
                var ol = UIKit.Image(parent, "Outline", outCol);
                ol.rectTransform.anchorMin = ol.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                ol.rectTransform.anchoredPosition = pos;
                ol.rectTransform.sizeDelta = size + Vector2.one * outline * 2f;
            }
            var p = UIKit.Image(parent, "Part", col);
            p.rectTransform.anchorMin = p.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            p.rectTransform.anchoredPosition = pos;
            p.rectTransform.sizeDelta = size;
        }
    }
}
