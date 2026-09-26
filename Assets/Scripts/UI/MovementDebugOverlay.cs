using UnityEngine;
using Vamp.Movement;
using Vamp.Player;

namespace Vamp.UI
{
    /// <summary>
    /// Developer movement readout (toggle with F1): speed, max speed, velocity, grounded/air/wall-run state,
    /// dash cooldown + charges, momentum, direction, plus FPS/frame time and a speed graph.
    /// IMGUI on purpose: dev-only, zero setup, stripped from release builds via DEVELOPMENT_BUILD / editor check.
    /// </summary>
    public sealed class MovementDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private int graphSamples = 180;

        private float[] _speedHistory;
        private int _historyIndex;
        private float _fpsSmoothed;
        private GUIStyle _style;
        private GUIStyle _header;
        private Texture2D _pixel;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            _speedHistory = new float[Mathf.Max(16, graphSamples)];
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        private void Update()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (player == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / dt, 0.1f);
            _speedHistory[_historyIndex] = player.Movement.HorizontalSpeed;
            _historyIndex = (_historyIndex + 1) % _speedHistory.Length;
        }

#if UNITY_EDITOR || DEBUG
        private void OnGUI()
        {
            if (player == null || !player.ShowMovementDebug) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _style.normal.textColor = Color.white;
                _header = new GUIStyle(_style) { fontSize = 16, fontStyle = FontStyle.Bold };
            }

            var m = player.Movement;
            var s = m.Settings;
            var dash = m.GetAbility<DashAbility>();
            var wall = m.GetAbility<WallRunAbility>();
            var slide = m.GetAbility<SlideAbility>();
            Vector3 v = m.Velocity;
            float hs = m.HorizontalSpeed;
            float heading = hs > 0.1f ? Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg : 0f;
            float momentum = hs / Mathf.Max(0.01f, s.sprintSpeed);

            Rect box = new Rect(20f, 110f, 330f, 420f);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(box, _pixel);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, box.height - 16f));
            GUILayout.Label("<color=#C8102E>VAMP</color> MOVEMENT DEBUG", _header);
            GUILayout.Label("FPS            " + _fpsSmoothed.ToString("0") + "   (" + (1000f / Mathf.Max(1f, _fpsSmoothed)).ToString("0.0") + " ms)", _style);
            GUILayout.Label("Speed          " + hs.ToString("0.00") + " m/s   (3D " + m.Speed.ToString("0.0") + ")", _style);
            GUILayout.Label("Max / Peak     " + s.maxHorizontalSpeed.ToString("0") + " / " + m.PeakSpeed.ToString("0.0") + " m/s", _style);
            GUILayout.Label("Velocity       " + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0"), _style);
            GUILayout.Label("Momentum       " + (momentum * 100f).ToString("0") + "% of sprint", _style);
            GUILayout.Label("Direction      " + heading.ToString("0") + "°  (look " + player.View.Yaw.ToString("0") + "°)", _style);
            GUILayout.Label("State          <b>" + m.State.ToString().ToUpperInvariant() + "</b>", _style);
            GUILayout.Label("Grounded       " + Bool(m.IsGrounded) + "   Air " + m.AirTime.ToString("0.00") + "s", _style);
            GUILayout.Label("Crouch         " + Bool(m.IsCrouching) + "   h=" + m.CurrentHeight.ToString("0.00"), _style);
            if (slide != null)
                GUILayout.Label("Slide          " + Bool(slide.IsActive) + "   boost cd " + slide.BoostCooldownRemaining.ToString("0.00"), _style);
            if (wall != null)
                GUILayout.Label("Wall run       " + Bool(wall.IsActive) + "   side " + wall.WallSide + "   t " + wall.RunTime.ToString("0.00") + "/" + s.wallRunMaxDuration.ToString("0.0"), _style);
            if (dash != null)
                GUILayout.Label("Dash           " + dash.Charges.ToString("0.00") + "/" + dash.MaxCharges + "   cd " + dash.CooldownRemaining.ToString("0.00") + "   air " + m.AirDashesUsed + "/" + s.airDashesPerAirtime, _style);
            GUILayout.EndArea();

            DrawGraph(new Rect(box.x, box.yMax + 6f, box.width, 70f), s.maxHorizontalSpeed);
        }

        private void DrawGraph(Rect r, float max)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(r, _pixel);
            int n = _speedHistory.Length;
            float w = r.width / n;
            for (int i = 0; i < n; i++)
            {
                float val = _speedHistory[(_historyIndex + i) % n];
                float h = Mathf.Clamp01(val / Mathf.Max(1f, max)) * (r.height - 4f);
                GUI.color = val > 20f ? new Color(0.9f, 0.1f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.7f);
                GUI.DrawTexture(new Rect(r.x + i * w, r.yMax - 2f - h, Mathf.Max(1f, w), h), _pixel);
            }
            GUI.color = Color.white;
        }

        private static string Bool(bool b)
        {
            return b ? "<color=#7CFC00>YES</color>" : "<color=#888888>no</color>";
        }
#endif
    }
}
