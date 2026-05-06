using System;
using HoyoToon.Runtime.Rendering.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HoyoToon.Runtime.Rendering.PostProcessing.HSR
{
    public abstract class HsrPostProcessRendererFeature<TPass> : ScriptableRendererFeature
        where TPass : ScriptableRenderPass, IDisposable
    {
        private TPass _renderPass;

        public sealed override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_renderPass == null || ShouldSkipCamera(renderingData.cameraData.cameraType))
            {
                return;
            }

            if (!ShouldEnqueuePass(ref renderingData, _renderPass))
            {
                return;
            }

            ConfigurePass(ref renderingData, _renderPass);
            renderer.EnqueuePass(_renderPass);
        }

        public sealed override void Create()
        {
            _renderPass?.Dispose();
            _renderPass = null;
            _renderPass = CreateRenderPass();
        }

        protected sealed override void Dispose(bool disposing)
        {
            _renderPass?.Dispose();
            _renderPass = default;
        }

        protected virtual void ConfigurePass(ref RenderingData renderingData, TPass renderPass)
        {
        }

        protected virtual bool ShouldEnqueuePass(ref RenderingData renderingData, TPass renderPass)
        {
            return true;
        }

        protected abstract TPass CreateRenderPass();

        private static bool ShouldSkipCamera(CameraType cameraType)
        {
            return cameraType == CameraType.Preview || cameraType == CameraType.Reflection;
        }
    }

    public enum MissingShaderPassLogLevel
    {
        None,
        Warning,
        Error,
    }

    public abstract class HsrFullscreenMaterialRenderPass : ScriptableRenderPass, IDisposable
    {
        private readonly int _mainTexId = Shader.PropertyToID("_MainTex");

        protected HsrFullscreenMaterialRenderPass(string shaderName)
        {
            PassMaterial = CoreUtils.CreateEngineMaterial(shaderName);
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        protected Material PassMaterial { get; private set; }

        public void Dispose()
        {
            if (PassMaterial == null)
            {
                return;
            }

            CoreUtils.Destroy(PassMaterial);
            PassMaterial = null;
        }

        protected int FindPass(ref int cachedPassIndex, string shaderPassName, string missingPassMessage, MissingShaderPassLogLevel logLevel)
        {
            if (cachedPassIndex != int.MinValue)
            {
                return cachedPassIndex;
            }

            cachedPassIndex = MaterialPassResolver.ResolveNamedPass(PassMaterial, shaderPassName);
            if (cachedPassIndex < 0)
            {
                if (logLevel == MissingShaderPassLogLevel.Warning)
                {
                    Debug.LogWarning(missingPassMessage);
                }
                else if (logLevel == MissingShaderPassLogLevel.Error)
                {
                    Debug.LogError(missingPassMessage);
                }
            }

            return cachedPassIndex;
        }

        protected bool TryGetSourceTexture(
            ContextContainer frameData,
            bool allowCameraColorFallback,
            bool skipActiveTargetBackBuffer,
            out UniversalResourceData resourceData,
            out TextureHandle source)
        {
            resourceData = frameData.Get<UniversalResourceData>();
            if (skipActiveTargetBackBuffer && resourceData.isActiveTargetBackBuffer)
            {
                source = default;
                return false;
            }

            source = resourceData.activeColorTexture;
            if (!source.IsValid() && allowCameraColorFallback)
            {
                source = resourceData.cameraColor;
            }

            return source.IsValid();
        }

        protected TextureHandle CreateColorDestination(RenderGraph renderGraph, TextureHandle source, string destinationName)
        {
            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = destinationName;
            destinationDesc.clearBuffer = false;
            return renderGraph.CreateTexture(destinationDesc);
        }

        protected RenderGraphUtils.BlitMaterialParameters CreateBlitParameters(TextureHandle source, TextureHandle destination, int passIndex)
        {
            return new RenderGraphUtils.BlitMaterialParameters(
                source,
                destination,
                PassMaterial,
                passIndex,
                null,
                RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle,
                _mainTexId);
        }
    }
}
