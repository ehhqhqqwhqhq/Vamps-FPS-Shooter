using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Weapons;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// ATTACHMENTS for one weapon: a column per slot (OPTIC · MUZZLE · UNDERBARREL · MAGAZINE). Each attachment unlocks at
    /// a player level; click one to fit it, click NONE to take it off. Opened from LOADOUT (also mid-match).
    /// </summary>
    public sealed class AttachmentsScreen : MenuScreen
    {
        private readonly WeaponData _weapon;
        private RectTransform _body;

        public AttachmentsScreen(WeaponData weapon) { _weapon = weapon; }

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "ATTACHMENTS", (_weapon != null ? _weapon.displayName : "") + "  ·  UNLOCK MORE BY LEVELLING UP  ·  CHANGES APPLY ON YOUR NEXT SPAWN", 1400f);
            _body = UIKit.Row(col, 560f, 18f, "Slots");
            _body.GetComponent<HorizontalLayoutGroup>().childControlHeight = true;
            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);
            Refresh();
        }

        private void Refresh()
        {
            UIKit.Clear(_body);
            if (_weapon == null || Game.Progression == null || !Game.Progression.IsLoaded) return;
            int level = Game.Progression.Profile.level;
            for (int s = 0; s < 4; s++)
            {
                var slot = (AttachmentSlot)s;
                var column = UIKit.Column(_body, 6f, "Slot_" + slot);
                UIKit.Size(column, -1, 330f);
                UIKit.Size(UIKit.Label(column, Attachments.SlotNames[s], 22, UIKit.Text), 32f);
                if (!Attachments.SlotAllowed(_weapon, slot) || Attachments.For(_weapon, slot).Count == 0)
                {
                    UIKit.Caption(column, "NOT AVAILABLE ON THIS WEAPON", 13);
                    continue;
                }
                string eq = Attachments.Equipped(_weapon.id, slot);
                var eqDef = Attachments.Get(eq);
                if (eqDef != null && !Attachments.Fits(_weapon, eqDef)) eq = null;
                Option(column, slot, null, "NONE", "STOCK PARTS", string.IsNullOrEmpty(eq), true);
                foreach (var a in Attachments.For(_weapon, slot))
                {
                    bool unlocked = Attachments.IsUnlocked(a);
                    Option(column, slot, a, a.Name, unlocked ? Attachments.StatLine(a) : "UNLOCKS AT LEVEL " + a.UnlockLevel + "  (YOU: " + level + ")",
                           a.Id == eq, unlocked);
                }
            }
        }

        private void Option(RectTransform parent, AttachmentSlot slot, AttachmentDef a, string title, string sub, bool equipped, bool unlocked)
        {
            var panel = UIKit.Panel(parent, "Opt", equipped ? new Color(0.35f, 0.03f, 0.06f, 0.95f) : new Color(0.06f, 0.06f, 0.07f, 0.92f));
            UIKit.Size(panel, 96f);
            panel.raycastTarget = true;
            if (equipped) panel.GetComponent<Outline>().effectColor = UIKit.Red;
            UIKit.VList(panel.transform, 2f, 10);
            UIKit.Size(UIKit.Label(panel.transform, (equipped ? "✓ " : "") + title, 18, unlocked ? UIKit.Text : UIKit.TextFaint), 26f);
            var s = UIKit.Label(panel.transform, sub, 12, unlocked ? UIKit.TextDim : UIKit.TextFaint, TextAnchor.UpperLeft, FontStyle.Normal);
            s.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIKit.Size(s, 34f);
            if (a != null && unlocked)
            {
                var d = UIKit.Label(panel.transform, a.Description, 11, UIKit.TextFaint, TextAnchor.UpperLeft, FontStyle.Normal);
                d.horizontalOverflow = HorizontalWrapMode.Wrap;
                UIKit.Size(d, 16f);
            }
            var btn = panel.gameObject.AddComponent<Button>();
            btn.targetGraphic = panel;
            string id = a != null ? a.Id : null;
            btn.onClick.AddListener(() =>
            {
                Audio.AudioController.PlayUI(Audio.SfxId.UIClick);
                if (!unlocked) { Toast("LOCKED", title + " UNLOCKS AT LEVEL " + a.UnlockLevel, true); return; }
                Attachments.SetEquipped(_weapon.id, slot, id);
                Refresh();
            });
        }
    }
}
