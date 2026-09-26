using UnityEngine;
using Vamp.Core;
using Vamp.Progression;
using Vamp.VFX;
using Vamp.Weapons;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// Live 3D preview for a shop card: a tiny stage far below the menu with its own camera rendering into a texture.
    /// Camos: the gun slowly turning with the camo applied. Trails: the gun firing that trail. Kill FX: the effect looping.
    /// </summary>
    public sealed class ShopPreview : MonoBehaviour
    {
        private static int _nextSlot;
        private CosmeticItem _item;
        private Camera _cam;
        private Transform _gun, _muzzle;
        private WeaponData _weapon;
        private float _timer;
        private Vector3 _center;
        public RenderTexture Texture { get; private set; }
        private bool _active = true;

        /// <summary>Only previews on screen render (the shop can have dozens of cards).</summary>
        public void SetActive(bool on)
        {
            if (_active == on) return;
            _active = on;
            if (_cam != null) _cam.enabled = on;
        }

        public static ShopPreview Create(CosmeticItem item, int width = 512, int height = 256)
        {
            int slot = _nextSlot++ % 64;
            var go = new GameObject("ShopPreview_" + item.Id);
            go.transform.position = new Vector3(slot * 30f, -3000f, 0f);
            var p = go.AddComponent<ShopPreview>();
            p.Build(item, width, height);
            return p;
        }

        private void Build(CosmeticItem item, int width, int height)
        {
            _item = item;
            _center = transform.position;
            Texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { name = "ShopPreview", antiAliasing = 2 };
            Texture.Create();

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.targetTexture = Texture;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.035f, 0.02f, 0.025f, 1f);
            _cam.fieldOfView = 28f;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 40f;

            // Lights: warm key + red rim (small range, only this stage)
            AddLight(new Vector3(-1.2f, 1.2f, -1.6f), new Color(1f, 0.95f, 0.9f), 2.2f, 6f);
            AddLight(new Vector3(1.4f, 0.4f, 1.2f), new Color(1f, 0.1f, 0.15f), 4f, 6f);

            if (item.Type == CosmeticType.Gloves)
            {
                // The butterfly-knife arms idling with these gloves on.
                var armsPrefab = Resources.Load<GameObject>("ButterflyArms");
                if (armsPrefab != null)
                {
                    var arms = Instantiate(armsPrefab, transform);
                    arms.transform.localPosition = Vector3.zero;
                    GloveSkins.Apply(arms, item.Id);
                    var anim = arms.GetComponentInChildren<Animation>();
                    if (anim != null && anim["Idle"] != null) anim.Play("Idle");
                }
                camGo.transform.localPosition = new Vector3(0.0f, -0.04f, -0.16f);
                camGo.transform.LookAt(transform.position + new Vector3(0.13f, -0.02f, 0.3f));
                _cam.fieldOfView = 26f;
                _cam.nearClipPlane = 0.02f;
                return;
            }
            if (item.Type == CosmeticType.KillEffect)
            {
                camGo.transform.localPosition = new Vector3(0f, 0.1f, -6.2f);
                _cam.fieldOfView = 38f;
                return;
            }

            _weapon = Game.Weapons != null ? Game.Weapons.Get("havoc") : null;
            if (_weapon != null && _weapon.viewModelPrefab != null)
            {
                var g = Instantiate(_weapon.viewModelPrefab, transform);
                foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);
                _gun = g.transform;
                _muzzle = _gun.Find("Muzzle");
                if (item.Type == CosmeticType.WeaponSkin) WeaponCamo.Apply(g, item.Id);
            }
            if (item.Type == CosmeticType.WeaponSkin)
            {
                camGo.transform.localPosition = new Vector3(0f, 0.02f, -1.3f);
                _cam.fieldOfView = 30f;
                if (_gun != null) { _gun.localPosition = new Vector3(-0.08f, -0.06f, 0f); _gun.localRotation = Quaternion.Euler(0f, 90f, 0f); }
            }
            else
            {
                camGo.transform.localPosition = new Vector3(1.1f, 0.02f, -2.2f);
                _cam.fieldOfView = 36f;
                if (_gun != null) { _gun.localPosition = new Vector3(-0.35f, -0.08f, 0f); _gun.localRotation = Quaternion.Euler(0f, 90f, 0f); }
            }
        }

        private void AddLight(Vector3 local, Color c, float intensity, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        private void Update()
        {
            if (_item == null || !_active) return;
            float dt = Time.unscaledDeltaTime;
            _timer -= dt;
            switch (_item.Type)
            {
                case CosmeticType.WeaponSkin:
                    if (_gun != null) _gun.localRotation = Quaternion.Euler(Mathf.Sin(Time.unscaledTime * 0.7f) * 6f, 90f + Mathf.Sin(Time.unscaledTime * 0.5f) * 25f, 0f);
                    break;
                case CosmeticType.WeaponTrail:
                    if (_timer <= 0f && _gun != null)
                    {
                        _timer = 0.22f;
                        Vector3 from = _muzzle != null ? _muzzle.position : _gun.position + _gun.forward * 0.5f;
                        Vector3 to = from + _gun.forward * 9f + Random.insideUnitSphere * 0.2f;
                        CosmeticFx.Tracer(from, to, _weapon, _item.Id, 1);
                        SimpleVfx.MuzzleFlash(from, 0.18f);
                    }
                    break;
                case CosmeticType.KillEffect:
                    if (_timer <= 0f)
                    {
                        _timer = 1.9f;
                        CosmeticFx.PlayKill(_item.Id, _center - Vector3.up * 0.9f);
                    }
                    break;
            }
        }

        private void OnDestroy()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }
    }
}
