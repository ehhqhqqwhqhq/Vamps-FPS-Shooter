using UnityEngine;

namespace Vamp.Weapons
{
    /// <summary>
    /// First-person reload choreography (pure maths, shared by the game and the Arms Lab).
    /// Magazine weapons: cant the gun, left hand grabs the mag, pulls it, tosses it, brings a fresh one, seats it,
    /// goes back to the handguard. Shotguns: left hand feeds shells into the port one at a time; pump racks after shots.
    /// Everything is in weapon-root space.
    /// </summary>
    public static class ReloadAnim
    {
        public struct Pose
        {
            public Vector3 GunPos, GunEuler;     // added to the hip pose
            public Vector3 LeftPos;              // left-hand grip target
            public Quaternion LeftRot;
            public float MagOut;                 // metres along the mag's drop direction
            public Vector3 MagExtra, MagExtraEuler;
            public bool MagVisible;
            public bool ShellVisible;
            public Vector3 ShellPos;
        }

        private static float S(float a, float b, float t)
        {
            float x = Mathf.Clamp01((t - a) / (b - a));
            return x * x * (3f - 2f * x);
        }

        public static Pose Idle(Vector3 leftPos, Quaternion leftRot)
        {
            return new Pose { LeftPos = leftPos, LeftRot = leftRot, MagVisible = true };
        }

        /// <summary>Magazine change. p = reload progress 0..1.</summary>
        public static Pose Magazine(WeaponParts parts, float p, Vector3 restLeft, Quaternion restLeftRot)
        {
            var pose = Idle(restLeft, restLeftRot);
            if (parts == null || !parts.HasMag) return pose;
            bool pistol = parts.Pistol;

            // Gun: cant it (right side down), lift a little and bring it in, hold, then back.
            float cant = S(0f, 0.14f, p) * (1f - S(0.8f, 1f, p));
            // Lift and pull the gun in so the mag well is on screen.
            pose.GunEuler = (pistol ? new Vector3(-14f, 14f, -22f) : new Vector3(-16f, 12f, -30f)) * cant;
            pose.GunPos = (pistol ? new Vector3(-0.06f, 0.08f, -0.04f) : new Vector3(-0.08f, 0.09f, -0.01f)) * cant;
            float jolt = S(0.64f, 0.69f, p) * (1f - S(0.69f, 0.78f, p));   // seating the new mag
            pose.GunPos.y += 0.014f * jolt;
            pose.GunEuler.x -= 4f * jolt;

            Vector3 outDir = parts.MagOutDir;
            float L = parts.MagLength;
            float outMax = L * 0.95f + 0.015f;
            Vector3 toss = pistol ? new Vector3(-0.05f, -0.2f, -0.04f) : new Vector3(-0.1f, -0.24f, -0.06f);

            float magOut; Vector3 extra = Vector3.zero, extraE = Vector3.zero; bool visible = true;
            if (p < 0.12f) magOut = 0f;
            else if (p < 0.3f) magOut = outMax * S(0.12f, 0.3f, p);
            else if (p < 0.42f)
            {
                magOut = outMax;
                float t = S(0.3f, 0.42f, p);
                extra = toss * t; extraE = new Vector3(20f, 0f, 70f) * t;
                visible = p < 0.405f;                                        // old mag gone
            }
            else if (p < 0.6f)
            {
                magOut = outMax;
                float t = 1f - S(0.42f, 0.6f, p);                            // fresh mag comes up from below
                extra = toss * t; extraE = new Vector3(-15f, 0f, -40f) * t;
            }
            else if (p < 0.7f) magOut = outMax * (1f - S(0.6f, 0.7f, p));
            else magOut = 0f;
            pose.MagOut = magOut;
            pose.MagExtra = extra;
            pose.MagExtraEuler = extraE;
            pose.MagVisible = visible;

            // Left hand: wraps the mag body from the left side, index finger up by the mag well.
            Vector3 grab = parts.MagTop + outDir * (L * (pistol ? 0.75f : 0.5f)) + new Vector3(-0.012f, 0f, 0f);
            Quaternion grabRot = Quaternion.LookRotation(-outDir, Vector3.right);
            Vector3 onMag = grab + outDir * magOut + extra;
            Quaternion onMagRot = Quaternion.Euler(extraE) * grabRot;

            float reach = S(0.02f, 0.12f, p), back = S(0.72f, 0.88f, p);
            if (p < 0.12f)
            {
                pose.LeftPos = Vector3.Lerp(restLeft, onMag, reach) + Vector3.down * (0.05f * Mathf.Sin(reach * Mathf.PI));
                pose.LeftRot = Quaternion.Slerp(restLeftRot, onMagRot, reach);
            }
            else if (p < 0.72f)
            {
                pose.LeftPos = onMag;
                pose.LeftRot = onMagRot;
            }
            else
            {
                pose.LeftPos = Vector3.Lerp(onMag, restLeft, back) + Vector3.down * (0.04f * Mathf.Sin(back * Mathf.PI));
                pose.LeftRot = Quaternion.Slerp(onMagRot, restLeftRot, back);
            }
            return pose;
        }

        /// <summary>One shell into the loading port. p = progress of this shell 0..1, blend = how far into the reload stance.</summary>
        public static Pose Shell(WeaponParts parts, float p, float blend, Vector3 restLeft, Quaternion restLeftRot)
        {
            var pose = Idle(restLeft, restLeftRot);
            Vector3 port = parts != null ? parts.LoadPort : new Vector3(0f, -0.03f, 0.1f);
            pose.GunEuler = new Vector3(-12f, 14f, -34f) * blend;
            pose.GunPos = new Vector3(-0.08f, 0.08f, 0.0f) * blend;

            // Palm up under the port; fetch a shell from below-left, push it up into the port.
            Vector3 below = port + new Vector3(-0.01f, -0.045f, -0.01f);
            Vector3 fetch = port + new Vector3(-0.07f, -0.12f, -0.05f);
            Vector3 hand;
            if (p < 0.35f) hand = Vector3.Lerp(port, fetch, S(0f, 0.35f, p));
            else if (p < 0.7f) hand = Vector3.Lerp(fetch, below, S(0.35f, 0.7f, p));
            else if (p < 0.85f) hand = Vector3.Lerp(below, port, S(0.7f, 0.85f, p));
            else hand = port;
            pose.LeftPos = Vector3.Lerp(restLeft, hand, blend);
            pose.LeftRot = Quaternion.Slerp(restLeftRot, Quaternion.Euler(0f, 0f, 20f), blend);
            pose.ShellVisible = blend > 0.5f && p > 0.25f && p < 0.84f;
            pose.ShellPos = hand + new Vector3(0f, 0.022f, 0f);
            return pose;
        }

        /// <summary>Pump rack after a shot: t 0..1 over the rack. Returns pump travel 0..1.</summary>
        public static float Pump(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            return t < 0.45f ? S(0f, 0.45f, t) : 1f - S(0.45f, 1f, t);
        }
    }
}
