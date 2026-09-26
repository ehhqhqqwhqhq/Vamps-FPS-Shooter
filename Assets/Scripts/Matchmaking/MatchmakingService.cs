using System;
using System.Collections.Generic;
using UnityEngine;
using Vamp.Match;
using Vamp.Social;

namespace Vamp.Matchmaking
{
    public enum MatchmakingState { Idle, Searching, MatchFound, Failed }

    [Serializable]
    public sealed class QuickPlayPreferences
    {
        public List<GameMode> modes = new List<GameMode> { GameMode.FreeForAll, GameMode.TeamDeathmatch };
        public string region = "AUTO";
        public int maxPing = 80;
        /// <summary>Arena sizes wanted (1 = 1V1, 2 = 2V2, 3 = 3V3).</summary>
        public List<int> arenaSizes = new List<int>();
    }

    /// <summary>
    /// Quick Play flow (SEARCHING... PLAYERS FOUND x/10 · PING). OFFLINE: the "search" fills the lobby with bots
    /// and produces a local match config, so the whole loop (party → search → match found → match → results →
    /// return to party → rematch) is playable. An online implementation replaces <see cref="Tick"/>'s fill logic
    /// with a real queue ticket.
    /// </summary>
    public sealed class MatchmakingService
    {
        public static readonly string[] Regions = { "AUTO", "NA EAST", "NA WEST", "EU WEST", "EU CENTRAL", "ASIA", "OCEANIA", "SA" };

        private readonly IPartyService _party;
        private float _elapsed;
        private float _nextFill;
        private System.Random _rng = new System.Random();

        public MatchmakingState State { get; private set; }
        public QuickPlayPreferences Preferences { get; private set; }
        public int PlayersFound { get; private set; }
        public int PlayersNeeded { get; private set; }
        public int PingMs { get; private set; }
        public float SearchTime { get { return _elapsed; } }
        public MatchConfig FoundMatch { get; private set; }
        public bool IsOnline { get { return false; } }

        public event Action Changed;
        public event Action<MatchConfig> MatchFound;

        public MatchmakingService(IPartyService party)
        {
            _party = party;
            Preferences = new QuickPlayPreferences();
        }

        public void StartQuickPlay(QuickPlayPreferences prefs)
        {
            if (prefs != null) Preferences = prefs;
            if (Preferences.arenaSizes == null) Preferences.arenaSizes = new List<int>();
            if ((Preferences.modes == null || Preferences.modes.Count == 0) && Preferences.arenaSizes.Count == 0)
                Preferences.modes = new List<GameMode> { GameMode.FreeForAll };
            if (Preferences.modes == null) Preferences.modes = new List<GameMode>();

            int pick = _rng.Next(Preferences.modes.Count + Preferences.arenaSizes.Count);
            if (pick < Preferences.modes.Count)
            {
                FoundMatch = MatchConfig.Defaults(Preferences.modes[pick]);
                FoundMatch.mapId = Maps.MapCatalog.RandomBattleMap(_rng, false);
            }
            else
            {
                FoundMatch = MatchConfig.Arena(Preferences.arenaSizes[pick - Preferences.modes.Count]);
                FoundMatch.mapId = Maps.MapCatalog.RandomBattleMap(_rng, true);
            }
            FoundMatch.isPrivate = false;

            PlayersNeeded = FoundMatch.maxPlayers;
            PlayersFound = Mathf.Max(1, _party.Members.Count);
            PingMs = 0;
            _elapsed = 0f;
            _nextFill = 0.4f;
            State = MatchmakingState.Searching;
            _party.SetState(LobbyState.Searching);
            Raise();
        }

        public void Cancel()
        {
            if (State != MatchmakingState.Searching) return;
            State = MatchmakingState.Idle;
            _party.SetState(LobbyState.Party);
            Raise();
        }

        /// <summary>Driven by GameRoot every frame.</summary>
        public void Tick(float dt)
        {
            if (State != MatchmakingState.Searching) return;
            _elapsed += dt;
            if (_elapsed < _nextFill) return;
            _nextFill = _elapsed + 0.25f + (float)_rng.NextDouble() * 0.35f;
            PlayersFound = Mathf.Min(PlayersNeeded, PlayersFound + 1 + _rng.Next(2));
            Raise();

            if (PlayersFound >= PlayersNeeded)
            {
                FoundMatch.bots = PlayersNeeded - 1;
                State = MatchmakingState.MatchFound;
                _party.SetState(LobbyState.MatchFound);
                Raise();
                if (Game_Notify != null) Game_Notify();
                if (MatchFound != null) MatchFound(FoundMatch);
            }
        }

        /// <summary>Hook for a notification toast (set by GameRoot).</summary>
        public Action Game_Notify;

        private void Raise()
        {
            if (Changed != null) Changed();
        }

        public void Reset()
        {
            State = MatchmakingState.Idle;
            Raise();
        }
    }
}
