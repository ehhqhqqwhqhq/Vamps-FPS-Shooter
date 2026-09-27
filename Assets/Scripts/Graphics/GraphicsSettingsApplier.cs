using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vamp.Settings;
using Vamp.VFX;

namespace Vamp.Graphics
{
    /// <summary>
    /// Applies graphics settings to Unity/URP:
    ///   textures → mipmap limit + aniso · shadows → URP shadow distance/cascades · lighting → additional light budget
    ///   anti-aliasing → camera AA mode (FXAA/SMAA/TAA) · view distance → camera far plane + LOD bias
    ///   reflections → realtime probes · effects/particles → VFX density · AO → SSAO renderer feature (if present)
    ///   post processing/bloom/motion blur/DOF/vignette → PostProcessController.
    /// The active URP asset is edited in memory; inside the Editor its original values are restored when play
    /// stops so your project asset is never permanently changed.
    /// </summary>
    public static class GraphicsSettingsApplier
    {
        private struct Snapshot
        {
            public float ShadowDistance;
            public int Cascades;
            public int Msaa;
            public float RenderScale;
            public int AdditionalLights;
            public int ShadowRes;
            public bool Captured;
        }

        private static UniversalRenderPipelineAsset _asset;
        private static Snapshot _original;
        private static readonly Dictionary<ScriptableRendererFeature, bool> OriginalFeatureState = new Dictionary<ScriptableRendererFeature, bool>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _asset = null;
            _original = default(Snapshot);
            OriginalFeatureState.Clear();
        }

        public static void Apply(GraphicsSettingsData g, AccessibilitySettingsData a)
        {
            bool competitive = g.competitiveVisuals;

            // --- Textures
            switch (g.textures)
            {
                case QualityTier.Low: QualitySettings.globalTextureMipmapLimit = 2; QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable; break;
                case QualityTier.Medium: QualitySettings.globalTextureMipmapLimit = 1; QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable; break;
                default: QualitySettings.globalTextureMipmapLimit = 0; QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable; break;
            }

            // --- Reflections
            QualitySettings.realtimeReflectionProbes = g.reflections >= QualityTier.Medium;

            // --- View distance (LOD bias here, far plane per camera below)
            QualitySettings.lodBias = g.viewDistance == QualityTier.Low ? 0.7f : g.viewDistance == QualityTier.Medium ? 1f
                                    : g.viewDistance == QualityTier.High ? 1.5f : 2f;

            // --- URP asset
            var urp = Asset();
            if (urp != null)
            {
                Capture(urp);
                switch (g.shadows)
                {
                    case QualityTier.Off: urp.shadowDistance = 0f; urp.shadowCascadeCount = 1; break;
                    case QualityTier.Low: urp.shadowDistance = 30f; urp.shadowCascadeCount = 1; break;
                    case QualityTier.Medium: urp.shadowDistance = 60f; urp.shadowCascadeCount = 2; break;
                    case QualityTier.High: urp.shadowDistance = 110f; urp.shadowCascadeCount = 4; break;
                    default: urp.shadowDistance = 160f; urp.shadowCascadeCount = 4; break;
                }
                SetProp(urp, "mainLightShadowmapResolution", g.shadows >= QualityTier.Ultra ? 4096 : g.shadows >= QualityTier.Medium ? 2048 : 1024);
                SetProp(urp, "supportsHDR", true);
                urp.maxAdditionalLightsCount = g.lighting == QualityTier.Low ? 2 : g.lighting == QualityTier.Medium ? 4 : 8;
                urp.renderScale = Mathf.Clamp(g.renderScale, 0.5f, 1f);
                urp.msaaSampleCount = 1; // screen-space AA is handled per camera
                SetAmbientOcclusion(urp, g.ambientOcclusion != QualityTier.Off && !competitive);
            }

            // --- VFX density
            SimpleVfx.ReducedEffects = competitive || g.effects == QualityTier.Low || g.particles == QualityTier.Low;

            ApplyToCameras(g);
            PostProcessController.Apply(g);
        }

        /// <summary>Per-camera settings. Called again whenever a scene loads.</summary>
        public static void ApplyToCameras(GraphicsSettingsData g)
        {
            float far = g.viewDistance == QualityTier.Low ? 250f : g.viewDistance == QualityTier.Medium ? 400f
                      : g.viewDistance == QualityTier.High ? 600f : 900f;
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null) continue;
                cam.farClipPlane = far;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                data.renderPostProcessing = true;
                switch (g.antiAliasing)
                {
                    case AntiAliasingOption.Off: data.antialiasing = AntialiasingMode.None; break;
                    case AntiAliasingOption.FXAA: data.antialiasing = AntialiasingMode.FastApproximateAntialiasing; break;
                    case AntiAliasingOption.SMAA:
                        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        data.antialiasingQuality = g.postProcessing >= QualityTier.High ? AntialiasingQuality.High : AntialiasingQuality.Medium;
                        break;
                    default: data.antialiasing = AntialiasingMode.TemporalAntiAliasing; break;
                }
            }
        }

        private static UniversalRenderPipelineAsset Asset()
        {
            if (_asset != null) return _asset;
            _asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (_asset == null) _asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            return _asset;
        }

        private static void Capture(UniversalRenderPipelineAsset urp)
        {
            if (_original.Captured) return;
            _original = new Snapshot
            {
                ShadowDistance = urp.shadowDistance,
                Cascades = urp.shadowCascadeCount,
                Msaa = urp.msaaSampleCount,
                RenderScale = urp.renderScale,
                AdditionalLights = urp.maxAdditionalLightsCount,
                ShadowRes = urp.mainLightShadowmapResolution,
                Captured = true
            };
        }

        /// <summary>Editor safety: put the project's URP asset back the way it was.</summary>
        public static void RestoreOriginal()
        {
            if (!_original.Captured || _asset == null) return;
            _asset.shadowDistance = _original.ShadowDistance;
            _asset.shadowCascadeCount = _original.Cascades;
            _asset.msaaSampleCount = _original.Msaa;
            _asset.renderScale = _original.RenderScale;
            _asset.maxAdditionalLightsCount = _original.AdditionalLights;
            SetProp(_asset, "mainLightShadowmapResolution", _original.ShadowRes);
            foreach (var kv in OriginalFeatureState)
                if (kv.Key != null) kv.Key.SetActive(kv.Value);
            OriginalFeatureState.Clear();
            _original.Captured = false;
        }

        /// <summary>Sets a URP asset property if this URP version exposes a setter (quietly skipped otherwise).</summary>
        private static void SetProp(UniversalRenderPipelineAsset urp, string name, object value)
        {
            try
            {
                var p = typeof(UniversalRenderPipelineAsset).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                var set = p != null ? p.GetSetMethod(true) : null;
                if (set != null) set.Invoke(urp, new[] { value });
            }
            catch (System.Exception) { }
        }

        private static void SetAmbientOcclusion(UniversalRenderPipelineAsset urp, bool enabled)
        {
            // Renderer data isn't exposed publicly; look it up defensively. If anything fails, AO is simply left as-is.
            try
            {
                var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                var list = field != null ? field.GetValue(urp) as ScriptableRendererData[] : null;
                if (list == null) return;
                foreach (var data in list)
                {
                    if (data == null) continue;
                    foreach (var feature in data.rendererFeatures)
                    {
                        if (feature == null || !feature.GetType().Name.Contains("AmbientOcclusion")) continue;
                        if (!OriginalFeatureState.ContainsKey(feature)) OriginalFeatureState[feature] = feature.isActive;
                        feature.SetActive(enabled);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[VAMP] Could not toggle ambient occlusion: " + e.Message);
            }
        }
    }
}
