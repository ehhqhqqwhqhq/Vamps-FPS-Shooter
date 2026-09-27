using UnityEngine;

namespace Vamp.Weapons
{
    /// <summary>The tactical laser's beam: runs from the emitter to whatever it hits (max 60 m), with a dot there.</summary>
    public sealed class LaserBeam : MonoBehaviour
    {
        public Material Material;
        private LineRenderer _line;
        private Transform _dot;
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private void Start()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.sharedMaterial = Material;
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.startWidth = 0.004f;
            _line.endWidth = 0.012f;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dot.GetComponent<Collider>());
            dot.name = "LaserDot";
            dot.GetComponent<Renderer>().sharedMaterial = Material;
            dot.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dot.transform.localScale = Vector3.one * 0.05f;
            _dot = dot.transform;
        }

        private void LateUpdate()
        {
            if (_line == null) return;
            Vector3 from = transform.position, dir = transform.forward;
            float dist = 60f;
            bool hit = false;
            int n = Physics.RaycastNonAlloc(from, dir, Hits, 60f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].collider.transform.root == transform.root) continue;
                if (Hits[i].distance < dist) { dist = Hits[i].distance; hit = true; }
            }
            Vector3 end = from + dir * dist;
            _line.SetPosition(0, from);
            _line.SetPosition(1, end);
            if (_dot != null)
            {
                _dot.gameObject.SetActive(hit && gameObject.activeInHierarchy);
                _dot.position = end - dir * 0.02f;
            }
        }

        private void OnDisable() { if (_dot != null) _dot.gameObject.SetActive(false); }
        private void OnDestroy() { if (_dot != null) Destroy(_dot.gameObject); }
    }
}
