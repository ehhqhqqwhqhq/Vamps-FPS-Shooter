using System;
using UnityEngine;

namespace Vamp.Settings
{
    public enum DisplayModeOption { Fullscreen, Borderless, Windowed }
    public enum QualityPreset { VeryLow, Low, Medium, High, Ultra, Custom }
    public enum QualityTier { Off, Low, Medium, High, Ultra }
    public enum AntiAliasingOption { Off, FXAA, SMAA, TAA }
    public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }
    public enum ColorblindMode { Off, Protanopia, Deuteranopia, Tritanopia }
    public enum VoiceMode { PushToTalk, OpenMic }

    [Serializable]
    public sealed class ControlSettingsData
    {
        [Tooltip("Input System binding overrides JSON (see InputController).")]
        public string keybindsJson = "";
        public bool toggleCrouch = false;
        public bool toggleAim = false;
    }

    [Serializable]
    public sealed class MouseSettingsData
    {
        public float sensitivity = 1f;
        public float adsSensitivity = 1f;
        public float sniperSensitivity = 1f;
        public bool mouseAcceleration = false;
        public bool rawInput = true;
        public bool invertY = false;
        public bool invertX = false;
        public float fov = 90f;
    }

    [Serializable]
    public sealed class DisplaySettingsData
    {
        public DisplayModeOption displayMode = DisplayModeOption.Fullscreen;
        [Tooltip("0 = use the monitor's native resolution.")]
        public int width = 0;
        public int height = 0;
        [Tooltip("0 = highest available for the chosen resolution.")]
        public int refreshRate = 0;
        public bool vSync = false;
        [Tooltip("0 = AUTO (monitor refresh), -1 = UNLIMITED, otherwise a cap.")]
        public int fpsLimit = 0;
        public int menuFps = 60;
        [Tooltip("30, 60 or -1 (unlimited).")]
        public int backgroundFps = 30;
        public bool showFps = false;
        public ScreenCorner fpsPosition = ScreenCorner.TopRight;
        public bool showPerformanceMonitor = false;
    }

    [Serializable]
    public sealed class GraphicsSettingsData
    {
        public QualityPreset preset = QualityPreset.High;
        public QualityTier textures = QualityTier.High;
        public QualityTier shadows = QualityTier.High;
        public QualityTier effects = QualityTier.High;
        public QualityTier lighting = QualityTier.High;
        public QualityTier postProcessing = QualityTier.High;
        public AntiAliasingOption antiAliasing = AntiAliasingOption.SMAA;
        public QualityTier viewDistance = QualityTier.High;
        public QualityTier particles = QualityTier.High;
        public QualityTier reflections = QualityTier.High;
        public QualityTier ambientOcclusion = QualityTier.High;
        public bool motionBlur = false;
        public bool depthOfField = false;
        public bool vignette = false;
        public bool bloom = true;
        [Range(0f, 1f)] public float bloomIntensity = 0.5f;
        public bool competitiveVisuals = false;
        [Range(0.5f, 1f)] public float renderScale = 1f;
    }

    [Serializable]
    public sealed class AudioSettingsData
    {
        public float master = 0.8f;
        public float music = 0.5f;
        public float sfx = 1f;
        public float voice = 1f;
        public float ui = 0.8f;
        public float announcer = 1f;
        public float voiceChat = 1f;
        public VoiceMode voiceMode = VoiceMode.PushToTalk;
        public bool micMuted = false;
    }

    [Serializable]
    public sealed class AccessibilitySettingsData
    {
        public ColorblindMode colorblind = ColorblindMode.Off;
        public float subtitleSize = 1f;
        public bool subtitleBackground = true;
        public bool hitMarkers = true;
        public float hitMarkerSize = 1f;
        public bool damageIndicators = true;
        [Tooltip("0..1, default 0.5 (50%).")]
        public float screenShake = 0.5f;
        [Tooltip("Speed FOV, landing dip, camera tilt.")]
        public bool cameraEffects = true;
        public float hudScale = 1f;
        public CrosshairSettings crosshair = new CrosshairSettings();
    }

    /// <summary>
    /// Every player setting (what the spec calls the settings storage blob: mouse_sensitivity, ads_sensitivity,
    /// fov, keybinds, display_mode, resolution, fps_limit, graphics_*, screen_shake, show_fps...).
    /// Saved per account. Hardware-specific values are re-validated against the current PC when applied.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public int version = 1;
        public bool firstLaunchDone = false;
        public ControlSettingsData controls = new ControlSettingsData();
        public MouseSettingsData mouse = new MouseSettingsData();
        public DisplaySettingsData display = new DisplaySettingsData();
        public GraphicsSettingsData graphics = new GraphicsSettingsData();
        public AudioSettingsData audio = new AudioSettingsData();
        public AccessibilitySettingsData accessibility = new AccessibilitySettingsData();

        public GameSettings Clone()
        {
            return JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));
        }

        /// <summary>Clamp / repair everything so invalid data can never crash or break the game.</summary>
        public void Validate()
        {
            if (controls == null) controls = new ControlSettingsData();
            if (mouse == null) mouse = new MouseSettingsData();
            if (display == null) display = new DisplaySettingsData();
            if (graphics == null) graphics = new GraphicsSettingsData();
            if (audio == null) audio = new AudioSettingsData();
            if (accessibility == null) accessibility = new AccessibilitySettingsData();
            if (accessibility.crosshair == null) accessibility.crosshair = new CrosshairSettings();
            if (controls.keybindsJson == null) controls.keybindsJson = "";

            mouse.sensitivity = Clamp(mouse.sensitivity, LookSettings.MinSensitivity, LookSettings.MaxSensitivity, 1f);
            mouse.adsSensitivity = Clamp(mouse.adsSensitivity, LookSettings.MinSensitivity, LookSettings.MaxSensitivity, 1f);
            mouse.sniperSensitivity = Clamp(mouse.sniperSensitivity, LookSettings.MinSensitivity, LookSettings.MaxSensitivity, 1f);
            mouse.fov = Clamp(mouse.fov, LookSettings.MinFov, LookSettings.MaxFov, 90f);

            display.displayMode = EnumOr(display.displayMode, DisplayModeOption.Fullscreen);
            display.fpsPosition = EnumOr(display.fpsPosition, ScreenCorner.TopRight);
            if (display.width < 0 || display.height < 0) { display.width = 0; display.height = 0; }
            if (display.refreshRate < 0) display.refreshRate = 0;
            if (display.fpsLimit < -1 || display.fpsLimit > 1000) display.fpsLimit = 0;
            if (display.fpsLimit > 0 && display.fpsLimit < 30) display.fpsLimit = 30;
            if (display.menuFps != -1 && (display.menuFps < 30 || display.menuFps > 1000)) display.menuFps = 60;
            if (display.backgroundFps != 30 && display.backgroundFps != 60 && display.backgroundFps != -1) display.backgroundFps = 30;

            graphics.preset = EnumOr(graphics.preset, QualityPreset.High);
            graphics.textures = EnumOr(graphics.textures, QualityTier.High);
            if (graphics.textures == QualityTier.Off) graphics.textures = QualityTier.Low;
            graphics.shadows = EnumOr(graphics.shadows, QualityTier.High);
            graphics.effects = EnumOr(graphics.effects, QualityTier.High);
            if (graphics.effects == QualityTier.Off) graphics.effects = QualityTier.Low;
            graphics.lighting = EnumOr(graphics.lighting, QualityTier.High);
            if (graphics.lighting == QualityTier.Off) graphics.lighting = QualityTier.Low;
            graphics.postProcessing = EnumOr(graphics.postProcessing, QualityTier.High);
            if (graphics.postProcessing == QualityTier.Off) graphics.postProcessing = QualityTier.Low;
            graphics.antiAliasing = EnumOr(graphics.antiAliasing, AntiAliasingOption.SMAA);
            graphics.viewDistance = EnumOr(graphics.viewDistance, QualityTier.High);
            if (graphics.viewDistance == QualityTier.Off) graphics.viewDistance = QualityTier.Low;
            graphics.particles = EnumOr(graphics.particles, QualityTier.High);
            if (graphics.particles == QualityTier.Off) graphics.particles = QualityTier.Low;
            graphics.reflections = EnumOr(graphics.reflections, QualityTier.High);
            graphics.ambientOcclusion = EnumOr(graphics.ambientOcclusion, QualityTier.High);
            graphics.bloomIntensity = Clamp(graphics.bloomIntensity, 0f, 1f, 0.5f);
            graphics.renderScale = Clamp(graphics.renderScale, 0.5f, 1f, 1f);

            audio.master = Clamp(audio.master, 0f, 1f, 0.8f);
            audio.music = Clamp(audio.music, 0f, 1f, 0.5f);
            audio.sfx = Clamp(audio.sfx, 0f, 1f, 1f);
            audio.voice = Clamp(audio.voice, 0f, 1f, 1f);
            audio.ui = Clamp(audio.ui, 0f, 1f, 0.8f);
            audio.announcer = Clamp(audio.announcer, 0f, 1f, 1f);
            audio.voiceChat = Clamp(audio.voiceChat, 0f, 1f, 1f);
            audio.voiceMode = EnumOr(audio.voiceMode, VoiceMode.PushToTalk);

            accessibility.colorblind = EnumOr(accessibility.colorblind, ColorblindMode.Off);
            accessibility.subtitleSize = Clamp(accessibility.subtitleSize, 0.75f, 2f, 1f);
            accessibility.hitMarkerSize = Clamp(accessibility.hitMarkerSize, 0.5f, 2f, 1f);
            accessibility.screenShake = Clamp(accessibility.screenShake, 0f, 1f, 0.5f);
            accessibility.hudScale = Clamp(accessibility.hudScale, 0.75f, 1.25f, 1f);
            accessibility.crosshair.Validate();
        }

        private static float Clamp(float v, float min, float max, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return fallback;
            return Mathf.Clamp(v, min, max);
        }

        private static T EnumOr<T>(T value, T fallback) where T : struct
        {
            return Enum.IsDefined(typeof(T), value) ? value : fallback;
        }

        // ------------------------------------------------------------------ Presets

        /// <summary>Applies a preset's individual values (the tables from the spec).</summary>
        public static void ApplyPreset(GraphicsSettingsData g, QualityPreset preset)
        {
            if (preset == QualityPreset.Custom) { g.preset = QualityPreset.Custom; return; }
            g.preset = preset;
            switch (preset)
            {
                case QualityPreset.VeryLow:
                    Set(g, QualityTier.Low, QualityTier.Off, QualityTier.Low, QualityTier.Low, QualityTier.Low, AntiAliasingOption.Off,
                        QualityTier.Low, QualityTier.Low, QualityTier.Off, QualityTier.Off);
                    g.renderScale = 0.85f;
                    break;
                case QualityPreset.Low:
                    Set(g, QualityTier.Low, QualityTier.Low, QualityTier.Low, QualityTier.Low, QualityTier.Low, AntiAliasingOption.FXAA,
                        QualityTier.Medium, QualityTier.Low, QualityTier.Low, QualityTier.Off);
                    g.renderScale = 1f;
                    break;
                case QualityPreset.Medium:
                    Set(g, QualityTier.Medium, QualityTier.Medium, QualityTier.Medium, QualityTier.Medium, QualityTier.Medium, AntiAliasingOption.FXAA,
                        QualityTier.Medium, QualityTier.Medium, QualityTier.Medium, QualityTier.Medium);
                    g.renderScale = 1f;
                    break;
                case QualityPreset.High:
                    Set(g, QualityTier.High, QualityTier.High, QualityTier.High, QualityTier.High, QualityTier.High, AntiAliasingOption.SMAA,
                        QualityTier.High, QualityTier.High, QualityTier.High, QualityTier.High);
                    g.renderScale = 1f;
                    break;
                case QualityPreset.Ultra:
                    Set(g, QualityTier.Ultra, QualityTier.Ultra, QualityTier.Ultra, QualityTier.Ultra, QualityTier.Ultra, AntiAliasingOption.TAA,
                        QualityTier.Ultra, QualityTier.Ultra, QualityTier.Ultra, QualityTier.Ultra);
                    g.renderScale = 1f;
                    break;
            }
            // Motion blur / DOF stay OFF by default at every preset (competitive defaults).
            g.motionBlur = false;
            g.depthOfField = false;
        }

        private static void Set(GraphicsSettingsData g, QualityTier tex, QualityTier shadows, QualityTier fx, QualityTier light,
                                QualityTier post, AntiAliasingOption aa, QualityTier view, QualityTier particles,
                                QualityTier reflections, QualityTier ao)
        {
            g.textures = tex; g.shadows = shadows; g.effects = fx; g.lighting = light; g.postProcessing = post;
            g.antiAliasing = aa; g.viewDistance = view; g.particles = particles; g.reflections = reflections; g.ambientOcclusion = ao;
        }

        /// <summary>True if the individual values still match the named preset (used to keep/drop "CUSTOM").</summary>
        public static bool MatchesPreset(GraphicsSettingsData g, QualityPreset preset)
        {
            if (preset == QualityPreset.Custom) return false;
            var probe = new GraphicsSettingsData();
            ApplyPreset(probe, preset);
            return probe.textures == g.textures && probe.shadows == g.shadows && probe.effects == g.effects
                && probe.lighting == g.lighting && probe.postProcessing == g.postProcessing && probe.antiAliasing == g.antiAliasing
                && probe.viewDistance == g.viewDistance && probe.particles == g.particles && probe.reflections == g.reflections
                && probe.ambientOcclusion == g.ambientOcclusion && Mathf.Approximately(probe.renderScale, g.renderScale);
        }
    }
}
