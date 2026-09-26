using UnityEngine;
using UnityEngine.InputSystem;

namespace Vamp.Core
{
    /// <summary>
    /// Builds VAMP's default Input System action asset in code so the defaults live in exactly one place.
    /// Player rebinds are stored as binding *overrides* on top of these defaults (see InputController),
    /// which is what makes "Reset to defaults" trivial and safe.
    ///
    /// Action names are the contract used by gameplay, the rebinding UI and saved settings. Do not rename lightly.
    /// </summary>
    public static class VampInputActions
    {
        public const string GameplayMap = "Gameplay";
        public const string DebugMap = "Debug";

        public const string Move = "Move";
        public const string Look = "Look";
        public const string Jump = "Jump";
        public const string Crouch = "Crouch";
        public const string Sprint = "Sprint";
        public const string Slide = "Slide";
        public const string Dash = "Dash";
        public const string WallJump = "WallJump";
        public const string Fire = "Fire";
        public const string Aim = "Aim";
        public const string Reload = "Reload";
        public const string Interact = "Interact";
        public const string DropWeapon = "DropWeapon";
        public const string PrimaryWeapon = "PrimaryWeapon";
        public const string SecondaryWeapon = "SecondaryWeapon";
        public const string Melee = "Melee";
        public const string Scoreboard = "Scoreboard";
        public const string VoiceChat = "VoiceChat";

        public const string Pause = "Pause";
        public const string ToggleMovementDebug = "ToggleMovementDebug";
        public const string ToggleCursor = "ToggleCursor";

        public static InputActionAsset CreateDefault()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "VampInput";

            var map = asset.AddActionMap(GameplayMap);

            var move = map.AddAction(Move, InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            var look = map.AddAction(Look, InputActionType.PassThrough, "<Mouse>/delta");
            look.expectedControlType = "Vector2";

            // Jump has a secondary bind by default (Mouse 4) to demonstrate / support multiple binds per action.
            Btn(map, Jump, "<Keyboard>/space", "<Mouse>/backButton");

            Btn(map, Crouch, "<Keyboard>/leftCtrl");
            Btn(map, Sprint, "<Keyboard>/leftShift");
            Btn(map, Slide, "<Keyboard>/c");
            Btn(map, WallJump, "<Keyboard>/v");
            Btn(map, Dash, "<Keyboard>/q");

            Btn(map, Fire, "<Mouse>/leftButton");
            Btn(map, Aim, "<Mouse>/rightButton");
            Btn(map, Reload, "<Keyboard>/r");
            Btn(map, Interact, "<Keyboard>/e");
            Btn(map, DropWeapon, "<Keyboard>/g");

            Btn(map, PrimaryWeapon, "<Keyboard>/1");
            Btn(map, SecondaryWeapon, "<Keyboard>/2");
            Btn(map, Melee, "<Keyboard>/3");

            Btn(map, Scoreboard, "<Keyboard>/tab");
            Btn(map, VoiceChat, "<Keyboard>/t");

            // Pause is fixed to Escape (not rebindable) so players can always reach the menu.
            map.AddAction(Pause, InputActionType.Button, "<Keyboard>/escape");

            // Developer bindings live in their own map so they can be stripped / disabled in release builds.
            var debug = asset.AddActionMap(DebugMap);
            debug.AddAction(ToggleMovementDebug, InputActionType.Button, "<Keyboard>/f1");
            debug.AddAction(ToggleCursor, InputActionType.Button, "<Keyboard>/f10");

            return asset;
        }

        /// <summary>Every button action gets a PRIMARY and a SECONDARY binding slot (secondary may be empty).</summary>
        private static void Btn(InputActionMap map, string name, string primary, string secondary = "")
        {
            var a = map.AddAction(name, InputActionType.Button, primary);
            a.AddBinding(secondary);
        }

        /// <summary>Rebindable actions shown in SETTINGS ▸ CONTROLS: (display name, action, binding index).
        /// Movement keys are the parts of the Move composite (indices 1-4).</summary>
        public static readonly (string label, string action, int index, bool hasSecondary)[] Rebindable =
        {
            ("MOVE FORWARD", Move, 1, false), ("MOVE BACKWARD", Move, 2, false), ("MOVE LEFT", Move, 3, false), ("MOVE RIGHT", Move, 4, false),
            ("JUMP", Jump, 0, true), ("CROUCH", Crouch, 0, true), ("SPRINT", Sprint, 0, true), ("SLIDE", Slide, 0, true),
            ("WALL JUMP", WallJump, 0, true), ("DASH", Dash, 0, true), ("FIRE", Fire, 0, true), ("AIM", Aim, 0, true),
            ("RELOAD", Reload, 0, true), ("PRIMARY WEAPON", PrimaryWeapon, 0, true), ("SECONDARY WEAPON", SecondaryWeapon, 0, true),
            ("MELEE", Melee, 0, true), ("INTERACT", Interact, 0, true), ("DROP WEAPON", DropWeapon, 0, true),
            ("SCOREBOARD", Scoreboard, 0, true), ("PUSH TO TALK", VoiceChat, 0, true),
        };

        public static string LabelFor(string action)
        {
            foreach (var r in Rebindable) if (r.action == action && r.index == 0) return r.label;
            if (action == Move) return "MOVEMENT";
            return action.ToUpperInvariant();
        }
    }
}
