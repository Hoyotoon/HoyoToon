using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.RadialBlur;
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.Uber;

namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR.ChromaticAberration
{
    public class RPGChromaticAberrationRenderer : HsrPostProcessRendererFeature<RPGChromaticAberrationRenderer.RPGChromaticAberrationRenderPass>
    {
        protected override RPGChromaticAberrationRenderPass CreateRenderPass()
        {
            return new RPGChromaticAberrationRenderPass();
        }

        public sealed class RPGChromaticAberrationRenderPass : HsrFullscreenMaterialRenderPass
        {
            private const string ShaderName = "HoyoToon/Honkai Star Rail/Post Processing/Uber/UberPostProcess";
            private const string AberrationPassName = "ChromaticAberration";
            private const string RenderGraphPassName = "RPG Chromatic Aberration";

            private int _chromaticPassIndex = int.MinValue;

            private readonly int filterAId = Shader.PropertyToID("_ChromaFilterA");
            private readonly int filterBId = Shader.PropertyToID("_ChromaFilterB");
            private readonly int filterCId = Shader.PropertyToID("_ChromaFilterC");
            private readonly int intensityId = Shader.PropertyToID("_ChromaticAberration_Amount");
            private bool _loggedMissingSettings;

            public RPGChromaticAberrationRenderPass()
                : base(ShaderName)
            {
            }

            private int GetPassIndex()
            {
                return FindPass(
                    ref _chromaticPassIndex,
                    AberrationPassName,
                    $"{nameof(RPGChromaticAberrationRenderer)}: shader pass '{AberrationPassName}' was not found on '{ShaderName}'.",
                    MissingShaderPassLogLevel.Warning);
            }

            private bool TryApplyVolumeSettings()
            {
                if (PassMaterial == null)
                {
                    return false;
                }

                RPGUber uberSettings = VolumeManager.instance.stack.GetComponent<RPGUber>();
                if (uberSettings != null && uberSettings.UseUberControl.value && !uberSettings.EnableChromaticAberration.value)
                {
                    return false;
                }

                RPGChromaticAberration settings = VolumeManager.instance.stack.GetComponent<RPGChromaticAberration>();
                if (settings == null)
                {
                    if (!_loggedMissingSettings)
                    {
                        Debug.LogWarning($"{nameof(RPGChromaticAberrationRenderer)}: no {nameof(RPGChromaticAberration)} found in the active volume stack.");
                        _loggedMissingSettings = true;
                    }

                    return false;
                }

                if (!settings.IsActive())
                {
                    return false;
                }

                // Combined rendering is owned by RPGRadialBlurRenderer when this toggle is enabled.
                if (settings.CombineWithRadialBlur.value)
                {
                    bool radialAllowedByUber = uberSettings == null || !uberSettings.UseUberControl.value || uberSettings.EnableRadialBlur.value;
                    if (radialAllowedByUber)
                    {
                        return false;
                    }
                }

                _loggedMissingSettings = false;
                PassMaterial.SetColor(filterAId, settings.FilterA.value);
                PassMaterial.SetColor(filterBId, settings.FilterB.value);
                PassMaterial.SetColor(filterCId, settings.FilterC.value);
                PassMaterial.SetFloat(intensityId, settings.intensity.value);

                return true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!TryApplyVolumeSettings())
                {
                    return;
                }

                int passIndex = GetPassIndex();
                if (passIndex < 0)
                {
                    return;
                }

                if (!TryGetSourceTexture(frameData, allowCameraColorFallback: false, skipActiveTargetBackBuffer: true, out UniversalResourceData resourceData, out TextureHandle source))
                {
                    return;
                }

                TextureHandle destination = CreateColorDestination(renderGraph, source, "CameraColor-RPGChromaticAberration");
                var blitParameters = CreateBlitParameters(source, destination, passIndex);
                renderGraph.AddBlitPass(blitParameters, passName: RenderGraphPassName);

                resourceData.cameraColor = destination;
            }
        }
    }
}
