using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using HoyoToon.Rendering.PostProcessing.HSR.RadialBlur;
using HoyoToon.Rendering.PostProcessing.HSR.Uber;

namespace HoyoToon.Rendering.PostProcessing.HSR.ChromaticAberration
{
    public class RPGChromaticAberrationRenderer : ScriptableRendererFeature
    {
        private RPGChromaticAberrationRenderPass _renderPass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_renderPass == null)
            {
                return;
            }

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            {
                return;
            }

            renderer.EnqueuePass(_renderPass);
        }

        public override void Create()
        {
            _renderPass = new RPGChromaticAberrationRenderPass();
        }

        protected override void Dispose(bool disposing)
        {
            _renderPass?.Dispose();
            _renderPass = null;
        }

        class RPGChromaticAberrationRenderPass : ScriptableRenderPass
        {
            private const string ShaderName = "HoyoToon/Honkai Star Rail/Post Processing/Uber/UberPostProcess";
            private const string AberrationPassName = "ChromaticAberration";
            private const string RenderGraphPassName = "RPG Chromatic Aberration";

            private Material _material;
            private int _chromaticPassIndex = int.MinValue;

            private readonly int filterAId = Shader.PropertyToID("_ChromaFilterA");
            private readonly int filterBId = Shader.PropertyToID("_ChromaFilterB");
            private readonly int filterCId = Shader.PropertyToID("_ChromaFilterC");
            private readonly int intensityId = Shader.PropertyToID("_ChromaticAberration_Amount");
            private readonly int mainTexId = Shader.PropertyToID("_MainTex");
            private bool _loggedMissingSettings;

            public RPGChromaticAberrationRenderPass()
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

            private int GetPassIndex()
            {
                if (_chromaticPassIndex != int.MinValue)
                {
                    return _chromaticPassIndex;
                }

                _chromaticPassIndex = _material != null ? _material.FindPass(AberrationPassName) : -1;
                if (_chromaticPassIndex < 0)
                {
                    Debug.LogWarning($"{nameof(RPGChromaticAberrationRenderer)}: shader pass '{AberrationPassName}' was not found on '{ShaderName}'.");
                }

                return _chromaticPassIndex;
            }

            private bool TryApplyVolumeSettings()
            {
                if (_material == null)
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
                _material.SetColor(filterAId, settings.FilterA.value);
                _material.SetColor(filterBId, settings.FilterB.value);
                _material.SetColor(filterCId, settings.FilterC.value);
                _material.SetFloat(intensityId, settings.intensity.value);

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
                destinationDesc.name = "CameraColor-RPGChromaticAberration";
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