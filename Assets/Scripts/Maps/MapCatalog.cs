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
            new MapInfo { Id = "movement_lab", DisplayName = "MOVEMENT LAB", SceneName = "MovementTest",
                          Description = "TRAINING FACILITY · EVERY MOVEMENT MECHANIC", SupportsRace = true },
        };

        public static MapInfo Get(string id)
        {
            foreach (var m in Maps) if (m.Id == id) return m;
            return Maps[0];
        }
    }
}
