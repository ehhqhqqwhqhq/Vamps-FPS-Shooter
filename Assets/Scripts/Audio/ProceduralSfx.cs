using UnityEngine;

namespace Vamp.Audio
{
    /// <summary>
    /// Placeholder sound synthesis so the prototype has hit/kill/weapon/movement feedback without assets.
    /// Replace with real sound design in Phase 15 via AudioController overrides.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int Rate = 44100;

        private delegate float SampleFn(float t, float noise);

        public static AudioClip Create(SfxId id)
        {
            switch (id)
            {
                case SfxId.HitTick:
                    return Make("HitTick", 0.05f, (t, n) => Mathf.Sin(t * 2f * Mathf.PI * 1900f) * Env(t, 70f) * 0.6f);
                case SfxId.HitHeadshot:
                    return Make("HitHeadshot", 0.14f, (t, n) =>
                        (Mathf.Sin(t * 2f * Mathf.PI * 2600f) + 0.6f * Mathf.Sin(t * 2f * Mathf.PI * 3900f)) * Env(t, 28f) * 0.45f);
                case SfxId.Kill:
                    return Make("Kill", 0.35f, (t, n) =>
                        Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(220f, 70f, t / 0.35f)) * Env(t, 9f) * 0.8f + n * Env(t, 60f) * 0.3f);
                case SfxId.ShotgunFire:
                    return MakeFiltered("ShotgunFire", 0.45f, 0.35f, (t, n) =>
                        n * Env(t, 9f) * 0.9f + Mathf.Sin(t * 2f * Mathf.PI * 65f) * Env(t, 14f) * 0.7f);
                case SfxId.PistolFire:
                    return MakeFiltered("PistolFire", 0.18f, 0.6f, (t, n) =>
                        n * Env(t, 22f) * 0.8f + Mathf.Sin(t * 2f * Mathf.PI * 140f) * Env(t, 30f) * 0.4f);
                case SfxId.RocketFire:
                    return MakeFiltered("RocketFire", 0.5f, 0.2f, (t, n) => n * Env(t, 5f) * Mathf.Clamp01(t * 40f) * 0.9f);
                case SfxId.Explosion:
                    return MakeFiltered("Explosion", 1.1f, 0.08f, (t, n) =>
                        n * Env(t, 3.5f) * 1.4f + Mathf.Sin(t * 2f * Mathf.PI * 45f) * Env(t, 5f) * 0.8f);
                case SfxId.DryFire:
                    return Make("DryFire", 0.03f, (t, n) => n * Env(t, 200f) * 0.5f);
                case SfxId.Reload:
                    return Make("Reload", 0.12f, (t, n) => n * (Env(t, 90f) + (t > 0.07f ? Env(t - 0.07f, 90f) : 0f)) * 0.5f);
                case SfxId.Jump:
                    return MakeFiltered("Jump", 0.1f, 0.3f, (t, n) => n * Env(t, 35f) * 0.35f);
                case SfxId.Land:
                    return MakeFiltered("Land", 0.14f, 0.12f, (t, n) => n * Env(t, 30f) * 0.7f + Mathf.Sin(t * 2f * Mathf.PI * 80f) * Env(t, 30f) * 0.4f);
                case SfxId.Dash:
                    return MakeFiltered("Dash", 0.22f, 0.5f, (t, n) => n * Mathf.Sin(Mathf.PI * t / 0.22f) * 0.6f);
                case SfxId.Slide:
                    return MakeFiltered("Slide", 0.4f, 0.15f, (t, n) => n * Mathf.Sin(Mathf.PI * t / 0.4f) * 0.35f);
                case SfxId.WallJump:
                    return MakeFiltered("WallJump", 0.14f, 0.4f, (t, n) => n * Env(t, 25f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 300f) * Env(t, 40f) * 0.2f);
                case SfxId.Equip:
                    return Make("Equip", 0.08f, (t, n) => n * Env(t, 60f) * 0.4f);
                case SfxId.RifleFire:
                    return MakeFiltered("RifleFire", 0.2f, 0.5f, (t, n) =>
                        n * Env(t, 26f) * 0.8f + Mathf.Sin(t * 2f * Mathf.PI * 110f) * Env(t, 35f) * 0.45f);
                case SfxId.SmgFire:
                    return MakeFiltered("SmgFire", 0.12f, 0.7f, (t, n) => n * Env(t, 40f) * 0.7f);
                case SfxId.SniperFire:
                    return MakeFiltered("SniperFire", 0.9f, 0.25f, (t, n) =>
                        n * Env(t, 7f) * 1.1f + Mathf.Sin(t * 2f * Mathf.PI * 55f) * Env(t, 6f) * 0.8f);
                case SfxId.EnergyFire:
                    return Make("EnergyFire", 0.14f, (t, n) =>
                        Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(1400f, 500f, t / 0.14f)) * Env(t, 18f) * 0.5f);
                case SfxId.HeavyFire:
                    return MakeFiltered("HeavyFire", 0.6f, 0.2f, (t, n) =>
                        n * Env(t, 6f) * 1.2f + Mathf.Sin(t * 2f * Mathf.PI * 45f) * Env(t, 8f) * 0.9f);
                case SfxId.MeleeSwing:
                    return MakeFiltered("MeleeSwing", 0.18f, 0.35f, (t, n) => n * Mathf.Sin(Mathf.PI * t / 0.18f) * 0.6f);
                case SfxId.Overheat:
                    return Make("Overheat", 0.5f, (t, n) => (Mathf.Sin(t * 2f * Mathf.PI * 880f) * 0.3f + n * 0.3f) * Env(t, 5f));
                case SfxId.UIClick:
                    return Make("UIClick", 0.04f, (t, n) => Mathf.Sin(t * 2f * Mathf.PI * 1200f) * Env(t, 90f) * 0.35f);
                case SfxId.UIHover:
                    return Make("UIHover", 0.03f, (t, n) => Mathf.Sin(t * 2f * Mathf.PI * 2400f) * Env(t, 140f) * 0.15f);
                case SfxId.UIOpen:
                    return Make("UIOpen", 0.18f, (t, n) => Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(300f, 700f, t / 0.18f)) * Env(t, 14f) * 0.3f);
                case SfxId.UIError:
                    return Make("UIError", 0.2f, (t, n) => Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * 180f)) * Env(t, 14f) * 0.2f);
                case SfxId.LevelUp:
                    return Make("LevelUp", 1.2f, (t, n) =>
                    {
                        float f = t < 0.15f ? 440f : t < 0.3f ? 554.4f : t < 0.45f ? 659.3f : 880f;
                        return (Mathf.Sin(t * 2f * Mathf.PI * f) + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * f * 2f)) * Env(t, 2.5f) * 0.35f;
                    });
                case SfxId.Unlock:
                    return Make("Unlock", 0.6f, (t, n) => (Mathf.Sin(t * 2f * Mathf.PI * 660f) + Mathf.Sin(t * 2f * Mathf.PI * 990f)) * Env(t, 6f) * 0.25f);
                case SfxId.Countdown:
                    return Make("Countdown", 0.15f, (t, n) => Mathf.Sin(t * 2f * Mathf.PI * 660f) * Env(t, 18f) * 0.45f);
                case SfxId.CountdownGo:
                    return Make("CountdownGo", 0.5f, (t, n) => Mathf.Sin(t * 2f * Mathf.PI * 1320f) * Env(t, 6f) * 0.45f);
                case SfxId.MatchEnd:
                    return MakeFiltered("MatchEnd", 1.5f, 0.1f, (t, n) => (n * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 55f)) * Env(t, 2f) * 0.8f);
                case SfxId.Announcer:
                    return MakeFiltered("Announcer", 0.7f, 0.15f, (t, n) =>
                        (Mathf.Sin(t * 2f * Mathf.PI * 70f) * 0.8f + n * 0.4f) * Env(t, 4f) + Mathf.Sin(t * 2f * Mathf.PI * 1760f) * Env(t, 20f) * 0.2f);
                case SfxId.Death:
                    return MakeFiltered("Death", 0.8f, 0.12f, (t, n) => (n * 0.7f + Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(120f, 40f, t / 0.8f))) * Env(t, 3.5f) * 0.7f);
                case SfxId.Checkpoint:
                    return Make("Checkpoint", 0.3f, (t, n) => Mathf.Sin(t * 2f * Mathf.PI * (t < 0.1f ? 880f : 1320f)) * Env(t, 9f) * 0.4f);
                default:
                    return Make("Silence", 0.01f, (t, n) => 0f);
            }
        }

        private static AudioClip _menuLoop, _matchLoop;

        /// <summary>16 s seamless dark-electronic loop (120 BPM, Am-F-Dm-E). Placeholder until real music exists.</summary>
        public static AudioClip MusicLoop(bool intense)
        {
            if (intense && _matchLoop != null) return _matchLoop;
            if (!intense && _menuLoop != null) return _menuLoop;

            const float bpm = 120f;
            float beat = 60f / bpm;
            int beats = 32;
            float seconds = beat * beats;
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var rng = new System.Random(intense ? 7 : 3);
            float[] roots = { 55f, 43.65f, 36.71f, 41.2f }; // A1 F1 D1 E1
            float lp = 0f, padLp = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                int b = (int)(t / beat);
                float inBeat = t - b * beat;
                float root = roots[(b / 8) % 4];
                float eighth = t % (beat * 0.5f);

                // Bass: filtered saw on eighth notes
                float saw = 2f * ((t * root) % 1f) - 1f;
                float bassEnv = Mathf.Exp(-eighth * (intense ? 9f : 5f));
                lp = Mathf.Lerp(lp, saw, 0.02f + 0.08f * bassEnv);
                float bass = lp * bassEnv * 0.55f;

                // Pad: detuned saws an octave+fifth up, heavy low-pass
                float pad = (2f * ((t * root * 3f) % 1f) - 1f) + (2f * ((t * root * 3.01f) % 1f) - 1f) + (2f * ((t * root * 4.49f) % 1f) - 1f);
                padLp = Mathf.Lerp(padLp, pad, 0.015f);
                float padOut = padLp * 0.12f;

                // Kick on every beat (sine pitch drop)
                float kick = Mathf.Sin(2f * Mathf.PI * (50f * inBeat + 60f * (1f - Mathf.Exp(-inBeat * 30f)) / 30f)) * Mathf.Exp(-inBeat * 9f);
                kick *= intense ? 0.9f : ((b % 2 == 0) ? 0.55f : 0f);

                // Hats on offbeats (intense only) + noise riser at the end of each 8-bar phrase
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float offbeat = (t + beat * 0.5f) % beat;
                float hat = intense ? noise * Mathf.Exp(-offbeat * 60f) * 0.15f : 0f;
                float phrase = (t % (beat * 8f)) / (beat * 8f);
                float riser = noise * Mathf.Pow(phrase, 6f) * 0.08f;

                data[i] = Mathf.Clamp((bass + padOut + kick + hat + riser) * 0.7f, -1f, 1f);
            }
            // Short crossfade at the loop point to avoid a click
            int fade = 400;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = data[i] * k + data[count - fade + i] * (1f - k);
            }
            var clip = AudioClip.Create(intense ? "VAMP_MatchLoop" : "VAMP_MenuLoop", count, 1, Rate, false);
            clip.SetData(data, 0);
            if (intense) _matchLoop = clip; else _menuLoop = clip;
            return clip;
        }

        private static float Env(float t, float decay)
        {
            return Mathf.Exp(-t * decay);
        }

        private static AudioClip Make(string name, float seconds, SampleFn fn)
        {
            return MakeFiltered(name, seconds, 1f, fn);
        }

        /// <param name="lowpass">0..1 one-pole low-pass coefficient (1 = unfiltered).</param>
        private static AudioClip MakeFiltered(string name, float seconds, float lowpass, SampleFn fn)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(seconds * Rate));
            var data = new float[count];
            var rng = new System.Random(name.GetHashCode());
            float prev = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float s = fn(t, noise);
                prev = Mathf.Lerp(prev, s, lowpass);
                data[i] = Mathf.Clamp(prev, -1f, 1f);
            }
            var clip = AudioClip.Create("VAMP_" + name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
