using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Rendering.HSR;
using HoyoToon.Runtime.Rendering.Utilities;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Scene.HSR;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityScene = UnityEngine.SceneManagement.Scene;

public class CustomRPTransparentBuffer : ScriptableRendererFeature
{
    [SerializeField] CustomRPTransparentBufferSettings settings;
    CustomRPTransparentBufferPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        if (settings == null)
            settings = new CustomRPTransparentBufferSettings();

        m_ScriptablePass = new CustomRPTransparentBufferPass(settings)
        {
            // Configure this pass to run after LightingGBuffer forward by default.
            renderPassEvent = settings.renderPassEvent
        };

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
        readonly HashSet<DrawItemKey> m_DrawItemKeys = new HashSet<DrawItemKey>();
        readonly List<Material> m_SharedMaterialScratch = new List<Material>(8);
        DrawItem[] m_DrawItemSnapshot = Array.Empty<DrawItem>();
        int m_DrawItemSnapshotCount;
        bool m_HasDrawItemSnapshot;
        int m_LastDrawItemSourceVersion;
        static readonly ShaderTagId s_LightModeTag = new ShaderTagId("LightMode");
        const string RequiredLightModeTag = "CustomRPTransparent";

        static readonly int k_LightingAlphaMaskId = Shader.PropertyToID("_LightingAlphaMask");

        struct DrawItem
        {
            public Renderer Renderer;
            public Material Material;
            public int SubMeshIndex;
            public int PassIndex;
        }

        readonly struct DrawItemKey : IEquatable<DrawItemKey>
        {
            public readonly int RendererId;
            public readonly int MaterialId;
            public readonly int SubmeshIndex;
            public readonly int PassIndex;

            public DrawItemKey(Renderer renderer, Material material, int submeshIndex, int passIndex)
            {
                RendererId = renderer != null ? renderer.GetInstanceID() : 0;
                MaterialId = material != null ? material.GetInstanceID() : 0;
                SubmeshIndex = submeshIndex;
                PassIndex = passIndex;
            }

            public bool Equals(DrawItemKey other)
            {
                return RendererId == other.RendererId
                    && MaterialId == other.MaterialId
                    && SubmeshIndex == other.SubmeshIndex
                    && PassIndex == other.PassIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is DrawItemKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = RendererId;
                    hash = (hash * 397) ^ MaterialId;
                    hash = (hash * 397) ^ SubmeshIndex;
                    hash = (hash * 397) ^ PassIndex;
                    return hash;
                }
            }
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
            public int drawItemCount;
            public HsrInheritedLightingGlobals.SceneGlobalState sceneGlobals;
            public HsrInheritedLightingGlobals.SceneKeywordState sceneKeywords;
        }

        static bool HasRequiredLightModePass(Material material)
        {
            return HsrRendererMaterialQueryUtility.HasShaderPassWithTagValue(material, s_LightModeTag, RequiredLightModeTag, StringComparison.Ordinal);
        }

        static int GetRenderableSubMeshCount(Renderer renderer, int sharedMaterialCount)
        {
            if (renderer == null)
                return 0;

            int subMeshCount = RendererTraversalUtility.GetSubMeshCount(renderer);
            return subMeshCount > 0 ? subMeshCount : sharedMaterialCount;
        }

        bool EnsureDrawItemSnapshot(UnityScene scene)
        {
            int sourceVersion = CaptureDrawItemSourceVersion(scene);
            if (m_HasDrawItemSnapshot && sourceVersion == m_LastDrawItemSourceVersion)
                return m_DrawItemSnapshotCount > 0;

            RebuildDrawItemsFromControllers();
            m_DrawItemSnapshotCount = RendererSnapshotUtility.CopyToSnapshot(m_DrawItems, ref m_DrawItemSnapshot);
            m_HasDrawItemSnapshot = true;
            m_LastDrawItemSourceVersion = sourceVersion;
            return m_DrawItemSnapshotCount > 0;
        }

        int CaptureDrawItemSourceVersion(UnityScene scene)
        {
            HSRCharacterController.GetRegisteredActiveControllersInScene(scene, m_Controllers);

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + RenderSceneUtility.GetSceneHandleOrDefault(scene);
                hash = hash * 31 + HSRCharacterController.RendererTopologyVersion;
                hash = hash * 31 + HsrRendererMaterialQueryUtility.MaterialPassCacheVersion;
                hash = hash * 31 + m_Controllers.Count;
                for (int i = 0; i < m_Controllers.Count; ++i)
                {
                    HSRCharacterController controller = m_Controllers[i];
                    hash = hash * 31 + (controller != null ? controller.GetInstanceID() : 0);
                    hash = hash * 31 + (controller != null ? controller.RendererScopeVersion : 0);
                    hash = hash * 31 + (controller != null ? controller.EffectMaterialsVersion : 0);
                }

                return hash;
            }
        }

        void RebuildDrawItemsFromControllers()
        {
            m_DrawItems.Clear();
            m_DrawItemKeys.Clear();

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
                    if (renderer == null)
                        continue;

                    if (!HsrRendererMaterialQueryUtility.TryGetSharedMaterials(renderer, m_SharedMaterialScratch, out int slottedMaterialCount))
                        continue;
                    try
                    {
                        int subMeshCount = GetRenderableSubMeshCount(renderer, slottedMaterialCount);
                        if (subMeshCount <= 0)
                            continue;

                        int slottedCount = Mathf.Min(slottedMaterialCount, subMeshCount);
                        for (int subMeshIndex = 0; subMeshIndex < slottedCount; ++subMeshIndex)
                        {
                            Material slottedMaterial = m_SharedMaterialScratch[subMeshIndex];
                            if (!HasRequiredLightModePass(slottedMaterial))
                                continue;

                            TryAddDrawItem(renderer, slottedMaterial, subMeshIndex);
                        }
                    }
                    finally
                    {
                        m_SharedMaterialScratch.Clear();
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
                        if (renderer == null)
                            continue;

                        HsrRendererMaterialQueryUtility.TryGetSharedMaterials(renderer, m_SharedMaterialScratch, out int sharedMaterialCount);
                        m_SharedMaterialScratch.Clear();

                        int subMeshCount = GetRenderableSubMeshCount(renderer, sharedMaterialCount);
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

            int passIndex = -1;
            DrawItemKey key = new DrawItemKey(renderer, material, subMeshIndex, passIndex);
            if (!m_DrawItemKeys.Add(key))
                return;

            m_DrawItems.Add(new DrawItem
            {
                Renderer = renderer,
                Material = material,
                SubMeshIndex = subMeshIndex,
                PassIndex = passIndex
            });
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        static void ExecutePass(PassData data, RasterGraphContext context)
        {
            HsrInheritedLightingGlobals.ApplySceneGlobals(context.cmd, data.sceneGlobals);
            HsrInheritedLightingGlobals.ApplySceneKeywords(context.cmd, data.sceneKeywords);

            DrawItem[] drawItems = data.drawItems;
            if (drawItems == null)
            {
                HsrInheritedLightingGlobals.ClearSceneKeywords(context.cmd);
                return;
            }

            for (int i = 0; i < data.drawItemCount; ++i)
            {
                DrawItem drawItem = drawItems[i];
                if (!IsDrawItemRenderable(drawItem))
                    continue;

                if (drawItem.PassIndex >= 0)
                    context.cmd.DrawRenderer(drawItem.Renderer, drawItem.Material, drawItem.SubMeshIndex, drawItem.PassIndex);
                else
                    context.cmd.DrawRenderer(drawItem.Renderer, drawItem.Material, drawItem.SubMeshIndex);
            }

            HsrInheritedLightingGlobals.ClearSceneKeywords(context.cmd);
        }

        static bool IsDrawItemRenderable(DrawItem drawItem)
        {
            Renderer renderer = drawItem.Renderer;
            return renderer != null
                && drawItem.Material != null
                && renderer.enabled
                && renderer.gameObject != null
                && renderer.gameObject.activeInHierarchy;
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            const string passName = "Render Custom Pass";

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!ShouldRenderForCamera(cameraData.cameraType, cameraData.isPreviewCamera))
                return;

            UnityScene renderScene = ResolveRenderScene(cameraData.camera);
            if (!EnsureDrawItemSnapshot(renderScene))
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
                passData.drawItems = m_DrawItemSnapshot;
                passData.drawItemCount = m_DrawItemSnapshotCount;
                HSRSceneController sceneController = HSRSceneController.FindForScene(renderScene);
                passData.sceneGlobals = HsrInheritedLightingGlobals.CaptureSceneGlobals(sceneController, clearEnvironmentWhenMissing: false, cameraData.camera);
                passData.sceneKeywords = HsrInheritedLightingGlobals.CaptureSceneKeywords(sceneController);

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
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);

                // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }

        private static UnityScene ResolveRenderScene(Camera camera)
        {
            return RenderSceneUtility.ResolveRenderScene(
                camera,
                scene => HSRSceneController.FindForScene(scene) != null);
        }

    }
}
