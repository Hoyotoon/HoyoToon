using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using HoyoToon.Rendering.PostProcessing.HSR.ChromaticAberration;
using HoyoToon.Rendering.PostProcessing.HSR.Uber;

namespace HoyoToon.Rendering.PostProcessing.HSR.RadialBlur
{
    public class RPGRadialBlurRenderer : ScriptableRendererFeature
    {
        private RPGRadialBlurRenderPass _renderPass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_renderPass == null)
            {
                return;
            }

            renderer.EnqueuePass(_renderPass);
        }

        public override void Create()
        {
            _renderPass = new RPGRadialBlurRenderPass();
        }

        protected override void Dispose(bool disposing)
        {
            _renderPass?.Dispose();
            _renderPass = null;
        }

        class RPGRadialBlurRenderPass : ScriptableRenderPass
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

            private Material _material;
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
            private readonly int mainTexId = Shader.PropertyToID("_MainTex");
            private bool _loggedMissingSettings;

            public RPGRadialBlurRenderPass()
            {
                _material = CoreUtils.CreateEngineMaterial(ShaderName);
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
                if (_material == null)
                {
                    return;
                }

                CoreUtils.Destroy(_material);
                _material = null;
            }

            private int GetPassIndex(BlurMode mode)
            {
                if (mode == BlurMode.Directional)
                {
                    if (_directionalPassIndex != int.MinValue)
                    {
                        return _directionalPassIndex;
                    }

                    _directionalPassIndex = _material.FindPass(DirectionalShaderPassName);
                    if (_directionalPassIndex == -1)
                    {
                        Debug.LogWarning($"{nameof(RPGRadialBlurRenderer)}: shader pass '{DirectionalShaderPassName}' was not found on '{ShaderName}'.");
                        return GetPassIndex(BlurMode.Radial);
                    }

                    return _directionalPassIndex;
                }

                if (mode == BlurMode.Radial)
                {
                    if (_radialBlurPassIndex != int.MinValue)
                    {
                        return _radialBlurPassIndex;
                    }

                    _radialBlurPassIndex = _material.FindPass(ShaderPassName);
                    if (_radialBlurPassIndex == -1)
                    {
                        Debug.LogError($"Shader {_material.shader.name} does not have a pass named {ShaderPassName}");
                    }

                    return _radialBlurPassIndex;
                }

                if (_combinedPassIndex != int.MinValue)
                {
                    return _combinedPassIndex;
                }

                _combinedPassIndex = _material.FindPass(CombinedShaderPassName);
                if (_combinedPassIndex == -1)
                {
                    Debug.LogWarning($"{nameof(RPGRadialBlurRenderer)}: shader pass '{CombinedShaderPassName}' was not found on '{ShaderName}'.");
                    return GetPassIndex(BlurMode.Radial);
                }

                return _combinedPassIndex;
            }

            private bool TryApplyVolumeSettings()
            {
                if (_material == null)
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
                    _material.SetVector(radialParamId, new Vector4(0f, 1f, 0.5f, 0.5f));
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

                    _material.SetVector(directionalParamsId, new Vector4(step.x, step.y, directionalSampleCount, 0f));
                    _blurMode = BlurMode.Directional;
                    return true;
                }

                float sampleCount = Mathf.Clamp(Mathf.Round(settings.RadialIteration.value), 1f, 10f);
                float totalBlurRadius = settings.RadialBlurRadius.value;
                float blurStep = totalBlurRadius / sampleCount;
                float centerX = settings.RadialBlurX.value;
                float centerY = settings.RadialBlurY.value;

                _material.SetVector(radialParamId, new Vector4(blurStep, sampleCount, centerX, centerY));

                RPGChromaticAberration chromaticSettings = VolumeManager.instance.stack.GetComponent<RPGChromaticAberration>();
                bool chromaticAllowedByUber = uberSettings == null || !uberSettings.UseUberControl.value || uberSettings.EnableChromaticAberration.value;
                bool useCombinedPass = chromaticAllowedByUber && chromaticSettings != null && chromaticSettings.CombineWithRadialBlur.value;
                _blurMode = useCombinedPass ? BlurMode.Combined : BlurMode.Radial;
                if (useCombinedPass)
                {
                    _material.SetColor(filterAId, chromaticSettings.FilterA.value);
                    _material.SetColor(filterBId, chromaticSettings.FilterB.value);
                    _material.SetColor(filterCId, chromaticSettings.FilterC.value);
                    _material.SetFloat(intensityId, chromaticSettings.intensity.value);
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

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureHandle source = resourceData.activeColorTexture;
                if (!source.IsValid())
                {
                    return;
                }

                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "CameraColor-RPGRadialBlur";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters blitParameters = new(
                    source,
                    destination,
                    _material,
                    passIndex,
                    null,
                    RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle,
                    mainTexId);
                renderGraph.AddBlitPass(blitParameters, passName: RenderGraphPassName);

                resourceData.cameraColor = destination;
            }

        }
    }
}