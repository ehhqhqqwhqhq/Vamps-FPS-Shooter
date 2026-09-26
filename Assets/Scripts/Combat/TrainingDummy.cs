using UnityEngine;

namespace Vamp.Combat
{
    /// <summary>
    /// Training target: takes damage via HealthController + Hitboxes, flashes on hit, optionally strafes,
    /// disappears on death and respawns. Used in the movement test map and (later) the Training mode.
    /// </summary>
    [RequireComponent(typeof(HealthController))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        [SerializeField] private float respawnDelay = 2f;
        [Tooltip("Side-to-side strafe distance (0 = static).")]
        [SerializeField] private float strafeAmplitude = 0f;
        [SerializeField] private float strafeSpeed = 1.2f;
        [SerializeField] private Color baseColor = new Color(0.75f, 0.75f, 0.78f);
        [SerializeField] private Color hitColor = new Color(1f, 0.1f, 0.1f);

        private HealthController _health;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Material[] _materials;
        private bool[] _isHead;
        private Vector3 _origin;
        private float _flash;
        private float _respawnTimer;
        private float _phase;

        public void Configure(float strafe, float speed)
        {
            strafeAmplitude = strafe;
            strafeSpeed = speed;
        }

        private void Awake()
        {
            _health = GetComponent<HealthController>();
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
            _materials = new Material[_renderers.Length];
            _isHead = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _materials[i] = _renderers[i].material; // per-dummy instance so flashes are independent
                var hb = _renderers[i].GetComponent<Hitbox>();
                _isHead[i] = hb != null && hb.IsHead;
            }
            _origin = transform.position;
            _phase = Random.value * 10f;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void OnDestroy()
        {
            if (_materials == null) return;
            foreach (var m in _materials) if (m != null) Destroy(m);
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            _flash = 1f;
        }

        private void OnDied(DamageInfo info)
        {
            SetVisible(false);
            _respawnTimer = respawnDelay;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (!_health.IsAlive)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0f)
                {
                    _health.Revive();
                    SetVisible(true);
                }
                return;
            }

            if (strafeAmplitude > 0f)
            {
                _phase += dt * strafeSpeed;
                transform.position = _origin + transform.right * Mathf.Sin(_phase) * strafeAmplitude;
            }

            _flash = Mathf.MoveTowards(_flash, 0f, dt * 6f);
            Color c = Color.Lerp(baseColor, hitColor, _flash);
            for (int i = 0; i < _materials.Length; i++)
            {
                // Head keeps its accent tint; body lerps.
                if (_isHead[i])
                    _materials[i].color = Color.Lerp(hitColor * 0.8f, Color.white, _flash);
                else
                    _materials[i].color = c;
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in _renderers) r.enabled = visible;
            foreach (var c in _colliders) c.enabled = visible;
        }
    }
}
