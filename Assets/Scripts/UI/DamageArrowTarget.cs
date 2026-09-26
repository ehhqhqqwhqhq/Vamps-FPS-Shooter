using UnityEngine;

namespace Vamp.UI
{
    /// <summary>Remembers where damage came from so the HUD arrow keeps pointing there while you turn.</summary>
    public sealed class DamageArrowTarget : MonoBehaviour
    {
        public Vector3 Source;
    }
}
