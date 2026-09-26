using UnityEngine;
using Vamp.Core;
using Vamp.Movement;
using Vamp.Settings;
using Vamp.Weapons;

namespace Vamp.Player
{
    /// <summary>
    /// First-person camera.
    /// Hierarchy:  Player (yaw)  →  CameraRoot (eye height + pitch = the AIM transform)  →  Camera (visual only:
    /// roll tilt, recoil punch, screen shake). Weapons aim from CameraRoot, so screen shake and view punch
    /// never move your bullets.
    /// Look is applied immediately in the same frame input is read (no smoothing = no added latency).
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private LookSettings look = new LookSettings();

        [Header("Tilt")]
        [SerializeField] private float wallRunTilt = 9f;
        [SerializeField] private float slideTilt = 3f;
        [SerializeField] private float strafeTilt = 0.8f;
        [SerializeField] private float tiltSharpness = 8f;

        [Header("Speed FOV")]
        [SerializeField] private float speedFovStart = 11.5f;
        [SerializeField] private float speedFovFull = 30f;
        [SerializeField] private float fovSharpness = 6f;

        [Header("Landing Dip")]
        [SerializeField] private float landingDipPerSpeed = 0.035f;
        [SerializeField] private float maxLandingDipVelocity = 1.2f;
        [SerializeField] private float dipSpring = 90f;
        [SerializeField] private float dipDamping = 14f;

        [Header("Shake / Kick")]
        [SerializeField] private float maxShakeAngle = 2.5f;
        [SerializeField] private float shakeFrequency = 22f;
        [SerializeField] private float traumaDecay = 2.2f;
        [SerializeField] private float kickRecoverSharpness = 16f;

        private MovementController _motor;
        private WallRunAbility _wallRun;
        private float _yaw, _pitch, _roll;
        private float _dip, _dipVel;
        private float _trauma;
        private Vector2 _kick;
        private float _smoothedFov;
        private float _strafeInput;

        public LookSettings Look { get { return look; } }
        public Transform AimTransform { get { return cameraRoot; } }
        public Camera PlayerCamera { get { return playerCamera; } }
        public float Yaw { get { return _yaw; } }
        public float Pitch { get { return _pitch; } }

        private void Awake()
        {
            _motor = GetComponent<MovementController>();
            _wallRun = GetComponent<WallRunAbility>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (cameraRoot == null && playerCamera != null) cameraRoot = playerCamera.transform.parent;
            if (cameraRoot == null || playerCamera == null)
            {
                Debug.LogError("[VAMP] CameraController needs Player > CameraRoot > Camera.", this);
                enabled = false;
                return;
            }
            look.Validate();
            _yaw = transform.eulerAngles.y;
            if (Core.Game.Settings != null)
            {
                Core.Game.Settings.Changed += OnSettingsChanged;
                OnSettingsChanged(Core.Game.Settings.Current);
            }
            _smoothedFov = look.fieldOfView;
            if (_motor != null) _motor.MovementEventRaised += OnMovementEvent;
        }

        private void OnDestroy()
        {
            if (_motor != null) _motor.MovementEventRaised -= OnMovementEvent;
            if (Core.Game.Settings != null) Core.Game.Settings.Changed -= OnSettingsChanged;
        }

        private bool _acceleration;
        private bool _cameraEffects = true;

        /// <summary>Account settings → look settings (sensitivity, ADS/sniper, invert, FOV, shake, camera effects).</summary>
        private void OnSettingsChanged(GameSettings s)
        {
            look.sensitivity = s.mouse.sensitivity;
            look.adsSensitivity = s.mouse.adsSensitivity;
            look.sniperSensitivity = s.mouse.sniperSensitivity;
            look.invertX = s.mouse.invertX;
            look.invertY = s.mouse.invertY;
            look.fieldOfView = s.mouse.fov;
            _cameraEffects = s.accessibility.cameraEffects;
            float shake = s.accessibility.screenShake;
            if (s.graphics.competitiveVisuals) shake *= 0.4f;
            look.screenShake = shake;
            look.speedFovBonus = _cameraEffects ? 8f : 0f;
            _acceleration = s.mouse.mouseAcceleration;
            look.Validate();
        }

        /// <summary>Call BEFORE movement so movement uses this frame's yaw.</summary>
        public void TickLook(in PlayerInputFrame input, AimSensitivityMode mode)
        {
            if (!enabled) return;
            float mult = mode == AimSensitivityMode.Ads ? look.adsSensitivity
                       : mode == AimSensitivityMode.Sniper ? look.sniperSensitivity : 1f;
            float scale = look.sensitivity * mult * look.degreesPerCount;
            if (_acceleration)
            {
                // Optional mild acceleration curve (OFF by default for competitive play).
                float speed = input.LookDelta.magnitude / Mathf.Max(0.001f, Time.unscaledDeltaTime) / 1000f;
                scale *= 1f + Mathf.Clamp(speed * 0.15f, 0f, 0.6f);
            }

            _yaw += input.LookDelta.x * scale * (look.invertX ? -1f : 1f);
            _pitch -= input.LookDelta.y * scale * (look.invertY ? -1f : 1f);
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            _yaw = Mathf.Repeat(_yaw, 360f);
            _strafeInput = input.Move.x;

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>Call AFTER movement: eye height, tilt, FOV, shake. zoom = weapon ADS FOV multiplier.</summary>
        public void TickPostMove(float dt, float zoom)
        {
            if (!enabled || _motor == null) return;

            // Landing dip (critically-damped-ish spring).
            float accel = -dipSpring * _dip - dipDamping * _dipVel;
            _dipVel += accel * dt;
            _dip += _dipVel * dt;
            cameraRoot.localPosition = new Vector3(0f, _motor.EyeHeight + _dip, 0f);

            // Roll.
            float targetRoll = -_strafeInput * strafeTilt;
            if (_wallRun != null && _wallRun.IsActive) targetRoll = _wallRun.WallSide * wallRunTilt; // lean away from wall
            else if (_motor.State == MovementState.Sliding) targetRoll = slideTilt;
            if (!_cameraEffects) targetRoll *= 0.25f;
            _roll = Mathf.Lerp(_roll, targetRoll, 1f - Mathf.Exp(-tiltSharpness * dt));

            // Speed FOV (subtle), then ADS zoom on top.
            float t = Mathf.InverseLerp(speedFovStart, speedFovFull, _motor.HorizontalSpeed);
            float targetFov = look.fieldOfView + look.speedFovBonus * t;
            _smoothedFov = Mathf.Lerp(_smoothedFov, targetFov, 1f - Mathf.Exp(-fovSharpness * dt));
            float hfov = Mathf.Clamp(_smoothedFov * zoom, 5f, 170f);
            playerCamera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(hfov, playerCamera.aspect);

            // Kick recovery + trauma decay.
            _kick = Vector2.Lerp(_kick, Vector2.zero, 1f - Mathf.Exp(-kickRecoverSharpness * dt));
            _trauma = Mathf.Max(0f, _trauma - traumaDecay * dt);

            float shake = _trauma * _trauma * maxShakeAngle * look.screenShake;
            float time = Time.time * shakeFrequency;
            float sx = (Mathf.PerlinNoise(time, 0.3f) - 0.5f) * 2f * shake;
            float sy = (Mathf.PerlinNoise(0.7f, time) - 0.5f) * 2f * shake;
            float sz = (Mathf.PerlinNoise(time, time * 0.5f) - 0.5f) * 2f * shake;

            playerCamera.transform.localRotation = Quaternion.Euler(_kick.x + sx, _kick.y + sy, _roll + sz);
        }

        /// <summary>Real aim change (moves your crosshair).</summary>
        public void AddRecoil(float pitchUp, float yawRandom)
        {
            _pitch = Mathf.Clamp(_pitch - pitchUp, -89f, 89f);
            _yaw += Random.Range(-yawRandom, yawRandom);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>Visual punch only (recovers, does not move aim).</summary>
        public void AddViewKick(float degrees)
        {
            _kick.x -= degrees;
        }

        public void AddShake(float trauma)
        {
            _trauma = Mathf.Clamp01(_trauma + trauma);
        }

        public void SetView(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -89f, 89f);
            _kick = Vector2.zero;
            _trauma = 0f;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void ApplyLookSettings(LookSettings settings)
        {
            if (settings == null) return;
            settings.Validate();
            look = settings;
        }

        private void OnMovementEvent(MovementEvent e)
        {
            switch (e.Type)
            {
                case MovementEventType.Landed:
                    if (e.Magnitude > 3f && _cameraEffects)
                    {
                        _dipVel -= Mathf.Min(maxLandingDipVelocity, e.Magnitude * landingDipPerSpeed);
                        if (e.Magnitude > 14f) AddShake(0.25f);
                    }
                    break;
                case MovementEventType.ExternalImpulse:
                    AddShake(Mathf.Clamp01(e.Magnitude / 30f));
                    break;
                case MovementEventType.Dashed:
                    AddShake(0.08f);
                    break;
            }
        }
    }
}
