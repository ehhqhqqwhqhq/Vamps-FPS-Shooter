using UnityEngine;
using Vamp.Core;

namespace Vamp.Movement
{
    public enum MovementState
    {
        Grounded,
        Airborne,
        Sliding,
        WallRunning,
        Dashing
    }

    public enum MovementEventType
    {
        Jumped,
        Landed,
        SlideStarted,
        SlideEnded,
        WallRunStarted,
        WallRunEnded,
        WallJumped,
        Dashed,
        ExternalImpulse
    }

    /// <summary>
    /// Broadcast by MovementController. Audio, VFX, camera, stats and (later, server-side) movement XP
    /// subscribe to this instead of polling movement internals.
    /// </summary>
    public struct MovementEvent
    {
        public MovementEventType Type;
        public Vector3 Position;
        public Vector3 Velocity;
        /// <summary>Event-specific value: landing impact speed, impulse size, etc.</summary>
        public float Magnitude;
    }

    /// <summary>
    /// Base class for movement abilities (slide, wall run, dash...). The MovementController ticks
    /// abilities in <see cref="Order"/> order; the first one that returns true from Tick "claims" the
    /// tick and the default ground/air movement is skipped. Lower-priority abilities are told they
    /// were suppressed so they can end cleanly.
    /// </summary>
    [RequireComponent(typeof(MovementController))]
    public abstract class MovementAbility : MonoBehaviour
    {
        protected MovementController Motor { get; private set; }
        protected MovementSettings Settings { get { return Motor.Settings; } }

        /// <summary>Lower runs first.</summary>
        public abstract int Order { get; }
        public abstract bool IsActive { get; }

        internal void Bind(MovementController motor)
        {
            Motor = motor;
            OnBind();
        }

        protected virtual void OnBind() { }

        /// <returns>true to take control of movement for this tick.</returns>
        public abstract bool Tick(in PlayerInputFrame input, float dt);

        /// <summary>A higher-priority ability claimed this tick.</summary>
        public virtual void OnSuppressed() { }
        public virtual void OnLanded() { }
        /// <summary>Called right BEFORE an external impulse (explosion) is added to velocity.</summary>
        public virtual void OnExternalImpulse(Vector3 impulse) { }
        /// <summary>Respawn / teleport.</summary>
        public virtual void ResetState() { }
    }
}
