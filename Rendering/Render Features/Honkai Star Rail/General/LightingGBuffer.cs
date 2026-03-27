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
        internal static TextureHandle SharedAlphaMaskHandle;

        [SerializeField] LightingGBufferSettings settings;
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
            SharedAlphaMaskHandle = default;

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
            static readonly int k_LightingAlphaMaskId = Shader.PropertyToID("_LightingAlphaMask");

            static readonly int k_CascadeShadowSplitSpheres0Id = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
            static readonly int k_CascadeShadowSplitSpheres1Id = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            static readonly int k_CascadeShadowSplitSpheres2Id = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            static readonly int k_CascadeShadowSplitSpheres3Id = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
            static readonly int k_CascadeShadowSplitSphereRadiiId = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");
            static readonly int k_MainLightShadowParamsId = Shader.PropertyToID("_MainLightShadowParams");
            static readonly int k_MainLightShadowmapSizeId = Shader.PropertyToID("_MainLightShadowmapSize");
            static readonly int k_MainLightShadowCascadeCountId = Shader.PropertyToID("_MainLightShadowCascadeCount");
            static readonly int k_MainLightWorldToShadowId = Shader.PropertyToID("_MainLightWorldToShadow");
            static readonly int k_MainLightWorldToShadowArrId = Shader.PropertyToID("_MainLightWorldToShadowArr");
            static readonly int k_EsGlobalRotMatrixId = Shader.PropertyToID("_ES_GlobalRotMatrix");

            static readonly ShaderTagId k_LightingGBufferTag = new ShaderTagId("LightingGBuffer");
            static readonly ShaderTagId k_LightingGBufferEyeHairTag = new ShaderTagId("LightingGBufferEyeHair");
            static readonly ShaderTagId k_LightingForwardTag = new ShaderTagId("LightingForward");
            static readonly ShaderTagId k_ForwardEmissionTag = new ShaderTagId("ForwardEmission");
            static readonly ShaderTagId k_CustomForwardTag = new ShaderTagId("CustomForward");
            static readonly ShaderTagId k_CustomForward2Tag = new ShaderTagId("CustomForward2");
            static readonly ShaderTagId k_CustomRpTransparentTag = new ShaderTagId("CustomRPTransparent");
            static readonly ShaderTagId k_RpgOutlineTag = new ShaderTagId("RPGOutline");
            static readonly ShaderTagId k_UniversalForwardTag = new ShaderTagId("UniversalForward");
            static readonly ShaderTagId k_UniversalForwardOnlyTag = new ShaderTagId("UniversalForwardOnly");
            static readonly ShaderTagId k_SrpDefaultUnlitTag = new ShaderTagId("SRPDefaultUnlit");
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
            static readonly List<ShaderTagId> k_AlphaMaskPassTags = new List<ShaderTagId>
        {
            k_LightingGBufferTag,
            k_LightingGBufferEyeHairTag,
            k_LightingForwardTag,
            k_ForwardEmissionTag,
            k_CustomForwardTag,
            k_CustomForward2Tag,
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
            static readonly Matrix4x4[] k_MainLightWorldToShadowScratch = new Matrix4x4[5];
            static readonly Vector4[] k_EsGlobalRotMatrixScratch = new Vector4[4];
            static Material s_AlphaMaskOverrideMaterial;
            static readonly Color k_TransparentClearColor = new Color(0f, 0f, 0f, 0f);

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
                public bool clearColorTarget;
            }

            private class ForwardPassData
            {
                public RendererListHandle forwardEmission;
                public RendererListHandle customForward;
                public RendererListHandle customForward2;
                public TextureHandle gBufferA;
                public TextureHandle depthBufferOrCopy;
                public TextureHandle alphaMask;
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

            static CrpPassMiscData BuildPassMiscData()
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

                return new CrpPassMiscData
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
            }

            static void ApplyPassMiscGlobals(in CrpPassMiscData passMiscData)
            {
                k_MainLightWorldToShadowScratch[0] = passMiscData._MainLightWorldToShadowArr0;
                k_MainLightWorldToShadowScratch[1] = passMiscData._MainLightWorldToShadowArr1;
                k_MainLightWorldToShadowScratch[2] = passMiscData._MainLightWorldToShadowArr2;
                k_MainLightWorldToShadowScratch[3] = passMiscData._MainLightWorldToShadowArr3;
                k_MainLightWorldToShadowScratch[4] = passMiscData._MainLightWorldToShadowArr4;

                Shader.SetGlobalVector(k_CascadeShadowSplitSpheres0Id, passMiscData._CascadeShadowSplitSpheres0);
                Shader.SetGlobalVector(k_CascadeShadowSplitSpheres1Id, passMiscData._CascadeShadowSplitSpheres1);
                Shader.SetGlobalVector(k_CascadeShadowSplitSpheres2Id, passMiscData._CascadeShadowSplitSpheres2);
                Shader.SetGlobalVector(k_CascadeShadowSplitSpheres3Id, passMiscData._CascadeShadowSplitSpheres3);
                Shader.SetGlobalVector(k_CascadeShadowSplitSphereRadiiId, passMiscData._CascadeShadowSplitSphereRadii);
                Shader.SetGlobalVector(k_MainLightShadowParamsId, passMiscData._MainLightShadowParams);
                Shader.SetGlobalVector(k_MainLightShadowmapSizeId, passMiscData._MainLightShadowmapSize);
                Shader.SetGlobalFloat(k_MainLightShadowCascadeCountId, passMiscData._MainLightShadowCascadeCount);
                Shader.SetGlobalMatrixArray(k_MainLightWorldToShadowArrId, k_MainLightWorldToShadowScratch);
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

            static void ApplyRpgEnvPerMainCameraGlobals(in RpgEnvPerMainCameraData envData)
            {
                Matrix4x4 globalRotMatrix = envData._ES_GlobalRotMatrix;
                k_EsGlobalRotMatrixScratch[0] = globalRotMatrix.GetRow(0);
                k_EsGlobalRotMatrixScratch[1] = globalRotMatrix.GetRow(1);
                k_EsGlobalRotMatrixScratch[2] = globalRotMatrix.GetRow(2);
                k_EsGlobalRotMatrixScratch[3] = globalRotMatrix.GetRow(3);

                Shader.SetGlobalFloat("_GlobalOneMinusAvatarIntensity", envData._GlobalOneMinusAvatarIntensity);
                Shader.SetGlobalVector("_XPad0", envData._XPad0);
                Shader.SetGlobalVector("_ES_MonsterLightDir", envData._ES_MonsterLightDir);
                Shader.SetGlobalFloat("_ES_Indoor", envData._ES_Indoor);
                Shader.SetGlobalFloat("_ES_TransitionRate", envData._ES_TransitionRate);
                Shader.SetGlobalFloat("_ES_SelfShadowLerpHair", envData._ES_SelfShadowLerpHair);
                Shader.SetGlobalFloat("_ES_LEVEL_ADJUST_ON", envData._ES_LEVEL_ADJUST_ON);
                Shader.SetGlobalFloat("_XPad1", envData._XPad1);
                Shader.SetGlobalVectorArray(k_EsGlobalRotMatrixId, k_EsGlobalRotMatrixScratch);
                Shader.SetGlobalFloat("_ES_CharacterToonRampMode", envData._ES_CharacterToonRampMode);
                Shader.SetGlobalFloat("_ES_CharacterDisableLocalMainLight", envData._ES_CharacterDisableLocalMainLight);
                Shader.SetGlobalVector("_XPad2", envData._XPad2);
                Shader.SetGlobalVector("_ES_AddColor", envData._ES_AddColor);
                Shader.SetGlobalVector("_ES_SPColor", envData._ES_SPColor);
                Shader.SetGlobalFloat("_ES_SPIntensity", envData._ES_SPIntensity);
                Shader.SetGlobalVector("_XPad3", envData._XPad3);
                Shader.SetGlobalVector("_ES_RimShadowColor", envData._ES_RimShadowColor);
                Shader.SetGlobalFloat("_ES_RimShadowIntensity", envData._ES_RimShadowIntensity);
                Shader.SetGlobalFloat("_ES_CharacterShadowFactor", envData._ES_CharacterShadowFactor);
                Shader.SetGlobalFloat("_ES_OutLineDarkenVal", envData._ES_OutLineDarkenVal);
                Shader.SetGlobalFloat("_ES_OutLineLightedVal", envData._ES_OutLineLightedVal);
                Shader.SetGlobalFloat("_ES_OutlineDisableDistanceScale", envData._ES_OutlineDisableDistanceScale);
                Shader.SetGlobalFloat("_ES_OutlineFallbackScale", envData._ES_OutlineFallbackScale);
                Shader.SetGlobalFloat("_ES_HeightLerpTop", envData._ES_HeightLerpTop);
                Shader.SetGlobalFloat("_ES_HeightLerpBottom", envData._ES_HeightLerpBottom);
                Shader.SetGlobalVector("_ES_HeightLerpTopColor", envData._ES_HeightLerpTopColor);
                Shader.SetGlobalVector("_ES_HeightLerpMiddleColor", envData._ES_HeightLerpMiddleColor);
                Shader.SetGlobalVector("_ES_HeightLerpBottomColor", envData._ES_HeightLerpBottomColor);
                Shader.SetGlobalVector("_ES_RimLightOffset", envData._ES_RimLightOffset);
                Shader.SetGlobalFloat("_ES_RimLightWidth", envData._ES_RimLightWidth);
                Shader.SetGlobalFloat("_ES_RimLightIntensity", envData._ES_RimLightIntensity);
                Shader.SetGlobalFloat("_ES_RimLightAddMode", envData._ES_RimLightAddMode);
                Shader.SetGlobalFloat("_ES_RimLightMode", envData._ES_RimLightMode);
                Shader.SetGlobalVector("_XPad4", envData._XPad4);
                Shader.SetGlobalVector("_ES_RimLightColor", envData._ES_RimLightColor);
                Shader.SetGlobalVector("_ES_LevelSkinLightColor", envData._ES_LevelSkinLightColor);
                Shader.SetGlobalVector("_ES_LevelSkinShadowColor", envData._ES_LevelSkinShadowColor);
                Shader.SetGlobalVector("_ES_LevelHighLightColor", envData._ES_LevelHighLightColor);
                Shader.SetGlobalVector("_ES_LevelShadowColor", envData._ES_LevelShadowColor);
                Shader.SetGlobalFloat("_ES_LevelShadow", envData._ES_LevelShadow);
                Shader.SetGlobalFloat("_ES_LevelMid", envData._ES_LevelMid);
                Shader.SetGlobalFloat("_ES_LevelHighLight", envData._ES_LevelHighLight);
                Shader.SetGlobalFloat("_ES_LevelEyeShadowIntensity", envData._ES_LevelEyeShadowIntensity);
                Shader.SetGlobalFloat("_ES_IndoorCharShadowAsCookie", envData._ES_IndoorCharShadowAsCookie);
                Shader.SetGlobalFloat("_ES_FogColor", envData._ES_FogColor);
                Shader.SetGlobalFloat("_ES_FogDensity", envData._ES_FogDensity);
                Shader.SetGlobalFloat("_ES_FogNear", envData._ES_FogNear);
                Shader.SetGlobalFloat("_ES_FogFar", envData._ES_FogFar);
                Shader.SetGlobalFloat("_ES_HeightFogColor", envData._ES_HeightFogColor);
                Shader.SetGlobalFloat("_ES_HeightFogBaseHeight", envData._ES_HeightFogBaseHeight);
                Shader.SetGlobalFloat("_ES_HeightFogRange", envData._ES_HeightFogRange);
                Shader.SetGlobalFloat("_ES_HeightFogDensity", envData._ES_HeightFogDensity);
                Shader.SetGlobalFloat("_ES_HeightFogFogNear", envData._ES_HeightFogFogNear);
                Shader.SetGlobalFloat("_ES_HeightFogFogFar", envData._ES_HeightFogFogFar);
                Shader.SetGlobalFloat("_ES_FogCharacterNearFactor", envData._ES_FogCharacterNearFactor);
                Shader.SetGlobalFloat("_ES_HeightFogAddAjust", envData._ES_HeightFogAddAjust);
                Shader.SetGlobalFloat("_ES_DisableFogTransition", envData._ES_DisableFogTransition);
                Shader.SetGlobalVector("_XPad5", envData._XPad5);
                Shader.SetGlobalVector("_ES_EffCustomLightPosition", envData._ES_EffCustomLightPosition);
                Shader.SetGlobalFloat("_OutlineScale", envData._OutlineScale);
            }

            // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
            // It is used to execute draw commands.
            static void ExecuteGBufferPass(GBufferPassData data, RasterGraphContext context)
            {
                // Let Unity-owned preview/reflection cameras keep their own clear color.
                if (data.clearColorTarget)
                {
                    context.cmd.ClearRenderTarget(false, true, k_TransparentClearColor);
                }
                context.cmd.DrawRendererList(data.lightingGBuffer);

                // Outline must run as the final step of Lighting GBuffer.
                context.cmd.DrawRendererList(data.rpgOutline);
            }

            static void ExecuteForwardPass(ForwardPassData data, RasterGraphContext context)
            {
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
                    bool isPreviewOrReflectionCamera = cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection;

                    ApplyPassMiscGlobals(BuildPassMiscData());
                    ApplyRpgEnvPerMainCameraGlobals(BuildEnvironmentState(HSRSceneController.instance));

                    GraphicsFormat gBufferAFormat = GetSupportedColorFormat(GraphicsFormat.R8G8B8A8_UNorm, cameraData.cameraTargetDescriptor.graphicsFormat);
                    GraphicsFormat gBufferBFormat = GetSupportedColorFormat(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.R16G16B16A16_UNorm);
                    GraphicsFormat gBufferCFormat = GetSupportedColorFormat(GraphicsFormat.R8_UNorm, GraphicsFormat.R8G8B8A8_UNorm);
                    GraphicsFormat alphaMaskFormat = GetSupportedColorFormat(GraphicsFormat.R8_UNorm, GraphicsFormat.R8G8B8A8_UNorm);

                    RenderTextureDescriptor gBufferADescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, gBufferAFormat);
                    RenderTextureDescriptor gBufferBDescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, gBufferBFormat);
                    RenderTextureDescriptor gBufferCDescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, gBufferCFormat);
                    RenderTextureDescriptor alphaMaskDescriptor = BuildColorDescriptor(cameraData.cameraTargetDescriptor, alphaMaskFormat);
                    RenderTextureDescriptor depthRebuildDummyADescriptor = BuildSingleSampleColorDescriptor(cameraData.cameraTargetDescriptor, gBufferAFormat);
                    RenderTextureDescriptor depthRebuildDummyBDescriptor = BuildSingleSampleColorDescriptor(cameraData.cameraTargetDescriptor, gBufferBFormat);
                    RenderTextureDescriptor depthRebuildDummyCDescriptor = BuildSingleSampleColorDescriptor(cameraData.cameraTargetDescriptor, gBufferCFormat);
                    RenderTextureDescriptor depthCopyDescriptor = BuildDepthCopyDescriptor(cameraData.cameraTargetDescriptor);

                    TextureHandle gBufferA = UniversalRenderer.CreateRenderGraphTexture(renderGraph, gBufferADescriptor, "_GBufferA", false);
                    TextureHandle gBufferB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, gBufferBDescriptor, "_GBufferB", false);
                    TextureHandle gBufferC = UniversalRenderer.CreateRenderGraphTexture(renderGraph, gBufferCDescriptor, "_GBufferC", false);
                    TextureHandle alphaMask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, alphaMaskDescriptor, "_LightingAlphaMask", false);
                    TextureHandle depthBufferOrCopy = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthCopyDescriptor, "_DepthBufferOrCopy", false);
                    TextureHandle depthRebuildDummyA = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthRebuildDummyADescriptor, "_LightingGBufferDepthDummyA", false);
                    TextureHandle depthRebuildDummyB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthRebuildDummyBDescriptor, "_LightingGBufferDepthDummyB", false);
                    TextureHandle depthRebuildDummyC = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthRebuildDummyCDescriptor, "_LightingGBufferDepthDummyC", false);

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
                        passData.clearColorTarget = !isPreviewOrReflectionCamera;

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
                ApplyPassMiscGlobals(BuildPassMiscData());
                ApplyRpgEnvPerMainCameraGlobals(BuildEnvironmentState(HSRSceneController.instance));
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
                    passData.alphaMask = m_Owner.m_SharedAlphaMask;

                    builder.UseRendererList(passData.forwardEmission);
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
        }
    }
}
