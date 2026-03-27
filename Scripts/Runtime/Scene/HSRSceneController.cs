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

    // [Header("Global Intensity")]
    [HideInInspector]public float _GlobalOneMinusAvatarIntensity = 0f;
    
    [Header("Main Light Settings")]
    [PropertyLabel("Main Light"), Tooltip("The main directional light in the scene. If null, the controller will attempt to find a light named 'HoyoToon Scene Light'.")]
    public Light main_light;
    public Light[] CharacterLights;
    [HideInInspector] public Vector3 _ES_MonsterLightDir = new Vector3(0, 1, 0);
    [HideInInspector] public bool _ES_Indoor;
    [HideInInspector] public float _ES_TransitionRate = 1.0f;
    [PropertyLabel("Hair Shadow Height Adjust")]
    public float _ES_SelfShadowLerpHair;


    // [Header("Environment Rotation")]
    [HideInInspector] public Vector3 _es_global_rotation_euler = Vector3.zero;
    [HideInInspector] public Matrix4x4 _ES_GlobalRotMatrix { get; private set; }
    private Vector3 _cachedGlobalRotationEuler;
    private bool _isGlobalRotMatrixDirty = true;
    private bool _hasAppliedSceneMaterialKeywords;
    private int _lastSceneMaterialKeywordStateHash;


    [Header("Character Lighting & Outlines")]
    [Range(0f, 1f)]
    [PropertyLabel("Ramp Day Night Blend"), Tooltip("Blends between day and night ramp textures. 0 = day, 1 = night.")]
    public float _ES_CharacterToonRampMode = 0f;
    [PropertyLabel("Disable Local Main Light"), Tooltip("If enabled, character materials will not receive lighting from local main lights (e.g. CharacterLights).")]
    [HideInInspector] public bool _ES_CharacterDisableLocalMainLight = false;
    [PropertyLabel("Add Color"), Tooltip("Adds a color overlay to the character materials.")]
    public Color _ES_AddColor = Color.clear;
    [PropertyLabel("Specular Color"), Tooltip("The color of the specular highlights on character materials.")]
    public Color _ES_SPColor = Color.white;
    [PropertyLabel("Specular Intensity"), Tooltip("The intensity of the specular highlights on character materials.")]
    public float _ES_SPIntensity = 1.0f;
    [PropertyLabel("Outline Darken Value"), Tooltip("The darken value of the character outlines.")]
    public float _ES_OutLineDarkenVal = 0.0f;
    [PropertyLabel("Outline Lightened Value"), Tooltip("The lighted value of the character outlines.")]
    public float _ES_OutLineLightedVal = 0.0f;
    [PropertyLabel("Outline Disable Distance Scale"), Tooltip("The distance scale at which outlines are disabled.")]

    public float _ES_OutlineDisableDistanceScale = 0f;
    [PropertyLabel("Outline Fallback Scale"), Tooltip("The scale of the character outlines when the main light is below the horizon.")]
    public float _ES_OutlineFallbackScale = 1.0f;   
    [PropertyLabel("Outline Scale"), Tooltip("The scale of the character outlines.")]
    public float _OutlineScale = 0.0149f;

    [Header("Rim Shadow")]
    
    [PropertyLabel("Rim Shadow Color"), Tooltip("The color of the rim shadow on character materials.")]
    public Color _ES_RimShadowColor = Color.black;
    [PropertyLabel("Rim Shadow Intensity"), Tooltip("The intensity of the rim shadow on character materials.")]
    public float _ES_RimShadowIntensity = 1.0f;
    [HideInInspector] public float _ES_CharacterShadowFactor = 1.0f;
    [Header("Rim Light")]
    [PropertyLabel("Rim Light Offset"), Tooltip("The offset of the rim light direction")]
    public Vector2 _ES_RimLightOffset = Vector2.zero;
    [PropertyLabel("Rim Light Mode"), Tooltip("Use lightmap Red Channel for Mask Blend")]
    public float _ES_RimLightMode = 0.0f;
    [PropertyLabel("Rim Light Width"), Tooltip("The width of the rim light on character materials.")]
    public float _ES_RimLightWidth = 1.0f;
    [PropertyLabel("Rim Light Intensity"), Tooltip("The intensity of the rim light on character materials.")]

    public float _ES_RimLightIntensity = 1.0f;
    [PropertyLabel("Rim Light Add Mode"), Tooltip("Adds a rim light on top of the rim shadow instead of multiplying it.")]
    public float _ES_RimLightAddMode = 0f;
    [PropertyLabel("Rim Light Color"), Tooltip("The color of the rim light on character materials.")]
    public Color _ES_RimLightColor = Color.white;

    [PropertyLabel("Enable Height Light"), Tooltip("Enables height-based color lerping for character materials.")]
    public bool HeightLerpEnable = false; 
    [PropertyLabel("Height Light Top"), Tooltip("The normalized height at which the top color is fully applied in height-based color lerping.")]
    public float _ES_HeightLerpTop = 0.2f;
    [PropertyLabel("Height Light Bottom"), Tooltip("The normalized height at which the bottom color is fully applied in height-based color lerping.")]
    public float _ES_HeightLerpBottom = 0.4f;
    [PropertyLabel("Height Light Top Color"), Tooltip("The color applied at the top height in height-based color lerping.")]

    public Color _ES_HeightLerpTopColor = Color.white;
    [PropertyLabel("Height Light Middle Color"), Tooltip("The color applied at the middle height in height-based color lerping.")]
    public Color _ES_HeightLerpMiddleColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    [PropertyLabel("Height Light Bottom Color"), Tooltip("The color applied at the bottom height in height-based color lerping.")]
    public Color _ES_HeightLerpBottomColor = new Color(0.3137254715f, 0.3137254715f, 0.4901961088f, 0.5000f);

    // [Header("Shadow Color Grading")]
    [PropertyLabel("Enable Shadow Color Grading")]
    public bool _ES_LEVEL_ADJUST_ON = false;
    [PropertyLabel("Skin Area Light Color"), Tooltip("The color applied to the lit areas of character skin materials when shadow color grading is enabled. Alpha controls the strength/intensity of the color")]
    public Color _ES_LevelSkinLightColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    [PropertyLabel("Skin Area Shadow Color"), Tooltip("The color applied to the shadowed areas of character skin materials when shadow color grading is enabled. Alpha controls the strength/intensity of the color")]
    public Color _ES_LevelSkinShadowColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    [PropertyLabel("Highlight Area Color"), Tooltip("The color applied to the highlight areas of character materials when shadow color grading is enabled. Alpha controls the strength/intensity of the color")]
    public Color _ES_LevelHighLightColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    [PropertyLabel("Shadow Area Color"), Tooltip("The color applied to the shadowed areas of character materials when shadow color grading is enabled. Alpha controls the strength/intensity of the color")]
    public Color _ES_LevelShadowColor = new Color(1.0000f, 1.0000f, 1.0000f, 0.5000f);
    [PropertyLabel("Grading Shadow Area"), Tooltip("The strength of the shadow color grading effect on character materials.")]
    [Range(0, 1)] public float _ES_LevelShadow = 0.0f;
    [PropertyLabel("Grading Mid Lighting Area"), Tooltip("The strength of the highlight color grading effect on character materials.")]
    [Range(0, 1)] public float _ES_LevelMid = 0.55f;
    [PropertyLabel("Grading Highlight Area"), Tooltip("The strength of the highlight color grading effect on character materials.")]
    [Range(0, 1)] public float _ES_LevelHighLight = 1.0f;
    [PropertyLabel("Grading EyeShadow Area"),
     Tooltip("The strength of the skin area color grading effect on character skin materials.")]
    [Range(0, 1)] public float _ES_LevelEyeShadowIntensity = 1.0f;
    [HideInInspector]public bool _ES_IndoorCharShadowAsCookie = false;

    // [Header("Fog Settings")]
    [PropertyLabel("Fog Color"), Tooltip("The color of the fog in the scene.")]
    public float _ES_FogColor = 0.5625f;
    [PropertyLabel("Fog Density"), Tooltip("The density of the fog in the scene.")]
    public float _ES_FogDensity = 1.0f;
    [PropertyLabel("Fog Near"), Tooltip("The distance from the camera at which the fog starts to appear.")]
    public float _ES_FogNear = 300f;
    [PropertyLabel("Fog Far"), Tooltip("The distance from the camera at which the fog ends.")]
    public float _ES_FogFar = 800f;
    [Space(10)]
    [PropertyLabel("Height Fog Color"), Tooltip("The color of the height-based fog in the scene.")]
    public float _ES_HeightFogColor = 0.8125f;
    [PropertyLabel("Height Fog Base Height"), Tooltip("The base height of the height-based fog. The fog density will be calculated based on the difference between the world position height and this base height.")]
    public float _ES_HeightFogBaseHeight = 6f;
    [PropertyLabel("Height Fog Range"), Tooltip("The range of the height-based fog.")]
    public float _ES_HeightFogRange = 40f;
    [PropertyLabel("Height Fog Density"), Tooltip("The density of the height-based fog in the scene.")]
    public float _ES_HeightFogDensity = 1f;
    [PropertyLabel("Height Fog Near"), Tooltip("The distance from the camera at which the height-based fog starts to appear.")]
    public float _ES_HeightFogFogNear = 10f;
    [PropertyLabel("Height Fog Far"), Tooltip("The distance from the camera at which the height-based fog ends.")]
    public float _ES_HeightFogFogFar = 130f;
    [PropertyLabel("Fog Character Near Factor"), Tooltip("The factor that determines how close the fog affects characters.")]
    public float _ES_FogCharacterNearFactor =  0.1f;
    [PropertyLabel("Height Fog Adjustment"), Tooltip("The adjustment value for the height-based fog.")]
    public float _ES_HeightFogAddAjust = 0f;
    [PropertyLabel("Disable Fog Transition"), Tooltip("If enabled, fog will not transition smoothly when changing settings, but will instead snap to the new settings immediately.")]
    public bool _ES_DisableFogTransition = false;
    
    // [Header("Effects")]
    [HideInInspector] public Vector4 _ES_EffCustomLightPosition = Vector4.zero;

    // [Header("Draw Extensions (Local Lights)")]
    [HideInInspector] public Color _CharacterLocalMainLightColor1 = Color.white;
    [HideInInspector] public Color _CharacterLocalMainLightColor2 = Color.white;
    [HideInInspector] public Color _CharacterLocalMainLightDark = Color.black;
    [HideInInspector] public float _DisableCharacterLocalLight = 0f;

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