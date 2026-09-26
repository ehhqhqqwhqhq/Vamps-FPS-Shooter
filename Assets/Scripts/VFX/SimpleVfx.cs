using System.Collections.Generic;
using UnityEngine;

namespace Vamp.VFX
{
    /// <summary>
    /// Prototype VFX: pooled tracers, impact sparks, muzzle flashes and explosion flashes built from
    /// primitives. Zero allocations per shot after warm-up. Phase 15 replaces this with VFX Graph /
    /// particle prefabs behind the same static API, respecting the EFFECT QUALITY setting.
    /// </summary>
    public sealed class SimpleVfx : MonoBehaviour
    {
        private sealed class Tracer
        {
            public LineRenderer Line;
            public float Age, Life, Width;
            public Color Color;
        }

        private sealed class Flash
        {
            public Transform Tr;
            public Renderer Renderer;
            public Material Mat;
            public float Age, Life, StartScale, EndScale;
            public Color Color;
        }

        private const int TracerPool = 32;
        private const int FlashPool = 64;

        /// <summary>Competitive visuals / low effect quality: shorter, smaller, dimmer effects.</summary>
        public static bool ReducedEffects;

        private static SimpleVfx _instance;
        private readonly List<Tracer> _tracers = new List<Tracer>();
        private readonly List<Flash> _flashes = new List<Flash>();
        private int _nextTracer, _nextFlash;
        private Material _lineMat;

        private static SimpleVfx Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[SimpleVfx]");
                    _instance = go.AddComponent<SimpleVfx>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public static Material CreateFxMaterial()
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            return new Material(s);
        }

        private void Build()
        {
            _lineMat = CreateFxMaterial();
            for (int i = 0; i < TracerPool; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = _lineMat;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                _tracers.Add(new Tracer { Line = lr });
            }

            for (int i = 0; i < FlashPool; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Flash";
                go.layer = 2; // Ignore Raycast
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                var r = go.GetComponent<Renderer>();
                var mat = CreateFxMaterial();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                _flashes.Add(new Flash { Tr = go.transform, Renderer = r, Mat = mat });
            }
        }

        // ------------------------------------------------------------------ API

        public static void TracerLine(Vector3 from, Vector3 to, Color color, float width = 0.03f, float life = 0.06f)
        {
            var self = Instance;
            var t = self._tracers[self._nextTracer];
            self._nextTracer = (self._nextTracer + 1) % self._tracers.Count;
            t.Age = 0f;
            t.Life = ReducedEffects ? life * 0.6f : life;
            t.Width = width;
            t.Color = color;
            t.Line.SetPosition(0, from);
            t.Line.SetPosition(1, to);
            t.Line.enabled = true;
            Apply(t, 0f);
        }

        public static void Impact(Vector3 position, Color color, float size = 0.18f)
        {
            SpawnFlash(position, color, size, size * 0.3f, 0.12f);
        }

        public static void MuzzleFlash(Vector3 position, float size = 0.25f)
        {
            SpawnFlash(position, new Color(1f, 0.55f, 0.35f, 0.9f), size, size * 1.6f, 0.05f);
        }

        public static void Explosion(Vector3 position, float radius)
        {
            SpawnFlash(position, new Color(1f, 0.25f, 0.15f, 0.85f), radius * 0.3f, radius * 2f, ReducedEffects ? 0.18f : 0.3f);
            if (!ReducedEffects) SpawnFlash(position, new Color(1f, 0.9f, 0.7f, 1f), radius * 0.2f, radius * 0.9f, 0.12f);
        }

        private static void SpawnFlash(Vector3 position, Color color, float startScale, float endScale, float life)
        {
            var self = Instance;
            var f = self._flashes[self._nextFlash];
            self._nextFlash = (self._nextFlash + 1) % self._flashes.Count;
            f.Age = 0f;
            f.Life = life;
            f.StartScale = startScale;
            f.EndScale = endScale;
            f.Color = color;
            f.Tr.position = position;
            f.Tr.gameObject.SetActive(true);
            Apply(f, 0f);
        }

        // ------------------------------------------------------------------ Update

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _tracers.Count; i++)
            {
                var t = _tracers[i];
                if (!t.Line.enabled) continue;
                t.Age += dt;
                if (t.Age >= t.Life) { t.Line.enabled = false; continue; }
                Apply(t, t.Age / t.Life);
            }
            for (int i = 0; i < _flashes.Count; i++)
            {
                var f = _flashes[i];
                if (!f.Tr.gameObject.activeSelf) continue;
                f.Age += dt;
                if (f.Age >= f.Life) { f.Tr.gameObject.SetActive(false); continue; }
                Apply(f, f.Age / f.Life);
            }
        }

        private static void Apply(Tracer t, float k)
        {
            Color c = t.Color;
            c.a *= 1f - k;
            t.Line.startColor = c;
            t.Line.endColor = c;
            float w = t.Width * (1f - k * 0.5f);
            t.Line.startWidth = w;
            t.Line.endWidth = w * 0.5f;
        }

        private static void Apply(Flash f, float k)
        {
            f.Tr.localScale = Vector3.one * Mathf.Lerp(f.StartScale, f.EndScale, k);
            Color c = f.Color;
            c.a *= 1f - k;
            f.Mat.color = c;
        }
    }
}
