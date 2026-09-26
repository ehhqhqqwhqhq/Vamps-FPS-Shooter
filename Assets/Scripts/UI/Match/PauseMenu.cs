using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Match;
using Vamp.Player;
using Vamp.UI.Menus;

namespace Vamp.UI
{
    /// <summary>
    /// In-match menu (ESC): RESUME · SCOREBOARD tip · SETTINGS (the full settings screen) · LEAVE MATCH · QUIT.
    /// Offline matches pause time; online matches would keep running.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour, IScreenHost
    {
        private MatchController _match;
        private Canvas _canvas;
        private RectTransform _content;
        private RectTransform _modal;
        private readonly Stack<MenuScreen> _stack = new Stack<MenuScreen>();

        public bool IsOpen { get; private set; }
        /// <summary>Online matches keep running while the menu is open.</summary>
        public bool KeepTimeRunning;
        /// <summary>Online: leaving goes through the network session.</summary>
        public System.Action LeaveOverride;

        private PlayerController P { get { return _match != null && _match.enabled ? _match.Player : PlayerController.Local; } }
        public RectTransform ContentRoot { get { return _content; } }
        public Transform ModalRoot { get { return _modal; } }

        private void Start()
        {
            _match = GetComponent<MatchController>();
            _canvas = UIFactory.CreateCanvas("Pause Canvas", transform, 200);
            var bg = UIKit.Image(_canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.82f));
            bg.raycastTarget = true;
            UIKit.Stretch(bg.rectTransform);
            _content = UIKit.StretchNode("Content", _canvas.transform);
            _modal = UIKit.StretchNode("Modal", _canvas.transform);
            _canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            var player = P;
            bool esc = player != null ? player.Input.PausePressed : (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame);
            if (!esc) { if (IsOpen && _stack.Count > 0) _stack.Peek().Tick(Time.unscaledDeltaTime); return; }
            if (_modal.childCount > 0) return;
            if (!IsOpen) Open();
            else Back();
        }

        public void Open()
        {
            if (IsOpen || (_match != null && _match.enabled && _match.Phase == MatchPhase.Ended)) return;
            IsOpen = true;
            _canvas.gameObject.SetActive(true);
            if (!KeepTimeRunning) Time.timeScale = 0f;
            InputController.SetCursorLocked(false);
            if (P != null) P.Input.GameplayInputEnabled = false;
            ClearTo(new PauseScreen(this));
        }

        public void Close()
        {
            while (_stack.Count > 0) _stack.Pop().Destroy();
            IsOpen = false;
            _canvas.gameObject.SetActive(false);
            Time.timeScale = 1f;
            if (P != null) P.Input.GameplayInputEnabled = true;
            InputController.SetCursorLocked(true);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        public void Push(MenuScreen screen)
        {
            if (_stack.Count > 0) _stack.Peek().Root.gameObject.SetActive(false);
            screen.Build(this, _content);
            _stack.Push(screen);
            screen.OnShow();
        }

        public void Replace(MenuScreen screen)
        {
            if (_stack.Count > 0) _stack.Pop().Destroy();
            Push(screen);
        }

        public void Back()
        {
            if (_stack.Count == 0) { Close(); return; }
            if (_stack.Peek().OnBack()) return;
            if (_stack.Count <= 1) { Close(); return; }
            _stack.Pop().Destroy();
            _stack.Peek().Root.gameObject.SetActive(true);
            _stack.Peek().OnShow();
        }

        public void ClearTo(MenuScreen screen)
        {
            while (_stack.Count > 0) _stack.Pop().Destroy();
            Push(screen);
        }

        public void LeaveMatch()
        {
            Time.timeScale = 1f;
            if (LeaveOverride != null) LeaveOverride();
            else if (_match != null && _match.enabled) _match.Leave();
            else if (Game.Scenes != null) Game.Scenes.GoToMainMenu();
        }

        /// <summary>The first page of the pause menu.</summary>
        private sealed class PauseScreen : MenuScreen
        {
            private readonly PauseMenu _menu;
            public PauseScreen(PauseMenu menu) { _menu = menu; }

            protected override void OnBuild(RectTransform root)
            {
                var m = MatchController.Instance;
                string sub = m != null ? MatchConfig.ModeName(m.Config.mode) + "  ·  " + m.Map.DisplayName : null;
                var col = Page(root, "PAUSED", sub, 560f, 140f);
                UIKit.Button(col, "RESUME", () => _menu.Close(), UIKit.ButtonStyle.Menu, 32, 58f);
                UIKit.Button(col, "SETTINGS", () => Host.Push(new SettingsScreen()), UIKit.ButtonStyle.Menu, 32, 58f);
                if (m != null && m.Config.mode == GameMode.Training)
                    UIKit.Button(col, "RESTART TRAINING", () => { Time.timeScale = 1f; m.Rematch(); }, UIKit.ButtonStyle.Menu, 32, 58f);
                UIKit.Button(col, "LEAVE MATCH", () =>
                    UIKit.Modal(Host.ModalRoot, "LEAVE MATCH?", m != null && m.Config.mode != GameMode.Training ? "YOU WON'T EARN XP FOR AN UNFINISHED MATCH." : "",
                        ("LEAVE", () => _menu.LeaveMatch(), UIKit.ButtonStyle.Danger), ("STAY", null, UIKit.ButtonStyle.Box)),
                    UIKit.ButtonStyle.Menu, 32, 58f);
                UIKit.Button(col, "QUIT GAME", () =>
                    UIKit.Modal(Host.ModalRoot, "QUIT VAMP?", "", ("QUIT", WelcomeScreen.Quit, UIKit.ButtonStyle.Danger), ("CANCEL", null, UIKit.ButtonStyle.Box)),
                    UIKit.ButtonStyle.Menu, 32, 58f);
                UIKit.Spacer(col, 20f);
                UIKit.Caption(col, "HOLD TAB FOR THE SCOREBOARD  ·  ESC TO RESUME", 13);
            }

            public override bool ShowTopBar { get { return false; } }
        }
    }
}
