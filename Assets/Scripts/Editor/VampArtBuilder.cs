using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Vamp.Characters;
using Vamp.Weapons;
using B = Vamp.EditorTools.VampPrototypeBuilder;

namespace Vamp.EditorTools
{
    /// <summary>
    /// Imported art → game-ready prefabs.
    ///  • Weapons (Assets/Art/Weapons/*.fbx): each model is auto-oriented (barrel → +Z, top → +Y), scaled to a real-world
    ///    length and gets Grip / Offhand / Muzzle / Sight points. Saved to Assets/Prefabs/Weapons/Models and assigned to
    ///    WeaponData.viewModelPrefab (used for first person AND the third-person gun in characters' hands).
    ///  • Character (Assets/Art/Characters/StickMan.fbx): scaled to 1.8 m, feet at 0, facing +Z, saved as
    ///    Resources/VampCharacter.prefab with a <see cref="CharacterRig"/> (procedural animation - the pack has no clips).
    ///  • Day-time lighting: procedural sky, warm sun, trilight ambient, light haze.
    /// VAMP ▸ Preview Art opens a scene with every model laid out, to check orientation.
    /// </summary>
    public static class VampArtBuilder
    {
        public const string WeaponArt = "Assets/Art/Weapons";
        public const string CharacterFbx = "Assets/Art/Characters/StickMan.fbx";
        public const string WeaponPrefabs = "Assets/Prefabs/Weapons/Models";
        public const string CharacterPrefab = "Assets/Resources/VampCharacter.prefab";
        public const string ArtMaterials = "Assets/Art/Materials";

        /// <summary>Real-world length (m) per model file.</summary>
        private static readonly Dictionary<string, float> Lengths = new Dictionary<string, float>
        {
            { "1911", 0.22f }, { "USP", 0.21f }, { "Ruger22", 0.26f }, { "MP7", 0.45f }, { "MP5", 0.55f }, { "Vector", 0.62f },
            { "AK47", 0.88f }, { "M4A1", 0.84f }, { "SCAR", 0.9f }, { "AutoShotgun", 0.95f }, { "MossbergShotgun", 1.0f },
            { "PumpShotgun", 0.98f }, { "HuntingRifle", 1.1f }, { "LeverAction", 1.0f }, { "AWP", 1.18f },
        };

        /// <summary>Manual corrections if the automatic orientation guesses wrong: (flip front/back, flip upside down).</summary>
        private static readonly Dictionary<string, (bool flipZ, bool flipY)> Fixes = new Dictionary<string, (bool, bool)>
        {
            { "AWP", (false, true) }, // long stock + low bipod fool the auto-detection: it came out upside down
        };

        /// <summary>Which model each VAMP weapon uses (weapon id → model file). BLAST and BLADE keep generated models.</summary>
        public static readonly Dictionary<string, string> WeaponModels = new Dictionary<string, string>
        {
            { "v9", "USP" }, { "reaper", "1911" }, { "ripper", "Vector" }, { "havoc", "AK47" },
            { "arc", "SCAR" }, { "brute", "PumpShotgun" }, { "widow", "AWP" },
        };

        private static Material _gunMetal, _stick, _daySky;

        // ================================================================== Entry points

        /// <summary>
        /// Step 1 - run BEFORE loading any other asset: fixing import settings reimports the FBX files, which can
        /// invalidate references to assets already held in memory (that's how the Player prefab lost its settings).
        /// </summary>
        public static void ImportModels()
        {
            B.EnsureFolder(ArtMaterials);
            B.EnsureFolder("Assets/Prefabs/Weapons");
            B.EnsureFolder(WeaponPrefabs);
            B.EnsureFolder("Assets/Resources");
            _gunMetal = LitMat("GunMetal", new Color(0.16f, 0.16f, 0.17f), 0.75f, 0.5f);
            _stick = LitMat("StickMan", new Color(0.85f, 0.85f, 0.87f), 0f, 0.35f);

            var prefabs = new Dictionary<string, GameObject>();
            foreach (var kv in Lengths)
            {
                var p = BuildWeaponPrefab(kv.Key, kv.Value);
                if (p != null) prefabs[kv.Key] = p;
            }
            BuildCharacterPrefab();
            BuildKnifePrefab();
            VampArmsBuilder.Build();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Step 2 - point each VAMP weapon at its imported model prefab.</summary>
        public static void AssignWeaponModels(IEnumerable<WeaponData> weapons)
        {
            foreach (var w in weapons)
            {
                if (w == null) continue;
                string model;
                if (w.id == "knife") model = "Knife";
                else if (!WeaponModels.TryGetValue(w.id, out model)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabs + "/" + model + ".prefab");
                if (prefab == null) continue;
                w.viewModelPrefab = prefab;
                EditorUtility.SetDirty(w);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("VAMP/Preview Art", priority = 30)]
        public static void Preview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            B.EnsureFolders();
            ImportModels();
            B.CreateMaterials();
            SetupDayLighting(new Vector3(50f, -35f, 0f));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            floor.GetComponent<Renderer>().sharedMaterial = B._concreteLight;

            float x = -7f;
            foreach (var kv in Lengths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabs + "/" + kv.Key + ".prefab");
                if (prefab == null) continue;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                g.transform.position = new Vector3(x, 1.2f, 0f);
                g.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // barrel points left (-X) when seen from the camera
                var label = new GameObject(kv.Key).AddComponent<TextMesh>();
                label.text = kv.Key;
                label.characterSize = 0.05f;
                label.fontSize = 40;
                label.anchor = TextAnchor.MiddleCenter;
                label.color = Color.black;
                label.transform.position = new Vector3(x, 0.8f, 0f);
                x += 1f;
            }

            var knife = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabs + "/Knife.prefab");
            if (knife != null)
            {
                var k = (GameObject)PrefabUtility.InstantiatePrefab(knife);
                k.transform.position = new Vector3(x, 1.2f, 0f);
                k.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            }

            var ch = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
            if (ch != null)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(ch);
                c.transform.position = new Vector3(-9f, 0f, 1.5f);
                var c2 = (GameObject)PrefabUtility.InstantiatePrefab(ch);
                c2.transform.position = new Vector3(9f, 0f, 1.5f);
                c2.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }

            var cam = new GameObject("Preview Camera").AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 1.6f, -9f);
            cam.transform.rotation = Quaternion.Euler(6f, 0f, 0f);
            cam.fieldOfView = 55f;
            cam.tag = "MainCamera";

            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                sv.LookAt(new Vector3(0f, 1.1f, 0f), Quaternion.Euler(8f, 0f, 0f), 9f);
                sv.Repaint();
            }
            Debug.Log("[VAMP] Art preview: guns should point LEFT with sights UP; characters face the camera (left) and away (right).");
        }

        // ================================================================== Lighting

        public static void SetupDayLighting(Vector3 sunEuler)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(sunEuler);

            RenderSettings.sun = sun;
            RenderSettings.skybox = DaySky();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.7f, 0.78f, 0.88f);
            RenderSettings.fogDensity = 0.0035f;
        }

        public static Material DaySky()
        {
            if (_daySky != null) return _daySky;
            B.EnsureFolder(ArtMaterials);
            string path = ArtMaterials + "/DaySky.mat";
            var sh = Shader.Find("Skybox/Procedural");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (sh != null) mat.shader = sh;
            SetF(mat, "_SunDisk", 2f);
            SetF(mat, "_SunSize", 0.04f);
            SetF(mat, "_SunSizeConvergence", 5f);
            SetF(mat, "_AtmosphereThickness", 1f);
            SetF(mat, "_Exposure", 1.25f);
            if (mat.HasProperty("_SkyTint")) mat.SetColor("_SkyTint", new Color(0.5f, 0.62f, 0.8f));
            if (mat.HasProperty("_GroundColor")) mat.SetColor("_GroundColor", new Color(0.37f, 0.36f, 0.35f));
            EditorUtility.SetDirty(mat);
            _daySky = mat;
            return mat;
        }

        private static void SetF(Material m, string p, float v)
        {
            if (m.HasProperty(p)) m.SetFloat(p, v);
        }

        private static Material LitMat(string name, Color c, float metallic, float smooth)
        {
            string path = ArtMaterials + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(B.LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = c;
            SetF(mat, "_Metallic", metallic);
            SetF(mat, "_Smoothness", smooth);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ================================================================== Import settings

        private static void ConfigureImporter(string path, bool character)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return;
            bool dirty = false;
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
            if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
            if (mi.importCameras) { mi.importCameras = false; dirty = true; }
            if (mi.importLights) { mi.importLights = false; dirty = true; }
            if (!mi.useFileScale) { mi.useFileScale = true; dirty = true; }
            var anim = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (mi.animationType != anim) { mi.animationType = anim; dirty = true; }
            if (dirty) mi.SaveAndReimport();
        }

        // ================================================================== Weapons

        private static GameObject BuildWeaponPrefab(string model, float length)
        {
            string fbx = WeaponArt + "/" + model + ".fbx";
            if (AssetDatabase.LoadMainAssetAtPath(fbx) == null) return null;
            ConfigureImporter(fbx, false);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (src == null) return null;

            var root = new GameObject(model);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var verts = Collect(inst.transform, root.transform);
            if (verts.Count == 0) { Object.DestroyImmediate(root); return null; }

            // 1) Longest extent → forward (Z), second → up (Y).
            var b = BoundsOf(verts);
            var ext = b.size;
            int[] order = { 0, 1, 2 };
            System.Array.Sort(order, (p, q) => ext[q].CompareTo(ext[p]));
            Vector3 fwd = Axis(order[0]), up = Axis(order[1]);
            Rotate(inst.transform, verts, Quaternion.Inverse(Quaternion.LookRotation(fwd, up)));

            // 2) Upright: the top band (receiver/barrel) runs the full length; the bottom band (grip/mag) is short.
            b = BoundsOf(verts);
            if (ZSpread(verts, b, false) > ZSpread(verts, b, true)) Rotate(inst.transform, verts, Quaternion.Euler(0f, 0f, 180f));
            // 3) Muzzle forward: the grip sits behind the middle of the gun.
            b = BoundsOf(verts);
            if (BandCenterZ(verts, b, false) > b.center.z) Rotate(inst.transform, verts, Quaternion.Euler(0f, 180f, 0f));
            (bool flipZ, bool flipY) fix;
            if (Fixes.TryGetValue(model, out fix))
            {
                if (fix.flipY) Rotate(inst.transform, verts, Quaternion.Euler(0f, 0f, 180f));
                if (fix.flipZ) Rotate(inst.transform, verts, Quaternion.Euler(0f, 180f, 0f));
            }

            // 4) Real-world scale.
            b = BoundsOf(verts);
            float s = length / Mathf.Max(0.0001f, b.size.z);
            inst.transform.localScale *= s;
            inst.transform.localPosition *= s;
            for (int i = 0; i < verts.Count; i++) verts[i] *= s;
            b = BoundsOf(verts);

            // 5) Reference points (root space) - origin = grip.
            float gripZ = BandCenterZ(verts, b, false);
            if (length < 0.3f) gripZ = Mathf.Lerp(b.min.z, b.max.z, 0.3f); // pistols: grip at the back third
            Vector3 origin = new Vector3(b.center.x, b.center.y, gripZ);
            inst.transform.localPosition -= origin;
            for (int i = 0; i < verts.Count; i++) verts[i] -= origin;
            b = BoundsOf(verts);

            float barrelY = MeanY(verts, b.max.z - length * 0.08f, b.max.z, b.center.y);
            float sightY = MaxY(verts, -length * 0.15f, length * 0.3f, b.max.y);
            Point(root, "Muzzle", new Vector3(0f, barrelY, b.max.z + 0.01f));
            Point(root, "Sight", new Vector3(0f, sightY, 0.05f));
            Point(root, "Grip", new Vector3(0f, b.min.y + b.size.y * 0.25f, 0f));
            if (length < 0.3f) Point(root, "Offhand", new Vector3(-0.02f, b.min.y + b.size.y * 0.25f, -0.01f));
            else Point(root, "Offhand", new Vector3(0f, barrelY - 0.03f, b.max.z * 0.5f));

            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = _gunMetal;
                r.sharedMaterials = mats;
            }

            SetupParts(root, inst.transform, model, length, verts);

            string path = WeaponPrefabs + "/" + model + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ================================================================== Moving parts (reload animation)

        /// <summary>Which sub-object of each model is the magazine / the pump (checked by rendering the parts).</summary>
        private static readonly Dictionary<string, string> MagParts = new Dictionary<string, string>
        {
            { "AK47", "Cube.003" }, { "SCAR", "Cube.002" }, { "Vector", "Cube.004" }, { "AWP", "Cube.004" },
            { "USP", "Cube.003" }, { "1911", "Cube.005" },
        };
        private static readonly Dictionary<string, string> PumpParts = new Dictionary<string, string> { { "PumpShotgun", "Cylinder" } };

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        private static void SetupParts(GameObject root, Transform inst, string model, float length, List<Vector3> allVerts)
        {
            var parts = root.AddComponent<WeaponParts>();
            parts.Pistol = length < 0.3f;
            // Parts get re-parented onto sockets, which a connected model instance doesn't allow.
            if (PrefabUtility.IsPartOfPrefabInstance(inst.gameObject))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(inst.gameObject), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var grip = root.transform.Find("Grip");
            float gripY = grip != null ? grip.localPosition.y : 0f;

            string magName;
            var mag = MagParts.TryGetValue(model, out magName) ? FindDeep(inst, magName) : null;
            if (mag != null)
            {
                var mv = Collect(mag, root.transform);
                if (mv.Count > 0)
                {
                    Vector3 c = Vector3.zero;
                    foreach (var v in mv) c += v;
                    c /= mv.Count;
                    Vector3 outDir;
                    if (parts.Pistol) outDir = new Vector3(0f, -0.95f, -0.3f).normalized;   // along the raked grip
                    else
                    {
                        // Principal axis of the mag (power iteration on the covariance), pointing down.
                        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
                        foreach (var v in mv)
                        {
                            var d = v - c;
                            xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
                        }
                        outDir = new Vector3(0.1f, -1f, 0.2f);
                        for (int i = 0; i < 30; i++)
                            outDir = new Vector3(xx * outDir.x + xy * outDir.y + xz * outDir.z,
                                                 xy * outDir.x + yy * outDir.y + yz * outDir.z,
                                                 xz * outDir.x + yz * outDir.y + zz * outDir.z).normalized;
                        outDir.x = 0f;
                        outDir.Normalize();
                        if (outDir.y > 0f) outDir = -outDir;
                    }
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var v in mv) { float t = Vector3.Dot(v - c, outDir); lo = Mathf.Min(lo, t); hi = Mathf.Max(hi, t); }
                    var socket = new GameObject("MagSocket").transform;
                    socket.SetParent(root.transform, false);
                    socket.localPosition = c + outDir * lo;
                    socket.localRotation = Quaternion.LookRotation(outDir, Vector3.forward);
                    mag.SetParent(socket, true);
                    float len = hi - lo;
                    if (parts.Pistol && len < 0.05f)
                    {
                        // Only the base plate is modelled: add the magazine body that hides inside the grip.
                        float w = 0.02f, dpt = 0.03f;
                        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        body.name = "MagBody";
                        Object.DestroyImmediate(body.GetComponent<Collider>());
                        body.transform.SetParent(socket, false);
                        body.transform.localPosition = new Vector3(0f, 0f, -0.045f);
                        body.transform.localScale = new Vector3(w, dpt, 0.09f);
                        body.GetComponent<Renderer>().sharedMaterial = _gunMetal;
                        len += 0.09f;
                    }
                    parts.MagSocket = socket;
                    parts.MagLength = Mathf.Max(0.05f, len);
                }
            }

            string pumpName;
            var pump = PumpParts.TryGetValue(model, out pumpName) ? FindDeep(inst, pumpName) : null;
            if (pump != null)
            {
                var ps = new GameObject("PumpSocket").transform;
                ps.SetParent(root.transform, false);
                pump.SetParent(ps, true);
                parts.PumpSocket = ps;
                parts.PumpTravel = 0.07f;
            }

            // Shell port: underside of the receiver just ahead of the trigger.
            float minY = float.MaxValue;
            foreach (var v in allVerts)
                if (v.z > 0.09f && v.z < 0.16f && v.y > gripY + 0.02f && Mathf.Abs(v.x) < 0.03f) minY = Mathf.Min(minY, v.y);
            parts.LoadPort = new Vector3(0f, minY < float.MaxValue ? minY - 0.005f : gripY + 0.05f, 0.12f);
        }

        // ================================================================== Combat knife (Asset Store: "Free Modern Combat Knife")

        /// <summary>Knife correction if the auto orientation guesses wrong: (flip tip/handle, flip edge/spine).</summary>
        public static (bool flipZ, bool flipY) KnifeFix = (false, false);
        public const float KnifeLength = 0.3f;

        /// <summary>Finds the imported knife (a prefab from the package if there is one, else its model file).</summary>
        public static GameObject FindKnifeSource(out string path)
        {
            path = "Assets/Combat Knife/2kblackblade.prefab"; // black blade, 2K textures
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) return go;
            foreach (var type in new[] { "t:Prefab", "t:Model" })
            {
                foreach (var guid in AssetDatabase.FindAssets(type))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.IndexOf("knife", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (p.StartsWith(WeaponPrefabs) || p.StartsWith("Assets/Scripts")) continue;
                    go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go == null || go.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                    path = p;
                    return go;
                }
            }
            path = null;
            return null;
        }

        private static void BuildKnifePrefab()
        {
            string srcPath;
            var src = FindKnifeSource(out srcPath);
            if (src == null)
            {
                Debug.Log("[VAMP] Combat knife model not found (import \"Free Modern Combat Knife\" from My Assets) - melee keeps the generated blade.");
                return;
            }
            if (srcPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) || srcPath.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase))
            {
                var mi = AssetImporter.GetAtPath(srcPath) as ModelImporter;
                if (mi != null && mi.importAnimation) { mi.importAnimation = false; mi.SaveAndReimport(); src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath); }
            }

            var root = new GameObject("Knife");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            if (inst == null) inst = Object.Instantiate(src);
            if (PrefabUtility.IsPartOfPrefabInstance(inst)) PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);

            var verts = Collect(inst.transform, root.transform);
            if (verts.Count == 0) { Object.DestroyImmediate(root); return; }

            // Longest extent → forward (Z), second (blade height) → up (Y), thinnest → X.
            var b = BoundsOf(verts);
            var ext = b.size;
            int[] order = { 0, 1, 2 };
            System.Array.Sort(order, (p, q) => ext[q].CompareTo(ext[p]));
            Rotate(inst.transform, verts, Quaternion.Inverse(Quaternion.LookRotation(Axis(order[0]), Axis(order[1]))));

            // Tip forward: the handle end is the thicker one (blade is flat).
            b = BoundsOf(verts);
            if (EndThickness(verts, b, true) > EndThickness(verts, b, false)) Rotate(inst.transform, verts, Quaternion.Euler(0f, 180f, 0f));
            if (KnifeFix.flipZ) Rotate(inst.transform, verts, Quaternion.Euler(0f, 180f, 0f));
            if (KnifeFix.flipY) Rotate(inst.transform, verts, Quaternion.Euler(0f, 0f, 180f));

            // Real-world length.
            b = BoundsOf(verts);
            float s = KnifeLength / Mathf.Max(0.0001f, b.size.z);
            inst.transform.localScale *= s;
            inst.transform.localPosition *= s;
            for (int i = 0; i < verts.Count; i++) verts[i] *= s;
            b = BoundsOf(verts);

            // Origin = middle of the handle (back 20%).
            Vector3 origin = new Vector3(b.center.x, b.center.y, b.min.z + b.size.z * 0.2f);
            inst.transform.localPosition -= origin;
            for (int i = 0; i < verts.Count; i++) verts[i] -= origin;
            b = BoundsOf(verts);

            Point(root, "Muzzle", new Vector3(0f, 0f, b.max.z));
            Point(root, "Grip", Vector3.zero);
            Point(root, "Offhand", new Vector3(-0.05f, -0.2f, -0.2f));

            // Materials: keep the pack's textures, converted to URP Lit when they use the built-in pipeline shaders.
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = UrpMaterial(mats[i]);
                r.sharedMaterials = mats;
            }

            string path = WeaponPrefabs + "/Knife.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[VAMP] Combat knife built from " + srcPath);
        }

        /// <summary>Mean X thickness of the vertices in the front (or back) 25% of the length.</summary>
        private static float EndThickness(List<Vector3> v, Bounds b, bool front)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            float cut = front ? b.max.z - b.size.z * 0.25f : b.min.z + b.size.z * 0.25f;
            foreach (var p in v)
            {
                if (front ? p.z < cut : p.z > cut) continue;
                lo = Mathf.Min(lo, p.x); hi = Mathf.Max(hi, p.x);
            }
            return hi > lo ? hi - lo : 0f;
        }

        private static Material UrpMaterial(Material m)
        {
            if (m == null) return _gunMetal;
            string sh = m.shader != null ? m.shader.name : "";
            if (sh.StartsWith("Universal Render Pipeline") || sh.StartsWith("Shader Graphs") || sh.StartsWith("VAMP")) return m;
            string path = ArtMaterials + "/Knife_" + m.name.Replace("/", "_") + ".mat";
            var conv = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = conv == null;
            if (create) conv = new Material(B.LitShader());
            Texture main = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            if (main == null && m.HasProperty("_BaseMap")) main = m.GetTexture("_BaseMap");
            conv.SetTexture("_BaseMap", main);
            conv.SetColor("_BaseColor", m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
            if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null)
            {
                conv.SetTexture("_BumpMap", m.GetTexture("_BumpMap"));
                conv.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap") != null)
            {
                conv.SetTexture("_MetallicGlossMap", m.GetTexture("_MetallicGlossMap"));
                conv.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (m.HasProperty("_OcclusionMap") && m.GetTexture("_OcclusionMap") != null)
            {
                conv.SetTexture("_OcclusionMap", m.GetTexture("_OcclusionMap"));
                conv.EnableKeyword("_OCCLUSIONMAP");
            }
            conv.SetFloat("_Metallic", m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0.6f);
            conv.SetFloat("_Smoothness", m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f);
            if (create) AssetDatabase.CreateAsset(conv, path);
            else EditorUtility.SetDirty(conv);
            return conv;
        }

        private static void Point(GameObject root, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root.transform, false);
            t.localPosition = pos;
        }

        private static Vector3 Axis(int i) { return i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward; }

        private static void Rotate(Transform model, List<Vector3> verts, Quaternion q)
        {
            model.localPosition = q * model.localPosition;
            model.localRotation = q * model.localRotation;
            for (int i = 0; i < verts.Count; i++) verts[i] = q * verts[i];
        }

        private static List<Vector3> Collect(Transform model, Transform space)
        {
            var list = new List<Vector3>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) list.Add(m.MultiplyPoint3x4(v));
            }
            foreach (var sm in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (sm.sharedMesh == null) continue;
                var m = space.worldToLocalMatrix * sm.transform.localToWorldMatrix;
                foreach (var v in sm.sharedMesh.vertices) list.Add(m.MultiplyPoint3x4(v));
            }
            return list;
        }

        private static Bounds BoundsOf(List<Vector3> v)
        {
            var b = new Bounds(v[0], Vector3.zero);
            for (int i = 1; i < v.Count; i++) b.Encapsulate(v[i]);
            return b;
        }

        /// <summary>Z extent of the vertices in the top (or bottom) 30% of the height.</summary>
        private static float ZSpread(List<Vector3> v, Bounds b, bool top)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            float cut = top ? b.max.y - b.size.y * 0.3f : b.min.y + b.size.y * 0.3f;
            foreach (var p in v)
            {
                if (top ? p.y < cut : p.y > cut) continue;
                lo = Mathf.Min(lo, p.z); hi = Mathf.Max(hi, p.z);
            }
            return hi > lo ? hi - lo : 0f;
        }

        private static float BandCenterZ(List<Vector3> v, Bounds b, bool top)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            float cut = top ? b.max.y - b.size.y * 0.3f : b.min.y + b.size.y * 0.3f;
            foreach (var p in v)
            {
                if (top ? p.y < cut : p.y > cut) continue;
                lo = Mathf.Min(lo, p.z); hi = Mathf.Max(hi, p.z);
            }
            return hi > lo ? (lo + hi) * 0.5f : b.center.z;
        }

        private static float MeanY(List<Vector3> v, float z0, float z1, float fallback)
        {
            float sum = 0f; int n = 0;
            foreach (var p in v) if (p.z >= z0 && p.z <= z1) { sum += p.y; n++; }
            return n > 0 ? sum / n : fallback;
        }

        private static float MaxY(List<Vector3> v, float z0, float z1, float fallback)
        {
            float m = float.MinValue;
            foreach (var p in v) if (p.z >= z0 && p.z <= z1) m = Mathf.Max(m, p.y);
            return m > float.MinValue ? m : fallback;
        }

        // ================================================================== Character

        private static void BuildCharacterPrefab()
        {
            if (AssetDatabase.LoadMainAssetAtPath(CharacterFbx) == null) { Debug.LogWarning("[VAMP] " + CharacterFbx + " missing - characters keep capsule placeholders."); return; }
            ConfigureImporter(CharacterFbx, true);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbx);
            if (src == null) return;

            var root = new GameObject("VampCharacter");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            foreach (var a in inst.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);

            var verts = Collect(inst.transform, root.transform);
            if (verts.Count > 0)
            {
                var b = BoundsOf(verts);
                float s = 1.8f / Mathf.Max(0.01f, b.size.y);
                inst.transform.localScale *= s;
                inst.transform.localPosition = new Vector3(-b.center.x * s, -b.min.y * s, -b.center.z * s);
            }
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.sharedMaterial = _stick;
                r.updateWhenOffscreen = true;
            }
            root.AddComponent<CharacterRig>();
            PrefabUtility.SaveAsPrefabAsset(root, CharacterPrefab);
            Object.DestroyImmediate(root);
        }
    }
}
