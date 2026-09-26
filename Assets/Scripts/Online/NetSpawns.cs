using System.Collections.Generic;
using UnityEngine;
using Vamp.Match;
using Vamp.Player;

namespace Vamp.Online
{
    /// <summary>Spawn selection for online matches: team spawns first, as far from other players as possible.</summary>
    public static class NetSpawns
    {
        public static SpawnChoice Choose(int team, IList<Vector3> avoid)
        {
            var points = Object.FindObjectsByType<SpawnPoint>();
            SpawnPoint best = null;
            float bestScore = float.MinValue;
            foreach (var p in points)
            {
                if (p == null) continue;
                float score = Random.value * 2f; // small shuffle so ties don't always pick the same pad
                if (team >= 0 && p.Team >= 0 && p.Team != team) score -= 1000f;
                float nearest = 999f;
                if (avoid != null) foreach (var a in avoid) nearest = Mathf.Min(nearest, Vector3.Distance(a, p.transform.position));
                score += Mathf.Min(nearest, 60f);
                if (score > bestScore) { bestScore = score; best = p; }
            }
            if (best == null) return new SpawnChoice { Valid = true, Position = new Vector3(0f, 1f, 0f), Yaw = 0f };
            return new SpawnChoice { Valid = true, Position = best.transform.position, Yaw = best.transform.eulerAngles.y };
        }

        /// <summary>Positions of every living player and bot.</summary>
        public static List<Vector3> EveryoneAlive()
        {
            var l = new List<Vector3>();
            foreach (var p in NetPlayer.All) if (p != null && p.Health != null && p.Health.IsAlive) l.Add(p.transform.position);
            foreach (var b in NetBot.All) if (b != null && b.Health != null && b.Health.IsAlive) l.Add(b.transform.position);
            return l;
        }

        /// <summary>Positions of every other living player (for the owner's own respawn choice).</summary>
        public static List<Vector3> OthersAlive(NetPlayer self)
        {
            var l = new List<Vector3>();
            foreach (var p in NetPlayer.All) if (p != null && p != self && p.Health != null && p.Health.IsAlive) l.Add(p.transform.position);
            foreach (var b in NetBot.All) if (b != null && b.Health != null && b.Health.IsAlive) l.Add(b.transform.position);
            return l;
        }
    }
}
