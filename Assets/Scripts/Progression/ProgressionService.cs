using System;
using System.Collections.Generic;
using UnityEngine;
using Vamp.Accounts;
using Vamp.Core;

namespace Vamp.Progression
{
    /// <summary>
    /// Levels, XP, prestige, unlocks, stats and challenges for the logged-in account.
    /// OFFLINE AUTHORITY: this is the component that becomes server-side code. Clients will never grant XP,
    /// levels or unlocks themselves - they only display what the server returns. Even offline, every match report
    /// goes through <see cref="Sanitize"/> so obviously invalid values can't be banked.
    /// </summary>
    public sealed partial class ProgressionService
    {
        private readonly ProgressionConfig _config;
        private string _accountId;

        public PlayerProfileData Profile { get; private set; }
        public PlayerStatsData Stats { get; private set; }
        public ChallengeSaveData Challenges { get; private set; }
        public ProgressionConfig Config { get { return _config; } }
        public bool IsLoaded { get { return Profile != null; } }

        public event Action ProfileChanged;
        public event Action<int, int> LeveledUp;           // old, new
        public event Action<CosmeticItem> ItemUnlocked;

        public ProgressionService(IAccountService accounts)
        {
            _config = ProgressionConfig.Load();
            accounts.AccountCreated += a => CreateDefaults(a);
            accounts.LoggedIn += a => Load(a);
            accounts.LoggedOut += () => { Profile = null; Stats = null; Challenges = null; _accountId = null; IsTester = false; };
        }

        private string Dir { get { return "accounts/" + _accountId + "/"; } }

        // ------------------------------------------------------------------ Persistence

        private void CreateDefaults(AccountRecord account)
        {
            _accountId = account.id;
            Profile = new PlayerProfileData { account_id = account.id };
            foreach (var id in CosmeticCatalog.DefaultUnlocks()) Profile.unlocked.Add(id);
            Stats = new PlayerStatsData { account_id = account.id };
            Challenges = new ChallengeSaveData();
            Save();
        }

        private void Load(AccountRecord account)
        {
            _accountId = account.id;
            IsTester = IsTesterName(account.username);
            PlayerProfileData p;
            PlayerStatsData s;
            ChallengeSaveData c;
            Profile = JsonStore.TryLoad(Dir + "profile.json", out p) ? p : new PlayerProfileData { account_id = account.id };
            Stats = JsonStore.TryLoad(Dir + "stats.json", out s) ? s : new PlayerStatsData { account_id = account.id };
            Challenges = JsonStore.TryLoad(Dir + "challenges.json", out c) ? c : new ChallengeSaveData();
            Repair();
            Save();
            if (ProfileChanged != null) ProfileChanged();
        }

        /// <summary>Makes loaded data consistent (e.g. re-grants unlocks for the stored level, clamps values).</summary>
        private void Repair()
        {
            if (Profile.unlocked == null) Profile.unlocked = new List<string>();
            if (Profile.seen_unlocks == null) Profile.seen_unlocks = new List<string>();
            if (Profile.loadout == null) Profile.loadout = new LoadoutData();
            if (Profile.loadout.weaponSkins == null) Profile.loadout.weaponSkins = new List<IdPair>();
            if (Profile.weapon_xp == null) Profile.weapon_xp = new List<IdCount>();
            if (string.IsNullOrEmpty(Profile.weapon_trail)) Profile.weapon_trail = "trail_none";
            // Camos are earned per weapon now: drop any equipped camo that weapon hasn't unlocked yet.
            foreach (var pair in Profile.loadout.weaponSkins)
                if (!IsCamoUnlocked(pair.key, pair.value)) pair.value = "skin_default";
            Profile.rank_points = Mathf.Max(0, Profile.rank_points);
            Profile.ranked_coins = Mathf.Max(0, Profile.ranked_coins);
            if (Stats.weapon_kills == null) Stats.weapon_kills = new List<IdCount>();
            if (Stats.map_plays == null) Stats.map_plays = new List<IdCount>();
            if (Stats.milestones == null) Stats.milestones = new List<string>();
            if (Challenges.states == null) Challenges.states = new List<ChallengeState>();

            if (IsTester) ApplyTesterPerks();
            Profile.level = Mathf.Clamp(Profile.level, 1, _config.maxLevel);
            Profile.prestige_level = Mathf.Clamp(Profile.prestige_level, 0, _config.maxPrestige);
            Profile.xp = Mathf.Clamp(Profile.xp, 0, Mathf.Max(0, _config.XpToNext(Profile.level) - 1));

            foreach (var id in CosmeticCatalog.DefaultUnlocks()) Grant(id, false);
            for (int l = 1; l <= Profile.level; l++)
                foreach (var item in CosmeticCatalog.UnlocksAtLevel(l)) Grant(item.Id, false);
            for (int pr = 1; pr <= Profile.prestige_level; pr++)
                foreach (var item in CosmeticCatalog.UnlocksAtPrestige(pr)) Grant(item.Id, false);

            // Equipped items must be owned.
            Profile.profile_icon = OwnedOr(Profile.profile_icon, "icon_vamp_symbol");
            Profile.icon_frame = OwnedOr(Profile.icon_frame, "frame_basic");
            Profile.banner = OwnedOr(Profile.banner, "banner_default");
            Profile.title = OwnedOr(Profile.title, "title_rookie");
            Profile.kill_effect = OwnedOr(Profile.kill_effect, "kfx_none");
            Profile.weapon_trail = OwnedOr(Profile.weapon_trail, "trail_none");
            if (Profile.title_auto) Profile.title = CosmeticCatalog.TierTitleId(Profile.level);
        }

        private string OwnedOr(string id, string fallback)
        {
            return !string.IsNullOrEmpty(id) && Profile.unlocked.Contains(id) ? id : fallback;
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_accountId) || Profile == null) return;
            Profile.favorite_weapon = PlayerStatsData.Top(Stats.weapon_kills);
            Profile.favorite_map = PlayerStatsData.Top(Stats.map_plays);
            JsonStore.Save(Dir + "profile.json", Profile);
            JsonStore.Save(Dir + "stats.json", Stats);
            JsonStore.Save(Dir + "challenges.json", Challenges);
        }

        // ------------------------------------------------------------------ Queries

        public int XpToNext { get { return Profile == null ? 1 : _config.XpToNext(Profile.level); } }
        public bool IsMaxLevel { get { return Profile != null && Profile.level >= _config.maxLevel; } }
        public bool CanPrestige { get { return IsMaxLevel && Profile.prestige_level < _config.maxPrestige; } }
        public bool IsUnlocked(string id) { return Profile != null && Profile.unlocked.Contains(id); }

        public string LevelLabel
        {
            get
            {
                if (Profile == null) return "1";
                return Profile.prestige_level > 0 ? new string('★', Mathf.Min(3, Profile.prestige_level)) + " " + Profile.level : Profile.level.ToString();
            }
        }

        public List<(ChallengeDefinition def, ChallengeState state)> ActiveChallenges()
        {
            var list = new List<(ChallengeDefinition, ChallengeState)>();
            if (Challenges == null) return list;
            DateTime now = DateTime.Now;
            foreach (var d in ChallengeCatalog.ActiveDaily(now)) list.Add((d, StateFor(d, ChallengeCatalog.DailyPeriod(now))));
            foreach (var d in ChallengeCatalog.ActiveWeekly(now)) list.Add((d, StateFor(d, ChallengeCatalog.WeeklyPeriod(now))));
            return list;
        }

        private ChallengeState StateFor(ChallengeDefinition d, string period)
        {
            var s = Challenges.states.Find(x => x.id == d.Id && x.period == period);
            if (s == null)
            {
                s = new ChallengeState { id = d.Id, period = period };
                Challenges.states.Add(s);
                if (Challenges.states.Count > 60) Challenges.states.RemoveAt(0);
            }
            return s;
        }

        // ------------------------------------------------------------------ Match results

        public MatchXpResult ApplyMatch(MatchReport report)
        {
            var result = new MatchXpResult { OldLevel = Profile != null ? Profile.level : 1, OldPrestige = Profile != null ? Profile.prestige_level : 0 };
            if (Profile == null || report == null)
            {
                result.NewLevel = result.OldLevel;
                return result;
            }

            Sanitize(report);
            var c = _config;

            if (report.completed) Line(result, "MATCH COMPLETION", c.matchCompletion);
            if (report.won) Line(result, "MATCH VICTORY", c.matchVictory);
            Line(result, "KILLS", report.kills * c.kill);
            Line(result, "HEADSHOTS", report.headshots * c.headshot);
            Line(result, "ASSISTS", report.assists * c.assist);
            Line(result, "OBJECTIVES", report.objectives * c.objective);

            // Movement XP (anti-farm: per-event caps, per-match cap, scaled for very short matches)
            int movement = Mathf.Min(report.wallRuns, c.maxWallRunEvents) * c.wallRun
                         + Mathf.Min(report.wallJumps, c.maxWallJumpEvents) * c.wallJump
                         + Mathf.Min(report.rocketJumps, c.maxRocketJumpEvents) * c.rocketJump
                         + report.airborneKills * c.airborneKill
                         + report.highSpeedKills * c.highSpeedKill
                         + report.movementCombos * c.movementCombo;
            if (report.raceTime > 0f) movement += c.movementRaceFinish;
            float lengthScale = Mathf.Clamp01(report.durationSeconds / Mathf.Max(1f, c.fullMovementXpAfterSeconds));
            movement = Mathf.Min(c.movementXpCap, Mathf.RoundToInt(movement * lengthScale));
            Line(result, "MOVEMENT", movement);

            // Challenges
            int challengeXp = 0;
            foreach (var entry in ActiveChallenges())
            {
                if (entry.state.completed) continue;
                entry.state.progress = Mathf.Min(entry.def.Target, entry.state.progress + ChallengeCatalog.ValueFor(entry.def.Stat, report));
                if (entry.state.progress >= entry.def.Target)
                {
                    entry.state.completed = true;
                    challengeXp += entry.def.XpReward;
                    result.CompletedChallenges.Add(entry.def.Description);
                    if (!string.IsNullOrEmpty(entry.def.RewardItem) && Grant(entry.def.RewardItem, true)) result.Unlocks.Add(entry.def.RewardItem);
                    Notify(NotificationKind.Challenge, "CHALLENGE COMPLETE", entry.def.Description);
                }
            }
            Line(result, "CHALLENGE", challengeXp);

            foreach (var l in result.Lines) result.Total += l.Xp;

            UpdateStats(report);
            AddXp(result.Total, result.Unlocks);
            result.NewLevel = Profile.level;
            Save();
            if (ProfileChanged != null) ProfileChanged();
            return result;
        }

        private static void Line(MatchXpResult r, string label, int xp)
        {
            if (xp > 0) r.Lines.Add(new XpLine { Label = label, Xp = xp });
        }

        /// <summary>Clamp impossible values (the offline stand-in for server-side validation).</summary>
        private void Sanitize(MatchReport r)
        {
            r.durationSeconds = Mathf.Clamp(r.durationSeconds, 0f, 3600f);
            int maxKills = Mathf.CeilToInt(r.durationSeconds / 60f * _config.maxKillsPerMinute) + 1;
            r.kills = Mathf.Clamp(r.kills, 0, maxKills);
            r.headshots = Mathf.Clamp(r.headshots, 0, r.kills);
            r.airborneKills = Mathf.Clamp(r.airborneKills, 0, r.kills);
            r.highSpeedKills = Mathf.Clamp(r.highSpeedKills, 0, r.kills);
            r.assists = Mathf.Clamp(r.assists, 0, maxKills);
            r.deaths = Mathf.Max(0, r.deaths);
            r.objectives = Mathf.Clamp(r.objectives, 0, 50);
            r.movementCombos = Mathf.Clamp(r.movementCombos, 0, Mathf.CeilToInt(r.durationSeconds / 10f) + 1);
            r.bestSpeed = Mathf.Clamp(r.bestSpeed, 0f, 80f);
            r.longestKill = Mathf.Clamp(r.longestKill, 0f, 500f);
            if (r.durationSeconds < 20f && r.won) r.won = false; // not a real match
            if (r.weaponKills == null) r.weaponKills = new List<IdCount>();
        }

        private void UpdateStats(MatchReport r)
        {
            var s = Stats;
            s.kills += r.kills;
            s.deaths += r.deaths;
            s.assists += r.assists;
            s.headshots += r.headshots;
            if (r.completed && r.mode != "TRAINING")
            {
                s.matches++;
                if (r.won) s.wins++; else s.losses++;
            }
            s.playtime += r.durationSeconds;
            s.best_speed = Mathf.Max(s.best_speed, r.bestSpeed);
            s.longest_kill = Mathf.Max(s.longest_kill, r.longestKill);
            s.wall_runs += r.wallRuns;
            s.wall_jumps += r.wallJumps;
            s.slides += r.slides;
            s.dashes += r.dashes;
            s.rocket_jumps += r.rocketJumps;
            s.airborne_kills += r.airborneKills;
            s.high_speed_kills += r.highSpeedKills;
            s.distance_travelled += r.distance;
            if (r.raceTime > 0f && (s.best_race_time <= 0f || r.raceTime < s.best_race_time)) s.best_race_time = r.raceTime;
            foreach (var wk in r.weaponKills) s.Add(s.weapon_kills, wk.id, wk.count);
            s.Add(s.map_plays, r.map, 1);

            Milestone(s.kills >= 100, "100 KILLS");
            Milestone(s.kills >= 1000, "1,000 KILLS");
            Milestone(s.wins >= 10, "10 WINS");
            Milestone(s.wins >= 100, "100 WINS");
            Milestone(s.best_speed >= 25f, "REACHED 25 M/S");
            Milestone(s.best_speed >= 35f, "REACHED 35 M/S");
            Milestone(s.wall_jumps >= 500, "500 WALL JUMPS");
            Milestone(s.airborne_kills >= 50, "50 AIRBORNE KILLS");
        }

        private void Milestone(bool reached, string name)
        {
            if (!reached || Stats.milestones.Contains(name)) return;
            Stats.milestones.Add(name);
            Notify(NotificationKind.Achievement, "MILESTONE", name);
        }

        // ------------------------------------------------------------------ XP / levels / prestige

        private void AddXp(int amount, List<string> unlocksOut)
        {
            if (amount <= 0) return;
            Profile.total_xp += amount;
            int oldLevel = Profile.level;
            Profile.xp += amount;
            while (Profile.level < _config.maxLevel && Profile.xp >= _config.XpToNext(Profile.level))
            {
                Profile.xp -= _config.XpToNext(Profile.level);
                Profile.level++;
                foreach (var item in CosmeticCatalog.UnlocksAtLevel(Profile.level))
                    if (Grant(item.Id, true)) unlocksOut.Add(item.Id);
            }
            if (Profile.level >= _config.maxLevel) Profile.xp = Mathf.Min(Profile.xp, _config.XpToNext(Profile.level) - 1);

            if (Profile.level != oldLevel)
            {
                if (Profile.title_auto) Profile.title = CosmeticCatalog.TierTitleId(Profile.level);
                Notify(NotificationKind.LevelUp, "LEVEL UP", "LEVEL " + Profile.level + " REACHED");
                if (LeveledUp != null) LeveledUp(oldLevel, Profile.level);
            }
        }

        /// <summary>Optional prestige at max level: back to level 1 with a star and prestige rewards.</summary>
        public bool Prestige(List<string> unlocksOut = null)
        {
            if (!CanPrestige) return false;
            Profile.prestige_level++;
            Profile.level = 1;
            Profile.xp = 0;
            if (Profile.title_auto) Profile.title = CosmeticCatalog.TierTitleId(1);
            foreach (var item in CosmeticCatalog.UnlocksAtPrestige(Profile.prestige_level))
                if (Grant(item.Id, true) && unlocksOut != null) unlocksOut.Add(item.Id);
            Notify(NotificationKind.LevelUp, "PRESTIGE " + CosmeticCatalog.ToRoman(Profile.prestige_level), "WELCOME BACK TO LEVEL 1");
            Save();
            if (ProfileChanged != null) ProfileChanged();
            return true;
        }

        private bool Grant(string id, bool notify)
        {
            if (Profile.unlocked.Contains(id) || CosmeticCatalog.Get(id) == null) return false;
            Profile.unlocked.Add(id);
            if (notify)
            {
                var item = CosmeticCatalog.Get(id);
                Notify(NotificationKind.Unlock, "NEW " + item.Type.ToString().ToUpperInvariant() + " UNLOCKED", item.Name);
                if (ItemUnlocked != null) ItemUnlocked(item);
            }
            return true;
        }

        private static void Notify(NotificationKind kind, string title, string message)
        {
            if (Game.Notifications != null) Game.Notifications.Push(kind, title, message);
        }

        public void RaiseChanged()
        {
            Save();
            if (ProfileChanged != null) ProfileChanged();
        }
    }
}
