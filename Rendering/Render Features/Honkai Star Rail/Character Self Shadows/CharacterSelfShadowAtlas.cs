using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Character;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HoyoToon.Rendering.HSR
{
    public class CharacterSelfShadowAtlas : ScriptableRendererFeature
    {
        [SerializeField] CharacterSelfShadowAtlasSettings settings = new CharacterSelfShadowAtlasSettings();

        CharacterSelfShadowAtlasPass m_Pass;
        readonly Dictionary<Renderer, ShadowCastingMode> m_MainShadowCasterOriginalModes = new Dictionary<Renderer, ShadowCastingMode>();
        readonly HashSet<Renderer> m_MainShadowCasterDesiredOff = new HashSet<Renderer>();
        readonly List<Renderer> m_MainShadowCasterRestoreScratch = new List<Renderer>();
        readonly List<HSRCharacterController> m_MainShadowControllerScratch = new List<HSRCharacterController>();
        readonly List<Material> m_MainShadowMaterialScratch = new List<Material>(8);
        int m_LastMainShadowFilterFrame = -1;

        public override void Create()
        {
            if (settings == null)
                settings = new CharacterSelfShadowAtlasSettings();

            m_Pass = new CharacterSelfShadowAtlasPass(settings);
            m_Pass.renderPassEvent = settings.renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType cameraType = renderingData.cameraData.cameraType;
            bool canAffectSceneRendering = cameraType == CameraType.Game || cameraType == CameraType.SceneView;

            if (!settings.excludeFromMainShadowMap)
            {
                RestoreAllMainShadowCasterOverrides();
            }
            else if (canAffectSceneRendering)
            {
                // Multiple cameras can trigger AddRenderPasses in the same frame; filter once per frame.
                if (m_LastMainShadowFilterFrame != Time.frameCount)
                {
                    UpdateMainShadowCasterFiltering();
                    m_LastMainShadowFilterFrame = Time.frameCount;
                }
            }

            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            RestoreAllMainShadowCasterOverrides();
            base.Dispose(disposing);
        }

        bool RendererUsesExcludedMainShadowPass(Renderer renderer)
        {
            if (renderer == null)
                return false;

            m_MainShadowMaterialScratch.Clear();
            renderer.GetSharedMaterials(m_MainShadowMaterialScratch);

            if (m_MainShadowMaterialScratch.Count == 0)
                return false;

            for (int i = 0; i < m_MainShadowMaterialScratch.Count; ++i)
            {
                Material material = m_MainShadowMaterialScratch[i];
                if (material == null)
                    continue;

                string lightMode = material.GetTag("LightMode", false, string.Empty);
                if (string.Equals(lightMode, "LightingGBuffer", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(lightMode, "CustomForward", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (material.FindPass("LightingGBuffer") >= 0)
                    return true;

                if (material.FindPass("CustomForward") >= 0)
                    return true;
            }

            m_MainShadowMaterialScratch.Clear();

            return false;
        }

        void UpdateMainShadowCasterFiltering()
        {
            m_MainShadowCasterDesiredOff.Clear();
            HSRCharacterController.GetActiveControllers(m_MainShadowControllerScratch, forceRefresh: false);

            for (int i = 0; i < m_MainShadowControllerScratch.Count; ++i)
            {
                HSRCharacterController controller = m_MainShadowControllerScratch[i];
                if (controller == null || !controller.EnableCharacterSelfShadow)
                    continue;

                Renderer[] scopedRenderers = controller.GetScopedRenderers();
                if (scopedRenderers == null)
                    continue;

                for (int r = 0; r < scopedRenderers.Length; ++r)
                {
                    Renderer renderer = scopedRenderers[r];
                    if (renderer == null)
                        continue;

                    if (!RendererUsesExcludedMainShadowPass(renderer))
                        continue;

                    m_MainShadowCasterDesiredOff.Add(renderer);
                }
            }

            foreach (Renderer renderer in m_MainShadowCasterDesiredOff)
            {
                if (renderer == null)
                    continue;

                if (!m_MainShadowCasterOriginalModes.ContainsKey(renderer))
                    m_MainShadowCasterOriginalModes.Add(renderer, renderer.shadowCastingMode);

                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            m_MainShadowCasterRestoreScratch.Clear();
            foreach (var kvp in m_MainShadowCasterOriginalModes)
            {
                if (kvp.Key != null && m_MainShadowCasterDesiredOff.Contains(kvp.Key))
                    continue;

                m_MainShadowCasterRestoreScratch.Add(kvp.Key);
            }

            for (int i = 0; i < m_MainShadowCasterRestoreScratch.Count; ++i)
            {
                Renderer renderer = m_MainShadowCasterRestoreScratch[i];
                if (renderer != null && m_MainShadowCasterOriginalModes.TryGetValue(renderer, out ShadowCastingMode originalMode))
                    renderer.shadowCastingMode = originalMode;

                m_MainShadowCasterOriginalModes.Remove(renderer);
            }

            m_MainShadowCasterRestoreScratch.Clear();
            m_MainShadowControllerScratch.Clear();
        }

        void RestoreAllMainShadowCasterOverrides()
        {
            foreach (var kvp in m_MainShadowCasterOriginalModes)
            {
                if (kvp.Key != null)
                    kvp.Key.shadowCastingMode = kvp.Value;
            }

            m_MainShadowCasterOriginalModes.Clear();
            m_MainShadowCasterDesiredOff.Clear();
            m_MainShadowCasterRestoreScratch.Clear();
            m_MainShadowControllerScratch.Clear();
        }

        [Serializable]
        public class CharacterSelfShadowAtlasSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            [Min(512)] public int maxAtlasResolution = 4096;
            public bool excludeFromMainShadowMap = true;
        }

        class CharacterSelfShadowAtlasPass : ScriptableRenderPass
        {
            const int k_MaxSlots = 4;
            const int k_PassMissing = -1;
            const int k_PassExcluded = -2;
            const string k_CharacterLightName = "CharacterLight";
            const string k_CharacterShadowLightName = "CharacterShadowLight";

            static readonly int k_CharacterSelfShadowMapId = Shader.PropertyToID("_CharacterSelfShadowMap");
            static readonly int k_CharacterSelfShadowTextureId = Shader.PropertyToID("_CharacterSelfShadowTexture");
            static readonly int k_CharacterSelfShadowWorldToShadowArrId = Shader.PropertyToID("_CharacterSelfShadowWorldToShadowArr");
            static readonly int k_CharacterSelfShadowAtlasRectArrId = Shader.PropertyToID("_CharacterSelfShadowAtlasRectArr");
            static readonly int k_CharacterSelfShadowSlotCountId = Shader.PropertyToID("_CharacterSelfShadowSlotCount");
            static readonly int k_CharacterSelfShadowAtlasTexelSizeId = Shader.PropertyToID("_CharacterSelfShadowAtlasTexelSize");
            static readonly int k_LightDirectionId = Shader.PropertyToID("_LightDirection");
            static readonly ShaderTagId k_LightModeTag = new ShaderTagId("LightMode");
            static readonly ShaderTagId k_HsrPerObjectShadowCasterTag = new ShaderTagId("HSRPerObjectShadowCaster");
            static readonly ShaderTagId k_ShadowCasterTag = new ShaderTagId("ShadowCaster");
            static readonly ShaderTagId k_ShadowCasterUpperTag = new ShaderTagId("SHADOWCASTER");
            static readonly ShaderTagId k_DepthOnlyTag = new ShaderTagId("DepthOnly");
            static readonly ShaderTagId k_DepthNormalsOnlyTag = new ShaderTagId("DepthNormalsOnly");
            static readonly ShaderTagId k_DepthNormalsTag = new ShaderTagId("DepthNormals");

            const string k_CastingSelfShadowKeyword = "_CASTING_SELF_SHADOW";

            static readonly string[] k_FallbackCasterPassNames =
            {
                "ShadowCaster",
                "DepthOnly",
                "DepthNormalsOnly",
                "DepthNormals"
            };

            static readonly Matrix4x4[] k_WorldToShadowArray =
            {
                Matrix4x4.identity,
                Matrix4x4.identity,
                Matrix4x4.identity,
                Matrix4x4.identity
            };

            static readonly Vector4[] k_AtlasRectArray =
            {
                Vector4.zero,
                Vector4.zero,
                Vector4.zero,
                Vector4.zero
            };

            static readonly List<HSRCharacterController> k_ControllerScratch = new List<HSRCharacterController>(k_MaxSlots * 2);
            static readonly HashSet<HSRCharacterController> k_AssignedControllerSet = new HashSet<HSRCharacterController>();
            static readonly List<Light> k_LightScratch = new List<Light>(8);
            static readonly List<CullingCandidate> k_CandidateScratch = new List<CullingCandidate>(16);
            static readonly List<Material> k_MaterialScratch = new List<Material>(8);
            static readonly List<int> k_PassIndexScratch = new List<int>(8);

            readonly CharacterSelfShadowAtlasSettings m_Settings;

            struct SlotData
            {
                public HSRCharacterController controller;
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
                public Matrix4x4 viewMatrix;
                public Matrix4x4 projectionMatrix;
                public Vector4 lightDirection;
                public float priority;
                public int preferredResolution;
            }

            class PassData
            {
                public SlotData[] slots;
                public Matrix4x4 cameraView;
                public Matrix4x4 cameraProjection;
            }

            class BindGlobalsPassData
            {
                public TextureHandle atlasTexture;
            }

            public CharacterSelfShadowAtlasPass(CharacterSelfShadowAtlasSettings settings)
            {
                m_Settings = settings;
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

            static bool UsesCustomForward(Material material)
            {
                if (material == null)
                    return false;

                string materialLightMode = material.GetTag("LightMode", false, string.Empty);
                if (string.Equals(materialLightMode, "CustomForward", StringComparison.OrdinalIgnoreCase))
                    return true;

                Shader shader = material.shader;
                if (shader == null)
                    return false;

                for (int i = 0; i < shader.passCount; ++i)
                {
                    ShaderTagId lightMode = shader.FindPassTagValue(i, k_LightModeTag);
                    if (string.Equals(lightMode.name, "CustomForward", StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return material.FindPass("CustomForward") >= 0;
            }

            static bool IsExplicitlyExcludedFromSelfShadow(Material material)
            {
                if (material == null)
                    return false;

                if (UsesCustomForward(material))
                    return true;

                string renderType = material.GetTag("RenderType", false, string.Empty);
                if (string.Equals(renderType, "Transparent", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (material.renderQueue > (int)RenderQueue.GeometryLast)
                    return true;

                Shader shader = material.shader;
                if (shader != null && shader.name.IndexOf("/Transparent", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                return false;
            }

            static int FindSelfShadowPassIndex(Material material, string preferredPassName)
            {
                if (material == null)
                    return k_PassMissing;

                if (IsExplicitlyExcludedFromSelfShadow(material))
                    return k_PassExcluded;

                Shader shader = material.shader;
                if (shader == null)
                    return k_PassMissing;

                if (!string.IsNullOrEmpty(preferredPassName))
                {
                    int preferredPassIndex = material.FindPass(preferredPassName);
                    if (preferredPassIndex >= 0)
                        return preferredPassIndex;
                }

                for (int i = 0; i < shader.passCount; ++i)
                {
                    ShaderTagId lightMode = shader.FindPassTagValue(i, k_LightModeTag);
                    if (lightMode == k_HsrPerObjectShadowCasterTag)
                        return i;
                }

                int hsrPass = material.FindPass("HSRPerObjectShadowCaster");
                if (hsrPass >= 0)
                    return hsrPass;

                for (int i = 0; i < shader.passCount; ++i)
                {
                    ShaderTagId lightMode = shader.FindPassTagValue(i, k_LightModeTag);
                    if (lightMode == k_ShadowCasterTag || lightMode == k_ShadowCasterUpperTag)
                    {
                        return i;
                    }
                }

                int shadowCasterPass = material.FindPass("ShadowCaster");
                if (shadowCasterPass >= 0)
                    return shadowCasterPass;

                for (int i = 0; i < shader.passCount; ++i)
                {
                    ShaderTagId lightMode = shader.FindPassTagValue(i, k_LightModeTag);
                    if (lightMode == k_DepthOnlyTag ||
                        lightMode == k_DepthNormalsOnlyTag ||
                        lightMode == k_DepthNormalsTag)
                    {
                        return i;
                    }
                }

                for (int i = 0; i < k_FallbackCasterPassNames.Length; ++i)
                {
                    int passIndex = material.FindPass(k_FallbackCasterPassNames[i]);
                    if (passIndex >= 0)
                        return passIndex;
                }

                return k_PassMissing;
            }

            static bool IsOwnedLight(HSRCharacterController controller, Light light)
            {
                if (controller == null || light == null || light.transform == null)
                    return false;

                Transform root = controller.transform;
                if (root == null || light.transform == root)
                    return false;

                return light.transform.IsChildOf(root);
            }

            static bool TryGetOwnedSelfShadowLight(HSRCharacterController controller, out Light ownedLight)
            {
                ownedLight = null;
                if (controller == null)
                    return false;

                if (IsOwnedLight(controller, controller.CharacterShadowLight) && controller.CharacterShadowLight.isActiveAndEnabled)
                {
                    ownedLight = controller.CharacterShadowLight;
                    return true;
                }

                if (IsOwnedLight(controller, controller.CharacterLight) && controller.CharacterLight.isActiveAndEnabled)
                {
                    ownedLight = controller.CharacterLight;
                    return true;
                }

                k_LightScratch.Clear();
                controller.GetComponentsInChildren(true, k_LightScratch);

                Light firstActiveOwned = null;
                for (int i = 0; i < k_LightScratch.Count; ++i)
                {
                    Light light = k_LightScratch[i];
                    if (!IsOwnedLight(controller, light) || !light.isActiveAndEnabled)
                        continue;

                    if (string.Equals(light.gameObject.name, k_CharacterShadowLightName, StringComparison.Ordinal))
                    {
                        ownedLight = light;
                        break;
                    }

                    if (string.Equals(light.gameObject.name, k_CharacterLightName, StringComparison.Ordinal))
                    {
                        ownedLight = light;
                        break;
                    }

                    if (firstActiveOwned == null)
                        firstActiveOwned = light;
                }

                if (ownedLight == null)
                    ownedLight = firstActiveOwned;

                k_LightScratch.Clear();
                return ownedLight != null;
            }

            static Matrix4x4 BuildClipToTextureMatrix()
            {
                Matrix4x4 clipToTexture = Matrix4x4.identity;
                clipToTexture.m00 = 0.5f;
                clipToTexture.m03 = 0.5f;
                clipToTexture.m11 = 0.5f;
                clipToTexture.m13 = 0.5f;

                // Reversed-Z is handled by the projection matrix row flip before this transform.
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

            static void ApplyDisabledSelfShadowGlobals()
            {
                for (int i = 0; i < k_MaxSlots; ++i)
                {
                    k_WorldToShadowArray[i] = Matrix4x4.identity;
                    k_AtlasRectArray[i] = Vector4.zero;
                }

                Shader.SetGlobalTexture(k_CharacterSelfShadowMapId, Texture2D.blackTexture);
                Shader.SetGlobalTexture(k_CharacterSelfShadowTextureId, Texture2D.blackTexture);
                Shader.SetGlobalMatrixArray(k_CharacterSelfShadowWorldToShadowArrId, k_WorldToShadowArray);
                Shader.SetGlobalVectorArray(k_CharacterSelfShadowAtlasRectArrId, k_AtlasRectArray);
                Shader.SetGlobalFloat(k_CharacterSelfShadowSlotCountId, 0f);
                Shader.SetGlobalVector(k_CharacterSelfShadowAtlasTexelSizeId, new Vector4(1f, 1f, 1f, 1f));
            }

            static void ExecutePass(PassData data, RasterGraphContext context)
            {
                context.cmd.ClearRenderTarget(true, false, Color.clear);
                context.cmd.SetGlobalDepthBias(1.0f, 2.5f);
                CoreUtils.SetKeyword(context.cmd, k_CastingSelfShadowKeyword, true);

                if (data.slots == null || data.slots.Length == 0)
                {
                    context.cmd.SetGlobalDepthBias(0f, 0f);
                    CoreUtils.SetKeyword(context.cmd, k_CastingSelfShadowKeyword, false);
                    return;
                }

                for (int i = 0; i < data.slots.Length; ++i)
                {
                    SlotData slot = data.slots[i];

                    context.cmd.SetGlobalVector(k_LightDirectionId, slot.lightDirection);
                    context.cmd.SetViewport(slot.viewport);
                    context.cmd.SetViewProjectionMatrices(slot.viewMatrix, slot.projectionMatrix);

                    Renderer[] renderers = slot.renderers;
                    if (renderers == null)
                        continue;

                    for (int r = 0; r < renderers.Length; ++r)
                    {
                        Renderer renderer = renderers[r];
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                            continue;

                        k_MaterialScratch.Clear();
                        renderer.GetSharedMaterials(k_MaterialScratch);
                        int materialCount = k_MaterialScratch.Count;
                        if (materialCount == 0)
                            continue;

                        int subMeshCount = GetSubMeshCount(renderer);
                        if (subMeshCount <= 0)
                            continue;

                        k_PassIndexScratch.Clear();
                        int fallbackMaterialIndex = -1;
                        for (int m = 0; m < materialCount; ++m)
                        {
                            Material material = k_MaterialScratch[m];
                            int passIndex = FindSelfShadowPassIndex(material, slot.casterPassName);
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

                }

                context.cmd.SetGlobalDepthBias(0f, 0f);
                CoreUtils.SetKeyword(context.cmd, k_CastingSelfShadowKeyword, false);
                context.cmd.SetViewProjectionMatrices(data.cameraView, data.cameraProjection);
            }

            static void ExecuteBindGlobalsPass(BindGlobalsPassData data, RasterGraphContext context)
            {
                context.cmd.SetGlobalTexture(k_CharacterSelfShadowMapId, data.atlasTexture, RenderTextureSubElement.Depth);
                context.cmd.SetGlobalTexture(k_CharacterSelfShadowTextureId, data.atlasTexture, RenderTextureSubElement.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                Camera camera = cameraData.camera;

                CameraType cameraType = cameraData.cameraType;
                bool supportedCamera = cameraType == CameraType.Game || cameraType == CameraType.SceneView;

                if (camera == null || !supportedCamera)
                {
                    ApplyDisabledSelfShadowGlobals();
                    k_ControllerScratch.Clear();
                    k_AssignedControllerSet.Clear();
                    k_CandidateScratch.Clear();
                    return;
                }

                HSRCharacterController.GetActiveControllers(k_ControllerScratch, forceRefresh: false);
                k_AssignedControllerSet.Clear();
                k_CandidateScratch.Clear();

                for (int i = 0; i < k_ControllerScratch.Count; ++i)
                {
                    HSRCharacterController controller = k_ControllerScratch[i];
                    if (controller == null || !controller.EnableCharacterSelfShadow)
                        continue;

                    Renderer[] scopedRenderers = controller.GetScopedRenderers();
                    if (scopedRenderers == null || scopedRenderers.Length == 0)
                        continue;

                    if (!controller.TryGetCharacterSelfShadowBounds(out Bounds bounds))
                        continue;

                    if (!TryGetOwnedSelfShadowLight(controller, out Light shadowLight))
                        continue;

                    if (!CharacterPerObjectShadowUtility.TryBuildSelfShadowMatrices(
                            camera,
                            controller.transform,
                            bounds,
                            shadowLight,
                            controller.CharacterSelfShadowLightFollow,
                            controller.CharacterSelfShadowInvertLightDirection,
                            controller.CharacterSelfShadowNearPlane,
                            controller.CharacterSelfShadowFarPlane,
                            controller.CharacterSelfShadowOrthographicSize,
                            out Matrix4x4 viewMatrix,
                            out Matrix4x4 projectionMatrix,
                            out float priority,
                            out Vector4 lightDirection))
                    {
                        continue;
                    }

                    k_CandidateScratch.Add(new CullingCandidate
                    {
                        controller = controller,
                        renderers = scopedRenderers,
                        casterPassName = controller.CharacterSelfShadowCasterPassName,
                        viewMatrix = viewMatrix,
                        projectionMatrix = projectionMatrix,
                        lightDirection = lightDirection,
                        priority = priority,
                        preferredResolution = Mathf.Max(128, controller.CharacterSelfShadowResolution),
                    });
                }

                if (k_CandidateScratch.Count <= 0)
                {
                    for (int i = 0; i < k_ControllerScratch.Count; ++i)
                    {
                        HSRCharacterController controller = k_ControllerScratch[i];
                        if (controller != null)
                            controller.SetCharacterSelfShadowState(Vector4.zero, 0, false);
                    }

                    ApplyDisabledSelfShadowGlobals();
                    k_ControllerScratch.Clear();
                    k_AssignedControllerSet.Clear();
                    k_CandidateScratch.Clear();
                    return;
                }

                k_CandidateScratch.Sort((a, b) => a.priority.CompareTo(b.priority));

                int assignedCount = Mathf.Min(k_MaxSlots, k_CandidateScratch.Count);
                SlotData[] slotData = new SlotData[assignedCount];

                int perSlotResolution = 128;
                for (int i = 0; i < assignedCount; ++i)
                {
                    perSlotResolution = Mathf.Max(perSlotResolution, k_CandidateScratch[i].preferredResolution);
                }

                int maxAtlasResolution = Mathf.Max(512, m_Settings.maxAtlasResolution);
                perSlotResolution = Mathf.Max(128, perSlotResolution);

                int gridCols = assignedCount > 1 ? 2 : 1;
                int gridRows = assignedCount > 2 ? 2 : 1;

                int maxPerSlotFromWidth = Mathf.Max(128, maxAtlasResolution / gridCols);
                int maxPerSlotFromHeight = Mathf.Max(128, maxAtlasResolution / gridRows);
                perSlotResolution = Mathf.Min(perSlotResolution, Mathf.Min(maxPerSlotFromWidth, maxPerSlotFromHeight));

                int atlasWidth = perSlotResolution * gridCols;
                int atlasHeight = perSlotResolution * gridRows;

                for (int i = 0; i < k_MaxSlots; ++i)
                {
                    k_WorldToShadowArray[i] = Matrix4x4.identity;
                    k_AtlasRectArray[i] = Vector4.zero;
                }

                for (int assignedSlot = 0; assignedSlot < assignedCount; ++assignedSlot)
                {
                    CullingCandidate candidate = k_CandidateScratch[assignedSlot];
                    HSRCharacterController controller = candidate.controller;

                    int col = assignedSlot % gridCols;
                    int row = assignedSlot / gridCols;
                    Vector4 atlasRect = new Vector4(
                        (float)(col * perSlotResolution) / atlasWidth,
                        (float)(row * perSlotResolution) / atlasHeight,
                        (float)perSlotResolution / atlasWidth,
                        (float)perSlotResolution / atlasHeight);

                    Matrix4x4 shadowProjection = candidate.projectionMatrix;
                    if (SystemInfo.usesReversedZBuffer)
                    {
                        shadowProjection.m20 = -shadowProjection.m20;
                        shadowProjection.m21 = -shadowProjection.m21;
                        shadowProjection.m22 = -shadowProjection.m22;
                        shadowProjection.m23 = -shadowProjection.m23;
                    }

                    Matrix4x4 worldToShadow = BuildAtlasScaleBiasMatrix(atlasRect) * BuildClipToTextureMatrix() * shadowProjection * candidate.viewMatrix;

                    slotData[assignedSlot].controller = controller;
                    slotData[assignedSlot].renderers = candidate.renderers;
                    slotData[assignedSlot].casterPassName = candidate.casterPassName;
                    slotData[assignedSlot].viewMatrix = candidate.viewMatrix;
                    slotData[assignedSlot].projectionMatrix = candidate.projectionMatrix;
                    slotData[assignedSlot].lightDirection = candidate.lightDirection;
                    slotData[assignedSlot].viewport = new Rect(col * perSlotResolution, row * perSlotResolution, perSlotResolution, perSlotResolution);

                    k_WorldToShadowArray[assignedSlot] = worldToShadow;
                    k_AtlasRectArray[assignedSlot] = atlasRect;

                    controller.SetCharacterSelfShadowState(atlasRect, assignedSlot, true);
                    k_AssignedControllerSet.Add(controller);
                }

                for (int i = 0; i < k_ControllerScratch.Count; ++i)
                {
                    HSRCharacterController controller = k_ControllerScratch[i];
                    if (controller == null)
                        continue;

                    if (k_AssignedControllerSet.Contains(controller))
                        continue;

                    controller.SetCharacterSelfShadowState(Vector4.zero, 0, false);
                }

                k_ControllerScratch.Clear();
                k_AssignedControllerSet.Clear();
                k_CandidateScratch.Clear();

                Shader.SetGlobalMatrixArray(k_CharacterSelfShadowWorldToShadowArrId, k_WorldToShadowArray);
                Shader.SetGlobalVectorArray(k_CharacterSelfShadowAtlasRectArrId, k_AtlasRectArray);
                Shader.SetGlobalFloat(k_CharacterSelfShadowSlotCountId, assignedCount);
                Shader.SetGlobalVector(
                    k_CharacterSelfShadowAtlasTexelSizeId,
                    new Vector4(
                        1f / atlasWidth,
                        1f / atlasHeight,
                        atlasWidth,
                        atlasHeight));

                RenderTextureDescriptor atlasDescriptor = cameraData.cameraTargetDescriptor;
                atlasDescriptor.width = atlasWidth;
                atlasDescriptor.height = atlasHeight;
                atlasDescriptor.msaaSamples = 1;
                atlasDescriptor.colorFormat = RenderTextureFormat.Depth;
                atlasDescriptor.graphicsFormat = GraphicsFormat.None;
                atlasDescriptor.depthStencilFormat = GraphicsFormat.D16_UNorm;
                atlasDescriptor.depthBufferBits = 16;
                atlasDescriptor.shadowSamplingMode = ShadowSamplingMode.CompareDepths;
                atlasDescriptor.bindMS = false;
                atlasDescriptor.enableRandomWrite = false;
                atlasDescriptor.autoGenerateMips = false;
                atlasDescriptor.useMipMap = false;
                atlasDescriptor.mipCount = 1;

                TextureHandle atlasTexture = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    atlasDescriptor,
                    "_CharacterSelfShadowMap",
                    false);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Character Self Shadow Atlas", out var passData))
                {
                    passData.slots = slotData;
                    passData.cameraView = camera.worldToCameraMatrix;
                    passData.cameraProjection = camera.projectionMatrix;

                    builder.SetRenderAttachmentDepth(atlasTexture, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
                }

                using (var builder = renderGraph.AddRasterRenderPass<BindGlobalsPassData>("Character Self Shadow Atlas Globals", out var passData))
                {
                    passData.atlasTexture = atlasTexture;

                    builder.UseTexture(atlasTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((BindGlobalsPassData data, RasterGraphContext context) => ExecuteBindGlobalsPass(data, context));
                }
            }
        }
    }
}
