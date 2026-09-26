using System;
using UnityEngine;

namespace Vamp.Combat
{
    /// <summary>
    /// Health + armor. 100 HP / 50 armor by default. Armor absorbs a configurable share of incoming damage.
    /// SERVER-AUTHORITATIVE in multiplayer: only the server calls ApplyDamage/Heal/Kill; clients receive
    /// replicated values and fire the same events for UI/VFX.
    /// </summary>
    public sealed class HealthController : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float maxArmor = 50f;
        [SerializeField] private float startingArmor = 0f;
        [Tooltip("Share of incoming damage taken by armor while armor remains (0.66 = armor soaks two thirds).")]
        [Range(0f, 1f)] [SerializeField] private float armorAbsorption = 0.66f;
        [Tooltip("Multiplier for damage you deal to yourself (rocket jumping).")]
        [SerializeField] private float selfDamageMultiplier = 1f;
        [Header("Regeneration (0 = off; used by Training so rocket jumps don't drain you)")]
        [SerializeField] private float regenDelay = 0f;
        [SerializeField] private float regenPerSecond = 0f;

        private float _lastDamageTime = -999f;

        public float MaxHealth { get { return maxHealth; } }
        public float MaxArmor { get { return maxArmor; } }
        public float Health { get; private set; }
        public float Armor { get; private set; }
        public bool IsAlive { get { return Health > 0f; } }
        public GameObject Owner { get { return gameObject; } }
        public DamageInfo LastDamage { get; private set; }

        /// <summary>
        /// Match rules hook (friendly fire, no damage during countdown...). Return false to block damage.
        /// Set by MatchController (server-side in multiplayer).
        /// </summary>
        public static Func<DamageInfo, GameObject, bool> DamageFilter;

        /// <summary>
        /// Online hook: return true to take the hit away from local simulation (it is sent to the host, which applies
        /// it with <see cref="ApplyAuthoritative"/> and replicates the result to everyone).
        /// </summary>
        public static Func<HealthController, DamageInfo, bool> Intercept;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { DamageFilter = null; Intercept = null; }

        private bool _authoritative;

        public event Action<DamageInfo, DamageResult> Damaged;
        public event Action<DamageInfo> Died;
        public event Action Revived;
        public event Action HealthChanged;

        private void Awake()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            Health = maxHealth;
            Armor = Mathf.Clamp(startingArmor, 0f, maxArmor);
            if (HealthChanged != null) HealthChanged();
        }

        public void Revive()
        {
            ResetHealth();
            if (Revived != null) Revived();
        }

        public DamageResult ApplyDamage(in DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f || float.IsNaN(info.Amount)) return DamageResult.None;
            if (!_authoritative && Intercept != null && Intercept(this, info)) return DamageResult.None;
            if (DamageFilter != null && !DamageFilter(info, gameObject)) return DamageResult.None;

            float amount = info.Amount;
            if (info.Instigator == gameObject) amount *= selfDamageMultiplier;

            var result = new DamageResult { IsHeadshot = info.IsHeadshot };
            if (Armor > 0f)
            {
                float toArmor = Mathf.Min(Armor, amount * armorAbsorption);
                Armor -= toArmor;
                amount -= toArmor;
                result.ArmorDamage = toArmor;
            }

            _lastDamageTime = Time.time;
            float toHealth = Mathf.Min(Health, amount);
            Health -= toHealth;
            result.HealthDamage = toHealth;
            result.Killed = Health <= 0f;
            LastDamage = info;

            if (Damaged != null) Damaged(info, result);
            if (HealthChanged != null) HealthChanged();
            if (result.Killed)
            {
                KillFeed.Report(info, gameObject);
                if (Died != null) Died(info);
            }
            return result;
        }

        /// <summary>Host only: apply a validated hit, bypassing <see cref="Intercept"/>.</summary>
        public DamageResult ApplyAuthoritative(DamageInfo info)
        {
            _authoritative = true;
            try { return ApplyDamage(info); }
            finally { _authoritative = false; }
        }

        /// <summary>
        /// Clients: mirror a hit the host applied (health/armor are the host's values). Raises the same events as a
        /// local hit so HUD, damage arrows, death screen and kill feed work unchanged.
        /// </summary>
        public void ApplyReplicated(DamageInfo info, DamageResult result, float health, float armor)
        {
            bool wasAlive = IsAlive;
            Health = Mathf.Clamp(health, 0f, maxHealth);
            Armor = Mathf.Clamp(armor, 0f, maxArmor);
            LastDamage = info;
            _lastDamageTime = Time.time;
            if (Damaged != null) Damaged(info, result);
            if (HealthChanged != null) HealthChanged();
            if (wasAlive && result.Killed)
            {
                Health = 0f;
                KillFeed.Report(info, gameObject);
                if (Died != null) Died(info);
            }
        }

        /// <summary>Clients: snap to replicated values without events (respawn / late state).</summary>
        public void SetReplicated(float health, float armor)
        {
            bool revive = !IsAlive && health > 0f;
            Health = Mathf.Clamp(health, 0f, maxHealth);
            Armor = Mathf.Clamp(armor, 0f, maxArmor);
            if (HealthChanged != null) HealthChanged();
            if (revive && Revived != null) Revived();
        }

        public void SetRegen(float delay, float perSecond)
        {
            regenDelay = Mathf.Max(0f, delay);
            regenPerSecond = Mathf.Max(0f, perSecond);
        }

        private void Update()
        {
            if (regenPerSecond <= 0f || !IsAlive || Health >= maxHealth) return;
            if (Time.time - _lastDamageTime < regenDelay) return;
            Heal(regenPerSecond * Time.deltaTime);
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Min(maxHealth, Health + amount);
            if (HealthChanged != null) HealthChanged();
        }

        public void AddArmor(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Armor = Mathf.Min(maxArmor, Armor + amount);
            if (HealthChanged != null) HealthChanged();
        }

        public void Kill(DamageType type)
        {
            if (!IsAlive) return;
            var info = new DamageInfo { Amount = Health + Armor + 1f, Type = type, Instigator = null, Point = transform.position };
            Armor = 0f;
            ApplyDamage(info);
        }
    }
}
