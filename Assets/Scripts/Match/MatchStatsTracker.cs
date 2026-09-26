using System.Collections.Generic;
using UnityEngine;
using Vamp.Combat;
using Vamp.Movement;
using Vamp.Progression;

namespace Vamp.Match
{
    /// <summary>
    /// Collects the LOCAL player's match stats: kills per weapon, headshots, airborne / high-speed kills, longest kill,
    /// best speed, distance, and movement actions (wall runs ≥ 0.5 s, wall jumps, slides, dashes, rocket jumps,
    /// movement combos = 3+ different movement actions in one airtime). Produces the MatchReport for XP.
    /// In multiplayer the server tracks these from authoritative events instead.
    /// </summary>
    public sealed class MatchStatsTracker
    {
        private readonly Participant _player;
        private readonly MovementController _motor;
        private readonly float _highSpeed;
        private readonly Dictionary<string, int> _weaponKills = new Dictionary<string, int>();
        private readonly HashSet<MovementEventType> _airChain = new HashSet<MovementEventType>();
        private bool _comboAwarded;
        private Vector3 _lastPos;

        public int WallRuns, WallJumps, Slides, Dashes, RocketJumps, AirborneKills, HighSpeedKills, Combos, Headshots;
        public float LongestKill, BestSpeed, Distance;

        public MatchStatsTracker(Participant player, float highSpeedThreshold)
        {
            _player = player;
            _highSpeed = highSpeedThreshold;
            _motor = player != null ? player.GetComponent<MovementController>() : null;
            if (_motor != null)
            {
                _motor.MovementEventRaised += OnMovement;
                _motor.ResetPeakSpeed();
                _lastPos = _motor.transform.position;
            }
            KillFeed.EntryAdded += OnKill;
        }

        public void Dispose()
        {
            if (_motor != null) _motor.MovementEventRaised -= OnMovement;
            KillFeed.EntryAdded -= OnKill;
        }

        public void Tick()
        {
            if (_motor == null) return;
            Vector3 p = _motor.transform.position;
            float d = Vector3.Distance(p, _lastPos);
            if (d < 20f) Distance += d; // ignore teleports / respawns
            _lastPos = p;
            BestSpeed = Mathf.Max(BestSpeed, _motor.PeakSpeed);
        }

        private void OnMovement(MovementEvent e)
        {
            switch (e.Type)
            {
                case MovementEventType.WallRunEnded:
                    if (e.Magnitude >= 0.5f) { WallRuns++; Chain(e.Type); } // anti-farm: only real runs count
                    break;
                case MovementEventType.WallJumped: WallJumps++; Chain(e.Type); break;
                case MovementEventType.SlideStarted: Slides++; break;
                case MovementEventType.Dashed: Dashes++; Chain(e.Type); break;
                case MovementEventType.ExternalImpulse:
                    if (e.Magnitude > 8f) { RocketJumps++; Chain(e.Type); }
                    break;
                case MovementEventType.Jumped:
                    if (_motor.State == MovementState.Sliding || _motor.IsCrouching) Chain(MovementEventType.SlideStarted);
                    break;
                case MovementEventType.Landed:
                    _airChain.Clear();
                    _comboAwarded = false;
                    break;
            }
        }

        private void Chain(MovementEventType t)
        {
            _airChain.Add(t);
            if (!_comboAwarded && _airChain.Count >= 3)
            {
                Combos++;
                _comboAwarded = true;
                if (Core.Game.Notifications != null) Core.Game.Notifications.Push(Core.NotificationKind.Achievement, "MOVEMENT COMBO", "+" + (Core.Game.Progression != null ? Core.Game.Progression.Config.movementCombo : 100) + " XP");
            }
        }

        private void OnKill(KillFeedEntry e)
        {
            if (_player == null || e.KillerObject == null || e.Suicide) return;
            if (e.KillerObject.GetComponentInParent<Participant>() != _player) return;

            int c;
            _weaponKills.TryGetValue(e.WeaponId ?? "", out c);
            _weaponKills[e.WeaponId ?? ""] = c + 1;
            if (e.Headshot) Headshots++;
            if (_motor != null)
            {
                if (!_motor.IsGrounded) AirborneKills++;
                if (_motor.HorizontalSpeed >= _highSpeed) HighSpeedKills++;
            }
            if (e.VictimObject != null)
                LongestKill = Mathf.Max(LongestKill, Vector3.Distance(_player.transform.position, e.VictimObject.transform.position));
        }

        public MatchReport BuildReport(MatchConfig cfg, float duration, bool won, int placement)
        {
            var r = new MatchReport
            {
                mode = cfg.ModeLabel,
                map = cfg.mapId,
                completed = true,
                won = won,
                placement = placement,
                kills = _player != null ? _player.Kills : 0,
                deaths = _player != null ? _player.Deaths : 0,
                assists = _player != null ? _player.Assists : 0,
                headshots = Headshots,
                objectives = 0,
                score = _player != null ? _player.Score : 0,
                durationSeconds = duration,
                bestSpeed = BestSpeed,
                longestKill = LongestKill,
                wallRuns = WallRuns,
                wallJumps = WallJumps,
                slides = Slides,
                dashes = Dashes,
                rocketJumps = RocketJumps,
                airborneKills = AirborneKills,
                highSpeedKills = HighSpeedKills,
                movementCombos = Combos,
                distance = Distance,
                raceTime = _player != null && _player.Finished ? _player.RaceTime : 0f
            };
            foreach (var kv in _weaponKills) r.weaponKills.Add(new IdCount { id = kv.Key, count = kv.Value });
            return r;
        }
    }
}
