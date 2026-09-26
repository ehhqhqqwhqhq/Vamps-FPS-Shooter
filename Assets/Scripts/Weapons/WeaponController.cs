using System;
using System.Collections.Generic;
using UnityEngine;
using Vamp.Audio;
using Vamp.Combat;
using Vamp.Core;
using Vamp.Movement;
using Vamp.VFX;

namespace Vamp.Weapons
{
    /// <summary>What the shooter learns about a shot (drives hit markers, sounds, kill feed).</summary>
    public struct HitReport
    {
        public IDamageable Target;
        public float Damage;
        public bool Headshot;
        public bool Killed;
        public Vector3 Point;
        public string WeaponId;
    }

    public enum AimSensitivityMode { Hip, Ads, Sniper }

    /// <summary>
    /// Loadout, switching, fire timing, ammo, reload, ADS, spread, hitscan/projectile delivery.
    /// Ticked explicitly by PlayerController after movement.
    ///
    /// MULTIPLAYER NOTE: in Phase 6 the client keeps doing all of this for instant feedback (prediction),
    /// but sends only (tick, aim origin, aim direction, weapon id, shot seed) to the server; the server
    /// re-runs the same fire logic against lag-compensated hitboxes and is the only one that applies damage.
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private WeaponData[] loadout = new WeaponData[0];
        [SerializeField] private Transform aimOrigin;
        [Tooltip("Optional muzzle point (from the view model) for tracers/flash.")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [Tooltip("Training: reserve ammo never runs out.")]
        [SerializeField] private bool infiniteReserve = true;

        private sealed class WeaponState
        {
            public WeaponData Data;
            public int Mag;
            public int Reserve;
            public float Heat;
            public float OverheatTimer;
            public float LastHeatShot = -99f;
        }

        private sealed class HitAccum
        {
            public float Damage;
            public bool Headshot;
            public Vector3 Point;
            public float Distance;
        }

        private static readonly RaycastHit[] RayHits = new RaycastHit[16];

        private WeaponState[] _states = new WeaponState[0];
        private int _current = -1;
        private float _time;
        private float _nextFireTime;
        private float _equipTimer;
        private bool _reloading;
        private float _reloadTimer;
        private float _aimBlend;
        private int _shotCounter;
        private MovementController _movement;
        private readonly Dictionary<IDamageable, HitAccum> _accum = new Dictionary<IDamageable, HitAccum>();
        private readonly List<IDamageable> _accumKeys = new List<IDamageable>();

        public event Action<WeaponData> Fired;
        public event Action<HitReport> HitConfirmed;
        public event Action<WeaponData> WeaponChanged;
        public event Action AmmoChanged;
        public event Action<WeaponData> ReloadStarted;

        private bool HasCurrent { get { return _current >= 0 && _current < _states.Length; } }
        public WeaponData Current { get { return HasCurrent ? _states[_current].Data : null; } }
        public int CurrentIndex { get { return _current; } }
        public int Magazine { get { return HasCurrent ? _states[_current].Mag : 0; } }
        public int Reserve { get { return HasCurrent ? _states[_current].Reserve : 0; } }
        public bool InfiniteReserve { get { return infiniteReserve; } }

        /// <summary>Online proxies of other players: their equipped bullet trail (set by NetPlayer).</summary>
        public bool IsProxy { get; set; }
        public string ProxyTrail { get; set; }
        private string TrailId { get { return IsProxy ? ProxyTrail : CosmeticFx.LocalTrail; } }
        public bool IsReloading { get { return _reloading; } }
        public float ReloadProgress
        {
            get
            {
                if (!_reloading || Current == null) return 0f;
                return 1f - Mathf.Clamp01(_reloadTimer / Current.reloadTime);
            }
        }
        public float AimBlend { get { return _aimBlend; } }
        public bool IsAiming { get { return _aimBlend > 0.5f; } }
        public float ZoomMultiplier { get { return Current == null ? 1f : Mathf.Lerp(1f, Current.adsFovMultiplier, _aimBlend); } }
        public AimSensitivityMode SensitivityMode
        {
            get
            {
                if (!IsAiming || Current == null) return AimSensitivityMode.Hip;
                return Current.isSniper ? AimSensitivityMode.Sniper : AimSensitivityMode.Ads;
            }
        }
        /// <summary>Current spread cone (degrees) - used by a dynamic crosshair.</summary>
        public float CurrentSpread { get; private set; }
        /// <summary>0..1 heat for energy weapons.</summary>
        public float Heat { get { return HasCurrent ? _states[_current].Heat : 0f; } }
        public bool Overheated { get { return HasCurrent && _states[_current].OverheatTimer > 0f; } }
        public bool ShowScope { get { return Current != null && Current.scopeOverlay && _aimBlend > 0.95f; } }
        public int LoadoutCount { get { return _states.Length; } }
        public WeaponData WeaponAt(int i) { return i >= 0 && i < _states.Length ? _states[i].Data : null; }

        /// <summary>Owner used for kill credit etc. (Participant or player object).</summary>
        public GameObject Owner { get { return gameObject; } }

        private void Awake()
        {
            _movement = GetComponent<MovementController>();
            if (aimOrigin == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) aimOrigin = cam.transform;
            }

            BuildStates(loadout);
        }

        private void BuildStates(WeaponData[] weapons)
        {
            var states = new List<WeaponState>();
            if (weapons != null)
                foreach (var d in weapons)
                {
                    if (d == null) continue;
                    states.Add(new WeaponState { Data = d, Mag = d.magazineSize, Reserve = d.reserveAmmo });
                }
            _states = states.ToArray();
        }

        /// <summary>
        /// Replace the loadout at runtime (player loadout from profile, Gun Game progression, weapon restrictions).
        /// In multiplayer the SERVER decides the loadout; the client mirrors it.
        /// </summary>
        public void SetLoadout(WeaponData[] weapons, int equipIndex = 0)
        {
            loadout = weapons;
            BuildStates(weapons);
            _current = -1;
            _reloading = false;
            if (_states.Length > 0) Equip(Mathf.Clamp(equipIndex, 0, _states.Length - 1));
            else if (WeaponChanged != null) WeaponChanged(null);
        }

        private void Start()
        {
            if (_current < 0 && _states.Length > 0) Equip(0);
            else if (_states.Length == 0) Debug.LogWarning("[VAMP] WeaponController has an empty loadout.", this);
        }

        public void SetMuzzle(Transform t) { muzzle = t; }
        public Transform Muzzle { get { return muzzle; } }
        public Transform AimOrigin { get { return aimOrigin; } }

        // ------------------------------------------------------------------ Tick

        public void Tick(in PlayerInputFrame input, float dt)
        {
            _time += dt;
            if (_current < 0 || aimOrigin == null) return;

            if (input.WeaponSlotPressed >= 0) Equip(input.WeaponSlotPressed);

            var state = _states[_current];
            var d = state.Data;

            if (_equipTimer > 0f) _equipTimer -= dt;

            bool wantsAim = input.AimHeld && d.canAim && !_reloading;
            float adsRate = d.adsTime > 0f ? dt / d.adsTime : 1f;
            _aimBlend = Mathf.MoveTowards(_aimBlend, wantsAim ? 1f : 0f, adsRate);

            if (input.ReloadPressed && !d.usesHeat) TryStartReload();
            if (_reloading) TickReload(state, dt);

            CurrentSpread = ComputeSpread(d);

            bool trigger = d.fireMode == FireMode.FullAuto ? input.FireHeld : input.FirePressed;

            // Heat weapons cool down when not firing and lock out when overheated.
            if (d.usesHeat)
            {
                if (state.OverheatTimer > 0f)
                {
                    state.OverheatTimer -= dt;
                    state.Heat = Mathf.MoveTowards(state.Heat, 0f, dt / Mathf.Max(0.1f, d.overheatLockout));
                    return;
                }
                // Only cools once you've actually stopped shooting (it used to cool between every shot of the
                // 1200 RPM beam, so holding the trigger never overheated = infinite fire).
                if (!trigger && _time - state.LastHeatShot >= d.heatCoolDelay)
                    state.Heat = Mathf.MoveTowards(state.Heat, 0f, d.heatCoolRate * dt);
            }

            if (!trigger || _equipTimer > 0f || _time < _nextFireTime) return;

            if (d.delivery == DeliveryType.Melee)
            {
                Melee(d);
                return;
            }

            if (d.usesHeat)
            {
                state.Heat += d.heatPerShot;
                state.LastHeatShot = _time;
                if (state.Heat >= 1f)
                {
                    state.Heat = 1f;
                    state.OverheatTimer = d.overheatLockout;
                    AudioController.Play2D(SfxId.Overheat, 0.8f);
                }
                state.Mag = Mathf.Max(state.Mag, 1);
            }

            if (_reloading)
            {
                // Shell-by-shell weapons can interrupt the reload if there's something loaded.
                if (d.reloadPerShell && state.Mag > 0) _reloading = false;
                else return;
            }

            if (state.Mag <= 0)
            {
                if (input.FirePressed) AudioController.Play2D(SfxId.DryFire);
                TryStartReload();
                return;
            }

            Fire(state);
        }

        // ------------------------------------------------------------------ Firing

        private void Fire(WeaponState state)
        {
            var d = state.Data;
            if (!d.usesHeat) state.Mag--;
            _nextFireTime = _time + d.SecondsPerShot;
            _shotCounter++;

            Vector3 origin = aimOrigin.position;
            Vector3 forward = aimOrigin.forward;
            Vector3 muzzlePos = muzzle != null ? muzzle.position
                : origin + forward * 0.6f + aimOrigin.right * 0.18f - aimOrigin.up * 0.15f;

            if (d.delivery == DeliveryType.Hitscan) FireHitscan(d, origin, forward, muzzlePos);
            else FireProjectile(d, origin, forward, muzzlePos);

            SimpleVfx.MuzzleFlash(muzzlePos);
            PlayFireSound(d);

            if (d.airborneSelfKnockback > 0f && _movement != null && !_movement.IsGrounded)
                _movement.AddImpulse(-forward * d.airborneSelfKnockback);

            if (Fired != null) Fired(d);
            if (AmmoChanged != null) AmmoChanged();

            if (state.Mag <= 0) TryStartReload();
        }

        private void FireHitscan(WeaponData d, Vector3 origin, Vector3 forward, Vector3 muzzlePos)
        {
            _accum.Clear();
            _accumKeys.Clear();
            int pellets = Mathf.Max(1, d.pelletsPerShot);
            float spread = CurrentSpread;

            for (int p = 0; p < pellets; p++)
            {
                Vector3 dir = SpreadDirection(forward, spread, p, pellets, d.fixedPelletPattern);
                RaycastHit hit;
                Vector3 end;
                if (RaycastIgnoringSelf(origin, dir, d.range, out hit))
                {
                    end = hit.point;
                    Hitbox hb = hit.collider.GetComponent<Hitbox>();
                    IDamageable target = hb != null ? hb.Owner : hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && target.IsAlive && target.Owner != gameObject)
                    {
                        bool head = hb != null && hb.IsHead;
                        float dmg = d.DamageAtDistance(hit.distance) * (head ? d.headshotMultiplier : 1f)
                                    * (hb != null ? hb.RegionMultiplier : 1f);
                        HitAccum a;
                        if (!_accum.TryGetValue(target, out a))
                        {
                            a = new HitAccum { Point = hit.point, Distance = hit.distance };
                            _accum.Add(target, a);
                            _accumKeys.Add(target);
                        }
                        a.Damage += dmg;
                        a.Headshot |= head;
                        SimpleVfx.Impact(hit.point, head ? new Color(1f, 0.1f, 0.1f, 1f) : new Color(0.9f, 0.2f, 0.2f, 1f));
                    }
                    else
                    {
                        SimpleVfx.Impact(hit.point, new Color(1f, 0.85f, 0.6f, 1f), 0.1f);
                    }
                }
                else end = origin + dir * d.range;

                if (d.showTracers) CosmeticFx.Tracer(muzzlePos, end, d, TrailId, pellets);
            }

            // One damage event per target per shot (all pellets combined) = one clean hit marker.
            for (int i = 0; i < _accumKeys.Count; i++)
            {
                var target = _accumKeys[i];
                var a = _accum[target];
                var info = new DamageInfo
                {
                    Amount = a.Damage,
                    Type = pellets > 1 ? DamageType.Pellet : DamageType.Bullet,
                    Instigator = gameObject,
                    WeaponId = d.id,
                    Point = a.Point,
                    Direction = forward,
                    IsHeadshot = a.Headshot,
                    Distance = a.Distance
                };
                DamageResult r = target.ApplyDamage(info);
                Report(target, r, info);
            }
        }

        private void FireProjectile(WeaponData d, Vector3 origin, Vector3 forward, Vector3 muzzlePos)
        {
            Vector3 dir = SpreadDirection(forward, CurrentSpread, 0, 1, false);

            // Spawn from the eye so what you aim at is what you hit; if a wall is closer than the
            // spawn offset (rocket jumping into a floor/wall), detonate right there.
            const float spawnOffset = 0.6f;
            RaycastHit hit;
            if (RaycastIgnoringSelf(origin, dir, spawnOffset, out hit))
            {
                var go = new GameObject(d.displayName + " Projectile");
                var proj = go.AddComponent<Projectile>();
                proj.Launch(d, gameObject, hit.point + hit.normal * 0.05f, dir, hitMask, OnProjectileDamage);
                proj.Detonate(hit.point + hit.normal * 0.05f);
                return;
            }

            var rocket = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rocket.name = d.displayName + " Projectile";
            rocket.layer = 2; // Ignore Raycast
            Destroy(rocket.GetComponent<Collider>());
            rocket.transform.localScale = Vector3.one * 0.2f;
            var r = rocket.GetComponent<Renderer>();
            r.sharedMaterial = ProjectileMaterial();
            var p = rocket.AddComponent<Projectile>();
            p.Launch(d, gameObject, origin + dir * spawnOffset, dir, hitMask, OnProjectileDamage);
        }

        private static Material _projectileMat;
        private static Material ProjectileMaterial()
        {
            if (_projectileMat == null)
            {
                _projectileMat = SimpleVfx.CreateFxMaterial();
                _projectileMat.color = new Color(1f, 0.3f, 0.2f, 1f);
            }
            return _projectileMat;
        }

        private void OnProjectileDamage(IDamageable target, DamageResult result, DamageInfo info)
        {
            if (target.Owner == gameObject) return; // no hit marker for self-damage
            Report(target, result, info);
        }

        private void Report(IDamageable target, DamageResult r, DamageInfo info)
        {
            if (r.TotalDamage <= 0f && !r.Killed) return;
            if (r.Killed) AudioController.Play2D(SfxId.Kill);
            else if (r.IsHeadshot) AudioController.Play2D(SfxId.HitHeadshot);
            else AudioController.Play2D(SfxId.HitTick);

            if (HitConfirmed != null)
            {
                HitConfirmed(new HitReport
                {
                    Target = target,
                    Damage = r.TotalDamage,
                    Headshot = r.IsHeadshot,
                    Killed = r.Killed,
                    Point = info.Point,
                    WeaponId = info.WeaponId
                });
            }
        }

        /// <summary>Online: show a hit marker / play hit sounds for a hit the host confirmed.</summary>
        public void ReportExternal(IDamageable target, DamageResult r, DamageInfo info)
        {
            Report(target, r, info);
        }

        /// <summary>
        /// Online: replay another player's shot on this machine (their proxy). Visuals and sound only - hitscan deals
        /// no damage; rockets explode locally so knockback feels right, but their damage is blocked by
        /// <see cref="HealthController.Intercept"/> (only the shooter's own machine reports hits).
        /// </summary>
        public void SimulateRemoteShot(WeaponData d, Vector3 origin, Vector3 forward, Vector3 muzzlePos)
        {
            if (d == null) return;
            if (d.delivery == DeliveryType.Projectile) FireProjectile(d, origin, forward, muzzlePos);
            else if (d.delivery == DeliveryType.Hitscan)
            {
                int pellets = Mathf.Max(1, d.pelletsPerShot);
                for (int p = 0; p < pellets; p++)
                {
                    Vector3 dir = SpreadDirection(forward, d.hipSpread, p, pellets, d.fixedPelletPattern);
                    RaycastHit hit;
                    Vector3 end = RaycastIgnoringSelf(origin, dir, d.range, out hit) ? hit.point : origin + dir * d.range;
                    if (d.showTracers) CosmeticFx.Tracer(muzzlePos, end, d, TrailId, pellets);
                    if (hit.collider != null) SimpleVfx.Impact(hit.point, new Color(1f, 0.85f, 0.6f, 1f), 0.1f);
                }
            }
            if (d.delivery != DeliveryType.Melee) SimpleVfx.MuzzleFlash(muzzlePos);
            AudioController.Play(SoundFor(d), muzzlePos);
        }

        private void Melee(WeaponData d)
        {
            _nextFireTime = _time + d.SecondsPerShot;
            AudioController.Play2D(SfxId.MeleeSwing, 0.9f, UnityEngine.Random.Range(0.95f, 1.05f));
            if (Fired != null) Fired(d);

            Vector3 origin = aimOrigin.position;
            Vector3 forward = aimOrigin.forward;
            RaycastHit best = default(RaycastHit);
            bool found = false;
            float bestDist = float.MaxValue;
            int n = Physics.SphereCastNonAlloc(origin, d.meleeRadius, forward, RayHits, d.meleeRange, hitMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (RayHits[i].collider.transform.IsChildOf(transform)) continue;
                if (RayHits[i].distance < bestDist) { bestDist = RayHits[i].distance; best = RayHits[i]; found = true; }
            }
            if (!found) return;

            Hitbox hb = best.collider.GetComponent<Hitbox>();
            IDamageable target = hb != null ? hb.Owner : best.collider.GetComponentInParent<IDamageable>();
            Vector3 point = best.distance <= 0f ? origin + forward * 0.5f : best.point;
            SimpleVfx.Impact(point, new Color(1f, 0.9f, 0.9f, 1f), 0.25f);
            if (target == null || !target.IsAlive || target.Owner == gameObject) return;

            // Backstab: attacker is behind the victim.
            bool back = Vector3.Dot(target.Owner.transform.forward, forward) > 0.5f;
            bool head = hb != null && hb.IsHead;
            var info = new DamageInfo
            {
                Amount = d.damage * (back ? d.backstabMultiplier : 1f) * (head ? d.headshotMultiplier : 1f),
                Type = DamageType.Melee,
                Instigator = gameObject,
                WeaponId = d.id,
                Point = point,
                Direction = forward,
                IsHeadshot = head,
                Distance = best.distance
            };
            Report(target, target.ApplyDamage(info), info);
        }

        private void PlayFireSound(WeaponData d)
        {
            if (d.fireSound != null)
            {
                // Real clip assigned on the WeaponData.
                AudioSource.PlayClipAtPoint(d.fireSound, aimOrigin.position);
                return;
            }
            SfxId id = SoundFor(d);
            AudioController.Play2D(id, 0.9f, UnityEngine.Random.Range(0.96f, 1.04f));
        }

        public static SfxId SoundFor(WeaponData d)
        {
            if (d.delivery == DeliveryType.Projectile) return SfxId.RocketFire;
            if (d.usesHeat) return SfxId.EnergyFire;
            if (d.isSniper) return SfxId.SniperFire;
            if (d.pelletsPerShot > 1) return d.damage * d.pelletsPerShot >= 150f ? SfxId.HeavyFire : SfxId.ShotgunFire;
            if (d.fireMode == FireMode.FullAuto) return d.fireRate >= 800f ? SfxId.SmgFire : SfxId.RifleFire;
            return SfxId.PistolFire;
        }

        // ------------------------------------------------------------------ Spread

        private float ComputeSpread(WeaponData d)
        {
            float s = Mathf.Lerp(d.hipSpread, d.adsSpread, _aimBlend);
            if (_movement != null)
            {
                if (!_movement.IsGrounded) s += d.airborneSpreadAdd;
                else
                {
                    float moveFactor = Mathf.Clamp01(_movement.HorizontalSpeed / Mathf.Max(0.1f, _movement.Settings.sprintSpeed));
                    s += d.movingSpreadAdd * moveFactor;
                }
            }
            return s;
        }

        private Vector3 SpreadDirection(Vector3 forward, float spreadDeg, int pellet, int pellets, bool fixedPattern)
        {
            if (spreadDeg <= 0f) return forward;
            Vector2 offset;
            if (fixedPattern && pellets > 1)
            {
                // Predictable pattern: centre pellet + alternating inner/outer ring.
                if (pellet == 0) offset = Vector2.zero;
                else
                {
                    float angle = (pellet - 1) * (360f / (pellets - 1)) * Mathf.Deg2Rad;
                    float ring = (pellet % 2 == 0) ? 1f : 0.55f;
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                }
            }
            else
            {
                // TODO(Phase 6): seed from (tick, shotCounter) so server + client agree on spread.
                offset = UnityEngine.Random.insideUnitCircle;
            }
            offset *= spreadDeg;
            Quaternion q = Quaternion.AngleAxis(offset.x, aimOrigin.up) * Quaternion.AngleAxis(-offset.y, aimOrigin.right);
            return q * forward;
        }

        private bool RaycastIgnoringSelf(Vector3 origin, Vector3 dir, float range, out RaycastHit best)
        {
            best = default(RaycastHit);
            int n = Physics.RaycastNonAlloc(origin, dir, RayHits, range, hitMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (RayHits[i].collider.transform.IsChildOf(transform)) continue;
                if (RayHits[i].distance < bestDist)
                {
                    bestDist = RayHits[i].distance;
                    best = RayHits[i];
                    found = true;
                }
            }
            return found;
        }

        // ------------------------------------------------------------------ Reload / switch

        private void TryStartReload()
        {
            if (_current < 0 || _reloading) return;
            var s = _states[_current];
            if (s.Data.usesHeat || s.Data.delivery == DeliveryType.Melee) return;
            if (s.Mag >= s.Data.magazineSize) return;
            if (!infiniteReserve && s.Reserve <= 0) return;
            _reloading = true;
            _reloadTimer = s.Data.reloadTime;
            _aimBlend = 0f;
            AudioController.Play2D(SfxId.Reload, 0.7f);
            if (ReloadStarted != null) ReloadStarted(s.Data);
        }

        private void TickReload(WeaponState s, float dt)
        {
            _reloadTimer -= dt;
            if (_reloadTimer > 0f) return;

            var d = s.Data;
            int needed = d.magazineSize - s.Mag;
            int take = d.reloadPerShell ? 1 : needed;
            if (!infiniteReserve) take = Mathf.Min(take, s.Reserve);
            s.Mag += take;
            if (!infiniteReserve) s.Reserve -= take;
            if (AmmoChanged != null) AmmoChanged();

            bool more = d.reloadPerShell && s.Mag < d.magazineSize && (infiniteReserve || s.Reserve > 0);
            if (more)
            {
                _reloadTimer = d.reloadTime;
                AudioController.Play2D(SfxId.Reload, 0.5f, 1.1f);
            }
            else _reloading = false;
        }

        public void Equip(int index)
        {
            if (index < 0 || index >= _states.Length || index == _current) return;
            _current = index;
            _reloading = false;
            _aimBlend = 0f;
            _equipTimer = _states[index].Data.equipTime;
            AudioController.Play2D(SfxId.Equip, 0.6f);
            if (WeaponChanged != null) WeaponChanged(_states[index].Data);
            if (AmmoChanged != null) AmmoChanged();
        }

        /// <summary>Respawn: full ammo, first weapon.</summary>
        public void ResetLoadout()
        {
            foreach (var s in _states)
            {
                s.Mag = s.Data.magazineSize;
                s.Reserve = s.Data.reserveAmmo;
                s.Heat = 0f;
                s.OverheatTimer = 0f;
            }
            _reloading = false;
            _current = -1;
            if (_states.Length > 0) Equip(0);
        }
    }
}
