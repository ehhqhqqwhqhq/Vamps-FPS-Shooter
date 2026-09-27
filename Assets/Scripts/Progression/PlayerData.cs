using System;
using System.Collections.Generic;

namespace Vamp.Progression
{
    [Serializable]
    public sealed class IdCount
    {
        public string id;
        public int count;
    }

    [Serializable]
    public sealed class IdPair
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class LoadoutData
    {
        public string primary = "brute";
        public string secondary = "v9";
        public string melee = "knife";
        /// <summary>weapon id → skin id (cosmetic only).</summary>
        public List<IdPair> weaponSkins = new List<IdPair>();

        public string SkinFor(string weaponId)
        {
            foreach (var p in weaponSkins) if (p.key == weaponId) return p.value;
            return "skin_default";
        }

        public void SetSkin(string weaponId, string skinId)
        {
            foreach (var p in weaponSkins)
                if (p.key == weaponId) { p.value = skinId; return; }
            weaponSkins.Add(new IdPair { key = weaponId, value = skinId });
        }
    }

    /// <summary>Spec `player_profiles` (+ equipped cosmetics, unlocks and loadout).</summary>
    [Serializable]
    public sealed class PlayerProfileData
    {
        public string account_id;
        public int level = 1;
        /// <summary>XP inside the current level.</summary>
        public int xp = 0;
        public long total_xp = 0;
        public int prestige_level = 0;

        public string profile_icon = "icon_vamp_symbol";
        public string icon_frame = "frame_basic";
        public string banner = "banner_default";
        public string title = "title_rookie";
        public bool title_visible = true;
        public bool title_auto = true;
        public string kill_effect = "kfx_none";
        public string emote = "emote_nod";
        public string crosshair_style = "xhair_default";
        public string character_skin = "char_default";
        public string profile_background = "bg_default";
        public string ui_theme = "theme_default";
        public string killfeed_style = "kf_default";

        public string favorite_weapon = "";
        public string favorite_map = "";

        public List<string> unlocked = new List<string>();
        public List<string> seen_unlocks = new List<string>();
        public LoadoutData loadout = new LoadoutData();

        /// <summary>Equipped bullet trail (cosmetic).</summary>
        public string weapon_trail = "trail_none";
        /// <summary>Equipped first-person gloves (cosmetic).</summary>
        public string gloves = "glove_tactical";
        /// <summary>Weapon id → total weapon XP (weapon levels unlock camos for that weapon).</summary>
        public List<IdCount> weapon_xp = new List<IdCount>();
        /// <summary>One-time gifts already given to this account.</summary>
        public List<string> claimed_gifts = new List<string>();

        // ---- Ranked (online)
        public int rank_points = 0;
        public int ranked_coins = 0;
        public int ranked_wins = 0;
        public int ranked_losses = 0;
        public int ranked_matches = 0;
        public int best_rank_points = 0;
    }

    /// <summary>Spec `player_stats` plus movement statistics and per-weapon / per-map breakdowns.</summary>
    [Serializable]
    public sealed class PlayerStatsData
    {
        public string account_id;
        public int kills;
        public int deaths;
        public int wins;
        public int losses;
        public int matches;
        public double playtime;
        public float best_speed;
        public float longest_kill;
        public int headshots;
        public int assists;

        public int wall_runs;
        public int wall_jumps;
        public int slides;
        public int dashes;
        public int rocket_jumps;
        public int airborne_kills;
        public int high_speed_kills;
        public double distance_travelled;
        public float best_race_time;

        public List<IdCount> weapon_kills = new List<IdCount>();
        public List<IdCount> map_plays = new List<IdCount>();
        public List<string> milestones = new List<string>();

        public float KD { get { return deaths == 0 ? kills : (float)kills / deaths; } }

        public void Add(List<IdCount> list, string id, int amount)
        {
            if (string.IsNullOrEmpty(id)) return;
            foreach (var c in list)
                if (c.id == id) { c.count += amount; return; }
            list.Add(new IdCount { id = id, count = amount });
        }

        public static string Top(List<IdCount> list)
        {
            string best = "";
            int max = 0;
            foreach (var c in list)
                if (c.count > max) { max = c.count; best = c.id; }
            return best;
        }
    }

    [Serializable]
    public sealed class ChallengeState
    {
        public string id;
        public string period;
        public int progress;
        public bool completed;
    }

    [Serializable]
    public sealed class ChallengeSaveData
    {
        public List<ChallengeState> states = new List<ChallengeState>();
    }

    /// <summary>
    /// What a finished match reports for XP/stats. In multiplayer the SERVER builds this from authoritative
    /// events; the client never sends it. Offline, <see cref="ProgressionService"/> sanitises it (see LocalAuthority).
    /// </summary>
    [Serializable]
    public sealed class MatchReport
    {
        public string mode;
        public string map;
        public bool completed = true;
        public bool won;
        public int placement = 1;
        public int kills;
        public int deaths;
        public int assists;
        public int headshots;
        public int objectives;
        public int score;
        public float durationSeconds;
        public float bestSpeed;
        public float longestKill;
        public int wallRuns;
        public int wallJumps;
        public int slides;
        public int dashes;
        public int rocketJumps;
        public int airborneKills;
        public int highSpeedKills;
        public int movementCombos;
        public float distance;
        public float raceTime;
        public List<IdCount> weaponKills = new List<IdCount>();
    }

    public struct XpLine
    {
        public string Label;
        public int Xp;
    }

    public sealed class MatchXpResult
    {
        public List<XpLine> Lines = new List<XpLine>();
        public int Total;
        public int OldLevel;
        public int NewLevel;
        public int OldPrestige;
        public List<string> Unlocks = new List<string>();
        public List<string> CompletedChallenges = new List<string>();
        public int LevelsGained { get { return NewLevel - OldLevel; } }
    }
}
