using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamp.Characters;
using Vamp.Combat;
using Vamp.Core;
using Vamp.Match;
using Vamp.Movement;
using Vamp.Player;
using Vamp.Progression;
using Vamp.UI;
using Vamp.VFX;
using Vamp.Weapons;

namespace Vamp.Online
{
    /// <summary>
    /// Networked player (added to the Player prefab variant Resources/VampNetPlayer).
    ///
    /// Authority model for player-hosted custom games:
    ///  • MOVEMENT is owner-authoritative: the owner runs the normal VAMP movement and replicates position / velocity /
    ///    aim / body state (NetMotion). Everyone else renders a smoothed proxy with the stick-man animation.
    ///  • HITS are detected on the shooter's machine (what you see is what you hit) and sent to the HOST, which validates
    ///    them (alive, weapon damage cap, range, team rules) and applies damage with the real HealthController. The
    ///    result (health, armor, kill) is replicated to everyone, so HUD / kill feed / death screen behave as offline.
    ///  • The host keeps score, timer and win conditions (NetSession).
    /// Other players' shots are replayed locally for tracers, sound and rocket knockback.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class NetPlayer : NetworkBehaviour, ICharacterSource
    {
        public static readonly List<NetPlayer> All = new List<NetPlayer>();
        public static NetPlayer LocalPlayer { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { All.Clear(); LocalPlayer = null; }

        private readonly NetworkVariable<NetMotion> _motion = new NetworkVariable<NetMotion>(default(NetMotion),
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector2> _hp = new NetworkVariable<Vector2>(new Vector2(100f, 0f),
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private PlayerController _pc;
        private CharacterPresenter _presenter;
        private CharacterController _cc;
        private GameObject _head;
        private float _yaw;
        private bool _snapNext = true;

        public HealthController Health { get; private set; }
        public WeaponController Weapons { get; private set; }
        public PlayerController Controller { get { return _pc; } }

        // ------------------------------------------------------------------ ICharacterSource (proxy animation)
        public Vector3 Velocity { get { return _motion.Value.Velocity; } }
        public float Yaw { get { return _yaw; } }
        public float Pitch { get { return _motion.Value.Pitch; } }
        public bool Grounded { get { return (_motion.Value.Flags & NetMotion.Grounded) != 0; } }
        public bool Sliding { get { return (_motion.Value.Flags & NetMotion.Sliding) != 0; } }
        public bool WallRunning { get { return (_motion.Value.Flags & NetMotion.WallRunning) != 0; } }
        public float WallSide { get { return (_motion.Value.Flags & NetMotion.WallRight) != 0 ? 1f : -1f; } }
        public float Crouch { get { return (_motion.Value.Flags & NetMotion.Crouched) != 0 ? 1f : 0f; } }
        public WeaponData Weapon { get { return WeaponByIndex(_motion.Value.Weapon); } }
        public string WeaponCamo { get { return Vamp.Weapons.WeaponCamo.FromIndex(_motion.Value.Camo); } }

        // ------------------------------------------------------------------ Spawn

        private void Awake()
        {
            _pc = GetComponent<PlayerController>();
            Health = GetComponent<HealthController>();
            Weapons = GetComponent<WeaponController>();
            _presenter = GetComponent<CharacterPresenter>();
            _cc = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Health.SetRegen(0f, 0f);
            var session = NetSession.Instance;
            string name = session != null ? session.NameOf(OwnerClientId) : "PLAYER";
            var identity = GetComponent<CombatIdentity>();
            if (identity != null) identity.DisplayName = name;
            gameObject.name = (IsOwner ? "NetPlayer (You) " : "NetPlayer ") + name;
            _yaw = transform.eulerAngles.y;

            // Participant: lets the host's bots find this player as a target.
            var part = GetComponent<Participant>();
            if (part == null) part = gameObject.AddComponent<Participant>();
            part.IsBot = false;
            part.IsLocalPlayer = IsOwner;
            part.DisplayName = name;
            part.Team = session != null && session.Config.IsTeamMode ? session.TeamOf(OwnerClientId) : -1;
            if (identity != null) identity.DisplayName = name;

            if (IsOwner) SetupOwner(session);
            else SetupProxy(session);

            if (IsServer) _hp.Value = new Vector2(Health.Health, Health.Armor);
            _hp.OnValueChanged += OnHpChanged;
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            _hp.OnValueChanged -= OnHpChanged;
            if (IsOwner)
            {
                _pc.Respawned -= OnRespawned;
                Weapons.Fired -= OnFired;
                if (LocalPlayer == this) LocalPlayer = null;
                if (PlayerController.Local == _pc) PlayerController.Local = null;
                LoadoutChange.Clear(_pc);
            }
        }

        private void SetupOwner(NetSession session)
        {
            LocalPlayer = this;
            PlayerController.Local = _pc;
            if (_presenter != null) _presenter.SetLocalView(true);
            var cfg = session != null ? session.Config : MatchConfig.Defaults(GameMode.FreeForAll);

            if (!Mathf.Approximately(cfg.gravityMultiplier, 1f) || !Mathf.Approximately(cfg.movementSpeedMultiplier, 1f))
                _pc.Movement.SetSettings(_pc.Movement.CreateScaledCopy(cfg.gravityMultiplier, cfg.movementSpeedMultiplier));
            Weapons.SetLoadout(Attachments.ApplyLocal(BuildLoadout(cfg)));
            LoadoutChange.Setup(_pc, cfg, () => Attachments.ApplyLocal(BuildLoadout(cfg)));
            _pc.RespawnDelay = cfg.respawnDelay;
            _pc.RespawnEnabled = cfg.respawns;
            int team = session != null && cfg.IsTeamMode ? session.TeamOf(OwnerClientId) : -1;
            _pc.SpawnSelector = p => NetSpawns.Choose(team, NetSpawns.OthersAlive(this));
            _pc.Respawned += OnRespawned;
            Weapons.Fired += OnFired;

            _pc.Movement.Teleport(transform.position, transform.eulerAngles.y);
            _pc.View.SetView(transform.eulerAngles.y, 0f);
            _pc.Input.GameplayInputEnabled = true;
            InputController.SetCursorLocked(true);
        }

        private void SetupProxy(NetSession session)
        {
            Weapons.IsProxy = true;
            _pc.enabled = false;
            _pc.Input.enabled = false;
            _pc.View.enabled = false;
            var feedback = GetComponent<PlayerFeedback>();
            if (feedback != null) feedback.enabled = false;
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.gameObject.SetActive(false);

            if (_presenter != null)
            {
                _presenter.Source = this;
                _presenter.SetLocalView(false);
            }
            ApplyTint(session);

            // Head hitbox so headshots work on other players (hit detection runs on the shooter's machine).
            _head = new GameObject("HeadHitbox");
            _head.transform.SetParent(transform, false);
            _head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            var sc = _head.AddComponent<SphereCollider>();
            sc.radius = 0.2f;
            _head.AddComponent<Hitbox>().Configure(true, 1f);
        }

        public void ApplyTint(NetSession session)
        {
            if (_presenter == null || IsOwner) return;
            var cfg = session != null ? session.Config : null;
            bool ally = cfg != null && cfg.IsTeamMode && session.TeamOf(OwnerClientId) == session.TeamOf(NetworkManager.LocalClientId);
            _presenter.SetTint(ally ? new Color(0.35f, 0.55f, 0.95f) : new Color(0.9f, 0.18f, 0.2f), 0.15f);
        }

        private WeaponData[] BuildLoadout(MatchConfig cfg)
        {
            var cat = Game.Weapons;
            if (cat == null) return new WeaponData[0];
            var lo = Game.Progression != null && Game.Progression.Profile != null ? Game.Progression.Profile.loadout : new LoadoutData();
            var list = new List<WeaponData>();
            AddAllowed(list, cat, lo.primary, WeaponSlot.Primary, cfg);
            AddAllowed(list, cat, lo.secondary, WeaponSlot.Secondary, cfg);
            AddAllowed(list, cat, lo.melee, WeaponSlot.Melee, cfg);
            return list.ToArray();
        }

        private static void AddAllowed(List<WeaponData> list, WeaponCatalog cat, string wanted, WeaponSlot slot, MatchConfig cfg)
        {
            var w = cat.Get(wanted);
            if (w != null && WeaponCatalog.FitsLoadoutSlot(w, slot) && !cfg.restrictedWeapons.Contains(w.id) && !list.Contains(w)) { list.Add(w); return; }
            foreach (var x in cat.weapons)
                if (x != null && x.slot == slot && !cfg.restrictedWeapons.Contains(x.id)) { list.Add(x); return; }
        }

        // ------------------------------------------------------------------ Per frame

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner) WriteMotion();
            else UpdateProxy(Time.deltaTime);
        }

        private void WriteMotion()
        {
            var mv = _pc.Movement;
            byte flags = 0;
            if (mv.IsGrounded) flags |= NetMotion.Grounded;
            var slide = GetComponent<SlideAbility>();
            var wall = GetComponent<WallRunAbility>();
            if (slide != null && slide.IsActive) flags |= NetMotion.Sliding;
            if (wall != null && wall.IsActive)
            {
                flags |= NetMotion.WallRunning;
                if (Vector3.Dot(wall.WallNormal, transform.right) > 0f) flags |= NetMotion.WallRight;
            }
            if (mv.IsCrouching) flags |= NetMotion.Crouched;
            _motion.Value = new NetMotion
            {
                Position = transform.position,
                Velocity = mv.Velocity,
                Yaw = _pc.View.Yaw,
                Pitch = _pc.View.Pitch,
                Flags = flags,
                Weapon = (sbyte)WeaponIndex(Weapons.Current),
                Camo = Vamp.Weapons.WeaponCamo.ToIndex(Vamp.Weapons.WeaponCamo.LocalFor(Weapons.Current)),
                Fx = CosmeticFx.ToIndex(CosmeticType.KillEffect, CosmeticFx.LocalKillFx),
                Trail = CosmeticFx.ToIndex(CosmeticType.WeaponTrail, CosmeticFx.LocalTrail),
                Skin = CosmeticFx.ToIndex(CosmeticType.CharacterSkin, Characters.CharacterSkins.Local)
            };
        }

        /// <summary>This player's equipped kill effect (replicated).</summary>
        public string KillFx { get { return IsOwner ? CosmeticFx.LocalKillFx : CosmeticFx.FromIndex(CosmeticType.KillEffect, _motion.Value.Fx); } }

        private void UpdateProxy(float dt)
        {
            var m = _motion.Value;
            Weapons.ProxyTrail = CosmeticFx.FromIndex(CosmeticType.WeaponTrail, m.Trail);
            if (_presenter != null) _presenter.SetSkin(CosmeticFx.FromIndex(CosmeticType.CharacterSkin, m.Skin));
            if (m.Position == Vector3.zero && m.Velocity == Vector3.zero) return; // nothing received yet
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
            if (_head != null) _head.transform.localPosition = new Vector3(0f, Crouch > 0.5f || Sliding ? 1.0f : 1.62f, 0f);

            bool alive = Health.IsAlive;
            if (_cc != null && _cc.enabled != alive) _cc.enabled = alive;
            if (_head != null && _head.activeSelf != alive) _head.SetActive(alive);
            if (_presenter != null && _presenter.Rig != null) _presenter.Rig.SetVisible(alive);
            if (!alive) _snapNext = true;
        }

        // ------------------------------------------------------------------ Weapons

        public static int WeaponIndex(WeaponData d)
        {
            if (d == null || Game.Weapons == null) return -1;
            int i = Game.Weapons.weapons.IndexOf(d);
            if (i < 0) i = Game.Weapons.weapons.FindIndex(w => w != null && w.id == d.id); // runtime copy (attachments)
            return i;
        }

        public static WeaponData WeaponByIndex(int i)
        {
            if (Game.Weapons == null || i < 0 || i >= Game.Weapons.weapons.Count) return null;
            return Game.Weapons.weapons[i];
        }

        private void OnFired(WeaponData d)
        {
            if (d == null || d.delivery == DeliveryType.Melee) return;
            var aim = Weapons.AimOrigin != null ? Weapons.AimOrigin : transform;
            FireToHostRpc(aim.position, aim.forward, WeaponIndex(d));
        }

        [Rpc(SendTo.Server)]
        private void FireToHostRpc(Vector3 origin, Vector3 forward, int weapon)
        {
            FireFxRpc(origin, forward, weapon);
        }

        [Rpc(SendTo.NotOwner)]
        private void FireFxRpc(Vector3 origin, Vector3 forward, int weapon)
        {
            var d = WeaponByIndex(weapon);
            if (d == null) return;
            Vector3 muzzle = origin + forward * 0.6f;
            if (_presenter != null && _presenter.Rig != null)
            {
                var m = _presenter.Rig.transform.Find("HeldWeapon/Muzzle");
                if (m != null) muzzle = m.position;
            }
            Weapons.SimulateRemoteShot(d, origin, forward, muzzle);
        }

        // ------------------------------------------------------------------ Damage routing

        /// <summary>Installed as HealthController.Intercept while online.</summary>
        public static bool InterceptDamage(HealthController target, DamageInfo info)
        {
            var victimPlayer = target.GetComponent<NetPlayer>();
            var victimBot = victimPlayer == null ? target.GetComponent<NetBot>() : null;
            if ((victimPlayer == null || !victimPlayer.IsSpawned) && (victimBot == null || !victimBot.IsSpawned)) return false; // not networked

            // Bots only run on the host: their shots are applied there directly.
            var botShooter = info.Instigator != null ? info.Instigator.GetComponent<NetBot>() : null;
            if (botShooter != null)
            {
                if (botShooter.IsServer && NetSession.Instance != null && NetSession.Instance.State == NetState.InMatch)
                {
                    var session = NetSession.Instance;
                    if (session.Config.IsTeamMode && !session.Config.friendlyFire &&
                        session.TeamOf(botShooter.MemberId) == session.TeamOf(victimPlayer != null ? victimPlayer.OwnerClientId : victimBot.MemberId)) return true;
                    int w = WeaponIndex(Game.Weapons != null ? Game.Weapons.Get(info.WeaponId) : null);
                    if (victimPlayer != null) victimPlayer.ServerTakeHit(info, botShooter.MemberId, botShooter.NetworkObjectId, w, false, session);
                    else victimBot.ServerTakeHit(info, botShooter.MemberId, botShooter.NetworkObjectId, w, session);
                }
                return true;
            }

            var me = LocalPlayer;
            if (me == null) return true;
            bool mine = info.Instigator != null && info.Instigator == me.gameObject;
            bool world = info.Instigator == null && victimPlayer == me;      // kill plane etc. on my own player
            if (mine || world)
            {
                var d = Game.Weapons != null ? Game.Weapons.Get(info.WeaponId) : null;
                ulong victimId = victimPlayer != null ? victimPlayer.NetworkObjectId : victimBot.NetworkObjectId;
                me.HitToHostRpc(victimId, info.Amount, (byte)info.Type, info.IsHeadshot, info.Point, info.Direction,
                                info.Distance, WeaponIndex(d), world);
            }
            return true;                                                   // host applies + replicates
        }

        [Rpc(SendTo.Server)]
        private void HitToHostRpc(ulong victimId, float amount, byte type, bool head, Vector3 point, Vector3 dir, float distance, int weapon, bool world)
        {
            var session = NetSession.Instance;
            if (session == null || session.State != NetState.InMatch) return;
            NetworkObject no;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(victimId, out no)) return;
            var victim = no.GetComponent<NetPlayer>();
            var bot = victim == null ? no.GetComponent<NetBot>() : null;
            if (victim == null && bot == null) return;
            var vHealth = victim != null ? victim.Health : bot.Health;
            if (vHealth == null || !vHealth.IsAlive) return;
            bool self = victim == this;
            if (world && !self) return;
            ulong victimMember = victim != null ? victim.OwnerClientId : bot.MemberId;

            // ---- Validation (light anti-cheat)
            var d = WeaponByIndex(weapon);
            if (!world)
            {
                if (d == null) return;
                float cap = Mathf.Max(d.damage * Mathf.Max(1, d.pelletsPerShot) * Mathf.Max(1f, d.headshotMultiplier),
                                      d.explosionDamage, d.damage * Mathf.Max(1f, d.backstabMultiplier)) * 1.25f + 1f;
                if (d.delivery == DeliveryType.Melee) cap = Mathf.Max(cap, Vamp.Weapons.WeaponController.BackstabDamage); // backstab = one-hit kill
                amount = Mathf.Clamp(amount, 0f, cap);
                float reach = d.delivery == DeliveryType.Projectile ? 400f : d.delivery == DeliveryType.Melee ? d.meleeRange + 4f : d.range + 8f;
                if (!self && Vector3.Distance(transform.position, no.transform.position) > reach + 10f) return;
                var cfg = session.Config;
                if (!self && cfg.IsTeamMode && !cfg.friendlyFire && session.TeamOf(OwnerClientId) == session.TeamOf(victimMember)) return;
            }
            if (amount <= 0f) return;

            var info = new DamageInfo
            {
                Amount = amount,
                Type = (DamageType)type,
                Instigator = world ? null : gameObject,
                WeaponId = d != null ? d.id : "",
                Point = point,
                Direction = dir,
                IsHeadshot = head,
                Distance = distance
            };
            ulong instigatorObj = world ? ulong.MaxValue : NetworkObjectId;
            ulong killer = world ? victimMember : OwnerClientId;
            if (victim != null) victim.ServerTakeHit(info, killer, instigatorObj, weapon, world, session);
            else bot.ServerTakeHit(info, killer, instigatorObj, weapon, session);
        }

        /// <summary>Host: apply a validated hit to this player and mirror it to everyone.</summary>
        internal void ServerTakeHit(DamageInfo info, ulong killerMember, ulong instigatorObjectId, int weapon, bool world, NetSession session)
        {
            if (!Health.IsAlive) return;
            var r = Health.ApplyAuthoritative(info);   // raises events on the host's copy
            if (r.TotalDamage <= 0f && !r.Killed) return;
            _hp.Value = new Vector2(Health.Health, Health.Armor);
            var local = LocalPlayer;
            if (local != null && !world && info.Instigator == local.gameObject && local != this)
            {
                local.Weapons.ReportExternal(Health, r, info); // the host is the shooter
                if (r.Killed) NetMatchTally.HumanKill(info.WeaponId, info.IsHeadshot);
            }
            DamageRpc(instigatorObjectId, info.Amount, (byte)info.Type, info.IsHeadshot, info.Point, info.Direction, info.Distance, weapon,
                      r.HealthDamage, r.ArmorDamage, r.Killed, Health.Health, Health.Armor);
            if (r.Killed && session != null) session.ServerRecordKill(killerMember, OwnerClientId);
        }

        /// <summary>Host → clients: mirror a hit on this (victim) player.</summary>
        [Rpc(SendTo.NotServer)]
        private void DamageRpc(ulong instigatorId, float amount, byte type, bool head, Vector3 point, Vector3 dir, float distance, int weapon,
                               float healthDamage, float armorDamage, bool killed, float health, float armor)
        {
            GameObject instigator = null;
            NetworkObject no;
            if (instigatorId != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(instigatorId, out no)) instigator = no.gameObject;
            var d = WeaponByIndex(weapon);
            var info = new DamageInfo
            {
                Amount = amount, Type = (DamageType)type, Instigator = instigator, WeaponId = d != null ? d.id : "",
                Point = point, Direction = dir, IsHeadshot = head, Distance = distance
            };
            var result = new DamageResult { HealthDamage = healthDamage, ArmorDamage = armorDamage, Killed = killed, IsHeadshot = head };
            Health.ApplyReplicated(info, result, health, armor);
            if (LocalPlayer != null && instigator == LocalPlayer.gameObject && LocalPlayer != this)
            {
                LocalPlayer.Weapons.ReportExternal(Health, result, info);
                if (killed) NetMatchTally.HumanKill(info.WeaponId, head); // a kill on a REAL player (weapon XP)
            }
        }

        private void OnHpChanged(Vector2 previous, Vector2 current)
        {
            if (IsServer) return;
            // Deaths arrive through DamageRpc (with killer info); this only restores health on respawn / keeps values in sync.
            if (current.x <= 0f && Health.IsAlive) return;
            if (Mathf.Abs(current.x - Health.Health) > 0.01f || Mathf.Abs(current.y - Health.Armor) > 0.01f || (!Health.IsAlive && current.x > 0f))
                Health.SetReplicated(current.x, current.y);
        }

        // ------------------------------------------------------------------ Respawn

        private void OnRespawned()
        {
            _snapNext = true;
            RespawnToHostRpc();
        }

        [Rpc(SendTo.Server)]
        private void RespawnToHostRpc()
        {
            if (!Health.IsAlive) Health.Revive();
            _hp.Value = new Vector2(Health.Health, Health.Armor);
        }
    }
}
