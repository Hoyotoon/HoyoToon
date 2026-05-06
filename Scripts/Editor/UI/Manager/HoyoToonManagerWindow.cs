using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.API;
using HoyoToon.Editor.API.Users;
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
using HoyoToon.Runtime.ScriptableObjects.Users;
using HoyoToon.Runtime.Scene.Environment;
using HoyoToon.Runtime.Scene.Placement;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HoyoToon.Editor.UI.Manager
{
    public sealed class HoyoToonManagerWindow : EditorWindow
    {
        private const string MenuPath = "HoyoToon/Manager";
        private const string PackageAssetRoot = "Packages/com.hoyotoon.hoyotoon/Scripts/Editor/UI/Manager";
        private const string AvatarCacheRelativePath = "Library/HoyoToon/UserAvatarCache";
        private const string UxmlAssetPath = PackageAssetRoot + "/UXML/HoyoToonWindow.uxml";
        private const string ThemeAssetPath = PackageAssetRoot + "/USS/HoyoToonTheme.uss";
        private const string LayoutAssetPath = PackageAssetRoot + "/USS/HoyoToonLayout.uss";
        private const string ComponentsAssetPath = PackageAssetRoot + "/USS/HoyoToonComponents.uss";
        private const string HeaderBackgroundAssetPath = "Packages/com.hoyotoon.hoyotoon/Resources/UI/background.png";
        private const string HeaderLogoAssetPath = "Packages/com.hoyotoon.hoyotoon/Resources/UI/hoyotoon.png";
        private const string LastCharacterSplashAssetPathSessionKey = "HoyoToon.Manager.LastCharacterSplashAssetPath";
        private const string LastCharacterSplashModelAssetPathSessionKey = "HoyoToon.Manager.LastCharacterSplashModelAssetPath";
        private const double CharacterSplashRefreshIntervalSeconds = 0.15d;
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
        private const double RemoteAvatarRetryDelaySeconds = 30d;
        private const double ValueInteractionRefreshQuietSeconds = 0.3d;
        private const long ValueInteractionRefreshPollMilliseconds = 50L;
        private const int MaxRemoteAvatarPayloadBytes = 2 * 1024 * 1024;
        private const int MaxRemoteAvatarTextureCacheEntries = 8;

        private static readonly Vector2 MinimumWindowSize = new Vector2(360f, 520f);
        private static readonly Dictionary<string, Texture2D> RemoteAvatarTextures =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static readonly HashSet<string> RemoteAvatarLoads =
            new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> FailedRemoteAvatarRetryTimes =
            new Dictionary<string, double>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> RemoteAvatarLastUsedTimes =
            new Dictionary<string, double>(StringComparer.Ordinal);

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
        private Image characterSplashImage;
        private Button versionBadgeButton;
        private Image logoImage;
        private VisualElement userProfileButton;
        private Image userAvatarImage;
        private Label userAvatarInitials;
        private Label userProfileName;
        private Label userProfileRole;
        private Label userProfileUid;
        private Label userProfileTagline;
        private VisualElement globalContextHost;
        private Button createPrefabButton;
        private Button regenerateMaterialsButton;
        private Button regenerateTangentsButton;
        private IVisualElementScheduledItem versionBadgeSchedule;
        private IVisualElementScheduledItem managerValueInteractionRefreshSchedule;
        private bool userProfilePromptOpen;
        private bool userProfileRestoreRequested;
        private bool userProfileRestoreAttempted;
        private bool userProfileRefreshRequested;
        private bool userProfileRefreshAttempted;
        private bool managerRefreshQueued;
        private bool managerValueInteractionActive;
        private bool managerRefreshDeferredByValueInteraction;
        private int managerValueInteractionPointerId = -1;
        private double managerValueInteractionQuietUntil;
        private string lastCharacterSplashAssetPath = string.Empty;
        private string lastCharacterSplashModelAssetPath = string.Empty;
        private int lastCharacterSplashModelInstanceId;
        private int observedCharacterSplashPlacementControllerId;
        private int observedCharacterSplashActiveModelId;
        private int observedCharacterSplashActiveModelIndex = -1;
        private int observedCharacterSplashInputSwitchVersion = -1;
        private double nextCharacterSplashRefreshTime;

        internal string ActiveModuleId => activeModule != null ? activeModule.Id : string.Empty;
        internal ModuleContext CurrentContext => currentContext;

        [InitializeOnLoadMethod]
        private static void RegisterRemoteAvatarCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ClearAllRemoteAvatarTextures;
            AssemblyReloadEvents.beforeAssemblyReload += ClearAllRemoteAvatarTextures;
            EditorApplication.quitting -= ClearAllRemoteAvatarTextures;
            EditorApplication.quitting += ClearAllRemoteAvatarTextures;
        }

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
            HoyoToonUserProfileStorage.ProfileChanged -= HandleUserProfileChanged;
            HoyoToonUserProfileStorage.ProfileChanged += HandleUserProfileChanged;
            CharacterIconCacheUtility.CharacterIconsCached -= HandleCharacterIconsCached;
            CharacterIconCacheUtility.CharacterIconsCached += HandleCharacterIconsCached;
            Undo.undoRedoPerformed -= HandleUndoRedoPerformed;
            Undo.undoRedoPerformed += HandleUndoRedoPerformed;
            EditorApplication.hierarchyChanged -= HandleEditorContextChanged;
            EditorApplication.hierarchyChanged += HandleEditorContextChanged;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= RefreshCharacterSplashArtworkForPlacementChanges;
            EditorApplication.update += RefreshCharacterSplashArtworkForPlacementChanges;
        }

        private void OnDisable()
        {
            versionBadgeSchedule?.Pause();
            managerValueInteractionRefreshSchedule?.Pause();
            HoyoToonUserProfileStorage.ProfileChanged -= HandleUserProfileChanged;
            CharacterIconCacheUtility.CharacterIconsCached -= HandleCharacterIconsCached;
            Undo.undoRedoPerformed -= HandleUndoRedoPerformed;
            EditorApplication.hierarchyChanged -= HandleEditorContextChanged;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.update -= RefreshCharacterSplashArtworkForPlacementChanges;
            EditorApplication.delayCall -= RefreshManagerContextAfterEditorChange;
            managerRefreshQueued = false;
            managerValueInteractionActive = false;
            managerRefreshDeferredByValueInteraction = false;
            managerValueInteractionPointerId = -1;
            managerValueInteractionRefreshSchedule = null;
            managerValueInteractionQuietUntil = 0d;
            activeModule?.OnDeselected(currentContext);
            OnboardingManager.NotifyManagerClosed(this);
        }

        private void OnFocus()
        {
            RequestDeferredManagerRefresh();
            UpdateVersionBadge();
            RefreshUserProfileHeader();
        }

        private void HandleUndoRedoPerformed()
        {
            RequestDeferredManagerRefresh();
        }

        private void HandleEditorContextChanged()
        {
            RequestDeferredManagerRefresh();
        }

        private void HandleCharacterIconsCached(string contextAssetPath)
        {
            if (this == null || characterSplashImage == null || !IsCurrentCharacterSplashContext(contextAssetPath))
            {
                return;
            }

            ApplyActiveCharacterSplashArtwork();
            Repaint();
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            ResetObservedCharacterSplashState();
            RequestDeferredManagerRefresh();
        }

        private void RequestDeferredManagerRefresh()
        {
            if (IsManagerRefreshBlockedByValueInteraction())
            {
                TryRefreshCharacterSplashArtworkFromLivePlacement();
                managerRefreshDeferredByValueInteraction = true;
                return;
            }

            if (managerRefreshQueued)
            {
                return;
            }

            managerRefreshQueued = true;
            EditorApplication.delayCall += RefreshManagerContextAfterEditorChange;
        }

        private void RefreshManagerContextAfterEditorChange()
        {
            managerRefreshQueued = false;
            if (this == null)
            {
                return;
            }

            if (IsManagerRefreshBlockedByValueInteraction())
            {
                TryRefreshCharacterSplashArtworkFromLivePlacement();
                managerRefreshDeferredByValueInteraction = true;
                return;
            }

            RefreshManualContext();
            UpdateVersionBadge();
            RefreshUserProfileHeader();
        }

        public void CreateGUI()
        {
            IManagerModule previousActiveModule = activeModule;
            string activeModuleId = previousActiveModule != null ? previousActiveModule.Id : string.Empty;
            previousActiveModule?.OnDeselected(currentContext);

            modules = ModuleRegistry.CreateModules();
            IManagerModule initialModule = modules.FirstOrDefault(module => string.Equals(module.Id, activeModuleId, StringComparison.Ordinal))
                ?? modules.FirstOrDefault();
            activeModule = null;

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
            RefreshUserProfileHeader();
            StartVersionBadgeSchedule();
            ApplyWidthClass(position.width);
            SetActiveModule(initialModule != null ? initialModule.Id : string.Empty, true);
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
            shellRoot.RegisterCallback<PointerDownEvent>(HandleManagerValuePointerDown, TrickleDown.TrickleDown);
            shellRoot.RegisterCallback<PointerUpEvent>(HandleManagerValuePointerUp, TrickleDown.TrickleDown);
            shellRoot.RegisterCallback<PointerCancelEvent>(HandleManagerValuePointerCancel, TrickleDown.TrickleDown);
            RegisterManagerValueChangeGuards(shellRoot);

            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(shellRoot);
            shellRoot.StretchToParentSize();
            shellRoot.style.flexGrow = 1f;

            ScrollView shellScrollView = shellRoot.Q<ScrollView>("ShellScrollView");
            if (shellScrollView != null)
            {
                shellScrollView.mode = ScrollViewMode.Vertical;
                shellScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                shellScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
                shellScrollView.contentContainer.style.flexDirection = FlexDirection.Column;
                shellScrollView.contentContainer.style.flexGrow = 1f;
                shellScrollView.contentContainer.style.minHeight = 0f;
            }

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
            characterSplashImage = shellRoot.Q<Image>("CharacterSplashImage");
            versionBadgeButton = shellRoot.Q<Button>("VersionBadge");
            logoImage = shellRoot.Q<Image>("LogoImage");
            userProfileButton = shellRoot.Q<VisualElement>("UserProfileButton");
            userAvatarImage = shellRoot.Q<Image>("UserAvatarImage");
            userAvatarInitials = shellRoot.Q<Label>("UserAvatarInitials");
            userProfileName = shellRoot.Q<Label>("UserProfileName");
            userProfileRole = shellRoot.Q<Label>("UserProfileRole");
            userProfileUid = shellRoot.Q<Label>("UserProfileUid");
            userProfileTagline = shellRoot.Q<Label>("UserProfileTagline");
            globalContextHost = shellRoot.Q<VisualElement>("GlobalContextHost");
            createPrefabButton = shellRoot.Q<Button>("CreatePrefabButton");
            regenerateMaterialsButton = shellRoot.Q<Button>("RegenerateMaterialsButton");
            regenerateTangentsButton = shellRoot.Q<Button>("RegenerateTangentsButton");

            if (versionBadgeButton != null)
            {
                versionBadgeButton.clicked += HandleVersionBadgeClicked;
            }

            if (userProfileButton != null)
            {
                userProfileButton.RegisterCallback<ClickEvent>(HandleUserProfileClicked);
            }

            if (userProfileUid != null)
            {
                userProfileUid.RegisterCallback<ClickEvent>(HandleUserProfileUidClicked);
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
            placementController.ApplyNow();
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

            if (ShouldClearQueuedModelsAfterAutoSetup(result))
            {
                ClearQueuedModelSelection();
            }
            else
            {
                RefreshDetectionState();
                RefreshManualContext();
            }

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

        private void ClearQueuedModelSelection()
        {
            batchModelAssets.Clear();
            selectedModelAsset = null;
            selectedModelAssetPath = string.Empty;
            selectedModelFolder = null;
            selectedModelFolderPath = string.Empty;
            validationMessage = string.Empty;
            ClearDetectionState();
            RefreshManualContext();
        }

        private static bool ShouldClearQueuedModelsAfterAutoSetup(AutoSetupResult result)
        {
            return result != null
                && result.Succeeded
                && result.ExecutedFeatures.Count > 0;
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

        private void RefreshUserProfileHeader()
        {
            if (userProfileButton == null)
            {
                return;
            }

            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            bool hasProfile = HoyoToonUserProfileStorage.IsComplete(profile);
            if (hasProfile)
            {
                TryRefreshUserProfileForHeader(profile);
            }
            else
            {
                TryRestoreUserProfileForHeader();
            }

            string displayName = hasProfile
                ? profile.Username
                : userProfileRestoreRequested
                    ? "Restoring profile"
                    : HoyoToonUserProfileService.IsCreating
                        ? "Creating profile"
                        : "Set profile";
            string uidText = hasProfile ? "UID: " + profile.UID : "UID: Not assigned";
            string roleName = hasProfile && !string.IsNullOrWhiteSpace(profile.RoleName)
                ? profile.RoleName.Trim()
                : HoyoToonApi.DefaultUserRoleName;
            string roleColor = hasProfile && !string.IsNullOrWhiteSpace(profile.RoleColor)
                ? profile.RoleColor.Trim()
                : HoyoToonApi.DefaultUserRoleColor;
            Color resolvedRoleColor = ResolveUserRoleColor(roleColor);
            string taglineText = hasProfile
                ? HoyoToonUserProfileService.IsUpdatingAvatar
                    ? "Updating avatar URL..."
                    : "Click to update avatar URL."
                : userProfileRestoreRequested
                    ? "Checking your saved HoyoToon profile..."
                    : HoyoToonUserProfileService.IsCreating
                        ? "Reserving your HoyoToon UID..."
                        : "Click to create your HoyoToon profile.";
            Texture2D avatarTexture = hasProfile ? ResolveUserAvatarTexture(profile.Avatar) : null;

            userProfileButton.tooltip = hasProfile
                ? "HoyoToon profile\nUsername: " + profile.Username + "\nUID: " + profile.UID + "\nClick to update avatar URL."
                : "Create your local HoyoToon profile.";
            userProfileButton.SetEnabled(!HoyoToonUserProfileService.IsCreating
                && !HoyoToonUserProfileService.IsUpdatingAvatar
                && !userProfileRestoreRequested);

            if (userProfileName != null)
            {
                userProfileName.text = displayName;
            }

            if (userProfileRole != null)
            {
                userProfileRole.text = hasProfile ? roleName : string.Empty;
                userProfileRole.style.display = hasProfile ? DisplayStyle.Flex : DisplayStyle.None;
                userProfileRole.style.color = resolvedRoleColor;
                userProfileRole.tooltip = hasProfile ? roleName : string.Empty;
            }

            if (userProfileUid != null)
            {
                userProfileUid.text = uidText;
                userProfileUid.tooltip = hasProfile ? "Click to copy UID." : "UID is not assigned yet.";
            }

            if (userProfileTagline != null)
            {
                userProfileTagline.text = taglineText;
            }

            if (userAvatarImage != null)
            {
                userAvatarImage.image = avatarTexture;
                userAvatarImage.scaleMode = ScaleMode.ScaleAndCrop;
                userAvatarImage.style.display = avatarTexture != null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (userAvatarInitials != null)
            {
                userAvatarInitials.text = BuildUserInitials(displayName);
                userAvatarInitials.style.display = avatarTexture == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void TryRestoreUserProfileForHeader()
        {
            if (userProfileRestoreRequested
                || userProfileRestoreAttempted
                || !HoyoToonUserProfileGlobalStore.TryLoad(out _))
            {
                return;
            }

            userProfileRestoreAttempted = true;
            userProfileRestoreRequested = true;
            _ = RestoreUserProfileForHeaderAsync();
        }

        private async Task RestoreUserProfileForHeaderAsync()
        {
            try
            {
                await HoyoToonUserProfileService.RestoreLocalUserProfileAsync(CancellationToken.None);
                userProfileRefreshAttempted = true;
            }
            catch
            {
            }
            finally
            {
                userProfileRestoreRequested = false;
                RefreshUserProfileHeader();
                Repaint();
            }
        }

        private void TryRefreshUserProfileForHeader(HoyoToonUserProfileSO profile)
        {
            if (userProfileRefreshRequested
                || userProfileRefreshAttempted
                || !HoyoToonUserProfileStorage.IsComplete(profile))
            {
                return;
            }

            userProfileRefreshAttempted = true;
            userProfileRefreshRequested = true;
            _ = RefreshUserProfileForHeaderAsync(profile);
        }

        private async Task RefreshUserProfileForHeaderAsync(HoyoToonUserProfileSO profile)
        {
            try
            {
                await HoyoToonUserProfileService.RefreshLocalProfileFromApiAsync(profile, CancellationToken.None);
            }
            catch
            {
            }
            finally
            {
                userProfileRefreshRequested = false;
                RefreshUserProfileHeader();
                Repaint();
            }
        }

        private void HandleUserProfileChanged()
        {
            RefreshUserProfileHeader();
            Repaint();
        }

        private void HandleUserProfileClicked(ClickEvent evt)
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (HoyoToonUserProfileStorage.IsComplete(profile))
            {
                if (userProfilePromptOpen || HoyoToonUserProfileService.IsUpdatingAvatar)
                {
                    return;
                }

                userProfilePromptOpen = true;
                RefreshUserProfileHeader();
                HoyoToonUserProfilePrompt.ShowAvatarEditor(
                    profile,
                    avatarUrl => _ = UpdateUserAvatarFromHeaderAsync(avatarUrl),
                    () =>
                    {
                        userProfilePromptOpen = false;
                        RefreshUserProfileHeader();
                    });
                return;
            }

            if (userProfilePromptOpen
                || HoyoToonUserProfileService.IsCreating
                || HoyoToonUserProfileService.IsUpdatingAvatar)
            {
                return;
            }

            userProfilePromptOpen = true;
            RefreshUserProfileHeader();
            HoyoToonUserProfilePrompt.Show(
                username => _ = CreateUserProfileFromHeaderAsync(username),
                () =>
                {
                    userProfilePromptOpen = false;
                    RefreshUserProfileHeader();
                });
        }

        private void HandleUserProfileUidClicked(ClickEvent evt)
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            if (!HoyoToonUserProfileStorage.IsComplete(profile))
            {
                return;
            }

            evt?.StopPropagation();
            EditorGUIUtility.systemCopyBuffer = profile.UID;
            ShowNotification(new GUIContent("Copied UID " + profile.UID));
        }

        private async Task CreateUserProfileFromHeaderAsync(string username)
        {
            try
            {
                await HoyoToonUserProfileService.CreateLocalUserProfileAsync(username, CancellationToken.None);
            }
            catch (Exception exception)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(
                    "Create HoyoToon Profile",
                    exception.Message,
                    "OK");
            }
            finally
            {
                RefreshUserProfileHeader();
            }
        }

        private async Task UpdateUserAvatarFromHeaderAsync(string avatarUrl)
        {
            HoyoToonUserProfileSO profile = HoyoToonUserProfileStorage.GetLocalProfile();
            string previousAvatar = profile != null ? profile.Avatar : string.Empty;
            if (HoyoToonUserProfileService.TryNormalizeAvatarUrl(
                avatarUrl,
                out string normalizedAvatarUrl,
                out _))
            {
                ClearRemoteUserAvatarCache(normalizedAvatarUrl);
            }

            try
            {
                HoyoToonUserProfileSO updatedProfile = await HoyoToonUserProfileService.UpdateLocalUserAvatarAsync(
                    profile,
                    avatarUrl,
                    CancellationToken.None);
                ClearRemoteUserAvatarCache(previousAvatar);
                ClearRemoteUserAvatarCache(updatedProfile != null ? updatedProfile.Avatar : avatarUrl);
            }
            catch (Exception exception)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(
                    "Update HoyoToon Avatar",
                    exception.Message,
                    "OK");
            }
            finally
            {
                RefreshUserProfileHeader();
            }
        }

        private Texture2D ResolveUserAvatarTexture(string avatar)
        {
            if (string.IsNullOrWhiteSpace(avatar))
            {
                return null;
            }

            string normalizedAvatar = avatar.Trim().Replace('\\', '/');
            if (normalizedAvatar.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || normalizedAvatar.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (RemoteAvatarTextures.TryGetValue(normalizedAvatar, out Texture2D remoteTexture))
                {
                    if (remoteTexture != null)
                    {
                        TouchRemoteAvatarTexture(normalizedAvatar);
                        return remoteTexture;
                    }

                    RemoteAvatarTextures.Remove(normalizedAvatar);
                    RemoteAvatarLastUsedTimes.Remove(normalizedAvatar);
                }

                if (TryLoadCachedRemoteAvatarTexture(normalizedAvatar, out Texture2D cachedTexture))
                {
                    AddRemoteAvatarTexture(normalizedAvatar, cachedTexture);
                    return cachedTexture;
                }

                if (FailedRemoteAvatarRetryTimes.TryGetValue(normalizedAvatar, out double retryTime))
                {
                    if (EditorApplication.timeSinceStartup < retryTime)
                    {
                        return null;
                    }

                    FailedRemoteAvatarRetryTimes.Remove(normalizedAvatar);
                }

                BeginRemoteUserAvatarLoad(normalizedAvatar);
                return null;
            }

            if (normalizedAvatar.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || normalizedAvatar.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                Texture2D assetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedAvatar);
                if (assetTexture != null)
                {
                    return assetTexture;
                }
            }

            const string ResourcesSegment = "/Resources/";
            int resourcesIndex = normalizedAvatar.IndexOf(ResourcesSegment, StringComparison.OrdinalIgnoreCase);
            if (resourcesIndex >= 0)
            {
                normalizedAvatar = normalizedAvatar.Substring(resourcesIndex + ResourcesSegment.Length);
            }

            string resourceKey = Path.ChangeExtension(normalizedAvatar, null);
            return UnityEngine.Resources.Load<Texture2D>(resourceKey)
                ?? UnityEngine.Resources.Load<Texture2D>("UI/" + resourceKey);
        }

        private void BeginRemoteUserAvatarLoad(string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl) || !RemoteAvatarLoads.Add(avatarUrl))
            {
                return;
            }

            UnityWebRequest request = UnityWebRequest.Get(avatarUrl);
            request.timeout = 15;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (!IsUnityWebRequestFailed(request))
                    {
                        byte[] payload = request.downloadHandler != null ? request.downloadHandler.data : null;
                        if (TryCreateRemoteAvatarTexture(payload, out Texture2D texture))
                        {
                            ClearRemoteAvatarTextureOnly(avatarUrl);
                            AddRemoteAvatarTexture(avatarUrl, texture);
                            FailedRemoteAvatarRetryTimes.Remove(avatarUrl);
                            TrySaveRemoteAvatarCache(avatarUrl, payload);
                        }
                        else
                        {
                            MarkRemoteAvatarLoadFailed(avatarUrl);
                        }
                    }
                    else
                    {
                        MarkRemoteAvatarLoadFailed(avatarUrl);
                    }
                }
                finally
                {
                    RemoteAvatarLoads.Remove(avatarUrl);
                    request.Dispose();
                    RefreshUserProfileHeader();
                    Repaint();
                }
            };
        }

        private static void ClearRemoteUserAvatarCache(string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                return;
            }

            string normalizedAvatar = avatarUrl.Trim().Replace('\\', '/');
            ClearRemoteAvatarTextureOnly(normalizedAvatar);
            RemoteAvatarLoads.Remove(normalizedAvatar);
            FailedRemoteAvatarRetryTimes.Remove(normalizedAvatar);
            DeleteRemoteAvatarCache(normalizedAvatar);
        }

        private static void ClearAllRemoteAvatarTextures()
        {
            foreach (Texture2D texture in RemoteAvatarTextures.Values)
            {
                DestroyRemoteAvatarTexture(texture);
            }

            RemoteAvatarTextures.Clear();
            RemoteAvatarLoads.Clear();
            FailedRemoteAvatarRetryTimes.Clear();
            RemoteAvatarLastUsedTimes.Clear();
        }

        private static void AddRemoteAvatarTexture(string avatarUrl, Texture2D texture)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl) || texture == null)
            {
                DestroyRemoteAvatarTexture(texture);
                return;
            }

            string normalizedAvatar = avatarUrl.Trim().Replace('\\', '/');
            if (RemoteAvatarTextures.TryGetValue(normalizedAvatar, out Texture2D existingTexture)
                && existingTexture != texture)
            {
                DestroyRemoteAvatarTexture(existingTexture);
            }

            RemoteAvatarTextures[normalizedAvatar] = texture;
            TouchRemoteAvatarTexture(normalizedAvatar);
            EnforceRemoteAvatarTextureCacheLimit();
        }

        private static void TouchRemoteAvatarTexture(string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                return;
            }

            RemoteAvatarLastUsedTimes[avatarUrl.Trim().Replace('\\', '/')] = EditorApplication.timeSinceStartup;
        }

        private static void EnforceRemoteAvatarTextureCacheLimit()
        {
            while (RemoteAvatarTextures.Count > MaxRemoteAvatarTextureCacheEntries)
            {
                string oldestAvatar = null;
                double oldestUsedTime = double.MaxValue;
                foreach (string avatarUrl in RemoteAvatarTextures.Keys)
                {
                    double usedTime = RemoteAvatarLastUsedTimes.TryGetValue(avatarUrl, out double recordedTime)
                        ? recordedTime
                        : 0d;
                    if (oldestAvatar == null || usedTime < oldestUsedTime)
                    {
                        oldestAvatar = avatarUrl;
                        oldestUsedTime = usedTime;
                    }
                }

                if (string.IsNullOrWhiteSpace(oldestAvatar))
                {
                    return;
                }

                ClearRemoteAvatarTextureOnly(oldestAvatar);
            }
        }

        private static void ClearRemoteAvatarTextureOnly(string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                return;
            }

            string normalizedAvatar = avatarUrl.Trim().Replace('\\', '/');
            if (RemoteAvatarTextures.TryGetValue(normalizedAvatar, out Texture2D texture))
            {
                DestroyRemoteAvatarTexture(texture);
            }

            RemoteAvatarTextures.Remove(normalizedAvatar);
            RemoteAvatarLastUsedTimes.Remove(normalizedAvatar);
        }

        private static void MarkRemoteAvatarLoadFailed(string avatarUrl)
        {
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                return;
            }

            FailedRemoteAvatarRetryTimes[avatarUrl] = EditorApplication.timeSinceStartup + RemoteAvatarRetryDelaySeconds;
        }

        private static bool TryLoadCachedRemoteAvatarTexture(string avatarUrl, out Texture2D texture)
        {
            texture = null;
            if (!TryGetRemoteAvatarCachePath(avatarUrl, out string cachePath) || !File.Exists(cachePath))
            {
                return false;
            }

            try
            {
                if (new FileInfo(cachePath).Length > MaxRemoteAvatarPayloadBytes)
                {
                    File.Delete(cachePath);
                    return false;
                }

                byte[] payload = File.ReadAllBytes(cachePath);
                return TryCreateRemoteAvatarTexture(payload, out texture);
            }
            catch
            {
                return false;
            }
        }

        private static void TrySaveRemoteAvatarCache(string avatarUrl, byte[] payload)
        {
            if (payload == null
                || payload.Length <= 0
                || payload.Length > MaxRemoteAvatarPayloadBytes
                || !TryGetRemoteAvatarCachePath(avatarUrl, out string cachePath))
            {
                return;
            }

            try
            {
                string cacheDirectory = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrWhiteSpace(cacheDirectory))
                {
                    Directory.CreateDirectory(cacheDirectory);
                }

                File.WriteAllBytes(cachePath, payload);
            }
            catch
            {
            }
        }

        private static void DeleteRemoteAvatarCache(string avatarUrl)
        {
            if (!TryGetRemoteAvatarCachePath(avatarUrl, out string cachePath))
            {
                return;
            }

            try
            {
                if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }
            }
            catch
            {
            }
        }

        private static bool TryCreateRemoteAvatarTexture(byte[] payload, out Texture2D texture)
        {
            texture = null;
            if (payload == null || payload.Length <= 0 || payload.Length > MaxRemoteAvatarPayloadBytes)
            {
                return false;
            }

            Texture2D loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "HoyoToon User Avatar",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (!ImageConversion.LoadImage(loadedTexture, payload))
            {
                DestroyRemoteAvatarTexture(loadedTexture);
                return false;
            }

            texture = loadedTexture;
            return true;
        }

        private static void DestroyRemoteAvatarTexture(Texture2D texture)
        {
            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool TryGetRemoteAvatarCachePath(string avatarUrl, out string cachePath)
        {
            cachePath = string.Empty;
            if (string.IsNullOrWhiteSpace(avatarUrl) || string.IsNullOrWhiteSpace(Application.dataPath))
            {
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string cacheDirectory = Path.GetFullPath(Path.Combine(projectRoot, AvatarCacheRelativePath));
            cachePath = Path.Combine(cacheDirectory, ComputeStableHash(avatarUrl) + ".bytes");
            return true;
        }

        private static string ComputeStableHash(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
            {
                builder.Append(hash[index].ToString("x2"));
            }

            return builder.ToString();
        }

        private static Color ResolveUserRoleColor(string roleColor)
        {
            if (!string.IsNullOrWhiteSpace(roleColor)
                && ColorUtility.TryParseHtmlString(roleColor.Trim(), out Color parsedColor))
            {
                return parsedColor;
            }

            ColorUtility.TryParseHtmlString(HoyoToonApi.DefaultUserRoleColor, out Color fallbackColor);
            return fallbackColor;
        }

        private static bool IsUnityWebRequestFailed(UnityWebRequest request)
        {
            if (request == null)
            {
                return true;
            }

#if UNITY_2020_2_OR_NEWER
            return request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError
                || request.result == UnityWebRequest.Result.DataProcessingError;
#else
            return request.isNetworkError || request.isHttpError;
#endif
        }

        private static string BuildUserInitials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            string[] parts = displayName
                .Split(new[] { ' ', '\t', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 0)
            {
                return displayName.Substring(0, 1).ToUpperInvariant();
            }

            string first = parts[0].Substring(0, 1);
            string second = parts.Length > 1 ? parts[1].Substring(0, 1) : string.Empty;
            return (first + second).ToUpperInvariant();
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

            ApplyActiveCharacterSplashArtwork();
        }

        private void ApplyActiveCharacterSplashArtwork()
        {
            if (characterSplashImage == null)
            {
                return;
            }

            GameObject activeModel = ResolveLivePlacementActiveModel();
            Texture2D splashTexture = ResolveActiveCharacterSplashTexture(activeModel, out string activeModelAssetPath);
            if (splashTexture == null && ShouldUseRememberedCharacterSplash(activeModel, activeModelAssetPath))
            {
                splashTexture = ResolveRememberedCharacterSplashTexture();
            }

            characterSplashImage.image = splashTexture;
            characterSplashImage.scaleMode = ScaleMode.ScaleToFit;
            characterSplashImage.style.display = splashTexture != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private Texture2D ResolveActiveCharacterSplashTexture(GameObject activeModel, out string activeModelAssetPath)
        {
            activeModelAssetPath = string.Empty;
            if (activeModel == null)
            {
                return null;
            }

            foreach (string contextAssetPath in EnumerateCharacterSplashContextAssetPaths(activeModel))
            {
                string gameKey = ResolveCharacterSplashGameKey(activeModel, contextAssetPath);
                if (string.IsNullOrWhiteSpace(gameKey))
                {
                    continue;
                }

                Texture2D splashTexture = ResolveCharacterSplashTexture(gameKey, contextAssetPath, out string splashAssetPath);
                if (splashTexture == null)
                {
                    splashTexture = ResolveCharacterSplashTextureByName(
                        activeModel,
                        gameKey,
                        contextAssetPath,
                        out splashAssetPath);
                }

                if (splashTexture == null)
                {
                    continue;
                }

                activeModelAssetPath = contextAssetPath;
                RememberCharacterSplashAssetPath(activeModel.GetInstanceID(), contextAssetPath, splashAssetPath);
                return splashTexture;
            }

            return null;
        }

        private Texture2D ResolveCharacterSplashTexture(string gameKey, string contextAssetPath, out string splashAssetPath)
        {
            splashAssetPath = string.Empty;
            CharacterIconDetector.CharacterIconResolutionResult result =
                CharacterIconDetector.ResolveCharacterIcon(gameKey, contextAssetPath);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.SplashIconUrl))
            {
                return null;
            }

            if (!CharacterIconCacheUtility.TryGetCachedCharacterIconPath(
                contextAssetPath,
                null,
                null,
                result.SplashIconUrl,
                out string cachedSplashIconPath))
            {
                return null;
            }

            splashAssetPath = AssetContextJsonQueryUtility.ToAssetPath(cachedSplashIconPath);
            if (string.IsNullOrWhiteSpace(splashAssetPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(splashAssetPath);
        }

        private Texture2D ResolveCharacterSplashTextureByName(
            GameObject activeModel,
            string gameKey,
            string contextAssetPath,
            out string splashAssetPath)
        {
            splashAssetPath = string.Empty;
            string characterName = NormalizeSceneCharacterName(ResolvePlacementCharacterName(activeModel));
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return null;
            }

            string resolvedGameKey = gameKey;
            int characterId = 0;
            string avatarIconUrl = null;
            string roundIconUrl = null;
            string splashIconUrl = null;
            bool resolved = false;
            if (!string.IsNullOrWhiteSpace(resolvedGameKey))
            {
                resolved = CharacterIconDetector.TryResolveCharacterIconUrls(
                    resolvedGameKey,
                    characterName,
                    out characterId,
                    out avatarIconUrl,
                    out roundIconUrl,
                    out splashIconUrl);
            }

            if (!resolved)
            {
                resolved = CharacterIconDetector.TryResolveCharacterIconUrls(
                    characterName,
                    out resolvedGameKey,
                    out characterId,
                    out avatarIconUrl,
                    out roundIconUrl,
                    out splashIconUrl);
            }

            if (!resolved || string.IsNullOrWhiteSpace(resolvedGameKey) || string.IsNullOrWhiteSpace(splashIconUrl))
            {
                return null;
            }

            CharacterIconCacheUtility.TryEnsureCharacterIconsCached(
                contextAssetPath,
                resolvedGameKey,
                characterId,
                avatarIconUrl,
                roundIconUrl,
                splashIconUrl,
                out _);

            if (!CharacterIconCacheUtility.TryGetCachedCharacterIconPath(
                contextAssetPath,
                null,
                null,
                splashIconUrl,
                out string cachedSplashIconPath))
            {
                return null;
            }

            splashAssetPath = AssetContextJsonQueryUtility.ToAssetPath(cachedSplashIconPath);
            return string.IsNullOrWhiteSpace(splashAssetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(splashAssetPath);
        }

        private string ResolveCharacterSplashGameKey(GameObject activeModel, string contextAssetPath)
        {
            string gameKey = currentContext != null && activeModel == currentContext.PlacementActiveModel
                ? currentContext.PlacementActiveGameKey
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(gameKey))
            {
                return gameKey;
            }

            if (GameDetector.TryDetectGameFromAssetContext(contextAssetPath, out GameConfigSO game, out _)
                && game != null)
            {
                return game.Key ?? string.Empty;
            }

            return ResolvePlacementGameKey(activeModel);
        }

        private bool IsCurrentCharacterSplashContext(string contextAssetPath)
        {
            if (string.IsNullOrWhiteSpace(contextAssetPath))
            {
                return true;
            }

            GameObject activeModel = ResolveLivePlacementActiveModel();
            if (activeModel == null)
            {
                return false;
            }

            string normalizedContextAssetPath = AssetContextJsonQueryUtility.NormalizeAssetPath(contextAssetPath);
            return EnumerateCharacterSplashContextAssetPaths(activeModel)
                .Any(assetPath => string.Equals(
                    AssetContextJsonQueryUtility.NormalizeAssetPath(assetPath),
                    normalizedContextAssetPath,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeSceneCharacterName(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return string.Empty;
            }

            string normalizedName = characterName.Trim();
            const string CloneSuffix = "(Clone)";
            if (normalizedName.EndsWith(CloneSuffix, StringComparison.OrdinalIgnoreCase))
            {
                normalizedName = normalizedName.Substring(0, normalizedName.Length - CloneSuffix.Length).Trim();
            }

            return normalizedName;
        }

        private void RememberCharacterSplashAssetPath(int modelInstanceId, string modelAssetPath, string splashAssetPath)
        {
            if (string.IsNullOrWhiteSpace(modelAssetPath) || string.IsNullOrWhiteSpace(splashAssetPath))
            {
                return;
            }

            lastCharacterSplashModelInstanceId = modelInstanceId;
            lastCharacterSplashModelAssetPath = modelAssetPath;
            lastCharacterSplashAssetPath = splashAssetPath;
            SessionState.SetString(LastCharacterSplashModelAssetPathSessionKey, modelAssetPath);
            SessionState.SetString(LastCharacterSplashAssetPathSessionKey, splashAssetPath);
        }

        private bool ShouldUseRememberedCharacterSplash(GameObject activeModel, string activeModelAssetPath)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            if (activeModel != null
                && lastCharacterSplashModelInstanceId != 0
                && activeModel.GetInstanceID() == lastCharacterSplashModelInstanceId)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(activeModelAssetPath))
            {
                return activeModel == null;
            }

            string rememberedModelAssetPath = GetRememberedCharacterSplashModelAssetPath();
            return !string.IsNullOrWhiteSpace(rememberedModelAssetPath)
                && string.Equals(activeModelAssetPath, rememberedModelAssetPath, StringComparison.OrdinalIgnoreCase);
        }

        private Texture2D ResolveRememberedCharacterSplashTexture()
        {
            string splashAssetPath = !string.IsNullOrWhiteSpace(lastCharacterSplashAssetPath)
                ? lastCharacterSplashAssetPath
                : SessionState.GetString(LastCharacterSplashAssetPathSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(splashAssetPath))
            {
                return null;
            }

            Texture2D splashTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(splashAssetPath);
            if (splashTexture == null)
            {
                return null;
            }

            lastCharacterSplashAssetPath = splashAssetPath;
            return splashTexture;
        }

        private string GetRememberedCharacterSplashModelAssetPath()
        {
            if (!string.IsNullOrWhiteSpace(lastCharacterSplashModelAssetPath))
            {
                return lastCharacterSplashModelAssetPath;
            }

            lastCharacterSplashModelAssetPath =
                SessionState.GetString(LastCharacterSplashModelAssetPathSessionKey, string.Empty);
            return lastCharacterSplashModelAssetPath;
        }

        private static IEnumerable<string> EnumerateCharacterSplashContextAssetPaths(GameObject activeModel)
        {
            if (activeModel == null)
            {
                yield break;
            }

            HashSet<string> emittedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (TryResolveModelAssetPath(activeModel, out string modelAssetPath)
                && emittedPaths.Add(modelAssetPath))
            {
                yield return modelAssetPath;
            }

            foreach (Renderer renderer in activeModel.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < (materials != null ? materials.Length : 0); index++)
                {
                    string materialAssetPath = AssetDatabase.GetAssetPath(materials[index]);
                    if (!string.IsNullOrWhiteSpace(materialAssetPath)
                        && emittedPaths.Add(materialAssetPath))
                    {
                        yield return materialAssetPath;
                    }
                }
            }

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in activeModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string meshAssetPath = skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null
                    ? AssetDatabase.GetAssetPath(skinnedMeshRenderer.sharedMesh)
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(meshAssetPath)
                    && emittedPaths.Add(meshAssetPath))
                {
                    yield return meshAssetPath;
                }
            }

            foreach (MeshFilter meshFilter in activeModel.GetComponentsInChildren<MeshFilter>(true))
            {
                string meshAssetPath = meshFilter != null && meshFilter.sharedMesh != null
                    ? AssetDatabase.GetAssetPath(meshFilter.sharedMesh)
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(meshAssetPath)
                    && emittedPaths.Add(meshAssetPath))
                {
                    yield return meshAssetPath;
                }
            }
        }

        private GameObject ResolveLivePlacementActiveModel()
        {
            CharacterPlacementController placementController = ResolveLivePlacementController();
            return placementController != null ? placementController.ActiveModel : currentContext?.PlacementActiveModel;
        }

        private CharacterPlacementController ResolveLivePlacementController()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CharacterPlacementController playModeController = CharacterPlacementController.GetPrimaryCachedOrFind();
                if (playModeController != null)
                {
                    return playModeController;
                }
            }

            return currentContext?.PlacementController ?? CharacterPlacementController.GetPrimaryCachedOrFind();
        }

        private void RefreshCharacterSplashArtworkForPlacementChanges()
        {
            if (EditorApplication.timeSinceStartup < nextCharacterSplashRefreshTime)
            {
                return;
            }

            nextCharacterSplashRefreshTime =
                EditorApplication.timeSinceStartup + CharacterSplashRefreshIntervalSeconds;

            TryRefreshCharacterSplashArtworkFromLivePlacement();
        }

        private bool TryRefreshCharacterSplashArtworkFromLivePlacement()
        {
            if (characterSplashImage == null)
            {
                return false;
            }

            CharacterPlacementController placementController = ResolveLivePlacementController();
            GameObject activeModel = placementController != null ? placementController.ActiveModel : null;
            int placementControllerId = placementController != null ? placementController.GetInstanceID() : 0;
            int activeModelId = activeModel != null ? activeModel.GetInstanceID() : 0;
            int activeModelIndex = placementController != null ? placementController.ActiveModelIndex : -1;
            int inputSwitchVersion = placementController != null ? placementController.InputSwitchVersion : -1;

            if (observedCharacterSplashPlacementControllerId == placementControllerId
                && observedCharacterSplashActiveModelId == activeModelId
                && observedCharacterSplashActiveModelIndex == activeModelIndex
                && observedCharacterSplashInputSwitchVersion == inputSwitchVersion)
            {
                return false;
            }

            observedCharacterSplashPlacementControllerId = placementControllerId;
            observedCharacterSplashActiveModelId = activeModelId;
            observedCharacterSplashActiveModelIndex = activeModelIndex;
            observedCharacterSplashInputSwitchVersion = inputSwitchVersion;
            currentContext = BuildModuleContext();
            ApplyActiveCharacterSplashArtwork();
            Repaint();
            return true;
        }

        private void ResetObservedCharacterSplashState()
        {
            observedCharacterSplashPlacementControllerId = 0;
            observedCharacterSplashActiveModelId = 0;
            observedCharacterSplashActiveModelIndex = -1;
            observedCharacterSplashInputSwitchVersion = -1;
            nextCharacterSplashRefreshTime = 0d;
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
            if (IsManagerRefreshBlockedByValueInteraction())
            {
                TryRefreshCharacterSplashArtworkFromLivePlacement();
                managerRefreshDeferredByValueInteraction = true;
                return;
            }

            currentContext = BuildModuleContext();
            RefreshGlobalContext();
            RefreshFooterActionStates();
            RefreshBody();
        }

        private void RefreshGlobalContext()
        {
            ApplyActiveCharacterSplashArtwork();

            if (globalContextHost == null)
            {
                return;
            }

            globalContextHost.Clear();
            globalContextHost.Add(SetupModule.CreatePlacementSection(currentContext));
        }

        internal void RefreshManagerContext()
        {
            if (IsManagerRefreshBlockedByValueInteraction())
            {
                TryRefreshCharacterSplashArtworkFromLivePlacement();
                managerRefreshDeferredByValueInteraction = true;
                return;
            }

            RefreshManualContext();
            RegisterOnboardingTargets();
        }

        private void RegisterManagerValueChangeGuards(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            root.RegisterCallback<ChangeEvent<bool>>(evt => HandleManagerValueChanged(evt), TrickleDown.TrickleDown);
            root.RegisterCallback<ChangeEvent<int>>(evt => HandleManagerValueChanged(evt), TrickleDown.TrickleDown);
            root.RegisterCallback<ChangeEvent<float>>(evt => HandleManagerValueChanged(evt), TrickleDown.TrickleDown);
            root.RegisterCallback<ChangeEvent<string>>(evt => HandleManagerValueChanged(evt), TrickleDown.TrickleDown);
            root.RegisterCallback<ChangeEvent<Color>>(evt => HandleManagerValueChanged(evt), TrickleDown.TrickleDown);
            root.RegisterCallback<ChangeEvent<Vector3>>(evt => HandleManagerValueChanged(evt), TrickleDown.TrickleDown);
            root.RegisterCallback<ChangeEvent<Object>>(evt => HandleManagerValueChanged(evt), TrickleDown.TrickleDown);
            root.RegisterCallback<ChangeEvent<Enum>>(evt => HandleManagerValueChanged(evt), TrickleDown.TrickleDown);
        }

        private void HandleManagerValueChanged<T>(ChangeEvent<T> evt)
        {
            if (evt == null || !IsManagerEditableValueTarget(evt.target as VisualElement))
            {
                return;
            }

            managerValueInteractionQuietUntil = EditorApplication.timeSinceStartup + ValueInteractionRefreshQuietSeconds;
            EnsureManagerValueInteractionRefreshSchedule();
        }

        private bool IsManagerRefreshBlockedByValueInteraction()
        {
            if (managerValueInteractionActive)
            {
                return true;
            }

            if (EditorApplication.timeSinceStartup < managerValueInteractionQuietUntil)
            {
                EnsureManagerValueInteractionRefreshSchedule();
                return true;
            }

            return false;
        }

        private void EnsureManagerValueInteractionRefreshSchedule()
        {
            if (shellRoot == null || managerValueInteractionRefreshSchedule != null)
            {
                return;
            }

            managerValueInteractionRefreshSchedule = shellRoot.schedule
                .Execute(FlushDeferredManagerRefreshAfterValueInteraction)
                .Every(ValueInteractionRefreshPollMilliseconds);
        }

        private void FlushDeferredManagerRefreshAfterValueInteraction()
        {
            if (managerValueInteractionActive || EditorApplication.timeSinceStartup < managerValueInteractionQuietUntil)
            {
                return;
            }

            managerValueInteractionRefreshSchedule?.Pause();
            managerValueInteractionRefreshSchedule = null;

            if (!managerRefreshDeferredByValueInteraction)
            {
                return;
            }

            managerRefreshDeferredByValueInteraction = false;
            RequestDeferredManagerRefresh();
        }

        private void HandleManagerValuePointerDown(PointerDownEvent evt)
        {
            if (evt == null || evt.button != 0 || managerValueInteractionActive)
            {
                return;
            }

            if (!IsSliderInteractionTarget(evt.target as VisualElement))
            {
                return;
            }

            managerValueInteractionActive = true;
            managerValueInteractionPointerId = evt.pointerId;
        }

        private void HandleManagerValuePointerUp(PointerUpEvent evt)
        {
            if (!managerValueInteractionActive)
            {
                return;
            }

            if (evt != null && managerValueInteractionPointerId >= 0 && evt.pointerId != managerValueInteractionPointerId)
            {
                return;
            }

            EndManagerValueInteraction();
        }

        private void HandleManagerValuePointerCancel(PointerCancelEvent evt)
        {
            if (!managerValueInteractionActive)
            {
                return;
            }

            if (evt != null && managerValueInteractionPointerId >= 0 && evt.pointerId != managerValueInteractionPointerId)
            {
                return;
            }

            EndManagerValueInteraction();
        }

        private void EndManagerValueInteraction()
        {
            managerValueInteractionActive = false;
            managerValueInteractionPointerId = -1;

            if (!managerRefreshDeferredByValueInteraction)
            {
                return;
            }

            managerRefreshDeferredByValueInteraction = false;
            RequestDeferredManagerRefresh();
        }

        private static bool IsSliderInteractionTarget(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current is Slider || current is SliderInt || current is MinMaxSlider)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsManagerEditableValueTarget(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current is Slider
                    || current is SliderInt
                    || current is MinMaxSlider
                    || current is Toggle
                    || current is TextField
                    || current is IntegerField
                    || current is FloatField
                    || current is EnumField
                    || current is DropdownField
                    || current is ObjectField
                    || current is ColorField
                    || current is Vector3Field)
                {
                    return true;
                }
            }

            return false;
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

            if (userProfileButton != null)
            {
                OnboardingTargetRegistry.RegisterVisualElement(
                    "Manager.HeaderUserProfile",
                    userProfileButton,
                    "Header user profile",
                    "Manager Header",
                    () => userProfileButton.Focus());
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

            EnvironmentManager environmentManager = EnvironmentManager.GetPrimaryCachedOrFind();
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
                PlacementTeamCharacterNames = placementTeamCharacterNames,
                EnvironmentManager = environmentManager
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

            foreach (BatchModelDetectionInfo detection in detections ?? Array.Empty<BatchModelDetectionInfo>())
            {
                if (detection == null || detection.ModelAsset == null)
                {
                    continue;
                }

                int existingIndex = FindDuplicateQueuedDetectionIndex(orderedDetections, detection);
                if (existingIndex < 0)
                {
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

        private static int FindDuplicateQueuedDetectionIndex(
            IReadOnlyList<BatchModelDetectionInfo> existingDetections,
            BatchModelDetectionInfo candidateDetection)
        {
            if (existingDetections == null || candidateDetection == null)
            {
                return -1;
            }

            for (int index = 0; index < existingDetections.Count; index++)
            {
                if (IsDuplicateQueuedDetection(existingDetections[index], candidateDetection))
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool IsDuplicateQueuedDetection(
            BatchModelDetectionInfo existingDetection,
            BatchModelDetectionInfo candidateDetection)
        {
            if (existingDetection == null || candidateDetection == null)
            {
                return false;
            }

            string existingAssetPath = AssetContextJsonQueryUtility.NormalizeAssetPath(existingDetection.AssetPath);
            string candidateAssetPath = AssetContextJsonQueryUtility.NormalizeAssetPath(candidateDetection.AssetPath);
            if (!string.IsNullOrWhiteSpace(existingAssetPath)
                && string.Equals(existingAssetPath, candidateAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (existingDetection.IsConverted == candidateDetection.IsConverted)
            {
                return false;
            }

            return string.Equals(
                BuildBatchDuplicateKey(existingDetection),
                BuildBatchDuplicateKey(candidateDetection),
                StringComparison.OrdinalIgnoreCase);
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

            if (width <= 479f)
            {
                shellRoot.AddToClassList(WidthNarrowClass);
            }
            else if (width <= 699f)
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
