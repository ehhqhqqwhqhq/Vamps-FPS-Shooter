using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vamp.Combat;
using Vamp.Core;
using Vamp.Match;
using Vamp.Movement;
using Vamp.Player;
using Vamp.Settings;
using Vamp.Weapons;

namespace Vamp.UI
{
    /// <summary>
    /// Competitive HUD (uGUI, built in code):
    ///   TOP LEFT objective · TOP CENTER mode, score + timer · TOP RIGHT kill feed · CENTER crosshair, hit marker,
    ///   damage direction · BOTTOM LEFT health/armor · BOTTOM CENTER speed + dash · BOTTOM RIGHT weapon/ammo/heat.
    /// Reads game state only. Crosshair, hit markers, damage indicators, HUD scale and colours follow settings.
    /// Text re-renders only when values change (no per-frame garbage).
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private float hitMarkerDuration = 0.25f;
        [SerializeField] private float killFeedLifetime = 5f;
        [SerializeField] private int killFeedMaxEntries = 5;

        private sealed class FeedEntry
        {
            public RectTransform Root;
            public Image Background;
            public Text Label;
            public float Age;
            public bool Active;
        }

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private Text _health, _armor, _ammo, _weaponName, _speed, _timer, _mode, _score, _objective, _death, _peak, _state, _hint;
        private Image _healthFill, _armorFill, _speedFill, _reloadFill, _heatFill;
        private RectTransform _crosshairRoot, _damageRoot, _scope;
        private readonly List<Image> _dashPips = new List<Image>();
        private readonly Image[] _hitLines = new Image[4];
        private readonly List<FeedEntry> _feed = new List<FeedEntry>();
        private readonly List<KeyValuePair<Image, float>> _damageArrows = new List<KeyValuePair<Image, float>>();
        private float _hitTimer;
        private Color _hitColor = Color.white;
        private float _hitScale = 1f;
        private float _time;
        private bool _subscribed;
        private float _lastSpread = -1f;
        private GameSettings _settings;

        private int _lastHealth = -1, _lastArmor = -1, _lastMag = -1, _lastReserve = -2, _lastSpeed = -1, _lastSeconds = -99, _lastPeak = -1;
        private int _lastDeathTenths = -1;
        private string _lastScore, _lastObjective;
        private WeaponData _lastWeapon;
        private MovementState _lastState = (MovementState)(-1);

        private const float HealthBarWidth = 180f;
        private const float SpeedBarWidth = 220f;
        private const float ReloadBarWidth = 60f;

        private void Awake()
        {
            Build();
            if (Game.Settings != null)
            {
                Game.Settings.Changed += OnSettings;
                OnSettings(Game.Settings.Current);
            }
            KillFeed.EntryAdded += OnKill;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            KillFeed.EntryAdded -= OnKill;
            if (Game.Settings != null) Game.Settings.Changed -= OnSettings;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (player == null) player = PlayerController.Local != null ? PlayerController.Local : FindAnyObjectByType<PlayerController>();
            if (player == null || player.Weapons == null || player.Health == null) return;
            player.Weapons.HitConfirmed += OnHitConfirmed;
            player.Health.Damaged += OnDamaged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || player == null) return;
            if (player.Weapons != null) player.Weapons.HitConfirmed -= OnHitConfirmed;
            if (player.Health != null) player.Health.Damaged -= OnDamaged;
            _subscribed = false;
        }

        private void OnSettings(GameSettings s)
        {
            _settings = s;
            if (_scaler != null) _scaler.referenceResolution = new Vector2(1920f, 1080f) / Mathf.Max(0.5f, s.accessibility.hudScale);
            _lastSpread = -1f; // redraw crosshair
        }

        // ------------------------------------------------------------------ Build

        private void Build()
        {
            _canvas = UIFactory.CreateCanvas("HUD Canvas", transform, 10);
            _scaler = _canvas.GetComponent<CanvasScaler>();
            Transform root = _canvas.transform;
            var W = UIFactory.White;
            var R = UIFactory.DeepRed;

            // TOP LEFT - objective
            _objective = UIFactory.Label("Objective", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(640f, 30f), 20, TextAnchor.UpperLeft, UIFactory.DimWhite);
            _objective.supportRichText = true;
            _peak = UIFactory.Label("Peak", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -56f), new Vector2(600f, 26f), 16, TextAnchor.UpperLeft, UIFactory.DimWhite, FontStyle.Normal);

            // TOP CENTER - mode, score, timer
            UIFactory.Box("TopBar", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(300f, 84f), UIFactory.DarkGray);
            UIFactory.Box("TopAccent", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(300f, 2f), R);
            _mode = UIFactory.Label("Mode", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(300f, 20f), 14, TextAnchor.MiddleCenter, UIFactory.DimWhite);
            _timer = UIFactory.Label("Timer", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(300f, 34f), 30, TextAnchor.MiddleCenter, W);
            _score = UIFactory.Label("Score", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(300f, 24f), 17, TextAnchor.MiddleCenter, W);
            _score.supportRichText = true;

            // CENTER - crosshair, hit marker, damage direction, reload, scope
            _scope = UIKit.StretchNode("Scope", root);
            BuildScope(_scope);
            _scope.gameObject.SetActive(false);

            _crosshairRoot = UIFactory.Rect("Crosshair", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            var hitRoot = UIFactory.Rect("HitMarker", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i == 0 || i == 3) ? 1f : -1f;
                float sy = (i < 2) ? 1f : -1f;
                var line = UIFactory.Box("Hit" + i, hitRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(sx * 11f, sy * 11f), new Vector2(3f, 12f), W);
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sx * sy * -45f);
                line.enabled = false;
                _hitLines[i] = line;
            }
            _damageRoot = UIFactory.Rect("DamageIndicators", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));

            _reloadFill = UIFactory.Bar("Reload", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(ReloadBarWidth, 3f), W);
            _reloadFill.transform.parent.gameObject.SetActive(false);

            _death = UIFactory.Label("Death", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1100f, 120f), 34, TextAnchor.MiddleCenter, R);
            _death.gameObject.SetActive(false);

            // BOTTOM LEFT - health / armor (compact, no panel - text shadow keeps it readable on bright maps)
            _health = Shadowed(UIFactory.Label("Health", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 44f), new Vector2(90f, 40f), 36, TextAnchor.LowerLeft, W));
            Shadowed(UIFactory.Label("HPLabel", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(104f, 48f), new Vector2(30f, 16f), 12, TextAnchor.LowerLeft, UIFactory.DimWhite)).text = "HP";
            _armor = Shadowed(UIFactory.Label("Armor", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(132f, 48f), new Vector2(90f, 16f), 12, TextAnchor.LowerLeft, UIFactory.DimWhite));
            _healthFill = UIFactory.Bar("HealthBar", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 36f), new Vector2(HealthBarWidth, 4f), W);
            _armorFill = UIFactory.Bar("ArmorBar", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 29f), new Vector2(HealthBarWidth, 2f), new Color(0.6f, 0.75f, 1f, 1f));

            // BOTTOM CENTER - speed + dash
            _speed = UIFactory.Label("Speed", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(240f, 40f), 34, TextAnchor.LowerCenter, W);
            _speed.supportRichText = true;
            _state = UIFactory.Label("State", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 98f), new Vector2(240f, 20f), 14, TextAnchor.LowerCenter, UIFactory.DimWhite);
            _speedFill = UIFactory.Bar("SpeedBar", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(SpeedBarWidth, 4f), R);

            // BOTTOM RIGHT - weapon / ammo / heat (compact, no panel)
            _ammo = Shadowed(UIFactory.Label("Ammo", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 36f), new Vector2(220f, 42f), 34, TextAnchor.LowerRight, W));
            _ammo.supportRichText = true;
            _weaponName = Shadowed(UIFactory.Label("Weapon", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 78f), new Vector2(220f, 18f), 13, TextAnchor.LowerRight, UIFactory.DimWhite));
            _heatFill = UIFactory.Bar("Heat", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 29f), new Vector2(160f, 3f), new Color(1f, 0.5f, 0.1f));
            _heatFill.transform.parent.gameObject.SetActive(false);

            _hint = UIFactory.Label("Hint", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(1100f, 20f), 13, TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.35f), FontStyle.Normal);

            // TOP RIGHT - kill feed pool
            for (int i = 0; i < killFeedMaxEntries; i++)
            {
                var bg = UIFactory.Box("Feed" + i, root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f - i * 34f), new Vector2(460f, 30f), UIFactory.DarkGray);
                var label = UIFactory.Label("Text", bg.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(440f, 30f), 16, TextAnchor.MiddleRight, W);
                label.supportRichText = true;
                bg.gameObject.SetActive(false);
                _feed.Add(new FeedEntry { Root = bg.rectTransform, Background = bg, Label = label });
            }
        }

        private static Text Shadowed(Text t)
        {
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.75f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private static Texture2D _scopeTex;

        /// <summary>Round sniper scope: circular lens (screen-height sized), black outside, thick posts + fine centre cross.</summary>
        private static void BuildScope(RectTransform root)
        {
            var lens = UIKit.Node("Lens", root);
            lens.anchorMin = new Vector2(0.5f, 0f);
            lens.anchorMax = new Vector2(0.5f, 1f);
            lens.pivot = new Vector2(0.5f, 0.5f);
            lens.sizeDelta = Vector2.zero;
            var fit = lens.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fit.aspectRatio = 1f;
            var img = lens.gameObject.AddComponent<RawImage>();
            img.texture = ScopeTexture();
            img.raycastTarget = false;

            // Black out everything left and right of the lens.
            foreach (float side in new[] { -1f, 1f })
            {
                var bar = UIKit.Image(lens, side < 0f ? "Left" : "Right", Color.black);
                var rt = bar.rectTransform;
                rt.anchorMin = new Vector2(side < 0f ? 0f : 1f, 0f);
                rt.anchorMax = new Vector2(side < 0f ? 0f : 1f, 1f);
                rt.pivot = new Vector2(side < 0f ? 1f : 0f, 0.5f);
                rt.sizeDelta = new Vector2(6000f, 0f);
                rt.anchoredPosition = new Vector2(side * 0.5f, 0f);
            }
            var dot = UIKit.Image(root, "Dot", new Color(0.95f, 0.05f, 0.1f, 1f));
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(4f, 4f);
        }

        private static Texture2D ScopeTexture()
        {
            if (_scopeTex != null) return _scopeTex;
            const int n = 1024;
            _scopeTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "VampScope", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f, r = n * 0.5f - 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;           // 0 centre → 1 rim
                    float a;
                    if (d >= 1f) a = 1f;                                     // outside the lens: black
                    else
                    {
                        a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.86f, 1f, d)) * 0.95f; // dark rim / vignette
                        float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                        bool post = d > 0.32f;                               // thick posts toward the edge
                        float w = post ? n * 0.006f : n * 0.0011f;
                        if ((ax < w && (post || ay < r)) || (ay < w && (post || ax < r))) a = Mathf.Max(a, post ? 1f : 0.9f);
                        // small range marks on the fine cross
                        if (!post && ax < n * 0.006f && Mathf.Abs(ay % (n * 0.04f)) < 1.2f && ay > 4f) a = Mathf.Max(a, 0.85f);
                        if (!post && ay < n * 0.006f && Mathf.Abs(ax % (n * 0.04f)) < 1.2f && ax > 4f) a = Mathf.Max(a, 0.85f);
                    }
                    px[y * n + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            _scopeTex.SetPixels32(px);
            _scopeTex.Apply(false, true);
            return _scopeTex;
        }

        // ------------------------------------------------------------------ Events

        private void OnHitConfirmed(HitReport report)
        {
            if (_settings != null && !_settings.accessibility.hitMarkers) return;
            _hitTimer = hitMarkerDuration * (report.Killed ? 1.6f : 1f);
            _hitColor = report.Killed ? UIKit.EnemyColor() : report.Headshot ? new Color(1f, 0.45f, 0.45f, 1f) : Color.white;
            float size = _settings != null ? _settings.accessibility.hitMarkerSize : 1f;
            _hitScale = (report.Killed ? 1.5f : report.Headshot ? 1.25f : 1f) * size;
            for (int i = 0; i < 4; i++)
            {
                _hitLines[i].enabled = true;
                _hitLines[i].rectTransform.localScale = Vector3.one * _hitScale;
            }
        }

        private void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (_settings != null && !_settings.accessibility.damageIndicators) return;
            if (info.Instigator == null || player == null || info.Instigator == player.gameObject) return;
            var arrow = UIKit.Image(_damageRoot, "DamageArrow", UIKit.EnemyColor());
            arrow.rectTransform.sizeDelta = new Vector2(70f, 8f);
            _damageArrows.Add(new KeyValuePair<Image, float>(arrow, 1.2f));
            PlaceArrow(arrow, info.Instigator.transform.position);
            arrow.gameObject.AddComponent<DamageArrowTarget>().Source = info.Instigator.transform.position;
        }

        private void PlaceArrow(Image arrow, Vector3 source)
        {
            if (player == null) return;
            Vector3 to = source - player.transform.position;
            to.y = 0f;
            float angle = Vector3.SignedAngle(player.transform.forward, to, Vector3.up); // + = to the right
            float rad = angle * Mathf.Deg2Rad;
            arrow.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 120f;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        private void OnKill(KillFeedEntry e)
        {
            for (int i = _feed.Count - 1; i > 0; i--)
            {
                _feed[i].Label.text = _feed[i - 1].Label.text;
                _feed[i].Background.color = _feed[i - 1].Background.color;
                _feed[i].Age = _feed[i - 1].Age;
                _feed[i].Active = _feed[i - 1].Active;
                _feed[i].Root.gameObject.SetActive(_feed[i].Active);
            }

            string weapon = Game.Weapons != null ? Game.Weapons.DisplayName(e.WeaponId) : (e.WeaponId ?? "").ToUpperInvariant();
            string killer = Colorize(e.Killer, e.KillerObject);
            string victim = Colorize(e.Victim, e.VictimObject);
            string text = e.Suicide ? victim + "   <color=#FFFFFF88>[" + weapon + "]</color>"
                                    : killer + "   <color=#FFFFFF88>[" + weapon + (e.Headshot ? " · HS" : "") + "]</color>   " + victim;

            bool involvesMe = player != null && (e.KillerObject == player.gameObject || e.VictimObject == player.gameObject);
            var top = _feed[0];
            top.Label.text = text;
            top.Background.color = involvesMe ? new Color(0.55f, 0.03f, 0.08f, 0.85f) : UIFactory.DarkGray;
            top.Age = 0f;
            top.Active = true;
            top.Root.gameObject.SetActive(true);
        }

        private string Colorize(string name, GameObject go)
        {
            if (go == null || player == null) return name;
            if (go == player.gameObject) return "<color=#FFFFFF>" + name + "</color>";
            var me = player.GetComponent<Participant>();
            var them = go.GetComponentInParent<Participant>();
            if (me == null || them == null) return name;
            Color c = me.IsEnemyOf(them) ? UIKit.EnemyColor() : UIKit.AllyColor();
            return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + name + "</color>";
        }

        // ------------------------------------------------------------------ Update

        private void Update()
        {
            if (PlayerController.Local != null && player != PlayerController.Local)
            {
                Unsubscribe();
                player = PlayerController.Local;
            }
            TrySubscribe();
            if (player == null) return;
            float dt = Time.deltaTime;
            _time += dt;

            UpdateHealth();
            UpdateWeapon();
            UpdateMovement();
            UpdateMatchInfo();
            UpdateHitMarker(Time.unscaledDeltaTime);
            UpdateFeed(Time.unscaledDeltaTime);
            UpdateDamageArrows(Time.unscaledDeltaTime);
            UpdateDeath();
            UpdateCrosshair();
        }

        private void UpdateCrosshair()
        {
            var c = _settings != null ? _settings.accessibility.crosshair : null;
            if (c == null) return;
            float spread = c.dynamic && player.Weapons != null ? player.Weapons.CurrentSpread : 0f;
            bool scoped = player.Weapons != null && player.Weapons.ShowScope;
            if (_scope.gameObject.activeSelf != scoped) _scope.gameObject.SetActive(scoped);
            if (_crosshairRoot.gameObject.activeSelf == scoped) _crosshairRoot.gameObject.SetActive(!scoped);
            if (Mathf.Abs(spread - _lastSpread) < 0.05f) return;
            _lastSpread = spread;
            for (int i = _crosshairRoot.childCount - 1; i >= 0; i--) Destroy(_crosshairRoot.GetChild(i).gameObject);
            CrosshairDrawer.Draw(_crosshairRoot, c, spread);
        }

        private void UpdateHealth()
        {
            var h = player.Health;
            int hp = Mathf.CeilToInt(h.Health);
            int ar = Mathf.CeilToInt(h.Armor);
            if (hp != _lastHealth)
            {
                _lastHealth = hp;
                _health.text = hp.ToString();
                _health.color = hp <= 30 ? UIFactory.DeepRed : UIFactory.White;
                UIFactory.SetFill(_healthFill, HealthBarWidth, h.Health / h.MaxHealth);
            }
            if (ar != _lastArmor)
            {
                _lastArmor = ar;
                _armor.text = "ARMOR " + ar;
                UIFactory.SetFill(_armorFill, HealthBarWidth, h.MaxArmor > 0f ? h.Armor / h.MaxArmor : 0f);
            }
        }

        private void UpdateWeapon()
        {
            var w = player.Weapons;
            if (w.Current != _lastWeapon)
            {
                _lastWeapon = w.Current;
                _weaponName.text = w.Current != null ? w.Current.displayName.ToUpperInvariant() : "";
                _lastMag = -1;
                _heatFill.transform.parent.gameObject.SetActive(w.Current != null && w.Current.usesHeat);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < w.LoadoutCount; i++) sb.Append(i + 1).Append(' ').Append(w.WeaponAt(i).displayName).Append("   ");
                _hint.text = sb + "·   TAB SCOREBOARD   ·   ESC MENU   ·   F1 MOVEMENT DEBUG";
            }
            if (w.Current == null)
            {
                _ammo.text = "";
                return;
            }
            if (w.Current.usesHeat)
            {
                UIFactory.SetFill(_heatFill, 160f, w.Heat);
                _heatFill.color = w.Overheated ? UIFactory.DeepRed : new Color(1f, 0.5f + 0.5f * (1f - w.Heat), 0.1f);
                string heat = w.Overheated ? "<color=#C8102E>OVERHEAT</color>" : Mathf.RoundToInt(w.Heat * 100f) + "<size=26>%</size>";
                if (_ammo.text != heat) _ammo.text = heat;
                return;
            }
            if (w.Current.delivery == DeliveryType.Melee)
            {
                if (_ammo.text != "—") _ammo.text = "—";
                return;
            }
            int reserve = w.InfiniteReserve ? -1 : w.Reserve;
            if (w.Magazine != _lastMag || reserve != _lastReserve)
            {
                _lastMag = w.Magazine;
                _lastReserve = reserve;
                _ammo.text = w.Magazine + "<size=26> / " + (w.InfiniteReserve ? "INF" : reserve.ToString()) + "</size>";
                _ammo.color = w.Magazine == 0 ? UIFactory.DeepRed : UIFactory.White;
            }

            bool reloading = w.IsReloading;
            var bar = _reloadFill.transform.parent.gameObject;
            if (bar.activeSelf != reloading) bar.SetActive(reloading);
            if (reloading) UIFactory.SetFill(_reloadFill, ReloadBarWidth, w.ReloadProgress);
        }

        private void UpdateMovement()
        {
            var m = player.Movement;
            float speed = m.HorizontalSpeed;
            int s = Mathf.RoundToInt(speed);
            if (s != _lastSpeed)
            {
                _lastSpeed = s;
                _speed.text = s + "<size=16> M/S</size>";
                UIFactory.SetFill(_speedFill, SpeedBarWidth, speed / m.Settings.maxHorizontalSpeed);
            }
            if (m.State != _lastState)
            {
                _lastState = m.State;
                _state.text = m.State == MovementState.Grounded ? "" : m.State.ToString().ToUpperInvariant();
            }
            int peak = Mathf.RoundToInt(m.PeakSpeed);
            if (peak != _lastPeak)
            {
                _lastPeak = peak;
                _peak.text = "BEST SPEED  " + peak + " M/S";
            }

            var dash = m.GetAbility<DashAbility>();
            if (dash != null)
            {
                while (_dashPips.Count < dash.MaxCharges)
                {
                    int i = _dashPips.Count;
                    var pip = UIFactory.Bar("DashPip" + i, _canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2((i - (dash.MaxCharges - 1) * 0.5f) * 40f, 36f), new Vector2(34f, 4f), UIFactory.White);
                    _dashPips.Add(pip);
                }
                for (int i = 0; i < _dashPips.Count; i++) UIFactory.SetFill(_dashPips[i], 34f, Mathf.Clamp01(dash.Charges - i));
            }
        }

        /// <summary>Online matches supply their own mode / objective / score / timer.</summary>
        public delegate bool MatchInfoProvider(out string mode, out string objective, out string score, out float seconds);
        public static MatchInfoProvider ExternalMatchInfo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetExternal() { ExternalMatchInfo = null; }

        private void UpdateMatchInfo()
        {
            var match = MatchController.Instance;
            string mode, objective, score;
            float seconds;
            if (ExternalMatchInfo != null && ExternalMatchInfo(out mode, out objective, out score, out seconds)) { }
            else if (match == null || match.Config == null)
            {
                mode = "TRAINING";
                objective = "MOVEMENT LAB";
                score = "";
                seconds = _time;
            }
            else
            {
                var cfg = match.Config;
                mode = MatchConfig.ModeName(cfg.mode);
                var me = match.Local;
                seconds = match.TimeRemaining >= 0f ? match.TimeRemaining : match.Elapsed;
                switch (cfg.mode)
                {
                    case GameMode.TeamDeathmatch:
                    case GameMode.Elimination:
                        int mine = me != null ? Mathf.Max(0, me.Team) : 0;
                        score = "<color=#" + ColorUtility.ToHtmlStringRGB(UIKit.AllyColor()) + ">" + match.TeamScores[mine] + "</color>   —   <color=#"
                              + ColorUtility.ToHtmlStringRGB(UIKit.EnemyColor()) + ">" + match.TeamScores[1 - mine] + "</color>";
                        if (cfg.mode == GameMode.Elimination)
                        {
                            int a = 0, b = 0;
                            foreach (var p in match.Participants) if (p != null && p.Alive) { if (p.Team == mine) a++; else b++; }
                            objective = "ROUND " + match.Round + "  ·  " + a + " V " + b + " ALIVE  ·  FIRST TO " + Mathf.Max(1, cfg.scoreLimit);
                        }
                        else objective = cfg.scoreLimit > 0 ? "FIRST TEAM TO " + cfg.scoreLimit + " KILLS" : "MOST KILLS WINS";
                        break;
                    case GameMode.GunGame:
                        int lvl = me != null ? me.GunGameLevel : 0;
                        int total = Game.Weapons != null ? Game.Weapons.gunGameOrder.Count : 0;
                        score = "WEAPON " + Mathf.Min(lvl + 1, total) + " / " + total;
                        objective = "GET A KILL WITH EVERY WEAPON";
                        break;
                    case GameMode.MovementRace:
                        int next = me != null ? me.NextCheckpoint : 0;
                        score = "CHECKPOINT " + next + " / " + (match.Checkpoints != null ? match.Checkpoints.Count : 0);
                        objective = "REACH EVERY RED CHECKPOINT  ·  TIME " + match.Elapsed.ToString("0.0");
                        break;
                    case GameMode.Training:
                        score = "";
                        objective = match.Map.DisplayName + "  ·  FREE PRACTICE";
                        break;
                    default:
                        Participant leader = null;
                        foreach (var p in match.Participants) if (p != null && (leader == null || p.Kills > leader.Kills)) leader = p;
                        score = "YOU " + (me != null ? me.Kills : 0) + "   ·   LEADER " + (leader != null ? leader.Kills : 0);
                        objective = cfg.scoreLimit > 0 ? "FIRST TO " + cfg.scoreLimit + " KILLS" : "MOST KILLS WINS";
                        break;
                }
                if (match.Phase == MatchPhase.Intro || match.Phase == MatchPhase.Countdown) objective = "GET READY";
            }

            if (_mode.text != mode) _mode.text = mode;
            if (score != _lastScore) { _lastScore = score; _score.text = score; }
            if (objective != _lastObjective) { _lastObjective = objective; _objective.text = objective; }
            int secs = Mathf.CeilToInt(seconds);
            if (secs != _lastSeconds)
            {
                _lastSeconds = secs;
                _timer.text = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
            }
        }

        private void UpdateHitMarker(float dt)
        {
            if (_hitTimer <= 0f) return;
            _hitTimer -= dt;
            float a = Mathf.Clamp01(_hitTimer / hitMarkerDuration);
            Color c = _hitColor;
            c.a = a;
            for (int i = 0; i < 4; i++)
            {
                _hitLines[i].color = c;
                if (_hitTimer <= 0f) _hitLines[i].enabled = false;
            }
        }

        private void UpdateFeed(float dt)
        {
            for (int i = 0; i < _feed.Count; i++)
            {
                var e = _feed[i];
                if (!e.Active) continue;
                e.Age += dt;
                if (e.Age >= killFeedLifetime)
                {
                    e.Active = false;
                    e.Root.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateDamageArrows(float dt)
        {
            for (int i = _damageArrows.Count - 1; i >= 0; i--)
            {
                var kv = _damageArrows[i];
                float t = kv.Value - dt;
                if (kv.Key == null) { _damageArrows.RemoveAt(i); continue; }
                if (t <= 0f)
                {
                    Destroy(kv.Key.gameObject);
                    _damageArrows.RemoveAt(i);
                    continue;
                }
                var src = kv.Key.GetComponent<DamageArrowTarget>();
                if (src != null) PlaceArrow(kv.Key, src.Source);
                var c = kv.Key.color;
                c.a = Mathf.Clamp01(t / 0.6f);
                kv.Key.color = c;
                _damageArrows[i] = new KeyValuePair<Image, float>(kv.Key, t);
            }
        }

        private void UpdateDeath()
        {
            bool dead = player.IsDead;
            if (_death.gameObject.activeSelf != dead) _death.gameObject.SetActive(dead);
            if (!dead) { _lastDeathTenths = -1; return; }
            int tenths = Mathf.CeilToInt(Mathf.Max(0f, player.RespawnRemaining) * 10f);
            if (tenths == _lastDeathTenths) return;
            _lastDeathTenths = tenths;
            var d = player.LastDeath;
            string weapon = Game.Weapons != null ? Game.Weapons.DisplayName(d.WeaponId) : "";
            string line2 = !player.RespawnEnabled ? "SPECTATING  ·  WAITING FOR THE NEXT ROUND"
                         : "RESPAWN " + (tenths / 10f).ToString("0.0");
            _death.text = "ELIMINATED BY " + player.LastKillerName
                        + "\n<size=18><color=#FFFFFFAA>" + (string.IsNullOrEmpty(weapon) ? "" : weapon + "  ·  ") + Mathf.RoundToInt(d.Amount) + " DAMAGE"
                        + (d.IsHeadshot ? "  ·  HEADSHOT" : "") + "</color></size>\n<size=22><color=#FFFFFFAA>" + line2 + "</color></size>";
            _death.supportRichText = true;
        }
    }
}
