using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Rendering.Utilities;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Rendering.HSR
{
    public class CharacterManikinAreaFloorShadow : ScriptableRendererFeature
    {
        [SerializeField] CharacterManikinAreaFloorShadowSettings settings = new CharacterManikinAreaFloorShadowSettings();

        CharacterManikinAreaFloorShadowPass m_Pass;

        public override void Create()
        {
            settings ??= new CharacterManikinAreaFloorShadowSettings();
            m_Pass = new CharacterManikinAreaFloorShadowPass(settings);
            m_Pass.renderPassEvent = settings.renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType cameraType = renderingData.cameraData.cameraType;
            bool supportedCamera = !renderingData.cameraData.isPreviewCamera
                && (cameraType == CameraType.Game || cameraType == CameraType.SceneView);

            if (!supportedCamera)
                return;

            renderer.EnqueuePass(m_Pass);
        }

        [Serializable]
        public class CharacterManikinAreaFloorShadowSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            [Min(256)] public int maxAtlasResolution = 2048;
            [Range(1, 4)] public int maxCharacters = 4;
            [Range(1, 3)] public int slicesPerCharacter = 3;
            [Min(64)] public int slotResolution = 512;
            [Range(0f, 1f)] public float lightFollowAmount = 0.65f;
            public bool firstSliceUsesSceneLight = true;
            [Range(-89f, 89f)] public float firstSliceFakePitch = 0f;
            [Range(0f, 360f)] public float firstSliceFakeYaw = 0f;
            [Range(-180f, 180f)] public float firstSliceFakeRoll = 0f;
            [Range(-180f, 180f)] public float secondSliceYawOffset = 0f;
            [Range(-180f, 180f)] public float thirdSliceYawOffset = 0f;
            [Min(0.0001f)] public float nearPlane = 0.01f;
            [Min(0.001f)] public float farPlane = 2f;
            [Min(0f)] public float floorProjectionDistance = 3f;
            [Min(0.01f)] public float minOrthographicSize = 0.01f;
            [Range(0f, 1f)] public float shadowStrength = 0.9f;
            [Min(0.1f)] public float pcssSearchRadius = 2.0f;
            [Min(0.1f)] public float pcssMinFilterRadius = 0.8f;
            [Min(0.1f)] public float pcssMaxFilterRadius = 4.0f;
            [Min(0f)] public float radialBlurStart = 0.0f;
            [Min(0.001f)] public float radialBlurEnd = 2.5f;
            [Range(0f, 3f)] public float radialBlurStrength = 1.0f;
            [Min(0f)] public float radialFadeStart = 1.0f;
            [Min(0.001f)] public float radialFadeEnd = 3.0f;
            [Min(0.00001f)] public float floorDepthThreshold = 0.0025f;
        }

        class CharacterManikinAreaFloorShadowPass : ScriptableRenderPass
        {
            const string k_LightModeTagName = "LightMode";
            const string k_ReceiverLightModeName = "ManikinShadowReceiver";
            const string k_DrawDepthLightModeName = "ManikinDrawDepth";
            const string k_AtlasPassName = "Character Manikin Raw Shadow Atlas";
            const string k_DepthPassName = "Character Manikin Floor Depth";
            const string k_ReceiverPassName = "Character Manikin Receiver";
            const string k_BindGlobalsPassName = "Character Manikin Shadow Globals";

            const int k_MaxSlots = 12;
            const int k_GlobalSnapshotRingSize = 8;
            const int k_PassMissing = -1;
            const int k_PassExcluded = -2;
            const int k_MaterialPassResolverId = 2;
            const int k_LegacyReceiverMissCheckInterval = 30;

            static readonly int k_ManikinRawShadowAtlasId = Shader.PropertyToID("_ManikinRawShadowAtlas");
            static readonly int k_ManikinDepthId = Shader.PropertyToID("_ManikinDepth");
            static readonly int k_ManikinShadowId = Shader.PropertyToID("_ManikinShadow");
            static readonly int k_ManikinWorldToShadowArrId = Shader.PropertyToID("_ManikinWorldToShadowArr");
            static readonly int k_ManikinShadowAtlasRectArrId = Shader.PropertyToID("_ManikinShadowAtlasRectArr");
            static readonly int k_ManikinShadowOriginArrId = Shader.PropertyToID("_ManikinShadowOriginArr");
            static readonly int k_ManikinSlotCountId = Shader.PropertyToID("_ManikinShadowSlotCount");
            static readonly int k_ManikinRawShadowAtlasTexelSizeId = Shader.PropertyToID("_ManikinRawShadowAtlasTexelSize");
            static readonly int k_ManikinShadowStrengthId = Shader.PropertyToID("_ManikinShadowStrength");
            static readonly int k_ManikinPcssSearchRadiusId = Shader.PropertyToID("_ManikinPcssSearchRadius");
            static readonly int k_ManikinPcssMinFilterRadiusId = Shader.PropertyToID("_ManikinPcssMinFilterRadius");
            static readonly int k_ManikinPcssMaxFilterRadiusId = Shader.PropertyToID("_ManikinPcssMaxFilterRadius");
            static readonly int k_ManikinRadialBlurStartId = Shader.PropertyToID("_ManikinRadialBlurStart");
            static readonly int k_ManikinRadialBlurEndId = Shader.PropertyToID("_ManikinRadialBlurEnd");
            static readonly int k_ManikinRadialBlurStrengthId = Shader.PropertyToID("_ManikinRadialBlurStrength");
            static readonly int k_ManikinRadialFadeStartId = Shader.PropertyToID("_ManikinRadialFadeStart");
            static readonly int k_ManikinRadialFadeEndId = Shader.PropertyToID("_ManikinRadialFadeEnd");
            static readonly int k_ManikinFloorDepthThresholdId = Shader.PropertyToID("_ManikinFloorDepthThreshold");
            static readonly int k_LightDirectionId = Shader.PropertyToID("_LightDirection");

            static readonly ShaderTagId k_LightModeTag = new ShaderTagId(k_LightModeTagName);
            static readonly ShaderTagId k_ManikinReceiverTag = new ShaderTagId(k_ReceiverLightModeName);
            static readonly ShaderTagId k_ManikinDepthTag = new ShaderTagId(k_DrawDepthLightModeName);
            static readonly ShaderTagId k_HsrPerObjectShadowCasterTag = new ShaderTagId("HSRPerObjectShadowCaster");
            static readonly ShaderTagId k_ShadowCasterTag = new ShaderTagId("ShadowCaster");
            static readonly ShaderTagId k_ShadowCasterUpperTag = new ShaderTagId("SHADOWCASTER");
            static readonly ShaderTagId k_DepthOnlyTag = new ShaderTagId("DepthOnly");
            static readonly ShaderTagId k_DepthNormalsOnlyTag = new ShaderTagId("DepthNormalsOnly");
            static readonly ShaderTagId k_DepthNormalsTag = new ShaderTagId("DepthNormals");
            static readonly ShaderTagId[] k_HsrPerObjectShadowCasterPassTags = { k_HsrPerObjectShadowCasterTag };
            static readonly ShaderTagId[] k_ShadowCasterPassTags = { k_ShadowCasterTag, k_ShadowCasterUpperTag };
            static readonly ShaderTagId[] k_DepthFallbackPassTags = { k_DepthOnlyTag, k_DepthNormalsOnlyTag, k_DepthNormalsTag };

            static readonly string[] k_FallbackCasterPassNames =
            {
                "ShadowCaster",
                "DepthOnly",
                "DepthNormalsOnly",
                "DepthNormals"
            };

            static readonly Matrix4x4[] k_WorldToShadowArray =
            {
                Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity,
                Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity,
                Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity,
                Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity,
            };

            static readonly Vector4[] k_ShadowAtlasRectArray =
            {
                Vector4.zero, Vector4.zero, Vector4.zero,
                Vector4.zero, Vector4.zero, Vector4.zero,
                Vector4.zero, Vector4.zero, Vector4.zero,
                Vector4.zero, Vector4.zero, Vector4.zero,
            };

            static readonly Vector4[] k_ShadowOriginArray =
            {
                Vector4.zero, Vector4.zero, Vector4.zero,
                Vector4.zero, Vector4.zero, Vector4.zero,
                Vector4.zero, Vector4.zero, Vector4.zero,
                Vector4.zero, Vector4.zero, Vector4.zero,
            };

            static readonly GlobalArraySnapshot[] k_GlobalSnapshotRing = CreateGlobalSnapshotRing();
            static readonly List<HSRCharacterController> k_ControllerScratch = new List<HSRCharacterController>(8);
            static readonly List<Material> k_MaterialScratch = new List<Material>(8);
            static readonly List<int> k_PassIndexScratch = new List<int>(8);
            static readonly List<CullingCandidate> k_CandidateScratch = new List<CullingCandidate>(16);
            static readonly List<GameObject> k_RootScratch = new List<GameObject>(16);
            static readonly List<Renderer> k_RendererScratch = new List<Renderer>(128);
            static readonly List<ShaderTagId> k_ReceiverPassTagList = new List<ShaderTagId> { k_ManikinReceiverTag };
            static readonly List<ShaderTagId> k_DepthPassTagList = new List<ShaderTagId> { k_ManikinDepthTag };
            static readonly Dictionary<HsrRendererMaterialQueryUtility.MaterialPassCacheKey, int> k_MaterialPassIndexCache =
                new Dictionary<HsrRendererMaterialQueryUtility.MaterialPassCacheKey, int>(128);
            static int k_ObservedMaterialPassCacheVersion;
            static int k_NextGlobalSnapshotIndex;

            readonly CharacterManikinAreaFloorShadowSettings m_Settings;
            bool m_CachedLegacyHasReceiverPass;
            int m_LastLegacyReceiverPassCheckFrame = -k_LegacyReceiverMissCheckInterval;
            int m_LastLegacyReceiverPassSceneHandle;

            struct SlotData
            {
                public Renderer[] renderers;
                public string casterPassName;
                public Matrix4x4 viewMatrix;
                public Matrix4x4 projectionMatrix;
                public Vector4 lightDirection;
                public Rect viewport;
            }

            struct CullingCandidate
            {
                public HSRCharacterController controller;
                public Renderer[] renderers;
                public string casterPassName;
                public Bounds bounds;
                public float priority;
            }

            class AtlasPassData
            {
                public SlotData[] slots;
                public Matrix4x4 cameraView;
                public Matrix4x4 cameraProjection;
            }

            class DepthPassData
            {
                public RendererListHandle rendererList;
            }

            class ReceiverPassData
            {
                public RendererListHandle rendererList;
                public TextureHandle rawAtlas;
                public TextureHandle floorDepth;
                public TextureHandle fallbackTexture;
            }

            class BindGlobalsPassData
            {
                public TextureHandle rawAtlas;
                public TextureHandle floorDepth;
                public TextureHandle receiverShadow;
                public TextureHandle fallbackTexture;
                public GlobalArraySnapshot globalsSnapshot;
                public int slotCount;
                public Vector4 rawShadowAtlasTexelSize;
                public float shadowStrength;
                public float pcssSearchRadius;
                public float pcssMinFilterRadius;
                public float pcssMaxFilterRadius;
                public float radialBlurStart;
                public float radialBlurEnd;
                public float radialBlurStrength;
                public float radialFadeStart;
                public float radialFadeEnd;
                public float floorDepthThreshold;
            }

            sealed class GlobalArraySnapshot
            {
                public readonly Matrix4x4[] worldToShadowArray = new Matrix4x4[k_MaxSlots];
                public readonly Vector4[] shadowAtlasRectArray = new Vector4[k_MaxSlots];
                public readonly Vector4[] shadowOriginArray = new Vector4[k_MaxSlots];
            }

            public CharacterManikinAreaFloorShadowPass(CharacterManikinAreaFloorShadowSettings settings)
            {
                m_Settings = settings;
            }

            internal static UnityScene ResolveRenderScene(Camera camera)
            {
                return RenderSceneUtility.ResolveRenderScene(camera);
            }

            public bool HasSceneReceiverPass(UnityScene scene)
            {
                if (RenderParticipantRegistry.HasManikinShadowReceiver(scene))
                    return true;

                return HasLegacySceneReceiverPass(scene);
            }

            bool HasLegacySceneReceiverPass(UnityScene scene)
            {
                int sceneHandle = RenderSceneUtility.GetSceneHandleOrDefault(scene);
                if (m_LastLegacyReceiverPassSceneHandle == sceneHandle)
                {
                    if (m_CachedLegacyHasReceiverPass
                        || Time.frameCount - m_LastLegacyReceiverPassCheckFrame < k_LegacyReceiverMissCheckInterval)
                    {
                        return m_CachedLegacyHasReceiverPass;
                    }
                }

                m_LastLegacyReceiverPassSceneHandle = sceneHandle;
                m_LastLegacyReceiverPassCheckFrame = Time.frameCount;
                m_CachedLegacyHasReceiverPass = false;
                k_RootScratch.Clear();

                if (!RenderSceneUtility.IsSceneUsable(scene))
                    return false;

                scene.GetRootGameObjects(k_RootScratch);
                for (int rootIndex = 0; rootIndex < k_RootScratch.Count; ++rootIndex)
                {
                    GameObject root = k_RootScratch[rootIndex];
                    if (root == null)
                        continue;

                    k_RendererScratch.Clear();
                    root.GetComponentsInChildren(true, k_RendererScratch);
                    for (int rendererIndex = 0; rendererIndex < k_RendererScratch.Count; ++rendererIndex)
                    {
                        Renderer renderer = k_RendererScratch[rendererIndex];
                        if (!PlanarReflectionParticipant.IsActiveRenderer(renderer))
                            continue;

                        if (!HsrRendererMaterialQueryUtility.TryGetSharedMaterials(renderer, k_MaterialScratch, out int materialCount))
                            continue;

                        try
                        {
                            for (int m = 0; m < materialCount; ++m)
                            {
                                Material material = k_MaterialScratch[m];
                                if (!HsrRendererMaterialQueryUtility.HasMaterialTagOrShaderPassOrNamedPass(
                                        material,
                                        k_LightModeTagName,
                                        k_LightModeTag,
                                        k_ReceiverLightModeName,
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                m_CachedLegacyHasReceiverPass = true;
                                k_RootScratch.Clear();
                                k_RendererScratch.Clear();
                                return true;
                            }
                        }
                        finally
                        {
                            k_MaterialScratch.Clear();
                        }
                    }
                }

                k_RootScratch.Clear();
                k_RendererScratch.Clear();
                return false;
            }

            static void ResetGlobalArrays()
            {
                for (int i = 0; i < k_MaxSlots; ++i)
                {
                    k_WorldToShadowArray[i] = Matrix4x4.identity;
                    k_ShadowAtlasRectArray[i] = Vector4.zero;
                    k_ShadowOriginArray[i] = Vector4.zero;
                }
            }

            static GlobalArraySnapshot[] CreateGlobalSnapshotRing()
            {
                var ring = new GlobalArraySnapshot[k_GlobalSnapshotRingSize];
                for (int i = 0; i < ring.Length; ++i)
                    ring[i] = new GlobalArraySnapshot();

                return ring;
            }

            static GlobalArraySnapshot CaptureGlobalArraySnapshot()
            {
                GlobalArraySnapshot snapshot = k_GlobalSnapshotRing[k_NextGlobalSnapshotIndex];
                k_NextGlobalSnapshotIndex = (k_NextGlobalSnapshotIndex + 1) % k_GlobalSnapshotRing.Length;

                Array.Copy(k_WorldToShadowArray, snapshot.worldToShadowArray, k_MaxSlots);
                Array.Copy(k_ShadowAtlasRectArray, snapshot.shadowAtlasRectArray, k_MaxSlots);
                Array.Copy(k_ShadowOriginArray, snapshot.shadowOriginArray, k_MaxSlots);
                return snapshot;
            }

            static int GetSubMeshCount(Renderer renderer)
            {
                return Mathf.Max(1, RendererTraversalUtility.GetSubMeshCount(renderer));
            }

            static int FindCasterPassIndex(Material material, string preferredPassName)
            {
                if (material == null)
                    return k_PassMissing;

                SyncMaterialPassCacheVersion();
                int excludedStateVersion = GetMaterialPassExcludedStateVersion(material);
                if (HsrRendererMaterialQueryUtility.TryGetCachedMaterialPassIndex(
                        k_MaterialPassIndexCache,
                        material,
                        preferredPassName,
                        k_MaterialPassResolverId,
                        excludedStateVersion,
                        out int cachedPassIndex))
                {
                    return cachedPassIndex;
                }

                int passIndex = ResolveCasterPassIndexUncached(material, preferredPassName);
                HsrRendererMaterialQueryUtility.StoreCachedMaterialPassIndex(
                    k_MaterialPassIndexCache,
                    material,
                    preferredPassName,
                    k_MaterialPassResolverId,
                    excludedStateVersion,
                    passIndex);

                return passIndex;
            }

            static int ResolveCasterPassIndexUncached(Material material, string preferredPassName)
            {
                if (material == null)
                    return k_PassMissing;

                if (material.renderQueue > (int)RenderQueue.GeometryLast)
                    return k_PassExcluded;

                Shader shader = material.shader;
                if (shader == null)
                    return k_PassMissing;

                if (!string.IsNullOrEmpty(preferredPassName))
                {
                    int preferredPassIndex = MaterialPassResolver.ResolveNamedPass(material, preferredPassName);
                    if (preferredPassIndex >= 0)
                        return preferredPassIndex;
                }

                int hsrPassByTag = HsrRendererMaterialQueryUtility.FindFirstShaderPassWithAnyTagValue(material, k_LightModeTag, k_HsrPerObjectShadowCasterPassTags);
                if (hsrPassByTag >= 0)
                    return hsrPassByTag;

                int hsrPass = MaterialPassResolver.ResolveNamedPass(material, "HSRPerObjectShadowCaster");
                if (hsrPass >= 0)
                    return hsrPass;

                int shadowCasterByTag = HsrRendererMaterialQueryUtility.FindFirstShaderPassWithAnyTagValue(material, k_LightModeTag, k_ShadowCasterPassTags);
                if (shadowCasterByTag >= 0)
                    return shadowCasterByTag;

                int shadowCasterPass = MaterialPassResolver.ResolveNamedPass(material, "ShadowCaster");
                if (shadowCasterPass >= 0)
                    return shadowCasterPass;

                int depthFallbackPass = HsrRendererMaterialQueryUtility.FindFirstShaderPassWithAnyTagValue(material, k_LightModeTag, k_DepthFallbackPassTags);
                if (depthFallbackPass >= 0)
                    return depthFallbackPass;

                for (int i = 0; i < k_FallbackCasterPassNames.Length; ++i)
                {
                    int passIndex = MaterialPassResolver.ResolveNamedPass(material, k_FallbackCasterPassNames[i]);
                    if (passIndex >= 0)
                        return passIndex;
                }

                return k_PassMissing;
            }

            static int GetMaterialPassExcludedStateVersion(Material material)
            {
                return material != null ? material.renderQueue : 0;
            }

            static void SyncMaterialPassCacheVersion()
            {
                int cacheVersion = HsrRendererMaterialQueryUtility.MaterialPassCacheVersion;
                if (k_ObservedMaterialPassCacheVersion == cacheVersion)
                    return;

                k_MaterialPassIndexCache.Clear();
                k_ObservedMaterialPassCacheVersion = cacheVersion;
            }

            static bool HasCaster(Renderer[] renderers, string preferredPassName)
            {
                if (renderers == null)
                    return false;

                for (int r = 0; r < renderers.Length; ++r)
                {
                    Renderer renderer = renderers[r];
                    if (!RendererTraversalUtility.IsRendererActive(renderer))
                        continue;

                    if (!HsrRendererMaterialQueryUtility.TryGetSharedMaterials(renderer, k_MaterialScratch, out int materialCount))
                        continue;

                    try
                    {
                        for (int m = 0; m < materialCount; ++m)
                        {
                            if (FindCasterPassIndex(k_MaterialScratch[m], preferredPassName) >= 0)
                                return true;
                        }
                    }
                    finally
                    {
                        k_MaterialScratch.Clear();
                    }
                }

                return false;
            }

            static Matrix4x4 BuildClipToTextureMatrix()
            {
                Matrix4x4 clipToTexture = Matrix4x4.identity;
                clipToTexture.m00 = 0.5f;
                clipToTexture.m03 = 0.5f;
                clipToTexture.m11 = 0.5f;
                clipToTexture.m13 = 0.5f;
                clipToTexture.m22 = 0.5f;
                clipToTexture.m23 = 0.5f;
                return clipToTexture;
            }

            static Matrix4x4 BuildAtlasScaleBiasMatrix(Vector4 atlasRect)
            {
                Matrix4x4 matrix = Matrix4x4.identity;
                matrix.m00 = atlasRect.z;
                matrix.m03 = atlasRect.x;
                matrix.m11 = atlasRect.w;
                matrix.m13 = atlasRect.y;
                return matrix;
            }

            static Vector3 BuildBaseSliceDirection(CharacterManikinAreaFloorShadowSettings settings)
            {
                if (settings.firstSliceUsesSceneLight)
                {
                    Light sun = RenderSettings.sun;
                    if (sun != null)
                        return sun.transform.forward;
                }

                Quaternion fakeRotation = Quaternion.Euler(
                    settings.firstSliceFakePitch,
                    settings.firstSliceFakeYaw,
                    settings.firstSliceFakeRoll);
                return fakeRotation * Vector3.forward;
            }

            static float GetSliceYawOffset(CharacterManikinAreaFloorShadowSettings settings, int sliceIndex)
            {
                switch (sliceIndex)
                {
                    case 1:
                        return settings.secondSliceYawOffset;
                    case 2:
                        return settings.thirdSliceYawOffset;
                    default:
                        return 0f;
                }
            }

            static Vector3 BuildSliceDirection(CharacterManikinAreaFloorShadowSettings settings, Vector3 baseDirection, int sliceIndex, int sliceCount)
            {
                if (sliceCount <= 1)
                    return baseDirection;

                if (sliceIndex <= 0)
                    return baseDirection;

                float angle = 360f * ((float)sliceIndex / sliceCount);
                angle += GetSliceYawOffset(settings, sliceIndex);
                Quaternion yaw = Quaternion.AngleAxis(angle, Vector3.up);
                return yaw * baseDirection;
            }

            static void ExecuteAtlasPass(AtlasPassData data, RasterGraphContext context)
            {
                context.cmd.ClearRenderTarget(true, false, Color.clear);
                context.cmd.SetGlobalDepthBias(1.0f, 2.5f);

                SlotData[] slots = data.slots;
                if (slots == null || slots.Length == 0)
                {
                    context.cmd.SetGlobalDepthBias(0f, 0f);
                    return;
                }

                for (int i = 0; i < slots.Length; ++i)
                {
                    SlotData slot = slots[i];

                    context.cmd.SetGlobalVector(k_LightDirectionId, slot.lightDirection);
                    context.cmd.SetViewport(slot.viewport);
                    context.cmd.SetViewProjectionMatrices(slot.viewMatrix, slot.projectionMatrix);

                    Renderer[] renderers = slot.renderers;
                    if (renderers == null)
                        continue;

                    for (int r = 0; r < renderers.Length; ++r)
                    {
                        Renderer renderer = renderers[r];
                        if (!RendererTraversalUtility.IsRendererActive(renderer))
                            continue;

                        if (!HsrRendererMaterialQueryUtility.TryGetSharedMaterials(renderer, k_MaterialScratch, out int materialCount))
                            continue;

                        try
                        {
                            int subMeshCount = GetSubMeshCount(renderer);
                            if (subMeshCount <= 0)
                                continue;

                            k_PassIndexScratch.Clear();
                            int fallbackMaterialIndex = -1;
                            for (int m = 0; m < materialCount; ++m)
                            {
                                Material material = k_MaterialScratch[m];
                                int passIndex = FindCasterPassIndex(material, slot.casterPassName);
                                k_PassIndexScratch.Add(passIndex);
                                if (fallbackMaterialIndex < 0 && passIndex >= 0)
                                    fallbackMaterialIndex = m;
                            }

                            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; ++subMeshIndex)
                            {
                                int materialIndex = Mathf.Min(subMeshIndex, materialCount - 1);
                                Material material = k_MaterialScratch[materialIndex];
                                int passIndex = k_PassIndexScratch[materialIndex];

                                if (passIndex == k_PassMissing && fallbackMaterialIndex >= 0)
                                {
                                    material = k_MaterialScratch[fallbackMaterialIndex];
                                    passIndex = k_PassIndexScratch[fallbackMaterialIndex];
                                }

                                if (passIndex < 0)
                                    continue;

                                context.cmd.DrawRenderer(renderer, material, subMeshIndex, passIndex);
                            }
                        }
                        finally
                        {
                            k_MaterialScratch.Clear();
                            k_PassIndexScratch.Clear();
                        }
                    }
                }

                context.cmd.SetGlobalDepthBias(0f, 0f);
                context.cmd.SetViewProjectionMatrices(data.cameraView, data.cameraProjection);
            }

            static void ExecuteDepthPass(DepthPassData data, RasterGraphContext context)
            {
                context.cmd.ClearRenderTarget(false, true, new Color(1e8f, 0f, 0f, 0f));
                context.cmd.DrawRendererList(data.rendererList);
            }

            static void ExecuteReceiverPass(ReceiverPassData data, RasterGraphContext context)
            {
                if (data.rawAtlas.IsValid())
                    context.cmd.SetGlobalTexture(k_ManikinRawShadowAtlasId, data.rawAtlas, RenderTextureSubElement.Depth);
                else
                    context.cmd.SetGlobalTexture(k_ManikinRawShadowAtlasId, data.fallbackTexture);

                if (data.floorDepth.IsValid())
                    context.cmd.SetGlobalTexture(k_ManikinDepthId, data.floorDepth);
                else
                    context.cmd.SetGlobalTexture(k_ManikinDepthId, data.fallbackTexture);

                context.cmd.DrawRendererList(data.rendererList);
            }

            static void ExecuteBindGlobalsPass(BindGlobalsPassData data, RasterGraphContext context)
            {
                if (data.rawAtlas.IsValid())
                    context.cmd.SetGlobalTexture(k_ManikinRawShadowAtlasId, data.rawAtlas, RenderTextureSubElement.Depth);
                else
                    context.cmd.SetGlobalTexture(k_ManikinRawShadowAtlasId, data.fallbackTexture);

                if (data.floorDepth.IsValid())
                    context.cmd.SetGlobalTexture(k_ManikinDepthId, data.floorDepth);
                else
                    context.cmd.SetGlobalTexture(k_ManikinDepthId, data.fallbackTexture);

                if (data.receiverShadow.IsValid())
                    context.cmd.SetGlobalTexture(k_ManikinShadowId, data.receiverShadow);
                else
                    context.cmd.SetGlobalTexture(k_ManikinShadowId, data.fallbackTexture);

                GlobalArraySnapshot snapshot = data.globalsSnapshot;
                context.cmd.SetGlobalMatrixArray(k_ManikinWorldToShadowArrId, snapshot.worldToShadowArray);
                context.cmd.SetGlobalVectorArray(k_ManikinShadowAtlasRectArrId, snapshot.shadowAtlasRectArray);
                context.cmd.SetGlobalVectorArray(k_ManikinShadowOriginArrId, snapshot.shadowOriginArray);
                context.cmd.SetGlobalFloat(k_ManikinSlotCountId, data.slotCount);
                context.cmd.SetGlobalVector(k_ManikinRawShadowAtlasTexelSizeId, data.rawShadowAtlasTexelSize);
                context.cmd.SetGlobalFloat(k_ManikinShadowStrengthId, data.shadowStrength);
                context.cmd.SetGlobalFloat(k_ManikinPcssSearchRadiusId, data.pcssSearchRadius);
                context.cmd.SetGlobalFloat(k_ManikinPcssMinFilterRadiusId, data.pcssMinFilterRadius);
                context.cmd.SetGlobalFloat(k_ManikinPcssMaxFilterRadiusId, data.pcssMaxFilterRadius);
                context.cmd.SetGlobalFloat(k_ManikinRadialBlurStartId, data.radialBlurStart);
                context.cmd.SetGlobalFloat(k_ManikinRadialBlurEndId, data.radialBlurEnd);
                context.cmd.SetGlobalFloat(k_ManikinRadialBlurStrengthId, data.radialBlurStrength);
                context.cmd.SetGlobalFloat(k_ManikinRadialFadeStartId, data.radialFadeStart);
                context.cmd.SetGlobalFloat(k_ManikinRadialFadeEndId, data.radialFadeEnd);
                context.cmd.SetGlobalFloat(k_ManikinFloorDepthThresholdId, data.floorDepthThreshold);
            }

            void PopulateGlobalsPassData(
                BindGlobalsPassData passData,
                RenderGraph renderGraph,
                TextureHandle rawAtlas,
                TextureHandle floorDepth,
                TextureHandle receiverShadow,
                GlobalArraySnapshot globalsSnapshot,
                int slotCount,
                Vector4 rawShadowAtlasTexelSize)
            {
                passData.rawAtlas = rawAtlas;
                passData.floorDepth = floorDepth;
                passData.receiverShadow = receiverShadow;
                passData.fallbackTexture = renderGraph.defaultResources.blackTexture;
                passData.globalsSnapshot = globalsSnapshot;
                passData.slotCount = slotCount;
                passData.rawShadowAtlasTexelSize = rawShadowAtlasTexelSize;
                passData.shadowStrength = m_Settings.shadowStrength;
                passData.pcssSearchRadius = m_Settings.pcssSearchRadius;
                passData.pcssMinFilterRadius = m_Settings.pcssMinFilterRadius;
                passData.pcssMaxFilterRadius = m_Settings.pcssMaxFilterRadius;
                passData.radialBlurStart = m_Settings.radialBlurStart;
                passData.radialBlurEnd = m_Settings.radialBlurEnd;
                passData.radialBlurStrength = m_Settings.radialBlurStrength;
                passData.radialFadeStart = m_Settings.radialFadeStart;
                passData.radialFadeEnd = m_Settings.radialFadeEnd;
                passData.floorDepthThreshold = m_Settings.floorDepthThreshold;
            }

            void RecordBindGlobalsPass(
                RenderGraph renderGraph,
                TextureHandle rawAtlas,
                TextureHandle floorDepth,
                TextureHandle receiverShadow,
                GlobalArraySnapshot globalsSnapshot,
                int slotCount,
                Vector4 rawShadowAtlasTexelSize)
            {
                using (var builder = renderGraph.AddRasterRenderPass<BindGlobalsPassData>(k_BindGlobalsPassName, out var passData))
                {
                    PopulateGlobalsPassData(
                        passData,
                        renderGraph,
                        rawAtlas,
                        floorDepth,
                        receiverShadow,
                        globalsSnapshot,
                        slotCount,
                        rawShadowAtlasTexelSize);

                    if (rawAtlas.IsValid())
                        builder.UseTexture(rawAtlas, AccessFlags.Read);
                    if (floorDepth.IsValid())
                        builder.UseTexture(floorDepth, AccessFlags.Read);
                    if (receiverShadow.IsValid())
                        builder.UseTexture(receiverShadow, AccessFlags.Read);
                    builder.UseTexture(passData.fallbackTexture, AccessFlags.Read);

                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(
                        rawAtlas.IsValid() ? rawAtlas : passData.fallbackTexture,
                        k_ManikinRawShadowAtlasId);
                    builder.SetGlobalTextureAfterPass(
                        floorDepth.IsValid() ? floorDepth : passData.fallbackTexture,
                        k_ManikinDepthId);
                    builder.SetGlobalTextureAfterPass(
                        receiverShadow.IsValid() ? receiverShadow : passData.fallbackTexture,
                        k_ManikinShadowId);
                    builder.SetRenderFunc((BindGlobalsPassData data, RasterGraphContext context) => ExecuteBindGlobalsPass(data, context));
                }
            }

            void RecordDisabledGlobalsPass(RenderGraph renderGraph)
            {
                ResetGlobalArrays();
                RecordBindGlobalsPass(
                    renderGraph,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    TextureHandle.nullHandle,
                    CaptureGlobalArraySnapshot(),
                    0,
                    new Vector4(1f, 1f, 1f, 1f));
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                Camera camera = cameraData.camera;

                CameraType cameraType = cameraData.cameraType;
                bool supportedCamera = !cameraData.isPreviewCamera
                    && (cameraType == CameraType.Game || cameraType == CameraType.SceneView);
                UnityScene renderScene = ResolveRenderScene(camera);

                if (camera == null || !supportedCamera || !HasSceneReceiverPass(renderScene))
                {
                    RecordDisabledGlobalsPass(renderGraph);
                    return;
                }

                HSRCharacterController.GetRegisteredActiveControllersInScene(renderScene, k_ControllerScratch);
                k_CandidateScratch.Clear();

                for (int i = 0; i < k_ControllerScratch.Count; ++i)
                {
                    HSRCharacterController controller = k_ControllerScratch[i];
                    if (controller == null || !controller.EnableCharacterSelfShadow)
                        continue;

                    Renderer[] scopedRenderers = controller.GetScopedRenderers();
                    if (scopedRenderers == null || scopedRenderers.Length == 0)
                        continue;

                    string casterPassName = controller.CharacterSelfShadowCasterPassName;
                    if (!HasCaster(scopedRenderers, casterPassName))
                        continue;

                    if (!controller.TryGetCharacterSelfShadowBounds(out Bounds bounds))
                        continue;

                    Vector3 toCenter = bounds.center - camera.transform.position;
                    float priority = toCenter.sqrMagnitude;

                    k_CandidateScratch.Add(new CullingCandidate
                    {
                        controller = controller,
                        renderers = scopedRenderers,
                        casterPassName = casterPassName,
                        bounds = bounds,
                        priority = priority
                    });
                }

                k_CandidateScratch.Sort((a, b) => a.priority.CompareTo(b.priority));

                int maxCharacters = Mathf.Clamp(m_Settings.maxCharacters, 1, 4);
                int slicesPerCharacter = Mathf.Clamp(m_Settings.slicesPerCharacter, 1, 3);
                int assignedCharacters = Mathf.Min(maxCharacters, k_CandidateScratch.Count);
                int assignedSlotCount = Mathf.Min(k_MaxSlots, assignedCharacters * slicesPerCharacter);

                for (int i = 0; i < k_MaxSlots; ++i)
                {
                    k_WorldToShadowArray[i] = Matrix4x4.identity;
                    k_ShadowAtlasRectArray[i] = Vector4.zero;
                    k_ShadowOriginArray[i] = Vector4.zero;
                }

                int slotResolution = Mathf.Max(64, m_Settings.slotResolution);
                int maxAtlasResolution = Mathf.Max(256, m_Settings.maxAtlasResolution);
                slotResolution = Mathf.Min(slotResolution, maxAtlasResolution / Mathf.Max(1, slicesPerCharacter));
                slotResolution = Mathf.Min(slotResolution, maxAtlasResolution / Mathf.Max(1, assignedCharacters));
                slotResolution = Mathf.Max(64, slotResolution);

                int atlasWidth = Mathf.Max(1, slotResolution * Mathf.Max(1, slicesPerCharacter));
                int atlasHeight = Mathf.Max(1, slotResolution * Mathf.Max(1, assignedCharacters));

                SlotData[] slotData = assignedSlotCount > 0 ? new SlotData[assignedSlotCount] : Array.Empty<SlotData>();
                Vector3 baseSliceDirection = BuildBaseSliceDirection(m_Settings);

                int slotWriteIndex = 0;
                for (int charIndex = 0; charIndex < assignedCharacters; ++charIndex)
                {
                    CullingCandidate candidate = k_CandidateScratch[charIndex];

                    for (int sliceIndex = 0; sliceIndex < slicesPerCharacter; ++sliceIndex)
                    {
                        if (slotWriteIndex >= assignedSlotCount)
                            break;

                        Vector3 projectionAnchor = candidate.bounds.center;

                        Bounds shadowBounds = candidate.bounds;

                        Vector3 sliceDirection = BuildSliceDirection(m_Settings, baseSliceDirection, sliceIndex, slicesPerCharacter);
                        if (!CharacterManikinPerObjectShadowUtility.TryBuildSelfShadowMatricesFromDirection(
                                camera,
                                candidate.controller.transform,
                                shadowBounds,
                                projectionAnchor,
                                sliceDirection,
                                m_Settings.lightFollowAmount,
                                m_Settings.nearPlane,
                                m_Settings.farPlane,
                                m_Settings.minOrthographicSize,
                                out Matrix4x4 viewMatrix,
                                out Matrix4x4 projectionMatrix,
                                out _,
                                out Vector4 lightDirection))
                        {
                            continue;
                        }

                        Vector4 atlasRect = new Vector4(
                            (float)(sliceIndex * slotResolution) / atlasWidth,
                            (float)(charIndex * slotResolution) / atlasHeight,
                            (float)slotResolution / atlasWidth,
                            (float)slotResolution / atlasHeight);

                        Matrix4x4 shadowProjection = projectionMatrix;
                        if (SystemInfo.usesReversedZBuffer)
                        {
                            shadowProjection.m20 = -shadowProjection.m20;
                            shadowProjection.m21 = -shadowProjection.m21;
                            shadowProjection.m22 = -shadowProjection.m22;
                            shadowProjection.m23 = -shadowProjection.m23;
                        }

                        Matrix4x4 worldToShadow = BuildAtlasScaleBiasMatrix(atlasRect)
                            * BuildClipToTextureMatrix()
                            * shadowProjection
                            * viewMatrix;

                        slotData[slotWriteIndex].renderers = candidate.renderers;
                        slotData[slotWriteIndex].casterPassName = candidate.casterPassName;
                        slotData[slotWriteIndex].viewMatrix = viewMatrix;
                        slotData[slotWriteIndex].projectionMatrix = projectionMatrix;
                        slotData[slotWriteIndex].lightDirection = lightDirection;
                        slotData[slotWriteIndex].viewport = new Rect(
                            sliceIndex * slotResolution,
                            charIndex * slotResolution,
                            slotResolution,
                            slotResolution);

                        k_WorldToShadowArray[slotWriteIndex] = worldToShadow;
                        k_ShadowAtlasRectArray[slotWriteIndex] = atlasRect;
                        k_ShadowOriginArray[slotWriteIndex] = new Vector4(
                            candidate.bounds.center.x,
                            candidate.bounds.center.y,
                            candidate.bounds.center.z,
                            1f);

                        slotWriteIndex++;
                    }
                }

                int activeSlotCount = slotWriteIndex;
                GlobalArraySnapshot globalsSnapshot = CaptureGlobalArraySnapshot();
                Vector4 rawShadowAtlasTexelSize = new Vector4(
                    1f / Mathf.Max(1, atlasWidth),
                    1f / Mathf.Max(1, atlasHeight),
                    atlasWidth,
                    atlasHeight);

                RenderTextureDescriptor rawAtlasDescriptor = cameraData.cameraTargetDescriptor;
                rawAtlasDescriptor.width = Mathf.Max(1, atlasWidth);
                rawAtlasDescriptor.height = Mathf.Max(1, atlasHeight);
                rawAtlasDescriptor.msaaSamples = 1;
                rawAtlasDescriptor.colorFormat = RenderTextureFormat.Depth;
                rawAtlasDescriptor.graphicsFormat = GraphicsFormat.None;
                rawAtlasDescriptor.depthStencilFormat = GraphicsFormat.D16_UNorm;
                rawAtlasDescriptor.depthBufferBits = 16;
                rawAtlasDescriptor.shadowSamplingMode = ShadowSamplingMode.None;
                rawAtlasDescriptor.bindMS = false;
                rawAtlasDescriptor.enableRandomWrite = false;
                rawAtlasDescriptor.autoGenerateMips = false;
                rawAtlasDescriptor.useMipMap = false;
                rawAtlasDescriptor.mipCount = 1;

                RenderTextureDescriptor floorDepthDescriptor = cameraData.cameraTargetDescriptor;
                floorDepthDescriptor.msaaSamples = Mathf.Max(1, cameraData.cameraTargetDescriptor.msaaSamples);
                floorDepthDescriptor.depthBufferBits = 0;
                floorDepthDescriptor.depthStencilFormat = GraphicsFormat.None;
                floorDepthDescriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
                floorDepthDescriptor.sRGB = false;
                floorDepthDescriptor.bindMS = cameraData.cameraTargetDescriptor.bindMS;
                floorDepthDescriptor.enableRandomWrite = false;
                floorDepthDescriptor.autoGenerateMips = false;
                floorDepthDescriptor.useMipMap = false;
                floorDepthDescriptor.mipCount = 1;

                RenderTextureDescriptor shadowDescriptor = cameraData.cameraTargetDescriptor;
                shadowDescriptor.msaaSamples = Mathf.Max(1, cameraData.cameraTargetDescriptor.msaaSamples);
                shadowDescriptor.depthBufferBits = 0;
                shadowDescriptor.depthStencilFormat = GraphicsFormat.None;
                shadowDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
                shadowDescriptor.sRGB = false;
                shadowDescriptor.bindMS = cameraData.cameraTargetDescriptor.bindMS;
                shadowDescriptor.enableRandomWrite = false;
                shadowDescriptor.autoGenerateMips = false;
                shadowDescriptor.useMipMap = false;
                shadowDescriptor.mipCount = 1;

                TextureHandle rawAtlasTexture = activeSlotCount > 0
                    ? UniversalRenderer.CreateRenderGraphTexture(renderGraph, rawAtlasDescriptor, "_ManikinRawShadowAtlas", false)
                    : TextureHandle.nullHandle;
                TextureHandle floorDepthTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, floorDepthDescriptor, "_ManikinDepth", false);
                TextureHandle receiverShadowTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, shadowDescriptor, "_ManikinShadow", false);

                if (activeSlotCount > 0 && rawAtlasTexture.IsValid())
                {
                    using (var builder = renderGraph.AddRasterRenderPass<AtlasPassData>(k_AtlasPassName, out var passData))
                    {
                        SlotData[] activeSlots = new SlotData[activeSlotCount];
                        Array.Copy(slotData, activeSlots, activeSlotCount);

                        passData.slots = activeSlots;
                        passData.cameraView = camera.worldToCameraMatrix;
                        passData.cameraProjection = camera.projectionMatrix;

                        builder.SetRenderAttachmentDepth(rawAtlasTexture, AccessFlags.ReadWrite);
                        builder.AllowGlobalStateModification(true);
                        builder.AllowPassCulling(false);
                        builder.SetRenderFunc((AtlasPassData data, RasterGraphContext context) => ExecuteAtlasPass(data, context));
                    }
                }

                DrawingSettings depthDrawing = CreateDrawingSettings(k_DepthPassTagList, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                FilteringSettings depthFiltering = new FilteringSettings(RenderQueueRange.opaque);
                RendererListParams depthRendererListParams = new RendererListParams(renderingData.cullResults, depthDrawing, depthFiltering);
                RendererListHandle depthRendererList = renderGraph.CreateRendererList(depthRendererListParams);

                using (var builder = renderGraph.AddRasterRenderPass<DepthPassData>(k_DepthPassName, out var passData))
                {
                    passData.rendererList = depthRendererList;

                    builder.UseRendererList(depthRendererList);
                    builder.SetRenderAttachment(floorDepthTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(floorDepthTexture, k_ManikinDepthId);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((DepthPassData data, RasterGraphContext context) => ExecuteDepthPass(data, context));
                }

                DrawingSettings receiverDrawing = CreateDrawingSettings(k_ReceiverPassTagList, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                FilteringSettings receiverFiltering = new FilteringSettings(RenderQueueRange.opaque);
                RendererListParams receiverRendererListParams = new RendererListParams(renderingData.cullResults, receiverDrawing, receiverFiltering);
                RendererListHandle receiverRendererList = renderGraph.CreateRendererList(receiverRendererListParams);

                using (var builder = renderGraph.AddRasterRenderPass<ReceiverPassData>(k_ReceiverPassName, out var passData))
                {
                    passData.rendererList = receiverRendererList;
                    passData.rawAtlas = rawAtlasTexture;
                    passData.floorDepth = floorDepthTexture;
                    passData.fallbackTexture = renderGraph.defaultResources.blackTexture;

                    builder.UseRendererList(receiverRendererList);
                    if (rawAtlasTexture.IsValid())
                        builder.UseTexture(rawAtlasTexture, AccessFlags.Read);
                    builder.UseTexture(floorDepthTexture, AccessFlags.Read);
                    builder.UseTexture(passData.fallbackTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(receiverShadowTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(receiverShadowTexture, k_ManikinShadowId);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((ReceiverPassData data, RasterGraphContext context) => ExecuteReceiverPass(data, context));
                }

                RecordBindGlobalsPass(
                    renderGraph,
                    rawAtlasTexture,
                    floorDepthTexture,
                    receiverShadowTexture,
                    globalsSnapshot,
                    activeSlotCount,
                    rawShadowAtlasTexelSize);
            }
        }
    }
}

