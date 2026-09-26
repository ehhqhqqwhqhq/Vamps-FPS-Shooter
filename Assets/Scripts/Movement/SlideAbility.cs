using UnityEngine;
using Vamp.Core;

namespace Vamp.Movement
{
    /// <summary>
    /// Sliding: starts from Slide/Crouch press (or landing while holding either) when moving fast enough.
    /// Preserves momentum with low friction, accelerates downhill, can be steered slightly and
    /// cancels into a jump that keeps all horizontal speed (Sprint -> Crouch -> Slide -> Jump).
    /// </summary>
    public sealed class SlideAbility : MovementAbility
    {
        public override int Order { get { return 20; } }

        private bool _active;
        private float _boostCooldown;

        public override bool IsActive { get { return _active; } }
        public float SlideTime { get; private set; }
        public float BoostCooldownRemaining { get { return Mathf.Max(0f, _boostCooldown); } }

        public override bool Tick(in PlayerInputFrame input, float dt)
        {
            _boostCooldown -= dt;
            var s = Settings;
            bool crouchIntent = input.CrouchHeld || input.SlideHeld;

            if (!_active)
            {
                if (!Motor.IsGrounded || Motor.HasJumpBuffered) return false;
                bool trigger = input.SlidePressed || input.CrouchPressed
                               || (Motor.JustLanded && s.slideOnLandWhileCrouching && crouchIntent);
                if (!trigger || Motor.HorizontalSpeed < s.slideMinStartSpeed) return false;
                Begin();
            }

            // Slid off a ledge: end the slide, keep momentum (air movement takes over this tick).
            if (!Motor.IsGrounded) { End(); return false; }
            if (!crouchIntent) { End(); return false; }

            // Slide-jump: keep every bit of horizontal speed.
            if (Motor.HasJumpBuffered)
            {
                End();
                Motor.Jump();
                return true;
            }

            Vector3 n = Motor.GroundNormal;
            Vector3 v = Vector3.ProjectOnPlane(Motor.Velocity, n);

            // Downhill acceleration (magnitude of downhill vector = sin(slope angle)).
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, n);
            v += downhill * s.slideSlopeAcceleration * dt;

            // Low friction.
            float speed = v.magnitude;
            if (speed > 0.001f)
            {
                float control = Mathf.Max(speed, s.stopSpeed);
                float newSpeed = Mathf.Max(0f, speed - control * s.slideFriction * dt);
                v *= newSpeed / speed;
                speed = newSpeed;
            }

            // Slight steering toward input direction.
            Vector3 wish = Motor.WishDirection(input.Move);
            if (wish.sqrMagnitude > 0.01f && speed > 0.1f)
            {
                Vector3 planeWish = Vector3.ProjectOnPlane(wish, n).normalized;
                Vector3 dir = Vector3.RotateTowards(v / speed, planeWish, s.slideSteerRate * Mathf.Deg2Rad * dt, 0f);
                v = dir * speed;
            }

            Motor.Velocity = v;
            SlideTime += dt;

            if (speed < s.slideEndSpeed)
            {
                End(); // continues as a normal crouch next tick
                return true;
            }

            Motor.SetState(MovementState.Sliding);
            return true;
        }

        private void Begin()
        {
            var s = Settings;
            _active = true;
            SlideTime = 0f;
            Motor.ForceCrouch = true;

            if (_boostCooldown <= 0f)
            {
                Vector3 h = Motor.HorizontalVelocity;
                float speed = h.magnitude;
                float boosted = Mathf.Min(speed + s.slideBoost, Mathf.Max(speed, s.slideBoostMaxSpeed));
                if (speed > 0.01f)
                {
                    h *= boosted / speed;
                    Motor.Velocity = new Vector3(h.x, Motor.Velocity.y, h.z);
                }
                _boostCooldown = s.slideBoostCooldown;
            }
            Motor.Raise(MovementEventType.SlideStarted, Motor.HorizontalSpeed);
        }

        private void End()
        {
            if (!_active) return;
            _active = false;
            Motor.ForceCrouch = false;
            Motor.Raise(MovementEventType.SlideEnded, Motor.HorizontalSpeed);
        }

        public override void OnSuppressed() { End(); }
        public override void OnExternalImpulse(Vector3 impulse) { if (impulse.y > 0.5f) End(); }
        public override void ResetState()
        {
            _active = false;
            _boostCooldown = 0f;
            SlideTime = 0f;
        }
    }
}
