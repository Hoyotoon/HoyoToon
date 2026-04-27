using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.ChromaticAberration;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.Uber;

namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR.RadialBlur
{
    public class RPGRadialBlurRenderer : HsrPostProcessRendererFeature<RPGRadialBlurRenderer.RPGRadialBlurRenderPass>
    {
        protected override RPGRadialBlurRenderPass CreateRenderPass()
        {
            return new RPGRadialBlurRenderPass();
        }

        public sealed class RPGRadialBlurRenderPass : HsrFullscreenMaterialRenderPass
        {
            private const string ShaderName = "HoyoToon/Honkai Star Rail/Post Processing/Uber/UberPostProcess";
            private const string ShaderPassName = "RadialBlur";
            private const string CombinedShaderPassName = "RadialBlurWithChromaticAberration";
            private const string DirectionalShaderPassName = "DirectionalBlur";
            private const string RenderGraphPassName = "RPG Radial Blur";

            private enum BlurMode
            {
                Radial,
                Combined,
                Directional,
            }

            private int _radialBlurPassIndex = int.MinValue;
            private int _combinedPassIndex = int.MinValue;
            private int _directionalPassIndex = int.MinValue;
            private BlurMode _blurMode = BlurMode.Radial;

            private readonly int radialParamId = Shader.PropertyToID("_RadialParam");
            private readonly int directionalParamsId = Shader.PropertyToID("_DirectionalBlurParams");
            private readonly int filterAId = Shader.PropertyToID("_ChromaFilterA");
            private readonly int filterBId = Shader.PropertyToID("_ChromaFilterB");
            private readonly int filterCId = Shader.PropertyToID("_ChromaFilterC");
            private readonly int intensityId = Shader.PropertyToID("_ChromaticAberration_Amount");
            private bool _loggedMissingSettings;

            public RPGRadialBlurRenderPass()
                : base(ShaderName)
            {
            }

            private int GetPassIndex(BlurMode mode)
            {
                if (mode == BlurMode.Directional)
                {
                    int directionalPassIndex = FindPass(
                        ref _directionalPassIndex,
                        DirectionalShaderPassName,
                        $"{nameof(RPGRadialBlurRenderer)}: shader pass '{DirectionalShaderPassName}' was not found on '{ShaderName}'.",
                        MissingShaderPassLogLevel.Warning);
                    if (directionalPassIndex == -1)
                    {
                        return GetPassIndex(BlurMode.Radial);
                    }

                    return directionalPassIndex;
                }

                if (mode == BlurMode.Radial)
                {
                    string shaderLabel = PassMaterial != null ? PassMaterial.shader.name : ShaderName;
                    return FindPass(
                        ref _radialBlurPassIndex,
                        ShaderPassName,
                        $"Shader {shaderLabel} does not have a pass named {ShaderPassName}",
                        MissingShaderPassLogLevel.Error);
                }

                int combinedPassIndex = FindPass(
                    ref _combinedPassIndex,
                    CombinedShaderPassName,
                    $"{nameof(RPGRadialBlurRenderer)}: shader pass '{CombinedShaderPassName}' was not found on '{ShaderName}'.",
                    MissingShaderPassLogLevel.Warning);
                if (combinedPassIndex == -1)
                {
                    return GetPassIndex(BlurMode.Radial);
                }

                return combinedPassIndex;
            }

            private bool TryApplyVolumeSettings()
            {
                if (PassMaterial == null)
                {
                    return false;
                }

                RPGUber uberSettings = VolumeManager.instance.stack.GetComponent<RPGUber>();
                if (uberSettings != null && uberSettings.UseUberControl.value && !uberSettings.EnableRadialBlur.value)
                {
                    return false;
                }

                RPGRadialBlur settings = VolumeManager.instance.stack.GetComponent<RPGRadialBlur>();
                if (settings == null)
                {
                    if (!_loggedMissingSettings)
                    {
                        Debug.LogWarning($"{nameof(RPGRadialBlurRenderer)}: no {nameof(RPGRadialBlur)} found in the active volume stack.");
                        _loggedMissingSettings = true;
                    }

                    // Defaults keep the pass valid while producing no visible blur.
                    PassMaterial.SetVector(radialParamId, new Vector4(0f, 1f, 0.5f, 0.5f));
                    _blurMode = BlurMode.Radial;
                    
                    return true;
                }

                if (!settings.IsActive())
                {
                    return false;
                }

                _loggedMissingSettings = false;

                if (settings.EnableDirectionBlur.value)
                {
                    float directionalSampleCount = Mathf.Clamp(Mathf.Round(settings.BlurIteration.value), 1f, 10f);
                    float angleRadians = settings.Angle.value * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
                    Vector2 step = direction * settings.BlurRadius.value;

                    PassMaterial.SetVector(directionalParamsId, new Vector4(step.x, step.y, directionalSampleCount, 0f));
                    _blurMode = BlurMode.Directional;
                    return true;
                }

                float sampleCount = Mathf.Clamp(Mathf.Round(settings.RadialIteration.value), 1f, 10f);
                float totalBlurRadius = settings.RadialBlurRadius.value;
                float blurStep = totalBlurRadius / sampleCount;
                float centerX = settings.RadialBlurX.value;
                float centerY = settings.RadialBlurY.value;

                PassMaterial.SetVector(radialParamId, new Vector4(blurStep, sampleCount, centerX, centerY));

                RPGChromaticAberration chromaticSettings = VolumeManager.instance.stack.GetComponent<RPGChromaticAberration>();
                bool chromaticAllowedByUber = uberSettings == null || !uberSettings.UseUberControl.value || uberSettings.EnableChromaticAberration.value;
                bool useCombinedPass = chromaticAllowedByUber && chromaticSettings != null && chromaticSettings.CombineWithRadialBlur.value;
                _blurMode = useCombinedPass ? BlurMode.Combined : BlurMode.Radial;
                if (useCombinedPass)
                {
                    PassMaterial.SetColor(filterAId, chromaticSettings.FilterA.value);
                    PassMaterial.SetColor(filterBId, chromaticSettings.FilterB.value);
                    PassMaterial.SetColor(filterCId, chromaticSettings.FilterC.value);
                    PassMaterial.SetFloat(intensityId, chromaticSettings.intensity.value);
                }

                return true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!TryApplyVolumeSettings())
                {
                    return;
                }

                int passIndex = GetPassIndex(_blurMode);
                if (passIndex < 0)
                {
                    return;
                }

                if (!TryGetSourceTexture(frameData, allowCameraColorFallback: false, skipActiveTargetBackBuffer: true, out UniversalResourceData resourceData, out TextureHandle source))
                {
                    return;
                }

                TextureHandle destination = CreateColorDestination(renderGraph, source, "CameraColor-RPGRadialBlur");
                var blitParameters = CreateBlitParameters(source, destination, passIndex);
                renderGraph.AddBlitPass(blitParameters, passName: RenderGraphPassName);

                resourceData.cameraColor = destination;
            }
        }
    }
}
