using System;
using UnityEngine;
using Vamp.Combat;
using Vamp.Core;
using Vamp.Movement;
using Vamp.Weapons;

namespace Vamp.Player
{
    /// <summary>
    /// Orchestrates one player's systems in a fixed, explicit order every frame:
    ///   input → look → movement → camera post-move → weapons.
    /// Systems don't Update themselves, which keeps ordering deterministic and lets the network layer
    /// (Phase 6) drive the exact same Simulate calls for prediction and server authority.
    /// Also owns local death/respawn for the prototype (server-controlled spawns come with multiplayer).
    /// </summary>
    public struct SpawnChoice
    {
        public bool Valid;
        public Vector3 Position;
        public float Yaw;
    }

    [RequireComponent(typeof(MovementController))]
    [RequireComponent(typeof(CameraController))]
    [RequireComponent(typeof(WeaponController))]
    [RequireComponent(typeof(HealthController))]
    [RequireComponent(typeof(InputController))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 1.5f;
        [SerializeField] private float killPlaneY = -40f;
        [Tooltip("Simulation step clamp so a hitch can't tunnel you through geometry.")]
        [SerializeField] private float maxSimulationStep = 0.05f;

        public InputController Input { get; private set; }
        public MovementController Movement { get; private set; }
        public CameraController View { get; private set; }
        public WeaponController Weapons { get; private set; }
        public HealthController Health { get; private set; }

        public bool IsDead { get; private set; }
        public float RespawnRemaining { get; private set; }
        public string LastKillerName { get; private set; }
        public DamageInfo LastDeath { get; private set; }
        public bool ShowMovementDebug { get; set; }

        public event Action Respawned;
        public event Action<DamageInfo> Died;

        /// <summary>Match-controlled spawn selection (safest spawn). Null = fixed spawn point.</summary>
        public Func<PlayerController, SpawnChoice> SpawnSelector;

        /// <summary>The player this machine controls (online: set by the owning NetPlayer; offline: the only player).</summary>
        public static PlayerController Local;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLocal() { Local = null; }
        /// <summary>False in Elimination until the next round.</summary>
        public bool RespawnEnabled = true;
        /// <summary>Overrides respawn delay (match rules).</summary>
        public float RespawnDelay { get { return respawnDelay; } set { respawnDelay = Mathf.Max(0f, value); } }
        /// <summary>Freeze all simulation (match over, paused).</summary>
        public bool Frozen { get; set; }

        private Vector3 _spawnPosition;
        private float _spawnYaw;

        private void Awake()
        {
            Input = GetComponent<InputController>();
            Movement = GetComponent<MovementController>();
            View = GetComponent<CameraController>();
            Weapons = GetComponent<WeaponController>();
            Health = GetComponent<HealthController>();
            _spawnPosition = transform.position;
            _spawnYaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            Health.Died += OnDied;
            Weapons.Fired += OnFired;
        }

        private void OnDisable()
        {
            Health.Died -= OnDied;
            Weapons.Fired -= OnFired;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, maxSimulationStep);
            if (Input.MovementDebugTogglePressed) ShowMovementDebug = !ShowMovementDebug;

            if (Frozen)
            {
                View.TickPostMove(dt, 1f);
                return;
            }

            if (IsDead)
            {
                RespawnRemaining -= Time.deltaTime;
                View.TickPostMove(dt, 1f);
                if (RespawnEnabled && RespawnRemaining <= 0f) Respawn();
                return;
            }

            PlayerInputFrame frame = Input.Frame;
            View.TickLook(frame, Weapons.SensitivityMode);
            Movement.Simulate(frame, dt);
            View.TickPostMove(dt, Weapons.ZoomMultiplier);
            Weapons.Tick(frame, dt);

            if (transform.position.y < killPlaneY) Health.Kill(DamageType.World);
        }

        public void SetSpawn(Vector3 position, float yaw)
        {
            _spawnPosition = position;
            _spawnYaw = yaw;
        }

        public void Respawn()
        {
            IsDead = false;
            Health.Revive();
            if (SpawnSelector != null)
            {
                var choice = SpawnSelector(this);
                if (choice.Valid) { _spawnPosition = choice.Position; _spawnYaw = choice.Yaw; }
            }
            Movement.Teleport(_spawnPosition, _spawnYaw);
            View.SetView(_spawnYaw, 0f);
            Weapons.ResetLoadout();
            if (Respawned != null) Respawned();
        }

        private void OnDied(DamageInfo info)
        {
            IsDead = true;
            RespawnRemaining = respawnDelay;
            LastKillerName = CombatIdentity.NameOf(info.Instigator);
            LastDeath = info;
            Movement.Velocity = Vector3.zero;
            if (Died != null) Died(info);
        }

        private void OnFired(WeaponData d)
        {
            // Straight-up recoil: every shot climbs the same amount, no sideways kick - pull straight down to control it.
            View.AddRecoil(d.recoilPitch, 0f);
            // Visual punch only moves the picture, not the aim - keep it small while aiming so the sights stay true.
            View.AddViewKick(d.viewKick * (1f - 0.75f * Weapons.AimBlend));
            View.AddShake(d.screenShake);
        }
    }
}
