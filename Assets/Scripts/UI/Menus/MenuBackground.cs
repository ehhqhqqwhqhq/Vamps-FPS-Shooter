using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// Animated 3D menu backdrop built at runtime: dark industrial VAMP facility with machinery, pipes, moving red
    /// lights, drifting smoke, sparks and a slow camera move. Kept cheap (a few dozen primitives, two particle systems).
    /// </summary>
    public sealed class MenuBackground : MonoBehaviour
    {
        private Camera _cam;
        private readonly List<Light> _sweepers = new List<Light>();
        private readonly List<Transform> _fans = new List<Transform>();
        private Transform _root;
        private Light _warning;
        private float _t;

        private void Awake()
        {
            _root = new GameObject("MenuBackground").transform;
            bool day = RenderSettings.skybox != null; // daytime scene (VAMP ▸ Build All Scenes): open-air yard, no roof
            var matDark = day ? Mat(new Color(0.42f, 0.42f, 0.44f), 0.1f, 0.3f) : Mat(new Color(0.08f, 0.08f, 0.09f), 0.3f, 0.35f);
            var matMetal = Mat(new Color(0.18f, 0.18f, 0.2f), 0.8f, 0.55f);
            var matRed = Mat(new Color(0.6f, 0.03f, 0.07f), 0f, 0.5f, new Color(2.2f, 0.08f, 0.15f));
            var matWhite = Mat(new Color(0.9f, 0.9f, 0.9f), 0f, 0.5f, new Color(1.2f, 1.2f, 1.3f));

            // Room
            Box(new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f), matDark);
            if (day)
            {
                Box(new Vector3(0f, 8f, 18f), new Vector3(60f, 16f, 1f), matDark);
                Box(new Vector3(-18f, 2.5f, 0f), new Vector3(1f, 5f, 60f), matDark);
                Box(new Vector3(18f, 2.5f, 0f), new Vector3(1f, 5f, 60f), matDark);
            }
            else
            {
                Box(new Vector3(0f, 12f, 18f), new Vector3(60f, 26f, 1f), matDark);
                Box(new Vector3(-18f, 12f, 0f), new Vector3(1f, 26f, 60f), matDark);
                Box(new Vector3(18f, 12f, 0f), new Vector3(1f, 26f, 60f), matDark);
                Box(new Vector3(0f, 25f, 0f), new Vector3(60f, 1f, 60f), matDark);
            }

            // Catwalks, pillars, pipes, machinery
            Box(new Vector3(0f, 6f, 10f), new Vector3(30f, 0.4f, 3f), matMetal);
            Box(new Vector3(0f, 7f, 8.6f), new Vector3(30f, 0.08f, 0.08f), matMetal);
            for (int i = -2; i <= 2; i++)
            {
                Box(new Vector3(i * 7f, 6f, 14f), new Vector3(1.2f, 12f, 1.2f), matMetal);
                var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Prep(pipe, matMetal);
                pipe.transform.position = new Vector3(i * 7f + 2f, 11f, 16.5f);
                pipe.transform.localScale = new Vector3(0.6f, 11f, 0.6f);
            }
            for (int i = 0; i < 3; i++)
            {
                var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Prep(pipe, matMetal);
                pipe.transform.position = new Vector3(0f, (day ? 12f : 16f) + i * 1.4f, 16f);
                pipe.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                pipe.transform.localScale = new Vector3(0.45f, 30f, 0.45f);
            }
            Box(new Vector3(-9f, 2f, 6f), new Vector3(5f, 4f, 3f), matMetal);
            Box(new Vector3(-9f, 4.2f, 6f), new Vector3(5.2f, 0.2f, 3.2f), matRed);
            Box(new Vector3(10f, 1.5f, 4f), new Vector3(3f, 3f, 3f), matMetal);
            Box(new Vector3(12.5f, 3f, 7f), new Vector3(2f, 6f, 2f), matMetal);

            // Industrial fans
            for (int i = 0; i < 2; i++)
            {
                var fan = new GameObject("Fan").transform;
                fan.SetParent(_root, false);
                fan.position = new Vector3(-6f + i * 12f, day ? 12.5f : 16f, 17.4f);
                for (int b = 0; b < 4; b++)
                {
                    var blade = Box(Vector3.zero, new Vector3(0.6f, 3.2f, 0.1f), matMetal);
                    blade.transform.SetParent(fan, false);
                    blade.transform.localRotation = Quaternion.Euler(0f, 0f, b * 45f);
                }
                _fans.Add(fan);
            }

            // Floor + wall light strips (red emissive) and white warning strips
            for (int i = 0; i < 6; i++) Box(new Vector3(-15f + i * 6f, 0.02f, 2f), new Vector3(4f, 0.02f, 0.2f), matRed);
            Box(new Vector3(0f, 9f, 17.45f), new Vector3(28f, 0.15f, 0.05f), matRed);
            Box(new Vector3(0f, 3f, 17.45f), new Vector3(28f, 0.08f, 0.05f), matWhite);

            // Big VAMP sign on the back wall
            var sign = new GameObject("Sign");
            sign.transform.SetParent(_root, false);
            sign.transform.position = new Vector3(4f, 12.5f, 17.3f);
            var tm = sign.AddComponent<TextMesh>();
            tm.text = "V A M P";
            tm.font = UIFactory.Font;
            tm.fontSize = 120;
            tm.characterSize = 0.18f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(0.8f, 0.05f, 0.1f, 0.9f);
            sign.GetComponent<MeshRenderer>().sharedMaterial = UIFactory.Font.material;

            // Lighting - the scene provides a daytime sky + sun (VAMP ▸ Build All Scenes); fall back to the dark look otherwise.
            if (!day)
            {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.06f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.025f);
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.skybox = null;
            }

            for (int i = 0; i < 3; i++)
            {
                var l = new GameObject("Sweeper").AddComponent<Light>();
                l.transform.SetParent(_root, false);
                l.type = LightType.Spot;
                l.color = new Color(1f, 0.1f, 0.14f);
                l.intensity = 60f;
                l.range = 40f;
                l.spotAngle = 38f;
                l.transform.position = new Vector3(-10f + i * 10f, 22f, 6f);
                _sweepers.Add(l);
            }
            _warning = new GameObject("Warning").AddComponent<Light>();
            _warning.transform.SetParent(_root, false);
            _warning.type = LightType.Point;
            _warning.color = new Color(1f, 0.2f, 0.1f);
            _warning.range = 14f;
            _warning.transform.position = new Vector3(-9f, 6f, 5f);

            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.enabled = !day;
            fill.transform.SetParent(_root, false);
            fill.type = LightType.Directional;
            fill.color = new Color(0.6f, 0.65f, 0.8f);
            fill.intensity = 0.25f;
            fill.transform.rotation = Quaternion.Euler(35f, 160f, 0f);

            // Smoke + sparks
            Particles("Smoke", new Vector3(0f, 1f, 8f), new Color(0.35f, 0.35f, 0.4f, 0.08f), 6f, 0.4f, 3f, 12f, 30, new Vector3(30f, 1f, 16f));
            Particles("Sparks", new Vector3(-9f, 4.4f, 6f), new Color(1f, 0.5f, 0.2f, 1f), 1.2f, 4f, 0.06f, 0.08f, 0, new Vector3(0.5f, 0.1f, 0.5f), true);
            Particles("Dust", new Vector3(0f, 6f, 6f), new Color(1f, 1f, 1f, 0.25f), 12f, 0.15f, 0.04f, 0.06f, 60, new Vector3(30f, 12f, 20f));

            // Camera (reuse the scene's main camera if any)
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Menu Camera");
                go.tag = "MainCamera";
                _cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            _cam.clearFlags = day ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.01f, 0.01f, 0.012f);
            _cam.fieldOfView = 55f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = day ? 1000f : 200f;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            // Slow camera drift
            float s = Mathf.Sin(_t * 0.08f);
            _cam.transform.position = new Vector3(-3f + s * 4f, 3.2f + Mathf.Sin(_t * 0.13f) * 0.4f, -12f + Mathf.Cos(_t * 0.07f) * 1.5f);
            _cam.transform.rotation = Quaternion.LookRotation(new Vector3(2f + s * 2f, 6.5f, 14f) - _cam.transform.position);

            for (int i = 0; i < _sweepers.Count; i++)
            {
                float a = _t * 0.5f + i * 2.1f;
                _sweepers[i].transform.rotation = Quaternion.Euler(70f + Mathf.Sin(a) * 18f, Mathf.Cos(a * 0.8f) * 35f, 0f);
            }
            foreach (var f in _fans) f.Rotate(0f, 0f, 90f * Time.unscaledDeltaTime, Space.Self);
            _warning.intensity = (Mathf.Sin(_t * 4f) > 0.2f) ? 6f : 0.5f;
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        // ------------------------------------------------------------------ Helpers

        private static Texture2D _softDot;

        private static Texture2D SoftDot()
        {
            if (_softDot != null) return _softDot;
            const int n = 64;
            _softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    _softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            _softDot.Apply();
            return _softDot;
        }

        private static Material Mat(Color c, float metallic, float smooth, Color? emission = null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            return m;
        }

        private GameObject Box(Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Prep(go, m);
            go.transform.position = pos;
            go.transform.localScale = size;
            return go;
        }

        private void Prep(GameObject go, Material m)
        {
            go.transform.SetParent(_root, false);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        private void Particles(string name, Vector3 pos, Color color, float lifetime, float speed, float sizeMin, float sizeMax,
                               int rate, Vector3 box, bool bursty = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = bursty ? 1.2f : -0.01f;
            var em = ps.emission;
            em.rateOverTime = rate;
            if (bursty)
            {
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12, 20, 1000, 1.7f) });
            }
            var shape = ps.shape;
            shape.shapeType = bursty ? ParticleSystemShapeType.Hemisphere : ParticleSystemShapeType.Box;
            shape.scale = box;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = VFX.SimpleVfx.CreateFxMaterial();
            mat.color = Color.white;
            mat.mainTexture = SoftDot();
            r.sharedMaterial = mat;
            ps.Play();
        }
    }
}
