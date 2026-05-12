using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Rendering.Utilities;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Rendering.Core
{
    public class PlanarReflection : ScriptableRendererFeature
    {
        private const string DefaultReflectionTextureName = "_ReflectionColor";
        private static readonly string[] DefaultReflectionReceiverShaderNames =
        {
            "HoyoToon/Honkai Star Rail/UI/Manikin/Floor"
        };

        [Serializable]
        public class PlanarReflectionSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingShadows;
            [Min(1)] public int reflectionWidth = 1024;
            [Min(1)] public int reflectionHeight = 1024;
            public float reflectionPlaneY = 0f;
            public string reflectionTextureName = DefaultReflectionTextureName;
            [HideInInspector]
            public string[] reflectionReceiverShaderNames = (string[])DefaultReflectionReceiverShaderNames.Clone();
            public string[] layerNames =
            {
                "Default",
                "Honkai Star Rail",
                "Honkai Impact 3rd",
                "Honkai Impact3rd",
                "Genshin Impact",
                "Zenless Zone Zero"
            };
        }

        [SerializeField] private PlanarReflectionSettings settings = new PlanarReflectionSettings();
        private PlanarReflectionPass m_ScriptablePass;

        public override void Create()
        {
            if (settings == null)
                settings = new PlanarReflectionSettings();

            m_ScriptablePass?.Cleanup();
            m_ScriptablePass = null;
            m_ScriptablePass = new PlanarReflectionPass(settings)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_ScriptablePass == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (renderingData.cameraData.isPreviewCamera
                || cameraType == CameraType.Preview
                || cameraType == CameraType.Reflection
                || (cameraType != CameraType.Game && cameraType != CameraType.SceneView))
            {
                return;
            }

            renderer.EnqueuePass(m_ScriptablePass);
        }

        private class PlanarReflectionPass : ScriptableRenderPass
        {
            private const int LegacyMissScanIntervalFrames = 30;
            private const int MaterialPassCacheMaxEntries = 512;
            private const int ReflectionMaterialPassQueryId = 1001;

            private static readonly List<PlanarReflectionParticipant> k_Participants =
                new List<PlanarReflectionParticipant>(32);

            private static readonly List<Renderer> k_FilteredRenderers = new List<Renderer>(256);
            private static readonly List<Renderer> k_ReflectionReceiverRenderers = new List<Renderer>(32);
            private static readonly List<Renderer> k_LegacyRenderers = new List<Renderer>(512);
            private static readonly List<Renderer> k_LegacyRendererScratch = new List<Renderer>(256);
            private static readonly List<GameObject> k_LegacyRootScratch = new List<GameObject>(32);
            private static readonly List<Material> k_SharedMaterials = new List<Material>(16);
            private static readonly HashSet<Renderer> k_UniqueRenderers = new HashSet<Renderer>();
            private static readonly HashSet<Renderer> k_UniqueReceiverRenderers = new HashSet<Renderer>();
            private static readonly HashSet<Renderer> k_LegacyReceiverRenderers = new HashSet<Renderer>();
            private static readonly string[] k_RenderPassNames =
            {
                "CustomForward",
                "LightingGBuffer",
                "UniversalForward",
                "UniversalForwardOnly",
                "SRPDefaultUnlit",
                "ForwardBase",
                "Always"
            };

            private readonly PlanarReflectionSettings settings;
            private int m_LastSceneHandle;
            private int m_LastLayerMask;
            private int m_LastReceiverShaderNamesHash;
            private int m_LastRegistryVersion = -1;
            private bool m_CachedHasReflectionReceiver;
            private Renderer[] m_CachedRenderers = Array.Empty<Renderer>();
            private Renderer[] m_CachedReflectionReceiverRenderers = Array.Empty<Renderer>();
            private int m_LastLegacyScanFrame = -LegacyMissScanIntervalFrames;
            private int m_LastLegacySceneHandle;
            private int m_LastLegacyLayerMask;
            private int m_LastLegacyReceiverShaderNamesHash;
            private bool m_CachedLegacyHasReflectionReceiver;
            private Renderer[] m_CachedLegacyRenderers = Array.Empty<Renderer>();
            private Renderer[] m_CachedLegacyReceiverRenderers = Array.Empty<Renderer>();

            private class PassData
            {
                public Renderer[] renderers;
                public Matrix4x4 reflectionMatrix;
                public Matrix4x4 reflectedViewMatrix;
                public Matrix4x4 cameraViewMatrix;
                public Matrix4x4 cameraProjectionMatrix;
                public int layerMask;
            }

            private class ClearGlobalsPassData
            {
                public TextureHandle fallbackTexture;
                public int reflectionTextureId;
            }

            public PlanarReflectionPass(PlanarReflectionSettings settings)
            {
                this.settings = settings;
            }

            private static string ResolveReflectionTextureName(PlanarReflectionSettings settings)
            {
                return settings == null || string.IsNullOrWhiteSpace(settings.reflectionTextureName)
                    ? DefaultReflectionTextureName
                    : settings.reflectionTextureName;
            }

            private static string[] ResolveReflectionReceiverShaderNames(PlanarReflectionSettings settings)
            {
                return settings == null
                    || settings.reflectionReceiverShaderNames == null
                    || settings.reflectionReceiverShaderNames.Length == 0
                        ? DefaultReflectionReceiverShaderNames
                        : settings.reflectionReceiverShaderNames;
            }

            private static int BuildLayerMask(string[] layerNames)
            {
                int mask = 0;

                if (layerNames == null)
                    return mask;

                for (int i = 0; i < layerNames.Length; ++i)
                {
                    string layerName = layerNames[i];
                    if (string.IsNullOrWhiteSpace(layerName))
                        continue;

                    int layer = LayerMask.NameToLayer(layerName);
                    if (layer >= 0)
                        mask |= 1 << layer;
                }

                return mask;
            }

            private static int ComputeStringArrayHash(string[] values)
            {
                unchecked
                {
                    int hash = 17;
                    if (values == null)
                        return hash;

                    for (int i = 0; i < values.Length; ++i)
                    {
                        string value = values[i];
                        hash = hash * 31 + (value != null ? StringComparer.Ordinal.GetHashCode(value) : 0);
                    }

                    return hash;
                }
            }

            private Renderer[] CollectRenderers(UnityScene scene, int layerMask, out bool hasReflectionReceiver)
            {
                int registryVersion = RenderParticipantRegistry.GetVersion(scene);
                int sceneHandle = RenderSceneUtility.GetSceneHandleOrDefault(scene);
                string[] receiverShaderNames = ResolveReflectionReceiverShaderNames(settings);
                int receiverShaderNamesHash = ComputeStringArrayHash(receiverShaderNames);
                bool legacyFallbackAllowed = IsLegacyReflectionFallbackAllowed();
                bool cacheMatches = m_LastSceneHandle == sceneHandle
                    && m_LastLayerMask == layerMask
                    && m_LastReceiverShaderNamesHash == receiverShaderNamesHash
                    && m_LastRegistryVersion == registryVersion;
                bool retryLegacyMiss = legacyFallbackAllowed
                    && cacheMatches
                    && !m_CachedHasReflectionReceiver
                    && Time.frameCount - m_LastLegacyScanFrame >= LegacyMissScanIntervalFrames;
                if (cacheMatches && !retryLegacyMiss)
                {
                    hasReflectionReceiver = HasAnyActiveRenderer(m_CachedReflectionReceiverRenderers);
                    return m_CachedRenderers;
                }

                m_LastSceneHandle = sceneHandle;
                m_LastLayerMask = layerMask;
                m_LastReceiverShaderNamesHash = receiverShaderNamesHash;
                m_LastRegistryVersion = registryVersion;
                m_CachedHasReflectionReceiver = false;
                k_FilteredRenderers.Clear();
                k_ReflectionReceiverRenderers.Clear();
                k_UniqueRenderers.Clear();
                k_UniqueReceiverRenderers.Clear();

                RenderParticipantRegistry.GetPlanarReflectionParticipants(scene, k_Participants);

                for (int i = 0; i < k_Participants.Count; ++i)
                {
                    PlanarReflectionParticipant participant = k_Participants[i];
                    if (participant == null || !participant.ReceivesReflection)
                        continue;

                    AddReceiverRenderers(participant.Renderers);
                }

                bool hasMarkedReceiver = k_ReflectionReceiverRenderers.Count > 0;

                for (int i = 0; i < k_Participants.Count; ++i)
                {
                    PlanarReflectionParticipant participant = k_Participants[i];
                    if (participant == null || !participant.CastsReflection)
                        continue;

                    IReadOnlyList<Renderer> renderers = participant.Renderers;
                    if (renderers == null)
                        continue;

                    for (int rendererIndex = 0; rendererIndex < renderers.Count; ++rendererIndex)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        if (!IsPotentialReflectionCaster(renderer, layerMask))
                            continue;

                        if (!k_UniqueRenderers.Add(renderer))
                            continue;

                        k_FilteredRenderers.Add(renderer);
                    }
                }

                if (legacyFallbackAllowed && (!hasMarkedReceiver || k_FilteredRenderers.Count == 0))
                {
                    Renderer[] legacyRenderers = CollectLegacyRenderers(
                        scene,
                        layerMask,
                        receiverShaderNames,
                        out Renderer[] legacyReceiverRenderers);

                    for (int i = 0; i < legacyReceiverRenderers.Length; ++i)
                    {
                        AddReceiverRenderer(legacyReceiverRenderers[i]);
                    }

                    for (int i = 0; i < legacyRenderers.Length; ++i)
                    {
                        Renderer renderer = legacyRenderers[i];
                        if (!k_UniqueRenderers.Add(renderer))
                            continue;

                        k_FilteredRenderers.Add(renderer);
                    }
                }

                m_CachedHasReflectionReceiver = k_ReflectionReceiverRenderers.Count > 0;
                m_CachedRenderers = k_FilteredRenderers.ToArray();
                m_CachedReflectionReceiverRenderers = k_ReflectionReceiverRenderers.ToArray();
                hasReflectionReceiver = HasAnyActiveRenderer(m_CachedReflectionReceiverRenderers);
                k_Participants.Clear();
                k_ReflectionReceiverRenderers.Clear();
                k_UniqueRenderers.Clear();
                k_UniqueReceiverRenderers.Clear();
                return m_CachedRenderers;
            }

            private static bool IsLegacyReflectionFallbackAllowed()
            {
                return true;
            }

            private Renderer[] CollectLegacyRenderers(
                UnityScene scene,
                int layerMask,
                string[] receiverShaderNames,
                out Renderer[] reflectionReceivers)
            {
                int sceneHandle = RenderSceneUtility.GetSceneHandleOrDefault(scene);
                int receiverShaderNamesHash = ComputeStringArrayHash(receiverShaderNames);
                if (m_LastLegacySceneHandle == sceneHandle
                    && m_LastLegacyLayerMask == layerMask
                    && m_LastLegacyReceiverShaderNamesHash == receiverShaderNamesHash)
                {
                    if (m_CachedLegacyHasReflectionReceiver
                        || Time.frameCount - m_LastLegacyScanFrame < LegacyMissScanIntervalFrames)
                    {
                        reflectionReceivers = m_CachedLegacyReceiverRenderers;
                        return m_CachedLegacyRenderers;
                    }
                }

                m_LastLegacyScanFrame = Time.frameCount;
                m_LastLegacySceneHandle = sceneHandle;
                m_LastLegacyLayerMask = layerMask;
                m_LastLegacyReceiverShaderNamesHash = receiverShaderNamesHash;
                m_CachedLegacyHasReflectionReceiver = false;
                k_LegacyRenderers.Clear();
                k_LegacyReceiverRenderers.Clear();
                k_LegacyRootScratch.Clear();

                if (RenderSceneUtility.IsSceneUsable(scene))
                {
                    scene.GetRootGameObjects(k_LegacyRootScratch);
                    for (int rootIndex = 0; rootIndex < k_LegacyRootScratch.Count; ++rootIndex)
                    {
                        GameObject root = k_LegacyRootScratch[rootIndex];
                        if (root == null)
                            continue;

                        k_LegacyRendererScratch.Clear();
                        root.GetComponentsInChildren(true, k_LegacyRendererScratch);
                        for (int rendererIndex = 0; rendererIndex < k_LegacyRendererScratch.Count; ++rendererIndex)
                        {
                            Renderer renderer = k_LegacyRendererScratch[rendererIndex];
                            if (renderer == null || renderer.gameObject == null)
                                continue;

                            if (!RendererUsesReflectionReceiverShader(renderer, receiverShaderNames))
                                continue;

                            k_LegacyReceiverRenderers.Add(renderer);
                            m_CachedLegacyHasReflectionReceiver = true;
                        }
                    }

                    for (int rootIndex = 0; rootIndex < k_LegacyRootScratch.Count; ++rootIndex)
                    {
                        GameObject root = k_LegacyRootScratch[rootIndex];
                        if (root == null)
                            continue;

                        k_LegacyRendererScratch.Clear();
                        root.GetComponentsInChildren(true, k_LegacyRendererScratch);
                        for (int rendererIndex = 0; rendererIndex < k_LegacyRendererScratch.Count; ++rendererIndex)
                        {
                            Renderer renderer = k_LegacyRendererScratch[rendererIndex];
                            if (renderer == null
                                || renderer.gameObject == null
                                || k_LegacyReceiverRenderers.Contains(renderer))
                            {
                                continue;
                            }

                            GameObject go = renderer.gameObject;
                            if (go == null || (layerMask & (1 << go.layer)) == 0)
                                continue;

                            k_LegacyRenderers.Add(renderer);
                        }
                    }
                }

                m_CachedLegacyRenderers = k_LegacyRenderers.ToArray();
                m_CachedLegacyReceiverRenderers = CopyRendererSetToArray(k_LegacyReceiverRenderers);
                reflectionReceivers = m_CachedLegacyReceiverRenderers;
                k_LegacyRenderers.Clear();
                k_LegacyReceiverRenderers.Clear();
                k_LegacyRendererScratch.Clear();
                k_LegacyRootScratch.Clear();
                return m_CachedLegacyRenderers;
            }

            private static Renderer[] CopyRendererSetToArray(HashSet<Renderer> renderers)
            {
                if (renderers == null || renderers.Count == 0)
                    return Array.Empty<Renderer>();

                Renderer[] copy = new Renderer[renderers.Count];
                int index = 0;
                foreach (Renderer renderer in renderers)
                {
                    copy[index++] = renderer;
                }

                return copy;
            }

            private static void AddReceiverRenderers(IReadOnlyList<Renderer> renderers)
            {
                if (renderers == null)
                    return;

                for (int i = 0; i < renderers.Count; ++i)
                {
                    AddReceiverRenderer(renderers[i]);
                }
            }

            private static void AddReceiverRenderer(Renderer renderer)
            {
                if (renderer == null || renderer.gameObject == null)
                    return;

                if (k_UniqueReceiverRenderers.Add(renderer))
                    k_ReflectionReceiverRenderers.Add(renderer);
            }

            private static bool HasAnyActiveRenderer(IReadOnlyList<Renderer> renderers)
            {
                if (renderers == null)
                    return false;

                for (int i = 0; i < renderers.Count; ++i)
                {
                    if (PlanarReflectionParticipant.IsActiveRenderer(renderers[i]))
                        return true;
                }

                return false;
            }

            private static bool IsPotentialReflectionCaster(Renderer renderer, int layerMask)
            {
                if (renderer == null || renderer.gameObject == null)
                    return false;

                return (layerMask & (1 << renderer.gameObject.layer)) != 0;
            }

            private static bool IsRenderableReflectionCaster(Renderer renderer, int layerMask)
            {
                return IsPotentialReflectionCaster(renderer, layerMask)
                    && PlanarReflectionParticipant.IsActiveRenderer(renderer);
            }

            private static bool HasAnyRenderableReflectionCaster(Renderer[] renderers, int layerMask)
            {
                if (renderers == null)
                    return false;

                for (int i = 0; i < renderers.Length; ++i)
                {
                    if (IsRenderableReflectionCaster(renderers[i], layerMask))
                        return true;
                }

                return false;
            }

            private static bool RendererUsesReflectionReceiverShader(Renderer renderer, string[] receiverShaderNames)
            {
                if (renderer == null || receiverShaderNames == null || receiverShaderNames.Length == 0)
                    return false;

                renderer.GetSharedMaterials(k_SharedMaterials);
                try
                {
                    for (int i = 0; i < k_SharedMaterials.Count; ++i)
                    {
                        Material material = k_SharedMaterials[i];
                        Shader shader = material != null ? material.shader : null;
                        string shaderName = shader != null ? shader.name : null;
                        if (string.IsNullOrEmpty(shaderName))
                            continue;

                        for (int shaderIndex = 0; shaderIndex < receiverShaderNames.Length; ++shaderIndex)
                        {
                            string receiverShaderName = receiverShaderNames[shaderIndex];
                            if (!string.IsNullOrWhiteSpace(receiverShaderName)
                                && string.Equals(shaderName, receiverShaderName, StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
                finally
                {
                    k_SharedMaterials.Clear();
                }
            }

            private static Matrix4x4 BuildReflectionMatrix(float planeY)
            {
                // Reflect around the world plane y = planeY.
                return Matrix4x4.TRS(new Vector3(0f, 2f * planeY, 0f), Quaternion.identity, new Vector3(1f, -1f, 1f));
            }

            private static int GetMaterialPassIndex(Material material)
            {
                return MaterialPassResolver.ResolveFirstPass(
                    material,
                    k_RenderPassNames,
                    ReflectionMaterialPassQueryId,
                    fallbackToFirstPass: true,
                    maxEntries: MaterialPassCacheMaxEntries);
            }

            private static void ClearMaterialPassIndexCache()
            {
                MaterialPassResolver.ClearQuery(ReflectionMaterialPassQueryId);
            }

            private static Mesh ResolveMesh(Renderer renderer)
            {
                return RendererTraversalUtility.GetRendererMesh(renderer);
            }

            private static int GetRendererSubMeshCount(Renderer renderer)
            {
                return RendererTraversalUtility.GetSubMeshCount(renderer);
            }

            private static void DrawRendererWithCurrentView(Renderer renderer, RasterCommandBuffer cmd)
            {
                int subMeshCount = GetRendererSubMeshCount(renderer);
                if (subMeshCount <= 0)
                    return;

                k_SharedMaterials.Clear();
                renderer.GetSharedMaterials(k_SharedMaterials);
                if (k_SharedMaterials.Count == 0)
                    return;

                for (int subMesh = 0; subMesh < k_SharedMaterials.Count; ++subMesh)
                {
                    Material sourceMaterial = k_SharedMaterials[subMesh];
                    int passIndex = GetMaterialPassIndex(sourceMaterial);
                    if (passIndex < 0)
                        continue;

                    int subMeshIndex = Mathf.Min(subMesh, Mathf.Max(0, subMeshCount - 1));
                    cmd.DrawRenderer(renderer, sourceMaterial, subMeshIndex, passIndex);
                }
            }

            private static void ExecutePass(PassData data, RasterGraphContext context)
            {
                context.cmd.ClearRenderTarget(true, true, Color.clear);

                if (data.renderers == null || data.renderers.Length == 0)
                    return;

                try
                {
                    context.cmd.SetInvertCulling(true);
                    context.cmd.SetViewProjectionMatrices(data.cameraViewMatrix, data.cameraProjectionMatrix);
                    bool usingReflectedViewMatrix = false;

                    for (int i = 0; i < data.renderers.Length; ++i)
                    {
                        Renderer renderer = data.renderers[i];
                        if (!IsRenderableReflectionCaster(renderer, data.layerMask))
                            continue;

                        if (renderer is SkinnedMeshRenderer)
                        {
                            if (!usingReflectedViewMatrix)
                            {
                                context.cmd.SetViewProjectionMatrices(data.reflectedViewMatrix, data.cameraProjectionMatrix);
                                usingReflectedViewMatrix = true;
                            }

                            DrawRendererWithCurrentView(renderer, context.cmd);
                            continue;
                        }

                        if (usingReflectedViewMatrix)
                        {
                            context.cmd.SetViewProjectionMatrices(data.cameraViewMatrix, data.cameraProjectionMatrix);
                            usingReflectedViewMatrix = false;
                        }

                        Mesh mesh = ResolveMesh(renderer);
                        if (mesh == null || mesh.subMeshCount == 0)
                            continue;

                        Matrix4x4 reflectedMatrix = data.reflectionMatrix * renderer.localToWorldMatrix;

                        k_SharedMaterials.Clear();
                        renderer.GetSharedMaterials(k_SharedMaterials);
                        if (k_SharedMaterials.Count == 0)
                            continue;

                        for (int subMesh = 0; subMesh < k_SharedMaterials.Count; ++subMesh)
                        {
                            Material sourceMaterial = k_SharedMaterials[subMesh];
                            int passIndex = GetMaterialPassIndex(sourceMaterial);
                            if (passIndex < 0)
                                continue;

                            int subMeshIndex = Mathf.Min(subMesh, Mathf.Max(0, mesh.subMeshCount - 1));
                            context.cmd.DrawMesh(mesh, reflectedMatrix, sourceMaterial, subMeshIndex, passIndex);
                        }
                    }
                }
                finally
                {
                    context.cmd.SetViewProjectionMatrices(data.cameraViewMatrix, data.cameraProjectionMatrix);
                    context.cmd.SetInvertCulling(false);
                }
            }

            private static void ExecuteClearGlobalsPass(ClearGlobalsPassData data, RasterGraphContext context)
            {
                context.cmd.SetGlobalTexture(data.reflectionTextureId, data.fallbackTexture);
            }

            private static void RecordClearGlobalsPass(RenderGraph renderGraph, int reflectionTextureId)
            {
                using (var builder = renderGraph.AddRasterRenderPass<ClearGlobalsPassData>("HoyoToon Planar Reflection Globals", out var passData))
                {
                    passData.fallbackTexture = renderGraph.defaultResources.blackTexture;
                    passData.reflectionTextureId = reflectionTextureId;

                    builder.UseTexture(passData.fallbackTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(passData.fallbackTexture, reflectionTextureId);
                    builder.SetRenderFunc((ClearGlobalsPassData data, RasterGraphContext context) => ExecuteClearGlobalsPass(data, context));
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UnityScene renderScene = ResolveRenderScene(cameraData.camera);
                int layerMask = BuildLayerMask(settings.layerNames);
                Renderer[] targetRenderers = CollectRenderers(
                    renderScene,
                    layerMask,
                    out bool hasReflectionReceiver);
                string textureName = ResolveReflectionTextureName(settings);
                int reflectionTextureId = Shader.PropertyToID(textureName);

                // Only render reflections when both a receiver and at least one reflectable renderer exist.
                if (!hasReflectionReceiver || !HasAnyRenderableReflectionCaster(targetRenderers, layerMask))
                {
                    RecordClearGlobalsPass(renderGraph, reflectionTextureId);
                    return;
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("HoyoToon Planar Reflection", out var passData))
                {
                    RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
                    descriptor.width = Mathf.Max(1, settings.reflectionWidth);
                    descriptor.height = Mathf.Max(1, settings.reflectionHeight);
                    descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                    descriptor.colorFormat = RenderTextureFormat.ARGB32;
                    descriptor.depthStencilFormat = GraphicsFormat.None;
                    descriptor.depthBufferBits = 0;
                    descriptor.msaaSamples = 1;
                    descriptor.bindMS = false;
                    descriptor.enableRandomWrite = false;
                    descriptor.autoGenerateMips = false;
                    descriptor.useMipMap = false;

                    TextureHandle reflectionTexture = UniversalRenderer.CreateRenderGraphTexture(
                        renderGraph,
                        descriptor,
                        textureName,
                        false);

                    RenderTextureDescriptor depthDescriptor = descriptor;
                    depthDescriptor.graphicsFormat = GraphicsFormat.None;
                    depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                    depthDescriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;
                    depthDescriptor.depthBufferBits = 32;

                    TextureHandle reflectionDepthTexture = UniversalRenderer.CreateRenderGraphTexture(
                        renderGraph,
                        depthDescriptor,
                        "_ReflectionDepth",
                        false);

                    Camera camera = cameraData.camera;
                    Matrix4x4 cameraViewMatrix = camera != null ? camera.worldToCameraMatrix : Matrix4x4.identity;
                    Matrix4x4 cameraProjectionMatrix = camera != null ? camera.projectionMatrix : Matrix4x4.identity;
                    Matrix4x4 reflectionMatrix = BuildReflectionMatrix(settings.reflectionPlaneY);

                    passData.renderers = targetRenderers;
                    passData.reflectionMatrix = reflectionMatrix;
                    passData.reflectedViewMatrix = cameraViewMatrix * reflectionMatrix;
                    passData.cameraViewMatrix = cameraViewMatrix;
                    passData.cameraProjectionMatrix = cameraProjectionMatrix;
                    passData.layerMask = layerMask;

                    builder.SetRenderAttachment(reflectionTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(reflectionDepthTexture, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(reflectionTexture, reflectionTextureId);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
                }
            }

            static UnityScene ResolveRenderScene(Camera camera)
            {
                return RenderSceneUtility.ResolveRenderScene(camera);
            }

            public void Cleanup()
            {
                ClearMaterialPassIndexCache();
            }
        }

        protected override void Dispose(bool disposing)
        {
            m_ScriptablePass?.Cleanup();
            m_ScriptablePass = null;
        }
    }
}

