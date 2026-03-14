#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.UI.ManagerInspector.Components;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.Onboarding;
using StepIds = HoyoToon.Editor.Onboarding.GuidedTourController.StepIds;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.UI.ManagerInspector
{
    [CustomEditor(typeof(HoyoToonManager))]
    public class ManagerEditor : UnityEditor.Editor
    {
        private static readonly Dictionary<int, GameObject> s_PendingModelCache = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, ManagerNavbar> s_ModuleNavbarCache = new Dictionary<int, ManagerNavbar>();
        private static readonly Dictionary<string, bool> s_TourCalloutLayoutCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static bool s_AssemblyReloadHooked;

        private ManagerHeader _managerHeader;
        private ManagerNavbar _moduleNavbar;
        private ManagerFooter _footer;

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
            _managerHeader = new ManagerHeader();
            _moduleNavbar = GetOrCreateNavbarForTarget(target as HoyoToonManager);
            _footer = new ManagerFooter(actions: new ManagerFooter.ActionContext(
                activeModelProvider: GetActiveManagedModel,
                prefabFolderResolver: ResolveActiveModelFolder,
                createPrefabAction: CreatePrefabFromActiveModel,
                regenerateMaterialsAction: RegenerateMaterialsForActiveModel));
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
            _managerHeader = null;
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

                if (_managerHeader == null)
                {
                    _managerHeader = new ManagerHeader();
                }

                if (_moduleNavbar == null)
                {
                    _moduleNavbar = GetOrCreateNavbarForTarget(target as HoyoToonManager);
                }

                if (_footer == null)
                {
                    _footer = new ManagerFooter(actions: new ManagerFooter.ActionContext(
                        activeModelProvider: GetActiveManagedModel,
                        prefabFolderResolver: ResolveActiveModelFolder,
                        createPrefabAction: CreatePrefabFromActiveModel,
                        regenerateMaterialsAction: RegenerateMaterialsForActiveModel));
                }

                serializedObject.Update();
                GuidedTourController.NotifyManagerSeen(target as HoyoToonManager);
                if (_managedModelsProperty == null || _activeModelIndexProperty == null)
                {
                    CacheSerializedProperties();
                }

                _managerHeader.Draw();
                EditorGUILayout.Space(ManagerUILayout.SpacingSection);

                if (_modelsDirty)
                {
                    _modelsDirty = false;
                    AutoDiscoverSceneModels((HoyoToonManager)target);
                }
                DrawHeaderSection();

                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();

                EditorGUILayout.Space(ManagerUILayout.SpacingMedium);
                DrawNavbarTourCalloutIfNeeded();
                DrawModuleNavbar();

                EditorGUILayout.Space(ManagerUILayout.SpacingSection);
                DrawBodySection();
                EditorGUILayout.Space(ManagerUILayout.SpacingMedium);
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
                    DrawTourCalloutIfNeeded(StepIds.AddModel);
                    DrawAddModelRow();
                    DrawTourCalloutIfNeeded(StepIds.AutoSetup);
                    DrawBatchInputRow();
                }
                EditorGUILayout.Space(ManagerUILayout.SpacingMedium);
                DrawSectionHeader("Active Model", "Select the model that modules operate on.");
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                {
                    DrawActiveModelRow();
                }
            }
        }

        private static void DrawTourCalloutIfNeeded(params string[] stepIds)
        {
            if (!GuidedTourController.IsActive)
            {
                return;
            }

            if (Event.current == null)
            {
                return;
            }

            var step = GuidedTourController.CurrentStep;
            if (stepIds == null || stepIds.Length == 0)
            {
                return;
            }

            bool match = GetTourCalloutMatch(step.id, stepIds);

            if (!match)
            {
                return;
            }

            string body = step.instruction;
            if (step.id == StepIds.Model)
            {
                body = "Nice work! Your model is now in the scene. Next we will explore the rest of the manager together.";
            }

            TourCallout.Draw(
                $"Guided Tour: {step.title}",
                body,
                null,
                null);
        }

        private static bool GetTourCalloutMatch(string currentStepId, string[] stepIds)
        {
            string key = string.Join("|", stepIds);
            if (Event.current.type == EventType.Layout)
            {
                bool layoutMatch = IsMatchingTourStep(currentStepId, stepIds);
                s_TourCalloutLayoutCache[key] = layoutMatch;
                return layoutMatch;
            }

            if (s_TourCalloutLayoutCache.TryGetValue(key, out var cachedMatch))
            {
                return cachedMatch;
            }

            return IsMatchingTourStep(currentStepId, stepIds);
        }

        private static bool IsMatchingTourStep(string currentStepId, string[] stepIds)
        {
            foreach (var id in stepIds)
            {
                if (string.Equals(currentStepId, id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
                EditorGUILayout.Space(ManagerUILayout.SpacingMedium);
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
            if (!GuidedTourController.IsActive)
            {
                return;
            }

            var step = GuidedTourController.CurrentStep;
            if (string.Equals(step.id, StepIds.LightingSelect, StringComparison.OrdinalIgnoreCase))
            {
                string body = "Nice work! Your model is now in the scene. Click Lighting so we can adjust the key light.";
                TourCallout.Draw(
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

            TourCallout.Draw(
                $"Guided Tour: {step.title}",
                step.instruction,
                null,
                null);
        }

        private static bool IsNavbarStep(string stepId)
        {
            switch (stepId)
            {
                case StepIds.Modules:
                case StepIds.MainModule:
                case StepIds.LightingSelect:
                case StepIds.ScriptablesSelect:
                case StepIds.RendersSelect:
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
                case StepIds.Modules:
                    return selectedModule is Modules.ModelsModule;
                case StepIds.MainModule:
                    return selectedModule is Modules.MainModule;
                case StepIds.LightingSelect:
                    return selectedModule is Modules.LightingModule;
                case StepIds.ScriptablesSelect:
                    return selectedModule is Modules.SceneModule;
                case StepIds.RendersSelect:
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
            var selectionAssets = ManagerModelDiscoveryService.GetSupportedAssetsFromSelection();
            bool hasBatchSelection = selectionAssets.Count > 1;
            using (new EditorGUILayout.HorizontalScope())
            {
                var addModelRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                _addModelPickerControlId = GUIUtility.GetControlID(FocusType.Passive, addModelRect);
                HandleTourObjectPickerOpen(addModelRect, _addModelPickerControlId);
                bool pickerUpdated = HandleTourObjectPickerUpdates(_addModelPickerControlId);
                var newPending = (GameObject)EditorGUI.ObjectField(addModelRect, "Add Model", _pendingModelAsset, typeof(GameObject), false);
                TourOverlay.DrawHighlightIfActive("tour.manager.addmodel", addModelRect, "Add FBX");
                if (pickerUpdated)
                {
                    newPending = _pendingModelAsset;
                }
                if (newPending != _pendingModelAsset)
                {
                    _pendingModelAsset = newPending;
                    CachePendingModel(target, _pendingModelAsset);
                }
                _pendingModelIsReady = ManagerModelDiscoveryService.IsPendingModelReady(_pendingModelAsset) || selectionAssets.Count > 0;
                if (GuidedTourController.IsActive && _pendingModelIsReady)
                {
                    GuidedTourController.NotifyPendingModelSelected();
                }
                if (selectionAssets.Count > 0)
                {
                    EditorGUILayout.LabelField($"{selectionAssets.Count} selected", EditorStyles.miniLabel, GUILayout.Width(90f));
                }
                using (new EditorGUI.DisabledScope(!_pendingModelIsReady))
                {
                    string buttonLabel = ResolveAddModelButtonLabel(hasBatchSelection);
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
                    TourOverlay.DrawHighlightIfActive("tour.manager.autosetup", buttonRect, "Auto Setup");
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
            if (!GuidedTourController.IsActive)
            {
                return;
            }

            var step = GuidedTourController.CurrentStep;
            if (!string.Equals(step.id, StepIds.AddModel, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 0 || !fieldRect.Contains(evt.mousePosition))
            {
                return;
            }

            var desiredAsset = FindTourFbxAsset();
            EditorGUIUtility.ShowObjectPicker<GameObject>(desiredAsset, false, GuidedTourController.DesiredFbxAssetName, controlId);
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
            var searchName = GuidedTourController.DesiredFbxAssetName;
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
            EditorGUILayout.Space(ManagerUILayout.SpacingSmall);
            DrawDragAndDropBox();
            EditorGUILayout.Space(ManagerUILayout.SpacingSmall);

            using (new EditorGUILayout.HorizontalScope())
            {
                _pendingFolderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Folder", _pendingFolderAsset, typeof(DefaultAsset), false);
                _includeSubfolders = EditorGUILayout.ToggleLeft("Include subfolders", _includeSubfolders, GUILayout.Width(140f));
                using (new EditorGUI.DisabledScope(_pendingFolderAsset == null))
                {
                    if (GUILayout.Button("Queue Folder", GUILayout.Width(100f)))
                    {
                        var assets = ManagerModelDiscoveryService.GetSupportedAssetsFromFolder(_pendingFolderAsset, _includeSubfolders);
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
                var draggedAssets = ManagerModelDiscoveryService.GetSupportedAssetsFromObjects(DragAndDrop.objectReferences);
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
            ManagerBatchSetupController.RunSetupForAssets(
                target as HoyoToonManager,
                assets,
                AddManagedModelInstance,
                MarkModelsDirty);
        }

        private void AddQueuedAssets(List<GameObject> assets)
        {
            ManagerBatchSetupController.AddQueuedAssets(_queuedBatchAssets, assets);
        }

        private void RunBatchSetup(List<GameObject> assets)
        {
            ManagerBatchSetupController.RunBatchSetup(
                target as HoyoToonManager,
                assets,
                AddManagedModelInstance,
                MarkModelsDirty);
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
            if (!ModelSetupUtility.TryPrepareFbxRequest(manager, modelAsset, null, ModelSetupUtility.SetupRequestSource.AddModel, out var preparedRequest))
            {
                return false;
            }

            if (ModelSetupUtility.TryRunPreparedRequest(preparedRequest, out var instance, out var resolved))
            {
                AddManagedModelInstance(instance);
                resolvedAsset = resolved ?? modelAsset;
                _modelsDirty = true;
                return true;
            }

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
            if (_managedModelsProperty == null || manager == null)
            {
                return;
            }

            ManagerModelDiscoveryService.AutoDiscoverSceneModels(
                manager,
                _managedModelsProperty,
                _activeModelIndexProperty,
                AddManagedModelInstance,
                RemoveManagedModelAt);
        }

        private void HandleHierarchyChanged()
        {
            MarkModelsDirty();
        }

        private void MarkModelsDirty()
        {
            _modelsDirty = true;
        }
        
        private static string ResolveAddModelButtonLabel(bool isBatchSelection)
        {
            if (isBatchSelection)
            {
                return "Auto Setup (Batch)";
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

            if (ManagerAssetActionHelper.TryGetActiveModelFolder(model, out var folder))
            {
                return folder;
            }

            if (ManagerAssetActionHelper.IsTourPrefabStep())
            {
                return ManagerAssetActionHelper.EnsureTourPrefabFolder();
            }

            return null;
        }

        private void CreatePrefabFromActiveModel(GameObject activeModel)
        {
            if (activeModel == null)
            {
                return;
            }

            if (!ManagerAssetActionHelper.TryCreatePrefabFromActiveModel(
                activeModel,
                ResolveActiveModelFolder,
                out var prefab,
                out var savePath,
                out var errorTitle,
                out var errorMessage))
            {
                if (!string.IsNullOrEmpty(errorTitle) && !string.IsNullOrEmpty(errorMessage))
                {
                    DialogWindow.ShowError(errorTitle, errorMessage);
                }

                return;
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            DialogWindow.ShowInfo("Prefab Created", $"Saved prefab to:\n{savePath}");
            if (GuidedTourController.IsActive)
            {
                string stepId = GuidedTourController.CurrentStep.id;
                if (string.Equals(stepId, StepIds.Footer, StringComparison.OrdinalIgnoreCase))
                {
                    GuidedTourController.CompleteFooterStep();
                }

                GuidedTourController.NotifyPrefabCreated();
            }
        }

        private void RegenerateMaterialsForActiveModel(GameObject activeModel)
        {
            if (!ManagerAssetActionHelper.TryRegenerateMaterialsForActiveModel(activeModel, out var warningOrError))
            {
                DialogWindow.ShowWarning("Regenerate Materials", warningOrError);
            }
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

        private static ManagerNavbar GetOrCreateNavbarForTarget(HoyoToonManager manager)
        {
            if (manager == null)
            {
                var fallback = new ManagerNavbar();
                RegisterDefaultModules(fallback);
                return fallback;
            }

            int key = manager.GetInstanceID();
            if (s_ModuleNavbarCache.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            var navbar = new ManagerNavbar();
            RegisterDefaultModules(navbar);
            s_ModuleNavbarCache[key] = navbar;
            return navbar;
        }

        private static void RegisterDefaultModules(ManagerNavbar navbar)
        {
            if (navbar == null)
            {
                return;
            }

            navbar.RegisterModule(new Modules.MainModule());
            navbar.RegisterModule(new Modules.ModelsModule());
            navbar.RegisterModule(new Modules.CharacterModule());
            navbar.RegisterModule(new Modules.SceneModule());
            navbar.RegisterModule(new Modules.LightingModule());
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
