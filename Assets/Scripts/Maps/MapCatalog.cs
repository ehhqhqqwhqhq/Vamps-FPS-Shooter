using System.Collections.Generic;

namespace Vamp.Maps
{
    public sealed class MapInfo
    {
        public string Id;
        public string DisplayName;
        public string SceneName;
        public string Description;
        public bool SupportsRace;
        /// <summary>Small map suited to 1V1 / 2V2 / 3V3.</summary>
        public bool Arena;
        /// <summary>Real combat map (not the movement lab).</summary>
        public bool Battle = true;
    }

    /// <summary>Playable maps. Adding a map = add its scene to Build Settings + one entry here.</summary>
    public static class MapCatalog
    {
        public const string BootScene = "Boot";
        public const string MainMenuScene = "MainMenu";

        public static readonly List<MapInfo> Maps = new List<MapInfo>
        {
            new MapInfo { Id = "vertex", DisplayName = "VERTEX", SceneName = "Vertex",
                          Description = "ABANDONED INDUSTRIAL FACILITY · VERTICAL ARENA", SupportsRace = true },
            new MapInfo { Id = "foundry", DisplayName = "FOUNDRY", SceneName = "Foundry", Arena = true,
                          Description = "COMPACT STEELWORKS · CENTRE TOWER, SIDE CATWALKS · BUILT FOR 1V1-3V3" },
            new MapInfo { Id = "outpost", DisplayName = "OUTPOST", SceneName = "Outpost", Arena = true,
                          Description = "DESERT BASE · ROCKS, BUNKER ROOF, TWO WATCHTOWERS" },
            new MapInfo { Id = "skyline", DisplayName = "SKYLINE", SceneName = "Skyline",
                          Description = "ROOFTOPS · BRIDGES, GAPS AND WALL-RUN ALLEYS" },
            new MapInfo { Id = "movement_lab", DisplayName = "MOVEMENT LAB", SceneName = "MovementTest", Battle = false,
                          Description = "TRAINING FACILITY · EVERY MOVEMENT MECHANIC", SupportsRace = true },
        };

        /// <summary>Random combat map for quick play (arena modes prefer small maps).</summary>
        public static string RandomBattleMap(System.Random rng, bool arena)
        {
            var pool = new List<MapInfo>();
            foreach (var m in Maps) if (m.Battle && (!arena || m.Arena)) pool.Add(m);
            if (pool.Count == 0) return Maps[0].Id;
            return pool[rng.Next(pool.Count)].Id;
        }

        public static MapInfo Get(string id)
        {
            foreach (var m in Maps) if (m.Id == id) return m;
            return Maps[0];
        }
    }
}
