using System.Collections.Generic;
using UnityEngine;
using Vamp.Core;

namespace Vamp.Progression
{
    /// <summary>Rank tiers shown next to rank points (RP).</summary>
    public static class RankTiers
    {
        private static readonly int[] Floors = { 0, 300, 700, 1200, 1800, 2500 };
        private static readonly string[] Names = { "BRONZE", "SILVER", "GOLD", "PLATINUM", "DIAMOND", "VAMPIRE" };
        private static readonly Color[] Colors =
        {
            new Color(0.72f, 0.45f, 0.25f), new Color(0.75f, 0.78f, 0.82f), new Color(1f, 0.78f, 0.2f),
            new Color(0.35f, 0.85f, 0.8f), new Color(0.45f, 0.65f, 1f), new Color(0.9f, 0.08f, 0.15f)
        };

        public static int Index(int rp)
        {
            int i = 0;
            while (i + 1 < Floors.Length && rp >= Floors[i + 1]) i++;
            return i;
        }

        public static string Name(int rp) { return Names[Index(rp)]; }
        public static Color TierColor(int rp) { return Colors[Index(rp)]; }

        /// <summary>0..1 progress to the next tier.</summary>
        public static float Progress(int rp)
        {
            int i = Index(rp);
            if (i + 1 >= Floors.Length) return 1f;
            return Mathf.InverseLerp(Floors[i], Floors[i + 1], rp);
        }

        public static int NextFloor(int rp)
        {
            int i = Index(rp);
            return i + 1 < Floors.Length ? Floors[i + 1] : Floors[i];
        }
    }

    public sealed class RankedResult
    {
        public int CoinsEarned;
        public int RpDelta;
        public int NewRp;
        public string OldTier, NewTier;
    }

    /// <summary>Weapon levels (camo unlocks), ranked points / coins and the ranked shop.</summary>
    public sealed partial class ProgressionService
    {
        public const int MaxWeaponLevel = 20;
        public const int WeaponXpPerKill = 100;
        public const int WeaponXpPerHeadshot = 25;
        public const int CoinsPerWin = 200;
        public const int CoinsPerKill = 20;

        public event System.Action<string, CosmeticItem> CamoUnlocked; // weapon id, camo

        // ------------------------------------------------------------------ Weapon levels

        public static int WeaponXpToNext(int level) { return 300 + 100 * (level - 1); }

        public int WeaponXp(string weaponId)
        {
            if (Profile == null || Profile.weapon_xp == null) return 0;
            foreach (var c in Profile.weapon_xp) if (c.id == weaponId) return c.count;
            return 0;
        }

        public int WeaponLevel(string weaponId)
        {
            int xp = WeaponXp(weaponId), level = 1;
            while (level < MaxWeaponLevel && xp >= WeaponXpToNext(level)) { xp -= WeaponXpToNext(level); level++; }
            return level;
        }

        /// <summary>XP inside the current weapon level and the amount needed for the next one.</summary>
        public void WeaponLevelProgress(string weaponId, out int into, out int needed)
        {
            int xp = WeaponXp(weaponId), level = 1;
            while (level < MaxWeaponLevel && xp >= WeaponXpToNext(level)) { xp -= WeaponXpToNext(level); level++; }
            into = xp;
            needed = level >= MaxWeaponLevel ? 0 : WeaponXpToNext(level);
        }

        /// <summary>Can this camo be used on this weapon? (default always; level camos per weapon; shop camos everywhere)</summary>
        public bool IsCamoUnlocked(string weaponId, string skinId)
        {
            if (string.IsNullOrEmpty(skinId) || skinId == "skin_default") return true;
            var item = CosmeticCatalog.Get(skinId);
            if (item == null || item.Type != CosmeticType.WeaponSkin || Profile == null) return false;
            switch (item.Source)
            {
                case UnlockSource.Default: return true;
                case UnlockSource.WeaponLevel: return WeaponLevel(weaponId) >= item.UnlockLevel;
                default: return Profile.unlocked.Contains(skinId);
            }
        }

        /// <summary>Adds weapon XP for kills on REAL players (bots never count). Returns the camos unlocked.</summary>
        public List<string> AddWeaponXp(string weaponId, int kills, int headshots)
        {
            var unlocked = new List<string>();
            if (Profile == null || string.IsNullOrEmpty(weaponId) || kills <= 0) return unlocked;
            int before = WeaponLevel(weaponId);
            int add = kills * WeaponXpPerKill + Mathf.Clamp(headshots, 0, kills) * WeaponXpPerHeadshot;
            IdCount entry = null;
            foreach (var c in Profile.weapon_xp) if (c.id == weaponId) entry = c;
            if (entry == null) { entry = new IdCount { id = weaponId }; Profile.weapon_xp.Add(entry); }
            entry.count += add;
            int after = WeaponLevel(weaponId);
            if (after > before)
            {
                string weaponName = Game.Weapons != null ? Game.Weapons.DisplayName(weaponId) : weaponId.ToUpperInvariant();
                Notify(NotificationKind.LevelUp, "WEAPON LEVEL UP", weaponName + " · LEVEL " + after);
                foreach (var camo in CosmeticCatalog.OfType(CosmeticType.WeaponSkin))
                {
                    if (camo.Source != UnlockSource.WeaponLevel || camo.UnlockLevel <= before || camo.UnlockLevel > after) continue;
                    unlocked.Add(camo.Id);
                    Notify(NotificationKind.Unlock, "NEW CAMO UNLOCKED", camo.Name + " FOR " + weaponName);
                    if (CamoUnlocked != null) CamoUnlocked(weaponId, camo);
                }
            }
            return unlocked;
        }

        // ------------------------------------------------------------------ Ranked

        /// <summary>After a ranked match: rank points up/down, coins for the win and for kills on real players.</summary>
        public RankedResult ApplyRanked(bool won, bool draw, int humanKills)
        {
            var r = new RankedResult();
            if (Profile == null) return r;
            humanKills = Mathf.Clamp(humanKills, 0, 60);
            r.OldTier = RankTiers.Name(Profile.rank_points);
            r.RpDelta = draw ? 0 : won ? 30 + Mathf.Min(10, humanKills * 2) : -20;
            Profile.rank_points = Mathf.Max(0, Profile.rank_points + r.RpDelta);
            Profile.best_rank_points = Mathf.Max(Profile.best_rank_points, Profile.rank_points);
            r.CoinsEarned = (won ? CoinsPerWin : 0) + humanKills * CoinsPerKill;
            Profile.ranked_coins += r.CoinsEarned;
            Profile.ranked_matches++;
            if (won) Profile.ranked_wins++;
            else if (!draw) Profile.ranked_losses++;
            r.NewRp = Profile.rank_points;
            r.NewTier = RankTiers.Name(Profile.rank_points);
            if (r.NewTier != r.OldTier) Notify(NotificationKind.LevelUp, "NEW RANK", r.NewTier);
            RaiseChanged();
            return r;
        }

        // ------------------------------------------------------------------ Ranked shop

        public bool Owns(string itemId) { return IsUnlocked(itemId); }

        public bool Purchase(string itemId, out string error)
        {
            error = "";
            var item = CosmeticCatalog.Get(itemId);
            if (Profile == null || item == null || item.Source != UnlockSource.Shop) { error = "NOT FOR SALE"; return false; }
            if (Profile.unlocked.Contains(itemId)) { error = "ALREADY OWNED"; return false; }
            if (Profile.ranked_coins < item.Price) { error = "NOT ENOUGH COINS - WIN RANKED MATCHES TO EARN MORE"; return false; }
            Profile.ranked_coins -= item.Price;
            Grant(itemId, true);
            RaiseChanged();
            return true;
        }

        /// <summary>Offline / custom matches: career stats only - no XP (XP comes from quick match and ranked).</summary>
        public void RecordStatsOnly(MatchReport report)
        {
            if (Profile == null || report == null) return;
            Sanitize(report);
            UpdateStats(report);
            Save();
            if (ProfileChanged != null) ProfileChanged();
        }
    }
}
