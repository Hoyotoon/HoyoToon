using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HoyoToon.Runtime.Rendering.Core
{
    public class HoyoToonPlanarReflection : ScriptableRendererFeature
    {
        private const string DefaultReflectionTextureName = "_ReflectionColor";
        private static readonly string[] DefaultReflectionReceiverShaderNames =
        {
            "HoyoToon/Honkai Star Rail/UI/Manikin/Floor"
        };

        [Serializable]
        public class HoyoToonPlanarReflectionSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingShadows;
            [Min(1)] public int reflectionWidth = 1024;
            [Min(1)] public int reflectionHeight = 1024;
            public float reflectionPlaneY = 0f;
            public string reflectionTextureName = DefaultReflectionTextureName;
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

        [SerializeField] private HoyoToonPlanarReflectionSettings settings = new HoyoToonPlanarReflectionSettings();
        private HoyoToonPlanarReflectionPass m_ScriptablePass;

        public override void Create()
        {
            if (settings == null)
                settings = new HoyoToonPlanarReflectionSettings();

            m_ScriptablePass?.Cleanup();
            m_ScriptablePass = null;
            m_ScriptablePass = new HoyoToonPlanarReflectionPass(settings)
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

        private class HoyoToonPlanarReflectionPass : ScriptableRenderPass
        {
            private static readonly List<Renderer> k_Renderers = new List<Renderer>(512);
            private static readonly List<Renderer> k_FilteredRenderers = new List<Renderer>(256);
            private static readonly List<Material> k_SharedMaterials = new List<Material>(16);
            private static readonly Dictionary<int, Mesh> k_BakedMeshCache = new Dictionary<int, Mesh>(128);
            private static readonly HashSet<int> k_CurrentBakedMeshKeys = new HashSet<int>();
            private static readonly List<int> k_StaleBakedMeshKeys = new List<int>(32);
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

            private readonly HoyoToonPlanarReflectionSettings settings;
            private int m_LastCollectedFrame = -1;
            private int m_LastLayerMask;
            private int m_LastReceiverShaderNamesHash;
            private bool m_CachedHasReflectionReceiver;
            private Renderer[] m_CachedRenderers = Array.Empty<Renderer>();

            private class PassData
            {
                public Renderer[] renderers;
                public Matrix4x4 reflectionMatrix;
            }

            public HoyoToonPlanarReflectionPass(HoyoToonPlanarReflectionSettings settings)
            {
                this.settings = settings;
            }

            private static string ResolveReflectionTextureName(HoyoToonPlanarReflectionSettings settings)
            {
                return settings == null || string.IsNullOrWhiteSpace(settings.reflectionTextureName)
                    ? DefaultReflectionTextureName
                    : settings.reflectionTextureName;
            }

            private static string[] ResolveReflectionReceiverShaderNames(HoyoToonPlanarReflectionSettings settings)
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

            private Renderer[] CollectRenderers(int layerMask, string[] receiverShaderNames, out bool hasReflectionReceiver)
            {
                int receiverShaderNamesHash = ComputeStringArrayHash(receiverShaderNames);
                if (m_LastCollectedFrame == Time.frameCount
                    && m_LastLayerMask == layerMask
                    && m_LastReceiverShaderNamesHash == receiverShaderNamesHash)
                {
                    hasReflectionReceiver = m_CachedHasReflectionReceiver;
                    return m_CachedRenderers;
                }

                m_LastCollectedFrame = Time.frameCount;
                m_LastLayerMask = layerMask;
                m_LastReceiverShaderNamesHash = receiverShaderNamesHash;
                m_CachedHasReflectionReceiver = false;
                k_FilteredRenderers.Clear();
                k_CurrentBakedMeshKeys.Clear();

#if UNITY_2023_1_OR_NEWER
                k_Renderers.Clear();
                Renderer[] allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                k_Renderers.AddRange(allRenderers);
#else
                k_Renderers.Clear();
                k_Renderers.AddRange(UnityEngine.Object.FindObjectsOfType<Renderer>());
#endif

                for (int i = 0; i < k_Renderers.Count; ++i)
                {
                    Renderer renderer = k_Renderers[i];
                    if (renderer == null || !renderer.enabled)
                        continue;

                    GameObject go = renderer.gameObject;
                    if (go == null || !go.activeInHierarchy)
                        continue;

                    if (RendererUsesReflectionReceiverShader(renderer, receiverShaderNames))
                    {
                        m_CachedHasReflectionReceiver = true;
                        break;
                    }
                }

                for (int i = 0; i < k_Renderers.Count; ++i)
                {
                    Renderer renderer = k_Renderers[i];
                    if (renderer == null || !renderer.enabled)
                        continue;

                    GameObject go = renderer.gameObject;
                    if (go == null || !go.activeInHierarchy)
                        continue;

                    if ((layerMask & (1 << go.layer)) == 0)
                        continue;

                    if (RendererUsesReflectionReceiverShader(renderer, receiverShaderNames))
                        continue;

                    k_FilteredRenderers.Add(renderer);
                    if (renderer is SkinnedMeshRenderer)
                        k_CurrentBakedMeshKeys.Add(renderer.GetInstanceID());
                }

                m_CachedRenderers = k_FilteredRenderers.ToArray();
                hasReflectionReceiver = m_CachedHasReflectionReceiver;
                PruneBakedMeshCache();
                return m_CachedRenderers;
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

            private static bool HasRenderableReflectionPass(Renderer renderer)
            {
                if (renderer == null)
                    return false;

                renderer.GetSharedMaterials(k_SharedMaterials);
                try
                {
                    for (int i = 0; i < k_SharedMaterials.Count; ++i)
                    {
                        if (GetMaterialPassIndex(k_SharedMaterials[i]) >= 0)
                            return true;
                    }

                    return false;
                }
                finally
                {
                    k_SharedMaterials.Clear();
                }
            }

            private static void PruneBakedMeshCache()
            {
                k_StaleBakedMeshKeys.Clear();
                foreach (var pair in k_BakedMeshCache)
                {
                    if (!k_CurrentBakedMeshKeys.Contains(pair.Key))
                        k_StaleBakedMeshKeys.Add(pair.Key);
                }

                for (int i = 0; i < k_StaleBakedMeshKeys.Count; ++i)
                {
                    int key = k_StaleBakedMeshKeys[i];
                    if (k_BakedMeshCache.TryGetValue(key, out Mesh mesh) && mesh != null)
                        CoreUtils.Destroy(mesh);

                    k_BakedMeshCache.Remove(key);
                }

                k_StaleBakedMeshKeys.Clear();
            }

            private static Matrix4x4 BuildReflectionMatrix(float planeY)
            {
                // Reflect around the world plane y = planeY.
                return Matrix4x4.TRS(new Vector3(0f, 2f * planeY, 0f), Quaternion.identity, new Vector3(1f, -1f, 1f));
            }

            private static int GetMaterialPassIndex(Material material)
            {
                if (material == null)
                    return -1;

                for (int i = 0; i < k_RenderPassNames.Length; ++i)
                {
                    int pass = material.FindPass(k_RenderPassNames[i]);
                    if (pass >= 0)
                        return pass;
                }

                return material.passCount > 0 ? 0 : -1;
            }

            private static Mesh GetBakedMesh(SkinnedMeshRenderer skinnedRenderer)
            {
                int key = skinnedRenderer.GetInstanceID();
                if (!k_BakedMeshCache.TryGetValue(key, out Mesh bakedMesh) || bakedMesh == null)
                {
                    bakedMesh = new Mesh
                    {
                        name = "HoyoToonPlanarReflection_BakedMesh"
                    };
                    bakedMesh.MarkDynamic();
                    k_BakedMeshCache[key] = bakedMesh;
                }

                bakedMesh.Clear();
                skinnedRenderer.BakeMesh(bakedMesh);
                return bakedMesh;
            }

            private static Mesh ResolveMesh(Renderer renderer)
            {
                if (renderer is MeshRenderer meshRenderer)
                {
                    MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                    return filter != null ? filter.sharedMesh : null;
                }

                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                    return GetBakedMesh(skinnedRenderer);

                return null;
            }

            private static void ExecutePass(PassData data, RasterGraphContext context)
            {
                context.cmd.ClearRenderTarget(true, true, Color.clear);

                if (data.renderers == null || data.renderers.Length == 0)
                    return;

                try
                {
                    context.cmd.SetInvertCulling(true);

                    for (int i = 0; i < data.renderers.Length; ++i)
                    {
                        Renderer renderer = data.renderers[i];
                        if (renderer == null)
                            continue;

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
                    context.cmd.SetInvertCulling(false);
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                int layerMask = BuildLayerMask(settings.layerNames);
                Renderer[] targetRenderers = CollectRenderers(
                    layerMask,
                    ResolveReflectionReceiverShaderNames(settings),
                    out bool hasReflectionReceiver);
                string textureName = ResolveReflectionTextureName(settings);
                int reflectionTextureId = Shader.PropertyToID(textureName);

                // Only render reflections when both a receiver and at least one reflectable renderer exist.
                if (!hasReflectionReceiver || targetRenderers.Length == 0)
                {
                    Shader.SetGlobalTexture(reflectionTextureId, Texture2D.blackTexture);
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

                    passData.renderers = targetRenderers;
                    passData.reflectionMatrix = BuildReflectionMatrix(settings.reflectionPlaneY);

                    builder.SetRenderAttachment(reflectionTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(reflectionDepthTexture, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(reflectionTexture, reflectionTextureId);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
                }
            }

            public void Cleanup()
            {
                foreach (var pair in k_BakedMeshCache)
                {
                    if (pair.Value != null)
                        CoreUtils.Destroy(pair.Value);
                }

                k_BakedMeshCache.Clear();
            }
        }

        protected override void Dispose(bool disposing)
        {
            m_ScriptablePass?.Cleanup();
            m_ScriptablePass = null;
        }
    }
}
