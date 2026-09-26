using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vamp.UI
{
    /// <summary>Persistent loading screen: title, step checklist, progress bar and a movement tip.</summary>
    public sealed class LoadingScreenView : MonoBehaviour
    {
        private static readonly string[] Tips =
        {
            "SLIDE INTO A JUMP TO KEEP ALL OF YOUR MOMENTUM.",
            "STRAFE AND TURN IN THE AIR TOGETHER TO BUILD SPEED.",
            "WALL JUMPS KEEP YOUR SPEED ALONG THE WALL. CHAIN THEM.",
            "ROCKET AT YOUR FEET WHILE JUMPING TO REACH HIGH GROUND.",
            "BRUTE KNOCKS YOU BACKWARDS IN THE AIR. AIM DOWN FOR A BOOST.",
            "DASH REFRESHES WHEN YOU LAND OR WALL RUN.",
            "LANDING AND JUMPING IN THE SAME MOMENT SKIPS GROUND FRICTION.",
            "PROGRESSION IS COSMETIC. SKILL WINS FIGHTS."
        };

        private Canvas _canvas;
        private CanvasGroup _group;
        private Text _title;
        private Text _tip;
        private RectTransform _barFill;
        private readonly List<Text> _steps = new List<Text>();
        private RectTransform _stepsRoot;
        private float _target;
        private bool _visible;

        private void Awake()
        {
            _canvas = UIFactory.CreateCanvas("Loading Canvas", transform, 900);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            var root = _canvas.transform;

            var bg = UIKit.Image(root, "Bg", new Color(0.02f, 0.02f, 0.025f, 1f));
            UIKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            _title = UIKit.Label(root, "LOADING...", 64, UIKit.Text, TextAnchor.LowerLeft);
            var trt = _title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(0f, 0f);
            trt.anchoredPosition = new Vector2(96f, 260f);
            trt.sizeDelta = new Vector2(-192f, 90f);

            _stepsRoot = UIKit.Node("Steps", root);
            _stepsRoot.anchorMin = _stepsRoot.anchorMax = new Vector2(0f, 0f);
            _stepsRoot.pivot = new Vector2(0f, 1f);
            _stepsRoot.anchoredPosition = new Vector2(100f, 250f);
            _stepsRoot.sizeDelta = new Vector2(600f, 120f);
            UIKit.VList(_stepsRoot, 4f);

            var barBg = UIKit.Image(root, "BarBg", new Color(1f, 1f, 1f, 0.1f));
            var brt = barBg.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 90f);
            brt.sizeDelta = new Vector2(-192f, 3f);
            var fill = UIKit.Image(barBg.transform, "Fill", UIKit.Red);
            _barFill = fill.rectTransform;
            _barFill.anchorMin = new Vector2(0f, 0f); _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.pivot = new Vector2(0f, 0.5f);
            _barFill.sizeDelta = Vector2.zero;

            _tip = UIKit.Label(root, "", 16, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
            var tip = _tip.rectTransform;
            tip.anchorMin = new Vector2(0f, 0f); tip.anchorMax = new Vector2(1f, 0f);
            tip.pivot = new Vector2(0f, 0f);
            tip.anchoredPosition = new Vector2(96f, 50f);
            tip.sizeDelta = new Vector2(-192f, 30f);

            _canvas.gameObject.SetActive(false);
        }

        public void Show(string title, string[] steps)
        {
            _canvas.gameObject.SetActive(true);
            _visible = true;
            _group.blocksRaycasts = true;
            _title.text = title;
            _tip.text = "TIP  ·  " + Tips[Random.Range(0, Tips.Length)];
            _target = 0f;
            _barFill.sizeDelta = new Vector2(0f, 0f);
            UIKit.Clear(_stepsRoot);
            _steps.Clear();
            foreach (var s in steps)
            {
                var t = UIKit.Label(_stepsRoot, "   " + s, 18, UIKit.TextFaint, TextAnchor.MiddleLeft);
                UIKit.Size(t, 26f);
                _steps.Add(t);
            }
        }

        public void SetStep(int index, bool active)
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                string raw = _steps[i].text.Substring(3);
                if (i < index) { _steps[i].text = "✓  " + raw; _steps[i].color = UIKit.TextDim; }
                else if (i == index && active) { _steps[i].text = "›  " + raw; _steps[i].color = UIKit.Text; }
            }
        }

        public void SetProgress(float p) { _target = p; }

        public void Hide()
        {
            _visible = false;
            _group.blocksRaycasts = false;
            for (int i = 0; i < _steps.Count; i++) SetStep(_steps.Count, true);
        }

        private void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;
            float dt = Time.unscaledDeltaTime;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _visible ? 1f : 0f, dt * (_visible ? 10f : 4f));
            var parent = (RectTransform)_barFill.parent;
            float w = Mathf.Lerp(_barFill.sizeDelta.x, parent.rect.width * _target, 1f - Mathf.Exp(-12f * dt));
            _barFill.sizeDelta = new Vector2(w, 0f);
            if (!_visible && _group.alpha <= 0f) _canvas.gameObject.SetActive(false);
        }
    }
}
