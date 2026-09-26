using System;
using System.Collections.Generic;
using UnityEngine;
using Vamp.Combat;
using Vamp.Progression;
using Vamp.Weapons;

namespace Vamp.VFX
{
    /// <summary>
    /// Cosmetic effects: KILL FX (played where your victim dies) and BULLET TRAILS (how your tracers look).
    /// Pure visuals built from pooled particle systems - never affect gameplay.
    /// Online, other players' equipped effects arrive as small indices (see <see cref="ToIndex"/>).
    /// </summary>
    public static class CosmeticFx
    {
        /// <summary>Returns the kill effect id for a killer (the local player → your equipped one). Online adds proxies.</summary>
        public static Func<GameObject, string> KillFxResolver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { KillFxResolver = null; _systems.Clear(); _root = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            KillFeed.EntryAdded -= OnKill;
            KillFeed.EntryAdded += OnKill;
        }

        private static void OnKill(KillFeedEntry e)
        {
            if (e.Suicide || e.VictimObject == null || e.KillerObject == null) return;
            string id = null;
            if (KillFxResolver != null) id = KillFxResolver(e.KillerObject);
            if (string.IsNullOrEmpty(id) && Player.PlayerController.Local != null && e.KillerObject == Player.PlayerController.Local.gameObject)
                id = LocalKillFx;
            if (string.IsNullOrEmpty(id) || id == "kfx_none") return;
            PlayKill(id, e.VictimObject.transform.position + Vector3.up * 1.1f);
        }

        public static string LocalKillFx
        {
            get
            {
                var p = Core.Game.Progression;
                return p != null && p.Profile != null ? p.Profile.kill_effect : null;
            }
        }

        public static string LocalTrail
        {
            get
            {
                var p = Core.Game.Progression;
                return p != null && p.Profile != null ? p.Profile.weapon_trail : null;
            }
        }

        // ------------------------------------------------------------------ Network indices

        public static byte ToIndex(CosmeticType type, string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            var list = CosmeticCatalog.OfType(type);
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return (byte)(i + 1);
            return 0;
        }

        public static string FromIndex(CosmeticType type, byte index)
        {
            var list = CosmeticCatalog.OfType(type);
            return index > 0 && index <= list.Count ? list[index - 1].Id : null;
        }

        // ------------------------------------------------------------------ Particle pools

        private sealed class Sys
        {
            public ParticleSystem Ps;
        }

        private static readonly Dictionary<string, Sys> _systems = new Dictionary<string, Sys>();
        private static Transform _root;
        private static Texture2D _dot, _skull;

        private static Texture2D Dot()
        {
            if (_dot != null) return _dot;
            const int n = 32;
            _dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FxDot" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    _dot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            _dot.Apply();
            return _dot;
        }

        private static Texture2D SkullTex()
        {
            if (_skull == null) _skull = Resources.Load<Texture2D>("Fx/Skull");
            return _skull != null ? _skull : Dot();
        }

        /// <summary>kind: "spark" (stretched), "fall" (stretched, gravity), "glow" (soft blobs), "rise" (blobs floating up),
        /// "puff" (blobs that grow - smoke / gas), "skull".</summary>
        private static ParticleSystem Get(string kind)
        {
            Sys s;
            if (_systems.TryGetValue(kind, out s) && s.Ps != null) return s.Ps;
            if (_root == null)
            {
                var r = new GameObject("[CosmeticFx]");
                UnityEngine.Object.DontDestroyOnLoad(r);
                _root = r.transform;
            }
            var go = new GameObject("Fx_" + kind);
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.gravityModifier = kind == "fall" ? 1.2f : kind == "rise" ? -0.35f : 0f;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = kind != "skull";
            size.size = kind == "puff"
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.6f))
                : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = kind == "spark" || kind == "fall";
            drag.drag = 2.5f;

            var pr = go.GetComponent<ParticleSystemRenderer>();
            var mat = SimpleVfx.CreateFxMaterial();
            mat.mainTexture = kind == "skull" ? SkullTex() : Dot();
            pr.sharedMaterial = mat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            if (kind == "spark" || kind == "fall")
            {
                pr.renderMode = ParticleSystemRenderMode.Stretch;
                pr.velocityScale = 0.06f;
                pr.lengthScale = 1.5f;
            }
            else pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sortingFudge = kind == "skull" ? -10f : 0f;
            ps.Play();
            _systems[kind] = new Sys { Ps = ps };
            return ps;
        }

        private static void Emit(string kind, Vector3 pos, Vector3 vel, Color color, float size, float life)
        {
            var ps = Get(kind);
            var ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startColor = color,
                startSize = size,
                startLifetime = life,
                applyShapeToPosition = false
            };
            ps.Emit(ep, 1);
        }

        private static float R(float a, float b) { return UnityEngine.Random.Range(a, b); }

        // ------------------------------------------------------------------ Kill effects

        public static void PlayKill(string id, Vector3 p)
        {
            bool reduced = SimpleVfx.ReducedEffects;
            int n = reduced ? 1 : 2;
            switch (id)
            {
                case "kfx_red_burst":
                    SimpleVfx.Impact(p, new Color(1f, 0.1f, 0.12f, 0.9f), 0.9f);
                    for (int i = 0; i < 30 * n; i++)
                        Emit("spark", p, UnityEngine.Random.onUnitSphere * R(6f, 14f), new Color(1f, R(0.05f, 0.25f), 0.1f), R(0.05f, 0.12f), R(0.3f, 0.7f));
                    break;

                case "kfx_bats":
                    for (int i = 0; i < 18 * n; i++)
                    {
                        Vector3 v = new Vector3(R(-1f, 1f), R(0.6f, 1.6f), R(-1f, 1f)).normalized * R(3f, 7f);
                        Emit("glow", p + UnityEngine.Random.insideUnitSphere * 0.4f, v, new Color(0.05f, 0.03f, 0.05f, 1f), R(0.18f, 0.32f), R(0.8f, 1.4f));
                    }
                    for (int i = 0; i < 10 * n; i++)
                        Emit("spark", p, UnityEngine.Random.onUnitSphere * R(3f, 6f), new Color(0.8f, 0.05f, 0.1f), 0.06f, 0.5f);
                    break;

                case "kfx_shatter":
                    SimpleVfx.Impact(p, new Color(1f, 1f, 1f, 0.8f), 0.7f);
                    for (int i = 0; i < 40 * n; i++)
                    {
                        Vector3 v = UnityEngine.Random.onUnitSphere * R(3f, 8f);
                        v.y = Mathf.Abs(v.y) + 2f;
                        Emit("fall", p + UnityEngine.Random.insideUnitSphere * 0.5f, v, new Color(R(0.8f, 1f), R(0.85f, 1f), 1f), R(0.05f, 0.14f), R(0.8f, 1.3f));
                    }
                    break;

                case "kfx_crimson_eruption":
                    SimpleVfx.Explosion(p, 2.2f);
                    Emit("glow", p, Vector3.zero, new Color(1f, 0.15f, 0.1f, 0.9f), 2.6f, 0.35f);
                    Emit("glow", p, Vector3.zero, new Color(1f, 0.85f, 0.7f, 1f), 1.1f, 0.2f);
                    for (int i = 0; i < 70 * n; i++)
                    {
                        Vector3 v = new Vector3(R(-1f, 1f), R(0.3f, 2.2f), R(-1f, 1f)).normalized * R(7f, 18f);
                        Emit("fall", p, v, new Color(1f, R(0.02f, 0.35f), R(0.02f, 0.1f)), R(0.06f, 0.16f), R(0.6f, 1.4f));
                    }
                    for (int i = 0; i < 36; i++)
                    {
                        float a = i / 36f * Mathf.PI * 2f;
                        Vector3 dir = new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a));
                        Emit("spark", p - Vector3.up * 0.8f, dir * 11f, new Color(1f, 0.2f, 0.15f), 0.12f, 0.45f); // shock ring
                    }
                    break;

                case "kfx_soul_reap":
                    Emit("skull", p + Vector3.up * 0.4f, Vector3.up * 1.4f, new Color(1f, 0.2f, 0.22f, 1f), 1.5f, 1.3f);
                    Emit("glow", p + Vector3.up * 0.4f, Vector3.up * 1.4f, new Color(1f, 0.05f, 0.1f, 0.6f), 2.8f, 1.1f);
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * Mathf.PI * 2f;
                        Vector3 at = p - Vector3.up * 0.9f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.1f;
                        Emit("spark", at, Vector3.up * R(9f, 16f), new Color(1f, 0.1f, 0.15f), 0.14f, R(0.35f, 0.6f)); // spikes
                    }
                    for (int i = 0; i < 40 * n; i++)
                        Emit("glow", p + UnityEngine.Random.insideUnitSphere * 0.6f, (UnityEngine.Random.insideUnitSphere + Vector3.up) * R(1f, 3f),
                             new Color(1f, R(0f, 0.2f), 0.1f, 0.9f), R(0.08f, 0.2f), R(0.8f, 1.6f));
                    break;

                case "kfx_frost":
                    SimpleVfx.Impact(p, new Color(0.7f, 0.9f, 1f, 0.9f), 0.8f);
                    for (int i = 0; i < 45 * n; i++)
                    {
                        Vector3 v = UnityEngine.Random.onUnitSphere * R(2f, 7f);
                        v.y = Mathf.Abs(v.y) + 1.5f;
                        Emit("fall", p + UnityEngine.Random.insideUnitSphere * 0.5f, v, new Color(R(0.6f, 0.9f), R(0.85f, 1f), 1f), R(0.06f, 0.16f), R(0.9f, 1.5f));
                    }
                    for (int i = 0; i < 20; i++) Emit("glow", p + UnityEngine.Random.insideUnitSphere * 0.8f, Vector3.down * 0.5f, new Color(0.8f, 0.95f, 1f, 0.7f), R(0.05f, 0.1f), R(1f, 2f));
                    break;

                case "kfx_gold":
                    Emit("glow", p, Vector3.zero, new Color(1f, 0.85f, 0.3f, 0.9f), 1.8f, 0.3f);
                    for (int i = 0; i < 60 * n; i++)
                    {
                        Vector3 v = new Vector3(R(-1f, 1f), R(1f, 2.5f), R(-1f, 1f)).normalized * R(5f, 12f);
                        Emit("fall", p, v, new Color(1f, R(0.7f, 0.9f), R(0.1f, 0.35f)), R(0.05f, 0.12f), R(0.8f, 1.6f));
                    }
                    for (int i = 0; i < 20; i++) Emit("glow", p + UnityEngine.Random.insideUnitSphere * 1.2f, Vector3.zero, new Color(1f, 0.95f, 0.6f, 1f), R(0.05f, 0.12f), R(0.4f, 1f));
                    break;

                case "kfx_void":
                    for (int i = 0; i < 50 * n; i++)
                    {
                        Vector3 d = UnityEngine.Random.onUnitSphere;
                        Emit("spark", p + d * 2.4f, -d * 5f, new Color(0.6f, R(0.1f, 0.3f), 1f), 0.07f, 0.45f); // sucked in
                    }
                    Emit("glow", p, Vector3.zero, new Color(0.05f, 0f, 0.1f, 1f), 1.4f, 0.6f);
                    Emit("glow", p, Vector3.zero, new Color(0.6f, 0.1f, 1f, 0.8f), 2.8f, 0.5f);
                    for (int i = 0; i < 40 * n; i++)
                        Emit("spark", p, UnityEngine.Random.onUnitSphere * R(8f, 16f), new Color(0.75f, 0.3f, 1f), R(0.05f, 0.1f), R(0.3f, 0.6f));
                    break;

                case "kfx_toxic":
                    for (int i = 0; i < 26 * n; i++)
                        Emit("puff", p + UnityEngine.Random.insideUnitSphere * 0.7f, (UnityEngine.Random.insideUnitSphere + Vector3.up * 0.4f) * R(0.5f, 1.5f),
                             new Color(R(0.2f, 0.4f), 1f, R(0.1f, 0.3f), 0.45f), R(0.8f, 1.4f), R(1.2f, 2.2f));
                    for (int i = 0; i < 20 * n; i++)
                        Emit("fall", p, UnityEngine.Random.onUnitSphere * R(2f, 5f), new Color(0.4f, 1f, 0.2f), 0.06f, R(0.6f, 1.1f));
                    break;

                case "kfx_thunder":
                {
                    Vector3 top = p + Vector3.up * 14f + new Vector3(R(-1.5f, 1.5f), 0f, R(-1.5f, 1.5f));
                    Vector3 prev = top;
                    for (int s = 1; s <= 8; s++)
                    {
                        Vector3 pt = Vector3.Lerp(top, p, s / 8f) + (s < 8 ? new Vector3(R(-0.8f, 0.8f), 0f, R(-0.8f, 0.8f)) : Vector3.zero);
                        SimpleVfx.TracerLine(prev, pt, new Color(0.7f, 0.85f, 1f, 1f), 0.14f, 0.25f);
                        SimpleVfx.TracerLine(prev, pt, new Color(1f, 1f, 1f, 1f), 0.05f, 0.2f);
                        prev = pt;
                    }
                    SimpleVfx.Impact(p, new Color(0.7f, 0.85f, 1f, 1f), 1.6f);
                    Emit("glow", p, Vector3.zero, new Color(0.5f, 0.7f, 1f, 0.9f), 3f, 0.3f);
                    for (int i = 0; i < 40 * n; i++)
                        Emit("spark", p, UnityEngine.Random.onUnitSphere * R(6f, 14f), new Color(0.6f, 0.8f, 1f), R(0.04f, 0.09f), R(0.2f, 0.5f));
                    break;
                }

                case "kfx_bloodmoon":
                    Emit("glow", p, Vector3.zero, new Color(0.9f, 0.05f, 0.1f, 0.8f), 2.2f, 0.6f);
                    for (int i = 0; i < 48; i++)
                    {
                        float a = i / 48f * Mathf.PI * 2f;
                        Emit("spark", p, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 8f, new Color(1f, 0.05f, 0.1f), 0.12f, 0.5f);
                    }
                    for (int i = 0; i < 14 * n; i++)
                        Emit("rise", p + UnityEngine.Random.insideUnitSphere * 0.8f, Vector3.up * R(0.5f, 2f), new Color(0.9f, 0.02f, 0.08f, 1f), R(0.15f, 0.3f), R(1.2f, 2f));
                    break;

                case "kfx_phoenix":
                    Emit("glow", p, Vector3.zero, new Color(1f, 0.6f, 0.1f, 0.9f), 2f, 0.35f);
                    for (int i = 0; i < 60 * n; i++)
                        Emit("rise", p - Vector3.up * 0.8f + new Vector3(R(-0.5f, 0.5f), 0f, R(-0.5f, 0.5f)), Vector3.up * R(4f, 9f) + UnityEngine.Random.insideUnitSphere,
                             new Color(1f, R(0.25f, 0.7f), R(0f, 0.1f), 1f), R(0.15f, 0.4f), R(0.6f, 1.2f));
                    for (int i = 0; i < 30 * n; i++)
                        Emit("spark", p, new Vector3(R(-1f, 1f), R(0.5f, 2f), R(-1f, 1f)).normalized * R(6f, 12f), new Color(1f, 0.8f, 0.3f), 0.05f, R(0.4f, 0.8f));
                    break;

                case "kfx_ghost":
                    for (int i = 0; i < 10; i++)
                        Emit("rise", p + UnityEngine.Random.insideUnitSphere * 0.5f, Vector3.up * R(1f, 2.5f) + UnityEngine.Random.insideUnitSphere * 0.4f,
                             new Color(0.9f, 0.95f, 1f, 0.55f), R(0.5f, 0.9f), R(1.5f, 2.5f));
                    for (int i = 0; i < 30 * n; i++)
                        Emit("glow", p + UnityEngine.Random.insideUnitSphere * 0.8f, Vector3.up * R(0.5f, 2f), new Color(0.85f, 0.95f, 1f, 0.8f), R(0.05f, 0.12f), R(1f, 2f));
                    break;
            }
        }

        // ------------------------------------------------------------------ Bullet trails

        /// <summary>Draws one tracer in the style of <paramref name="trailId"/> (null / trail_none = the weapon's normal tracer).</summary>
        public static void Tracer(Vector3 from, Vector3 to, WeaponData d, string trailId, int pellets)
        {
            float width = pellets > 1 ? 0.015f : 0.025f;
            Color baseColor = d != null ? d.tracerColor : new Color(1f, 0.9f, 0.7f, 0.8f);
            if (string.IsNullOrEmpty(trailId) || trailId == "trail_none")
            {
                SimpleVfx.TracerLine(from, to, baseColor, width);
                return;
            }
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.01f) return;
            Vector3 n = dir / len;
            bool reduced = SimpleVfx.ReducedEffects;
            int budget = pellets > 1 ? 3 : 10;
            if (reduced) budget = Mathf.Max(1, budget / 3);

            switch (trailId)
            {
                case "trail_ember":
                    SimpleVfx.TracerLine(from, to, new Color(1f, 0.55f, 0.15f, 0.95f), width * 1.3f, 0.09f);
                    for (int i = 0; i < budget; i++)
                    {
                        Vector3 at = from + n * R(0f, len);
                        Emit("fall", at, UnityEngine.Random.insideUnitSphere * 1.5f + Vector3.up, new Color(1f, R(0.3f, 0.6f), 0.1f), 0.04f, R(0.3f, 0.6f));
                    }
                    break;

                case "trail_phantom":
                    SimpleVfx.TracerLine(from, to, new Color(1f, 0.08f, 0.12f, 1f), width * 1.4f, 0.12f);
                    for (int i = 0; i < budget * 2; i++)
                    {
                        Vector3 at = from + n * R(0f, len);
                        Vector3 v = (UnityEngine.Random.onUnitSphere * 2.5f) + n * R(2f, 6f);
                        Emit("spark", at, v, new Color(1f, R(0.05f, 0.3f), 0.15f), R(0.03f, 0.07f), R(0.2f, 0.45f));
                    }
                    break;

                case "trail_razor":
                {
                    // A curved crimson slash: two offset arcs + electric sparks.
                    Vector3 side = Vector3.Cross(n, Vector3.up);
                    if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                    side.Normalize();
                    float bend = Mathf.Min(0.6f, len * 0.04f);
                    Vector3 prev = from;
                    const int segs = 6;
                    for (int s = 1; s <= segs; s++)
                    {
                        float t = s / (float)segs;
                        Vector3 pt = from + dir * t + side * Mathf.Sin(t * Mathf.PI) * bend;
                        SimpleVfx.TracerLine(prev, pt, new Color(1f, 0.12f, 0.2f, 1f), width * 1.8f, 0.14f);
                        prev = pt;
                    }
                    SimpleVfx.TracerLine(from, to, new Color(1f, 0.75f, 0.8f, 0.9f), width * 0.6f, 0.08f);
                    for (int i = 0; i < budget; i++)
                    {
                        Vector3 at = from + n * R(0f, len);
                        Emit("spark", at, UnityEngine.Random.onUnitSphere * R(3f, 7f), new Color(1f, R(0.4f, 0.8f), R(0.6f, 0.9f)), 0.035f, R(0.12f, 0.25f));
                    }
                    break;
                }


                case "trail_void":
                    SimpleVfx.TracerLine(from, to, new Color(0.75f, 0.3f, 1f, 1f), width * 3.2f, 0.14f);
                    SimpleVfx.TracerLine(from, to, new Color(0.08f, 0f, 0.15f, 1f), width * 1.1f, 0.14f);
                    for (int i = 0; i < budget; i++)
                        Emit("glow", from + n * R(0f, len), UnityEngine.Random.insideUnitSphere * 0.6f, new Color(0.7f, 0.25f, 1f, 0.9f), R(0.08f, 0.18f), R(0.25f, 0.5f));
                    break;

                case "trail_gold":
                    SimpleVfx.TracerLine(from, to, new Color(1f, 0.82f, 0.25f, 1f), width * 1.4f, 0.1f);
                    for (int i = 0; i < budget; i++)
                        Emit("glow", from + n * R(0f, len), UnityEngine.Random.insideUnitSphere * 0.3f, new Color(1f, 0.95f, 0.6f, 1f), R(0.04f, 0.09f), R(0.3f, 0.7f));
                    break;

                case "trail_frost":
                    SimpleVfx.TracerLine(from, to, new Color(0.7f, 0.92f, 1f, 1f), width * 1.3f, 0.1f);
                    for (int i = 0; i < budget; i++)
                        Emit("fall", from + n * R(0f, len), Vector3.down * R(0f, 0.5f) + UnityEngine.Random.insideUnitSphere * 0.3f, new Color(0.85f, 0.95f, 1f), 0.05f, R(0.6f, 1.1f));
                    break;

                case "trail_toxic":
                    SimpleVfx.TracerLine(from, to, new Color(0.35f, 1f, 0.2f, 1f), width * 1.4f, 0.1f);
                    for (int i = 0; i < budget; i++)
                        Emit("fall", from + n * R(0f, len), Vector3.down * R(0.5f, 1.5f), new Color(0.4f, 1f, 0.2f), 0.06f, R(0.4f, 0.8f));
                    break;

                case "trail_plasma":
                    SimpleVfx.TracerLine(from, to, new Color(0.2f, 0.95f, 1f, 0.8f), width * 3.2f, 0.1f);
                    SimpleVfx.TracerLine(from, to, new Color(0.9f, 1f, 1f, 1f), width * 1f, 0.1f);
                    for (int r = 0; r < Mathf.Min(6, budget); r++)
                    {
                        Vector3 at = from + n * (len * (r + 0.5f) / 6f);
                        Vector3 side = Vector3.Cross(n, Vector3.up).normalized;
                        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                        Vector3 up2 = Vector3.Cross(side, n);
                        for (int k = 0; k < 8; k++)
                        {
                            float a = k / 8f * Mathf.PI * 2f;
                            Emit("glow", at, (side * Mathf.Cos(a) + up2 * Mathf.Sin(a)) * 1.5f, new Color(0.3f, 1f, 1f, 0.9f), 0.05f, 0.25f);
                        }
                    }
                    break;

                case "trail_hellfire":
                    SimpleVfx.TracerLine(from, to, new Color(1f, 0.4f, 0.05f, 1f), width * 2f, 0.12f);
                    SimpleVfx.TracerLine(from, to, new Color(1f, 0.9f, 0.4f, 1f), width * 0.7f, 0.08f);
                    for (int i = 0; i < budget * 2; i++)
                        Emit("rise", from + n * R(0f, len), Vector3.up * R(0.5f, 2f) + UnityEngine.Random.insideUnitSphere * 0.3f,
                             new Color(1f, R(0.2f, 0.6f), 0.05f, 1f), R(0.06f, 0.14f), R(0.4f, 0.9f));
                    break;

                case "trail_shadow":
                    SimpleVfx.TracerLine(from, to, new Color(0.03f, 0.03f, 0.04f, 0.95f), width * 1.8f, 0.2f);
                    for (int i = 0; i < budget; i++)
                        Emit("puff", from + n * R(0f, len), UnityEngine.Random.insideUnitSphere * 0.2f, new Color(0.05f, 0.05f, 0.06f, 0.5f), R(0.15f, 0.3f), R(0.6f, 1.1f));
                    break;

                case "trail_rainbow":
                {
                    const int segs = 7;
                    for (int sgm = 0; sgm < segs; sgm++)
                    {
                        Color c = Color.HSVToRGB((sgm / (float)segs + Time.time * 0.5f) % 1f, 0.9f, 1f);
                        SimpleVfx.TracerLine(from + dir * (sgm / (float)segs), from + dir * ((sgm + 1) / (float)segs), c, width * 1.6f, 0.12f);
                    }
                    for (int i = 0; i < budget; i++)
                        Emit("glow", from + n * R(0f, len), UnityEngine.Random.insideUnitSphere * 0.5f, Color.HSVToRGB(R(0f, 1f), 0.8f, 1f), 0.05f, R(0.2f, 0.5f));
                    break;
                }

                default:
                    SimpleVfx.TracerLine(from, to, baseColor, width);
                    break;
            }
        }
    }
}
