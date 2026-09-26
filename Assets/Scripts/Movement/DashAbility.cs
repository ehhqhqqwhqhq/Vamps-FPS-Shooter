using UnityEngine;
using Vamp.Core;

namespace Vamp.Movement
{
    /// <summary>
    /// Directional dash (forward/back/left/right/air) based on movement input; forward if no input.
    /// Charge-based energy with recharge + a short cooldown between dashes, limited air dashes per
    /// airtime (refreshed by landing or wall running). Exit speed never drops below your pre-dash speed
    /// in that direction, so dashing adds to momentum without becoming a free speed engine.
    /// </summary>
    public sealed class DashAbility : MovementAbility
    {
        public override int Order { get { return 0; } }

        private bool _active;
        private float _timeLeft;
        private float _cooldown;
        private float _charges;
        private float _preDashSpeedAlong;
        private Vector3 _dir;

        public override bool IsActive { get { return _active; } }
        public float Charges { get { return _charges; } }
        public int MaxCharges { get { return Settings.dashMaxCharges; } }
        public float CooldownRemaining { get { return Mathf.Max(0f, _cooldown); } }

        protected override void OnBind()
        {
            _charges = Settings.dashMaxCharges;
        }

        public override bool Tick(in PlayerInputFrame input, float dt)
        {
            var s = Settings;
            if (_charges < s.dashMaxCharges && s.dashRechargeTime > 0f)
                _charges = Mathf.Min(s.dashMaxCharges, _charges + dt / s.dashRechargeTime);
            _cooldown -= dt;

            if (_active)
            {
                _timeLeft -= dt;

                if (s.allowDashJump && Motor.IsGrounded && Motor.HasJumpBuffered)
                {
                    EndDash();
                    Motor.Jump();
                    return true;
                }

                if (_timeLeft <= 0f)
                {
                    EndDash();
                    return false; // normal movement resumes this tick
                }

                Vector3 v = _dir * s.dashSpeed;
                if (s.dashIgnoresGravity) v.y = 0f;
                else v.y = Motor.Velocity.y - s.gravity * dt;
                Motor.Velocity = v;
                Motor.SetState(MovementState.Dashing);
                return true;
            }

            if (!Motor.HasDashBuffered || _charges < 1f || _cooldown > 0f) return false;

            bool grounded = Motor.IsGrounded;
            if (!grounded && Motor.AirDashesUsed >= s.airDashesPerAirtime) return false;

            Motor.ConsumeDashBuffer();

            Vector3 dir = Motor.WishDirection(input.Move);
            if (dir.sqrMagnitude < 0.01f)
            {
                dir = transform.forward;
                dir.y = 0f;
            }
            dir.Normalize();

            _preDashSpeedAlong = Mathf.Max(0f, Vector3.Dot(Motor.HorizontalVelocity, dir));
            _dir = dir;
            _charges -= 1f;
            _cooldown = s.dashCooldown;
            _timeLeft = s.dashDuration;
            _active = true;
            if (!grounded) Motor.AirDashesUsed++;

            Motor.Velocity = new Vector3(dir.x * s.dashSpeed, s.dashIgnoresGravity ? 0f : Motor.Velocity.y, dir.z * s.dashSpeed);
            Motor.SetState(MovementState.Dashing);
            Motor.Raise(MovementEventType.Dashed, s.dashSpeed);
            return true;
        }

        private void EndDash()
        {
            if (!_active) return;
            _active = false;
            var s = Settings;
            float exit = Mathf.Max(_preDashSpeedAlong, s.dashSpeed * s.dashExitSpeedFraction);
            Motor.Velocity = new Vector3(_dir.x * exit, Motor.Velocity.y, _dir.z * exit);
        }

        public override void OnExternalImpulse(Vector3 impulse) { EndDash(); }

        public override void ResetState()
        {
            _active = false;
            _cooldown = 0f;
            _charges = Settings.dashMaxCharges;
        }
    }
}
