using UnityEngine;

namespace Vamp.Combat
{
    /// <summary>
    /// Put on each hit collider of a character (head, body, limbs). Routes hits to the owning
    /// IDamageable and tells the weapon whether it was a headshot. In multiplayer these colliders are
    /// what the server rewinds for lag compensation.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Hitbox : MonoBehaviour
    {
        [SerializeField] private bool isHead;
        [Tooltip("Extra multiplier for this region (limbs < 1). Headshot multiplier comes from the weapon.")]
        [SerializeField] private float regionMultiplier = 1f;

        private IDamageable _owner;

        public bool IsHead { get { return isHead; } }
        public float RegionMultiplier { get { return regionMultiplier; } }
        public IDamageable Owner
        {
            get
            {
                if (_owner == null) _owner = GetComponentInParent<IDamageable>();
                return _owner;
            }
        }

        public void Configure(bool head, float multiplier)
        {
            isHead = head;
            regionMultiplier = multiplier;
        }
    }
}
