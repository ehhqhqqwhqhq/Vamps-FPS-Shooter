using System.Collections.Generic;
using UnityEngine;
using Vamp.Progression;

namespace Vamp.Weapons
{
    /// <summary>
    /// Weapon camos / skins (cosmetic only). Patterned camos use the VAMP/Camo shader: the texture is projected in the
    /// model's own space (tri-planar), so it wraps any imported gun no matter how its UVs are laid out, and it doesn't
    /// swim when the gun moves. Colour skins (GRAPHITE, BONE...) just tint the gun.
    /// </summary>
    public static class WeaponCamo
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _shader;
        private static bool _shaderChecked;

        public static bool IsDefault(string skinId)
        {
            return string.IsNullOrEmpty(skinId) || skinId == "skin_default";
        }

        /// <summary>The local player's camo for a weapon (from their saved loadout).</summary>
        public static string LocalFor(WeaponData d)
        {
            var prog = Core.Game.Progression;
            if (d == null || prog == null || prog.Profile == null || prog.Profile.loadout == null) return null;
            return prog.Profile.loadout.SkinFor(d.id);
        }

        /// <summary>Stable small number for networking (0 = default).</summary>
        public static byte ToIndex(string skinId)
        {
            if (IsDefault(skinId)) return 0;
            var list = CosmeticCatalog.OfType(CosmeticType.WeaponSkin);
            for (int i = 0; i < list.Count; i++) if (list[i].Id == skinId) return (byte)(i + 1);
            return 0;
        }

        public static string FromIndex(byte index)
        {
            var list = CosmeticCatalog.OfType(CosmeticType.WeaponSkin);
            return index > 0 && index <= list.Count ? list[index - 1].Id : null;
        }

        /// <summary>Recolours every renderer under root. Default skin = leave the model's own materials.</summary>
        public static void Apply(GameObject root, string skinId)
        {
            if (root == null || IsDefault(skinId)) return;
            var skin = CosmeticCatalog.Get(skinId);
            if (skin == null || skin.Type != CosmeticType.WeaponSkin) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var src = r.sharedMaterials;
                var mats = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    // Knives: the camo goes on the handle, the blade stays steel.
                    bool blade = src[i] != null && src[i].name.IndexOf("blade", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    mats[i] = blade && !string.IsNullOrEmpty(skin.Texture) ? src[i] : MaterialFor(skin, src[i]);
                }
                r.sharedMaterials = mats;
            }
        }

        private static Material MaterialFor(CosmeticItem skin, Material original)
        {
            string key = skin.Id;
            if (string.IsNullOrEmpty(skin.Texture)) key += "|" + (original != null ? original.name : "none");
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;

            if (!string.IsNullOrEmpty(skin.Texture))
            {
                var tex = Resources.Load<Texture2D>("Camos/" + skin.Texture);
                var sh = CamoShader();
                if (sh != null)
                {
                    m = new Material(sh) { name = "Camo_" + skin.Texture };
                    m.SetTexture("_CamoTex", tex);
                    m.SetFloat("_Scale", 3.2f);
                }
                else
                {
                    // Fallback: URP Lit with the texture on the model's UVs.
                    m = new Material(LitShader()) { name = "Camo_" + skin.Texture };
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    m.mainTexture = tex;
                    m.color = Color.white;
                }
            }
            else
            {
                m = original != null ? new Material(original) : new Material(LitShader());
                m.name = "Skin_" + skin.Id;
                m.color = Color.Lerp(skin.Color, Color.black, 0.15f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.color);
            }
            Cache[key] = m;
            return m;
        }

        private static Shader CamoShader()
        {
            if (!_shaderChecked)
            {
                _shaderChecked = true;
                _shader = Resources.Load<Shader>("Shaders/VampCamo");
                if (_shader == null) _shader = Shader.Find("VAMP/Camo");
                if (_shader != null && !_shader.isSupported) _shader = null;
                if (_shader == null) Debug.LogWarning("[VAMP] Camo shader unavailable - using UV-mapped camo fallback.");
            }
            return _shader;
        }

        private static Shader LitShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            return s != null ? s : Shader.Find("Standard");
        }
    }
}
