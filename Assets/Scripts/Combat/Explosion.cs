using System;
using System.Collections.Generic;
using UnityEngine;
using Vamp.Audio;
using Vamp.Movement;
using Vamp.VFX;
using Vamp.Weapons;

namespace Vamp.Combat
{
    /// <summary>
    /// Radial damage + knockback. Knockback on the instigator is what makes rocket jumping work.
    /// Server-authoritative in multiplayer (clients only predict their own self-knockback).
    /// </summary>
    public static class Explosion
    {
        private static readonly Collider[] Buffer = new Collider[64];
        private static readonly HashSet<IDamageable> Damaged = new HashSet<IDamageable>();
        private static readonly HashSet<MovementController> Pushed = new HashSet<MovementController>();
        private static readonly HashSet<Rigidbody> Bodies = new HashSet<Rigidbody>();

        public static void Detonate(Vector3 center, WeaponData data, GameObject instigator, LayerMask mask,
                                    Action<IDamageable, DamageResult, DamageInfo> onDamaged)
        {
            if (data == null || data.explosionRadius <= 0f) return;

            Damaged.Clear();
            Pushed.Clear();
            Bodies.Clear();

            float radius = data.explosionRadius;
            int count = Physics.OverlapSphereNonAlloc(center, radius, Buffer, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Buffer[i];
                if (c == null) continue;

                Vector3 closest = c.bounds.ClosestPoint(center);
                float dist = Vector3.Distance(center, closest);
                if (dist > radius) continue;
                float falloff = Mathf.Lerp(data.explosionMinFalloff, 1f, 1f - dist / radius);

                // --- Damage (once per damageable, blocked by walls)
                Hitbox hb = c.GetComponent<Hitbox>();
                IDamageable target = hb != null ? hb.Owner : c.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive && !Damaged.Contains(target) && HasLineOfSight(center, c, target, mask))
                {
                    Damaged.Add(target);
                    bool self = instigator != null && target.Owner == instigator;
                    var info = new DamageInfo
                    {
                        Amount = data.explosionDamage * falloff * (self ? data.selfDamageMultiplier : 1f),
                        Type = DamageType.Explosion,
                        Instigator = instigator,
                        WeaponId = data.id,
                        Point = closest,
                        Direction = (closest - center).normalized,
                        Distance = dist
                    };
                    DamageResult result = target.ApplyDamage(info);
                    if (onDamaged != null) onDamaged(target, result, info);
                }

                // --- Knockback on players (rocket jumps)
                MovementController mover = c.GetComponentInParent<MovementController>();
                if (mover != null && !Pushed.Contains(mover))
                {
                    Pushed.Add(mover);
                    bool self = instigator != null && mover.gameObject == instigator;
                    Vector3 body = mover.transform.position + Vector3.up * (mover.CurrentHeight * 0.5f);
                    Vector3 dir = body - center;
                    dir = dir.sqrMagnitude < 0.0001f ? Vector3.up : dir.normalized;
                    dir = (dir + Vector3.up * data.knockbackUpBias).normalized;
                    float k = data.explosionKnockback * falloff * (self ? data.selfKnockbackMultiplier : 1f);
                    mover.AddImpulse(dir * k);
                }

                // --- Physics props
                Rigidbody rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic && !Bodies.Contains(rb))
                {
                    Bodies.Add(rb);
                    rb.AddExplosionForce(data.explosionKnockback, center, radius, 0.3f, ForceMode.VelocityChange);
                }
            }

            SimpleVfx.Explosion(center, radius);
            AudioController.Play(SfxId.Explosion, center);
        }

        private static bool HasLineOfSight(Vector3 center, Collider targetCollider, IDamageable target, LayerMask mask)
        {
            Vector3 to = targetCollider.bounds.center;
            RaycastHit hit;
            if (!Physics.Linecast(center, to, out hit, mask, QueryTriggerInteraction.Ignore)) return true;
            if (hit.collider == targetCollider) return true;
            Hitbox hb = hit.collider.GetComponent<Hitbox>();
            IDamageable blocker = hb != null ? hb.Owner : hit.collider.GetComponentInParent<IDamageable>();
            return blocker == target;
        }
    }
}
