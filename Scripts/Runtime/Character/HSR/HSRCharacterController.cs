using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HoyoToon.Runtime.Core;
using HoyoToon.Runtime.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Runtime.Character.HSR
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
        private const string CharacterLayerName = "Honkai Star Rail";
        private const string HairTag = "Honkai Star Rail Hair";
        private const string HairToken = "Hair";
        private const string PlanarReflectionParticipantTypeName = "PlanarReflectionParticipant";
        private static readonly string[] s_HeadBoneCandidateNames = { "Head", "Head_M" };
        private static readonly List<HSRCharacterController> s_ActiveControllers = new List<HSRCharacterController>();
        private static readonly HashSet<Renderer> s_TrackedRenderers = new HashSet<Renderer>();
        private static readonly HashSet<Renderer> s_CurrentTrackedRenderers = new HashSet<Renderer>();
        private static readonly List<Renderer> s_StaleRenderers = new List<Renderer>();
        private static readonly List<GameObject> s_SceneRootScratch = new List<GameObject>(16);
        private static readonly List<HSRCharacterController> s_ControllerDiscoveryScratch = new List<HSRCharacterController>(8);
        private static readonly Dictionary<int, Light> s_SceneLightBySceneHandle = new Dictionary<int, Light>();
        private static readonly Dictionary<int, float> s_NextSlowRegistryDiscoveryTimeBySceneHandle = new Dictionary<int, float>();
        private static float s_NextSlowRegistryDiscoveryTime;
        private static bool s_TopologyDirty = true;
        private static int s_RendererTopologyVersion;
        private static readonly int s_StencilEyeId = Shader.PropertyToID("_StencilEye");
        private static readonly int s_CrpPerDrawExId = Shader.PropertyToID("CRP_PerDrawEx");
        private static readonly int s_CharacterSelfShadowAtlasRectId = Shader.PropertyToID("_CharacterSelfShadowAtlasRect");
        private static readonly int s_CharacterSelfShadowSliceIndexId = Shader.PropertyToID("_CharacterSelfShadowSliceIndex");
        private static readonly int s_CharacterSelfShadowValidId = Shader.PropertyToID("_CharacterSelfShadowValid");
        private static readonly int s_HsrComputeSkinnedVerticesId = Shader.PropertyToID("_HSRComputeSkinnedVertices");
        private static readonly int s_HsrComputeSkinningEnabledId = Shader.PropertyToID("_HSRComputeSkinningEnabled");
        private static readonly int s_HsrComputeSkinningVertexOffsetId = Shader.PropertyToID("_HSRComputeSkinningVertexOffset");
        private static ComputeBuffer s_GlobalFallbackSkinnedVerticesBuffer;
        private static readonly int s_CrpPerDrawExSize = Marshal.SizeOf<CrpPerDrawExData>();
        private const int SkinningKernelThreadGroupSize = 64;
        private const string SkinningKernelName = "CSMain";

        public static event Action ActiveControllerRegistryChanged;
        public static event Action RendererTopologyChanged;
        public static int RendererTopologyVersion => s_RendererTopologyVersion;

        static HSRCharacterController()
        {
            RuntimeEditorBridge.RegisterEditModeCleanup(ReleaseGlobalFallbackSkinnedVerticesBuffer);
        }

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
            public Transform rootBone;
            public Transform[] bones;
            public Matrix4x4[] bindPoses;
            public BoneWeight[] boneWeights;
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
            ReleaseGlobalFallbackSkinnedVerticesBuffer();
            EnsureGlobalFallbackSkinnedVerticesBufferBound();
            s_NextSlowRegistryDiscoveryTime = 0f;
            s_NextSlowRegistryDiscoveryTimeBySceneHandle.Clear();
            s_SceneRootScratch.Clear();
            s_ControllerDiscoveryScratch.Clear();
            s_TopologyDirty = true;
            s_RendererTopologyVersion = 0;
            s_SceneLightBySceneHandle.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void EnsureGlobalComputeSkinningFallbackOnStartup()
        {
            EnsureGlobalFallbackSkinnedVerticesBufferBound();
        }

        private static ComputeBuffer EnsureGlobalFallbackSkinnedVerticesBufferBound()
        {
            if (s_GlobalFallbackSkinnedVerticesBuffer == null)
            {
                s_GlobalFallbackSkinnedVerticesBuffer = new ComputeBuffer(1, Marshal.SizeOf<SkinData>());
                s_GlobalFallbackSkinnedVerticesBuffer.SetData(new SkinData[1]);
            }

            Shader.SetGlobalBuffer(s_HsrComputeSkinnedVerticesId, s_GlobalFallbackSkinnedVerticesBuffer);
            return s_GlobalFallbackSkinnedVerticesBuffer;
        }

        private static void ReleaseGlobalFallbackSkinnedVerticesBuffer()
        {
            if (s_GlobalFallbackSkinnedVerticesBuffer == null)
                return;

            s_GlobalFallbackSkinnedVerticesBuffer.Release();
            s_GlobalFallbackSkinnedVerticesBuffer = null;
        }

        [Header("Light Sources")]
        public Light CharacterLight;
        public Light SceneLight;
        public Light CharacterShadowLight;

        [Header("Lighting Controls")]

        [PropertyLabel("Sync Light with Character Light")]
        [Tooltip("Whether to sync the new local light direction with the CharacterLight direction.")]
        public bool _SyncNewLocalLightDirWithCharacterLight = true;
        [PropertyLabel("Invert Synced Direction")]
        [Tooltip("Invert the synced new local light direction when true.")]
        public bool _InvertSyncedNewLocalLightDir = false;
        public bool SyncWithSceneLight = true;
        public bool SyncCharacterShadowLightWithCharacterLight = true;

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


        // [Header("Overrides")]
        [PropertyLabel("Disable Character Light")]
        [Range(0, 1)] public float _DisableCharacterLocalLight = 0f;
        [PropertyLabel("Enable Custom Camera Override")]
        [Range(0, 1)] public float _EnableCustomCameraOverride = 1f;

        [Header("Character Self Shadows")]
        public bool EnableCharacterSelfShadow = true;
        [Min(128)] public int CharacterSelfShadowResolution = 2048;
        [Min(0.01f)] public float CharacterSelfShadowOrthographicSize = 0.01f;
        [Min(0.0001f)] public float CharacterSelfShadowNearPlane = 0.01f;
        [Min(0.001f)] public float CharacterSelfShadowFarPlane = 1f;
        [Range(0f, 1f)] public float CharacterSelfShadowLightFollow = 0.5f;
        public bool CharacterSelfShadowInvertLightDirection = false;
        [HideInInspector] public string CharacterSelfShadowCasterPassName = "ShadowCaster";

        [Header("Stencils")]
        public float _StencilEyeValue;

        [Header("Renderers")]
        [SerializeField] private Renderer[] renderers;
        public CharacterSkinningMode SkinningMode = CharacterSkinningMode.BuiltIn;
        [Tooltip("Compute shader used for custom skinning. Must match SkinningUVCoords.compute layout.")]
        public ComputeShader CustomSkinningCompute;

        [Header("Effects")]
        public List<EffectMaterialEntry> EffectMaterials = new List<EffectMaterialEntry>();
        private bool m_RendererScopeDirty = true;
        private bool m_HasSyncedState;
        private int m_LastSyncedStateHash;
        private Vector3 m_LastPosition;
        private readonly Dictionary<Renderer, RendererConstantBufferState> m_RendererConstantBufferStates = new Dictionary<Renderer, RendererConstantBufferState>();
        private readonly List<Renderer> m_RendererCandidateScratch = new List<Renderer>(32);
        private readonly List<Renderer> m_RendererScopeScratch = new List<Renderer>(32);
        private readonly List<Renderer> m_StaleRendererScratch = new List<Renderer>(8);
        private readonly List<Renderer> m_RendererConstantBufferReleaseScratch = new List<Renderer>(8);
        private readonly HashSet<Renderer> m_ActiveRendererScratch = new HashSet<Renderer>();
        private readonly List<Transform> m_TransformScratch = new List<Transform>(64);
        private readonly List<MonoBehaviour> m_BehaviourScratch = new List<MonoBehaviour>(32);
        private ComputeBuffer m_RendererConstantBuffer;
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
        private bool m_HasEligibleComputeSkinningRenderers;
        private bool m_HasComputeInputsHash;
        private int m_LastComputeInputsHash;
        private bool m_HasComputeInputSnapshot;
        private int m_LastComputeInputSnapshotHash;
        private int m_LastComputeInputRendererScopeVersion = -1;
        private bool m_HasComputeDispatchSegments;
        private bool m_ComputeSkinningHasDispatchedPose;
        private bool m_HasPlanarReflectionParticipantForComputeSkinning;
        private readonly List<Vector3> m_ComputeVertexScratch = new List<Vector3>();
        private readonly List<Vector3> m_ComputeNormalScratch = new List<Vector3>();
        private readonly List<Vector4> m_ComputeTangentScratch = new List<Vector4>();
        private readonly List<Vector4> m_ComputeUv7Scratch = new List<Vector4>();
        private readonly List<Vector4> m_ComputeUv8Scratch = new List<Vector4>();
        private int m_RendererScopeVersion;
        private int m_EffectMaterialsVersion;
        private int m_LastEffectMaterialsHash;

        public int RendererScopeVersion => m_RendererScopeVersion;
        public int EffectMaterialsVersion
        {
            get
            {
                InvalidateEffectMaterialsIfChanged();
                return m_EffectMaterialsVersion;
            }
        }

        public bool TryGetComputeSkinningBlendShapeWarning(out string warningMessage)
        {
            warningMessage = null;
            if (!IsComputeSkinningRequested())
                return false;

            Renderer[] scopedRenderers = GetScopedRenderers();
            if (scopedRenderers == null || scopedRenderers.Length == 0)
                return false;

            List<string> blendShapeRendererNames = null;
            for (int i = 0; i < scopedRenderers.Length; ++i)
            {
                SkinnedMeshRenderer skinnedRenderer = scopedRenderers[i] as SkinnedMeshRenderer;
                Mesh mesh = skinnedRenderer != null ? skinnedRenderer.sharedMesh : null;
                if (mesh == null || mesh.blendShapeCount <= 0)
                    continue;

                if (blendShapeRendererNames == null)
                    blendShapeRendererNames = new List<string>();

                blendShapeRendererNames.Add(skinnedRenderer.name);
            }

            if (blendShapeRendererNames == null || blendShapeRendererNames.Count == 0)
                return false;

            warningMessage = $"Compute skinning skips blendshape renderers and leaves them on built-in skinning to avoid per-frame CPU mesh baking overhead: {string.Join(", ", blendShapeRendererNames)}.";
            return true;
        }

        private void RefreshScopedRenderers()
        {
            ApplyRuntimeLayer();

            m_RendererScopeScratch.Clear();
            m_RendererCandidateScratch.Clear();
            GetComponentsInChildren<Renderer>(true, m_RendererCandidateScratch);
            for (int i = 0; i < m_RendererCandidateScratch.Count; ++i)
            {
                var renderer = m_RendererCandidateScratch[i];
                if (renderer == null)
                    continue;

                if (renderer.GetComponentInParent<HSRCharacterController>() != this)
                    continue;

                m_RendererScopeScratch.Add(renderer);
            }

            bool rendererScopeChanged = renderers == null || !AreRendererArrayAndListEqual(renderers, m_RendererScopeScratch);
            if (rendererScopeChanged)
                renderers = m_RendererScopeScratch.Count > 0 ? m_RendererScopeScratch.ToArray() : Array.Empty<Renderer>();

            m_RendererCandidateScratch.Clear();
            m_RendererScopeScratch.Clear();
            m_HasPlanarReflectionParticipantForComputeSkinning = HasPlanarReflectionParticipantInHierarchy();
            PruneRendererConstantBuffers();
            SyncCharacterLightCullingMasks();
            ApplyRuntimeTags();
            ApplyCharacterSelfShadowCastingMode();
            m_RendererScopeDirty = false;
            m_HasSyncedState = false;

            if (rendererScopeChanged)
            {
                m_RendererScopeVersion++;
                MarkComputeSkinningDirty();
                s_TopologyDirty = true;
                NotifyRendererTopologyChanged();
            }
        }

        private bool HasPlanarReflectionParticipantInHierarchy()
        {
            m_BehaviourScratch.Clear();
            GetComponentsInChildren<MonoBehaviour>(true, m_BehaviourScratch);
            for (int i = 0; i < m_BehaviourScratch.Count; ++i)
            {
                MonoBehaviour behaviour = m_BehaviourScratch[i];
                if (behaviour == null)
                    continue;

                Type behaviourType = behaviour.GetType();
                if (behaviourType != null && behaviourType.Name == PlanarReflectionParticipantTypeName)
                {
                    m_BehaviourScratch.Clear();
                    return true;
                }
            }

            m_BehaviourScratch.Clear();
            return false;
        }

        private void ApplyRuntimeLayer()
        {
            int layer = LayerMask.NameToLayer(CharacterLayerName);
            if (layer < 0)
                return;

            m_TransformScratch.Clear();
            GetComponentsInChildren<Transform>(true, m_TransformScratch);
            for (int i = 0; i < m_TransformScratch.Count; ++i)
            {
                Transform child = m_TransformScratch[i];
                if (child == null || child.gameObject.layer == layer)
                    continue;

                child.gameObject.layer = layer;
            }

            m_TransformScratch.Clear();
        }

        private void PruneRendererConstantBuffers()
        {
            if (m_RendererConstantBufferStates.Count == 0)
                return;

            m_ActiveRendererScratch.Clear();
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; ++i)
                {
                    if (renderers[i] != null)
                        m_ActiveRendererScratch.Add(renderers[i]);
                }
            }

            m_StaleRendererScratch.Clear();
            foreach (var kvp in m_RendererConstantBufferStates)
            {
                if (!m_ActiveRendererScratch.Contains(kvp.Key))
                    m_StaleRendererScratch.Add(kvp.Key);
            }

            for (int i = 0; i < m_StaleRendererScratch.Count; ++i)
                ReleaseRendererConstantBuffer(m_StaleRendererScratch[i]);

            m_ActiveRendererScratch.Clear();
            m_StaleRendererScratch.Clear();
        }

        private void ApplyRuntimeTags()
        {
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

                UnityTagUtility.TryAssignTag(renderer.gameObject, HairTag);
            }
        }

        private void ApplyCharacterSelfShadowCastingMode()
        {
            var rendererArray = renderers;
            if (rendererArray == null)
                return;

            ShadowCastingMode targetMode = EnableCharacterSelfShadow
                ? ShadowCastingMode.Off
                : ShadowCastingMode.On;

            for (int i = 0; i < rendererArray.Length; ++i)
            {
                Renderer renderer = rendererArray[i];
                if (renderer == null || renderer.shadowCastingMode == targetMode)
                    continue;

                renderer.shadowCastingMode = targetMode;
                RuntimeEditorBridge.MarkDirty(renderer);
            }
        }

        private static bool IsNamedSceneLight(Light light)
        {
            return HsrSceneLightQueryUtility.IsNamedLight(light, SceneLightName);
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

        private Light ResolveSceneLightReference()
        {
            UnityScene scene = gameObject.scene;
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return null;

            int sceneHandle = RenderSceneUtility.GetSceneHandleOrDefault(scene);
            if (sceneHandle != 0 && s_SceneLightBySceneHandle.TryGetValue(sceneHandle, out var cachedLight))
            {
                if (IsCachedSceneLightValid(cachedLight, sceneHandle))
                {
                    return cachedLight;
                }

                s_SceneLightBySceneHandle.Remove(sceneHandle);
            }

            Light registered = HsrSceneLightQueryUtility.FindRegisteredNamedLightInScene(scene, SceneLightName);
            if (registered != null)
            {
                if (sceneHandle != 0)
                    s_SceneLightBySceneHandle[sceneHandle] = registered;

                return registered;
            }

            if (Application.isPlaying)
                return null;

            Light resolved = HsrSceneLightQueryUtility.FindNamedLightInScene(scene, SceneLightName);

            if (sceneHandle != 0)
            {
                if (resolved != null)
                {
                    s_SceneLightBySceneHandle[sceneHandle] = resolved;
                }
                else
                {
                    s_SceneLightBySceneHandle.Remove(sceneHandle);
                }
            }

            return resolved;
        }

        private static bool IsCachedSceneLightValid(Light light, int sceneHandle)
        {
            return light != null
                && light.gameObject != null
                && light.gameObject.scene.handle == sceneHandle
                && IsNamedSceneLight(light);
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
            {
                s_TopologyDirty = true;
                NotifyActiveControllerRegistryChanged();
                NotifyRendererTopologyChanged();
            }
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
                NotifyActiveControllerRegistryChanged();
                NotifyRendererTopologyChanged();
            }
        }

        private static void Unregister(HSRCharacterController controller)
        {
            if (controller == null)
                return;

            if (s_ActiveControllers.Remove(controller))
            {
                s_TopologyDirty = true;
                NotifyActiveControllerRegistryChanged();
                NotifyRendererTopologyChanged();
            }
        }

        private static void NotifyActiveControllerRegistryChanged()
        {
            ActiveControllerRegistryChanged?.Invoke();
        }

        private static void NotifyRendererTopologyChanged()
        {
            unchecked
            {
                s_RendererTopologyVersion++;
            }

            RendererTopologyChanged?.Invoke();
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

        private static void EnsureSceneRegistryWithSlowPath(UnityScene scene, bool force)
        {
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return;

            PruneNullControllers();

            if (!force && HasActiveControllerInScene(scene))
                return;

            int sceneHandle = scene.handle;
            if (!force
                && s_NextSlowRegistryDiscoveryTimeBySceneHandle.TryGetValue(sceneHandle, out float nextDiscoveryTime)
                && Time.realtimeSinceStartup < nextDiscoveryTime)
            {
                return;
            }

            s_SceneRootScratch.Clear();
            scene.GetRootGameObjects(s_SceneRootScratch);
            for (int rootIndex = 0; rootIndex < s_SceneRootScratch.Count; ++rootIndex)
            {
                GameObject root = s_SceneRootScratch[rootIndex];
                if (root == null)
                    continue;

                s_ControllerDiscoveryScratch.Clear();
                root.GetComponentsInChildren(true, s_ControllerDiscoveryScratch);
                for (int i = 0; i < s_ControllerDiscoveryScratch.Count; ++i)
                    Register(s_ControllerDiscoveryScratch[i]);
            }

            s_ControllerDiscoveryScratch.Clear();
            s_SceneRootScratch.Clear();
            s_NextSlowRegistryDiscoveryTimeBySceneHandle[sceneHandle] = Time.realtimeSinceStartup + RegistrySlowDiscoveryInterval;
        }

        private static bool HasActiveControllerInScene(UnityScene scene)
        {
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return false;

            for (int i = 0; i < s_ActiveControllers.Count; ++i)
            {
                HSRCharacterController controller = s_ActiveControllers[i];
                if (controller != null
                    && controller.isActiveAndEnabled
                    && controller.gameObject.scene == scene)
                {
                    return true;
                }
            }

            return false;
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

        public static int GetActiveControllersInScene(UnityScene scene, List<HSRCharacterController> results, bool forceRefresh = false)
        {
            if (results == null)
                return 0;

            results.Clear();
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return 0;

            EnsureSceneRegistryWithSlowPath(scene, forceRefresh);
            PruneNullControllers();

            for (int i = 0; i < s_ActiveControllers.Count; ++i)
            {
                var controller = s_ActiveControllers[i];
                if (controller != null
                    && controller.isActiveAndEnabled
                    && controller.gameObject.scene == scene)
                {
                    results.Add(controller);
                }
            }

            return results.Count;
        }

        public static int GetRegisteredActiveControllersInScene(UnityScene scene, List<HSRCharacterController> results)
        {
            if (results == null)
                return 0;

            results.Clear();
            if (!RenderSceneUtility.IsSceneUsable(scene))
                return 0;

            PruneNullControllers();
            for (int i = 0; i < s_ActiveControllers.Count; ++i)
            {
                var controller = s_ActiveControllers[i];
                if (controller != null
                    && controller.isActiveAndEnabled
                    && controller.gameObject.scene == scene)
                {
                    results.Add(controller);
                }
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
            EnsureGlobalFallbackSkinnedVerticesBufferBound();
            RefreshLightReferences();
            RefreshScopedRenderers();
            MarkComputeSkinningDirty();
            Register(this);
            NotifyCharacterLightRegistration(CharacterLight, register: true);
            DetermineStencilEyeValueFromName();
            SyncToRenderer();
        }

        private void OnTransformChildrenChanged()
        {
            RefreshLightReferences();
            m_RendererScopeDirty = true;
            RefreshScopedRenderers();
            MarkComputeSkinningDirty();
            SyncToRenderer();
        }

        private void OnDisable()
        {
            TeardownComputeSkinning();
            ReleaseAllRendererConstantBuffers();
            Unregister(this);
            NotifyCharacterLightRegistration(CharacterLight, register: false);
        }

        private void OnDestroy()
        {
            TeardownComputeSkinning();
            ReleaseAllRendererConstantBuffers();
            Unregister(this);
            NotifyCharacterLightRegistration(CharacterLight, register: false);
        }

        private static void NotifyCharacterLightRegistration(Light light, bool register)
        {
            if (light == null)
                return;

            if (register)
                HsrCharacterLightRegistry.Register(light);
            else
                HsrCharacterLightRegistry.Unregister(light);
        }

        private void OnValidate()
        {
            TryAssignDefaultComputeSkinningShader();
            EnsureGlobalFallbackSkinnedVerticesBufferBound();
            SanitizeEffectMaterialEntries();
            InvalidateEffectMaterialsIfChanged();
            SceneLight = ResolveSceneLightReference();
            SyncCharacterLightCullingMasks();
            m_HasSyncedState = false;
            MarkComputeSkinningDirty();
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

        private void InvalidateEffectMaterialsIfChanged()
        {
            int currentHash = ComputeEffectMaterialsHash();
            if (currentHash == m_LastEffectMaterialsHash)
                return;

            m_LastEffectMaterialsHash = currentHash;
            m_EffectMaterialsVersion++;
            NotifyRendererTopologyChanged();
        }

        private void Reset()
        {
            TryAssignDefaultComputeSkinningShader();
            RefreshLightReferences();

            CharacterLight = EnsureOwnedCharacterLight();
            CharacterShadowLight = EnsureOwnedCharacterShadowLight();
            SyncCharacterLightCullingMasks();

            if (HeadBone == null)
                HeadBone = TransformSearchUtility.FindChildRecursive(transform, s_HeadBoneCandidateNames);
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

            ApplyCharacterSelfShadowCastingMode();

            ComputeBuffer skinnedVerticesBuffer = GetSkinnedVerticesBufferForBinding();
            if (skinnedVerticesBuffer == null)
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
            ComputeBuffer rendererConstantBuffer = GetOrCreateRendererConstantBuffer();
            if (rendererConstantBuffer == null)
                return;

            rendererConstantBuffer.SetData(m_CrpPerDrawExUpload);

            foreach (var ren in renderers)
            {
                if (ren == null)
                    continue;

                RendererConstantBufferState state = GetOrCreateRendererConstantBufferState(ren);

                ren.GetPropertyBlock(state.propertyBlock);
                state.propertyBlock.SetConstantBuffer(s_CrpPerDrawExId, rendererConstantBuffer, 0, s_CrpPerDrawExSize);
                state.propertyBlock.SetFloat(s_StencilEyeId, _StencilEyeValue);
                state.propertyBlock.SetVector(s_CharacterSelfShadowAtlasRectId, m_CharacterSelfShadowAtlasRect);
                state.propertyBlock.SetFloat(s_CharacterSelfShadowSliceIndexId, m_CharacterSelfShadowSliceIndex);
                state.propertyBlock.SetFloat(s_CharacterSelfShadowValidId, m_CharacterSelfShadowValid);

                int computeVertexOffset = 0;
                bool hasComputeSkinning = m_ComputeSkinningInitialized && m_ComputeVertexOffsets.TryGetValue(ren, out computeVertexOffset);
                state.propertyBlock.SetInteger(s_HsrComputeSkinningEnabledId, hasComputeSkinning ? 1 : 0);
                state.propertyBlock.SetInteger(s_HsrComputeSkinningVertexOffsetId, hasComputeSkinning ? computeVertexOffset : 0);
                state.propertyBlock.SetBuffer(s_HsrComputeSkinnedVerticesId, skinnedVerticesBuffer);

                ren.SetPropertyBlock(state.propertyBlock);
            }
        }

        private ComputeBuffer GetOrCreateRendererConstantBuffer()
        {
            if (m_RendererConstantBuffer == null)
                m_RendererConstantBuffer = new ComputeBuffer(1, s_CrpPerDrawExSize, ComputeBufferType.Constant);

            return m_RendererConstantBuffer;
        }

        private ComputeBuffer GetSkinnedVerticesBufferForBinding()
        {
            if (m_ComputeOutputBuffer != null)
                return m_ComputeOutputBuffer;

            try
            {
                return EnsureGlobalFallbackSkinnedVerticesBufferBound();
            }
            catch (Exception ex)
            {
                LogComputeSkinningErrorOnce($"HSRCharacterController: Failed to ensure fallback skinned-vertices buffer for shader binding. {ex.Message}");
                return null;
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
            m_RendererConstantBufferStates.Remove(renderer);
        }

        private void ReleaseAllRendererConstantBuffers()
        {
            m_RendererConstantBufferReleaseScratch.Clear();
            foreach (Renderer renderer in m_RendererConstantBufferStates.Keys)
                m_RendererConstantBufferReleaseScratch.Add(renderer);

            for (int i = 0; i < m_RendererConstantBufferReleaseScratch.Count; ++i)
                ReleaseRendererConstantBuffer(m_RendererConstantBufferReleaseScratch[i]);

            m_RendererConstantBufferReleaseScratch.Clear();

            ReleaseComputeBuffer(ref m_RendererConstantBuffer);
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
                hash = hash * 31 + EnableCharacterSelfShadow.GetHashCode();

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

        private int ComputeEffectMaterialsHash()
        {
            unchecked
            {
                int hash = 17;
                int count = EffectMaterials != null ? EffectMaterials.Count : 0;
                hash = hash * 31 + count;
                for (int i = 0; i < count; ++i)
                {
                    EffectMaterialEntry entry = EffectMaterials[i];
                    hash = hash * 31 + (int)entry.Type;
                    hash = hash * 31 + (entry.Material != null ? entry.Material.GetInstanceID() : 0);
                }

                return hash;
            }
        }

        private static bool AreRendererArrayAndListEqual(Renderer[] left, List<Renderer> right)
        {
            int leftLength = left != null ? left.Length : 0;
            int rightLength = right != null ? right.Count : 0;
            if (leftLength != rightLength)
                return false;

            for (int i = 0; i < leftLength; ++i)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private void DetermineStencilEyeValueFromName()
        {
            string keyString = gameObject.name ?? string.Empty;
            if (string.IsNullOrEmpty(keyString))
                keyString = "Unnamed";

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
            InvalidateComputeSkinningIfInputsChanged();
            UpdateComputeSkinning();
            TrySyncToRendererIfDirty(force: false);
        }

        private void InvalidateComputeSkinningIfInputsChanged()
        {
            if (!IsComputeSkinningRequested())
            {
                InvalidateComputeSkinningInputCache();
                return;
            }

            if (m_ComputeSkinningDirty || !m_ComputeSkinningInitialized)
                return;

            if (m_RendererScopeDirty || renderers == null)
            {
                MarkComputeSkinningDirty();
                m_HasSyncedState = false;
                return;
            }

            int currentSnapshotHash = ComputeSkinningInputSnapshotHash();
            if (m_HasComputeInputSnapshot
                && m_LastComputeInputRendererScopeVersion == m_RendererScopeVersion
                && currentSnapshotHash == m_LastComputeInputSnapshotHash)
            {
                return;
            }

            int currentDeepHash = ComputeSkinningInputsHash();
            m_LastComputeInputSnapshotHash = currentSnapshotHash;
            m_LastComputeInputRendererScopeVersion = m_RendererScopeVersion;
            m_HasComputeInputSnapshot = true;

            if (!m_HasComputeInputsHash)
            {
                m_LastComputeInputsHash = currentDeepHash;
                m_HasComputeInputsHash = true;
                return;
            }

            if (currentDeepHash == m_LastComputeInputsHash)
                return;

            m_LastComputeInputsHash = currentDeepHash;
            MarkComputeSkinningDirty();
            m_HasSyncedState = false;
        }

        private void MarkComputeSkinningDirty()
        {
            m_ComputeSkinningDirty = true;
            InvalidateComputeSkinningInputCache();
        }

        private void InvalidateComputeSkinningInputCache()
        {
            m_HasComputeInputsHash = false;
            m_HasComputeInputSnapshot = false;
            m_LastComputeInputRendererScopeVersion = -1;
        }

        private void UpdateComputeSkinningInputCache()
        {
            m_LastComputeInputSnapshotHash = ComputeSkinningInputSnapshotHash();
            m_LastComputeInputRendererScopeVersion = m_RendererScopeVersion;
            m_HasComputeInputSnapshot = true;
            m_LastComputeInputsHash = ComputeSkinningInputsHash();
            m_HasComputeInputsHash = true;
        }

        private int ComputeSkinningInputSnapshotHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)SkinningMode;
                hash = hash * 31 + (CustomSkinningCompute != null ? CustomSkinningCompute.GetInstanceID() : 0);
                hash = hash * 31 + m_RendererScopeVersion;

                int rendererCount = renderers != null ? renderers.Length : 0;
                hash = hash * 31 + rendererCount;
                for (int i = 0; i < rendererCount; ++i)
                {
                    Renderer renderer = renderers[i];
                    hash = hash * 31 + (renderer != null ? renderer.GetInstanceID() : 0);

                    SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
                    if (skinnedRenderer == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    Mesh mesh = skinnedRenderer.sharedMesh;
                    hash = hash * 31 + (mesh != null ? mesh.GetInstanceID() : 0);
                    hash = hash * 31 + (mesh != null ? mesh.vertexCount : 0);
                    hash = hash * 31 + (mesh != null ? mesh.blendShapeCount : 0);
                    hash = hash * 31 + (mesh != null ? mesh.bindposeCount : 0);
                    hash = hash * 31 + (mesh != null && mesh.isReadable ? 1 : 0);

                    Transform rootBone = skinnedRenderer.rootBone;
                    hash = hash * 31 + (rootBone != null ? rootBone.GetInstanceID() : 0);
                }

                return hash;
            }
        }

        private int ComputeSkinningInputsHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + transform.lossyScale.GetHashCode();

                Renderer[] scopedRenderers = GetScopedRenderers();
                int rendererCount = scopedRenderers != null ? scopedRenderers.Length : 0;
                hash = hash * 31 + rendererCount;

                for (int i = 0; i < rendererCount; ++i)
                {
                    Renderer renderer = scopedRenderers[i];
                    if (renderer == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    hash = hash * 31 + renderer.GetInstanceID();
                    hash = hash * 31 + renderer.transform.lossyScale.GetHashCode();

                    SkinnedMeshRenderer skinnedRenderer = renderer as SkinnedMeshRenderer;
                    if (skinnedRenderer == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    Mesh mesh = skinnedRenderer.sharedMesh;
                    hash = hash * 31 + (mesh != null ? mesh.GetInstanceID() : 0);
                    hash = hash * 31 + (mesh != null ? mesh.vertexCount : 0);
                    hash = hash * 31 + (mesh != null ? mesh.blendShapeCount : 0);
                    hash = hash * 31 + (mesh != null ? mesh.bindposeCount : 0);

                    Transform rootBone = skinnedRenderer.rootBone;
                    hash = hash * 31 + (rootBone != null ? rootBone.GetInstanceID() : 0);

                    Transform[] bones = skinnedRenderer.bones;
                    int boneCount = bones != null ? bones.Length : 0;
                    hash = hash * 31 + boneCount;
                    for (int b = 0; b < boneCount; ++b)
                    {
                        Transform bone = bones[b];
                        hash = hash * 31 + (bone != null ? bone.GetInstanceID() : 0);
                    }
                }

                return hash;
            }
        }

        private bool IsComputeSkinningRequested()
        {
            if (SkinningMode != CharacterSkinningMode.Compute)
                return false;

            if (CustomSkinningCompute == null)
            {
                LogComputeSkinningErrorOnce("HSRCharacterController: Compute skinning requested but no compute shader is assigned. Assign SkinningUVCoords.compute during setup before building the player.");
                return false;
            }

            return true;
        }

        private void TryAssignDefaultComputeSkinningShader()
        {
            if (CustomSkinningCompute != null || Application.isPlaying)
                return;

            CustomSkinningCompute = HSRCharacterEditorHooks.ResolveDefaultComputeShader();
            if (CustomSkinningCompute != null)
            {
                RuntimeEditorBridge.MarkDirty(this);
                MarkComputeSkinningDirty();
            }
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

            if (!m_ComputeSkinningInitialized || !m_HasEligibleComputeSkinningRenderers)
                return;

            bool missingRequiredBuffers = m_ComputeOutputBuffer == null;
            if (!missingRequiredBuffers && m_HasComputeDispatchSegments)
            {
                missingRequiredBuffers =
                    m_ComputeBaseVerticesBuffer == null ||
                    m_ComputeBoneWeightsBuffer == null ||
                    m_ComputeBoneIndicesBuffer == null ||
                    m_ComputeBoneMatricesBuffer == null;
            }

            if (missingRequiredBuffers)
            {
                MarkComputeSkinningDirty();
                RebuildComputeSkinning();
                m_HasSyncedState = false;
                if (!m_ComputeSkinningInitialized)
                    return;
            }

            if (!ShouldDispatchComputeSkinningThisFrame())
                return;

            UploadComputeBoneMatrices();
            DispatchComputeSkinning();
            m_ComputeSkinningHasDispatchedPose = true;
            ClearComputeSkinningPoseChangeFlags();
        }

        private bool ShouldDispatchComputeSkinningThisFrame()
        {
            if (!m_ComputeSkinningInitialized || !m_HasComputeDispatchSegments)
                return false;

            if (!HasActiveComputeSkinningConsumer())
                return false;

            return !m_ComputeSkinningHasDispatchedPose || HasComputeSkinningPoseChanged();
        }

        private bool HasActiveComputeSkinningConsumer()
        {
            for (int i = 0; i < m_ComputeSkinSegments.Count; ++i)
            {
                ComputeSkinSegment segment = m_ComputeSkinSegments[i];
                Renderer renderer = segment != null ? segment.renderer : null;
                if (!IsActiveRendererForComputeSkinning(renderer))
                    continue;

                if (renderer.isVisible || IsForcedOffscreenComputeSkinningConsumer(renderer))
                    return true;
            }

            return false;
        }

        private bool IsForcedOffscreenComputeSkinningConsumer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            if (EnableCharacterSelfShadow)
                return true;

            if (m_HasPlanarReflectionParticipantForComputeSkinning)
                return true;

            ShadowCastingMode shadowMode = renderer.shadowCastingMode;
            return shadowMode != ShadowCastingMode.Off;
        }

        private static bool IsActiveRendererForComputeSkinning(Renderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && !renderer.forceRenderingOff
                && renderer.gameObject != null
                && renderer.gameObject.activeInHierarchy;
        }

        private bool HasComputeSkinningPoseChanged()
        {
            if (transform.hasChanged)
                return true;

            for (int i = 0; i < m_ComputeSkinSegments.Count; ++i)
            {
                ComputeSkinSegment segment = m_ComputeSkinSegments[i];
                if (segment == null || segment.renderer == null)
                    continue;

                if (segment.renderer.transform.hasChanged)
                    return true;

                Transform rootBone = segment.rootBone != null ? segment.rootBone : segment.renderer.transform;
                if (rootBone != null && rootBone.hasChanged)
                    return true;

                Transform[] bones = segment.bones;
                int boneCount = bones != null ? bones.Length : 0;
                for (int b = 0; b < boneCount; ++b)
                {
                    Transform bone = bones[b];
                    if (bone != null && bone.hasChanged)
                        return true;
                }
            }

            return false;
        }

        private void ClearComputeSkinningPoseChangeFlags()
        {
            transform.hasChanged = false;

            for (int i = 0; i < m_ComputeSkinSegments.Count; ++i)
            {
                ComputeSkinSegment segment = m_ComputeSkinSegments[i];
                if (segment == null || segment.renderer == null)
                    continue;

                segment.renderer.transform.hasChanged = false;

                Transform rootBone = segment.rootBone != null ? segment.rootBone : segment.renderer.transform;
                if (rootBone != null)
                    rootBone.hasChanged = false;

                Transform[] bones = segment.bones;
                int boneCount = bones != null ? bones.Length : 0;
                for (int b = 0; b < boneCount; ++b)
                {
                    Transform bone = bones[b];
                    if (bone != null)
                        bone.hasChanged = false;
                }
            }
        }

        private void RebuildComputeSkinning()
        {
            TeardownComputeSkinning();

            if (renderers == null || m_RendererScopeDirty)
                RefreshScopedRenderers();

            m_ComputeSkinningDirty = false;
            UpdateComputeSkinningInputCache();

            m_ComputeSkinSegments.Clear();
            m_ComputeVertexOffsets.Clear();
            m_HasEligibleComputeSkinningRenderers = false;
            m_HasComputeDispatchSegments = false;
            m_ComputeSkinningHasDispatchedPose = false;

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
                    if (mesh.blendShapeCount > 0)
                        continue;

                    if (!mesh.isReadable)
                    {
                        LogComputeSkinningErrorOnce($"HSRCharacterController: Mesh '{mesh.name}' is not Read/Write enabled; skipping compute skinning for this renderer.");
                        continue;
                    }

                    int vertexCount = mesh.vertexCount;
                    if (vertexCount <= 0)
                        continue;

                    BoneWeight[] boneWeights = mesh.boneWeights;
                    Matrix4x4[] bindPoses = mesh.bindposes;
                    if (boneWeights == null || boneWeights.Length != vertexCount || bindPoses == null || bindPoses.Length == 0)
                        continue;

                    var segment = new ComputeSkinSegment
                    {
                        renderer = skinnedRenderer,
                        mesh = mesh,
                        rootBone = skinnedRenderer.rootBone,
                        bones = skinnedRenderer.bones,
                        bindPoses = bindPoses,
                        boneWeights = boneWeights,
                        vertexOffset = totalVertexCount,
                        vertexCount = vertexCount,
                        matrixOffset = totalMatrixCount,
                        matrixCount = bindPoses.Length
                    };

                    m_ComputeSkinSegments.Add(segment);
                    m_ComputeVertexOffsets[skinnedRenderer] = segment.vertexOffset;
                    totalVertexCount += segment.vertexCount;
                    totalMatrixCount += segment.matrixCount;
                    m_HasEligibleComputeSkinningRenderers = true;
                    m_HasComputeDispatchSegments = true;
                }
            }

            if (m_ComputeSkinSegments.Count == 0 || totalVertexCount == 0)
            {
                m_ComputeSkinningInitialized = true;
                m_HasLoggedComputeSkinningError = false;
                return;
            }

            if (m_HasComputeDispatchSegments)
            {
                try
                {
                    m_ComputeKernel = CustomSkinningCompute.FindKernel(SkinningKernelName);
                }
                catch (Exception)
                {
                    LogComputeSkinningErrorOnce($"HSRCharacterController: Kernel '{SkinningKernelName}' not found on compute shader '{CustomSkinningCompute.name}'.");
                    return;
                }
            }

            SkinData[] baseVertices = new SkinData[totalVertexCount];
            Vector4[] weights = new Vector4[totalVertexCount];
            UInt4[] indices = new UInt4[totalVertexCount];
            m_ComputeBoneMatricesUpload = m_HasComputeDispatchSegments ? new Matrix4x4[totalMatrixCount] : null;

            for (int s = 0; s < m_ComputeSkinSegments.Count; ++s)
            {
                ComputeSkinSegment segment = m_ComputeSkinSegments[s];
                Mesh mesh = segment.mesh;
                BoneWeight[] boneWeights = segment.boneWeights;
                m_ComputeVertexScratch.Clear();
                m_ComputeNormalScratch.Clear();
                m_ComputeTangentScratch.Clear();
                m_ComputeUv7Scratch.Clear();
                m_ComputeUv8Scratch.Clear();
                mesh.GetVertices(m_ComputeVertexScratch);
                mesh.GetNormals(m_ComputeNormalScratch);
                mesh.GetTangents(m_ComputeTangentScratch);
                mesh.GetUVs(6, m_ComputeUv7Scratch);
                mesh.GetUVs(7, m_ComputeUv8Scratch);

                if (m_ComputeVertexScratch.Count != segment.vertexCount)
                {
                    LogComputeSkinningErrorOnce($"HSRCharacterController: Mesh '{mesh.name}' vertex data changed while rebuilding compute skinning.");
                    TeardownComputeSkinning();
                    MarkComputeSkinningDirty();
                    return;
                }

                bool hasComputeNormals = m_ComputeNormalScratch.Count == segment.vertexCount;
                bool hasComputeTangents = m_ComputeTangentScratch.Count == segment.vertexCount;
                bool hasUv7 = m_ComputeUv7Scratch.Count == segment.vertexCount;
                bool hasUv8 = m_ComputeUv8Scratch.Count == segment.vertexCount;

                for (int i = 0; i < segment.vertexCount; ++i)
                {
                    int globalVertexIndex = segment.vertexOffset + i;
                    BoneWeight bw = boneWeights[i];

                    baseVertices[globalVertexIndex] = new SkinData
                    {
                        pos = m_ComputeVertexScratch[i],
                        pad0 = 0f,
                        norm = hasComputeNormals ? m_ComputeNormalScratch[i] : Vector3.up,
                        pad1 = 0f,
                        tangent = hasComputeTangents ? m_ComputeTangentScratch[i] : new Vector4(1f, 0f, 0f, 1f),
                        tangent1 = new Vector4(
                            hasUv7 ? m_ComputeUv7Scratch[i].x : 0f,
                            hasUv7 ? m_ComputeUv7Scratch[i].y : 0f,
                            hasUv8 ? m_ComputeUv8Scratch[i].x : 0f,
                            hasUv8 ? m_ComputeUv8Scratch[i].y : 1f)
                    };

                    weights[globalVertexIndex] = new Vector4(bw.weight0, bw.weight1, bw.weight2, bw.weight3);
                    indices[globalVertexIndex] = new UInt4((uint)bw.boneIndex0, (uint)bw.boneIndex1, (uint)bw.boneIndex2, (uint)bw.boneIndex3);
                }

                m_ComputeVertexScratch.Clear();
                m_ComputeNormalScratch.Clear();
                m_ComputeTangentScratch.Clear();
                m_ComputeUv7Scratch.Clear();
                m_ComputeUv8Scratch.Clear();
            }

            m_ComputeOutputBuffer = new ComputeBuffer(totalVertexCount, Marshal.SizeOf<SkinData>());

            if (m_HasComputeDispatchSegments)
            {
                m_ComputeBaseVerticesBuffer = new ComputeBuffer(totalVertexCount, Marshal.SizeOf<SkinData>());
                m_ComputeBoneWeightsBuffer = new ComputeBuffer(totalVertexCount, Marshal.SizeOf<Vector4>());
                m_ComputeBoneIndicesBuffer = new ComputeBuffer(totalVertexCount, Marshal.SizeOf<UInt4>());
                m_ComputeBoneMatricesBuffer = new ComputeBuffer(totalMatrixCount, Marshal.SizeOf<Matrix4x4>());

                m_ComputeBaseVerticesBuffer.SetData(baseVertices);
                m_ComputeBoneWeightsBuffer.SetData(weights);
                m_ComputeBoneIndicesBuffer.SetData(indices);

                CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BaseVertices", m_ComputeBaseVerticesBuffer);
                CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BoneWeights", m_ComputeBoneWeightsBuffer);
                CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BoneIndices", m_ComputeBoneIndicesBuffer);
                CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_BoneMatrices", m_ComputeBoneMatricesBuffer);
            }

            // Seed output so the buffer contains stable data before the first dispatch.
            m_ComputeOutputBuffer.SetData(baseVertices);

            if (m_HasComputeDispatchSegments)
                CustomSkinningCompute.SetBuffer(m_ComputeKernel, "_OutputBuffer", m_ComputeOutputBuffer);

            m_ComputeSkinningInitialized = true;
            m_HasLoggedComputeSkinningError = false;
        }

        private void UploadComputeBoneMatrices()
        {
            if (!m_ComputeSkinningInitialized || !m_HasComputeDispatchSegments || m_ComputeBoneMatricesUpload == null)
                return;

            for (int s = 0; s < m_ComputeSkinSegments.Count; ++s)
            {
                ComputeSkinSegment segment = m_ComputeSkinSegments[s];
                if (segment.renderer == null)
                    continue;

                Transform skinRoot = segment.rootBone != null ? segment.rootBone : segment.renderer.transform;
                Matrix4x4 rootWorldToLocal = skinRoot.worldToLocalMatrix;
                Matrix4x4 fallbackLocalToWorld = skinRoot.localToWorldMatrix;
                Transform[] bones = segment.bones;
                Matrix4x4[] bindPoses = segment.bindPoses;

                for (int i = 0; i < segment.matrixCount; ++i)
                {
                    Matrix4x4 boneLocalToWorld = fallbackLocalToWorld;
                    if (bones != null && i < bones.Length && bones[i] != null)
                        boneLocalToWorld = bones[i].localToWorldMatrix;

                    int matrixIndex = segment.matrixOffset + i;
                    m_ComputeBoneMatricesUpload[matrixIndex] = rootWorldToLocal * boneLocalToWorld * bindPoses[i];
                }
            }

            m_ComputeBoneMatricesBuffer.SetData(m_ComputeBoneMatricesUpload);
        }

        private void DispatchComputeSkinning()
        {
            if (!m_ComputeSkinningInitialized || !m_HasComputeDispatchSegments || CustomSkinningCompute == null)
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
            m_ComputeVertexScratch.Clear();
            m_ComputeNormalScratch.Clear();
            m_ComputeTangentScratch.Clear();
            m_ComputeUv7Scratch.Clear();
            m_ComputeUv8Scratch.Clear();
            m_HasEligibleComputeSkinningRenderers = false;
            InvalidateComputeSkinningInputCache();
            m_HasComputeDispatchSegments = false;
            m_ComputeSkinningHasDispatchedPose = false;

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

