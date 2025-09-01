using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using HoyoToon.UI.Core;

namespace HoyoToon
{
    /// <summary>
    /// Main HoyoToon UI Manager window using modern Unity UI Toolkit
    /// Follows HoyoToon framework naming conventions and architecture
    /// </summary>
    public class HoyoToonUIManager : EditorWindow
    {
        #region Singleton Instance

        private static HoyoToonUIManager _instance;
        public static HoyoToonUIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = GetWindow<HoyoToonUIManager>(false, "HoyoToon Manager", false);

                    // Ensure proper sizing for singleton access
                    _instance.minSize = new Vector2(HoyoToonUILayout.MIN_WINDOW_WIDTH, HoyoToonUILayout.MIN_WINDOW_HEIGHT);

                    if (_instance.position.width < HoyoToonUILayout.DEFAULT_WINDOW_WIDTH ||
                        _instance.position.height < HoyoToonUILayout.DEFAULT_WINDOW_HEIGHT)
                    {
                        var rect = _instance.position;
                        rect.width = HoyoToonUILayout.DEFAULT_WINDOW_WIDTH;
                        rect.height = HoyoToonUILayout.DEFAULT_WINDOW_HEIGHT;
                        _instance.position = rect;
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Menu Items and Window Management

        [MenuItem("HoyoToon/Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<HoyoToonUIManager>("HoyoToon Manager");

            HoyoToonLogs.LogDebug($"ShowWindow called - Current window size: {window.position.size} (Width: {window.position.width}, Height: {window.position.height})");

            // Enforce minimum window size (1200x900) - cannot go smaller
            window.minSize = new Vector2(HoyoToonUILayout.MIN_WINDOW_WIDTH, HoyoToonUILayout.MIN_WINDOW_HEIGHT);

            // Always enforce minimum size if window is smaller
            if (window.position.width < HoyoToonUILayout.MIN_WINDOW_WIDTH ||
                window.position.height < HoyoToonUILayout.MIN_WINDOW_HEIGHT)
            {
                var rect = window.position;
                rect.width = Mathf.Max(rect.width, HoyoToonUILayout.MIN_WINDOW_WIDTH);
                rect.height = Mathf.Max(rect.height, HoyoToonUILayout.MIN_WINDOW_HEIGHT);

                // Center the window on screen
                rect.x = (Screen.currentResolution.width - rect.width) / 2f;
                rect.y = (Screen.currentResolution.height - rect.height) / 2f;

                window.position = rect;

                HoyoToonLogs.LogDebug($"Window resized to minimum: {rect.size} (Width: {rect.width}, Height: {rect.height}) and centered at ({rect.x}, {rect.y})");
            }
            else
            {
                HoyoToonLogs.LogDebug($"Window size acceptable - keeping current size: {window.position.size}");
            }
        }

        #endregion

        #region Core UI Elements

        // Model selection and preview
        private ObjectField modelSelector;
        private ScrollView contentView;
        private VisualElement previewContainer;
        private IMGUIContainer previewIMGUI;

        // Action controls
        private Button fullSetupBtn;
        private Button refreshBtn;
        private Button settingsBtn; // Replaced with addToSceneBtn
        private Button addToSceneBtn;
        private VisualElement quickActionsContainer;

        // Status and progress
        private VisualElement statusBar;
        private Label statusLabel;
        private ProgressBar progressBar;

        // Validation indicators
        private Label validationModel, validationRig, validationMaterials, validationTextures, validationShaders;

        // Tab system
        private Dictionary<string, ToolbarButton> tabButtons = new Dictionary<string, ToolbarButton>();
        private Dictionary<string, IHoyoToonTabController> tabControllers = new Dictionary<string, IHoyoToonTabController>();
        private string currentTab = "Setup";
        private IHoyoToonTabController currentTabController;

        #endregion

        #region Data Management

        private GameObject selectedModel;
        private GameObject sceneInstance;
        private HoyoToonModelAnalysisData analysisData;

        // Analysis caching for performance
        private HoyoToonModelAnalysisData cachedAnalysisData;
        private GameObject lastAnalyzedModel;
        private bool isAnalysisCacheValid = false;

        // Event for cache invalidation from tab controllers
        public static event System.Action OnAnalysisCacheInvalidationRequested;

        private HoyoToonUISettings settings;
        private HoyoToonBannerData bannerData;

        // Preview system
        private PreviewRenderUtility previewUtility;
        private Vector2 lastWindowSize;
        private UnityEditor.Editor gameObjectEditor;
        private Vector2 previewDrag = Vector2.zero;
        private float previewDistance = 3f;
        private float previewZoom = 0f; // Forward/backward panning offset
        private Vector2 previewRotation = new Vector2(0, 0);
        private bool isInteractingWithPreview = false;
        private Vector2 lastMousePosition;
        private bool isUpdatingPreview = false; // Flag to prevent recursive preview updates
        private bool hierarchyChangePending = false; // Flag to prevent multiple hierarchy change callbacks

        #endregion

        #region Layout Constants

        private struct HoyoToonUILayout
        {
            public const float BANNER_HEIGHT = 150f;
            public const float LOGO_WIDTH = 348f;
            public const float LOGO_HEIGHT = 114f;
            public const float CHARACTER_MAX_WIDTH = 256f;
            public const float CHARACTER_MAX_HEIGHT = 180f;
            public const float MIN_LOGO_DISTANCE = 5f;
            public const float PREVIEW_PANEL_WIDTH = 420f;
            public const float MIN_CONTENT_WIDTH = 400f;
            public const float TAB_HEIGHT = 30f;
            public const float STATUS_BAR_HEIGHT = 25f;

            // Window sizing constraints
            public const float DEFAULT_WINDOW_WIDTH = 1200f;
            public const float DEFAULT_WINDOW_HEIGHT = 900f;
            public const float MIN_WINDOW_WIDTH = 1200f;
            public const float MIN_WINDOW_HEIGHT = 900f;
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            try
            {
                // Set singleton instance
                _instance = this;

                // Enforce window sizing constraints
                minSize = new Vector2(HoyoToonUILayout.MIN_WINDOW_WIDTH, HoyoToonUILayout.MIN_WINDOW_HEIGHT);

                HoyoToonLogs.LogDebug("HoyoToonUIManager: Initializing...");

                LoadHoyoToonUISettings();
                InitializeHoyoToonBannerData();
                InitializeHoyoToonTabControllers();
                SetupHoyoToonPreviewSystem();

                // Subscribe to cache invalidation requests
                OnAnalysisCacheInvalidationRequested += InvalidateAnalysisCache;
                CreateHoyoToonUI();
                SetupHoyoToonEventListeners();
                InitializeHoyoToonAnalysisData();
                UpdateHoyoToonUI();
                SwitchToHoyoToonTab("Setup");

                // Initialize window size tracking for layout updates
                lastWindowSize = position.size;
                HoyoToonLogs.LogDebug($"Initial window size set to: {lastWindowSize} (Width: {lastWindowSize.x}, Height: {lastWindowSize.y})");

                // Ensure quick actions are refreshed after everything is initialized
                EditorApplication.delayCall += () =>
                {
                    if (this != null && quickActionsContainer != null)
                    {
                        RefreshQuickActions();
                        Repaint();
                    }
                };

                HoyoToonLogs.LogDebug("HoyoToonUIManager: Initialization complete.");
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Error in HoyoToonUIManager.OnEnable: {e.Message}");
                CreateHoyoToonFallbackUI();
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from cache invalidation requests
            OnAnalysisCacheInvalidationRequested -= InvalidateAnalysisCache;

            CleanupHoyoToonEventListeners();
            CleanupHoyoToonTabControllers();
            CleanupHoyoToonPreviewSystem();
            SaveHoyoToonUISettings();

            // Clear singleton instance
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnDestroy()
        {
            CleanupHoyoToonPreviewSystem();
        }

        private void Update()
        {
            // Check for window size changes and update UI layout accordingly
            if (position.size != lastWindowSize)
            {
                // Debug log for window size tracking - only log significant changes to avoid spam
                float sizeDifference = Mathf.Abs(position.size.x - lastWindowSize.x) + Mathf.Abs(position.size.y - lastWindowSize.y);
                if (sizeDifference > 50.0f) // Only log if the size change is more than 50 pixels total (increased from 5 to reduce spam)
                {
                    HoyoToonLogs.LogDebug($"Window size changed from {lastWindowSize} to {position.size} " +
                                        $"(Width: {position.size.x}, Height: {position.size.y})");
                }

                lastWindowSize = position.size;
                HandleWindowResize();
            }

            // Preview interaction is now handled directly in OnHoyoToonPreviewGUI()
            // No need to handle it here anymore
        }

        private void HandleWindowResize()
        {
            // Force UI layout refresh when window is resized or fullscreen toggles
            EditorApplication.delayCall += () =>
            {
                // Update UI layout to accommodate new window size
                if (rootVisualElement != null && rootVisualElement.childCount > 0)
                {
                    // Force layout recalculation
                    rootVisualElement.MarkDirtyRepaint();

                    // Update quick actions for current layout
                    RefreshQuickActions();

                    // Only update current tab content if it's a significant resize
                    // Minor resizes don't need content rebuilding
                    if (currentTabController != null && Math.Abs(position.size.x - lastWindowSize.x) > 50)
                    {
                        currentTabController.UpdateContent();
                    }

                    // Force a repaint to show layout changes
                    Repaint();
                }
            };
        }

        #endregion

        #region Initialization

        private void LoadHoyoToonUISettings()
        {
            settings = new HoyoToonUISettings();
            settings.LoadFromEditorPrefs();
        }

        private void SaveHoyoToonUISettings()
        {
            settings?.SaveToEditorPrefs();
        }

        private void InitializeHoyoToonBannerData()
        {
            bannerData = new HoyoToonBannerData();
            UpdateHoyoToonBannerData();
        }

        private void InitializeHoyoToonTabControllers()
        {
            tabControllers["Setup"] = new HoyoToonSetupTabController();
            tabControllers["FBX"] = new HoyoToonFBXTabController();
            tabControllers["Materials"] = new HoyoToonMaterialsTabController();
            //tabControllers["Shaders"] = new HoyoToonShadersTabController();
            tabControllers["Textures"] = new HoyoToonTexturesTabController();
            tabControllers["Scene"] = new HoyoToonSceneTabController();
            //tabControllers["Settings"] = new HoyoToonSettingsTabController();
            tabControllers["Resources"] = new HoyoToonResourcesTabController();

            foreach (var controller in tabControllers.Values)
            {
                controller.Initialize();
            }
        }

        private void SetupHoyoToonPreviewSystem()
        {
            try
            {
                previewUtility = new PreviewRenderUtility();
                previewUtility.ambientColor = new Color(0.2f, 0.2f, 0.3f, 1f);
                previewUtility.lights[0].intensity = 1.2f;
                previewUtility.lights[0].transform.rotation = Quaternion.Euler(50f, 50f, 0f);
                previewUtility.cameraFieldOfView = 30f;
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Failed to setup preview system: {e.Message}");
            }
        }

        private void InitializeHoyoToonAnalysisData()
        {
            analysisData = new HoyoToonModelAnalysisData();
        }

        private void SetupHoyoToonEventListeners()
        {
            HoyoToonModelAnalysisComponent.OnAnalysisComplete += OnHoyoToonAnalysisComplete;
            EditorApplication.playModeStateChanged += OnHoyoToonPlayModeStateChanged;

            // Add asset change detection for automatic UI refresh
            EditorApplication.projectChanged += OnHoyoToonProjectChanged;
            AssetDatabase.importPackageCompleted += OnHoyoToonAssetImportCompleted;

            // Add hierarchy change detection for preview management
            EditorApplication.hierarchyChanged += OnHoyoToonHierarchyChanged;
        }

        private void CleanupHoyoToonEventListeners()
        {
            HoyoToonModelAnalysisComponent.OnAnalysisComplete -= OnHoyoToonAnalysisComplete;
            EditorApplication.playModeStateChanged -= OnHoyoToonPlayModeStateChanged;

            // Remove asset change listeners
            EditorApplication.projectChanged -= OnHoyoToonProjectChanged;
            AssetDatabase.importPackageCompleted -= OnHoyoToonAssetImportCompleted;

            // Remove hierarchy change listener
            EditorApplication.hierarchyChanged -= OnHoyoToonHierarchyChanged;
        }
        private void CleanupHoyoToonTabControllers()
        {
            foreach (var controller in tabControllers.Values)
            {
                controller.Cleanup();
            }
            tabControllers.Clear();
        }

        private void CleanupHoyoToonPreviewSystem()
        {
            try
            {
                CleanupCurrentPreview();

                if (previewUtility != null)
                {
                    previewUtility.Cleanup();
                    previewUtility = null;
                }
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Error during preview cleanup: {e.Message}");
            }
        }

        #endregion

        #region UI Creation

        private void CreateHoyoToonUI()
        {
            rootVisualElement.Clear();

            var mainContainer = new VisualElement();
            mainContainer.style.flexGrow = 1;
            mainContainer.style.flexDirection = FlexDirection.Column;

            // Dynamic Banner Section
            CreateHoyoToonBannerSection(mainContainer);

            // Content area below banner
            var contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;
            contentContainer.style.flexDirection = FlexDirection.Column;
            contentContainer.style.marginTop = 10;
            contentContainer.style.marginLeft = 10;
            contentContainer.style.marginRight = 10;
            contentContainer.style.marginBottom = 10;

            // Model Selector Section
            CreateHoyoToonModelSelectorSection(contentContainer);

            // Main split container (left: tabs/info, right: preview)
            var splitContainer = new VisualElement();
            splitContainer.style.flexDirection = FlexDirection.Row;
            splitContainer.style.flexGrow = 1;
            splitContainer.style.flexShrink = 1; // Allow shrinking
            splitContainer.style.marginTop = 10;
            splitContainer.style.minHeight = 400; // Ensure minimum height

            // Left side: validation, tabs, and info
            CreateHoyoToonContentSection(splitContainer);

            // Right side: preview
            CreateHoyoToonPreviewSection(splitContainer);

            contentContainer.Add(splitContainer);

            // Status Bar
            CreateHoyoToonStatusBar(contentContainer);

            mainContainer.Add(contentContainer);
            rootVisualElement.Add(mainContainer);
        }

        private void CreateHoyoToonBannerSection(VisualElement parent)
        {
            UpdateHoyoToonBannerData(); // Update banner data based on current model

            var bannerSection = new VisualElement();
            bannerSection.style.height = HoyoToonUILayout.BANNER_HEIGHT;
            bannerSection.style.width = Length.Percent(100);
            bannerSection.style.minHeight = HoyoToonUILayout.BANNER_HEIGHT;
            bannerSection.style.overflow = Overflow.Hidden;
            bannerSection.style.position = Position.Relative;

            // Use IMGUI for banner to match material inspector behavior
            var bannerIMGUI = new IMGUIContainer(() =>
            {
                DrawHoyoToonBannerIMGUI();
            });
            bannerIMGUI.style.height = HoyoToonUILayout.BANNER_HEIGHT;
            bannerIMGUI.style.width = Length.Percent(100);

            bannerSection.Add(bannerIMGUI);
            parent.Add(bannerSection);
        }

        private void DrawHoyoToonBannerIMGUI()
        {
            var layout = CreateHoyoToonBannerLayout();

            // Draw background
            DrawHoyoToonBannerBackground(layout);

            // Draw character images
            DrawHoyoToonCharacterImages(layout);

            // Draw logo (centered)
            DrawHoyoToonBannerLogo(layout);
        }

        private HoyoToonBannerLayoutData CreateHoyoToonBannerLayout()
        {
            var data = new HoyoToonBannerLayoutData();

            // Calculate layout rectangles like in material inspector
            data.contentRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(HoyoToonUILayout.BANNER_HEIGHT));
            data.originalY = data.contentRect.y;

            // Extended background rect to fill entire width
            data.bgRect = new Rect(0, 0, EditorGUIUtility.currentViewWidth,
                data.contentRect.height + data.originalY);

            // Logo rect centered in the banner area
            float logoY = data.originalY + (HoyoToonUILayout.BANNER_HEIGHT - HoyoToonUILayout.LOGO_HEIGHT) / 2;
            data.logoRect = new Rect((data.bgRect.width - HoyoToonUILayout.LOGO_WIDTH) / 2,
                logoY, HoyoToonUILayout.LOGO_WIDTH, HoyoToonUILayout.LOGO_HEIGHT);

            return data;
        }

        private void DrawHoyoToonBannerBackground(HoyoToonBannerLayoutData layout)
        {
            // Try to load background texture
            Texture2D bg = bannerData.backgroundTexture ?? Resources.Load<Texture2D>("UI/background");
            if (bg != null)
            {
                GUI.DrawTexture(layout.bgRect, bg, ScaleMode.StretchToFill);
            }
            else
            {
                // Fallback gradient background based on detected game
                var gameColor = GetHoyoToonGameThemeColor(bannerData.detectedGame);
                EditorGUI.DrawRect(layout.bgRect, gameColor);
            }
        }

        private void DrawHoyoToonCharacterImages(HoyoToonBannerLayoutData layout)
        {
            // Draw left character
            if (bannerData.characterLeftTexture != null)
            {
                var leftRect = CalculateHoyoToonCharacterRect(bannerData.characterLeftTexture, layout, true);
                GUI.DrawTexture(leftRect, bannerData.characterLeftTexture, ScaleMode.ScaleToFit);
            }

            // Draw right character
            if (bannerData.characterRightTexture != null)
            {
                var rightRect = CalculateHoyoToonCharacterRect(bannerData.characterRightTexture, layout, false);
                GUI.DrawTexture(rightRect, bannerData.characterRightTexture, ScaleMode.ScaleToFit);
            }
        }

        private void DrawHoyoToonBannerLogo(HoyoToonBannerLayoutData layout)
        {
            // Try to load logo texture
            Texture2D logo = bannerData.logoTexture ?? Resources.Load<Texture2D>("UI/hoyotoon");
            if (logo == null)
                logo = Resources.Load<Texture2D>("UI/managerlogo");

            if (logo != null)
            {
                GUI.DrawTexture(layout.logoRect, logo, ScaleMode.ScaleToFit);
            }
            else
            {
                // Fallback text logo
                var style = new GUIStyle(EditorStyles.boldLabel);
                style.fontSize = 28;
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleCenter;

                // Add shadow effect
                var shadowStyle = new GUIStyle(style);
                shadowStyle.normal.textColor = Color.black;
                var shadowRect = layout.logoRect;
                shadowRect.x += 2;
                shadowRect.y += 2;
                GUI.Label(shadowRect, "HoyoToon Manager", shadowStyle);
                GUI.Label(layout.logoRect, "HoyoToon Manager", style);
            }
        }

        private Rect CalculateHoyoToonCharacterRect(Texture2D texture, HoyoToonBannerLayoutData layout, bool isLeftSide)
        {
            // Calculate character dimensions while maintaining aspect ratio
            float aspectRatio = (float)texture.width / texture.height;
            float characterWidth = Mathf.Min(HoyoToonUILayout.CHARACTER_MAX_WIDTH,
                HoyoToonUILayout.CHARACTER_MAX_HEIGHT * aspectRatio);
            float characterHeight = Mathf.Min(HoyoToonUILayout.CHARACTER_MAX_HEIGHT,
                HoyoToonUILayout.CHARACTER_MAX_WIDTH / aspectRatio);

            // Calculate Y position (bottom of banner)
            float characterY = layout.originalY + HoyoToonUILayout.BANNER_HEIGHT - characterHeight;

            // Calculate X position with logo boundary constraints
            float characterX;
            if (isLeftSide)
            {
                float maxAllowedX = layout.logoRect.x - characterWidth - HoyoToonUILayout.MIN_LOGO_DISTANCE;
                characterX = Mathf.Min(layout.bgRect.x, maxAllowedX);
            }
            else
            {
                float minAllowedX = layout.logoRect.xMax + HoyoToonUILayout.MIN_LOGO_DISTANCE;
                characterX = Mathf.Max(layout.bgRect.xMax - characterWidth, minAllowedX);
            }

            return new Rect(characterX, characterY, characterWidth, characterHeight);
        }

        // Data structure to hold banner layout information (matching material inspector)
        private struct HoyoToonBannerLayoutData
        {
            public Rect contentRect;
            public Rect bgRect;
            public Rect logoRect;
            public float originalY;
        }

        private void CreateHoyoToonModelSelectorSection(VisualElement parent)
        {
            var selectorContainer = new VisualElement();
            selectorContainer.style.flexDirection = FlexDirection.Row;
            selectorContainer.style.alignItems = Align.Center;
            selectorContainer.style.marginBottom = 10;
            selectorContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            selectorContainer.style.paddingTop = 8;
            selectorContainer.style.paddingBottom = 8;
            selectorContainer.style.paddingLeft = 10;
            selectorContainer.style.paddingRight = 10;
            selectorContainer.style.borderTopLeftRadius = 6;
            selectorContainer.style.borderTopRightRadius = 6;
            selectorContainer.style.borderBottomLeftRadius = 6;
            selectorContainer.style.borderBottomRightRadius = 6;
            selectorContainer.style.borderTopWidth = 1;
            selectorContainer.style.borderBottomWidth = 1;
            selectorContainer.style.borderLeftWidth = 1;
            selectorContainer.style.borderRightWidth = 1;
            selectorContainer.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            selectorContainer.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            selectorContainer.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            selectorContainer.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            selectorContainer.style.maxWidth = 600;
            selectorContainer.style.minHeight = 40; // Prevent flattening
            selectorContainer.style.flexShrink = 0; // Prevent shrinking

            var selectorLabel = new Label("Model:");
            selectorLabel.style.marginRight = 10;
            selectorLabel.style.minWidth = 120;
            selectorLabel.style.maxWidth = 120;
            selectorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            selectorLabel.style.flexShrink = 0; // Prevent label shrinking

            modelSelector = new ObjectField();
            modelSelector.objectType = typeof(GameObject);
            modelSelector.allowSceneObjects = false;
            modelSelector.style.flexGrow = 1;
            modelSelector.style.minWidth = 200;
            modelSelector.style.maxWidth = 400;
            modelSelector.RegisterValueChangedCallback(OnHoyoToonModelSelected);

            selectorContainer.Add(selectorLabel);
            selectorContainer.Add(modelSelector);
            parent.Add(selectorContainer);
        }

        private void CreateHoyoToonContentSection(VisualElement parent)
        {
            var leftContainer = new VisualElement();
            leftContainer.style.flexGrow = 1;
            leftContainer.style.flexShrink = 1; // Allow shrinking but maintain minimum
            leftContainer.style.flexDirection = FlexDirection.Column;
            leftContainer.style.marginRight = 10;
            leftContainer.style.minWidth = HoyoToonUILayout.MIN_CONTENT_WIDTH;
            leftContainer.style.maxWidth = Length.Percent(70); // Prevent taking too much space

            // Validation Section
            CreateHoyoToonValidationSection(leftContainer);

            // Tab Navigation
            CreateHoyoToonTabNavigation(leftContainer);

            // Content View (main content area) - with proper flex settings and height constraints
            contentView = new ScrollView();
            contentView.style.flexGrow = 1;
            contentView.style.flexShrink = 1; // Allow shrinking
            contentView.style.marginTop = 5;
            contentView.style.marginBottom = 10; // Add bottom margin for action buttons
            contentView.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            contentView.style.borderTopLeftRadius = 8;
            contentView.style.borderTopRightRadius = 8;
            contentView.style.borderBottomLeftRadius = 8;
            contentView.style.borderBottomRightRadius = 8;
            contentView.style.borderTopWidth = 1;
            contentView.style.borderBottomWidth = 1;
            contentView.style.borderLeftWidth = 1;
            contentView.style.borderRightWidth = 1;
            contentView.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            contentView.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            contentView.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            contentView.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            contentView.style.paddingTop = 10;
            contentView.style.paddingBottom = 10;
            contentView.style.paddingLeft = 10;
            contentView.style.paddingRight = 10;
            contentView.style.minHeight = 250; // Ensure minimum height
            contentView.style.maxHeight = 500; // Prevent excessive height that breaks layout
            leftContainer.Add(contentView);

            // Quick Actions Section (moved above action buttons per user request)
            CreateHoyoToonQuickActionsSection(leftContainer);

            // Action Buttons Section - always at bottom with fixed positioning
            CreateHoyoToonActionButtonsSection(leftContainer);

            parent.Add(leftContainer);
        }

        private void CreateHoyoToonValidationSection(VisualElement parent)
        {
            var validationContainer = new VisualElement();
            validationContainer.style.flexDirection = FlexDirection.Row;
            validationContainer.style.justifyContent = Justify.SpaceBetween;
            validationContainer.style.marginBottom = 10;
            validationContainer.style.backgroundColor = new Color(0.1f, 0.15f, 0.2f, 0.8f);
            validationContainer.style.paddingTop = 8;
            validationContainer.style.paddingBottom = 8;
            validationContainer.style.paddingLeft = 10;
            validationContainer.style.paddingRight = 10;
            validationContainer.style.borderTopLeftRadius = 6;
            validationContainer.style.borderTopRightRadius = 6;
            validationContainer.style.borderBottomLeftRadius = 6;
            validationContainer.style.borderBottomRightRadius = 6;
            validationContainer.style.borderTopWidth = 1;
            validationContainer.style.borderBottomWidth = 1;
            validationContainer.style.borderLeftWidth = 1;
            validationContainer.style.borderRightWidth = 1;
            validationContainer.style.borderTopColor = new Color(0.3f, 0.4f, 0.5f, 0.8f);
            validationContainer.style.borderBottomColor = new Color(0.3f, 0.4f, 0.5f, 0.8f);
            validationContainer.style.borderLeftColor = new Color(0.3f, 0.4f, 0.5f, 0.8f);
            validationContainer.style.borderRightColor = new Color(0.3f, 0.4f, 0.5f, 0.8f);
            validationContainer.style.minHeight = 40; // Prevent flattening
            validationContainer.style.flexShrink = 0; // Prevent shrinking

            // Create preparation status indicators
            validationModel = CreateHoyoToonValidationIndicator("Model");
            validationRig = CreateHoyoToonValidationIndicator("Rig");
            validationMaterials = CreateHoyoToonValidationIndicator("Materials");
            validationTextures = CreateHoyoToonValidationIndicator("Textures");
            validationShaders = CreateHoyoToonValidationIndicator("Shaders");

            validationContainer.Add(validationModel);
            validationContainer.Add(validationRig);
            validationContainer.Add(validationMaterials);
            validationContainer.Add(validationTextures);
            validationContainer.Add(validationShaders);

            parent.Add(validationContainer);
        }

        private Label CreateHoyoToonValidationIndicator(string name)
        {
            var indicator = new Label($"● {name}");
            indicator.style.color = Color.gray;
            indicator.style.fontSize = 12;
            indicator.style.unityFontStyleAndWeight = FontStyle.Bold;
            indicator.style.marginLeft = 5;
            indicator.style.marginRight = 5;
            return indicator;
        }

        private void CreateHoyoToonTabNavigation(VisualElement parent)
        {
            var tabContainer = new VisualElement();
            tabContainer.style.flexDirection = FlexDirection.Row;
            tabContainer.style.marginBottom = 5;
            tabContainer.style.flexShrink = 0; // Prevent shrinking during layout changes
            tabContainer.style.minHeight = HoyoToonUILayout.TAB_HEIGHT; // Maintain minimum height
            tabContainer.style.justifyContent = Justify.SpaceBetween; // Even distribution
            tabContainer.style.alignItems = Align.Stretch; // Same height for all tabs

            var tabNames = tabControllers.Keys.ToArray();
            foreach (var tabName in tabNames)
            {
                var tabButton = CreateHoyoToonTabButton(tabName);
                tabButtons[tabName] = tabButton;
                tabContainer.Add(tabButton);
            }

            parent.Add(tabContainer);
        }

        private ToolbarButton CreateHoyoToonTabButton(string tabName)
        {
            var button = new ToolbarButton(() => SwitchToHoyoToonTab(tabName));
            button.text = tabName;
            button.style.flexGrow = 1; // Equal distribution
            button.style.flexShrink = 0; // Prevent shrinking
            button.style.height = HoyoToonUILayout.TAB_HEIGHT;
            button.style.marginLeft = 1; // Consistent left margin
            button.style.marginRight = 1; // Consistent right margin
            button.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 0;
            button.style.borderBottomRightRadius = 0;
            button.style.borderTopWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderBottomWidth = 0;
            button.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
            button.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
            button.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
            button.style.unityTextAlign = TextAnchor.MiddleCenter; // Center text alignment
            return button;
        }

        private void CreateHoyoToonQuickActionsSection(VisualElement parent)
        {
            // Store reference to quick actions container for refreshing
            quickActionsContainer = new VisualElement();
            quickActionsContainer.style.marginTop = 10;
            quickActionsContainer.style.marginBottom = 10;
            quickActionsContainer.style.paddingTop = 10;
            quickActionsContainer.style.paddingBottom = 10;
            quickActionsContainer.style.paddingLeft = 15;
            quickActionsContainer.style.paddingRight = 15;
            quickActionsContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            quickActionsContainer.style.borderTopLeftRadius = 8;
            quickActionsContainer.style.borderTopRightRadius = 8;
            quickActionsContainer.style.borderBottomLeftRadius = 8;
            quickActionsContainer.style.borderBottomRightRadius = 8;
            quickActionsContainer.style.flexShrink = 0; // Prevent shrinking
            quickActionsContainer.style.position = Position.Relative; // Maintain relative positioning
            quickActionsContainer.style.alignSelf = Align.Stretch; // Maintain full width

            var headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.alignItems = Align.Center;
            headerContainer.style.marginBottom = 8;

            var quickActionsLabel = new Label("Quick Actions");
            quickActionsLabel.style.fontSize = 14;
            quickActionsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            quickActionsLabel.style.color = Color.white;
            quickActionsLabel.style.marginRight = 10;
            headerContainer.Add(quickActionsLabel);

            quickActionsContainer.Add(headerContainer);

            // Create buttons container with horizontal wrap layout
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.flexWrap = Wrap.Wrap;
            buttonsContainer.style.alignItems = Align.Stretch;
            buttonsContainer.style.justifyContent = Justify.FlexStart;
            quickActionsContainer.Add(buttonsContainer);

            // Don't call RefreshQuickActions here - it will be called after tab initialization
            parent.Add(quickActionsContainer);
        }

        private void RefreshQuickActions()
        {
            if (quickActionsContainer == null) return;

            // Find the buttons container (second child after header)
            var buttonsContainer = quickActionsContainer.ElementAt(1);
            buttonsContainer.Clear();

            // Get quick actions from the active tab
            var quickActions = GetCurrentTabQuickActions();

            if (quickActions != null && quickActions.Count > 0)
            {
                foreach (var action in quickActions)
                {
                    var button = new Button(() =>
                    {
                        action.Action?.Invoke();
                        UpdateHoyoToonUI(); // Refresh UI after any quick action
                    });
                    button.text = action.Label;
                    button.SetEnabled(action.IsEnabled);

                    // Consistent styling for quick action buttons
                    button.style.height = 28;
                    button.style.minWidth = 100;
                    button.style.maxWidth = 150;
                    button.style.flexGrow = 1;
                    button.style.marginRight = 5;
                    button.style.marginBottom = 3;
                    button.style.backgroundColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
                    button.style.borderTopLeftRadius = 4;
                    button.style.borderTopRightRadius = 4;
                    button.style.borderBottomLeftRadius = 4;
                    button.style.borderBottomRightRadius = 4;
                    button.style.color = Color.white;
                    button.style.fontSize = 11;
                    button.style.unityFontStyleAndWeight = FontStyle.Bold;

                    // Hover effects
                    button.RegisterCallback<MouseEnterEvent>(evt =>
                    {
                        button.style.backgroundColor = new Color(0.4f, 0.6f, 0.8f, 0.9f);
                    });
                    button.RegisterCallback<MouseLeaveEvent>(evt =>
                    {
                        button.style.backgroundColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
                    });

                    buttonsContainer.Add(button);
                }
            }
            else
            {
                var noActionsLabel = new Label("No quick actions available for this tab");
                noActionsLabel.style.fontSize = 12;
                noActionsLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                noActionsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noActionsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                noActionsLabel.style.marginTop = 5;
                noActionsLabel.style.marginBottom = 5;
                buttonsContainer.Add(noActionsLabel);
            }
        }

        private List<QuickAction> GetCurrentTabQuickActions()
        {
            // If currentTabController is null, try to get FBX tab controller as fallback
            var tabController = currentTabController;
            if (tabController == null && tabControllers.ContainsKey("FBX"))
            {
                tabController = tabControllers["FBX"];
                Debug.Log("HoyoToon: Using FBX tab as fallback for quick actions");
            }

            if (tabController == null)
            {
                Debug.LogWarning("HoyoToon: No tab controller available for quick actions");
                return new List<QuickAction>(); // Return empty list instead of null
            }

            // Use the modular interface's GetQuickActions method directly
            try
            {
                var actions = tabController.GetQuickActions();

                if (actions != null)
                {
                    Debug.Log($"HoyoToon: Found {actions.Count} quick actions for {tabController.GetType().Name}");
                    // Convert to UI Manager's QuickAction format
                    var convertedActions = new List<QuickAction>();
                    foreach (var action in actions)
                    {
                        convertedActions.Add(new QuickAction(action.Label, action.Action, action.IsEnabled));
                    }
                    return convertedActions;
                }
                else
                {
                    Debug.LogWarning($"HoyoToon: GetQuickActions returned null for {tabController.GetType().Name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"HoyoToon: Failed to get quick actions from {tabController.GetType().Name}: {e.Message}");
            }

            return new List<QuickAction>(); // Return empty list instead of null
        }

        public class QuickAction
        {
            public string Label { get; set; }
            public System.Action Action { get; set; }
            public bool IsEnabled { get; set; } = true;

            public QuickAction(string label, System.Action action, bool isEnabled = true)
            {
                Label = label;
                Action = action;
                IsEnabled = isEnabled;
            }
        }

        private void CreateHoyoToonActionButtonsSection(VisualElement parent)
        {
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.justifyContent = Justify.SpaceAround;
            buttonContainer.style.flexShrink = 0; // Prevent shrinking
            buttonContainer.style.marginTop = 5;
            buttonContainer.style.marginBottom = 5;
            buttonContainer.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            buttonContainer.style.paddingTop = 10;
            buttonContainer.style.paddingBottom = 10;
            buttonContainer.style.paddingLeft = 10;
            buttonContainer.style.paddingRight = 10;
            buttonContainer.style.borderTopLeftRadius = 6;
            buttonContainer.style.borderTopRightRadius = 6;
            buttonContainer.style.borderBottomLeftRadius = 6;
            buttonContainer.style.borderBottomRightRadius = 6;
            buttonContainer.style.borderTopWidth = 1;
            buttonContainer.style.borderBottomWidth = 1;
            buttonContainer.style.borderLeftWidth = 1;
            buttonContainer.style.borderRightWidth = 1;
            buttonContainer.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            buttonContainer.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            buttonContainer.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            buttonContainer.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

            // Full Setup Button
            fullSetupBtn = new Button(RunHoyoToonFullSetup);
            fullSetupBtn.text = "Full Setup";
            fullSetupBtn.style.height = 40;
            fullSetupBtn.style.minWidth = 120;
            fullSetupBtn.style.flexGrow = 1;
            fullSetupBtn.style.marginLeft = 2;
            fullSetupBtn.style.marginRight = 2;
            fullSetupBtn.style.backgroundColor = new Color(0.2f, 0.6f, 0.8f);
            fullSetupBtn.style.borderTopLeftRadius = 8;
            fullSetupBtn.style.borderTopRightRadius = 8;
            fullSetupBtn.style.borderBottomLeftRadius = 8;
            fullSetupBtn.style.borderBottomRightRadius = 8;
            fullSetupBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            fullSetupBtn.style.color = Color.white;

            // Refresh Button
            refreshBtn = new Button(RefreshHoyoToonAnalysis);
            refreshBtn.text = "Refresh";
            refreshBtn.style.height = 40;
            refreshBtn.style.minWidth = 80;
            refreshBtn.style.flexGrow = 1;
            refreshBtn.style.marginLeft = 2;
            refreshBtn.style.marginRight = 2;
            refreshBtn.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
            refreshBtn.style.borderTopLeftRadius = 8;
            refreshBtn.style.borderTopRightRadius = 8;
            refreshBtn.style.borderBottomLeftRadius = 8;
            refreshBtn.style.borderBottomRightRadius = 8;
            refreshBtn.style.color = Color.white;

            // Settings Button -> replaced with Add to Scene Button
            addToSceneBtn = new Button(AddHoyoToonModelToScene);
            addToSceneBtn.text = "Add to Scene";
            addToSceneBtn.style.height = 40;
            addToSceneBtn.style.minWidth = 100;
            addToSceneBtn.style.flexGrow = 1;
            addToSceneBtn.style.marginLeft = 2;
            addToSceneBtn.style.marginRight = 2;
            addToSceneBtn.style.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
            addToSceneBtn.style.borderTopLeftRadius = 8;
            addToSceneBtn.style.borderTopRightRadius = 8;
            addToSceneBtn.style.borderBottomLeftRadius = 8;
            addToSceneBtn.style.borderBottomRightRadius = 8;
            addToSceneBtn.style.color = Color.white;

            buttonContainer.Add(fullSetupBtn);
            buttonContainer.Add(refreshBtn);
            buttonContainer.Add(addToSceneBtn);
            parent.Add(buttonContainer);
        }

        private void CreateHoyoToonPreviewSection(VisualElement parent)
        {
            var previewPanel = new VisualElement();
            previewPanel.style.width = HoyoToonUILayout.PREVIEW_PANEL_WIDTH;
            previewPanel.style.minWidth = 300; // Ensure minimum width
            previewPanel.style.maxWidth = 500; // Updated to allow larger preview with the new window size
            previewPanel.style.flexShrink = 0; // Prevent shrinking
            previewPanel.style.flexDirection = FlexDirection.Column;
            previewPanel.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            previewPanel.style.borderTopLeftRadius = 8;
            previewPanel.style.borderTopRightRadius = 8;
            previewPanel.style.borderBottomLeftRadius = 8;
            previewPanel.style.borderBottomRightRadius = 8;
            previewPanel.style.borderTopWidth = 1;
            previewPanel.style.borderBottomWidth = 1;
            previewPanel.style.borderLeftWidth = 1;
            previewPanel.style.borderRightWidth = 1;
            previewPanel.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            previewPanel.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            previewPanel.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            previewPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            previewPanel.style.paddingTop = 10;
            previewPanel.style.paddingBottom = 10;
            previewPanel.style.paddingLeft = 10;
            previewPanel.style.paddingRight = 10;

            var previewHeader = new Label("Model Preview");
            previewHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewHeader.style.fontSize = 14;
            previewHeader.style.marginBottom = 10;
            previewHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
            previewHeader.style.color = Color.white;
            previewPanel.Add(previewHeader);

            previewContainer = new VisualElement();
            previewContainer.style.flexGrow = 1;
            previewContainer.style.flexShrink = 0; // Prevent shrinking of preview
            previewContainer.style.minHeight = 480; // Increased from 350 to better utilize the 900px window height
            previewContainer.style.maxHeight = 650; // Increased from 500 to allow more preview space in the larger window
            previewContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            previewContainer.style.borderTopLeftRadius = 4;
            previewContainer.style.borderTopRightRadius = 4;
            previewContainer.style.borderBottomLeftRadius = 4;
            previewContainer.style.borderBottomRightRadius = 4;
            previewContainer.style.borderTopWidth = 1;
            previewContainer.style.borderBottomWidth = 1;
            previewContainer.style.borderLeftWidth = 1;
            previewContainer.style.borderRightWidth = 1;
            previewContainer.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            previewContainer.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            previewContainer.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            previewContainer.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            // Add overflow hidden to ensure content stays within bounds
            previewContainer.style.overflow = Overflow.Hidden;

            // Preview IMGUI Container with constrained sizing
            previewIMGUI = new IMGUIContainer(OnHoyoToonPreviewGUI);
            previewIMGUI.style.flexGrow = 1;
            previewIMGUI.style.flexShrink = 0;
            previewIMGUI.style.overflow = Overflow.Hidden; // Keep preview content within bounds
            previewContainer.Add(previewIMGUI);

            previewPanel.Add(previewContainer);

            var previewInfo = new Label("Left drag: rotate • Right drag: pan • Scroll: zoom • F: reset view");
            previewInfo.style.fontSize = 9;
            previewInfo.style.color = new Color(0.7f, 0.7f, 0.7f);
            previewInfo.style.unityTextAlign = TextAnchor.MiddleCenter;
            previewInfo.style.marginTop = 5;
            previewInfo.style.flexShrink = 0; // Prevent shrinking
            previewPanel.Add(previewInfo);

            parent.Add(previewPanel);
        }

        private void CreateHoyoToonStatusBar(VisualElement parent)
        {
            statusBar = new VisualElement();
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.alignItems = Align.Center;
            statusBar.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            statusBar.style.paddingTop = 5;
            statusBar.style.paddingBottom = 5;
            statusBar.style.paddingLeft = 10;
            statusBar.style.paddingRight = 10;
            statusBar.style.height = HoyoToonUILayout.STATUS_BAR_HEIGHT;

            statusLabel = new Label("Ready");
            statusLabel.style.flexGrow = 1;
            statusLabel.style.fontSize = 12;
            statusLabel.style.color = Color.white;

            progressBar = new ProgressBar();
            progressBar.style.width = 200;
            progressBar.style.display = DisplayStyle.None;

            statusBar.Add(statusLabel);
            statusBar.Add(progressBar);
            parent.Add(statusBar);
        }

        private void CreateHoyoToonFallbackUI()
        {
            rootVisualElement.Clear();
            var fallbackContainer = new VisualElement();
            fallbackContainer.style.alignItems = Align.Center;
            fallbackContainer.style.justifyContent = Justify.Center;
            fallbackContainer.style.flexGrow = 1;

            var fallbackLabel = new Label("HoyoToon Manager - UI loading failed. Check console for details.");
            fallbackLabel.style.color = Color.red;
            fallbackLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            fallbackLabel.style.fontSize = 16;
            fallbackLabel.style.marginTop = 20;
            fallbackLabel.style.marginLeft = 20;
            fallbackLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            var retryBtn = new Button(() =>
            {
                try
                {
                    CreateHoyoToonUI();
                }
                catch (System.Exception e)
                {
                    HoyoToonLogs.ErrorDebug($"Retry failed: {e.Message}");
                }
            });
            retryBtn.text = "Retry";
            retryBtn.style.marginTop = 10;
            retryBtn.style.width = 100;
            retryBtn.style.height = 30;

            fallbackContainer.Add(fallbackLabel);
            fallbackContainer.Add(retryBtn);
            rootVisualElement.Add(fallbackContainer);
        }

        #endregion

        #region Event Handlers

        private void OnHoyoToonModelSelected(ChangeEvent<UnityEngine.Object> evt)
        {
            var newModel = evt.newValue as GameObject;

            if (newModel != null && !ValidateHoyoToonFBXAsset(newModel))
            {
                EditorUtility.DisplayDialog("Invalid Selection",
                    "Please select a valid FBX model file, preferably one converted with Hoyo2VRC.", "OK");
                modelSelector.value = null;
                return;
            }

            if (newModel != selectedModel)
            {
                selectedModel = newModel;

                if (selectedModel != null)
                {
                    statusLabel.text = "Analyzing model...";

                    // Step 1: Set model on all tabs WITHOUT triggering updates yet
                    SetModelOnAllTabsWithoutUpdate(selectedModel);

                    // Step 2: Perform analysis once (this is the expensive operation)
                    AnalyzeHoyoToonSelectedModel();

                    // Step 3: Now update everything with the completed analysis
                    UpdateAllSystemsWithAnalysisData();
                }
                else
                {
                    // Model was cleared
                    analysisData = new HoyoToonModelAnalysisData();
                    SetModelOnAllTabsWithoutUpdate(null);
                    UpdateAllSystemsWithAnalysisData();
                }
            }
        }

        /// <summary>
        /// Set the model on all tabs without triggering content updates
        /// This prevents redundant updates before analysis is complete
        /// </summary>
        private void SetModelOnAllTabsWithoutUpdate(GameObject model)
        {
            foreach (var controller in tabControllers.Values)
            {
                // Use the modular interface's SetSelectedModel method
                controller.SetSelectedModel(model);
            }
        }

        /// <summary>
        /// Update all systems after analysis is complete
        /// This ensures everything updates with complete data in the correct order
        /// </summary>
        private void UpdateAllSystemsWithAnalysisData()
        {
            // Update UI elements
            UpdateHoyoToonUI();

            // Update banner data using the analysis results
            UpdateHoyoToonBannerData();

            // Update all tab controllers with analysis data
            UpdateAllTabControllersWithAnalysisData();

            // Update quick actions for current tab
            RefreshQuickActions();

            // Update current tab content if active
            if (currentTabController != null)
            {
                currentTabController.UpdateContent();
            }

            // Update preview
            UpdateHoyoToonPreview();

            // Force repaint
            Repaint();
        }

        /// <summary>
        /// Update all tab controllers with the latest analysis data
        /// </summary>
        private void UpdateAllTabControllersWithAnalysisData()
        {
            foreach (var controller in tabControllers.Values)
            {
                // Use the modular interface's SetAnalysisData method
                controller.SetAnalysisData(analysisData);
            }
        }

        private void UpdateAllTabControllersWithModel(GameObject model)
        {
            foreach (var controller in tabControllers.Values)
            {
                // Use the modular interface's SetSelectedModel method
                controller.SetSelectedModel(model);
            }
        }

        private void OnHoyoToonAnalysisComplete(HoyoToonModelAnalysisData data)
        {
            analysisData = data;

            // Update status text to focus on preparation workflow
            if (analysisData.readyForSetup)
            {
                statusLabel.text = "Model ready for HoyoToon conversion";
            }
            else if (analysisData.issues.Count > 0)
            {
                statusLabel.text = $"Preparation needed: {analysisData.issues.Count} items require attention";
            }
            else
            {
                statusLabel.text = "Analyzing model for HoyoToon preparation...";
            }

            progressBar.style.display = DisplayStyle.None;

            // Force immediate UI update with new analysis data
            UpdateHoyoToonUI();

            // Refresh quick actions immediately with analysis data
            RefreshQuickActions();

            // Update all tab controllers with new analysis data by re-initializing them
            foreach (var controller in tabControllers.Values)
            {
                controller.Initialize(contentView, analysisData);
                controller.UpdateContent();
            }

            // Force repaint to show all changes immediately
            Repaint();
        }

        private void OnHoyoToonPlayModeStateChanged(PlayModeStateChange state)
        {
            // Clean up preview when entering play mode
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CleanupHoyoToonPreviewSystem();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SetupHoyoToonPreviewSystem();
            }
        }

        private void OnHoyoToonProjectChanged()
        {
            // Delay the refresh to allow asset database to complete operations
            EditorApplication.delayCall += () =>
            {
                if (selectedModel != null && this != null)
                {
                    ForceAnalysisRefresh();
                }
            };
        }

        private void OnHoyoToonAssetImportCompleted(string packageName)
        {
            // Refresh UI after asset imports (like materials being generated)
            EditorApplication.delayCall += () =>
            {
                if (selectedModel != null && this != null)
                {
                    ForceAnalysisRefresh();
                }
            };
        }

        private void OnHoyoToonHierarchyChanged()
        {
            // Prevent recursive calls during preview updates or if change is already pending
            if (isUpdatingPreview || hierarchyChangePending) return;

            hierarchyChangePending = true;

            // Delay the check to allow hierarchy operations to complete
            EditorApplication.delayCall += () =>
            {
                hierarchyChangePending = false;

                if (selectedModel != null && this != null && !isUpdatingPreview)
                {
                    // Store the current model-in-scene state before processing
                    bool wasModelInScene = IsModelAlreadyInScene();

                    // Check if the scene instance was actually removed (not just disabled) and notify Scene tab
                    if (tabControllers.ContainsKey("Scene") && tabControllers["Scene"] is HoyoToonSceneTabController sceneController)
                    {
                        // Let the scene controller validate its instance reference
                        sceneController.UpdateContent();
                    }

                    // Only trigger major updates if the model's presence in scene actually changed
                    // This prevents unnecessary updates when just toggling individual GameObjects
                    bool isModelInSceneNow = IsModelAlreadyInScene();

                    if (wasModelInScene != isModelInSceneNow)
                    {
                        HoyoToonLogs.LogDebug($"Model scene presence changed: was {wasModelInScene}, now {isModelInSceneNow}");

                        // Refresh quick actions when hierarchy changes (models added/removed from scene)
                        RefreshQuickActions();

                        // Update validation indicators and button states to reflect scene changes
                        UpdateHoyoToonValidationIndicators();

                        // Refresh the current tab content to reflect scene state changes
                        UpdateHoyoToonCurrentTab();
                    }
                    else
                    {
                        // Model is still in scene, just minor hierarchy changes (toggling objects, etc.)
                        HoyoToonLogs.LogDebug("Minor hierarchy change detected, model still in scene");

                        // Only refresh the scene tab content if it's active, don't trigger major UI updates
                        if (currentTab == "Scene")
                        {
                            UpdateHoyoToonCurrentTab();
                        }
                    }

                    // Always repaint to update preview area
                    Repaint();
                }
            };
        }

        #endregion

        #region Core Functionality

        private bool ValidateHoyoToonFBXAsset(UnityEngine.Object obj)
        {
            if (obj == null) return false;
            string assetPath = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
        }

        private void AnalyzeHoyoToonSelectedModel()
        {
            if (selectedModel == null)
            {
                analysisData = new HoyoToonModelAnalysisData();
                statusLabel.text = "Select a model to analyze";
                progressBar.style.display = DisplayStyle.None;
                InvalidateAnalysisCache();
                return;
            }

            // Check if we can use cached analysis
            if (isAnalysisCacheValid && lastAnalyzedModel == selectedModel && cachedAnalysisData != null)
            {
                HoyoToonLogs.LogDebug($"Using cached analysis for model: {selectedModel.name}");
                analysisData = cachedAnalysisData;
                statusLabel.text = "Analysis complete (cached)";
                progressBar.style.display = DisplayStyle.None;
                return;
            }

            statusLabel.text = "Analyzing model comprehensively...";
            progressBar.style.display = DisplayStyle.Flex;
            progressBar.value = 0;

            HoyoToonLogs.LogDebug($"Starting comprehensive analysis for model: {selectedModel.name}");

            // Perform comprehensive analysis and cache the results
            analysisData = HoyoToonModelAnalysisComponent.AnalyzeModel(selectedModel);

            // Cache the analysis results
            cachedAnalysisData = analysisData;
            lastAnalyzedModel = selectedModel;
            isAnalysisCacheValid = true;

            statusLabel.text = "Analysis complete";
            progressBar.style.display = DisplayStyle.None;

            HoyoToonLogs.LogDebug($"Comprehensive analysis completed and cached for model: {selectedModel.name}");
        }

        /// <summary>
        /// Invalidate the analysis cache when changes are made through the UI
        /// </summary>
        public void InvalidateAnalysisCache()
        {
            isAnalysisCacheValid = false;
            cachedAnalysisData = null;
            lastAnalyzedModel = null;
            HoyoToonLogs.LogDebug("Analysis cache invalidated - next analysis will be comprehensive");
        }

        /// <summary>
        /// Static method for tab controllers to request cache invalidation
        /// </summary>
        public static void RequestAnalysisCacheInvalidation()
        {
            OnAnalysisCacheInvalidationRequested?.Invoke();
        }

        /// <summary>
        /// Quick check if model has been processed by Hoyo2VRC
        /// </summary>
        private bool CheckIfHoyo2VRCProcessed(GameObject model)
        {
            // Simple check - look for typical Hoyo2VRC naming patterns or structure
            // This is much faster than full analysis
            return model.name.Contains("Hoyo2VRC") ||
                   model.transform.Find("Armature") != null ||
                   model.GetComponentsInChildren<Transform>().Any(t => t.name.Contains("VRC"));
        }

        private void RefreshHoyoToonAnalysis()
        {
            if (selectedModel != null)
            {
                AnalyzeHoyoToonSelectedModel();
            }
        }

        /// <summary>
        /// Force immediate re-analysis and UI refresh after setup operations
        /// This ensures the UI reflects changes after materials are generated, FBX is setup, etc.
        /// </summary>
        public void ForceAnalysisRefresh()
        {
            if (selectedModel != null)
            {
                // Re-analyze the current model
                AnalyzeHoyoToonSelectedModel();

                // Update all systems with the new analysis data
                UpdateAllSystemsWithAnalysisData();

                HoyoToonLogs.LogDebug("Forced analysis refresh completed.");
            }
        }
        private void OpenHoyoToonSettings()
        {
            // Switch to settings tab
            SwitchToHoyoToonTab("Settings");
        }

        private void AddHoyoToonModelToScene()
        {
            if (selectedModel == null)
            {
                EditorUtility.DisplayDialog("No Model Selected", "Please select a model first before adding to scene.", "OK");
                return;
            }

            // Check if this should be an add or remove operation
            if (IsModelAlreadyInScene())
            {
                // Remove model from scene
                RemoveHoyoToonModelFromScene();
            }
            else
            {
                // Add model to scene
                try
                {
                    // Directly instantiate the selected model without changing Unity's selection
                    // This prevents potential issues with selection change callbacks triggering duplicate instantiation
                    GameObject sceneObject = GameObject.Instantiate(selectedModel);
                    sceneObject.name = selectedModel.name;
                    Undo.RegisterCreatedObjectUndo(sceneObject, "Add HoyoToon Model to Scene");

                    if (sceneObject != null)
                    {
                        EditorUtility.DisplayDialog("Success", "Model added to scene successfully! You can now see the final configured result in the Scene view.", "OK");

                        // Focus on the added object in scene
                        Selection.activeGameObject = sceneObject;
                        EditorGUIUtility.PingObject(sceneObject);

                        // Clean up the preview since we now have the model in scene
                        CleanupCurrentPreview();

                        // Notify Scene tab controller about the new scene instance
                        if (tabControllers.ContainsKey("Scene") && tabControllers["Scene"] is HoyoToonSceneTabController sceneController)
                        {
                            sceneController.SetSceneInstance(sceneObject);
                        }

                        // Refresh UI to reflect that model is now in scene
                        RefreshQuickActions();
                        UpdateHoyoToonCurrentTab();
                        UpdateHoyoToonValidationIndicators(); // Update button state immediately

                        // Optionally focus the scene view on the object
                        if (SceneView.lastActiveSceneView != null)
                        {
                            SceneView.lastActiveSceneView.FrameSelected();
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", "Failed to add model to scene. Please try dragging the model manually from the Project window.", "OK");
                    }
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to add model to scene: {e.Message}", "OK");
                    HoyoToonLogs.ErrorDebug($"Error adding model to scene: {e.Message}");
                }
            }
        }

        private void RemoveHoyoToonModelFromScene()
        {
            if (selectedModel == null) return;

            try
            {
                // Find the model instance in the scene
                GameObject sceneInstance = FindModelInstanceInScene();

                if (sceneInstance != null)
                {
                    if (EditorUtility.DisplayDialog("Confirm",
                        $"Remove model '{selectedModel.name}' from scene?", "Yes", "Cancel"))
                    {
                        // Notify Scene tab controller that the instance is being removed
                        if (tabControllers.ContainsKey("Scene") && tabControllers["Scene"] is HoyoToonSceneTabController sceneController)
                        {
                            sceneController.ClearSceneInstance();
                        }

                        Undo.DestroyObjectImmediate(sceneInstance);

                        EditorUtility.DisplayDialog("Success", "Model removed from scene successfully!", "OK");

                        // Refresh UI to reflect that model is no longer in scene
                        RefreshQuickActions();
                        UpdateHoyoToonCurrentTab();
                        UpdateHoyoToonValidationIndicators(); // Update button state immediately
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Not Found", "Model instance not found in scene.", "OK");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to remove model from scene: {e.Message}", "OK");
                HoyoToonLogs.ErrorDebug($"Error removing model from scene: {e.Message}");
            }
        }

        private GameObject FindModelInstanceInScene()
        {
            if (selectedModel == null) return null;

            // Find all GameObjects in the scene that might be instances of our model
            var allGameObjects = FindObjectsOfType<GameObject>();

            foreach (var obj in allGameObjects)
            {
                // Skip preview instances and prefabs
                if (obj.hideFlags == HideFlags.HideAndDontSave) continue;
                if (obj.name.StartsWith("Preview_")) continue;

                // Check if this object was instantiated from our selected model
                var objName = obj.name.Replace("(Clone)", "").Trim();
                var modelName = selectedModel.name;

                if (objName == modelName || obj.name.Contains(modelName))
                {
                    // Additional verification: check if it has similar structure
                    if (HasSimilarStructure(obj, selectedModel))
                    {
                        return obj;
                    }
                }
            }

            return null;
        }

        private void SwitchToHoyoToonTab(string tabName)
        {
            if (!tabControllers.ContainsKey(tabName))
                return;

            // Deselect current tab
            if (!string.IsNullOrEmpty(currentTab) && tabControllers.ContainsKey(currentTab))
            {
                tabControllers[currentTab].OnTabDeselected();
                if (tabButtons.ContainsKey(currentTab))
                {
                    tabButtons[currentTab].style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                }
            }

            currentTab = tabName;
            currentTabController = tabControllers[currentTab]; // Update current tab controller reference

            // Select new tab
            var controller = tabControllers[currentTab];

            // Initialize the controller with current data (this is lightweight)
            controller.Initialize(contentView, analysisData);
            if (selectedModel != null)
            {
                // Use the modular interface's SetSelectedModel method
                controller.SetSelectedModel(selectedModel);
            }

            // OnTabSelected will set isActive = true and create content
            controller.OnTabSelected();

            if (tabButtons.ContainsKey(currentTab))
            {
                tabButtons[currentTab].style.backgroundColor = new Color(0.3f, 0.5f, 0.7f);
            }

            // Only refresh quick actions (don't update entire UI unnecessarily)
            RefreshQuickActions();

            // Force repaint to show tab changes immediately
            Repaint();
        }

        private void UpdateHoyoToonUI()
        {
            UpdateHoyoToonValidationIndicators();
            UpdateHoyoToonCurrentTab();
            UpdateHoyoToonBannerData();
        }

        private void UpdateHoyoToonValidationIndicators()
        {
            if (analysisData == null) return;

            // Change focus from "validation" to "preparation status"
            // Green = Ready/Complete, Yellow = Needs Action, Red = Critical Issue

            validationShaders.text = "● Resources";
            validationMaterials.text = "● Materials";
            validationTextures.text = "● Textures";
            validationModel.text = "● Model";
            validationRig.text = "● Rig";

            // Model status
            if (analysisData.hasValidModel)
            {
                validationModel.style.color = Color.green;
                validationModel.text = "● Model Loaded";
            }
            else
            {
                validationModel.style.color = Color.red;
                validationModel.text = "● No Model Selected";
            }

            // Rig status - focus on Hoyo2VRC processing first, then rig type
            if (!analysisData.isHoyo2VRCConverted)
            {
                validationRig.style.color = Color.red;
                validationRig.text = "● Needs Hoyo2VRC";
            }
            else if (analysisData.isHumanoidRig)
            {
                validationRig.style.color = Color.green;
                validationRig.text = "● Humanoid Ready";
            }
            else
            {
                validationRig.style.color = Color.yellow;
                validationRig.text = "● Generic - Setup Needed";
            }

            // Materials status - missing vs needs generation
            if (analysisData.hasMaterials && analysisData.hasCorrectShaders)
            {
                validationMaterials.style.color = Color.green;
                validationMaterials.text = "● Materials Ready";
            }
            else if (analysisData.hasMaterials)
            {
                validationMaterials.style.color = Color.yellow;
                validationMaterials.text = "● Mat's Need Generation";
            }
            else
            {
                validationMaterials.style.color = Color.red;
                validationMaterials.text = "● Missing Materials";
            }

            // Textures status
            if (analysisData.hasTextures)
            {
                validationTextures.style.color = Color.green;
                validationTextures.text = "● Textures Ready";
            }
            else
            {
                validationTextures.style.color = Color.yellow;
                validationTextures.text = "● Textures Need Configuration";
            }

            // Resources status - check if all game resources are downloaded
            bool hasAllResourcesDownloaded = CheckAllResourcesDownloaded();
            if (hasAllResourcesDownloaded)
            {
                validationShaders.style.color = Color.green;
                validationShaders.text = "● Resources Ready";
            }
            else
            {
                validationShaders.style.color = Color.yellow;
                validationShaders.text = "● Need Download";
            }

            // Update button states and text based on prerequisites and readiness
            bool meetsPrereqs = selectedModel != null && analysisData.hasValidModel && analysisData.isHoyo2VRCConverted && hasAllResourcesDownloaded;

            // Check if there are any setup steps needed (only if prerequisites are met)
            bool hasSetupStepsNeeded = false;
            if (meetsPrereqs)
            {
                var neededSteps = DetermineNeededSetupSteps();
                hasSetupStepsNeeded = neededSteps.Count > 0;
            }

            // Enable button if prereqs are met (either for setup steps OR for adding to scene)
            fullSetupBtn.SetEnabled(meetsPrereqs);

            if (!selectedModel)
            {
                fullSetupBtn.text = "Select Model";
            }
            else if (!analysisData.hasValidModel)
            {
                fullSetupBtn.text = "Invalid Model";
            }
            else if (!analysisData.isHoyo2VRCConverted)
            {
                fullSetupBtn.text = "Process with Hoyo2VRC First";
            }
            else if (!hasAllResourcesDownloaded)
            {
                fullSetupBtn.text = "Download Resources First";
            }
            else if (!hasSetupStepsNeeded)
            {
                fullSetupBtn.text = "Add to Scene";
            }
            else
            {
                fullSetupBtn.text = "Full Setup";
            }

            refreshBtn.SetEnabled(selectedModel != null);

            // Update "Add to Scene" button based on model selection and whether it's already in scene
            if (addToSceneBtn != null)
            {
                bool modelInScene = selectedModel != null && IsModelAlreadyInScene();
                addToSceneBtn.SetEnabled(selectedModel != null);

                // Removed debug logging to prevent console spam during UI interactions
                // The button state changes are visual and don't need constant logging

                if (selectedModel == null)
                {
                    addToSceneBtn.text = "Add to Scene";
                    addToSceneBtn.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f); // Gray when disabled
                }
                else if (modelInScene)
                {
                    addToSceneBtn.text = "Remove from Scene";
                    addToSceneBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f); // Red when showing Remove
                }
                else
                {
                    addToSceneBtn.text = "Add to Scene";
                    addToSceneBtn.style.backgroundColor = new Color(0.3f, 0.7f, 0.3f); // Green when available
                }
            }
        }

        private bool CheckAllResourcesDownloaded()
        {
            // Check if all game resources are downloaded using the HoyoToonResourceManager
            var resourceStatus = HoyoToonResourceManager.GetResourceStatus();

            // All games need to have their resources downloaded
            // Use the actual keys from HoyoToonResourceConfig.Games
            return resourceStatus.ContainsKey("Genshin") && resourceStatus["Genshin"].HasResources &&
                   resourceStatus.ContainsKey("StarRail") && resourceStatus["StarRail"].HasResources &&
                   resourceStatus.ContainsKey("Hi3") && resourceStatus["Hi3"].HasResources &&
                   resourceStatus.ContainsKey("Wuwa") && resourceStatus["Wuwa"].HasResources &&
                   resourceStatus.ContainsKey("ZZZ") && resourceStatus["ZZZ"].HasResources;
        }

        private void UpdateHoyoToonCurrentTab()
        {
            // Only update current tab content if there's actually a need to refresh
            // Avoid constant rebuilding which causes performance issues
            if (!string.IsNullOrEmpty(currentTab) && tabControllers.ContainsKey(currentTab))
            {
                // Don't call UpdateContent() here as it causes constant rebuilding
                // Tab content is updated when:
                // 1. Tab is first selected (OnTabSelected)
                // 2. Model changes (SetSelectedModel)
                // 3. Manual refresh is requested
                // This prevents unnecessary rebuilds during regular UI updates
            }
        }

        private void UpdateHoyoToonBannerData()
        {
            if (bannerData == null) return;

            // Update banner based on selected model and use the DataManager's detection system
            // Only update if we have a valid selected model - don't use random Project selections
            if (selectedModel != null && analysisData != null)
            {
                // Use the existing DataManager system instead of the analysis data's string-based detection
                HoyoToonDataManager.GetHoyoToonData();
                HoyoToonParseManager.DetermineBodyType(selectedModel);

                // Get the current game info from the DataManager system
                var (gameName, shaderKey, bodyType) = HoyoToonParseManager.GetCurrentGameInfo();

                bannerData.detectedGame = GetGameCategoryFromBodyType(bodyType);
                bannerData.detectedCharacter = analysisData.detectedCharacter ?? "Unknown";

                HoyoToonLogs.LogDebug($"Banner updated from selected model - Game: {bannerData.detectedGame}, BodyType: {bodyType}, Character: {bannerData.detectedCharacter}");
            }
            else
            {
                // Don't change banner data when no model is selected in HoyoToon
                // This prevents the Scene tab from changing based on Project selections
                if (bannerData.detectedGame == null)
                {
                    bannerData.detectedGame = "Auto";
                    bannerData.detectedCharacter = "Wise"; // Default
                }
                HoyoToonLogs.LogDebug("Banner data unchanged - no valid selected model in HoyoToon");
            }

            // Load character textures based on detected game
            LoadHoyoToonCharacterTextures();
        }

        private void LoadHoyoToonCharacterTextures()
        {
            // Reset textures first
            bannerData.characterLeftTexture = null;
            bannerData.characterRightTexture = null;

            // Map game types to actual file prefixes
            string gamePrefix = GetGameFilePrefix(bannerData.detectedGame);

            if (!string.IsNullOrEmpty(gamePrefix))
            {
                // Load left and right character images based on actual file structure
                bannerData.characterLeftTexture = Resources.Load<Texture2D>($"UI/{gamePrefix}l");
                bannerData.characterRightTexture = Resources.Load<Texture2D>($"UI/{gamePrefix}r");

                // Also try to load the logo for this game
                var gameLogo = Resources.Load<Texture2D>($"UI/{gamePrefix}logo");
                if (gameLogo != null)
                {
                    bannerData.logoTexture = gameLogo;
                }
            }

            // Fallback to default HoyoToon banner if no specific game assets found
            if (bannerData.characterLeftTexture == null && bannerData.characterRightTexture == null)
            {
                // Use default HoyoToon logo
                bannerData.logoTexture = Resources.Load<Texture2D>("UI/hoyotoon");
            }
        }

        private string GetGameCategoryFromBodyType(HoyoToonParseManager.BodyType bodyType)
        {
            switch (bodyType)
            {
                // Genshin Impact variants
                case HoyoToonParseManager.BodyType.GIBoy:
                case HoyoToonParseManager.BodyType.GIGirl:
                case HoyoToonParseManager.BodyType.GILady:
                case HoyoToonParseManager.BodyType.GIMale:
                case HoyoToonParseManager.BodyType.GILoli:
                    return "GenshinImpact";

                // Honkai Star Rail variants
                case HoyoToonParseManager.BodyType.HSRMaid:
                case HoyoToonParseManager.BodyType.HSRKid:
                case HoyoToonParseManager.BodyType.HSRLad:
                case HoyoToonParseManager.BodyType.HSRMale:
                case HoyoToonParseManager.BodyType.HSRLady:
                case HoyoToonParseManager.BodyType.HSRGirl:
                case HoyoToonParseManager.BodyType.HSRBoy:
                case HoyoToonParseManager.BodyType.HSRMiss:
                    return "HonkaiStarRail";

                // Honkai Impact 3rd variants
                case HoyoToonParseManager.BodyType.HI3P1:
                    return "HonkaiImpact";
                case HoyoToonParseManager.BodyType.HI3P2:
                    return "HonkaiImpactP2";

                // Wuthering Waves
                case HoyoToonParseManager.BodyType.WuWa:
                    return "WutheringWaves";

                // Zenless Zone Zero
                case HoyoToonParseManager.BodyType.ZZZ:
                    return "ZenlessZoneZero";

                default:
                    return "Auto";
            }
        }

        private string GetGameFilePrefix(string detectedGame)
        {
            if (string.IsNullOrEmpty(detectedGame) || detectedGame == "Auto")
                return "";

            // Handle main game categories for UI logo mapping
            switch (detectedGame.ToLower())
            {
                // Genshin Impact
                case "genshin":
                case "gi":
                case "genshinimpact":
                    return "gi";

                // Honkai Impact 3rd Part 1
                case "honkai":
                case "hi3":
                case "honkaiimpact":
                case "hi3p1":
                case "honkaiimpactpart1":
                    return "hi3p1";

                // Honkai Impact 3rd Part 2
                case "hi3p2":
                case "honkaiimpactpart2":
                case "honkaiimpactp2":
                    return "hi3p2";

                // Honkai Star Rail
                case "starrail":
                case "hsr":
                case "honkaistarrail":
                    return "hsr";

                // Wuthering Waves
                case "wuthering":
                case "wuwa":
                case "wutheringwaves":
                    return "wuwa";

                // Zenless Zone Zero
                case "zenless":
                case "zzz":
                case "zenlesszonezero":
                    return "zzz";

                default:
                    return "";
            }
        }

        private Color GetHoyoToonGameThemeColor(string game)
        {
            switch (game?.ToLower())
            {
                case "genshin":
                case "gi":
                    return new Color(0.2f, 0.4f, 0.8f, 1f); // Blue
                case "honkai":
                case "hi3":
                    return new Color(0.8f, 0.2f, 0.4f, 1f); // Red/Pink
                case "starrail":
                case "hsr":
                    return new Color(0.6f, 0.3f, 0.8f, 1f); // Purple
                case "wuthering":
                case "wuwa":
                    return new Color(0.2f, 0.8f, 0.4f, 1f); // Green
                case "zenless":
                case "zzz":
                    return new Color(0.9f, 0.6f, 0.1f, 1f); // Orange
                default:
                    return new Color(0.15f, 0.15f, 0.25f, 1f); // Default dark blue
            }
        }

        private void RunHoyoToonFullSetup()
        {
            if (selectedModel == null)
            {
                EditorUtility.DisplayDialog("No Model Selected",
                    "Please select an FBX model before running full setup.", "OK");
                return;
            }

            if (!analysisData.isHoyo2VRCConverted)
            {
                EditorUtility.DisplayDialog("Prerequisites Not Met",
                    "This model must be processed by Hoyo2VRC first. HoyoToon detected that this model doesn't have the required bone naming structure (Bip bones). Please process it with Hoyo2VRC and try again.", "OK");
                return;
            }

            try
            {
                statusLabel.text = "Analyzing setup requirements...";
                progressBar.style.display = DisplayStyle.Flex;
                progressBar.value = 0;

                Selection.activeObject = selectedModel;

                // Determine what steps actually need to be performed
                var setupSteps = DetermineNeededSetupSteps();

                if (setupSteps.Count == 0)
                {
                    // All setup steps are complete - check if model is already in scene
                    if (!IsModelAlreadyInScene())
                    {
                        statusLabel.text = "Adding model to scene...";
                        progressBar.value = 50;

                        // Add model to scene since everything else is ready
                        var sceneObject = HoyoToonSceneManager.AddSelectedObjectToScene();
                        if (sceneObject != null)
                        {
                            // Ensure materials are properly applied to the scene instance
                            ApplyMaterialsToSceneObject(sceneObject);
                            statusLabel.text = "Model successfully added to scene!";
                            progressBar.value = 100;

                            // Hide progress bar after a brief moment
                            EditorApplication.delayCall += () =>
                            {
                                progressBar.style.display = DisplayStyle.None;
                            };
                        }
                        else
                        {
                            statusLabel.text = "Failed to add model to scene";
                            progressBar.style.display = DisplayStyle.None;
                        }
                    }
                    else
                    {
                        statusLabel.text = "Model is already fully configured and in scene!";
                        progressBar.style.display = DisplayStyle.None;
                        EditorUtility.DisplayDialog("Already Complete",
                            "This model is already fully configured for HoyoToon and present in the scene. No additional setup is needed!", "OK");
                    }
                    return;
                }

                statusLabel.text = $"Running {setupSteps.Count} setup steps...";
                ExecuteSetupSteps(setupSteps);
            }
            catch (System.Exception e)
            {
                statusLabel.text = $"Setup failed: {e.Message}";
                HoyoToonLogs.ErrorDebug($"Full setup failed: {e.Message}");
                progressBar.style.display = DisplayStyle.None;
            }
        }

        private List<SetupStep> DetermineNeededSetupSteps()
        {
            var neededSteps = new List<SetupStep>();

            // Step 1: Material Generation (FIRST - before FBX setup to ensure materials exist)
            // Always generate materials if they don't exist, regardless of shader status
            bool needsMaterialGeneration = !analysisData.hasCorrectShaders ||
                                          analysisData.materials.Any(m => m.needsShaderUpdate) ||
                                          !DoMaterialsExistInProject();

            if (needsMaterialGeneration)
            {
                neededSteps.Add(new SetupStep
                {
                    name = "Generate/Update Materials",
                    action = () =>
                    {
                        // Ensure the correct model is selected before calling material generation
                        var previousSelection = Selection.activeObject;
                        Selection.activeObject = selectedModel;
                        try
                        {
                            statusLabel.text = "Generating materials...";
                            progressBar.value = 25;

                            HoyoToonLogs.LogDebug("Generating materials for model...");

                            // Use core manager's optimized function
                            HoyoToonMaterialManager.GenerateMaterialsFromJson();

                            // Invalidate analysis cache since materials changed
                            InvalidateAnalysisCache();

                            statusLabel.text = "Refreshing assets...";
                            progressBar.value = 50;

                            // Force asset refresh to ensure materials are loaded
                            AssetDatabase.Refresh();

                            statusLabel.text = "Applying materials...";
                            progressBar.value = 75;

                            // Wait for asset import to complete
                            EditorApplication.delayCall += () =>
                            {
                                // Immediately apply materials to the model after generation
                                HoyoToonLogs.LogDebug("Applying generated materials to model...");
                                ApplyMaterialsToModel();
                            };
                        }
                        finally
                        {
                            Selection.activeObject = previousSelection;
                        }
                    },
                    progressWeight = 35
                });
            }

            // Step 2: FBX Import Settings (SECOND - after materials are generated)
            if (!analysisData.hasImportSettingsSet || !analysisData.isHumanoidRig)
            {
                neededSteps.Add(new SetupStep
                {
                    name = "Configure FBX Import Settings",
                    action = () =>
                    {
                        // Ensure the correct model is selected before calling setup operations
                        var previousSelection = Selection.activeObject;
                        Selection.activeObject = selectedModel;
                        try
                        {
                            HoyoToonLogs.LogDebug("Configuring FBX import settings...");
                            HoyoToonManager.SetupFBX();
                        }
                        finally
                        {
                            Selection.activeObject = previousSelection;
                        }
                    },
                    progressWeight = 25
                });
            }

            // Step 3: Add Model to Scene (THIRD - after FBX setup and materials)
            if (!IsModelAlreadyInScene())
            {
                neededSteps.Add(new SetupStep
                {
                    name = "Add Model to Scene",
                    action = () =>
                    {
                        // Ensure the correct model is selected before adding to scene
                        var previousSelection = Selection.activeObject;
                        Selection.activeObject = selectedModel;
                        try
                        {
                            HoyoToonLogs.LogDebug("Adding model to scene...");
                            var sceneObject = HoyoToonSceneManager.AddSelectedObjectToScene();
                            HoyoToonLogs.LogDebug($"Added model to scene: {sceneObject?.name}");

                            // CRITICAL: Ensure the scene object gets properly configured
                            if (sceneObject != null)
                            {
                                // Apply generated materials to the scene instance
                                ApplyMaterialsToSceneObject(sceneObject);

                                HoyoToonLogs.LogDebug("Scene object configured with materials.");
                            }
                        }
                        finally
                        {
                            Selection.activeObject = previousSelection;
                        }
                    },
                    progressWeight = 15
                });
            }            // Step 4: Tangent Generation (FOURTH - final step after everything is set up)
            // Use scene-specific tangent detection instead of asset-based analysis
            HoyoToonLogs.LogDebug($"Checking scene-based tangent requirements...");

            // Check if we need tangent generation by looking at the scene instance (if it will exist)
            bool needsSceneInstanceTangents = false;
            GameObject tempSceneCheck = null;

            // If model is already in scene, check that instance
            if (IsModelAlreadyInScene())
            {
                tempSceneCheck = FindModelInstanceInScene();
            }
            else
            {
                // Model will be added to scene in previous step, so assume we'll have a scene instance
                // We'll do the actual check in the tangent generation step itself
                needsSceneInstanceTangents = true; // Assume we might need it, check properly in the step
            }

            // If we found an existing scene instance, use Scene tab logic to check
            if (tempSceneCheck != null && tabControllers.ContainsKey("Scene") && tabControllers["Scene"] is HoyoToonSceneTabController sceneController)
            {
                sceneController.SetSceneInstance(tempSceneCheck);
                // Use reflection or make these methods public, or assume we need to check in the step
                needsSceneInstanceTangents = true; // For now, always add the step and let it decide
            }

            HoyoToonLogs.LogDebug($"Scene-based tangent check result: needsSceneInstanceTangents={needsSceneInstanceTangents}");

            if (needsSceneInstanceTangents)
            {
                HoyoToonLogs.LogDebug("Adding scene-instance tangent generation step to setup");
                neededSteps.Add(new SetupStep
                {
                    name = "Generate Tangents",
                    action = () =>
                    {
                        HoyoToonLogs.LogDebug("Starting scene-instance tangent generation...");

                        // Find the scene instance (should exist from previous step)
                        var sceneName = selectedModel.name;
                        var sceneObject = GameObject.Find(sceneName);

                        if (sceneObject != null)
                        {
                            HoyoToonLogs.LogDebug($"Found scene object: {sceneObject.name}");

                            // Get the Scene tab controller and use its logic
                            if (tabControllers.ContainsKey("Scene") && tabControllers["Scene"] is HoyoToonSceneTabController sceneController)
                            {
                                // Set the scene instance on the controller
                                sceneController.SetSceneInstance(sceneObject);

                                // Use the Scene tab's tangent generation logic by accessing it through reflection
                                // or by directly checking if tangents can be generated
                                var canGenerateMethod = typeof(HoyoToonSceneTabController).GetMethod("CanGenerateTangentsForSceneInstance",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                var generateMethod = typeof(HoyoToonSceneTabController).GetMethod("GenerateTangentsForSceneInstance",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                                if (canGenerateMethod != null && generateMethod != null)
                                {
                                    bool canGenerate = (bool)canGenerateMethod.Invoke(sceneController, null);

                                    if (canGenerate)
                                    {
                                        statusLabel.text = "Generating tangents for scene instance...";
                                        progressBar.value = 50;

                                        HoyoToonLogs.LogDebug("Calling Scene tab tangent generation logic");
                                        generateMethod.Invoke(sceneController, null);

                                        statusLabel.text = "Tangents generated successfully";
                                        progressBar.value = 100;

                                        HoyoToonLogs.LogDebug("Scene-instance tangent generation completed using Scene tab logic");
                                    }
                                    else
                                    {
                                        HoyoToonLogs.LogDebug("Scene tab logic determined tangents cannot be generated for this model");
                                        statusLabel.text = "Tangents not needed for this model";
                                        progressBar.value = 100;
                                    }
                                }
                                else
                                {
                                    // Fallback to direct method call
                                    HoyoToonLogs.LogDebug("Using fallback tangent generation");
                                    HoyoToonMeshManager.GenTangents(sceneObject);
                                    statusLabel.text = "Tangents generated successfully";
                                    progressBar.value = 100;
                                }

                                // Invalidate analysis cache since scene instance changed
                                InvalidateAnalysisCache();
                            }
                            else
                            {
                                // Fallback to direct scene instance tangent generation
                                HoyoToonLogs.LogDebug("Scene tab not available, using direct tangent generation");
                                HoyoToonMeshManager.GenTangents(sceneObject);

                                statusLabel.text = "Tangents generated successfully";
                                progressBar.value = 100;

                                // Invalidate analysis cache since mesh changed
                                InvalidateAnalysisCache();
                            }
                        }
                        else
                        {
                            HoyoToonLogs.WarningDebug("Could not find scene object for tangent generation, skipping");
                            statusLabel.text = "Scene object not found, skipping tangents";
                            progressBar.value = 100;
                        }
                    },
                    progressWeight = 25
                });
            }

            // Step 4: Texture Import Settings (optional optimization step)
            if (analysisData.textures.Any(t => !t.hasOptimalSettings))
            {
                neededSteps.Add(new SetupStep
                {
                    name = "Optimize Texture Settings",
                    action = () =>
                    {
                        // Apply optimal texture import settings for detected game type using DataManager system
                        HoyoToonDataManager.GetHoyoToonData();
                        HoyoToonParseManager.DetermineBodyType(selectedModel);
                        var (gameName, shaderKey, bodyType) = HoyoToonParseManager.GetCurrentGameInfo();

                        string gameKey = GetGameKeyFromBodyType(bodyType);
                        if (!string.IsNullOrEmpty(gameKey))
                        {
                            var texturePaths = analysisData.textures.Select(t => t.path).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                            if (texturePaths.Length > 0)
                            {
                                HoyoToonTextureManager.SetTextureImportSettings(texturePaths, gameKey);
                            }
                        }
                    },
                    progressWeight = 10
                });
            }

            HoyoToonLogs.LogDebug($"Determined {neededSteps.Count} setup steps needed:");
            foreach (var step in neededSteps)
            {
                HoyoToonLogs.LogDebug($"  - {step.name}");
            }

            return neededSteps;
        }

        private void ExecuteSetupSteps(List<SetupStep> steps)
        {
            int totalWeight = steps.Sum(s => s.progressWeight);
            int currentProgress = 0;

            var stepQueue = new Queue<SetupStep>(steps);
            ExecuteNextStep(stepQueue, totalWeight, currentProgress);
        }

        private void ExecuteNextStep(Queue<SetupStep> stepQueue, int totalWeight, int currentProgress)
        {
            if (stepQueue.Count == 0)
            {
                // All steps completed
                progressBar.value = 100;
                statusLabel.text = "Setup completed successfully!";

                EditorApplication.delayCall += () =>
                {
                    // Invalidate cache and force re-analysis to update UI with new state
                    InvalidateAnalysisCache();
                    ForceAnalysisRefresh();
                    progressBar.style.display = DisplayStyle.None;

                    EditorUtility.DisplayDialog("Setup Complete",
                        "HoyoToon setup completed successfully! The model has been configured and added to the scene.", "OK");
                };
                return;
            }

            var step = stepQueue.Dequeue();
            statusLabel.text = step.name;

            try
            {
                step.action.Invoke();
                currentProgress += step.progressWeight;
                progressBar.value = (currentProgress * 100) / totalWeight;

                HoyoToonLogs.LogDebug($"Completed setup step: {step.name}");

                // Schedule next step with delay to allow asset processing
                EditorApplication.delayCall += () =>
                {
                    ExecuteNextStep(stepQueue, totalWeight, currentProgress);
                };
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Setup step '{step.name}' failed: {e.Message}");
                statusLabel.text = $"Setup failed at: {step.name}";
                progressBar.style.display = DisplayStyle.None;

                EditorUtility.DisplayDialog("Setup Failed",
                    $"Setup failed during '{step.name}': {e.Message}", "OK");
            }
        }

        private string GetGameKeyFromBodyType(HoyoToonParseManager.BodyType? bodyType)
        {
            if (!bodyType.HasValue) return "";
            return GetGameKeyFromBodyType(bodyType.Value);
        }

        private string GetGameKeyFromBodyType(HoyoToonParseManager.BodyType bodyType)
        {
            var bodyTypeString = bodyType.ToString();

            if (bodyTypeString.StartsWith("GI")) return "GIShader";
            if (bodyTypeString.StartsWith("HSR")) return "HSRShader";
            if (bodyTypeString.StartsWith("HI3"))
            {
                // Determine if it's Part 1 or Part 2
                return bodyTypeString.Contains("P2") ? "HI3P2Shader" : "HI3Shader";
            }
            if (bodyTypeString.StartsWith("WuWa")) return "WuWaShader";
            if (bodyTypeString.StartsWith("ZZZ")) return "ZZZShader";

            return "";
        }

        private class SetupStep
        {
            public string name;
            public System.Action action;
            public int progressWeight; // Relative weight for progress calculation
        }

        #region Material Application Helpers

        /// <summary>
        /// Apply generated materials to the original model asset
        /// </summary>
        private void ApplyMaterialsToModel()
        {
            if (selectedModel == null) return;

            try
            {
                HoyoToonLogs.LogDebug("Applying materials to model asset...");

                // Get all renderers in the model
                var renderers = selectedModel.GetComponentsInChildren<Renderer>();

                foreach (var renderer in renderers)
                {
                    ApplyMaterialsToRenderer(renderer);
                }

                HoyoToonLogs.LogDebug($"Applied materials to {renderers.Length} renderers in model asset.");

                // Force asset database refresh to ensure changes are saved
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Failed to apply materials to model: {e.Message}");
            }
        }

        /// <summary>
        /// Apply generated materials to a scene object instance
        /// </summary>
        private void ApplyMaterialsToSceneObject(GameObject sceneObject)
        {
            if (sceneObject == null) return;

            try
            {
                HoyoToonLogs.LogDebug($"Applying materials to scene object: {sceneObject.name}");

                // Force asset refresh to ensure all materials are loaded
                AssetDatabase.Refresh();

                // Get all renderers in the scene object
                var renderers = sceneObject.GetComponentsInChildren<Renderer>();
                int materialsApplied = 0;

                foreach (var renderer in renderers)
                {
                    if (ApplyMaterialsToRenderer(renderer))
                    {
                        materialsApplied++;
                    }
                }

                if (materialsApplied > 0)
                {
                    HoyoToonLogs.LogDebug($"Applied materials to {materialsApplied} renderers in scene object.");

                    // Mark the scene as dirty to ensure changes are saved
                    EditorUtility.SetDirty(sceneObject);

                    // Force the scene view to update
                    SceneView.RepaintAll();
                }
                else
                {
                    HoyoToonLogs.WarningDebug($"No materials were applied to scene object '{sceneObject.name}'. This may indicate materials haven't been generated yet.");
                }
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Failed to apply materials to scene object: {e.Message}");
            }
        }

        /// <summary>
        /// Apply HoyoToon materials to a specific renderer based on material naming
        /// Returns true if any materials were applied
        /// </summary>
        private bool ApplyMaterialsToRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterials == null) return false;

            var materials = renderer.sharedMaterials;
            bool materialsChanged = false;

            for (int i = 0; i < materials.Length; i++)
            {
                var originalMaterial = materials[i];
                if (originalMaterial == null) continue;

                // Try to find corresponding HoyoToon material
                var hoyoToonMaterial = FindHoyoToonMaterial(originalMaterial.name);
                if (hoyoToonMaterial != null && hoyoToonMaterial != originalMaterial)
                {
                    materials[i] = hoyoToonMaterial;
                    materialsChanged = true;
                    HoyoToonLogs.LogDebug($"Applied HoyoToon material '{hoyoToonMaterial.name}' to renderer '{renderer.name}' slot {i}");
                }
                else if (hoyoToonMaterial == null)
                {
                    HoyoToonLogs.WarningDebug($"Could not find HoyoToon material for '{originalMaterial.name}' on renderer '{renderer.name}'");
                }
            }

            if (materialsChanged)
            {
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }

            return materialsChanged;
        }

        /// <summary>
        /// Apply tangent-modified meshes to a scene object if they exist
        /// </summary>
        private void ApplyTangentMeshesToSceneObject(GameObject sceneObject)
        {
            if (sceneObject == null) return;

            try
            {
                HoyoToonLogs.LogDebug($"Checking for tangent meshes to apply to scene object: {sceneObject.name}");

                // Get the original FBX model path to find the corresponding tangent meshes
                string originalFbxPath = AssetDatabase.GetAssetPath(selectedModel);
                if (string.IsNullOrEmpty(originalFbxPath))
                {
                    HoyoToonLogs.WarningDebug("Could not find original FBX path for tangent mesh application");
                    return;
                }

                string meshesFolder = Path.GetDirectoryName(originalFbxPath) + "/Meshes";
                if (!AssetDatabase.IsValidFolder(meshesFolder))
                {
                    HoyoToonLogs.LogDebug("No tangent meshes folder found - meshes probably don't need tangents");
                    return;
                }

                // Apply tangent meshes to MeshFilter components
                var meshFilters = sceneObject.GetComponentsInChildren<MeshFilter>();
                int meshesApplied = 0;

                foreach (var meshFilter in meshFilters)
                {
                    if (meshFilter.sharedMesh != null)
                    {
                        string tangentMeshPath = meshesFolder + "/" + meshFilter.sharedMesh.name + ".asset";
                        var tangentMesh = AssetDatabase.LoadAssetAtPath<Mesh>(tangentMeshPath);

                        if (tangentMesh != null)
                        {
                            meshFilter.sharedMesh = tangentMesh;
                            meshesApplied++;
                            HoyoToonLogs.LogDebug($"Applied tangent mesh '{tangentMesh.name}' to MeshFilter on '{meshFilter.name}'");
                            EditorUtility.SetDirty(meshFilter);
                        }
                    }
                }

                // Apply tangent meshes to SkinnedMeshRenderer components
                var skinnedRenderers = sceneObject.GetComponentsInChildren<SkinnedMeshRenderer>();

                foreach (var renderer in skinnedRenderers)
                {
                    if (renderer.sharedMesh != null)
                    {
                        string tangentMeshPath = meshesFolder + "/" + renderer.sharedMesh.name + ".asset";
                        var tangentMesh = AssetDatabase.LoadAssetAtPath<Mesh>(tangentMeshPath);

                        if (tangentMesh != null)
                        {
                            renderer.sharedMesh = tangentMesh;
                            meshesApplied++;
                            HoyoToonLogs.LogDebug($"Applied tangent mesh '{tangentMesh.name}' to SkinnedMeshRenderer on '{renderer.name}'");
                            EditorUtility.SetDirty(renderer);
                        }
                    }
                }

                if (meshesApplied > 0)
                {
                    HoyoToonLogs.LogDebug($"Applied {meshesApplied} tangent meshes to scene object");
                    EditorUtility.SetDirty(sceneObject);
                    SceneView.RepaintAll();
                }
                else
                {
                    HoyoToonLogs.LogDebug("No tangent meshes found to apply - model probably doesn't require tangents");
                }
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Failed to apply tangent meshes to scene object: {e.Message}");
            }
        }

        /// <summary>
        /// Find a generated HoyoToon material by name using the same logic as material generation
        /// </summary>
        private Material FindHoyoToonMaterial(string originalMaterialName)
        {
            if (string.IsNullOrEmpty(originalMaterialName)) return null;

            // The material generation system creates materials in the same directory as the FBX
            // with the same name as the JSON file (which matches the material name)
            string fbxPath = AssetDatabase.GetAssetPath(selectedModel);
            if (string.IsNullOrEmpty(fbxPath))
            {
                HoyoToonLogs.WarningDebug("Could not find FBX path for material search");
                return null;
            }

            string fbxDirectory = Path.GetDirectoryName(fbxPath);

            // Strategy 1: Look for material in the same directory as the FBX (standard material generation location)
            string materialPath = Path.Combine(fbxDirectory, originalMaterialName + ".mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material != null)
            {
                HoyoToonLogs.LogDebug($"Found material in FBX directory: {material.name}");
                return material;
            }

            // Strategy 2: Look in Materials subfolder (alternative material generation location)
            string materialsFolderPath = Path.Combine(fbxDirectory, "Materials", originalMaterialName + ".mat");
            material = AssetDatabase.LoadAssetAtPath<Material>(materialsFolderPath);

            if (material != null)
            {
                HoyoToonLogs.LogDebug($"Found material in Materials subfolder: {material.name}");
                return material;
            }

            // Strategy 3: Look in other common material folder names that the generation system checks
            string[] commonMaterialFolders = { "Material", "Mat" };
            foreach (string folderName in commonMaterialFolders)
            {
                string altMaterialPath = Path.Combine(fbxDirectory, folderName, originalMaterialName + ".mat");
                material = AssetDatabase.LoadAssetAtPath<Material>(altMaterialPath);

                if (material != null)
                {
                    HoyoToonLogs.LogDebug($"Found material in {folderName} folder: {material.name}");
                    return material;
                }
            }

            // Strategy 4: Search for any HoyoToon material with matching name in the FBX directory tree
            string[] materialGuids = AssetDatabase.FindAssets($"t:Material {originalMaterialName}", new[] { fbxDirectory });

            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material != null && material.name == originalMaterialName &&
                    material.shader != null && material.shader.name.Contains("HoyoToon"))
                {
                    HoyoToonLogs.LogDebug($"Found HoyoToon material by search: {material.name}");
                    return material;
                }
            }

            HoyoToonLogs.WarningDebug($"Could not find HoyoToon material for: {originalMaterialName}");
            return null;
        }

        /// <summary>
        /// Check if HoyoToon materials exist in the project for the selected model
        /// </summary>
        private bool DoMaterialsExistInProject()
        {
            if (selectedModel == null) return false;

            try
            {
                // Check if the HoyoToon materials folder exists and has materials
                string materialsPath = "Assets/HoyoToon/Materials";
                if (!AssetDatabase.IsValidFolder(materialsPath))
                {
                    return false;
                }

                // Look for any materials in the HoyoToon folder that might belong to this model
                string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { materialsPath });

                if (materialGuids.Length == 0)
                {
                    return false;
                }

                // Check if any materials match the expected naming pattern for this model
                string modelName = selectedModel.name;
                foreach (string guid in materialGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                    if (material != null && material.name.Contains(modelName))
                    {
                        return true;
                    }
                }

                // If we have materials but none match the model name, still consider them as existing
                // (they might be from a previous generation)
                return materialGuids.Length > 0;
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Error checking if materials exist: {e.Message}");
                return false;
            }
        }

        #endregion

        #endregion

        #region Preview System

        private void UpdateHoyoToonPreview()
        {
            // Prevent recursive calls
            if (isUpdatingPreview) return;

            try
            {
                isUpdatingPreview = true;

                if (previewUtility == null)
                {
                    HoyoToonLogs.LogDebug("Preview utility not initialized, attempting to reinitialize...");
                    SetupHoyoToonPreviewSystem();
                    if (previewUtility == null)
                    {
                        HoyoToonLogs.ErrorDebug("Failed to initialize preview utility");
                        return;
                    }
                }

                // Clean up existing preview
                CleanupCurrentPreview();

                // Check if model is already in the scene - if so, disable preview
                if (selectedModel != null && IsModelAlreadyInScene())
                {
                    HoyoToonLogs.LogDebug($"Model {selectedModel.name} is already in the scene - disabling preview to avoid duplicates");

                    // Show message in preview area instead
                    ShowModelInSceneMessage();
                    return;
                }

                // Create new preview if model selected and not in scene
                if (selectedModel != null)
                {
                    try
                    {
                        // Create instance for preview
                        sceneInstance = Instantiate(selectedModel);
                        sceneInstance.name = $"Preview_{selectedModel.name}";
                        sceneInstance.hideFlags = HideFlags.HideAndDontSave;

                        // Ensure the model has renderers
                        var renderers = sceneInstance.GetComponentsInChildren<Renderer>();
                        if (renderers.Length == 0)
                        {
                            HoyoToonLogs.WarningDebug($"Model {selectedModel.name} has no renderers - preview may not display correctly");
                        }

                        // Only log detailed information if there are issues
                        var meshRenderers = sceneInstance.GetComponentsInChildren<MeshRenderer>();
                        var skinnedRenderers = sceneInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

                        // Check for issues without spamming console
                        bool hasIssues = false;
                        foreach (var meshRenderer in meshRenderers)
                        {
                            var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                            if (meshFilter == null)
                            {
                                HoyoToonLogs.WarningDebug($"MeshRenderer on {meshRenderer.name} is missing MeshFilter component");
                                hasIssues = true;
                            }
                        }

                        foreach (var skinnedRenderer in skinnedRenderers)
                        {
                            if (skinnedRenderer.sharedMesh == null)
                            {
                                HoyoToonLogs.WarningDebug($"SkinnedMeshRenderer on {skinnedRenderer.name} has no shared mesh");
                                hasIssues = true;
                            }
                        }

                        // Create editor for the instance
                        gameObjectEditor = UnityEditor.Editor.CreateEditor(sceneInstance);

                        // Reset preview interaction state
                        isInteractingWithPreview = false;
                        previewRotation = new Vector2(0, 0); // Start looking straight at the model
                        previewDistance = 2f;

                        // Calculate proper distance based on model bounds - start closer
                        var bounds = GetHoyoToonModelBounds(sceneInstance);
                        if (bounds.size.magnitude > 0.1f)
                        {
                            previewDistance = Mathf.Max(bounds.size.magnitude * 1.0f, 1.5f); // Closer distance multiplier
                        }

                        // Preview creation completed successfully (removed log to prevent spam)
                    }
                    catch (System.Exception e)
                    {
                        HoyoToonLogs.ErrorDebug($"Failed to create preview for model {selectedModel.name}: {e.Message}\nStack: {e.StackTrace}");
                        CleanupCurrentPreview();
                    }
                }
            }
            finally
            {
                isUpdatingPreview = false;
            }
        }

        private void CleanupCurrentPreview()
        {
            if (sceneInstance != null)
            {
                DestroyImmediate(sceneInstance);
                sceneInstance = null;
            }

            if (gameObjectEditor != null)
            {
                DestroyImmediate(gameObjectEditor);
                gameObjectEditor = null;
            }
        }

        private void OnHoyoToonPreviewGUI()
        {
            var previewRect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (previewUtility == null)
            {
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                var style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.red;
                EditorGUI.LabelField(previewRect, "Preview System Error", style);
                return;
            }

            if (selectedModel == null)
            {
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                var style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.gray;
                EditorGUI.LabelField(previewRect, "Select a model to preview", style);
                return;
            }

            // Check if model is already in scene - show message instead of preview
            if (selectedModel != null && IsModelAlreadyInScene())
            {
                EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.25f, 0.15f, 1f));
                var style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = new Color(0.7f, 1f, 0.7f);
                style.fontSize = 12;
                style.wordWrap = true;

                var message = $"✓ {selectedModel.name}\nis in the scene\n\nPreview disabled to\navoid duplicates";
                EditorGUI.LabelField(previewRect, message, style);
                return;
            }

            if (sceneInstance == null)
            {
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                var style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.yellow;
                EditorGUI.LabelField(previewRect, "Loading preview...", style);

                // Only try to recreate the preview if we're not already updating
                // and only on Layout or Repaint events to avoid excessive calls
                if (!isUpdatingPreview && !hierarchyChangePending && (Event.current.type == EventType.Layout || Event.current.type == EventType.Repaint))
                {
                    // Use EditorApplication.delayCall to avoid calling during GUI events
                    EditorApplication.delayCall += () =>
                    {
                        if (!isUpdatingPreview && !hierarchyChangePending && sceneInstance == null && selectedModel != null)
                        {
                            UpdateHoyoToonPreview();
                        }
                    };
                }
                return;
            }

            // Handle mouse interaction
            var evt = Event.current;
            if (evt != null && previewRect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.MouseDown && (evt.button == 1 || evt.button == 2))
                {
                    isInteractingWithPreview = true;
                    lastMousePosition = evt.mousePosition;
                    evt.Use();
                }
                else if (evt.type == EventType.MouseDrag && isInteractingWithPreview)
                {
                    var deltaMousePosition = evt.mousePosition - lastMousePosition;

                    // Updated interaction modes per user request
                    if (evt.button == 1) // Right mouse - rotate around model
                    {
                        previewRotation.x += deltaMousePosition.x * 0.5f;
                        previewRotation.y -= deltaMousePosition.y * 0.5f;

                        // Clamp vertical rotation
                        previewRotation.y = Mathf.Clamp(previewRotation.y, -90f, 90f);
                    }
                    else if (evt.button == 2) // Middle mouse - pan view in all directions
                    {
                        // Enhanced panning: X for left/right (inverted to match Scene view), Y for up/down, scroll for forward/back
                        // Invert X-axis: when dragging left, camera moves right (like Scene view)
                        // Fix inverted Y-axis: negate deltaMousePosition.y for natural panning
                        Vector3 panDelta = new Vector3(-deltaMousePosition.x, deltaMousePosition.y, 0) * 0.01f;
                        previewDrag += new Vector2(panDelta.x, panDelta.y);

                        // Prevent scroll wheel from interfering with middle mouse drag
                        evt.Use();
                    }
                    // Left mouse (button 0) does nothing per user request

                    lastMousePosition = evt.mousePosition;
                    evt.Use();
                    Repaint();
                }
                else if (evt.type == EventType.ScrollWheel && !isInteractingWithPreview)
                {
                    // Enhanced scroll: Normal scroll for zoom, Shift+scroll for forward/backward panning
                    if (evt.shift)
                    {
                        // Shift+scroll for forward/backward movement (move along Z-axis in camera space)
                        // We'll use a separate variable for Z-axis panning
                        previewZoom += evt.delta.y * 0.01f;
                    }
                    else
                    {
                        // Normal scroll for zoom in/out
                        previewDistance = Mathf.Clamp(previewDistance + evt.delta.y * 0.1f, 0.5f, 20f);
                    }
                    evt.Use();
                    Repaint();
                }
                else if (evt.type == EventType.KeyDown)
                {
                    // Reset view with 'F' key like Unity
                    if (evt.keyCode == KeyCode.F)
                    {
                        previewRotation = new Vector2(0, 0); // Reset to straight view
                        previewDrag = Vector2.zero;
                        previewZoom = 0f; // Reset forward/backward panning
                        var bounds = GetHoyoToonModelBounds(sceneInstance);
                        previewDistance = Mathf.Max(bounds.size.magnitude * 1.0f, 1.5f); // Closer reset distance
                        evt.Use();
                        Repaint();
                    }
                }
            }

            if (evt != null && evt.type == EventType.MouseUp)
            {
                if (evt.button == 1 || evt.button == 2) // Only respond to right and middle mouse buttons
                {
                    isInteractingWithPreview = false;
                }
            }

            try
            {
                // Debug: Check if sceneInstance is valid at render time
                if (sceneInstance == null)
                {
                    EditorGUI.DrawRect(previewRect, new Color(0.3f, 0.1f, 0.1f, 1f));
                    var debugStyle = new GUIStyle(EditorStyles.label);
                    debugStyle.alignment = TextAnchor.MiddleCenter;
                    debugStyle.normal.textColor = Color.red;
                    EditorGUI.LabelField(previewRect, "DEBUG: sceneInstance is null during render", debugStyle);
                    return;
                }

                // Render the preview
                previewUtility.BeginPreview(previewRect, GUIStyle.none);

                // Get model bounds and setup camera
                var bounds = GetHoyoToonModelBounds(sceneInstance);
                Vector3 center = bounds.center;

                // Position camera based on rotation and distance
                // Add 180 degrees to the horizontal rotation to view from the front
                float adjustedRotationX = previewRotation.x + 180f;

                Vector3 cameraDirection = new Vector3(
                    Mathf.Sin(adjustedRotationX * Mathf.Deg2Rad) * Mathf.Cos(previewRotation.y * Mathf.Deg2Rad),
                    Mathf.Sin(previewRotation.y * Mathf.Deg2Rad),
                    Mathf.Cos(adjustedRotationX * Mathf.Deg2Rad) * Mathf.Cos(previewRotation.y * Mathf.Deg2Rad)
                );

                // Calculate camera position before applying pan offset
                Vector3 baseCameraPosition = center - cameraDirection * previewDistance;

                // Apply panning relative to the camera's view direction
                // Calculate camera's right and up vectors for proper panning
                Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraDirection).normalized;
                Vector3 cameraUp = Vector3.Cross(cameraDirection, cameraRight).normalized;

                // Apply pan offset in camera space (X,Y panning + Z zoom offset)
                Vector3 panOffset = cameraRight * previewDrag.x + cameraUp * previewDrag.y + cameraDirection * previewZoom;
                Vector3 finalCenter = center + panOffset;
                Vector3 cameraPosition = baseCameraPosition + panOffset;

                previewUtility.camera.transform.position = cameraPosition;
                previewUtility.camera.transform.LookAt(finalCenter);
                previewUtility.camera.nearClipPlane = 0.1f;
                previewUtility.camera.farClipPlane = 100f;
                previewUtility.camera.fieldOfView = 24f;

                // Setup enhanced lighting similar to reference image
                previewUtility.camera.clearFlags = CameraClearFlags.Color;
                previewUtility.camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);

                // Main directional light (key light)
                if (previewUtility.lights != null && previewUtility.lights.Length > 0)
                {
                    var mainLight = previewUtility.lights[0];
                    mainLight.enabled = true;
                    mainLight.type = LightType.Directional;
                    mainLight.intensity = 1.0f;
                    mainLight.color = Color.white;
                    mainLight.transform.rotation = Quaternion.Euler(42f, 180f, 0f);
                    mainLight.shadows = LightShadows.Soft;
                }

                // Secondary fill light for better illumination
                if (previewUtility.lights != null && previewUtility.lights.Length > 1)
                {
                    var fillLight = previewUtility.lights[1];
                    fillLight.enabled = true;
                    fillLight.type = LightType.Directional;
                    fillLight.intensity = 0.3f;
                    fillLight.color = new Color(0.8f, 0.9f, 1f, 1f); // Slightly blue fill
                    fillLight.transform.rotation = Quaternion.Euler(-20f, -45f, 0f);
                    fillLight.shadows = LightShadows.None;
                }

                // Set ambient lighting for overall illumination
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.4f, 0.5f, 0.7f, 1f);
                RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.3f, 1f);
                RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
                RenderSettings.ambientIntensity = 0.2f;

                // Render all renderers in the scene instance - check for both MeshRenderer and SkinnedMeshRenderer
                var meshRenderers = sceneInstance.GetComponentsInChildren<MeshRenderer>();
                var skinnedRenderers = sceneInstance.GetComponentsInChildren<SkinnedMeshRenderer>();

                // Render MeshRenderers
                foreach (var renderer in meshRenderers)
                {
                    if (renderer != null && renderer.enabled)
                    {
                        var meshFilter = renderer.GetComponent<MeshFilter>();
                        if (meshFilter != null && meshFilter.sharedMesh != null)
                        {
                            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                            {
                                var material = renderer.sharedMaterials[i];
                                if (material != null)
                                {
                                    previewUtility.DrawMesh(
                                        meshFilter.sharedMesh,
                                        renderer.transform.localToWorldMatrix,
                                        material,
                                        i
                                    );
                                }
                            }
                        }
                    }
                }

                // Render SkinnedMeshRenderers
                foreach (var renderer in skinnedRenderers)
                {
                    if (renderer != null && renderer.enabled && renderer.sharedMesh != null)
                    {
                        for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                        {
                            var material = renderer.sharedMaterials[i];
                            if (material != null)
                            {
                                previewUtility.DrawMesh(
                                    renderer.sharedMesh,
                                    renderer.transform.localToWorldMatrix,
                                    material,
                                    i
                                );
                            }
                        }
                    }
                }

                previewUtility.Render();
                var texture = previewUtility.EndPreview();

                if (texture != null)
                {
                    GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);

                    // Draw controls overlay
                    DrawHoyoToonPreviewControlsOverlay(previewRect);
                }
                else
                {
                    EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                    var style = new GUIStyle(EditorStyles.label);
                    style.alignment = TextAnchor.MiddleCenter;
                    style.normal.textColor = Color.red;
                    EditorGUI.LabelField(previewRect, "Preview Render Failed", style);
                }
            }
            catch (System.Exception e)
            {
                // Make sure to end preview even if an error occurs
                try
                {
                    previewUtility.EndPreview();
                }
                catch
                {
                    // Ignore errors in cleanup
                }

                HoyoToonLogs.ErrorDebug($"Preview render failed: {e.Message}");
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));

                var style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.red;
                style.fontSize = 10;
                EditorGUI.LabelField(previewRect, $"Preview Error:\n{e.Message}", style);
            }
        }

        private void DrawHoyoToonPreviewControlsOverlay(Rect previewRect)
        {
            // Draw controls overlay in bottom-left corner
            var overlayRect = new Rect(previewRect.x + 5, previewRect.yMax - 60, 180, 55);

            // Semi-transparent background
            var originalColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(overlayRect, EditorGUIUtility.whiteTexture);
            GUI.color = originalColor;

            // Controls text
            var controlsStyle = new GUIStyle(EditorStyles.miniLabel);
            controlsStyle.normal.textColor = Color.white;
            controlsStyle.fontSize = 9;
            controlsStyle.padding = new RectOffset(5, 5, 3, 3);

            var controlsText = "Right Mouse: Rotate\nMiddle Mouse: Pan\nScroll: Zoom\nF: Reset View";
            GUI.Label(overlayRect, controlsText, controlsStyle);
        }

        private Bounds GetHoyoToonModelBounds(GameObject model)
        {
            if (model == null) return new Bounds(Vector3.zero, Vector3.one);

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                // Fallback to transform bounds if no renderers
                var transforms = model.GetComponentsInChildren<Transform>();
                if (transforms.Length > 0)
                {
                    var bounds = new Bounds(transforms[0].position, Vector3.zero);
                    foreach (var t in transforms)
                    {
                        bounds.Encapsulate(t.position);
                    }
                    // Ensure minimum size
                    if (bounds.size.magnitude < 0.1f)
                        bounds.size = Vector3.one;
                    return bounds;
                }
                return new Bounds(Vector3.zero, Vector3.one);
            }

            var modelBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].bounds.size != Vector3.zero)
                {
                    modelBounds.Encapsulate(renderers[i].bounds);
                }
            }

            // Ensure bounds are not zero
            if (modelBounds.size.magnitude < 0.1f)
                modelBounds.size = Vector3.one;

            return modelBounds;
        }

        /// <summary>
        /// Check if the selected model is already instantiated in the current scene
        /// This method is robust against GameObject activation states
        /// </summary>
        private bool IsModelAlreadyInScene()
        {
            if (selectedModel == null) return false;

            // Use Resources.FindObjectsOfTypeAll to find both active and inactive objects
            // But filter to only scene objects (not assets)
            var allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(obj => obj.scene.IsValid() && obj.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                .ToArray();

            int candidateCount = 0;

            foreach (var obj in allGameObjects)
            {
                // Skip preview instances and prefabs
                if (obj.hideFlags == HideFlags.HideAndDontSave) continue;
                if (obj.name.StartsWith("Preview_")) continue;

                // Check if this object was instantiated from our selected model
                // This includes checking for exact name matches or instantiated names
                var objName = obj.name.Replace("(Clone)", "").Trim();
                var modelName = selectedModel.name;

                if (objName == modelName || obj.name.Contains(modelName))
                {
                    candidateCount++;
                    HoyoToonLogs.LogDebug($"Found candidate object: '{obj.name}' (active: {obj.activeInHierarchy}) for model '{modelName}'");

                    // Additional verification: check if it has similar structure
                    if (HasSimilarStructure(obj, selectedModel))
                    {
                        HoyoToonLogs.LogDebug($"Model '{modelName}' confirmed in scene as '{obj.name}' (active: {obj.activeInHierarchy})");
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check if two GameObjects have similar structure (same child count and renderer count)
        /// </summary>
        private bool HasSimilarStructure(GameObject sceneObj, GameObject prefabObj)
        {
            if (sceneObj == null || prefabObj == null) return false;

            var sceneRenderers = sceneObj.GetComponentsInChildren<Renderer>();
            var prefabRenderers = prefabObj.GetComponentsInChildren<Renderer>();

            var sceneTransforms = sceneObj.GetComponentsInChildren<Transform>();
            var prefabTransforms = prefabObj.GetComponentsInChildren<Transform>();

            // Compare renderer and transform counts as a basic structure check
            return sceneRenderers.Length == prefabRenderers.Length &&
                   sceneTransforms.Length == prefabTransforms.Length;
        }

        /// <summary>
        /// Show a message in the preview area when model is already in scene
        /// </summary>
        private void ShowModelInSceneMessage()
        {
            // This will be handled by the IMGUI preview area to show the message
            // The actual rendering happens in OnHoyoToonPreviewGUI
        }

        #endregion
    }
}