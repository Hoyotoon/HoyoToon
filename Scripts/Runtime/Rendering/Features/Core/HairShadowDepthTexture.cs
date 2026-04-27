using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Rendering.HSR;
using HoyoToon.Runtime.Rendering.Utilities;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

namespace HoyoToon.Runtime.Rendering.Core
{
    public class HairShadowDepthTexture : ScriptableRendererFeature
    {
        [SerializeField] HairShadowDepthTextureSettings settings = new HairShadowDepthTextureSettings();
        HairShadowDepthTexturePass m_ScriptablePass;

        /// <inheritdoc/>
        public override void Create()
        {
            if (settings == null)
                settings = new HairShadowDepthTextureSettings();

            m_ScriptablePass = new HairShadowDepthTexturePass(settings);

            // Configures where the render pass should be injected.
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
            CameraType cameraType = renderingData.cameraData.cameraType;
            if (renderingData.cameraData.isPreviewCamera
                || (cameraType != CameraType.Game && cameraType != CameraType.SceneView))
            {
                return;
            }

            renderer.EnqueuePass(m_ScriptablePass);
        }

        // Use this class to pass around settings from the feature to the pass
        [Serializable]
        public class HairShadowDepthTextureSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            public string hairTag = "Honkai Star Rail Hair";
            [Min(1)] public int shadowMapWidth = 1024;
            [Min(1)] public int shadowMapHeight = 1024;
        }

        class HairShadowDepthTexturePass : ScriptableRenderPass
        {
            static readonly int k_CharacterHairShadowMapId = Shader.PropertyToID("_CharacterHairShadowMap");
            static readonly string[] k_DepthPassNames =
            {
            "ShadowCaster",
            "DepthOnly",
            "DepthNormalsOnly",
            "DepthNormals"
        };

            static readonly List<Renderer> k_RendererScratch = new List<Renderer>(64);
            static readonly List<Renderer> k_HairRenderersScratch = new List<Renderer>(64);
            static readonly List<HSRCharacterController> k_ControllerScratch = new List<HSRCharacterController>(16);
            static readonly List<Material> k_MaterialScratch = new List<Material>(8);
            static readonly Dictionary<Material, int> k_DepthPassIndexByMaterial = new Dictionary<Material, int>();
            static readonly Plane[] k_FrustumPlanes = new Plane[6];

            readonly HairShadowDepthTextureSettings settings;
            readonly List<Renderer> m_CachedTaggedHairRenderers = new List<Renderer>(64);
            Renderer[] m_HairRendererSnapshot = Array.Empty<Renderer>();
            int m_CachedRendererTopologyVersion = -1;
            string m_CachedHairTag = string.Empty;

            public HairShadowDepthTexturePass(HairShadowDepthTextureSettings settings)
            {
                this.settings = settings;
            }

            // This class stores the data needed by the RenderGraph pass.
            // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
            private class PassData
            {
                public Renderer[] hairRenderers;
                public int hairRendererCount;
            }

            static int GetSubMeshCount(Renderer renderer)
            {
                if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
                    return Mathf.Max(1, skinnedRenderer.sharedMesh.subMeshCount);

                if (renderer is MeshRenderer meshRenderer)
                {
                    MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                        return Mathf.Max(1, meshFilter.sharedMesh.subMeshCount);
                }

                return 1;
            }

            static int GetDepthPassIndex(Material material)
            {
                if (material == null)
                    return -1;

                if (k_DepthPassIndexByMaterial.TryGetValue(material, out int cachedPassIndex))
                    return cachedPassIndex;

                for (int i = 0; i < k_DepthPassNames.Length; ++i)
                {
                    int passIndex = material.FindPass(k_DepthPassNames[i]);
                    if (passIndex >= 0)
                    {
                        k_DepthPassIndexByMaterial[material] = passIndex;
                        return passIndex;
                    }
                }

                k_DepthPassIndexByMaterial[material] = 0;
                return 0;
            }

            int CollectHairRenderers(Camera camera)
            {
                k_HairRenderersScratch.Clear();

                if (string.IsNullOrEmpty(settings.hairTag))
                    return 0;

                RebuildTaggedHairRendererCacheIfNeeded();

                bool hasCamera = camera != null && IsFinite(camera.transform.position) && IsFinite(camera.transform.forward);
                if (hasCamera)
                    GeometryUtility.CalculateFrustumPlanes(camera, k_FrustumPlanes);

                for (int i = 0; i < m_CachedTaggedHairRenderers.Count; ++i)
                {
                    Renderer renderer = m_CachedTaggedHairRenderers[i];
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        continue;

                    Bounds bounds = renderer.bounds;
                    if (!IsValid(bounds))
                        continue;

                    if (hasCamera && !GeometryUtility.TestPlanesAABB(k_FrustumPlanes, bounds))
                        continue;

                    k_HairRenderersScratch.Add(renderer);
                }

                return RendererSnapshotUtility.CopyToSnapshot(k_HairRenderersScratch, ref m_HairRendererSnapshot);
            }

            void RebuildTaggedHairRendererCacheIfNeeded()
            {
                int topologyVersion = HSRCharacterController.RendererTopologyVersion;
                if (topologyVersion == m_CachedRendererTopologyVersion
                    && string.Equals(settings.hairTag, m_CachedHairTag, StringComparison.Ordinal))
                {
                    PruneNullCachedHairRenderers();
                    return;
                }

                m_CachedTaggedHairRenderers.Clear();
                m_CachedRendererTopologyVersion = topologyVersion;
                m_CachedHairTag = settings.hairTag ?? string.Empty;

                HSRCharacterController.GetActiveControllers(k_ControllerScratch, forceRefresh: false);
                for (int i = 0; i < k_ControllerScratch.Count; ++i)
                {
                    HSRCharacterController controller = k_ControllerScratch[i];
                    if (controller == null)
                        continue;

                    Renderer[] scopedRenderers = controller.GetScopedRenderers();
                    if (scopedRenderers == null)
                        continue;

                    k_RendererScratch.Clear();
                    k_RendererScratch.AddRange(scopedRenderers);
                    for (int r = 0; r < k_RendererScratch.Count; ++r)
                    {
                        Renderer renderer = k_RendererScratch[r];
                        if (renderer == null || !UnityTagUtility.TryCompareTag(renderer.gameObject, m_CachedHairTag))
                            continue;

                        m_CachedTaggedHairRenderers.Add(renderer);
                    }

                    k_RendererScratch.Clear();
                }
            }

            void PruneNullCachedHairRenderers()
            {
                for (int i = m_CachedTaggedHairRenderers.Count - 1; i >= 0; --i)
                {
                    if (m_CachedTaggedHairRenderers[i] == null)
                        m_CachedTaggedHairRenderers.RemoveAt(i);
                }
            }

            static bool IsValid(Bounds bounds)
            {
                return IsFinite(bounds.center)
                    && IsFinite(bounds.extents)
                    && IsFinite(bounds.min)
                    && IsFinite(bounds.max);
            }

            static bool IsFinite(Vector3 value)
            {
                return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                    && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                    && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
            }

            // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
            // It is used to execute draw commands.
            static void ExecutePass(PassData data, RasterGraphContext context)
            {
                context.cmd.ClearRenderTarget(true, false, Color.clear);

                Renderer[] hairRenderers = data.hairRenderers;
                if (hairRenderers == null)
                    return;

                for (int i = 0; i < data.hairRendererCount; ++i)
                {
                    Renderer renderer = hairRenderers[i];
                    if (renderer == null)
                        continue;

                    if (!HsrRendererMaterialQueryUtility.TryGetSharedMaterials(renderer, k_MaterialScratch, out int materialCount))
                        continue;

                    try
                    {
                        int subMeshCount = GetSubMeshCount(renderer);
                        for (int m = 0; m < materialCount; ++m)
                        {
                            Material material = k_MaterialScratch[m];
                            int passIndex = GetDepthPassIndex(material);
                            if (passIndex < 0)
                                continue;

                            int subMeshIndex = Mathf.Min(m, subMeshCount - 1);
                            context.cmd.DrawRenderer(renderer, material, subMeshIndex, passIndex);
                        }
                    }
                    finally
                    {
                        k_MaterialScratch.Clear();
                    }
                }
            }

            // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
            // FrameData is a context container through which URP resources can be accessed and managed.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                const string passName = "Hair Shadow Depth Texture";

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.isPreviewCamera
                    || (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView))
                {
                    return;
                }

                int hairRendererCount = CollectHairRenderers(cameraData.camera);
                if (hairRendererCount == 0)
                {
                    Shader.SetGlobalTexture(k_CharacterHairShadowMapId, Texture2D.blackTexture);
                    return;
                }

                // This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                {
                    // Use this scope to set the required inputs and outputs of the pass and to
                    // setup the passData with the required properties needed at pass execution time.

                    // Make use of frameData to access resources and camera data through the dedicated containers.
                    // Eg:
                    // UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    int shadowMapWidth = Mathf.Max(1, settings.shadowMapWidth);
                    int shadowMapHeight = Mathf.Max(1, settings.shadowMapHeight);

                    RenderTextureDescriptor hairDepthDescriptor = cameraData.cameraTargetDescriptor;
                    hairDepthDescriptor.width = shadowMapWidth;
                    hairDepthDescriptor.height = shadowMapHeight;
                    hairDepthDescriptor.msaaSamples = 1;
                    hairDepthDescriptor.colorFormat = RenderTextureFormat.Depth;
                    hairDepthDescriptor.graphicsFormat = GraphicsFormat.None;
                    hairDepthDescriptor.depthStencilFormat = GraphicsFormat.D16_UNorm;
                    hairDepthDescriptor.depthBufferBits = 16;
                    hairDepthDescriptor.bindMS = false;
                    hairDepthDescriptor.enableRandomWrite = false;
                    hairDepthDescriptor.mipCount = 1;
                    hairDepthDescriptor.autoGenerateMips = false;

                    TextureHandle hairDepthTexture = UniversalRenderer.CreateRenderGraphTexture(
                        renderGraph,
                        hairDepthDescriptor,
                        "_CharacterHairShadowMap",
                        false);

                    passData.hairRenderers = m_HairRendererSnapshot;
                    passData.hairRendererCount = hairRendererCount;

                    // Setup pass inputs and outputs through the builder interface.
                    // Eg:
                    // builder.UseTexture(sourceTexture);
                    // TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraData.cameraTargetDescriptor, "Destination Texture", false);

                    builder.SetRenderAttachmentDepth(hairDepthTexture, AccessFlags.ReadWrite);
                    builder.SetGlobalTextureAfterPass(hairDepthTexture, k_CharacterHairShadowMapId);
                    builder.AllowPassCulling(false);

                    // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
                }
            }
        }
    }
}
