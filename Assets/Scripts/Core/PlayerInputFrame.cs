using UnityEngine;

namespace Vamp.Core
{
    /// <summary>
    /// One frame of sampled player intent. Gameplay systems only ever read this struct,
    /// never the devices directly. In the multiplayer phase this is the unit that gets
    /// sent to the server and replayed for client prediction / reconciliation.
    /// </summary>
    public struct PlayerInputFrame
    {
        public Vector2 Move;          // x = strafe, y = forward. Normalised to max length 1.
        public Vector2 LookDelta;     // raw mouse counts this frame (sensitivity is applied by CameraController)

        public bool JumpPressed;
        public bool JumpHeld;
        public bool CrouchPressed;
        public bool CrouchHeld;
        public bool SprintHeld;
        public bool SlidePressed;
        public bool SlideHeld;
        public bool DashPressed;
        public bool WallJumpPressed;

        public bool FirePressed;
        public bool FireHeld;
        public bool AimHeld;
        public bool ReloadPressed;
        public bool InteractPressed;

        /// <summary>-1 = no switch requested, otherwise loadout slot index (0 = primary, 1 = secondary, 2 = melee/third).</summary>
        public int WeaponSlotPressed;

        public bool ScoreboardHeld;
        public bool VoiceHeld;

        public static PlayerInputFrame Empty
        {
            get
            {
                var f = new PlayerInputFrame();
                f.WeaponSlotPressed = -1;
                return f;
            }
        }
    }
}
