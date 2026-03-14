using UnityEngine;
using System.Collections.Generic;
using HoyoToon.Runtime.Character;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HoyoToon.Runtime.Scene
{
[ExecuteAlways]
public class HSRSceneController : MonoBehaviour
{
    private const float CharacterLightSlowDiscoveryInterval = 2.0f;
    private const float MaterialKeywordSlowSyncInterval = 2.0f;
    private const string CharacterLightName = "CharacterLight";
    private const string SceneLightName = "HoyoToon Scene Light";
    private const string HsrShaderNameToken = "Honkai Star Rail";
    private const string HeightLerpKeyword = "_HEIGHTLERP";
    private const string FogKeyword = "_ENABLE_FOG";
    private static readonly List<Light> s_CharacterLightRegistry = new List<Light>();
    private static readonly List<HSRCharacterController> s_CharacterControllers = new List<HSRCharacterController>();
    private static readonly List<Material> s_RuntimeMaterials = new List<Material>();
    private static readonly HashSet<Material> s_UniqueCharacterMaterials = new HashSet<Material>();
    private static bool s_CharacterLightRegistryDirty = true;
    private static float s_NextSlowCharacterLightDiscoveryTime;
    private static float s_NextSlowMaterialKeywordSyncTime;

    [Header("Global Intensity")]
    public float _GlobalOneMinusAvatarIntensity = 0f;
    
    [Header("Main Light Settings")]
    public Light main_light;
    public Light[] CharacterLights;
    public Vector3 _ES_MonsterLightDir = new Vector3(0, 1, 0);
    public bool _ES_Indoor;
    public float _ES_TransitionRate = 1.0f;
    public bool _ES_LEVEL_ADJUST_ON = false;
    public float _ES_SelfShadowLerpHair;

    [Header("Main Light Shadows")]
    public bool _EnableMainLightShadows = true;
    [Range(256, 8192)] public int _MainLightShadowResolution = 2048;
    [Range(1, 4)] public int _MainLightShadowCascades = 4;
    [Range(0.01f, 0.99f)] public float _MainLightShadowCascade2Split = 0.25f;
    public Vector3 _MainLightShadowCascade4Split = new Vector3(0.067f, 0.2f, 0.467f);
    [Range(0.0f, 1.0f)] public float _MainLightShadowCascadeBorder = 0.1f;
    [Range(0.0f, 10.0f)] public float _MainLightShadowNearPlaneOffset = 0.1f;

    [Header("Shadow Debug")]
    public bool _DebugMainLightShadowFullscreen = false;
    [Range(0, 3)] public int _DebugMainLightShadowSlice = 0;
    
    [Header("Global Rotation")]
    [Header("Environment Rotation")]
    public Vector3 _es_global_rotation_euler = Vector3.zero;
    public Matrix4x4 _ES_GlobalRotMatrix { get; private set; }
    private Vector3 _cachedGlobalRotationEuler;
    private bool _isGlobalRotMatrixDirty = true;
    private bool _hasAppliedSceneMaterialKeywords;
    private int _lastSceneMaterialKeywordStateHash;


    [Header("Character Lighting & Outlines")]
    public float _ES_CharacterToonRampMode = 0f;
    public bool _ES_CharacterDisableLocalMainLight = false;
    public Color _ES_AddColor = Color.clear;
    public Color _ES_SPColor = Color.white;
    public float _ES_SPIntensity = 1.0f;
    public float _ES_OutLineDarkenVal = 0.5f;
    public float _ES_OutLineLightedVal = 1.0f;
    public float _ES_OutlineDisableDistanceScale = 0f;
    public float _ES_OutlineFallbackScale = 1.0f;   
    public float _OutlineScale = 0.0149f;

    [Header("Rim Settings")]
    public Color _ES_RimShadowColor = Color.black;
    public float _ES_RimShadowIntensity = 1.0f;
    public float _ES_CharacterShadowFactor = 1.0f;
    public Vector2 _ES_RimLightOffset = Vector2.zero;
    public float _ES_RimLightMode = 0.0f;
    public float _ES_RimLightWidth = 1.0f;
    public float _ES_RimLightIntensity = 1.0f;
    public float _ES_RimLightAddMode = 0f;
    public Color _ES_RimLightColor = Color.white;

    [Header("Height Lerp Colors")]
    public bool HeightLerpEnable = false; 
    public float _ES_HeightLerpTop = 0.2f;
    public float _ES_HeightLerpBottom = 0.4f;
    public Color _ES_HeightLerpTopColor = Color.white;
    public Color _ES_HeightLerpMiddleColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    public Color _ES_HeightLerpBottomColor = new Color(0.3137254715f, 0.3137254715f, 0.4901961088f, 0.5000f);

    [Header("Level Lighting")]
    public Color _ES_LevelSkinLightColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    public Color _ES_LevelSkinShadowColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    public Color _ES_LevelHighLightColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    public Color _ES_LevelShadowColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    [Range(0, 1)] public float _ES_LevelShadow = 0.0f;
    [Range(0, 1)] public float _ES_LevelMid = 0.55f;
    [Range(0, 1)] public float _ES_LevelHighLight = 1.0f;
    [Range(0, 1)] public float _ES_LevelEyeShadowIntensity = 1.0f;
    public bool _ES_IndoorCharShadowAsCookie = false;

    [Header("Fog Settings")]
    public float _ES_FogColor = 0.5625f;
    public float _ES_FogDensity = 1.0f;
    public float _ES_FogNear = 300f;
    public float _ES_FogFar = 800f;
    public float _ES_HeightFogColor = 0.8125f;
    public float _ES_HeightFogBaseHeight = 6f;
    public float _ES_HeightFogRange = 40f;
    public float _ES_HeightFogDensity = 1f;
    public float _ES_HeightFogFogNear = 10f;
    public float _ES_HeightFogFogFar = 130f;
    public float _ES_FogCharacterNearFactor =  0.1f;
    public float _ES_HeightFogAddAjust = 0f;
    public bool _ES_DisableFogTransition = false;
    
    [Header("Effects")]
    public Vector4 _ES_EffCustomLightPosition = Vector4.zero;

    [Header("Draw Extensions (Local Lights)")]
    public Color _CharacterLocalMainLightColor1 = Color.white;
    public Color _CharacterLocalMainLightColor2 = Color.white;
    public Color _CharacterLocalMainLightDark = Color.black;
    public float _DisableCharacterLocalLight = 0f;

    private static HSRSceneController _instance;
    public static HSRSceneController instance {
        get {
            if (_instance == null) _instance = FindFirstObjectByType<HSRSceneController>();
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistryOnSubsystemRegistration()
    {
        s_CharacterLightRegistry.Clear();
        s_CharacterControllers.Clear();
        s_RuntimeMaterials.Clear();
        s_UniqueCharacterMaterials.Clear();
        s_CharacterLightRegistryDirty = true;
        s_NextSlowCharacterLightDiscoveryTime = 0f;
        s_NextSlowMaterialKeywordSyncTime = 0f;
        _instance = null;
    }

    public static void RegisterCharacterLight(Light characterLight)
    {
        if (characterLight == null)
            return;

        PruneNullCharacterLights();
        if (!s_CharacterLightRegistry.Contains(characterLight))
        {
            s_CharacterLightRegistry.Add(characterLight);
            s_CharacterLightRegistryDirty = true;
        }
    }

    public static void UnregisterCharacterLight(Light characterLight)
    {
        if (characterLight == null)
            return;

        if (s_CharacterLightRegistry.Remove(characterLight))
            s_CharacterLightRegistryDirty = true;
    }

    private static void PruneNullCharacterLights()
    {
        for (int i = s_CharacterLightRegistry.Count - 1; i >= 0; --i)
        {
            if (s_CharacterLightRegistry[i] == null)
            {
                s_CharacterLightRegistry.RemoveAt(i);
                s_CharacterLightRegistryDirty = true;
            }
        }
    }

    private static bool IsCharacterLight(Light light)
    {
        return light != null && light.gameObject != null && light.gameObject.name == CharacterLightName;
    }

    private static bool IsSceneMainLight(Light light)
    {
        return light != null && light.gameObject != null && light.gameObject.name == SceneLightName;
    }

    private static Light FindSceneMainLight()
    {
        var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light resolved = null;
        int resolvedId = int.MaxValue;
        for (int i = 0; i < allLights.Length; ++i)
        {
            var light = allLights[i];
            if (!IsSceneMainLight(light))
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

    private void ResolveMainLightReference()
    {
        main_light = FindSceneMainLight();
    }

    private void RebuildCharacterLightsFromScene()
    {
        var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        var discoveredCharacterLights = new List<Light>();
        Light resolvedSceneLight = null;
        int resolvedSceneLightId = int.MaxValue;

        for (int i = 0; i < allLights.Length; ++i)
        {
            var light = allLights[i];
            if (light == null || light.gameObject == null)
                continue;

            if (IsCharacterLight(light))
                discoveredCharacterLights.Add(light);

            if (!IsSceneMainLight(light))
                continue;

            int instanceId = light.GetInstanceID();
            if (instanceId < resolvedSceneLightId)
            {
                resolvedSceneLight = light;
                resolvedSceneLightId = instanceId;
            }
        }

        s_CharacterLightRegistry.Clear();
        s_CharacterLightRegistry.AddRange(discoveredCharacterLights);
        CharacterLights = discoveredCharacterLights.ToArray();
        main_light = resolvedSceneLight;
        s_CharacterLightRegistryDirty = false;
    }

    private void RefreshCharacterLightsIfNeeded(bool forceSlowPath)
    {
        if (s_CharacterLightRegistryDirty || forceSlowPath || CharacterLights == null)
            RebuildCharacterLightsFromScene();

        if (!IsSceneMainLight(main_light))
            ResolveMainLightReference();

        if (!forceSlowPath && Time.realtimeSinceStartup < s_NextSlowCharacterLightDiscoveryTime)
            return;

        RebuildCharacterLightsFromScene();

        s_NextSlowCharacterLightDiscoveryTime = Time.realtimeSinceStartup + CharacterLightSlowDiscoveryInterval;
    }

    private void OnEnable()
    { 
        if (_instance == null || _instance == this)
        {
            _instance = this;
        }
        else
        {
            Debug.LogWarning("[HoyoToon] Multiple HSRSceneController instances detected. Keeping the first active instance.", this);
        }

        RefreshCharacterLightsIfNeeded(forceSlowPath: true);
        InvalidateGlobalRotMatrixCache();
        InvalidateSceneMaterialKeywordSync();
        SyncSceneMaterialKeywordsIfNeeded(forceSlowPath: true);
    }

    private void OnDisable()
    {
        if (_instance == this)
        {
            ApplySceneMaterialKeywords(clearOnly: true);
            _hasAppliedSceneMaterialKeywords = false;
            var replacement = FindFirstObjectByType<HSRSceneController>();
            _instance = replacement != this ? replacement : null;
            if (_instance != null)
            {
                _instance.InvalidateSceneMaterialKeywordSync();
                _instance.SyncSceneMaterialKeywordsIfNeeded(forceSlowPath: true);
            }
            return;
        }

        if (_instance == this)
            _instance = FindFirstObjectByType<HSRSceneController>();
    }

    private void OnValidate()
    {
        RefreshCharacterLightsIfNeeded(forceSlowPath: true);
        InvalidateGlobalRotMatrixCache();
        InvalidateSceneMaterialKeywordSync();
        SyncSceneMaterialKeywordsIfNeeded(forceSlowPath: true);
    }

    private void InvalidateGlobalRotMatrixCache()
    {
        _isGlobalRotMatrixDirty = true;
    }

    private void InvalidateSceneMaterialKeywordSync()
    {
        _hasAppliedSceneMaterialKeywords = false;
        s_NextSlowMaterialKeywordSyncTime = 0f;
    }

    private void SyncSceneMaterialKeywordsIfNeeded(bool forceSlowPath)
    {
        int stateHash = ComputeSceneMaterialKeywordStateHash();
        bool slowSyncDue = Time.realtimeSinceStartup >= s_NextSlowMaterialKeywordSyncTime;
        if (!forceSlowPath && !slowSyncDue && _hasAppliedSceneMaterialKeywords && stateHash == _lastSceneMaterialKeywordStateHash)
            return;

        ApplySceneMaterialKeywords(clearOnly: false);
        _lastSceneMaterialKeywordStateHash = stateHash;
        _hasAppliedSceneMaterialKeywords = true;
        s_NextSlowMaterialKeywordSyncTime = Time.realtimeSinceStartup + MaterialKeywordSlowSyncInterval;
    }

    private int ComputeSceneMaterialKeywordStateHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + HeightLerpEnable.GetHashCode();
            hash = hash * 31 + IsFogEnabled().GetHashCode();
            return hash;
        }
    }

    private bool IsFogEnabled()
    {
        bool standardFogEnabled = _ES_FogDensity > 0f && _ES_FogFar > _ES_FogNear;
        bool heightFogEnabled = _ES_HeightFogDensity > 0f && _ES_HeightFogFogFar > _ES_HeightFogFogNear;
        return standardFogEnabled || heightFogEnabled;
    }

    private void ApplySceneMaterialKeywords(bool clearOnly)
    {
        s_UniqueCharacterMaterials.Clear();

        HSRCharacterController.GetActiveControllers(s_CharacterControllers, forceRefresh: false);
        for (int i = 0; i < s_CharacterControllers.Count; ++i)
        {
            var controller = s_CharacterControllers[i];
            if (controller == null || controller.renderers == null)
                continue;

            for (int j = 0; j < controller.renderers.Length; ++j)
            {
                CollectRendererMaterials(controller.renderers[j], s_UniqueCharacterMaterials);
            }
        }

        foreach (var material in s_UniqueCharacterMaterials)
        {
            if (!UsesSceneMaterialKeywords(material))
                continue;

            ApplySceneMaterialKeywords(material, clearOnly);
        }

        s_UniqueCharacterMaterials.Clear();
        s_CharacterControllers.Clear();
    }

    private static void CollectRendererMaterials(Renderer renderer, HashSet<Material> uniqueMaterials)
    {
        if (renderer == null || uniqueMaterials == null)
            return;

        if (Application.isPlaying)
        {
            renderer.GetMaterials(s_RuntimeMaterials);
            for (int i = 0; i < s_RuntimeMaterials.Count; ++i)
            {
                var material = s_RuntimeMaterials[i];
                if (material != null)
                    uniqueMaterials.Add(material);
            }
            s_RuntimeMaterials.Clear();
            return;
        }

        var sharedMaterials = renderer.sharedMaterials;
        for (int i = 0; i < sharedMaterials.Length; ++i)
        {
            var material = sharedMaterials[i];
            if (material != null)
                uniqueMaterials.Add(material);
        }
    }

    private static bool UsesSceneMaterialKeywords(Material material)
    {
        return material != null
            && material.shader != null
            && material.shader.name != null
            && material.shader.name.IndexOf(HsrShaderNameToken, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ApplySceneMaterialKeywords(Material material, bool clearOnly)
    {
        if (material == null)
            return;

        SetMaterialKeyword(material, HeightLerpKeyword, !clearOnly && HeightLerpEnable);
        SetMaterialKeyword(material, FogKeyword, !clearOnly && IsFogEnabled());
    }

    private static void SetMaterialKeyword(Material material, string keyword, bool enabled)
    {
        if (material == null || string.IsNullOrEmpty(keyword))
            return;

        bool isEnabled = material.IsKeywordEnabled(keyword);
        if (enabled)
        {
            if (!isEnabled)
                material.EnableKeyword(keyword);
        }
        else if (isEnabled)
        {
            material.DisableKeyword(keyword);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(material);
#endif
    }

    

    private void Update() {
        RefreshCharacterLightsIfNeeded(forceSlowPath: false);
        SyncSceneMaterialKeywordsIfNeeded(forceSlowPath: false);

        // Optional: Auto-sync monster light dir to main light if not manually overridden
        if (main_light != null && _ES_MonsterLightDir == Vector3.zero) {
            _ES_MonsterLightDir = -main_light.transform.forward;
        }

        if (_isGlobalRotMatrixDirty || _cachedGlobalRotationEuler != _es_global_rotation_euler)
        {
            _cachedGlobalRotationEuler = _es_global_rotation_euler;
            _ES_GlobalRotMatrix = Matrix4x4.Rotate(Quaternion.Euler(_cachedGlobalRotationEuler));
            _isGlobalRotMatrixDirty = false;
        }
    }

    public Matrix4x4 GetGlobalRotMatrix()
    {
        if (_isGlobalRotMatrixDirty || _cachedGlobalRotationEuler != _es_global_rotation_euler)
        {
            _cachedGlobalRotationEuler = _es_global_rotation_euler;
            _ES_GlobalRotMatrix = Matrix4x4.Rotate(Quaternion.Euler(_cachedGlobalRotationEuler));
            _isGlobalRotMatrixDirty = false;
        }

        return _ES_GlobalRotMatrix;
    }
}
}