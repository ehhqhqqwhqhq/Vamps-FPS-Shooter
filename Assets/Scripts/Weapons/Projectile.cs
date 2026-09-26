using System;
using UnityEngine;
using Vamp.Combat;

namespace Vamp.Weapons
{
    /// <summary>
    /// Raycast-stepped projectile (no Rigidbody: exact, cheap, easy to simulate on a server).
    /// Direct hit damage = WeaponData.damage, then explodes if the weapon has an explosion radius.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private WeaponData _data;
        private GameObject _instigator;
        private LayerMask _mask;
        private Vector3 _velocity;
        private float _life;
        private bool _done;
        private Action<IDamageable, DamageResult, DamageInfo> _onDamaged;

        public void Launch(WeaponData data, GameObject instigator, Vector3 position, Vector3 direction, LayerMask mask,
                           Action<IDamageable, DamageResult, DamageInfo> onDamaged)
        {
            _data = data;
            _instigator = instigator;
            _mask = mask;
            _onDamaged = onDamaged;
            _velocity = direction.normalized * data.projectileSpeed;
            _life = data.projectileLifetime;
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        private void Update()
        {
            if (_done || _data == null) return;
            float dt = Time.deltaTime;
            _life -= dt;

            _velocity += Vector3.down * _data.projectileGravity * dt;
            Vector3 step = _velocity * dt;
            float len = step.magnitude;

            if (len > 0.0001f)
            {
                int n = Physics.SphereCastNonAlloc(transform.position, _data.projectileRadius, step / len, Hits, len, _mask, QueryTriggerInteraction.Ignore);
                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (IsInstigator(Hits[i].collider)) continue;
                    // SphereCast reports distance 0 / point zero for initial overlaps; treat as hit at current position.
                    if (Hits[i].distance < bestDist)
                    {
                        bestDist = Hits[i].distance;
                        best = i;
                    }
                }

                if (best >= 0)
                {
                    RaycastHit h = Hits[best];
                    Vector3 point = h.distance <= 0f ? transform.position : h.point + h.normal * 0.05f;
                    DirectHit(h.collider, point);
                    Detonate(point);
                    return;
                }

                transform.position += step;
                transform.rotation = Quaternion.LookRotation(_velocity);
            }

            if (_life <= 0f) Detonate(transform.position);
        }

        private bool IsInstigator(Collider c)
        {
            return _instigator != null && c.transform.IsChildOf(_instigator.transform);
        }

        private void DirectHit(Collider c, Vector3 point)
        {
            if (_data.damage <= 0f) return;
            Hitbox hb = c.GetComponent<Hitbox>();
            IDamageable target = hb != null ? hb.Owner : c.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) return;
            var info = new DamageInfo
            {
                Amount = _data.damage * (hb != null && hb.IsHead ? _data.headshotMultiplier : 1f),
                Type = DamageType.Bullet,
                Instigator = _instigator,
                WeaponId = _data.id,
                Point = point,
                Direction = _velocity.normalized,
                IsHeadshot = hb != null && hb.IsHead
            };
            DamageResult r = target.ApplyDamage(info);
            if (_onDamaged != null) _onDamaged(target, r, info);
        }

        public void Detonate(Vector3 point)
        {
            if (_done) return;
            _done = true;
            Explosion.Detonate(point, _data, _instigator, _mask, _onDamaged);
            Destroy(gameObject);
        }
    }
}
