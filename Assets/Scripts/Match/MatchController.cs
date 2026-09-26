using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Vamp.Audio;
using Vamp.Bots;
using Vamp.Combat;
using Vamp.Core;
using Vamp.Maps;
using Vamp.Movement;
using Vamp.Player;
using Vamp.Progression;
using Vamp.Social;
using Vamp.Weapons;

namespace Vamp.Match
{
    public enum MatchPhase { Intro, Countdown, Live, RoundOver, Ended }

    public sealed class MatchOutcome
    {
        public bool Won;
        public int Placement;
        public string Title;
        public string Subtitle;
        public List<Participant> Standings = new List<Participant>();
        public MatchXpResult Xp;
        public MatchReport Report;
    }

    /// <summary>
    /// Runs a match: rules for FREE FOR ALL, TEAM DEATHMATCH, GUN GAME, MOVEMENT RACE, ELIMINATION, TRAINING and
    /// custom options; spawns the player and bots; server-controlled spawning; scoring, assists, kill streaks;
    /// match flow (intro → countdown → match → end → XP / stats → post match). OFFLINE this is the authority;
    /// online, exactly this logic moves to the dedicated server.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private string mapId = "vertex";
        [SerializeField] private float introSeconds = 2.5f;
        [SerializeField] private float countdownSeconds = 3f;
        [SerializeField] private LayerMask worldMask = Physics.DefaultRaycastLayers;

        public static MatchController Instance { get; private set; }

        public MatchConfig Config { get; private set; }
        public MatchPhase Phase { get; private set; }
        public float PhaseTimer { get; private set; }
        public float Elapsed { get; private set; }
        public float TimeRemaining { get { return Config.timeLimitMinutes > 0f ? Mathf.Max(0f, Config.timeLimitMinutes * 60f - Elapsed) : -1f; } }
        public readonly int[] TeamScores = new int[2];
        public int Round { get; private set; }
        public readonly List<Participant> Participants = new List<Participant>();
        public Participant Local { get; private set; }
        public PlayerController Player { get; private set; }
        public List<RaceCheckpoint> Checkpoints { get; private set; }
        public MatchOutcome Outcome { get; private set; }
        public MapInfo Map { get; private set; }

        public event Action<string, string> Announced;
        public event Action<MatchOutcome> Ended;

        private SpawnController _spawns;
        private MatchStatsTracker _stats;
        private readonly List<KeyValuePair<Participant, float>> _pendingRespawns = new List<KeyValuePair<Participant, float>>();
        private int _lastCountdown = -1;
        private float _roundTimer;

        // ------------------------------------------------------------------ Setup

        /// <summary>Online matches are run by the network session - map scenes' offline controller stands down.</summary>
        public static bool SuppressOffline;

        private void Awake()
        {
            if (SuppressOffline) { enabled = false; return; }
            Instance = this;
            Round = 1;
        }

        private void Start()
        {
            if (SuppressOffline) return;
            Config = MatchLaunch.Pending ?? DefaultConfigForScene();
            MatchLaunch.Pending = null;
            if (MatchLaunch.Last == null) MatchLaunch.Last = Config.Clone();
            Map = MapCatalog.Get(Config.mapId);
            if (Game.Party != null) Game.Party.SetState(LobbyState.PreMatch);

            BuildNavMesh();

            var points = new List<SpawnPoint>(FindObjectsByType<SpawnPoint>());
            _spawns = new SpawnController(points, worldMask);
            BotController.RoamPoints.Clear();
            foreach (var p in points) BotController.RoamPoints.Add(p.transform.position);
            BotController.Active = false;

            Checkpoints = new List<RaceCheckpoint>(FindObjectsByType<RaceCheckpoint>());
            Checkpoints.Sort((a, b) => a.Index.CompareTo(b.Index));
            bool race = Config.mode == GameMode.MovementRace;
            foreach (var cp in Checkpoints) cp.gameObject.SetActive(race);

            SetupPlayer();
            SetupBots();

            HealthController.DamageFilter = CanDamage;
            KillFeed.EntryAdded += OnKill;
            RaceCheckpoint.Passed += OnCheckpoint;

            _stats = new MatchStatsTracker(Local, Game.Progression != null ? Game.Progression.Config.highSpeedKillThreshold : 18f);

            if (Config.benchmark)
            {
                gameObject.AddComponent<Graphics.BenchmarkRunner>();
                SetPhase(MatchPhase.Live);
                BotController.Active = true;
                return;
            }

            gameObject.AddComponent<UI.MatchUI>();
            gameObject.AddComponent<UI.PauseMenu>();

            if (Config.mode == GameMode.Training)
            {
                SetPhase(MatchPhase.Live);
                BotController.Active = true;
                Announce("TRAINING", "FREE PRACTICE · ESC FOR MENU");
            }
            else
            {
                SetPhase(MatchPhase.Intro);
                PhaseTimer = introSeconds;
                if (Player != null) Player.Input.MovementLocked = true;
            }
            UpdateCheckpointVisuals();
        }

        private MatchConfig DefaultConfigForScene()
        {
            string scene = SceneManager.GetActiveScene().name;
            foreach (var m in MapCatalog.Maps)
                if (m.SceneName == scene) mapId = m.Id;
            var cfg = mapId == "movement_lab" ? MatchConfig.Defaults(GameMode.Training) : MatchConfig.Defaults(GameMode.FreeForAll);
            cfg.mapId = mapId;
            if (mapId != "movement_lab") cfg.bots = 5;
            return cfg;
        }

        private void BuildNavMesh()
        {
            var surface = FindAnyObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                var go = new GameObject("[NavMesh]");
                surface = go.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            }
            try { surface.BuildNavMesh(); }
            catch (Exception e) { Debug.LogWarning("[VAMP] NavMesh build failed, bots will stand still: " + e.Message); }
        }

        private void SetupPlayer()
        {
            Player = FindAnyObjectByType<PlayerController>();
            if (Player == null && playerPrefab != null) Player = Instantiate(playerPrefab).GetComponent<PlayerController>();
            if (Player == null)
            {
                Debug.LogError("[VAMP] MatchController: no player in scene and no player prefab assigned.");
                return;
            }

            Local = Player.GetComponent<Participant>();
            if (Local == null) Local = Player.gameObject.AddComponent<Participant>();
            Local.IsLocalPlayer = true;
            Local.IsBot = false;
            Local.DisplayName = Game.Username;
            var prof = Game.Progression != null ? Game.Progression.Profile : null;
            if (prof != null)
            {
                Local.Level = prof.level;
                Local.Prestige = prof.prestige_level;
                Local.Icon = prof.profile_icon;
                Local.Frame = prof.icon_frame;
                var t = CosmeticCatalog.Get(prof.title);
                Local.Title = prof.title_visible && t != null ? t.Name : "";
            }
            Local.Team = Config.IsTeamMode ? 0 : -1;
            Local.ApplyIdentity();
            Participants.Add(Local);

            // Custom game movement rules (runtime copy - never touches the project asset)
            if (!Mathf.Approximately(Config.gravityMultiplier, 1f) || !Mathf.Approximately(Config.movementSpeedMultiplier, 1f))
                Player.Movement.SetSettings(Player.Movement.CreateScaledCopy(Config.gravityMultiplier, Config.movementSpeedMultiplier));

            Player.Weapons.SetLoadout(BuildLoadout(Local));
            Player.RespawnDelay = Config.respawnDelay;
            Player.RespawnEnabled = Config.respawns && Config.mode != GameMode.Elimination && Config.mode != GameMode.MovementRace;
            Player.SpawnSelector = p => Choose(Local);

            if (Config.mode == GameMode.MovementRace)
            {
                var start = GameObject.Find("RaceStart");
                if (start != null) Teleport(Local, start.transform.position, start.transform.eulerAngles.y);
            }
            else if (Config.mode != GameMode.Training)
            {
                var c = Choose(Local);
                if (c.Valid) Teleport(Local, c.Position, c.Yaw);
            }
            Local.LastSpawnTime = Time.time;

            if (FindAnyObjectByType<UI.HUDController>() == null)
            {
                var hud = new GameObject("HUD");
                hud.AddComponent<UI.HUDController>();
                hud.AddComponent<UI.MovementDebugOverlay>();
            }
        }

        private void SetupBots()
        {
            int count = Mathf.Clamp(Config.bots, 0, 15);
            if (Config.mode == GameMode.MovementRace) count = 0;
            if (Config.benchmark) count = 8;
            for (int i = 0; i < count; i++)
            {
                int team = Config.IsTeamMode ? ((i % 2 == 0) ? 1 : 0) : -1;
                var probe = new GameObject("probe").AddComponent<Participant>();
                probe.Team = team;
                var spawn = _spawns.Pick(probe, Participants, Config.IsTeamMode);
                Destroy(probe.gameObject);
                Vector3 pos = spawn != null ? spawn.transform.position : UnityEngine.Random.insideUnitSphere * 10f;
                float yaw = spawn != null ? spawn.transform.eulerAngles.y : 0f;
                var bot = BotFactory.Create(i, team, Config.benchmark ? BotDifficulty.Brutal : Config.botDifficulty, null,
                                            Local != null && team >= 0 && team == Local.Team, pos, yaw);
                var bc = bot.GetComponent<BotController>();
                bc.Weapon = BotWeapon(bot);
                bot.Health.Died += info => OnBotDied(bot);
                bot.LastSpawnTime = Time.time;
                Participants.Add(bot);
            }
        }

        // ------------------------------------------------------------------ Loadouts

        private bool Allowed(WeaponData w)
        {
            return w != null && !Config.restrictedWeapons.Contains(w.id);
        }

        private WeaponData PickAllowed(string id, WeaponSlot slot)
        {
            var cat = Game.Weapons;
            if (cat == null) return null;
            var w = cat.Get(id);
            if (w != null && WeaponCatalog.FitsLoadoutSlot(w, slot) && Allowed(w)) return w;
            foreach (var x in cat.InSlot(slot)) if (Allowed(x)) return x;
            return null;
        }

        private WeaponData[] BuildLoadout(Participant p)
        {
            var cat = Game.Weapons;
            if (cat == null) return new WeaponData[0];
            if (Config.mode == GameMode.GunGame)
            {
                int lvl = Mathf.Clamp(p.GunGameLevel, 0, Mathf.Max(0, cat.gunGameOrder.Count - 1));
                return cat.gunGameOrder.Count > 0 ? new[] { cat.gunGameOrder[lvl] } : new WeaponData[0];
            }
            if (Config.mode == GameMode.MovementRace) return new WeaponData[0];

            var lo = Game.Progression != null && Game.Progression.Profile != null ? Game.Progression.Profile.loadout : new LoadoutData();
            var list = new List<WeaponData>();
            var primary = PickAllowed(lo.primary, WeaponSlot.Primary);
            var secondary = PickAllowed(lo.secondary, WeaponSlot.Secondary);
            if (primary != null) list.Add(primary);
            if (secondary != null) list.Add(secondary);
            if (Config.mode == GameMode.Training)
            {
                var blast = cat.Get("blast");
                if (blast != null && !list.Contains(blast)) list.Add(blast); // slot 3 = rocket jump practice
            }
            else
            {
                var melee = PickAllowed(lo.melee, WeaponSlot.Melee);
                if (melee != null) list.Add(melee);
            }
            return list.ToArray();
        }

        private WeaponData BotWeapon(Participant bot)
        {
            var cat = Game.Weapons;
            if (cat == null) return null;
            if (Config.mode == GameMode.GunGame && cat.gunGameOrder.Count > 0)
                return cat.gunGameOrder[Mathf.Clamp(bot.GunGameLevel, 0, cat.gunGameOrder.Count - 1)];
            var options = new List<WeaponData>();
            foreach (var id in new[] { "havoc", "ripper", "brute", "v9", "arc", "widow", "reaper" })
            {
                var w = cat.Get(id);
                if (Allowed(w)) options.Add(w);
            }
            if (options.Count == 0) foreach (var w in cat.weapons) if (Allowed(w) && w.delivery == DeliveryType.Hitscan) options.Add(w);
            return options.Count > 0 ? options[UnityEngine.Random.Range(0, options.Count)] : null;
        }

        // ------------------------------------------------------------------ Spawning

        private SpawnChoice Choose(Participant who)
        {
            var sp = _spawns.Pick(who, Participants, Config.IsTeamMode);
            if (sp == null) return new SpawnChoice();
            who.LastSpawnTime = Time.time;
            return new SpawnChoice { Valid = true, Position = sp.transform.position + Vector3.up * 0.05f, Yaw = sp.transform.eulerAngles.y };
        }

        private void Teleport(Participant p, Vector3 pos, float yaw)
        {
            if (p == Local && Player != null)
            {
                Player.SetSpawn(pos, yaw);
                Player.Movement.Teleport(pos, yaw);
                Player.View.SetView(yaw, 0f);
            }
            else
            {
                var bot = p.GetComponent<BotController>();
                if (bot != null) bot.Warp(pos, yaw);
            }
        }

        private void RespawnBot(Participant bot)
        {
            if (bot == null) return;
            bot.Health.Revive();
            bot.ClearDamagers();
            var c = Choose(bot);
            if (c.Valid) Teleport(bot, c.Position, c.Yaw);
            SetBotVisible(bot, true);
            var bc = bot.GetComponent<BotController>();
            if (bc != null && Config.mode == GameMode.GunGame) bc.Weapon = BotWeapon(bot);
        }

        private static void SetBotVisible(Participant bot, bool visible)
        {
            foreach (var r in bot.GetComponentsInChildren<Renderer>()) r.enabled = visible;
            foreach (var c in bot.GetComponentsInChildren<Collider>()) c.enabled = visible;
        }

        private void OnBotDied(Participant bot)
        {
            SetBotVisible(bot, false);
            Audio.AudioController.Play(SfxId.Death, bot.transform.position + Vector3.up, 0.8f);
            if (Config.respawns && Config.mode != GameMode.Elimination && Phase == MatchPhase.Live)
                _pendingRespawns.Add(new KeyValuePair<Participant, float>(bot, Time.time + Config.respawnDelay));
        }

        // ------------------------------------------------------------------ Rules

        private bool CanDamage(DamageInfo info, GameObject victim)
        {
            if (Phase != MatchPhase.Live && Config.mode != GameMode.Training) return false;
            if (info.Instigator == null) return true;
            var a = info.Instigator.GetComponentInParent<Participant>();
            var v = victim.GetComponentInParent<Participant>();
            if (a == null || v == null || a == v) return true;
            if (!a.IsEnemyOf(v) && !Config.friendlyFire) return false;
            return true;
        }

        private void OnKill(KillFeedEntry e)
        {
            var victim = e.VictimObject != null ? e.VictimObject.GetComponentInParent<Participant>() : null;
            if (victim == null) return; // training dummies etc.
            var killer = e.KillerObject != null ? e.KillerObject.GetComponentInParent<Participant>() : null;

            victim.Deaths++;
            victim.Streak = 0;

            if (killer != null && killer != victim)
            {
                if (!killer.IsEnemyOf(victim))
                {
                    killer.Score = Mathf.Max(0, killer.Score - 50);
                    if (killer.IsLocalPlayer) Announce("TEAM KILL", "-50");
                }
                else
                {
                    killer.Kills++;
                    killer.Streak++;
                    killer.BestStreak = Mathf.Max(killer.BestStreak, killer.Streak);
                    killer.Score += 100 + (e.Headshot ? 25 : 0);
                    if (e.Headshot) killer.Headshots++;
                    if (Config.IsTeamMode && killer.Team >= 0 && Config.mode == GameMode.TeamDeathmatch) TeamScores[killer.Team]++;
                    StreakAnnouncement(killer);
                    if (Config.mode == GameMode.GunGame) GunGameAdvance(killer);
                }
            }
            else if (!Config.IsTeamMode)
            {
                victim.Score = Mathf.Max(0, victim.Score - 50); // suicide
            }

            foreach (var d in victim.RecentDamagers(killer, 5f))
            {
                if (!d.IsEnemyOf(victim)) continue;
                d.Assists++;
                d.Score += 50;
            }
            victim.ClearDamagers();

            if (victim.IsLocalPlayer && killer != null && killer != victim && killer.Streak >= 5)
                Announce(killer.DisplayName.ToUpperInvariant() + " IS " + StreakName(killer.Streak), "");

            CheckWin(killer);
        }

        private static string StreakName(int streak)
        {
            if (streak >= 10) return "DOMINATING";
            if (streak >= 7) return "UNSTOPPABLE";
            if (streak >= 5) return "ON A RAMPAGE";
            return "ON A KILLING SPREE";
        }

        private void StreakAnnouncement(Participant killer)
        {
            if (!killer.IsLocalPlayer) return;
            string name = killer.Streak == 3 ? "KILLING SPREE" : killer.Streak == 5 ? "RAMPAGE" : killer.Streak == 7 ? "UNSTOPPABLE" : killer.Streak == 10 ? "DOMINATING" : null;
            if (name != null) Announce(name, killer.Streak + " KILLS WITHOUT DYING");
        }

        private void GunGameAdvance(Participant killer)
        {
            var cat = Game.Weapons;
            if (cat == null || cat.gunGameOrder.Count == 0) return;
            killer.GunGameLevel++;
            if (killer.GunGameLevel >= cat.gunGameOrder.Count) return; // CheckWin ends it
            if (killer.IsLocalPlayer && Player != null)
            {
                Player.Weapons.SetLoadout(BuildLoadout(killer));
                Announce(cat.gunGameOrder[killer.GunGameLevel].displayName, "WEAPON " + (killer.GunGameLevel + 1) + " / " + cat.gunGameOrder.Count);
            }
            else
            {
                var bc = killer.GetComponent<BotController>();
                if (bc != null) bc.Weapon = BotWeapon(killer);
            }
        }

        private void CheckWin(Participant killer)
        {
            if (Phase != MatchPhase.Live) return;
            switch (Config.mode)
            {
                case GameMode.FreeForAll:
                    if (killer != null && Config.scoreLimit > 0 && killer.Kills >= Config.scoreLimit) EndMatch(-1);
                    break;
                case GameMode.TeamDeathmatch:
                    for (int t = 0; t < 2; t++)
                        if (Config.scoreLimit > 0 && TeamScores[t] >= Config.scoreLimit) { EndMatch(t); return; }
                    break;
                case GameMode.GunGame:
                    if (killer != null && Game.Weapons != null && killer.GunGameLevel >= Game.Weapons.gunGameOrder.Count) EndMatch(-1);
                    break;
                case GameMode.Elimination:
                    CheckRound();
                    break;
            }
        }

        private void CheckRound()
        {
            int alive0 = 0, alive1 = 0;
            foreach (var p in Participants)
            {
                if (p == null || !p.Alive) continue;
                if (p.Team == 0) alive0++; else if (p.Team == 1) alive1++;
            }
            if (alive0 > 0 && alive1 > 0) return;
            int winner = alive0 > 0 ? 0 : 1;
            TeamScores[winner]++;
            bool localWon = Local != null && Local.Team == winner;
            if (TeamScores[winner] >= Mathf.Max(1, Config.scoreLimit))
            {
                EndMatch(winner);
                return;
            }
            Announce(localWon ? "ROUND WON" : "ROUND LOST", TeamScores[Local != null ? Local.Team : 0] + " - " + TeamScores[Local != null ? 1 - Local.Team : 1]);
            SetPhase(MatchPhase.RoundOver);
            _roundTimer = 3.5f;
        }

        private void NextRound()
        {
            Round++;
            foreach (var p in Participants)
            {
                if (p == null) continue;
                p.ClearDamagers();
                if (p == Local && Player != null)
                {
                    Player.Respawn();
                }
                else
                {
                    p.Health.Revive();
                    var c = Choose(p);
                    if (c.Valid) Teleport(p, c.Position, c.Yaw);
                    SetBotVisible(p, true);
                }
            }
            Elapsed = 0f;
            SetPhase(MatchPhase.Countdown);
            PhaseTimer = countdownSeconds;
            _lastCountdown = -1;
            if (Player != null) Player.Input.MovementLocked = true;
            BotController.Active = false;
            Announce("ROUND " + Round, "");
        }

        private void OnCheckpoint(RaceCheckpoint cp, Participant p)
        {
            if (Config.mode != GameMode.MovementRace || Phase != MatchPhase.Live || p == null || !p.IsLocalPlayer) return;
            if (cp.Index != p.NextCheckpoint) return;
            p.NextCheckpoint++;
            AudioController.PlayUI(SfxId.Checkpoint);
            if (p.NextCheckpoint >= Checkpoints.Count)
            {
                p.Finished = true;
                p.RaceTime = Elapsed;
                EndMatch(-1);
            }
            else Announce("CHECKPOINT " + p.NextCheckpoint + " / " + Checkpoints.Count, Elapsed.ToString("0.00") + " S");
            UpdateCheckpointVisuals();
        }

        private void UpdateCheckpointVisuals()
        {
            if (Checkpoints == null || Local == null) return;
            foreach (var cp in Checkpoints) cp.SetState(cp.Index == Local.NextCheckpoint, cp.Index < Local.NextCheckpoint);
        }

        // ------------------------------------------------------------------ Loop

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_stats != null) _stats.Tick();

            switch (Phase)
            {
                case MatchPhase.Intro:
                    PhaseTimer -= dt;
                    if (PhaseTimer <= 0f)
                    {
                        SetPhase(MatchPhase.Countdown);
                        PhaseTimer = countdownSeconds;
                        _lastCountdown = -1;
                    }
                    break;

                case MatchPhase.Countdown:
                    PhaseTimer -= dt;
                    int n = Mathf.CeilToInt(PhaseTimer);
                    if (n != _lastCountdown && n > 0)
                    {
                        _lastCountdown = n;
                        Announce(n.ToString(), "");
                        AudioController.PlayUI(SfxId.Countdown);
                    }
                    if (PhaseTimer <= 0f)
                    {
                        SetPhase(MatchPhase.Live);
                        Announce("GO", "");
                        AudioController.PlayUI(SfxId.CountdownGo);
                        if (Player != null) Player.Input.MovementLocked = false;
                        BotController.Active = true;
                        if (Game.Party != null) Game.Party.SetState(LobbyState.InMatch);
                    }
                    break;

                case MatchPhase.Live:
                    Elapsed += dt;
                    for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
                    {
                        if (Time.time < _pendingRespawns[i].Value) continue;
                        RespawnBot(_pendingRespawns[i].Key);
                        _pendingRespawns.RemoveAt(i);
                    }
                    if (Config.mode == GameMode.Elimination) CheckRound();
                    if (Config.timeLimitMinutes > 0f && TimeRemaining <= 0f && Config.mode != GameMode.Training)
                    {
                        if (Config.mode == GameMode.Elimination)
                        {
                            // Time out: team with more players alive wins the round.
                            int a0 = 0, a1 = 0;
                            foreach (var p in Participants) if (p != null && p.Alive) { if (p.Team == 0) a0++; else if (p.Team == 1) a1++; }
                            foreach (var p in Participants) if (p != null && p.Alive && p.Team == (a0 >= a1 ? 1 : 0)) p.Health.Kill(DamageType.World);
                        }
                        else EndMatch(TeamScores[0] == TeamScores[1] ? -1 : (TeamScores[0] > TeamScores[1] ? 0 : 1));
                    }
                    break;

                case MatchPhase.RoundOver:
                    _roundTimer -= dt;
                    if (_roundTimer <= 0f) NextRound();
                    break;
            }
        }

        private void SetPhase(MatchPhase p)
        {
            Phase = p;
        }

        public void Announce(string title, string subtitle)
        {
            if (Announced != null) Announced(title, subtitle);
            if (title != "GO" && title.Length > 1) AudioController.PlayUI(SfxId.Announcer, 0.6f);
        }

        // ------------------------------------------------------------------ End

        /// <param name="winningTeam">team index for team modes, -1 for individual modes.</param>
        public void EndMatch(int winningTeam)
        {
            if (Phase == MatchPhase.Ended) return;
            SetPhase(MatchPhase.Ended);
            BotController.Active = false;
            if (Player != null)
            {
                Player.Input.MovementLocked = true;
                Player.RespawnEnabled = false;
            }

            var standings = new List<Participant>();
            foreach (var p in Participants) if (p != null) standings.Add(p);
            if (Config.mode == GameMode.GunGame) standings.Sort((a, b) => b.GunGameLevel != a.GunGameLevel ? b.GunGameLevel.CompareTo(a.GunGameLevel) : b.Kills.CompareTo(a.Kills));
            else standings.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : b.Kills.CompareTo(a.Kills));

            int placement = Local != null ? standings.IndexOf(Local) + 1 : 1;
            bool won;
            if (Config.IsTeamMode) won = Local != null && winningTeam >= 0 && Local.Team == winningTeam;
            else if (Config.mode == GameMode.MovementRace) won = Local != null && Local.Finished;
            else won = placement == 1;

            string title = Config.mode == GameMode.MovementRace ? (won ? "FINISHED" : "TIME UP")
                         : Config.IsTeamMode && winningTeam < 0 ? "DRAW" : won ? "VICTORY" : "DEFEAT";
            string sub = Config.mode == GameMode.MovementRace && won ? "TIME " + Local.RaceTime.ToString("0.00") + " S"
                       : Config.IsTeamMode ? TeamScores[Local != null ? Local.Team : 0] + " - " + TeamScores[Local != null ? Mathf.Max(0, 1 - Local.Team) : 1]
                       : "PLACED #" + placement;

            var outcome = new MatchOutcome { Won = won, Placement = placement, Title = title, Subtitle = sub, Standings = standings };
            if (_stats != null && Config.mode != GameMode.Training)
            {
                outcome.Report = _stats.BuildReport(Config, Elapsed, won, placement);
                if (Game.Progression != null && Game.Progression.IsLoaded) outcome.Xp = Game.Progression.ApplyMatch(outcome.Report);
            }
            Outcome = outcome;

            AudioController.SetMusic(null);
            AudioController.PlayUI(SfxId.MatchEnd);
            Announce(title, sub);
            if (Game.Party != null) Game.Party.SetState(LobbyState.PostMatch);
            if (Ended != null) Ended(outcome);
        }

        /// <summary>Leaving early (pause menu). Offline: no XP for unfinished matches.</summary>
        public void Leave()
        {
            if (Game.Scenes != null) Game.Scenes.GoToMainMenu();
        }

        public void Rematch()
        {
            var cfg = MatchLaunch.Last != null ? MatchLaunch.Last.Clone() : Config.Clone();
            if (Game.Scenes != null) Game.Scenes.StartMatch(cfg);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            HealthController.DamageFilter = null;
            KillFeed.EntryAdded -= OnKill;
            RaceCheckpoint.Passed -= OnCheckpoint;
            if (_stats != null) _stats.Dispose();
            BotController.Active = true;
        }

        public List<Participant> Team(int team)
        {
            var l = new List<Participant>();
            foreach (var p in Participants) if (p != null && p.Team == team) l.Add(p);
            return l;
        }
    }
}
