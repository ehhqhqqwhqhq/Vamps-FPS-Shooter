using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Audio;
using Vamp.Core;
using Vamp.Match;
using Vamp.Progression;
using Vamp.UI.Menus;

namespace Vamp.UI
{
    /// <summary>
    /// Match overlays: pre-match intro (map, mode, player cards), countdown & announcer text (kill streaks, rounds),
    /// TAB scoreboard (icon, level, player, kills, deaths, score, ping), match results → XP breakdown →
    /// level up (single or multiple, with unlocks, skippable) → REMATCH / RETURN TO MENU.
    /// </summary>
    public sealed class MatchUI : MonoBehaviour
    {
        private MatchController _match;
        private Canvas _canvas;
        private RectTransform _root;

        private Text _announce, _announceSub;
        private float _announceTimer;
        private CanvasGroup _announceGroup;

        private RectTransform _intro;
        private RectTransform _scoreboard;
        private RectTransform _scoreRows;
        private float _scoreRefresh;
        private RectTransform _results;
        private bool _resultsQueued;
        private float _resultsDelay;
        private Text _subtitle;

        private void Start()
        {
            _match = GetComponent<MatchController>();
            _canvas = UIFactory.CreateCanvas("Match Canvas", transform, 60);
            _root = (RectTransform)_canvas.transform;
            UIKit.EnsureEventSystem();
            ApplyScale();

            // Announcements (center-top)
            var ann = UIKit.Node("Announce", _root);
            ann.anchorMin = ann.anchorMax = new Vector2(0.5f, 0.68f);
            ann.sizeDelta = new Vector2(1200f, 160f);
            _announceGroup = ann.gameObject.AddComponent<CanvasGroup>();
            _announceGroup.alpha = 0f;
            _announce = UIKit.Label(ann, "", 72, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Stretch(_announce.rectTransform, 0, 0, 0, 50);
            var ol = _announce.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.7f);
            ol.effectDistance = new Vector2(2f, -2f);
            _announceSub = UIKit.Label(ann, "", 22, UIKit.TextDim, TextAnchor.MiddleCenter);
            UIKit.Stretch(_announceSub.rectTransform, 0, 0, 115, 0);

            // Subtitles for announcer lines (accessibility)
            _subtitle = UIKit.Label(_root, "", 20, UIKit.Text, TextAnchor.MiddleCenter);
            var srt = _subtitle.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.2f);
            srt.sizeDelta = new Vector2(900f, 40f);
            _subtitle.enabled = false;

            BuildScoreboard();
            if (_match.Phase == MatchPhase.Intro) BuildIntro();

            _match.Announced += OnAnnounced;
            _match.Ended += OnEnded;
            if (Game.Settings != null) Game.Settings.Changed += OnSettings;
        }

        private void OnDestroy()
        {
            if (_match != null)
            {
                _match.Announced -= OnAnnounced;
                _match.Ended -= OnEnded;
            }
            if (Game.Settings != null) Game.Settings.Changed -= OnSettings;
        }

        private void OnSettings(Settings.GameSettings s) { ApplyScale(); }

        private void ApplyScale()
        {
            var scaler = _canvas.GetComponent<CanvasScaler>();
            float k = Game.Settings != null ? Game.Settings.Current.accessibility.hudScale : 1f;
            scaler.referenceResolution = new Vector2(1920f, 1080f) / Mathf.Max(0.5f, k);
        }

        // ------------------------------------------------------------------ Announcements

        private void OnAnnounced(string title, string sub)
        {
            _announce.text = title;
            _announceSub.text = sub;
            _announce.color = title == "GO" || title == "VICTORY" || title.Contains("SPREE") || title == "RAMPAGE" || title == "UNSTOPPABLE" || title == "DOMINATING"
                ? UIKit.Red : UIKit.Text;
            _announceTimer = title.Length <= 2 ? 0.9f : 2.4f;
            _announceGroup.alpha = 1f;
            _announce.transform.localScale = Vector3.one * 1.25f;

            // Announcer subtitle (sized/backed by accessibility settings)
            if (title.Length > 2 && Game.Settings != null)
            {
                var a = Game.Settings.Current.accessibility;
                _subtitle.enabled = true;
                _subtitle.fontSize = Mathf.RoundToInt(20 * a.subtitleSize);
                _subtitle.text = (a.subtitleBackground ? "<color=#000000AA>▌</color>" : "") + "ANNOUNCER: " + title + (a.subtitleBackground ? "<color=#000000AA>▐</color>" : "");
            }
        }

        // ------------------------------------------------------------------ Intro

        private void BuildIntro()
        {
            _intro = UIKit.StretchNode("Intro", _root);
            var bg = UIKit.Image(_intro, "Bg", new Color(0f, 0f, 0f, 0.75f));
            UIKit.Stretch(bg.rectTransform);
            var col = UIKit.StretchNode("Col", _intro, 120, 120, 140, 120);
            UIKit.VList(col, 10f, 0, TextAnchor.UpperCenter);
            UIKit.Size(UIKit.Label(col, _match.Map.DisplayName, 90, UIKit.Text, TextAnchor.MiddleCenter), 110f);
            UIKit.Size(UIKit.Label(col, _match.Config.ModeLabel + "  ·  " + _match.Map.Description, 20, UIKit.Red, TextAnchor.MiddleCenter), 30f);
            UIKit.Size(UIKit.Label(col, MatchConfig.ModeDescription(_match.Config.mode), 16, UIKit.TextDim, TextAnchor.MiddleCenter, FontStyle.Normal), 26f);
            UIKit.Spacer(col, 30f);
            var cards = UIKit.Row(col, 300f, 18f, "Cards");
            var h = cards.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            int shown = 0;
            foreach (var p in _match.Participants)
            {
                if (p == null || shown >= 5) continue;
                if (_match.Config.IsTeamMode && _match.Local != null && p.Team != _match.Local.Team) continue;
                shown++;
                var data = new PlayerProfileData { level = p.Level, prestige_level = p.Prestige, profile_icon = p.Icon, icon_frame = p.Frame, title = "", title_visible = false };
                if (p.IsLocalPlayer && Game.Progression != null && Game.Progression.Profile != null) data = Game.Progression.Profile;
                var card = IdentityViews.Card(cards, data, p.DisplayName, 210f, 290f, p.IsBot ? "BOT" : "YOU", p.IsLocalPlayer);
            }
        }

        // ------------------------------------------------------------------ Scoreboard

        private void BuildScoreboard()
        {
            var panel = UIKit.Panel(_root, "Scoreboard", new Color(0.03f, 0.03f, 0.035f, 0.93f));
            _scoreboard = panel.rectTransform;
            _scoreboard.anchorMin = _scoreboard.anchorMax = new Vector2(0.5f, 0.5f);
            _scoreboard.sizeDelta = new Vector2(1100f, 680f);
            UIKit.VList(_scoreboard, 6f, 24);
            var head = UIKit.Row(_scoreboard, 40f, 10f);
            UIKit.Size(UIKit.Label(head, _match.Config.ModeLabel, 28, UIKit.Text), -1, -1, 1f);
            UIKit.Size(UIKit.Label(head, _match.Map.DisplayName, 18, UIKit.TextDim, TextAnchor.MiddleRight), -1, 300f);
            UIKit.Divider(_scoreboard, UIKit.Red, 2f);
            var cols = UIKit.Row(_scoreboard, 26f, 10f);
            ColumnHeaders(cols);
            _scoreRows = UIKit.Column(_scoreboard, 3f, "Rows");
            _scoreboard.gameObject.SetActive(false);
        }

        private static readonly float[] Widths = { 64f, 70f, 380f, 110f, 110f, 110f, 110f, 80f };

        private void ColumnHeaders(Transform row)
        {
            string last = _match.Config.mode == GameMode.GunGame ? "WEAPON" : _match.Config.mode == GameMode.MovementRace ? "TIME" : "SCORE";
            string[] names = { "ICON", "LEVEL", "PLAYER", "KILLS", "DEATHS", "ASSISTS", last, "PING" };
            for (int i = 0; i < names.Length; i++)
                UIKit.Size(UIKit.Label(row, names[i], 13, UIKit.TextDim, i == 2 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter), -1, Widths[i]);
        }

        private void RefreshScoreboard()
        {
            UIKit.Clear(_scoreRows);
            var list = new List<Participant>(_match.Participants);
            list.RemoveAll(p => p == null);
            list.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : b.Kills.CompareTo(a.Kills));
            if (_match.Config.IsTeamMode)
            {
                for (int t = 0; t < 2; t++)
                {
                    bool mine = _match.Local != null && _match.Local.Team == t;
                    var th = UIKit.Label(_scoreRows, (mine ? "YOUR TEAM" : "ENEMY TEAM") + "   " + _match.TeamScores[t], 16, mine ? UIKit.AllyColor() : UIKit.EnemyColor());
                    UIKit.Size(th, 28f);
                    foreach (var p in list) if (p.Team == t) Row(p);
                }
            }
            else foreach (var p in list) Row(p);
        }

        private void Row(Participant p)
        {
            var bg = UIKit.Image(_scoreRows, "Row", p.IsLocalPlayer ? new Color(0.5f, 0.03f, 0.07f, 0.5f) : new Color(1f, 1f, 1f, 0.03f));
            UIKit.Size(bg, 46f);
            UIKit.HList(bg.transform, 10f, 3);
            var iconCell = UIKit.Node("IconCell", bg.transform);
            UIKit.Size(iconCell, 40f, Widths[0]);
            var ic = IdentityViews.Icon(iconCell, p.Icon, p.Frame, 38f);
            ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.5f);
            string last = _match.Config.mode == GameMode.GunGame
                ? (Game.Weapons != null && Game.Weapons.gunGameOrder.Count > 0 ? Game.Weapons.gunGameOrder[Mathf.Clamp(p.GunGameLevel, 0, Game.Weapons.gunGameOrder.Count - 1)].displayName : "-")
                : _match.Config.mode == GameMode.MovementRace ? (p.Finished ? p.RaceTime.ToString("0.00") : "-") : p.Score.ToString();
            string[] vals = { null, IdentityViews.LevelText(p.Level, p.Prestige), p.DisplayName + (p.IsBot ? "  <color=#FFFFFF55>BOT</color>" : "") + (p.Alive ? "" : "  <color=#C8102E>✖</color>"),
                              p.Kills.ToString(), p.Deaths.ToString(), p.Assists.ToString(), last, p.IsBot ? "BOT" : "LOCAL" };
            for (int i = 1; i < vals.Length; i++)
            {
                var l = UIKit.Label(bg.transform, vals[i], i == 2 ? 19 : 17, i == 1 ? UIKit.Red : UIKit.Text, i == 2 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                UIKit.Size(l, -1, Widths[i]);
            }
        }

        // ------------------------------------------------------------------ Update

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_announceTimer > 0f)
            {
                _announceTimer -= dt;
                _announce.transform.localScale = Vector3.Lerp(_announce.transform.localScale, Vector3.one, 1f - Mathf.Exp(-12f * dt));
                if (_announceTimer <= 0.3f) _announceGroup.alpha = Mathf.Clamp01(_announceTimer / 0.3f);
                if (_announceTimer <= 0f) _subtitle.enabled = false;
            }

            if (_intro != null && _match.Phase != MatchPhase.Intro)
            {
                Destroy(_intro.gameObject);
                _intro = null;
            }

            bool tab = _match.Player != null && _match.Player.Input.Frame.ScoreboardHeld && _results == null;
            if (_scoreboard.gameObject.activeSelf != tab) _scoreboard.gameObject.SetActive(tab);
            if (tab)
            {
                _scoreRefresh -= dt;
                if (_scoreRefresh <= 0f) { _scoreRefresh = 0.5f; RefreshScoreboard(); }
            }

            if (_resultsQueued)
            {
                _resultsDelay -= dt;
                if (_resultsDelay <= 0f)
                {
                    _resultsQueued = false;
                    ShowResults(_match.Outcome);
                }
            }
        }

        // ------------------------------------------------------------------ Results

        private void OnEnded(MatchOutcome o)
        {
            _resultsQueued = true;
            _resultsDelay = 2.2f;
        }

        private void ShowResults(MatchOutcome o)
        {
            if (o == null) return;
            InputController.SetCursorLocked(false);
            if (_match.Player != null) _match.Player.Input.GameplayInputEnabled = false;
            var pause = GetComponent<PauseMenu>();
            if (pause != null) pause.enabled = false;

            _results = UIKit.StretchNode("Results", _root);
            var bg = UIKit.Image(_results, "Bg", new Color(0.02f, 0.02f, 0.025f, 0.96f));
            bg.raycastTarget = true;
            UIKit.Stretch(bg.rectTransform);
            ShowStandings(o);
        }

        private RectTransform Frame(string title, string sub)
        {
            UIKit.Clear(_results);
            var bg = UIKit.Image(_results, "Bg", new Color(0.02f, 0.02f, 0.025f, 0.96f));
            bg.raycastTarget = true;
            UIKit.Stretch(bg.rectTransform);
            var col = UIKit.StretchNode("Col", _results, 160, 160, 90, 70);
            UIKit.VList(col, 10f, 0, TextAnchor.UpperLeft);
            UIKit.Size(UIKit.Label(col, title, 84, title == "VICTORY" || title == "LEVEL UP" ? UIKit.Red : UIKit.Text), 100f);
            if (!string.IsNullOrEmpty(sub)) UIKit.Size(UIKit.Label(col, sub, 22, UIKit.TextDim), 32f);
            UIKit.Divider(col, UIKit.Red, 2f);
            UIKit.Spacer(col, 10f);
            return col;
        }

        private void ShowStandings(MatchOutcome o)
        {
            var col = Frame("MATCH COMPLETE", o.Title + "  ·  " + o.Subtitle);
            UIKit.Size(UIKit.Label(col, o.Title, 40, o.Won ? UIKit.Red : UIKit.Text), 50f);
            var head = UIKit.Row(col, 26f, 10f);
            foreach (var h in new[] { "#", "PLAYER", "KILLS", "DEATHS", "SCORE", "XP" })
                UIKit.Size(UIKit.Label(head, h, 13, UIKit.TextDim), -1, h == "PLAYER" ? 420f : 120f);
            for (int i = 0; i < o.Standings.Count && i < 10; i++)
            {
                var p = o.Standings[i];
                var row = UIKit.Row(col, 40f, 10f);
                var place = i == 0 ? "1ST" : i == 1 ? "2ND" : i == 2 ? "3RD" : (i + 1) + "TH";
                UIKit.Size(UIKit.Label(row, place, 20, i == 0 ? UIKit.Red : UIKit.Text), -1, 120f);
                UIKit.Size(UIKit.Label(row, IdentityViews.LevelText(p.Level, p.Prestige) + "  " + p.DisplayName, 20, p.IsLocalPlayer ? UIKit.Red : UIKit.Text), -1, 420f);
                UIKit.Size(UIKit.Label(row, p.Kills.ToString(), 20, UIKit.Text), -1, 120f);
                UIKit.Size(UIKit.Label(row, p.Deaths.ToString(), 20, UIKit.Text), -1, 120f);
                UIKit.Size(UIKit.Label(row, p.Score.ToString(), 20, UIKit.Text), -1, 120f);
                UIKit.Size(UIKit.Label(row, p.IsLocalPlayer && o.Xp != null ? "+" + o.Xp.Total : "", 20, UIKit.Red), -1, 120f);
            }
            UIKit.Spacer(col, 0f, true);
            var buttons = UIKit.Row(col, 56f, 12f);
            if (o.Xp != null)
            {
                var next = UIKit.Button(buttons, "CONTINUE", () => ShowXp(o), UIKit.ButtonStyle.Primary, 20, 54f);
                UIKit.Size(next, 54f, 280f);
            }
            else AddEndButtons(buttons);
        }

        private void ShowXp(MatchOutcome o)
        {
            var col = Frame("MATCH COMPLETE", "EXPERIENCE");
            foreach (var line in o.Xp.Lines)
            {
                var row = UIKit.Row(col, 36f, 10f);
                UIKit.Size(UIKit.Label(row, line.Label, 22, UIKit.Text), -1, 520f);
                UIKit.Size(UIKit.Label(row, "+" + line.Xp.ToString("N0"), 22, UIKit.Text, TextAnchor.MiddleRight), -1, 200f);
            }
            UIKit.Divider(col);
            var total = UIKit.Row(col, 46f, 10f);
            UIKit.Size(UIKit.Label(total, "TOTAL", 28, UIKit.Red), -1, 520f);
            UIKit.Size(UIKit.Label(total, "+" + o.Xp.Total.ToString("N0") + " XP", 28, UIKit.Red, TextAnchor.MiddleRight), -1, 200f);
            foreach (var c in o.Xp.CompletedChallenges)
                UIKit.Size(UIKit.Label(col, "CHALLENGE COMPLETE  ·  " + c, 18, UIKit.Good), 28f);
            AudioController.PlayUI(SfxId.Unlock, 0.6f);
            UIKit.Spacer(col, 0f, true);
            var buttons = UIKit.Row(col, 56f, 12f);
            if (o.Xp.LevelsGained > 0 || o.Xp.Unlocks.Count > 0)
            {
                var next = UIKit.Button(buttons, "CONTINUE", () => ShowLevelUp(o), UIKit.ButtonStyle.Primary, 20, 54f);
                UIKit.Size(next, 54f, 280f);
            }
            else AddEndButtons(buttons);
        }

        private void ShowLevelUp(MatchOutcome o)
        {
            var xp = o.Xp;
            string sub = xp.LevelsGained > 1 ? xp.OldLevel + " → " + xp.NewLevel + "   ·   " + xp.LevelsGained + " LEVELS GAINED"
                       : xp.LevelsGained == 1 ? "LEVEL " + xp.NewLevel + " UNLOCKED" : "NEW UNLOCKS";
            var col = Frame(xp.LevelsGained > 0 ? "LEVEL UP" : "UNLOCKED", sub);
            if (xp.LevelsGained > 0)
            {
                var big = UIKit.Label(col, xp.NewLevel.ToString(), 150, UIKit.Text, TextAnchor.MiddleLeft);
                UIKit.Size(big, 170f);
                big.gameObject.AddComponent<LevelPop>();
            }
            if (xp.Unlocks.Count > 0)
            {
                UIKit.Size(UIKit.Label(col, "NEW UNLOCKS:", 18, UIKit.TextDim), 26f);
                var row = UIKit.Row(col, 170f, 18f, "Unlocks");
                row.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
                foreach (var id in xp.Unlocks)
                {
                    var item = CosmeticCatalog.Get(id);
                    if (item == null) continue;
                    var cell = UIKit.Column(row, 6f);
                    cell.sizeDelta = new Vector2(150f, 170f);
                    var iconRow = UIKit.Row(cell, 100f, 0f);
                    iconRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                    iconRow.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
                    if (item.Type == CosmeticType.Icon) IdentityViews.Icon(iconRow, item.Id, null, 96f);
                    else
                    {
                        var sw = UIKit.Image(iconRow, "Swatch", item.Color);
                        sw.rectTransform.sizeDelta = new Vector2(96f, 96f);
                    }
                    UIKit.Size(UIKit.Label(cell, item.Name, 14, UIKit.Text, TextAnchor.MiddleCenter), 20f);
                    UIKit.Size(UIKit.Label(cell, item.Type.ToString().ToUpperInvariant(), 11, UIKit.TextDim, TextAnchor.MiddleCenter), 16f);
                }
            }
            AudioController.PlayUI(SfxId.LevelUp, 0.8f);
            SimpleBurst();
            UIKit.Spacer(col, 0f, true);
            var buttons = UIKit.Row(col, 56f, 12f);
            AddEndButtons(buttons);
        }

        private void SimpleBurst()
        {
            // Particle-ish reveal: a few red shards flying out (UI only, skippable by clicking on).
            for (int i = 0; i < 16; i++)
            {
                var shard = UIKit.Image(_results, "Shard", new Color(0.8f, 0.05f, 0.1f, 0.9f));
                shard.rectTransform.anchorMin = shard.rectTransform.anchorMax = new Vector2(0.2f, 0.6f);
                shard.rectTransform.sizeDelta = new Vector2(6f, 26f);
                var f = shard.gameObject.AddComponent<ShardFly>();
                float a = i / 16f * Mathf.PI * 2f;
                f.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(300f, 700f);
            }
        }

        private void AddEndButtons(Transform row)
        {
            var re = UIKit.Button(row, "REMATCH", () => _match.Rematch(), UIKit.ButtonStyle.Primary, 20, 54f);
            UIKit.Size(re, 54f, 260f);
            var menu = UIKit.Button(row, "RETURN TO MENU", () => _match.Leave(), UIKit.ButtonStyle.Box, 20, 54f);
            UIKit.Size(menu, 54f, 300f);
        }
    }
}
