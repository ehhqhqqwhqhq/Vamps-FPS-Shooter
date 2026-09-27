using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vamp.Accounts;
using Vamp.Core;
using Vamp.Maps;
using Vamp.Match;
using Vamp.Movement;
using Vamp.Progression;
using Vamp.UI.Menus;
using Vamp.Weapons;
using B = Vamp.EditorTools.VampPrototypeBuilder;

namespace Vamp.EditorTools
{
    /// <summary>
    /// VAMP ▸ Build All Scenes — generates the whole playable game:
    ///   Boot (initialises global systems) → MainMenu (animated 3D menu) → matches in VERTEX or the MOVEMENT LAB.
    /// Creates (only if missing) all 9 weapons, the weapon catalog (Resources), progression + account rule configs,
    /// materials and the Player prefab. Existing tuned assets are never overwritten.
    /// </summary>
    public static class VampSceneBuilder
    {
        public const string BootPath = "Assets/Scenes/Boot/Boot.unity";
        public const string MenuPath = "Assets/Scenes/MainMenu/MainMenu.unity";
        public const string VertexPath = "Assets/Scenes/Maps/Vertex.unity";
        public const string ResourcesFolder = "Assets/Resources";

        public sealed class Assets
        {
            public MovementSettings Movement;
            public WeaponCatalog Catalog;
            public WeaponData[] LabLoadout;

            /// <summary>
            /// Opening a new scene unloads in-memory assets that nothing in the scene references, which turns
            /// references held across scene builds into null (the Player prefab lost its MovementSettings + loadout).
            /// Call after every NewScene to re-acquire them from disk.
            /// </summary>
            public void Reload()
            {
                Movement = B.LoadOrCreateMovementSettings();
                Catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(ResourcesFolder + "/VampWeaponCatalog.asset");
                LabLoadout = new[]
                {
                    AssetDatabase.LoadAssetAtPath<WeaponData>(B.WeaponFolder + "/Brute_Shotgun.asset"),
                    AssetDatabase.LoadAssetAtPath<WeaponData>(B.WeaponFolder + "/V9_Pistol.asset"),
                    AssetDatabase.LoadAssetAtPath<WeaponData>(B.WeaponFolder + "/Blast_RocketLauncher.asset"),
                };
                B.CreateMaterials();
            }
        }

        [MenuItem("VAMP/Build All Scenes", priority = 0)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorUtility.DisplayProgressBar("VAMP", "Preparing assets...", 0.05f);
                var assets = PrepareAssets();
                EditorUtility.DisplayProgressBar("VAMP", "Building Movement Lab...", 0.2f);
                BuildLab(assets);
                EditorUtility.DisplayProgressBar("VAMP", "Building VERTEX...", 0.5f);
                BuildVertex(assets);
                EditorUtility.DisplayProgressBar("VAMP", "Building FOUNDRY, OUTPOST, SKYLINE...", 0.7f);
                VampMapsBuilder.BuildFoundry(assets);
                VampMapsBuilder.BuildOutpost(assets);
                VampMapsBuilder.BuildSkyline(assets);
                EditorUtility.DisplayProgressBar("VAMP", "Building HARBOR, CANYON, THE PIT, CROSSFIRE...", 0.75f);
                VampMapsBuilder.BuildHarbor(assets);
                VampMapsBuilder.BuildCanyon(assets);
                VampMapsBuilder.BuildPit(assets);
                VampMapsBuilder.BuildCrossfire(assets);
                EditorUtility.DisplayProgressBar("VAMP", "Building menus...", 0.8f);
                BuildMenuScene();
                BuildBootScene();
                ApplyBuildSettings();
                EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Single);
                Debug.Log("[VAMP] All scenes built. Boot scene is open - press Play to run the full game.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ================================================================== Assets

        public static Assets PrepareAssets()
        {
            // Open a fresh scene FIRST: opening scenes unloads unreferenced in-memory assets.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            B.EnsureFolders();
            foreach (var f in new[] { ResourcesFolder, "Assets/Scenes/Boot", "Assets/Scenes/MainMenu", "Assets/Scenes/Maps" }) B.EnsureFolder(f);
            EditorUtility.DisplayProgressBar("VAMP", "Importing weapon + character models...", 0.08f);
            VampArtBuilder.ImportModels(); // first: may reimport FBX files
            VampGraphicsBuilder.SetupRenderer();
            B.CreateMaterials();
            B._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var a = new Assets { Movement = B.LoadOrCreateMovementSettings() };
            var brute = B.LoadOrCreateWeapon("Brute_Shotgun", B.ConfigureBrute);
            var v9 = B.LoadOrCreateWeapon("V9_Pistol", B.ConfigureV9);
            var blast = B.LoadOrCreateWeapon("Blast_RocketLauncher", B.ConfigureBlast);
            var ripper = B.LoadOrCreateWeapon("Ripper_SMG", ConfigureRipper);
            var havoc = B.LoadOrCreateWeapon("Havoc_AR", ConfigureHavoc);
            var widow = B.LoadOrCreateWeapon("Widow_Sniper", ConfigureWidow);
            var arc = B.LoadOrCreateWeapon("Arc_Energy", ConfigureArc);
            var reaper = B.LoadOrCreateWeapon("Reaper", ConfigureReaper);
            var blade = B.LoadOrCreateWeapon("Blade_Melee", ConfigureBlade);
            var knife = B.LoadOrCreateWeapon("Knife_Melee", ConfigureKnife);
            var balisong = B.LoadOrCreateWeapon("Balisong_Melee", ConfigureBalisong);
            ArcHeat(arc);
            foreach (var w in new[] { brute, v9, blast, ripper, havoc, widow, arc, reaper, blade, knife, balisong }) RealName(w);

            string catPath = ResourcesFolder + "/VampWeaponCatalog.asset";
            var cat = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(catPath);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<WeaponCatalog>();
                AssetDatabase.CreateAsset(cat, catPath);
            }
            cat.weapons = new List<WeaponData> { brute, ripper, havoc, widow, blast, arc, v9, reaper, blade, knife, balisong }; // append only: online uses the index
            VampArtBuilder.AssignWeaponModels(cat.weapons);
            cat.gunGameOrder = new List<WeaponData> { havoc, ripper, arc, brute, widow, v9, reaper, blast, blade };
            EditorUtility.SetDirty(cat);
            a.Catalog = cat;
            a.LabLoadout = new[] { brute, v9, blast };

            if (AssetDatabase.LoadAssetAtPath<ProgressionConfig>(ResourcesFolder + "/VampProgressionConfig.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ProgressionConfig>(), ResourcesFolder + "/VampProgressionConfig.asset");
            if (AssetDatabase.LoadAssetAtPath<AccountRules>(ResourcesFolder + "/VampAccountRules.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AccountRules>(), ResourcesFolder + "/VampAccountRules.asset");

            AssetDatabase.SaveAssets();
            return a;
        }

        /// <summary>Weapons are named after what they are (ids stay the same - saves, loadouts and online use them).</summary>
        private static readonly Dictionary<string, string> RealNames = new Dictionary<string, string>
        {
            { "havoc", "AK-47" }, { "ripper", "VECTOR" }, { "arc", "SCAR" }, { "widow", "AWP" }, { "brute", "PUMP SHOTGUN" },
            { "v9", "USP" }, { "reaper", "M1911" }, { "blast", "ROCKET LAUNCHER" }, { "blade", "MACHETE" },
            { "knife", "COMBAT KNIFE" }, { "balisong", "BUTTERFLY" },
        };

        private static void RealName(WeaponData w)
        {
            string n;
            if (w == null || !RealNames.TryGetValue(w.id, out n) || w.displayName == n) return;
            w.displayName = n;
            EditorUtility.SetDirty(w);
        }

        private static void ConfigureRipper(WeaponData w)
        {
            w.id = "ripper"; w.displayName = "RIPPER"; w.slot = WeaponSlot.Primary;
            w.fireMode = FireMode.FullAuto; w.delivery = DeliveryType.Hitscan;
            w.damage = 17f; w.headshotMultiplier = 1.5f; w.range = 120f;
            w.falloffStart = 8f; w.falloffEnd = 25f; w.minDamageMultiplier = 0.55f;
            w.fireRate = 900f; w.magazineSize = 30; w.reserveAmmo = 150; w.reloadTime = 1.5f; w.equipTime = 0.25f;
            w.hipSpread = 1.6f; w.adsSpread = 0.9f; w.movingSpreadAdd = 0.4f; w.airborneSpreadAdd = 1f;
            w.recoilPitch = 0.35f; w.recoilYawRandom = 0.25f; w.viewKick = 0.5f; w.screenShake = 0.03f;
            w.adsFovMultiplier = 0.85f; w.adsTime = 0.12f;
            w.tracerColor = new Color(1f, 0.8f, 0.7f, 0.5f);
        }

        private static void ConfigureHavoc(WeaponData w)
        {
            w.id = "havoc"; w.displayName = "HAVOC"; w.slot = WeaponSlot.Primary;
            w.fireMode = FireMode.FullAuto; w.delivery = DeliveryType.Hitscan;
            w.damage = 22f; w.headshotMultiplier = 1.6f; w.range = 180f;
            w.falloffStart = 25f; w.falloffEnd = 60f; w.minDamageMultiplier = 0.7f;
            w.fireRate = 600f; w.magazineSize = 28; w.reserveAmmo = 140; w.reloadTime = 1.8f; w.equipTime = 0.35f;
            w.hipSpread = 1.2f; w.adsSpread = 0.2f; w.movingSpreadAdd = 0.8f; w.airborneSpreadAdd = 1.5f;
            w.recoilPitch = 0.55f; w.recoilYawRandom = 0.2f; w.viewKick = 0.7f; w.screenShake = 0.04f;
            w.adsFovMultiplier = 0.75f; w.adsTime = 0.16f;
            w.tracerColor = new Color(1f, 0.85f, 0.8f, 0.55f);
        }

        private static void ConfigureWidow(WeaponData w)
        {
            w.id = "widow"; w.displayName = "WIDOW"; w.slot = WeaponSlot.Primary;
            w.fireMode = FireMode.SemiAuto; w.delivery = DeliveryType.Hitscan;
            w.damage = 95f; w.headshotMultiplier = 2f; w.range = 320f;
            w.falloffStart = 300f; w.falloffEnd = 320f; w.minDamageMultiplier = 1f;
            w.fireRate = 45f; w.magazineSize = 5; w.reserveAmmo = 30; w.reloadTime = 2.4f; w.equipTime = 0.5f;
            w.hipSpread = 6f; w.adsSpread = 0f; w.movingSpreadAdd = 2f; w.airborneSpreadAdd = 4f;
            w.recoilPitch = 4f; w.recoilYawRandom = 0.6f; w.viewKick = 5f; w.screenShake = 0.2f;
            w.isSniper = true; w.scopeOverlay = true; w.adsFovMultiplier = 0.3f; w.adsTime = 0.22f;
            w.tracerColor = new Color(1f, 0.2f, 0.25f, 0.9f);
        }

        private static void ConfigureArc(WeaponData w)
        {
            w.id = "arc"; w.displayName = "ARC"; w.slot = WeaponSlot.Primary;
            w.fireMode = FireMode.FullAuto; w.delivery = DeliveryType.Hitscan;
            w.damage = 9f; w.headshotMultiplier = 1.4f; w.range = 60f;
            w.falloffStart = 15f; w.falloffEnd = 35f; w.minDamageMultiplier = 0.5f;
            w.fireRate = 1200f; w.magazineSize = 1; w.reserveAmmo = 0; w.reloadTime = 0.5f; w.equipTime = 0.3f;
            ArcHeat(w);
            w.hipSpread = 0.6f; w.adsSpread = 0.3f; w.movingSpreadAdd = 0.2f; w.airborneSpreadAdd = 0.4f;
            w.recoilPitch = 0.1f; w.recoilYawRandom = 0.08f; w.viewKick = 0.15f; w.screenShake = 0.01f;
            w.adsFovMultiplier = 0.9f; w.adsTime = 0.1f;
            w.tracerColor = new Color(1f, 0.95f, 0.95f, 0.8f);
        }

        /// <summary>
        /// ARC heat tuning (also re-applied to the existing asset on every build - this is a balance fix):
        /// ~29 shots / 1.45 s of continuous beam before it overheats, 2 s lockout, cools only after you let go.
        /// </summary>
        internal static void ArcHeat(WeaponData w)
        {
            w.usesHeat = true; w.heatPerShot = 0.035f; w.heatCoolRate = 0.5f; w.heatCoolDelay = 0.4f; w.overheatLockout = 2f;
            EditorUtility.SetDirty(w);
        }

        private static void ConfigureReaper(WeaponData w)
        {
            w.id = "reaper"; w.displayName = "REAPER"; w.slot = WeaponSlot.Secondary;
            w.fireMode = FireMode.SemiAuto; w.delivery = DeliveryType.Hitscan;
            w.damage = 12f; w.pelletsPerShot = 14; w.headshotMultiplier = 1.2f; w.range = 14f;
            w.falloffStart = 3f; w.falloffEnd = 8f; w.minDamageMultiplier = 0.1f;
            w.fireRate = 50f; w.magazineSize = 2; w.reserveAmmo = 16; w.reloadTime = 2f; w.equipTime = 0.4f;
            w.hipSpread = 7f; w.adsSpread = 6f; w.movingSpreadAdd = 0.5f; w.airborneSpreadAdd = 0.5f; w.fixedPelletPattern = true;
            w.recoilPitch = 5f; w.recoilYawRandom = 1f; w.viewKick = 6f; w.screenShake = 0.35f;
            w.airborneSelfKnockback = 10f;
            w.adsFovMultiplier = 0.95f; w.adsTime = 0.12f;
            w.tracerColor = new Color(1f, 0.3f, 0.3f, 0.7f);
        }

        private static void ConfigureBlade(WeaponData w)
        {
            w.id = "blade"; w.displayName = "BLADE"; w.slot = WeaponSlot.Melee;
            w.fireMode = FireMode.SemiAuto; w.delivery = DeliveryType.Melee;
            w.damage = 55f; w.headshotMultiplier = 1f; w.backstabMultiplier = 2f; w.meleeRange = 2.4f; w.meleeRadius = 0.6f;
            w.fireRate = 90f; w.magazineSize = 1; w.reserveAmmo = 0; w.equipTime = 0.2f;
            w.hipSpread = 0f; w.adsSpread = 0f; w.canAim = false; w.showTracers = false;
            w.recoilPitch = 0f; w.viewKick = 2f; w.screenShake = 0.05f;
        }

        private static void ConfigureKnife(WeaponData w)
        {
            w.id = "knife"; w.displayName = "COMBAT KNIFE"; w.slot = WeaponSlot.Melee;
            w.fireMode = FireMode.SemiAuto; w.delivery = DeliveryType.Melee;
            w.damage = 50f; w.headshotMultiplier = 1f; w.backstabMultiplier = 2.5f; w.meleeRange = 2.2f; w.meleeRadius = 0.55f;
            w.fireRate = 110f; w.magazineSize = 1; w.reserveAmmo = 0; w.equipTime = 0.15f;
            w.hipSpread = 0f; w.adsSpread = 0f; w.canAim = false; w.showTracers = false;
            w.recoilPitch = 0f; w.viewKick = 1.6f; w.screenShake = 0.04f;
        }

        private static void ConfigureBalisong(WeaponData w)
        {
            w.id = "balisong"; w.displayName = "BUTTERFLY KNIFE"; w.slot = WeaponSlot.Melee;
            w.fireMode = FireMode.SemiAuto; w.delivery = DeliveryType.Melee;
            w.damage = 50f; w.headshotMultiplier = 1f; w.backstabMultiplier = 2.5f; w.meleeRange = 2.2f; w.meleeRadius = 0.55f;
            w.fireRate = 110f; w.magazineSize = 1; w.reserveAmmo = 0; w.equipTime = 0.45f; // time for the flip-open
            w.hipSpread = 0f; w.adsSpread = 0f; w.canAim = false; w.showTracers = false;
            w.recoilPitch = 0f; w.viewKick = 1.6f; w.screenShake = 0.04f;
        }

        // ================================================================== Movement Lab

        public static void BuildLab(Assets a)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a.Reload();
            B.SetupLighting();
            var world = new GameObject("World").transform;
            var teleports = new List<DevTeleporter.Point>();
            B.BuildMap(world, teleports);

            // Spawn points (training / custom games in the lab)
            var spawns = B.Group("SpawnPoints", world);
            Spawn(spawns, new Vector3(0f, 0.05f, 0f), 0f, -1);
            Spawn(spawns, new Vector3(-6f, 0.05f, 60f), 0f, 0);
            Spawn(spawns, new Vector3(6f, 0.05f, 120f), 180f, 1);
            Spawn(spawns, new Vector3(40f, 4.05f, 3f), 0f, 0);
            Spawn(spawns, new Vector3(40f, 4.05f, 58f), 180f, 1);
            Spawn(spawns, new Vector3(-40f, 0.05f, 24f), 0f, 0);
            Spawn(spawns, new Vector3(64f, 0.05f, 20f), 270f, 1);
            Spawn(spawns, new Vector3(0f, 8.05f, -45f), 0f, -1);
            Spawn(spawns, new Vector3(11f, 4.05f, -34f), 225f, 0);
            Spawn(spawns, new Vector3(-11f, 4.05f, -56f), 45f, 1);

            // Movement race course through the lab
            var race = B.Group("RaceCourse", world);
            Marker("RaceStart", race, new Vector3(-11f, 0.05f, -2f), 0f);
            Gate(race, 0, new Vector3(-4f, 7.5f, 25f), 0f);
            Gate(race, 1, new Vector3(0f, 2f, 100f), 0f);
            Gate(race, 2, new Vector3(0f, 4f, 166f), 0f);
            Gate(race, 3, new Vector3(40f, 6f, 30f), 0f);
            Gate(race, 4, new Vector3(40f, 6f, 57f), 0f);
            Gate(race, 5, new Vector3(-40f, 3.5f, 60f), 180f);
            Gate(race, 6, new Vector3(-40f, 6.5f, 145f), 0f);
            Gate(race, 7, new Vector3(0f, 10f, -45f), 90f);

            var player = B.BuildPlayer(a.Movement, a.LabLoadout);
            B.BuildSystems(player, teleports);
            AddMatch(MapId: "movement_lab", playerPrefab: null);
            AddNavMesh();
            EditorSceneManager.SaveScene(scene, B.ScenePath);
        }

        // ================================================================== VERTEX

        private static Material _hazard, _vent, _floorDark;

        public static void BuildVertex(Assets a)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            a.Reload();

            _hazard = B.Mat("Hazard", new Color(0.55f, 0.04f, 0.07f), 0.3f, 0.5f, new Color(0.6f, 0.02f, 0.04f));
            _vent = B.Mat("Vent", new Color(0.12f, 0.12f, 0.13f), 0.9f, 0.4f, Color.black);
            _floorDark = B.Mat("FloorDark", new Color(0.09f, 0.09f, 0.1f), 0.2f, 0.3f, Color.black);

            // Lighting: dark facility, cold key light, red practicals
            VampArtBuilder.SetupDayLighting(new Vector3(52f, 30f, 0f));

            var root = new GameObject("VERTEX").transform;
            var low = B.Group("Low", root);
            var mid = B.Group("Mid", root);
            var high = B.Group("High", root);
            var detail = B.Group("Detail", root);
            var lights = B.Group("Lights", root);

            const float half = 44f;
            // --- Shell
            B.Block("Floor", low, new Vector3(0f, -0.5f, 0f), new Vector3(half * 2f, 1f, half * 2f), B._grid);
            B.Block("WallN", root, new Vector3(0f, 11f, half), new Vector3(half * 2f, 24f, 1f), B._concrete);
            B.Block("WallS", root, new Vector3(0f, 11f, -half), new Vector3(half * 2f, 24f, 1f), B._concrete);
            B.Block("WallE", root, new Vector3(half, 11f, 0f), new Vector3(1f, 24f, half * 2f), B._concrete);
            B.Block("WallW", root, new Vector3(-half, 11f, 0f), new Vector3(1f, 24f, half * 2f), B._concrete);
            for (int i = -3; i <= 3; i++)
                B.Block("RoofBeam", detail, new Vector3(i * 12f, 22.5f, 0f), new Vector3(1f, 1.2f, half * 2f), B._metal);

            // --- CORE: central machinery column with wall-run faces + ground arena
            B.Block("CoreColumn", low, new Vector3(0f, 7f, 0f), new Vector3(7f, 14f, 7f), B._metal);
            for (int s = 0; s < 4; s++)
            {
                Quaternion r = Quaternion.Euler(0f, s * 90f, 0f);
                var face = B.Block("CoreRunFace", low, r * new Vector3(0f, 7f, 3.6f), new Vector3(6f, 11f, 0.2f), B._wallRun, rotation: r);
                face.AddComponent<WallRunSurface>();
                B.Block("CoreTrim", detail, r * new Vector3(0f, 12.6f, 3.72f), new Vector3(6f, 0.12f, 0.05f), B._redGlow, collider: false, rotation: r);
            }
            B.Block("CoreTop", high, new Vector3(0f, 14.2f, 0f), new Vector3(9f, 0.4f, 9f), B._concreteLight);
            // Ground cover in the open centre
            Vector3[] cover = { new Vector3(10f, 1f, -4f), new Vector3(-10f, 1f, 4f), new Vector3(4f, 1f, 11f), new Vector3(-4f, 1f, -11f),
                                new Vector3(16f, 1.5f, -14f), new Vector3(-16f, 1.5f, 14f) };
            foreach (var c in cover) B.Block("Crate", low, c, new Vector3(3f, c.y * 2f, 3f), _vent);

            // --- MID ring catwalk (y = 7) around the arena, 6 m wide, inner edge at 18
            const float ringY = 7f;
            float ringIn = 18f, ringOut = 24f;
            float ringMid = (ringIn + ringOut) * 0.5f;
            float ringW = ringOut - ringIn;
            for (int s = 0; s < 4; s++)
            {
                Quaternion r = Quaternion.Euler(0f, s * 90f, 0f);
                // Leave a gap in the middle of each side (jump gap = risk/reward), two segments per side
                B.Block("Ring", mid, r * new Vector3(-13f, ringY - 0.25f, ringMid), new Vector3(22f, 0.5f, ringW), B._metal, rotation: r);
                B.Block("Ring", mid, r * new Vector3(12.5f, ringY - 0.25f, ringMid), new Vector3(11f, 0.5f, ringW), B._metal, rotation: r);
                B.Block("RingRail", detail, r * new Vector3(0f, ringY + 0.5f, ringIn + 0.05f), new Vector3(48f, 0.08f, 0.08f), B._metal, collider: false, rotation: r);
                // Support pillars
                for (int k = -2; k <= 2; k++)
                    B.Block("Pillar", low, r * new Vector3(k * 10f, ringY * 0.5f - 0.25f, ringIn + 0.6f), new Vector3(0.8f, ringY - 0.5f, 0.8f), B._metal, rotation: r);
                // Ramp from ground to mid (per side)
                B.Ramp("RampToMid", mid, r * new Vector3(-6f, 0f, 9f), r * new Vector3(-6f, ringY, ringIn + 0.5f), 4f, B._concreteLight);
                // Dark corridor under the ring (close quarters): inner wall with openings
                B.Block("CorridorWallA", low, r * new Vector3(-14f, 2.5f, ringIn - 0.5f), new Vector3(14f, 5f, 0.6f), B._concrete, rotation: r);
                B.Block("CorridorWallB", low, r * new Vector3(14f, 2.5f, ringIn - 0.5f), new Vector3(14f, 5f, 0.6f), B._concrete, rotation: r);
                // Free-standing wall-run panel beyond the ring: ring edge → wall run → wall jump onto a corner tower
                var panel = B.Block("RunPanel", mid, r * new Vector3(-19f, 11.5f, 28f), new Vector3(22f, 7f, 0.4f), B._wallRun, rotation: r);
                panel.AddComponent<WallRunSurface>();
                B.Block("PanelTrim", detail, r * new Vector3(-19f, 15.1f, 27.75f), new Vector3(22f, 0.12f, 0.05f), B._redGlow, collider: false, rotation: r);
                B.Block("PanelTrim", detail, r * new Vector3(-19f, 15.1f, 28.25f), new Vector3(22f, 0.12f, 0.05f), B._redGlow, collider: false, rotation: r);
                // Pipes along the outer wall
                Pipe(detail, r * new Vector3(0f, 18f, half - 1.5f), r, 80f);
                Pipe(detail, r * new Vector3(0f, 19.2f, half - 1.5f), r, 80f);
                // Vents
                B.Block("Vent", detail, r * new Vector3(12f, 3f, half - 0.55f), new Vector3(4f, 2f, 0.2f), _vent, rotation: r);
                // Warning light
                WarnLight(lights, r * new Vector3(-12f, 9f, ringIn), s % 2 == 0);
            }

            // --- HIGH corner towers (y = 14) with bridges to the core top
            const float highY = 14f;
            for (int c = 0; c < 4; c++)
            {
                Quaternion r = Quaternion.Euler(0f, c * 90f + 45f, 0f);
                Vector3 corner = r * new Vector3(0f, 0f, 50f);
                corner = new Vector3(Mathf.Sign(corner.x) * 36f, 0f, Mathf.Sign(corner.z) * 36f);
                B.Block("Tower", high, corner + new Vector3(0f, highY * 0.5f, 0f), new Vector3(10f, highY, 10f), B._concrete);
                B.Block("TowerLip", detail, corner + new Vector3(0f, highY + 0.02f, 0f), new Vector3(10f, 0.04f, 10f), B._concreteLight, collider: false);
                // Bridge from the tower toward the core top, stopping 4 m short (jump the gap)
                Vector3 dir = new Vector3(-corner.x, 0f, -corner.z).normalized;
                Vector3 s0 = corner + dir * 7f;
                Vector3 e0 = dir * 8.5f;
                Vector3 bm = (s0 + e0) * 0.5f;
                B.Block("Bridge", high, new Vector3(bm.x, highY - 0.2f, bm.z), new Vector3(3f, 0.4f, Vector3.Distance(s0, e0)), B._metal, rotation: Quaternion.LookRotation(dir));
                if (c % 2 == 0)
                {
                    // Wall-jump slot between the tower and the outer wall (2.5 m) - vertical shortcut
                    WarnLight(lights, new Vector3(Mathf.Sign(corner.x) * 42.2f, 3f, corner.z), true);
                }
                else
                {
                    // Long ramp from the ground up to the tower along the outer band
                    float sx = Mathf.Sign(corner.x), sz = Mathf.Sign(corner.z);
                    B.Ramp("TowerRamp", high, new Vector3(sx * 36f, 0f, 0f), new Vector3(sx * 36f, highY, sz * 31f), 3.5f, B._concreteLight);
                }
                B.Block("TowerLight", detail, corner + new Vector3(0f, highY + 3f, 0f), new Vector3(0.3f, 6f, 0.3f), B._metal);
                WarnLight(lights, corner + new Vector3(0f, highY + 6.2f, 0f), false);
            }

            // Machinery clusters in corridors (close combat, escape routes)
            Vector3[] mach = { new Vector3(21f, 1.5f, 30f), new Vector3(-21f, 1.5f, -30f), new Vector3(30f, 1.5f, -21f), new Vector3(-30f, 1.5f, 21f) };
            foreach (var m in mach)
            {
                B.Block("Machine", low, m, new Vector3(4f, 3f, 3f), B._metal);
                B.Block("MachineGlow", detail, m + new Vector3(0f, 1.55f, 0f), new Vector3(4.1f, 0.1f, 3.1f), _hazard, collider: false);
                Tank(low, m + new Vector3(0f, 0f, 3.2f * Mathf.Sign(-m.z)));
            }

            // --- Spawns (team 0 = west, team 1 = east)
            var spawns = B.Group("SpawnPoints", root);
            Spawn(spawns, new Vector3(-30f, 0.05f, 0f), 90f, 0);
            Spawn(spawns, new Vector3(30f, 0.05f, 0f), 270f, 1);
            Spawn(spawns, new Vector3(-28f, 0.05f, -34f), 45f, 0);
            Spawn(spawns, new Vector3(28f, 0.05f, 34f), 225f, 1);
            Spawn(spawns, new Vector3(-28f, 0.05f, 34f), 135f, 0);
            Spawn(spawns, new Vector3(28f, 0.05f, -34f), 315f, 1);
            Spawn(spawns, new Vector3(-21f, ringY + 0.05f, 8f), 90f, 0);
            Spawn(spawns, new Vector3(21f, ringY + 0.05f, -8f), 270f, 1);
            Spawn(spawns, new Vector3(-8f, ringY + 0.05f, -21f), 0f, -1);
            Spawn(spawns, new Vector3(8f, ringY + 0.05f, 21f), 180f, -1);
            Spawn(spawns, new Vector3(-36f, highY + 0.05f, -36f), 45f, 0);
            Spawn(spawns, new Vector3(36f, highY + 0.05f, 36f), 225f, 1);

            // --- Movement race course (low → mid → wall run → high → core → drop)
            var race = B.Group("RaceCourse", root);
            Marker("RaceStart", race, new Vector3(-30f, 0.05f, -6f), 0f);
            Gate(race, 0, new Vector3(-6f, 3f, 12f), 0f);
            Gate(race, 1, new Vector3(-6f, ringY + 2f, 21f), 0f);
            Gate(race, 2, new Vector3(-19f, 12.5f, 26.3f), 90f);
            Gate(race, 3, new Vector3(-36f, highY + 2.5f, 36f), 0f);
            Gate(race, 4, new Vector3(0f, 16.5f, 0f), 45f);
            Gate(race, 5, new Vector3(0f, 2.5f, -14f), 0f);
            Gate(race, 6, new Vector3(-36f, highY + 2.5f, -36f), 90f);
            Gate(race, 7, new Vector3(-30f, 2f, 6f), 0f);

            // Signs
            B.Sign(detail, "V E R T E X", new Vector3(0f, 19.5f, -half + 1.2f), 1.1f, Vector3.back);

            AddMatch("vertex", AssetDatabase.LoadAssetAtPath<GameObject>(B.PlayerPrefabPath));
            AddNavMesh();
            EditorSceneManager.SaveScene(scene, VertexPath);
        }

        // ================================================================== Menu + Boot

        public static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
            cam.AddComponent<AudioListener>();
            VampArtBuilder.SetupDayLighting(new Vector3(38f, -25f, 0f));
            new GameObject("Menu").AddComponent<MenuController>();
            EditorSceneManager.SaveScene(scene, MenuPath);
        }

        public static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            cam.AddComponent<AudioListener>();
            new GameObject("Boot").AddComponent<BootController>();
            EditorSceneManager.SaveScene(scene, BootPath);
        }

        public static void ApplyBuildSettings()
        {
            var ordered = new List<string> { BootPath, MenuPath, B.ScenePath, VertexPath,
                                             VampMapsBuilder.FoundryPath, VampMapsBuilder.OutpostPath, VampMapsBuilder.SkylinePath,
                                             VampMapsBuilder.HarborPath, VampMapsBuilder.CanyonPath, VampMapsBuilder.PitPath, VampMapsBuilder.CrossfirePath };
            var list = new List<EditorBuildSettingsScene>();
            foreach (var path in ordered)
                if (System.IO.File.Exists(path)) list.Add(new EditorBuildSettingsScene(path, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (!ordered.Contains(s.path)) list.Add(new EditorBuildSettingsScene(s.path, false)); // keep others, disabled
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ================================================================== Helpers

        internal static void AddMatch(string MapId, GameObject playerPrefab)
        {
            var go = new GameObject("Match");
            var mc = go.AddComponent<MatchController>();
            B.SetString(mc, "mapId", MapId);
            if (playerPrefab != null) B.SetRef(mc, "playerPrefab", playerPrefab);
        }

        internal static void AddNavMesh()
        {
            var go = new GameObject("NavMesh");
            var s = go.AddComponent<NavMeshSurface>();
            s.collectObjects = CollectObjects.All;
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        }

        internal static void Spawn(Transform parent, Vector3 pos, float yaw, int team)
        {
            var go = new GameObject("Spawn" + (team < 0 ? "" : "_T" + team));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var sp = go.AddComponent<SpawnPoint>();
            sp.Team = team;
            EditorUtility.SetDirty(sp);
        }

        private static void Marker(string name, Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>Race gate: square emissive frame + trigger box.</summary>
        private static void Gate(Transform parent, int index, Vector3 center, float yaw)
        {
            var go = new GameObject("Checkpoint_" + index);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(6f, 6f, 1.5f);
            float s = 3f, t = 0.18f;
            FramePart(go.transform, new Vector3(0f, s, 0f), new Vector3(s * 2f + t, t, t));
            FramePart(go.transform, new Vector3(0f, -s, 0f), new Vector3(s * 2f + t, t, t));
            FramePart(go.transform, new Vector3(-s, 0f, 0f), new Vector3(t, s * 2f, t));
            FramePart(go.transform, new Vector3(s, 0f, 0f), new Vector3(t, s * 2f, t));
            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0f, s + 0.8f, 0f);
            var tm = label.AddComponent<TextMesh>();
            tm.text = (index + 1).ToString();
            tm.font = B._font;
            tm.fontSize = 64;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
            label.GetComponent<MeshRenderer>().sharedMaterial = B._font.material;
            var cp = go.AddComponent<RaceCheckpoint>();
            cp.Index = index;
            EditorUtility.SetDirty(cp);
        }

        private static void FramePart(Transform parent, Vector3 local, Vector3 size)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "Frame";
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            p.transform.localPosition = local;
            p.transform.localScale = size;
            p.GetComponent<Renderer>().sharedMaterial = B._redGlow;
        }

        private static void Pipe(Transform parent, Vector3 pos, Quaternion r, float length)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = "Pipe";
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            p.transform.position = pos;
            p.transform.rotation = r * Quaternion.Euler(0f, 0f, 90f);
            p.transform.localScale = new Vector3(0.5f, length * 0.5f, 0.5f);
            p.GetComponent<Renderer>().sharedMaterial = B._metal;
            GameObjectUtility.SetStaticEditorFlags(p, StaticEditorFlags.BatchingStatic);
        }

        private static void Tank(Transform parent, Vector3 basePos)
        {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            t.name = "Tank";
            t.transform.SetParent(parent, false);
            t.transform.position = basePos + Vector3.up * 2f;
            t.transform.localScale = new Vector3(2.2f, 2f, 2.2f);
            t.GetComponent<Renderer>().sharedMaterial = B._concreteLight;
            GameObjectUtility.SetStaticEditorFlags(t, StaticEditorFlags.BatchingStatic);
        }

        private static void WarnLight(Transform parent, Vector3 pos, bool strobe)
        {
            var go = new GameObject("WarningLight");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.1f, 0.12f);
            l.range = 16f;
            l.intensity = 4f;
            l.shadows = LightShadows.None;
            var w = go.AddComponent<WarningLight>();
            w.Configure(strobe ? 6f : 4f, strobe ? 1.5f : 2f, strobe);
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.transform.SetParent(go.transform, false);
            bulb.transform.localScale = Vector3.one * 0.35f;
            bulb.GetComponent<Renderer>().sharedMaterial = B._redGlow;
        }

    }
}
