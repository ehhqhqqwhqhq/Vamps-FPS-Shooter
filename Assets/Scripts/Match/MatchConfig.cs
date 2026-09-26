using System;
using System.Collections.Generic;

namespace Vamp.Match
{
    public enum GameMode { FreeForAll, TeamDeathmatch, GunGame, MovementRace, Elimination, Training }
    public enum BotDifficulty { Easy, Normal, Hard, Brutal }

    /// <summary>
    /// Everything a match needs, including CUSTOM GAME host options. In multiplayer the server owns this object;
    /// clients only request changes.
    /// </summary>
    [Serializable]
    public sealed class MatchConfig
    {
        public GameMode mode = GameMode.FreeForAll;
        public string mapId = "vertex";
        public int maxPlayers = 8;
        public int bots = 7;
        public BotDifficulty botDifficulty = BotDifficulty.Normal;
        public float timeLimitMinutes = 8f;
        public int scoreLimit = 25;
        public bool respawns = true;
        public float respawnDelay = 2f;
        public bool friendlyFire = false;
        public float gravityMultiplier = 1f;
        public float movementSpeedMultiplier = 1f;
        public bool isPrivate = true;
        public bool isCustom = false;
        public int eliminationRounds = 5;
        /// <summary>Weapon ids NOT allowed (weapon restrictions).</summary>
        public List<string> restrictedWeapons = new List<string>();
        public string lobbyCode = "";
        /// <summary>Run the graphics benchmark instead of a normal match.</summary>
        public bool benchmark = false;

        public static string ModeName(GameMode m)
        {
            switch (m)
            {
                case GameMode.FreeForAll: return "FREE FOR ALL";
                case GameMode.TeamDeathmatch: return "TEAM DEATHMATCH";
                case GameMode.GunGame: return "GUN GAME";
                case GameMode.MovementRace: return "MOVEMENT RACE";
                case GameMode.Elimination: return "ELIMINATION";
                default: return "TRAINING";
            }
        }

        public static string ModeDescription(GameMode m)
        {
            switch (m)
            {
                case GameMode.FreeForAll: return "EVERYONE FOR THEMSELVES. FIRST TO THE SCORE LIMIT WINS.";
                case GameMode.TeamDeathmatch: return "TWO TEAMS. FIRST TEAM TO THE SCORE LIMIT WINS.";
                case GameMode.GunGame: return "EVERY KILL UPGRADES YOUR WEAPON. FIRST THROUGH ALL WEAPONS WINS.";
                case GameMode.MovementRace: return "HIT EVERY CHECKPOINT. FASTEST TIME WINS.";
                case GameMode.Elimination: return "NO RESPAWNS. LAST TEAM STANDING WINS THE ROUND.";
                default: return "FREE PRACTICE. NO TIMER, NO PRESSURE.";
            }
        }

        public static MatchConfig Defaults(GameMode mode)
        {
            var c = new MatchConfig { mode = mode };
            switch (mode)
            {
                case GameMode.TeamDeathmatch: c.scoreLimit = 40; c.maxPlayers = 10; c.bots = 9; break;
                case GameMode.GunGame: c.scoreLimit = 0; c.timeLimitMinutes = 10f; break;
                case GameMode.MovementRace: c.bots = 0; c.maxPlayers = 1; c.scoreLimit = 0; c.timeLimitMinutes = 5f; c.mapId = "movement_lab"; break;
                case GameMode.Elimination: c.respawns = false; c.maxPlayers = 8; c.bots = 7; c.scoreLimit = 3; c.timeLimitMinutes = 2.5f; break;
                case GameMode.Training: c.bots = 0; c.timeLimitMinutes = 0f; c.scoreLimit = 0; c.mapId = "movement_lab"; break;
            }
            return c;
        }

        public MatchConfig Clone()
        {
            var c = (MatchConfig)MemberwiseClone();
            c.restrictedWeapons = new List<string>(restrictedWeapons);
            return c;
        }

        public bool IsTeamMode { get { return mode == GameMode.TeamDeathmatch || mode == GameMode.Elimination; } }
    }

    /// <summary>Hand-off from menus to the match scene.</summary>
    public static class MatchLaunch
    {
        public static MatchConfig Pending;
        public static MatchConfig Last;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Pending = null; Last = null; }
    }
}
