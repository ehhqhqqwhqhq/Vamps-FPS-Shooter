using System.Collections.Generic;
using UnityEngine;

namespace Vamp.Weapons
{
    /// <summary>
    /// Fits attachment models onto a first-person gun (weapon root space: origin = grip, +Z = muzzle, metres).
    /// Optics sit on the top rail on the sight line, muzzle devices screw onto the Muzzle point (which then moves to
    /// their end), grips / lasers hang under the handguard, extended mags stretch the magazine.
    /// </summary>
    public static class AttachmentMount
    {
        private static Material _laserMat, _reticleMat, _tabMat;

        /// <summary>Returns true when an optic was fitted; lens = its aim point (root space).</summary>
        public static bool Fit(Transform root, WeaponData d, List<AttachmentDef> atts, out Vector3 lens)
        {
            lens = Vector3.zero;
            if (root == null || d == null || atts == null || atts.Count == 0) return false;
            bool optic = false;
            float lineX = -ArmsFitting.For(d.id).Ads.x;   // the gun's sight line (model centre may be off by a few mm)
            bool pistol = d.slot == WeaponSlot.Secondary;
            var sight = root.Find("Sight");
            var muzzle = root.Find("Muzzle");
            var off = root.Find("Offhand");
            Vector3 sightP = sight != null ? sight.localPosition : new Vector3(0f, 0.1f, 0.05f);
            Vector3 muzzleP = muzzle != null ? muzzle.localPosition : new Vector3(0f, 0.05f, 0.5f);
            Vector3 offP = off != null ? off.localPosition : new Vector3(0f, muzzleP.y - 0.03f, muzzleP.z * 0.5f);
            Material metal = DarkMetal();
            var parts = root.GetComponent<WeaponParts>();

            foreach (var a in atts)
            {
                switch (a.Slot)
                {
                    case AttachmentSlot.Optic:
                    {
                        var go = Spawn(a.Model, root);
                        if (go == null) break;
                        var rail = ArmsFitting.For(d.id).Rail;
                        go.transform.localPosition = rail != Vector2.zero ? new Vector3(lineX, rail.x, rail.y)
                                                                          : new Vector3(lineX, sightP.y - 0.002f, pistol ? 0.0f : 0.045f);
                        var l = go.transform.Find("Lens");
                        lens = l != null ? root.InverseTransformPoint(l.position) : go.transform.localPosition + new Vector3(0f, 0.03f, 0f);
                        lens.x = lineX;
                        optic = true;
                        break;
                    }
                    case AttachmentSlot.Muzzle:
                    {
                        var go = Spawn(a.Model, root);
                        if (go == null) break;
                        go.transform.localPosition = new Vector3(muzzleP.x, muzzleP.y, muzzleP.z - 0.012f);
                        var end = go.transform.Find("End");
                        if (muzzle != null && end != null) muzzle.localPosition = root.InverseTransformPoint(end.position) + new Vector3(0f, 0f, 0.01f);
                        break;
                    }
                    case AttachmentSlot.Underbarrel:
                    {
                        // Shotguns: the grip rides on the pump.
                        Transform host = parts != null && parts.PumpSocket != null ? parts.PumpSocket : root;
                        if (a.Laser) Laser(root, pistol ? new Vector3(lineX, muzzleP.y - 0.035f, muzzleP.z - 0.07f)
                                                       : new Vector3(lineX + 0.028f, muzzleP.y - 0.022f, muzzleP.z * 0.55f));
                        else if (a.Id == "att_vgrip")
                        {
                            var g = Part(PrimitiveType.Cylinder, host, root, new Vector3(lineX, offP.y - 0.075f, offP.z + 0.08f), new Vector3(0.03f, 0.045f, 0.03f), Quaternion.identity, metal);
                            g.name = "VerticalGrip";
                            Part(PrimitiveType.Cube, host, root, new Vector3(lineX, offP.y - 0.027f, offP.z + 0.08f), new Vector3(0.026f, 0.012f, 0.045f), Quaternion.identity, metal);
                        }
                        else
                        {
                            var g = Part(PrimitiveType.Cube, host, root, new Vector3(lineX, offP.y - 0.045f, offP.z + 0.07f), new Vector3(0.026f, 0.035f, 0.075f), Quaternion.Euler(-32f, 0f, 0f), metal);
                            g.name = "AngledGrip";
                        }
                        break;
                    }
                    case AttachmentSlot.Magazine:
                        if (parts != null && parts.MagSocket != null && a.Mag > 1f)
                        {
                            var sc = parts.MagSocket.localScale;
                            parts.MagSocket.localScale = new Vector3(sc.x, sc.y, sc.z * 1.4f); // the mag runs along the socket's +Z
                        }
                        else if (parts != null && parts.MagSocket != null && a.Reload < 1f && parts.MagSocket.childCount > 0)
                        {
                            // FAST MAG: a red pull tab on the base of the magazine.
                            var tab = Part(PrimitiveType.Cube, parts.MagSocket, parts.MagSocket, new Vector3(0f, 0f, parts.MagLength * 0.95f),
                                           new Vector3(0.03f, 0.012f, 0.02f), Quaternion.identity, Mat(ref _tabMat, new Color(0.8f, 0.06f, 0.08f), 1f));
                            tab.name = "FastMagTab";
                        }
                        break;
                }
            }
            return optic;
        }

        /// <summary>Glowing red dot at the lens (after camos are applied, so it stays red).</summary>
        public static void AddReticle(Transform root, Vector3 lens)
        {
            var dot = Part(PrimitiveType.Sphere, root, root, lens + new Vector3(0f, 0f, 0.02f), Vector3.one * 0.0022f, Quaternion.identity,
                           Mat(ref _reticleMat, new Color(1f, 0.05f, 0.08f), 4f));
            dot.name = "Reticle";
        }

        private static GameObject Spawn(string model, Transform root)
        {
            var prefab = string.IsNullOrEmpty(model) ? null : Resources.Load<GameObject>("Attachments/" + model);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, root, false);
            go.name = "ATT_" + model;
            foreach (var c in go.GetComponentsInChildren<Collider>()) { if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c); }
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.gameObject.layer = root.gameObject.layer;
            }
            return go;
        }

        private static GameObject Part(PrimitiveType type, Transform parent, Transform space, Vector3 posInSpace, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.position = space.TransformPoint(posInSpace);
            go.transform.rotation = space.rotation * rot;
            go.transform.localScale = scale;
            go.layer = space.gameObject.layer;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static Material _dark;
        private static Material DarkMetal()
        {
            if (_dark != null) return _dark;
            _dark = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "AttachmentMetal", color = new Color(0.07f, 0.07f, 0.08f) };
            if (_dark.HasProperty("_Metallic")) _dark.SetFloat("_Metallic", 0.7f);
            if (_dark.HasProperty("_Smoothness")) _dark.SetFloat("_Smoothness", 0.5f);
            return _dark;
        }

        private static Material FirstMaterial(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial != null) return r.sharedMaterial;
            return null;
        }

        private static Material Mat(ref Material cache, Color c, float emission)
        {
            if (cache != null) return cache;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            cache = new Material(sh) { color = c * Mathf.Max(1f, emission) };
            if (cache.HasProperty("_BaseColor")) cache.SetColor("_BaseColor", c * Mathf.Max(1f, emission));
            return cache;
        }

        private static void Laser(Transform root, Vector3 pos)
        {
            var body = Part(PrimitiveType.Cube, root, root, pos, new Vector3(0.022f, 0.022f, 0.06f), Quaternion.identity, DarkMetal());
            body.name = "Laser";
            var emitter = new GameObject("LaserEmitter").transform;
            emitter.SetParent(root, false);
            emitter.localPosition = pos + new Vector3(0f, 0f, 0.031f);
            var lb = emitter.gameObject.AddComponent<LaserBeam>();
            lb.Material = Mat(ref _laserMat, new Color(1f, 0.05f, 0.05f), 3f);
        }
    }
}
