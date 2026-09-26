using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using B = Vamp.EditorTools.VampPrototypeBuilder;

namespace Vamp.EditorTools
{
    /// <summary>
    /// First-person hands and arms.
    ///  • Hands (Assets/Art/Hands/Hand_R.obj / Hand_L.obj): the "hand low poly" model, pre-posed into a grip.
    ///    Origin = centre of the gripped cylinder. Right: grip axis +Y, fingers wrap around the front (+Z), palm on the right.
    ///    Left: grip axis +Z (a handguard), palm up, fingers wrap up the right side, thumb along the left.
    ///    Saved as Resources/Hands/Hand_R|Hand_L with a glove material and a sleeve.
    ///  • Butterfly knife arms (Assets/Art/Arms/ButterflyKnife/ButterflyArms.fbx): "FPS Butterfly Knife" by BURNER
    ///    (Sketchfab, CC BY 4.0). One long take split into Legacy clips (Draw, Idle, InspectA, InspectB), saved as
    ///    Resources/ButterflyArms with the rig moved so its camera sits at the prefab origin, looking down +Z.
    /// </summary>
    public static class VampArmsBuilder
    {
        public const string HandsDir = "Assets/Art/Hands";
        public const string ArmsDir = "Assets/Art/Arms/ButterflyKnife";
        public const string ArmsFbx = ArmsDir + "/ButterflyArms.fbx";
        private const string Mats = VampArtBuilder.ArtMaterials;

        // Wrist of each posed hand (hand space, metres) - where the sleeve starts.
        private static readonly Vector3 WristR = new Vector3(0.016f, -0.015f, -0.126f);
        private static readonly Vector3 WristL = new Vector3(-0.126f, -0.022f, -0.015f);

        public static void Build()
        {
            B.EnsureFolder("Assets/Resources");
            B.EnsureFolder("Assets/Resources/Hands");
            BuildButterflyArms();
        }

        // ================================================================== Hands

        private static void BuildHands()
        {
            var glove = Mat("HandGlove", new Color(0.09f, 0.085f, 0.085f), 0f, 0.35f);
            var sleeve = Mat("HandSleeve", new Color(0.13f, 0.02f, 0.03f), 0f, 0.25f);
            BuildHand("Hand_R", glove, sleeve, WristR, new Vector3(0.1f, -0.25f, -1f));
            BuildHand("Hand_L", glove, sleeve, WristL, new Vector3(-0.7f, -0.35f, -0.6f));
        }

        private static void BuildHand(string name, Material glove, Material sleeve, Vector3 wrist, Vector3 armDir)
        {
            string path = HandsDir + "/" + name + ".obj";
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) { Debug.LogWarning("[VAMP] " + path + " missing - weapons are shown without hands."); return; }
            bool dirty = false;
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
            if (mi.importNormals != ModelImporterNormals.Calculate) { mi.importNormals = ModelImporterNormals.Calculate; dirty = true; }
            if (Mathf.Abs(mi.normalSmoothingAngle - 70f) > 0.1f) { mi.normalSmoothingAngle = 70f; dirty = true; }
            if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
            if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; dirty = true; }
            if (Mathf.Abs(mi.globalScale - 1f) > 0.0001f) { mi.globalScale = 1f; dirty = true; }
            if (mi.useFileScale) { mi.useFileScale = false; dirty = true; }
            if (dirty) mi.SaveAndReimport();

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Mesh) { mesh = (Mesh)o; break; }
            }
            if (mesh == null) { Debug.LogWarning("[VAMP] no mesh in " + path); return; }

            var root = new GameObject(name);
            var model = new GameObject("Hand");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = model.AddComponent<MeshRenderer>();
            r.sharedMaterial = glove;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            // Sleeve: a tapered tube from the wrist back out of view.
            var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            s.name = "Sleeve";
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.transform.SetParent(root.transform, false);
            Vector3 dir = armDir.normalized;
            const float len = 0.45f;
            s.transform.localPosition = wrist + dir * (len * 0.5f - 0.01f);
            s.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            s.transform.localScale = new Vector3(0.056f, len * 0.5f, 0.052f);
            var sr = s.GetComponent<Renderer>();
            sr.sharedMaterial = sleeve;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;

            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Hands/" + name + ".prefab");
            Object.DestroyImmediate(root);
        }

        // ================================================================== Butterfly knife arms

        private struct Clip
        {
            public string Name; public int First, Last; public WrapMode Wrap;
            public Clip(string n, int a, int b, WrapMode w) { Name = n; First = a; Last = b; Wrap = w; }
        }

        // 25 fps. The source is one continuous take: raise → closed spins (0-9 s) → flip open (9-11.4) → open holds and
        // tricks (11.4-22.4) → close (22.5) → two-handed stance (26.5-33.5).
        private static readonly Clip[] Clips =
        {
            new Clip("Draw", 218, 290, WrapMode.Once),      // closed in hand → flip open
            new Clip("Idle", 285, 300, WrapMode.PingPong),  // open hold
            new Clip("InspectA", 300, 455, WrapMode.Once),  // show-off poses and a flip
            new Clip("InspectB", 455, 535, WrapMode.Once),  // fast handle rolls
        };

        private static void BuildButterflyArms()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ArmsFbx) == null) { Debug.Log("[VAMP] " + ArmsFbx + " missing - butterfly knife keeps the procedural model."); return; }

            // Textures: normal maps flagged as such, 2K max.
            foreach (var n in new[] { "Textura", "Textura_Normal", "Ch21_Diffuse", "Ch21_Normal", "Balisong_Tex", "Balisong_Normal" })
            {
                var ti = AssetImporter.GetAtPath(ArmsDir + "/" + n + ".png") as TextureImporter;
                if (ti == null) continue;
                bool normal = n.EndsWith("Normal");
                bool d = false;
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; d = true; }
                if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; d = true; }
                if (d) ti.SaveAndReimport();
            }

            var mi = AssetImporter.GetAtPath(ArmsFbx) as ModelImporter;
            if (mi == null) return;
            bool dirty = false;
            if (mi.animationType != ModelImporterAnimationType.Legacy) { mi.animationType = ModelImporterAnimationType.Legacy; dirty = true; }
            if (mi.importAnimation) { mi.importAnimation = false; dirty = true; } // Unity finds no take in this file: clips are baked below
            if (mi.importCameras) { mi.importCameras = false; dirty = true; }
            if (mi.importLights) { mi.importLights = false; dirty = true; }
            if (mi.materialImportMode != ModelImporterMaterialImportMode.ImportStandard) { mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; dirty = true; }
            if (!mi.useFileScale) { mi.useFileScale = true; dirty = true; }
            if (mi.animationCompression != ModelImporterAnimationCompression.KeyframeReduction) { mi.animationCompression = ModelImporterAnimationCompression.KeyframeReduction; dirty = true; }
            if (dirty) mi.SaveAndReimport();

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsFbx);
            if (src == null) return;

            var armsMat = TexMat("ButterflyArms", ArmsDir + "/Textura.png", ArmsDir + "/Textura_Normal.png", 0f, 0.3f);
            var hairMat = TexMat("ButterflyArmsCh21", ArmsDir + "/Ch21_Diffuse.png", ArmsDir + "/Ch21_Normal.png", 0f, 0.3f);
            var knifeMat = TexMat("ButterflyKnife", ArmsDir + "/Balisong_Tex.png", ArmsDir + "/Balisong_Normal.png", 0.8f, 0.65f);

            var root = new GameObject("ButterflyArms");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Rig";
            inst.transform.SetParent(root.transform, false);

            // The rig's camera looks down -Z in model space: turn it round and put the camera at the origin.
            var cam = FindDeep(inst.transform, "Camera");
            Vector3 camPos = cam != null ? inst.transform.InverseTransformPoint(cam.position) : new Vector3(0f, 0f, 0.0474f);
            // Which way does the rig face after Unity's axis conversion? The right hand must end up in front of the camera.
            var hand = FindDeep(inst.transform, "mixamorig:RightHand");
            float handZ = hand != null ? inst.transform.InverseTransformPoint(hand.position).z - camPos.z : -1f;
            inst.transform.localRotation = handZ < 0f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            inst.transform.localPosition = -(inst.transform.localRotation * camPos);
            if (cam != null && cam.childCount == 0 && cam.GetComponents<Component>().Length == 1) Object.DestroyImmediate(cam.gameObject);

            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string mn = mats[i] != null ? mats[i].name : "";
                    mats[i] = mn.StartsWith("Balisong") ? knifeMat : mn.StartsWith("Ch21") ? hairMat : r.name == "Cylinder" ? knifeMat : armsMat;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = true;
                if (r.name == "Cylinder") r.gameObject.name = "KnifeMesh";
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;

            var anim = inst.GetComponent<Animation>();
            if (anim == null) anim = inst.AddComponent<Animation>();
            foreach (var clip in BakeClips(inst.transform))
                if (clip != null) anim.AddClip(clip, clip.name);
            anim.clip = anim.GetClip("Idle");
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.AlwaysAnimate;

            // Blade tip marker (follows the Seguro/blade bone if present).
            var blade = FindDeep(inst.transform, "Navaja");
            var tip = new GameObject("BladeTipPoint").transform;
            tip.SetParent(blade != null ? blade : root.transform, false);

            int clipCount = anim.GetClipCount();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/ButterflyArms.prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[VAMP] Butterfly knife arms built (" + clipCount + " clips, camera at " + camPos.ToString("F3") + ").");
        }

        // ================================================================== Baked clips

        private struct Key10 { public Vector3 T; public Quaternion R; public Vector3 S; }

        /// <summary>
        /// Unity's FBX importer finds no animation take in this file, so the animation is baked offline
        /// (ButterflyAnim.bytes: per-frame local TRS of every animated node, FBX axes) and converted here.
        /// The FBX→Unity axis mapping is measured from the rest pose rather than assumed.
        /// </summary>
        private static List<AnimationClip> BakeClips(Transform rig)
        {
            var result = new List<AnimationClip>();
            string bytesPath = ArmsDir + "/ButterflyAnim.bytes";
            if (!System.IO.File.Exists(bytesPath)) { Debug.LogWarning("[VAMP] " + bytesPath + " missing - butterfly knife has no animation."); return result; }
            var br = new System.IO.BinaryReader(new System.IO.MemoryStream(System.IO.File.ReadAllBytes(bytesPath)));
            if (new string(br.ReadChars(4)) != "VBA1") return result;
            int count = br.ReadInt32(), frames = br.ReadInt32();
            float fps = br.ReadSingle();
            var names = new string[count];
            var rest = new Key10[count];
            var data = new Key10[count, frames];
            for (int i = 0; i < count; i++)
            {
                names[i] = System.Text.Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                rest[i] = ReadKey(br);
                for (int f = 0; f < frames; f++) data[i, f] = ReadKey(br);
            }

            var bones = new Transform[count];
            for (int i = 0; i < count; i++) bones[i] = FindDeep(rig, names[i]);

            // Axis mapping: position p' = k * (sx,sy,sz)·p. Pick the signs/scale that best explain the rest pose.
            Vector3 bestSign = new Vector3(-1f, 1f, 1f);
            float bestK = 1f, bestErr = float.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                var sg = new Vector3((m & 1) != 0 ? -1f : 1f, (m & 2) != 0 ? -1f : 1f, (m & 4) != 0 ? -1f : 1f);
                float num = 0f, den = 0f;
                for (int i = 0; i < count; i++)
                {
                    if (bones[i] == null) continue;
                    var a = Vector3.Scale(sg, rest[i].T);
                    num += Vector3.Dot(a, bones[i].localPosition);
                    den += a.sqrMagnitude;
                }
                if (den < 1e-9f) continue;
                float k = num / den, err = 0f;
                for (int i = 0; i < count; i++)
                    if (bones[i] != null) err += (Vector3.Scale(sg, rest[i].T) * k - bones[i].localPosition).sqrMagnitude;
                if (err < bestErr) { bestErr = err; bestK = k; bestSign = sg; }
            }
            float det = bestSign.x * bestSign.y * bestSign.z;
            float rotErr = 0f;
            int found = 0;
            for (int i = 0; i < count; i++)
            {
                if (bones[i] == null) continue;
                found++;
                rotErr = Mathf.Max(rotErr, Quaternion.Angle(ConvRot(rest[i].R, bestSign, det), bones[i].localRotation));
            }
            Debug.Log("[VAMP] Butterfly anim: " + found + "/" + count + " bones, axis " + bestSign + " x" + bestK.ToString("G4") +
                      ", rest error pos " + Mathf.Sqrt(bestErr).ToString("G3") + " rot " + rotErr.ToString("F1") + "°");

            string clipDir = ArmsDir + "/Clips";
            B.EnsureFolder(clipDir);
            foreach (var c in Clips)
            {
                var clip = new AnimationClip { name = c.Name, legacy = true, frameRate = fps, wrapMode = c.Wrap };
                int last = Mathf.Min(c.Last, frames - 1);
                for (int i = 0; i < count; i++)
                {
                    if (bones[i] == null) continue;
                    string path = AnimationUtility.CalculateTransformPath(bones[i], rig);
                    int n = last - c.First + 1;
                    var px = new Keyframe[n]; var py = new Keyframe[n]; var pz = new Keyframe[n];
                    var qx = new Keyframe[n]; var qy = new Keyframe[n]; var qz = new Keyframe[n]; var qw = new Keyframe[n];
                    var sx = new Keyframe[n]; var sy = new Keyframe[n]; var sz = new Keyframe[n];
                    Quaternion prev = Quaternion.identity;
                    bool scaled = false;
                    for (int f = 0; f < n; f++)
                    {
                        var k = data[i, c.First + f];
                        float t = f / fps;
                        Vector3 p = Vector3.Scale(bestSign, k.T) * bestK;
                        Quaternion q = ConvRot(k.R, bestSign, det);
                        if (f > 0 && Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        prev = q;
                        px[f] = new Keyframe(t, p.x); py[f] = new Keyframe(t, p.y); pz[f] = new Keyframe(t, p.z);
                        qx[f] = new Keyframe(t, q.x); qy[f] = new Keyframe(t, q.y); qz[f] = new Keyframe(t, q.z); qw[f] = new Keyframe(t, q.w);
                        sx[f] = new Keyframe(t, k.S.x); sy[f] = new Keyframe(t, k.S.y); sz[f] = new Keyframe(t, k.S.z);
                        if (Mathf.Abs(k.S.x - 1f) + Mathf.Abs(k.S.y - 1f) + Mathf.Abs(k.S.z - 1f) > 0.001f) scaled = true;
                    }
                    Set(clip, path, "localPosition.x", px); Set(clip, path, "localPosition.y", py); Set(clip, path, "localPosition.z", pz);
                    Set(clip, path, "localRotation.x", qx); Set(clip, path, "localRotation.y", qy); Set(clip, path, "localRotation.z", qz); Set(clip, path, "localRotation.w", qw);
                    if (scaled) { Set(clip, path, "localScale.x", sx); Set(clip, path, "localScale.y", sy); Set(clip, path, "localScale.z", sz); }
                }
                clip.EnsureQuaternionContinuity();
                string clipPath = clipDir + "/" + c.Name + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; EditorUtility.SetDirty(existing); }
                else AssetDatabase.CreateAsset(clip, clipPath);
                result.Add(clip);
            }
            // GunPose: right hand from a tight fist (frame 330), left side from the two-handed stance (frame 700).
            {
                var clip = new AnimationClip { name = "GunPose", legacy = true, frameRate = fps, wrapMode = WrapMode.ClampForever };
                for (int i = 0; i < count; i++)
                {
                    if (bones[i] == null) continue;
                    bool leftSide = names[i].Contains("Left") || names[i].EndsWith("_l") || names[i].Contains(".l");
                    int f = Mathf.Min(leftSide ? 700 : 330, frames - 1);
                    var k = data[i, f];
                    string path = AnimationUtility.CalculateTransformPath(bones[i], rig);
                    Vector3 pp = Vector3.Scale(bestSign, k.T) * bestK;
                    Quaternion q = ConvRot(k.R, bestSign, det);
                    Set(clip, path, "localPosition.x", Two(pp.x)); Set(clip, path, "localPosition.y", Two(pp.y)); Set(clip, path, "localPosition.z", Two(pp.z));
                    Set(clip, path, "localRotation.x", Two(q.x)); Set(clip, path, "localRotation.y", Two(q.y)); Set(clip, path, "localRotation.z", Two(q.z)); Set(clip, path, "localRotation.w", Two(q.w));
                }
                string clipPath = clipDir + "/GunPose.anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; EditorUtility.SetDirty(existing); }
                else AssetDatabase.CreateAsset(clip, clipPath);
                result.Add(clip);
            }
            return result;
        }

        private static Keyframe[] Two(float v) { return new[] { new Keyframe(0f, v), new Keyframe(0.04f, v) }; }

        private static Key10 ReadKey(System.IO.BinaryReader br)
        {
            var k = new Key10();
            k.T = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            k.R = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            k.S = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            return k;
        }

        /// <summary>Rotation under the axis mapping S: R' = S R S (a reflection when det = -1).</summary>
        private static Quaternion ConvRot(Quaternion q, Vector3 sign, float det)
        {
            var v = Vector3.Scale(sign, new Vector3(q.x, q.y, q.z)) * det;
            return new Quaternion(v.x, v.y, v.z, q.w);
        }

        private static void Set(AnimationClip clip, string path, string prop, Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            clip.SetCurve(path, typeof(Transform), prop, curve);
        }

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

        // ================================================================== Materials

        private static Material Mat(string name, Color c, float metallic, float smooth)
        {
            string path = Mats + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(B.LitShader());
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = c;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material TexMat(string name, string albedo, string normal, float metallic, float smooth)
        {
            var mat = Mat(name, Color.white, metallic, smooth);
            var a = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
            if (a != null) { mat.SetTexture("_BaseMap", a); if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", a); }
            if (n != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
