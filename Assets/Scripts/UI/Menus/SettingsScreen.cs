using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Graphics;
using Vamp.Settings;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// SETTINGS: CONTROLS · MOUSE · DISPLAY · GRAPHICS · AUDIO · ACCESSIBILITY, with SEARCH SETTINGS...,
    /// RESET ALL SETTINGS and per-change live apply. Display changes require KEEP/REVERT confirmation (15 s).
    /// The screen only edits Game.Settings - gameplay systems react through SettingsController.Changed.
    /// </summary>
    public sealed class SettingsScreen : MenuScreen
    {
        private static readonly string[] TabNames = { "CONTROLS", "MOUSE", "DISPLAY", "GRAPHICS", "AUDIO", "ACCESSIBILITY" };

        // Search index: label, tab, section
        private static readonly (string label, int tab, string section)[] Index =
        {
            ("KEY BINDINGS", 0, "CONTROLS"), ("RESET CONTROLS TO DEFAULTS", 0, "CONTROLS"), ("TOGGLE CROUCH", 0, "CONTROLS"), ("TOGGLE AIM", 0, "CONTROLS"),
            ("MOUSE SENSITIVITY", 1, "MOUSE"), ("ADS SENSITIVITY", 1, "MOUSE"), ("SNIPER SENSITIVITY", 1, "MOUSE"), ("MOUSE ACCELERATION", 1, "MOUSE"),
            ("RAW MOUSE INPUT", 1, "MOUSE"), ("INVERT Y", 1, "MOUSE"), ("INVERT X", 1, "MOUSE"), ("FIELD OF VIEW (FOV)", 1, "MOUSE"),
            ("DISPLAY MODE", 2, "DISPLAY"), ("RESOLUTION", 2, "DISPLAY"), ("REFRESH RATE", 2, "DISPLAY"), ("V-SYNC", 2, "DISPLAY"),
            ("FPS LIMIT", 2, "DISPLAY"), ("MENU FPS", 2, "PERFORMANCE"), ("BACKGROUND FPS", 2, "PERFORMANCE"), ("SHOW FPS", 2, "PERFORMANCE"),
            ("FPS COUNTER POSITION", 2, "PERFORMANCE"), ("PERFORMANCE MONITOR", 2, "PERFORMANCE"),
            ("QUALITY PRESET", 3, "GRAPHICS"), ("TEXTURE QUALITY", 3, "GRAPHICS"), ("SHADOW QUALITY", 3, "GRAPHICS"), ("EFFECT QUALITY", 3, "GRAPHICS"),
            ("LIGHTING QUALITY", 3, "GRAPHICS"), ("POST PROCESSING", 3, "GRAPHICS"), ("ANTI-ALIASING", 3, "GRAPHICS"), ("VIEW DISTANCE", 3, "GRAPHICS"),
            ("PARTICLES", 3, "GRAPHICS"), ("REFLECTIONS", 3, "GRAPHICS"), ("AMBIENT OCCLUSION", 3, "GRAPHICS"), ("MOTION BLUR", 3, "GRAPHICS"),
            ("DEPTH OF FIELD", 3, "GRAPHICS"), ("VIGNETTE", 3, "GRAPHICS"), ("BLOOM", 3, "GRAPHICS"), ("RENDER SCALE", 3, "GRAPHICS"),
            ("COMPETITIVE VISUALS", 3, "GRAPHICS"), ("SCREEN SHAKE", 3, "GRAPHICS"), ("BENCHMARK", 3, "GRAPHICS"), ("DETECT GRAPHICS", 3, "GRAPHICS"),
            ("MASTER VOLUME", 4, "AUDIO"), ("MUSIC VOLUME", 4, "AUDIO"), ("SFX VOLUME", 4, "AUDIO"), ("VOICE VOLUME", 4, "AUDIO"),
            ("UI VOLUME", 4, "AUDIO"), ("ANNOUNCER VOLUME", 4, "AUDIO"), ("VOICE CHAT VOLUME", 4, "AUDIO"), ("PUSH TO TALK / OPEN MIC", 4, "AUDIO"),
            ("COLORBLIND MODE", 5, "ACCESSIBILITY"), ("SUBTITLE SIZE", 5, "ACCESSIBILITY"), ("SUBTITLE BACKGROUND", 5, "ACCESSIBILITY"),
            ("HIT MARKERS", 5, "ACCESSIBILITY"), ("DAMAGE INDICATORS", 5, "ACCESSIBILITY"), ("CAMERA EFFECTS", 5, "ACCESSIBILITY"),
            ("HUD SCALE", 5, "ACCESSIBILITY"), ("CROSSHAIR", 5, "ACCESSIBILITY"),
        };

        private int _tab;
        private RectTransform _content;
        private RectTransform _searchResults;
        private InputField _search;
        private List<Button> _tabs;
        private KeybindEditor _keys;
        private OptionSelector _presetSelector;
        private RectTransform _crosshairPreview;

        // Display confirmation
        private DisplaySettingsData _displayBackup;
        private GameObject _confirmModal;
        private Text _confirmText;
        private float _confirmTimer;

        public SettingsScreen(int tab = 0) { _tab = tab; }

        private GameSettings S { get { return Game.Settings.Current; } }

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "SETTINGS", null, 1180f);
            var searchRow = UIKit.Row(col, 44f, 10f);
            _search = UIKit.Input(searchRow, "SEARCH SETTINGS...", false, 40, 42f);
            UIKit.Size(_search, 42f, 420f);
            _search.onValueChanged.AddListener(OnSearch);
            _searchResults = UIKit.Column(col, 2f, "SearchResults");

            _tabs = UIKit.Tabs(col, TabNames, _tab, i => { _tab = i; BuildTab(); }, 16);
            ScrollRect scroll;
            _content = UIKit.ScrollList(col, out scroll, 4f);
            UIKit.Size(scroll, -1, -1, -1, 1f);

            var footer = UIKit.Row(col, 46f, 10f);
            var reset = UIKit.Button(footer, "RESET ALL SETTINGS", () =>
                UIKit.Modal(Host.ModalRoot, "RESET ALL SETTINGS?", "RESTORES CONTROLS, MOUSE, DISPLAY, GRAPHICS, AUDIO AND ACCESSIBILITY TO DEFAULTS.",
                    ("RESET", () =>
                    {
                        Game.Settings.ResetAll();
                        _keys.ResetDefaults();
                        BuildTab();
                        RefreshBinds();
                    }, UIKit.ButtonStyle.Danger), ("CANCEL", null, UIKit.ButtonStyle.Box)),
                UIKit.ButtonStyle.Danger, 15, 44f);
            UIKit.Size(reset, 44f, 260f);
            UIKit.Spacer(footer, 0f, true);
            var back = UIKit.Button(footer, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 16, 44f);
            UIKit.Size(back, 44f, 200f);

            _keys = new KeybindEditor();
            BuildTab();
        }

        public override void OnHide()
        {
            if (_confirmModal != null) RevertDisplay();
            if (_bindWindow != null) CloseBindWindow();
            if (Game.Settings != null) Game.Settings.Save();
            if (_keys != null) { _keys.Dispose(); _keys = null; }
        }

        public override bool OnBack()
        {
            if (_keys != null && _keys.IsRebinding) { _keys.Cancel(); return true; }
            if (_bindWindow != null) { CloseBindWindow(); return true; }
            return false;
        }

        private void Changed()
        {
            Game.Settings.ApplyNonDisplay();
        }

        // ------------------------------------------------------------------ Search

        private void OnSearch(string q)
        {
            UIKit.Clear(_searchResults);
            q = (q ?? "").Trim().ToUpperInvariant();
            if (q.Length < 2) return;
            int n = 0;
            foreach (var e in Index)
            {
                if (!e.label.Contains(q) && !e.section.Contains(q)) continue;
                if (n++ >= 6) break;
                int tab = e.tab;
                var b = UIKit.Button(_searchResults, e.label + "    <color=#FFFFFF66>" + TabNames[e.tab] + " → " + e.label + "</color>", () =>
                {
                    _search.text = "";
                    _tab = tab;
                    for (int i = 0; i < _tabs.Count; i++) UIKit.SetSelected(_tabs[i], i == tab);
                    BuildTab();
                }, UIKit.ButtonStyle.Ghost, 15, 34f);
                UIKit.ButtonText(b).alignment = TextAnchor.MiddleLeft;
            }
            if (n == 0) UIKit.Size(UIKit.Label(_searchResults, "NO MATCHING SETTINGS", 14, UIKit.TextFaint), 26f);
        }

        // ------------------------------------------------------------------ Tabs

        private void BuildTab()
        {
            UIKit.Clear(_content);
            _crosshairPreview = null;
            switch (_tab)
            {
                case 0: Controls(); break;
                case 1: Mouse(); break;
                case 2: Display(); break;
                case 3: GraphicsTab(); break;
                case 4: AudioTab(); break;
                default: Accessibility(); break;
            }
        }

        private void Section(string title)
        {
            UIKit.Spacer(_content, 8f);
            UIKit.Size(UIKit.Label(_content, title, 14, UIKit.Red), 24f);
            UIKit.Divider(_content);
        }

        private void Note(string text)
        {
            var l = UIKit.Label(_content, text, 13, UIKit.TextFaint, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIKit.Size(l, 22f);
        }

        // ---- CONTROLS
        private GameObject _bindWindow;
        private RectTransform _bindBody;

        private static readonly (string title, string[] labels)[] BindGroups =
        {
            ("MOVEMENT", new[] { "MOVE FORWARD", "MOVE BACKWARD", "MOVE LEFT", "MOVE RIGHT", "JUMP", "CROUCH", "SPRINT", "SLIDE", "WALL JUMP", "DASH" }),
            ("COMBAT", new[] { "FIRE", "AIM", "RELOAD", "PRIMARY WEAPON", "SECONDARY WEAPON", "MELEE", "DROP WEAPON" }),
            ("OTHER", new[] { "INTERACT", "SCOREBOARD", "PUSH TO TALK" }),
        };

        private void Controls()
        {
            Section("KEY BINDINGS");
            var open = UIKit.Button(_content, "OPEN KEY BINDINGS", OpenBindWindow, UIKit.ButtonStyle.Primary, 20, 60f);
            UIKit.Size(open, 60f);
            Note("OPENS A FULL-SIZE WINDOW WITH EVERY ACTION, ITS PRIMARY AND SECONDARY BIND.");
            Section("OPTIONS");
            UIKit.Toggle(_content, "TOGGLE CROUCH", S.controls.toggleCrouch, v => { S.controls.toggleCrouch = v; Changed(); });
            UIKit.Toggle(_content, "TOGGLE AIM", S.controls.toggleAim, v => { S.controls.toggleAim = v; Changed(); });
            UIKit.Spacer(_content, 8f);
            UIKit.Button(_content, "RESET CONTROLS TO DEFAULTS", ConfirmResetBinds, UIKit.ButtonStyle.Box, 15, 42f);
        }

        private void ConfirmResetBinds()
        {
            UIKit.Modal(Host.ModalRoot, "RESET TO DEFAULTS?", "ALL KEY BINDINGS WILL BE RESTORED.",
                ("RESET", () => { _keys.ResetDefaults(); RefreshBinds(); }, UIKit.ButtonStyle.Danger), ("CANCEL", null, UIKit.ButtonStyle.Box));
        }

        /// <summary>Large key-binding window over the whole menu: two columns, grouped, every bind visible at once.</summary>
        private void OpenBindWindow()
        {
            if (_bindWindow != null) return;
            var dim = UIKit.Image(Host.ModalRoot, "KeyBindWindow", new Color(0f, 0f, 0f, 0.82f));
            dim.raycastTarget = true;
            UIKit.Stretch(dim.rectTransform);
            dim.transform.SetAsLastSibling();
            _bindWindow = dim.gameObject;

            var panel = UIKit.Panel(dim.transform, "Panel", new Color(0.05f, 0.05f, 0.06f, 0.98f));
            UIKit.Stretch(panel.rectTransform, 90f, 90f, 60f, 60f);
            UIKit.VList(panel.transform, 12f, 36);

            var accent = UIKit.Image(panel.transform, "Accent", UIKit.Red);
            UIKit.Size(accent, 3f);
            var header = UIKit.Row(panel.transform, 56f, 10f);
            UIKit.Size(UIKit.Heading(header, "KEY BINDINGS", 44), 56f, 600f);
            UIKit.Spacer(header, 0f, true);
            var close = UIKit.Button(header, "CLOSE  [ESC]", CloseBindWindow, UIKit.ButtonStyle.Ghost, 16, 44f);
            UIKit.Size(close, 44f, 200f);
            UIKit.Size(UIKit.Label(panel.transform, "CLICK A BIND, THEN PRESS ANY KEY OR MOUSE BUTTON.  ESC CANCELS.  PAUSE IS ALWAYS ESC.", 15, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal), 24f);
            UIKit.Divider(panel.transform);

            ScrollRect scroll;
            _bindBody = UIKit.ScrollList(panel.transform, out scroll, 0f);
            UIKit.Size(scroll, -1, -1, 1f, 1f);

            UIKit.Divider(panel.transform);
            var footer = UIKit.Row(panel.transform, 50f, 12f);
            var reset = UIKit.Button(footer, "RESET TO DEFAULTS", ConfirmResetBinds, UIKit.ButtonStyle.Danger, 16, 48f);
            UIKit.Size(reset, 48f, 280f);
            UIKit.Spacer(footer, 0f, true);
            var done = UIKit.Button(footer, "DONE", CloseBindWindow, UIKit.ButtonStyle.Primary, 18, 48f);
            UIKit.Size(done, 48f, 240f);

            BuildBindBody();
            Audio.AudioController.PlayUI(Audio.SfxId.UIOpen);
        }

        private void CloseBindWindow()
        {
            if (_keys != null && _keys.IsRebinding) _keys.Cancel();
            if (_bindWindow != null) UnityEngine.Object.Destroy(_bindWindow);
            _bindWindow = null;
            _bindBody = null;
            if (Game.Settings != null) Game.Settings.Save();
        }

        private void BuildBindBody()
        {
            if (_bindBody == null) return;
            UIKit.Clear(_bindBody);
            var cols = UIKit.Node("Columns", _bindBody);
            var h = UIKit.HList(cols, 48f);
            h.childAlignment = TextAnchor.UpperLeft;
            h.childForceExpandHeight = false;
            var left = UIKit.Column(cols, 6f, "Left");
            UIKit.Size(left, -1, -1, 1f);
            var right = UIKit.Column(cols, 6f, "Right");
            UIKit.Size(right, -1, -1, 1f);

            BindGroup(left, BindGroups[0].title, BindGroups[0].labels);
            BindGroup(right, BindGroups[1].title, BindGroups[1].labels);
            UIKit.Spacer(right, 18f);
            BindGroup(right, BindGroups[2].title, BindGroups[2].labels);
            // Pause is fixed to ESC (spec) - shown for completeness.
            var pause = UIKit.Row(right, 46f, 12f);
            UIKit.Size(UIKit.Label(pause, "PAUSE / MENU", 18, UIKit.TextDim), -1, -1, 1f);
            var fixedKey = UIKit.Label(pause, "ESC  (FIXED)", 16, UIKit.TextFaint, TextAnchor.MiddleCenter);
            UIKit.Size(fixedKey, -1, 412f);
        }

        private void BindGroup(Transform col, string title, string[] labels)
        {
            UIKit.Size(UIKit.Label(col, title, 16, UIKit.Red), 28f);
            var head = UIKit.Row(col, 22f, 12f);
            UIKit.Size(UIKit.Label(head, "ACTION", 13, UIKit.TextFaint), -1, -1, 1f);
            UIKit.Size(UIKit.Label(head, "PRIMARY", 13, UIKit.TextFaint, TextAnchor.MiddleCenter), -1, 200f);
            UIKit.Size(UIKit.Label(head, "SECONDARY", 13, UIKit.TextFaint, TextAnchor.MiddleCenter), -1, 200f);
            UIKit.Divider(col);
            foreach (var label in labels)
            {
                foreach (var r in VampInputActions.Rebindable)
                {
                    if (r.label != label) continue;
                    var row = UIKit.Row(col, 46f, 12f);
                    UIKit.Size(UIKit.Label(row, r.label, 18, UIKit.Text), -1, -1, 1f);
                    BindButton(row, r.action, r.index);
                    if (r.hasSecondary) BindButton(row, r.action, 1);
                    else UIKit.Size(UIKit.Label(row, "—", 16, UIKit.TextFaint, TextAnchor.MiddleCenter), -1, 200f);
                    break;
                }
            }
        }

        private void RefreshBinds()
        {
            if (_bindWindow != null) BuildBindBody();
            else if (_tab == 0) BuildTab();
        }

        private void BindButton(Transform row, string action, int index)
        {
            Button b = null;
            b = UIKit.Button(row, _keys.Display(action, index), () => Rebind(action, index, b), UIKit.ButtonStyle.Box, 17, 42f);
            UIKit.Size(b, 42f, 200f);
        }

        private void Rebind(string action, int index, Button button)
        {
            var modal = UIKit.Modal(Host.ModalRoot, "PRESS A KEY", "PRESS ANY KEYBOARD KEY\nOR MOUSE BUTTON.\n\n[ESC] CANCEL");
            _keys.StartRebind(action, index, c =>
            {
                UnityEngine.Object.Destroy(modal);
                if (c.Conflicts.Count == 0)
                {
                    _keys.Apply(c, false);
                    RefreshBinds();
                    return;
                }
                string names = "";
                foreach (var x in c.Conflicts) names += "\n" + x.Label;
                UIKit.Modal(Host.ModalRoot, "KEY CONFLICT", c.DisplayPath + " IS CURRENTLY ASSIGNED TO:\n" + names,
                    ("REPLACE", () => { _keys.Apply(c, true); RefreshBinds(); }, UIKit.ButtonStyle.Primary),
                    ("CANCEL", null, UIKit.ButtonStyle.Box));
            }, () => UnityEngine.Object.Destroy(modal));
        }

        // ---- MOUSE
        private void Mouse()
        {
            var m = S.mouse;
            Section("MOUSE");
            UIKit.SliderRow(_content, "MOUSE SENSITIVITY", 0.01f, 10f, m.sensitivity, "0.00", v => { m.sensitivity = v; Changed(); });
            UIKit.SliderRow(_content, "ADS SENSITIVITY", 0.01f, 10f, m.adsSensitivity, "0.00", v => { m.adsSensitivity = v; Changed(); });
            UIKit.SliderRow(_content, "SNIPER SENSITIVITY", 0.01f, 10f, m.sniperSensitivity, "0.00", v => { m.sniperSensitivity = v; Changed(); });
            UIKit.Toggle(_content, "MOUSE ACCELERATION", m.mouseAcceleration, v => { m.mouseAcceleration = v; Changed(); });
            UIKit.Toggle(_content, "RAW MOUSE INPUT", m.rawInput, v => { m.rawInput = v; Changed(); });
            Note("VAMP READS RAW, UNACCELERATED MOUSE DELTAS. ACCELERATION ADDS A SMALL SPEED CURVE WHEN ON.");
            UIKit.Toggle(_content, "INVERT Y", m.invertY, v => { m.invertY = v; Changed(); });
            UIKit.Toggle(_content, "INVERT X", m.invertX, v => { m.invertX = v; Changed(); });
            Section("FIELD OF VIEW");
            UIKit.SliderRow(_content, "FOV", 70f, 120f, m.fov, "0", v => { m.fov = v; Changed(); PreviewFov(v); }, true);
            Note("HORIZONTAL FOV. THE BACKGROUND PREVIEWS YOUR CHOICE LIVE.");
        }

        private static void PreviewFov(float hfov)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(hfov, cam.aspect);
        }

        // ---- DISPLAY
        private void Display()
        {
            var d = S.display;
            Section("DISPLAY");
            UIKit.Selector(_content, "DISPLAY MODE", new[] { "FULLSCREEN", "BORDERLESS", "WINDOWED" }, (int)d.displayMode, i => d.displayMode = (DisplayModeOption)i);

            var res = DisplaySettingsApplier.GetResolutions();
            int rw, rh, rhz;
            DisplaySettingsApplier.Resolve(d, out rw, out rh, out rhz);
            var resNames = new List<string>();
            int resIdx = 0;
            for (int i = 0; i < res.Count; i++)
            {
                resNames.Add(res[i].Label);
                if (res[i].Width == rw && res[i].Height == rh) resIdx = i;
            }
            OptionSelector rateSel = null;
            UIKit.Selector(_content, "RESOLUTION", resNames, resIdx, i =>
            {
                d.width = res[i].Width;
                d.height = res[i].Height;
                d.refreshRate = 0;
                var r = DisplaySettingsApplier.GetRefreshRates(d.width, d.height);
                rateSel.SetOptions(Hz(r), 0);
            });
            var rates = DisplaySettingsApplier.GetRefreshRates(rw, rh);
            rateSel = UIKit.Selector(_content, "REFRESH RATE", Hz(rates), Mathf.Max(0, rates.IndexOf(rhz)), i =>
            {
                var r = DisplaySettingsApplier.GetRefreshRates(d.width > 0 ? d.width : rw, d.height > 0 ? d.height : rh);
                d.refreshRate = r[Mathf.Clamp(i, 0, r.Count - 1)];
            });
            UIKit.Toggle(_content, "V-SYNC", d.vSync, v => d.vSync = v);
            UIKit.Button(_content, "APPLY DISPLAY CHANGES", ApplyDisplay, UIKit.ButtonStyle.Primary, 16, 44f);
            Note("DISPLAY CHANGES ASK FOR CONFIRMATION AND REVERT AUTOMATICALLY AFTER 15 SECONDS. (THE EDITOR GAME VIEW IGNORES RESOLUTION.)");

            Section("PERFORMANCE");
            int[] limits = { 30, 60, 90, 120, 144, 165, 240, 360, -1, 0 };
            var limitNames = new List<string>();
            foreach (var l in limits) limitNames.Add(l == -1 ? "UNLIMITED" : l == 0 ? "AUTO (" + DisplaySettingsApplier.MonitorRefreshRate() + ")" : l.ToString());
            UIKit.Selector(_content, "FPS LIMIT", limitNames, Mathf.Max(0, Array.IndexOf(limits, d.fpsLimit)), i => { d.fpsLimit = limits[i]; Changed(); });
            int[] menu = { 30, 60, 120, 144, -1 };
            UIKit.Selector(_content, "MENU FPS", new[] { "30", "60", "120", "144", "UNLIMITED" }, Mathf.Max(0, Array.IndexOf(menu, d.menuFps)), i => { d.menuFps = menu[i]; Changed(); });
            int[] bg = { 30, 60, -1 };
            UIKit.Selector(_content, "BACKGROUND FPS", new[] { "30", "60", "UNLIMITED" }, Mathf.Max(0, Array.IndexOf(bg, d.backgroundFps)), i => { d.backgroundFps = bg[i]; Changed(); });
            UIKit.Toggle(_content, "SHOW FPS", d.showFps, v => { d.showFps = v; Changed(); });
            UIKit.Selector(_content, "FPS COUNTER POSITION", new[] { "TOP LEFT", "TOP RIGHT", "BOTTOM LEFT", "BOTTOM RIGHT" }, (int)d.fpsPosition, i => { d.fpsPosition = (ScreenCorner)i; Changed(); });
            UIKit.Toggle(_content, "PERFORMANCE MONITOR", d.showPerformanceMonitor, v => { d.showPerformanceMonitor = v; Changed(); });
        }

        private static List<string> Hz(List<int> rates)
        {
            var l = new List<string>();
            foreach (var r in rates) l.Add(r + " HZ");
            return l;
        }

        private void ApplyDisplay()
        {
            _displayBackup = JsonUtility.FromJson<DisplaySettingsData>(JsonUtility.ToJson(Game.Settings.Current.display));
            // Backup = what the screen is actually running right now.
            _displayBackup.displayMode = Screen.fullScreenMode == FullScreenMode.Windowed ? DisplayModeOption.Windowed
                                       : Screen.fullScreenMode == FullScreenMode.FullScreenWindow ? DisplayModeOption.Borderless : DisplayModeOption.Fullscreen;
            _displayBackup.width = Screen.width;
            _displayBackup.height = Screen.height;
            _displayBackup.refreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            _displayBackup.vSync = QualitySettings.vSyncCount > 0;

            Game.Settings.Apply();
            _confirmTimer = 15f;
            _confirmModal = UIKit.Modal(Host.ModalRoot, "KEEP THESE SETTINGS?", "REVERTING IN:\n\n15",
                ("KEEP", () => { _confirmModal = null; Game.Settings.Save(); }, UIKit.ButtonStyle.Primary),
                ("REVERT", () => { _confirmModal = null; RevertDisplay(); }, UIKit.ButtonStyle.Box));
            foreach (var t in _confirmModal.GetComponentsInChildren<Text>())
                if (t.text.StartsWith("REVERTING")) _confirmText = t;
        }

        private void RevertDisplay()
        {
            if (_displayBackup == null) return;
            var d = Game.Settings.Current.display;
            d.displayMode = _displayBackup.displayMode;
            d.width = _displayBackup.width;
            d.height = _displayBackup.height;
            d.refreshRate = _displayBackup.refreshRate;
            d.vSync = _displayBackup.vSync;
            if (_confirmModal != null) { UnityEngine.Object.Destroy(_confirmModal); _confirmModal = null; }
            Game.Settings.Apply();
            Game.Settings.Save();
            _displayBackup = null;
            if (_tab == 2) BuildTab();
        }

        public override void Tick(float dt)
        {
            if (_confirmModal == null) return;
            _confirmTimer -= Time.unscaledDeltaTime;
            if (_confirmText != null) _confirmText.text = "REVERTING IN:\n\n" + Mathf.CeilToInt(Mathf.Max(0f, _confirmTimer));
            if (_confirmTimer <= 0f) RevertDisplay();
        }

        // ---- GRAPHICS
        private static readonly string[] TierNames = { "OFF", "LOW", "MEDIUM", "HIGH", "ULTRA" };
        private static readonly string[] TierNamesNoOff = { "LOW", "MEDIUM", "HIGH", "ULTRA" };

        private void GraphicsTab()
        {
            var g = S.graphics;
            Section("QUALITY");
            _presetSelector = UIKit.Selector(_content, "QUALITY PRESET", new[] { "VERY LOW", "LOW", "MEDIUM", "HIGH", "ULTRA", "CUSTOM" }, (int)g.preset, i =>
            {
                if (i == (int)QualityPreset.Custom) { g.preset = QualityPreset.Custom; return; }
                Game.Settings.SetGraphicsPreset((QualityPreset)i);
                Changed();
                BuildTab();
            });
            TierNoOff("TEXTURE QUALITY", g.textures, v => g.textures = v);
            Tier("SHADOW QUALITY", g.shadows, v => g.shadows = v);
            TierNoOff("EFFECT QUALITY", g.effects, v => g.effects = v);
            TierNoOff("LIGHTING QUALITY", g.lighting, v => g.lighting = v);
            TierNoOff("POST PROCESSING", g.postProcessing, v => g.postProcessing = v);
            UIKit.Selector(_content, "ANTI-ALIASING", new[] { "OFF", "FXAA", "SMAA", "TAA" }, (int)g.antiAliasing, i => { g.antiAliasing = (AntiAliasingOption)i; Individual(); });
            TierNoOff("VIEW DISTANCE", g.viewDistance, v => g.viewDistance = v);
            TierNoOff("PARTICLES", g.particles, v => g.particles = v);
            Tier("REFLECTIONS", g.reflections, v => g.reflections = v);
            Tier("AMBIENT OCCLUSION", g.ambientOcclusion, v => g.ambientOcclusion = v);
            UIKit.SliderRow(_content, "RENDER SCALE", 0.5f, 1f, g.renderScale, "0%", v => { g.renderScale = v; Individual(); }, false, v => Mathf.RoundToInt(v * 100f) + "%");

            Section("CAMERA EFFECTS");
            UIKit.Toggle(_content, "MOTION BLUR", g.motionBlur, v => { g.motionBlur = v; Changed(); });
            UIKit.Toggle(_content, "DEPTH OF FIELD", g.depthOfField, v => { g.depthOfField = v; Changed(); });
            UIKit.Toggle(_content, "VIGNETTE", g.vignette, v => { g.vignette = v; Changed(); });
            UIKit.Toggle(_content, "BLOOM", g.bloom, v => { g.bloom = v; Changed(); });
            UIKit.SliderRow(_content, "BLOOM INTENSITY", 0f, 1f, g.bloomIntensity, "0%", v => { g.bloomIntensity = v; Changed(); }, false, v => Mathf.RoundToInt(v * 100f) + "%");
            UIKit.SliderRow(_content, "SCREEN SHAKE", 0f, 1f, S.accessibility.screenShake, "0%", v => { S.accessibility.screenShake = v; Changed(); }, false, v => Mathf.RoundToInt(v * 100f) + "%");
            UIKit.Toggle(_content, "COMPETITIVE VISUALS", g.competitiveVisuals, v => { g.competitiveVisuals = v; Changed(); });
            Note("COMPETITIVE VISUALS: MOTION BLUR/DOF OFF, FEWER PARTICLES, LESS SHAKE, LESS CLUTTER. GAMEPLAY INFO IS NEVER REMOVED.");

            Section("TOOLS");
            var tools = UIKit.Row(_content, 44f, 10f);
            var detect = UIKit.Button(tools, "DETECT RECOMMENDED", () => GraphicsDetectPopup.Show(Host), UIKit.ButtonStyle.Box, 15, 42f);
            UIKit.Size(detect, 42f, 280f);
            var bench = UIKit.Button(tools, "RUN BENCHMARK", () => Benchmark.Launch(), UIKit.ButtonStyle.Box, 15, 42f);
            UIKit.Size(bench, 42f, 280f);
        }

        private void Individual()
        {
            Game.Settings.RefreshPresetLabel();
            if (_presetSelector != null) _presetSelector.SetIndexWithoutNotify((int)S.graphics.preset);
            Changed();
        }

        private void Tier(string label, QualityTier value, Action<QualityTier> set)
        {
            UIKit.Selector(_content, label, TierNames, (int)value, i => { set((QualityTier)i); Individual(); });
        }

        private void TierNoOff(string label, QualityTier value, Action<QualityTier> set)
        {
            UIKit.Selector(_content, label, TierNamesNoOff, Mathf.Max(0, (int)value - 1), i => { set((QualityTier)(i + 1)); Individual(); });
        }

        // ---- AUDIO
        private void AudioTab()
        {
            var a = S.audio;
            Func<float, string> pct = v => Mathf.RoundToInt(v * 100f) + "%";
            Section("VOLUME");
            UIKit.SliderRow(_content, "MASTER VOLUME", 0f, 1f, a.master, "0%", v => { a.master = v; Changed(); }, false, pct);
            UIKit.SliderRow(_content, "MUSIC VOLUME", 0f, 1f, a.music, "0%", v => { a.music = v; Changed(); }, false, pct);
            UIKit.SliderRow(_content, "SFX VOLUME", 0f, 1f, a.sfx, "0%", v => { a.sfx = v; Changed(); }, false, pct);
            UIKit.SliderRow(_content, "VOICE VOLUME", 0f, 1f, a.voice, "0%", v => { a.voice = v; Changed(); }, false, pct);
            UIKit.SliderRow(_content, "UI VOLUME", 0f, 1f, a.ui, "0%", v => { a.ui = v; Changed(); Audio.AudioController.PlayUI(Audio.SfxId.UIClick); }, false, pct);
            UIKit.SliderRow(_content, "ANNOUNCER VOLUME", 0f, 1f, a.announcer, "0%", v => { a.announcer = v; Changed(); }, false, pct);
            Section("VOICE CHAT");
            UIKit.SliderRow(_content, "VOICE CHAT VOLUME", 0f, 1f, a.voiceChat, "0%", v => { a.voiceChat = v; Changed(); }, false, pct);
            UIKit.Selector(_content, "VOICE MODE", new[] { "PUSH TO TALK", "OPEN MIC" }, (int)a.voiceMode, i => { a.voiceMode = (VoiceMode)i; Changed(); });
            UIKit.Toggle(_content, "MUTE MICROPHONE", a.micMuted, v => { a.micMuted = v; Changed(); });
            Note("VOICE CHAT (PARTY / TEAM) NEEDS ONLINE SERVICES. SETTINGS ARE SAVED FOR WHEN IT'S CONNECTED.");
        }

        // ---- ACCESSIBILITY
        private void Accessibility()
        {
            var x = S.accessibility;
            Func<float, string> pct = v => Mathf.RoundToInt(v * 100f) + "%";
            Section("VISION");
            UIKit.Selector(_content, "COLORBLIND MODE", new[] { "OFF", "PROTANOPIA", "DEUTERANOPIA", "TRITANOPIA" }, (int)x.colorblind, i => { x.colorblind = (ColorblindMode)i; Changed(); });
            UIKit.SliderRow(_content, "SUBTITLE SIZE", 0.75f, 2f, x.subtitleSize, "0%", v => { x.subtitleSize = v; Changed(); }, false, pct);
            UIKit.Toggle(_content, "SUBTITLE BACKGROUND", x.subtitleBackground, v => { x.subtitleBackground = v; Changed(); });
            UIKit.SliderRow(_content, "HUD SCALE", 0.75f, 1.25f, x.hudScale, "0%", v => { x.hudScale = v; Changed(); }, false, pct);
            Section("FEEDBACK");
            UIKit.Toggle(_content, "HIT MARKERS", x.hitMarkers, v => { x.hitMarkers = v; Changed(); });
            UIKit.SliderRow(_content, "HIT MARKER SIZE", 0.5f, 2f, x.hitMarkerSize, "0%", v => { x.hitMarkerSize = v; Changed(); }, false, pct);
            UIKit.Toggle(_content, "DAMAGE INDICATORS", x.damageIndicators, v => { x.damageIndicators = v; Changed(); });
            UIKit.SliderRow(_content, "SCREEN SHAKE", 0f, 1f, x.screenShake, "0%", v => { x.screenShake = v; Changed(); }, false, pct);
            UIKit.Toggle(_content, "CAMERA EFFECTS (SPEED FOV, TILT)", x.cameraEffects, v => { x.cameraEffects = v; Changed(); });
            UIKit.Toggle(_content, "MOTION BLUR", S.graphics.motionBlur, v => { S.graphics.motionBlur = v; Changed(); });

            Section("CROSSHAIR");
            var c = x.crosshair;
            _crosshairPreview = UIKit.Node("CrosshairPreview", _content);
            UIKit.Size(_crosshairPreview, 120f);
            var bgi = _crosshairPreview.gameObject.AddComponent<Image>();
            bgi.color = new Color(0.25f, 0.27f, 0.3f, 1f);
            DrawCrosshair();
            UIKit.Selector(_content, "TYPE", new[] { "CROSS", "DOT", "CROSS + DOT" }, (int)c.type, i => { c.type = (CrosshairType)i; CrossChanged(); });
            UIKit.SliderRow(_content, "SIZE", 1f, 30f, c.size, "0", v => { c.size = v; CrossChanged(); }, true);
            UIKit.SliderRow(_content, "THICKNESS", 1f, 10f, c.thickness, "0", v => { c.thickness = v; CrossChanged(); }, true);
            UIKit.SliderRow(_content, "GAP", 0f, 30f, c.gap, "0", v => { c.gap = v; CrossChanged(); }, true);
            UIKit.SliderRow(_content, "OPACITY", 0.1f, 1f, c.opacity, "0%", v => { c.opacity = v; CrossChanged(); }, false, pct);
            UIKit.Toggle(_content, "OUTLINE", c.outline, v => { c.outline = v; CrossChanged(); });
            UIKit.Toggle(_content, "CENTER DOT", c.centerDot, v => { c.centerDot = v; CrossChanged(); });
            UIKit.Toggle(_content, "DYNAMIC", c.dynamic, v => { c.dynamic = v; CrossChanged(); });
            var colors = new[] { Color.white, new Color(1f, 0.2f, 0.25f), new Color(0.3f, 1f, 0.4f), new Color(0.2f, 1f, 1f), new Color(1f, 1f, 0.2f), new Color(1f, 0.3f, 1f) };
            int ci = 0;
            for (int i = 0; i < colors.Length; i++) if (Approximately(colors[i], c.color)) ci = i;
            UIKit.Selector(_content, "COLOR", new[] { "WHITE", "RED", "GREEN", "CYAN", "YELLOW", "MAGENTA" }, ci, i => { c.color = colors[i]; CrossChanged(); });
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.05f;
        }

        private void CrossChanged()
        {
            Changed();
            DrawCrosshair();
        }

        private void DrawCrosshair()
        {
            if (_crosshairPreview == null) return;
            for (int i = _crosshairPreview.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_crosshairPreview.GetChild(i).gameObject);
            CrosshairDrawer.Draw(_crosshairPreview, S.accessibility.crosshair, 0f, 1.5f);
        }
    }
}
