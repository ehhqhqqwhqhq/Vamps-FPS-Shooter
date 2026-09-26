using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Progression;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// CUSTOMIZE: ICONS (collection with filters + OWNED x / 60), FRAMES, BANNERS, TITLES (equip / hide / auto),
    /// KILL EFFECTS, EMOTES, CROSSHAIR STYLES, CHARACTER SKINS, PLAYER CARD preview. All cosmetic.
    /// </summary>
    public sealed class CustomizeScreen : MenuScreen
    {
        private static readonly CosmeticType[] Types =
        {
            CosmeticType.Icon, CosmeticType.Frame, CosmeticType.Banner, CosmeticType.Title, CosmeticType.KillEffect,
            CosmeticType.Emote, CosmeticType.CrosshairStyle, CosmeticType.CharacterSkin
        };
        private static readonly string[] TypeNames = { "ICONS", "FRAMES", "BANNERS", "TITLES", "KILL FX", "EMOTES", "CROSSHAIR", "CHARACTER" };
        private static readonly string[] Filters = { "ALL", "OWNED", "LOCKED", "LEVEL REWARDS", "PRESTIGE", "CHALLENGES", "EVENTS" };

        private int _type;
        private int _filter;
        private RectTransform _grid;
        private RectTransform _preview;
        private Text _owned;
        private OptionSelector _filterSel;

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "CUSTOMIZE", "SHOW OFF YOUR PROGRESS · COSMETICS NEVER AFFECT GAMEPLAY", 1400f);
            UIKit.Tabs(col, TypeNames, 0, i => { _type = i; Refresh(); }, 15);
            var filterRow = UIKit.Row(col, 44f, 12f);
            _filterSel = UIKit.Selector(filterRow, "FILTER", Filters, 0, i => { _filter = i; Refresh(); }, 90f);
            UIKit.Size(_filterSel, 44f, 560f);
            UIKit.Spacer(filterRow, 0f, true);
            _owned = UIKit.Label(filterRow, "", 18, UIKit.Text, TextAnchor.MiddleRight);
            UIKit.Size(_owned, -1, 260f);

            var body = UIKit.Row(col, 560f, 30f, "Body");
            ScrollRect scroll;
            _grid = UIKit.ScrollList(body, out scroll, 8f);
            UIKit.Size(scroll, 560f, -1, 1f);
            // Replace the vertical list with a grid for icon-like items.
            Object.DestroyImmediate(_grid.GetComponent<VerticalLayoutGroup>());
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150f, 176f);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(4, 4, 4, 4);

            _preview = UIKit.Column(body, 10f, "Preview");
            UIKit.Size(_preview, 560f, 300f);
            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);
            Refresh();
        }

        private bool PassesFilter(CosmeticItem item, bool owned)
        {
            switch (_filter)
            {
                case 1: return owned;
                case 2: return !owned;
                case 3: return item.Source == UnlockSource.Level || item.Source == UnlockSource.Default;
                case 4: return item.Source == UnlockSource.Prestige;
                case 5: return item.Source == UnlockSource.Challenge;
                case 6: return item.Source == UnlockSource.Event;
                default: return true;
            }
        }

        private void Refresh()
        {
            UIKit.Clear(_grid);
            UIKit.Clear(_preview);
            var prog = Game.Progression;
            if (prog == null || !prog.IsLoaded) return;
            var type = Types[_type];
            var items = CosmeticCatalog.OfType(type);
            int owned = 0;
            foreach (var i in items) if (prog.IsUnlocked(i.Id)) owned++;
            _owned.text = (type == CosmeticType.Icon ? "ICON COLLECTION  " : "") + "OWNED " + owned + " / " + items.Count;

            string equipped = Game.Customization.Equipped(type);
            foreach (var item in items)
            {
                bool has = prog.IsUnlocked(item.Id);
                if (!PassesFilter(item, has)) continue;
                Tile(item, has, item.Id == equipped);
            }

            // Preview: player card + title options
            UIKit.Caption(_preview, "PLAYER CARD", 14);
            var cardRow = UIKit.Row(_preview, 320f, 0f);
            cardRow.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            IdentityViews.Card(cardRow, prog.Profile, Game.Username, 260f, 320f);
            if (type == CosmeticType.Title)
            {
                UIKit.Toggle(_preview, "SHOW TITLE", prog.Profile.title_visible, v => { Game.Customization.SetTitleVisible(v); Refresh(); });
                UIKit.Button(_preview, "USE TIER TITLE (AUTO)", () => { Game.Customization.SetTitleAuto(); Refresh(); }, UIKit.ButtonStyle.Ghost, 15, 40f);
            }
        }

        private void Tile(CosmeticItem item, bool owned, bool equipped)
        {
            var tile = UIKit.Panel(_grid, "Tile", equipped ? new Color(0.25f, 0.02f, 0.05f, 0.9f) : new Color(0.06f, 0.06f, 0.07f, 0.9f));
            var btn = tile.gameObject.AddComponent<Button>();
            btn.targetGraphic = tile;
            if (equipped) tile.GetComponent<Outline>().effectColor = UIKit.Red;
            UIKit.VList(tile.transform, 4f, 10, TextAnchor.UpperCenter);

            var visual = UIKit.Row(tile.transform, 96f, 0f);
            visual.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            visual.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            if (item.Type == CosmeticType.Icon)
                IdentityViews.Icon(visual, item.Id, Game.Progression.Profile.icon_frame, 86f, !owned);
            else if (item.Type == CosmeticType.Frame)
                IdentityViews.Icon(visual, Game.Progression.Profile.profile_icon, item.Id, 86f, !owned);
            else
            {
                var swatch = UIKit.Image(visual, "Swatch", owned ? item.Color : new Color(0.2f, 0.2f, 0.2f));
                swatch.rectTransform.sizeDelta = new Vector2(96f, item.Type == CosmeticType.Banner ? 40f : 86f);
                var glyph = UIKit.Label(swatch.transform, item.Type == CosmeticType.Title ? "T" : owned ? "" : "?", 36, UIKit.Text, TextAnchor.MiddleCenter);
                UIKit.Stretch(glyph.rectTransform);
            }

            var name = UIKit.Label(tile.transform, item.Name, 13, owned ? UIKit.Text : UIKit.TextFaint, TextAnchor.MiddleCenter);
            UIKit.Size(name, 34f);
            var sub = UIKit.Label(tile.transform, equipped ? "EQUIPPED" : owned ? "OWNED" : "LOCKED", 11,
                                  equipped ? UIKit.Red : owned ? UIKit.TextDim : UIKit.TextFaint, TextAnchor.MiddleCenter);
            UIKit.Size(sub, 16f);

            string id = item.Id;
            btn.onClick.AddListener(() =>
            {
                if (!owned)
                {
                    UIKit.Modal(Host.ModalRoot, "LOCKED", item.Name + "\n\n" + item.UnlockText, ("OK", null, UIKit.ButtonStyle.Box));
                    return;
                }
                Audio.AudioController.PlayUI(Audio.SfxId.UIClick);
                Game.Customization.Equip(id);
                Refresh();
            });
            tile.gameObject.AddComponent<UIHover>();
        }
    }
}
