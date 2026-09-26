using System;
using UnityEngine;
using Vamp.Core;

namespace Vamp.Movement
{
    /// <summary>
    /// VAMP's movement motor. Kinematic (CharacterController based) with Quake/Source-style
    /// ground friction/acceleration and air strafing, plus pluggable abilities (slide, wall run, dash).
    ///
    /// Design notes:
    /// - All feel values come from <see cref="MovementSettings"/>.
    /// - Simulation is driven explicitly through <see cref="Simulate"/> by PlayerController, with an
    ///   input frame + dt. This is deliberate: in the multiplayer phase the exact same function is run
    ///   by the server (authoritative) and by the client (prediction / reconciliation replay).
    /// - This component has no Update of its own.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class MovementController : MonoBehaviour
    {
        private const float JumpGroundLockTime = 0.1f;   // engine guard, not a feel value
        private const float MinMoveSqr = 0.000001f;

        [SerializeField] private MovementSettings settings;

        public MovementSettings Settings { get { return settings; } }
        public CharacterController Controller { get; private set; }

        private Vector3 _velocity;
        public Vector3 Velocity { get { return _velocity; } set { _velocity = value; } }
        public Vector3 HorizontalVelocity { get { return new Vector3(_velocity.x, 0f, _velocity.z); } }
        public float HorizontalSpeed { get { return HorizontalVelocity.magnitude; } }
        public float Speed { get { return _velocity.magnitude; } }

        public bool IsGrounded { get; private set; }
        public bool JustLanded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public MovementState State { get; private set; }
        public float CurrentHeight { get; private set; }
        public bool IsCrouching { get { return CurrentHeight < settings.standingHeight - 0.01f; } }
        public float EyeHeight { get { return CurrentHeight - settings.eyeOffsetFromTop; } }
        public float AirTime { get; private set; }
        public float PeakSpeed { get; private set; }
        public int AirDashesUsed { get; set; }

        /// <summary>Set by abilities that need a crouched capsule (slide).</summary>
        public bool ForceCrouch { get; set; }

        public PlayerInputFrame LastInput { get; private set; }

        public event Action<MovementEvent> MovementEventRaised;

        private MovementAbility[] _abilities = new MovementAbility[0];
        private float _jumpBuffer, _wallJumpBuffer, _dashBuffer;
        private float _coyoteTimer, _ungroundLock, _landingGrace;
        private Vector3 _pendingImpulse;
        private readonly Collider[] _overlapBuffer = new Collider[8];

        private void Awake()
        {
            Controller = GetComponent<CharacterController>();
            if (settings == null)
            {
                Debug.LogWarning("[VAMP] MovementController has no MovementSettings assigned - using defaults.", this);
                settings = ScriptableObject.CreateInstance<MovementSettings>();
            }

            Controller.radius = settings.capsuleRadius;
            Controller.slopeLimit = settings.maxGroundAngle;
            Controller.stepOffset = Mathf.Min(0.35f, settings.crouchHeight * 0.5f);
            Controller.minMoveDistance = 0f;
            ApplyHeight(settings.standingHeight);

            _abilities = GetComponents<MovementAbility>();
            Array.Sort(_abilities, (a, b) => a.Order.CompareTo(b.Order));
            foreach (var ability in _abilities) ability.Bind(this);
        }

        // ------------------------------------------------------------------ Simulation

        public void Simulate(in PlayerInputFrame input, float dt)
        {
            if (dt <= 0f || !Controller.enabled) return;
            LastInput = input;

            TickBuffers(input, dt);
            UpdateGround(dt);
            UpdateCapsuleHeight(input, dt);
            ApplyPendingImpulse();

            State = IsGrounded ? MovementState.Grounded : MovementState.Airborne;

            bool claimed = false;
            for (int i = 0; i < _abilities.Length; i++)
            {
                var ability = _abilities[i];
                if (!ability.enabled) continue;
                if (claimed)
                {
                    if (ability.IsActive) ability.OnSuppressed();
                    continue;
                }
                claimed = ability.Tick(input, dt);
            }

            if (!claimed)
            {
                if (IsGrounded) GroundMove(input, dt);
                else AirMove(input, dt);
            }

            ClampSpeeds();
            MoveCharacter(dt);

            float hs = HorizontalSpeed;
            if (hs > PeakSpeed) PeakSpeed = hs;
        }

        private void TickBuffers(in PlayerInputFrame input, float dt)
        {
            float buffer = settings.jumpBufferTime;
            _jumpBuffer = input.JumpPressed ? buffer : Mathf.Max(0f, _jumpBuffer - dt);
            _wallJumpBuffer = input.WallJumpPressed ? buffer : Mathf.Max(0f, _wallJumpBuffer - dt);
            _dashBuffer = input.DashPressed ? buffer : Mathf.Max(0f, _dashBuffer - dt);
            _ungroundLock -= dt;
            _landingGrace -= dt;
        }

        // ------------------------------------------------------------------ Ground detection

        private void UpdateGround(float dt)
        {
            bool wasGrounded = IsGrounded;
            JustLanded = false;

            bool grounded = false;
            Vector3 normal = Vector3.up;

            if (_ungroundLock <= 0f && !(_velocity.y > 1f && !wasGrounded))
            {
                float r = settings.capsuleRadius * 0.95f;
                Vector3 origin = transform.position + Vector3.up * (settings.capsuleRadius + 0.1f);
                float dist = 0.1f + Controller.skinWidth + settings.groundCheckDistance;
                RaycastHit hit;
                if (Physics.SphereCast(origin, r, Vector3.down, out hit, dist, settings.environmentMask, QueryTriggerInteraction.Ignore))
                {
                    if (Vector3.Angle(hit.normal, Vector3.up) <= settings.maxGroundAngle)
                    {
                        grounded = true;
                        normal = hit.normal;
                    }
                }
            }

            IsGrounded = grounded;
            GroundNormal = normal;

            if (grounded)
            {
                _coyoteTimer = settings.coyoteTime;
                if (!wasGrounded)
                {
                    JustLanded = true;
                    _landingGrace = settings.landingFrictionGrace;
                    AirDashesUsed = 0;
                    float impact = Mathf.Max(0f, -_velocity.y);
                    AirTime = 0f;
                    Raise(MovementEventType.Landed, impact);
                    for (int i = 0; i < _abilities.Length; i++) _abilities[i].OnLanded();
                }
                AirTime = 0f;
            }
            else
            {
                _coyoteTimer -= dt;
                AirTime += dt;
            }
        }

        // ------------------------------------------------------------------ Crouch / capsule

        private void UpdateCapsuleHeight(in PlayerInputFrame input, float dt)
        {
            bool wantCrouch = ForceCrouch || input.CrouchHeld || input.SlideHeld;
            float target = wantCrouch ? settings.crouchHeight : settings.standingHeight;

            if (target > CurrentHeight && !HasHeadroom(target)) target = CurrentHeight; // blocked above - stay down

            if (!Mathf.Approximately(target, CurrentHeight))
                ApplyHeight(Mathf.MoveTowards(CurrentHeight, target, settings.crouchTransitionSpeed * dt));
        }

        private void ApplyHeight(float height)
        {
            CurrentHeight = height;
            Controller.height = height;
            Controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private bool HasHeadroom(float targetHeight)
        {
            float r = settings.capsuleRadius * 0.95f;
            Vector3 feet = transform.position;
            Vector3 bottom = feet + Vector3.up * Mathf.Max(r, CurrentHeight - r);
            Vector3 top = feet + Vector3.up * (targetHeight - r);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, r, _overlapBuffer, settings.environmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (_overlapBuffer[i] != Controller) return false;
            return true;
        }

        // ------------------------------------------------------------------ Default ground / air movement

        private void GroundMove(in PlayerInputFrame input, float dt)
        {
            if (_jumpBuffer > 0f)
            {
                Jump();
                return;
            }

            Vector3 wishDir = WishDirection(input.Move);
            float inputMag = Mathf.Min(1f, wishDir.magnitude);
            if (inputMag > 0.0001f) wishDir /= inputMag;

            float targetSpeed;
            if (IsCrouching) targetSpeed = settings.crouchSpeed;
            else if (input.SprintHeld && input.Move.y >= settings.sprintMinForwardInput) targetSpeed = settings.sprintSpeed;
            else targetSpeed = settings.walkSpeed;
            targetSpeed *= inputMag;

            // Keep velocity on the ground plane (slopes).
            _velocity = Vector3.ProjectOnPlane(_velocity, GroundNormal);

            if (_landingGrace <= 0f)
            {
                float friction = settings.groundFriction + (inputMag < 0.01f ? settings.groundDeceleration : 0f);
                ApplyFriction(friction, dt);
            }

            Vector3 planeWish = Vector3.ProjectOnPlane(wishDir, GroundNormal).normalized;
            Accelerate(planeWish, targetSpeed, settings.groundAcceleration, dt);
        }

        private void AirMove(in PlayerInputFrame input, float dt)
        {
            // Coyote jump (just ran off a ledge).
            if (_jumpBuffer > 0f && _coyoteTimer > 0f)
            {
                Jump();
                return;
            }

            Vector3 wishDir = WishDirection(input.Move);
            float inputMag = Mathf.Min(1f, wishDir.magnitude);
            if (inputMag > 0.0001f) wishDir /= inputMag;

            if (inputMag > 0.01f)
            {
                // 1) Air strafing: Source-style capped air acceleration. Turning the mouse in sync with
                //    strafe keys lets skilled players gain speed; holding W does nothing above the cap.
                float wishSpeed = settings.sprintSpeed * inputMag;
                float cappedWish = Mathf.Min(wishSpeed, settings.airWishSpeedCap);
                float current = Vector3.Dot(HorizontalVelocity, wishDir);
                float add = cappedWish - current;
                if (add > 0f)
                {
                    float accel = Mathf.Min(settings.airAcceleration * wishSpeed * dt, add);
                    _velocity += wishDir * accel;
                }

                // 2) Low-speed control so standing jumps aren't dead in the air.
                if (HorizontalSpeed < settings.walkSpeed)
                    Accelerate(wishDir, settings.walkSpeed * inputMag, settings.airLowSpeedAcceleration, dt);

                // 3) Air control: holding only forward gently steers momentum toward where you look.
                if (input.Move.y > 0.1f && Mathf.Abs(input.Move.x) < 0.1f && settings.airControlTurnRate > 0f)
                {
                    Vector3 h = HorizontalVelocity;
                    float s = h.magnitude;
                    if (s > 0.5f)
                    {
                        Vector3 look = transform.forward;
                        look.y = 0f;
                        Vector3 dir = Vector3.RotateTowards(h / s, look.normalized, settings.airControlTurnRate * Mathf.Deg2Rad * dt, 0f);
                        _velocity.x = dir.x * s;
                        _velocity.z = dir.z * s;
                    }
                }
            }

            ApplyGravity(dt, 1f);
        }

        // ------------------------------------------------------------------ Public helpers for abilities

        /// <summary>World-space, flat wish direction from move input (magnitude 0..1).</summary>
        public Vector3 WishDirection(Vector2 move)
        {
            Vector3 f = transform.forward; f.y = 0f; f.Normalize();
            Vector3 r = transform.right; r.y = 0f; r.Normalize();
            return Vector3.ClampMagnitude(r * move.x + f * move.y, 1f);
        }

        public void Accelerate(Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            if (wishSpeed <= 0f || wishDir.sqrMagnitude < 0.0001f) return;
            float current = Vector3.Dot(_velocity, wishDir);
            float add = wishSpeed - current;
            if (add <= 0f) return;
            float accelSpeed = Mathf.Min(accel * wishSpeed * dt, add);
            _velocity += wishDir * accelSpeed;
        }

        public void ApplyFriction(float friction, float dt)
        {
            float speed = _velocity.magnitude;
            if (speed < 0.01f)
            {
                _velocity = Vector3.zero;
                return;
            }
            float control = Mathf.Max(speed, settings.stopSpeed);
            float newSpeed = Mathf.Max(0f, speed - control * friction * dt);
            _velocity *= newSpeed / speed;
        }

        public void ApplyGravity(float dt, float multiplier)
        {
            float g = settings.gravity * multiplier;
            if (_velocity.y < 0f) g *= settings.fallGravityMultiplier;
            _velocity.y -= g * dt;
        }

        public void Jump()
        {
            float carried = Mathf.Max(0f, _velocity.y) * settings.upwardVelocityCarryOnJump;
            _velocity.y = carried + settings.jumpForce;
            IsGrounded = false;
            _ungroundLock = JumpGroundLockTime;
            _jumpBuffer = 0f;
            _coyoteTimer = 0f;
            Raise(MovementEventType.Jumped, _velocity.y);
        }

        /// <summary>Prevents ground detection for a short time (used by wall jumps / impulses).</summary>
        public void LockUngrounded(float seconds)
        {
            _ungroundLock = Mathf.Max(_ungroundLock, seconds);
            IsGrounded = false;
            _coyoteTimer = 0f;
        }

        public bool HasJumpBuffered { get { return _jumpBuffer > 0f; } }
        public bool HasWallJumpBuffered { get { return _wallJumpBuffer > 0f; } }
        public bool HasDashBuffered { get { return _dashBuffer > 0f; } }
        public void ConsumeJumpBuffer() { _jumpBuffer = 0f; }
        public void ConsumeWallJumpBuffer() { _wallJumpBuffer = 0f; }
        public void ConsumeDashBuffer() { _dashBuffer = 0f; }

        internal void SetState(MovementState state) { State = state; }

        internal void Raise(MovementEventType type, float magnitude)
        {
            var handler = MovementEventRaised;
            if (handler == null) return;
            handler(new MovementEvent { Type = type, Position = transform.position, Velocity = _velocity, Magnitude = magnitude });
        }

        // ------------------------------------------------------------------ External forces

        /// <summary>
        /// Adds an instantaneous velocity change (explosions / rocket jumps / knockback). Applied at the
        /// start of the next simulation tick. In multiplayer only the server may call this for real;
        /// clients predict it.
        /// </summary>
        public void AddImpulse(Vector3 deltaVelocity)
        {
            _pendingImpulse += deltaVelocity * settings.externalImpulseMultiplier;
        }

        private void ApplyPendingImpulse()
        {
            if (_pendingImpulse.sqrMagnitude < MinMoveSqr) return;
            Vector3 impulse = _pendingImpulse;
            _pendingImpulse = Vector3.zero;

            for (int i = 0; i < _abilities.Length; i++) _abilities[i].OnExternalImpulse(impulse);

            // Rocket jumps should launch you even if you were falling: cancel downward velocity first.
            if (impulse.y > 0f && _velocity.y < 0f) _velocity.y = 0f;
            _velocity += impulse;

            if (impulse.y > 0.5f) LockUngrounded(settings.impulseUngroundTime);
            Raise(MovementEventType.ExternalImpulse, impulse.magnitude);
        }

        // ------------------------------------------------------------------ Integration + collision

        private void ClampSpeeds()
        {
            Vector3 h = HorizontalVelocity;
            float max = settings.maxHorizontalSpeed;
            if (h.sqrMagnitude > max * max)
            {
                h = h.normalized * max;
                _velocity.x = h.x;
                _velocity.z = h.z;
            }
            if (_velocity.y < -settings.maxFallSpeed) _velocity.y = -settings.maxFallSpeed;
        }

        private void MoveCharacter(float dt)
        {
            Vector3 displacement = _velocity * dt;
            // Stick to ground on slopes / stairs. Displacement only - never stored in velocity.
            if (IsGrounded && _velocity.y <= 0.01f) displacement += Vector3.down * settings.groundSnapDistance;
            if (displacement.sqrMagnitude > MinMoveSqr) Controller.Move(displacement);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Clip velocity against what we hit so we slide along surfaces instead of "sticking"
            // and so ceilings/walls actually stop us.
            float into = Vector3.Dot(_velocity, hit.normal);
            if (into < 0f) _velocity -= hit.normal * into;
        }

        // ------------------------------------------------------------------ Teleport / respawn

        public void Teleport(Vector3 position, float yawDegrees)
        {
            Controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            Controller.enabled = true;
            _velocity = Vector3.zero;
            _pendingImpulse = Vector3.zero;
            _jumpBuffer = _wallJumpBuffer = _dashBuffer = 0f;
            AirDashesUsed = 0;
            ForceCrouch = false;
            ApplyHeight(settings.standingHeight);
            IsGrounded = false;
            for (int i = 0; i < _abilities.Length; i++) _abilities[i].ResetState();
        }

        public void ResetPeakSpeed() { PeakSpeed = 0f; }

        /// <summary>
        /// Swap tuning at runtime (CUSTOM GAME gravity / movement speed). Pass a runtime copy, never the project asset.
        /// </summary>
        public void SetSettings(MovementSettings runtimeCopy)
        {
            if (runtimeCopy == null) return;
            settings = runtimeCopy;
            Controller.radius = settings.capsuleRadius;
            Controller.slopeLimit = settings.maxGroundAngle;
            ApplyHeight(settings.standingHeight);
        }

        /// <summary>Creates a scaled runtime copy of the current settings.</summary>
        public MovementSettings CreateScaledCopy(float gravityMultiplier, float speedMultiplier)
        {
            var copy = Instantiate(settings);
            copy.name = settings.name + " (Match)";
            copy.gravity *= gravityMultiplier;
            copy.walkSpeed *= speedMultiplier;
            copy.sprintSpeed *= speedMultiplier;
            copy.crouchSpeed *= speedMultiplier;
            copy.maxHorizontalSpeed *= speedMultiplier;
            copy.slideBoostMaxSpeed *= speedMultiplier;
            copy.wallRunSpeed *= speedMultiplier;
            copy.dashSpeed *= speedMultiplier;
            copy.wallRunMinSpeed *= speedMultiplier;
            copy.slideMinStartSpeed *= speedMultiplier;
            return copy;
        }

        public T GetAbility<T>() where T : MovementAbility
        {
            for (int i = 0; i < _abilities.Length; i++)
            {
                var t = _abilities[i] as T;
                if (t != null) return t;
            }
            return null;
        }
    }
}
