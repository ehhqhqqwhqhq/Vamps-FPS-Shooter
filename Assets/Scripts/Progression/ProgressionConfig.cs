using UnityEngine;

namespace Vamp.Progression
{
    /// <summary>
    /// All XP / level numbers. Put an asset named "VampProgressionConfig" in a Resources folder to tune;
    /// otherwise these defaults are used. Server uses the same config when progression goes online.
    /// </summary>
    [CreateAssetMenu(menuName = "VAMP/Progression Config", fileName = "VampProgressionConfig")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        [Header("Levels")]
        public int xpForLevel2 = 1000;
        public int xpIncreasePerLevel = 200;
        public int maxLevel = 100;
        public int maxPrestige = 10;

        [Header("Match XP")]
        public int matchCompletion = 250;
        public int matchVictory = 500;
        public int kill = 50;
        public int headshot = 25;
        public int assist = 20;
        public int objective = 100;

        [Header("Movement XP")]
        public int wallRun = 5;
        public int wallJump = 5;
        public int airborneKill = 50;
        public int rocketJump = 10;
        public int highSpeedKill = 75;
        public int movementCombo = 100;
        public float highSpeedKillThreshold = 18f;
        public int movementRaceFinish = 300;

        [Header("Anti-farm")]
        [Tooltip("Max movement XP per match.")]
        public int movementXpCap = 600;
        public int maxWallRunEvents = 40;
        public int maxWallJumpEvents = 40;
        public int maxRocketJumpEvents = 20;
        [Tooltip("Matches shorter than this give proportionally less movement XP.")]
        public float fullMovementXpAfterSeconds = 120f;
        [Tooltip("Sanity cap: kills per minute beyond this are treated as invalid.")]
        public float maxKillsPerMinute = 30f;

        private static ProgressionConfig _default;

        public static ProgressionConfig Load()
        {
            var c = Resources.Load<ProgressionConfig>("VampProgressionConfig");
            if (c != null) return c;
            if (_default == null) _default = CreateInstance<ProgressionConfig>();
            return _default;
        }

        /// <summary>XP needed to go from <paramref name="level"/> to level+1 (1→2 = 1,000, 2→3 = 1,200 ...).</summary>
        public int XpToNext(int level)
        {
            return xpForLevel2 + xpIncreasePerLevel * Mathf.Max(0, level - 1);
        }
    }
}
