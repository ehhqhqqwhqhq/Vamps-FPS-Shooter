using System;
using UnityEngine;

namespace Vamp.Settings
{
    public enum CrosshairType { Cross, Dot, CrossWithDot }

    /// <summary>Crosshair customisation (Accessibility / HUD settings). Pixel values at 1080p reference.</summary>
    [Serializable]
    public sealed class CrosshairSettings
    {
        public CrosshairType type = CrosshairType.Cross;
        [Range(1f, 30f)] public float size = 8f;
        [Range(1f, 10f)] public float thickness = 2f;
        [Range(0f, 30f)] public float gap = 4f;
        [Range(0.1f, 1f)] public float opacity = 1f;
        public bool outline = true;
        [Range(1f, 4f)] public float outlineThickness = 1f;
        public bool centerDot = false;
        [Tooltip("Gap grows with weapon spread.")]
        public bool dynamic = false;
        public Color color = Color.white;

        public void Validate()
        {
            size = Mathf.Clamp(size, 1f, 30f);
            thickness = Mathf.Clamp(thickness, 1f, 10f);
            gap = Mathf.Clamp(gap, 0f, 30f);
            opacity = Mathf.Clamp(opacity, 0.1f, 1f);
            outlineThickness = Mathf.Clamp(outlineThickness, 1f, 4f);
        }
    }
}
