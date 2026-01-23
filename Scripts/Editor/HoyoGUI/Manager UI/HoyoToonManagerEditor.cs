#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.EditorTools.ManagerUI.Components;
using HoyoToon.EditorTools.ManagerScene;
using HoyoToon.Utilities;

namespace HoyoToon.EditorTools.ManagerUI
{
    [CustomEditor(typeof(HoyoToonManager))]
    public class HoyoToonManagerEditor : Editor
    {
        private static readonly Dictionary<int, GameObject> s_PendingModelCache = new Dictionary<int, GameObject>();

        private static class Styles
        {
            public static readonly GUIStyle HeaderTitle;
            public static readonly GUIStyle SubHeader;

            static Styles()
            {
                HeaderTitle = new GUIStyle(EditorStyles.largeLabel)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };

                SubHeader = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 11,
                    wordWrap = true
                };
            }
        }

        private HoyoToonBannerHeader _bannerHeader;
        private HoyoToonManagerModuleNavbar _moduleNavbar;
        private HoyoToonManagerFooter _footer;

        private SerializedProperty _managedModelsProperty;
        private SerializedProperty _activeModelIndexProperty;
        private bool _modelsDirty = true;
        private GameObject _pendingModelAsset;
        private bool _pendingModelIsReady;
        private HoyoToonManagerValidationUtility.ValidationResult _pendingValidationResult;

        private void OnEnable()
        {
            _bannerHeader = new HoyoToonBannerHeader();
            _moduleNavbar = new HoyoToonManagerModuleNavbar();
            _moduleNavbar.RegisterModule(new Modules.MainModule());
            _moduleNavbar.RegisterModule(new Modules.LightingModule());
            _moduleNavbar.RegisterModule(new Modules.MaterialsModule());
            _moduleNavbar.RegisterModule(new Modules.PostProcessingModule());
            _footer = new HoyoToonManagerFooter(
                activeModelProvider: GetActiveManagedModel,
                prefabFolderResolver: ResolveActiveModelFolder,
                createPrefabAction: CreatePrefabFromActiveModel);
            CacheSerializedProperties();
            _modelsDirty = true;
            _pendingModelAsset = TryGetCachedPendingModel(target, out var cached) ? cached : null;
            _pendingModelIsReady = false;
            _pendingValidationResult = HoyoToonManagerValidationUtility.GetPendingModelValidation();
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        }

        private void OnDisable()
        {
            CachePendingModel(target, _pendingModelAsset);
            _moduleNavbar?.Dispose();
            _moduleNavbar = null;
            _bannerHeader = null;
            _footer = null;
            _managedModelsProperty = null;
            _activeModelIndexProperty = null;
            _pendingModelAsset = null;
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
        }

        public override void OnInspectorGUI()
        {
            try
            {
                serializedObject.Update();
                if (_managedModelsProperty == null || _activeModelIndexProperty == null)
                {
                    CacheSerializedProperties();
                }

                _bannerHeader.Draw();
                EditorGUILayout.Space(8f);

                if (_modelsDirty)
                {
                    AutoDiscoverSceneModels((HoyoToonManager)target);
                }
                UpdateValidationState();
                DrawHeaderSection();

                // Push header edits (like active model selection) immediately so downstream UI reads fresh data.
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();

                EditorGUILayout.Space(6f);
                DrawModuleNavbar();

                EditorGUILayout.Space(8f);
                DrawBodySection();
                EditorGUILayout.Space(6f);
                _footer?.Draw();

                serializedObject.ApplyModifiedProperties();
            }
            catch (Exception ex)
            {
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.HelpBox($"HoyoToon Manager UI error: {ex.Message}", MessageType.Error);
            }
        }

        private void CacheSerializedProperties()
        {
            _managedModelsProperty = serializedObject.FindProperty("managedModels");
            _activeModelIndexProperty = serializedObject.FindProperty("activeModelIndex");
        }

        private void DrawModuleNavbar()
        {
            if (_moduleNavbar == null)
            {
                EditorGUILayout.HelpBox("No modules are registered.", MessageType.Info);
                return;
            }

            _moduleNavbar.Draw();
        }

        private void UpdateValidationState()
        {
            var manager = (HoyoToonManager)target;
            _pendingModelIsReady = HoyoToonManagerValidationUtility.CanProcessModel(manager, _pendingModelAsset, out _pendingValidationResult);
        }

        private void DrawHeaderSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
                DrawAddModelRow();
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Active Model", EditorStyles.boldLabel);
                DrawActiveModelRow();
            }
        }

        private void DrawBodySection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (ShouldShowCrossTabValidationWarning())
                {
                    EditorGUILayout.HelpBox("Validation issues detected. Switch to the Main tab to review details.", MessageType.Error);
                }
                EditorGUILayout.Space(6f);
                DrawSelectedModule();
            }
        }

        private bool ShouldShowCrossTabValidationWarning()
        {
            var selectedIndex = _moduleNavbar?.SelectedIndex ?? -1;
            if (selectedIndex <= 0)
            {
                return false;
            }

            if (_pendingModelAsset != null && !_pendingValidationResult.IsReady)
            {
                return true;
            }

            return false;
        }

        private void DrawActiveModelRow()
        {
            if (_managedModelsProperty == null || _activeModelIndexProperty == null)
            {
                EditorGUILayout.LabelField("No managed models detected.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            int modelCount = _managedModelsProperty.arraySize;
            int storedIndex = Mathf.Clamp(_activeModelIndexProperty.intValue, -1, modelCount - 1);
            if (storedIndex != _activeModelIndexProperty.intValue)
            {
                _activeModelIndexProperty.intValue = storedIndex;
            }

            string[] options = BuildModelDisplayNames();
            int popupIndex = storedIndex + 1;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Selection", GUILayout.Width(100f));
                int newPopupIndex = EditorGUILayout.Popup(popupIndex, options);
                int newStoredIndex = newPopupIndex - 1;
                if (newStoredIndex != storedIndex)
                {
                    _activeModelIndexProperty.intValue = newStoredIndex;
                    var manager = (HoyoToonManager)target;
                    HoyoToonSceneLightController.RefreshForManager(manager);
                }

                using (new EditorGUI.DisabledScope(newStoredIndex < 0 || newStoredIndex >= modelCount))
                {
                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(50f)))
                    {
                        var model = GetManagedModelAtIndex(newStoredIndex);
                        if (model)
                        {
                            Selection.activeObject = model;
                            EditorGUIUtility.PingObject(model);
                        }
                    }
                }
            }
        }

        private string[] BuildModelDisplayNames()
        {
            int count = _managedModelsProperty.arraySize;
            var names = new string[count + 1];
            names[0] = "None";

            for (int i = 0; i < count; i++)
            {
                var element = _managedModelsProperty.GetArrayElementAtIndex(i);
                var go = element.objectReferenceValue as GameObject;
                names[i + 1] = go ? go.name : $"Missing Reference ({i + 1})";
            }

            return names;
        }

        private void DrawAddModelRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var newPending = (GameObject)EditorGUILayout.ObjectField("Add Model", _pendingModelAsset, typeof(GameObject), false);
                if (newPending != _pendingModelAsset)
                {
                    _pendingModelAsset = newPending;
                    CachePendingModel(target, _pendingModelAsset);
                }
                using (new EditorGUI.DisabledScope(!_pendingModelIsReady))
                {
                    string buttonLabel = ResolveAddModelButtonLabel(_pendingModelAsset);
                    if (GUILayout.Button(buttonLabel, GUILayout.Width(110f)))
                    {
                        if (TryAddModelFromAsset(_pendingModelAsset, out var resolvedAsset))
                        {
                            _pendingModelAsset = resolvedAsset ?? _pendingModelAsset;
                            CachePendingModel(target, _pendingModelAsset);
                        }
                    }
                }
            }
        }

        private static void CachePendingModel(UnityEngine.Object targetObject, GameObject pendingAsset)
        {
            if (targetObject == null)
            {
                return;
            }

            int key = targetObject.GetInstanceID();
            if (pendingAsset == null)
            {
                s_PendingModelCache.Remove(key);
            }
            else
            {
                s_PendingModelCache[key] = pendingAsset;
            }
        }

        private static bool TryGetCachedPendingModel(UnityEngine.Object targetObject, out GameObject pendingAsset)
        {
            pendingAsset = null;
            if (targetObject == null)
            {
                return false;
            }

            return s_PendingModelCache.TryGetValue(targetObject.GetInstanceID(), out pendingAsset);
        }

        private bool TryAddModelFromAsset(GameObject modelAsset, out GameObject resolvedAsset)
        {
            resolvedAsset = modelAsset;
            var manager = (HoyoToonManager)target;
            if (manager == null || modelAsset == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(modelAsset);
            if (string.IsNullOrEmpty(assetPath))
            {
                HoyoToonDialogWindow.ShowError("Unsupported Asset", "Please select an FBX or prefab asset.");
                return false;
            }

            if (assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                if (HoyoToonModelSetupUtility.TryProcessFbxAndInstantiate(manager, modelAsset, out var instance, out var resolved))
                {
                    AddManagedModelInstance(instance);
                    resolvedAsset = resolved ?? modelAsset;
                    _modelsDirty = true;
                    return true;
                }

                return false;
            }

            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                if (HoyoToonModelSetupUtility.TryInstantiatePrefabAsset(manager, modelAsset, out var instance, out var resolved))
                {
                    AddManagedModelInstance(instance);
                    resolvedAsset = resolved ?? modelAsset;
                    _modelsDirty = true;
                    return true;
                }

                return false;
            }

            HoyoToonDialogWindow.ShowError("Unsupported Asset", "Only FBX and prefab assets are supported.");
            return false;
        }

        private void AddManagedModelInstance(GameObject model, bool setActive = true)
        {
            if (model == null)
            {
                return;
            }

            if (_managedModelsProperty == null || _activeModelIndexProperty == null)
            {
                CacheSerializedProperties();
                if (_managedModelsProperty == null || _activeModelIndexProperty == null)
                {
                    return;
                }
            }

            if (!model.scene.IsValid())
            {
                return;
            }

            for (int i = 0; i < _managedModelsProperty.arraySize; i++)
            {
                if (_managedModelsProperty.GetArrayElementAtIndex(i).objectReferenceValue == model)
                {
                    if (setActive)
                    {
                        _activeModelIndexProperty.intValue = i;
                    }
                    return;
                }
            }

            int newIndex = _managedModelsProperty.arraySize;
            _managedModelsProperty.arraySize++;
            _managedModelsProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = model;
            if (setActive)
            {
                _activeModelIndexProperty.intValue = newIndex;
            }
        }

        private void RemoveManagedModelAt(int index)
        {
            if (index < 0 || index >= _managedModelsProperty.arraySize)
            {
                return;
            }

            int previousSize = _managedModelsProperty.arraySize;
            _managedModelsProperty.DeleteArrayElementAtIndex(index);
            if (_managedModelsProperty.arraySize == previousSize)
            {
                _managedModelsProperty.DeleteArrayElementAtIndex(index);
            }

            int storedIndex = _activeModelIndexProperty.intValue;
            if (storedIndex == index)
            {
                _activeModelIndexProperty.intValue = Mathf.Clamp(index - 1, -1, _managedModelsProperty.arraySize - 1);
            }
            else if (storedIndex > index)
            {
                _activeModelIndexProperty.intValue = storedIndex - 1;
            }
        }

        private GameObject GetManagedModelAtIndex(int index)
        {
            if (index < 0 || index >= _managedModelsProperty.arraySize)
            {
                return null;
            }

            return _managedModelsProperty.GetArrayElementAtIndex(index).objectReferenceValue as GameObject;
        }

        private void AutoDiscoverSceneModels(HoyoToonManager manager)
        {
            if (!_modelsDirty || _managedModelsProperty == null || manager == null)
            {
                return;
            }

            _modelsDirty = false;

            PruneInvalidManagedModels(manager);

            var managerScene = manager.gameObject.scene;
            if (!managerScene.IsValid())
            {
                return;
            }

            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid() || renderer.gameObject.scene != managerScene)
                {
                    continue;
                }

                if (!RendererUsesHoyoToon(renderer))
                {
                    continue;
                }

                var root = ResolveRendererRoot(renderer, manager);
                if (!IsSceneModelValid(root, manager))
                {
                    continue;
                }

                AddManagedModelInstance(root, false);
            }

            if (_activeModelIndexProperty != null && _activeModelIndexProperty.intValue < 0 && _managedModelsProperty.arraySize > 0)
            {
                _activeModelIndexProperty.intValue = 0;
            }
        }

        private void PruneInvalidManagedModels(HoyoToonManager manager)
        {
            if (manager == null)
            {
                return;
            }

            for (int i = _managedModelsProperty.arraySize - 1; i >= 0; i--)
            {
                var element = _managedModelsProperty.GetArrayElementAtIndex(i);
                var go = element?.objectReferenceValue as GameObject;
                if (!IsSceneModelValid(go, manager))
                {
                    RemoveManagedModelAt(i);
                }
            }
        }

        private bool IsSceneModelValid(GameObject go, HoyoToonManager manager)
        {
            if (!go || manager == null)
            {
                return false;
            }

            if (go == manager.gameObject)
            {
                return false;
            }

            if (!go.scene.IsValid() || go.scene != manager.gameObject.scene)
            {
                return false;
            }

            return HasHoyoToonRenderer(go);
        }

        private bool HasHoyoToonRenderer(GameObject root)
        {
            if (!root)
            {
                return false;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (RendererUsesHoyoToon(renderer))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RendererUsesHoyoToon(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            var materials = renderer.sharedMaterials;
            foreach (var material in materials)
            {
                if (!material || material.shader == null)
                {
                    continue;
                }

                if (material.shader.name.IndexOf("HoyoToon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawSelectedModule()
        {
            if (_moduleNavbar == null)
            {
                EditorGUILayout.HelpBox("No modules are registered.", MessageType.Info);
                return;
            }

            var module = _moduleNavbar.GetSelectedModule();
            if (module == null)
            {
                EditorGUILayout.HelpBox("Selected module is missing.", MessageType.Warning);
                return;
            }

            module.OnGUI((HoyoToonManager)target);
        }

        private void HandleHierarchyChanged()
        {
            _modelsDirty = true;
        }
        
        private static string ResolveAddModelButtonLabel(GameObject pendingAsset)
        {
            if (pendingAsset == null)
            {
                return "Auto Setup";
            }

            var assetPath = AssetDatabase.GetAssetPath(pendingAsset);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return "Add Prefab";
            }

            return "Auto Setup";
        }

        private GameObject GetActiveManagedModel()
        {
            var manager = (HoyoToonManager)target;
            return manager != null ? manager.ActiveModel : null;
        }

        private string ResolveActiveModelFolder(GameObject model)
        {
            if (model == null)
            {
                return null;
            }

            return TryGetActiveModelFolder(model, out var folder) ? folder : null;
        }

        private static bool TryGetActiveModelFolder(GameObject activeModel, out string folder)
        {
            folder = null;
            if (activeModel == null)
            {
                return false;
            }

            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(activeModel);
            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = AssetDatabase.GetAssetPath(activeModel);
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            folder = directory.Replace('\\', '/');
            return true;
        }

        private void CreatePrefabFromActiveModel(GameObject activeModel)
        {
            if (activeModel == null)
            {
                return;
            }

            if (!TryGetActiveModelFolder(activeModel, out var folder))
            {
                HoyoToonDialogWindow.ShowError("Cannot Create Prefab", "Unable to locate the original asset folder for this model.");
                return;
            }

            var prefabName = $"{activeModel.name}.prefab";
            var savePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefabName}");

            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(activeModel, savePath, InteractionMode.UserAction);
                if (prefab == null)
                {
                    HoyoToonDialogWindow.ShowError("Prefab Creation Failed", "Unity could not create the prefab asset. Check the Console for more information.");
                    return;
                }

                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                HoyoToonDialogWindow.ShowInfo("Prefab Created", $"Saved prefab to:\n{savePath}");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ManagerError($"Failed to create prefab for {activeModel.name}: {ex.Message}");
                HoyoToonDialogWindow.ShowError("Prefab Creation Failed", $"Could not create prefab: {ex.Message}");
            }
        }

        private static GameObject ResolveRendererRoot(Renderer renderer, HoyoToonManager manager)
        {
            if (renderer == null)
            {
                return null;
            }

            var managerTransform = manager != null ? manager.transform : null;
            var current = renderer.transform;

            while (current != null && current.parent != null && current.parent != managerTransform)
            {
                current = current.parent;
            }

            if (current == null)
            {
                return null;
            }

            if (current == managerTransform)
            {
                return null;
            }

            return current.gameObject;
        }
    }
}
#endif
