using UnityEngine;

namespace Vamp.Match
{
    /// <summary>Placed by level designers. Team -1 = any team (FFA), 0/1 = team-preferred spawns.</summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private int team = -1;
        public int Team { get { return team; } set { team = value; } }
        public float LastUsed { get; set; } = -999f;

        private void OnDrawGizmos()
        {
            Gizmos.color = team == 0 ? new Color(0.3f, 0.6f, 1f) : team == 1 ? new Color(1f, 0.3f, 0.3f) : Color.white;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward);
        }
    }
}
