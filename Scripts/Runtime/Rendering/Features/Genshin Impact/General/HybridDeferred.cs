using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;

namespace HoyoToon.Runtime.Rendering.GI
{
    public class HybridDeferred : ScriptableRendererFeature
    {
        [SerializeField] HybridDeferredSettings settings = new HybridDeferredSettings();
        HybridDeferredPass m_Pass;

        public override void Create()
        {
            settings ??= new HybridDeferredSettings();
            m_Pass = new HybridDeferredPass();
            m_Pass.renderPassEvent = settings.renderPassEvent;
            m_Pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null)
                settings = new HybridDeferredSettings();

            renderer.EnqueuePass(m_Pass);
        }

        [Serializable]
        public class HybridDeferredSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        class HybridDeferredPass : ScriptableRenderPass
        {
            static readonly ShaderTagId k_HybridDeferredTag = new ShaderTagId("HYBRIDDEFERRED");
            static readonly ShaderTagId k_HybridDeferredHairTransparencyTag = new ShaderTagId("HYBRIDDEFERREDHAIRTRANSPARENCY");
            static readonly ShaderTagId k_HybridDeferredHairShadowStencilTag = new ShaderTagId("HYBRIDDEFERREDHAIRSHADOWSTENCIL");
            static readonly ShaderTagId k_HybridDeferredOutlineTag = new ShaderTagId("HYBRIDDEFERREDOUTLINE");

            static readonly List<ShaderTagId> k_HybridDeferredPassTags = new List<ShaderTagId>
            {
                k_HybridDeferredTag,
                k_HybridDeferredHairTransparencyTag,
                k_HybridDeferredHairShadowStencilTag
            };

            static readonly List<ShaderTagId> k_HybridDeferredOutlinePassTags = new List<ShaderTagId>
            {
                k_HybridDeferredOutlineTag
            };

            static readonly SortingCriteria k_QueueDrivenSortFlags =
                SortingCriteria.SortingLayer |
                SortingCriteria.RenderQueue |
                SortingCriteria.CanvasOrder |
                SortingCriteria.OptimizeStateChanges;

            static readonly Color k_TransparentClearColor = new Color(0f, 0f, 0f, 0f);

            class PassData
            {
                public RendererListHandle hybridDeferred;
                public RendererListHandle hybridDeferredOutline;
            }

            static void ExecutePass(PassData data, RasterGraphContext context)
            {
                // Main hybrid deferred stages first.
                context.cmd.DrawRendererList(data.hybridDeferred);

                // Outline must render after all deferred/hair stages.
                context.cmd.DrawRendererList(data.hybridDeferredOutline);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                RendererListHandle CreateRendererList(List<ShaderTagId> shaderTagIds)
                {
                    DrawingSettings drawingSettings = CreateDrawingSettings(shaderTagIds, renderingData, cameraData, lightData, k_QueueDrivenSortFlags);
                    FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all);
                    RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
                    return renderGraph.CreateRendererList(rendererListParams);
                }

                const string passName = "Hybrid Deferred";

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                {
                    passData.hybridDeferred = CreateRendererList(k_HybridDeferredPassTags);
                    passData.hybridDeferredOutline = CreateRendererList(k_HybridDeferredOutlinePassTags);

                    builder.UseRendererList(passData.hybridDeferred);
                    builder.UseRendererList(passData.hybridDeferredOutline);

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
                }
            }
        }
    }
}
