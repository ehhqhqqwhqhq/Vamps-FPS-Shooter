using UnityEngine;

namespace Vamp.Combat
{
    /// <summary>Public identity used by kill feed / scoreboard. Later populated from the account (username, level, icon).</summary>
    public sealed class CombatIdentity : MonoBehaviour
    {
        [SerializeField] private string displayName = "PLAYER";
        public string DisplayName { get { return displayName; } set { displayName = value; } }

        public static string NameOf(GameObject go)
        {
            if (go == null) return "WORLD";
            var id = go.GetComponentInParent<CombatIdentity>();
            return id != null ? id.DisplayName : go.name.ToUpperInvariant();
        }
    }
}
