using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Scene.HSR;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Rendering.HSR
{
    public class LightingGBuffer : ScriptableRendererFeature
    {
        internal static TextureHandle SharedAlphaMaskHandle;

        [SerializeField] LightingGBufferSettings settings = new LightingGBufferSettings();
        LightingGBufferPass m_GBufferStagePass;
        LightingGBufferPass m_ForwardStagePass;

        TextureHandle m_SharedGBufferA;
        TextureHandle m_SharedDepthBufferOrCopy;
        TextureHandle m_SharedAlphaMask;
        bool m_HasSharedForwardInputs;
        bool m_HasWarnedForwardOrder;

        /// <inheritdoc/>
        public override void Create()
        {
            settings ??= new LightingGBufferSettings();
            m_GBufferStagePass = new LightingGBufferPass(this, LightingGBufferPass.StageType.GBuffer);
            m_ForwardStagePass = new LightingGBufferPass(this, LightingGBufferPass.StageType.Forward);

            // Configure where each stage should be injected.
            m_GBufferStagePass.renderPassEvent = settings.renderPassEvent;
            m_ForwardStagePass.renderPassEvent = settings.forwardRenderPassEvent;

            m_GBufferStagePass.ConfigureInput(ScriptableRenderPassInput.Depth);
            m_ForwardStagePass.ConfigureInput(ScriptableRenderPassInput.Depth);

            // You can request URP color texture and depth buffer as inputs by uncommenting the line below,
            // URP will ensure copies of these resources are available for sampling before executing the render pass.
            // Only uncomment it if necessary, it will have a performance impact, especially on mobiles and other TBDR GPUs where it will break render passes.
            //m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);

            // You can request URP to render to an intermediate texture by uncommenting the line below.
            // Use this option for passes that do not support rendering directly to the backbuffer.
            // Only uncomment it if necessary, it will have a performance impact, especially on mobiles and other TBDR GPUs where it will break render passes.
            //m_ScriptablePass.requiresIntermediateTexture = true;
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null)
                settings = new LightingGBufferSettings();

            m_HasSharedForwardInputs = false;
            SharedAlphaMaskHandle = default;

            if (!ShouldRenderForCamera(renderingData.cameraData.cameraType, renderingData.cameraData.isPreviewCamera))
                return;

            if (!LightingGBufferPass.HasSceneLightingWork(renderingData.cameraData.camera))
                return;

            if (!m_HasWarnedForwardOrder && settings.forwardRenderPassEvent < settings.renderPassEvent)
            {
                Debug.LogWarning("LightingGBuffer: forwardRenderPassEvent is earlier than renderPassEvent. Forward stage may execute before GBuffer outputs are produced.");
                m_HasWarnedForwardOrder = true;
            }

            renderer.EnqueuePass(m_GBufferStagePass);
            renderer.EnqueuePass(m_ForwardStagePass);
        }

        static bool ShouldRenderForCamera(CameraType cameraType, bool isPreviewCamera)
        {
            if (isPreviewCamera)
                return false;

            return cameraType == CameraType.Game || cameraType == CameraType.SceneView;
        }

        // Use this class to pass around settings from the feature to the pass
        [Serializable]
        public class LightingGBufferSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            public RenderPassEvent forwardRenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        class LightingGBufferPass : ScriptableRenderPass
        {
            public enum StageType
            {
                GBuffer,
                Forward
            }

            readonly LightingGBuffer m_Owner;
            readonly StageType m_StageType;

            static readonly int k_GBufferAId = Shader.PropertyToID("_GBufferA");
            static readonly int k_GBufferBId = Shader.PropertyToID("_GBufferB");
            static readonly int k_GBufferCId = Shader.PropertyToID("_GBufferC");
            static readonly int k_DepthBufferOrCopyId = Shader.PropertyToID("_DepthBufferOrCopy");
            static readonly int k_LightingAlphaMaskId = Shader.PropertyToID("_LightingAlphaMask");

            static readonly ShaderTagId k_LightingGBufferTag = new ShaderTagId("LightingGBuffer");
            static readonly ShaderTagId k_LightingGBufferEyeHairTag = new ShaderTagId("LightingGBufferEyeHair");
            static readonly ShaderTagId k_LightingForwardTag = new ShaderTagId("LightingForward");
            static readonly ShaderTagId k_ForwardEmissionTag = new ShaderTagId("ForwardEmission");
            static readonly ShaderTagId k_CustomForwardTag = new ShaderTagId("CustomForward");
            static readonly ShaderTagId k_CustomForward2Tag = new ShaderTagId("CustomForward2");
            static readonly ShaderTagId k_CustomForwardOpaqueTag = new ShaderTagId("CustomForwardOpaque");
            static readonly ShaderTagId k_CustomForwardOpaque2Tag = new ShaderTagId("CustomForwardOpaque2");
            static readonly ShaderTagId k_CustomRpTransparentTag = new ShaderTagId("CustomRPTransparent");
            static readonly ShaderTagId k_RpgOutlineTag = new ShaderTagId("RPGOutline");
            static readonly ShaderTagId k_UniversalForwardTag = new ShaderTagId("UniversalForward");
            static readonly ShaderTagId k_UniversalForwardOnlyTag = new ShaderTagId("UniversalForwardOnly");
            static readonly ShaderTagId k_SrpDefaultUnlitTag = new ShaderTagId("SRPDefaultUnlit");
            static readonly ShaderTagId k_LightModeTag = new ShaderTagId("LightMode");
            static readonly List<ShaderTagId> k_GBufferPassTag = new List<ShaderTagId>
        {
            k_LightingGBufferTag,
            k_LightingGBufferEyeHairTag
        };
            static readonly List<ShaderTagId> k_GBufferDepthRebuildTag = new List<ShaderTagId>
        {
            k_LightingGBufferTag
        };
            static readonly List<ShaderTagId> k_ForwardEmissionPassTag = new List<ShaderTagId>
        {
            k_ForwardEmissionTag
        };
            static readonly List<ShaderTagId> k_CustomForwardPassTag = new List<ShaderTagId>
        {
            k_CustomForwardTag
        };
            static readonly List<ShaderTagId> k_CustomForward2PassTag = new List<ShaderTagId>
            {
            k_CustomForward2Tag
        };
            static readonly List<ShaderTagId> k_CustomForwardOpaquePassTag = new List<ShaderTagId>
        {
            k_CustomForwardOpaqueTag
        };
            static readonly List<ShaderTagId> k_CustomForwardOpaque2PassTag = new List<ShaderTagId>
        {
            k_CustomForwardOpaque2Tag
        };
            static readonly List<ShaderTagId> k_OutlinePassTag = new List<ShaderTagId>
        {
            k_RpgOutlineTag
        };
            static readonly List<ShaderTagId> k_AlphaMaskPassTags = new List<ShaderTagId>
        {
            k_LightingGBufferTag,
            k_LightingGBufferEyeHairTag,
            k_LightingForwardTag,
            k_ForwardEmissionTag,
            k_CustomForwardTag,
            k_CustomForward2Tag,
            k_CustomForwardOpaqueTag,
            k_CustomForwardOpaque2Tag,
            k_CustomRpTransparentTag,
            k_RpgOutlineTag,
            k_UniversalForwardTag,
            k_UniversalForwardOnlyTag,
            k_SrpDefaultUnlitTag
        };
            static readonly SortingCriteria k_QueueDrivenSortFlags =
                SortingCriteria.SortingLayer |
                SortingCriteria.RenderQueue |
                SortingCriteria.CanvasOrder |
                SortingCriteria.OptimizeStateChanges;
            static readonly ShaderTagId[] k_LightingActivityTags =
            {
                k_LightingGBufferTag,
                k_LightingGBufferEyeHairTag,
                k_LightingForwardTag,
                k_ForwardEmissionTag,
                k_CustomForwardTag,
                k_CustomForward2Tag,
                k_CustomForwardOpaqueTag,
                k_CustomForwardOpaque2Tag,
                k_CustomRpTransparentTag,
                k_RpgOutlineTag
            };
            static readonly List<HSRCharacterController> k_ActivityControllers = new List<HSRCharacterController>(8);
            static readonly List<Material> k_ActivityMaterials = new List<Material>(16);
            static readonly Dictionary<int, SceneActivityCache> k_SceneActivityCache =
                new Dictionary<int, SceneActivityCache>();
            static Material s_AlphaMaskOverrideMaterial;
            static readonly Color k_TransparentClearColor = new Color(0f, 0f, 0f, 0f);

            struct SceneActivityCache
            {
                public int RendererTopologyVersion;
                public int MaterialPassCacheVersion;
                public int ControllerSignature;
                public int SceneControllerSignature;
                public bool HasLightingWork;
            }

            public LightingGBufferPass(LightingGBuffer owner, StageType stageType)
            {
                m_Owner = owner;
                m_StageType = stageType;
            }

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void ResetSceneActivityCache()
            {
                k_ActivityControllers.Clear();
                k_ActivityMaterials.Clear();
                k_SceneActivityCache.Clear();
            }

            internal static bool HasSceneLightingWork(Camera camera)
            {
                UnityScene scene = ResolveRenderScene(camera);
                if (!RenderSceneUtility.IsSceneUsable(scene))
                    return false;

                int sceneHandle = scene.handle;
                int controllerCount = HSRCharacterController.GetRegisteredActiveControllersInScene(scene, k_ActivityControllers);
                int controllerSignature = BuildControllerSignature(controllerCount);
                HSRSceneController sceneController = HSRSceneController.FindForSceneOrActiveScene(scene);
                int sceneControllerSignature = sceneController != null ? sceneController.GetInstanceID() : 0;
                int rendererTopologyVersion = HSRCharacterController.RendererTopologyVersion;
                int materialPassCacheVersion = HsrRendererMaterialQueryUtility.MaterialPassCacheVersion;

                if (k_SceneActivityCache.TryGetValue(sceneHandle, out SceneActivityCache cache)
                    && cache.RendererTopologyVersion == rendererTopologyVersion
                    && cache.MaterialPassCacheVersion == materialPassCacheVersion
                    && cache.ControllerSignature == controllerSignature
                    && cache.SceneControllerSignature == sceneControllerSignature)
                {
                    k_ActivityControllers.Clear();
                    return cache.HasLightingWork;
                }

                bool hasLightingWork = sceneController != null;
                for (int i = 0; i < controllerCount && !hasLightingWork; ++i)
                {
                    HSRCharacterController controller = k_ActivityControllers[i];
                    if (controller == null)
                        continue;

                    hasLightingWork = ControllerHasLightingWork(controller);
                }

                k_ActivityControllers.Clear();
                k_SceneActivityCache[sceneHandle] = new SceneActivityCache
                {
                    RendererTopologyVersion = rendererTopologyVersion,
                    MaterialPassCacheVersion = materialPassCacheVersion,
                    ControllerSignature = controllerSignature,
                    SceneControllerSignature = sceneControllerSignature,
                    HasLightingWork = hasLightingWork
                };

                return hasLightingWork;
            }

            static UnityScene ResolveRenderScene(Camera camera)
            {
                return RenderSceneUtility.ResolveRenderScene(camera);
            }

            static int BuildControllerSignature(int controllerCount)
            {
                unchecked
                {
                    int hash = controllerCount;
                    for (int i = 0; i < controllerCount; ++i)
                    {
                        HSRCharacterController controller = k_ActivityControllers[i];
                        hash = (hash * 397) ^ (controller != null ? controller.GetInstanceID() : 0);
                        hash = (hash * 397) ^ (controller != null ? controller.RendererScopeVersion : 0);
                        hash = (hash * 397) ^ (controller != null ? controller.EffectMaterialsVersion : 0);
                    }

                    return hash;
                }
            }

            static bool ControllerHasLightingWork(HSRCharacterController controller)
            {
                Renderer[] scopedRenderers = controller.GetScopedRenderers();
                if (scopedRenderers != null)
                {
                    for (int i = 0; i < scopedRenderers.Length; ++i)
                    {
                        if (RendererHasLightingWork(scopedRenderers[i]))
                            return true;
                    }
                }

                List<HSRCharacterController.EffectMaterialEntry> effectMaterials = controller.EffectMaterials;
                if (effectMaterials == null)
                    return false;

                for (int i = 0; i < effectMaterials.Count; ++i)
                {
                    if (MaterialHasLightingWork(effectMaterials[i].Material))
                        return true;
                }

                return false;
            }

            static bool RendererHasLightingWork(Renderer renderer)
            {
                if (renderer == null)
                    return false;

                if (!HsrRendererMaterialQueryUtility.TryGetSharedMaterials(renderer, k_ActivityMaterials, out int materialCount))
                    return false;

                try
                {
                    for (int i = 0; i < materialCount; ++i)
                    {
                        if (MaterialHasLightingWork(k_ActivityMaterials[i]))
                            return true;
                    }
                }
                finally
                {
                    k_ActivityMaterials.Clear();
                }

                return false;
            }

            static bool MaterialHasLightingWork(Material material)
            {
                if (material == null)
                    return false;

                for (int i = 0; i < k_LightingActivityTags.Length; ++i)
                {
                    string lightModeName = k_LightingActivityTags[i].name;
                    if (HsrRendererMaterialQueryUtility.HasMaterialTagOrShaderPassOrNamedPass(
                            material,
                            "LightMode",
                            k_LightModeTag,
                            lightModeName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            // This class stores the data needed by the RenderGraph pass.
            // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
            private class GBufferPassData
            {
                public RendererListHandle lightingGBuffer;
                public RendererListHandle rpgOutline;
                public HsrInheritedLightingGlobals.SceneGlobalState sceneGlobals;
                public HsrInheritedLightingGlobals.SceneKeywordState sceneKeywords;
            }

            private class ForwardPassData
            {
                public RendererListHandle forwardEmission;
                public RendererListHandle customForward;
                public RendererListHandle customForward2;
                public RendererListHandle customForwardOpaque;
                public RendererListHandle customForwardOpaque2;
                public TextureHandle gBufferA;
                public TextureHandle depthBufferOrCopy;
                public TextureHandle alphaMask;
                public HsrInheritedLightingGlobals.SceneGlobalState sceneGlobals;
                public HsrInheritedLightingGlobals.SceneKeywordState sceneKeywords;
            }

            private class DepthRebuildPassData
            {
                public RendererListHandle lightingGBufferDepthOnly;
            }

            static GraphicsFormat GetSupportedColorFormat(GraphicsFormat preferred, GraphicsFormat fallback)
            {
                if (SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render))
                    return preferred;

                if (SystemInfo.IsFormatSupported(fallback, GraphicsFormatUsage.Render))
                    return fallback;

                return GraphicsFormat.R8G8B8A8_UNorm;
            }

            static RenderTextureDescriptor BuildColorDescriptor(RenderTextureDescriptor baseDescriptor, GraphicsFormat format)
            {
                RenderTextureDescriptor descriptor = baseDescriptor;
                descriptor.msaaSamples = Mathf.Max(1, baseDescriptor.msaaSamples);
                descriptor.depthBufferBits = 0;
                descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.graphicsFormat = format;
                descriptor.sRGB = GraphicsFormatUtility.IsSRGBFormat(format);
                descriptor.bindMS = baseDescriptor.bindMS;
                descriptor.enableRandomWrite = false;
                descriptor.autoGenerateMips = false;
                descriptor.useMipMap = false;
                descriptor.mipCount = 1;
                return descriptor;
            }

            static RenderTextureDescriptor BuildDepthCopyDescriptor(RenderTextureDescriptor baseDescriptor)
            {
                RenderTextureDescriptor descriptor = baseDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.colorFormat = RenderTextureFormat.Depth;
                descriptor.graphicsFormat = GraphicsFormat.None;
                descriptor.depthStencilFormat = GraphicsFormat.D16_UNorm;
                descriptor.depthBufferBits = 16;
                descriptor.sRGB = false;
                descriptor.bindMS = false;
                descriptor.enableRandomWrite = false;
                descriptor.autoGenerateMips = false;
                descriptor.useMipMap = false;
                descriptor.mipCount = 1;
                return descriptor;
            }

            // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
            // It is used to execute draw commands.
            static void ExecuteGBufferPass(GBufferPassData data, RasterGraphContext context)
            {
                HsrInheritedLightingGlobals.ApplySceneGlobals(context.cmd, data.sceneGlobals);
                HsrInheritedLightingGlobals.ApplySceneKeywords(context.cmd, data.sceneKeywords);
                context.cmd.DrawRendererList(data.lightingGBuffer);

                // Outline must run as the final step of Lighting GBuffer.
                context.cmd.DrawRendererList(data.rpgOutline);
                HsrInheritedLightingGlobals.ClearSceneKeywords(context.cmd);
            }

            static void ExecuteForwardPass(ForwardPassData data, RasterGraphContext context)
            {
                HsrInheritedLightingGlobals.ApplySceneGlobals(context.cmd, data.sceneGlobals);
                HsrInheritedLightingGlobals.ApplySceneKeywords(context.cmd, data.sceneKeywords);
                context.cmd.SetGlobalTexture(k_GBufferAId, data.gBufferA);
                context.cmd.SetGlobalTexture(k_DepthBufferOrCopyId, data.depthBufferOrCopy, RenderTextureSubElement.Depth);

                // Forward group order mirrors the original HSR renderer: emission, opaque custom passes, then custom forward passes.
                context.cmd.DrawRendererList(data.forwardEmission);
                context.cmd.DrawRendererList(data.customForwardOpaque);
                context.cmd.DrawRendererList(data.customForwardOpaque2);
                context.cmd.DrawRendererList(data.customForward);
                context.cmd.DrawRendererList(data.customForward2);
                HsrInheritedLightingGlobals.ClearSceneKeywords(context.cmd);
            }

            static void ExecuteDepthRebuildPass(DepthRebuildPassData data, RasterGraphContext context)
            {
                // Rebuild depth exclusively from LightingGBuffer-tagged renderers.
                context.cmd.ClearRenderTarget(true, false, k_TransparentClearColor);
                context.cmd.DrawRendererList(data.lightingGBufferDepthOnly);
            }

            // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
            // FrameData is a context container through which URP resources can be accessed and managed.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                // Make use of frameData to access resources and camera data through the dedicated containers.
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                if (!ShouldRenderForCamera(cameraData.cameraType, cameraData.isPreviewCamera))
                    return;

                HSRSceneController sceneController = ResolveSceneController(cameraData.camera);
                HsrInheritedLightingGlobals.SceneGlobalState sceneGlobals =
                    HsrInheritedLightingGlobals.CaptureSceneGlobals(sceneController, clearEnvironmentWhenMissing: true, cameraData.camera);
                HsrInheritedLightingGlobals.SceneKeywordState sceneKeywords =
                    HsrInheritedLightingGlobals.CaptureSceneKeywords(sceneController);

                RendererListHandle CreateRendererList(List<ShaderTagId> shaderTagIds, RenderQueueRange renderQueueRange, SortingCriteria sortingCriteria, Material overrideMaterial = null)
                {
                    DrawingSettings drawingSettings = CreateDrawingSettings(shaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
                    if (overrideMaterial != null)
                    {
                        drawingSettings.overrideMaterial = overrideMaterial;
                        drawingSettings.overrideMaterialPassIndex = 0;
                    }
                    FilteringSettings filteringSettings = new FilteringSettings(renderQueueRange);
                    RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
                    return renderGraph.CreateRendererList(rendererListParams);
                }

                if (m_StageType == StageType.GBuffer)
                {
                    const string gBufferPassName = "Lighting GBuffer";
                    const string gBufferACopyPassName = "Lighting GBufferA Copy";
                    const string gBufferDepthRebuildPassName = "Lighting GBuffer Depth Rebuild";

                    GraphicsFormat gBufferAFormat = GetSupportedColorFormat(GraphicsFormat.R8G8B8A8_UNorm, cameraData.cameraTargetDescriptor.graphicsFormat);
                    GraphicsFormat gBufferBFormat = GetSupportedColorFormat(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.R16G16B16A16_UNorm);
                    GraphicsFormat gBufferCFormat = GetSupportedColorFormat(GraphicsFormat.R8_UNorm, GraphicsFormat.R8G8B8A8_UNorm);
                    GraphicsFormat alphaMaskFormat = GetSupportedColorFormat(GraphicsFormat.R8_UNorm, GraphicsFormat.R8G8B8A8_UNorm);

                    RenderTextureDescriptor gBufferADescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, gBufferAFormat);
                    RenderTextureDescriptor gBufferBDescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, gBufferBFormat);
                    RenderTextureDescriptor gBufferCDescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, gBufferCFormat);
                    RenderTextureDescriptor alphaMaskDescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, alphaMaskFormat);
                    RenderTextureDescriptor depthCopyDescriptor = BuildDepthCopyDescriptor(cameraData.cameraTargetDescriptor);

                    TextureHandle gBufferA = UniversalRenderer.CreateRenderGraphTexture(renderGraph, gBufferADescriptor, "_GBufferA", false);
                    TextureHandle gBufferB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, gBufferBDescriptor, "_GBufferB", false);
                    TextureHandle gBufferC = UniversalRenderer.CreateRenderGraphTexture(renderGraph, gBufferCDescriptor, "_GBufferC", false);
                    TextureHandle alphaMask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, alphaMaskDescriptor, "_LightingAlphaMask", false);
                    TextureHandle depthBufferOrCopy = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthCopyDescriptor, "_DepthBufferOrCopy", false);

                    RendererListHandle lightingGBuffer = CreateRendererList(k_GBufferPassTag, RenderQueueRange.all, k_QueueDrivenSortFlags);
                    RendererListHandle lightingGBufferDepthOnly = CreateRendererList(k_GBufferDepthRebuildTag, RenderQueueRange.all, k_QueueDrivenSortFlags);
                    RendererListHandle rpgOutline = CreateRendererList(k_OutlinePassTag, RenderQueueRange.all, k_QueueDrivenSortFlags);

                    bool canCopyColorToGBufferA = !resourceData.isActiveTargetBackBuffer;

                    m_Owner.m_SharedGBufferA = gBufferA;
                    m_Owner.m_SharedDepthBufferOrCopy = depthBufferOrCopy;
                    m_Owner.m_SharedAlphaMask = alphaMask;
                    SharedAlphaMaskHandle = alphaMask;
                    m_Owner.m_HasSharedForwardInputs = canCopyColorToGBufferA;

                    // This pass writes all GBuffer MRTs.
                    using (var builder = renderGraph.AddRasterRenderPass<GBufferPassData>(gBufferPassName, out var passData))
                    {
                        passData.lightingGBuffer = lightingGBuffer;
                        passData.rpgOutline = rpgOutline;
                        passData.sceneGlobals = sceneGlobals;
                        passData.sceneKeywords = sceneKeywords;

                        builder.UseRendererList(passData.lightingGBuffer);
                        builder.UseRendererList(passData.rpgOutline);

                        // Bind MRTs so SV_Target0/1/2 from shader can be written to GBuffer textures.
                        builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                        builder.SetRenderAttachment(gBufferB, 1);
                        builder.SetRenderAttachment(gBufferC, 2);
                        builder.SetRenderAttachment(alphaMask, 3);
                        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                        builder.SetGlobalTextureAfterPass(gBufferB, k_GBufferBId);
                        builder.SetGlobalTextureAfterPass(gBufferC, k_GBufferCId);
                        builder.SetGlobalTextureAfterPass(alphaMask, k_LightingAlphaMaskId);
                        builder.AllowGlobalStateModification(true);
                        builder.AllowPassCulling(false);

                        builder.SetRenderFunc((GBufferPassData data, RasterGraphContext context) => ExecuteGBufferPass(data, context));
                    }

                    // Copy camera color result from SV_Target0 into _GBufferA for downstream forward sampling.
                    if (canCopyColorToGBufferA)
                    {
                        renderGraph.AddBlitPass(resourceData.activeColorTexture, gBufferA, Vector2.one, Vector2.zero, passName: gBufferACopyPassName);
                    }

                    // Keep this as its own pass so it appears as a distinct depth rebuild step after Lighting GBuffer.
                    using (var builder = renderGraph.AddRasterRenderPass<DepthRebuildPassData>(gBufferDepthRebuildPassName, out var passData))
                    {
                        passData.lightingGBufferDepthOnly = lightingGBufferDepthOnly;

                        builder.UseRendererList(passData.lightingGBufferDepthOnly);
                        builder.SetRenderAttachmentDepth(depthBufferOrCopy, AccessFlags.ReadWrite);
                        builder.SetGlobalTextureAfterPass(depthBufferOrCopy, k_DepthBufferOrCopyId);
                        builder.AllowPassCulling(false);

                        builder.SetRenderFunc((DepthRebuildPassData data, RasterGraphContext context) => ExecuteDepthRebuildPass(data, context));
                    }

                    return;
                }

                if (!m_Owner.m_HasSharedForwardInputs)
                {
                    return;
                }

                const string forwardPassName = "Lighting Forward Group";
                RendererListHandle forwardEmission = CreateRendererList(k_ForwardEmissionPassTag, RenderQueueRange.all, k_QueueDrivenSortFlags);
                RendererListHandle customForward = CreateRendererList(k_CustomForwardPassTag, RenderQueueRange.all, k_QueueDrivenSortFlags);
                RendererListHandle customForward2 = CreateRendererList(k_CustomForward2PassTag, RenderQueueRange.all, k_QueueDrivenSortFlags);
                RendererListHandle customForwardOpaque = CreateRendererList(k_CustomForwardOpaquePassTag, RenderQueueRange.all, k_QueueDrivenSortFlags);
                RendererListHandle customForwardOpaque2 = CreateRendererList(k_CustomForwardOpaque2PassTag, RenderQueueRange.all, k_QueueDrivenSortFlags);

                // Forward group pass that can sample _GBufferA from the GBuffer stage.
                using (var builder = renderGraph.AddRasterRenderPass<ForwardPassData>(forwardPassName, out var passData))
                {
                    passData.forwardEmission = forwardEmission;
                    passData.customForward = customForward;
                    passData.customForward2 = customForward2;
                    passData.customForwardOpaque = customForwardOpaque;
                    passData.customForwardOpaque2 = customForwardOpaque2;
                    passData.gBufferA = m_Owner.m_SharedGBufferA;
                    passData.depthBufferOrCopy = m_Owner.m_SharedDepthBufferOrCopy;
                    passData.alphaMask = m_Owner.m_SharedAlphaMask;
                    passData.sceneGlobals = sceneGlobals;
                    passData.sceneKeywords = sceneKeywords;

                    builder.UseRendererList(passData.forwardEmission);
                    builder.UseRendererList(passData.customForwardOpaque);
                    builder.UseRendererList(passData.customForwardOpaque2);
                    builder.UseRendererList(passData.customForward);
                    builder.UseRendererList(passData.customForward2);

                    // ForwardEmission samples _GBufferA and _DepthBufferOrCopy, so declare explicit read dependencies.
                    builder.UseTexture(passData.gBufferA, AccessFlags.Read);
                    builder.UseTexture(passData.depthBufferOrCopy, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachment(passData.alphaMask, 1);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                    builder.SetGlobalTextureAfterPass(passData.alphaMask, k_LightingAlphaMaskId);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((ForwardPassData data, RasterGraphContext context) => ExecuteForwardPass(data, context));
                }
            }

            private static HSRSceneController ResolveSceneController(Camera camera)
            {
                return camera != null
                    ? HSRSceneController.FindForSceneOrActiveScene(camera.gameObject.scene)
                    : HSRSceneController.FindForSceneOrActiveScene(default);
            }
        }
    }
}
