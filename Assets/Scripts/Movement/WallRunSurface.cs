using UnityEngine;

namespace Vamp.Movement
{
    /// <summary>
    /// Marks a collider (or any of its children) as a designated wall-run surface.
    /// Only used when MovementSettings.requireWallRunSurface is true.
    /// Level designers place these intentionally; map readability comes from the matching visual trim.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallRunSurface : MonoBehaviour
    {
        [Tooltip("Optional per-surface speed multiplier for special fast walls.")]
        [SerializeField] private float speedMultiplier = 1f;
        public float SpeedMultiplier { get { return speedMultiplier; } }
    }
}
