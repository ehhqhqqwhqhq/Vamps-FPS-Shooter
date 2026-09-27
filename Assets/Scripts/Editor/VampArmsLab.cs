using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vamp.Weapons;
using B = Vamp.EditorTools.VampPrototypeBuilder;

namespace Vamp.EditorTools
{
    /// <summary>
    /// VAMP ▸ Arms Lab: lays out every weapon held by the first-person arms exactly as in game (hip position, IK,
    /// ArmsFitting corrections) and renders each one from the player's eye, the right side and the front-left into
    /// &lt;project&gt;/ArmsLab/&lt;weapon&gt;.png, for tuning ArmsFitting.Fits.
    /// </summary>
    public static class VampArmsLab
    {
        private const int W = 640, H = 360;

        [MenuItem("VAMP/Arms Lab", priority = 31)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var arms = Resources.Load<GameObject>("ButterflyArms");
            if (arms == null) { Debug.LogError("[VAMP] Arms Lab: Resources/ButterflyArms missing - run Build All Scenes first."); return; }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);

            var body = new Material(B.LitShader()) { color = new Color(0.16f, 0.16f, 0.17f) };
            var accent = new Material(B.LitShader()) { color = new Color(0.8f, 0.1f, 0.12f) };

            var weapons = new List<WeaponData>();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponData"))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (w != null && w.id != "balisong" && !weapons.Exists(x => x.id == w.id)) weapons.Add(w);
            }
            weapons.Sort((a, b) => a.slot != b.slot ? a.slot.CompareTo(b.slot) : string.CompareOrdinal(a.id, b.id));

            string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ArmsLab");
            Directory.CreateDirectory(outDir);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var cam = new GameObject("LabCamera").AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.33f, 0.35f, 0.38f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;

            int i = 0;
            var names = new List<string>();
            var log = new List<string>();
            foreach (var d in weapons)
            {
                var eye = new GameObject("Eye_" + d.id).transform;
                eye.position = new Vector3(i * 4f, 1.6f, 0f);
                i++;

                GameObject root;
                bool placeholder = d.viewModelPrefab == null;
                if (!placeholder) root = (GameObject)PrefabUtility.InstantiatePrefab(d.viewModelPrefab);
                else
                {
                    root = new GameObject("VM_" + d.id);
                    WeaponViewModel.BuildPlaceholder(root.transform, d, body, accent);
                }
                root.transform.SetParent(eye, false);
                root.transform.localPosition = ArmsFitting.Hip(d, placeholder, new Vector3(0.2f, -0.2f, 0.42f));
                root.transform.localRotation = d.delivery == DeliveryType.Melee ? Quaternion.Euler(-22f, -28f, -35f) : Quaternion.identity;

                var a = (GameObject)Object.Instantiate(arms, eye, false);
                a.name = "Arms";
                var knife = Find(a.transform, "KnifeMesh");
                if (knife != null) knife.gameObject.SetActive(false);
                GloveSkins.Apply(a, "glove_tactical");
                // Several renders per editor frame: make skinning follow every pose change.
                foreach (var smr in a.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
                var fp = FirstPersonArms.Create(a);
                Vector3 rp, lp; Quaternion rr, lr; bool left;
                ArmsFitting.Compute(root.transform, d, out rp, out rr, out left, out lp, out lr);
                fp.Solve(root.transform, rp, rr, left, lp, lr);
                log.Add(d.id + ": " + fp.Describe(root.transform, false) + " | " + fp.Describe(root.transform, true));
                if (Markers)
                {
                    Marker(root.transform, rp, rr, new Color(1f, 0.1f, 0.1f));
                    if (left) Marker(root.transform, lp, lr, new Color(0.1f, 1f, 0.2f));
                }

                // Three views: eye (game FOV 90 horizontal), right side, front-left.
                var sheet = new Texture2D(W * 2, H * 2, TextureFormat.RGB24, false);
                Shot(cam, rt, sheet, 0, 1, eye.position, eye.rotation, 58.7f);
                Vector3 focus = root.transform.TransformPoint(new Vector3(0f, -0.02f, 0.1f));
                Vector3 side = focus + eye.right * 0.8f + eye.forward * 0.02f;
                Shot(cam, rt, sheet, 1, 1, side, Quaternion.LookRotation(focus - side), 40f);
                Vector3 lfocus = left ? root.transform.TransformPoint(lp) : focus;
                Vector3 lside = lfocus - eye.right * 0.35f - eye.up * 0.12f;
                Shot(cam, rt, sheet, 0, 0, lside, Quaternion.LookRotation(lfocus - lside, eye.up), 34f);
                Vector3 gripW = root.transform.TransformPoint(rp);
                Vector3 close = gripW + eye.right * 0.35f + eye.up * 0.05f - eye.forward * 0.05f;
                Shot(cam, rt, sheet, 1, 0, close, Quaternion.LookRotation(gripW - close, eye.up), 30f);
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(outDir, d.id + ".png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                names.Add(d.id);

                // Reload strip: four moments of the reload from the player's eye (+ a wider view of the same moment).
                var parts = root.GetComponent<WeaponParts>();
                if (parts != null && d.delivery != DeliveryType.Melee && !d.usesHeat && left)
                {
                    var rs = new Texture2D(W * 4, H * 2, TextureFormat.RGB24, false);
                    float[] ps = d.reloadPerShell ? new[] { 0.2f, 0.55f, 0.8f, -1f } : new[] { 0.1f, 0.24f, 0.5f, 0.66f };
                    Vector3 hip = root.transform.localPosition;
                    Quaternion baseRot = root.transform.localRotation;
                    GameObject shell = null;
                    for (int k = 0; k < ps.Length; k++)
                    {
                        ReloadAnim.Pose pose;
                        float pump = 0f;
                        if (ps[k] < 0f) { pose = ReloadAnim.Idle(lp, lr); pump = 1f; }
                        else pose = d.reloadPerShell ? ReloadAnim.Shell(parts, ps[k], 1f, lp, lr) : ReloadAnim.Magazine(parts, ps[k], lp, lr);
                        parts.SetPump(pump);
                        if (pump > 0f) pose.LeftPos += Vector3.back * parts.PumpTravel;
                        root.transform.localPosition = hip + pose.GunPos;
                        root.transform.localRotation = baseRot * Quaternion.Euler(pose.GunEuler);
                        parts.SetMag(pose.MagOut, pose.MagExtra, pose.MagExtraEuler, pose.MagVisible);
                        if (shell == null)
                        {
                            shell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            Object.DestroyImmediate(shell.GetComponent<Collider>());
                            shell.GetComponent<Renderer>().sharedMaterial = accent;
                            shell.transform.SetParent(root.transform, false);
                            shell.transform.localScale = new Vector3(0.019f, 0.032f, 0.019f);
                            shell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        }
                        shell.SetActive(pose.ShellVisible);
                        shell.transform.localPosition = pose.ShellPos;
                        fp.Solve(root.transform, rp, rr, true, pose.LeftPos, pose.LeftRot);
                        log.Add(d.id + " reload p=" + ps[k] + " magTop " + parts.MagTop.ToString("F3") + " out " + parts.MagOutDir.ToString("F2") + " len " + parts.MagLength.ToString("F3")
                                + " magOut " + pose.MagOut.ToString("F3") + " left " + pose.LeftPos.ToString("F3") + " rest " + lp.ToString("F3") + " port " + parts.LoadPort.ToString("F3")
                                + " | " + fp.Describe(root.transform, true));
                        Shot(cam, rt, rs, k, 1, eye.position, eye.rotation, 58.7f);
                        Vector3 f2 = root.transform.TransformPoint(new Vector3(0f, -0.05f, 0.1f));
                        Vector3 wide = f2 - eye.right * 0.55f + eye.up * 0.05f - eye.forward * 0.1f;
                        Shot(cam, rt, rs, k, 0, wide, Quaternion.LookRotation(f2 - wide, eye.up), 40f);
                    }
                    rs.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, d.id + "_reload.png"), rs.EncodeToPNG());
                    Object.DestroyImmediate(rs);
                    root.transform.localPosition = hip;
                    root.transform.localRotation = baseRot;
                    parts.ResetParts();
                    if (shell != null) shell.SetActive(false);
                }

                // Muzzle flash preview (hip pose): player view + a close side view of the muzzle.
                if (d.delivery != DeliveryType.Melee && (d.id == "havoc" || d.id == "v9" || d.id == "brute" || d.id == "widow"))
                {
                    var mz = root.transform.Find("Muzzle");
                    if (mz != null)
                    {
                        Vamp.VFX.MuzzleFx.Play(mz.position, root.transform.forward, d.pelletsPerShot > 1 ? 0.5f : d.slot == WeaponSlot.Secondary ? 0.24f : 0.34f);
                        var ft = new Texture2D(W * 2, H, TextureFormat.RGB24, false);
                        Shot(cam, rt, ft, 0, 0, eye.position, eye.rotation, 58.7f);
                        Vector3 side2 = mz.position + eye.right * 0.6f + eye.forward * 0.15f;
                        Shot(cam, rt, ft, 1, 0, side2, Quaternion.LookRotation(mz.position + eye.forward * 0.15f - side2, eye.up), 40f);
                        ft.Apply();
                        File.WriteAllBytes(Path.Combine(outDir, d.id + "_flash.png"), ft.EncodeToPNG());
                        Object.DestroyImmediate(ft);
                        Vamp.VFX.MuzzleFx.DestroyPool();
                    }
                }

                // Aim down sights: the sights must sit on the crosshair (screen centre).
                if (d.delivery != DeliveryType.Melee && d.canAim)
                {
                    Vector3 ads = placeholder ? new Vector3(0f, -0.13f, 0.32f) + ArmsFitting.For(d.id).Ads
                                              : ArmsFitting.Ads(root.transform, d, new Vector3(0f, -0.13f, 0.32f));
                    Vector3 hipKeep = root.transform.localPosition;
                    root.transform.localPosition = ads;
                    root.transform.localRotation = Quaternion.identity;
                    var partsA = root.GetComponent<WeaponParts>();
                    if (partsA != null) partsA.ResetParts();
                    fp.Solve(root.transform, rp, rr, left, lp, lr);
                    var at = new Texture2D(W * 2, H, TextureFormat.RGB24, false);
                    Shot(cam, rt, at, 0, 0, eye.position, eye.rotation, 58.7f);
                    Shot(cam, rt, at, 1, 0, eye.position, eye.rotation, 14f);
                    for (int c = 0; c < 2; c++)
                        for (int k = -14; k <= 14; k++)
                        {
                            if (Mathf.Abs(k) < 3) continue;
                            at.SetPixel(c * W + W / 2 + k, H / 2, Color.green);
                            at.SetPixel(c * W + W / 2, H / 2 + k, Color.green);
                        }
                    at.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, d.id + "_ads.png"), at.EncodeToPNG());
                    Object.DestroyImmediate(at);
                    root.transform.localPosition = hipKeep;
                }
            }
            File.WriteAllLines(Path.Combine(outDir, "log.txt"), log.ToArray());
            cam.targetTexture = null;
            Object.DestroyImmediate(cam.gameObject);
            rt.Release();
            Debug.Log("[VAMP] Arms Lab: rendered " + names.Count + " weapons (" + string.Join(", ", names.ToArray()) + ") to " + outDir);
        }

        public static bool Markers = true;

        private static void Marker(Transform parent, Vector3 pos, Quaternion rot, Color c)
        {
            var m = new Material(B.LitShader()) { color = c };
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = Vector3.one * 0.012f;
            g.GetComponent<Renderer>().sharedMaterial = m;
            // Grip axis (frame up) as a thin rod
            var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(r.GetComponent<Collider>());
            r.transform.SetParent(parent, false);
            r.transform.localPosition = pos;
            r.transform.localRotation = rot;
            r.transform.localScale = new Vector3(0.004f, 0.05f, 0.004f);
            r.GetComponent<Renderer>().sharedMaterial = m;
        }

        private static void Shot(Camera cam, RenderTexture rt, Texture2D sheet, int col, int row, Vector3 pos, Quaternion rot, float fov)
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, W, H), col * W, row * H);
            RenderTexture.active = prev;
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = Find(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
