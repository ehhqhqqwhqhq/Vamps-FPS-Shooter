using UnityEngine;
using Vamp.Core;

namespace Vamp.Movement
{
    /// <summary>
    /// Wall running + wall jumping.
    /// - Wall run: airborne, fast enough, high enough, holding forward, next to a (designated) near-vertical wall.
    ///   Preserves momentum (never slows you below your entry speed), reduced gravity, aiming stays free.
    /// - Wall jump: Jump or Wall Jump while wall running, or while touching any near-vertical wall in the air.
    ///   Directional (steered by movement input), keeps along-wall momentum, locks out the same wall briefly
    ///   so you can chain wall-to-wall but can't climb a single wall forever.
    /// </summary>
    public sealed class WallRunAbility : MovementAbility
    {
        public override int Order { get { return 10; } }

        private bool _active;
        private Vector3 _wallNormal;
        private Collider _wall;
        private float _speedMultiplier = 1f;
        private Collider _lockedWall;
        private float _lockTimer;
        private float _wallJumpCooldown;

        public override bool IsActive { get { return _active; } }
        public float RunTime { get; private set; }
        public Vector3 WallNormal { get { return _wallNormal; } }
        /// <summary>-1 = wall on the left, +1 = wall on the right, 0 = none. Used for camera tilt.</summary>
        public int WallSide { get; private set; }
        public float WallJumpCooldownRemaining { get { return Mathf.Max(0f, _wallJumpCooldown); } }

        public override bool Tick(in PlayerInputFrame input, float dt)
        {
            var s = Settings;
            _wallJumpCooldown -= dt;
            _lockTimer -= dt;
            if (_lockTimer <= 0f) _lockedWall = null;

            if (Motor.IsGrounded)
            {
                if (_active) End(false);
                return false;
            }

            bool wantsWallJump = (Motor.HasJumpBuffered || Motor.HasWallJumpBuffered) && _wallJumpCooldown <= 0f;
            RaycastHit hit;

            if (_active)
            {
                if (!FindWall(true, false, out hit) || Vector3.Dot(hit.normal, _wallNormal) < 0.7f)
                {
                    End(false); // ran off the end of the wall - keep momentum
                    return false;
                }
                SetWall(hit);

                if (wantsWallJump)
                {
                    WallJump(input, _wallNormal, _wall);
                    return true;
                }

                RunTime += dt;
                bool forwardOk = !s.wallRunRequiresForwardInput || input.Move.y > 0.1f;
                if (RunTime > s.wallRunMaxDuration || !forwardOk || input.CrouchPressed)
                {
                    End(true);
                    return false;
                }

                RunAlongWall(dt);
                if (!_active) return false;
                Motor.SetState(MovementState.WallRunning);
                return true;
            }

            // Not running: wall jump off any nearby wall.
            if (wantsWallJump && s.allowWallJumpWithoutWallRun && FindWall(false, true, out hit))
            {
                WallJump(input, hit.normal, hit.collider);
                return true;
            }

            // Try to start a wall run.
            if (Motor.HorizontalSpeed < s.wallRunMinSpeed) return false;
            if (s.wallRunRequiresForwardInput && input.Move.y <= 0.1f) return false;
            if (!HighEnough()) return false;
            if (!FindWall(true, false, out hit)) return false;

            Vector3 along = AlongDirection(hit.normal);
            if (Vector3.Dot(Motor.HorizontalVelocity, along) < s.wallRunMinSpeed * 0.5f) return false;

            Begin(hit);
            RunAlongWall(dt);
            if (!_active) return false;
            Motor.SetState(MovementState.WallRunning);
            return true;
        }

        private void Begin(RaycastHit hit)
        {
            var s = Settings;
            _active = true;
            RunTime = 0f;
            SetWall(hit);

            Vector3 v = Motor.Velocity;
            v.y = Mathf.Max(v.y, s.wallRunEntryMinUpSpeed);
            Motor.Velocity = v;

            if (s.wallRunRefreshesAirDash) Motor.AirDashesUsed = 0;
            Motor.Raise(MovementEventType.WallRunStarted, Motor.HorizontalSpeed);
        }

        private void End(bool lockThisWall)
        {
            if (!_active) return;
            _active = false;
            WallSide = 0;
            if (lockThisWall && _wall != null)
            {
                _lockedWall = _wall;
                _lockTimer = Settings.sameWallLockout;
            }
            Motor.Raise(MovementEventType.WallRunEnded, RunTime);
        }

        private void SetWall(RaycastHit hit)
        {
            _wallNormal = hit.normal;
            _wall = hit.collider;
            WallRunSurface surface;
            _speedMultiplier = TryGetSurface(hit.collider, out surface) ? surface.SpeedMultiplier : 1f;
            WallSide = Vector3.Dot(transform.right, -hit.normal) > 0f ? 1 : -1;
        }

        private void RunAlongWall(float dt)
        {
            var s = Settings;
            Vector3 along = AlongDirection(_wallNormal);
            float speed = Vector3.Dot(Motor.HorizontalVelocity, along);
            float target = s.wallRunSpeed * _speedMultiplier;
            if (speed < target) speed = Mathf.MoveTowards(speed, target, s.wallRunAcceleration * dt);

            float vy = Motor.Velocity.y;
            // Full gravity while still rising (no wall-climbing), reduced gravity once descending.
            vy -= (vy > 0f ? s.gravity : s.wallRunGravity) * dt;
            vy = Mathf.Max(vy, -s.wallRunMaxFallSpeed);

            Motor.Velocity = along * speed + Vector3.up * vy - _wallNormal * s.wallRunStickForce;

            if (speed < s.wallRunMinSpeed * 0.5f) End(true);
        }

        private Vector3 AlongDirection(Vector3 wallNormal)
        {
            Vector3 along = Vector3.Cross(wallNormal, Vector3.up).normalized;
            Vector3 h = Motor.HorizontalVelocity;
            float d = h.sqrMagnitude > 1f ? Vector3.Dot(h, along) : Vector3.Dot(transform.forward, along);
            return d >= 0f ? along : -along;
        }

        private void WallJump(in PlayerInputFrame input, Vector3 normal, Collider wall)
        {
            var s = Settings;
            normal.y = 0f;
            normal.Normalize();

            Vector3 h = Motor.HorizontalVelocity;
            float into = Vector3.Dot(h, normal);
            if (into < 0f) h -= normal * into; // strip only the into-wall component

            Vector3 wish = Motor.WishDirection(input.Move);
            Vector3 push = normal;
            if (wish.sqrMagnitude > 0.01f)
            {
                push = Vector3.Lerp(normal, wish.normalized, s.wallJumpInputInfluence);
                float away = Vector3.Dot(push, normal);
                if (away < 0.25f) push += normal * (0.25f - away); // always leave the wall
                push.y = 0f;
                push.Normalize();
            }

            Vector3 look = transform.forward;
            look.y = 0f;
            look.Normalize();

            h += push * s.wallJumpOutForce + look * s.wallJumpForwardBoost;
            Motor.Velocity = new Vector3(h.x, s.wallJumpUpForce, h.z);

            Motor.ConsumeJumpBuffer();
            Motor.ConsumeWallJumpBuffer();
            Motor.LockUngrounded(0.1f);

            if (_active)
            {
                _active = false;
                WallSide = 0;
                Motor.Raise(MovementEventType.WallRunEnded, RunTime);
            }
            _lockedWall = wall;
            _lockTimer = s.sameWallLockout;
            _wallJumpCooldown = s.wallJumpCooldown;

            Motor.Raise(MovementEventType.WallJumped, Motor.HorizontalSpeed);
        }

        // ------------------------------------------------------------------ Probing

        private static readonly Vector3[] RunProbeDirs =
        {
            new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f),
            new Vector3(0.7071f, 0f, 0.7071f), new Vector3(-0.7071f, 0f, 0.7071f),
            new Vector3(0.7071f, 0f, -0.7071f), new Vector3(-0.7071f, 0f, -0.7071f)
        };

        private static readonly Vector3[] JumpProbeDirs =
        {
            new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f),
            new Vector3(0.7071f, 0f, 0.7071f), new Vector3(-0.7071f, 0f, 0.7071f),
            new Vector3(0.7071f, 0f, -0.7071f), new Vector3(-0.7071f, 0f, -0.7071f)
        };

        /// <param name="forRunning">true = must be a (designated) run surface and not locked.</param>
        /// <param name="anyDirection">true = include front/back probes (wall jumps).</param>
        private bool FindWall(bool forRunning, bool anyDirection, out RaycastHit best)
        {
            var s = Settings;
            best = default(RaycastHit);
            bool found = false;
            float bestDist = float.MaxValue;
            Vector3 origin = transform.position + Vector3.up * (Motor.CurrentHeight * 0.5f);
            float range = s.capsuleRadius + s.wallCheckDistance;
            float maxNormalY = Mathf.Sin(s.maxWallAngleFromVertical * Mathf.Deg2Rad);
            Vector3[] dirs = anyDirection ? JumpProbeDirs : RunProbeDirs;

            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3 dir = transform.TransformDirection(dirs[i]);
                RaycastHit hit;
                if (!Physics.Raycast(origin, dir, out hit, range, s.environmentMask, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.normal.y) > maxNormalY) continue;
                if (hit.collider == _lockedWall) continue;
                if (forRunning && s.requireWallRunSurface)
                {
                    WallRunSurface surface;
                    if (!TryGetSurface(hit.collider, out surface)) continue;
                }
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    best = hit;
                    found = true;
                }
            }
            return found;
        }

        private bool HighEnough()
        {
            var s = Settings;
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            return !Physics.Raycast(origin, Vector3.down, s.wallRunMinHeight + 0.1f, s.environmentMask, QueryTriggerInteraction.Ignore);
        }

        private static bool TryGetSurface(Collider c, out WallRunSurface surface)
        {
            if (c.TryGetComponent(out surface)) return true;
            surface = c.GetComponentInParent<WallRunSurface>();
            return surface != null;
        }

        public override void OnSuppressed() { End(false); }
        public override void OnExternalImpulse(Vector3 impulse) { if (impulse.sqrMagnitude > 4f) End(false); }
        public override void OnLanded() { _lockedWall = null; _lockTimer = 0f; }
        public override void ResetState()
        {
            _active = false;
            WallSide = 0;
            _lockedWall = null;
            _lockTimer = 0f;
            _wallJumpCooldown = 0f;
            RunTime = 0f;
        }
    }
}
