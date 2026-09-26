using System.Collections.Generic;
using UnityEngine;

namespace Vamp.Weapons
{
    /// <summary>
    /// Where the first-person hands go on each weapon, plus per-weapon hand-tuned corrections.
    /// Positions are in weapon space (metres, +Z = muzzle, +Y = up); angles in degrees applied in the grip frame.
    /// Tune with VAMP ▸ Arms Lab (renders every weapon with the arms from the player's view and from the side).
    /// </summary>
    public static class ArmsFitting
    {
        public struct Fit
        {
            public Vector3 RightPos, RightEuler;   // added to the right-hand grip point / rotation
            public Vector3 LeftPos, LeftEuler;     // added to the left-hand point / rotation
            public Vector3 Hip;                    // added to the hip-fire view position
        }

        /// <summary>Per weapon id. Anything missing = zero.</summary>
        public static readonly Dictionary<string, Fit> Fits = new Dictionary<string, Fit>
        {
            { "havoc",  new Fit { RightPos = new Vector3(0f, 0.02f, 0.01f), RightEuler = new Vector3(8f, 0f, 0f) } },
            { "arc",    new Fit { RightPos = new Vector3(0f, 0.015f, 0f) } },
            { "widow",  new Fit { RightPos = new Vector3(0f, -0.03f, 0.04f) } },
            { "ripper", new Fit { RightPos = new Vector3(0f, 0.04f, 0.07f), RightEuler = new Vector3(10f, 0f, 0f) } },
            { "brute",  new Fit { RightPos = new Vector3(0f, 0.01f, 0f) } },
            { "blast",  new Fit { LeftPos = new Vector3(0f, -0.06f, 0f) } },
            { "v9",     new Fit { Hip = new Vector3(0f, 0.03f, -0.02f) } },
            { "reaper", new Fit { Hip = new Vector3(0f, 0.03f, -0.02f) } },
            { "blade",  new Fit { Hip = new Vector3(0f, 0.05f, 0.13f) } },
        };

        public static Fit For(string id)
        {
            Fit f;
            return id != null && Fits.TryGetValue(id, out f) ? f : default(Fit);
        }

        /// <summary>Hip-fire position of the weapon root relative to the eye.</summary>
        public static Vector3 Hip(WeaponData d, bool placeholder, Vector3 placeholderHip)
        {
            Vector3 hip;
            if (d.delivery == DeliveryType.Melee) hip = new Vector3(0.19f, -0.15f, 0.27f);
            else if (placeholder) hip = placeholderHip;
            else hip = d.slot == WeaponSlot.Secondary ? new Vector3(0.15f, -0.14f, 0.34f) : new Vector3(0.18f, -0.17f, 0.24f);
            return hip + For(d.id).Hip;
        }

        /// <summary>Grip targets for the arms on this weapon (weapon space).</summary>
        public static void Compute(Transform root, WeaponData d, out Vector3 rGrip, out Quaternion rRot, out bool hasLeft, out Vector3 lGrip, out Quaternion lRot)
        {
            var fit = For(d.id);
            var gripT = root.Find("Grip");
            var muzzleT = root.Find("Muzzle");
            var offT = root.Find("Offhand");
            Vector3 grip = gripT != null ? gripT.localPosition : new Vector3(0f, -0.06f, 0f);
            Vector3 muzzle = muzzleT != null ? muzzleT.localPosition : new Vector3(0f, 0.03f, 0.45f);
            lGrip = Vector3.zero;
            lRot = Quaternion.identity;

            if (d.delivery == DeliveryType.Melee)
            {
                // Knuckles up, index finger towards the blade; left arm down out of view.
                rGrip = grip + fit.RightPos;
                rRot = Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Euler(fit.RightEuler);
                hasLeft = false;
                return;
            }
            // Right: pinky→index along the grip (raked like a real pistol grip), knuckles forward.
            // The Grip point sits low on the grip; the fist centres a little higher.
            rGrip = grip + new Vector3(0f, 0.025f, 0f) + fit.RightPos;
            rRot = Quaternion.Euler(14f, 0f, 0f) * Quaternion.Euler(fit.RightEuler);
            hasLeft = true;
            float length = muzzle.z - grip.z;
            if (length < 0.3f)
            {
                // Pistol: left hand wraps the right from the left side, palm facing the grip.
                lGrip = grip + new Vector3(-0.03f, 0.013f, 0.004f) + fit.LeftPos;
                lRot = Quaternion.Euler(14f, 0f, 0f) * Quaternion.LookRotation(Vector3.up, Vector3.right) * Quaternion.Euler(fit.LeftEuler);
            }
            else
            {
                // Long gun: palm up under the handguard, index finger forward.
                Vector3 off = offT != null ? offT.localPosition + new Vector3(0f, 0.01f, 0f) : new Vector3(0f, muzzle.y - 0.03f, muzzle.z * 0.5f);
                off.z = Mathf.Clamp(off.z, grip.z + 0.16f, grip.z + 0.34f);
                lGrip = off + fit.LeftPos;
                lRot = Quaternion.Euler(fit.LeftEuler);
            }
        }
    }
}
