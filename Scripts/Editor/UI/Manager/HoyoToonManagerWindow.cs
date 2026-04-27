using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoyoToon.Editor.AssetPipeline.Materials;
using HoyoToon.Editor.AssetPipeline.Models;
using HoyoToon.Editor.Detection.Character;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Editor.Setup;
using HoyoToon.Editor.UI.Manager.Modules;
using HoyoToon.Editor.Updater;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Runtime.ScriptableObjects.Games;
using HoyoToon.Runtime.Scene.Placement;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HoyoToon.Editor.UI.Manager
{
    public sealed class HoyoToonManagerWindow : EditorWindow
    {
        private const string MenuPath = "HoyoToon/Manager";
        private const string PackageAssetRoot = "Packages/com.hoyotoon.hoyotoon/Scripts/Editor/UI/Manager";
        private const string UxmlAssetPath = PackageAssetRoot + "/UXML/HoyoToonWindow.uxml";
        private const string ThemeAssetPath = PackageAssetRoot + "/USS/HoyoToonTheme.uss";
        private const string LayoutAssetPath = PackageAssetRoot + "/USS/HoyoToonLayout.uss";
        private const string ComponentsAssetPath = PackageAssetRoot + "/USS/HoyoToonComponents.uss";
        private const string HeaderBackgroundAssetPath = "Packages/com.hoyotoon.hoyotoon/Resources/UI/background.png";
        private const string HeaderLogoAssetPath = "Packages/com.hoyotoon.hoyotoon/Resources/UI/hoyotoon.png";
        private const string ConvertedAssetLabel = "HoyoToonConverted";
        private const string VersionUnavailableLabel = "Version unavailable";
        private const string WidthNarrowClass = "ht-width-narrow";
        private const string WidthStandardClass = "ht-width-standard";
        private const string WidthWideClass = "ht-width-wide";
        private const string VersionBadgeUnknownClass = "ht-version-badge--unknown";
        private const string VersionBadgeCheckingClass = "ht-version-badge--checking";
        private const string VersionBadgeUpToDateClass = "ht-version-badge--up-to-date";
        private const string VersionBadgeUpdateAvailableClass = "ht-version-badge--update-available";
        private const string VersionBadgeLocalAheadClass = "ht-version-badge--local-ahead";
        private const string VersionBadgeApplyingClass = "ht-version-badge--applying";
        private const string VersionBadgeErrorClass = "ht-version-badge--error";

        private static readonly Vector2 MinimumWindowSize = new Vector2(360f, 520f);

        private readonly Dictionary<string, Button> tabButtonsById = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly List<InspectorAction> footerActions = new List<InspectorAction>();

        private IReadOnlyList<IManagerModule> modules = Array.Empty<IManagerModule>();
        private IManagerModule activeModule;
        private ModuleContext currentContext;
        private GameObject selectedModelAsset;
        private DefaultAsset selectedModelFolder;
        private string selectedModelAssetPath = string.Empty;
        private string selectedModelFolderPath = string.Empty;
        private string validationMessage = string.Empty;
        private string detectedModelName = string.Empty;
        private string detectedCharacterName = string.Empty;
        private string detectedGameKey = string.Empty;
        private string matchedJsonAssetPath = string.Empty;
        private string detectionSummaryMessage = "Slot an FBX model to inspect detection results and run Auto Setup.";
        private int detectedJsonCount;
        private int detectedMaterialCount;
        private string packageVersionLabel = VersionUnavailableLabel;
        private readonly List<GameObject> batchModelAssets = new List<GameObject>();
        private readonly List<BatchModelDetectionInfo> batchDetectionResults = new List<BatchModelDetectionInfo>();

        private VisualElement shellRoot;
        private VisualElement moduleContentRoot;
        private Image bannerBackground;
        private Button versionBadgeButton;
        private Image logoImage;
        private VisualElement globalContextHost;
        private Button createPrefabButton;
        private Button regenerateMaterialsButton;
        private Button regenerateTangentsButton;
        private IVisualElementScheduledItem versionBadgeSchedule;

        internal string ActiveModuleId => activeModule != null ? activeModule.Id : string.Empty;
        internal ModuleContext CurrentContext => currentContext;

        [MenuItem(MenuPath, false, 20)]
        private static void OpenWindow()
        {
            OnboardingSignals.RecordAction("MainMenu.OpenManager");
            HoyoToonManagerWindow window = GetWindow<HoyoToonManagerWindow>();
            window.titleContent = new GUIContent("HoyoToon");
            window.minSize = MinimumWindowSize;
            window.Show();
        }

        internal static HoyoToonManagerWindow ShowWindowForOnboarding()
        {
            HoyoToonManagerWindow window = GetWindow<HoyoToonManagerWindow>();
            window.titleContent = new GUIContent("HoyoToon");
            window.minSize = MinimumWindowSize;
            window.Show();
            window.Focus();
            return window;
        }

        internal static bool TryGetOpenWindow(out HoyoToonManagerWindow window)
        {
            window = UnityEngine.Resources.FindObjectsOfTypeAll<HoyoToonManagerWindow>()
                .FirstOrDefault(candidate => candidate != null);
            return window != null;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("HoyoToon");
            minSize = MinimumWindowSize;
            packageVersionLabel = ResolvePackageVersionLabel();
        }

        private void OnDisable()
        {
            versionBadgeSchedule?.Pause();
            activeModule?.OnDeselected(currentContext);
            OnboardingManager.NotifyManagerClosed(this);
        }

        private void OnFocus()
        {
            RefreshManualContext();
            UpdateVersionBadge();
        }

        public void CreateGUI()
        {
            string activeModuleId = activeModule != null ? activeModule.Id : string.Empty;
            modules = ModuleRegistry.CreateModules();
            activeModule = modules.FirstOrDefault(module => string.Equals(module.Id, activeModuleId, StringComparison.Ordinal))
                ?? modules.FirstOrDefault();

            packageVersionLabel = ResolvePackageVersionLabel();
            currentContext = BuildModuleContext();

            rootVisualElement.Clear();
            tabButtonsById.Clear();
            footerActions.Clear();

            BuildShell();
            if (shellRoot == null)
            {
                return;
            }

            BuildTabs();
            BuildFooterActions();
            RefreshGlobalContext();
            UpdateVersionBadge();
            StartVersionBadgeSchedule();
            ApplyWidthClass(position.width);
            SetActiveModule(activeModule != null ? activeModule.Id : string.Empty, true);
            RegisterOnboardingTargets();
            OnboardingManager.AttachToManagerWindow(this, rootVisualElement);
        }

        private void BuildShell()
        {
            VisualTreeAsset tree = LoadAsset<VisualTreeAsset>(UxmlAssetPath);
            StyleSheet theme = LoadAsset<StyleSheet>(ThemeAssetPath);
            StyleSheet layout = LoadAsset<StyleSheet>(LayoutAssetPath);
            StyleSheet components = LoadAsset<StyleSheet>(ComponentsAssetPath);

            if (tree == null || theme == null || layout == null || components == null)
            {
                BuildErrorState(
                    "The HoyoToon Manager assets could not be loaded. " +
                    "Make sure the UXML and USS files exist under Scripts/Editor/UI/Manager.");
                return;
            }

            shellRoot = tree.CloneTree();
            shellRoot.styleSheets.Add(theme);
            shellRoot.styleSheets.Add(layout);
            shellRoot.styleSheets.Add(components);
            shellRoot.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);

            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(shellRoot);
            shellRoot.StretchToParentSize();
            shellRoot.style.flexGrow = 1f;

            ScrollView tabScrollView = shellRoot.Q<ScrollView>("TabScrollView");
            if (tabScrollView != null)
            {
                tabScrollView.mode = ScrollViewMode.Vertical;
                tabScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                tabScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            }

            ScrollView bodyScrollView = shellRoot.Q<ScrollView>("BodyScrollView");
            if (bodyScrollView != null)
            {
                bodyScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                bodyScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }

            moduleContentRoot = shellRoot.Q<VisualElement>("ModuleContentRoot");
            bannerBackground = shellRoot.Q<Image>("BannerBackground");
            versionBadgeButton = shellRoot.Q<Button>("VersionBadge");
            logoImage = shellRoot.Q<Image>("LogoImage");
            globalContextHost = shellRoot.Q<VisualElement>("GlobalContextHost");
            createPrefabButton = shellRoot.Q<Button>("CreatePrefabButton");
            regenerateMaterialsButton = shellRoot.Q<Button>("RegenerateMaterialsButton");
            regenerateTangentsButton = shellRoot.Q<Button>("RegenerateTangentsButton");

            if (versionBadgeButton != null)
            {
                versionBadgeButton.clicked += HandleVersionBadgeClicked;
            }

            ApplyHeaderArtwork();
        }

        private void BuildErrorState(string message)
        {
            shellRoot = null;

            VisualElement fallbackRoot = new VisualElement();
            fallbackRoot.style.flexGrow = 1f;
            fallbackRoot.style.paddingLeft = 12f;
            fallbackRoot.style.paddingRight = 12f;
            fallbackRoot.style.paddingTop = 12f;
            fallbackRoot.style.paddingBottom = 12f;

            HelpBox helpBox = new HelpBox(message, HelpBoxMessageType.Error);
            fallbackRoot.Add(helpBox);
            rootVisualElement.Add(fallbackRoot);
        }

        private void BuildTabs()
        {
            VisualElement tabRow = shellRoot.Q<VisualElement>("TabRow");
            if (tabRow == null)
            {
                return;
            }

            tabRow.Clear();

            foreach (IManagerModule module in modules.OrderBy(module => module.Order))
            {
                IManagerModule capturedModule = module;
                string targetId = GetOnboardingTabTargetId(capturedModule.Id);
                Button tabButton = new Button(() =>
                {
                    OnboardingSignals.RecordAction(targetId);
                    SetActiveModule(capturedModule.Id, false);
                })
                {
                    name = capturedModule.Id + "Tab",
                    text = capturedModule.DisplayName,
                    tooltip = capturedModule.DisplayName + " module"
                };

                tabButton.AddToClassList("ht-tab");
                tabRow.Add(tabButton);
                tabButtonsById[capturedModule.Id] = tabButton;
                RegisterTabOnboardingTarget(capturedModule, tabButton);
            }

            RefreshTabVisualState();
        }

        private void BuildFooterActions()
        {
            footerActions.Add(new InspectorAction("Create Prefab", HandleCreatePrefab, false, "primary"));
            footerActions.Add(new InspectorAction("Regenerate Materials", HandleRegenerateMaterials, false, "secondary"));
            footerActions.Add(new InspectorAction("Regenerate Tangents", HandleRegenerateTangents, false, "secondary"));

            if (footerActions.Count > 0)
            {
                BindFooterButton(createPrefabButton, footerActions[0]);
            }

            if (footerActions.Count > 1)
            {
                BindFooterButton(regenerateMaterialsButton, footerActions[1]);
            }

            if (footerActions.Count > 2)
            {
                BindFooterButton(regenerateTangentsButton, footerActions[2]);
            }

            RefreshFooterActionStates();
        }

        private static void BindFooterButton(Button button, InspectorAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.text = action.Label;
            button.clicked += () => action.Callback?.Invoke();
            button.AddToClassList("ht-footer-button");
            button.AddToClassList(action.StyleKind == "primary" ? "ht-btn-primary" : "ht-btn-secondary");
        }

        private void SetActiveModule(string moduleId, bool forceRefresh)
        {
            IManagerModule nextModule = modules.FirstOrDefault(module => string.Equals(module.Id, moduleId, StringComparison.Ordinal));
            if (nextModule == null)
            {
                return;
            }

            if (!forceRefresh && ReferenceEquals(activeModule, nextModule))
            {
                RefreshBody();
                return;
            }

            activeModule?.OnDeselected(currentContext);
            activeModule = nextModule;
            OnboardingSignals.RecordValueChanged("Manager.ActiveModule", nextModule.Id);
            RefreshTabVisualState();
            RefreshBody();
            activeModule.OnSelected(currentContext);
        }

        private void RefreshBody()
        {
            if (moduleContentRoot == null)
            {
                return;
            }

            moduleContentRoot.Clear();

            VisualElement content = activeModule?.CreateContent(currentContext) ?? CreateEmptyState();

            if (content != null)
            {
                moduleContentRoot.Add(content);
            }

            Repaint();
        }

        private void RefreshTabVisualState()
        {
            string activeModuleId = activeModule != null ? activeModule.Id : string.Empty;

            foreach (KeyValuePair<string, Button> tabEntry in tabButtonsById)
            {
                string moduleId = tabEntry.Key;
                Button button = tabEntry.Value;

                if (button == null)
                {
                    continue;
                }

                if (string.Equals(moduleId, activeModuleId, StringComparison.Ordinal))
                {
                    button.AddToClassList("ht-tab--active");
                }
                else
                {
                    button.RemoveFromClassList("ht-tab--active");
                }
            }
        }

        internal void SetSelectedModelAsset(GameObject modelAsset)
        {
            if (!TryValidateSelectedModel(modelAsset, out string assetPath, out string newValidationMessage))
            {
                ApplySelectedModels(Array.Empty<GameObject>(), null, string.Empty, newValidationMessage);
                return;
            }

            if (modelAsset == null)
            {
                return;
            }

            List<GameObject> updatedQueue = new List<GameObject>(batchModelAssets)
            {
                modelAsset
            };
            ApplySelectedModels(updatedQueue, selectedModelFolder, selectedModelFolderPath, string.Empty);
        }

        internal void SetSingleSelectedModelAssetForOnboarding(GameObject modelAsset)
        {
            if (!TryValidateSelectedModel(modelAsset, out _, out string newValidationMessage))
            {
                ApplySelectedModels(Array.Empty<GameObject>(), null, string.Empty, newValidationMessage);
                return;
            }

            if (modelAsset == null)
            {
                return;
            }

            ApplySelectedModels(new[] { modelAsset }, null, string.Empty, string.Empty);
        }

        internal void UseProjectSelectionForModelBatch()
        {
            List<GameObject> selectedModels = CollectFbxModelAssets(Selection.objects);
            if (selectedModels.Count <= 0)
            {
                ApplySelectedModels(Array.Empty<GameObject>(), null, string.Empty, "Select one or more FBX model assets in the Project window.");
                return;
            }

            ApplySelectedModels(selectedModels, null, string.Empty, string.Empty);
        }

        internal void SetSelectedModelFolder(DefaultAsset folderAsset)
        {
            if (folderAsset == null)
            {
                ApplySelectedModels(Array.Empty<GameObject>(), null, string.Empty, string.Empty);
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(folderAsset);
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                ApplySelectedModels(Array.Empty<GameObject>(), null, string.Empty, "Folder only supports Project folders.");
                return;
            }

            List<GameObject> folderModels = FindFbxModelAssetsInFolder(folderPath);
            if (folderModels.Count <= 0)
            {
                ApplySelectedModels(Array.Empty<GameObject>(), folderAsset, folderPath, "No FBX model assets were found in the selected folder.");
                return;
            }

            ApplySelectedModels(folderModels, folderAsset, folderPath, string.Empty);
        }

        internal void RemoveQueuedModelAsset(GameObject modelAsset)
        {
            if (modelAsset == null || batchModelAssets.Count <= 0)
            {
                return;
            }

            string targetPath = AssetDatabase.GetAssetPath(modelAsset);
            int removedCount = batchModelAssets.RemoveAll(candidate =>
            {
                if (ReferenceEquals(candidate, modelAsset))
                {
                    return true;
                }

                string candidatePath = AssetDatabase.GetAssetPath(candidate);
                return !string.IsNullOrWhiteSpace(targetPath)
                    && string.Equals(candidatePath, targetPath, StringComparison.OrdinalIgnoreCase);
            });

            if (removedCount <= 0)
            {
                return;
            }

            if (ReferenceEquals(selectedModelAsset, modelAsset)
                || batchModelAssets.All(candidate => !ReferenceEquals(candidate, selectedModelAsset)))
            {
                selectedModelAsset = batchModelAssets.FirstOrDefault();
                selectedModelAssetPath = selectedModelAsset != null
                    ? AssetDatabase.GetAssetPath(selectedModelAsset) ?? string.Empty
                    : string.Empty;
            }

            validationMessage = string.Empty;
            RefreshDetectionState();
            RefreshManualContext();
        }

        internal void RemovePlacementModel(GameObject model)
        {
            if (model == null)
            {
                return;
            }

            CharacterPlacementController placementController = currentContext != null
                ? currentContext.PlacementController
                : CharacterPlacementController.GetPrimaryCachedOrFind();
            if (placementController == null)
            {
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("HoyoToon Remove Placement Model");
            Undo.RecordObject(placementController, "HoyoToon Remove Placement Model");
            placementController.RemoveModel(model);
            EditorUtility.SetDirty(placementController);
            Undo.DestroyObjectImmediate(model);
            Undo.CollapseUndoOperations(undoGroup);
            RefreshManualContext();
        }

        private void ApplySelectedModels(IEnumerable<GameObject> modelAssets, DefaultAsset folderAsset, string folderPath, string newValidationMessage)
        {
            batchModelAssets.Clear();

            HashSet<string> seenAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GameObject modelAsset in modelAssets ?? Array.Empty<GameObject>())
            {
                if (!TryValidateSelectedModel(modelAsset, out string assetPath, out _))
                {
                    continue;
                }

                if (seenAssetPaths.Add(assetPath))
                {
                    batchModelAssets.Add(modelAsset);
                }
            }

            selectedModelFolder = folderAsset;
            selectedModelFolderPath = folderPath ?? string.Empty;
            selectedModelAsset = batchModelAssets.FirstOrDefault();
            selectedModelAssetPath = selectedModelAsset != null ? AssetDatabase.GetAssetPath(selectedModelAsset) ?? string.Empty : string.Empty;
            validationMessage = newValidationMessage ?? string.Empty;
            RefreshDetectionState();
            RefreshManualContext();
        }

        internal void RunAutoSetupForSelectedModel()
        {
            if (batchModelAssets.Count <= 0)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Auto Setup", "Add at least one FBX model first.", "OK");
                OnboardingSignals.RecordOperationFailure(OnboardingOperationKind.Setup, "Add at least one FBX model first.");
                return;
            }

            Object[] previousSelection = Selection.objects?.ToArray() ?? Array.Empty<Object>();
            Object previousActiveObject = Selection.activeObject;
            AutoSetupResult result = null;

            try
            {
                Selection.objects = batchModelAssets.Cast<Object>().ToArray();
                Selection.activeObject = selectedModelAsset;
                result = AutoSetup.RunSelection(new AutoSetupOptions());
            }
            catch (Exception exception)
            {
                OnboardingSignals.RecordOperationFailure(OnboardingOperationKind.Setup, exception.Message);
                throw;
            }
            finally
            {
                Selection.objects = previousSelection;
                Selection.activeObject = previousActiveObject;
            }

            RefreshDetectionState();
            RefreshManualContext();

            if (result != null && result.Succeeded)
            {
                OnboardingSignals.RecordOperationSuccess(OnboardingOperationKind.Setup);
            }
            else if (result != null)
            {
                string message = result.Errors.Count > 0
                    ? result.Errors[0]
                    : "Auto Setup finished without reporting success.";
                OnboardingSignals.RecordOperationFailure(OnboardingOperationKind.Setup, message);
            }
        }

        private void RefreshFooterActionStates()
        {
            bool hasActiveCharacter = GetActivePlacementModel() != null;
            bool hasSelection = hasActiveCharacter;
            bool hasValidTarget = hasActiveCharacter;

            if (footerActions.Count > 0)
            {
                footerActions[0].IsEnabled = hasValidTarget;
                ApplyActionState(createPrefabButton, footerActions[0]);
            }

            if (footerActions.Count > 1)
            {
                footerActions[1].IsEnabled = hasSelection;
                ApplyActionState(regenerateMaterialsButton, footerActions[1]);
            }

            if (footerActions.Count > 2)
            {
                footerActions[2].IsEnabled = hasValidTarget;
                ApplyActionState(regenerateTangentsButton, footerActions[2]);
            }
        }

        private static void ApplyActionState(Button button, InspectorAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.SetEnabled(action.IsEnabled);
        }

        private void UpdateVersionBadge()
        {
            if (versionBadgeButton == null)
            {
                return;
            }

            packageVersionLabel = ResolvePackageVersionLabel();

            UpdaterStatusSnapshot snapshot = PackageUpdaterService.GetStatusSnapshot();
            UpdateAvailabilityState state = snapshot != null
                ? (UpdateAvailabilityState)snapshot.state
                : UpdateAvailabilityState.Unknown;
            UpdateAvailabilityState visualState = GetVersionVisualState(state);

            versionBadgeButton.text = visualState == UpdateAvailabilityState.UpToDate
                ? packageVersionLabel + " ✓"
                : packageVersionLabel;
            versionBadgeButton.tooltip = BuildVersionBadgeTooltip(snapshot, state);
            versionBadgeButton.SetEnabled(!PackageUpdaterService.IsBusy());

            versionBadgeButton.RemoveFromClassList(VersionBadgeUnknownClass);
            versionBadgeButton.RemoveFromClassList(VersionBadgeCheckingClass);
            versionBadgeButton.RemoveFromClassList(VersionBadgeUpToDateClass);
            versionBadgeButton.RemoveFromClassList(VersionBadgeUpdateAvailableClass);
            versionBadgeButton.RemoveFromClassList(VersionBadgeLocalAheadClass);
            versionBadgeButton.RemoveFromClassList(VersionBadgeApplyingClass);
            versionBadgeButton.RemoveFromClassList(VersionBadgeErrorClass);
            versionBadgeButton.AddToClassList(GetUpdaterStateClass(visualState));
        }

        private void ApplyHeaderArtwork()
        {
            Texture2D backgroundTexture = LoadAsset<Texture2D>(HeaderBackgroundAssetPath);
            if (bannerBackground != null && backgroundTexture != null)
            {
                bannerBackground.image = backgroundTexture;
                bannerBackground.scaleMode = ScaleMode.ScaleAndCrop;
                bannerBackground.style.display = DisplayStyle.Flex;
            }

            Texture2D logoTexture = LoadAsset<Texture2D>(HeaderLogoAssetPath);
            if (logoImage != null)
            {
                logoImage.image = logoTexture;
                logoImage.scaleMode = ScaleMode.ScaleToFit;
                logoImage.style.display = logoTexture != null ? DisplayStyle.Flex : DisplayStyle.None;
            }

        }

        private void StartVersionBadgeSchedule()
        {
            versionBadgeSchedule?.Pause();
            if (shellRoot == null)
            {
                return;
            }

            versionBadgeSchedule = shellRoot.schedule.Execute(UpdateVersionBadge).Every(1250);
        }

        private static string ResolvePackageVersionLabel()
        {
            try
            {
                UnityEditor.PackageManager.PackageInfo packageInfo =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(HoyoToonManagerWindow).Assembly);
                if (packageInfo != null && !string.IsNullOrWhiteSpace(packageInfo.version))
                {
                    return NormalizeVersionLabel(packageInfo.version);
                }
            }
            catch
            {
            }

            return VersionUnavailableLabel;
        }

        private static string NormalizeVersionLabel(string version)
        {
            string normalizedVersion = string.IsNullOrWhiteSpace(version) ? VersionUnavailableLabel : version.Trim();
            if (string.Equals(normalizedVersion, VersionUnavailableLabel, StringComparison.Ordinal))
            {
                return normalizedVersion;
            }

            return normalizedVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? normalizedVersion
                : "v" + normalizedVersion;
        }

        private VisualElement CreateEmptyState()
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ht-column");
            root.AddToClassList("ht-gap-8");

            VisualElement card = new VisualElement();
            card.AddToClassList("ht-card");
            card.AddToClassList("ht-column");
            card.AddToClassList("ht-gap-8");
            card.AddToClassList("ht-empty-state");

            Label title = new Label("No module content is available yet");
            title.AddToClassList("ht-title-md");
            card.Add(title);

            Label body = new Label(
                "The manager is waiting for a module view. Add an FBX model in Main to populate the manual HoyoToon workflow.");
            body.AddToClassList("ht-body-text");
            card.Add(body);

            HelpBox helpBox = new HelpBox(
                "The shell, tabs, and footer stay active, and the manager will not change itself when Unity's selection changes elsewhere.",
                HelpBoxMessageType.Info);
            helpBox.AddToClassList("ht-helpbox-info");
            card.Add(helpBox);

            VisualElement actionRow = new VisualElement();
            actionRow.AddToClassList("ht-row");
            actionRow.AddToClassList("ht-gap-8");

            Button refreshButton = new Button(RefreshManualContext)
            {
                text = "Refresh"
            };
            refreshButton.AddToClassList("ht-btn-ghost");
            actionRow.Add(refreshButton);

            card.Add(actionRow);
            root.Add(card);
            return root;
        }

        private void RefreshManualContext()
        {
            currentContext = BuildModuleContext();
            RefreshGlobalContext();
            RefreshFooterActionStates();
            RefreshBody();
        }

        private void RefreshGlobalContext()
        {
            if (globalContextHost == null)
            {
                return;
            }

            globalContextHost.Clear();
            globalContextHost.Add(SetupModule.CreatePlacementSection(currentContext));
        }

        internal void RefreshManagerContext()
        {
            RefreshManualContext();
            RegisterOnboardingTargets();
        }

        internal void SelectModuleForOnboarding(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                return;
            }

            if (modules == null || modules.Count <= 0)
            {
                modules = ModuleRegistry.CreateModules();
            }

            currentContext = BuildModuleContext();
            SetActiveModule(moduleId, true);
            RefreshGlobalContext();
            RefreshFooterActionStates();
            RegisterOnboardingTargets();
            Focus();
        }

        internal bool HasQueuedModelNamed(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            return batchDetectionResults.Any(detection =>
                    detection != null
                    && !string.IsNullOrWhiteSpace(detection.ModelName)
                    && detection.ModelName.IndexOf(characterName, StringComparison.OrdinalIgnoreCase) >= 0)
                || batchModelAssets.Any(model =>
                    model != null
                    && model.name.IndexOf(characterName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal bool HasPlacementCharacterNamed(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName) || currentContext == null)
            {
                return false;
            }

            return (currentContext.PlacementManagedCharacterNames ?? Array.Empty<string>())
                .Any(name => !string.IsNullOrWhiteSpace(name)
                    && name.IndexOf(characterName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal bool IsActivePlacementCharacterNamed(string characterName)
        {
            if (currentContext == null)
            {
                currentContext = BuildModuleContext();
            }

            if (currentContext == null || currentContext.PlacementActiveModel == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(characterName))
            {
                return true;
            }

            return ContainsCharacterName(currentContext.PlacementActiveCharacterName, characterName)
                || ContainsCharacterName(currentContext.PlacementActiveModel.name, characterName);
        }

        internal bool TrySelectPlacementCharacterForOnboarding(string characterName)
        {
            CharacterPlacementController placementController = currentContext != null
                ? currentContext.PlacementController
                : CharacterPlacementController.GetPrimaryCachedOrFind();
            if (placementController == null)
            {
                return false;
            }

            placementController.EnsureRosterConsistency();
            IReadOnlyList<GameObject> managedModels = placementController.ManagedModels;
            if (managedModels == null || managedModels.Count <= 0)
            {
                return false;
            }

            int selectedIndex = ResolvePlacementModelIndex(managedModels, characterName);
            if (selectedIndex < 0)
            {
                return false;
            }

            Undo.RecordObject(placementController, "HoyoToon Select Active Character");
            if (placementController.PlacementMode == ManagerPlacementMode.Team)
            {
                placementController.PlacementMode = ManagerPlacementMode.Single;
            }

            placementController.SetFocusedModel(managedModels[selectedIndex]);
            EditorUtility.SetDirty(placementController);
            RefreshManualContext();
            return true;
        }

        internal bool TryClearActivePlacementCharacterForOnboarding(string targetCharacterName = null)
        {
            CharacterPlacementController placementController = currentContext != null
                ? currentContext.PlacementController
                : CharacterPlacementController.GetPrimaryCachedOrFind();
            if (placementController == null)
            {
                return false;
            }

            Undo.RecordObject(placementController, "HoyoToon Clear Active Character");
            if (placementController.PlacementMode == ManagerPlacementMode.Team)
            {
                placementController.PlacementMode = ManagerPlacementMode.Single;
            }

            GameObject fallbackModel = ResolveAlternatePlacementModel(
                placementController.ManagedModels,
                placementController.ActiveModel,
                targetCharacterName);
            if (fallbackModel != null)
            {
                placementController.SetFocusedModel(fallbackModel);
            }

            EditorUtility.SetDirty(placementController);
            RefreshManualContext();
            return true;
        }

        internal bool TrySetPlacementModeForOnboarding(ManagerPlacementMode placementMode)
        {
            CharacterPlacementController placementController = currentContext != null
                ? currentContext.PlacementController
                : CharacterPlacementController.GetPrimaryCachedOrFind();
            if (placementController == null)
            {
                return false;
            }

            placementController.EnsureRosterConsistency();
            if (placementController.PlacementMode == placementMode)
            {
                return true;
            }

            Undo.RecordObject(placementController, "HoyoToon Change Placement Mode");
            placementController.PlacementMode = placementMode;
            EditorUtility.SetDirty(placementController);
            RefreshManualContext();
            return true;
        }

        private static int ResolvePlacementModelIndex(IReadOnlyList<GameObject> managedModels, string characterName)
        {
            if (managedModels == null || managedModels.Count <= 0)
            {
                return -1;
            }

            if (string.IsNullOrWhiteSpace(characterName))
            {
                return 0;
            }

            for (int index = 0; index < managedModels.Count; index++)
            {
                GameObject model = managedModels[index];
                if (model == null)
                {
                    continue;
                }

                string resolvedName = ResolvePlacementCharacterName(model);
                if (ContainsCharacterName(resolvedName, characterName) || ContainsCharacterName(model.name, characterName))
                {
                    return index;
                }
            }

            return -1;
        }

        private static GameObject ResolveAlternatePlacementModel(
            IReadOnlyList<GameObject> managedModels,
            GameObject activeModel,
            string targetCharacterName)
        {
            if (managedModels == null || managedModels.Count <= 0)
            {
                return null;
            }

            for (int index = 0; index < managedModels.Count; index++)
            {
                GameObject model = managedModels[index];
                if (model == null || ReferenceEquals(model, activeModel))
                {
                    continue;
                }

                if (!ContainsCharacterName(ResolvePlacementCharacterName(model), targetCharacterName)
                    && !ContainsCharacterName(model.name, targetCharacterName))
                {
                    return model;
                }
            }

            for (int index = 0; index < managedModels.Count; index++)
            {
                GameObject model = managedModels[index];
                if (model != null)
                {
                    return model;
                }
            }

            return null;
        }

        private static bool ContainsCharacterName(string value, string characterName)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.IsNullOrWhiteSpace(characterName)
                && value.IndexOf(characterName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RegisterOnboardingTargets()
        {
            if (shellRoot == null)
            {
                return;
            }

            OnboardingTargetRegistry.RegisterVisualElement(
                "Manager.Window",
                shellRoot,
                "HoyoToon Manager window",
                "Manager");

            if (versionBadgeButton != null)
            {
                OnboardingTargetRegistry.RegisterVisualElement(
                    "Manager.HeaderUpdateButton",
                    versionBadgeButton,
                    "Header update button",
                    "Manager Header",
                    () => versionBadgeButton.Focus());
            }

            RegisterTabOnboardingTargets();
        }

        private void RegisterTabOnboardingTargets()
        {
            if (modules == null || tabButtonsById == null)
            {
                return;
            }

            foreach (IManagerModule module in modules)
            {
                if (module == null || !tabButtonsById.TryGetValue(module.Id, out Button tabButton) || tabButton == null)
                {
                    continue;
                }

                RegisterTabOnboardingTarget(module, tabButton);
            }
        }

        private static void RegisterTabOnboardingTarget(IManagerModule module, Button tabButton)
        {
            if (module == null || tabButton == null)
            {
                return;
            }

            string targetId = GetOnboardingTabTargetId(module.Id);
            OnboardingTargetRegistry.RegisterVisualElement(
                targetId,
                tabButton,
                module.DisplayName + " tab",
                "Manager Navigation",
                () => tabButton.Focus());
        }

        private ModuleContext BuildModuleContext()
        {
            Object[] selectedObjects = batchModelAssets.Cast<Object>().ToArray();
            CharacterPlacementController placementController = CharacterPlacementController.GetPrimaryCachedOrFind();
            if (placementController != null)
            {
                placementController.EnsureRosterConsistency();
            }
            GameObject placementActiveModel = placementController != null ? placementController.ActiveModel : null;
            string placementActiveGameKey = ResolvePlacementGameKey(placementActiveModel);
            string placementActiveCharacterName = ResolvePlacementCharacterName(placementActiveModel);
            string[] placementManagedCharacterNames = placementController != null
                ? placementController.ManagedModels.Select(model => model != null ? model.name : "Missing").ToArray()
                : Array.Empty<string>();
            string[] placementTeamCharacterNames = placementController != null
                ? placementController.TeamActiveModels.Select(model => model != null ? model.name : "Missing").ToArray()
                : Array.Empty<string>();

            return new ModuleContext
            {
                Window = this,
                ActiveObject = selectedModelAsset,
                SelectedObjects = selectedObjects,
                SelectedGameObject = selectedModelAsset,
                SelectedModelAsset = selectedModelAsset,
                SelectedModelAssetPath = selectedModelAssetPath,
                SelectedModelFolder = selectedModelFolder,
                SelectedModelFolderPath = selectedModelFolderPath,
                BatchModelDetections = batchDetectionResults.ToArray(),
                TargetRoot = selectedModelAsset,
                TargetComponent = null,
                SerializedObject = null,
                DetectedModelName = detectedModelName,
                DetectedCharacterName = detectedCharacterName,
                DetectedGameKey = detectedGameKey,
                MatchedJsonAssetPath = matchedJsonAssetPath,
                JsonCount = detectedJsonCount,
                MaterialCount = detectedMaterialCount,
                ValidationMessage = validationMessage,
                DetectionSummaryMessage = detectionSummaryMessage,
                PlacementController = placementController,
                PlacementActiveModel = placementActiveModel,
                PlacementActiveCharacterName = placementActiveCharacterName,
                PlacementActiveGameKey = placementActiveGameKey,
                PlacementManagedCharacterNames = placementManagedCharacterNames,
                PlacementTeamCharacterNames = placementTeamCharacterNames
            };
        }

        private static bool TryValidateSelectedModel(GameObject modelAsset, out string assetPath, out string newValidationMessage)
        {
            assetPath = string.Empty;
            newValidationMessage = string.Empty;

            if (modelAsset == null)
            {
                return true;
            }

            assetPath = AssetDatabase.GetAssetPath(modelAsset);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                newValidationMessage = "Add Model only supports FBX model assets from the Project window.";
                return false;
            }

            return true;
        }

        private void ClearDetectionState()
        {
            batchDetectionResults.Clear();
            detectedModelName = string.Empty;
            detectedCharacterName = string.Empty;
            detectedGameKey = string.Empty;
            matchedJsonAssetPath = string.Empty;
            detectedJsonCount = 0;
            detectedMaterialCount = 0;
            detectionSummaryMessage = "Slot an FBX model to inspect detection results and run Auto Setup.";
        }

        private void RefreshDetectionState()
        {
            ClearDetectionState();
            if (batchModelAssets.Count <= 0)
            {
                return;
            }

            GameObject preferredModel = selectedModelAsset;
            foreach (GameObject modelAsset in batchModelAssets)
            {
                if (!TryValidateSelectedModel(modelAsset, out string assetPath, out _))
                {
                    continue;
                }

                batchDetectionResults.Add(BuildBatchDetectionInfo(modelAsset, assetPath));
            }

            List<BatchModelDetectionInfo> filteredDetections = CollapseConvertedDuplicates(batchDetectionResults);
            batchDetectionResults.Clear();
            batchDetectionResults.AddRange(filteredDetections);
            batchModelAssets.Clear();
            batchModelAssets.AddRange(filteredDetections
                .Select(detection => detection.ModelAsset)
                .Where(modelAsset => modelAsset != null));

            selectedModelAsset = preferredModel != null && batchModelAssets.Contains(preferredModel)
                ? preferredModel
                : batchModelAssets.FirstOrDefault();
            selectedModelAssetPath = selectedModelAsset != null
                ? AssetDatabase.GetAssetPath(selectedModelAsset) ?? string.Empty
                : string.Empty;

            BatchModelDetectionInfo primaryDetection = batchDetectionResults.FirstOrDefault();
            if (primaryDetection != null)
            {
                detectedModelName = primaryDetection.ModelName;
                detectedCharacterName = primaryDetection.ModelName;
                detectedGameKey = primaryDetection.GameKey;
                matchedJsonAssetPath = primaryDetection.MatchedJsonAssetPath;
                detectedJsonCount = primaryDetection.JsonCount;
                detectedMaterialCount = primaryDetection.MaterialCount;
            }

            detectionSummaryMessage = BuildDetectionSummaryMessage();
        }

        private static BatchModelDetectionInfo BuildBatchDetectionInfo(GameObject modelAsset, string modelAssetPath)
        {
            List<string> jsonAssetPaths = AssetContextJsonQueryUtility.EnumerateJsonAssetPaths(modelAssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            BatchModelDetectionInfo detection = new BatchModelDetectionInfo
            {
                ModelAsset = modelAsset,
                AssetPath = modelAssetPath ?? string.Empty,
                IsConverted = HasConvertedLabel(modelAssetPath),
                JsonCount = jsonAssetPaths.Count,
                MaterialCount = CountMaterialAssets(modelAssetPath)
            };

            if (GameDetector.TryDetectGameFromAssetContext(modelAssetPath, out GameConfigSO game, out string detectedJsonAssetPath)
                && game != null)
            {
                detection.GameKey = game.Key ?? string.Empty;
                detection.MatchedJsonAssetPath = detectedJsonAssetPath ?? string.Empty;
                detection.ModelName = ResolveDetectedModelName(detection.GameKey, modelAssetPath, detection.MatchedJsonAssetPath);
            }

            List<string> compatibleJsonAssetPaths = ResolveCompatibleJsonAssetPaths(jsonAssetPaths, detection.GameKey, detection.ModelName);
            detection.CompatibleJsonCount = compatibleJsonAssetPaths.Count;
            detection.CompatibleMaterialCount = CountCompatibleMaterialAssets(modelAssetPath, detection.GameKey, detection.ModelName, compatibleJsonAssetPaths);
            return detection;
        }

        private static List<BatchModelDetectionInfo> CollapseConvertedDuplicates(IEnumerable<BatchModelDetectionInfo> detections)
        {
            List<BatchModelDetectionInfo> orderedDetections = new List<BatchModelDetectionInfo>();
            Dictionary<string, int> indexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (BatchModelDetectionInfo detection in detections ?? Array.Empty<BatchModelDetectionInfo>())
            {
                if (detection == null || detection.ModelAsset == null)
                {
                    continue;
                }

                string key = BuildBatchDuplicateKey(detection);
                if (!indexByKey.TryGetValue(key, out int existingIndex))
                {
                    indexByKey[key] = orderedDetections.Count;
                    orderedDetections.Add(detection);
                    continue;
                }

                BatchModelDetectionInfo existingDetection = orderedDetections[existingIndex];
                if (ShouldReplaceQueuedDetection(existingDetection, detection))
                {
                    orderedDetections[existingIndex] = detection;
                }
            }

            return orderedDetections;
        }

        private static string BuildBatchDuplicateKey(BatchModelDetectionInfo detection)
        {
            string assetPath = detection != null ? detection.AssetPath : string.Empty;
            string directoryPath = string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : Path.GetDirectoryName(assetPath) ?? string.Empty;
            string modelKey = detection != null && !string.IsNullOrWhiteSpace(detection.ModelName)
                ? detection.ModelName.Trim()
                : !string.IsNullOrWhiteSpace(assetPath)
                    ? Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty
                    : string.Empty;

            return directoryPath + "|" + modelKey;
        }

        private static bool ShouldReplaceQueuedDetection(BatchModelDetectionInfo existingDetection, BatchModelDetectionInfo candidateDetection)
        {
            if (existingDetection == null)
            {
                return true;
            }

            if (candidateDetection == null)
            {
                return false;
            }

            if (candidateDetection.IsConverted != existingDetection.IsConverted)
            {
                return candidateDetection.IsConverted;
            }

            bool candidateHasDetectedName = !string.IsNullOrWhiteSpace(candidateDetection.ModelName);
            bool existingHasDetectedName = !string.IsNullOrWhiteSpace(existingDetection.ModelName);
            if (candidateHasDetectedName != existingHasDetectedName)
            {
                return candidateHasDetectedName;
            }

            return false;
        }

        private string BuildDetectionSummaryMessage()
        {
            if (batchDetectionResults.Count <= 0)
            {
                return "No FBX added.";
            }

            if (batchDetectionResults.Count > 1)
            {
                int detectedCount = batchDetectionResults.Count(result => !string.IsNullOrWhiteSpace(result.GameKey));
                return detectedCount >= batchDetectionResults.Count
                    ? batchDetectionResults.Count + " models detected."
                    : detectedCount + "/" + batchDetectionResults.Count + " models detected.";
            }

            if (!string.IsNullOrWhiteSpace(detectedGameKey))
            {
                return string.IsNullOrWhiteSpace(matchedJsonAssetPath)
                    ? "Detection ready."
                    : "Matched JSON: " + matchedJsonAssetPath;
            }

            if (detectedJsonCount <= 0)
            {
                return "No JSON found.";
            }

            return "Game not detected.";
        }

        private static List<GameObject> CollectFbxModelAssets(IEnumerable<Object> objects)
        {
            List<(string AssetPath, GameObject ModelAsset)> models = new List<(string AssetPath, GameObject ModelAsset)>();
            HashSet<string> seenAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Object candidate in objects ?? Array.Empty<Object>())
            {
                if (!(candidate is GameObject modelAsset))
                {
                    continue;
                }

                if (!TryValidateSelectedModel(modelAsset, out string assetPath, out _))
                {
                    continue;
                }

                if (seenAssetPaths.Add(assetPath))
                {
                    models.Add((assetPath, modelAsset));
                }
            }

            models.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.AssetPath, right.AssetPath));
            return models.Select(item => item.ModelAsset).ToList();
        }

        private static List<GameObject> FindFbxModelAssetsInFolder(string folderAssetPath)
        {
            string[] assetGuids = AssetDatabase.FindAssets("t:Model", new[] { folderAssetPath });
            List<(string AssetPath, GameObject ModelAsset)> models = new List<(string AssetPath, GameObject ModelAsset)>();
            HashSet<string> seenAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string assetGuid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (modelAsset == null || !seenAssetPaths.Add(assetPath))
                {
                    continue;
                }

                models.Add((assetPath, modelAsset));
            }

            models.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.AssetPath, right.AssetPath));
            return models.Select(item => item.ModelAsset).ToList();
        }

        private static int CountMaterialAssets(string contextAssetPath)
        {
            if (!AssetContextJsonQueryUtility.TryResolveSearchRootDirectory(contextAssetPath, out string searchRootDirectory))
            {
                return 0;
            }

            string searchRootAssetPath = AssetContextJsonQueryUtility.ToAssetPath(searchRootDirectory);
            if (string.IsNullOrWhiteSpace(searchRootAssetPath))
            {
                return 0;
            }

            return AssetDatabase.FindAssets("t:Material", new[] { searchRootAssetPath }).Length;
        }

        private static List<string> ResolveCompatibleJsonAssetPaths(
            IEnumerable<string> jsonAssetPaths,
            string detectedGameKey,
            string detectedModelName)
        {
            List<string> compatibleJsonAssetPaths = new List<string>();

            foreach (string jsonAssetPath in jsonAssetPaths ?? Array.Empty<string>())
            {
                string absoluteJsonPath = AssetContextJsonQueryUtility.ToAbsolutePath(jsonAssetPath);
                if (string.IsNullOrWhiteSpace(absoluteJsonPath) || !File.Exists(absoluteJsonPath))
                {
                    continue;
                }

                string rawJson;
                try
                {
                    rawJson = File.ReadAllText(absoluteJsonPath);
                }
                catch
                {
                    continue;
                }

                if (!GameDetector.TryDetectGame(rawJson, jsonAssetPath, out GameConfigSO detectedGame, out string detectedCharacterName)
                    || detectedGame == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(detectedGameKey)
                    && !string.Equals(detectedGame.Key, detectedGameKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(detectedModelName))
                {
                    if (string.IsNullOrWhiteSpace(detectedCharacterName)
                        || !string.Equals(detectedCharacterName, detectedModelName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                compatibleJsonAssetPaths.Add(jsonAssetPath);
            }

            return compatibleJsonAssetPaths;
        }

        private static int CountCompatibleMaterialAssets(
            string contextAssetPath,
            string detectedGameKey,
            string detectedModelName,
            IEnumerable<string> compatibleJsonAssetPaths)
        {
            if (!AssetContextJsonQueryUtility.TryResolveSearchRootDirectory(contextAssetPath, out string searchRootDirectory))
            {
                return 0;
            }

            string searchRootAssetPath = AssetContextJsonQueryUtility.ToAssetPath(searchRootDirectory);
            if (string.IsNullOrWhiteSpace(searchRootAssetPath))
            {
                return 0;
            }

            HashSet<string> expectedMaterialPaths = new HashSet<string>(
                (compatibleJsonAssetPaths ?? Array.Empty<string>())
                    .Select(jsonAssetPath => Path.ChangeExtension(jsonAssetPath, ".mat"))
                    .Where(assetPath => !string.IsNullOrWhiteSpace(assetPath))
                    .Select(AssetContextJsonQueryUtility.NormalizeAssetPath),
                StringComparer.OrdinalIgnoreCase);

            return AssetDatabase.FindAssets("t:Material", new[] { searchRootAssetPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(assetPath => !string.IsNullOrWhiteSpace(assetPath))
                .Select(AssetContextJsonQueryUtility.NormalizeAssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(materialAssetPath =>
                    expectedMaterialPaths.Contains(materialAssetPath)
                    || (!string.IsNullOrWhiteSpace(detectedGameKey)
                        && !string.IsNullOrWhiteSpace(detectedModelName)
                        && string.Equals(
                            CharacterNameDetector.TryExtractCharacterName(detectedGameKey, materialAssetPath),
                            detectedModelName,
                            StringComparison.OrdinalIgnoreCase)));
        }

        private static bool HasConvertedLabel(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                return false;
            }

            return AssetDatabase.GetLabels(asset)
                .Any(label => string.Equals(label, ConvertedAssetLabel, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveDetectedModelName(string gameKey, string modelAssetPath, string matchedJsonAssetPath)
        {
            if (string.IsNullOrWhiteSpace(gameKey))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(matchedJsonAssetPath))
            {
                string detectedName = CharacterNameDetector.TryExtractCharacterName(gameKey, matchedJsonAssetPath);
                if (!string.IsNullOrWhiteSpace(detectedName))
                {
                    return detectedName;
                }
            }

            if (!string.IsNullOrWhiteSpace(modelAssetPath))
            {
                string detectedName = CharacterNameDetector.TryExtractCharacterName(gameKey, modelAssetPath);
                if (!string.IsNullOrWhiteSpace(detectedName))
                {
                    return detectedName;
                }
            }

            return string.Empty;
        }

        private void HandleVersionBadgeClicked()
        {
            PackageUpdaterService.CheckForUpdates(showUpToDateDialog: true, automatic: false, cleanMissingFiles: true);
            UpdateVersionBadge();
        }

        private static string GetUpdaterStateLabel(UpdateAvailabilityState state)
        {
            switch (state)
            {
                case UpdateAvailabilityState.Checking:
                    return "Checking";
                case UpdateAvailabilityState.UpToDate:
                    return "Up to date";
                case UpdateAvailabilityState.UpdateAvailable:
                    return "Update available";
                case UpdateAvailabilityState.LocalAhead:
                    return "Local ahead";
                case UpdateAvailabilityState.Applying:
                    return "Applying";
                case UpdateAvailabilityState.Error:
                    return "Status error";
                default:
                    return "Check updates";
            }
        }

        private static UpdateAvailabilityState GetVersionVisualState(UpdateAvailabilityState state)
        {
            switch (state)
            {
                case UpdateAvailabilityState.UpToDate:
                    return UpdateAvailabilityState.UpToDate;
                case UpdateAvailabilityState.UpdateAvailable:
                case UpdateAvailabilityState.LocalAhead:
                case UpdateAvailabilityState.Applying:
                case UpdateAvailabilityState.Error:
                    return UpdateAvailabilityState.UpdateAvailable;
                default:
                    return UpdateAvailabilityState.Checking;
            }
        }

        private static string GetUpdaterStateClass(UpdateAvailabilityState state)
        {
            switch (state)
            {
                case UpdateAvailabilityState.Checking:
                    return VersionBadgeCheckingClass;
                case UpdateAvailabilityState.UpToDate:
                    return VersionBadgeUpToDateClass;
                case UpdateAvailabilityState.UpdateAvailable:
                    return VersionBadgeUpdateAvailableClass;
                case UpdateAvailabilityState.LocalAhead:
                case UpdateAvailabilityState.Applying:
                case UpdateAvailabilityState.Error:
                    return VersionBadgeUpdateAvailableClass;
                default:
                    return VersionBadgeCheckingClass;
            }
        }

        private static string BuildVersionBadgeTooltip(UpdaterStatusSnapshot snapshot, UpdateAvailabilityState state)
        {
            if (snapshot == null)
            {
                return "Click to check for package updates.";
            }

            string localVersion = string.IsNullOrWhiteSpace(snapshot.localVersion) ? "unknown" : snapshot.localVersion;
            string remoteVersion = string.IsNullOrWhiteSpace(snapshot.remoteVersion) ? "unknown" : snapshot.remoteVersion;
            string message = string.IsNullOrWhiteSpace(snapshot.statusMessage) ? "Click to check for package updates." : snapshot.statusMessage;
            return "Local: " + localVersion + "\nRemote: " + remoteVersion + "\n" + message;
        }

        private static string ResolvePlacementCharacterName(GameObject placementActiveModel)
        {
            if (!TryResolveModelAssetPath(placementActiveModel, out string assetPath))
            {
                return placementActiveModel != null ? placementActiveModel.name : string.Empty;
            }

            if (GameDetector.TryDetectGameFromAssetContext(assetPath, out GameConfigSO game, out string placementMatchedJsonPath) && game != null)
            {
                string characterName = CharacterNameDetector.TryExtractCharacterName(game.Key, assetPath);
                if (!string.IsNullOrWhiteSpace(characterName))
                {
                    return characterName;
                }

                if (!string.IsNullOrWhiteSpace(placementMatchedJsonPath))
                {
                    characterName = CharacterNameDetector.TryExtractCharacterName(game.Key, placementMatchedJsonPath);
                    if (!string.IsNullOrWhiteSpace(characterName))
                    {
                        return characterName;
                    }
                }
            }

            return placementActiveModel != null ? placementActiveModel.name : string.Empty;
        }

        private static string ResolvePlacementGameKey(GameObject placementActiveModel)
        {
            if (!TryResolveModelAssetPath(placementActiveModel, out string assetPath))
            {
                return string.Empty;
            }

            return GameDetector.TryDetectGameFromAssetContext(assetPath, out GameConfigSO game, out _) && game != null
                ? game.Key ?? string.Empty
                : string.Empty;
        }

        private static bool TryResolveModelAssetPath(GameObject candidate, out string assetPath)
        {
            assetPath = string.Empty;
            if (candidate == null)
            {
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(candidate);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                return true;
            }

            assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                return true;
            }

            Object source = PrefabUtility.GetCorrespondingObjectFromSource(candidate);
            if (source == null)
            {
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(source);
            return !string.IsNullOrWhiteSpace(assetPath);
        }

        private void HandleCreatePrefab()
        {
            GameObject targetRoot = GetActivePlacementModel();
            if (targetRoot == null)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Create Prefab", "Set an active character first.", "OK");
                return;
            }

            if (!ConfirmKnownCharacterProblem(targetRoot, "Create Prefab"))
            {
                return;
            }

            string defaultName = BuildSafeAssetName(targetRoot.name);
            string chosenPath = EditorUtility.SaveFilePanelInProject(
                "Create Prefab",
                defaultName,
                "prefab",
                "Choose a location for the generated prefab.");

            if (string.IsNullOrWhiteSpace(chosenPath))
            {
                return;
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(chosenPath);
            GameObject prefab = null;
            GameObject temporaryInstance = null;

            try
            {
                if (EditorUtility.IsPersistent(targetRoot))
                {
                    temporaryInstance = PrefabUtility.InstantiatePrefab(targetRoot) as GameObject;
                    if (temporaryInstance == null)
                    {
                        temporaryInstance = Instantiate(targetRoot);
                    }

                    prefab = PrefabUtility.SaveAsPrefabAsset(temporaryInstance, assetPath);
                }
                else
                {
                    prefab = targetRoot.scene.IsValid()
                        ? PrefabUtility.SaveAsPrefabAssetAndConnect(targetRoot, assetPath, InteractionMode.UserAction)
                        : PrefabUtility.SaveAsPrefabAsset(targetRoot, assetPath);
                }
            }
            finally
            {
                if (temporaryInstance != null)
                {
                    DestroyImmediate(temporaryInstance);
                }
            }

            if (prefab == null)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(
                    "Create Prefab",
                    "Prefab creation did not complete. Check the Console for more details.",
                    "OK");
                return;
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Create Prefab", "Created prefab at:\n" + assetPath, "OK");
        }

        private void HandleRegenerateMaterials()
        {
            GameObject targetRoot = GetActivePlacementModel();
            if (targetRoot != null && !ConfirmKnownCharacterProblem(targetRoot, "Regenerate Materials"))
            {
                return;
            }

            Object[] selectedObjects = BuildFooterSelectionTargets();
            MaterialGenerator.Result result = MaterialGenerator.GenerateFromSelection(selectedObjects);

            string dialogBody;
            if (!result.HasSelection)
            {
                dialogBody = "Set an active character first.";
            }
            else if (!result.HasJsonCandidates)
            {
                dialogBody = "No material JSON files were found beside the active character.";
            }
            else
            {
                dialogBody =
                    "Scanned: " + result.ScannedCount + "\n" +
                    "Created: " + result.CreatedCount + "\n" +
                    "Updated: " + result.UpdatedCount + "\n" +
                    "Failed: " + result.FailedCount;
            }

            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Regenerate Materials", dialogBody, "OK");
        }

        private void HandleRegenerateTangents()
        {
            GameObject targetRoot = GetActivePlacementModel();
            if (targetRoot == null)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Regenerate Tangents", "Set an active character first.", "OK");
                return;
            }

            if (!ConfirmKnownCharacterProblem(targetRoot, "Regenerate Tangents"))
            {
                return;
            }

            TangentSettingsApplicator.Initialize();
            bool regenerated = TangentSettingsApplicator.TryRegenerateFromConfig(targetRoot);
            string message = regenerated
                ? "Regenerated tangents for '" + targetRoot.name + "'."
                : "No tangent configuration could be resolved for '" + targetRoot.name + "'.";
            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Regenerate Tangents", message, "OK");
        }

        private GameObject GetActivePlacementModel()
        {
            return currentContext != null ? currentContext.PlacementActiveModel : null;
        }

        private static bool ConfirmKnownCharacterProblem(GameObject model, string actionName)
        {
            if (!TryResolveModelAssetPath(model, out string assetPath))
            {
                return true;
            }

            return CharacterProblemPrompt.ConfirmAssetContext(assetPath, actionName, model);
        }

        private Object[] BuildFooterSelectionTargets()
        {
            GameObject activeCharacter = GetActivePlacementModel();
            if (activeCharacter == null)
            {
                return Array.Empty<Object>();
            }

            List<Object> selection = new List<Object> { activeCharacter };

            if (PrefabUtility.IsPartOfPrefabInstance(activeCharacter))
            {
                GameObject sourceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(activeCharacter);
                Object sourceAsset = sourceRoot != null ? PrefabUtility.GetCorrespondingObjectFromSource(sourceRoot) : null;
                if (sourceAsset != null && !selection.Contains(sourceAsset))
                {
                    selection.Add(sourceAsset);
                }
            }

            return selection.ToArray();
        }

        private static string BuildSafeAssetName(string rawName)
        {
            string source = string.IsNullOrWhiteSpace(rawName) ? "HoyoToonPrefab" : rawName.Trim();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] safeChars = source.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray();
            return new string(safeChars);
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyWidthClass(evt.newRect.width);
        }

        private void ApplyWidthClass(float width)
        {
            if (shellRoot == null)
            {
                return;
            }

            shellRoot.RemoveFromClassList(WidthNarrowClass);
            shellRoot.RemoveFromClassList(WidthStandardClass);
            shellRoot.RemoveFromClassList(WidthWideClass);

            if (width <= 359f)
            {
                shellRoot.AddToClassList(WidthNarrowClass);
            }
            else if (width <= 619f)
            {
                shellRoot.AddToClassList(WidthStandardClass);
            }
            else
            {
                shellRoot.AddToClassList(WidthWideClass);
            }
        }

        private static T LoadAsset<T>(string assetPath) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                Debug.LogError("HoyoToon Manager could not load asset at path: " + assetPath);
            }

            return asset;
        }

        private static string GetOnboardingTabTargetId(string moduleId)
        {
            switch (moduleId)
            {
                case "assets":
                    return "Manager.Nav.AssetsTab";
                case "setup":
                    return "Manager.Nav.SetupTab";
                case "character":
                    return "Manager.Nav.CharacterTab";
                case "scene":
                    return "Manager.Nav.SceneTab";
                case "renders":
                    return "Manager.Nav.RenderTab";
                default:
                    return "Manager.Nav." + moduleId + "Tab";
            }
        }
    }
}
