using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Vamp.Bots;
using Vamp.Movement;
using Vamp.Player;
using Vamp.Weapons;

namespace Vamp.Characters
{
    /// <summary>External animation input (online proxies of other players).</summary>
    public interface ICharacterSource
    {
        Vector3 Velocity { get; }
        float Yaw { get; }
        float Pitch { get; }
        bool Grounded { get; }
        bool Sliding { get; }
        bool WallRunning { get; }
        float WallSide { get; }
        float Crouch { get; }
        WeaponData Weapon { get; }
    }

    /// <summary>
    /// Puts the stick-man character on a player or bot and feeds its <see cref="CharacterRig"/> every frame
    /// (velocity, aim, grounded / slide / wall-run / crouch, held weapon). Presentation only.
    /// For the local player the body only casts shadows (first-person view stays clean).
    /// Falls back silently (keeps the old capsule visuals) when the character prefab hasn't been built yet.
    /// </summary>
    public sealed class CharacterPresenter : MonoBehaviour
    {
        public bool LocalView;
        public Color Tint = new Color(0.85f, 0.85f, 0.87f);
        public float TintEmission;

        private CharacterRig _rig;
        private MovementController _move;
        private CameraController _cam;
        private WeaponController _weapons;
        private SlideAbility _slide;
        private WallRunAbility _wall;
        private NavMeshAgent _agent;
        private BotController _bot;
        private Vector3 _lastPos;
        private Vector3 _vel;

        public CharacterRig Rig { get { return _rig; } }

        /// <summary>When set, animation is driven by this source instead of local components (online proxies).</summary>
        public ICharacterSource Source;

        /// <summary>Switch between first-person (shadow only) and third-person (fully visible) presentation.</summary>
        public void SetLocalView(bool local)
        {
            LocalView = local;
            if (_rig == null) return;
            _rig.SetShadowMode(local ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On);
            foreach (var t in _rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = local ? 2 : 0;
        }

        public void SetTint(Color c, float emission = 0f)
        {
            Tint = c;
            TintEmission = emission;
            if (_rig != null) _rig.SetTint(c, emission);
        }
        public bool HasCharacter { get { return _rig != null; } }

        /// <summary>Adds the character; returns false (and adds nothing) when the prefab is unavailable.</summary>
        public static CharacterPresenter Attach(GameObject owner, bool localView, Color tint, float emission = 0f)
        {
            if (Resources.Load<GameObject>("VampCharacter") == null) return null;
            var p = owner.AddComponent<CharacterPresenter>();
            p.LocalView = localView;
            p.Tint = tint;
            p.TintEmission = emission;
            p.Build();
            return p;
        }

        private void Awake()
        {
            _move = GetComponent<MovementController>();
            _cam = GetComponent<CameraController>();
            _weapons = GetComponent<WeaponController>();
            _slide = GetComponent<SlideAbility>();
            _wall = GetComponent<WallRunAbility>();
            _agent = GetComponent<NavMeshAgent>();
            _bot = GetComponent<BotController>();
            _lastPos = transform.position;
        }

        private void Start()
        {
            if (_rig == null) Build();
        }

        private void Build()
        {
            if (_rig != null) return;
            _rig = CharacterRig.Spawn(transform);
            if (_rig == null) return;
            _rig.SetTint(Tint, TintEmission);
            if (LocalView)
            {
                // Seen only through its shadow; also kept out of first-person hitscan (Ignore Raycast layer).
                _rig.SetShadowMode(ShadowCastingMode.ShadowsOnly);
                foreach (var t in _rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            }
        }

        private void LateUpdate()
        {
            if (_rig == null) return;
            if (_bot == null && _move == null) _bot = GetComponent<BotController>(); // bots add their brain after the body
            if (_agent == null && _move == null) _agent = GetComponent<NavMeshAgent>();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Source != null)
            {
                _rig.transform.rotation = Quaternion.Euler(0f, Source.Yaw, 0f);
                _rig.Velocity = Source.Velocity;
                _rig.AimDirection = Quaternion.Euler(Source.Pitch, Source.Yaw, 0f) * Vector3.forward;
                _rig.Grounded = Source.Grounded;
                _rig.Sliding = Source.Sliding;
                _rig.WallRunning = Source.WallRunning;
                _rig.WallSide = Source.WallSide;
                _rig.Crouch = Source.Crouch;
                _rig.SetWeapon(Source.Weapon);
                _rig.Pose(dt);
                return;
            }

            // Velocity
            if (_move != null) _vel = _move.Velocity;
            else if (_agent != null && _agent.enabled) _vel = _agent.velocity;
            else _vel = (transform.position - _lastPos) / dt;
            _lastPos = transform.position;

            // Body yaw: players turn with the camera; bots already rotate their root.
            Vector3 aim = transform.forward;
            if (_cam != null)
            {
                _rig.transform.rotation = Quaternion.Euler(0f, _cam.Yaw, 0f);
                aim = Quaternion.Euler(_cam.Pitch, _cam.Yaw, 0f) * Vector3.forward;
                if (_cam.AimTransform != null) aim = _cam.AimTransform.forward;
            }
            else
            {
                _rig.transform.localRotation = Quaternion.identity;
                if (_bot != null && _bot.Eye != null) aim = _bot.Eye.forward;
            }

            _rig.Velocity = _vel;
            _rig.AimDirection = aim;
            if (_move != null)
            {
                _rig.Grounded = _move.IsGrounded;
                _rig.Sliding = _slide != null && _slide.IsActive;
                _rig.WallRunning = _wall != null && _wall.IsActive;
                if (_rig.WallRunning)
                {
                    float side = Vector3.Dot(_wall.WallNormal, _rig.transform.right);
                    _rig.WallSide = side > 0f ? 1f : -1f; // wall on the left → normal points right → lean right (away)
                }
                var s = _move.Settings;
                _rig.Crouch = s != null ? Mathf.InverseLerp(s.standingHeight, s.crouchHeight, _move.CurrentHeight) : 0f;
            }
            else
            {
                _rig.Grounded = true;
                _rig.Sliding = false;
                _rig.WallRunning = false;
                _rig.Crouch = 0f;
            }

            WeaponData w = null;
            if (_weapons != null) w = _weapons.Current;
            else if (_bot != null) w = _bot.Weapon;
            _rig.SetWeapon(w);
            _rig.Pose(dt);
        }
    }
}
