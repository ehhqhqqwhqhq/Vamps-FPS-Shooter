using UnityEngine;

namespace Vamp.Weapons
{
    /// <summary>
    /// Procedural BUTTERFLY KNIFE (balisong): a blade and two handles that swing around the pivot pins, so it can do
    /// real flip openings / tricks. Origin = middle of the grip, blade towards +Z (same convention as imported guns).
    /// Angles are around the X axis at the pin: blade 0 = open (pointing forward), 180 = closed between the handles.
    /// The latch handle swings a full turn around the pin during a flip; the safe handle stays in the hand.
    /// </summary>
    public sealed class BalisongModel : MonoBehaviour
    {
        public const float HandleLength = 0.125f;
        public const float BladeLength = 0.11f;
        private static readonly Vector3 Pin = new Vector3(0f, 0.012f, 0.055f);

        private Transform _blade, _latch, _safe;
        private static Material _steel, _handle, _inlay;

        public Transform Tip { get; private set; }

        public static BalisongModel Build(Transform parent)
        {
            if (_steel == null)
            {
                _steel = Mat("BalisongBlade", new Color(0.78f, 0.8f, 0.84f), 0.95f, 0.85f);
                _handle = Mat("BalisongHandle", new Color(0.07f, 0.07f, 0.08f), 0.4f, 0.55f);
                _inlay = Mat("BalisongInlay", new Color(0.75f, 0.05f, 0.1f), 0.3f, 0.6f);
            }
            var go = new GameObject("Balisong");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BalisongModel>();
            b.Create();
            return b;
        }

        private static Material Mat(string name, Color c, float metallic, float smooth)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh) { name = name, color = c };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        private static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default(Vector3))
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.layer = 2;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = size;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        private Transform Pivot(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.localPosition = Pin;
            return t;
        }

        private void Create()
        {
            // Blade (tang at the pin, edge down, tip forward)
            _blade = Pivot("BladePivot");
            Box(_blade, "Blade", new Vector3(0f, 0.002f, BladeLength * 0.5f), new Vector3(0.0028f, 0.02f, BladeLength), _steel);
            Box(_blade, "BladeTip", new Vector3(0f, 0.004f, BladeLength + 0.008f), new Vector3(0.0026f, 0.014f, 0.022f), _steel, new Vector3(28f, 0f, 0f));
            Box(_blade, "BladeSpine", new Vector3(0f, 0.0125f, BladeLength * 0.45f), new Vector3(0.0032f, 0.003f, BladeLength * 0.8f), _steel);
            Box(_blade, "BladeTang", new Vector3(0f, 0f, 0f), new Vector3(0.004f, 0.016f, 0.012f), _steel);
            Tip = new GameObject("Muzzle").transform;
            Tip.SetParent(_blade, false);
            Tip.localPosition = new Vector3(0f, 0f, BladeLength + 0.02f);

            // Handles: channel-shaped, one each side of the blade, with a red inlay and a pin cap.
            _safe = Pivot("SafeHandle");
            BuildHandle(_safe, 0.0082f);
            _latch = Pivot("LatchHandle");
            BuildHandle(_latch, -0.0082f);
            Box(_latch, "Latch", new Vector3(0f, -0.004f, -HandleLength + 0.004f), new Vector3(0.018f, 0.004f, 0.01f), _steel);
        }

        private void BuildHandle(Transform pivot, float x)
        {
            Box(pivot, "Handle", new Vector3(x, 0f, -HandleLength * 0.5f), new Vector3(0.007f, 0.026f, HandleLength), _handle);
            Box(pivot, "Inlay", new Vector3(x + Mathf.Sign(x) * 0.0037f, 0f, -HandleLength * 0.5f), new Vector3(0.0012f, 0.012f, HandleLength * 0.8f), _inlay);
            Box(pivot, "PinCap", new Vector3(x + Mathf.Sign(x) * 0.004f, 0f, 0f), new Vector3(0.002f, 0.008f, 0.008f), _steel);
        }

        /// <summary>blade: 0 open ... 180 closed. latch: extra swing of the latch handle (0 = in the hand).</summary>
        public void SetPose(float bladeDeg, float latchDeg)
        {
            if (_blade == null) return;
            _blade.localRotation = Quaternion.Euler(-bladeDeg, 0f, 0f);
            _latch.localRotation = Quaternion.Euler(latchDeg, 0f, 0f);
            _safe.localRotation = Quaternion.identity;
        }
    }
}
