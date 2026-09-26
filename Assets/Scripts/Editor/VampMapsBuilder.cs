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

        private static Material _sand, _rock, _brick, _roof, _steel;

        private static void Materials()
        {
            _sand = B.Mat("Sand", new Color(0.78f, 0.67f, 0.48f), 0f, 0.1f, Color.black);
            _rock = B.Mat("Rock", new Color(0.52f, 0.45f, 0.38f), 0f, 0.15f, Color.black);
            _brick = B.Mat("Brick", new Color(0.55f, 0.32f, 0.26f), 0f, 0.2f, Color.black);
            _roof = B.Mat("Rooftop", new Color(0.36f, 0.36f, 0.38f), 0.1f, 0.25f, Color.black);
            _steel = B.Mat("Steel", new Color(0.3f, 0.32f, 0.35f), 0.8f, 0.5f, Color.black);
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
            VampArtBuilder.SetupDayLighting(new Vector3(55f, -40f, 0f));
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
            VampArtBuilder.SetupDayLighting(new Vector3(48f, 20f, 0f));
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
            VampArtBuilder.SetupDayLighting(new Vector3(40f, -60f, 0f));
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
