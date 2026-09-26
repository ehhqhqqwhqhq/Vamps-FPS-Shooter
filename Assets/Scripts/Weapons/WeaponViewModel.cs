using UnityEngine;
using Vamp.Core;
using Vamp.Movement;

namespace Vamp.Weapons
{
    /// <summary>
    /// Procedural first-person weapon presentation: placeholder block models, hip/ADS positioning,
    /// fire kick, reload dip, look sway and movement bob. Uses WeaponData.viewModelPrefab when assigned.
    /// Presentation only - never affects where bullets go.
    /// </summary>
    public sealed class WeaponViewModel : MonoBehaviour
    {
        [SerializeField] private WeaponController weapons;
        [SerializeField] private InputController input;
        [SerializeField] private MovementController movement;
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material accentMaterial;

        [Header("Placement")]
        [SerializeField] private Vector3 hipPosition = new Vector3(0.2f, -0.2f, 0.42f);
        [SerializeField] private Vector3 adsPosition = new Vector3(0f, -0.13f, 0.32f);

        [Header("Motion")]
        [SerializeField] private float kickBack = 0.07f;
        [SerializeField] private float kickPitch = 8f;
        [SerializeField] private float kickRecover = 14f;
        [SerializeField] private float swayAmount = 0.02f;
        [SerializeField] private float swayMax = 4f;
        [SerializeField] private float bobAmount = 0.012f;
        [SerializeField] private float bobFrequency = 11f;

        private Transform _model;
        private Transform _muzzle;
        private float _kick;
        private Vector2 _sway;
        private float _bobPhase;
        private float _equipLower;
        private Vector3 _hip, _ads;

        private void Awake()
        {
            if (weapons == null) weapons = GetComponentInParent<WeaponController>();
            if (input == null) input = GetComponentInParent<InputController>();
            if (movement == null) movement = GetComponentInParent<MovementController>();
        }

        private void OnEnable()
        {
            if (weapons == null) return;
            weapons.WeaponChanged += Rebuild;
            weapons.Fired += OnFired;
            if (weapons.Current != null) Rebuild(weapons.Current);
        }

        private void OnDisable()
        {
            if (weapons == null) return;
            weapons.WeaponChanged -= Rebuild;
            weapons.Fired -= OnFired;
        }

        private void OnFired(WeaponData d)
        {
            _kick = Mathf.Min(1.5f, _kick + (d.pelletsPerShot > 1 || d.delivery == DeliveryType.Projectile ? 1f : 0.45f));
        }

        private void Rebuild(WeaponData d)
        {
            if (_model != null) Destroy(_model.gameObject);
            _model = null;
            if (d == null) return;
            _equipLower = 1f;

            GameObject root;
            if (d.viewModelPrefab != null)
            {
                // Imported model: origin = grip, barrel +Z, real-world scale (see VampArtBuilder).
                root = Instantiate(d.viewModelPrefab, transform);
                foreach (var c in root.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.gameObject.layer = 2;
                }
                var sight = root.transform.Find("Sight");
                _ads = sight != null ? new Vector3(0f, -sight.localPosition.y - 0.005f, 0.24f) : adsPosition;
                _hip = d.slot == WeaponSlot.Secondary ? new Vector3(0.17f, -0.16f, 0.36f) : new Vector3(0.18f, -0.17f, 0.24f);
            }
            else
            {
                root = new GameObject("VM_" + d.id);
                root.transform.SetParent(transform, false);
                BuildPlaceholder(root.transform, d);
                _ads = adsPosition;
                _hip = hipPosition;
            }
            _model = root.transform;
            _model.localPosition = _hip;
            _model.localRotation = Quaternion.identity;

            _muzzle = _model.Find("Muzzle");
            if (_muzzle == null)
            {
                _muzzle = new GameObject("Muzzle").transform;
                _muzzle.SetParent(_model, false);
                _muzzle.localPosition = new Vector3(0f, 0.03f, 0.45f);
            }
            if (weapons != null) weapons.SetMuzzle(_muzzle);
        }

        private void BuildPlaceholder(Transform root, WeaponData d)
        {
            // Rough silhouettes so weapons read differently at a glance.
            float length = 0.35f, height = 0.09f, width = 0.07f;
            if (d.delivery == DeliveryType.Melee) { length = 0.45f; height = 0.035f; width = 0.012f; } // blade
            else if (d.isSniper) { length = 0.8f; height = 0.08f; width = 0.06f; }             // sniper
            else if (d.usesHeat) { length = 0.42f; height = 0.12f; width = 0.1f; }             // energy
            else if (d.pelletsPerShot > 1) { length = 0.5f; height = 0.11f; width = 0.09f; }  // shotgun
            else if (d.delivery == DeliveryType.Projectile) { length = 0.6f; height = 0.14f; width = 0.14f; } // launcher
            else if (d.slot == WeaponSlot.Secondary) { length = 0.22f; height = 0.08f; width = 0.05f; }   // pistol

            Part(root, "Body", new Vector3(0f, 0f, length * 0.35f), new Vector3(width, height, length), bodyMaterial);
            Part(root, "Grip", new Vector3(0f, -height * 0.9f, 0f), new Vector3(width * 0.8f, height * 1.3f, width), bodyMaterial);
            Part(root, "Accent", new Vector3(0f, height * 0.55f, length * 0.4f), new Vector3(width * 1.02f, height * 0.12f, length * 0.7f), SkinMaterial(d));
            if (d.isSniper) Part(root, "Scope", new Vector3(0f, height * 1.1f, length * 0.25f), new Vector3(width * 0.8f, width * 0.8f, length * 0.35f), bodyMaterial);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0f, 0f, length * 0.85f);
        }

        private Material _skinMat;

        /// <summary>Weapon skins are cosmetic: they only recolour the accent material.</summary>
        private Material SkinMaterial(WeaponData d)
        {
            var prog = Core.Game.Progression;
            if (prog == null || prog.Profile == null || accentMaterial == null) return accentMaterial;
            var skin = Progression.CosmeticCatalog.Get(prog.Profile.loadout.SkinFor(d.id));
            if (skin == null || skin.Id == "skin_default") return accentMaterial;
            if (_skinMat == null) _skinMat = new Material(accentMaterial);
            _skinMat.color = skin.Color;
            if (_skinMat.HasProperty("_EmissionColor")) _skinMat.SetColor("_EmissionColor", skin.Color * 0.6f);
            return _skinMat;
        }

        private void OnDestroy()
        {
            if (_skinMat != null) Destroy(_skinMat);
        }

        private static void Part(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = 2; // Ignore Raycast
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void LateUpdate()
        {
            if (_model == null || weapons == null) return;
            float dt = Time.deltaTime;

            _kick = Mathf.Lerp(_kick, 0f, 1f - Mathf.Exp(-kickRecover * dt));
            _equipLower = Mathf.MoveTowards(_equipLower, 0f, dt * 5f);

            Vector2 look = input != null ? input.Frame.LookDelta : Vector2.zero;
            Vector2 targetSway = Vector2.ClampMagnitude(-look * swayAmount, swayMax);
            _sway = Vector2.Lerp(_sway, targetSway, 1f - Mathf.Exp(-10f * dt));

            float speed = movement != null && movement.IsGrounded ? movement.HorizontalSpeed : 0f;
            _bobPhase += dt * bobFrequency * Mathf.Clamp01(speed / 8f);
            float bobScale = Mathf.Clamp01(speed / 10f) * (1f - weapons.AimBlend * 0.8f);
            Vector3 bob = new Vector3(Mathf.Cos(_bobPhase) * bobAmount, Mathf.Abs(Mathf.Sin(_bobPhase)) * bobAmount, 0f) * bobScale;

            float reload = weapons.IsReloading ? Mathf.Sin(weapons.ReloadProgress * Mathf.PI) : 0f;

            Vector3 pos = Vector3.Lerp(_hip, _ads, weapons.AimBlend) + bob;
            pos.z -= _kick * kickBack;
            pos.y -= (reload * 0.12f) + _equipLower * 0.25f;
            _model.localPosition = pos;
            _model.localRotation = Quaternion.Euler(-_kick * kickPitch + reload * 35f + _sway.y, _sway.x, reload * -15f);
            bool scoped = weapons.ShowScope;
            if (_model.gameObject.activeSelf == scoped) _model.gameObject.SetActive(!scoped);
        }
    }
}
