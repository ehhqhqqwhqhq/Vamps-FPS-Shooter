using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vamp.Audio;
using Vamp.Combat;
using Vamp.Core;
using Vamp.Movement;
using Vamp.Player;
using Vamp.UI;
using Vamp.Weapons;

namespace Vamp.EditorTools
{
    /// <summary>
    /// One-click prototype setup:  VAMP ▸ Build Movement Test Scene
    /// Creates (only if missing) the MovementSettings + weapon assets, prototype materials, the Player prefab,
    /// and (re)generates Assets/Scenes/Testing/MovementTest.unity containing the movement test map:
    /// long straightaway, slide hill, wall-run canyon, wall-jump chimney, gap platforms, rocket-jump towers,
    /// a small vertical arena and a dummy range.
    /// Existing ScriptableObject assets are never overwritten, so your tuning survives a rebuild.
    /// </summary>
    public static class VampPrototypeBuilder
    {
        internal const string ScenePath = "Assets/Scenes/Testing/MovementTest.unity";
        internal const string MatFolder = "Assets/Art/Materials/Prototype";
        internal const string TexFolder = "Assets/Art/Textures/Prototype";
        internal const string WeaponFolder = "Assets/ScriptableObjects/Weapons";
        internal const string SettingsFolder = "Assets/ScriptableObjects/Settings";
        internal const string PlayerPrefabPath = "Assets/Prefabs/Players/Player.prefab";

        internal static Material _concrete, _concreteLight, _metal, _wallRun, _redGlow, _whiteGlow, _grid, _dummy, _dummyHead, _gunBody, _gunAccent;
        internal static Font _font;

        [MenuItem("VAMP/Rebuild Movement Lab Only", priority = 20)]
        public static void BuildMovementTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorUtility.DisplayProgressBar("VAMP", "Building Movement Lab...", 0.3f);
                var assets = VampSceneBuilder.PrepareAssets();
                VampSceneBuilder.BuildLab(assets);
                VampSceneBuilder.ApplyBuildSettings();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ================================================================== Assets

        internal static void EnsureFolders()
        {
            foreach (var f in new[] { MatFolder, TexFolder, WeaponFolder, SettingsFolder, "Assets/Prefabs/Players", "Assets/Scenes/Testing" })
                EnsureFolder(f);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        internal static Shader LitShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        internal static Material Mat(string name, Color color, float metallic, float smoothness, Color emission)
        {
            string path = MatFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static void CreateMaterials()
        {
            _concrete = Mat("Concrete", new Color(0.15f, 0.15f, 0.16f), 0f, 0.2f, Color.black);
            _concreteLight = Mat("ConcreteLight", new Color(0.27f, 0.27f, 0.29f), 0f, 0.25f, Color.black);
            _metal = Mat("Metal", new Color(0.23f, 0.23f, 0.25f), 0.75f, 0.55f, Color.black);
            _wallRun = Mat("WallRunSurface", new Color(0.2f, 0.05f, 0.07f), 0.5f, 0.6f, new Color(0.25f, 0.01f, 0.03f));
            _redGlow = Mat("RedGlow", new Color(0.8f, 0.05f, 0.1f), 0f, 0.5f, new Color(2.5f, 0.1f, 0.2f));
            _whiteGlow = Mat("WhiteGlow", Color.white, 0f, 0.5f, new Color(1.6f, 1.6f, 1.7f));
            _dummy = Mat("Dummy", new Color(0.75f, 0.75f, 0.78f), 0f, 0.3f, Color.black);
            _dummyHead = Mat("DummyHead", new Color(0.8f, 0.1f, 0.12f), 0f, 0.3f, Color.black);
            _gunBody = Mat("GunBody", new Color(0.09f, 0.09f, 0.1f), 0.8f, 0.6f, Color.black);
            _gunAccent = Mat("GunAccent", new Color(0.8f, 0.05f, 0.1f), 0.2f, 0.6f, new Color(1.2f, 0.05f, 0.1f));

            _grid = Mat("GridFloor", Color.white, 0f, 0.15f, Color.black);
            _grid.mainTexture = LoadOrCreateGridTexture();
            _grid.mainTextureScale = new Vector2(120f, 120f); // 240 m floor → 2 m grid
            EditorUtility.SetDirty(_grid);
        }

        internal static Texture2D LoadOrCreateGridTexture()
        {
            string path = TexFolder + "/Grid.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;

            const int size = 128;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Grid", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var dark = new Color(0.11f, 0.11f, 0.12f);
            var line = new Color(0.3f, 0.3f, 0.32f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, (x < 2 || y < 2) ? line : dark);
            tex.Apply(true);
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        internal static MovementSettings LoadOrCreateMovementSettings()
        {
            string path = SettingsFolder + "/MovementSettings_Default.asset";
            var s = AssetDatabase.LoadAssetAtPath<MovementSettings>(path);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<MovementSettings>();
            AssetDatabase.CreateAsset(s, path);
            return s;
        }

        internal static WeaponData LoadOrCreateWeapon(string fileName, System.Action<WeaponData> configure)
        {
            string path = WeaponFolder + "/" + fileName + ".asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (w != null) return w; // keep designer tuning
            w = ScriptableObject.CreateInstance<WeaponData>();
            configure(w);
            AssetDatabase.CreateAsset(w, path);
            return w;
        }

        internal static void ConfigureBrute(WeaponData w)
        {
            w.id = "brute"; w.displayName = "BRUTE"; w.slot = WeaponSlot.Primary;
            w.fireMode = FireMode.SemiAuto; w.delivery = DeliveryType.Hitscan;
            w.damage = 10f; w.pelletsPerShot = 10; w.headshotMultiplier = 1.25f; w.range = 60f;
            w.falloffStart = 6f; w.falloffEnd = 18f; w.minDamageMultiplier = 0.3f;
            w.fireRate = 70f; w.magazineSize = 6; w.reserveAmmo = 30; w.reloadTime = 0.45f; w.reloadPerShell = true; w.equipTime = 0.35f;
            w.hipSpread = 5.5f; w.adsSpread = 4f; w.movingSpreadAdd = 0.5f; w.airborneSpreadAdd = 0.5f; w.fixedPelletPattern = true;
            w.recoilPitch = 3.5f; w.recoilYawRandom = 0.8f; w.viewKick = 4f; w.screenShake = 0.2f;
            w.airborneSelfKnockback = 7f;
            w.canAim = true; w.adsFovMultiplier = 0.9f; w.adsTime = 0.12f;
            w.tracerColor = new Color(1f, 0.5f, 0.4f, 0.8f);
        }

        internal static void ConfigureV9(WeaponData w)
        {
            w.id = "v9"; w.displayName = "V-9"; w.slot = WeaponSlot.Secondary;
            w.fireMode = FireMode.SemiAuto; w.delivery = DeliveryType.Hitscan;
            w.damage = 24f; w.pelletsPerShot = 1; w.headshotMultiplier = 2f; w.range = 150f;
            w.falloffStart = 20f; w.falloffEnd = 45f; w.minDamageMultiplier = 0.6f;
            w.fireRate = 400f; w.magazineSize = 12; w.reserveAmmo = 60; w.reloadTime = 1.1f; w.equipTime = 0.25f;
            w.hipSpread = 0.9f; w.adsSpread = 0.15f; w.movingSpreadAdd = 0.6f; w.airborneSpreadAdd = 1.5f;
            w.recoilPitch = 1.1f; w.recoilYawRandom = 0.3f; w.viewKick = 1.2f; w.screenShake = 0.05f;
            w.canAim = true; w.adsFovMultiplier = 0.85f; w.adsTime = 0.12f;
            w.tracerColor = new Color(1f, 0.85f, 0.8f, 0.6f);
        }

        internal static void ConfigureBlast(WeaponData w)
        {
            w.id = "blast"; w.displayName = "BLAST"; w.slot = WeaponSlot.Primary;
            w.fireMode = FireMode.SemiAuto; w.delivery = DeliveryType.Projectile;
            w.damage = 40f; w.headshotMultiplier = 1f; w.range = 200f;
            w.fireRate = 75f; w.magazineSize = 4; w.reserveAmmo = 20; w.reloadTime = 1.6f; w.equipTime = 0.45f;
            w.hipSpread = 0f; w.adsSpread = 0f; w.movingSpreadAdd = 0f; w.airborneSpreadAdd = 0f;
            w.recoilPitch = 2.5f; w.recoilYawRandom = 0.4f; w.viewKick = 3f; w.screenShake = 0.3f;
            w.canAim = false;
            w.projectileSpeed = 42f; w.projectileGravity = 0f; w.projectileLifetime = 5f; w.projectileRadius = 0.12f;
            w.explosionRadius = 4f; w.explosionDamage = 85f; w.explosionKnockback = 14f; w.explosionMinFalloff = 0.3f;
            w.selfDamageMultiplier = 0.35f; w.selfKnockbackMultiplier = 1.15f; w.knockbackUpBias = 0.35f;
            w.showTracers = false;
        }

        // ================================================================== Lighting

        internal static void SetupLighting()
        {
            VampArtBuilder.SetupDayLighting(new Vector3(50f, -35f, 0f));
        }

        // ================================================================== Map

        internal static void BuildMap(Transform world, List<DevTeleporter.Point> teleports)
        {
            // Ground (x -120..120, z -80..180)
            Block("Ground", world, new Vector3(0f, -0.5f, 50f), new Vector3(240f, 1f, 260f), _grid);

            // ---------- Spawn
            var spawnArea = Group("Spawn", world);
            Block("SpawnPad", spawnArea, new Vector3(0f, 0.01f, 0f), new Vector3(8f, 0.02f, 8f), _concreteLight, collider: false);
            Block("SpawnStripe", spawnArea, new Vector3(0f, 0.03f, 4f), new Vector3(8f, 0.02f, 0.2f), _redGlow, collider: false);
            Sign(spawnArea, "VAMP // MOVEMENT LAB", new Vector3(0f, 5f, 12f), 0.5f);
            Sign(spawnArea, "F2-F9 TELEPORT   ·   F1 DEBUG", new Vector3(0f, 3.8f, 12f), 0.22f);
            AddTeleport(teleports, "Spawn", spawnArea, new Vector3(0f, 0.05f, 0f), 0f);

            // ---------- Slide hill + straightaway (x 0, z 20..170)
            var lane = Group("Straightaway", world);
            Block("HillTop", lane, new Vector3(-4f, 3f, 25f), new Vector3(20f, 6f, 10f), _concrete);
            Ramp("HillAccess", lane, new Vector3(-11f, 0f, 2f), new Vector3(-11f, 6f, 20f), 6f, _concreteLight);
            Ramp("SlideSlope", lane, new Vector3(0f, 6f, 30f), new Vector3(0f, 0f, 54f), 12f, _metal);
            Block("SlideStripeL", lane, new Vector3(-6.1f, 0.05f, 110f), new Vector3(0.2f, 0.1f, 110f), _redGlow, collider: false);
            Block("SlideStripeR", lane, new Vector3(6.1f, 0.05f, 110f), new Vector3(0.2f, 0.1f, 110f), _redGlow, collider: false);
            for (int m = 0; m <= 100; m += 10)
            {
                Block("Marker" + m, lane, new Vector3(0f, 0.02f, 55f + m), new Vector3(12f, 0.04f, 0.3f), _whiteGlow, collider: false);
                Sign(lane, m + "M", new Vector3(-7.5f, 0.6f, 55f + m), 0.3f);
            }
            Ramp("Kicker", lane, new Vector3(0f, 0f, 160f), new Vector3(0f, 2.5f, 168f), 8f, _metal);
            Sign(lane, "SLIDE HILL  ·  SPRINT → SLIDE → JUMP", new Vector3(-4f, 8.5f, 22f), 0.35f);
            AddTeleport(teleports, "Slide Hill", lane, new Vector3(0f, 6.05f, 26f), 0f);

            // Long practice wall-run wall beside the lane
            var practiceWall = Block("PracticeWallRun", lane, new Vector3(11f, 4f, 95f), new Vector3(1f, 8f, 60f), _wallRun);
            practiceWall.AddComponent<WallRunSurface>();
            Block("PracticeWallTrim", lane, new Vector3(10.45f, 7.6f, 95f), new Vector3(0.1f, 0.15f, 60f), _redGlow, collider: false);

            // ---------- Wall-run canyon (x 40)
            var canyon = Group("WallRunCanyon", world);
            Block("StartTower", canyon, new Vector3(40f, 2f, 5f), new Vector3(12f, 4f, 10f), _concrete);
            Ramp("StartAccess", canyon, new Vector3(40f, 0f, -22f), new Vector3(40f, 4f, 0f), 8f, _concreteLight);
            for (int side = -1; side <= 1; side += 2)
            {
                // Inner faces 7 m apart: wide enough to run, narrow enough to chain wall-to-wall.
                var wall = Block(side < 0 ? "WallLeft" : "WallRight", canyon, new Vector3(40f + side * 4f, 6.5f, 32f), new Vector3(1f, 11f, 40f), _wallRun);
                wall.AddComponent<WallRunSurface>();
                Block("Trim", canyon, new Vector3(40f + side * 3.45f, 11.8f, 32f), new Vector3(0.1f, 0.15f, 40f), _redGlow, collider: false);
                Block("Trim", canyon, new Vector3(40f + side * 3.45f, 1.2f, 32f), new Vector3(0.1f, 0.15f, 40f), _redGlow, collider: false);
            }
            Block("EndTower", canyon, new Vector3(40f, 2f, 57f), new Vector3(12f, 4f, 10f), _concrete);
            Ramp("EndExit", canyon, new Vector3(40f, 4f, 62f), new Vector3(40f, 0f, 80f), 8f, _concreteLight);
            Sign(canyon, "WALL-RUN CANYON  ·  RUN → WALL JUMP → RUN", new Vector3(40f, 14f, 10f), 0.35f);
            AddTeleport(teleports, "Wall-Run Canyon", canyon, new Vector3(40f, 4.05f, 3f), 0f);

            // ---------- Wall-jump chimney (x -40, z 30)
            var shaft = Group("WallJumpChimney", world);
            const float gap = 3.2f, height = 18f;
            for (int side = -1; side <= 1; side += 2)
                Block(side < 0 ? "ShaftLeft" : "ShaftRight", shaft, new Vector3(-40f + side * (gap * 0.5f + 0.5f), height * 0.5f, 34f), new Vector3(1f, height, 8f), _metal);
            Block("ShaftBack", shaft, new Vector3(-40f, height * 0.5f, 38.5f), new Vector3(gap + 2f, height, 1f), _metal);
            Block("Summit", shaft, new Vector3(-40f, height + 0.25f, 42f), new Vector3(10f, 0.5f, 8f), _concreteLight);
            Block("SummitGlow", shaft, new Vector3(-40f, height + 0.52f, 38.2f), new Vector3(10f, 0.04f, 0.3f), _redGlow, collider: false);
            for (int h = 3; h < height; h += 3)
                Sign(shaft, h + "M", new Vector3(-37.2f, h, 30f), 0.25f);
            Sign(shaft, "WALL-JUMP CHIMNEY  ·  ALTERNATE WALLS", new Vector3(-40f, 4f, 27f), 0.3f);
            AddTeleport(teleports, "Wall-Jump Chimney", shaft, new Vector3(-40f, 0.05f, 28f), 0f);

            // ---------- Gap platforms (x -40, z 60+)
            var gaps = Group("GapPlatforms", world);
            float z = 60f;
            Block("GapStart", gaps, new Vector3(-40f, 1f, z), new Vector3(6f, 2f, 6f), _concrete);
            Ramp("GapAccess", gaps, new Vector3(-40f, 0f, z - 12f), new Vector3(-40f, 2f, z - 3f), 4f, _concreteLight);
            AddTeleport(teleports, "Gap Platforms", gaps, new Vector3(-40f, 2.05f, z), 0f);
            float[] gapSizes = { 5f, 7f, 9f, 11f, 13f, 15f };
            float[] heights = { 2f, 2.5f, 3f, 3f, 3.5f, 4f };
            z += 3f;
            for (int i = 0; i < gapSizes.Length; i++)
            {
                z += gapSizes[i] + 2f;
                Block("Gap" + gapSizes[i], gaps, new Vector3(-40f, heights[i] * 0.5f, z), new Vector3(4f, heights[i], 4f), i % 2 == 0 ? _concrete : _concreteLight);
                Block("GapEdge", gaps, new Vector3(-40f, heights[i] + 0.02f, z - 1.9f), new Vector3(4f, 0.04f, 0.2f), _redGlow, collider: false);
                Sign(gaps, gapSizes[i] + "M GAP", new Vector3(-40f, heights[i] + 2.2f, z), 0.25f);
                z += 2f;
            }
            Sign(gaps, "GAPS  ·  SLIDE-JUMP / DASH / AIR STRAFE", new Vector3(-40f, 6f, 57f), 0.3f);

            // ---------- Rocket-jump towers (x 72)
            var towers = Group("RocketJumpTowers", world);
            float[] towerHeights = { 5f, 10f, 14f };
            string[] towerLabels = { "5M  ·  ROCKET AT FEET", "10M  ·  JUMP + ROCKET", "14M  ·  ADVANCED" };
            for (int i = 0; i < towerHeights.Length; i++)
            {
                float tz = 10f + i * 22f;
                float th = towerHeights[i];
                Block("Tower" + th, towers, new Vector3(76f, th * 0.5f, tz), new Vector3(8f, th, 8f), _concrete);
                Block("TowerLip", towers, new Vector3(71.95f, th - 0.1f, tz), new Vector3(0.1f, 0.2f, 8f), _redGlow, collider: false);
                Sign(towers, towerLabels[i], new Vector3(71f, th + 1.5f, tz), 0.25f, faceDir: Vector3.right);
                SpawnDummy(towers, new Vector3(77f, th, tz + 2f), 0f, 0f);
            }
            // Tall wall next to the 14 m tower to rocket off
            Block("RocketWall", towers, new Vector3(70f, 9f, 60f), new Vector3(8f, 18f, 1f), _metal);
            Sign(towers, "ROCKET JUMP ZONE  ·  PRESS 3 FOR BLAST", new Vector3(64f, 6f, 30f), 0.3f, faceDir: Vector3.right);
            AddTeleport(teleports, "Rocket Towers", towers, new Vector3(64f, 0.05f, 20f), 90f);

            // ---------- Mini vertical arena (preview of VERTEX flow), behind spawn
            var arena = Group("VerticalArena", world);
            Vector3 c = new Vector3(0f, 0f, -45f);
            Block("CoreTower", arena, c + new Vector3(0f, 4f, 0f), new Vector3(8f, 8f, 8f), _concrete);
            Block("CoreGlow", arena, c + new Vector3(0f, 8.02f, 0f), new Vector3(8f, 0.04f, 8f), _concreteLight, collider: false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                Quaternion rot = Quaternion.Euler(0f, a, 0f);
                Vector3 p = c + rot * new Vector3(0f, 0f, 16f);
                Block("MidPlatform" + i, arena, p + new Vector3(0f, 2f, 0f), new Vector3(6f, 4f, 6f), _concreteLight);
                Ramp("MidRamp" + i, arena, p + rot * new Vector3(0f, 0f, 14f), p + rot * new Vector3(0f, 4f, 3f), 4f, _metal);
                // Wall-run panel linking mid platform to the core
                Vector3 wallPos = Vector3.Lerp(p, c, 0.5f) + rot * new Vector3(4f, 5f, 0f);
                var link = Block("LinkWall" + i, arena, wallPos, new Vector3(1f, 6f, 9f), _wallRun, rotation: rot);
                link.AddComponent<WallRunSurface>();
                Block("Cover" + i, arena, c + Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 1f, 9f), new Vector3(3f, 2f, 1f), _metal);
                SpawnDummy(arena, p + new Vector3(0f, 4f, 0f), 0f, 0f);
            }
            Sign(arena, "VERTICAL ARENA  ·  LOW / MID / HIGH ROUTES", c + new Vector3(0f, 11f, 12f), 0.3f, faceDir: Vector3.back);
            SpawnDummy(arena, c + new Vector3(0f, 8f, 0f), 2f, 1f);
            AddTeleport(teleports, "Vertical Arena", arena, c + new Vector3(0f, 0.05f, 24f), 180f);

            // ---------- Dummy range (right of spawn)
            var range = Group("DummyRange", world);
            float[] distances = { 5f, 10f, 20f, 35f };
            for (int i = 0; i < distances.Length; i++)
            {
                SpawnDummy(range, new Vector3(14f + i * 2.5f, 0f, distances[i]), 0f, 0f);
                Sign(range, distances[i] + "M", new Vector3(14f + i * 2.5f, 2.6f, distances[i]), 0.2f);
            }
            SpawnDummy(range, new Vector3(-4f, 0f, 12f), 3f, 1.2f);
            SpawnDummy(range, new Vector3(22f, 0f, 45f), 5f, 0.9f);

            // ---------- Mood lights
            var lights = Group("Lights", world);
            Vector3[] lightPositions =
            {
                new Vector3(0f, 7f, 0f), new Vector3(40f, 10f, 32f), new Vector3(-40f, 10f, 34f),
                new Vector3(76f, 16f, 30f), new Vector3(0f, 12f, -45f), new Vector3(0f, 6f, 100f), new Vector3(-40f, 7f, 90f)
            };
            for (int i = 0; i < lightPositions.Length; i++)
            {
                var l = new GameObject("RedLight" + i).AddComponent<Light>();
                l.transform.SetParent(lights, false);
                l.transform.position = lightPositions[i];
                l.type = LightType.Point;
                l.color = new Color(1f, 0.12f, 0.15f);
                l.range = 22f;
                l.intensity = 4f;
                l.shadows = LightShadows.None;
            }
        }

        // ================================================================== Player / systems

        internal static GameObject BuildPlayer(MovementSettings movement, WeaponData[] loadout)
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0f, 0.05f, 0f);

            go.AddComponent<CharacterController>();
            var motor = go.AddComponent<MovementController>();
            go.AddComponent<DashAbility>();
            go.AddComponent<WallRunAbility>();
            go.AddComponent<SlideAbility>();
            go.AddComponent<InputController>();
            var health = go.AddComponent<HealthController>();
            var cam = go.AddComponent<CameraController>();
            var weapons = go.AddComponent<WeaponController>();
            go.AddComponent<PlayerController>();
            go.AddComponent<PlayerFeedback>();
            var identity = go.AddComponent<CombatIdentity>();

            var root = new GameObject("CameraRoot").transform;
            root.SetParent(go.transform, false);
            root.localPosition = new Vector3(0f, 1.65f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root, false);
            var camera = camGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.55f, 0.65f, 0.8f);
            camGo.AddComponent<AudioListener>();
            var vm = camGo.AddComponent<WeaponViewModel>();
            var presenter = go.AddComponent<Vamp.Characters.CharacterPresenter>();
            presenter.LocalView = true;

            SetRef(motor, "settings", movement);
            SetRef(cam, "cameraRoot", root);
            SetRef(cam, "playerCamera", camera);
            SetRef(weapons, "aimOrigin", root);
            SetArray(weapons, "loadout", loadout);
            SetRef(vm, "weapons", weapons);
            SetRef(vm, "bodyMaterial", _gunBody);
            SetRef(vm, "accentMaterial", _gunAccent);
            SetString(identity, "displayName", "YOU");
            // Training convenience: regenerate after 2.5 s so rocket-jump practice doesn't kill you.
            SetFloat(health, "regenDelay", 2.5f);
            SetFloat(health, "regenPerSecond", 40f);

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, PlayerPrefabPath, InteractionMode.AutomatedAction);
            if (prefab == null) Debug.LogWarning("[VAMP] Could not save Player prefab (scene still works).");
            return go;
        }

        internal static void BuildSystems(GameObject player, List<DevTeleporter.Point> teleports)
        {
            var pc = player.GetComponent<PlayerController>();

            var systems = new GameObject("Systems");

            var hud = new GameObject("HUD");
            var hudCtrl = hud.AddComponent<HUDController>();
            SetRef(hudCtrl, "player", pc);
            var overlay = hud.AddComponent<MovementDebugOverlay>();
            SetRef(overlay, "player", pc);

            var tp = systems.AddComponent<DevTeleporter>();
            SetRef(tp, "player", pc);
            var so = new SerializedObject(tp);
            var arr = so.FindProperty("points");
            arr.arraySize = teleports.Count;
            for (int i = 0; i < teleports.Count; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("name").stringValue = teleports[i].name;
                el.FindPropertyRelative("target").objectReferenceValue = teleports[i].target;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================================================================== Geometry helpers

        internal static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        internal static GameObject Block(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
                                        bool collider = true, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = rotation ?? Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        /// <summary>Ramp surface running from bottom-edge centre to top-edge centre.</summary>
        internal static GameObject Ramp(string name, Transform parent, Vector3 from, Vector3 to, float width, Material mat)
        {
            const float thickness = 0.5f;
            Vector3 dir = to - from;
            Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 center = (from + to) * 0.5f - (rot * Vector3.up) * (thickness * 0.5f);
            return Block(name, parent, center, new Vector3(width, thickness, dir.magnitude + 0.3f), mat, true, rot);
        }

        internal static void Sign(Transform parent, string text, Vector3 position, float size, Vector3? faceDir = null)
        {
            var go = new GameObject("Sign_" + text);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            // TextMesh reads correctly when the viewer looks along its +Z. faceDir = the direction the VIEWER looks.
            go.transform.rotation = Quaternion.LookRotation(faceDir.HasValue ? faceDir.Value : Vector3.forward);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = _font;
            tm.fontSize = 64;
            tm.characterSize = size * 0.15f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(1f, 1f, 1f, 0.9f);
            var mr = go.GetComponent<MeshRenderer>();
            if (_font != null) mr.sharedMaterial = _font.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        internal static void SpawnDummy(Transform parent, Vector3 feet, float strafe, float speed)
        {
            var root = new GameObject("Dummy");
            root.transform.SetParent(parent, false);
            root.transform.position = feet;
            root.AddComponent<HealthController>();
            var id = root.AddComponent<CombatIdentity>();
            SetString(id, "displayName", "DUMMY");
            var dummy = root.AddComponent<TrainingDummy>();
            SetFloat(dummy, "strafeAmplitude", strafe);
            SetFloat(dummy, "strafeSpeed", speed);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localScale = new Vector3(0.8f, 0.75f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = _dummy;
            body.AddComponent<Hitbox>();

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            head.transform.localScale = Vector3.one * 0.42f;
            head.GetComponent<Renderer>().sharedMaterial = _dummyHead;
            var hb = head.AddComponent<Hitbox>();
            SetBool(hb, "isHead", true);
        }

        internal static void AddTeleport(List<DevTeleporter.Point> list, string name, Transform parent, Vector3 position, float yaw)
        {
            var t = new GameObject("TP_" + name).transform;
            t.SetParent(parent, false);
            t.position = position;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            list.Add(new DevTeleporter.Point { name = name, target = t });
        }

        // ================================================================== SerializedObject helpers

        internal static SerializedProperty Prop(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p == null) Debug.LogError("[VAMP] Missing serialized field '" + name + "' on " + so.targetObject.GetType().Name);
            return p;
        }

        internal static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
