using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HoyoToon.Runtime.Scene;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;

namespace HoyoToon.Rendering.HSR
{
    public class LightingGBuffer : ScriptableRendererFeature
    {
        [SerializeField] LightingGBufferSettings settings;
        LightingGBufferPass m_GBufferStagePass;
        LightingGBufferPass m_ForwardStagePass;

        TextureHandle m_SharedGBufferA;
        TextureHandle m_SharedDepthBufferOrCopy;
        bool m_HasSharedForwardInputs;
        bool m_HasWarnedForwardOrder;

        /// <inheritdoc/>
        public override void Create()
        {
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
            m_HasSharedForwardInputs = false;

            if (!m_HasWarnedForwardOrder && settings.forwardRenderPassEvent < settings.renderPassEvent)
            {
                Debug.LogWarning("LightingGBuffer: forwardRenderPassEvent is earlier than renderPassEvent. Forward stage may execute before GBuffer outputs are produced.");
                m_HasWarnedForwardOrder = true;
            }

            renderer.EnqueuePass(m_GBufferStagePass);
            renderer.EnqueuePass(m_ForwardStagePass);
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

            static readonly int k_CrpPassMiscBufferId = Shader.PropertyToID("CRP_PassMisc_CRPBuildin");
            static readonly int k_RpgEnvPerMainCameraBufferId = Shader.PropertyToID("RPGEnv_PerMainCamera");

            static readonly int k_CascadeShadowSplitSpheres0Id = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
            static readonly int k_CascadeShadowSplitSpheres1Id = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            static readonly int k_CascadeShadowSplitSpheres2Id = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            static readonly int k_CascadeShadowSplitSpheres3Id = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
            static readonly int k_CascadeShadowSplitSphereRadiiId = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
            static readonly int k_MainLightShadowParamsId = Shader.PropertyToID("_MainLightShadowParams");
            static readonly int k_MainLightShadowmapSizeId = Shader.PropertyToID("_MainLightShadowmapSize");
            static readonly int k_MainLightShadowCascadeCountId = Shader.PropertyToID("_MainLightShadowCascadeCount");
            static readonly int k_MainLightWorldToShadowId = Shader.PropertyToID("_MainLightWorldToShadow");

            static readonly ShaderTagId k_LightingGBufferTag = new ShaderTagId("LightingGBuffer");
            static readonly ShaderTagId k_LightingGBufferEyeHairTag = new ShaderTagId("LightingGBufferEyeHair");
            static readonly ShaderTagId k_ForwardEmissionTag = new ShaderTagId("ForwardEmission");
            static readonly ShaderTagId k_CustomForwardTag = new ShaderTagId("CustomForward");
            static readonly ShaderTagId k_CustomForward2Tag = new ShaderTagId("CustomForward2");
            static readonly ShaderTagId k_RpgOutlineTag = new ShaderTagId("RPGOutline");
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
            static readonly List<ShaderTagId> k_OutlinePassTag = new List<ShaderTagId>
        {
            k_RpgOutlineTag
        };
            static readonly SortingCriteria k_QueueDrivenSortFlags =
                SortingCriteria.SortingLayer |
                SortingCriteria.RenderQueue |
                SortingCriteria.CanvasOrder |
                SortingCriteria.OptimizeStateChanges;

            [StructLayout(LayoutKind.Sequential)]
            struct CrpPassMiscData
            {
                public Vector4 _CascadeShadowSplitSpheres0;
                public Vector4 _CascadeShadowSplitSpheres1;
                public Vector4 _CascadeShadowSplitSpheres2;
                public Vector4 _CascadeShadowSplitSpheres3;
                public Vector4 _CascadeShadowSplitSphereRadii;
                public Vector4 _MainLightShadowParams;
                public Vector4 _MainLightShadowmapSize;
                public float _MainLightShadowCascadeCount;
                public Vector3 _PadShadowCB;
                public Matrix4x4 _MainLightWorldToShadowArr0;
                public Matrix4x4 _MainLightWorldToShadowArr1;
                public Matrix4x4 _MainLightWorldToShadowArr2;
                public Matrix4x4 _MainLightWorldToShadowArr3;
                public Matrix4x4 _MainLightWorldToShadowArr4;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct RpgEnvPerMainCameraData
            {
                public float _GlobalOneMinusAvatarIntensity;
                public Vector3 _XPad0;
                public Vector3 _ES_MonsterLightDir;
                public float _ES_Indoor;
                public float _ES_TransitionRate;
                public float _ES_SelfShadowLerpHair;
                public float _ES_LEVEL_ADJUST_ON;
                public float _XPad1;
                public Matrix4x4 _ES_GlobalRotMatrix;
                public float _ES_CharacterToonRampMode;
                public float _ES_CharacterDisableLocalMainLight;
                public Vector2 _XPad2;
                public Vector4 _ES_AddColor;
                public Vector4 _ES_SPColor;
                public float _ES_SPIntensity;
                public Vector3 _XPad3;
                public Vector4 _ES_RimShadowColor;
                public float _ES_RimShadowIntensity;
                public float _ES_CharacterShadowFactor;
                public float _ES_OutLineDarkenVal;
                public float _ES_OutLineLightedVal;
                public float _ES_OutlineDisableDistanceScale;
                public float _ES_OutlineFallbackScale;
                public float _ES_HeightLerpTop;
                public float _ES_HeightLerpBottom;
                public Vector4 _ES_HeightLerpTopColor;
                public Vector4 _ES_HeightLerpMiddleColor;
                public Vector4 _ES_HeightLerpBottomColor;
                public Vector2 _ES_RimLightOffset;
                public float _ES_RimLightWidth;
                public float _ES_RimLightIntensity;
                public float _ES_RimLightAddMode;
                public float _ES_RimLightMode;
                public Vector2 _XPad4;
                public Vector4 _ES_RimLightColor;
                public Vector4 _ES_LevelSkinLightColor;
                public Vector4 _ES_LevelSkinShadowColor;
                public Vector4 _ES_LevelHighLightColor;
                public Vector4 _ES_LevelShadowColor;
                public float _ES_LevelShadow;
                public float _ES_LevelMid;
                public float _ES_LevelHighLight;
                public float _ES_LevelEyeShadowIntensity;
                public float _ES_IndoorCharShadowAsCookie;
                public float _ES_FogColor;
                public float _ES_FogDensity;
                public float _ES_FogNear;
                public float _ES_FogFar;
                public float _ES_HeightFogColor;
                public float _ES_HeightFogBaseHeight;
                public float _ES_HeightFogRange;
                public float _ES_HeightFogDensity;
                public float _ES_HeightFogFogNear;
                public float _ES_HeightFogFogFar;
                public float _ES_FogCharacterNearFactor;
                public float _ES_HeightFogAddAjust;
                public float _ES_DisableFogTransition;
                public Vector2 _XPad5;
                public Vector4 _ES_EffCustomLightPosition;
                public float _OutlineScale;
            }

            public LightingGBufferPass(LightingGBuffer owner, StageType stageType)
            {
                m_Owner = owner;
                m_StageType = stageType;
            }

            // This class stores the data needed by the RenderGraph pass.
            // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
            private class GBufferPassData
            {
                public RendererListHandle lightingGBuffer;
                public RendererListHandle rpgOutline;
            }

            private class ForwardPassData
            {
                public RendererListHandle forwardEmission;
                public RendererListHandle customForward;
                public RendererListHandle customForward2;
                public TextureHandle gBufferA;
                public TextureHandle depthBufferOrCopy;
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

            static RenderTextureDescriptor BuildSingleSampleColorDescriptor(RenderTextureDescriptor baseDescriptor, GraphicsFormat format)
            {
                RenderTextureDescriptor descriptor = BuildColorDescriptor(baseDescriptor, format);
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
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

            static void PushPassMiscCBuffer(RasterCommandBuffer cmd)
            {
                float cascadeCount = Shader.GetGlobalFloat(k_MainLightShadowCascadeCountId);
                Matrix4x4[] sourceShadowMatrices = Shader.GetGlobalMatrixArray(k_MainLightWorldToShadowId);
                Matrix4x4 shadow0 = Matrix4x4.identity;
                Matrix4x4 shadow1 = Matrix4x4.identity;
                Matrix4x4 shadow2 = Matrix4x4.identity;
                Matrix4x4 shadow3 = Matrix4x4.identity;
                Matrix4x4 shadow4 = Matrix4x4.identity;

                if (sourceShadowMatrices != null && sourceShadowMatrices.Length > 0)
                {
                    if (sourceShadowMatrices.Length > 0) shadow0 = sourceShadowMatrices[0];
                    if (sourceShadowMatrices.Length > 1) shadow1 = sourceShadowMatrices[1];
                    if (sourceShadowMatrices.Length > 2) shadow2 = sourceShadowMatrices[2];
                    if (sourceShadowMatrices.Length > 3) shadow3 = sourceShadowMatrices[3];
                    if (sourceShadowMatrices.Length > 4) shadow4 = sourceShadowMatrices[4];

                    if (cascadeCount <= 0f)
                        cascadeCount = Mathf.Min(4, sourceShadowMatrices.Length);
                }
                else if (cascadeCount <= 0f)
                {
                    cascadeCount = 1f;
                }

                CrpPassMiscData passMiscData = new CrpPassMiscData
                {
                    _CascadeShadowSplitSpheres0 = Shader.GetGlobalVector(k_CascadeShadowSplitSpheres0Id),
                    _CascadeShadowSplitSpheres1 = Shader.GetGlobalVector(k_CascadeShadowSplitSpheres1Id),
                    _CascadeShadowSplitSpheres2 = Shader.GetGlobalVector(k_CascadeShadowSplitSpheres2Id),
                    _CascadeShadowSplitSpheres3 = Shader.GetGlobalVector(k_CascadeShadowSplitSpheres3Id),
                    _CascadeShadowSplitSphereRadii = Shader.GetGlobalVector(k_CascadeShadowSplitSphereRadiiId),
                    _MainLightShadowParams = Shader.GetGlobalVector(k_MainLightShadowParamsId),
                    _MainLightShadowmapSize = Shader.GetGlobalVector(k_MainLightShadowmapSizeId),
                    _MainLightShadowCascadeCount = cascadeCount,
                    _PadShadowCB = Vector3.zero,
                    _MainLightWorldToShadowArr0 = shadow0,
                    _MainLightWorldToShadowArr1 = shadow1,
                    _MainLightWorldToShadowArr2 = shadow2,
                    _MainLightWorldToShadowArr3 = shadow3,
                    _MainLightWorldToShadowArr4 = shadow4
                };

                ConstantBuffer.PushGlobal(cmd, passMiscData, k_CrpPassMiscBufferId);
            }

            static RpgEnvPerMainCameraData BuildEnvironmentState(HSRSceneController env)
            {
                RpgEnvPerMainCameraData envData = new RpgEnvPerMainCameraData();
                if (env == null)
                    return envData;

                envData._GlobalOneMinusAvatarIntensity = env._GlobalOneMinusAvatarIntensity;
                envData._ES_MonsterLightDir = env._ES_MonsterLightDir;
                envData._ES_Indoor = env._ES_Indoor ? 1f : 0f;
                envData._ES_TransitionRate = env._ES_TransitionRate;
                envData._ES_SelfShadowLerpHair = env._ES_SelfShadowLerpHair;
                envData._ES_LEVEL_ADJUST_ON = env._ES_LEVEL_ADJUST_ON ? 1f : 0f;
                envData._ES_GlobalRotMatrix = env.GetGlobalRotMatrix();
                envData._ES_CharacterToonRampMode = env._ES_CharacterToonRampMode;
                envData._ES_CharacterDisableLocalMainLight = env._ES_CharacterDisableLocalMainLight ? 1f : 0f;
                envData._ES_AddColor = env._ES_AddColor;
                envData._ES_SPColor = env._ES_SPColor;
                envData._ES_SPIntensity = env._ES_SPIntensity;
                envData._ES_RimShadowColor = env._ES_RimShadowColor;
                envData._ES_RimShadowIntensity = env._ES_RimShadowIntensity;
                envData._ES_CharacterShadowFactor = env._ES_CharacterShadowFactor;
                envData._ES_OutLineDarkenVal = env._ES_OutLineDarkenVal;
                envData._ES_OutLineLightedVal = env._ES_OutLineLightedVal;
                envData._ES_OutlineDisableDistanceScale = env._ES_OutlineDisableDistanceScale;
                envData._ES_OutlineFallbackScale = env._ES_OutlineFallbackScale;
                envData._ES_HeightLerpTop = env._ES_HeightLerpTop;
                envData._ES_HeightLerpBottom = env._ES_HeightLerpBottom;
                envData._ES_HeightLerpTopColor = env._ES_HeightLerpTopColor;
                envData._ES_HeightLerpMiddleColor = env._ES_HeightLerpMiddleColor;
                envData._ES_HeightLerpBottomColor = env._ES_HeightLerpBottomColor;
                envData._ES_RimLightOffset = env._ES_RimLightOffset;
                envData._ES_RimLightWidth = env._ES_RimLightWidth;
                envData._ES_RimLightIntensity = env._ES_RimLightIntensity;
                envData._ES_RimLightAddMode = env._ES_RimLightAddMode;
                envData._ES_RimLightMode = env._ES_RimLightMode;
                envData._ES_RimLightColor = env._ES_RimLightColor;
                envData._ES_LevelSkinLightColor = env._ES_LevelSkinLightColor;
                envData._ES_LevelSkinShadowColor = env._ES_LevelSkinShadowColor;
                envData._ES_LevelHighLightColor = env._ES_LevelHighLightColor;
                envData._ES_LevelShadowColor = env._ES_LevelShadowColor;
                envData._ES_LevelShadow = env._ES_LevelShadow;
                envData._ES_LevelMid = env._ES_LevelMid;
                envData._ES_LevelHighLight = env._ES_LevelHighLight;
                envData._ES_LevelEyeShadowIntensity = env._ES_LevelEyeShadowIntensity;
                envData._ES_IndoorCharShadowAsCookie = env._ES_IndoorCharShadowAsCookie ? 1f : 0f;
                envData._ES_FogColor = env._ES_FogColor;
                envData._ES_FogDensity = env._ES_FogDensity;
                envData._ES_FogNear = env._ES_FogNear;
                envData._ES_FogFar = env._ES_FogFar;
                envData._ES_HeightFogColor = env._ES_HeightFogColor;
                envData._ES_HeightFogBaseHeight = env._ES_HeightFogBaseHeight;
                envData._ES_HeightFogRange = env._ES_HeightFogRange;
                envData._ES_HeightFogDensity = env._ES_HeightFogDensity;
                envData._ES_HeightFogFogNear = env._ES_HeightFogFogNear;
                envData._ES_HeightFogFogFar = env._ES_HeightFogFogFar;
                envData._ES_FogCharacterNearFactor = env._ES_FogCharacterNearFactor;
                envData._ES_HeightFogAddAjust = env._ES_HeightFogAddAjust;
                envData._ES_DisableFogTransition = env._ES_DisableFogTransition ? 1f : 0f;
                envData._ES_EffCustomLightPosition = env._ES_EffCustomLightPosition;
                envData._OutlineScale = env._OutlineScale;
                return envData;
            }

            static void PushRpgEnvPerMainCameraCBuffer(RasterCommandBuffer cmd)
            {
                RpgEnvPerMainCameraData envData = BuildEnvironmentState(HSRSceneController.instance);
                ConstantBuffer.PushGlobal(cmd, envData, k_RpgEnvPerMainCameraBufferId);
            }

            // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
            // It is used to execute draw commands.
            static void ExecuteGBufferPass(GBufferPassData data, RasterGraphContext context)
            {
                PushPassMiscCBuffer(context.cmd);
                PushRpgEnvPerMainCameraCBuffer(context.cmd);

                // Keep existing camera color so SV_Target0 behaves like non-MRT default output.
                context.cmd.ClearRenderTarget(false, false, Color.clear);
                context.cmd.DrawRendererList(data.lightingGBuffer);

                // Outline must run as the final step of Lighting GBuffer.
                context.cmd.DrawRendererList(data.rpgOutline);
            }

            static void ExecuteForwardPass(ForwardPassData data, RasterGraphContext context)
            {
                PushPassMiscCBuffer(context.cmd);
                PushRpgEnvPerMainCameraCBuffer(context.cmd);
                context.cmd.SetGlobalTexture(k_GBufferAId, data.gBufferA);
                context.cmd.SetGlobalTexture(k_DepthBufferOrCopyId, data.depthBufferOrCopy, RenderTextureSubElement.Depth);

                // Forward group order: ForwardEmission first, then CustomForward, then CustomForward2.
                context.cmd.DrawRendererList(data.forwardEmission);
                context.cmd.DrawRendererList(data.customForward);
                context.cmd.DrawRendererList(data.customForward2);
            }

            static void ExecuteDepthRebuildPass(DepthRebuildPassData data, RasterGraphContext context)
            {
                // Rebuild depth exclusively from LightingGBuffer-tagged renderers.
                context.cmd.ClearRenderTarget(true, true, Color.clear);
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

                RendererListHandle CreateRendererList(List<ShaderTagId> shaderTagIds, RenderQueueRange renderQueueRange, SortingCriteria sortingCriteria)
                {
                    DrawingSettings drawingSettings = CreateDrawingSettings(shaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
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

                    RenderTextureDescriptor gBufferADescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, gBufferAFormat);
                    RenderTextureDescriptor gBufferBDescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, gBufferBFormat);
                    RenderTextureDescriptor gBufferCDescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, gBufferCFormat);
                    RenderTextureDescriptor depthRebuildDummyADescriptor = BuildSingleSampleColorDescriptor(cameraData.cameraTargetDescriptor, gBufferAFormat);
                    RenderTextureDescriptor depthRebuildDummyBDescriptor = BuildSingleSampleColorDescriptor(cameraData.cameraTargetDescriptor, gBufferBFormat);
                    RenderTextureDescriptor depthRebuildDummyCDescriptor = BuildSingleSampleColorDescriptor(cameraData.cameraTargetDescriptor, gBufferCFormat);
                    RenderTextureDescriptor depthCopyDescriptor = BuildDepthCopyDescriptor(cameraData.cameraTargetDescriptor);

                    TextureHandle gBufferA = UniversalRenderer.CreateRenderGraphTexture(renderGraph, gBufferADescriptor, "_GBufferA", false);
                    TextureHandle gBufferB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, gBufferBDescriptor, "_GBufferB", false);
                    TextureHandle gBufferC = UniversalRenderer.CreateRenderGraphTexture(renderGraph, gBufferCDescriptor, "_GBufferC", false);
                    TextureHandle depthBufferOrCopy = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthCopyDescriptor, "_DepthBufferOrCopy", false);
                    TextureHandle depthRebuildDummyA = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthRebuildDummyADescriptor, "_LightingGBufferDepthDummyA", false);
                    TextureHandle depthRebuildDummyB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthRebuildDummyBDescriptor, "_LightingGBufferDepthDummyB", false);
                    TextureHandle depthRebuildDummyC = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthRebuildDummyCDescriptor, "_LightingGBufferDepthDummyC", false);

                    RendererListHandle lightingGBuffer = CreateRendererList(k_GBufferPassTag, RenderQueueRange.all, k_QueueDrivenSortFlags);
                    RendererListHandle lightingGBufferDepthOnly = CreateRendererList(k_GBufferDepthRebuildTag, RenderQueueRange.all, k_QueueDrivenSortFlags);
                    RendererListHandle rpgOutline = CreateRendererList(k_OutlinePassTag, RenderQueueRange.all, k_QueueDrivenSortFlags);

                    m_Owner.m_SharedGBufferA = gBufferA;
                    m_Owner.m_SharedDepthBufferOrCopy = depthBufferOrCopy;
                    m_Owner.m_HasSharedForwardInputs = true;

                    // This pass writes all GBuffer MRTs.
                    using (var builder = renderGraph.AddRasterRenderPass<GBufferPassData>(gBufferPassName, out var passData))
                    {
                        passData.lightingGBuffer = lightingGBuffer;
                        passData.rpgOutline = rpgOutline;

                        builder.UseRendererList(passData.lightingGBuffer);
                        builder.UseRendererList(passData.rpgOutline);

                        // Bind MRTs so SV_Target0/1/2 from shader can be written to GBuffer textures.
                        builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                        builder.SetRenderAttachment(gBufferB, 1);
                        builder.SetRenderAttachment(gBufferC, 2);
                        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                        builder.SetGlobalTextureAfterPass(gBufferB, k_GBufferBId);
                        builder.SetGlobalTextureAfterPass(gBufferC, k_GBufferCId);
                        builder.AllowPassCulling(false);

                        builder.SetRenderFunc((GBufferPassData data, RasterGraphContext context) => ExecuteGBufferPass(data, context));
                    }

                    // Copy camera color result from SV_Target0 into _GBufferA for downstream forward sampling.
                    renderGraph.AddBlitPass(resourceData.activeColorTexture, gBufferA, Vector2.one, Vector2.zero, passName: gBufferACopyPassName);

                    // Keep this as its own pass so it appears as a distinct depth rebuild step after Lighting GBuffer.
                    using (var builder = renderGraph.AddRasterRenderPass<DepthRebuildPassData>(gBufferDepthRebuildPassName, out var passData))
                    {
                        passData.lightingGBufferDepthOnly = lightingGBufferDepthOnly;

                        builder.UseRendererList(passData.lightingGBufferDepthOnly);
                        builder.SetRenderAttachment(depthRebuildDummyA, 0);
                        builder.SetRenderAttachment(depthRebuildDummyB, 1);
                        builder.SetRenderAttachment(depthRebuildDummyC, 2);
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

                // Forward group pass that can sample _GBufferA from the GBuffer stage.
                using (var builder = renderGraph.AddRasterRenderPass<ForwardPassData>(forwardPassName, out var passData))
                {
                    passData.forwardEmission = forwardEmission;
                    passData.customForward = customForward;
                    passData.customForward2 = customForward2;
                    passData.gBufferA = m_Owner.m_SharedGBufferA;
                    passData.depthBufferOrCopy = m_Owner.m_SharedDepthBufferOrCopy;

                    builder.UseRendererList(passData.forwardEmission);
                    builder.UseRendererList(passData.customForward);
                    builder.UseRendererList(passData.customForward2);

                    // ForwardEmission samples _GBufferA and _DepthBufferOrCopy, so declare explicit read dependencies.
                    builder.UseTexture(passData.gBufferA, AccessFlags.Read);
                    builder.UseTexture(passData.depthBufferOrCopy, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((ForwardPassData data, RasterGraphContext context) => ExecuteForwardPass(data, context));
                }
            }
        }
    }
}
