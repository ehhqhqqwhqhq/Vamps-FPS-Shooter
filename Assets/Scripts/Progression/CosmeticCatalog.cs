using System.Collections.Generic;
using UnityEngine;

namespace Vamp.Progression
{
    public enum CosmeticType
    {
        Icon, Frame, Banner, Title, KillEffect, Emote, CrosshairStyle, WeaponSkin, CharacterSkin, ProfileBackground, UITheme, KillFeedStyle
    }

    public enum UnlockSource { Default, Level, Prestige, Challenge, Event }
    public enum IconShape { Circle, Diamond, Square, Shield }

    /// <summary>A cosmetic reward. NEVER affects gameplay - pure status.</summary>
    public sealed class CosmeticItem
    {
        public string Id;
        public string Name;
        public CosmeticType Type;
        public UnlockSource Source;
        public int UnlockLevel;
        public int Prestige;
        public string Glyph;
        public Color Color;
        public Color Accent;
        public IconShape Shape;
        public bool Animated;
        public string Description;
        /// <summary>Weapon camos: texture name in Resources/Camos (null = plain colour skin).</summary>
        public string Texture;

        public string UnlockText
        {
            get
            {
                switch (Source)
                {
                    case UnlockSource.Default: return "DEFAULT";
                    case UnlockSource.Level: return "UNLOCKS AT LEVEL " + UnlockLevel;
                    case UnlockSource.Prestige: return "UNLOCKS AT PRESTIGE " + Prestige;
                    case UnlockSource.Challenge: return "CHALLENGE REWARD";
                    default: return "EVENT REWARD";
                }
            }
        }
    }

    /// <summary>
    /// Every cosmetic in the game, built from data in code (configurable here in one place).
    /// Level icons follow the spec: 1 VAMP SYMBOL, 5 CLAW MARK, 10 VAMPIRE FANG, 15 BLOOD MOON, 20 VAMP SKULL,
    /// 25 DARK CROWN, 30 VAMP WOLF, 40 DEMONIC MASK, 50 ADVANCED VAMP EMBLEM, 75 ANIMATED BLOOD MOON,
    /// 100 LEGENDARY VAMP INSIGNIA - plus fillers, prestige, challenge and event icons (60 total).
    /// Art is placeholder glyph/shape styling until real icon art exists.
    /// </summary>
    public static class CosmeticCatalog
    {
        public static readonly Color Red = new Color(0.78f, 0.05f, 0.11f);
        public static readonly Color White = new Color(0.95f, 0.95f, 0.95f);
        public static readonly Color Steel = new Color(0.55f, 0.57f, 0.62f);
        public static readonly Color Dark = new Color(0.12f, 0.12f, 0.13f);
        public static readonly Color Blood = new Color(0.5f, 0.02f, 0.05f);

        private static List<CosmeticItem> _items;
        private static Dictionary<string, CosmeticItem> _byId;

        public static IReadOnlyList<CosmeticItem> All { get { Build(); return _items; } }

        public static CosmeticItem Get(string id)
        {
            Build();
            CosmeticItem item;
            return id != null && _byId.TryGetValue(id, out item) ? item : null;
        }

        public static List<CosmeticItem> OfType(CosmeticType type)
        {
            Build();
            return _items.FindAll(i => i.Type == type);
        }

        public static List<CosmeticItem> UnlocksAtLevel(int level)
        {
            Build();
            return _items.FindAll(i => i.Source == UnlockSource.Level && i.UnlockLevel == level);
        }

        public static List<CosmeticItem> UnlocksAtPrestige(int prestige)
        {
            Build();
            return _items.FindAll(i => i.Source == UnlockSource.Prestige && i.Prestige == prestige);
        }

        public static IEnumerable<string> DefaultUnlocks()
        {
            Build();
            foreach (var i in _items) if (i.Source == UnlockSource.Default) yield return i.Id;
        }

        /// <summary>Tier names from the spec (cosmetic).</summary>
        public static string TierName(int level)
        {
            if (level >= 100) return "VAMP";
            if (level >= 75) return "MASTER";
            if (level >= 50) return "VETERAN";
            if (level >= 40) return "ELITE";
            if (level >= 30) return "PREDATOR";
            if (level >= 20) return "HUNTER";
            if (level >= 10) return "RUNNER";
            return "ROOKIE";
        }

        public static string TierTitleId(int level)
        {
            return "title_" + TierName(level).ToLowerInvariant();
        }

        // ------------------------------------------------------------------ Data

        private static void Build()
        {
            if (_items != null) return;
            _items = new List<CosmeticItem>();

            // ---- Icons (level)
            Icon("icon_vamp_symbol", "VAMP SYMBOL", UnlockSource.Default, 1, "V", Red, IconShape.Diamond);
            Icon("icon_steel_v", "STEEL V", UnlockSource.Level, 3, "V", Steel, IconShape.Square);
            Icon("icon_claw_mark", "CLAW MARK", UnlockSource.Level, 5, "///", Red, IconShape.Circle);
            Icon("icon_night_eye", "NIGHT EYE", UnlockSource.Level, 7, "◉", White, IconShape.Circle);
            Icon("icon_vampire_fang", "VAMPIRE FANG", UnlockSource.Level, 10, "▼▼", White, IconShape.Shield);
            Icon("icon_red_dash", "RED DASH", UnlockSource.Level, 12, "»", Red, IconShape.Square);
            Icon("icon_blood_moon", "BLOOD MOON", UnlockSource.Level, 15, "●", Red, IconShape.Circle);
            Icon("icon_wall_runner", "WALL RUNNER", UnlockSource.Level, 18, "▌▌", Steel, IconShape.Square);
            Icon("icon_vamp_skull", "VAMP SKULL", UnlockSource.Level, 20, "☠", White, IconShape.Shield);
            Icon("icon_razor", "RAZOR", UnlockSource.Level, 22, "╳", Red, IconShape.Diamond);
            Icon("icon_dark_crown", "DARK CROWN", UnlockSource.Level, 25, "♛", Steel, IconShape.Shield);
            Icon("icon_bat_wing", "BAT WING", UnlockSource.Level, 28, "︾", Red, IconShape.Circle);
            Icon("icon_vamp_wolf", "VAMP WOLF", UnlockSource.Level, 30, "W", White, IconShape.Shield);
            Icon("icon_iron_cross", "IRON CROSS", UnlockSource.Level, 33, "✚", Steel, IconShape.Diamond);
            Icon("icon_crimson_eye", "CRIMSON EYE", UnlockSource.Level, 36, "◈", Red, IconShape.Diamond);
            Icon("icon_demonic_mask", "DEMONIC MASK", UnlockSource.Level, 40, "Ѫ", Red, IconShape.Shield);
            Icon("icon_night_hunter", "NIGHT HUNTER", UnlockSource.Level, 43, "⌖", White, IconShape.Circle);
            Icon("icon_black_heart", "BLACK HEART", UnlockSource.Level, 46, "♥", Blood, IconShape.Circle);
            Icon("icon_advanced_emblem", "ADVANCED VAMP EMBLEM", UnlockSource.Level, 50, "V", White, IconShape.Diamond, true);
            Icon("icon_twin_fang", "TWIN FANG", UnlockSource.Level, 55, "⋎⋎", Red, IconShape.Shield);
            Icon("icon_eclipse", "ECLIPSE", UnlockSource.Level, 60, "◐", White, IconShape.Circle);
            Icon("icon_bloodline", "BLOODLINE", UnlockSource.Level, 65, "∞", Red, IconShape.Square);
            Icon("icon_obsidian", "OBSIDIAN", UnlockSource.Level, 70, "◆", Steel, IconShape.Diamond);
            Icon("icon_animated_blood_moon", "ANIMATED BLOOD MOON", UnlockSource.Level, 75, "●", Red, IconShape.Circle, true);
            Icon("icon_nightfall", "NIGHTFALL", UnlockSource.Level, 80, "☾", White, IconShape.Circle);
            Icon("icon_apex", "APEX", UnlockSource.Level, 85, "▲", Red, IconShape.Diamond);
            Icon("icon_revenant", "REVENANT", UnlockSource.Level, 90, "Ѧ", White, IconShape.Shield);
            Icon("icon_sovereign", "SOVEREIGN", UnlockSource.Level, 95, "♚", Red, IconShape.Shield, true);
            Icon("icon_legendary_insignia", "LEGENDARY VAMP INSIGNIA", UnlockSource.Level, 100, "V", Red, IconShape.Diamond, true);

            // ---- Icons (prestige 1..10)
            for (int p = 1; p <= 10; p++)
                Add(new CosmeticItem
                {
                    Id = "icon_prestige_" + p, Name = "PRESTIGE " + ToRoman(p), Type = CosmeticType.Icon, Source = UnlockSource.Prestige,
                    Prestige = p, Glyph = new string('★', Mathf.Min(p, 3)) + (p > 3 ? p.ToString() : ""), Color = p >= 5 ? Red : White,
                    Accent = Dark, Shape = IconShape.Shield, Animated = p >= 5
                });

            // ---- Icons (challenges)
            string[] challengeIcons = { "SPEED DEMON", "HEADHUNTER", "AIR ACE", "WALL WALKER", "ROCKETEER", "SURVIVOR", "GUNSMITH",
                                        "SHARPSHOOTER", "BRAWLER", "SLIDER", "DASHER", "RACER", "CLOSER", "MARATHON", "UNTOUCHABLE" };
            string[] challengeGlyphs = { "⚡", "⊕", "✈", "▐", "☄", "♜", "⚙", "◎", "✊", "⟿", "»", "⚑", "✖", "∞", "◇" };
            for (int i = 0; i < challengeIcons.Length; i++)
                Add(new CosmeticItem
                {
                    Id = "icon_ch_" + challengeIcons[i].ToLowerInvariant().Replace(' ', '_'), Name = challengeIcons[i], Type = CosmeticType.Icon,
                    Source = UnlockSource.Challenge, Glyph = challengeGlyphs[i], Color = i % 2 == 0 ? Red : White, Accent = Dark,
                    Shape = (IconShape)(i % 4)
                });

            // ---- Icons (events)
            string[] eventIcons = { "LAUNCH NIGHT", "HALLOWEEN", "WINTER HUNT", "BLOOD FEST", "FOUNDERS", "ANNIVERSARY" };
            for (int i = 0; i < eventIcons.Length; i++)
                Add(new CosmeticItem
                {
                    Id = "icon_ev_" + eventIcons[i].ToLowerInvariant().Replace(' ', '_'), Name = eventIcons[i], Type = CosmeticType.Icon,
                    Source = UnlockSource.Event, Glyph = "✦", Color = Red, Accent = Dark, Shape = IconShape.Diamond
                });

            // ---- Frames
            Simple("frame_basic", "BASIC", CosmeticType.Frame, UnlockSource.Default, 1, Steel);
            Simple("frame_metal", "METAL", CosmeticType.Frame, UnlockSource.Level, 15, Steel);
            Simple("frame_razor", "RAZOR", CosmeticType.Frame, UnlockSource.Level, 26, Red);
            Simple("frame_vamp", "VAMP", CosmeticType.Frame, UnlockSource.Level, 33, Red);
            Simple("frame_hunter", "HUNTER", CosmeticType.Frame, UnlockSource.Level, 45, White);
            Simple("frame_veteran", "VETERAN", CosmeticType.Frame, UnlockSource.Level, 58, White);
            Simple("frame_master", "MASTER", CosmeticType.Frame, UnlockSource.Level, 78, Red);
            var legendary = Simple("frame_legendary", "LEGENDARY (ANIMATED)", CosmeticType.Frame, UnlockSource.Level, 100, Red);
            legendary.Animated = true;
            for (int p = 1; p <= 10; p += 3)
                Add(new CosmeticItem { Id = "frame_prestige_" + p, Name = "PRESTIGE FRAME " + ToRoman(p), Type = CosmeticType.Frame, Source = UnlockSource.Prestige, Prestige = p, Color = White, Animated = p >= 4 });

            // ---- Banners
            Simple("banner_default", "INDUSTRIAL", CosmeticType.Banner, UnlockSource.Default, 1, Dark);
            Simple("banner_red_line", "RED LINE", CosmeticType.Banner, UnlockSource.Level, 6, Red);
            Simple("banner_vertex", "VERTEX", CosmeticType.Banner, UnlockSource.Level, 13, Steel);
            Simple("banner_nightfall", "NIGHTFALL", CosmeticType.Banner, UnlockSource.Level, 24, Blood);
            Simple("banner_bloodrain", "BLOOD RAIN", CosmeticType.Banner, UnlockSource.Level, 38, Red);
            Simple("banner_monolith", "MONOLITH", CosmeticType.Banner, UnlockSource.Level, 52, White);
            Simple("banner_inferno", "INFERNO", CosmeticType.Banner, UnlockSource.Level, 68, Red);
            Simple("banner_throne", "THRONE", CosmeticType.Banner, UnlockSource.Level, 88, Steel);

            // ---- Titles (tiers from the spec + extras)
            Simple("title_rookie", "ROOKIE", CosmeticType.Title, UnlockSource.Default, 1, White);
            Simple("title_runner", "RUNNER", CosmeticType.Title, UnlockSource.Level, 10, White);
            Simple("title_hunter", "HUNTER", CosmeticType.Title, UnlockSource.Level, 20, White);
            Simple("title_predator", "PREDATOR", CosmeticType.Title, UnlockSource.Level, 30, Red);
            Simple("title_elite", "ELITE", CosmeticType.Title, UnlockSource.Level, 40, Red);
            Simple("title_veteran", "VETERAN", CosmeticType.Title, UnlockSource.Level, 50, Red);
            Simple("title_master", "MASTER", CosmeticType.Title, UnlockSource.Level, 75, Red);
            Simple("title_vamp", "VAMP", CosmeticType.Title, UnlockSource.Level, 100, Red);
            Simple("title_speed_demon", "SPEED DEMON", CosmeticType.Title, UnlockSource.Challenge, 0, Red);
            Simple("title_headhunter", "HEADHUNTER", CosmeticType.Title, UnlockSource.Challenge, 0, Red);
            Simple("title_wall_walker", "WALL WALKER", CosmeticType.Title, UnlockSource.Challenge, 0, White);

            // ---- Kill effects / emotes / crosshair styles / skins / backgrounds / themes / kill feed styles
            Simple("kfx_none", "NONE", CosmeticType.KillEffect, UnlockSource.Default, 1, White);
            Simple("kfx_red_burst", "RED BURST", CosmeticType.KillEffect, UnlockSource.Level, 16, Red);
            Simple("kfx_bats", "BAT SWARM", CosmeticType.KillEffect, UnlockSource.Level, 42, Dark);
            Simple("kfx_shatter", "SHATTER", CosmeticType.KillEffect, UnlockSource.Level, 72, White);

            Simple("emote_nod", "NOD", CosmeticType.Emote, UnlockSource.Default, 1, White);
            Simple("emote_salute", "SALUTE", CosmeticType.Emote, UnlockSource.Level, 9, White);
            Simple("emote_throat", "COLD STARE", CosmeticType.Emote, UnlockSource.Level, 35, Red);

            Simple("xhair_default", "STANDARD", CosmeticType.CrosshairStyle, UnlockSource.Default, 1, White);
            Simple("xhair_red", "BLOOD RED", CosmeticType.CrosshairStyle, UnlockSource.Level, 8, Red);
            Simple("xhair_steel", "STEEL", CosmeticType.CrosshairStyle, UnlockSource.Level, 27, Steel);

            Simple("skin_default", "FACTORY", CosmeticType.WeaponSkin, UnlockSource.Default, 1, Red);
            Simple("skin_graphite", "GRAPHITE", CosmeticType.WeaponSkin, UnlockSource.Level, 4, Steel);
            Simple("skin_bone", "BONE", CosmeticType.WeaponSkin, UnlockSource.Level, 17, White);
            Simple("skin_bloodsteel", "BLOODSTEEL", CosmeticType.WeaponSkin, UnlockSource.Level, 31, Red);
            Simple("skin_void", "VOID", CosmeticType.WeaponSkin, UnlockSource.Level, 62, Dark);
            Camo("camo_toxic", "TOXIC", "Toxic", new Color(0.1f, 1f, 0.1f));
            Camo("camo_riptide", "RIPTIDE", "Riptide", new Color(0.1f, 0.55f, 1f));
            Camo("camo_bloodrush", "BLOOD RUSH", "BloodRush", new Color(0.85f, 0.05f, 0.05f));

            Simple("char_default", "OPERATIVE", CosmeticType.CharacterSkin, UnlockSource.Default, 1, Steel);
            Simple("char_night", "NIGHT OPS", CosmeticType.CharacterSkin, UnlockSource.Level, 21, Dark);
            Simple("char_crimson", "CRIMSON", CosmeticType.CharacterSkin, UnlockSource.Level, 48, Red);

            Simple("bg_default", "CONCRETE", CosmeticType.ProfileBackground, UnlockSource.Default, 1, Dark);
            Simple("bg_furnace", "FURNACE", CosmeticType.ProfileBackground, UnlockSource.Level, 29, Red);

            Simple("theme_default", "VAMP RED", CosmeticType.UITheme, UnlockSource.Default, 1, Red);
            Simple("theme_mono", "MONOCHROME", CosmeticType.UITheme, UnlockSource.Level, 44, White);

            Simple("kf_default", "STANDARD", CosmeticType.KillFeedStyle, UnlockSource.Default, 1, White);
            Simple("kf_blood", "BLOOD TRAIL", CosmeticType.KillFeedStyle, UnlockSource.Level, 57, Red);

            _byId = new Dictionary<string, CosmeticItem>();
            foreach (var i in _items) _byId[i.Id] = i;
        }

        private static void Add(CosmeticItem item)
        {
            if (item.Accent == default(Color)) item.Accent = Dark;
            _items.Add(item);
        }

        private static void Icon(string id, string name, UnlockSource source, int level, string glyph, Color color, IconShape shape, bool animated = false)
        {
            Add(new CosmeticItem { Id = id, Name = name, Type = CosmeticType.Icon, Source = source, UnlockLevel = level, Glyph = glyph, Color = color, Accent = Dark, Shape = shape, Animated = animated });
        }

        /// <summary>Patterned weapon camo (texture in Resources/Camos). Unlocked for everyone.</summary>
        private static void Camo(string id, string name, string texture, Color color)
        {
            var item = Simple(id, name, CosmeticType.WeaponSkin, UnlockSource.Default, 1, color);
            item.Texture = texture;
        }

        private static CosmeticItem Simple(string id, string name, CosmeticType type, UnlockSource source, int level, Color color)
        {
            var item = new CosmeticItem { Id = id, Name = name, Type = type, Source = source, UnlockLevel = level, Color = color, Accent = Dark, Glyph = "" };
            Add(item);
            return item;
        }

        public static string ToRoman(int n)
        {
            string[] r = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return n >= 0 && n < r.Length ? r[n] : n.ToString();
        }
    }
}
