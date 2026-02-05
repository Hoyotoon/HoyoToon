#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.EditorTools.ManagerUI.Components;
using HoyoToon.EditorTools.ManagerScene;
using HoyoToon.Materials;
using HoyoToon.Utilities;
using HoyoToon.EditorTools.Onboarding;

namespace HoyoToon.EditorTools.ManagerUI
{
    [CustomEditor(typeof(HoyoToonManager))]
    public class HoyoToonManagerEditor : Editor
    {
        private static readonly Dictionary<int, GameObject> s_PendingModelCache = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, HoyoToonManagerModuleNavbar> s_ModuleNavbarCache = new Dictionary<int, HoyoToonManagerModuleNavbar>();
        private static readonly Dictionary<string, bool> s_TourCalloutLayoutCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static bool s_AssemblyReloadHooked;

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
        private DefaultAsset _pendingFolderAsset;
        private bool _includeSubfolders = true;
        private readonly List<GameObject> _queuedBatchAssets = new List<GameObject>();
        private int _addModelPickerControlId = -1;

        private void OnEnable()
        {
            EnsureModuleCacheHooks();
            _bannerHeader = new HoyoToonBannerHeader();
            _moduleNavbar = GetOrCreateNavbarForTarget(target as HoyoToonManager);
            _footer = new HoyoToonManagerFooter(
                activeModelProvider: GetActiveManagedModel,
                prefabFolderResolver: ResolveActiveModelFolder,
                createPrefabAction: CreatePrefabFromActiveModel,
                regenerateMaterialsAction: RegenerateMaterialsForActiveModel);
            CacheSerializedProperties();
            _modelsDirty = true;
            _pendingModelAsset = TryGetCachedPendingModel(target, out var cached) ? cached : null;
            _pendingModelIsReady = false;
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        }

        private void OnDisable()
        {
            CachePendingModel(target, _pendingModelAsset);
            ReleaseNavbarForTarget(target);
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
                if (target == null)
                {
                    EditorGUILayout.HelpBox("No HoyoToon Manager target found.", MessageType.Info);
                    return;
                }

                if (_bannerHeader == null)
                {
                    _bannerHeader = new HoyoToonBannerHeader();
                }

                if (_moduleNavbar == null)
                {
                    _moduleNavbar = GetOrCreateNavbarForTarget(target as HoyoToonManager);
                }

                if (_footer == null)
                {
                    _footer = new HoyoToonManagerFooter(
                        activeModelProvider: GetActiveManagedModel,
                        prefabFolderResolver: ResolveActiveModelFolder,
                        createPrefabAction: CreatePrefabFromActiveModel,
                        regenerateMaterialsAction: RegenerateMaterialsForActiveModel);
                }

                serializedObject.Update();
                HoyoToonGuidedTourController.NotifyManagerSeen(target as HoyoToonManager);
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
                DrawHeaderSection();

                // Push header edits (like active model selection) immediately so downstream UI reads fresh data.
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();

                EditorGUILayout.Space(6f);
                DrawNavbarTourCalloutIfNeeded();
                DrawModuleNavbar();

                EditorGUILayout.Space(8f);
                DrawBodySection();
                EditorGUILayout.Space(6f);
                _footer?.Draw();

                serializedObject.ApplyModifiedProperties();
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception ex)
            {
                serializedObject.ApplyModifiedProperties();
                HoyoToonLogger.Always("ManagerUI", ex.ToString(), LogType.Exception);
                GUIUtility.ExitGUI();
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


        private void DrawHeaderSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSectionHeader("Setup", "Auto setup for FBX assets.");
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    DrawTourCalloutIfNeeded("mainmodule", "addmodel");
                    DrawAddModelRow();
                    DrawTourCalloutIfNeeded("autosetup");
                    DrawBatchInputRow();
                }
                EditorGUILayout.Space(6f);
                DrawSectionHeader("Active Model", "Select the model that modules operate on.");
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    DrawActiveModelRow();
                }
            }
        }

        private static void DrawTourCalloutIfNeeded(params string[] stepIds)
        {
            if (!HoyoToonGuidedTourController.IsActive)
            {
                return;
            }

            if (Event.current == null)
            {
                return;
            }

            var step = HoyoToonGuidedTourController.CurrentStep;
            if (stepIds == null || stepIds.Length == 0)
            {
                return;
            }

            string key = string.Join("|", stepIds);
            bool match = false;
            if (Event.current.type == EventType.Layout)
            {
                foreach (var id in stepIds)
                {
                    if (string.Equals(step.id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        match = true;
                        break;
                    }
                }

                s_TourCalloutLayoutCache[key] = match;
            }
            else if (!s_TourCalloutLayoutCache.TryGetValue(key, out match))
            {
                foreach (var id in stepIds)
                {
                    if (string.Equals(step.id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        match = true;
                        break;
                    }
                }
            }

            if (!match)
            {
                return;
            }

            string body = step.instruction;
            if (step.id == "model")
            {
                body = "Nice work! Your model is now in the scene. Next we will explore the rest of the manager together.";
            }

            HoyoToonTourCallout.Draw(
                $"Guided Tour: {step.title}",
                body,
                null,
                null);
        }

        private static void DrawSectionHeader(string title, string subtitle)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
            }

            if (!string.IsNullOrEmpty(subtitle))
            {
                EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
            }
        }

        private void DrawBodySection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.Space(6f);
                var manager = target as HoyoToonManager;
                if (manager == null)
                {
                    EditorGUILayout.HelpBox("No HoyoToon Manager target found.", MessageType.Info);
                    return;
                }

                var module = _moduleNavbar != null ? _moduleNavbar.GetSelectedModule() : null;
                if (module == null)
                {
                    EditorGUILayout.HelpBox("Selected module is missing.", MessageType.Warning);
                    return;
                }

                if (manager.ActiveModel == null && module is Modules.MainModule)
                {
                    EditorGUILayout.HelpBox("No active model selected. Add a model above or choose one in Active Model.", MessageType.Info);
                    return;
                }

                module.OnGUI(manager);
            }
        }

        private void DrawNavbarTourCalloutIfNeeded()
        {
            if (!HoyoToonGuidedTourController.IsActive)
            {
                return;
            }

            var step = HoyoToonGuidedTourController.CurrentStep;
            if (string.Equals(step.id, "lighting_select", StringComparison.OrdinalIgnoreCase))
            {
                string body = "Nice work! Your model is now in the scene. Click Lighting so we can adjust the key light.";
                HoyoToonTourCallout.Draw(
                    $"Guided Tour: {step.title}",
                    body,
                    null,
                    null);
                return;
            }

            if (!IsNavbarStep(step.id))
            {
                return;
            }

            if (_moduleNavbar == null)
            {
                return;
            }

            var selectedModule = _moduleNavbar.GetSelectedModule();
            if (IsTargetModuleSelected(step.id, selectedModule))
            {
                return;
            }

            HoyoToonTourCallout.Draw(
                $"Guided Tour: {step.title}",
                step.instruction,
                null,
                null);
        }

        private static bool IsNavbarStep(string stepId)
        {
            switch (stepId)
            {
                case "modules":
                case "mainmodule":
                case "lighting_select":
                case "scriptables_select":
                case "postprocessing_select":
                case "renders_select":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsTargetModuleSelected(string stepId, object selectedModule)
        {
            if (selectedModule == null)
            {
                return false;
            }

            switch (stepId)
            {
                case "modules":
                    return selectedModule is Modules.ModelsModule;
                case "mainmodule":
                    return selectedModule is Modules.MainModule;
                case "lighting_select":
                    return selectedModule is Modules.LightingModule;
                case "scriptables_select":
                    return selectedModule is Modules.ScriptablesModule;
                case "postprocessing_select":
                    return selectedModule is Modules.PostProcessingModule;
                case "renders_select":
                    return selectedModule is Modules.RendersModule;
                default:
                    return false;
            }
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
                    HoyoToonScriptablesController.RefreshForManager(manager);
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
            var selectionAssets = GetSupportedAssetsFromSelection();
            bool hasBatchSelection = selectionAssets.Count > 1;
            using (new EditorGUILayout.HorizontalScope())
            {
                var addModelRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                _addModelPickerControlId = GUIUtility.GetControlID(FocusType.Passive, addModelRect);
                HandleTourObjectPickerOpen(addModelRect, _addModelPickerControlId);
                bool pickerUpdated = HandleTourObjectPickerUpdates(_addModelPickerControlId);
                var newPending = (GameObject)EditorGUI.ObjectField(addModelRect, "Add Model", _pendingModelAsset, typeof(GameObject), false);
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.manager.addmodel", addModelRect, "Add FBX");
                if (pickerUpdated)
                {
                    newPending = _pendingModelAsset;
                }
                if (newPending != _pendingModelAsset)
                {
                    _pendingModelAsset = newPending;
                    CachePendingModel(target, _pendingModelAsset);
                }
                _pendingModelIsReady = IsPendingModelReady(_pendingModelAsset) || selectionAssets.Count > 0;
                if (HoyoToonGuidedTourController.IsActive && _pendingModelIsReady)
                {
                    HoyoToonGuidedTourController.NotifyPendingModelSelected();
                }
                if (selectionAssets.Count > 0)
                {
                    EditorGUILayout.LabelField($"{selectionAssets.Count} selected", EditorStyles.miniLabel, GUILayout.Width(90f));
                }
                using (new EditorGUI.DisabledScope(!_pendingModelIsReady))
                {
                    string buttonLabel = ResolveAddModelButtonLabel(_pendingModelAsset, hasBatchSelection);
                    if (GUILayout.Button(buttonLabel, GUILayout.Width(110f)))
                    {
                        if (hasBatchSelection)
                        {
                            RunBatchSetup(selectionAssets);
                            return;
                        }

                        var targetAsset = _pendingModelAsset;
                        if (targetAsset == null && selectionAssets.Count == 1)
                        {
                            targetAsset = selectionAssets[0];
                        }

                        if (TryAddModelFromAsset(targetAsset, out var resolvedAsset))
                        {
                            _pendingModelAsset = resolvedAsset ?? _pendingModelAsset;
                            CachePendingModel(target, _pendingModelAsset);
                        }
                    }
                    var buttonRect = GUILayoutUtility.GetLastRect();
                    HoyoToonTourOverlay.DrawHighlightIfActive("tour.manager.autosetup", buttonRect, "Auto Setup");
                }
            }

            if (hasBatchSelection)
            {
                EditorGUILayout.LabelField("Batch mode uses the Project view selection (FBX only).", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Tip: multi-select FBX assets in the Project view to batch auto setup.", EditorStyles.miniLabel);
            }
        }

        private void HandleTourObjectPickerOpen(Rect fieldRect, int controlId)
        {
            if (!HoyoToonGuidedTourController.IsActive)
            {
                return;
            }

            var step = HoyoToonGuidedTourController.CurrentStep;
            if (!string.Equals(step.id, "addmodel", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 0 || !fieldRect.Contains(evt.mousePosition))
            {
                return;
            }

            var desiredAsset = FindTourFbxAsset();
            EditorGUIUtility.ShowObjectPicker<GameObject>(desiredAsset, false, HoyoToonGuidedTourController.DesiredFbxAssetName, controlId);
            evt.Use();
        }

        private bool HandleTourObjectPickerUpdates(int controlId)
        {
            var evt = Event.current;
            if (evt == null || !string.Equals(evt.commandName, "ObjectSelectorUpdated", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (EditorGUIUtility.GetObjectPickerControlID() != controlId)
            {
                return false;
            }

            var picked = EditorGUIUtility.GetObjectPickerObject() as GameObject;
            if (picked == null)
            {
                return false;
            }

            _pendingModelAsset = picked;
            CachePendingModel(target, _pendingModelAsset);
            Repaint();
            return true;
        }

        private static GameObject FindTourFbxAsset()
        {
            var searchName = HoyoToonGuidedTourController.DesiredFbxAssetName;
            var guids = AssetDatabase.FindAssets($"{searchName} t:GameObject");
            if (guids == null || guids.Length == 0)
            {
                return null;
            }

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private void DrawBatchInputRow()
        {
            EditorGUILayout.Space(4f);
            DrawDragAndDropBox();
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                _pendingFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Folder", _pendingFolderAsset, typeof(DefaultAsset), false);
                _includeSubfolders = EditorGUILayout.ToggleLeft("Include subfolders", _includeSubfolders, GUILayout.Width(140f));
                using (new EditorGUI.DisabledScope(_pendingFolderAsset == null))
                {
                    if (GUILayout.Button("Queue Folder", GUILayout.Width(100f)))
                    {
                        var assets = GetSupportedAssetsFromFolder(_pendingFolderAsset, _includeSubfolders);
                        AddQueuedAssets(assets);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Queued: {_queuedBatchAssets.Count}", EditorStyles.miniLabel, GUILayout.Width(120f));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_queuedBatchAssets.Count == 0))
                {
                    if (GUILayout.Button("Run Queue", GUILayout.Width(90f)))
                    {
                        var assets = new List<GameObject>(_queuedBatchAssets);
                        _queuedBatchAssets.Clear();
                        RunSetupForAssets(assets);
                    }
                }

                if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                {
                    _queuedBatchAssets.Clear();
                }
            }
        }

        private void DrawDragAndDropBox()
        {
            var dropRect = GUILayoutUtility.GetRect(0f, 46f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag FBX assets here to queue", EditorStyles.helpBox);

            var evt = Event.current;
            if (evt == null)
            {
                return;
            }

            if (!dropRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                var draggedAssets = GetSupportedAssetsFromObjects(DragAndDrop.objectReferences);
                if (draggedAssets.Count > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        AddQueuedAssets(draggedAssets);
                    }

                    evt.Use();
                }
            }
        }

        private void RunSetupForAssets(List<GameObject> assets)
        {
            if (assets == null || assets.Count == 0)
            {
                HoyoToonDialogWindow.ShowWarning("Batch Auto Setup", "No valid FBX assets were provided.");
                return;
            }

            if (assets.Count == 1)
            {
                if (TryAddModelFromAsset(assets[0], out _))
                {
                    _modelsDirty = true;
                }

                return;
            }

            RunBatchSetup(assets);
        }

        private void AddQueuedAssets(List<GameObject> assets)
        {
            if (assets == null || assets.Count == 0)
            {
                return;
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in _queuedBatchAssets)
            {
                if (TryGetSupportedAssetPath(asset, out var assetPath))
                {
                    existingPaths.Add(assetPath);
                }
            }

            foreach (var asset in assets)
            {
                if (TryGetSupportedAssetPath(asset, out var assetPath) && existingPaths.Add(assetPath))
                {
                    _queuedBatchAssets.Add(asset);
                }
            }
        }

        private static List<GameObject> GetSupportedAssetsFromObjects(UnityEngine.Object[] objects)
        {
            var results = new List<GameObject>();
            if (objects == null || objects.Length == 0)
            {
                return results;
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in objects)
            {
                var asset = obj as GameObject;
                if (!TryGetSupportedAssetPath(asset, out var assetPath))
                {
                    continue;
                }

                if (existingPaths.Add(assetPath))
                {
                    results.Add(asset);
                }
            }

            return results;
        }

        private static List<GameObject> GetSupportedAssetsFromFolder(DefaultAsset folderAsset, bool includeSubfolders)
        {
            var results = new List<GameObject>();
            if (folderAsset == null)
            {
                return results;
            }

            var folderPath = AssetDatabase.GetAssetPath(folderAsset);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return results;
            }

            if (!includeSubfolders)
            {
                var immediateAssets = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
                return FilterFolderAssets(immediateAssets, folderPath, true);
            }

            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
            if (guids == null || guids.Length == 0)
            {
                return results;
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null)
                {
                    continue;
                }

                if (existingPaths.Add(assetPath))
                {
                    results.Add(asset);
                }
            }

            return results;
        }

        private static List<GameObject> FilterFolderAssets(string[] guids, string folderPath, bool requireImmediateChild)
        {
            var results = new List<GameObject>();
            if (guids == null || guids.Length == 0)
            {
                return results;
            }

            var normalizedFolder = folderPath.Replace('\\', '/');
            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                var normalizedAsset = assetPath.Replace('\\', '/');
                if (requireImmediateChild)
                {
                    var parent = Path.GetDirectoryName(normalizedAsset)?.Replace('\\', '/');
                    if (!string.Equals(parent, normalizedFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                if (!normalizedAsset.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null)
                {
                    continue;
                }

                if (existingPaths.Add(assetPath))
                {
                    results.Add(asset);
                }
            }

            return results;
        }

        private static List<GameObject> GetSupportedAssetsFromSelection()
        {
            var results = new List<GameObject>();
            var selection = Selection.objects;
            if (selection == null || selection.Length == 0)
            {
                return results;
            }

            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in selection)
            {
                var asset = obj as GameObject;
                if (!TryGetSupportedAssetPath(asset, out var assetPath))
                {
                    continue;
                }

                if (existingPaths.Add(assetPath))
                {
                    results.Add(asset);
                }
            }

            return results;
        }

        private void RunBatchSetup(List<GameObject> assets)
        {
            var manager = (HoyoToonManager)target;
            if (manager == null)
            {
                HoyoToonDialogWindow.ShowError("Manager Missing", "Cannot setup without an active HoyoToon Manager in the scene.");
                return;
            }

            if (assets == null || assets.Count == 0)
            {
                HoyoToonDialogWindow.ShowWarning("Batch Auto Setup", "No valid FBX assets were provided.");
                return;
            }

            int successCount = 0;
            int failCount = 0;
            GameObject lastInstance = null;

            foreach (var asset in assets)
            {
                if (!TryGetSupportedAssetPath(asset, out var assetPath))
                {
                    failCount++;
                    continue;
                }

                bool success = HoyoToonModelSetupUtility.TryProcessFbxAndInstantiate(manager, asset, out var instance);

                if (success)
                {
                    successCount++;
                    if (instance != null)
                    {
                        AddManagedModelInstance(instance, false);
                        lastInstance = instance;
                    }
                }
                else
                {
                    failCount++;
                }
            }

            if (lastInstance != null)
            {
                AddManagedModelInstance(lastInstance, true);
            }

            _modelsDirty = true;

            if (failCount > 0)
            {
                HoyoToonDialogWindow.ShowWarning("Batch Auto Setup Complete", $"Completed with {successCount} success(es) and {failCount} failure(s). Check the console for details.");
            }
            else
            {
                HoyoToonDialogWindow.ShowInfo("Batch Auto Setup Complete", $"Successfully set up {successCount} model(s).");
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
                HoyoToonDialogWindow.ShowError("Unsupported Asset", "Please select an FBX asset.");
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

            HoyoToonDialogWindow.ShowError("Unsupported Asset", "Only FBX assets are supported right now.");
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
        
        private static string ResolveAddModelButtonLabel(GameObject pendingAsset, bool isBatchSelection)
        {
            if (isBatchSelection)
            {
                return "Auto Setup (Batch)";
            }

            return "Auto Setup";
        }

        private static bool IsPendingModelReady(GameObject pendingAsset)
        {
            if (pendingAsset == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(pendingAsset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

                 return assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetSupportedAssetPath(GameObject asset, out string assetPath)
        {
            assetPath = null;
            if (asset == null)
            {
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

                 return assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
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

            if (TryGetActiveModelFolder(model, out var folder))
            {
                return folder;
            }

            if (IsTourPrefabStep())
            {
                return EnsureTourPrefabFolder();
            }

            return null;
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

        private static bool IsTourPrefabStep()
        {
            if (!HoyoToonGuidedTourController.IsActive)
            {
                return false;
            }

            string stepId = HoyoToonGuidedTourController.CurrentStep.id;
            return string.Equals(stepId, "footer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stepId, "prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTourPrefabFolder()
        {
            const string root = "Assets/HoyoToon";
            const string prefabs = "Assets/HoyoToon/Prefabs";

            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets", "HoyoToon");
            }

            if (!AssetDatabase.IsValidFolder(prefabs))
            {
                AssetDatabase.CreateFolder(root, "Prefabs");
            }

            return prefabs;
        }

        private void CreatePrefabFromActiveModel(GameObject activeModel)
        {
            if (activeModel == null)
            {
                return;
            }

            if (!TryGetActiveModelFolder(activeModel, out var folder))
            {
                if (IsTourPrefabStep())
                {
                    folder = EnsureTourPrefabFolder();
                }

                if (string.IsNullOrEmpty(folder))
                {
                    HoyoToonDialogWindow.ShowError("Cannot Create Prefab", "Unable to locate the original asset folder for this model.");
                    return;
                }
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
                if (HoyoToonGuidedTourController.IsActive)
                {
                    string stepId = HoyoToonGuidedTourController.CurrentStep.id;
                    if (string.Equals(stepId, "footer", StringComparison.OrdinalIgnoreCase))
                    {
                        HoyoToonGuidedTourController.CompleteFooterStep();
                    }

                    HoyoToonGuidedTourController.NotifyPrefabCreated();
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ManagerError($"Failed to create prefab for {activeModel.name}: {ex.Message}");
                HoyoToonDialogWindow.ShowError("Prefab Creation Failed", $"Could not create prefab: {ex.Message}");
            }
        }

        private void RegenerateMaterialsForActiveModel(GameObject activeModel)
        {
            if (activeModel == null)
            {
                HoyoToonDialogWindow.ShowWarning("Regenerate Materials", "No active model selected.");
                return;
            }

            MaterialGeneration.GenerateAuto(activeModel, null, null, null, true);
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

        private static void EnsureModuleCacheHooks()
        {
            if (s_AssemblyReloadHooked)
            {
                return;
            }

            s_AssemblyReloadHooked = true;
            AssemblyReloadEvents.beforeAssemblyReload += ClearModuleNavbarCache;
        }

        private static void ClearModuleNavbarCache()
        {
            foreach (var navbar in s_ModuleNavbarCache.Values)
            {
                navbar?.Dispose();
            }

            s_ModuleNavbarCache.Clear();
        }

        private static HoyoToonManagerModuleNavbar GetOrCreateNavbarForTarget(HoyoToonManager manager)
        {
            if (manager == null)
            {
                var fallback = new HoyoToonManagerModuleNavbar();
                RegisterDefaultModules(fallback);
                return fallback;
            }

            int key = manager.GetInstanceID();
            if (s_ModuleNavbarCache.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            var navbar = new HoyoToonManagerModuleNavbar();
            RegisterDefaultModules(navbar);
            s_ModuleNavbarCache[key] = navbar;
            return navbar;
        }

        private static void RegisterDefaultModules(HoyoToonManagerModuleNavbar navbar)
        {
            if (navbar == null)
            {
                return;
            }

            navbar.RegisterModule(new Modules.MainModule());
            navbar.RegisterModule(new Modules.ModelsModule());
            navbar.RegisterModule(new Modules.LightingModule());
            navbar.RegisterModule(new Modules.ScriptablesModule());
            navbar.RegisterModule(new Modules.PostProcessingModule());
            navbar.RegisterModule(new Modules.RendersModule());
        }

        private static void ReleaseNavbarForTarget(UnityEngine.Object targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            int key = targetObject.GetInstanceID();
            if (targetObject is HoyoToonManager manager && manager)
            {
                // Keep cached to preserve module state across inspector focus changes.
                return;
            }

            if (s_ModuleNavbarCache.TryGetValue(key, out var existing))
            {
                existing?.Dispose();
                s_ModuleNavbarCache.Remove(key);
            }
        }
    }
}
#endif
