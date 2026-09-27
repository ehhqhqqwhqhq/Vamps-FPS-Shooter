using System.Collections.Generic;
using UnityEngine;

namespace Vamp.VFX
{
    /// <summary>
    /// Muzzle flashes built from the Kenney Particle Pack (CC0) sprites in Resources/VFX/Kenney
    /// (muzzle_0x, spark_0x, smoke_0x, flare_01); procedural stand-ins if the pack isn't imported.
    /// Each shot: two crossed flash cards along the barrel + a camera-facing star, a short light pop,
    /// a few stretched sparks and a smoke puff. Everything pooled, no allocations per shot.
    /// </summary>
    public sealed class MuzzleFx : MonoBehaviour
    {
        private sealed class Flash
        {
            public Transform Root, Front;
            public Renderer A, B, C;
            public float Age, Life, Size;
        }

        private static MuzzleFx _instance;
        private readonly List<Flash> _flashes = new List<Flash>();
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<float> _lightAge = new List<float>();
        private int _nextFlash, _nextLight;
        private Material[] _flashMats;
        private Material _starMat;
        private ParticleSystem _sparks, _smoke;
        private Mesh _quad, _quadBack;
        public static bool UsingKenney { get; private set; }

        private static MuzzleFx Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[MuzzleFx]");
                    if (Application.isPlaying) DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MuzzleFx>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; }

        /// <summary>Editor preview (Arms Lab): remove the pool so the next shot rebuilds it.</summary>
        public static void DestroyPool()
        {
            if (_instance != null) Object.DestroyImmediate(_instance.gameObject);
            _instance = null;
        }

        /// <summary>Flash at the muzzle, pointing along forward. size ≈ flash length in metres.</summary>
        public static void Play(Vector3 position, Vector3 forward, float size)
        {
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            Instance.Spawn(position, forward.normalized, size);
        }

        // ------------------------------------------------------------------ Setup

        private static Texture2D Load(string name) { return Resources.Load<Texture2D>("VFX/Kenney/" + name); }

        private static Material Mat(Texture tex, Color tint, bool additive)
        {
            var sh = Shader.Find("VAMP/Particle");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(sh) { mainTexture = tex };
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", tint);
            if (m.HasProperty("_SrcBlend"))
            {
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            }
            m.renderQueue = 3100;
            return m;
        }

        private void Build()
        {
            var flashTex = new List<Texture2D>();
            for (int i = 1; i <= 5; i++) { var t = Load("muzzle_0" + i); if (t != null) flashTex.Add(t); }
            UsingKenney = flashTex.Count > 0;
            if (!UsingKenney) flashTex.Add(ProceduralFlash());
            var star = Load("flare_01") ?? Load("star_07") ?? Load("star_06") ?? ProceduralStar();
            var spark = Load("spark_05") ?? Load("spark_01") ?? ProceduralDot();
            var smoke = Load("smoke_07") ?? Load("smoke_04") ?? ProceduralSmoke();

            var flashTint = new Color(2.2f, 1.45f, 0.8f, 1f);
            _flashMats = new Material[flashTex.Count];
            for (int i = 0; i < flashTex.Count; i++) _flashMats[i] = Mat(flashTex[i], flashTint, true);
            _starMat = Mat(star, new Color(2.4f, 1.7f, 1f, 1f), true);

            _quad = QuadMesh(false);
            _quadBack = QuadMesh(true);
            for (int i = 0; i < 12; i++) _flashes.Add(NewFlash());
            for (int i = 0; i < 4; i++)
            {
                var l = new GameObject("FlashLight").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.72f, 0.4f);
                l.range = 5f;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                _lights.Add(l);
                _lightAge.Add(1f);
            }

            _sparks = System("Sparks", Mat(spark, new Color(2.5f, 1.6f, 0.8f, 1f), true), true);
            _smoke = System("Smoke", Mat(smoke, new Color(0.55f, 0.55f, 0.58f, 0.35f), false), false);
        }

        /// <summary>Unit quad in XY, pivot at the left edge (x = 0) so it grows forward from the muzzle.</summary>
        private static Mesh QuadMesh(bool pivotLeft)
        {
            var m = new Mesh { name = pivotLeft ? "FlashCard" : "FlashQuad" };
            float x0 = pivotLeft ? 0f : -0.5f, x1 = pivotLeft ? 1f : 0.5f;
            m.vertices = new[] { new Vector3(x0, -0.5f, 0f), new Vector3(x1, -0.5f, 0f), new Vector3(x1, 0.5f, 0f), new Vector3(x0, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        private Renderer Card(Transform parent, Mesh mesh, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private Flash NewFlash()
        {
            var f = new Flash { Root = new GameObject("Flash").transform };
            f.Root.SetParent(transform, false);
            // Card A/B: side-on flash along the barrel (+Z of the root), 90° apart around the barrel.
            var pivotA = new GameObject("A").transform; pivotA.SetParent(f.Root, false); pivotA.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var pivotB = new GameObject("B").transform; pivotB.SetParent(f.Root, false); pivotB.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, -90f, 0f);
            f.A = Card(pivotA, _quadBack, "CardA");
            f.B = Card(pivotB, _quadBack, "CardB");
            f.Front = new GameObject("Front").transform; f.Front.SetParent(f.Root, false);
            f.C = Card(f.Front, _quad, "Star");
            f.C.sharedMaterial = _starMat;
            f.Root.gameObject.SetActive(false);
            f.Life = 1f; f.Age = 1f;
            return f;
        }

        private ParticleSystem System(string name, Material mat, bool sparks)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = sparks ? 300 : 120;
            main.gravityModifier = sparks ? 0.6f : -0.05f;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(sparks ? 1f : 0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            if (!sparks)
            {
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (sparks)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.012f;   // stretched sparks trail behind their position,
                r.lengthScale = 1f;         // so keep them short and spawn them ahead of the muzzle
            }
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ Spawn / update

        private void Spawn(Vector3 pos, Vector3 fwd, float size)
        {
            bool reduced = SimpleVfx.ReducedEffects;
            var f = _flashes[_nextFlash];
            _nextFlash = (_nextFlash + 1) % _flashes.Count;
            f.Age = 0f;
            f.Life = Random.Range(0.045f, 0.065f);
            f.Size = size * Random.Range(0.85f, 1.2f);
            f.Root.position = pos;
            f.Root.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            var mat = _flashMats[Random.Range(0, _flashMats.Length)];
            f.A.sharedMaterial = mat;
            f.B.sharedMaterial = mat;
            f.Root.gameObject.SetActive(true);
            Pose(f, 0f);

            var l = _lights[_nextLight];
            _lightAge[_nextLight] = 0f;
            _nextLight = (_nextLight + 1) % _lights.Count;
            l.transform.position = pos + fwd * size * 0.4f;
            l.enabled = !reduced;

            var p = new ParticleSystem.EmitParams();
            int n = reduced ? 2 : Random.Range(4, 8);
            for (int i = 0; i < n; i++)
            {
                p.position = pos + fwd * Random.Range(0.12f, 0.2f);
                p.velocity = (fwd + Random.insideUnitSphere * 0.35f).normalized * Random.Range(6f, 14f);
                p.startLifetime = Random.Range(0.06f, 0.16f);
                p.startSize = Random.Range(0.012f, 0.025f);
                p.startColor = Color.white;
                _sparks.Emit(p, 1);
            }
            if (!reduced)
            {
                p.position = pos + fwd * 0.05f;
                p.velocity = fwd * Random.Range(0.6f, 1.2f) + Vector3.up * 0.25f;
                p.startLifetime = Random.Range(0.5f, 0.8f);
                p.startSize = size * Random.Range(0.6f, 0.9f);
                p.rotation = Random.Range(0f, 360f);
                p.startColor = Color.white;
                _smoke.Emit(p, 1);
            }
        }

        private void Pose(Flash f, float t)
        {
            float grow = t < 0.3f ? Mathf.Lerp(0.6f, 1f, t / 0.3f) : Mathf.Lerp(1f, 0.7f, (t - 0.3f) / 0.7f);
            float len = f.Size * grow, wid = f.Size * 0.55f * grow;
            f.A.transform.localScale = new Vector3(len, wid, 1f);
            f.B.transform.localScale = new Vector3(len, wid, 1f);
            var cam = Camera.main;
            if (cam != null) f.Front.rotation = Quaternion.LookRotation(f.Front.position - cam.transform.position, cam.transform.up);
            float s = f.Size * 0.6f * grow;
            f.Front.localScale = new Vector3(s, s, s);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _flashes.Count; i++)
            {
                var f = _flashes[i];
                if (!f.Root.gameObject.activeSelf) continue;
                f.Age += dt;
                if (f.Age >= f.Life) { f.Root.gameObject.SetActive(false); continue; }
                Pose(f, f.Age / f.Life);
            }
            for (int i = 0; i < _lights.Count; i++)
            {
                if (!_lights[i].enabled) continue;
                _lightAge[i] += dt;
                float t = _lightAge[i] / 0.06f;
                if (t >= 1f) { _lights[i].enabled = false; continue; }
                _lights[i].intensity = 6f * (1f - t);
            }
        }

        // ------------------------------------------------------------------ Procedural stand-ins

        private static Texture2D Tex(int size, System.Func<float, float, float> f)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    byte a = (byte)(Mathf.Clamp01(f(u, v)) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        private static Texture2D ProceduralFlash()
        {
            return Tex(128, (u, v) =>
            {
                float x = (u + 1f) * 0.5f;                        // 0 at the muzzle end
                float width = 0.55f * (1f - x) * (1f - x) + 0.05f;
                float body = Mathf.Clamp01(1f - Mathf.Abs(v) / width);
                return body * body * Mathf.Clamp01(x * 6f) * (1f - x * 0.6f);
            });
        }

        private static Texture2D ProceduralStar()
        {
            return Tex(128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 3f)), 12f) * Mathf.Clamp01(1f - r);
                return Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - r * 1.6f), 2f) + spikes);
            });
        }

        private static Texture2D ProceduralDot() { return Tex(32, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), 2f)); }

        private static Texture2D ProceduralSmoke()
        {
            return Tex(64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float n = Mathf.PerlinNoise(u * 3f + 5f, v * 3f + 5f);
                return Mathf.Clamp01((1f - r) * (0.6f + 0.6f * n));
            });
        }
    }
}
