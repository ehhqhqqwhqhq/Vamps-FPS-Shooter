using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Settings;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// Main menu scene controller: builds the canvas, the animated 3D background, the persistent top bar
    /// (player card + XP bar + social panel), and manages the screen stack with fade transitions.
    /// Flow: not logged in → WELCOME; logged in → HOME. First launch shows GRAPHICS DETECTED.
    /// </summary>
    public sealed class MenuController : MonoBehaviour, IScreenHost
    {
        private Canvas _canvas;
        private RectTransform _content;
        private RectTransform _modal;
        private TopBar _topBar;
        private SocialPanel _social;
        private CanvasGroup _contentGroup;
        private readonly Stack<MenuScreen> _stack = new Stack<MenuScreen>();
        private float _fade = 1f;

        public RectTransform ContentRoot { get { return _content; } }
        public Transform ModalRoot { get { return _modal; } }

        public static MenuController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            UIKit.EnsureEventSystem();

            if (FindAnyObjectByType<MenuBackground>() == null) gameObject.AddComponent<MenuBackground>();

            _canvas = UIFactory.CreateCanvas("Menu Canvas", transform, 50);
            var root = _canvas.transform;

            var vignette = UIKit.Image(root, "Shade", new Color(0f, 0f, 0f, 0.35f));
            UIKit.Stretch(vignette.rectTransform);
            var leftShade = UIKit.Image(root, "LeftShade", new Color(0f, 0f, 0f, 0.55f));
            var lrt = leftShade.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.55f, 1f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            _content = UIKit.StretchNode("Content", root);
            _contentGroup = _content.gameObject.AddComponent<CanvasGroup>();

            _topBar = new TopBar(root, this);
            _social = new SocialPanel(root, this);

            _modal = UIKit.StretchNode("Modals", root);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _topBar.Dispose();
            _social.Dispose();
        }

        private void Start()
        {
            if (Game.Party != null) Game.Party.SetState(Game.IsLoggedIn ? Social.LobbyState.Party : Social.LobbyState.Offline);
            if (Game.IsLoggedIn) ClearTo(new HomeScreen());
            else ClearTo(new WelcomeScreen());
            // Back from an online match: return straight to the lobby.
            if (Game.IsLoggedIn && Game.Online != null && Game.Online.InSession) Push(new OnlineLobbyScreen());
            if (Game.Online != null) UpdateChecker.CheckOnce();

            if (Game.Settings != null && !Game.Settings.Current.firstLaunchDone) GraphicsDetectPopup.Show(this);

            var b = Graphics.Benchmark.Last;
            if (b.Valid)
            {
                Graphics.Benchmark.Last = default(Graphics.BenchmarkResult);
                string rec = Graphics.GraphicsDetector.PresetLabel(b.Recommended);
                UIKit.Modal(_modal, "BENCHMARK COMPLETE",
                    "AVERAGE FPS\n" + Mathf.RoundToInt(b.Average) + "\n\n1% LOW\n" + Mathf.RoundToInt(b.OnePercentLow) + "\n\nMAX FPS\n" + Mathf.RoundToInt(b.Max) + "\n\nRECOMMENDED PRESET\n" + rec,
                    ("APPLY " + rec, () => { Game.Settings.SetGraphicsPreset(b.Recommended); Game.Settings.Apply(); Game.Settings.Save(); }, UIKit.ButtonStyle.Primary),
                    ("KEEP CURRENT", null, UIKit.ButtonStyle.Box));
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _fade = Mathf.MoveTowards(_fade, 1f, dt * 6f);
            _contentGroup.alpha = _fade;

            bool showBars = _stack.Count > 0 && _stack.Peek().ShowTopBar && Game.IsLoggedIn;
            _topBar.SetVisible(showBars);
            _social.SetVisible(showBars);
            _topBar.Tick(dt);

            if (_stack.Count > 0) _stack.Peek().Tick(dt);

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && _modal.childCount == 0) Back();
        }

        // ------------------------------------------------------------------ IScreenHost

        public void Push(MenuScreen screen)
        {
            if (_stack.Count > 0) _stack.Peek().Root.gameObject.SetActive(false);
            screen.Build(this, _content);
            _stack.Push(screen);
            screen.OnShow();
            _fade = 0f;
        }

        public void Replace(MenuScreen screen)
        {
            if (_stack.Count > 0) _stack.Pop().Destroy();
            Push(screen);
        }

        public void Back()
        {
            if (_stack.Count == 0) return;
            if (_stack.Peek().OnBack()) return;
            if (_stack.Count <= 1) return;
            _stack.Pop().Destroy();
            var top = _stack.Peek();
            top.Root.gameObject.SetActive(true);
            top.OnShow();
            _fade = 0f;
        }

        public void ClearTo(MenuScreen screen)
        {
            while (_stack.Count > 0) _stack.Pop().Destroy();
            Push(screen);
        }

        public void RefreshTopBar()
        {
            _topBar.Refresh();
            _social.Refresh();
        }
    }
}
