using System.Collections.Generic;
using UnityEngine;

namespace Vamp.Progression
{
    public enum CosmeticType
    {
        Icon, Frame, Banner, Title, KillEffect, Emote, CrosshairStyle, WeaponSkin, CharacterSkin, ProfileBackground, UITheme, KillFeedStyle,
        WeaponTrail, Gloves
    }

    /// <summary>WeaponLevel = camo unlocked per weapon at that weapon's level. Shop = bought with ranked coins.</summary>
    public enum UnlockSource { Default, Level, Prestige, Challenge, Event, WeaponLevel, Shop }
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
        /// <summary>Ranked shop price in coins (Source = Shop).</summary>
        public int Price;

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
                    case UnlockSource.WeaponLevel: return "REACH WEAPON LEVEL " + UnlockLevel;
                    case UnlockSource.Shop: return "RANKED SHOP · " + Price.ToString("N0") + " COINS";
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

            // Weapon camos: unlocked PER WEAPON by levelling that weapon (kills on real players).
            Simple("skin_default", "FACTORY", CosmeticType.WeaponSkin, UnlockSource.Default, 1, Red);
            Simple("skin_graphite", "GRAPHITE", CosmeticType.WeaponSkin, UnlockSource.WeaponLevel, 2, Steel);
            Simple("skin_bone", "BONE", CosmeticType.WeaponSkin, UnlockSource.WeaponLevel, 4, White);
            Simple("skin_bloodsteel", "BLOODSTEEL", CosmeticType.WeaponSkin, UnlockSource.WeaponLevel, 6, Red);
            Simple("skin_void", "VOID", CosmeticType.WeaponSkin, UnlockSource.WeaponLevel, 8, Dark);
            Camo("camo_toxic", "TOXIC", "Toxic", new Color(0.1f, 1f, 0.1f), UnlockSource.WeaponLevel, 10, 0);
            Camo("camo_riptide", "RIPTIDE", "Riptide", new Color(0.1f, 0.55f, 1f), UnlockSource.WeaponLevel, 14, 0);
            Camo("camo_bloodrush", "BLOOD RUSH", "BloodRush", new Color(0.85f, 0.05f, 0.05f), UnlockSource.WeaponLevel, 18, 0);
            // Ranked shop camos (work on every weapon).
            Camo("camo_voidreaper", "VOID REAPER", "VoidReaper", new Color(0.8f, 0.05f, 0.08f), UnlockSource.Shop, 1, 3950)
                .Description = "AGGRESSIVE RED AND BLACK FRACTURED SKIN. BUILT TO DOMINATE.";
            Camo("camo_dragonsvein", "DRAGON'S VEIN", "DragonsVein", new Color(0.95f, 0.2f, 0.05f), UnlockSource.Shop, 1, 3950)
                .Description = "ANCIENT DRAGON-SCALE PATTERN WITH MOLTEN RED VEINS.";

            // Bullet tracers (ids keep the old "trail_" prefix so saved profiles stay valid)
            Simple("trail_none", "STANDARD", CosmeticType.WeaponTrail, UnlockSource.Default, 1, White);
            Simple("trail_ember", "EMBER", CosmeticType.WeaponTrail, UnlockSource.Level, 12, new Color(1f, 0.45f, 0.1f));
            Shop("trail_phantom", "PHANTOM STRIKE", CosmeticType.WeaponTrail, 2450, new Color(1f, 0.1f, 0.15f),
                 "CRIMSON PARTICLE TRACER THAT SHREDS THROUGH THE BATTLEFIELD.");
            Shop("trail_razor", "RAZOR WHIP", CosmeticType.WeaponTrail, 2450, new Color(1f, 0.2f, 0.3f),
                 "HIGH-SPEED SLASH TRACER WITH ELECTRIC CRIMSON SPARKS.");
            Camo("camo_neongrid", "NEON GRID", "NeonGrid", new Color(0.1f, 0.9f, 1f), UnlockSource.Shop, 1, 2950).Description = "GLOWING CYAN AND MAGENTA CIRCUIT GRID.";
            Camo("camo_carbon", "MIDNIGHT CARBON", "Carbon", new Color(0.15f, 0.15f, 0.17f), UnlockSource.Shop, 1, 1950).Description = "WOVEN CARBON FIBRE WITH A DARK SHEEN.";
            Camo("camo_arctic", "ARCTIC FROST", "Arctic", new Color(0.75f, 0.9f, 1f), UnlockSource.Shop, 1, 2950).Description = "CRACKED GLACIER ICE, FROZEN SOLID.";
            Camo("camo_solar", "SOLAR FLARE", "Solar", new Color(1f, 0.6f, 0.1f), UnlockSource.Shop, 1, 3450).Description = "SWIRLING PLASMA TORN FROM THE SUN.";
            Camo("camo_venom", "VENOM", "Venom", new Color(0.6f, 0.2f, 0.9f), UnlockSource.Shop, 1, 2950).Description = "PURPLE HIDE WITH DRIPPING ACID GREEN.";
            Camo("camo_damascus", "GOLD DAMASCUS", "Damascus", new Color(1f, 0.8f, 0.3f), UnlockSource.Shop, 1, 4950).Description = "FOLDED GOLD STEEL. PURE FLEX.";
            Camo("camo_bloodmoon", "BLOOD MOON", "BloodMoon", new Color(0.6f, 0.05f, 0.05f), UnlockSource.Shop, 1, 3450).Description = "CRATERED CRIMSON MOON SURFACE.";
            Camo("camo_digital", "DIGITAL HUNTER", "Digital", new Color(0.3f, 0.5f, 0.25f), UnlockSource.Shop, 1, 1950).Description = "CLASSIC PIXEL CAMO IN FOREST TONES.";
            Camo("camo_marble", "PHANTOM MARBLE", "Marble", new Color(0.9f, 0.9f, 0.92f), UnlockSource.Shop, 1, 3950).Description = "POLISHED WHITE MARBLE WITH GREY VEINS.";
            Camo("camo_storm", "ELECTRIC STORM", "Storm", new Color(0.3f, 0.6f, 1f), UnlockSource.Shop, 1, 3950).Description = "BLUE LIGHTNING CRACKING ACROSS A DARK SKY.";

            // More shop tracers
            Shop("trail_void", "VOID BEAM", CosmeticType.WeaponTrail, 2950, new Color(0.5f, 0.1f, 0.9f), "A DARK PURPLE BEAM WITH A BLACK HOLE CORE.");
            Shop("trail_gold", "GOLDEN ARROW", CosmeticType.WeaponTrail, 3450, new Color(1f, 0.8f, 0.2f), "GOLD TRACER THAT LEAVES A SPARKLE BEHIND.");
            Shop("trail_frost", "FROSTBITE", CosmeticType.WeaponTrail, 2450, new Color(0.6f, 0.9f, 1f), "ICY TRACER THAT SHEDS FALLING SNOWFLAKES.");
            Shop("trail_toxic", "TOXIC SPIT", CosmeticType.WeaponTrail, 1950, new Color(0.3f, 1f, 0.2f), "ACID-GREEN TRACER THAT DRIPS AS IT FLIES.");
            Shop("trail_plasma", "PLASMA", CosmeticType.WeaponTrail, 2950, new Color(0.2f, 0.95f, 1f), "THICK CYAN PLASMA BOLT WITH ENERGY RINGS.");
            Shop("trail_hellfire", "HELLFIRE", CosmeticType.WeaponTrail, 3450, new Color(1f, 0.35f, 0.05f), "BURNING TRACER THAT THROWS RISING EMBERS.");
            Shop("trail_shadow", "SHADOW", CosmeticType.WeaponTrail, 1950, new Color(0.1f, 0.1f, 0.12f), "A BLACK SMOKY STREAK THAT LINGERS.");
            Shop("trail_rainbow", "RAINBOW RUSH", CosmeticType.WeaponTrail, 4450, new Color(1f, 0.4f, 0.8f), "EVERY COLOUR AT ONCE. IMPOSSIBLE TO MISS.");

            // Shop kill effects
            Shop("kfx_crimson_eruption", "CRIMSON ERUPTION", CosmeticType.KillEffect, 2950, Red,
                 "EXPLOSIVE ELIMINATION EFFECT WITH A SHATTERING IMPACT.");
            Shop("kfx_soul_reap", "SOUL REAP", CosmeticType.KillEffect, 2950, new Color(1f, 0.15f, 0.2f),
                 "DEMONIC ENERGY BURST THAT CLAIMS EVERY ELIMINATION.");
            // More shop kill effects
            Shop("kfx_frost", "FROST SHATTER", CosmeticType.KillEffect, 2450, new Color(0.6f, 0.9f, 1f), "YOUR TARGET BREAKS APART INTO FALLING ICE.");
            Shop("kfx_gold", "GOLDEN BURST", CosmeticType.KillEffect, 3950, new Color(1f, 0.8f, 0.2f), "A FOUNTAIN OF GOLD SPARKS. EXPENSIVE TASTE.");
            Shop("kfx_void", "VOID COLLAPSE", CosmeticType.KillEffect, 3450, new Color(0.5f, 0.1f, 0.9f), "EVERYTHING GETS PULLED IN - THEN BLOWS OUT.");
            Shop("kfx_toxic", "TOXIC CLOUD", CosmeticType.KillEffect, 1950, new Color(0.3f, 1f, 0.2f), "A BILLOWING CLOUD OF GREEN GAS.");
            Shop("kfx_thunder", "THUNDERSTRIKE", CosmeticType.KillEffect, 3950, new Color(0.4f, 0.7f, 1f), "A LIGHTNING BOLT CALLED DOWN FROM THE SKY.");
            Shop("kfx_bloodmoon", "BLOOD MOON", CosmeticType.KillEffect, 2950, new Color(0.8f, 0.05f, 0.1f), "A CRIMSON RING WITH RISING BLOOD ORBS.");
            Shop("kfx_phoenix", "PHOENIX", CosmeticType.KillEffect, 4450, new Color(1f, 0.45f, 0.05f), "A COLUMN OF FIRE RISES FROM THE ASHES.");
            Shop("kfx_ghost", "GHOST", CosmeticType.KillEffect, 2450, new Color(0.9f, 0.95f, 1f), "PALE SPIRITS DRIFT UP AND FADE AWAY.");


            // Gloves (first-person hands)
            Simple("glove_tactical", "TACTICAL", CosmeticType.Gloves, UnlockSource.Default, 1, new Color(0.1f, 0.1f, 0.11f));
            Simple("glove_none", "BARE HANDS", CosmeticType.Gloves, UnlockSource.Default, 1, new Color(0.85f, 0.65f, 0.55f));
            Shop("glove_fingerless", "FINGERLESS", CosmeticType.Gloves, 950, new Color(0.12f, 0.12f, 0.13f), "CUT-OFF TACTICAL GLOVES. BETTER TRIGGER FEEL, OR SO THEY SAY.");
            Shop("glove_desert", "DESERT OPS", CosmeticType.Gloves, 950, new Color(0.62f, 0.5f, 0.34f), "SAND-TAN FIELD GLOVES.");
            Shop("glove_crimson", "CRIMSON LEATHER", CosmeticType.Gloves, 1450, new Color(0.55f, 0.04f, 0.06f), "BLOOD-RED LEATHER. VERY VAMP.");
            Shop("glove_arctic", "ARCTIC", CosmeticType.Gloves, 1450, new Color(0.85f, 0.87f, 0.9f), "CLEAN WHITE COLD-WEATHER GLOVES.");
            Shop("glove_woodland", "WOODLAND CAMO", CosmeticType.Gloves, 1950, new Color(0.3f, 0.33f, 0.2f), "CLASSIC GREEN AND BROWN CAMO.");
            Shop("glove_urban", "URBAN CAMO", CosmeticType.Gloves, 1950, new Color(0.55f, 0.56f, 0.58f), "GREY CITY CAMO.");
            Shop("glove_toxic", "TOXIC", CosmeticType.Gloves, 1950, new Color(0.25f, 0.9f, 0.1f), "RADIOACTIVE GREEN SPLATTER.");
            Shop("glove_carbon", "CARBON FIBER", CosmeticType.Gloves, 2450, new Color(0.2f, 0.2f, 0.22f), "WOVEN CARBON WEAVE.");
            Shop("glove_vamp", "VAMP CIRCUIT", CosmeticType.Gloves, 2950, new Color(0.9f, 0.05f, 0.1f), "BLACK GLOVES LACED WITH RED CIRCUIT LINES.");
            Shop("glove_void", "VOID", CosmeticType.Gloves, 2950, new Color(0.4f, 0.08f, 0.75f), "SWIRLING PURPLE NEBULA.");
            Shop("glove_gold", "GOLD PLATED", CosmeticType.Gloves, 3950, new Color(1f, 0.78f, 0.25f), "SOLID GOLD. SUBTLE.");

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

        /// <summary>Patterned weapon camo (texture in Resources/Camos).</summary>
        private static CosmeticItem Camo(string id, string name, string texture, Color color, UnlockSource source, int level, int price)
        {
            var item = Simple(id, name, CosmeticType.WeaponSkin, source, level, color);
            item.Texture = texture;
            item.Price = price;
            return item;
        }

        private static CosmeticItem Shop(string id, string name, CosmeticType type, int price, Color color, string description)
        {
            var item = Simple(id, name, type, UnlockSource.Shop, 1, color);
            item.Price = price;
            item.Description = description;
            return item;
        }

        /// <summary>Everything sold in the ranked shop.</summary>
        public static List<CosmeticItem> ShopItems()
        {
            Build();
            return _items.FindAll(i => i.Source == UnlockSource.Shop);
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
