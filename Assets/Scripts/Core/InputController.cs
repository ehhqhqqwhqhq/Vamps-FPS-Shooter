using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vamp.Core
{
    /// <summary>
    /// Owns the Input System action asset, samples a <see cref="PlayerInputFrame"/> every frame,
    /// and exposes a rebinding service (with conflict detection) for the settings UI.
    ///
    /// Nothing else in the project should read keyboard/mouse devices directly.
    /// Runs early so gameplay reads a fresh frame the same Update it was sampled (lowest latency).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class InputController : MonoBehaviour
    {
        private float _nextCycle;
        // Local persistence for now. In the Settings/Account phase this JSON moves into the
        // account-synced settings blob ("keybinds") and PlayerPrefs becomes the offline fallback.
        public const string BindingOverridesPrefsKey = "vamp.input.bindingOverrides";

        [Tooltip("Lock + hide the cursor when play starts.")]
        [SerializeField] private bool lockCursorOnStart = true;

        [Tooltip("Enable developer bindings (F1 debug overlay, Esc cursor toggle).")]
        [SerializeField] private bool enableDebugBindings = true;

        public InputActionAsset Asset { get; private set; }
        public PlayerInputFrame Frame { get; private set; } = PlayerInputFrame.Empty;

        /// <summary>When false, gameplay receives an empty frame (menus open, dead, rebinding, etc.).</summary>
        public bool GameplayInputEnabled { get; set; } = true;

        /// <summary>Countdown / intro: you can look around but not move, shoot or use abilities.</summary>
        public bool MovementLocked { get; set; }

        public bool MovementDebugTogglePressed { get; private set; }
        /// <summary>Esc this frame (pause menu). Always read, even when gameplay input is disabled.</summary>
        public bool PausePressed { get; private set; }

        public event Action BindingsChanged;

        private InputActionMap _gameplay;
        private InputActionMap _debug;
        private InputAction _move, _look, _jump, _crouch, _sprint, _slide, _dash, _wallJump;
        private InputAction _inspect;
        private InputAction _fire, _aim, _reload, _interact, _primary, _secondary, _melee, _scoreboard, _voice;
        private InputAction _toggleDebug, _toggleCursor, _pause;
        private bool _crouchToggled, _aimToggled;
        private bool _toggleCrouch, _toggleAim;
        private InputActionRebindingExtensions.RebindingOperation _activeRebind;

        private void Awake()
        {
            Asset = VampInputActions.CreateDefault();
            LoadBindingOverrides();

            _gameplay = Asset.FindActionMap(VampInputActions.GameplayMap, true);
            _debug = Asset.FindActionMap(VampInputActions.DebugMap, true);

            _move = _gameplay.FindAction(VampInputActions.Move, true);
            _look = _gameplay.FindAction(VampInputActions.Look, true);
            _jump = _gameplay.FindAction(VampInputActions.Jump, true);
            _crouch = _gameplay.FindAction(VampInputActions.Crouch, true);
            _sprint = _gameplay.FindAction(VampInputActions.Sprint, true);
            _slide = _gameplay.FindAction(VampInputActions.Slide, true);
            _dash = _gameplay.FindAction(VampInputActions.Dash, true);
            _wallJump = _gameplay.FindAction(VampInputActions.WallJump, true);
            _fire = _gameplay.FindAction(VampInputActions.Fire, true);
            _aim = _gameplay.FindAction(VampInputActions.Aim, true);
            _reload = _gameplay.FindAction(VampInputActions.Reload, true);
            _inspect = _gameplay.FindAction(VampInputActions.Inspect, true);
            _interact = _gameplay.FindAction(VampInputActions.Interact, true);
            _primary = _gameplay.FindAction(VampInputActions.PrimaryWeapon, true);
            _secondary = _gameplay.FindAction(VampInputActions.SecondaryWeapon, true);
            _melee = _gameplay.FindAction(VampInputActions.Melee, true);
            _scoreboard = _gameplay.FindAction(VampInputActions.Scoreboard, true);
            _voice = _gameplay.FindAction(VampInputActions.VoiceChat, true);

            _pause = _gameplay.FindAction(VampInputActions.Pause, true);
            _toggleDebug = _debug.FindAction(VampInputActions.ToggleMovementDebug, true);
            _toggleCursor = _debug.FindAction(VampInputActions.ToggleCursor, true);
        }

        private void OnEnable()
        {
            if (Asset == null) return;
            if (Game.Settings != null) Game.Settings.Changed += OnSettingsChanged;
            _gameplay.Enable();
            if (enableDebugBindings) _debug.Enable();
            if (lockCursorOnStart) SetCursorLocked(true);
        }

        private void OnDisable()
        {
            CancelRebind();
            if (Game.Settings != null) Game.Settings.Changed -= OnSettingsChanged;
            if (Asset != null) Asset.Disable();
        }

        private void OnDestroy()
        {
            _activeRebind?.Dispose();
            if (Asset != null) Destroy(Asset);
        }

        private void Update()
        {
            MovementDebugTogglePressed = enableDebugBindings && _toggleDebug.WasPressedThisFrame();
            PausePressed = _pause.WasPressedThisFrame();
#if UNITY_EDITOR
            // The Unity editor swallows Esc while the cursor is locked (it uses it to release the mouse). P works in the editor.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.pKey.wasPressedThisFrame) PausePressed = true;
#endif
            if (enableDebugBindings && _toggleCursor.WasPressedThisFrame() && _activeRebind == null)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            // Mouse released (e.g. Esc) => don't feed look/fire into gameplay.
            bool cursorFree = Cursor.lockState != CursorLockMode.Locked;
            if (!GameplayInputEnabled || _activeRebind != null)
            {
                var empty = PlayerInputFrame.Empty;
                empty.ScoreboardHeld = _scoreboard.IsPressed();
                Frame = empty;
                return;
            }

            var f = PlayerInputFrame.Empty;
            f.Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            f.LookDelta = cursorFree ? Vector2.zero : _look.ReadValue<Vector2>();

            f.JumpPressed = _jump.WasPressedThisFrame();
            f.JumpHeld = _jump.IsPressed();
            f.CrouchPressed = _crouch.WasPressedThisFrame();
            if (_toggleCrouch)
            {
                if (f.CrouchPressed) _crouchToggled = !_crouchToggled;
                if (f.CrouchPressed && !_crouchToggled) f.CrouchPressed = false;
                f.CrouchHeld = _crouchToggled;
            }
            else f.CrouchHeld = _crouch.IsPressed();
            f.SprintHeld = _sprint.IsPressed();
            f.SlidePressed = _slide.WasPressedThisFrame();
            f.SlideHeld = _slide.IsPressed();
            f.DashPressed = _dash.WasPressedThisFrame();
            f.WallJumpPressed = _wallJump.WasPressedThisFrame();

            f.FirePressed = !cursorFree && _fire.WasPressedThisFrame();
            f.FireHeld = !cursorFree && _fire.IsPressed();
            if (_toggleAim)
            {
                if (_aim.WasPressedThisFrame()) _aimToggled = !_aimToggled;
                f.AimHeld = !cursorFree && _aimToggled;
            }
            else f.AimHeld = !cursorFree && _aim.IsPressed();
            f.ReloadPressed = _reload.WasPressedThisFrame();
            f.InspectPressed = !cursorFree && _inspect.WasPressedThisFrame();
            f.InteractPressed = _interact.WasPressedThisFrame();

            if (_primary.WasPressedThisFrame()) f.WeaponSlotPressed = 0;
            else if (_secondary.WasPressedThisFrame()) f.WeaponSlotPressed = 1;
            else if (_melee.WasPressedThisFrame()) f.WeaponSlotPressed = 2;
            // Mouse wheel: scroll down = next weapon, up = previous (one step per notch burst).
            var mouse = Mouse.current;
            if (!cursorFree && mouse != null && f.WeaponSlotPressed < 0 && Time.unscaledTime >= _nextCycle)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    f.WeaponCycle = wheel < 0f ? 1 : -1;
                    _nextCycle = Time.unscaledTime + 0.12f;
                }
            }

            f.ScoreboardHeld = _scoreboard.IsPressed();
            f.VoiceHeld = _voice.IsPressed();

            if (MovementLocked)
            {
                var locked = PlayerInputFrame.Empty;
                locked.LookDelta = f.LookDelta;
                locked.ScoreboardHeld = f.ScoreboardHeld;
                f = locked;
            }

            Frame = f;
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ------------------------------------------------------------------ Rebinding service

        /// <summary>Result of an interactive rebind, handed to the UI so it can show the KEY CONFLICT prompt.</summary>
        public sealed class RebindCandidate
        {
            public InputAction Action;
            public int BindingIndex;
            public string NewPath;
            public List<BindingRef> Conflicts = new List<BindingRef>();
            public bool HasConflicts { get { return Conflicts.Count > 0; } }
        }

        public struct BindingRef
        {
            public InputAction Action;
            public int BindingIndex;
            public string DisplayName { get { return Action.name; } }
        }

        public bool IsRebinding { get { return _activeRebind != null; } }

        /// <summary>
        /// Starts listening for a key/mouse button ("PRESS A KEY"). Nothing is applied yet: the candidate is
        /// returned via <paramref name="onCandidate"/> and the UI must call <see cref="ApplyRebind"/>
        /// (optionally replacing conflicts) or simply drop it to cancel. Esc cancels.
        /// </summary>
        public void StartRebind(string actionName, int bindingIndex, Action<RebindCandidate> onCandidate, Action onCancelled)
        {
            CancelRebind();
            var action = Asset.FindAction(actionName, true);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                Debug.LogWarning("[VAMP] Invalid binding index " + bindingIndex + " for " + actionName);
                if (onCancelled != null) onCancelled();
                return;
            }

            bool wasEnabled = action.enabled;
            action.Disable();

            _activeRebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                // Take over "apply" so we can show a conflict prompt instead of silently stealing binds.
                .OnApplyBinding((op, path) =>
                {
                    var candidate = new RebindCandidate { Action = action, BindingIndex = bindingIndex, NewPath = path };
                    FindConflicts(path, action, bindingIndex, candidate.Conflicts);
                    if (onCandidate != null) onCandidate(candidate);
                })
                .OnComplete(op => FinishRebind(action, wasEnabled))
                .OnCancel(op =>
                {
                    FinishRebind(action, wasEnabled);
                    if (onCancelled != null) onCancelled();
                })
                .Start();
        }

        public void CancelRebind()
        {
            if (_activeRebind != null) _activeRebind.Cancel();
        }

        /// <summary>Applies a candidate. If it has conflicts and <paramref name="replaceConflicts"/> is false, nothing changes.</summary>
        public bool ApplyRebind(RebindCandidate candidate, bool replaceConflicts)
        {
            if (candidate == null || candidate.Action == null) return false;
            if (candidate.HasConflicts && !replaceConflicts) return false;

            if (candidate.HasConflicts)
            {
                // The user explicitly chose REPLACE: clear the old binding(s). Never done silently.
                foreach (var c in candidate.Conflicts)
                    c.Action.ApplyBindingOverride(c.BindingIndex, string.Empty);
            }

            candidate.Action.ApplyBindingOverride(candidate.BindingIndex, candidate.NewPath);
            SaveBindingOverrides();
            return true;
        }

        public void FindConflicts(string path, InputAction ignoreAction, int ignoreIndex, List<BindingRef> results)
        {
            if (string.IsNullOrEmpty(path)) return;
            foreach (var map in Asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    var bindings = action.bindings;
                    for (int i = 0; i < bindings.Count; i++)
                    {
                        if (bindings[i].isComposite) continue;
                        if (action == ignoreAction && i == ignoreIndex) continue;
                        if (string.Equals(bindings[i].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                            results.Add(new BindingRef { Action = action, BindingIndex = i });
                    }
                }
            }
        }

        public string GetBindingDisplayString(string actionName, int bindingIndex)
        {
            var action = Asset.FindAction(actionName, false);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "-";
            string s = action.GetBindingDisplayString(bindingIndex);
            return string.IsNullOrEmpty(s) ? "UNBOUND" : s.ToUpperInvariant();
        }

        /// <summary>RESET TO DEFAULTS (the UI is responsible for the confirmation prompt).</summary>
        public void ResetAllBindingsToDefault()
        {
            Asset.RemoveAllBindingOverrides();
            SaveBindingOverrides();
        }

        private void FinishRebind(InputAction action, bool reenable)
        {
            if (_activeRebind != null)
            {
                _activeRebind.Dispose();
                _activeRebind = null;
            }
            if (reenable) action.Enable();
        }

        private void SaveBindingOverrides()
        {
            string json = KeybindEditor.Serialize(Asset);
            if (Game.Settings != null)
            {
                Game.Settings.Current.controls.keybindsJson = json;
                Game.Settings.Save();
            }
            PlayerPrefs.SetString(BindingOverridesPrefsKey, json);
            PlayerPrefs.Save();
            if (BindingsChanged != null) BindingsChanged();
        }

        private void OnSettingsChanged(Settings.GameSettings s)
        {
            _toggleCrouch = s.controls.toggleCrouch;
            _toggleAim = s.controls.toggleAim;
            if (!_toggleCrouch) _crouchToggled = false;
            if (!_toggleAim) _aimToggled = false;
            if (_activeRebind != null) return;
            Asset.RemoveAllBindingOverrides();
            LoadBindingOverrides();
        }

        private void LoadBindingOverrides()
        {
            string json = Game.Settings != null ? Game.Settings.Current.controls.keybindsJson : PlayerPrefs.GetString(BindingOverridesPrefsKey, string.Empty);
            if (Game.Settings != null)
            {
                _toggleCrouch = Game.Settings.Current.controls.toggleCrouch;
                _toggleAim = Game.Settings.Current.controls.toggleAim;
            }
            KeybindEditor.Load(Asset, json);
        }
    }
}
