using System;
using System.Collections.Generic;
using HoyoToon.Rendering.HSR;
using HoyoToon.Runtime.Character;
using HoyoToon.Runtime.Scene;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CustomRPTransparentBuffer : ScriptableRendererFeature
{
    [SerializeField] CustomRPTransparentBufferSettings settings;
    CustomRPTransparentBufferPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new CustomRPTransparentBufferPass(settings);

        // Configure this pass to run after LightingGBuffer forward by default.
        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;

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
        if (!CustomRPTransparentBufferPass.ShouldRenderForCamera(renderingData.cameraData.cameraType, renderingData.cameraData.isPreviewCamera))
            return;

        renderer.EnqueuePass(m_ScriptablePass);
    }

    // Use this class to pass around settings from the feature to the pass
    [Serializable]
    public class CustomRPTransparentBufferSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    class CustomRPTransparentBufferPass : ScriptableRenderPass
    {
        readonly CustomRPTransparentBufferSettings settings;
        readonly List<HSRCharacterController> m_Controllers = new List<HSRCharacterController>();
        readonly List<DrawItem> m_DrawItems = new List<DrawItem>();
        readonly HashSet<int> m_DrawItemKeys = new HashSet<int>();
        static readonly ShaderTagId s_LightModeTag = new ShaderTagId("LightMode");
        const string RequiredLightModeTag = "CustomRPTransparent";

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
        static readonly int k_LightingAlphaMaskId = Shader.PropertyToID("_LightingAlphaMask");
        static readonly Matrix4x4[] k_MainLightWorldToShadowScratch = new Matrix4x4[5];
        static readonly Vector4[] k_EsGlobalRotMatrixScratch = new Vector4[4];

        struct DrawItem
        {
            public Renderer Renderer;
            public Material Material;
            public int SubMeshIndex;
        }

        public CustomRPTransparentBufferPass(CustomRPTransparentBufferSettings settings)
        {
            this.settings = settings;
        }

        internal static bool ShouldRenderForCamera(CameraType cameraType, bool isPreviewCamera)
        {
            if (isPreviewCamera)
                return false;

            return cameraType == CameraType.Game || cameraType == CameraType.SceneView;
        }

        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        private class PassData
        {
            public DrawItem[] drawItems;
        }

        static bool HasRequiredLightModePass(Material material)
        {
            if (material == null)
                return false;

            Shader shader = material.shader;
            if (shader == null)
                return false;

            int passCount = shader.passCount;
            for (int passIndex = 0; passIndex < passCount; ++passIndex)
            {
                ShaderTagId tagValue = shader.FindPassTagValue(passIndex, s_LightModeTag);
                if (string.Equals(tagValue.name, RequiredLightModeTag, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static int GetRenderableSubMeshCount(Renderer renderer)
        {
            if (renderer == null)
                return 0;

            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                Mesh skinnedMesh = skinnedRenderer.sharedMesh;
                if (skinnedMesh != null)
                    return skinnedMesh.subMeshCount;
            }

            if (renderer is MeshRenderer)
            {
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    return meshFilter.sharedMesh.subMeshCount;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            return sharedMaterials != null ? sharedMaterials.Length : 0;
        }

        void BuildDrawItems()
        {
            m_DrawItems.Clear();
            m_DrawItemKeys.Clear();

            HSRCharacterController.GetActiveControllers(m_Controllers, forceRefresh: false);

            for (int controllerIndex = 0; controllerIndex < m_Controllers.Count; ++controllerIndex)
            {
                HSRCharacterController controller = m_Controllers[controllerIndex];
                if (controller == null)
                    continue;

                List<HSRCharacterController.EffectMaterialEntry> effectEntries = controller.EffectMaterials;

                Renderer[] scopedRenderers = controller.GetScopedRenderers();
                if (scopedRenderers == null || scopedRenderers.Length == 0)
                    continue;

                for (int rendererIndex = 0; rendererIndex < scopedRenderers.Length; ++rendererIndex)
                {
                    Renderer renderer = scopedRenderers[rendererIndex];
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        continue;

                    int subMeshCount = GetRenderableSubMeshCount(renderer);
                    if (subMeshCount <= 0)
                        continue;

                    Material[] slottedMaterials = renderer.sharedMaterials;
                    if (slottedMaterials == null || slottedMaterials.Length == 0)
                        continue;

                    int slottedCount = Mathf.Min(slottedMaterials.Length, subMeshCount);
                    for (int subMeshIndex = 0; subMeshIndex < slottedCount; ++subMeshIndex)
                    {
                        Material slottedMaterial = slottedMaterials[subMeshIndex];
                        if (!HasRequiredLightModePass(slottedMaterial))
                            continue;

                        TryAddDrawItem(renderer, slottedMaterial, subMeshIndex);
                    }
                }

                if (effectEntries == null || effectEntries.Count == 0)
                    continue;

                for (int entryIndex = 0; entryIndex < effectEntries.Count; ++entryIndex)
                {
                    Material effectMaterial = effectEntries[entryIndex].Material;
                    if (!HasRequiredLightModePass(effectMaterial))
                        continue;

                    for (int rendererIndex = 0; rendererIndex < scopedRenderers.Length; ++rendererIndex)
                    {
                        Renderer renderer = scopedRenderers[rendererIndex];
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                            continue;

                        int subMeshCount = GetRenderableSubMeshCount(renderer);
                        if (subMeshCount <= 0)
                            continue;

                        for (int subMeshIndex = 0; subMeshIndex < subMeshCount; ++subMeshIndex)
                        {
                            TryAddDrawItem(renderer, effectMaterial, subMeshIndex);
                        }
                    }
                }
            }
        }

        void TryAddDrawItem(Renderer renderer, Material material, int subMeshIndex)
        {
            if (renderer == null || material == null || subMeshIndex < 0)
                return;

            int key = BuildDrawItemKey(renderer, material, subMeshIndex);
            if (!m_DrawItemKeys.Add(key))
                return;

            m_DrawItems.Add(new DrawItem
            {
                Renderer = renderer,
                Material = material,
                SubMeshIndex = subMeshIndex
            });
        }

        static int BuildDrawItemKey(Renderer renderer, Material material, int subMeshIndex)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + renderer.GetInstanceID();
                hash = hash * 31 + material.GetInstanceID();
                hash = hash * 31 + subMeshIndex;
                return hash;
            }
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        static void ExecutePass(PassData data, RasterGraphContext context)
        {
            ApplyInheritedLightingGlobals();

            DrawItem[] drawItems = data.drawItems;
            if (drawItems == null)
                return;

            for (int i = 0; i < drawItems.Length; ++i)
            {
                DrawItem drawItem = drawItems[i];
                if (drawItem.Renderer == null || drawItem.Material == null)
                    continue;

                context.cmd.DrawRenderer(drawItem.Renderer, drawItem.Material, drawItem.SubMeshIndex);
            }
        }

        static void ApplyInheritedLightingGlobals()
        {
            ApplyPassMiscGlobals();
            ApplyRpgEnvPerMainCameraGlobals(HSRSceneController.instance);
        }

        static void ApplyPassMiscGlobals()
        {
            float cascadeCount = Shader.GetGlobalFloat(k_MainLightShadowCascadeCountId);
            Matrix4x4[] sourceShadowMatrices = Shader.GetGlobalMatrixArray(k_MainLightWorldToShadowId);

            k_MainLightWorldToShadowScratch[0] = sourceShadowMatrices != null && sourceShadowMatrices.Length > 0 ? sourceShadowMatrices[0] : Matrix4x4.identity;
            k_MainLightWorldToShadowScratch[1] = sourceShadowMatrices != null && sourceShadowMatrices.Length > 1 ? sourceShadowMatrices[1] : Matrix4x4.identity;
            k_MainLightWorldToShadowScratch[2] = sourceShadowMatrices != null && sourceShadowMatrices.Length > 2 ? sourceShadowMatrices[2] : Matrix4x4.identity;
            k_MainLightWorldToShadowScratch[3] = sourceShadowMatrices != null && sourceShadowMatrices.Length > 3 ? sourceShadowMatrices[3] : Matrix4x4.identity;
            k_MainLightWorldToShadowScratch[4] = sourceShadowMatrices != null && sourceShadowMatrices.Length > 4 ? sourceShadowMatrices[4] : Matrix4x4.identity;

            if (cascadeCount <= 0f)
            {
                if (sourceShadowMatrices != null && sourceShadowMatrices.Length > 0)
                    cascadeCount = Mathf.Min(4, sourceShadowMatrices.Length);
                else
                    cascadeCount = 1f;
            }

            Shader.SetGlobalVector(k_CascadeShadowSplitSpheres0Id, Shader.GetGlobalVector(k_CascadeShadowSplitSpheres0Id));
            Shader.SetGlobalVector(k_CascadeShadowSplitSpheres1Id, Shader.GetGlobalVector(k_CascadeShadowSplitSpheres1Id));
            Shader.SetGlobalVector(k_CascadeShadowSplitSpheres2Id, Shader.GetGlobalVector(k_CascadeShadowSplitSpheres2Id));
            Shader.SetGlobalVector(k_CascadeShadowSplitSpheres3Id, Shader.GetGlobalVector(k_CascadeShadowSplitSpheres3Id));
            Shader.SetGlobalVector(k_CascadeShadowSplitSphereRadiiId, Shader.GetGlobalVector(k_CascadeShadowSplitSphereRadiiId));
            Shader.SetGlobalVector(k_MainLightShadowParamsId, Shader.GetGlobalVector(k_MainLightShadowParamsId));
            Shader.SetGlobalVector(k_MainLightShadowmapSizeId, Shader.GetGlobalVector(k_MainLightShadowmapSizeId));
            Shader.SetGlobalFloat(k_MainLightShadowCascadeCountId, cascadeCount);
            Shader.SetGlobalMatrixArray(k_MainLightWorldToShadowArrId, k_MainLightWorldToShadowScratch);
        }

        static void ApplyRpgEnvPerMainCameraGlobals(HSRSceneController env)
        {
            if (env == null)
                return;

            Matrix4x4 globalRotMatrix = env.GetGlobalRotMatrix();
            k_EsGlobalRotMatrixScratch[0] = globalRotMatrix.GetRow(0);
            k_EsGlobalRotMatrixScratch[1] = globalRotMatrix.GetRow(1);
            k_EsGlobalRotMatrixScratch[2] = globalRotMatrix.GetRow(2);
            k_EsGlobalRotMatrixScratch[3] = globalRotMatrix.GetRow(3);

            Shader.SetGlobalFloat("_GlobalOneMinusAvatarIntensity", env._GlobalOneMinusAvatarIntensity);
            Shader.SetGlobalVector("_XPad0", Vector3.zero);
            Shader.SetGlobalVector("_ES_MonsterLightDir", env._ES_MonsterLightDir);
            Shader.SetGlobalFloat("_ES_Indoor", env._ES_Indoor ? 1f : 0f);
            Shader.SetGlobalFloat("_ES_TransitionRate", env._ES_TransitionRate);
            Shader.SetGlobalFloat("_ES_SelfShadowLerpHair", env._ES_SelfShadowLerpHair);
            Shader.SetGlobalFloat("_ES_LEVEL_ADJUST_ON", env._ES_LEVEL_ADJUST_ON ? 1f : 0f);
            Shader.SetGlobalFloat("_XPad1", 0f);
            Shader.SetGlobalVectorArray(k_EsGlobalRotMatrixId, k_EsGlobalRotMatrixScratch);
            Shader.SetGlobalFloat("_ES_CharacterToonRampMode", env._ES_CharacterToonRampMode);
            Shader.SetGlobalFloat("_ES_CharacterDisableLocalMainLight", env._ES_CharacterDisableLocalMainLight ? 1f : 0f);
            Shader.SetGlobalVector("_XPad2", Vector2.zero);
            Shader.SetGlobalVector("_ES_AddColor", env._ES_AddColor);
            Shader.SetGlobalVector("_ES_SPColor", env._ES_SPColor);
            Shader.SetGlobalFloat("_ES_SPIntensity", env._ES_SPIntensity);
            Shader.SetGlobalVector("_XPad3", Vector3.zero);
            Shader.SetGlobalVector("_ES_RimShadowColor", env._ES_RimShadowColor);
            Shader.SetGlobalFloat("_ES_RimShadowIntensity", env._ES_RimShadowIntensity);
            Shader.SetGlobalFloat("_ES_CharacterShadowFactor", env._ES_CharacterShadowFactor);
            Shader.SetGlobalFloat("_ES_OutLineDarkenVal", env._ES_OutLineDarkenVal);
            Shader.SetGlobalFloat("_ES_OutLineLightedVal", env._ES_OutLineLightedVal);
            Shader.SetGlobalFloat("_ES_OutlineDisableDistanceScale", env._ES_OutlineDisableDistanceScale);
            Shader.SetGlobalFloat("_ES_OutlineFallbackScale", env._ES_OutlineFallbackScale);
            Shader.SetGlobalFloat("_ES_HeightLerpTop", env._ES_HeightLerpTop);
            Shader.SetGlobalFloat("_ES_HeightLerpBottom", env._ES_HeightLerpBottom);
            Shader.SetGlobalVector("_ES_HeightLerpTopColor", env._ES_HeightLerpTopColor);
            Shader.SetGlobalVector("_ES_HeightLerpMiddleColor", env._ES_HeightLerpMiddleColor);
            Shader.SetGlobalVector("_ES_HeightLerpBottomColor", env._ES_HeightLerpBottomColor);
            Shader.SetGlobalVector("_ES_RimLightOffset", env._ES_RimLightOffset);
            Shader.SetGlobalFloat("_ES_RimLightWidth", env._ES_RimLightWidth);
            Shader.SetGlobalFloat("_ES_RimLightIntensity", env._ES_RimLightIntensity);
            Shader.SetGlobalFloat("_ES_RimLightAddMode", env._ES_RimLightAddMode);
            Shader.SetGlobalFloat("_ES_RimLightMode", env._ES_RimLightMode);
            Shader.SetGlobalVector("_XPad4", Vector2.zero);
            Shader.SetGlobalVector("_ES_RimLightColor", env._ES_RimLightColor);
            Shader.SetGlobalVector("_ES_LevelSkinLightColor", env._ES_LevelSkinLightColor);
            Shader.SetGlobalVector("_ES_LevelSkinShadowColor", env._ES_LevelSkinShadowColor);
            Shader.SetGlobalVector("_ES_LevelHighLightColor", env._ES_LevelHighLightColor);
            Shader.SetGlobalVector("_ES_LevelShadowColor", env._ES_LevelShadowColor);
            Shader.SetGlobalFloat("_ES_LevelShadow", env._ES_LevelShadow);
            Shader.SetGlobalFloat("_ES_LevelMid", env._ES_LevelMid);
            Shader.SetGlobalFloat("_ES_LevelHighLight", env._ES_LevelHighLight);
            Shader.SetGlobalFloat("_ES_LevelEyeShadowIntensity", env._ES_LevelEyeShadowIntensity);
            Shader.SetGlobalFloat("_ES_IndoorCharShadowAsCookie", env._ES_IndoorCharShadowAsCookie ? 1f : 0f);
            Shader.SetGlobalFloat("_ES_FogColor", env._ES_FogColor);
            Shader.SetGlobalFloat("_ES_FogDensity", env._ES_FogDensity);
            Shader.SetGlobalFloat("_ES_FogNear", env._ES_FogNear);
            Shader.SetGlobalFloat("_ES_FogFar", env._ES_FogFar);
            Shader.SetGlobalFloat("_ES_HeightFogColor", env._ES_HeightFogColor);
            Shader.SetGlobalFloat("_ES_HeightFogBaseHeight", env._ES_HeightFogBaseHeight);
            Shader.SetGlobalFloat("_ES_HeightFogRange", env._ES_HeightFogRange);
            Shader.SetGlobalFloat("_ES_HeightFogDensity", env._ES_HeightFogDensity);
            Shader.SetGlobalFloat("_ES_HeightFogFogNear", env._ES_HeightFogFogNear);
            Shader.SetGlobalFloat("_ES_HeightFogFogFar", env._ES_HeightFogFogFar);
            Shader.SetGlobalFloat("_ES_FogCharacterNearFactor", env._ES_FogCharacterNearFactor);
            Shader.SetGlobalFloat("_ES_HeightFogAddAjust", env._ES_HeightFogAddAjust);
            Shader.SetGlobalFloat("_ES_DisableFogTransition", env._ES_DisableFogTransition ? 1f : 0f);
            Shader.SetGlobalVector("_XPad5", Vector2.zero);
            Shader.SetGlobalVector("_ES_EffCustomLightPosition", env._ES_EffCustomLightPosition);
            Shader.SetGlobalFloat("_OutlineScale", env._OutlineScale);
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            const string passName = "Render Custom Pass";

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!ShouldRenderForCamera(cameraData.cameraType, cameraData.isPreviewCamera))
                return;

            BuildDrawItems();
            if (m_DrawItems.Count == 0)
                return;

            // This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                // Use this scope to set the required inputs and outputs of the pass and to
                // setup the passData with the required properties needed at pass execution time.

                // Make use of frameData to access resources and camera data through the dedicated containers.
                // Eg:
                // UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                passData.drawItems = m_DrawItems.ToArray();

                // Setup pass inputs and outputs through the builder interface.
                // Eg:
                // builder.UseTexture(sourceTexture);
                // TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraData.cameraTargetDescriptor, "Destination Texture", false);

                // This sets the render target of the pass to the active color texture. Change it to your own render target as needed.
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                TextureHandle sharedAlphaMask = LightingGBuffer.SharedAlphaMaskHandle;
                if (sharedAlphaMask.IsValid())
                {
                    builder.SetRenderAttachment(sharedAlphaMask, 1);
                    builder.SetGlobalTextureAfterPass(sharedAlphaMask, k_LightingAlphaMaskId);
                }
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }
    }
}
