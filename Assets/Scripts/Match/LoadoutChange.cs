using System;
using UnityEngine;
using Vamp.Core;
using Vamp.Player;
using Vamp.Weapons;

namespace Vamp.Match
{
    /// <summary>
    /// Changing your loadout in the middle of a match (ESC ▸ CHANGE LOADOUT). Not in ranked, the 1V1 / 2V2 / 3V3
    /// arenas, gun game (weapons are fixed) or the movement race. The new loadout is given straight away if you
    /// spawned in the last few seconds or are dead; otherwise on your next spawn.
    /// </summary>
    public static class LoadoutChange
    {
        private const float SwapWindow = 8f;

        private static PlayerController _player;
        private static Func<WeaponData[]> _build;
        private static MatchConfig _config;
        private static float _spawnedAt;
        public static bool Pending { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _player = null; _build = null; _config = null; Pending = false; }

        public static bool AllowedIn(MatchConfig cfg)
        {
            if (cfg == null) return false;
            if (cfg.playlist == Playlist.Ranked || cfg.teamSize > 0) return false;
            return cfg.mode != GameMode.GunGame && cfg.mode != GameMode.MovementRace;
        }

        public static bool Allowed { get { return _player != null && _build != null && AllowedIn(_config); } }

        /// <summary>Called by the match (offline) / the local net player (online) once the local player is set up.</summary>
        public static void Setup(PlayerController player, MatchConfig cfg, Func<WeaponData[]> build)
        {
            if (_player != null) _player.Respawned -= OnRespawned;
            _player = player;
            _config = cfg;
            _build = build;
            Pending = false;
            _spawnedAt = Time.time;
            if (_player != null) _player.Respawned += OnRespawned;
        }

        public static void Clear(PlayerController player)
        {
            if (_player != player) return;
            if (_player != null) _player.Respawned -= OnRespawned;
            _player = null;
            _build = null;
            _config = null;
            Pending = false;
        }

        private static void OnRespawned()
        {
            _spawnedAt = Time.time;
            if (!Pending) return;
            Pending = false;
            Give();
        }

        private static void Give()
        {
            if (_player == null || _build == null) return;
            var loadout = _build();
            if (loadout != null && loadout.Length > 0) _player.Weapons.SetLoadout(loadout);
        }

        /// <summary>After the loadout screen closes: returns what happened, for a toast.</summary>
        public static string Commit()
        {
            if (!Allowed) return null;
            if (_player.IsDead) { Pending = true; return "YOUR NEW LOADOUT IS READY FOR YOUR NEXT SPAWN."; }
            if (Time.time - _spawnedAt <= SwapWindow) { Pending = false; Give(); return "LOADOUT SWAPPED."; }
            Pending = true;
            return "YOUR NEW LOADOUT IS READY FOR YOUR NEXT SPAWN.";
        }
    }
}
