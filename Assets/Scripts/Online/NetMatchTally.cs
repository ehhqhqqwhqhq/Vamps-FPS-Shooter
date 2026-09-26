using System.Collections.Generic;

namespace Vamp.Online
{
    /// <summary>
    /// This machine's kills on REAL players during the current online match, per weapon (weapon XP → camos).
    /// Kills on bots are never counted. Reset when a match starts.
    /// </summary>
    public static class NetMatchTally
    {
        public static readonly Dictionary<string, int> Kills = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> Headshots = new Dictionary<string, int>();
        public static int TotalKills;
        public static int TotalHeadshots;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Kills.Clear();
            Headshots.Clear();
            TotalKills = 0;
            TotalHeadshots = 0;
        }

        public static void HumanKill(string weaponId, bool headshot)
        {
            if (string.IsNullOrEmpty(weaponId)) weaponId = "unknown";
            int n;
            Kills[weaponId] = (Kills.TryGetValue(weaponId, out n) ? n : 0) + 1;
            TotalKills++;
            if (!headshot) return;
            Headshots[weaponId] = (Headshots.TryGetValue(weaponId, out n) ? n : 0) + 1;
            TotalHeadshots++;
        }
    }
}
