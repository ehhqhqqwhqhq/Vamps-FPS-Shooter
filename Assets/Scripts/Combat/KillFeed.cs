using System;
using UnityEngine;

namespace Vamp.Combat
{
    public struct KillFeedEntry
    {
        public string Killer;
        public string Victim;
        public string WeaponId;
        public bool Headshot;
        public bool Suicide;
        public GameObject KillerObject;
        public GameObject VictimObject;
    }

    /// <summary>
    /// Kill events bus. Locally raised by HealthController for the prototype; in multiplayer only the
    /// server raises kills and replicates them, so clients can't fake kill-feed entries.
    /// </summary>
    public static class KillFeed
    {
        public static event Action<KillFeedEntry> EntryAdded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { EntryAdded = null; }

        public static void Report(in DamageInfo killingBlow, GameObject victim)
        {
            var handler = EntryAdded;
            if (handler == null) return;
            bool suicide = killingBlow.Instigator == null || killingBlow.Instigator == victim;
            handler(new KillFeedEntry
            {
                Killer = CombatIdentity.NameOf(killingBlow.Instigator),
                Victim = CombatIdentity.NameOf(victim),
                WeaponId = string.IsNullOrEmpty(killingBlow.WeaponId) ? killingBlow.Type.ToString().ToUpperInvariant() : killingBlow.WeaponId,
                Headshot = killingBlow.IsHeadshot,
                Suicide = suicide,
                KillerObject = killingBlow.Instigator,
                VictimObject = victim
            });
        }
    }
}
