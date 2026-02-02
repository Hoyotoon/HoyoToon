using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using HoyoToon;
using System.Text.RegularExpressions;

namespace HoyoToon.EditorTools.ManagerScene
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class HoyoToonScriptablesController : MonoBehaviour
    {
        [Serializable]
        public sealed class RendererGroup
        {
            public GameObject Root;
            public string GameKey;
            public Renderer[] Renderers = Array.Empty<Renderer>();
        }

        [Serializable]
        public sealed class HsrLightingSettings
        {
            [HideInInspector]
            public bool Enabled = true;
            public bool UseColdRamp = false;
            public bool SPColorEnable = false;
            public Color SPColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            public bool SPIntensityEnable = false;
            public float SPIntensity = 0.1f;
            public bool EnableShadowBoost = false;
            [Range(0.0f, 0.5f)]
            public float ShadowBoost = 0.5f;

            public bool HeightLerpEnable = false;
            public float HeightLerpTop = 1.0f;
            public float HeightLerpBottom = 0.0f;
            public Color HeightLerpTopColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
            public Color HeightLerpMiddleColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
            public Color HeightLerpBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            public bool RimLightEnable = false;
            public Color RimLightColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            public float RimLightWidth = 1.0f;
            public float RimLightIntensity = 1.0f;
            public Vector4 RimLightOffset = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            public bool RimShadowEnable = false;
            public float RimShadowIntensity = 1.0f;
            public Color RimShadowColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);

            public bool LevelAdjustEnable = false;
            public Color LevelSkinLightColor = new Color(1.0f, 1.0f, 1.0f, 0.55f);
            public Color LevelSkinShadowColor = new Color(1.0f, 1.0f, 1.0f, 0.55f);
            public Color LevelHighLightColor = new Color(1.0f, 1.0f, 1.0f, 0.55f);
            public Color LevelShadowColor = new Color(1.0f, 1.0f, 1.0f, 0.55f);
            public float LevelShadow = 0.0f;
            public float LevelMid = 0.55f;
            public float LevelHighLight = 1.0f;

            public bool UseFakeDirectionalLight = false;

            public bool UseFakeFog = false;
            public Color FakeFogColor = Color.black;
            public float FakeFogDensity = 0.0f;
            public float FakeFogHeightFalloff = 0.0f;
            public float FakeFogStartHeight = 0.0f;
        }

        [Serializable]
        public sealed class GiLightingSettings
        {
            public bool Enabled = true;
        }

        [Serializable]
        public sealed class Hi3LightingSettings
        {
            public bool Enabled = true;
        }


        [Serializable]
        public sealed class ZzzLightingSettings
        {
            public bool Enabled = true;
        }


        [Serializable]
        public sealed class GameLightingSettings
        {
            public string GameKey;
            public HsrLightingSettings HonkaiStarRail = new HsrLightingSettings();
            public GiLightingSettings GenshinImpact = new GiLightingSettings();
            public Hi3LightingSettings HonkaiImpact3rd = new Hi3LightingSettings();
            public ZzzLightingSettings ZenlessZoneZero = new ZzzLightingSettings();
        }

#if UNITY_EDITOR
        internal sealed class UiGroupDefinition
        {
            public UiGroupDefinition(string defaultGroup, Dictionary<string, string[]> groups)
            {
                DefaultGroup = string.IsNullOrWhiteSpace(defaultGroup) ? "Settings" : defaultGroup;
                Groups = groups ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            }

            public string DefaultGroup { get; }
            public IReadOnlyDictionary<string, string[]> Groups { get; }
        }

        private static readonly Dictionary<string, UiGroupDefinition> UiGroupings = new Dictionary<string, UiGroupDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "Honkai Star Rail",
                new UiGroupDefinition("Other", new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "General", new[] { "UseColdRamp" } },
                    { "Shadows", new[] { "EnableShadowBoost", "ShadowBoost" } },
                    { "Specular Highlight", new[] { "SPColor*", "SPIntensity*" } },
                    { "Height Lerp", new[] { "HeightLerp*" } },
                    { "Rim Light", new[] { "RimLight*" } },
                    { "Rim Shadow", new[] { "RimShadow*" } },
                    { "Level Adjust", new[] { "Level*" } },
                    { "Directional & Fog", new[] { "UseFakeDirectionalLight", "UseFakeFog", "FakeFog*" } }
                })
            }
        };

        public static bool TryGetUiGrouping(string gameKey, out IReadOnlyDictionary<string, string[]> groups, out string defaultGroup)
        {
            groups = null;
            defaultGroup = "Settings";

            if (string.IsNullOrEmpty(gameKey))
            {
                return false;
            }

            if (UiGroupings.TryGetValue(gameKey, out var definition))
            {
                groups = definition.Groups;
                defaultGroup = definition.DefaultGroup;
                return true;
            }

            return false;
        }
#endif

        [Header("Manager")]
        public bool AutoFindManager = true;
        public HoyoToonManager Manager;

        [Header("Game Detection")]
        public bool AutoDetectGame = true;
        public string GameKey;

        [Header("Lights")]
        public bool AutoDetectDirectionalLight = true;
        public Light DirectionalLight;
        public bool AutoDetectPointLights = true;
        [Min(1)]
        public int MaxPointLights = 3;
        public Light[] PointLights = Array.Empty<Light>();

        [Header("Renderers")]
        public bool AutoCollectRenderers = true;
        public bool UseManagerModels = true;
        public bool UseActiveModelOnly = true;
        public bool IncludeInactive = true;
        public LayerMask LayerMask = ~0;
        public bool IncludeSkinnedRenderers = true;
        public bool IncludeMeshRenderers = true;
        public string[] IncludeNameContains;
        public string[] ExcludeNameContains;
        public string[] ShaderNameContains;
        public List<RendererGroup> RendererGroups = new List<RendererGroup>();
        public Renderer[] Renderers = Array.Empty<Renderer>();

        [Header("Game Settings")]
        public List<GameLightingSettings> GameSettings = new List<GameLightingSettings>();

        [Header("Apply")]
        public bool ApplyInEditMode = true;
        public bool ApplyInPlayMode = true;

        [Header("Diagnostics")]
        public bool EnableLogs = false;

        private bool _needsRefresh = true;
        private int _lastManagerId;
        private int _lastManagedCount;
        private int _lastActiveModelId;

        private void OnEnable()
        {
            EnsureProfilesInitialized();
            _needsRefresh = true;
            Refresh();
        }

        private void OnValidate()
        {
            EnsureProfilesInitialized();
            _needsRefresh = true;
            Refresh();
        }

        private void OnTransformChildrenChanged()
        {
            _needsRefresh = true;
        }

        private void OnTransformParentChanged()
        {
            _needsRefresh = true;
        }

        private void Update()
        {
            TrackManagerChanges();

            if (AutoCollectRenderers && (RendererGroups == null || RendererGroups.Count == 0))
            {
                _needsRefresh = true;
            }

            if (_needsRefresh && AutoCollectRenderers)
            {
                Refresh();
                _needsRefresh = false;
            }

            if (Application.isPlaying)
            {
                if (ApplyInPlayMode)
                {
                    Apply();
                }
            }
            else if (ApplyInEditMode)
            {
                Apply();
            }
        }

        public void Refresh()
        {
            if (AutoFindManager)
            {
                Manager = Manager != null ? Manager : FindObjectOfType<HoyoToonManager>();
            }

#if UNITY_EDITOR
            if (AutoDetectGame)
            {
                GameKey = DetectGameKeyFromObject(gameObject) ?? GameKey;
            }
#endif

            if (AutoDetectDirectionalLight)
            {
                DirectionalLight = ResolveDirectionalLight(DirectionalLight);
            }

            if (AutoDetectPointLights)
            {
                PointLights = ResolvePointLights(PointLights, Mathf.Max(1, MaxPointLights));
            }

            if (AutoCollectRenderers)
            {
                RefreshRendererGroups();
            }
            else
            {
                CleanupRendererGroups();
            }

            if (EnableLogs)
            {
                Debug.Log($"SceneLightController refreshed. Groups={RendererGroups?.Count ?? 0}, Renderers={Renderers?.Length ?? 0}", this);
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                if (gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
#endif
        }

        public bool IsValidForManager(HoyoToonManager manager, string expectedGameKey)
        {
            if (manager == null || Manager == null)
            {
                return false;
            }

            if (!ReferenceEquals(manager, Manager))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(expectedGameKey)
                && !string.Equals(GameKey, expectedGameKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public void Apply()
        {
            if (RendererGroups == null || RendererGroups.Count == 0)
            {
                return;
            }

            foreach (var group in RendererGroups)
            {
                if (group == null || group.Renderers == null || group.Renderers.Length == 0)
                {
                    continue;
                }

                var gameKey = group.GameKey;
                if (string.IsNullOrEmpty(gameKey))
                {
                    continue;
                }

                var settings = FindSettingsForGame(gameKey);
                if (settings == null)
                {
                    continue;
                }

                if (string.Equals(gameKey, "Honkai Star Rail", StringComparison.OrdinalIgnoreCase)
                    && settings.HonkaiStarRail != null
                    && settings.HonkaiStarRail.Enabled)
                {
                    ApplyHonkaiStarRailLighting(group.Renderers, settings.HonkaiStarRail);
                }
            }
        }

        public static HoyoToonScriptablesController EnsureForManager(HoyoToonManager manager, string gameKey = null)
        {
            if (manager == null)
            {
                return null;
            }

            var existing = FindObjectsOfType<HoyoToonScriptablesController>(true)
                .FirstOrDefault(controller => controller != null && controller.Manager == manager);

            if (existing != null)
            {
                if (!string.IsNullOrEmpty(gameKey))
                {
                    existing.GameKey = gameKey;
                }
                existing.Refresh();
                return existing;
            }

            var go = new GameObject("HoyoToon Scriptables Controller");
            go.transform.SetParent(manager.transform, false);
            var controllerNew = go.AddComponent<HoyoToonScriptablesController>();
            controllerNew.Manager = manager;
            if (!string.IsNullOrEmpty(gameKey))
            {
                controllerNew.GameKey = gameKey;
            }
            controllerNew.Refresh();
            return controllerNew;
        }

        public static HoyoToonScriptablesController EnsureForManager(HoyoToonManager manager, Transform parent, string gameKey)
        {
            if (manager == null)
            {
                return null;
            }

            var existing = FindObjectsOfType<HoyoToonScriptablesController>(true)
                .FirstOrDefault(controller => controller != null && controller.Manager == manager);

            if (existing != null)
            {
                if (!string.IsNullOrEmpty(gameKey))
                {
                    existing.GameKey = gameKey;
                }

                if (parent != null && existing.transform.parent != parent)
                {
                    existing.transform.SetParent(parent, false);
                }

                existing.Refresh();
                return existing;
            }

            var go = new GameObject("HoyoToon Scriptables Controller");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            else
            {
                go.transform.SetParent(manager.transform, false);
            }

            var controllerNew = go.AddComponent<HoyoToonScriptablesController>();
            controllerNew.Manager = manager;
            if (!string.IsNullOrEmpty(gameKey))
            {
                controllerNew.GameKey = gameKey;
            }
            controllerNew.Refresh();
            return controllerNew;
        }

        public static void RefreshForManager(HoyoToonManager manager)
        {
            if (manager == null)
            {
                return;
            }

            var controllers = FindObjectsOfType<HoyoToonScriptablesController>(true)
                .Where(controller => controller != null && controller.Manager == manager);

            foreach (var controller in controllers)
            {
                controller.Refresh();
            }
        }

        private void RefreshRendererGroups()
        {
            var groups = new List<RendererGroup>();
            var allRenderers = new List<Renderer>();

            var renderers = CollectRenderersInScene();
            if (renderers.Length == 0)
            {
                RendererGroups = new List<RendererGroup>();
                Renderers = Array.Empty<Renderer>();
                return;
            }

            var grouped = renderers
                .GroupBy(renderer => ResolveRendererRoot(renderer))
                .Where(group => group.Key != null);

            foreach (var group in grouped)
            {
                var root = group.Key.gameObject;
                var groupRenderers = group.Where(renderer => renderer != null).Distinct().ToArray();
                if (groupRenderers.Length == 0)
                {
                    continue;
                }

                var key = ResolveGameKeyForRoot(root);
                groups.Add(new RendererGroup
                {
                    Root = root,
                    GameKey = key,
                    Renderers = groupRenderers
                });

                allRenderers.AddRange(groupRenderers);
            }

            RendererGroups = groups;
            Renderers = allRenderers.Distinct().ToArray();
        }

        private Renderer[] CollectRenderersInScene()
        {
            var list = new List<Renderer>();
            var scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return Array.Empty<Renderer>();
            }

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                if (IncludeSkinnedRenderers)
                {
                    list.AddRange(root.GetComponentsInChildren<SkinnedMeshRenderer>(IncludeInactive));
                }

                if (IncludeMeshRenderers)
                {
                    list.AddRange(root.GetComponentsInChildren<MeshRenderer>(IncludeInactive));
                }
            }

            return list
                .Where(RendererPassesFilters)
                .Distinct()
                .ToArray();
        }

        private Transform ResolveRendererRoot(Renderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

#if UNITY_EDITOR
            var prefabRoot = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
            if (prefabRoot != null)
            {
                return prefabRoot.transform;
            }
#endif

            var animator = renderer.GetComponentInParent<Animator>(true);
            if (animator != null)
            {
                return animator.transform;
            }

            return renderer.transform.root;
        }

        private bool RendererPassesFilters(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            if ((LayerMask.value & (1 << renderer.gameObject.layer)) == 0)
            {
                return false;
            }

            string name = renderer.gameObject.name;
            if (!MatchesAny(name, IncludeNameContains, defaultValue: true))
            {
                return false;
            }

            if (MatchesAny(name, ExcludeNameContains, defaultValue: false))
            {
                return false;
            }

            if (ShaderNameContains != null && ShaderNameContains.Length > 0)
            {
                bool shaderMatch = false;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null) continue;
                    if (MatchesAny(material.shader.name, ShaderNameContains, defaultValue: false))
                    {
                        shaderMatch = true;
                        break;
                    }
                }

                if (!shaderMatch)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesAny(string value, string[] patterns, bool defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (patterns == null || patterns.Length == 0)
            {
                return defaultValue;
            }

            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                if (value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private GameLightingSettings FindSettingsForGame(string gameKey)
        {
            if (GameSettings == null || GameSettings.Count == 0 || string.IsNullOrEmpty(gameKey))
            {
                return null;
            }

            return GameSettings.FirstOrDefault(settings =>
                !string.IsNullOrEmpty(settings.GameKey)
                && string.Equals(settings.GameKey, gameKey, StringComparison.OrdinalIgnoreCase));
        }

        private static Light ResolveDirectionalLight(Light current)
        {
            if (current != null && current.type == LightType.Directional)
            {
                return current;
            }

            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
            {
                return RenderSettings.sun;
            }

            var lights = FindObjectsOfType<Light>(true);
            foreach (var light in lights)
            {
                if (light != null && light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }

        private static Light[] ResolvePointLights(Light[] current, int maxLights)
        {
            if (current != null && current.Length > 0 && current.All(light => light != null && light.type == LightType.Point))
            {
                return current.Take(maxLights).ToArray();
            }

            var lights = FindObjectsOfType<Light>(true)
                .Where(light => light != null && light.type == LightType.Point)
                .OrderByDescending(light => light.intensity)
                .Take(maxLights)
                .ToArray();

            return lights.Length > 0 ? lights : Array.Empty<Light>();
        }

        private void TrackManagerChanges()
        {
            int managerId = Manager != null ? Manager.GetInstanceID() : 0;
            int managedCount = Manager != null && Manager.ManagedModels != null ? Manager.ManagedModels.Count : 0;
            int activeId = Manager != null && Manager.ActiveModel != null ? Manager.ActiveModel.GetInstanceID() : 0;

            if (managerId != _lastManagerId || managedCount != _lastManagedCount || activeId != _lastActiveModelId)
            {
                _lastManagerId = managerId;
                _lastManagedCount = managedCount;
                _lastActiveModelId = activeId;
                _needsRefresh = true;
            }
        }

        private void CleanupRendererGroups()
        {
            if (RendererGroups == null)
            {
                RendererGroups = new List<RendererGroup>();
                Renderers = Array.Empty<Renderer>();
                return;
            }

            RendererGroups = RendererGroups
                .Where(group => group != null && group.Root != null)
                .Select(group =>
                {
                    group.Renderers = group.Renderers?.Where(renderer => renderer != null).ToArray() ?? Array.Empty<Renderer>();
                    return group;
                })
                .Where(group => group.Renderers.Length > 0)
                .ToList();

            Renderers = RendererGroups.SelectMany(group => group.Renderers).Distinct().ToArray();
        }

        private string ResolveGameKeyForRoot(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

#if UNITY_EDITOR
            if (AutoDetectGame)
            {
                var detected = DetectGameKeyFromObject(root);
                if (!string.IsNullOrEmpty(detected))
                {
                    if (EnableLogs)
                    {
                        Debug.Log($"SceneLightController detected game '{detected}' from root '{root.name}'.", this);
                    }
                    return detected;
                }

                var detectedFromRenderer = DetectGameKeyFromRenderers(root);
                if (!string.IsNullOrEmpty(detectedFromRenderer))
                {
                    if (EnableLogs)
                    {
                        Debug.Log($"SceneLightController detected game '{detectedFromRenderer}' from renderer under '{root.name}'.", this);
                    }
                    return detectedFromRenderer;
                }
            }
#endif

            return null;
        }

#if UNITY_EDITOR
        private string DetectGameKeyFromRenderers(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                if (materials == null)
                {
                    continue;
                }

                foreach (var material in materials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    var shaderName = material.shader != null ? material.shader.name : null;
                    var keyFromShader = ResolveGameKeyFromShader(shaderName);
                    if (!string.IsNullOrEmpty(keyFromShader))
                    {
                        return keyFromShader;
                    }

                    var assetPath = UnityEditor.AssetDatabase.GetAssetPath(material);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        continue;
                    }

                    var detected = DetectGameKeyViaReflection(material, assetPath);
                    if (!string.IsNullOrEmpty(detected))
                    {
                        return detected;
                    }
                }
            }

            return null;
        }

        private static string ResolveGameKeyFromShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
            {
                return null;
            }

            if (shaderName.IndexOf("Honkai Star Rail", StringComparison.OrdinalIgnoreCase) >= 0
                || shaderName.IndexOf("HSR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Honkai Star Rail";
            }

            if (shaderName.IndexOf("Genshin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Genshin Impact";
            }

            if (shaderName.IndexOf("Honkai Impact", StringComparison.OrdinalIgnoreCase) >= 0
                || shaderName.IndexOf("HI3", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Honkai Impact 3rd";
            }

            if (shaderName.IndexOf("Zenless", StringComparison.OrdinalIgnoreCase) >= 0
                || shaderName.IndexOf("ZZZ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Zenless Zone Zero";
            }

            return null;
        }

        private static string DetectGameKeyFromObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return null;
            }

            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(target);
            var context = target;

            if (string.IsNullOrEmpty(assetPath) && target is GameObject go)
            {
                var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (source != null)
                {
                    var sourcePath = UnityEditor.AssetDatabase.GetAssetPath(source);
                    if (!string.IsNullOrEmpty(sourcePath))
                    {
                        assetPath = sourcePath;
                        context = source;
                    }
                }
            }
            try
            {
                var detectionType = ResolveMaterialDetectionType();
                if (detectionType == null)
                {
                    return null;
                }

                var method = detectionType.GetMethod(
                    "DetectGameAndShaderAutoWithSource",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(UnityEngine.Object), typeof(string) },
                    null);
                if (method == null)
                {
                    return null;
                }

                var result = method.Invoke(null, new object[] { context, assetPath });
                if (result == null)
                {
                    return null;
                }

                var gameKeyProp = result.GetType().GetProperty("Item1");
                if (gameKeyProp == null)
                {
                    return null;
                }

                return gameKeyProp.GetValue(result) as string;
            }
            catch
            {
                return null;
            }
        }

        private static string DetectGameKeyViaReflection(UnityEngine.Object context, string assetPath)
        {
            if (context == null)
            {
                return null;
            }

            try
            {
                var detectionType = ResolveMaterialDetectionType();
                if (detectionType == null)
                {
                    return null;
                }

                var method = detectionType.GetMethod(
                    "DetectGameAndShaderAutoWithSource",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(UnityEngine.Object), typeof(string) },
                    null);
                if (method == null)
                {
                    return null;
                }

                var result = method.Invoke(null, new object[] { context, assetPath });
                if (result == null)
                {
                    return null;
                }

                var gameKeyProp = result.GetType().GetProperty("Item1");
                if (gameKeyProp == null)
                {
                    return null;
                }

                return gameKeyProp.GetValue(result) as string;
            }
            catch
            {
                return null;
            }
        }

        private static Type ResolveMaterialDetectionType()
        {
            const string typeName = "HoyoToon.Materials.MaterialDetection";
            var direct = Type.GetType(typeName);
            if (direct != null)
            {
                return direct;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
#endif

        private void EnsureProfilesInitialized()
        {
            if (RendererGroups == null)
            {
                RendererGroups = new List<RendererGroup>();
            }

            if (GameSettings == null)
            {
                GameSettings = new List<GameLightingSettings>();
            }

            if (GameSettings.Count > 0)
            {
                return;
            }

            GameSettings.Add(new GameLightingSettings { GameKey = "Honkai Star Rail" });
        }

        private void ApplyHonkaiStarRailLighting(Renderer[] renderers, HsrLightingSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null) continue;

                var mats = renderer.sharedMaterials;
                if (mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;

                    mat.SetFloat("_ES_CharacterToonRampMode", settings.UseColdRamp ? 1.0f : 0.0f);

                    if (settings.SPColorEnable && mat.HasProperty("_ES_SPColor"))
                        mat.SetColor("_ES_SPColor", settings.SPColor);
                    else
                        mat.SetColor("_ES_SPColor", Color.white);

                    if (settings.SPIntensityEnable && mat.HasProperty("_ES_SPIntensity"))
                        mat.SetFloat("_ES_SPIntensity", settings.SPIntensity);
                    else
                        mat.SetFloat("_ES_SPIntensity", 1.0f);

                    if (settings.EnableShadowBoost && mat.HasProperty("_ShadowBoost"))
                    {
                        mat.SetFloat("_ShadowBoost", 1.0f);
                        mat.SetFloat("_ShadowBoostVal", settings.ShadowBoost);
                    }
                    else
                    {
                        mat.SetFloat("_ShadowBoost", 0.0f);
                        mat.SetFloat("_ShadowBoostVal", 0.5f);
                    }

                    if (settings.HeightLerpEnable)
                    {
                        mat.SetFloat("_UseHeightLerp", 1.0f);
                        mat.SetFloat("_ES_HeightLerpBottom", settings.HeightLerpBottom);
                        mat.SetFloat("_ES_HeightLerpTop", settings.HeightLerpTop);
                        mat.SetColor("_ES_HeightLerpBottomColor", settings.HeightLerpBottomColor);
                        mat.SetColor("_ES_HeightLerpMiddleColor", settings.HeightLerpMiddleColor);
                        mat.SetColor("_ES_HeightLerpTopColor", settings.HeightLerpTopColor);
                    }
                    else
                    {
                        mat.SetFloat("_UseHeightLerp", 0.0f);
                    }

                    if (settings.LevelAdjustEnable)
                    {
                        mat.SetFloat("_ES_LEVEL_ADJUST_ON", 1.0f);
                        mat.SetColor("_ES_LevelSkinLightColor", settings.LevelSkinLightColor);
                        mat.SetColor("_ES_LevelSkinShadowColor", settings.LevelSkinShadowColor);
                        mat.SetColor("_ES_LevelHighLightColor", settings.LevelHighLightColor);
                        mat.SetColor("_ES_LevelShadowColor", settings.LevelShadowColor);
                        mat.SetFloat("_ES_LevelShadow", settings.LevelShadow);
                        mat.SetFloat("_ES_LevelMid", settings.LevelMid);
                        mat.SetFloat("_ES_LevelHighLight", settings.LevelHighLight);
                    }
                    else
                    {
                        mat.SetFloat("_ES_LEVEL_ADJUST_ON", 0.0f);
                    }

                    if (settings.RimLightEnable)
                    {
                        mat.SetFloat("_ES_RimLightWidth", settings.RimLightWidth);
                        mat.SetFloat("_ES_RimLightAddMode", settings.RimLightIntensity);
                        mat.SetVector("_ES_RimLightOffset", settings.RimLightOffset);
                        mat.SetColor("_ES_RimLightColor", settings.RimLightColor);
                        mat.SetFloat("_ES_RimLightIntensity", settings.RimLightIntensity);
                    }
                    else
                    {
                        mat.SetFloat("_ES_RimLightWidth", 1.0f);
                        mat.SetFloat("_ES_RimLightAddMode", 0.07f);
                        mat.SetVector("_ES_RimLightOffset", Vector4.zero);
                        mat.SetColor("_ES_RimLightColor", Color.white);
                        mat.SetFloat("_ES_RimLightIntensity", 0.1f);
                    }

                    if (settings.RimShadowEnable)
                    {
                        mat.SetFloat("_ES_RimShadowIntensity", settings.RimShadowIntensity);
                        mat.SetColor("_ES_RimShadowColor", settings.RimShadowColor);
                    }
                    else
                    {
                        mat.SetFloat("_ES_RimShadowIntensity", 0.0f);
                        mat.SetColor("_ES_RimShadowColor", Color.white);
                    }

                    int activeLightCount = 0;
                    for (int i = 0; i < Mathf.Max(1, MaxPointLights); i++)
                    {
                        var light = PointLights != null && i < PointLights.Length ? PointLights[i] : null;
                        if (light != null && light.isActiveAndEnabled && light.type == LightType.Point)
                        {
                            mat.SetVector("_FakePointLight" + i + "Pos", light.transform.position);
                            mat.SetFloat("_FakePointLight" + i + "Intensity", light.intensity);
                            mat.SetFloat("_FakePointLight" + i + "AttenuationRadius", light.range);
                            mat.SetColor("_FakePointLight" + i + "Color", light.color);
                            mat.SetFloat("_FakePointLight" + i, 1f);
                            activeLightCount++;
                        }
                        else
                        {
                            mat.SetFloat("_FakePointLight" + i, 0f);
                            mat.SetFloat("_FakePointLight" + i + "Intensity", 0f);
                        }
                    }

                    mat.SetInt("_FakePointLightNum", activeLightCount);

                    if (settings.UseFakeDirectionalLight && DirectionalLight != null && DirectionalLight.type == LightType.Directional)
                    {
                        mat.SetVector("_FakeDirectionalLightRotation", -DirectionalLight.transform.forward);
                        mat.SetFloat("_UseFakeDirectionalLight", 1f);
                    }
                    else
                    {
                        mat.SetFloat("_UseFakeDirectionalLight", 0f);
                    }

                    if (settings.UseFakeFog)
                    {
                        mat.SetFloat("_FakeFogHeightFalloff", settings.FakeFogHeightFalloff);
                        mat.SetFloat("_FakeFogStartHeight", settings.FakeFogStartHeight);
                        mat.SetFloat("_FakeFogDensity", settings.FakeFogDensity);
                        mat.SetColor("_FakeFogColor", settings.FakeFogColor);
                    }
                    else
                    {
                        mat.SetFloat("_FakeFogDensity", 0f);
                        mat.SetColor("_FakeFogColor", Color.black);
                    }
                }
            }
        }
    }
}