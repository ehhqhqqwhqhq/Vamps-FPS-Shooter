using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Progression;
using Vamp.Weapons;

namespace Vamp.UI.Menus
{
    /// <summary>LOADOUT: primary / secondary / melee + weapon skins (cosmetic), with a stat readout per weapon.</summary>
    public sealed class LoadoutScreen : MenuScreen
    {
        private int _slot;
        private RectTransform _list;
        private RectTransform _details;
        private List<Button> _tabs;

        private static readonly string[] SlotNames = { "PRIMARY", "SECONDARY", "MELEE" };

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "LOADOUT", "WEAPONS ARE NEVER LOCKED BEHIND PROGRESSION · SKINS ARE COSMETIC", 1100f);
            _tabs = UIKit.Tabs(col, SlotNames, 0, i => { _slot = i; Refresh(); });
            var body = UIKit.Row(col, 520f, 24f, "Body");
            ScrollRect scroll;
            _list = UIKit.ScrollList(body, out scroll, 6f);
            UIKit.Size(scroll, 520f, 420f);
            _details = UIKit.Column(body, 8f, "Details");
            UIKit.Size(_details, 520f, -1, 1f);
            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);
            Refresh();
        }

        private string Equipped()
        {
            var l = Game.Progression.Profile.loadout;
            return _slot == 0 ? l.primary : _slot == 1 ? l.secondary : l.melee;
        }

        private void Refresh()
        {
            UIKit.Clear(_list);
            UIKit.Clear(_details);
            if (Game.Weapons == null || Game.Progression == null || !Game.Progression.IsLoaded) return;
            var slot = (WeaponSlot)_slot;
            string eq = Equipped();
            foreach (var w in Game.Weapons.InSlot(slot))
            {
                var data = w;
                bool isEq = data.id == eq;
                var b = UIKit.Button(_list, (isEq ? "▶  " : "") + data.displayName, () =>
                {
                    Game.Customization.SetLoadoutWeapon(slot, data.id);
                    Refresh();
                }, isEq ? UIKit.ButtonStyle.Primary : UIKit.ButtonStyle.Box, 20, 52f);
                UIKit.ButtonText(b).alignment = TextAnchor.MiddleLeft;
            }
            ShowDetails(Game.Weapons.Get(eq));
        }

        private void ShowDetails(WeaponData w)
        {
            if (w == null) return;
            UIKit.Heading(_details, w.displayName, 40);
            UIKit.Caption(_details, Describe(w), 15);
            UIKit.Divider(_details);

            float dps = w.damage * w.pelletsPerShot * w.fireRate / 60f;
            Stat("DAMAGE", w.delivery == DeliveryType.Projectile ? (w.explosionDamage + w.damage) : w.damage * w.pelletsPerShot, 150f,
                 w.pelletsPerShot > 1 ? w.pelletsPerShot + " × " + w.damage : Mathf.RoundToInt(w.delivery == DeliveryType.Projectile ? w.explosionDamage + w.damage : w.damage).ToString());
            Stat("FIRE RATE", w.fireRate, 1000f, Mathf.RoundToInt(w.fireRate) + " RPM");
            Stat("DPS", dps, 300f, Mathf.RoundToInt(dps).ToString());
            Stat("RANGE", w.falloffEnd, 80f, Mathf.RoundToInt(w.falloffStart) + "–" + Mathf.RoundToInt(w.falloffEnd) + " M");
            Stat("ACCURACY", 6f - Mathf.Min(6f, w.hipSpread), 6f, w.hipSpread.ToString("0.0") + "°");
            Stat("MAGAZINE", w.magazineSize, 40f, w.usesHeat ? "HEAT" : w.magazineSize.ToString());
            Stat("HEADSHOT", w.headshotMultiplier, 3f, "×" + w.headshotMultiplier.ToString("0.0#"));

            UIKit.Spacer(_details, 8f);
            UIKit.Caption(_details, "WEAPON SKIN (COSMETIC)", 14);
            var skins = CosmeticCatalog.OfType(CosmeticType.WeaponSkin);
            var names = new List<string>();
            int idx = 0;
            string current = Game.Progression.Profile.loadout.SkinFor(w.id);
            for (int i = 0; i < skins.Count; i++)
            {
                bool owned = Game.Progression.IsUnlocked(skins[i].Id);
                names.Add(skins[i].Name + (owned ? "" : "  (LV " + skins[i].UnlockLevel + ")"));
                if (skins[i].Id == current) idx = i;
            }
            string weaponId = w.id;
            UIKit.Selector(_details, "SKIN", names, idx, i =>
            {
                if (!Game.Customization.SetWeaponSkin(weaponId, skins[i].Id))
                    Toast("LOCKED", skins[i].UnlockText, true);
            });
        }

        private void Stat(string label, float value, float max, string text)
        {
            var row = UIKit.Row(_details, 26f, 12f);
            var l = UIKit.Label(row, label, 14, UIKit.TextDim);
            UIKit.Size(l, -1, 130f);
            var barBg = UIKit.Image(row, "Bar", new Color(1f, 1f, 1f, 0.1f));
            UIKit.Size(barBg, 6f, -1, 1f);
            var fill = UIKit.Image(barBg.transform, "Fill", UIKit.Red);
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(value / max), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = UIKit.Label(row, text, 14, UIKit.Text, TextAnchor.MiddleRight);
            UIKit.Size(t, -1, 110f);
        }

        public static string Describe(WeaponData w)
        {
            switch (w.id)
            {
                case "v9": return "FAST SEMI-AUTOMATIC PISTOL. RELIABLE AT ANY SPEED.";
                case "ripper": return "HIGH FIRE-RATE SMG FOR CLOSE TO MID RANGE.";
                case "havoc": return "BALANCED AUTOMATIC RIFLE.";
                case "brute": return "POWERFUL CLOSE-RANGE SHOTGUN. FIRE IT IN THE AIR FOR A BOOST.";
                case "widow": return "HIGH-DAMAGE PRECISION SNIPER. ONE HEADSHOT KILLS.";
                case "blast": return "EXPLOSIVE LAUNCHER. ROCKET JUMP TO REACH HIGH GROUND.";
                case "arc": return "ENERGY BEAM. NO RELOAD - MANAGE THE HEAT.";
                case "reaper": return "DEVASTATING AT POINT BLANK, USELESS BEYOND IT.";
                case "blade": return "MELEE. QUICK, SILENT, LETHAL FROM BEHIND.";
                default: return "";
            }
        }
    }
}
