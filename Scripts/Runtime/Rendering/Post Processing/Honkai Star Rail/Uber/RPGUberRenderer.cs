using HoyoToon.Runtime.Rendering.PostProcessing.HSR.Bloom;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.ToneMapping;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR.Uber
{
    public class RPGUberRenderer : HsrPostProcessRendererFeature<RPGUberRenderer.RPGUberRenderPass>
    {
        protected override bool ShouldEnqueuePass(ref RenderingData renderingData, RPGUberRenderPass renderPass)
        {
            RPGUber uberSettings = VolumeManager.instance.stack.GetComponent<RPGUber>();

            RPGBloom bloomSettings = VolumeManager.instance.stack.GetComponent<RPGBloom>();
            bool useUberControl = uberSettings != null
                && uberSettings.UseUberControl.overrideState
                && uberSettings.UseUberControl.value;
            bool bloomAllowed = !useUberControl || uberSettings.EnableBloom.value;
            bool bloomActive = bloomAllowed && bloomSettings != null && bloomSettings.IsActive();
            if (bloomActive)
                return true;

            RPGTonemapping tonemappingSettings = VolumeManager.instance.stack.GetComponent<RPGTonemapping>();
            if (tonemappingSettings != null
                && tonemappingSettings.active
                && tonemappingSettings.IsActive()
                && tonemappingSettings.AnyPropertiesIsOverridden()
                && tonemappingSettings.tonemapping == RPGTonemapping.TonemappingMethod.GenerateLUTTexture)
            {
                return true;
            }

            return HasActiveUberSettings(uberSettings);
        }

        private static bool HasActiveUberSettings(RPGUber settings)
        {
            if (settings == null || !settings.active)
                return false;

            if (settings.UseUberControl.value)
                return true;

            return settings.BakedLutTexture.overrideState
                || settings.LutSlices.overrideState
                || settings.LutFactor.overrideState
                || settings.FlipLutY.overrideState;
        }

        protected override RPGUberRenderPass CreateRenderPass()
        {
            return new RPGUberRenderPass();
        }

        public sealed class RPGUberRenderPass : HsrFullscreenMaterialRenderPass
        {
            private const string ShaderName = "HoyoToon/Honkai Star Rail/Post Processing/Uber/UberPostProcess";
            private const string UberPassName = "UberPost";
            private const string RenderGraphPassName = "RPG Uber Post";

            private readonly int _bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
            private readonly int _hsrBloomTexId = Shader.PropertyToID("_HSRBloomTexture");
            private readonly int _lut2DTexId = Shader.PropertyToID("_Lut2DTex");
            private readonly int _lut2DTexParamId = Shader.PropertyToID("_Lut2DTexParam");

            private int _uberPassIndex = int.MinValue;
            private bool _loggedMissingBloom;
            private bool _loggedMissingLut;
            private bool _useBloomTexture;
            private bool _useGeneratedTonemappingLut;

            public RPGUberRenderPass()
                : base(ShaderName)
            {
            }

            private int GetPassIndex()
            {
                return FindPass(
                    ref _uberPassIndex,
                    UberPassName,
                    $"{nameof(RPGUberRenderer)}: shader pass '{UberPassName}' was not found on '{ShaderName}'.",
                    MissingShaderPassLogLevel.Warning);
            }

            private bool TryApplyVolumeSettings(Camera camera)
            {
                if (PassMaterial == null)
                {
                    return false;
                }

                RPGUber uberSettings = VolumeManager.instance.stack.GetComponent<RPGUber>();
                bool shouldUseUberControl = uberSettings != null
                    && uberSettings.UseUberControl.overrideState
                    && uberSettings.UseUberControl.value;
                bool allowBloom = !shouldUseUberControl || uberSettings.EnableBloom.value;

                RPGBloom bloomSettings = VolumeManager.instance.stack.GetComponent<RPGBloom>();
                bool bloomActive = allowBloom && bloomSettings != null && bloomSettings.IsActive();
                bool bloomTextureAvailable = bloomActive && RPGBloomRenderer.HasBloomTextureForCamera(camera);

                _useBloomTexture = bloomTextureAvailable;
                PassMaterial.SetTexture(_hsrBloomTexId, bloomTextureAvailable ? null : Texture2D.blackTexture);

                RPGTonemapping tonemappingSettings = VolumeManager.instance.stack.GetComponent<RPGTonemapping>();
                _useGeneratedTonemappingLut = tonemappingSettings != null
                    && tonemappingSettings.active
                    && tonemappingSettings.IsActive()
                    && tonemappingSettings.AnyPropertiesIsOverridden()
                    && tonemappingSettings.tonemapping == RPGTonemapping.TonemappingMethod.GenerateLUTTexture
                    && RPGTonemappingRenderer.HasGeneratedLutForCamera(camera);

                if (_useGeneratedTonemappingLut)
                {
                    // Clear local material binding so the shader reads the generated global _Lut2DTex.
                    PassMaterial.SetTexture(_lut2DTexId, null);
                    _loggedMissingLut = false;
                }
                else
                {
                    Texture lutTexture = uberSettings != null ? uberSettings.BakedLutTexture.value : null;
                    if (lutTexture == null)
                    {
                        lutTexture = RPGUber.GetDefaultLutTexture();
                    }

                    if (lutTexture == null)
                    {
                        if (!_loggedMissingLut)
                        {
                            Debug.LogWarning($"{nameof(RPGUberRenderer)}: no LUT texture is available; skipping the Uber pass.");
                            _loggedMissingLut = true;
                        }

                        return false;
                    }

                    _loggedMissingLut = false;
                    PassMaterial.SetTexture(_lut2DTexId, lutTexture);
                }

                Vector2 lutFactor = uberSettings != null ? uberSettings.LutFactor.value : new Vector2(0.00098f, 0.03125f);
                int lutSlices = uberSettings != null ? uberSettings.LutSlices.value : 31;
                float lutFlipY = uberSettings != null && uberSettings.FlipLutY.value ? 1f : 0f;
                PassMaterial.SetVector(_lut2DTexParamId, new Vector4(lutFactor.x, lutFactor.y, lutSlices, lutFlipY));

                float bloomIntensity = 0f;
                if (bloomTextureAvailable)
                {
                    _loggedMissingBloom = false;
                    bloomIntensity = bloomSettings.BloomIntensity.value;
                }
                else if (bloomActive && !_loggedMissingBloom)
                {
                    Debug.LogWarning($"{nameof(RPGUberRenderer)}: {nameof(RPGBloom)} is active but no bloom texture was produced this frame; Uber will continue with zero bloom contribution.");
                    _loggedMissingBloom = true;
                }

                PassMaterial.SetFloat(_bloomIntensityId, bloomIntensity);
                return true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                if (!TryApplyVolumeSettings(cameraData.camera))
                {
                    return;
                }

                int passIndex = GetPassIndex();
                if (passIndex < 0)
                {
                    return;
                }

                if (!TryGetSourceTexture(frameData, allowCameraColorFallback: true, skipActiveTargetBackBuffer: false, out UniversalResourceData resourceData, out TextureHandle source))
                {
                    return;
                }

                TextureHandle destination = CreateColorDestination(renderGraph, source, "CameraColor-RPGUberPost");
                var blitParameters = CreateBlitParameters(source, destination, passIndex);

                using (IBaseRenderGraphBuilder builder = renderGraph.AddBlitPass(
                           blitParameters,
                           passName: RenderGraphPassName,
                           returnBuilder: true))
                {
                    if (_useGeneratedTonemappingLut)
                    {
                        builder.UseGlobalTexture(_lut2DTexId);
                    }

                    if (_useBloomTexture)
                    {
                        builder.UseGlobalTexture(_hsrBloomTexId);
                    }
                }

                resourceData.cameraColor = destination;
            }
        }
    }
}
