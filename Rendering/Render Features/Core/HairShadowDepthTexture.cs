using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

namespace HoyoToon.Rendering.Core
{
    public class HairShadowDepthTexture : ScriptableRendererFeature
    {
        [SerializeField] HairShadowDepthTextureSettings settings;
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
            static readonly Plane[] k_FrustumPlanes = new Plane[6];

            readonly HairShadowDepthTextureSettings settings;

            public HairShadowDepthTexturePass(HairShadowDepthTextureSettings settings)
            {
                this.settings = settings;
            }

            // This class stores the data needed by the RenderGraph pass.
            // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
            private class PassData
            {
                public Renderer[] hairRenderers;
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

                for (int i = 0; i < k_DepthPassNames.Length; ++i)
                {
                    int passIndex = material.FindPass(k_DepthPassNames[i]);
                    if (passIndex >= 0)
                        return passIndex;
                }

                return 0;
            }

            Renderer[] CollectHairRenderers(Camera camera)
            {
                k_HairRenderersScratch.Clear();

                if (string.IsNullOrEmpty(settings.hairTag))
                    return Array.Empty<Renderer>();

                GameObject[] taggedObjects;
                try
                {
                    taggedObjects = GameObject.FindGameObjectsWithTag(settings.hairTag);
                }
                catch (UnityException)
                {
                    return Array.Empty<Renderer>();
                }

                bool hasCamera = camera != null;
                if (hasCamera)
                    GeometryUtility.CalculateFrustumPlanes(camera, k_FrustumPlanes);

                for (int i = 0; i < taggedObjects.Length; ++i)
                {
                    GameObject taggedObject = taggedObjects[i];
                    if (taggedObject == null)
                        continue;

                    taggedObject.GetComponentsInChildren(true, k_RendererScratch);
                    for (int r = 0; r < k_RendererScratch.Count; ++r)
                    {
                        Renderer renderer = k_RendererScratch[r];
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                            continue;

                        // Only draw renderers explicitly tagged as hair.
                        if (!renderer.gameObject.CompareTag(settings.hairTag))
                            continue;

                        if (hasCamera && !GeometryUtility.TestPlanesAABB(k_FrustumPlanes, renderer.bounds))
                            continue;

                        k_HairRenderersScratch.Add(renderer);
                    }

                    k_RendererScratch.Clear();
                }

                return k_HairRenderersScratch.ToArray();
            }

            // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
            // It is used to execute draw commands.
            static void ExecutePass(PassData data, RasterGraphContext context)
            {
                context.cmd.ClearRenderTarget(true, false, Color.clear);

                Renderer[] hairRenderers = data.hairRenderers;
                if (hairRenderers == null)
                    return;

                for (int i = 0; i < hairRenderers.Length; ++i)
                {
                    Renderer renderer = hairRenderers[i];
                    if (renderer == null)
                        continue;

                    Material[] materials = renderer.sharedMaterials;
                    if (materials == null || materials.Length == 0)
                        continue;

                    int subMeshCount = GetSubMeshCount(renderer);
                    for (int m = 0; m < materials.Length; ++m)
                    {
                        Material material = materials[m];
                        int passIndex = GetDepthPassIndex(material);
                        if (passIndex < 0)
                            continue;

                        int subMeshIndex = Mathf.Min(m, subMeshCount - 1);
                        context.cmd.DrawRenderer(renderer, material, subMeshIndex, passIndex);
                    }
                }
            }

            // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
            // FrameData is a context container through which URP resources can be accessed and managed.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                const string passName = "Hair Shadow Depth Texture";

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                Renderer[] hairRenderers = CollectHairRenderers(cameraData.camera);

                if (hairRenderers.Length == 0)
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

                    passData.hairRenderers = hairRenderers;

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