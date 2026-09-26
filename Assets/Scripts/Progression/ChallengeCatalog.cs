using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vamp.Progression
{
    public enum ChallengeStat
    {
        Kills, Headshots, Wins, WallJumps, WallRuns, AirborneKills, Slides, Dashes, RocketJumps, Matches, HighSpeedKills, RaceFinishes
    }

    public sealed class ChallengeDefinition
    {
        public string Id;
        public string Description;
        public ChallengeStat Stat;
        public int Target;
        public int XpReward;
        public string RewardItem;
        public bool Weekly;
    }

    /// <summary>Daily (3) and weekly (3) challenges, picked deterministically from the date so every player gets the same set.</summary>
    public static class ChallengeCatalog
    {
        private static readonly List<ChallengeDefinition> Daily = new List<ChallengeDefinition>
        {
            D("d_kills_25", "GET 25 KILLS", ChallengeStat.Kills, 25, 500),
            D("d_hs_10", "GET 10 HEADSHOTS", ChallengeStat.Headshots, 10, 500),
            D("d_wins_3", "WIN 3 MATCHES", ChallengeStat.Wins, 3, 750),
            D("d_walljumps_25", "PERFORM 25 WALL JUMPS", ChallengeStat.WallJumps, 25, 400),
            D("d_air_5", "GET 5 AIRBORNE KILLS", ChallengeStat.AirborneKills, 5, 500),
            D("d_wallruns_30", "WALL RUN 30 TIMES", ChallengeStat.WallRuns, 30, 400),
            D("d_slides_40", "SLIDE 40 TIMES", ChallengeStat.Slides, 40, 300),
            D("d_dash_40", "DASH 40 TIMES", ChallengeStat.Dashes, 40, 300),
            D("d_rj_10", "ROCKET JUMP 10 TIMES", ChallengeStat.RocketJumps, 10, 400),
            D("d_matches_5", "PLAY 5 MATCHES", ChallengeStat.Matches, 5, 400),
            D("d_speedkills_5", "GET 5 HIGH-SPEED KILLS", ChallengeStat.HighSpeedKills, 5, 600),
        };

        private static readonly List<ChallengeDefinition> Weekly = new List<ChallengeDefinition>
        {
            W("w_kills_200", "GET 200 KILLS", ChallengeStat.Kills, 200, 2500, "icon_ch_closer"),
            W("w_hs_75", "GET 75 HEADSHOTS", ChallengeStat.Headshots, 75, 2500, "icon_ch_headhunter"),
            W("w_wins_15", "WIN 15 MATCHES", ChallengeStat.Wins, 15, 3000, "icon_ch_survivor"),
            W("w_air_40", "GET 40 AIRBORNE KILLS", ChallengeStat.AirborneKills, 40, 3000, "icon_ch_air_ace"),
            W("w_walljumps_250", "PERFORM 250 WALL JUMPS", ChallengeStat.WallJumps, 250, 2500, "icon_ch_wall_walker"),
            W("w_speed_40", "GET 40 HIGH-SPEED KILLS", ChallengeStat.HighSpeedKills, 40, 3000, "icon_ch_speed_demon"),
            W("w_rj_80", "ROCKET JUMP 80 TIMES", ChallengeStat.RocketJumps, 80, 2500, "icon_ch_rocketeer"),
            W("w_race_10", "FINISH 10 MOVEMENT RACES", ChallengeStat.RaceFinishes, 10, 2500, "icon_ch_racer"),
        };

        private static ChallengeDefinition D(string id, string desc, ChallengeStat s, int target, int xp)
        {
            return new ChallengeDefinition { Id = id, Description = desc, Stat = s, Target = target, XpReward = xp };
        }

        private static ChallengeDefinition W(string id, string desc, ChallengeStat s, int target, int xp, string reward)
        {
            return new ChallengeDefinition { Id = id, Description = desc, Stat = s, Target = target, XpReward = xp, RewardItem = reward, Weekly = true };
        }

        public static string DailyPeriod(DateTime now) { return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

        public static string WeeklyPeriod(DateTime now)
        {
            int week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return now.Year + "-W" + week;
        }

        public static List<ChallengeDefinition> ActiveDaily(DateTime now) { return Pick(Daily, DailyPeriod(now), 3); }
        public static List<ChallengeDefinition> ActiveWeekly(DateTime now) { return Pick(Weekly, WeeklyPeriod(now), 3); }

        public static ChallengeDefinition Get(string id)
        {
            foreach (var c in Daily) if (c.Id == id) return c;
            foreach (var c in Weekly) if (c.Id == id) return c;
            return null;
        }

        private static List<ChallengeDefinition> Pick(List<ChallengeDefinition> pool, string seedText, int count)
        {
            int seed = 17;
            foreach (char ch in seedText) seed = seed * 31 + ch;
            var rng = new Random(seed);
            var copy = new List<ChallengeDefinition>(pool);
            var result = new List<ChallengeDefinition>();
            for (int i = 0; i < count && copy.Count > 0; i++)
            {
                int idx = rng.Next(copy.Count);
                result.Add(copy[idx]);
                copy.RemoveAt(idx);
            }
            return result;
        }

        public static int ValueFor(ChallengeStat stat, MatchReport r)
        {
            switch (stat)
            {
                case ChallengeStat.Kills: return r.kills;
                case ChallengeStat.Headshots: return r.headshots;
                case ChallengeStat.Wins: return r.won ? 1 : 0;
                case ChallengeStat.WallJumps: return r.wallJumps;
                case ChallengeStat.WallRuns: return r.wallRuns;
                case ChallengeStat.AirborneKills: return r.airborneKills;
                case ChallengeStat.Slides: return r.slides;
                case ChallengeStat.Dashes: return r.dashes;
                case ChallengeStat.RocketJumps: return r.rocketJumps;
                case ChallengeStat.Matches: return r.completed ? 1 : 0;
                case ChallengeStat.HighSpeedKills: return r.highSpeedKills;
                case ChallengeStat.RaceFinishes: return r.raceTime > 0f ? 1 : 0;
                default: return 0;
            }
        }
    }
}
