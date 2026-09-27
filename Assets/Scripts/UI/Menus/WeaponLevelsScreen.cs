using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Progression;
using Vamp.Weapons;

namespace Vamp.UI.Menus
{
    /// <summary>
    /// WEAPON LEVELS: every weapon's level, XP bar and its camo unlocks (weapon XP comes from kills on real players
    /// in quick match and ranked). Pick a weapon to see which camos it has unlocked and what's next.
    /// </summary>
    public sealed class WeaponLevelsScreen : MenuScreen
    {
        private RectTransform _list;
        private RectTransform _details;
        private string _selected;

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "WEAPON LEVELS", "KILL REAL PLAYERS IN QUICK MATCH AND RANKED TO LEVEL A WEAPON · EVERY FEW LEVELS UNLOCKS A CAMO FOR THAT WEAPON", 1300f);
            var body = UIKit.Row(col, 600f, 24f, "Body");
            ScrollRect scroll;
            _list = UIKit.ScrollList(body, out scroll, 6f);
            UIKit.Size(scroll, 600f, 640f);
            _details = UIKit.Column(body, 8f, "Details");
            UIKit.Size(_details, 600f, -1, 1f);
            var row = UIKit.Row(col, 46f, 12f);
            UIKit.Size(UIKit.Button(row, "OPEN LOADOUT", () => Host.Push(new LoadoutScreen()), UIKit.ButtonStyle.Box, 16, 44f), 44f, 220f);
            UIKit.Size(UIKit.Button(row, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 16, 44f), 44f, 200f);
            Refresh();
        }

        public override void OnShow()
        {
            if (Game.Progression != null) Game.Progression.ProfileChanged += Refresh;
        }

        public override void OnHide()
        {
            if (Game.Progression != null) Game.Progression.ProfileChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_list == null || Game.Weapons == null || Game.Progression == null || !Game.Progression.IsLoaded) return;
            UIKit.Clear(_list);
            UIKit.Clear(_details);
            var prog = Game.Progression;
            if (string.IsNullOrEmpty(_selected) && Game.Weapons.weapons.Count > 0) _selected = Game.Weapons.weapons[0].id;
            foreach (var w in Game.Weapons.weapons)
            {
                if (w == null) continue;
                var data = w;
                int level = prog.WeaponLevel(w.id), into, needed;
                prog.WeaponLevelProgress(w.id, out into, out needed);
                bool sel = w.id == _selected;

                var tile = UIKit.Panel(_list, "Weapon_" + w.id, sel ? new Color(0.25f, 0.02f, 0.05f, 0.9f) : new Color(0.06f, 0.06f, 0.07f, 0.9f));
                if (sel) tile.GetComponent<Outline>().effectColor = UIKit.Red;
                UIKit.Size(tile, 70f);
                var btn = tile.gameObject.AddComponent<Button>();
                btn.targetGraphic = tile;
                btn.onClick.AddListener(() => { Audio.AudioController.PlayUI(Audio.SfxId.UIClick); _selected = data.id; Refresh(); });
                UIKit.VList(tile.transform, 4f, 10);
                var top = UIKit.Row(tile.transform, 26f, 10f);
                UIKit.Size(UIKit.Label(top, w.displayName, 20, UIKit.Text), -1, 220f);
                UIKit.Spacer(top, 0f, true);
                UIKit.Size(UIKit.Label(top, "LV " + level + (level >= ProgressionService.MaxWeaponLevel ? "  MAX" : ""), 18,
                                       level >= ProgressionService.MaxWeaponLevel ? UIKit.Red : UIKit.Text, TextAnchor.MiddleRight), -1, 140f);
                Bar(tile.transform, needed > 0 ? into / (float)needed : 1f, needed > 0 ? into + " / " + needed + " XP" : "MAX LEVEL");
                tile.gameObject.AddComponent<UIHover>();
            }
            ShowDetails(Game.Weapons.Get(_selected));
        }

        private static void Bar(Transform parent, float t, string text)
        {
            var row = UIKit.Row(parent, 16f, 10f);
            var bg = UIKit.Image(row, "Bar", new Color(1f, 1f, 1f, 0.1f));
            UIKit.Size(bg, 6f, -1, 1f);
            var fill = UIKit.Image(bg.transform, "Fill", UIKit.Red);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            UIKit.Size(UIKit.Label(row, text, 12, UIKit.TextDim, TextAnchor.MiddleRight), -1, 150f);
        }

        private void ShowDetails(WeaponData w)
        {
            if (w == null) return;
            var prog = Game.Progression;
            int level = prog.WeaponLevel(w.id), into, needed;
            prog.WeaponLevelProgress(w.id, out into, out needed);
            UIKit.Heading(_details, w.displayName, 40);
            UIKit.Caption(_details, "WEAPON LEVEL " + level + " / " + ProgressionService.MaxWeaponLevel + "  ·  " + prog.WeaponXp(w.id).ToString("N0") + " TOTAL XP"
                                    + "  ·  +" + ProgressionService.WeaponXpPerKill + " XP PER KILL, +" + ProgressionService.WeaponXpPerHeadshot + " PER HEADSHOT", 14);
            Bar(_details, needed > 0 ? into / (float)needed : 1f, needed > 0 ? into + " / " + needed + " XP" : "MAX LEVEL");
            UIKit.Divider(_details);
            UIKit.Caption(_details, "CAMOS  ·  A NEW CAMO EVERY LEVEL FROM 2 TO 20 (SHOP CAMOS WORK ON EVERY WEAPON)", 14);

            var grid = UIKit.Node("Camos", _details);
            var camos = new System.Collections.Generic.List<CosmeticItem>();
            foreach (var camo in CosmeticCatalog.OfType(CosmeticType.WeaponSkin))
                if (camo.Source != UnlockSource.Shop) camos.Add(camo);
            camos.Sort((a, b) => a.UnlockLevel.CompareTo(b.UnlockLevel));
            int rows = (camos.Count + 4) / 5;
            UIKit.Size(grid, rows * 88f);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(108f, 80f);
            g.spacing = new Vector2(8f, 8f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 5;
            foreach (var camo in camos)
            {
                bool unlocked = prog.IsCamoUnlocked(w.id, camo.Id);
                var cell = UIKit.Panel(grid, camo.Id, new Color(0.06f, 0.06f, 0.07f, 0.95f));
                UIKit.VList(cell.transform, 2f, 6);
                var sw = new GameObject("Swatch", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                sw.transform.SetParent(cell.transform, false);
                UIKit.Size(sw, 30f);
                var tex = !string.IsNullOrEmpty(camo.Texture) ? Resources.Load<Texture2D>("Camos/" + camo.Texture) : null;
                sw.texture = tex;
                sw.uvRect = new Rect(0f, 0f, 1f, 0.3f);
                Color c = tex != null ? Color.white : camo.Id == "skin_default" ? new Color(0.16f, 0.16f, 0.17f) : camo.Color;
                sw.color = unlocked ? c : new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 1f);
                UIKit.Size(UIKit.Label(cell.transform, camo.Name, 11, unlocked ? UIKit.Text : UIKit.TextFaint, TextAnchor.MiddleLeft), 16f);
                string req = unlocked ? "UNLOCKED"
                           : camo.Source == UnlockSource.Shop ? "RANKED SHOP"
                           : "LEVEL " + camo.UnlockLevel;
                UIKit.Size(UIKit.Label(cell.transform, req, 10, unlocked ? UIKit.Good : UIKit.TextDim, TextAnchor.MiddleLeft), 14f);
            }
        }
    }
}
