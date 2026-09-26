using UnityEngine;
using UnityEngine.UI;
using Vamp.Core;
using Vamp.Settings;

namespace Vamp.UI
{
    /// <summary>
    /// SHOW FPS (FPS + PING in a chosen corner) and the PERFORMANCE MONITOR (FPS, frame time, ping, packet loss).
    /// Offline builds report ping as LOCAL and 0% loss.
    /// </summary>
    public sealed class PerformanceOverlay : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _text;
        private float _accum;
        private int _frames;
        private float _fps;
        private float _worstFrame;
        private float _refresh;

        private void Awake()
        {
            _canvas = UIFactory.CreateCanvas("Perf Canvas", transform, 980);
            _text = UIKit.Label(_canvas.transform, "", 15, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperRight, FontStyle.Bold);
            var o = _text.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.8f);
            _text.rectTransform.sizeDelta = new Vector2(260f, 120f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _accum += dt;
            _frames++;
            _worstFrame = Mathf.Max(_worstFrame, dt);
            _refresh -= dt;

            var s = Game.Settings;
            bool show = s != null && (s.Current.display.showFps || s.Current.display.showPerformanceMonitor);
            _text.enabled = show;
            if (!show || _refresh > 0f) return;
            _refresh = 0.25f;

            _fps = _frames / Mathf.Max(0.0001f, _accum);
            float frameMs = 1000f * _accum / Mathf.Max(1, _frames);
            _accum = 0f;
            _frames = 0;

            var d = s.Current.display;
            if (d.showPerformanceMonitor)
                _text.text = "FPS          " + Mathf.RoundToInt(_fps) + "\nFRAME TIME   " + frameMs.ToString("0.0") + " ms"
                           + "\nWORST        " + (_worstFrame * 1000f).ToString("0.0") + " ms\nPING         LOCAL\nPACKET LOSS  0%";
            else
                _text.text = "FPS: " + Mathf.RoundToInt(_fps) + "\nPING: LOCAL";
            _worstFrame = 0f;
            Place(d.fpsPosition, d.showPerformanceMonitor);
        }

        private void Place(ScreenCorner corner, bool monitor)
        {
            var rt = _text.rectTransform;
            bool left = corner == ScreenCorner.TopLeft || corner == ScreenCorner.BottomLeft;
            bool top = corner == ScreenCorner.TopLeft || corner == ScreenCorner.TopRight;
            var a = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = a;
            rt.anchoredPosition = new Vector2(left ? 12f : -12f, top ? -8f : 8f);
            rt.sizeDelta = new Vector2(monitor ? 260f : 140f, monitor ? 110f : 44f);
            _text.alignment = top ? (left ? TextAnchor.UpperLeft : TextAnchor.UpperRight) : (left ? TextAnchor.LowerLeft : TextAnchor.LowerRight);
        }
    }
}
