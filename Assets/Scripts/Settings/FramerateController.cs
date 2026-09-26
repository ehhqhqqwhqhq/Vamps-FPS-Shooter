using UnityEngine;

namespace Vamp.Settings
{
    /// <summary>
    /// Chooses the frame cap for the current context: GAMEPLAY FPS (limit / AUTO = monitor / UNLIMITED),
    /// MENU FPS, and BACKGROUND FPS when the window loses focus. V-Sync on overrides caps.
    /// </summary>
    public sealed class FramerateController : MonoBehaviour
    {
        public static bool InMenu { get; set; }

        private int _applied = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { InMenu = true; }

        private void Awake()
        {
            Application.runInBackground = true;
        }

        private void Update()
        {
            var settings = Core.Game.Settings;
            if (settings == null) return;
            var d = settings.Current.display;

            int target;
            if (!Application.isFocused) target = d.backgroundFps;
            else if (InMenu) target = d.menuFps;
            else if (d.fpsLimit == 0) target = DisplaySettingsApplier.MonitorRefreshRate();
            else target = d.fpsLimit; // -1 = unlimited

            if (d.vSync && Application.isFocused) target = -1;

            if (target != _applied)
            {
                _applied = target;
                Application.targetFrameRate = target;
            }
        }
    }
}
