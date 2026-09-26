using UnityEngine;

namespace Vamp.Combat
{
    public enum DamageType
    {
        Bullet,
        Pellet,
        Explosion,
        Energy,
        Melee,
        World
    }

    /// <summary>
    /// Everything needed to apply and later validate a hit. In multiplayer, the CLIENT only sends
    /// "I fired at tick T with this aim"; the SERVER rebuilds DamageInfo after lag-compensated
    /// validation. Clients never send damage numbers.
    /// </summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public GameObject Instigator;
        public string WeaponId;
        public Vector3 Point;
        public Vector3 Direction;
        public bool IsHeadshot;
        public float Distance;
    }

    public struct DamageResult
    {
        public float HealthDamage;
        public float ArmorDamage;
        public bool Killed;
        public bool IsHeadshot;
        public float TotalDamage { get { return HealthDamage + ArmorDamage; } }
        public static readonly DamageResult None = new DamageResult();
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        GameObject Owner { get; }
        DamageResult ApplyDamage(in DamageInfo info);
    }
}
