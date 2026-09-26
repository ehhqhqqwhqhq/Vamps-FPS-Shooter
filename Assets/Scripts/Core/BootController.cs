using UnityEngine;
using UnityEngine.UI;
using Vamp.UI;

namespace Vamp.Core
{
    /// <summary>
    /// Boot scene: global systems are created by GameRoot (config, account/session, settings, audio, services);
    /// this shows the VAMP splash briefly, then loads the main menu.
    /// </summary>
    public sealed class BootController : MonoBehaviour
    {
        [SerializeField] private float splashSeconds = 1.2f;
        private float _t;
        private bool _started;
        private Text _logo;

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }
            var canvas = UIFactory.CreateCanvas("Boot Canvas", transform, 0);
            var bg = UIKit.Image(canvas.transform, "Bg", Color.black);
            UIKit.Stretch(bg.rectTransform);
            _logo = UIKit.Label(canvas.transform, "VAMP", 160, UIKit.Text, TextAnchor.MiddleCenter);
            UIKit.Stretch(_logo.rectTransform);
            var tag = UIKit.Label(canvas.transform, "MOVE FAST. AIM FASTER. NEVER STOP MOVING.", 18, UIKit.Red, TextAnchor.MiddleCenter);
            UIKit.Stretch(tag.rectTransform, 0, 0, 640, 300);
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_logo != null) _logo.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_t * 2f));
            if (_started || _t < splashSeconds || Game.Scenes == null) return;
            _started = true;
            Game.Scenes.GoToMainMenu();
        }
    }
}
