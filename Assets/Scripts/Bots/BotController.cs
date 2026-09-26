using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Vamp.Audio;
using Vamp.Combat;
using Vamp.Match;
using Vamp.VFX;
using Vamp.Weapons;

namespace Vamp.Bots
{
    /// <summary>
    /// Offline opponents. NavMesh movement, target selection by distance + line of sight, reaction time, aim error
    /// and strafing scaled by difficulty. Bots use real WeaponData (damage, fire rate, falloff, headshots) through
    /// the same damage pipeline as players. They exist so the whole match loop works offline; online, the server
    /// can still use them to backfill lobbies.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Participant))]
    public sealed class BotController : MonoBehaviour
    {
        public BotDifficulty Difficulty = BotDifficulty.Normal;
        public WeaponData Weapon;
        public Transform Eye;
        public LayerMask HitMask = Physics.DefaultRaycastLayers;

        private NavMeshAgent _agent;
        private Participant _self;
        private Participant _target;
        private float _seenSince;
        private float _nextThink;
        private float _nextShot;
        private float _strafeTimer;
        private float _strafeDir = 1f;
        private int _mag;
        private float _reloadUntil;
        private Vector3 _roamGoal;
        private bool _hasRoamGoal;
        private static readonly RaycastHit[] Hits = new RaycastHit[12];

        public static List<Participant> All = new List<Participant>();
        public static List<Vector3> RoamPoints = new List<Vector3>();
        public static bool Active = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { All = new List<Participant>(); RoamPoints = new List<Vector3>(); Active = true; }

        private float Reaction { get { return Difficulty == BotDifficulty.Easy ? 0.6f : Difficulty == BotDifficulty.Normal ? 0.38f : Difficulty == BotDifficulty.Hard ? 0.24f : 0.15f; } }
        private float AimError { get { return Difficulty == BotDifficulty.Easy ? 4.5f : Difficulty == BotDifficulty.Normal ? 2.6f : Difficulty == BotDifficulty.Hard ? 1.5f : 0.9f; } }
        private float HeadChance { get { return Difficulty == BotDifficulty.Easy ? 0.05f : Difficulty == BotDifficulty.Normal ? 0.15f : Difficulty == BotDifficulty.Hard ? 0.25f : 0.35f; } }
        private float MoveSpeed { get { return Difficulty == BotDifficulty.Easy ? 7f : Difficulty == BotDifficulty.Normal ? 8.5f : Difficulty == BotDifficulty.Hard ? 9.5f : 10.5f; } }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _self = GetComponent<Participant>();
            _agent.acceleration = 40f;
            _agent.angularSpeed = 900f;
            _agent.stoppingDistance = 1f;
            _agent.autoBraking = false;
            _agent.radius = 0.4f;
            _agent.height = 1.8f;
            if (Eye == null) Eye = transform;
        }

        private void OnEnable() { if (!All.Contains(_self)) All.Add(_self); }
        private void OnDisable() { All.Remove(_self); }

        private void Start()
        {
            _agent.speed = MoveSpeed;
            _mag = Weapon != null ? Weapon.magazineSize : 10;
        }

        public void Warp(Vector3 position, float yaw)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(position, out hit, 3f, NavMesh.AllAreas)) position = hit.position;
            if (_agent.isOnNavMesh || NavMesh.SamplePosition(position, out hit, 3f, NavMesh.AllAreas)) _agent.Warp(position);
            else transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _target = null;
            _hasRoamGoal = false;
            _mag = Weapon != null ? Weapon.magazineSize : 10;
        }

        private void Update()
        {
            bool alive = _self.Alive;
            if (!alive || !Active || !_agent.isOnNavMesh)
            {
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                return;
            }
            _agent.isStopped = false;

            if (Time.time >= _nextThink)
            {
                _nextThink = Time.time + 0.2f;
                ChooseTarget();
            }

            if (_target != null && _target.Alive) Engage();
            else Roam();
        }

        private void ChooseTarget()
        {
            Participant best = null;
            float bestD = float.MaxValue;
            foreach (var p in All)
            {
                if (p == null || p == _self || !p.Alive || !_self.IsEnemyOf(p)) continue;
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d > 90f || d >= bestD) continue;
                if (!CanSee(p)) continue;
                best = p;
                bestD = d;
            }
            if (best != _target)
            {
                _target = best;
                _seenSince = Time.time;
            }
        }

        private bool CanSee(Participant p)
        {
            Vector3 from = Eye.position;
            Vector3 to = p.transform.position + Vector3.up * 1.3f;
            RaycastHit hit;
            if (!Physics.Linecast(from, to, out hit, HitMask, QueryTriggerInteraction.Ignore)) return true;
            if (hit.collider.transform.IsChildOf(transform)) return false;
            return hit.collider.GetComponentInParent<Participant>() == p;
        }

        private void Engage()
        {
            Vector3 toTarget = _target.transform.position - transform.position;
            float dist = toTarget.magnitude;
            float preferred = Weapon == null ? 12f : Weapon.pelletsPerShot > 1 || Weapon.delivery == DeliveryType.Melee ? 4f : Weapon.isSniper ? 35f : Mathf.Clamp(Weapon.falloffStart, 6f, 22f);

            // Face the target
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude > 0.01f)
            {
                _agent.updateRotation = false;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 540f * Time.deltaTime);
            }

            // Strafe around preferred range
            _strafeTimer -= Time.deltaTime;
            if (_strafeTimer <= 0f)
            {
                _strafeTimer = Random.Range(0.4f, 1.3f);
                _strafeDir = Random.value < 0.5f ? -1f : 1f;
            }
            Vector3 side = Vector3.Cross(Vector3.up, flat.normalized) * _strafeDir * 4f;
            Vector3 range = dist > preferred * 1.2f ? flat.normalized * 5f : dist < preferred * 0.6f ? -flat.normalized * 4f : Vector3.zero;
            Vector3 goal = transform.position + side + range;
            NavMeshHit nh;
            if (NavMesh.SamplePosition(goal, out nh, 3f, NavMesh.AllAreas)) _agent.SetDestination(nh.position);

            // Line of sight lost → chase
            if (!CanSee(_target))
            {
                _agent.SetDestination(_target.transform.position);
                return;
            }

            if (Time.time - _seenSince < Reaction) return;
            TryShoot(dist);
        }

        private void Roam()
        {
            _agent.updateRotation = true;
            if (!_hasRoamGoal || _agent.remainingDistance < 2f)
            {
                Vector3 p = RoamPoints.Count > 0 ? RoamPoints[Random.Range(0, RoamPoints.Count)] : transform.position + Random.insideUnitSphere * 20f;
                NavMeshHit nh;
                if (NavMesh.SamplePosition(p, out nh, 5f, NavMesh.AllAreas))
                {
                    _agent.SetDestination(nh.position);
                    _roamGoal = nh.position;
                    _hasRoamGoal = true;
                }
            }
        }

        private void TryShoot(float dist)
        {
            if (Weapon == null || Time.time < _nextShot || Time.time < _reloadUntil) return;
            if (Weapon.delivery == DeliveryType.Melee && dist > Weapon.meleeRange + 0.5f) return;

            _nextShot = Time.time + Weapon.SecondsPerShot * (Weapon.fireMode == FireMode.SemiAuto ? Random.Range(1.1f, 1.6f) : 1f);
            if (!Weapon.usesHeat && Weapon.delivery != DeliveryType.Melee)
            {
                _mag--;
                if (_mag <= 0)
                {
                    _mag = Weapon.magazineSize;
                    _reloadUntil = Time.time + Weapon.reloadTime * (Weapon.reloadPerShell ? Weapon.magazineSize * 0.6f : 1f);
                }
            }

            bool aimHead = Random.value < HeadChance;
            Vector3 aimPoint = _target.transform.position + Vector3.up * (aimHead ? 1.65f : 1.1f);
            Vector3 origin = Eye.position;
            Vector3 dir = (aimPoint - origin).normalized;
            // Moving targets are harder to hit.
            var tm = _target.GetComponent<Movement.MovementController>();
            float speedPenalty = tm != null ? Mathf.Clamp(tm.HorizontalSpeed / 12f, 0f, 2f) : 0f;
            float err = AimError * (1f + speedPenalty);

            int pellets = Mathf.Max(1, Weapon.pelletsPerShot);
            float total = 0f;
            bool headshot = false;
            IDamageable victim = null;
            Vector3 hitPoint = aimPoint;
            for (int i = 0; i < pellets; i++)
            {
                Vector2 e = Random.insideUnitCircle * (err + Weapon.hipSpread * 0.6f);
                Vector3 d = Quaternion.AngleAxis(e.x, Vector3.up) * Quaternion.AngleAxis(e.y, Vector3.Cross(dir, Vector3.up)) * dir;
                RaycastHit hit;
                float range = Weapon.delivery == DeliveryType.Melee ? Weapon.meleeRange + 0.5f : Weapon.range;
                if (!RaycastSkipSelf(origin, d, range, out hit))
                {
                    if (i == 0 && Weapon.showTracers) SimpleVfx.TracerLine(origin + d * 0.5f, origin + d * range, Weapon.tracerColor, 0.02f);
                    continue;
                }
                if (i == 0 && Weapon.showTracers) SimpleVfx.TracerLine(origin + d * 0.5f, hit.point, Weapon.tracerColor, 0.02f);
                var hb = hit.collider.GetComponent<Hitbox>();
                var dmg = hb != null ? hb.Owner : hit.collider.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Owner == gameObject) { SimpleVfx.Impact(hit.point, new Color(1f, 0.85f, 0.6f), 0.1f); continue; }
                bool head = hb != null ? hb.IsHead : hit.point.y > dmg.Owner.transform.position.y + 1.45f;
                float amount = Weapon.DamageAtDistance(hit.distance) * (head ? Weapon.headshotMultiplier : 1f);
                total += amount;
                headshot |= head;
                victim = dmg;
                hitPoint = hit.point;
            }

            SimpleVfx.MuzzleFlash(origin + dir * 0.6f, 0.2f);
            AudioController.Play(WeaponController.SoundFor(Weapon), origin, 0.8f, Random.Range(0.95f, 1.05f));

            if (victim != null && total > 0f)
            {
                victim.ApplyDamage(new DamageInfo
                {
                    Amount = total,
                    Type = pellets > 1 ? DamageType.Pellet : DamageType.Bullet,
                    Instigator = gameObject,
                    WeaponId = Weapon.id,
                    Point = hitPoint,
                    Direction = dir,
                    IsHeadshot = headshot,
                    Distance = Vector3.Distance(origin, hitPoint)
                });
            }
        }

        private bool RaycastSkipSelf(Vector3 origin, Vector3 dir, float range, out RaycastHit best)
        {
            best = default(RaycastHit);
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, range, HitMask, QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].collider.transform.IsChildOf(transform)) continue;
                if (Hits[i].distance < bestD) { bestD = Hits[i].distance; best = Hits[i]; found = true; }
            }
            return found;
        }
    }
}
