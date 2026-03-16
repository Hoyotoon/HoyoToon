using HoyoToon.Rendering.PostProcessing.HSR.Bloom;
using HoyoToon.Rendering.PostProcessing.HSR.ToneMapping;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HoyoToon.Rendering.PostProcessing.HSR.Uber
{
    public class RPGUberRenderer : ScriptableRendererFeature
    {
        private RPGUberRenderPass _renderPass;

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
            _renderPass = new RPGUberRenderPass();
        }

        protected override void Dispose(bool disposing)
        {
            _renderPass?.Dispose();
            _renderPass = null;
        }

        private class RPGUberRenderPass : ScriptableRenderPass
        {
            private const string ShaderName = "HoyoToon/Honkai Star Rail/Post Processing/Uber/UberPostProcess";
            private const string UberPassName = "UberPost";
            private const string RenderGraphPassName = "RPG Uber Post";

            private readonly int _mainTexId = Shader.PropertyToID("_MainTex");
            private readonly int _bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
            private readonly int _hsrBloomTexId = Shader.PropertyToID("_HSRBloomTexture");
            private readonly int _lut2DTexId = Shader.PropertyToID("_Lut2DTex");
            private readonly int _lut2DTexParamId = Shader.PropertyToID("_Lut2DTexParam");

            private Material _material;
            private int _uberPassIndex = int.MinValue;
            private bool _loggedMissingBloom;
            private bool _loggedMissingLut;
            private bool _useBloomTexture;
            private bool _useGeneratedTonemappingLut;

            public RPGUberRenderPass()
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
                if (_uberPassIndex != int.MinValue)
                {
                    return _uberPassIndex;
                }

                _uberPassIndex = _material != null ? _material.FindPass(UberPassName) : -1;
                if (_uberPassIndex < 0)
                {
                    Debug.LogWarning($"{nameof(RPGUberRenderer)}: shader pass '{UberPassName}' was not found on '{ShaderName}'.");
                }

                return _uberPassIndex;
            }

            private bool TryApplyVolumeSettings()
            {
                if (_material == null)
                {
                    return false;
                }

                RPGUber uberSettings = VolumeManager.instance.stack.GetComponent<RPGUber>();
                bool shouldUseUberControl = uberSettings != null
                    && uberSettings.UseUberControl.overrideState
                    && uberSettings.UseUberControl.value;
                if (shouldUseUberControl && !uberSettings.EnableBloom.value)
                {
                    return false;
                }

                RPGBloom bloomSettings = VolumeManager.instance.stack.GetComponent<RPGBloom>();
                bool bloomActive = bloomSettings != null && bloomSettings.IsActive();
                if (!shouldUseUberControl && !bloomActive)
                {
                    return false;
                }

                _useBloomTexture = bloomActive;

                RPGTonemapping tonemappingSettings = VolumeManager.instance.stack.GetComponent<RPGTonemapping>();
                _useGeneratedTonemappingLut = tonemappingSettings != null
                    && tonemappingSettings.active
                    && tonemappingSettings.IsActive()
                    && tonemappingSettings.AnyPropertiesIsOverridden()
                    && tonemappingSettings.tonemapping == RPGTonemapping.TonemappingMethod.GenerateLUTTexture
                    && RPGTonemappingRenderer.HasGeneratedLutThisFrame;

                if (_useGeneratedTonemappingLut)
                {
                    // Clear local material binding so the shader reads the generated global _Lut2DTex.
                    _material.SetTexture(_lut2DTexId, null);
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
                    _material.SetTexture(_lut2DTexId, lutTexture);
                }

                Vector2 lutFactor = uberSettings != null ? uberSettings.LutFactor.value : new Vector2(0.00098f, 0.03125f);
                int lutSlices = uberSettings != null ? uberSettings.LutSlices.value : 31;
                float lutFlipY = uberSettings != null && uberSettings.FlipLutY.value ? 1f : 0f;
                _material.SetVector(_lut2DTexParamId, new Vector4(lutFactor.x, lutFactor.y, lutSlices, lutFlipY));

                float bloomIntensity = 0f;
                if (bloomActive)
                {
                    _loggedMissingBloom = false;
                    bloomIntensity = bloomSettings.BloomIntensity.value;
                }
                else if (!_loggedMissingBloom)
                {
                    Debug.LogWarning($"{nameof(RPGUberRenderer)}: no active {nameof(RPGBloom)} found; Uber pass will write camera color with zero bloom contribution.");
                    _loggedMissingBloom = true;
                }

                _material.SetFloat(_bloomIntensityId, bloomIntensity);
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

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle source = resourceData.activeColorTexture;
                if (!source.IsValid())
                {
                    source = resourceData.cameraColor;
                }

                if (!source.IsValid())
                {
                    return;
                }

                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "CameraColor-RPGUberPost";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters blitParameters = new(
                    source,
                    destination,
                    _material,
                    passIndex,
                    null,
                    RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle,
                    _mainTexId);

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
