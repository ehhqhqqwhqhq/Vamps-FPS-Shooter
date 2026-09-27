using UnityEngine;

namespace Vamp.Weapons
{
    public enum WeaponSlot { Primary, Secondary, Melee }
    public enum FireMode { SemiAuto, FullAuto }
    public enum DeliveryType { Hitscan, Projectile, Melee }

    /// <summary>
    /// All weapon tuning lives here - one asset per weapon (V-9, RIPPER, HAVOC, BRUTE, WIDOW, BLAST, ARC, REAPER).
    /// The server uses the same asset (by <see cref="id"/>) to validate fire rate, ammo, range and damage.
    /// </summary>
    [CreateAssetMenu(menuName = "VAMP/Weapon Data", fileName = "WeaponData")]
    public sealed class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used by networking, stats and loadouts. Never change after release.")]
        public string id = "weapon";
        public string displayName = "WEAPON";
        public WeaponSlot slot = WeaponSlot.Primary;
        public FireMode fireMode = FireMode.SemiAuto;
        public DeliveryType delivery = DeliveryType.Hitscan;

        [Header("Damage")]
        [Tooltip("Damage per bullet/pellet at close range.")]
        public float damage = 20f;
        [Min(1)] public int pelletsPerShot = 1;
        public float headshotMultiplier = 1.5f;
        public float range = 150f;
        [Tooltip("Damage starts falling off at this distance...")]
        public float falloffStart = 20f;
        [Tooltip("...and reaches minDamageMultiplier here.")]
        public float falloffEnd = 50f;
        [Range(0f, 1f)] public float minDamageMultiplier = 0.5f;

        [Header("Fire Rate & Ammo")]
        [Tooltip("Rounds per minute.")]
        public float fireRate = 400f;
        [Min(1)] public int magazineSize = 12;
        public int reserveAmmo = 48;
        public float reloadTime = 1.4f;
        [Tooltip("Shell-by-shell reload (shotguns): reloadTime is per shell and firing interrupts.")]
        public bool reloadPerShell = false;
        public float equipTime = 0.3f;

        [Header("Accuracy (degrees, cone half-angle)")]
        public float hipSpread = 1.5f;
        public float adsSpread = 0.3f;
        public float movingSpreadAdd = 0.5f;
        public float airborneSpreadAdd = 1f;
        [Tooltip("Multi-pellet weapons: use a fixed, predictable pattern instead of random (competitive consistency).")]
        public bool fixedPelletPattern = true;

        [Header("Recoil / Feel")]
        public float recoilPitch = 1f;
        public float recoilYawRandom = 0.3f;
        [Tooltip("Visual camera punch (recovers, doesn't move aim).")]
        public float viewKick = 1.5f;
        public float screenShake = 0.15f;
        [Tooltip("Knockback on the shooter (opposite of aim) while airborne. Enables shotgun boosting.")]
        public float airborneSelfKnockback = 0f;

        [Header("Heat (energy weapons: no reload, manage heat instead)")]
        public bool usesHeat = false;
        [Tooltip("Heat added per shot (0..1 scale).")]
        public float heatPerShot = 0.04f;
        [Tooltip("Heat removed per second once the weapon has stopped firing for heatCoolDelay seconds.")]
        public float heatCoolRate = 0.45f;
        [Tooltip("Seconds after the last shot before the weapon starts to cool (no cooling while you hold the trigger).")]
        public float heatCoolDelay = 0.4f;
        [Tooltip("Lockout when the weapon overheats.")]
        public float overheatLockout = 1.4f;

        [Header("Melee (delivery = Melee)")]
        public float meleeRange = 2.4f;
        public float meleeRadius = 0.6f;
        [Tooltip("Damage multiplier when hitting from behind.")]
        public float backstabMultiplier = 2f;

        [Header("Aim Down Sights")]
        public bool canAim = true;
        [Tooltip("FOV multiplier while aiming (0.5 = 2x zoom).")]
        [Range(0.1f, 1f)] public float adsFovMultiplier = 0.8f;
        public float adsTime = 0.15f;
        [Tooltip("Uses the sniper sensitivity slider instead of the ADS slider.")]
        public bool isSniper = false;
        [Tooltip("Every shot goes exactly where the crosshair points - no spread at all (AWP).")]
        public bool perfectAccuracy;
        [Tooltip("Suppressor fitted: tiny muzzle flash, quieter shot (set by attachments at runtime).")]
        public bool suppressed;
        [Tooltip("Show a scope overlay while fully aimed (hides the view model).")]
        public bool scopeOverlay = false;

        [Header("Projectile (delivery = Projectile)")]
        public float projectileSpeed = 45f;
        public float projectileGravity = 0f;
        public float projectileLifetime = 5f;
        public float projectileRadius = 0.1f;

        [Header("Explosion (0 radius = none)")]
        public float explosionRadius = 0f;
        public float explosionDamage = 0f;
        [Tooltip("Velocity change applied at the explosion centre (falls off with distance).")]
        public float explosionKnockback = 0f;
        [Range(0f, 1f)] public float explosionMinFalloff = 0.3f;
        [Tooltip("Damage multiplier when you hit yourself (rocket jumping).")]
        [Range(0f, 1f)] public float selfDamageMultiplier = 0.35f;
        [Tooltip("Knockback multiplier on yourself (rocket jump strength).")]
        public float selfKnockbackMultiplier = 1.15f;
        [Tooltip("Extra upward bias on knockback direction so rocket jumps go up, not sideways.")]
        [Range(0f, 1f)] public float knockbackUpBias = 0.35f;

        [Header("Presentation (optional - prototype generates placeholders)")]
        public GameObject viewModelPrefab;
        public GameObject muzzleFlashPrefab;
        public GameObject impactPrefab;
        public AudioClip fireSound;
        public AudioClip reloadSound;
        public Color tracerColor = new Color(1f, 0.25f, 0.25f, 1f);
        public bool showTracers = true;

        public float SecondsPerShot { get { return 60f / Mathf.Max(1f, fireRate); } }

        public float DamageAtDistance(float distance)
        {
            if (distance <= falloffStart || falloffEnd <= falloffStart) return damage;
            float t = Mathf.InverseLerp(falloffStart, falloffEnd, distance);
            return damage * Mathf.Lerp(1f, minDamageMultiplier, t);
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            falloffEnd = Mathf.Max(falloffStart, falloffEnd);
            fireRate = Mathf.Max(1f, fireRate);
            reloadTime = Mathf.Max(0.05f, reloadTime);
        }
    }
}
