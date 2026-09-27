using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vamp.Settings;

namespace Vamp.Graphics
{
    /// <summary>
    /// One global, persistent post-processing volume owned by VAMP (high priority), driven by graphics settings:
    /// bloom (+ intensity), motion blur, depth of field, vignette, tonemapping and the VAMP colour grade.
    /// Motion blur / DOF default OFF; Competitive Visuals forces them off.
    /// </summary>
    public static class PostProcessController
    {
        private static Volume _volume;
        private static VolumeProfile _profile;
        private static Bloom _bloom;
        private static MotionBlur _motionBlur;
        private static DepthOfField _dof;
        private static Vignette _vignette;
        private static Tonemapping _tonemapping;
        private static ColorAdjustments _color;
        private static WhiteBalance _white;
        private static LiftGammaGain _lgg;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _volume = null;
            _profile = null;
        }

        private static void Ensure()
        {
            if (_volume != null) return;
            var go = new GameObject("[VAMP PostProcess]");
            Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "VAMP Runtime Profile";
            _volume.sharedProfile = _profile;

            _bloom = _profile.Add<Bloom>(true);
            _bloom.threshold.Override(0.95f);
            _bloom.scatter.Override(0.65f);
            _bloom.tint.Override(new Color(1f, 0.85f, 0.85f));

            _motionBlur = _profile.Add<MotionBlur>(true);
            _motionBlur.intensity.Override(0.35f);

            _dof = _profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianStart.Override(25f);
            _dof.gaussianEnd.Override(120f);

            _vignette = _profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.3f);
            _vignette.smoothness.Override(0.4f);

            _tonemapping = _profile.Add<Tonemapping>(true);
            _tonemapping.mode.Override(TonemappingMode.ACES);

            _color = _profile.Add<ColorAdjustments>(true);
            _color.postExposure.Override(0.15f);
            _color.contrast.Override(18f);
            _color.saturation.Override(4f);

            // Graphics revamp: warmer light, cooler shadows, a touch more punch.
            _white = _profile.Add<WhiteBalance>(true);
            _white.temperature.Override(6f);
            _lgg = _profile.Add<LiftGammaGain>(true);
            _lgg.lift.Override(new Vector4(0.97f, 0.99f, 1.03f, -0.02f));
            _lgg.gain.Override(new Vector4(1.02f, 1.0f, 0.97f, 0.02f));
        }

        public static void Apply(GraphicsSettingsData g)
        {
            Ensure();
            bool competitive = g.competitiveVisuals;
            bool postOn = g.postProcessing != QualityTier.Off;

            _bloom.active = postOn && g.bloom;
            _bloom.intensity.Override(Mathf.Lerp(0f, 1.6f, g.bloomIntensity) * (competitive ? 0.6f : 1f));
            _bloom.highQualityFiltering.Override(g.postProcessing >= QualityTier.High);

            _motionBlur.active = postOn && g.motionBlur && !competitive;
            _motionBlur.quality.Override(g.postProcessing >= QualityTier.High ? MotionBlurQuality.High : MotionBlurQuality.Low);

            _dof.active = postOn && g.depthOfField && !competitive;
            _vignette.active = postOn && g.vignette && !competitive;
            _tonemapping.active = postOn;
            _color.active = postOn && g.postProcessing >= QualityTier.Medium;
            _white.active = _color.active;
            _lgg.active = _color.active;
        }
    }
}
