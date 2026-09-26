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
            CosmeticType.WeaponTrail, CosmeticType.Gloves, CosmeticType.Emote, CosmeticType.CrosshairStyle, CosmeticType.CharacterSkin
        };
        private static readonly string[] TypeNames = { "ICONS", "FRAMES", "BANNERS", "TITLES", "KILL FX", "TRACERS", "GLOVES", "EMOTES", "CROSSHAIR", "CHARACTER" };
        private static readonly string[] Filters = { "ALL", "OWNED", "LOCKED", "LEVEL REWARDS", "PRESTIGE", "CHALLENGES", "EVENTS" };

        private int _type;
        private int _filter;
        private RectTransform _grid;
        private RectTransform _preview;
        private Text _owned;
        private OptionSelector _filterSel;
        private string _focused;          // kill fx / tracers: the item being previewed
        private ShopPreview _live;

        private static bool HasLivePreview(CosmeticType t) { return t == CosmeticType.KillEffect || t == CosmeticType.WeaponTrail || t == CosmeticType.Gloves; }

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "CUSTOMIZE", "SHOW OFF YOUR PROGRESS · COSMETICS NEVER AFFECT GAMEPLAY", 1400f);
            UIKit.Tabs(col, TypeNames, 0, i => { _type = i; _focused = null; Refresh(); }, 15);
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
            UIKit.Size(_preview, 560f, 440f);
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

        public override void OnHide()
        {
            ClearLive();
        }

        private void ClearLive()
        {
            if (_live != null) Object.Destroy(_live.gameObject);
            _live = null;
        }

        /// <summary>Kill FX / tracers: a live 3D preview of the selected item + EQUIP, so nobody equips one blind.</summary>
        private void LivePreview(CosmeticType type, string equipped)
        {
            var prog = Game.Progression;
            if (string.IsNullOrEmpty(_focused)) _focused = equipped;
            var item = CosmeticCatalog.Get(_focused);
            if (item == null) return;
            bool owned = prog.IsUnlocked(item.Id);

            UIKit.Caption(_preview, "PREVIEW", 14);
            var frame = UIKit.Panel(_preview, "PreviewFrame", new Color(0.03f, 0.02f, 0.025f, 1f));
            frame.GetComponent<Outline>().effectColor = new Color(0.9f, 0.08f, 0.14f, 0.7f);
            UIKit.Size(frame, 230f);
            _live = ShopPreview.Create(item, 560, 280);
            var raw = new GameObject("Live", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(frame.transform, false);
            raw.texture = _live.Texture;
            raw.raycastTarget = false;
            UIKit.Stretch(raw.rectTransform, 2f, 2f, 2f, 2f);
            var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 2f;
            if (item.Id == "kfx_none")
            {
                var none = UIKit.Label(frame.transform, "NO EFFECT", 22, UIKit.TextDim, TextAnchor.MiddleCenter);
                UIKit.Stretch(none.rectTransform);
            }

            UIKit.Size(UIKit.Label(_preview, item.Name, 30, UIKit.Text), 38f);
            string desc = !string.IsNullOrEmpty(item.Description) ? item.Description
                        : item.Id == "trail_none" ? "THE WEAPON'S NORMAL TRACER." : item.Id == "kfx_none" ? "NO EFFECT ON KILLS." : "";
            if (desc.Length > 0) { var d = UIKit.Label(_preview, desc, 15, UIKit.TextDim); d.horizontalOverflow = HorizontalWrapMode.Wrap; UIKit.Size(d, 40f); }

            bool isEquipped = item.Id == equipped;
            if (isEquipped)
            {
                var b = UIKit.Button(_preview, "EQUIPPED", null, UIKit.ButtonStyle.Box, 20, 52f);
                b.interactable = false;
            }
            else if (owned)
            {
                string id = item.Id;
                UIKit.Button(_preview, "EQUIP", () => { Game.Customization.Equip(id); Toast("EQUIPPED", item.Name); Refresh(); }, UIKit.ButtonStyle.Primary, 22, 52f);
            }
            else
            {
                var b = UIKit.Button(_preview, "LOCKED  ·  " + item.UnlockText, null, UIKit.ButtonStyle.Box, 16, 52f);
                b.interactable = false;
                if (item.Source == UnlockSource.Shop)
                    UIKit.Button(_preview, "GO TO SHOP", () => Host.Push(new ShopScreen()), UIKit.ButtonStyle.Ghost, 16, 44f);
            }
            UIKit.Caption(_preview, "CLICK ANY TILE TO PREVIEW IT", 12);
        }

        private void Refresh()
        {
            ClearLive();
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

            if (HasLivePreview(type))
            {
                LivePreview(type, equipped);
                return;
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
            bool focused = HasLivePreview(item.Type) && item.Id == _focused;
            var tile = UIKit.Panel(_grid, "Tile", equipped ? new Color(0.25f, 0.02f, 0.05f, 0.9f) : focused ? new Color(0.14f, 0.1f, 0.11f, 0.95f) : new Color(0.06f, 0.06f, 0.07f, 0.9f));
            var btn = tile.gameObject.AddComponent<Button>();
            btn.targetGraphic = tile;
            if (equipped || focused) tile.GetComponent<Outline>().effectColor = equipped ? UIKit.Red : Color.white;
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
                if (HasLivePreview(item.Type))
                {
                    Audio.AudioController.PlayUI(Audio.SfxId.UIClick);
                    _focused = id;
                    Refresh();
                    return;
                }
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
