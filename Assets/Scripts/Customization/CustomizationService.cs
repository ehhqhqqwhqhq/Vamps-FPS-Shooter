using System;
using Vamp.Progression;

namespace Vamp.Customization
{
    /// <summary>
    /// Equip cosmetics and set loadouts. Validates ownership (only unlocked items can be equipped) and never
    /// touches gameplay values. Server re-validates when online.
    /// </summary>
    public sealed class CustomizationService
    {
        private readonly ProgressionService _progression;

        public event Action Changed;

        public CustomizationService(ProgressionService progression)
        {
            _progression = progression;
        }

        private PlayerProfileData P { get { return _progression.Profile; } }

        public string Equipped(CosmeticType type)
        {
            if (P == null) return null;
            switch (type)
            {
                case CosmeticType.Icon: return P.profile_icon;
                case CosmeticType.Frame: return P.icon_frame;
                case CosmeticType.Banner: return P.banner;
                case CosmeticType.Title: return P.title;
                case CosmeticType.KillEffect: return P.kill_effect;
                case CosmeticType.Emote: return P.emote;
                case CosmeticType.CrosshairStyle: return P.crosshair_style;
                case CosmeticType.CharacterSkin: return P.character_skin;
                case CosmeticType.ProfileBackground: return P.profile_background;
                case CosmeticType.UITheme: return P.ui_theme;
                case CosmeticType.KillFeedStyle: return P.killfeed_style;
                default: return null;
            }
        }

        public bool Equip(string itemId)
        {
            var item = CosmeticCatalog.Get(itemId);
            if (item == null || P == null || !_progression.IsUnlocked(itemId)) return false;
            switch (item.Type)
            {
                case CosmeticType.Icon: P.profile_icon = itemId; break;
                case CosmeticType.Frame: P.icon_frame = itemId; break;
                case CosmeticType.Banner: P.banner = itemId; break;
                case CosmeticType.Title: P.title = itemId; P.title_auto = false; break;
                case CosmeticType.KillEffect: P.kill_effect = itemId; break;
                case CosmeticType.Emote: P.emote = itemId; break;
                case CosmeticType.CrosshairStyle: P.crosshair_style = itemId; break;
                case CosmeticType.CharacterSkin: P.character_skin = itemId; break;
                case CosmeticType.ProfileBackground: P.profile_background = itemId; break;
                case CosmeticType.UITheme: P.ui_theme = itemId; break;
                case CosmeticType.KillFeedStyle: P.killfeed_style = itemId; break;
                default: return false; // weapon skins go through SetWeaponSkin
            }
            Commit();
            return true;
        }

        public void SetTitleVisible(bool visible)
        {
            if (P == null) return;
            P.title_visible = visible;
            Commit();
        }

        public void SetTitleAuto()
        {
            if (P == null) return;
            P.title_auto = true;
            P.title = CosmeticCatalog.TierTitleId(P.level);
            Commit();
        }

        public bool SetWeaponSkin(string weaponId, string skinId)
        {
            var item = CosmeticCatalog.Get(skinId);
            if (P == null || item == null || item.Type != CosmeticType.WeaponSkin || !_progression.IsUnlocked(skinId)) return false;
            P.loadout.SetSkin(weaponId, skinId);
            Commit();
            return true;
        }

        public bool SetLoadoutWeapon(Weapons.WeaponSlot slot, string weaponId)
        {
            if (P == null) return false;
            var data = Core.Game.Weapons != null ? Core.Game.Weapons.Get(weaponId) : null;
            if (data == null || data.slot != slot) return false;
            switch (slot)
            {
                case Weapons.WeaponSlot.Primary: P.loadout.primary = weaponId; break;
                case Weapons.WeaponSlot.Secondary: P.loadout.secondary = weaponId; break;
                default: P.loadout.melee = weaponId; break;
            }
            Commit();
            return true;
        }

        private void Commit()
        {
            _progression.RaiseChanged();
            if (Changed != null) Changed();
        }
    }
}
