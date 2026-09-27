using System.Collections.Generic;
using UnityEngine;
using Vamp.Progression;

namespace Vamp.Weapons
{
    public enum AttachmentSlot { Optic, Muzzle, Underbarrel, Magazine }

    /// <summary>One gun attachment: which slot, when it unlocks (player level) and what it changes.</summary>
    public sealed class AttachmentDef
    {
        public string Id, Name, Description;
        public AttachmentSlot Slot;
        public int UnlockLevel;
        /// <summary>Model in Resources/Attachments (null = built from primitives).</summary>
        public string Model;
        public float Recoil = 1f, AdsTime = 1f, HipSpread = 1f, Reload = 1f, Mag = 1f, Zoom = 1f, Range = 1f;
        public bool Suppressed, Laser;
        public bool PistolsToo;     // allowed on secondaries (USP / M1911)
        public bool NotOnShotgun;   // e.g. optics with magnification
    }

    /// <summary>
    /// Gun attachments: 4 slots (OPTIC, MUZZLE, UNDERBARREL, MAGAZINE), unlocked by PLAYER LEVEL, chosen per weapon in
    /// LOADOUT. They change the weapon's handling (recoil, aim speed, spread, reload, magazine) and are fitted on the
    /// first-person gun. Stats are applied to a runtime copy of the weapon when the loadout is handed out.
    /// </summary>
    public static class Attachments
    {
        public static readonly List<AttachmentDef> All = new List<AttachmentDef>
        {
            // Optics (the AWP keeps its scope)
            new AttachmentDef { Id = "att_reddot", Name = "RED DOT", Slot = AttachmentSlot.Optic, UnlockLevel = 3, Model = "RedDot", PistolsToo = true,
                                AdsTime = 1.03f, Zoom = 0.95f, Description = "CLEAN RED DOT. SLIGHT ZOOM." },
            new AttachmentDef { Id = "att_holo", Name = "HOLO SIGHT", Slot = AttachmentSlot.Optic, UnlockLevel = 8, Model = "Holo",
                                AdsTime = 1.05f, Zoom = 0.92f, Description = "WIDE HOLOGRAPHIC WINDOW." },
            new AttachmentDef { Id = "att_2x", Name = "2X OPTIC", Slot = AttachmentSlot.Optic, UnlockLevel = 16, Model = "Optic2x", NotOnShotgun = true,
                                AdsTime = 1.12f, Zoom = 0.72f, Description = "2X MAGNIFIED RED DOT. SLOWER TO AIM." },
            // Muzzle
            new AttachmentDef { Id = "att_suppressor", Name = "SUPPRESSOR", Slot = AttachmentSlot.Muzzle, UnlockLevel = 5, Model = "Suppressor", PistolsToo = true,
                                Suppressed = true, Recoil = 0.95f, Description = "HIDES YOUR MUZZLE FLASH AND QUIETS YOUR SHOTS." },
            new AttachmentDef { Id = "att_heavysup", Name = "HEAVY SUPPRESSOR", Slot = AttachmentSlot.Muzzle, UnlockLevel = 12, Model = "HeavySuppressor",
                                Suppressed = true, Recoil = 0.85f, AdsTime = 1.08f, Description = "SILENT AND STEADY. HEAVIER TO AIM." },
            new AttachmentDef { Id = "att_brake", Name = "MUZZLE BRAKE", Slot = AttachmentSlot.Muzzle, UnlockLevel = 20, Model = "MuzzleBrake", PistolsToo = true,
                                Recoil = 0.75f, Description = "-25% RECOIL. LOUD AND PROUD." },
            // Underbarrel
            new AttachmentDef { Id = "att_vgrip", Name = "VERTICAL GRIP", Slot = AttachmentSlot.Underbarrel, UnlockLevel = 6,
                                Recoil = 0.85f, AdsTime = 1.04f, Description = "-15% RECOIL." },
            new AttachmentDef { Id = "att_laser", Name = "TACTICAL LASER", Slot = AttachmentSlot.Underbarrel, UnlockLevel = 10, Laser = true, PistolsToo = true,
                                HipSpread = 0.65f, Description = "-35% HIP-FIRE SPREAD. EVERYONE SEES THE BEAM." },
            new AttachmentDef { Id = "att_agrip", Name = "ANGLED GRIP", Slot = AttachmentSlot.Underbarrel, UnlockLevel = 14,
                                AdsTime = 0.82f, Description = "AIM DOWN SIGHTS 18% FASTER." },
            // Magazine
            new AttachmentDef { Id = "att_extmag", Name = "EXTENDED MAG", Slot = AttachmentSlot.Magazine, UnlockLevel = 9, PistolsToo = true,
                                Mag = 1.5f, Reload = 1.1f, Description = "+50% AMMO. SLIGHTLY SLOWER RELOAD." },
            new AttachmentDef { Id = "att_fastmag", Name = "FAST MAG", Slot = AttachmentSlot.Magazine, UnlockLevel = 18, PistolsToo = true,
                                Reload = 0.7f, Description = "RELOAD 30% FASTER." },
        };

        public static readonly string[] SlotNames = { "OPTIC", "MUZZLE", "UNDERBARREL", "MAGAZINE" };

        public static AttachmentDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var a in All) if (a.Id == id) return a;
            return null;
        }

        /// <summary>Can this weapon take attachments in this slot at all?</summary>
        public static bool SlotAllowed(WeaponData d, AttachmentSlot slot)
        {
            if (d == null || d.delivery != DeliveryType.Hitscan || d.usesHeat) return false; // guns only (no knives / launcher / beam)
            if (slot == AttachmentSlot.Optic && (d.isSniper || d.scopeOverlay)) return false;  // AWP keeps its scope
            if (slot == AttachmentSlot.Magazine && d.reloadPerShell) return false;             // pump shotgun: shells
            if (slot == AttachmentSlot.Underbarrel && d.slot == WeaponSlot.Secondary) return true; // laser only (filtered below)
            return true;
        }

        public static bool Fits(WeaponData d, AttachmentDef a)
        {
            if (d == null || a == null || !SlotAllowed(d, a.Slot)) return false;
            if (d.slot == WeaponSlot.Secondary && !a.PistolsToo) return false;
            if (a.NotOnShotgun && d.pelletsPerShot > 1) return false;
            return true;
        }

        public static List<AttachmentDef> For(WeaponData d, AttachmentSlot slot)
        {
            var list = new List<AttachmentDef>();
            foreach (var a in All) if (a.Slot == slot && Fits(d, a)) list.Add(a);
            return list;
        }

        public static bool IsUnlocked(AttachmentDef a)
        {
            if (a == null) return true;
            var prog = Core.Game.Progression;
            if (prog == null || prog.Profile == null) return false;
            return prog.IsTester || prog.Profile.prestige_level > 0 || prog.Profile.level >= a.UnlockLevel;
        }

        // ------------------------------------------------------------------ Saved choices (per weapon, per slot)

        private static string Key(string weaponId, AttachmentSlot slot) { return weaponId + "|" + (int)slot; }

        public static string Equipped(string weaponId, AttachmentSlot slot)
        {
            var prog = Core.Game.Progression;
            if (prog == null || prog.Profile == null || prog.Profile.loadout == null || prog.Profile.loadout.attachments == null) return null;
            string k = Key(weaponId, slot);
            foreach (var p in prog.Profile.loadout.attachments) if (p.key == k) return p.value;
            return null;
        }

        public static void SetEquipped(string weaponId, AttachmentSlot slot, string attachmentId)
        {
            var prog = Core.Game.Progression;
            if (prog == null || prog.Profile == null) return;
            var lo = prog.Profile.loadout;
            if (lo.attachments == null) lo.attachments = new List<IdPair>();
            string k = Key(weaponId, slot);
            lo.attachments.RemoveAll(p => p.key == k);
            if (!string.IsNullOrEmpty(attachmentId)) lo.attachments.Add(new IdPair { key = k, value = attachmentId });
            prog.Save();
        }

        /// <summary>The attachments the local player has on this weapon (only unlocked + fitting ones).</summary>
        public static List<AttachmentDef> LocalFor(WeaponData d)
        {
            var list = new List<AttachmentDef>();
            if (d == null) return list;
            for (int s = 0; s < 4; s++)
            {
                var a = Get(Equipped(d.id, (AttachmentSlot)s));
                if (a != null && a.Slot == (AttachmentSlot)s && Fits(d, a) && IsUnlocked(a)) list.Add(a);
            }
            return list;
        }

        // ------------------------------------------------------------------ Stats

        /// <summary>
        /// The local player's loadout with attachments applied: weapons that have any get a runtime copy with modified
        /// handling (same id, same model). Weapons without attachments are returned as they are.
        /// </summary>
        public static WeaponData[] ApplyLocal(WeaponData[] loadout)
        {
            if (loadout == null) return null;
            var result = new WeaponData[loadout.Length];
            for (int i = 0; i < loadout.Length; i++)
            {
                var d = loadout[i];
                var atts = LocalFor(d);
                if (d == null || atts.Count == 0) { result[i] = d; continue; }
                var c = Object.Instantiate(d);
                c.name = d.name;
                foreach (var a in atts)
                {
                    c.recoilPitch *= a.Recoil;
                    c.recoilYawRandom *= a.Recoil;
                    c.viewKick *= a.Recoil;
                    c.adsTime *= a.AdsTime;
                    c.hipSpread *= a.HipSpread;
                    c.movingSpreadAdd *= a.HipSpread;
                    c.reloadTime *= a.Reload;
                    c.adsFovMultiplier *= a.Zoom;
                    if (a.Mag > 1f)
                    {
                        c.magazineSize = Mathf.CeilToInt(c.magazineSize * a.Mag);
                        c.reserveAmmo = Mathf.CeilToInt(c.reserveAmmo * a.Mag);
                    }
                    if (a.Suppressed) c.suppressed = true;
                }
                result[i] = c;
            }
            return result;
        }

        public static string StatLine(AttachmentDef a)
        {
            var parts = new List<string>();
            if (a.Recoil != 1f) parts.Add(Pct(a.Recoil) + " RECOIL");
            if (a.AdsTime != 1f) parts.Add(Pct(a.AdsTime) + " AIM TIME");
            if (a.HipSpread != 1f) parts.Add(Pct(a.HipSpread) + " HIP SPREAD");
            if (a.Reload != 1f) parts.Add(Pct(a.Reload) + " RELOAD TIME");
            if (a.Mag != 1f) parts.Add(Pct(a.Mag) + " AMMO");
            if (a.Zoom != 1f) parts.Add(Mathf.RoundToInt((1f / a.Zoom - 1f) * 100f) + "% ZOOM");
            if (a.Suppressed) parts.Add("SILENCED");
            return string.Join("  ·  ", parts.ToArray());
        }

        private static string Pct(float m)
        {
            int p = Mathf.RoundToInt((m - 1f) * 100f);
            return (p > 0 ? "+" : "") + p + "%";
        }
    }
}
