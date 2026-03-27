using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using HoyoToon.Simulator.Utilities;
using System.ComponentModel;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace HoyoToon.Runtime.Character
{
    [ExecuteAlways]
    public class HSRCharacterController : MonoBehaviour
    {
        public enum EffectType
        {
            None = 0,
            AuraOutline = 1, 
            Custom = 100
        }

        [Serializable]
        public struct EffectMaterialEntry
        {
            public EffectType Type;
            public Material Material;
        }

        private const float RegistrySlowDiscoveryInterval = 2.0f;
        private const float CharacterLightBaseOffset = 0.5f;
        private const float CharacterLightCameraDistanceFactor = 0.05f;
        private const float CharacterLightMaxOffset = 0.5f;
        private const string CharacterLightName = "CharacterLight";
        private const string CharacterShadowLightName = "CharacterShadowLight";
        private const string SceneLightName = "HoyoToon Scene Light";
        private const string CharacterRootLayer = "Honkai Star Rail";
        private const string HairTag = "Honkai Star Rail Hair";
        private const string HairToken = "Hair";
        private static readonly List<HSRCharacterController> s_ActiveControllers = new List<HSRCharacterController>();
        private static readonly HashSet<Renderer> s_TrackedRenderers = new HashSet<Renderer>();
        private static readonly HashSet<Renderer> s_CurrentTrackedRenderers = new HashSet<Renderer>();
        private static readonly List<Renderer> s_StaleRenderers = new List<Renderer>();
        private static readonly List<Material> s_RendererMaterials = new List<Material>();
        private static float s_NextSlowRegistryDiscoveryTime;
        private static bool s_TopologyDirty = true;
        private static readonly int s_StencilEyeId = Shader.PropertyToID("_StencilEye");
        private static readonly int s_CrpPerDrawExId = Shader.PropertyToID("CRP_PerDrawEx");
        private static readonly int s_CharacterSelfShadowAtlasRectId = Shader.PropertyToID("_CharacterSelfShadowAtlasRect");
        private static readonly int s_CharacterSelfShadowSliceIndexId = Shader.PropertyToID("_CharacterSelfShadowSliceIndex");
        private static readonly int s_CharacterSelfShadowValidId = Shader.PropertyToID("_CharacterSelfShadowValid");
        private static readonly int s_HsrComputeSkinnedVerticesId = Shader.PropertyToID("_HSRComputeSkinnedVertices");
        private static readonly int s_HsrComputeSkinningEnabledId = Shader.PropertyToID("_HSRComputeSkinningEnabled");
        private static readonly int s_HsrComputeSkinningVertexOffsetId = Shader.PropertyToID("_HSRComputeSkinningVertexOffset");
        private const string SceneControllerTypeName = "HoyoToon.Runtime.Scene.HSRSceneController, com.hoyotoon.hoyotoon.Runtime";
        private static bool s_HasTriedResolveSceneControllerHooks;
        private static MethodInfo s_RegisterCharacterLightMethod;
        private static MethodInfo s_UnregisterCharacterLightMethod;
        private static readonly int s_CrpPerDrawExSize = Marshal.SizeOf<CrpPerDrawExData>();
        private const int SkinningKernelThreadGroupSize = 64;
        private const string SkinningKernelName = "CSMain";
        private const string DefaultSkinningComputeShaderAssetPath = "Packages/com.hoyotoon.hoyotoon/Shaders/Utility/ComputeShaders/SkinningUVCoords.compute";

        public enum CharacterSkinningMode
        {
            BuiltIn = 0,
            Compute = 1
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CrpPerDrawExData
        {
            public Vector4 _CharacterLocalMainLightPosition;
            public Vector4 _CharacterLocalMainLightColor;
            public Vector4 _CharacterLocalMainLightColor1;
            public Vector4 _CharacterLocalMainLightColor2;
            public Vector4 _CharacterLocalMainLightDark;
            public Vector4 _CharacterLocalMainLightDark1;
            public Vector4 _NewLocalLightDir;
            public Vector4 _NewLocalLightCharCenter;
            public Vector4 _NewLocalLightStrength;
            public float _DisableCharacterLocalLight;
            public float _EnableCustomCameraOverride;
            public Vector2 _Padding;
            public Vector4 _CharacterSelfShadowAtlasRect;
            public float _CharacterSelfShadowSliceIndex;
            public float _CharacterSelfShadowValid;
            public Vector2 _PadCharacterSelfShadow;
        }

        private sealed class RendererConstantBufferState
        {
            public readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            public readonly ComputeBuffer constantBuffer = new ComputeBuffer(1, s_CrpPerDrawExSize, ComputeBufferType.Constant);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SkinData
        {
            public Vector3 pos;
            public float pad0;
            public Vector3 norm;
            public float pad1;
            public Vector4 tangent;
            public Vector4 tangent1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UInt4
        {
            public uint x;
            public uint y;
            public uint z;
            public uint w;

            public UInt4(uint x, uint y, uint z, uint w)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
            }
        }

        private sealed class ComputeSkinSegment
        {
            public SkinnedMeshRenderer renderer;
            public Mesh mesh;
            public Transform[] bones;
            public Matrix4x4[] bindPoses;
            public int vertexOffset;
            public int vertexCount;
            public int matrixOffset;
            public int matrixCount;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistryOnSubsystemRegistration()
        {
            s_ActiveControllers.Clear();
            s_TrackedRenderers.Clear();
            s_CurrentTrackedRenderers.Clear();
            s_StaleRenderers.Clear();
            s_NextSlowRegistryDiscoveryTime = 0f;
            s_TopologyDirty = true;
        }

        [Header("Character Light")]
        public Light CharacterLight;
        public bool SyncWithSceneLight = true;
        public Light SceneLight;
        public Light CharacterShadowLight;
        public bool SyncCharacterShadowLightWithCharacterLight = true;
        [Header("Character Self Shadow")]
        public bool EnableCharacterSelfShadow = true;
        [Min(128)] public int CharacterSelfShadowResolution = 2048;
        [Min(0.01f)] public float CharacterSelfShadowOrthographicSize = 0.01f;
        [Min(0.0001f)] public float CharacterSelfShadowNearPlane = 0.01f;
        [Min(0.001f)] public float CharacterSelfShadowFarPlane = 1f;
        [Range(0f, 1f)] public float CharacterSelfShadowLightFollow = 0.5f;
        public bool CharacterSelfShadowInvertLightDirection = false;
        [HideInInspector] public string CharacterSelfShadowCasterPassName = "ShadowCaster";

        [Header("Character Skinning")]
        public CharacterSkinningMode SkinningMode = CharacterSkinningMode.Compute;
        [Tooltip("Compute shader used for custom skinning. Must match SkinningUVCoords.compute layout.")]
        public ComputeShader CustomSkinningCompute;
        
        [PropertyLabel("Main Light Position")]
        public Vector3 _CharacterLocalMainLightPosition = Vector3.zero;
        private Vector3 last_pos;
        private Transform HeadBone;
        [PropertyLabel("Light Type Strength", "Key Light", "Fill Light", 0f, 1f, "Shadow Tint (Non-Skin Regions)",  "Shadow Tint (Skin/Region 0)")]
        public Vector4 _NewLocalLightStrength = Vector4.zero;
        [PropertyLabel("Main Light Color")]
        public Color _CharacterLocalMainLightColor = Color.white;
        [PropertyLabel("Local Key Light Tint / Directional Blend")]
        public Color _CharacterLocalMainLightColor1 = Color.black;
        [PropertyLabel("Local Fill Light Tint")]
        public Color _CharacterLocalMainLightColor2 = Color.black;
        [PropertyLabel("Shadow Tint (Non-Skin Regions)")]
        public Color _CharacterLocalMainLightDark = Color.black;
        [PropertyLabel("Shadow Tint (Skin/Region 0)")]
        public Color _CharacterLocalMainLightDark1 = Color.black;

        // [Header("Local Light Override")]
        [PropertyLabel("Local Light Direction")]
        public Vector3 _NewLocalLightDir = new Vector3(0, 1, 0);
        [PropertyLabel("Local Light Character Center")]
        public Vector3 _NewLocalLightCharCenter = Vector3.zero;
        
        [PropertyLabel("Sync Light with Character Light")]
        [Tooltip("Whether to sync the new local light direction with the CharacterLight direction.")]
        public bool _SyncNewLocalLightDirWithCharacterLight = true;
        [PropertyLabel("Invert Synced Direction")]
        [Tooltip("Invert the synced new local light direction when true.")]
        public bool _InvertSyncedNewLocalLightDir = false;

        // [Header("Overrides")]
        [PropertyLabel("Disable Character Light")]
        [Range(0, 1)] public float _DisableCharacterLocalLight = 0f;
        [PropertyLabel("Enable Custom Camera Override")]
        [Range(0, 1)] public float _EnableCustomCameraOverride = 1f;

        [Header("Material Mapping")]
        public List<EffectMaterialEntry> EffectMaterials = new List<EffectMaterialEntry>();
        public Renderer[] renderers;
        public float _StencilEyeValue;
        private bool m_RendererScopeDirty = true;
        private bool m_HasSyncedState;
        private int m_LastSyncedStateHash;
        private Vector3 m_LastPosition;
        private bool m_HasValidatedClassificationEntries;
        private readonly Dictionary<Renderer, RendererConstantBufferState> m_RendererConstantBufferStates = new Dictionary<Renderer, RendererConstantBufferState>();
        private readonly CrpPerDrawExData[] m_CrpPerDrawExUpload = new CrpPerDrawExData[1];
        private Vector4 m_CharacterSelfShadowAtlasRect = Vector4.zero;
        private float m_CharacterSelfShadowSliceIndex;
        private float m_CharacterSelfShadowValid;
        private readonly List<ComputeSkinSegment> m_ComputeSkinSegments = new List<ComputeSkinSegment>();
        private readonly Dictionary<Renderer, int> m_ComputeVertexOffsets = new Dictionary<Renderer, int>();
        private ComputeBuffer m_ComputeBaseVerticesBuffer;
        private ComputeBuffer m_ComputeBoneWeightsBuffer;
        private ComputeBuffer m_ComputeBoneIndicesBuffer;
        private ComputeBuffer m_ComputeBoneMatricesBuffer;
        private ComputeBuffer m_ComputeOutputBuffer;
        private Matrix4x4[] m_ComputeBoneMatricesUpload;
        private int m_ComputeKernel = -1;
        private bool m_ComputeSkinningInitialized;
        private bool m_ComputeSkinningDirty = true;
        private bool m_HasLoggedComputeSkinningError;

        private void RefreshScopedRenderers()
        {
            var scoped = new List<Renderer>();
            var candidates = GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < candidates.Length; ++i)
            {
                var renderer = candidates[i];
                if (renderer == null)
                    continue;

                if (renderer.GetComponentInParent<HSRCharacterController>() != this)
                    continue;

                scoped.Add(renderer);
            }

            renderers = scoped.ToArray();
            PruneRendererConstantBuffers();
            SyncCharacterLightCullingMasks();
            ApplyRuntimeTags();
            m_RendererScopeDirty = false;
            m_HasSyncedState = false;
            m_ComputeSkinningDirty = true;
            s_TopologyDirty = true;
        }

        private void PruneRendererConstantBuffers()
        {
            if (m_RendererConstantBufferStates.Count == 0)
                return;

            HashSet<Renderer> activeRenderers = new HashSet<Renderer>();
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; ++i)
                {
                    if (renderers[i] != null)
                        activeRenderers.Add(renderers[i]);
                }
            }

            List<Renderer> staleRenderers = new List<Renderer>();
            foreach (var kvp in m_RendererConstantBufferStates)
            {
                if (!activeRenderers.Contains(kvp.Key))
                    staleRenderers.Add(kvp.Key);
            }

            for (int i = 0; i < staleRenderers.Count; ++i)
                ReleaseRendererConstantBuffer(staleRenderers[i]);
        }

        private void ApplyRuntimeTags()
        {
            EnsureRequiredClassificationEntries();

            var rendererArray = renderers;
            if (rendererArray == null)
                return;

            for (int i = 0; i < rendererArray.Length; ++i)
            {
                var renderer = rendererArray[i];
                if (renderer == null)
                    continue;

                string rendererName = renderer.name;
                if (string.IsNullOrEmpty(rendererName))
                    continue;

                if (rendererName.IndexOf(HairToken, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                TryAssignTag(renderer.gameObject, HairTag);
            }
        }

        private void ApplyLayerToOwnedHierarchy()
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < transforms.Length; ++i)
            {
                Transform current = transforms[i];
                if (current == null)
                    continue;

                if (current.GetComponentInParent<HSRCharacterController>() != this)
                    continue;

                TryAssignLayer(current.gameObject, CharacterRootLayer);
            }
        }

        private void EnsureRequiredClassificationEntries()
        {
            if (m_HasValidatedClassificationEntries)
                return;

            bool hairTagReady = EnsureTagExists(HairTag);
            m_HasValidatedClassificationEntries = hairTagReady;
        }

        private static bool IsNamedSceneLight(Light light)
        {
            return light != null && light.gameObject != null && string.Equals(light.gameObject.name, SceneLightName, StringComparison.Ordinal);
        }

        private Light ResolveOwnedCharacterLight()
        {
            if (CharacterLight != null &&
                CharacterLight.transform != null &&
                CharacterLight.transform != transform &&
                CharacterLight.transform.IsChildOf(transform) &&
                CharacterLight != CharacterShadowLight &&
                !string.Equals(CharacterLight.gameObject.name, CharacterShadowLightName, StringComparison.Ordinal))
                return CharacterLight;

            Light fallback = null;
            Light[] childLights = GetComponentsInChildren<Light>(includeInactive: true);
            for (int i = 0; i < childLights.Length; ++i)
            {
                Light childLight = childLights[i];
                if (childLight == null || childLight.transform == transform)
                    continue;

                if (!childLight.transform.IsChildOf(transform))
                    continue;

                if (childLight == CharacterShadowLight || string.Equals(childLight.gameObject.name, CharacterShadowLightName, StringComparison.Ordinal))
                    continue;

                if (string.Equals(childLight.gameObject.name, CharacterLightName, StringComparison.Ordinal))
                    return childLight;

                if (fallback == null)
                    fallback = childLight;
            }

            return fallback;
        }

        private static Light EnsureDirectionalLightComponent(GameObject target, string lightName)
        {
            if (target == null)
                return null;

            Light[] existingLights = target.GetComponents<Light>();
            Light light = existingLights.Length > 0 ? existingLights[0] : null;
            if (light == null)
                light = target.AddComponent<Light>();

            if (light == null)
            {
                Debug.LogWarning($"HSRCharacterController: Failed to resolve Light component on '{target.name}' for '{lightName}'.");
                return null;
            }

            light.type = LightType.Directional;
            return light;
        }

        private Light EnsureOwnedCharacterLight()
        {
            Light ownedCharacterLight = ResolveOwnedCharacterLight();
            if (ownedCharacterLight != null)
            {
                ownedCharacterLight.type = LightType.Directional;
                return ownedCharacterLight;
            }

            Transform existing = transform.Find(CharacterLightName);
            if (existing != null)
                return EnsureDirectionalLightComponent(existing.gameObject, CharacterLightName);

            GameObject lightObject = new GameObject(CharacterLightName);
            lightObject.transform.SetParent(transform, false);
            return EnsureDirectionalLightComponent(lightObject, CharacterLightName);
        }

        private Light EnsureOwnedCharacterShadowLight()
        {
            if (CharacterShadowLight != null &&
                CharacterShadowLight.transform != null &&
                CharacterShadowLight.transform != transform &&
                CharacterShadowLight.transform.IsChildOf(transform) &&
                CharacterShadowLight != CharacterLight)
            {
                CharacterShadowLight.type = LightType.Directional;
                return CharacterShadowLight;
            }

            Transform existing = transform.Find(CharacterShadowLightName);
            if (existing != null)
                return EnsureDirectionalLightComponent(existing.gameObject, CharacterShadowLightName);

            GameObject lightObject = new GameObject(CharacterShadowLightName);
            lightObject.transform.SetParent(transform, false);
            return EnsureDirectionalLightComponent(lightObject, CharacterShadowLightName);
        }

        private static Light ResolveSceneLightReference()
        {
            Light[] sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light resolved = null;
            int resolvedId = int.MaxValue;
            for (int i = 0; i < sceneLights.Length; ++i)
            {
                Light light = sceneLights[i];
                if (!IsNamedSceneLight(light))
                    continue;

                int instanceId = light.GetInstanceID();
                if (instanceId < resolvedId)
                {
                    resolved = light;
                    resolvedId = instanceId;
                }
            }

            return resolved;
        }

        private void RefreshLightReferences()
        {
            CharacterLight = EnsureOwnedCharacterLight();
            CharacterShadowLight = EnsureOwnedCharacterShadowLight();
            SceneLight = ResolveSceneLightReference();
            SyncCharacterLightCullingMasks();
        }

        private void SyncCharacterLightCullingMasks()
        {
            int controllerLayerMask = 1 << gameObject.layer;
            SyncCharacterLightCullingMask(CharacterLight, controllerLayerMask);
            SyncCharacterLightCullingMask(CharacterShadowLight, controllerLayerMask);
        }

        private static void SyncCharacterLightCullingMask(Light light, int cullingMask)
        {
            if (light == null || light.cullingMask == cullingMask)
                return;

            light.cullingMask = cullingMask;
        }

        private static void TryAssignLayer(GameObject target, string layerName)
        {
            if (target == null || string.IsNullOrEmpty(layerName))
                return;

            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning($"HSRCharacterController: Layer '{layerName}' does not exist, cannot assign layer to '{target.name}'.");
                return;
            }

            if (target.layer == layer)
                return;

            try
            {
                target.layer = layer;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"HSRCharacterController: Failed assigning layer '{layerName}' to '{target.name}'. Exception: {ex.Message}");
            }
        }

        private static void TryAssignTag(GameObject target, string tagName)
        {
            if (target == null || string.IsNullOrEmpty(tagName))
                return;

            if (string.Equals(target.tag, tagName, StringComparison.Ordinal))
                return;

            try
            {
                target.tag = tagName;
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"HSRCharacterController: Failed assigning tag '{tagName}' to '{target.name}'. Exception: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning($"HSRCharacterController: Invalid tag assignment '{tagName}' on '{target.name}'. Exception: {ex.Message}");
            }
        }

        private static bool EnsureLayerExists(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return false;

            if (LayerMask.NameToLayer(layerName) >= 0)
                return true;

#if UNITY_EDITOR
            try
            {
                SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                SerializedProperty layersProperty = tagManager.FindProperty("layers");

                for (int i = 8; i < 32; ++i)
                {
                    SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(layerProperty.stringValue))
                        continue;

                    layerProperty.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    tagManager.Update();
                    return LayerMask.NameToLayer(layerName) >= 0;
                }

                Debug.LogWarning($"HSRCharacterController: Could not create layer '{layerName}' because all user layer slots are occupied.");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"HSRCharacterController: Failed to create layer '{layerName}'. Exception: {ex.Message}");
                return false;
            }
#else
            Debug.LogWarning($"HSRCharacterController: Layer '{layerName}' is missing at runtime and cannot be created outside the editor.");
            return false;
#endif
        }

        private static bool EnsureTagExists(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return false;

#if UNITY_EDITOR
            try
            {
                string[] tags = InternalEditorUtility.tags;
                for (int i = 0; i < tags.Length; ++i)
                {
                    if (string.Equals(tags[i], tagName, StringComparison.Ordinal))
                        return true;
                }

                SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                SerializedProperty tagsProperty = tagManager.FindProperty("tags");

                for (int i = 0; i < tagsProperty.arraySize; ++i)
                {
                    SerializedProperty tagProperty = tagsProperty.GetArrayElementAtIndex(i);
                    if (string.Equals(tagProperty.stringValue, tagName, StringComparison.Ordinal))
                        return true;
                }

                tagsProperty.InsertArrayElementAtIndex(tagsProperty.arraySize);
                tagsProperty.GetArrayElementAtIndex(tagsProperty.arraySize - 1).stringValue = tagName;
                tagManager.ApplyModifiedProperties();
                tagManager.Update();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"HSRCharacterController: Failed to create tag '{tagName}'. Exception: {ex.Message}");
                return false;
            }
#else
            try
            {
                GameObject.FindGameObjectsWithTag(tagName);
                return true;
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"HSRCharacterController: Tag '{tagName}' is missing at runtime and cannot be created outside the editor. Exception: {ex.Message}");
                return false;
            }
#endif
        }

        private static void PruneNullControllers()
        {
            bool removed = false;
            for (int i = s_ActiveControllers.Count - 1; i >= 0; --i)
            {
                if (s_ActiveControllers[i] == null)
                {
                    s_ActiveControllers.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
                s_TopologyDirty = true;
        }

        private static void Register(HSRCharacterController controller)
        {
            if (controller == null)
                return;

            PruneNullControllers();
            if (!s_ActiveControllers.Contains(controller))
            {
                s_ActiveControllers.Add(controller);
                s_TopologyDirty = true;
            }
        }

        private static void Unregister(HSRCharacterController controller)
        {
            if (controller == null)
                return;

            if (s_ActiveControllers.Remove(controller))
                s_TopologyDirty = true;
        }

        private static void EnsureRegistryWithSlowPath(bool force)
        {
            PruneNullControllers();

            if (!force && s_ActiveControllers.Count > 0)
                return;

            if (!force && Time.realtimeSinceStartup < s_NextSlowRegistryDiscoveryTime)
                return;

            var found = UnityEngine.Object.FindObjectsByType<HSRCharacterController>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; ++i)
                Register(found[i]);

            s_NextSlowRegistryDiscoveryTime = Time.realtimeSinceStartup + RegistrySlowDiscoveryInterval;
        }

        public static HSRCharacterController GetPrimaryCachedOrFind()
        {
            PruneNullControllers();
            for (int i = 0; i < s_ActiveControllers.Count; ++i)
            {
                var controller = s_ActiveControllers[i];
                if (controller != null && controller.isActiveAndEnabled)
                    return controller;
            }

            EnsureRegistryWithSlowPath(force: false);

            for (int i = 0; i < s_ActiveControllers.Count; ++i)
            {
                var controller = s_ActiveControllers[i];
                if (controller != null && controller.isActiveAndEnabled)
                    return controller;
            }

            return null;
        }

        public static int GetActiveControllers(List<HSRCharacterController> results, bool forceRefresh = false)
        {
            if (results == null)
                return 0;

            EnsureRegistryWithSlowPath(forceRefresh);
            PruneNullControllers();

            results.Clear();
            for (int i = 0; i < s_ActiveControllers.Count; ++i)
            {
                var controller = s_ActiveControllers[i];
                if (controller != null && controller.isActiveAndEnabled)
                    results.Add(controller);
            }

            return results.Count;
        }

        public static void SyncAllToRenderer()
        {
            EnsureRegistryWithSlowPath(force: false);
            PruneNullControllers();

            bool topologyChanged = s_TopologyDirty;
            if (topologyChanged)
                RebuildTrackedRendererCacheAndResetStale();

            bool anyControllerSynced = false;
            for (int i = 0; i < s_ActiveControllers.Count; ++i)
            {
                var controller = s_ActiveControllers[i];
                if (controller == null || !controller.isActiveAndEnabled)
                    continue;

                if (controller.TrySyncToRendererIfDirty(topologyChanged))
                    anyControllerSynced = true;
            }

            if (!topologyChanged && !anyControllerSynced)
                return;
        }

        public Renderer[] GetScopedRenderers()
        {
            if (m_RendererScopeDirty || renderers == null)
                RefreshScopedRenderers();

            return renderers;
        }

        public bool TryGetCharacterSelfShadowBounds(out Bounds bounds)
        {
            bounds = default;

            Renderer[] scopedRenderers = GetScopedRenderers();
            if (scopedRenderers == null || scopedRenderers.Length == 0)
                return false;

            bool hasBounds = false;
            for (int i = 0; i < scopedRenderers.Length; ++i)
            {
                Renderer renderer = scopedRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        public void SetCharacterSelfShadowState(Vector4 atlasRect, int sliceIndex, bool valid)
        {
            float targetSlice = Mathf.Max(0, sliceIndex);
            float targetValid = valid ? 1f : 0f;

            bool changed =
                m_CharacterSelfShadowAtlasRect != atlasRect ||
                !Mathf.Approximately(m_CharacterSelfShadowSliceIndex, targetSlice) ||
                !Mathf.Approximately(m_CharacterSelfShadowValid, targetValid);

            if (!changed)
                return;

            m_CharacterSelfShadowAtlasRect = atlasRect;
            m_CharacterSelfShadowSliceIndex = targetSlice;
            m_CharacterSelfShadowValid = targetValid;

            TrySyncToRendererIfDirty(force: true);
        }

        private static void RebuildTrackedRendererCacheAndResetStale()
        {
            s_CurrentTrackedRenderers.Clear();

            for (int i = 0; i < s_ActiveControllers.Count; ++i)
            {
                var controller = s_ActiveControllers[i];
                if (controller == null || !controller.isActiveAndEnabled)
                    continue;

                if (controller.m_RendererScopeDirty || controller.renderers == null)
                    controller.RefreshScopedRenderers();

                var rendererArray = controller.renderers;
                if (rendererArray == null)
                    continue;

                for (int j = 0; j < rendererArray.Length; ++j)
                {
                    var renderer = rendererArray[j];
                    if (renderer != null)
                        s_CurrentTrackedRenderers.Add(renderer);
                }
            }

            s_StaleRenderers.Clear();
            foreach (var previousRenderer in s_TrackedRenderers)
            {
                if (!s_CurrentTrackedRenderers.Contains(previousRenderer))
                    s_StaleRenderers.Add(previousRenderer);
            }

            for (int i = 0; i < s_StaleRenderers.Count; ++i)
            {
                var staleRenderer = s_StaleRenderers[i];
                if (staleRenderer == null)
                    continue;

                for (int c = 0; c < s_ActiveControllers.Count; ++c)
                {
                    var controller = s_ActiveControllers[c];
                    if (controller == null)
                        continue;

                    controller.ReleaseRendererConstantBuffer(staleRenderer);
                }
            }

            s_TrackedRenderers.Clear();
            foreach (var renderer in s_CurrentTrackedRenderers)
                s_TrackedRenderers.Add(renderer);

            s_StaleRenderers.Clear();
            s_TopologyDirty = false;
        }

        private void OnEnable()
        {
            TryAssignDefaultComputeSkinningShader();
            RefreshLightReferences();
            RefreshScopedRenderers();
            m_ComputeSkinningDirty = true;
            Register(this);
            NotifySceneControllerCharacterLightRegistration(CharacterLight, register: true);
            DetermineStencilEyeValueFromName();
            SyncToRenderer();
        }

        private void OnTransformChildrenChanged()
        {
            RefreshLightReferences();
            m_RendererScopeDirty = true;
            RefreshScopedRenderers();
            SyncToRenderer();
        }

        private void OnDisable()
        {
            TeardownComputeSkinning();
            ReleaseAllRendererConstantBuffers();
            Unregister(this);
            NotifySceneControllerCharacterLightRegistration(CharacterLight, register: false);
        }

        private void OnDestroy()
        {
            TeardownComputeSkinning();
            ReleaseAllRendererConstantBuffers();
            Unregister(this);
            NotifySceneControllerCharacterLightRegistration(CharacterLight, register: false);
        }

        private static void NotifySceneControllerCharacterLightRegistration(Light light, bool register)
        {
            if (light == null)
                return;

            if (!TryResolveSceneControllerLightHooks())
                return;

            MethodInfo targetMethod = register ? s_RegisterCharacterLightMethod : s_UnregisterCharacterLightMethod;
            if (targetMethod == null)
                return;

            try
            {
                targetMethod.Invoke(null, new object[] { light });
            }
            catch (Exception)
            {
                // Rendering assembly is optional for runtime; ignore hook failures.
            }
        }

        private static bool TryResolveSceneControllerLightHooks()
        {
            if (s_HasTriedResolveSceneControllerHooks)
                return s_RegisterCharacterLightMethod != null && s_UnregisterCharacterLightMethod != null;

            s_HasTriedResolveSceneControllerHooks = true;

            Type sceneControllerType = Type.GetType(SceneControllerTypeName, throwOnError: false);
            if (sceneControllerType == null)
                return false;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            s_RegisterCharacterLightMethod = sceneControllerType.GetMethod("RegisterCharacterLight", flags, binder: null, types: new[] { typeof(Light) }, modifiers: null);
            s_UnregisterCharacterLightMethod = sceneControllerType.GetMethod("UnregisterCharacterLight", flags, binder: null, types: new[] { typeof(Light) }, modifiers: null);
            return s_RegisterCharacterLightMethod != null && s_UnregisterCharacterLightMethod != null;
        }

        private void OnValidate()
        {
            TryAssignDefaultComputeSkinningShader();
            SanitizeEffectMaterialEntries();
            RefreshLightReferences();
            m_HasSyncedState = false;
            m_ComputeSkinningDirty = true;
            s_TopologyDirty = true;

            if (isActiveAndEnabled)
                TrySyncToRendererIfDirty(force: true);
        }

        private void SanitizeEffectMaterialEntries()
        {
            if (EffectMaterials == null || EffectMaterials.Count == 0)
                return;

            for (int i = 0; i < EffectMaterials.Count; ++i)
            {
                EffectMaterialEntry entry = EffectMaterials[i];
                if (entry.Type != EffectType.None || entry.Material == null)
                    continue;

                entry.Material = null;
                EffectMaterials[i] = entry;
            }
        }

        private void Reset()
        {
            TryAssignDefaultComputeSkinningShader();
            RefreshLightReferences();

            CharacterLight = EnsureOwnedCharacterLight();
            CharacterShadowLight = EnsureOwnedCharacterShadowLight();
            SyncCharacterLightCullingMasks();

            if (HeadBone == null)
                HeadBone = BoneUtility.FindChildRecursive(transform, "Head") ?? BoneUtility.FindChildRecursive(transform, "Head_M");

            SceneLight = ResolveSceneLightReference();
        }

        public void SyncToRenderer()
        {
            if (m_RendererScopeDirty || renderers == null)
                RefreshScopedRenderers();

            SyncToRendererInternal();
            m_LastSyncedStateHash = ComputeSyncStateHash();
            m_HasSyncedState = true;
        }

        private bool TrySyncToRendererIfDirty(bool force)
        {
            if (m_RendererScopeDirty || renderers == null)
                RefreshScopedRenderers();

            int currentHash = ComputeSyncStateHash();
            if (!force && m_HasSyncedState && currentHash == m_LastSyncedStateHash)
                return false;

            SyncToRendererInternal();
            m_LastSyncedStateHash = currentHash;
            m_HasSyncedState = true;
            return true;
        }

        private void SyncToRendererInternal()
        {
            if (renderers == null)
                return;

            CrpPerDrawExData perDrawExData = new CrpPerDrawExData
            {
                _CharacterLocalMainLightPosition = (Vector4)_CharacterLocalMainLightPosition,
                _CharacterLocalMainLightColor = _CharacterLocalMainLightColor,
                _CharacterLocalMainLightColor1 = _CharacterLocalMainLightColor1,
                _CharacterLocalMainLightColor2 = _CharacterLocalMainLightColor2,
                _CharacterLocalMainLightDark = _CharacterLocalMainLightDark,
                _CharacterLocalMainLightDark1 = _CharacterLocalMainLightDark1,
                _NewLocalLightDir = (Vector4)_NewLocalLightDir,
                _NewLocalLightCharCenter = (Vector4)_NewLocalLightCharCenter,
                _NewLocalLightStrength = _NewLocalLightStrength,
                _DisableCharacterLocalLight = _DisableCharacterLocalLight,
                _EnableCustomCameraOverride = _EnableCustomCameraOverride,
                _Padding = Vector2.zero,
                _CharacterSelfShadowAtlasRect = m_CharacterSelfShadowAtlasRect,
                _CharacterSelfShadowSliceIndex = m_CharacterSelfShadowSliceIndex,
                _CharacterSelfShadowValid = m_CharacterSelfShadowValid,
                _PadCharacterSelfShadow = Vector2.zero
            };

            m_CrpPerDrawExUpload[0] = perDrawExData;

            foreach (var ren in renderers)
            {
                if (ren == null)
                    continue;

                RendererConstantBufferState state = GetOrCreateRendererConstantBufferState(ren);
                state.constantBuffer.SetData(m_CrpPerDrawExUpload);

                ren.GetPropertyBlock(state.propertyBlock);
                state.propertyBlock.SetConstantBuffer(s_CrpPerDrawExId, state.constantBuffer, 0, s_CrpPerDrawExSize);
                state.propertyBlock.SetFloat(s_StencilEyeId, _StencilEyeValue);
                state.propertyBlock.SetVector(s_CharacterSelfShadowAtlasRectId, m_CharacterSelfShadowAtlasRect);
                state.propertyBlock.SetFloat(s_CharacterSelfShadowSliceIndexId, m_CharacterSelfShadowSliceIndex);
                state.propertyBlock.SetFloat(s_CharacterSelfShadowValidId, m_CharacterSelfShadowValid);

                int computeVertexOffset = 0;
                bool hasComputeSkinning = m_ComputeSkinningInitialized && m_ComputeVertexOffsets.TryGetValue(ren, out computeVertexOffset);
                state.propertyBlock.SetInteger(s_HsrComputeSkinningEnabledId, hasComputeSkinning ? 1 : 0);
                state.propertyBlock.SetInteger(s_HsrComputeSkinningVertexOffsetId, hasComputeSkinning ? computeVertexOffset : 0);
                if (hasComputeSkinning)
                    state.propertyBlock.SetBuffer(s_HsrComputeSkinnedVerticesId, m_ComputeOutputBuffer);

                ren.SetPropertyBlock(state.propertyBlock);
                SyncStencilEyeMaterialOverride(ren);
            }
        }

        private void SyncStencilEyeMaterialOverride(Renderer renderer)
        {
            if (renderer == null)
                return;

            renderer.GetSharedMaterials(s_RendererMaterials);
            try
            {
                for (int i = 0; i < s_RendererMaterials.Count; ++i)
                {
                    Material material = s_RendererMaterials[i];
                    if (material == null || !material.HasProperty(s_StencilEyeId))
                        continue;

                    if (Mathf.Approximately(material.GetFloat(s_StencilEyeId), _StencilEyeValue))
                        continue;

                    material.SetFloat(s_StencilEyeId, _StencilEyeValue);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        EditorUtility.SetDirty(material);
#endif
                }
            }
            finally
            {
                s_RendererMaterials.Clear();
            }
        }

        private RendererConstantBufferState GetOrCreateRendererConstantBufferState(Renderer renderer)
        {
            if (!m_RendererConstantBufferStates.TryGetValue(renderer, out var state))
            {
                state = new RendererConstantBufferState();
                m_RendererConstantBufferStates.Add(renderer, state);
            }

            return state;
        }

        private void ReleaseRendererConstantBuffer(Renderer renderer)
        {
            if (renderer == null)
                return;

            if (!m_RendererConstantBufferStates.TryGetValue(renderer, out var state))
                return;

            state.propertyBlock.Clear();
            renderer.SetPropertyBlock(state.propertyBlock);
            state.constantBuffer.Release();
            m_RendererConstantBufferStates.Remove(renderer);
        }

        private void ReleaseAllRendererConstantBuffers()
        {
            if (m_RendererConstantBufferStates.Count == 0)
                return;

            List<Renderer> renderersToRelease = new List<Renderer>(m_RendererConstantBufferStates.Keys);
            for (int i = 0; i < renderersToRelease.Count; ++i)
                ReleaseRendererConstantBuffer(renderersToRelease[i]);
        }

        private int ComputeSyncStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _CharacterLocalMainLightPosition.GetHashCode();
                hash = hash * 31 + _CharacterLocalMainLightColor.GetHashCode();
                hash = hash * 31 + _CharacterLocalMainLightColor1.GetHashCode();
                hash = hash * 31 + _CharacterLocalMainLightColor2.GetHashCode();
                hash = hash * 31 + _CharacterLocalMainLightDark.GetHashCode();
                hash = hash * 31 + _CharacterLocalMainLightDark1.GetHashCode();
                hash = hash * 31 + _NewLocalLightDir.GetHashCode();
                hash = hash * 31 + _NewLocalLightCharCenter.GetHashCode();
                hash = hash * 31 + _NewLocalLightStrength.GetHashCode();
                hash = hash * 31 + _DisableCharacterLocalLight.GetHashCode();
                hash = hash * 31 + _EnableCustomCameraOverride.GetHashCode();
                hash = hash * 31 + _StencilEyeValue.GetHashCode();
                hash = hash * 31 + (int)SkinningMode;
                hash = hash * 31 + (CustomSkinningCompute != null ? CustomSkinningCompute.GetInstanceID() : 0);
                hash = hash * 31 + m_CharacterSelfShadowAtlasRect.GetHashCode();
                hash = hash * 31 + m_CharacterSelfShadowSliceIndex.GetHashCode();
                hash = hash * 31 + m_CharacterSelfShadowValid.GetHashCode();

                int rendererCount = renderers != null ? renderers.Length : 0;
                hash = hash * 31 + rendererCount;
                for (int i = 0; i < rendererCount; ++i)
                {
                    var renderer = renderers[i];
                    int rendererId = renderer != null ? renderer.GetInstanceID() : 0;
                    hash = hash * 31 + rendererId;
                }

                return hash;
            }
        }

        private void DetermineStencilEyeValueFromName()
        {
            string keyString = gameObject.name ?? string.Empty;
            keyString = string.IsNullOrEmpty(keyString)
                ? gameObject.GetInstanceID().ToString()
                : keyString + "_" + gameObject.GetInstanceID();
            int hash = keyString.GetHashCode();
            int normalized = Mathf.Abs(hash) % 127;
            int baseId = normalized * 2 + 1;
            _StencilEyeValue = ResolveUniqueStencilEye(baseId);
        }

        private int ResolveUniqueStencilEye(int desired)
        {
            EnsureRegistryWithSlowPath(force: false);
            var used = new HashSet<int>();
            for (int i = 0; i < s_ActiveControllers.Count; ++i)
            {
                var controller = s_ActiveControllers[i];
                if (controller == null || controller == this)
                    continue;

                int value = Mathf.RoundToInt(controller._StencilEyeValue);
                if (value > 0)
                    used.Add(value);
            }

            if (used.Count >= 128)
                return desired;

            int startIndex = (desired - 1) / 2;
            for (int attempt = 0; attempt < 128; ++attempt)
            {
                int offset;
                if (attempt == 0)
                    offset = 0;
                else if ((attempt & 1) == 1)
                    offset = (attempt + 1) / 2;
                else
                    offset = -(attempt / 2);

                int index = (startIndex + offset) % 128;
                if (index < 0)
                    index += 128;

                int candidate = index * 2 + 1;
                if (!used.Contains(candidate))
                    return candidate;
            }

            return desired;
        }

        private void Update()
        {
            if (CharacterLight == null || CharacterLight.transform == null || !CharacterLight.transform.IsChildOf(transform) || !IsNamedSceneLight(SceneLight))
                RefreshLightReferences();

            if (SyncWithSceneLight)
                SyncLight();

            OrbitLight();
            SyncCharacterShadowLight();
        }

        private void SyncCharacterShadowLight()
        {
            if (!SyncCharacterShadowLightWithCharacterLight)
                return;

            if (CharacterLight == null || CharacterShadowLight == null)
                return;

            CharacterShadowLight.transform.SetPositionAndRotation(
                CharacterLight.transform.position,
                CharacterLight.transform.rotation);

            CharacterShadowLight.color = CharacterLight.color;
        }

        private void LateUpdate()
        {
            UpdateComputeSkinning();
            TrySyncToRendererIfDirty(force: false);
        }

        private bool IsComputeSkinningRequested()
        {
            TryAssignDefaultComputeSkinningShader();
            return SkinningMode == CharacterSkinningMode.Compute && CustomSkinningCompute != null;
        }

        private void TryAssignDefaultComputeSkinningShader()
        {
            if (CustomSkinningCompute != null)
                return;

#if UNITY_EDITOR
            CustomSkinningCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultSkinningComputeShaderAssetPath);
            if (CustomSkinningCompute != null)
            {
                EditorUtility.SetDirty(this);
                m_ComputeSkinningDirty = true;
            }
#endif
        }

        private void UpdateComputeSkinning()
        {
            if (!IsComputeSkinningRequested())
            {
                if (m_ComputeSkinningInitialized)
                {
                    TeardownComputeSkinning();
                    m_HasSyncedState = false;
                }

                return;
            }

            if (!SystemInfo.supportsComputeShaders)
            {
                LogComputeSkinningErrorOnce("HSRCharacterController: Compute skinning requested but compute shaders are unsupported on this platform.");
                return;
            }

            if (m_ComputeSkinningDirty || !m_ComputeSkinningInitialized)
            {
                RebuildComputeSkinning();
                m_HasSyncedState = false;
            }

            if (!m_ComputeSkinningInitialized)
                return;

            if (m_ComputeBaseVerticesBuffer == null ||
                m_ComputeBoneWeightsBuffer == null ||
                m_ComputeBoneIndicesBuffer == null ||
                m_ComputeBoneMatricesBuffer == null ||
                m_ComputeOutputBuffer == null)
            {
                m_ComputeSkinningDirty = true;
                RebuildComputeSkinning();
                m_HasSyncedState = false;
                if (!m_ComputeSkinningInitialized)
                    return;
            }

            UploadComputeBoneMatrices();
            DispatchComputeSkinning();
        }

        private void RebuildComputeSkinning()
        {
            TeardownComputeSkinning();
            m_ComputeSkinningDirty = false;

            if (renderers == null || m_RendererScopeDirty)
                RefreshScopedRenderers();

            m_ComputeSkinSegments.Clear();
            m_ComputeVertexOffsets.Clear();

            int totalVertexCount = 0;
            int totalMatrixCount = 0;

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; ++i)
                {
                    SkinnedMeshRenderer skinnedRenderer = renderers[i] as SkinnedMeshRenderer;
                    if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
                        continue;

                    Mesh mesh = skinnedRenderer.sharedMesh;
                    if (!mesh.isReadable)
                    {
                        LogComputeSkinningErrorOnce($"HSRCharacterController: Mesh '{mesh.name}' is not Read/Write enabled; skipping compute skinning for this renderer.");
                        continue;
                    }

                    BoneWeight[] boneWeights = mesh.boneWeights;
                    Vector3[] vertices = mesh.vertices;
                    Matrix4x4[] bindPoses = mesh.bindposes;
                    if (vertices == null || vertices.Length == 0 || boneWeights == null || boneWeights.Length != vertices.Length || bindPoses == null || bindPoses.Length == 0)
                        continue;

                    var segment = new ComputeSkinSegment
                    {
                        renderer = skinnedRenderer,
                        mesh = mesh,
                        bones = skinnedRenderer.bones,
                        bindPoses = bindPoses,
                        vertexOffset = totalVertexCount,
                        vertexCount = vertices.Length,
                        matrixOffset = totalMatrixCount,
                        matrixCount = bindPoses.Length
                    };

                    m_ComputeSkinSegments.Add(segment);
                    m_ComputeVertexOffsets[skinnedRenderer] = segment.vertexOffset;
                    totalVertexCount += segment.vertexCount;
                    totalMatrixCount += segment.matrixCount;
                }
            }

            if (m_ComputeSkinSegments.Count == 0 || totalVertexCount == 0 || totalMatrixCount == 0)
                return;

            try
            {
                m_ComputeKernel = CustomSkinningCompute.FindKernel(SkinningKernelName);
            }
            catch (Exception)
            {
                LogComputeSkinningErrorOnce($"HSRCharacterController: Kernel '{SkinningKernelName}' not found on compute shader '{CustomSkinningCompute.name}'.");
                return;
            }

            SkinData[] baseVertices = new SkinData[totalVertexCount];
            Vector4[] weights = new Vector4[totalVertexCount];
            UInt4[] indices = new UInt4[totalVertexCount];
            m_ComputeBoneMatricesUpload = new Matrix4x4[totalMatrixCount];

            for (int s = 0; s < m_ComputeSkinSegments.Count; ++s)
            {
                ComputeSkinSegment segment = m_ComputeSkinSegments[s];
                Mesh mesh = segment.mesh;
                BoneWeight[] boneWeights = mesh.boneWeights;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector4[] tangents = mesh.tangents;
                List<Vector4> uv7 = new List<Vector4>(segment.vertexCount);
                List<Vector4> uv8 = new List<Vector4>(segment.vertexCount);
                mesh.GetUVs(6, uv7);
                mesh.GetUVs(7, uv8);

                bool hasNormals = normals != null && normals.Length == segment.vertexCount;
                bool hasTangents = tangents != null && tangents.Length == segment.vertexCount;
                bool hasUv7 = uv7.Count == segment.vertexCount;
                bool hasUv8 = uv8.Count == segment.vertexCount;

                for (int i = 0; i < segment.vertexCount; ++i)
                {
                    int globalVertexIndex = segment.vertexOffset + i;
                    BoneWeight bw = boneWeights[i];

                    baseVertices[globalVertexIndex] = new SkinData
                    {
                        pos = vertices[i],
                        pad0 = 0f,
                        norm = hasNormals ? normals[i] : Vector3.up,
                        pad1 = 0f,
                        tangent = hasTangents ? tangents[i] : new Vector4(1f, 0f, 0f, 1f),
                        tangent1 = new Vector4(
                            hasUv7 ? uv7[i].x : 0f,
                            hasUv7 ? uv7[i].y : 0f,
                            hasUv8 ? uv8[i].x : 0f,
                            hasUv8 ? uv8[i].y : 1f)
                    };

                    weights[globalVertexIndex] = new Vector4(bw.weight0, bw.weight1, bw.weight2, bw.weight3);
                    indices[globalVertexIndex] = new UInt4((uint)bw.boneIndex0, (uint)bw.boneIndex1, (uint)bw.boneIndex2, (uint)bw.boneIndex3);
                }
            }

            m_ComputeBaseVerticesBuffer = new ComputeBuffer(totalVertexCount, Marshal.SizeOf<SkinData>());
            m_ComputeBoneWeightsBuffer = new ComputeBuffer(totalVertexCount, Marshal.SizeOf<Vector4>());
            m_ComputeBoneIndicesBuffer = new ComputeBuffer(totalVertexCount, Marshal.SizeOf<UInt4>());
            m_ComputeBoneMatricesBuffer = new ComputeBuffer(totalMatrixCount, Marshal.SizeOf<Matrix4x4>());
            m_ComputeOutputBuffer = new ComputeBuffer(totalVertexCount, Marshal.SizeOf<SkinData>());

            m_ComputeBaseVerticesBuffer.SetData(baseVertices);
            m_ComputeBoneWeightsBuffer.SetData(weights);
            m_ComputeBoneIndicesBuffer.SetData(indices);

            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BaseVertices", m_ComputeBaseVerticesBuffer);
            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BoneWeights", m_ComputeBoneWeightsBuffer);
            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BoneIndices", m_ComputeBoneIndicesBuffer);
            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BoneMatrices", m_ComputeBoneMatricesBuffer);
            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_OutputBuffer", m_ComputeOutputBuffer);

            m_ComputeSkinningInitialized = true;
            m_HasLoggedComputeSkinningError = false;
        }

        private void UploadComputeBoneMatrices()
        {
            if (!m_ComputeSkinningInitialized || m_ComputeBoneMatricesUpload == null)
                return;

            for (int s = 0; s < m_ComputeSkinSegments.Count; ++s)
            {
                ComputeSkinSegment segment = m_ComputeSkinSegments[s];
                if (segment.renderer == null)
                    continue;

                Transform skinRoot = segment.renderer.rootBone != null ? segment.renderer.rootBone : segment.renderer.transform;
                Matrix4x4 rootWorldToLocal = skinRoot.worldToLocalMatrix;
                Matrix4x4 fallbackLocalToWorld = skinRoot.localToWorldMatrix;
                Transform[] bones = segment.bones;

                for (int i = 0; i < segment.matrixCount; ++i)
                {
                    Matrix4x4 boneLocalToWorld = fallbackLocalToWorld;
                    if (bones != null && i < bones.Length && bones[i] != null)
                        boneLocalToWorld = bones[i].localToWorldMatrix;

                    int matrixIndex = segment.matrixOffset + i;
                    m_ComputeBoneMatricesUpload[matrixIndex] = rootWorldToLocal * boneLocalToWorld * segment.bindPoses[i];
                }
            }

            m_ComputeBoneMatricesBuffer.SetData(m_ComputeBoneMatricesUpload);
        }

        private void DispatchComputeSkinning()
        {
            if (!m_ComputeSkinningInitialized || CustomSkinningCompute == null)
                return;

            // Rebind every frame to survive shader reimport/reload invalidating kernel bindings.
            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BaseVertices", m_ComputeBaseVerticesBuffer);
            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BoneWeights", m_ComputeBoneWeightsBuffer);
            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BoneIndices", m_ComputeBoneIndicesBuffer);
            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BoneMatrices", m_ComputeBoneMatricesBuffer);
            CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_OutputBuffer", m_ComputeOutputBuffer);

            for (int s = 0; s < m_ComputeSkinSegments.Count; ++s)
            {
                ComputeSkinSegment segment = m_ComputeSkinSegments[s];
                if (segment.renderer == null)
                    continue;

                CustomSkinningCompute.SetInt("_VertexCount", segment.vertexCount);
                CustomSkinningCompute.SetInt("_GlobalVertexOffset", segment.vertexOffset);
                CustomSkinningCompute.SetInt("_GlobalMatrixOffset", segment.matrixOffset);

                int threadGroupsX = Mathf.CeilToInt(segment.vertexCount / (float)SkinningKernelThreadGroupSize);
                CustomSkinningCompute.Dispatch(m_ComputeKernel, threadGroupsX, 1, 1);
            }
        }

        private void TeardownComputeSkinning()
        {
            m_ComputeSkinningInitialized = false;
            m_ComputeSkinningDirty = false;
            m_ComputeKernel = -1;
            m_ComputeSkinSegments.Clear();
            m_ComputeVertexOffsets.Clear();
            m_ComputeBoneMatricesUpload = null;

            ReleaseComputeBuffer(ref m_ComputeBaseVerticesBuffer);
            ReleaseComputeBuffer(ref m_ComputeBoneWeightsBuffer);
            ReleaseComputeBuffer(ref m_ComputeBoneIndicesBuffer);
            ReleaseComputeBuffer(ref m_ComputeBoneMatricesBuffer);
            ReleaseComputeBuffer(ref m_ComputeOutputBuffer);
        }

        private static void ReleaseComputeBuffer(ref ComputeBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void LogComputeSkinningErrorOnce(string message)
        {
            if (m_HasLoggedComputeSkinningError)
                return;

            m_HasLoggedComputeSkinningError = true;
            Debug.LogWarning(message);
        }

        private void SyncLight()
        {
            if (SceneLight == null || CharacterLight == null)
                return;

            CharacterLight.transform.rotation = SceneLight.transform.rotation;
            _CharacterLocalMainLightColor = SceneLight.color;
        }

        private void OrbitLight()
        {
            if (CharacterLight == null || HeadBone == null)
                return;

            Vector3 targetForward;
            if (SyncWithSceneLight && SceneLight != null)
            {
                targetForward = SceneLight.transform.forward;
            }
            else
            {
                targetForward = CharacterLight.transform.forward;
                if (targetForward.sqrMagnitude <= 1e-8f && _CharacterLocalMainLightPosition != Vector3.zero)
                    targetForward = -_CharacterLocalMainLightPosition.normalized;
            }

            if (targetForward.sqrMagnitude <= 1e-8f)
                return;

            targetForward.Normalize();
            SetCharacterLightForward(targetForward);

            float offsetDistance = CharacterLightBaseOffset;
            Camera referenceCamera = ResolveReferenceCamera();
            if (referenceCamera != null)
            {
                float cameraDistance = Vector3.Distance(referenceCamera.transform.position, HeadBone.position);
                offsetDistance += cameraDistance * CharacterLightCameraDistanceFactor;
            }

            offsetDistance = Mathf.Clamp(offsetDistance, 0.1f, CharacterLightMaxOffset);
            Vector3 offset = -targetForward * offsetDistance;
            CharacterLight.transform.position = HeadBone.position + offset;
            OrbitCharacterShadowLight(offsetDistance);

            _CharacterLocalMainLightPosition = -targetForward;
            m_LastPosition = _CharacterLocalMainLightPosition;
            if (_SyncNewLocalLightDirWithCharacterLight)
            {
                var syncedDir = _CharacterLocalMainLightPosition;
                if (_InvertSyncedNewLocalLightDir)
                    syncedDir = -syncedDir;

                _NewLocalLightDir = syncedDir;
            }
        }

        private void OrbitCharacterShadowLight(float offsetDistance)
        {
            if (CharacterShadowLight == null || HeadBone == null)
                return;

            Vector3 targetForward;
            if (SyncCharacterShadowLightWithCharacterLight && CharacterLight != null)
            {
                targetForward = CharacterLight.transform.forward;
            }
            else
            {
                // In unsynced mode, keep orbit direction driven by the shadow light's own rotation.
                targetForward = CharacterShadowLight.transform.forward;
                if (targetForward.sqrMagnitude <= 1e-8f && CharacterLight != null)
                    targetForward = CharacterLight.transform.forward;
            }

            if (targetForward.sqrMagnitude <= 1e-8f)
                return;

            targetForward.Normalize();

            SetLightForward(CharacterShadowLight, targetForward);
            Vector3 offset = -targetForward * offsetDistance;
            CharacterShadowLight.transform.position = HeadBone.position + offset;
        }

        private void SetCharacterLightForward(Vector3 desiredForward)
        {
            SetLightForward(CharacterLight, desiredForward);
        }

        private void SetLightForward(Light targetLight, Vector3 desiredForward)
        {
            if (targetLight == null || desiredForward.sqrMagnitude <= 1e-8f)
                return;

            Vector3 forward = desiredForward.normalized;
            Vector3 upSource = SyncWithSceneLight && SceneLight != null
                ? SceneLight.transform.up
                : transform.up;

            // Build up from stable references to avoid cumulative roll drift after full rotations.
            Vector3 up = Vector3.ProjectOnPlane(upSource, forward);
            if (up.sqrMagnitude <= 1e-8f && HeadBone != null)
                up = Vector3.ProjectOnPlane(HeadBone.up, forward);
            if (up.sqrMagnitude <= 1e-8f)
                up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude <= 1e-8f)
                up = Vector3.ProjectOnPlane(Vector3.right, forward);

            up.Normalize();

            targetLight.transform.rotation = Quaternion.LookRotation(forward, up);
        }

        private static Camera ResolveReferenceCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                return mainCamera;

            return Camera.current;
        }
    }
}
