using System.Collections.Generic;
using UnityEngine;
using Vamp.Combat;

namespace Vamp.Match
{
    /// <summary>
    /// A player or bot taking part in a match: identity (username, level, icon, title), team and scoreboard stats.
    /// SERVER-OWNED in multiplayer; offline the MatchController updates it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Participant : MonoBehaviour
    {
        public string Id;
        public string DisplayName = "PLAYER";
        public int Team = -1;
        public bool IsBot;
        public bool IsLocalPlayer;
        public int Level = 1;
        public int Prestige;
        public string Icon = "icon_vamp_symbol";
        public string Frame = "frame_basic";
        public string Title = "";
        public int Ping;

        public int Kills;
        public int Deaths;
        public int Assists;
        public int Score;
        public int Streak;
        public int BestStreak;
        public int Headshots;
        public int GunGameLevel;
        public int NextCheckpoint;
        public float RaceTime;
        public bool Finished;

        public HealthController Health { get; private set; }
        public bool Alive { get { return Health != null && Health.IsAlive; } }
        public float LastSpawnTime { get; set; }

        private readonly Dictionary<Participant, float> _damagers = new Dictionary<Participant, float>();

        /// <summary>Every active participant (players AND bots) - what bots pick their targets from.</summary>
        public static readonly List<Participant> Active = new List<Participant>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active.Clear(); }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() { Active.Remove(this); }

        private void Awake()
        {
            Health = GetComponent<HealthController>();
            if (string.IsNullOrEmpty(Id)) Id = System.Guid.NewGuid().ToString("N");
            if (Health != null) Health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (Health != null) Health.Damaged -= OnDamaged;
        }

        public void ApplyIdentity()
        {
            var id = GetComponent<CombatIdentity>();
            if (id == null) id = gameObject.AddComponent<CombatIdentity>();
            id.DisplayName = DisplayName;
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (info.Instigator == null) return;
            var from = info.Instigator.GetComponentInParent<Participant>();
            if (from != null && from != this) _damagers[from] = Time.time;
        }

        /// <summary>Everyone (except the killer) who damaged this participant in the last <paramref name="window"/> seconds.</summary>
        public List<Participant> RecentDamagers(Participant killer, float window)
        {
            var list = new List<Participant>();
            foreach (var kv in _damagers)
                if (kv.Key != null && kv.Key != killer && Time.time - kv.Value <= window) list.Add(kv.Key);
            return list;
        }

        public void ClearDamagers() { _damagers.Clear(); }

        public float KD { get { return Deaths == 0 ? Kills : (float)Kills / Deaths; } }

        public bool IsEnemyOf(Participant other)
        {
            if (other == null || other == this) return false;
            if (Team < 0 || other.Team < 0) return true;
            return Team != other.Team;
        }
    }
}
