using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Vamp.Audio;
using Vamp.Bots;
using Vamp.Characters;
using Vamp.Combat;
using Vamp.Core;
using Vamp.Match;
using Vamp.VFX;
using Vamp.Weapons;

namespace Vamp.Online
{
    /// <summary>
    /// A bot in an ONLINE match (quick match backfill when not enough real players are searching).
    /// The HOST runs the AI (NavMesh + <see cref="BotController"/>) and its health; everyone else sees a proxy that
    /// animates from the replicated motion and can be shot (hits go to the host like hits on players).
    /// Kills on bots never give XP, weapon XP or ranked coins.
    /// </summary>
    public sealed class NetBot : NetworkBehaviour, ICharacterSource
    {
        public static readonly List<NetBot> All = new List<NetBot>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { All.Clear(); }

        private readonly NetworkVariable<NetMotion> _motion = new NetworkVariable<NetMotion>();
        private readonly NetworkVariable<Vector2> _hp = new NetworkVariable<Vector2>(new Vector2(100f, 0f));
        private readonly NetworkVariable<ulong> _member = new NetworkVariable<ulong>();
        private readonly NetworkVariable<FixedString32Bytes> _name = new NetworkVariable<FixedString32Bytes>();
        private readonly NetworkVariable<int> _team = new NetworkVariable<int>(-1);
        private readonly NetworkVariable<int> _level = new NetworkVariable<int>(1);

        // Host-only setup (written before Spawn, copied into the network variables in OnNetworkSpawn)
        internal ulong SetupMember;
        internal string SetupName = "BOT";
        internal int SetupTeam = -1, SetupLevel = 1;
        internal BotDifficulty SetupDifficulty = BotDifficulty.Normal;
        internal WeaponData SetupWeapon;

        private HealthController _health;
        private Participant _participant;
        private BotController _brain;
        private NavMeshAgent _agent;
        private CharacterPresenter _presenter;
        private float _yaw;
        private bool _snapNext = true;
        private float _respawnAt = -1f;
        private bool _visible = true;

        public ulong MemberId { get { return _member.Value; } }
        public HealthController Health { get { return _health; } }
        public string DisplayName { get { return _name.Value.ToString(); } }

        // ------------------------------------------------------------------ ICharacterSource (proxies)
        public Vector3 Velocity { get { return _motion.Value.Velocity; } }
        public float Yaw { get { return _yaw; } }
        public float Pitch { get { return _motion.Value.Pitch; } }
        public bool Grounded { get { return true; } }
        public bool Sliding { get { return false; } }
        public bool WallRunning { get { return false; } }
        public float WallSide { get { return 1f; } }
        public float Crouch { get { return 0f; } }
        public WeaponData Weapon { get { return NetPlayer.WeaponByIndex(_motion.Value.Weapon); } }
        public string WeaponCamo { get { return null; } }

        // ------------------------------------------------------------------ Spawn

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer)
            {
                _member.Value = SetupMember;
                _name.Value = new FixedString32Bytes(SetupName.Length > 28 ? SetupName.Substring(0, 28) : SetupName);
                _team.Value = SetupTeam;
                _level.Value = SetupLevel;
            }

            var session = NetSession.Instance;
            int localTeam = session != null && NetworkManager != null ? session.TeamOf(NetworkManager.LocalClientId) : -1;
            bool ally = session != null && session.Config.IsTeamMode && _team.Value >= 0 && _team.Value == localTeam;
            _participant = BotFactory.Build(gameObject, _name.Value.ToString(), _level.Value, _team.Value, SetupDifficulty,
                                            IsServer ? SetupWeapon : null, ally, IsServer);
            _health = GetComponent<HealthController>();
            _health.SetRegen(0f, 0f);
            _presenter = GetComponent<CharacterPresenter>();
            gameObject.name = "NetBot " + _name.Value;
            _yaw = transform.eulerAngles.y;

            if (IsServer)
            {
                _brain = GetComponent<BotController>();
                _agent = GetComponent<NavMeshAgent>();
                if (_brain != null) _brain.ShotFired += OnShot;
                _health.Died += OnServerDied;
                _brain.Warp(transform.position, transform.eulerAngles.y);
                _hp.Value = new Vector2(_health.Health, _health.Armor);
            }
            else if (_presenter != null) _presenter.Source = this;
            _hp.OnValueChanged += OnHpChanged;
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            _hp.OnValueChanged -= OnHpChanged;
            if (_brain != null) _brain.ShotFired -= OnShot;
            if (_health != null) _health.Died -= OnServerDied;
        }

        // ------------------------------------------------------------------ Per frame

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) ServerTick();
            else ProxyTick(Time.deltaTime);
            bool alive = _health != null && _health.IsAlive;
            if (alive != _visible) SetVisible(alive);
        }

        private void ServerTick()
        {
            _motion.Value = new NetMotion
            {
                Position = transform.position,
                Velocity = _agent != null ? _agent.velocity : Vector3.zero,
                Yaw = transform.eulerAngles.y,
                Pitch = 0f,
                Flags = NetMotion.Grounded,
                Weapon = (sbyte)NetPlayer.WeaponIndex(_brain != null ? _brain.Weapon : null)
            };

            if (_respawnAt > 0f && Time.time >= _respawnAt)
            {
                _respawnAt = -1f;
                var session = NetSession.Instance;
                if (session == null || session.State != NetState.InMatch) return;
                var spawn = NetSpawns.Choose(session.Config.IsTeamMode ? _team.Value : -1, NetSpawns.EveryoneAlive());
                _health.Revive();
                _participant.ClearDamagers();
                _brain.Warp(spawn.Position, spawn.Yaw);
                _hp.Value = new Vector2(_health.Health, _health.Armor);
            }
        }

        private void ProxyTick(float dt)
        {
            var m = _motion.Value;
            if (m.Position == Vector3.zero && m.Velocity == Vector3.zero) return;
            Vector3 target = m.Position + m.Velocity * 0.05f;
            if (_snapNext || (transform.position - target).sqrMagnitude > 25f)
            {
                transform.position = target;
                _yaw = m.Yaw;
                _snapNext = false;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-18f * dt));
                _yaw = Mathf.LerpAngle(_yaw, m.Yaw, 1f - Mathf.Exp(-20f * dt));
            }
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_health != null && !_health.IsAlive) _snapNext = true;
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = visible;
            if (_presenter != null && _presenter.Rig != null) _presenter.Rig.SetVisible(visible);
        }

        // ------------------------------------------------------------------ Host: death / respawn / shots

        private void OnServerDied(DamageInfo info)
        {
            AudioController.Play(SfxId.Death, transform.position + Vector3.up, 0.8f);
            var session = NetSession.Instance;
            if (session != null && session.Config.respawns) _respawnAt = Time.time + Mathf.Max(1f, session.Config.respawnDelay);
        }

        private void OnShot(Vector3 origin, Vector3 end, WeaponData weapon)
        {
            ShotRpc(origin, end, NetPlayer.WeaponIndex(weapon));
        }

        [Rpc(SendTo.NotServer)]
        private void ShotRpc(Vector3 origin, Vector3 end, int weapon)
        {
            var d = NetPlayer.WeaponByIndex(weapon);
            Vector3 dir = (end - origin).normalized;
            if (d == null || d.showTracers) SimpleVfx.TracerLine(origin + dir * 0.5f, end, d != null ? d.tracerColor : Color.white, 0.02f);
            SimpleVfx.MuzzleFlash(origin + dir * 0.6f, 0.2f);
            if (d != null) AudioController.Play(WeaponController.SoundFor(d), origin, 0.8f, Random.Range(0.95f, 1.05f));
        }

        /// <summary>Host: apply a validated hit to this bot and mirror it to everyone.</summary>
        internal void ServerTakeHit(DamageInfo info, ulong killerMember, ulong instigatorObjectId, int weapon, NetSession session)
        {
            if (_health == null || !_health.IsAlive) return;
            var r = _health.ApplyAuthoritative(info);
            if (r.TotalDamage <= 0f && !r.Killed) return;
            _hp.Value = new Vector2(_health.Health, _health.Armor);
            var local = NetPlayer.LocalPlayer;
            if (local != null && info.Instigator == local.gameObject) local.Weapons.ReportExternal(_health, r, info); // host shot a bot
            DamageRpc(instigatorObjectId, info.Amount, (byte)info.Type, info.IsHeadshot, info.Point, info.Direction, info.Distance, weapon,
                      r.HealthDamage, r.ArmorDamage, r.Killed, _health.Health, _health.Armor);
            if (r.Killed && session != null) session.ServerRecordKill(killerMember, MemberId);
        }

        [Rpc(SendTo.NotServer)]
        private void DamageRpc(ulong instigatorId, float amount, byte type, bool head, Vector3 point, Vector3 dir, float distance, int weapon,
                               float healthDamage, float armorDamage, bool killed, float health, float armor)
        {
            GameObject instigator = null;
            NetworkObject no;
            if (instigatorId != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(instigatorId, out no)) instigator = no.gameObject;
            var d = NetPlayer.WeaponByIndex(weapon);
            var info = new DamageInfo
            {
                Amount = amount, Type = (DamageType)type, Instigator = instigator, WeaponId = d != null ? d.id : "",
                Point = point, Direction = dir, IsHeadshot = head, Distance = distance
            };
            var result = new DamageResult { HealthDamage = healthDamage, ArmorDamage = armorDamage, Killed = killed, IsHeadshot = head };
            _health.ApplyReplicated(info, result, health, armor);
            var local = NetPlayer.LocalPlayer;
            if (local != null && instigator == local.gameObject) local.Weapons.ReportExternal(_health, result, info);
        }

        private void OnHpChanged(Vector2 previous, Vector2 current)
        {
            if (IsServer || _health == null) return;
            if (current.x <= 0f && _health.IsAlive) return; // deaths arrive through DamageRpc
            if (Mathf.Abs(current.x - _health.Health) > 0.01f || Mathf.Abs(current.y - _health.Armor) > 0.01f || (!_health.IsAlive && current.x > 0f))
                _health.SetReplicated(current.x, current.y);
        }
    }
}
