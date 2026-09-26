using UnityEngine;

namespace Vamp.Movement
{
    /// <summary>
    /// Every movement tuning value lives here. Nothing in the movement code hard-codes feel numbers.
    /// Tip: edit this asset while in Play Mode - ScriptableObject changes persist, so you can tune live.
    /// Units are metres, seconds, m/s and m/s^2.
    /// Custom Games can later create a runtime copy (Instantiate) and scale gravity/speeds per match;
    /// the server will own that copy.
    /// </summary>
    [CreateAssetMenu(menuName = "VAMP/Movement Settings", fileName = "MovementSettings")]
    public sealed class MovementSettings : ScriptableObject
    {
        [Header("Ground")]
        public float walkSpeed = 7.5f;
        public float sprintSpeed = 11f;
        public float crouchSpeed = 4f;
        [Tooltip("Quake-style ground acceleration. Higher = snappier. accel per second = value * targetSpeed.")]
        public float groundAcceleration = 12f;
        [Tooltip("Ground friction (fraction of speed removed per second, scaled by stopSpeed floor).")]
        public float groundFriction = 7f;
        [Tooltip("Below this speed, friction behaves as if moving at this speed so you stop crisply (deceleration).")]
        public float stopSpeed = 3f;
        [Tooltip("Extra braking when no movement input is held.")]
        public float groundDeceleration = 4f;
        [Tooltip("Friction is skipped for this long after landing, so jump/slide chains keep momentum (skill window).")]
        public float landingFrictionGrace = 0.09f;
        [Tooltip("Sprint only applies when forward input is at least this.")]
        [Range(0f, 1f)] public float sprintMinForwardInput = 0.3f;

        [Header("Air")]
        [Tooltip("Air acceleration used for air strafing. Higher = easier to gain speed by strafing.")]
        public float airAcceleration = 40f;
        [Tooltip("Max speed that can be added along the wish direction per tick in air. Small value = classic air strafing.")]
        public float airWishSpeedCap = 1.4f;
        [Tooltip("Degrees/second the velocity turns toward the look direction when holding only forward in air.")]
        public float airControlTurnRate = 70f;
        [Tooltip("Plain air acceleration toward wish direction when below walk speed (lets you move after standing jumps).")]
        public float airLowSpeedAcceleration = 10f;

        [Header("Gravity & Jump")]
        public float gravity = 24f;
        [Tooltip("Gravity multiplier while falling. >1 removes floatiness.")]
        public float fallGravityMultiplier = 1.25f;
        public float jumpForce = 7.6f;
        public float coyoteTime = 0.1f;
        [Tooltip("Also used to buffer dash / wall-jump presses so inputs a few ms early still count.")]
        public float jumpBufferTime = 0.12f;
        [Tooltip("Fraction of existing upward velocity (e.g. running up a ramp) kept when jumping.")]
        [Range(0f, 1f)] public float upwardVelocityCarryOnJump = 0.5f;
        public float maxFallSpeed = 55f;

        [Header("Speed Caps")]
        [Tooltip("Absolute horizontal speed cap. Momentum can build up to here.")]
        public float maxHorizontalSpeed = 40f;

        [Header("Crouch / Capsule")]
        public float standingHeight = 1.8f;
        public float crouchHeight = 1.0f;
        public float capsuleRadius = 0.4f;
        public float crouchTransitionSpeed = 10f;
        [Tooltip("Eye position below top of capsule.")]
        public float eyeOffsetFromTop = 0.15f;

        [Header("Ground Detection")]
        public LayerMask environmentMask = Physics.DefaultRaycastLayers;
        public float groundCheckDistance = 0.12f;
        [Range(0f, 89f)] public float maxGroundAngle = 50f;
        [Tooltip("Downward displacement applied while grounded so you stick to slopes and stairs.")]
        public float groundSnapDistance = 0.3f;

        [Header("Slide")]
        public float slideMinStartSpeed = 6.5f;
        [Tooltip("Speed added when a slide starts (only if the boost is off cooldown).")]
        public float slideBoost = 4f;
        public float slideBoostCooldown = 1.1f;
        [Tooltip("Speed the slide boost cannot push you above.")]
        public float slideBoostMaxSpeed = 18f;
        public float slideFriction = 0.55f;
        [Tooltip("Downhill acceleration while sliding (multiplied by slope steepness).")]
        public float slideSlopeAcceleration = 22f;
        public float slideEndSpeed = 3.5f;
        public float slideSteerRate = 60f;
        [Tooltip("If you land while holding crouch/slide and are fast enough, you enter a slide.")]
        public bool slideOnLandWhileCrouching = true;

        [Header("Wall Run")]
        [Tooltip("If true, only colliders with a WallRunSurface component can be wall-run (designated surfaces).")]
        public bool requireWallRunSurface = true;
        public float wallCheckDistance = 0.55f;
        public float wallRunMinHeight = 1.0f;
        public float wallRunMinSpeed = 6f;
        [Tooltip("Speed the wall run accelerates you up to (does not slow you if faster).")]
        public float wallRunSpeed = 12.5f;
        public float wallRunAcceleration = 10f;
        public float wallRunGravity = 5f;
        [Tooltip("Vertical speed is clamped to at least this when a wall run begins (small lift on entry).")]
        public float wallRunEntryMinUpSpeed = 1.5f;
        public float wallRunMaxFallSpeed = 3f;
        public float wallRunMaxDuration = 1.75f;
        public float wallRunStickForce = 3f;
        [Tooltip("Must be holding forward to start/keep a wall run.")]
        public bool wallRunRequiresForwardInput = true;
        [Range(0f, 45f)] public float maxWallAngleFromVertical = 20f;
        public bool wallRunRefreshesAirDash = true;

        [Header("Wall Jump")]
        public float wallJumpUpForce = 7.5f;
        public float wallJumpOutForce = 7f;
        [Tooltip("How much movement input steers the wall-jump direction (0 = straight off the wall).")]
        [Range(0f, 1f)] public float wallJumpInputInfluence = 0.55f;
        [Tooltip("Speed added along the look direction on wall jump.")]
        public float wallJumpForwardBoost = 1.5f;
        [Tooltip("After jumping off a wall you cannot re-run / re-jump the SAME wall for this long (prevents climbing one wall).")]
        public float sameWallLockout = 0.6f;
        public float wallJumpCooldown = 0.15f;
        [Tooltip("Allow wall jumps when touching a wall in the air without wall running.")]
        public bool allowWallJumpWithoutWallRun = true;

        [Header("Dash")]
        public float dashSpeed = 24f;
        public float dashDuration = 0.14f;
        [Tooltip("Fraction of dash speed kept when the dash ends (never below your pre-dash speed in that direction).")]
        [Range(0f, 1f)] public float dashExitSpeedFraction = 0.6f;
        public int dashMaxCharges = 2;
        [Tooltip("Seconds to recharge one dash charge (the 'energy' of the dash).")]
        public float dashRechargeTime = 1.8f;
        [Tooltip("Minimum time between dashes.")]
        public float dashCooldown = 0.35f;
        public int airDashesPerAirtime = 1;
        public bool dashIgnoresGravity = true;
        [Tooltip("Lets you jump out of a grounded dash early (keeps the dash exit speed).")]
        public bool allowDashJump = true;

        [Header("External Forces (Rocket Jump / Knockback)")]
        public float externalImpulseMultiplier = 1f;
        [Tooltip("After an upward impulse you can't be re-grounded for this long (so explosions actually launch you).")]
        public float impulseUngroundTime = 0.15f;

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed);
            crouchHeight = Mathf.Clamp(crouchHeight, capsuleRadius * 2f, standingHeight);
            dashMaxCharges = Mathf.Max(0, dashMaxCharges);
            airDashesPerAirtime = Mathf.Max(0, airDashesPerAirtime);
            maxHorizontalSpeed = Mathf.Max(sprintSpeed, maxHorizontalSpeed);
            gravity = Mathf.Max(0f, gravity);
        }
    }
}
