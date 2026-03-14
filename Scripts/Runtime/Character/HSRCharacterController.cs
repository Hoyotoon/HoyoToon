using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using HoyoToon.Simulator.Utilities;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace HoyoToon.Runtime.Character
{
    [ExecuteAlways]
    public class HSRCharacterController : MonoBehaviour
    {
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
        private const string SceneControllerTypeName = "HoyoToon.Runtime.Scene.HSRSceneController, com.hoyotoon.hoyotoon.Runtime";
        private static bool s_HasTriedResolveSceneControllerHooks;
        private static MethodInfo s_RegisterCharacterLightMethod;
        private static MethodInfo s_UnregisterCharacterLightMethod;
        private static readonly int s_CrpPerDrawExSize = Marshal.SizeOf<CrpPerDrawExData>();

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
        public string CharacterSelfShadowCasterPassName = "ShadowCaster";
        
        public Vector3 _CharacterLocalMainLightPosition = Vector3.zero;
        private Vector3 last_pos;
        public Transform HeadBone;
        public Color _CharacterLocalMainLightColor = Color.white;
        public Color _CharacterLocalMainLightColor1 = Color.black;
        public Color _CharacterLocalMainLightColor2 = Color.black;
        public Color _CharacterLocalMainLightDark = Color.black;
        public Color _CharacterLocalMainLightDark1 = Color.black;

        [Header("New Local Light Override")]
        public Vector3 _NewLocalLightDir = new Vector3(0, 1, 0);
        public Vector3 _NewLocalLightCharCenter = Vector3.zero;
        public Vector4 _NewLocalLightStrength = Vector4.zero;
        [Tooltip("Whether to sync the new local light direction with the CharacterLight direction.")]
        public bool _SyncNewLocalLightDirWithCharacterLight = true;
        [Tooltip("Invert the synced new local light direction when true.")]
        public bool _InvertSyncedNewLocalLightDir = false;

        [Header("Overrides")]
        [Range(0, 1)] public float _DisableCharacterLocalLight = 0f;
        [Range(0, 1)] public float _EnableCustomCameraOverride = 1f;

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
            RefreshLightReferences();
            RefreshScopedRenderers();
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
            ReleaseAllRendererConstantBuffers();
            Unregister(this);
            NotifySceneControllerCharacterLightRegistration(CharacterLight, register: false);
        }

        private void OnDestroy()
        {
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
            RefreshLightReferences();
            m_HasSyncedState = false;
            s_TopologyDirty = true;

            if (isActiveAndEnabled)
                TrySyncToRendererIfDirty(force: true);
        }

        private void Reset()
        {
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
            TrySyncToRendererIfDirty(force: false);
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
