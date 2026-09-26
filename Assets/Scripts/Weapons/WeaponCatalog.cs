using System.Collections.Generic;
using UnityEngine;

namespace Vamp.Weapons
{
    /// <summary>
    /// Registry of every weapon (loaded from Resources/VampWeaponCatalog). Loadouts, the server and stats refer to
    /// weapons by stable id.
    /// </summary>
    [CreateAssetMenu(menuName = "VAMP/Weapon Catalog", fileName = "VampWeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        public List<WeaponData> weapons = new List<WeaponData>();
        [Tooltip("Gun Game order (first → last).")]
        public List<WeaponData> gunGameOrder = new List<WeaponData>();

        public static WeaponCatalog Load()
        {
            var c = Resources.Load<WeaponCatalog>("VampWeaponCatalog");
            if (c == null)
            {
                Debug.LogWarning("[VAMP] Resources/VampWeaponCatalog missing - run VAMP ▸ Build All.");
                c = CreateInstance<WeaponCatalog>();
            }
            return c;
        }

        public WeaponData Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var w in weapons) if (w != null && w.id == id) return w;
            return null;
        }

        public List<WeaponData> InSlot(WeaponSlot slot)
        {
            return weapons.FindAll(w => w != null && w.slot == slot);
        }

        public string DisplayName(string id)
        {
            var w = Get(id);
            return w != null ? w.displayName : (id ?? "").ToUpperInvariant();
        }
    }
}
