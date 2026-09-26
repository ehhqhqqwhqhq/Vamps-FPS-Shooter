using System.Collections.Generic;
using UnityEngine;
using Vamp.Core;

namespace Vamp.Weapons
{
    /// <summary>
    /// First-person glove cosmetics. Each design is a copy of the arms texture with the hands recoloured
    /// (Resources/Gloves/&lt;id&gt;.jpg, generated from the arms' UV layout). "glove_none" = bare hands.
    /// </summary>
    public static class GloveSkins
    {
        public const string ArmsRenderer = "Ch21_Eyelasshes";
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        /// <summary>The local player's equipped gloves.</summary>
        public static string Local
        {
            get
            {
                var p = Game.Progression;
                return p != null && p.Profile != null && !string.IsNullOrEmpty(p.Profile.gloves) ? p.Profile.gloves : "glove_tactical";
            }
        }

        public static void Apply(GameObject arms, string gloveId)
        {
            if (arms == null) return;
            foreach (var r in arms.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name != ArmsRenderer) continue;
                var baseMat = r.sharedMaterial;
                if (baseMat == null) continue;
                if (string.IsNullOrEmpty(gloveId) || gloveId == "glove_none") continue;
                r.sharedMaterial = For(baseMat, gloveId);
            }
        }

        private static Material For(Material baseMat, string gloveId)
        {
            Material m;
            if (Cache.TryGetValue(gloveId, out m) && m != null) return m;
            var tex = Resources.Load<Texture2D>("Gloves/" + gloveId);
            if (tex == null) return baseMat;
            m = new Material(baseMat) { name = "Arms_" + gloveId };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            Cache[gloveId] = m;
            return m;
        }
    }
}
