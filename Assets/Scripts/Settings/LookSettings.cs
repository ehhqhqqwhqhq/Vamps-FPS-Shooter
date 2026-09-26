using System;
using UnityEngine;

namespace Vamp.Settings
{
    /// <summary>
    /// Player-facing look/aim preferences. In Phase 13 SettingsController loads these from the
    /// account-synced settings (mouse_sensitivity, ads_sensitivity, sniper_sensitivity, fov, screen_shake)
    /// and pushes them into CameraController. Values are validated/clamped here so bad data can't break aim.
    /// </summary>
    [Serializable]
    public sealed class LookSettings
    {
        public const float MinSensitivity = 0.01f;
        public const float MaxSensitivity = 10f;
        public const float MinFov = 70f;
        public const float MaxFov = 120f;

        [Range(MinSensitivity, MaxSensitivity)] public float sensitivity = 1f;
        [Tooltip("Multiplier on top of sensitivity while aiming down sights.")]
        [Range(MinSensitivity, MaxSensitivity)] public float adsSensitivity = 1f;
        [Range(MinSensitivity, MaxSensitivity)] public float sniperSensitivity = 1f;
        public bool invertY = false;
        public bool invertX = false;

        [Tooltip("Horizontal field of view in degrees (what competitive players expect).")]
        [Range(MinFov, MaxFov)] public float fieldOfView = 90f;

        [Tooltip("0 = no screen shake, 1 = full.")]
        [Range(0f, 1f)] public float screenShake = 0.5f;

        [Tooltip("Extra FOV at high speed. Set 0 for a static FOV.")]
        [Range(0f, 20f)] public float speedFovBonus = 8f;

        [Tooltip("Degrees per mouse count at sensitivity 1 (0.022 = Quake/Source family).")]
        public float degreesPerCount = 0.022f;

        public void Validate()
        {
            sensitivity = Clamp(sensitivity, MinSensitivity, MaxSensitivity, 1f);
            adsSensitivity = Clamp(adsSensitivity, MinSensitivity, MaxSensitivity, 1f);
            sniperSensitivity = Clamp(sniperSensitivity, MinSensitivity, MaxSensitivity, 1f);
            fieldOfView = Clamp(fieldOfView, MinFov, MaxFov, 90f);
            screenShake = Clamp(screenShake, 0f, 1f, 0.5f);
            speedFovBonus = Clamp(speedFovBonus, 0f, 20f, 8f);
            if (degreesPerCount <= 0f || float.IsNaN(degreesPerCount)) degreesPerCount = 0.022f;
        }

        private static float Clamp(float v, float min, float max, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return fallback;
            return Mathf.Clamp(v, min, max);
        }
    }
}
