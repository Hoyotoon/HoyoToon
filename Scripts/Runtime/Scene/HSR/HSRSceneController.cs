using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using HoyoToon.Runtime.Character.HSR;
using HoyoToon.Runtime.Core;
using HoyoToon.Runtime.Rendering.HSR;

namespace HoyoToon.Runtime.Scene.HSR
{
[ExecuteAlways]
public class HSRSceneController : MonoBehaviour
{
    private const float CharacterLightSlowDiscoveryInterval = 2.0f;
    private const float MaterialKeywordSlowSyncInterval = 2.0f;
    private const float MinColorTemperature = 1000f;
    private const float MaxColorTemperature = 20000f;
    private const float DefaultDirectionalYaw = 180f;
    private const float DefaultAutoRotateSpeed = 50f;
    private const string CharacterLightName = "CharacterLight";
    private const string SceneLightName = "HoyoToon Scene Light";
    private const string HsrShaderNameToken = "Honkai Star Rail";
    private const string HeightLerpKeyword = "_HEIGHTLERP";
    private const string FogKeyword = "_ENABLE_FOG";
    private static readonly List<Light> s_CharacterLightRegistry = new List<Light>();
    private static readonly List<HSRCharacterController> s_CharacterControllers = new List<HSRCharacterController>();
    private static readonly List<Material> s_RuntimeMaterials = new List<Material>();
    private static readonly HashSet<Material> s_UniqueCharacterMaterials = new HashSet<Material>();
    private static readonly List<Light> s_DiscoveredCharacterLights = new List<Light>();
    private static bool s_CharacterLightRegistryDirty = true;
    private static float s_NextSlowCharacterLightDiscoveryTime;
    private static float s_NextSlowMaterialKeywordSyncTime;

    public enum AutoRotateDirection
    {
        Clockwise,
        CounterClockwise
    }

    private readonly struct TransformSnapshot
    {
        public TransformSnapshot(Transform transform)
        {
            Transform = transform;
            LocalPosition = transform != null ? transform.localPosition : Vector3.zero;
            LocalRotation = transform != null ? transform.localRotation : Quaternion.identity;
            LocalScale = transform != null ? transform.localScale : Vector3.one;
        }

        public Transform Transform { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
    }

    // [Header("Global Intensity")]
    [HideInInspector]public float _GlobalOneMinusAvatarIntensity = 0f;
    
    [Header("Main Light Settings")]
    [PropertyLabel("Main Light"), Tooltip("The main directional light in the scene. If null, the controller will attempt to find a light named 'HoyoToon Scene Light'.")]
    [SerializeField] private Light main_light;
    public Light MainLight => main_light;
    [PropertyLabel("Use Color Temperature"), Tooltip("Enables Kelvin-based temperature tinting on the scene main light.")]
    public bool MainLightUseColorTemperature = false;
    [PropertyLabel("Filter"), Tooltip("The base color tint applied to the scene main light.")]
    public Color MainLightColor = Color.white;
    [PropertyLabel("Temperature"), Tooltip("The scene main light color temperature in Kelvin.")]
    [Min(MinColorTemperature)]
    public float MainLightColorTemperature = 6570f;
    [PropertyLabel("Mode"), Tooltip("The bake mode used by the scene main light.")]
    public LightmapBakeType MainLightMode = LightmapBakeType.Realtime;
    [PropertyLabel("Intensity"), Tooltip("The intensity applied to the scene main light.")]
    [Min(0f)]
    public float MainLightIntensity = 1f;
    [PropertyLabel("Indirect Multiplier"), Tooltip("Bounce lighting multiplier applied to the scene main light.")]
    [Min(0f)]
    public float MainLightIndirectMultiplier = 1f;
    [PropertyLabel("Shadow Type"), Tooltip("Realtime shadow mode used by the scene main light.")]
    public LightShadows MainLightShadowType = LightShadows.Soft;
    [PropertyLabel("Shadow Resolution"), Tooltip("Shadow resolution override used by the scene main light.")]
    public LightShadowResolution MainLightShadowResolution = LightShadowResolution.FromQualitySettings;
    [PropertyLabel("Shadow Strength"), Tooltip("Shadow strength applied to the scene main light.")]
    [Range(0f, 2f)]
    public float MainLightShadowStrength = 1f;
    [PropertyLabel("Shadow Bias"), Tooltip("Bias applied to the scene main light shadows.")]
    [Range(0f, 2f)]
    public float MainLightShadowBias = 0.05f;
    [PropertyLabel("Shadow Normal Bias"), Tooltip("Normal bias applied to the scene main light shadows.")]
    [Range(0f, 3f)]
    public float MainLightShadowNormalBias = 0.4f;
    [PropertyLabel("Shadow Near Plane"), Tooltip("Near plane used for scene main light shadow rendering.")]
    [Range(0.01f, 10f)]
    public float MainLightShadowNearPlane = 0.2f;
    [PropertyLabel("Rotation"), Tooltip("Yaw rotation, in local space, applied to the scene main light when auto rotate is disabled.")]
    [Range(0f, 360f)]
    public float MainLightRotation = DefaultDirectionalYaw;
    [PropertyLabel("Auto Rotate"), Tooltip("Temporarily rotates the scene main light as a preview until disabled.")]
    public bool MainLightAutoRotate = false;
    [PropertyLabel("Auto Rotate Speed"), Tooltip("Degrees per second used while auto rotate is enabled.")]
    [Range(0f, 180f)]
    public float MainLightAutoRotateSpeed = DefaultAutoRotateSpeed;
    [PropertyLabel("Direction"), Tooltip("Auto rotate direction applied to the scene main light.")]
    public AutoRotateDirection MainLightAutoRotateDirection = AutoRotateDirection.Clockwise;
    [SerializeField, HideInInspector] private Light[] CharacterLights;
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
    private int _lastMainLightSettingsHash;
    private bool _mainLightAutoRotateActive;
    private float _lastMainLightAutoRotateTime;
    private TransformSnapshot _mainLightAutoRotateSnapshot;
    private bool _hasMainLightAutoRotateSnapshot;
    [SerializeField, HideInInspector] private bool _mainLightSettingsInitialized;


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
        s_DiscoveredCharacterLights.Clear();
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

    private void ResolveMainLightReference()
    {
        main_light = HsrSceneLightQueryUtility.FindLowestInstanceIdNamedLight(SceneLightName);
    }

    private void RebuildCharacterLightsFromScene()
    {
        Light resolvedSceneLight = HsrSceneLightQueryUtility.RebuildCharacterLightsAndResolveSceneMain(
            CharacterLightName,
            SceneLightName,
            s_DiscoveredCharacterLights);

        s_CharacterLightRegistry.Clear();
        s_CharacterLightRegistry.AddRange(s_DiscoveredCharacterLights);
        CharacterLights = s_DiscoveredCharacterLights.ToArray();
        main_light = resolvedSceneLight;
        s_CharacterLightRegistryDirty = false;
    }

    private void SyncCharacterLightsFromRegistry()
    {
        PruneNullCharacterLights();
        if (CharacterLights != null && CharacterLights.Length == s_CharacterLightRegistry.Count)
        {
            bool same = true;
            for (int i = 0; i < CharacterLights.Length; i++)
            {
                if (CharacterLights[i] != s_CharacterLightRegistry[i])
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                s_CharacterLightRegistryDirty = false;
                return;
            }
        }

        CharacterLights = s_CharacterLightRegistry.ToArray();
        s_CharacterLightRegistryDirty = false;
    }

    private void RefreshCharacterLightsIfNeeded(bool forceSlowPath)
    {
        if (forceSlowPath)
        {
            RebuildCharacterLightsFromScene();
            s_NextSlowCharacterLightDiscoveryTime = Time.realtimeSinceStartup + CharacterLightSlowDiscoveryInterval;
            return;
        }

        if (s_CharacterLightRegistryDirty || CharacterLights == null)
        {
            if (s_CharacterLightRegistry.Count > 0)
            {
                SyncCharacterLightsFromRegistry();
            }
            else
            {
                RebuildCharacterLightsFromScene();
                s_NextSlowCharacterLightDiscoveryTime = Time.realtimeSinceStartup + CharacterLightSlowDiscoveryInterval;
                return;
            }
        }

        if (!HsrSceneLightQueryUtility.IsNamedLight(main_light, SceneLightName) && Time.realtimeSinceStartup >= s_NextSlowCharacterLightDiscoveryTime)
        {
            ResolveMainLightReference();
            s_NextSlowCharacterLightDiscoveryTime = Time.realtimeSinceStartup + CharacterLightSlowDiscoveryInterval;
        }

        if (Time.realtimeSinceStartup < s_NextSlowCharacterLightDiscoveryTime)
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
        SyncMainLightIfNeeded(forceApply: true);
        InvalidateGlobalRotMatrixCache();
        InvalidateSceneMaterialKeywordSync();
        SyncSceneMaterialKeywordsIfNeeded(forceSlowPath: true);
    }

    private void OnDisable()
    {
        EndMainLightAutoRotateSession();

        if (_instance == this)
        {
            ApplySceneMaterialKeywords(clearOnly: true);
            _hasAppliedSceneMaterialKeywords = false;
            _instance = FindActiveReplacement(this);
            if (_instance != null)
            {
                _instance.InvalidateSceneMaterialKeywordSync();
                _instance.SyncSceneMaterialKeywordsIfNeeded(forceSlowPath: true);
            }
        }
    }

    private void OnValidate()
    {
        SanitizeMainLightSettings();
        SanitizeSceneSettings();
        InvalidateGlobalRotMatrixCache();
        InvalidateSceneMaterialKeywordSync();
        if (isActiveAndEnabled)
        {
            RefreshCharacterLightsIfNeeded(forceSlowPath: true);
            SyncMainLightIfNeeded(forceApply: true);
            SyncSceneMaterialKeywordsIfNeeded(forceSlowPath: true);
        }
    }

    private static HSRSceneController FindActiveReplacement(HSRSceneController current)
    {
        HSRSceneController[] controllers = FindObjectsByType<HSRSceneController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; ++i)
        {
            HSRSceneController controller = controllers[i];
            if (controller != null && controller != current && controller.isActiveAndEnabled)
                return controller;
        }

        return null;
    }

    private void SyncMainLightIfNeeded(bool forceApply)
    {
        SanitizeMainLightSettings();
        TryInitializeMainLightSettings();
        ApplyMainLightSettings(forceApply);
        SyncMainLightTransform(forceApply);
    }

    private void SanitizeMainLightSettings()
    {
        MainLightColorTemperature = Mathf.Clamp(MainLightColorTemperature, MinColorTemperature, MaxColorTemperature);
        MainLightIntensity = Mathf.Max(0f, MainLightIntensity);
        MainLightIndirectMultiplier = Mathf.Max(0f, MainLightIndirectMultiplier);
        MainLightShadowStrength = Mathf.Clamp(MainLightShadowStrength, 0f, 2f);
        MainLightShadowBias = Mathf.Clamp(MainLightShadowBias, 0f, 2f);
        MainLightShadowNormalBias = Mathf.Clamp(MainLightShadowNormalBias, 0f, 3f);
        MainLightShadowNearPlane = Mathf.Clamp(MainLightShadowNearPlane, 0.01f, 10f);
        MainLightRotation = NormalizeYaw(MainLightRotation);
        MainLightAutoRotateSpeed = Mathf.Clamp(MainLightAutoRotateSpeed, 0f, 180f);
    }

    private void SanitizeSceneSettings()
    {
        _ES_TransitionRate = Mathf.Max(0f, _ES_TransitionRate);
        _ES_CharacterToonRampMode = Mathf.Clamp01(_ES_CharacterToonRampMode);
        _ES_SPIntensity = Mathf.Max(0f, _ES_SPIntensity);
        _ES_OutlineDisableDistanceScale = Mathf.Max(0f, _ES_OutlineDisableDistanceScale);
        _ES_OutlineFallbackScale = Mathf.Max(0f, _ES_OutlineFallbackScale);
        _OutlineScale = Mathf.Max(0f, _OutlineScale);
        _ES_RimShadowIntensity = Mathf.Max(0f, _ES_RimShadowIntensity);
        _ES_RimLightWidth = Mathf.Max(0f, _ES_RimLightWidth);
        _ES_RimLightIntensity = Mathf.Max(0f, _ES_RimLightIntensity);
        _ES_RimLightAddMode = Mathf.Clamp01(_ES_RimLightAddMode);
        _ES_HeightFogRange = Mathf.Max(0.001f, _ES_HeightFogRange);
        _ES_FogDensity = Mathf.Max(0f, _ES_FogDensity);
        _ES_HeightFogDensity = Mathf.Max(0f, _ES_HeightFogDensity);
        _ES_FogNear = Mathf.Max(0f, _ES_FogNear);
        _ES_FogFar = Mathf.Max(_ES_FogNear, _ES_FogFar);
        _ES_HeightFogFogNear = Mathf.Max(0f, _ES_HeightFogFogNear);
        _ES_HeightFogFogFar = Mathf.Max(_ES_HeightFogFogNear, _ES_HeightFogFogFar);
        _ES_FogCharacterNearFactor = Mathf.Max(0f, _ES_FogCharacterNearFactor);
    }

    private void TryInitializeMainLightSettings()
    {
        if (_mainLightSettingsInitialized || main_light == null)
            return;

        MainLightUseColorTemperature = main_light.useColorTemperature;
        MainLightColor = main_light.color;
        MainLightColorTemperature = Mathf.Clamp(main_light.colorTemperature, MinColorTemperature, MaxColorTemperature);
        MainLightMode = main_light.lightmapBakeType;
        MainLightIntensity = Mathf.Max(0f, main_light.intensity);
        MainLightIndirectMultiplier = Mathf.Max(0f, main_light.bounceIntensity);
        MainLightShadowType = main_light.shadows;
        MainLightShadowResolution = main_light.shadowResolution;
        MainLightShadowStrength = Mathf.Clamp(main_light.shadowStrength, 0f, 2f);
        MainLightShadowBias = Mathf.Clamp(main_light.shadowBias, 0f, 2f);
        MainLightShadowNormalBias = Mathf.Clamp(main_light.shadowNormalBias, 0f, 3f);
        MainLightShadowNearPlane = Mathf.Clamp(main_light.shadowNearPlane, 0.01f, 10f);
        MainLightRotation = NormalizeYaw(main_light.transform.localEulerAngles.y);
        _mainLightSettingsInitialized = true;
        _lastMainLightSettingsHash = ComputeMainLightSettingsHash();
    }

    private void ApplyMainLightSettings(bool forceApply)
    {
        if (main_light == null)
            return;

        int settingsHash = ComputeMainLightSettingsHash();
        if (!forceApply && settingsHash == _lastMainLightSettingsHash)
        {
            SyncRenderSettingsSun();
            return;
        }

        bool lightChanged = false;
        if (main_light.type != LightType.Directional)
        {
            main_light.type = LightType.Directional;
            lightChanged = true;
        }

        if (main_light.useColorTemperature != MainLightUseColorTemperature)
        {
            main_light.useColorTemperature = MainLightUseColorTemperature;
            lightChanged = true;
        }

        if (main_light.color != MainLightColor)
        {
            main_light.color = MainLightColor;
            lightChanged = true;
        }

        if (!Mathf.Approximately(main_light.colorTemperature, MainLightColorTemperature))
        {
            main_light.colorTemperature = MainLightColorTemperature;
            lightChanged = true;
        }

        if (main_light.lightmapBakeType != MainLightMode)
        {
            main_light.lightmapBakeType = MainLightMode;
            lightChanged = true;
        }

        if (!Mathf.Approximately(main_light.intensity, MainLightIntensity))
        {
            main_light.intensity = MainLightIntensity;
            lightChanged = true;
        }

        if (!Mathf.Approximately(main_light.bounceIntensity, MainLightIndirectMultiplier))
        {
            main_light.bounceIntensity = MainLightIndirectMultiplier;
            lightChanged = true;
        }

        if (main_light.shadows != MainLightShadowType)
        {
            main_light.shadows = MainLightShadowType;
            lightChanged = true;
        }

        if (main_light.shadowResolution != MainLightShadowResolution)
        {
            main_light.shadowResolution = MainLightShadowResolution;
            lightChanged = true;
        }

        if (!Mathf.Approximately(main_light.shadowStrength, MainLightShadowStrength))
        {
            main_light.shadowStrength = MainLightShadowStrength;
            lightChanged = true;
        }

        if (!Mathf.Approximately(main_light.shadowBias, MainLightShadowBias))
        {
            main_light.shadowBias = MainLightShadowBias;
            lightChanged = true;
        }

        if (!Mathf.Approximately(main_light.shadowNormalBias, MainLightShadowNormalBias))
        {
            main_light.shadowNormalBias = MainLightShadowNormalBias;
            lightChanged = true;
        }

        if (!Mathf.Approximately(main_light.shadowNearPlane, MainLightShadowNearPlane))
        {
            main_light.shadowNearPlane = MainLightShadowNearPlane;
            lightChanged = true;
        }

        if (lightChanged)
            RuntimeEditorBridge.MarkDirty(main_light);

        _lastMainLightSettingsHash = settingsHash;
        SyncRenderSettingsSun();
    }

    private void SyncRenderSettingsSun()
    {
        if (main_light != null && main_light.type == LightType.Directional && RenderSettings.sun != main_light)
        {
            RenderSettings.sun = main_light;
        }
    }

    private void SyncMainLightTransform(bool forceApply)
    {
        if (main_light == null || main_light.transform == null)
        {
            EndMainLightAutoRotateSession();
            return;
        }

        if (MainLightAutoRotate)
        {
            BeginMainLightAutoRotateSession();
            RotateMainLight();
            return;
        }

        EndMainLightAutoRotateSession();
        ApplyManualMainLightRotation(forceApply);
    }

    private void ApplyManualMainLightRotation(bool forceApply)
    {
        if (main_light == null || main_light.transform == null)
            return;

        float normalizedYaw = NormalizeYaw(MainLightRotation);
        Vector3 currentEuler = main_light.transform.localEulerAngles;
        float currentYaw = NormalizeYaw(currentEuler.y);
        if (!forceApply && Mathf.Approximately(currentYaw, normalizedYaw))
            return;

        main_light.transform.localEulerAngles = new Vector3(currentEuler.x, normalizedYaw, currentEuler.z);
        RuntimeEditorBridge.MarkDirty(main_light.transform);
    }

    private void BeginMainLightAutoRotateSession()
    {
        if (main_light == null || main_light.transform == null)
            return;

        if (_hasMainLightAutoRotateSnapshot && _mainLightAutoRotateSnapshot.Transform != main_light.transform)
            EndMainLightAutoRotateSession();

        if (_mainLightAutoRotateActive)
            return;

        _mainLightAutoRotateSnapshot = new TransformSnapshot(main_light.transform);
        _hasMainLightAutoRotateSnapshot = true;
        _mainLightAutoRotateActive = true;
        _lastMainLightAutoRotateTime = Time.realtimeSinceStartup;
    }

    private void RotateMainLight()
    {
        if (!_mainLightAutoRotateActive || main_light == null || main_light.transform == null)
            return;

        float now = Time.realtimeSinceStartup;
        float deltaTime = now - _lastMainLightAutoRotateTime;
        _lastMainLightAutoRotateTime = now;
        if (deltaTime <= 0f || MainLightAutoRotateSpeed <= 0f)
            return;

        float direction = MainLightAutoRotateDirection == AutoRotateDirection.Clockwise ? 1f : -1f;
        Vector3 currentEuler = main_light.transform.localEulerAngles;
        float newYaw = NormalizeYaw(currentEuler.y + (MainLightAutoRotateSpeed * deltaTime * direction));
        main_light.transform.localEulerAngles = new Vector3(currentEuler.x, newYaw, currentEuler.z);
        RuntimeEditorBridge.MarkDirty(main_light.transform);
    }

    private void EndMainLightAutoRotateSession()
    {
        if (_hasMainLightAutoRotateSnapshot)
            RestoreMainLightAutoRotateTransform();

        _mainLightAutoRotateActive = false;
        _hasMainLightAutoRotateSnapshot = false;
        _lastMainLightAutoRotateTime = 0f;
    }

    private void RestoreMainLightAutoRotateTransform()
    {
        Transform transform = _mainLightAutoRotateSnapshot.Transform;
        if (transform == null)
            return;

        transform.localPosition = _mainLightAutoRotateSnapshot.LocalPosition;
        transform.localRotation = _mainLightAutoRotateSnapshot.LocalRotation;
        transform.localScale = _mainLightAutoRotateSnapshot.LocalScale;
        RuntimeEditorBridge.MarkDirty(transform);
    }

    private int ComputeMainLightSettingsHash()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (main_light != null ? main_light.GetInstanceID() : 0);
            hash = (hash * 31) + MainLightUseColorTemperature.GetHashCode();
            hash = (hash * 31) + MainLightColor.GetHashCode();
            hash = (hash * 31) + MainLightColorTemperature.GetHashCode();
            hash = (hash * 31) + MainLightMode.GetHashCode();
            hash = (hash * 31) + MainLightIntensity.GetHashCode();
            hash = (hash * 31) + MainLightIndirectMultiplier.GetHashCode();
            hash = (hash * 31) + MainLightShadowType.GetHashCode();
            hash = (hash * 31) + MainLightShadowResolution.GetHashCode();
            hash = (hash * 31) + MainLightShadowStrength.GetHashCode();
            hash = (hash * 31) + MainLightShadowBias.GetHashCode();
            hash = (hash * 31) + MainLightShadowNormalBias.GetHashCode();
            hash = (hash * 31) + MainLightShadowNearPlane.GetHashCode();
            return hash;
        }
    }

    private static float NormalizeYaw(float yaw)
    {
        return Mathf.Repeat(yaw, 360f);
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
            Renderer[] controllerRenderers = controller != null ? controller.GetScopedRenderers() : null;
            if (controllerRenderers == null)
                continue;

            for (int j = 0; j < controllerRenderers.Length; ++j)
                HsrRendererMaterialQueryUtility.AddSharedMaterials(controllerRenderers[j], s_RuntimeMaterials, s_UniqueCharacterMaterials);
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
        bool changed = false;
        if (enabled)
        {
            if (!isEnabled)
            {
                material.EnableKeyword(keyword);
                changed = true;
            }
        }
        else if (isEnabled)
        {
            material.DisableKeyword(keyword);
            changed = true;
        }

        if (changed)
            RuntimeEditorBridge.MarkDirty(material);
    }

    

    private void Update() {
        SanitizeSceneSettings();
        RefreshCharacterLightsIfNeeded(forceSlowPath: false);
        SyncMainLightIfNeeded(forceApply: false);
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
