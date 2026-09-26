using System.Collections.Generic;
using UnityEngine;

namespace Vamp.Match
{
    /// <summary>
    /// Server-controlled spawn selection: prefers spawns far from living enemies and out of their line of sight,
    /// avoids recently used spawns (anti spawn-camping / predictability) and respects team sides.
    /// </summary>
    public sealed class SpawnController
    {
        private readonly List<SpawnPoint> _points = new List<SpawnPoint>();
        private readonly LayerMask _losMask;

        public int Count { get { return _points.Count; } }

        public SpawnController(IEnumerable<SpawnPoint> points, LayerMask losMask)
        {
            _points.AddRange(points);
            _losMask = losMask;
        }

        public SpawnPoint Pick(Participant who, IList<Participant> everyone, bool teamSides)
        {
            if (_points.Count == 0) return null;
            SpawnPoint best = null;
            float bestScore = float.MinValue;
            foreach (var p in _points)
            {
                if (p == null) continue;
                Vector3 pos = p.transform.position + Vector3.up * 1.6f;
                float nearest = 200f;
                bool seen = false;
                foreach (var e in everyone)
                {
                    if (e == null || e == who || !e.Alive || (who != null && !who.IsEnemyOf(e))) continue;
                    Vector3 ep = e.transform.position + Vector3.up * 1.5f;
                    float d = Vector3.Distance(pos, ep);
                    if (d < nearest) nearest = d;
                    if (d < 60f && !Physics.Linecast(pos, ep, _losMask, QueryTriggerInteraction.Ignore)) seen = true;
                }

                float score = nearest;
                if (seen) score -= 40f;
                float sinceUsed = Time.time - p.LastUsed;
                if (sinceUsed < 4f) score -= (4f - sinceUsed) * 15f;
                if (teamSides && who != null && who.Team >= 0 && p.Team >= 0) score += p.Team == who.Team ? 25f : -35f;
                score += Random.Range(0f, 8f); // keep it unpredictable

                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            if (best != null) best.LastUsed = Time.time;
            return best;
        }
    }
}
