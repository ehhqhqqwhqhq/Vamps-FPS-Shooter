using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Progression;

namespace Vamp.UI.Menus
{
    /// <summary>Loads the shop's generated UI art (Resources/UI) as 9-sliced sprites.</summary>
    internal static class ShopArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name, float border = 0f)
        {
            Sprite s;
            if (Cache.TryGetValue(name, out s) && s != null) return s;
            var tex = Resources.Load<Texture2D>("UI/" + name);
            if (tex == null) return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                              SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            s.name = name;
            Cache[name] = s;
            return s;
        }

        public static Image Sliced(Transform parent, string name, string sprite, float border, Color? tint = null)
        {
            var img = UIKit.Image(parent, name, tint ?? Color.white);
            img.sprite = Get(sprite, border);
            img.type = border > 0f ? Image.Type.Sliced : Image.Type.Simple;
            img.preserveAspect = border <= 0f;
            return img;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static readonly Color Gold = new Color(1f, 0.78f, 0.22f, 1f);
    }

    /// <summary>
    /// RANKED SHOP: spend coins earned in ranked (wins + kills on real players) on camos, bullet trails and kill effects.
    /// Categories on the left, live 3D previews on every card. No real-money purchases.
    /// </summary>
    public sealed class ShopScreen : MenuScreen
    {
        private static readonly string[] CategoryNames = { "ALL", "WEAPON TRAILS", "GUN SKINS", "KILL FX" };
        private static readonly string[] CategoryIcons = { "IconAll", "IconTrail", "IconGun", "IconSkull" };

        private int _category;
        private RectTransform _grid;
        private RectTransform _cats;
        private Text _coins;
        private readonly List<ShopPreview> _previews = new List<ShopPreview>();

        public override bool ShowTopBar { get { return false; } }

        protected override void OnBuild(RectTransform root)
        {
            var shade = UIKit.Image(root, "Shade", new Color(0.01f, 0.005f, 0.008f, 0.92f));
            UIKit.Stretch(shade.rectTransform);

            // Title with glow + streaks
            var glow = ShopArt.Sliced(root, "TitleGlow", "Glow", 0f, new Color(1f, 1f, 1f, 0.55f));
            glow.preserveAspect = false;
            ShopArt.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(900f, 190f));
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    var st = ShopArt.Sliced(root, "Streak", "Streak", 0f, new Color(1f, 1f, 1f, 0.8f - i * 0.2f));
                    st.preserveAspect = false;
                    ShopArt.Place(st.rectTransform, new Vector2(0.5f, 1f), new Vector2(side < 0 ? 1f : 0f, 0.5f),
                                  new Vector2(side * (190f + i * 12f), -90f + (i - 1) * 16f), new Vector2(300f - i * 60f, 8f));
                    st.rectTransform.localScale = new Vector3(side < 0 ? 1f : -1f, 1f, 1f);
                }
            }
            var title = UIKit.Label(root, "SHOP", 118, new Color(0.95f, 0.08f, 0.12f), TextAnchor.MiddleCenter);
            ShopArt.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(700f, 150f));
            var ol = title.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 0.1f, 0.15f, 0.35f);
            ol.effectDistance = new Vector2(4f, -4f);
            var sh = title.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.4f, 0f, 0.03f, 0.9f);
            sh.effectDistance = new Vector2(0f, -6f);
            var sub = UIKit.Label(root, "RANKED SHOP  ·  COINS ARE EARNED BY WINNING RANKED MATCHES AND KILLING REAL PLAYERS", 14, UIKit.TextDim, TextAnchor.MiddleCenter);
            ShopArt.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1200f, 24f));

            // Back (top left) + coins (top right)
            var back = UIKit.Button(root, "‹  BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 46f);
            ShopArt.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -40f), new Vector2(170f, 46f));

            var coinBox = ShopArt.Sliced(root, "Coins", "Chip", 18f, new Color(1f, 1f, 1f, 0.95f));
            ShopArt.Place(coinBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -36f), new Vector2(330f, 70f));
            var coinIcon = ShopArt.Sliced(coinBox.transform, "CoinIcon", "Coin", 0f);
            ShopArt.Place(coinIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(40f, 40f));
            _coins = UIKit.Label(coinBox.transform, "0", 32, ShopArt.Gold, TextAnchor.MiddleLeft);
            ShopArt.Place(_coins.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, 8f), new Vector2(240f, 40f));
            var coinLbl = UIKit.Label(coinBox.transform, "RANKED COINS", 11, UIKit.TextDim, TextAnchor.MiddleLeft);
            ShopArt.Place(coinLbl.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(74f, -20f), new Vector2(240f, 16f));

            // Frame
            var frame = ShopArt.Sliced(root, "Frame", "ShopPanel", 60f);
            var frt = frame.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 1f);
            frt.offsetMin = new Vector2(40f, 30f); frt.offsetMax = new Vector2(-40f, -190f);

            // Categories
            _cats = UIKit.Node("Categories", frame.transform);
            _cats.anchorMin = new Vector2(0f, 0f); _cats.anchorMax = new Vector2(0f, 1f);
            _cats.pivot = new Vector2(0f, 1f);
            _cats.offsetMin = new Vector2(34f, 30f); _cats.offsetMax = new Vector2(324f, -30f);
            UIKit.VList(_cats, 14f);

            // Grid (scrolls if there are more items than fit)
            var area = UIKit.Node("GridArea", frame.transform);
            area.anchorMin = new Vector2(0f, 0f); area.anchorMax = new Vector2(1f, 1f);
            area.offsetMin = new Vector2(350f, 26f); area.offsetMax = new Vector2(-30f, -26f);
            ScrollRect scroll;
            _grid = UIKit.ScrollList(area, out scroll, 0f);
            UIKit.Stretch((RectTransform)scroll.transform);
            Object.DestroyImmediate(_grid.GetComponent<VerticalLayoutGroup>());
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(452f, 372f);
            grid.spacing = new Vector2(22f, 22f);
            grid.padding = new RectOffset(6, 6, 6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            Refresh();
        }

        public override void OnShow()
        {
            if (Game.Progression != null) Game.Progression.ProfileChanged += Refresh;
        }

        public override void OnHide()
        {
            if (Game.Progression != null) Game.Progression.ProfileChanged -= Refresh;
            ClearPreviews();
        }

        private void ClearPreviews()
        {
            foreach (var p in _previews) if (p != null) Object.Destroy(p.gameObject);
            _previews.Clear();
        }

        private bool InCategory(CosmeticItem item)
        {
            switch (_category)
            {
                case 1: return item.Type == CosmeticType.WeaponTrail;
                case 2: return item.Type == CosmeticType.WeaponSkin;
                case 3: return item.Type == CosmeticType.KillEffect;
                default: return true;
            }
        }

        private void Refresh()
        {
            if (_grid == null || Game.Progression == null || !Game.Progression.IsLoaded) return;
            _coins.text = Game.Progression.Profile.ranked_coins.ToString("N0");

            UIKit.Clear(_cats);
            var head = UIKit.Row(_cats, 40f, 10f);
            var bars = UIKit.Label(head, "≡", 26, UIKit.Red, TextAnchor.MiddleLeft);
            UIKit.Size(bars, -1, 26f);
            UIKit.Label(head, "CATEGORIES", 20, UIKit.Text, TextAnchor.MiddleLeft);
            for (int i = 0; i < CategoryNames.Length; i++) CategoryButton(i);

            UIKit.Clear(_grid);
            ClearPreviews();
            var items = CosmeticCatalog.ShopItems();
            // Trails, skins, kill fx - the same order as the categories
            items.Sort((a, b) => Order(a.Type).CompareTo(Order(b.Type)));
            foreach (var item in items) if (InCategory(item)) Card(item);
        }

        private static int Order(CosmeticType t)
        {
            return t == CosmeticType.WeaponTrail ? 0 : t == CosmeticType.WeaponSkin ? 1 : 2;
        }

        private void CategoryButton(int index)
        {
            bool on = index == _category;
            var bg = ShopArt.Sliced(_cats, "Cat_" + CategoryNames[index], on ? "CatButtonOn" : "CatButton", 26f);
            UIKit.Size(bg, 86f);
            bg.raycastTarget = true; // UIKit images don't catch clicks by default
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = cb;
            int idx = index;
            btn.onClick.AddListener(() =>
            {
                Audio.AudioController.PlayUI(Audio.SfxId.UIClick);
                _category = idx;
                Refresh();
            });
            var icon = ShopArt.Sliced(bg.transform, "Icon", CategoryIcons[index], 0f, on ? Color.white : new Color(1f, 1f, 1f, 0.75f));
            ShopArt.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(46f, 46f));
            var label = UIKit.Label(bg.transform, CategoryNames[index], 19, on ? UIKit.Text : new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleLeft);
            ShopArt.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(190f, 40f));
        }

        private static string TypeLabel(CosmeticType t)
        {
            return t == CosmeticType.WeaponTrail ? "WEAPON TRAILS" : t == CosmeticType.WeaponSkin ? "GUN SKINS" : "KILL FX";
        }

        private static string TypeIcon(CosmeticType t)
        {
            return t == CosmeticType.WeaponTrail ? "IconTrail" : t == CosmeticType.WeaponSkin ? "IconGun" : "IconSkull";
        }

        private void Card(CosmeticItem item)
        {
            var prog = Game.Progression;
            bool owned = prog.Owns(item.Id);
            bool equipped = owned && Game.Customization.Equipped(item.Type) == item.Id;

            var card = ShopArt.Sliced(_grid, "Card_" + item.Id, "ShopCard", 60f);
            card.raycastTarget = true; // lets the mouse wheel / drag scroll the grid

            // Live preview
            var preview = ShopPreview.Create(item);
            _previews.Add(preview);
            var raw = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            raw.transform.SetParent(card.transform, false);
            raw.texture = preview.Texture;
            raw.raycastTarget = false;
            var prt = raw.rectTransform;
            prt.anchorMin = new Vector2(0f, 1f); prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.offsetMin = new Vector2(26f, -206f); prt.offsetMax = new Vector2(-26f, -24f);

            // Category chip
            var chip = ShopArt.Sliced(card.transform, "Chip", "Chip", 16f);
            ShopArt.Place(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(190f, 38f));
            var chipIcon = ShopArt.Sliced(chip.transform, "Icon", TypeIcon(item.Type), 0f);
            ShopArt.Place(chipIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(22f, 22f));
            var chipText = UIKit.Label(chip.transform, TypeLabel(item.Type), 13, UIKit.Text, TextAnchor.MiddleLeft);
            ShopArt.Place(chipText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(150f, 24f));

            // Name + description
            var name = UIKit.Label(card.transform, item.Name, 28, UIKit.Text, TextAnchor.MiddleLeft);
            ShopArt.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -212f), new Vector2(390f, 38f));
            var desc = UIKit.Label(card.transform, item.Description ?? "", 14, UIKit.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            ShopArt.Place(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -250f), new Vector2(390f, 40f));

            // Price + buy
            if (!owned)
            {
                var coin = ShopArt.Sliced(card.transform, "Coin", "Coin", 0f);
                ShopArt.Place(coin.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(34f, 34f));
                bool afford = prog.Profile.ranked_coins >= item.Price;
                var price = UIKit.Label(card.transform, item.Price.ToString("N0"), 28, afford ? ShopArt.Gold : new Color(0.75f, 0.55f, 0.2f), TextAnchor.MiddleLeft);
                ShopArt.Place(price.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(72f, 28f), new Vector2(170f, 38f));
            }
            else
            {
                var have = UIKit.Label(card.transform, item.Type == CosmeticType.WeaponSkin ? "OWNED · EQUIP IN LOADOUT" : equipped ? "EQUIPPED" : "OWNED",
                                       15, equipped ? UIKit.Red : UIKit.TextDim, TextAnchor.MiddleLeft);
                ShopArt.Place(have.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 32f), new Vector2(240f, 30f));
            }

            string label = !owned ? "BUY" : item.Type == CosmeticType.WeaponSkin ? "LOADOUT" : equipped ? "EQUIPPED" : "EQUIP";
            var buyBg = ShopArt.Sliced(card.transform, "Buy", owned ? "OwnedButton" : "BuyButton", 22f);
            ShopArt.Place(buyBg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 22f), new Vector2(160f, 58f));
            buyBg.raycastTarget = true;
            var buy = buyBg.gameObject.AddComponent<Button>();
            buy.targetGraphic = buyBg;
            var bcb = buy.colors;
            bcb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            bcb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            buy.colors = bcb;
            buy.interactable = !(owned && equipped);
            var bl = UIKit.Label(buyBg.transform, label, label.Length > 5 ? 20 : 26, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Stretch(bl.rectTransform);
            buyBg.gameObject.AddComponent<UIHover>();

            var captured = item;
            buy.onClick.AddListener(() => OnBuy(captured));
        }

        private void OnBuy(CosmeticItem item)
        {
            Audio.AudioController.PlayUI(Audio.SfxId.UIClick);
            var prog = Game.Progression;
            if (prog.Owns(item.Id))
            {
                if (item.Type == CosmeticType.WeaponSkin) { Host.Push(new LoadoutScreen()); return; }
                Game.Customization.Equip(item.Id);
                Toast("EQUIPPED", item.Name);
                Refresh();
                return;
            }
            if (prog.Profile.ranked_coins < item.Price)
            {
                UIKit.Modal(Host.ModalRoot, "NOT ENOUGH COINS",
                            item.Name + " COSTS " + item.Price.ToString("N0") + " COINS. YOU HAVE " + prog.Profile.ranked_coins.ToString("N0") + ".\n\n"
                            + "EARN " + ProgressionService.CoinsPerWin + " COINS PER RANKED WIN AND " + ProgressionService.CoinsPerKill + " PER KILL ON A REAL PLAYER.",
                            ("OK", null, UIKit.ButtonStyle.Box));
                return;
            }
            UIKit.Modal(Host.ModalRoot, "BUY " + item.Name + "?", "PRICE: " + item.Price.ToString("N0") + " RANKED COINS",
                ("BUY", () =>
                {
                    string error;
                    if (prog.Purchase(item.Id, out error))
                    {
                        if (item.Type != CosmeticType.WeaponSkin) Game.Customization.Equip(item.Id);
                        Toast("PURCHASED", item.Name + (item.Type == CosmeticType.WeaponSkin ? " · EQUIP IT ON ANY WEAPON IN LOADOUT" : " · EQUIPPED"));
                    }
                    else Toast("PURCHASE FAILED", error, true);
                    Refresh();
                }, UIKit.ButtonStyle.Primary),
                ("CANCEL", null, UIKit.ButtonStyle.Box));
        }
    }

    /// <summary>RANKED LEADERBOARD (Unity Leaderboards): top players by rank points + your position.</summary>
    public sealed class LeaderboardScreen : MenuScreen
    {
        private RectTransform _rows;
        private Text _status;
        private RectTransform _mine;

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "LEADERBOARD", "RANKED · TOP PLAYERS BY RANK POINTS (RP)", 980f);
            _mine = UIKit.Row(col, 50f, 12f, "Mine");
            _status = UIKit.Label(col, "LOADING...", 16, UIKit.TextDim);
            UIKit.Size(_status, 24f);
            var head = UIKit.Row(col, 26f, 12f);
            Col(head, "#", 80f, UIKit.TextFaint, 14);
            Col(head, "PLAYER", -1, UIKit.TextFaint, 14);
            Col(head, "RANK", 180f, UIKit.TextFaint, 14);
            Col(head, "RP", 120f, UIKit.TextFaint, 14);
            UIKit.Divider(col);
            ScrollRect scroll;
            _rows = UIKit.ScrollList(col, out scroll, 4f);
            UIKit.Size(scroll, 400f, -1, 1f);
            var buttons = UIKit.Row(col, 46f, 12f);
            UIKit.Size(UIKit.Button(buttons, "REFRESH", Load, UIKit.ButtonStyle.Box, 16, 44f), 44f, 200f);
            UIKit.Size(UIKit.Button(buttons, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 16, 44f), 44f, 200f);
            Load();
        }

        private static Text Col(Transform row, string text, float width, Color color, int size)
        {
            var l = UIKit.Label(row, text, size, color);
            UIKit.Size(l, -1, width, width < 0f ? 1f : -1f);
            return l;
        }

        private void Load()
        {
            UIKit.Clear(_rows);
            UIKit.Clear(_mine);
            var prof = Game.Progression != null ? Game.Progression.Profile : null;
            if (prof != null)
            {
                Col(_mine, "YOU", 80f, UIKit.Red, 20);
                Col(_mine, Game.Username, -1, UIKit.Text, 20);
                Col(_mine, RankTiers.Name(prof.rank_points), 180f, RankTiers.TierColor(prof.rank_points), 20);
                Col(_mine, prof.rank_points.ToString(), 120f, UIKit.Text, 20);
            }
            var lb = Game.Leaderboard;
            if (lb == null) { _status.text = "THE LEADERBOARD NEEDS THE ONLINE BUILD"; return; }
            _status.text = "LOADING...";
            if (prof != null) lb.Submit(prof.rank_points);
            lb.Fetch(50, (ok, error, rows, me) =>
            {
                if (_rows == null) return;
                if (!ok) { _status.text = "COULD NOT LOAD THE LEADERBOARD: " + error; return; }
                _status.text = rows.Count == 0 ? "NO RANKED PLAYERS YET - PLAY A RANKED MATCH TO BE THE FIRST" :
                               me != null ? "YOUR POSITION: #" + me.Rank : "";
                foreach (var r in rows)
                {
                    var row = UIKit.Row(_rows, 36f, 12f);
                    var bg = row.gameObject.AddComponent<Image>();
                    bg.color = r.IsLocal ? new Color(0.78f, 0.05f, 0.11f, 0.25f) : new Color(1f, 1f, 1f, r.Rank % 2 == 0 ? 0.02f : 0.045f);
                    bg.raycastTarget = false;
                    Col(row, "  " + r.Rank, 80f, r.Rank <= 3 ? ShopArt.Gold : UIKit.Text, 18);
                    Col(row, r.Name, -1, UIKit.Text, 18);
                    Col(row, RankTiers.Name(r.Points), 180f, RankTiers.TierColor(r.Points), 18);
                    Col(row, r.Points.ToString(), 120f, UIKit.Text, 18);
                }
            });
        }
    }
}
