using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vamp.Movement;
using S = Vamp.EditorTools.VampSceneBuilder;
using B = Vamp.EditorTools.VampPrototypeBuilder;

namespace Vamp.EditorTools
{
    /// <summary>
    /// Extra combat maps (built by VAMP ▸ Build All Scenes):
    ///   FOUNDRY - compact steelworks for 1V1 / 2V2 / 3V3: raised team bases, centre tower, side catwalks.
    ///   OUTPOST - desert base: bunker with a walkable roof, two watchtowers, rocks, wall-run slabs.
    ///   SKYLINE - rooftops over a street grid: roof-to-roof ramps and bridges, wall-runnable buildings.
    /// Every map is mirrored so both teams get the same layout. Team 0 spawns west (-X), team 1 east (+X).
    /// </summary>
    public static class VampMapsBuilder
    {
        public const string FoundryPath = "Assets/Scenes/Maps/Foundry.unity";
        public const string OutpostPath = "Assets/Scenes/Maps/Outpost.unity";
        public const string SkylinePath = "Assets/Scenes/Maps/Skyline.unity";
        public const string HarborPath = "Assets/Scenes/Maps/Harbor.unity";
        public const string CanyonPath = "Assets/Scenes/Maps/Canyon.unity";
        public const string PitPath = "Assets/Scenes/Maps/Pit.unity";
        public const string CrossfirePath = "Assets/Scenes/Maps/Crossfire.unity";

        private static Material _sand, _rock, _brick, _roof, _steel, _water, _container, _containerB, _containerC, _wood;

        private static void Materials()
        {
            _sand = B.Mat("Sand", new Color(0.78f, 0.67f, 0.48f), 0f, 0.1f, Color.black);
            _rock = B.Mat("Rock", new Color(0.52f, 0.45f, 0.38f), 0f, 0.15f, Color.black);
            _brick = B.Mat("Brick", new Color(0.55f, 0.32f, 0.26f), 0f, 0.2f, Color.black);
            _roof = B.Mat("Rooftop", new Color(0.46f, 0.45f, 0.47f), 0.1f, 0.25f, Color.black);
            _steel = B.Mat("Steel", new Color(0.44f, 0.46f, 0.5f), 0.8f, 0.5f, Color.black);
            _water = B.Mat("Water", new Color(0.08f, 0.22f, 0.3f), 0f, 0.92f, Color.black);
            _container = B.Mat("ContainerRed", new Color(0.55f, 0.12f, 0.1f), 0.5f, 0.35f, Color.black);
            _containerB = B.Mat("ContainerBlue", new Color(0.1f, 0.25f, 0.5f), 0.5f, 0.35f, Color.black);
            _containerC = B.Mat("ContainerGreen", new Color(0.15f, 0.38f, 0.2f), 0.5f, 0.35f, Color.black);
            _wood = B.Mat("Wood", new Color(0.42f, 0.3f, 0.2f), 0f, 0.2f, Color.black);
        }

        private static GameObject Runnable(GameObject go)
        {
            go.AddComponent<WallRunSurface>();
            return go;
        }

        private static void Finish(string mapId, string path, string sign, Vector3 signPos, Transform root)
        {
            B.Sign(root, sign, signPos, 0.9f, Vector3.back); // signs sit on the south wall: players read them looking -Z
            S.AddMatch(mapId, AssetDatabase.LoadAssetAtPath<GameObject>(B.PlayerPrefabPath));
            S.AddNavMesh();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        }

        // ================================================================== FOUNDRY (52 x 36)

        public static void BuildFoundry(S.Assets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a.Reload();
            Materials();
            VampArtBuilder.SetupDayLighting(new Vector3(55f, -40f, 0f), "golden");
            var root = new GameObject("FOUNDRY").transform;
            var world = B.Group("World", root);
            var spawns = B.Group("SpawnPoints", root);

            const float hx = 26f, hz = 18f;
            B.Block("Floor", world, new Vector3(0f, -0.5f, 0f), new Vector3(hx * 2f, 1f, hz * 2f), B._grid);
            B.Block("WallN", world, new Vector3(0f, 6f, hz), new Vector3(hx * 2f, 12f, 1f), B._concrete);
            B.Block("WallS", world, new Vector3(0f, 6f, -hz), new Vector3(hx * 2f, 12f, 1f), B._concrete);
            B.Block("WallE", world, new Vector3(hx, 6f, 0f), new Vector3(1f, 12f, hz * 2f), B._concrete);
            B.Block("WallW", world, new Vector3(-hx, 6f, 0f), new Vector3(1f, 12f, hz * 2f), B._concrete);

            foreach (float s in new[] { -1f, 1f })
            {
                // Team base: raised platform (top y = 3) with two ramps down into the middle
                B.Block("Base", world, new Vector3(s * 21.5f, 1.5f, 0f), new Vector3(8f, 3f, 30f), _steel); // full width: joins both catwalks
                B.Ramp("BaseRamp", world, new Vector3(s * 12.5f, 0f, -4.5f), new Vector3(s * 17.5f, 3f, -4.5f), 3.5f, B._concreteLight);
                B.Ramp("BaseRamp", world, new Vector3(s * 12.5f, 0f, 4.5f), new Vector3(s * 17.5f, 3f, 4.5f), 3.5f, B._concreteLight);
                B.Block("BaseCover", world, new Vector3(s * 18.2f, 3.6f, 0f), new Vector3(0.6f, 1.2f, 3f), _steel);
                // Side catwalks (y = 3) join both bases along the long walls
                B.Block("Catwalk", world, new Vector3(0f, 2.75f, s * 13.5f), new Vector3(35f, 0.5f, 3f), _steel);
                for (int i = -2; i <= 2; i++)
                    B.Block("CatwalkPost", world, new Vector3(i * 7f, 1.25f, s * 13.5f), new Vector3(0.4f, 2.5f, 0.4f), B._metal);
                B.Block("CatwalkRail", world, new Vector3(0f, 3.5f, s * 12.1f), new Vector3(35f, 1f, 0.15f), B._metal);
                // Wall-run strip above each catwalk
                Runnable(B.Block("RunWall", world, new Vector3(0f, 6.5f, s * (hz - 0.6f)), new Vector3(26f, 5f, 0.2f), B._wallRun));
                // Mid cover
                B.Block("Crate", world, new Vector3(s * 7f, 1f, -4f * s), new Vector3(2.2f, 2f, 2.2f), B._metal);
                B.Block("Crate", world, new Vector3(s * 7.5f, 0.6f, 5f * s), new Vector3(1.8f, 1.2f, 1.8f), B._metal);
                B.Block("LowWall", world, new Vector3(0f, 0.6f, s * 7.5f), new Vector3(6f, 1.2f, 0.8f), B._concrete);
            }
            // Centre tower: wall-runnable faces, top reachable by wall jumps (6 m)
            B.Block("Tower", world, new Vector3(0f, 3f, 0f), new Vector3(4f, 6f, 4f), _steel);
            for (int k = 0; k < 4; k++)
            {
                var r = Quaternion.Euler(0f, k * 90f, 0f);
                Runnable(B.Block("TowerFace", world, r * new Vector3(0f, 3f, 2.05f), new Vector3(3.6f, 5.6f, 0.1f), B._wallRun, rotation: r));
            }
            B.Block("TowerTop", world, new Vector3(0f, 6.1f, 0f), new Vector3(5f, 0.2f, 5f), B._concreteLight);
            B.Block("Glow", world, new Vector3(0f, 0.02f, 0f), new Vector3(8f, 0.04f, 8f), B._redGlow, collider: false);

            foreach (int t in new[] { 0, 1 })
            {
                float s = t == 0 ? -1f : 1f;
                float yaw = t == 0 ? 90f : 270f;
                S.Spawn(spawns, new Vector3(s * 22f, 3.05f, -4f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 22f, 3.05f, 0f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 22f, 3.05f, 4f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 15f, 3.05f, 13.5f * -s), yaw, t);
            }
            S.Spawn(spawns, new Vector3(0f, 0.05f, 10f), 180f, -1);
            S.Spawn(spawns, new Vector3(0f, 0.05f, -10f), 0f, -1);
            Finish("foundry", FoundryPath, "F O U N D R Y", new Vector3(0f, 9.5f, -hz + 0.7f), root);
        }

        // ================================================================== OUTPOST (64 x 48)

        public static void BuildOutpost(S.Assets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a.Reload();
            Materials();
            VampArtBuilder.SetupDayLighting(new Vector3(48f, 20f, 0f), "desert");
            var root = new GameObject("OUTPOST").transform;
            var world = B.Group("World", root);
            var spawns = B.Group("SpawnPoints", root);

            const float hx = 32f, hz = 24f;
            B.Block("Sand", world, new Vector3(0f, -0.5f, 0f), new Vector3(hx * 2f, 1f, hz * 2f), _sand);
            B.Block("WallN", world, new Vector3(0f, 2f, hz), new Vector3(hx * 2f, 4f, 1.5f), _rock);
            B.Block("WallS", world, new Vector3(0f, 2f, -hz), new Vector3(hx * 2f, 4f, 1.5f), _rock);
            B.Block("WallE", world, new Vector3(hx, 2f, 0f), new Vector3(1.5f, 4f, hz * 2f), _rock);
            B.Block("WallW", world, new Vector3(-hx, 2f, 0f), new Vector3(1.5f, 4f, hz * 2f), _rock);

            // Central bunker 12 x 8, door gaps on every side, walkable roof (y = 4) with two ramps
            const float bh = 3.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                B.Block("BunkerWallZ", world, new Vector3(-3.75f, bh / 2f, s * 4f), new Vector3(4.5f, bh, 0.5f), B._concrete);
                B.Block("BunkerWallZ", world, new Vector3(3.75f, bh / 2f, s * 4f), new Vector3(4.5f, bh, 0.5f), B._concrete);
                B.Block("BunkerWallX", world, new Vector3(s * 6f, bh / 2f, -2.5f), new Vector3(0.5f, bh, 3f), B._concrete);
                B.Block("BunkerWallX", world, new Vector3(s * 6f, bh / 2f, 2.5f), new Vector3(0.5f, bh, 3f), B._concrete);
                B.Ramp("RoofRamp", world, new Vector3(0f, 0f, s * 11f), new Vector3(0f, bh + 0.5f, s * 4.2f), 3f, B._concreteLight);
            }
            B.Block("BunkerRoof", world, new Vector3(0f, bh + 0.25f, 0f), new Vector3(12.5f, 0.5f, 8.5f), B._concreteLight);
            B.Block("RoofCover", world, new Vector3(0f, bh + 1.1f, 0f), new Vector3(1f, 1.2f, 4f), B._concrete);

            foreach (float s in new[] { -1f, 1f })
            {
                // Watchtower: platform (y = 6) on four legs, ramp from the sand
                float tx = s * 25f;
                for (int i = 0; i < 4; i++)
                {
                    float ox = (i % 2 == 0 ? -2f : 2f), oz = (i < 2 ? -2f : 2f);
                    B.Block("TowerLeg", world, new Vector3(tx + ox, 3f, oz), new Vector3(0.4f, 6f, 0.4f), _steel);
                }
                B.Block("TowerDeck", world, new Vector3(tx, 6f - 0.25f, 0f), new Vector3(5f, 0.5f, 5f), _steel);
                B.Block("TowerRail", world, new Vector3(tx + s * 2.4f, 6.5f, 0f), new Vector3(0.2f, 1f, 5f), _steel); // outer edge (ramp side stays open)
                B.Ramp("TowerRamp", world, new Vector3(s * 13f, 0f, 0f), new Vector3(s * 22.4f, 6f, 0f), 2.6f, B._concreteLight);
                // Wall-run slabs + cover rocks
                Runnable(B.Block("RunSlab", world, new Vector3(s * 12f, 2.5f, 15f), new Vector3(9f, 5f, 0.5f), B._wallRun));
                Runnable(B.Block("RunSlab", world, new Vector3(s * 12f, 2.5f, -15f), new Vector3(9f, 5f, 0.5f), B._wallRun));
                B.Block("Container", world, new Vector3(s * 18f, 1.3f, -10f * s), new Vector3(6f, 2.6f, 2.5f), B._metal);
                B.Block("Container", world, new Vector3(s * 7f, 1.3f, 16f * s), new Vector3(2.5f, 2.6f, 6f), B._metal);
            }
            var rng = new System.Random(7);
            for (int i = 0, placed = 0; placed < 8 && i < 200; i++)
            {
                float x = (float)(rng.NextDouble() * 20.0 + 4.0), z = (float)(rng.NextDouble() * 34.0 - 17.0);
                // keep clear of the bunker + its ramps, the tower ramps, slabs and containers
                if (x < 9f && Mathf.Abs(z) < 13f) continue;
                if (Mathf.Abs(z) < 3f) continue;
                if (Mathf.Abs(Mathf.Abs(z) - 15f) < 2f && Mathf.Abs(x - 12f) < 6f) continue;
                if (Mathf.Abs(x - 18f) < 4.5f && Mathf.Abs(z + 10f) < 3f) continue;
                if (Mathf.Abs(x - 18f) < 4.5f && Mathf.Abs(z - 10f) < 3f) continue;
                if (Mathf.Abs(x - 7f) < 3f && Mathf.Abs(Mathf.Abs(z) - 16f) < 4.5f) continue;
                placed++;
                float size = (float)(rng.NextDouble() * 2.0 + 1.5);
                float yaw = (float)(rng.NextDouble() * 90.0);
                foreach (float s in new[] { -1f, 1f }) // mirrored for fairness
                    B.Block("Rock", world, new Vector3(s * x, size * 0.4f, s * z), new Vector3(size, size * 0.9f, size * 1.2f), _rock,
                            rotation: Quaternion.Euler(0f, yaw * s, 8f));
            }

            foreach (int t in new[] { 0, 1 })
            {
                float s = t == 0 ? -1f : 1f;
                float yaw = t == 0 ? 90f : 270f;
                S.Spawn(spawns, new Vector3(s * 29f, 0.05f, -8f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 29f, 0.05f, 8f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 25f, 6.05f, 0f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 20f, 0.05f, -19f * s), yaw, t);
            }
            S.Spawn(spawns, new Vector3(0f, 0.05f, 18f), 180f, -1);
            S.Spawn(spawns, new Vector3(0f, 0.05f, -18f), 0f, -1);
            Finish("outpost", OutpostPath, "O U T P O S T", new Vector3(0f, 6f, -hz + 1f), root);
        }

        // ================================================================== SKYLINE (80 x 70)

        public static void BuildSkyline(S.Assets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a.Reload();
            Materials();
            VampArtBuilder.SetupDayLighting(new Vector3(40f, -60f, 0f), "sunset");
            var root = new GameObject("SKYLINE").transform;
            var world = B.Group("World", root);
            var spawns = B.Group("SpawnPoints", root);

            const float hx = 40f, hz = 35f;
            B.Block("Street", world, new Vector3(0f, -0.5f, 0f), new Vector3(hx * 2f, 1f, hz * 2f), B._concrete);
            B.Block("WallN", world, new Vector3(0f, 4f, hz), new Vector3(hx * 2f, 8f, 1f), _brick);
            B.Block("WallS", world, new Vector3(0f, 4f, -hz), new Vector3(hx * 2f, 8f, 1f), _brick);
            B.Block("WallE", world, new Vector3(hx, 4f, 0f), new Vector3(1f, 8f, hz * 2f), _brick);
            B.Block("WallW", world, new Vector3(-hx, 4f, 0f), new Vector3(1f, 8f, hz * 2f), _brick);

            // 4 x 3 grid of buildings (12 x 10), 4 m streets between them. Neighbouring roofs differ by at most 2 m,
            // joined by short ramps / bridges, so you can cross the whole map on the roofs.
            float[] xs = { -24f, -8f, 8f, 24f };
            float[] zs = { -14f, 0f, 14f };
            float[,] h = { { 8f, 10f, 8f }, { 10f, 12f, 10f }, { 10f, 12f, 10f }, { 8f, 10f, 8f } };
            for (int c = 0; c < 4; c++)
                for (int r = 0; r < 3; r++)
                {
                    float height = h[c, r];
                    var b = B.Block("Building", world, new Vector3(xs[c], height / 2f, zs[r]), new Vector3(12f, height, 10f), _brick);
                    Runnable(b);
                    B.Block("Roof", world, new Vector3(xs[c], height + 0.05f, zs[r]), new Vector3(12.2f, 0.1f, 10.2f), _roof);
                    B.Block("AC", world, new Vector3(xs[c] + 2.5f, height + 0.7f, zs[r] - 2f), new Vector3(2f, 1.3f, 1.6f), _steel);
                    B.Block("Parapet", world, new Vector3(xs[c] - 5.8f, height + 0.45f, zs[r] + 3f), new Vector3(0.3f, 0.8f, 3.5f), _brick);
                }
            // Roof links across the 4 m streets
            for (int c = 0; c < 4; c++)
                for (int r = 0; r < 2; r++) // north-south neighbours
                    Link(world, new Vector3(xs[c], h[c, r], zs[r] + 5f), new Vector3(xs[c], h[c, r + 1], zs[r + 1] - 5f));
            for (int c = 0; c < 3; c++)
                for (int r = 0; r < 3; r++) // east-west neighbours
                    if (r != 1 || c == 1) Link(world, new Vector3(xs[c] + 6f, h[c, r], zs[r]), new Vector3(xs[c + 1] - 6f, h[c + 1, r], zs[r]));
            // Street ramps up to the outer roofs
            foreach (float s in new[] { -1f, 1f })
            {
                B.Ramp("StreetRamp", world, new Vector3(s * 24f, 0f, 33f), new Vector3(s * 24f, 8f, 19.2f), 3f, B._concreteLight);
                B.Ramp("StreetRamp", world, new Vector3(s * 24f, 0f, -33f), new Vector3(s * 24f, 8f, -19.2f), 3f, B._concreteLight);
                B.Block("Dumpster", world, new Vector3(s * 16f, 0.7f, 7f * s), new Vector3(2.5f, 1.4f, 1.5f), B._metal);
                B.Block("Dumpster", world, new Vector3(s * 34f, 0.7f, -7f * s), new Vector3(1.5f, 1.4f, 2.5f), B._metal);
            }

            foreach (int t in new[] { 0, 1 })
            {
                float s = t == 0 ? -1f : 1f;
                float yaw = t == 0 ? 90f : 270f;
                S.Spawn(spawns, new Vector3(s * 26f, 8.15f, -14f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 26f, 10.15f, 0f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 26f, 8.15f, 14f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 36f, 0.05f, 0f), yaw, t);
            }
            S.Spawn(spawns, new Vector3(0f, 0.05f, 24f), 180f, -1);
            S.Spawn(spawns, new Vector3(0f, 0.05f, -24f), 0f, -1);
            Finish("skyline", SkylinePath, "S K Y L I N E", new Vector3(0f, 6f, -hz + 0.7f), root);
        }

        // ================================================================== HARBOR (150 x 110) - big

        public static void BuildHarbor(S.Assets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a.Reload();
            Materials();
            VampArtBuilder.SetupDayLighting(new Vector3(35f, -120f, 0f), "golden");
            var root = new GameObject("HARBOR").transform;
            var world = B.Group("World", root);
            var spawns = B.Group("SpawnPoints", root);

            const float hx = 75f, hz = 55f, dockEdge = 30f;
            // Dock (south) and shallow harbour water (north, walkable 1.5 m below the dock)
            B.Block("Dock", world, new Vector3(0f, -0.5f, (-hz + dockEdge) / 2f), new Vector3(hx * 2f, 1f, dockEdge + hz), B._concrete);
            B.Block("Water", world, new Vector3(0f, -2f, (dockEdge + hz) / 2f), new Vector3(hx * 2f, 1f, hz - dockEdge), _water);
            B.Block("DockEdge", world, new Vector3(0f, -0.75f, dockEdge + 0.25f), new Vector3(hx * 2f, 1.5f, 0.5f), B._concreteLight);
            B.Block("WallS", world, new Vector3(0f, 4f, -hz), new Vector3(hx * 2f, 8f, 1f), B._concrete);
            B.Block("WallN", world, new Vector3(0f, 3f, hz), new Vector3(hx * 2f, 10f, 1f), B._concrete);
            B.Block("WallE", world, new Vector3(hx, 4f, 0f), new Vector3(1f, 10f, hz * 2f), B._concrete);
            B.Block("WallW", world, new Vector3(-hx, 4f, 0f), new Vector3(1f, 10f, hz * 2f), B._concrete);

            // Cargo ship moored in the middle: deck y = 5, bridge tower y = 11
            B.Block("Hull", world, new Vector3(0f, 1.5f, 43f), new Vector3(64f, 7f, 14f), _steel);
            B.Block("Deck", world, new Vector3(0f, 5.05f, 43f), new Vector3(64f, 0.1f, 14f), _wood);
            Runnable(B.Block("HullSide", world, new Vector3(0f, 2.5f, 35.95f), new Vector3(60f, 4f, 0.1f), B._wallRun));
            B.Block("Bridge", world, new Vector3(0f, 8f, 46f), new Vector3(10f, 6f, 6f), B._concreteLight);
            B.Block("BridgeRoof", world, new Vector3(0f, 11.1f, 46f), new Vector3(11f, 0.2f, 7f), _steel);
            foreach (float s in new[] { -1f, 1f })
            {
                B.Ramp("Gangway", world, new Vector3(s * 16f, 0f, 22f), new Vector3(s * 16f, 5.1f, 36.5f), 2.6f, _steel);
                B.Ramp("BridgeStairs", world, new Vector3(s * 16f, 5.1f, 47f), new Vector3(s * 5.4f, 11.2f, 47f), 2.2f, _steel);
                B.Block("DeckCrate", world, new Vector3(s * 24f, 6.3f, 41f), new Vector3(6f, 2.5f, 2.5f), s < 0 ? _container : _containerB);
                B.Block("DeckCrate", world, new Vector3(s * 10f, 6f, 40f), new Vector3(2f, 2f, 2f), _wood);
                // Water ramps back up to the dock
                B.Ramp("WaterRamp", world, new Vector3(s * 50f, -1.5f, 45f), new Vector3(s * 50f, 0f, 30.3f), 4f, B._concreteLight);
            }

            // Container yard (mirrored): stacks of 1-3 with ramps onto the tall ones
            var yard = new[]
            {
                new Vector4(12f, -8f, 1f, 0f), new Vector4(12f, -2f, 2f, 0f), new Vector4(20f, 8f, 3f, 1f), new Vector4(20f, 14f, 1f, 1f),
                new Vector4(28f, -18f, 2f, 0f), new Vector4(34f, -6f, 1f, 1f), new Vector4(42f, -12f, 3f, 0f), new Vector4(42f, 18f, 2f, 1f),
                new Vector4(52f, 4f, 1f, 0f), new Vector4(28f, 24f, 1f, 0f), new Vector4(6f, -30f, 2f, 1f), new Vector4(18f, -40f, 1f, 0f),
            };
            Material[] cols = { _container, _containerB, _containerC };
            int ci = 0;
            foreach (var c in yard)
            {
                int stack = (int)c.z;
                bool alongZ = c.w > 0.5f;
                foreach (float s in new[] { -1f, 1f })
                {
                    for (int k = 0; k < stack; k++)
                    {
                        var box = B.Block("Container", world, new Vector3(s * c.x, 1.3f + k * 2.6f, s * c.y),
                                          alongZ ? new Vector3(2.5f, 2.6f, 6f) : new Vector3(6f, 2.6f, 2.5f), cols[(ci + k) % 3]);
                        if (k == stack - 1 && stack >= 2) Runnable(box);
                    }
                    if (stack >= 2)
                    {
                        float top = stack * 2.6f;
                        Vector3 foot = alongZ ? new Vector3(s * c.x, 0f, s * c.y - s * (3f + top * 1.5f)) : new Vector3(s * c.x - s * (3f + top * 1.5f), 0f, s * c.y);
                        Vector3 head = alongZ ? new Vector3(s * c.x, top, s * c.y - s * 2.9f) : new Vector3(s * c.x - s * 2.9f, top, s * c.y);
                        B.Ramp("ContainerRamp", world, foot, head, 2.2f, _steel);
                    }
                }
                ci++;
            }

            // Gantry cranes: deck y = 14 via a two-flight ramp (7 m landing)
            foreach (float s in new[] { -1f, 1f })
            {
                float cx = s * 44f, cz = 30f;
                for (int i = 0; i < 4; i++)
                {
                    float ox = (i % 2 == 0 ? -4f : 4f), oz = (i < 2 ? -3f : 3f);
                    B.Block("CraneLeg", world, new Vector3(cx + ox, 9.5f, cz + oz), new Vector3(0.8f, 19f, 0.8f), _containerC);
                }
                B.Block("CraneDeck", world, new Vector3(cx, 13.75f, cz), new Vector3(10f, 0.5f, 8f), _steel);
                B.Block("CraneBoom", world, new Vector3(cx, 19.75f, cz + 8f), new Vector3(10f, 1.5f, 24f), _containerC);
                B.Block("CraneRail", world, new Vector3(cx + s * 4.8f, 14.5f, cz), new Vector3(0.2f, 1f, 8f), _steel);
                B.Block("Landing", world, new Vector3(cx - s * 9f, 6.75f, cz - 14f), new Vector3(6f, 0.5f, 6f), _steel);
                B.Block("LandingLeg", world, new Vector3(cx - s * 9f, 3.25f, cz - 14f), new Vector3(1f, 6.5f, 1f), _steel);
                B.Ramp("CraneRamp", world, new Vector3(cx - s * 9f, 0f, cz - 30f), new Vector3(cx - s * 9f, 7f, cz - 17f), 2.6f, _steel);
                B.Ramp("CraneRamp", world, new Vector3(cx - s * 9f, 7f, cz - 13f), new Vector3(cx - s * 1f, 14f, cz - 3.9f), 2.6f, _steel);

                // Warehouse (door gaps, walkable roof y = 8)
                float wx = s * 60f, wz = -34f;
                B.Block("WarehouseBack", world, new Vector3(wx + s * 7.75f, 4f, wz), new Vector3(0.5f, 8f, 16f), _brick);
                B.Block("WarehouseFront", world, new Vector3(wx - s * 7.75f, 4f, wz - 5f), new Vector3(0.5f, 8f, 6f), _brick);
                B.Block("WarehouseFront", world, new Vector3(wx - s * 7.75f, 4f, wz + 5f), new Vector3(0.5f, 8f, 6f), _brick);
                B.Block("WarehouseSide", world, new Vector3(wx, 4f, wz - 7.75f), new Vector3(16f, 8f, 0.5f), _brick);
                Runnable(B.Block("WarehouseSide", world, new Vector3(wx, 4f, wz + 7.75f), new Vector3(16f, 8f, 0.5f), _brick));
                B.Block("WarehouseRoof", world, new Vector3(wx, 8.25f, wz), new Vector3(16.5f, 0.5f, 16.5f), _roof);
                B.Ramp("RoofRamp", world, new Vector3(wx - s * 4f, 0f, wz + 22f), new Vector3(wx - s * 4f, 8.5f, wz + 8.3f), 3f, B._concreteLight);
                B.Block("Crates", world, new Vector3(wx + s * 3f, 1f, wz - 3f), new Vector3(3f, 2f, 3f), _wood);
                B.Block("Crates", world, new Vector3(wx - s * 2f, 0.75f, wz + 4f), new Vector3(2f, 1.5f, 2f), _wood);
                // Bollards and low cover along the quay
                for (int i = 0; i < 4; i++)
                    B.Block("Bollard", world, new Vector3(s * (8f + i * 14f), 0.5f, dockEdge - 1.5f), new Vector3(0.8f, 1f, 0.8f), _steel);
                B.Block("Barrier", world, new Vector3(s * 32f, 0.6f, -30f), new Vector3(8f, 1.2f, 0.8f), B._concreteLight);
                B.Block("Barrier", world, new Vector3(s * 8f, 0.6f, 10f), new Vector3(0.8f, 1.2f, 6f), B._concreteLight);
            }
            B.Block("Glow", world, new Vector3(0f, 0.02f, 0f), new Vector3(10f, 0.04f, 10f), B._redGlow, collider: false);

            foreach (int t in new[] { 0, 1 })
            {
                float s = t == 0 ? -1f : 1f;
                float yaw = t == 0 ? 90f : 270f;
                S.Spawn(spawns, new Vector3(s * 68f, 0.05f, -10f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 68f, 0.05f, 5f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 60f, 0.05f, -34f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 66f, 0.05f, 20f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 60f, 8.55f, -34f), yaw, t);
            }
            S.Spawn(spawns, new Vector3(0f, 5.15f, 42f), 180f, -1);
            S.Spawn(spawns, new Vector3(0f, 0.05f, -20f), 0f, -1);
            S.Spawn(spawns, new Vector3(-30f, 0.05f, 10f), 90f, -1);
            S.Spawn(spawns, new Vector3(30f, 0.05f, 10f), 270f, -1);
            S.Spawn(spawns, new Vector3(0f, 0.05f, -45f), 0f, -1);
            S.Spawn(spawns, new Vector3(-25f, 0.05f, -45f), 45f, -1);
            S.Spawn(spawns, new Vector3(25f, 0.05f, -45f), -45f, -1);
            Finish("harbor", HarborPath, "H A R B O R", new Vector3(0f, 6f, -hz + 0.7f), root);
        }

        // ================================================================== CANYON (220 x 80) - big

        public static void BuildCanyon(S.Assets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a.Reload();
            Materials();
            VampArtBuilder.SetupDayLighting(new Vector3(52f, 150f, 0f), "desert");
            var root = new GameObject("CANYON").transform;
            var world = B.Group("World", root);
            var spawns = B.Group("SpawnPoints", root);

            const float hx = 110f, hz = 40f, ledgeY = 8f;
            B.Block("Sand", world, new Vector3(0f, -0.5f, 0f), new Vector3(hx * 2f, 1f, hz * 2f), _sand);
            B.Block("CliffN", world, new Vector3(0f, 10f, hz + 2f), new Vector3(hx * 2f + 8f, 22f, 4f), _rock);
            B.Block("CliffS", world, new Vector3(0f, 10f, -hz - 2f), new Vector3(hx * 2f + 8f, 22f, 4f), _rock);
            B.Block("CliffE", world, new Vector3(hx + 2f, 10f, 0f), new Vector3(4f, 22f, hz * 2f), _rock);
            B.Block("CliffW", world, new Vector3(-hx - 2f, 10f, 0f), new Vector3(4f, 22f, hz * 2f), _rock);

            foreach (float s in new[] { -1f, 1f })
            {
                // Ledges along both cliffs (y = 8), with wall-run rock above them
                B.Block("Ledge", world, new Vector3(0f, ledgeY / 2f, s * (hz - 3f)), new Vector3(hx * 1.6f, ledgeY, 6f), _rock);
                Runnable(B.Block("CliffRun", world, new Vector3(0f, ledgeY + 5f, s * (hz - 0.1f)), new Vector3(hx * 1.4f, 8f, 0.2f), B._wallRun));
                foreach (float x in new[] { -70f, -20f, 20f, 70f })
                    B.Ramp("LedgeRamp", world, new Vector3(x, 0f, s * (hz - 20f)), new Vector3(x, ledgeY, s * (hz - 6.1f)), 3f, B._concreteLight);
            }
            // Rope bridges between the ledges (y = 8)
            foreach (float x in new[] { -45f, 45f })
            {
                B.Block("Bridge", world, new Vector3(x, ledgeY - 0.15f, 0f), new Vector3(3f, 0.3f, hz * 2f - 12f), _wood);
                B.Block("BridgeRail", world, new Vector3(x - 1.4f, ledgeY + 0.5f, 0f), new Vector3(0.1f, 1f, hz * 2f - 12f), _wood);
                B.Block("BridgeRail", world, new Vector3(x + 1.4f, ledgeY + 0.5f, 0f), new Vector3(0.1f, 1f, hz * 2f - 12f), _wood);
            }

            // Ruined temple in the middle: platform y = 3, pillars, an upper altar y = 7
            B.Block("Temple", world, new Vector3(0f, 1.5f, 0f), new Vector3(22f, 3f, 22f), B._concreteLight);
            B.Block("Altar", world, new Vector3(0f, 5f, 0f), new Vector3(6f, 4f, 6f), B._concrete);
            foreach (float s in new[] { -1f, 1f })
            {
                B.Ramp("TempleSteps", world, new Vector3(s * 19f, 0f, 0f), new Vector3(s * 11.1f, 3f, 0f), 6f, B._concreteLight);
                B.Ramp("AltarSteps", world, new Vector3(s * 9.5f, 3f, 5f * s), new Vector3(s * 3.1f, 7f, 5f * s), 2f, B._concrete);
                for (int i = -1; i <= 1; i += 2)
                {
                    Runnable(B.Block("Pillar", world, new Vector3(s * 9f, 7f, i * 9f), new Vector3(2f, 8f, 2f), _rock));
                    B.Block("PillarBroken", world, new Vector3(s * 4f, 4f, i * 9.5f), new Vector3(1.6f, 2f, 1.6f), _rock);
                }
            }

            // Mesas and rock cover along the canyon floor (mirrored)
            var mesas = new[]
            {
                new Vector4(32f, -14f, 6f, 10f), new Vector4(34f, 16f, 4f, 7f), new Vector4(60f, 0f, 8f, 12f),
                new Vector4(84f, -16f, 5f, 8f), new Vector4(82f, 20f, 3f, 6f),
            };
            foreach (var m in mesas)
                foreach (float s in new[] { -1f, 1f })
                {
                    float x = s * m.x, z = s * m.y, height = m.z, size = m.w;
                    B.Block("Mesa", world, new Vector3(x, height / 2f, z), new Vector3(size, height, size), _rock);
                    Runnable(B.Block("MesaFace", world, new Vector3(x, height / 2f, z - size / 2f - 0.05f), new Vector3(size * 0.8f, height * 0.9f, 0.1f), B._wallRun));
                    float run = height * 1.6f + 1f;
                    B.Ramp("MesaRamp", world, new Vector3(x - s * (size / 2f + run), 0f, z), new Vector3(x - s * (size / 2f - 0.1f), height, z), 2.4f, B._concreteLight);
                }
            var rng = new System.Random(11);
            for (int i = 0, placed = 0; placed < 14 && i < 400; i++)
            {
                float x = (float)(rng.NextDouble() * 90.0 + 12.0), z = (float)(rng.NextDouble() * 50.0 - 25.0);
                bool clear = true;
                foreach (var m in mesas)
                    if (Mathf.Abs(x - m.x) < m.w / 2f + m.z * 1.6f + 4f && Mathf.Abs(z - m.y) < m.w / 2f + 3f) clear = false;
                if (Mathf.Abs(Mathf.Abs(x) - 45f) < 3f) clear = false;
                if (Mathf.Abs(z) > 24f || (x < 22f && Mathf.Abs(z) < 12f) || x > 96f) clear = false;
                foreach (float rx in new[] { 20f, 70f }) if (Mathf.Abs(x - rx) < 3f && Mathf.Abs(z) > 18f) clear = false;
                if (!clear) continue;
                placed++;
                float size = (float)(rng.NextDouble() * 2.5 + 1.5);
                float yaw = (float)(rng.NextDouble() * 90.0);
                foreach (float s in new[] { -1f, 1f })
                    B.Block("Rock", world, new Vector3(s * x, size * 0.4f, s * z), new Vector3(size, size * 0.9f, size * 1.2f), _rock,
                            rotation: Quaternion.Euler(0f, yaw * s, 6f));
            }

            // Team camps at the ends: walls with gaps, a lookout (y = 5)
            foreach (float s in new[] { -1f, 1f })
            {
                float bx = s * 100f;
                B.Block("CampWall", world, new Vector3(bx - s * 6f, 1.25f, -9f), new Vector3(0.6f, 2.5f, 10f), _wood);
                B.Block("CampWall", world, new Vector3(bx - s * 6f, 1.25f, 9f), new Vector3(0.6f, 2.5f, 10f), _wood);
                B.Block("Lookout", world, new Vector3(bx + s * 2f, 4.75f, 0f), new Vector3(6f, 0.5f, 6f), _wood);
                foreach (float o in new[] { -2.5f, 2.5f })
                    foreach (float p in new[] { -2.5f, 2.5f })
                        B.Block("LookoutLeg", world, new Vector3(bx + s * 2f + o, 2.25f, p), new Vector3(0.4f, 4.5f, 0.4f), _wood);
                B.Ramp("LookoutRamp", world, new Vector3(bx - s * 5f, 0f, -8f), new Vector3(bx - s * 1.2f, 5f, -1f), 2f, _wood);
                B.Block("Tent", world, new Vector3(bx, 1.2f, 14f), new Vector3(5f, 2.4f, 4f), B._concreteLight);
                B.Block("Tent", world, new Vector3(bx, 1.2f, -14f), new Vector3(5f, 2.4f, 4f), B._concreteLight);
            }
            B.Block("Glow", world, new Vector3(0f, 3.02f, 0f), new Vector3(10f, 0.04f, 10f), B._redGlow, collider: false);

            foreach (int t in new[] { 0, 1 })
            {
                float s = t == 0 ? -1f : 1f;
                float yaw = t == 0 ? 90f : 270f;
                S.Spawn(spawns, new Vector3(s * 102f, 0.05f, -5f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 102f, 0.05f, 5f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 96f, 0.05f, 0f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 102f, 5.05f, 0f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 85f, ledgeY + 0.05f, 37f * -s), yaw, t);
            }
            foreach (float x in new[] { -60f, -30f, 0f, 30f, 60f })
            {
                S.Spawn(spawns, new Vector3(x, 0.05f, 28f), 180f, -1);
                S.Spawn(spawns, new Vector3(x, 0.05f, -28f), 0f, -1);
            }
            Finish("canyon", CanyonPath, "C A N Y O N", new Vector3(0f, 16f, -hz + 0.2f), root);
        }

        // ================================================================== THE PIT (28 x 28) - small

        public static void BuildPit(S.Assets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a.Reload();
            Materials();
            VampArtBuilder.SetupDayLighting(new Vector3(60f, 30f, 0f), "dusk");
            var root = new GameObject("PIT").transform;
            var world = B.Group("World", root);
            var spawns = B.Group("SpawnPoints", root);

            const float hx = 14f, hz = 14f, ring = 2f;
            B.Block("PitFloor", world, new Vector3(0f, -0.5f, 0f), new Vector3(hx * 2f, 1f, hz * 2f), B._grid);
            B.Block("WallN", world, new Vector3(0f, 5f, hz), new Vector3(hx * 2f, 10f, 1f), B._concrete);
            B.Block("WallS", world, new Vector3(0f, 5f, -hz), new Vector3(hx * 2f, 10f, 1f), B._concrete);
            B.Block("WallE", world, new Vector3(hx, 5f, 0f), new Vector3(1f, 10f, hz * 2f), B._concrete);
            B.Block("WallW", world, new Vector3(-hx, 5f, 0f), new Vector3(1f, 10f, hz * 2f), B._concrete);
            // Raised ring (y = 2) around a sunken centre, ramps down on every side
            foreach (float s in new[] { -1f, 1f })
            {
                B.Block("RingX", world, new Vector3(s * (hx - 2.5f), ring / 2f, 0f), new Vector3(5f, ring, hz * 2f), _steel);
                B.Block("RingZ", world, new Vector3(0f, ring / 2f, s * (hz - 2.5f)), new Vector3(hx * 2f - 10f, ring, 5f), _steel);
                B.Ramp("RingRampX", world, new Vector3(s * 5.5f, 0f, 0f), new Vector3(s * 9.1f, ring, 0f), 3f, B._concreteLight);
                B.Ramp("RingRampZ", world, new Vector3(0f, 0f, s * 5.5f), new Vector3(0f, ring, s * 9.1f), 3f, B._concreteLight);
                Runnable(B.Block("RunWall", world, new Vector3(0f, 6f, s * (hz - 0.6f)), new Vector3(14f, 5f, 0.2f), B._wallRun));
                Runnable(B.Block("RunWall", world, new Vector3(s * (hx - 0.6f), 6f, 0f), new Vector3(0.2f, 5f, 14f), B._wallRun));
            }
            // Corner towers (y = 5) and four pillars around the centre
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                B.Block("CornerTower", world, new Vector3(sx * (hx - 2.5f), 3.5f, sz * (hz - 2.5f)), new Vector3(5f, 3f, 5f), _steel);
                B.Ramp("TowerRamp", world, new Vector3(sx * (hx - 2.5f), ring, sz * (hz - 11f)), new Vector3(sx * (hx - 2.5f), 5f, sz * (hz - 5.1f)), 2.4f, _steel);
                Runnable(B.Block("Pillar", world, new Vector3(sx * 3.2f, 2.5f, sz * 3.2f), new Vector3(1.4f, 5f, 1.4f), B._concrete));
            }
            B.Block("CentreCover", world, new Vector3(0f, 0.6f, 0f), new Vector3(2.5f, 1.2f, 2.5f), B._metal);
            B.Block("Glow", world, new Vector3(0f, 0.02f, 0f), new Vector3(9f, 0.04f, 9f), B._redGlow, collider: false);

            foreach (int t in new[] { 0, 1 })
            {
                float s = t == 0 ? -1f : 1f;
                float yaw = t == 0 ? 90f : 270f;
                S.Spawn(spawns, new Vector3(s * (hx - 2.5f), ring + 0.05f, -6f), yaw, t);
                S.Spawn(spawns, new Vector3(s * (hx - 2.5f), ring + 0.05f, 6f), yaw, t);
                S.Spawn(spawns, new Vector3(s * (hx - 2.5f), 5.05f, -s * (hz - 2.5f)), yaw, t);
            }
            S.Spawn(spawns, new Vector3(0f, ring + 0.05f, hz - 2.5f), 180f, -1);
            S.Spawn(spawns, new Vector3(0f, ring + 0.05f, -hz + 2.5f), 0f, -1);
            Finish("pit", PitPath, "T H E   P I T", new Vector3(0f, 8.5f, -hz + 0.7f), root);
        }

        // ================================================================== CROSSFIRE (36 x 20) - small

        public static void BuildCrossfire(S.Assets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a.Reload();
            Materials();
            VampArtBuilder.SetupDayLighting(new Vector3(45f, 70f, 0f), "day");
            var root = new GameObject("CROSSFIRE").transform;
            var world = B.Group("World", root);
            var spawns = B.Group("SpawnPoints", root);

            const float hx = 18f, hz = 10f;
            B.Block("Floor", world, new Vector3(0f, -0.5f, 0f), new Vector3(hx * 2f, 1f, hz * 2f), B._grid);
            B.Block("WallN", world, new Vector3(0f, 4f, hz), new Vector3(hx * 2f, 8f, 1f), _brick);
            B.Block("WallS", world, new Vector3(0f, 4f, -hz), new Vector3(hx * 2f, 8f, 1f), _brick);
            B.Block("WallE", world, new Vector3(hx, 4f, 0f), new Vector3(1f, 8f, hz * 2f), _brick);
            B.Block("WallW", world, new Vector3(-hx, 4f, 0f), new Vector3(1f, 8f, hz * 2f), _brick);

            // Central divider between the two lanes: window gaps and one open crossing in the middle
            foreach (float s in new[] { -1f, 1f })
            {
                B.Block("DividerLow", world, new Vector3(s * 7.5f, 0.5f, 0f), new Vector3(9f, 1f, 0.6f), B._concrete);
                B.Block("DividerEnd", world, new Vector3(s * 14f, 1.75f, 0f), new Vector3(2f, 3.5f, 0.6f), B._concrete);
            }
            // Posts + lintel above the low wall = two window gaps per side
            foreach (float s in new[] { -1f, 1f })
            {
                B.Block("DividerPost", world, new Vector3(s * 4f, 1.75f, 0f), new Vector3(2f, 3.5f, 0.6f), B._concrete);
                B.Block("DividerPost", world, new Vector3(s * 8f, 1.75f, 0f), new Vector3(2f, 3.5f, 0.6f), B._concrete);
                B.Block("DividerPost", world, new Vector3(s * 11.5f, 1.75f, 0f), new Vector3(1f, 3.5f, 0.6f), B._concrete);
                B.Block("DividerTop", world, new Vector3(s * 8f, 3f, 0f), new Vector3(9f, 1f, 0.6f), B._concrete);
                Runnable(B.Block("RunWall", world, new Vector3(s * 8f, 4.5f, hz - 0.6f), new Vector3(10f, 5f, 0.2f), B._wallRun));
                // End bases: raised platform (y = 2) with a ramp into each lane
                B.Block("Base", world, new Vector3(s * 16f, 1f, 0f), new Vector3(3.5f, 2f, hz * 2f - 1f), _steel);
                B.Ramp("BaseRamp", world, new Vector3(s * 9.8f, 0f, -6f), new Vector3(s * 14.2f, 2f, -6f), 2.5f, B._concreteLight);
                B.Ramp("BaseRamp", world, new Vector3(s * 9.8f, 0f, 6f), new Vector3(s * 14.2f, 2f, 6f), 2.5f, B._concreteLight);
                // Lane cover
                B.Block("Crate", world, new Vector3(s * 6f, 0.6f, 5f * s), new Vector3(1.4f, 1.2f, 1.4f), _wood);
                B.Block("Crate", world, new Vector3(s * 3f, 0.9f, -6f * s), new Vector3(1.8f, 1.8f, 1.8f), _wood);
                B.Block("Barrel", world, new Vector3(s * 10f, 0.6f, -3f * s), new Vector3(1f, 1.2f, 1f), _containerB);
            }
            // Catwalk along the north wall (y = 3) with ramps from the bases
            B.Block("Catwalk", world, new Vector3(0f, 2.9f, hz - 1.6f), new Vector3(20f, 0.3f, 2.2f), _steel);
            B.Block("CatwalkRail", world, new Vector3(0f, 3.5f, hz - 2.65f), new Vector3(20f, 1f, 0.1f), _steel);
            foreach (float s in new[] { -1f, 1f })
                B.Ramp("CatwalkRamp", world, new Vector3(s * 14.2f, 2f, hz - 1.6f), new Vector3(s * 10f, 3.05f, hz - 1.6f), 2.2f, _steel);
            B.Block("Glow", world, new Vector3(0f, 0.02f, 0f), new Vector3(4f, 0.04f, 4f), B._redGlow, collider: false);

            foreach (int t in new[] { 0, 1 })
            {
                float s = t == 0 ? -1f : 1f;
                float yaw = t == 0 ? 90f : 270f;
                S.Spawn(spawns, new Vector3(s * 16f, 2.05f, -5f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 16f, 2.05f, 0f), yaw, t);
                S.Spawn(spawns, new Vector3(s * 16f, 2.05f, 5f), yaw, t);
            }
            S.Spawn(spawns, new Vector3(0f, 0.05f, 6f), 180f, -1);
            S.Spawn(spawns, new Vector3(0f, 0.05f, -6f), 0f, -1);
            Finish("crossfire", CrossfirePath, "C R O S S F I R E", new Vector3(0f, 6.5f, -hz + 0.7f), root);
        }

        /// <summary>Flat bridge or gentle ramp between two roof edges (4 m apart).</summary>
        private static void Link(Transform parent, Vector3 from, Vector3 to)
        {
            if (Mathf.Abs(from.y - to.y) < 0.01f)
            {
                Vector3 mid = (from + to) * 0.5f;
                Vector3 d = to - from;
                bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
                B.Block("Bridge", parent, new Vector3(mid.x, from.y - 0.1f, mid.z),
                        alongX ? new Vector3(d.magnitude + 1f, 0.2f, 2f) : new Vector3(2f, 0.2f, d.magnitude + 1f), _steel);
            }
            else
            {
                Vector3 lo = from.y < to.y ? from : to, hi = from.y < to.y ? to : from;
                B.Ramp("RoofRamp", parent, lo, hi, 2f, _steel);
            }
        }
    }
}
