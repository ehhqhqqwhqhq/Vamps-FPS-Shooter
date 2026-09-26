using UnityEngine;
using UnityEngine.UI;

namespace Vamp.UI
{
    /// <summary>
    /// Small helpers for building uGUI in code (prototype HUD). Also holds the VAMP UI palette so every
    /// screen uses the same black / dark gray / white / deep red identity.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color White = new Color(0.96f, 0.96f, 0.96f, 1f);
        public static readonly Color DimWhite = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color DeepRed = new Color(0.78f, 0.05f, 0.11f, 1f);
        public static readonly Color DarkGray = new Color(0.08f, 0.08f, 0.09f, 0.75f);
        public static readonly Color Black = new Color(0f, 0f, 0f, 0.85f);

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, Transform parent, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            canvas.pixelPerfect = false;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
                                 int fontSize, TextAnchor align, Color color, FontStyle style = FontStyle.Bold)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Horizontal bar: returns the fill image (pivot left; set width via SetFill).</summary>
        public static Image Bar(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color fill)
        {
            var bg = Box(name, parent, anchor, pivot, pos, size, new Color(1f, 1f, 1f, 0.12f));
            var f = Box(name + "Fill", bg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, size, fill);
            return f;
        }

        public static void SetFill(Image fill, float fullWidth, float t)
        {
            var rt = fill.rectTransform;
            rt.sizeDelta = new Vector2(fullWidth * Mathf.Clamp01(t), rt.sizeDelta.y);
        }
    }
}
