using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Vamp.Audio;
using Vamp.Settings;

namespace Vamp.UI
{
    /// <summary>
    /// VAMP's code-built uGUI widget kit. Style: black / dark gray / white / deep red, large uppercase text,
    /// thin borders, minimal icons, subtle hover animation. Every menu screen is assembled from these widgets,
    /// so the look stays consistent and screens stay small.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Bg = new Color(0.035f, 0.035f, 0.04f, 0.94f);
        public static readonly Color PanelBg = new Color(0.07f, 0.07f, 0.08f, 0.92f);
        public static readonly Color PanelLight = new Color(0.12f, 0.12f, 0.13f, 0.95f);
        public static readonly Color Border = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color Text = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.5f);
        public static readonly Color TextFaint = new Color(1f, 1f, 1f, 0.28f);
        public static readonly Color Red = new Color(0.78f, 0.05f, 0.11f, 1f);
        public static readonly Color RedDark = new Color(0.4f, 0.02f, 0.06f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.85f, 0.45f, 1f);

        private static Sprite _circle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _circle = null; }

        // ------------------------------------------------------------------ Colorblind-aware gameplay colours

        public static Color EnemyColor()
        {
            var mode = Core.Game.Settings != null ? Core.Game.Settings.Current.accessibility.colorblind : ColorblindMode.Off;
            switch (mode)
            {
                case ColorblindMode.Protanopia:
                case ColorblindMode.Deuteranopia: return new Color(1f, 0.62f, 0.1f);
                case ColorblindMode.Tritanopia: return new Color(1f, 0.2f, 0.35f);
                default: return new Color(0.95f, 0.15f, 0.2f);
            }
        }

        public static Color AllyColor()
        {
            var mode = Core.Game.Settings != null ? Core.Game.Settings.Current.accessibility.colorblind : ColorblindMode.Off;
            return mode == ColorblindMode.Tritanopia ? new Color(0.2f, 0.85f, 0.85f) : new Color(0.3f, 0.6f, 1f);
        }

        // ------------------------------------------------------------------ Infrastructure

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null) return;
            var go = new GameObject("[VAMP EventSystem]", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int s = 64;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "VampCircle", wrapMode = TextureWrapMode.Clamp };
                float r = s * 0.5f - 1f;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                    }
                tex.Apply();
                _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
                return _circle;
            }
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform StretchNode(string name, Transform parent, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            return Stretch(Node(name, parent), l, r, t, b);
        }

        public static Image Image(Transform parent, string name, Color color)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static LayoutElement Size(Component c, float height = -1f, float width = -1f, float flexW = -1f, float flexH = -1f)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
            if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
            if (flexW >= 0f) le.flexibleWidth = flexW;
            if (flexH >= 0f) le.flexibleHeight = flexH;
            return le;
        }

        public static VerticalLayoutGroup VList(Transform t, float spacing = 8f, int padding = 0, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = t.gameObject.GetComponent<VerticalLayoutGroup>();
            if (v == null) v = t.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HList(Transform t, float spacing = 8f, int padding = 0, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = t.gameObject.GetComponent<HorizontalLayoutGroup>();
            if (h == null) h = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return h;
        }

        public static RectTransform Row(Transform parent, float height, float spacing = 8f, string name = "Row")
        {
            var rt = Node(name, parent);
            HList(rt, spacing);
            // A HorizontalLayoutGroup with childForceExpandHeight reports flexibleHeight = 1, which made every row
            // soak up spare page height (tall search box / tabs / footer, tiny scroll lists). Rows are fixed height.
            Size(rt, height).flexibleHeight = 0f;
            return rt;
        }

        public static RectTransform Column(Transform parent, float spacing = 8f, string name = "Column", int padding = 0)
        {
            var rt = Node(name, parent);
            VList(rt, spacing, padding);
            return rt;
        }

        public static void Spacer(Transform parent, float size, bool flexible = false)
        {
            var rt = Node("Spacer", parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = size;
            le.preferredWidth = size;
            if (flexible) { le.flexibleHeight = 1f; le.flexibleWidth = 1f; }
        }

        // ------------------------------------------------------------------ Text

        public static Text Label(Transform parent, string text, int size = 18, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft,
                                 FontStyle style = FontStyle.Bold, string name = "Label")
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UIFactory.Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color ?? Text;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text Heading(Transform parent, string text, int size = 44)
        {
            var t = Label(parent, text, size, Text, TextAnchor.MiddleLeft, FontStyle.Bold, "Heading");
            Size(t, size + 12);
            return t;
        }

        public static Text Caption(Transform parent, string text, int size = 14)
        {
            var t = Label(parent, text, size, TextDim, TextAnchor.MiddleLeft, FontStyle.Normal, "Caption");
            Size(t, size + 10);
            return t;
        }

        public static Image Divider(Transform parent, Color? color = null, float thickness = 1f)
        {
            var img = Image(parent, "Divider", color ?? Border);
            Size(img, thickness);
            return img;
        }

        // ------------------------------------------------------------------ Panels

        public static Image Panel(Transform parent, string name = "Panel", Color? color = null, bool border = true)
        {
            var img = Image(parent, name, color ?? PanelBg);
            img.raycastTarget = true;
            if (border)
            {
                var ol = img.gameObject.AddComponent<Outline>();
                ol.effectColor = Border;
                ol.effectDistance = new Vector2(1f, -1f);
            }
            return img;
        }

        // ------------------------------------------------------------------ Buttons

        public enum ButtonStyle { Box, Menu, Primary, Danger, Tab, Ghost }

        public static Button Button(Transform parent, string text, Action onClick, ButtonStyle style = ButtonStyle.Box, int fontSize = 18, float height = 46f)
        {
            var rt = Node("Button_" + text, parent);
            var bg = rt.gameObject.AddComponent<Image>();
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            Size(rt, height);

            Color normal, highlight, pressed;
            TextAnchor align = TextAnchor.MiddleCenter;
            switch (style)
            {
                case ButtonStyle.Menu:
                    normal = new Color(0f, 0f, 0f, 0f); highlight = new Color(1f, 1f, 1f, 0.06f); pressed = new Color(0.78f, 0.05f, 0.11f, 0.25f);
                    align = TextAnchor.MiddleLeft; break;
                case ButtonStyle.Primary:
                    normal = Red; highlight = new Color(0.9f, 0.1f, 0.16f); pressed = RedDark; break;
                case ButtonStyle.Danger:
                    normal = RedDark; highlight = Red; pressed = new Color(0.3f, 0f, 0.03f); break;
                case ButtonStyle.Tab:
                    normal = new Color(0f, 0f, 0f, 0f); highlight = new Color(1f, 1f, 1f, 0.06f); pressed = new Color(1f, 1f, 1f, 0.1f); break;
                case ButtonStyle.Ghost:
                    normal = new Color(1f, 1f, 1f, 0.03f); highlight = new Color(1f, 1f, 1f, 0.09f); pressed = new Color(1f, 1f, 1f, 0.14f); break;
                default:
                    normal = PanelLight; highlight = new Color(0.2f, 0.2f, 0.22f, 1f); pressed = RedDark; break;
            }
            bg.color = Color.white;
            var cb = btn.colors;
            cb.normalColor = normal;
            cb.highlightedColor = highlight;
            cb.selectedColor = normal;
            cb.pressedColor = pressed;
            cb.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.35f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            if (style == ButtonStyle.Box || style == ButtonStyle.Ghost)
            {
                var ol = rt.gameObject.AddComponent<Outline>();
                ol.effectColor = Border;
                ol.effectDistance = new Vector2(1f, -1f);
            }

            var label = Label(rt, text, fontSize, Text, align, FontStyle.Bold, "Text");
            Stretch(label.rectTransform, style == ButtonStyle.Menu ? 18f : 10f, 10f, 0f, 0f);

            var hover = rt.gameObject.AddComponent<UIHover>();
            hover.Label = label;
            hover.Slide = style == ButtonStyle.Menu;
            if (style == ButtonStyle.Menu || style == ButtonStyle.Tab)
            {
                var accent = Image(rt, "Accent", Red);
                accent.rectTransform.anchorMin = new Vector2(0f, 0.2f);
                accent.rectTransform.anchorMax = new Vector2(0f, 0.8f);
                accent.rectTransform.pivot = new Vector2(0f, 0.5f);
                accent.rectTransform.sizeDelta = new Vector2(3f, 0f);
                accent.rectTransform.anchoredPosition = Vector2.zero;
                accent.enabled = false;
                hover.Accent = accent;
            }

            btn.onClick.AddListener(() =>
            {
                AudioController.PlayUI(SfxId.UIClick);
                if (onClick != null) onClick();
            });
            return btn;
        }

        public static Text ButtonText(Button b)
        {
            return b.transform.Find("Text").GetComponent<Text>();
        }

        public static void SetSelected(Button b, bool selected)
        {
            var hover = b.GetComponent<UIHover>();
            if (hover != null) hover.Selected = selected;
        }

        // ------------------------------------------------------------------ Inputs

        public static InputField Input(Transform parent, string placeholder, bool password = false, int limit = 64, float height = 46f)
        {
            var rt = Node("Input", parent);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            var ol = rt.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 1f, 1f, 0.2f);
            ol.effectDistance = new Vector2(1f, -1f);
            Size(rt, height);

            var text = Label(rt, "", 20, Text, TextAnchor.MiddleLeft, FontStyle.Normal, "Text");
            Stretch(text.rectTransform, 14f, 14f, 4f, 4f);
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var ph = Label(rt, placeholder, 18, TextFaint, TextAnchor.MiddleLeft, FontStyle.Italic, "Placeholder");
            Stretch(ph.rectTransform, 14f, 14f, 4f, 4f);

            var field = rt.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = bg;
            field.characterLimit = limit;
            field.lineType = InputField.LineType.SingleLine;
            field.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            field.caretColor = Red;
            field.selectionColor = new Color(0.78f, 0.05f, 0.11f, 0.4f);
            return field;
        }

        /// <summary>Runs <paramref name="action"/> when Enter is pressed while editing the field.</summary>
        public static void OnEnter(InputField field, Action action)
        {
            field.onEndEdit.AddListener(_ =>
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && action != null) action();
            });
        }

        /// <summary>Label + slider + exact numeric entry (e.g. sensitivity 0.01 → 10.00).</summary>
        public static Slider SliderRow(Transform parent, string label, float min, float max, float value, string format,
                                       Action<float> onChanged, bool wholeNumbers = false, Func<float, string> display = null)
        {
            var row = Row(parent, 44f, 16f, "Slider_" + label);
            var l = Label(row, label, 17, Text, TextAnchor.MiddleLeft);
            Size(l, -1, 300f);

            var sliderRt = Node("Slider", row);
            Size(sliderRt, 20f, -1f, 1f);
            var slider = BuildSlider(sliderRt);
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));

            var field = Input(row, "", false, 8, 36f);
            Size(field, 36f, 96f);
            field.textComponent.alignment = TextAnchor.MiddleCenter;
            field.textComponent.fontSize = 17;
            Func<float, string> fmt = display ?? (v => v.ToString(format));
            field.SetTextWithoutNotify(fmt(slider.value));

            slider.onValueChanged.AddListener(v =>
            {
                field.SetTextWithoutNotify(fmt(v));
                if (onChanged != null) onChanged(v);
            });
            field.onEndEdit.AddListener(s =>
            {
                float parsed;
                string clean = s.Replace("%", "").Trim();
                if (float.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed))
                {
                    if (display != null && s.Contains("%")) parsed /= 100f;
                    slider.value = Mathf.Clamp(parsed, min, max);
                }
                field.SetTextWithoutNotify(fmt(slider.value));
            });
            return slider;
        }

        public static Slider BuildSlider(RectTransform root)
        {
            var bg = Image(root, "Background", new Color(1f, 1f, 1f, 0.12f));
            Stretch(bg.rectTransform, 0, 0, 7, 7);
            var fillArea = StretchNode("Fill Area", root, 0, 8, 7, 7);
            var fill = Image(fillArea, "Fill", Red);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = StretchNode("Handle Area", root, 8, 8, 0, 0);
            var handle = Image(handleArea, "Handle", Text);
            handle.rectTransform.sizeDelta = new Vector2(10f, 0f);
            handle.raycastTarget = true;

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            var cb = s.colors;
            cb.highlightedColor = new Color(1f, 0.7f, 0.7f);
            cb.pressedColor = Red;
            s.colors = cb;
            return s;
        }

        /// <summary>"LABEL      &lt; VALUE &gt;" option cycler - the VAMP replacement for dropdowns.</summary>
        public static OptionSelector Selector(Transform parent, string label, IList<string> options, int index, Action<int> onChanged, float labelWidth = 300f)
        {
            var row = Row(parent, 44f, 12f, "Selector_" + label);
            var l = Label(row, label, 17, Text, TextAnchor.MiddleLeft);
            Size(l, -1, labelWidth);
            Spacer(row, 0f, true);

            var sel = row.gameObject.AddComponent<OptionSelector>();
            var left = Button(row, "<", null, ButtonStyle.Ghost, 18, 36f);
            Size(left, 36f, 40f);
            var value = Label(row, "", 18, Text, TextAnchor.MiddleCenter);
            Size(value, -1, 220f);
            var right = Button(row, ">", null, ButtonStyle.Ghost, 18, 36f);
            Size(right, 36f, 40f);
            sel.Init(options, index, value, onChanged);
            left.onClick.AddListener(() => sel.Step(-1));
            right.onClick.AddListener(() => sel.Step(1));
            return sel;
        }

        public static OptionSelector Toggle(Transform parent, string label, bool value, Action<bool> onChanged)
        {
            return Selector(parent, label, new[] { "OFF", "ON" }, value ? 1 : 0, i => { if (onChanged != null) onChanged(i == 1); });
        }

        // ------------------------------------------------------------------ Scroll lists

        public static RectTransform ScrollList(Transform parent, out ScrollRect scroll, float spacing = 6f, int padding = 0)
        {
            var root = Node("Scroll", parent);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.001f);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = StretchNode("Viewport", root);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            VList(content, spacing, padding);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            return content;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        // ------------------------------------------------------------------ Tabs

        public static List<Button> Tabs(Transform parent, IList<string> names, int selected, Action<int> onSelect, int fontSize = 18)
        {
            var row = Row(parent, 48f, 4f, "Tabs");
            var buttons = new List<Button>();
            for (int i = 0; i < names.Count; i++)
            {
                var b = Button(row, names[i], null, ButtonStyle.Tab, fontSize, 44f);
                Size(b, 44f, -1f, 1f);
                buttons.Add(b);
            }
            Action<int> select = null;
            select = i =>
            {
                for (int k = 0; k < buttons.Count; k++) SetSelected(buttons[k], k == i);
                if (onSelect != null) onSelect(i);
            };
            for (int i = 0; i < buttons.Count; i++)
            {
                int idx = i;
                buttons[i].onClick.AddListener(() => select(idx));
            }
            for (int k = 0; k < buttons.Count; k++) SetSelected(buttons[k], k == selected);
            Divider(parent);
            return buttons;
        }

        // ------------------------------------------------------------------ Modal

        public static GameObject Modal(Transform canvasRoot, string title, string body, params (string label, Action action, ButtonStyle style)[] buttons)
        {
            var dim = Image(canvasRoot, "Modal", new Color(0f, 0f, 0f, 0.75f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            dim.transform.SetAsLastSibling();

            var panel = Panel(dim.transform, "ModalPanel", new Color(0.06f, 0.06f, 0.07f, 0.98f));
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(620f, 0f);
            VList(prt, 14f, 32);
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var accent = Image(prt, "Accent", Red);
            Size(accent, 3f);
            Heading(prt, title, 34);
            if (!string.IsNullOrEmpty(body))
            {
                var b = Label(prt, body, 18, TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
                b.GetComponent<RectTransform>();
                var le = Size(b, -1);
                le.minHeight = 30f;
                var fitter = b.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            Spacer(prt, 6f);
            var row = Row(prt, 48f, 10f, "Buttons");
            var go = dim.gameObject;
            foreach (var bt in buttons)
            {
                var action = bt.action;
                var btn = Button(row, bt.label, () =>
                {
                    UnityEngine.Object.Destroy(go);
                    if (action != null) action();
                }, bt.style, 18, 48f);
                Size(btn, 48f, -1f, 1f);
            }
            AudioController.PlayUI(SfxId.UIOpen);
            return go;
        }
    }
}
