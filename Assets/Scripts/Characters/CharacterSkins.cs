using System.Collections.Generic;
using UnityEngine;
using Vamp.Progression;

namespace Vamp.Characters
{
    /// <summary>
    /// How each character skin (CosmeticType.CharacterSkin) looks. Drawn with the VAMP/Character shader: a colour /
    /// metal finish and optionally a pattern from Resources/Camos that sticks to the body, plus a glowing rim in the
    /// player's team colour so friend and foe stay easy to tell apart whatever they wear.
    /// </summary>
    public static class CharacterSkins
    {
        public struct Style
        {
            public Color Color;
            public bool TeamBase;          // body takes the team colour (the default OPERATIVE look)
            public float Metallic, Smoothness;
            public string Pattern;         // texture in Resources/Camos (null = plain)
            public float PatternScale;
            public float PatternGlow;
            public Color Emission;
        }

        private static readonly Dictionary<string, Style> Styles = new Dictionary<string, Style>
        {
            { "char_default",  new Style { Color = Color.white, TeamBase = true, Smoothness = 0.35f } },
            { "char_urban",    new Style { Color = Color.white, Pattern = "Urban", PatternScale = 1.1f, Smoothness = 0.25f } },
            { "char_woodland", new Style { Color = Color.white, Pattern = "Woodland", PatternScale = 1.1f, Smoothness = 0.2f } },
            { "char_night",    new Style { Color = new Color(0.05f, 0.05f, 0.06f), Metallic = 0.2f, Smoothness = 0.6f } },
            { "char_desert",   new Style { Color = Color.white, Pattern = "Desert", PatternScale = 1.1f, Smoothness = 0.2f } },
            { "char_chrome",   new Style { Color = new Color(0.92f, 0.92f, 0.95f), Metallic = 1f, Smoothness = 0.93f } },
            { "char_crimson",  new Style { Color = new Color(0.6f, 0.02f, 0.04f), Metallic = 0.3f, Smoothness = 0.7f, Emission = new Color(0.12f, 0f, 0.01f) } },
            { "char_tiger",    new Style { Color = Color.white, Pattern = "Tiger", PatternScale = 0.9f, Smoothness = 0.3f } },
            { "char_magma",    new Style { Color = Color.white, Pattern = "Magma", PatternScale = 1.2f, PatternGlow = 2.5f, Smoothness = 0.4f } },
            { "char_galaxy",   new Style { Color = Color.white, Pattern = "Galaxy", PatternScale = 0.8f, PatternGlow = 1.2f, Smoothness = 0.6f } },
            { "char_gold",     new Style { Color = new Color(1f, 0.76f, 0.3f), Metallic = 1f, Smoothness = 0.85f } },
            { "char_obsidian", new Style { Color = Color.white, Pattern = "Obsidian", PatternScale = 1.2f, PatternGlow = 3f, Metallic = 0.3f, Smoothness = 0.85f } },
            { "char_neon",     new Style { Color = Color.white, Pattern = "NeonGrid", PatternScale = 1.2f, PatternGlow = 2.5f, Smoothness = 0.5f } },
            { "char_phantom",  new Style { Color = new Color(0.85f, 0.9f, 1f), Smoothness = 0.8f, Emission = new Color(0.25f, 0.3f, 0.4f) } },
            { "char_carbon",   new Style { Color = Color.white, Pattern = "Carbon", PatternScale = 3f, Metallic = 0.2f, Smoothness = 0.75f } },
            { "char_candy",    new Style { Color = Color.white, Pattern = "Candy", PatternScale = 0.9f, Smoothness = 0.6f } },
            { "char_diamond",  new Style { Color = Color.white, Pattern = "Diamond", PatternScale = 1.4f, PatternGlow = 0.5f, Metallic = 0.5f, Smoothness = 0.95f } },
            { "char_storm",    new Style { Color = Color.white, Pattern = "Storm", PatternScale = 1f, PatternGlow = 2f, Smoothness = 0.5f } },
        };

        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _shader;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Cache.Clear(); }

        public static Shader CharShader
        {
            get
            {
                if (_shader == null) _shader = Shader.Find("VAMP/Character");
                return _shader;
            }
        }

        public static Style For(string skinId)
        {
            Style s;
            if (!string.IsNullOrEmpty(skinId) && Styles.TryGetValue(skinId, out s)) return s;
            return Styles["char_default"];
        }

        /// <summary>The local player's equipped character skin.</summary>
        public static string Local
        {
            get
            {
                var p = Core.Game.Progression;
                return p != null && p.Profile != null ? p.Profile.character_skin : "char_default";
            }
        }

        /// <summary>Random skin for bots, so matches show the collection off.</summary>
        public static string RandomSkin()
        {
            var list = CosmeticCatalog.OfType(CosmeticType.CharacterSkin);
            if (list.Count == 0) return "char_default";
            return UnityEngine.Random.value < 0.35f ? "char_default" : list[UnityEngine.Random.Range(0, list.Count)].Id;
        }

        /// <summary>Material for this skin worn by a player of the given team colour (null when the shader is missing).</summary>
        public static Material MaterialFor(string skinId, Color team, float teamGlow)
        {
            var sh = CharShader;
            if (sh == null) return null;
            if (string.IsNullOrEmpty(skinId) || !Styles.ContainsKey(skinId)) skinId = "char_default";
            string key = skinId + "|" + ColorUtility.ToHtmlStringRGB(team) + "|" + teamGlow.ToString("F2");
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;

            var s = For(skinId);
            m = new Material(sh) { name = "Character_" + skinId };
            m.SetColor("_BaseColor", s.TeamBase ? team : s.Color);
            m.SetFloat("_Metallic", s.Metallic);
            m.SetFloat("_Smoothness", s.Smoothness);
            m.SetColor("_EmissionColor", s.TeamBase ? team * teamGlow : s.Emission);
            var tex = !string.IsNullOrEmpty(s.Pattern) ? Resources.Load<Texture2D>("Camos/" + s.Pattern) : null;
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetFloat("_PatternStrength", 1f);
                m.SetFloat("_PatternScale", s.PatternScale > 0f ? s.PatternScale : 1f);
                m.SetFloat("_PatternGlow", s.PatternGlow);
            }
            // Team rim: subtle on the team-coloured default, strong on every other skin.
            m.SetColor("_RimColor", team * (s.TeamBase ? 0.6f : 1.4f));
            m.SetFloat("_RimPower", s.TeamBase ? 3f : 2.6f);
            Cache[key] = m;
            return m;
        }
    }
}
