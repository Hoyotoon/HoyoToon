using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HoyoToon.UI.Core;
using HoyoToon.UI.Components;

namespace HoyoToon
{
    /// <summary>
    /// Interface for HoyoToon tab controllers
    /// Defines the contract for all tab functionality in the HoyoToon UI Manager
    /// Enhanced with modular system support
    /// </summary>
    public interface IHoyoToonTabController
    {
        /// <summary>
        /// Initialize the tab controller with the content view and analysis data
        /// </summary>
        /// <param name="contentView">The main content area for this tab</param>
        /// <param name="analysisData">Current model analysis data</param>
        void Initialize(ScrollView contentView, HoyoToonModelAnalysisData analysisData);

        /// <summary>
        /// Initialize the tab controller (called once when tab controllers are created)
        /// </summary>
        void Initialize();

        /// <summary>
        /// Called when this tab is selected
        /// </summary>
        void OnTabSelected();

        /// <summary>
        /// Called when this tab is deselected
        /// </summary>
        void OnTabDeselected();

        /// <summary>
        /// Update the tab content (called when data changes)
        /// </summary>
        void UpdateContent();

        /// <summary>
        /// Cleanup resources when the tab is destroyed
        /// </summary>
        void Cleanup();

        /// <summary>
        /// Get the display name for this tab
        /// </summary>
        string TabName { get; }

        /// <summary>
        /// Whether this tab is currently active
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Whether this tab requires a valid model to function
        /// </summary>
        bool RequiresModel { get; }

        /// <summary>
        /// Set the currently selected model
        /// </summary>
        void SetSelectedModel(GameObject model);

        /// <summary>
        /// Set the analysis data for this tab
        /// </summary>
        void SetAnalysisData(HoyoToonModelAnalysisData data);

        /// <summary>
        /// Get quick actions for this tab (for modular system compatibility)
        /// </summary>
        List<HoyoToon.UI.Core.QuickAction> GetQuickActions();
    }

    /// <summary>
    /// Modular base class for HoyoToon tab controllers using the new UI component system
    /// Fully independent implementation with no bridging to the old system
    /// </summary>
    public abstract class HoyoToonModularBaseTabController : IHoyoToonTabController
    {
        #region Protected Fields

        protected ScrollView contentView;
        protected HoyoToonModelAnalysisData analysisData;
        protected GameObject selectedModel;
        protected HoyoToonUIComponentManager componentManager;
        protected Dictionary<string, HoyoToonUIComponent> tabComponents = new Dictionary<string, HoyoToonUIComponent>();
        protected bool isInitialized = false;
        protected bool isActive = false;

        #endregion

        #region Public Properties

        public abstract string TabName { get; }
        public virtual bool RequiresModel => true;
        public bool IsActive => isActive;

        #endregion

        #region IHoyoToonTabController Implementation

        public virtual void Initialize()
        {
            if (isInitialized) return;

            isInitialized = true;
            componentManager = new HoyoToonUIComponentManager();
            InitializeTabComponents();
            OnInitialize();
        }

        public virtual void Initialize(ScrollView contentView, HoyoToonModelAnalysisData analysisData)
        {
            this.contentView = contentView;
            this.analysisData = analysisData;

            if (!isInitialized)
            {
                Initialize();
            }
        }

        public virtual void OnTabSelected()
        {
            isActive = true;
            if (contentView != null)
            {
                contentView.Clear();
                CreateTabContent();
            }
        }

        public virtual void OnTabDeselected()
        {
            isActive = false;
            OnTabDeselectedInternal();
        }

        public virtual void UpdateContent()
        {
            if (isActive && contentView != null)
            {
                RefreshTabContent();
            }
        }

        public virtual void Cleanup()
        {
            isActive = false;
            isInitialized = false;
            contentView = null;
            analysisData = null;
            selectedModel = null;

            // Cleanup UI components
            componentManager?.CleanupAllComponents();
            tabComponents.Clear();

            OnCleanup();
        }

        /// <summary>
        /// Set the currently selected model
        /// </summary>
        public virtual void SetSelectedModel(GameObject model)
        {
            // Only update content if the model actually changed
            if (selectedModel != model)
            {
                selectedModel = model;
                if (isActive)
                {
                    UpdateContent();
                }
            }
        }

        /// <summary>
        /// Set the selected model without triggering content update
        /// Used during model loading to prevent redundant updates before analysis is complete
        /// </summary>
        public virtual void SetSelectedModelWithoutUpdate(GameObject model)
        {
            selectedModel = model;
        }

        /// <summary>
        /// Set the analysis data for this tab
        /// Used to provide analysis results to tabs after analysis is complete
        /// </summary>
        public virtual void SetAnalysisData(HoyoToonModelAnalysisData data)
        {
            analysisData = data;
        }

        /// <summary>
        /// Get quick actions for this tab using the new modular system
        /// </summary>
        public virtual List<HoyoToon.UI.Core.QuickAction> GetQuickActions()
        {
            return new List<HoyoToon.UI.Core.QuickAction>();
        }

        #endregion

        #region Abstract Methods for Derived Classes

        /// <summary>
        /// Initialize tab-specific components
        /// </summary>
        protected abstract void InitializeTabComponents();

        /// <summary>
        /// Create the main tab content using the modular system
        /// </summary>
        protected abstract void CreateTabContent();

        #endregion

        #region Virtual Methods

        /// <summary>
        /// Called during initialization - override for custom setup
        /// </summary>
        protected virtual void OnInitialize()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Called during cleanup - override for custom resource cleanup
        /// </summary>
        protected virtual void OnCleanup()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Called when tab is deselected - override for custom cleanup
        /// </summary>
        protected virtual void OnTabDeselectedInternal()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Refresh tab content when data changes
        /// </summary>
        protected virtual void RefreshTabContent()
        {
            if (contentView != null)
            {
                contentView.Clear();
                CreateTabContent();
            }
            UpdateModularComponents();
        }

        #endregion

        #region Component Management

        /// <summary>
        /// Add a component to this tab
        /// </summary>
        protected T AddComponent<T>() where T : HoyoToonUIComponent, new()
        {
            var component = new T();
            var componentId = component.ComponentId;

            if (!tabComponents.ContainsKey(componentId))
            {
                tabComponents[componentId] = component;
                componentManager.RegisterComponent(component);
                component.Initialize();
                return component;
            }

            return tabComponents[componentId] as T;
        }

        /// <summary>
        /// Get a component by type
        /// </summary>
        protected T GetComponent<T>() where T : HoyoToonUIComponent
        {
            return tabComponents.Values.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Get a component by ID
        /// </summary>
        protected HoyoToonUIComponent GetComponent(string componentId)
        {
            tabComponents.TryGetValue(componentId, out var component);
            return component;
        }

        /// <summary>
        /// Remove a component
        /// </summary>
        protected void RemoveComponent(string componentId)
        {
            if (tabComponents.TryGetValue(componentId, out var component))
            {
                component.Cleanup();
                componentManager.UnregisterComponent(componentId);
                tabComponents.Remove(componentId);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if model is available and valid
        /// </summary>
        protected bool IsModelAvailable()
        {
            return selectedModel != null && analysisData != null && analysisData.hasValidModel;
        }

        /// <summary>
        /// Show no model selected message
        /// </summary>
        protected void ShowNoModelMessage()
        {
            if (contentView == null) return;

            var messageContainer = new VisualElement();
            messageContainer.style.alignItems = Align.Center;
            messageContainer.style.justifyContent = Justify.Center;
            messageContainer.style.flexGrow = 1;
            messageContainer.style.marginTop = 50;

            var messageLabel = new Label("No FBX model selected");
            messageLabel.style.fontSize = 16;
            messageLabel.style.color = Color.gray;
            messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            var instructionLabel = new Label("Select an FBX model to view tab content");
            instructionLabel.style.fontSize = 12;
            instructionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            instructionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            instructionLabel.style.marginTop = 10;

            messageContainer.Add(messageLabel);
            messageContainer.Add(instructionLabel);
            contentView.Add(messageContainer);
        }

        /// <summary>
        /// Update all modular components with current data
        /// </summary>
        protected void UpdateModularComponents()
        {
            var globalData = new Dictionary<string, object>
            {
                { "analysisData", analysisData },
                { "selectedModel", selectedModel },
                { "isModelAvailable", IsModelAvailable() }
            };
            componentManager.UpdateAllComponents(globalData);
        }

        /// <summary>
        /// Check if all game resources are downloaded
        /// </summary>
        protected bool CheckAllResourcesDownloaded()
        {
            try
            {
                // Check if HoyoToonResourceManager exists and has the method
                var resourceManagerType = System.Type.GetType("HoyoToon.HoyoToonResourceManager");
                if (resourceManagerType != null)
                {
                    var getResourceStatusMethod = resourceManagerType.GetMethod("GetResourceStatus");
                    if (getResourceStatusMethod != null)
                    {
                        var resourceStatus = getResourceStatusMethod.Invoke(null, null) as System.Collections.Generic.Dictionary<string, object>;
                        if (resourceStatus != null)
                        {
                            return resourceStatus.ContainsKey("Genshin") &&
                                   resourceStatus.ContainsKey("StarRail") &&
                                   resourceStatus.ContainsKey("Hi3") &&
                                   resourceStatus.ContainsKey("Wuwa") &&
                                   resourceStatus.ContainsKey("ZZZ");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Could not check resource status: {ex.Message}");
            }

            // Fallback: assume resources are available
            return true;
        }

        #endregion

        #region UI Helper Methods

        /// <summary>
        /// Create a section header with styling
        /// </summary>
        protected Label CreateHoyoToonSectionHeader(string title)
        {
            var header = new Label(title);
            header.style.fontSize = 16;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.8f, 0.9f, 1f);
            header.style.marginTop = 15;
            header.style.marginBottom = 10;
            header.style.paddingLeft = 5;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
            return header;
        }

        /// <summary>
        /// Create a subsection header with styling
        /// </summary>
        protected Label CreateHoyoToonSubsectionHeader(string title)
        {
            var header = new Label(title);
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.7f, 0.8f, 0.9f);
            header.style.marginTop = 10;
            header.style.marginBottom = 5;
            header.style.paddingLeft = 3;
            return header;
        }

        /// <summary>
        /// Create an info row with label and value
        /// </summary>
        protected VisualElement CreateHoyoToonInfoRow(string label, string value, Color? valueColor = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 3;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;

            var labelElement = new Label(label);
            labelElement.style.color = new Color(0.8f, 0.8f, 0.8f);
            labelElement.style.flexGrow = 0;
            labelElement.style.flexShrink = 0;
            labelElement.style.minWidth = 120;

            var valueElement = new Label(value);
            valueElement.style.color = valueColor ?? Color.white;
            valueElement.style.flexGrow = 1;
            valueElement.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(labelElement);
            row.Add(valueElement);
            return row;
        }

        /// <summary>
        /// Create a warning box
        /// </summary>
        protected VisualElement CreateHoyoToonWarningBox(string message)
        {
            var box = new VisualElement();
            box.style.backgroundColor = new Color(0.6f, 0.4f, 0.1f, 0.3f);
            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = new Color(0.9f, 0.6f, 0.1f);
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 10;
            box.style.paddingRight = 10;
            box.style.marginTop = 5;
            box.style.marginBottom = 5;

            var label = new Label($"[Warning] {message}");
            label.style.color = new Color(0.9f, 0.8f, 0.6f);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            return box;
        }

        /// <summary>
        /// Create an error box
        /// </summary>
        protected VisualElement CreateHoyoToonErrorBox(string message)
        {
            var box = new VisualElement();
            box.style.backgroundColor = new Color(0.6f, 0.1f, 0.1f, 0.3f);
            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = new Color(0.9f, 0.2f, 0.2f);
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 10;
            box.style.paddingRight = 10;
            box.style.marginTop = 5;
            box.style.marginBottom = 5;

            var label = new Label($"✗ {message}");
            label.style.color = new Color(0.9f, 0.6f, 0.6f);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            return box;
        }

        /// <summary>
        /// Create a success box
        /// </summary>
        protected VisualElement CreateHoyoToonSuccessBox(string message)
        {
            var box = new VisualElement();
            box.style.backgroundColor = new Color(0.1f, 0.6f, 0.1f, 0.3f);
            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = new Color(0.2f, 0.9f, 0.2f);
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 10;
            box.style.paddingRight = 10;
            box.style.marginTop = 5;
            box.style.marginBottom = 5;

            var label = new Label($"✓ {message}");
            label.style.color = new Color(0.6f, 0.9f, 0.6f);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            return box;
        }

        /// <summary>
        /// Create a general info box with custom styling
        /// </summary>
        protected VisualElement CreateHoyoToonInfoBox(string message, Color? backgroundColor = null, Color? textColor = null)
        {
            var box = new VisualElement();
            box.style.backgroundColor = backgroundColor ?? new Color(0.2f, 0.3f, 0.4f);
            box.style.borderTopColor = new Color(0.4f, 0.5f, 0.6f);
            box.style.borderBottomColor = new Color(0.4f, 0.5f, 0.6f);
            box.style.borderLeftColor = new Color(0.4f, 0.5f, 0.6f);
            box.style.borderRightColor = new Color(0.4f, 0.5f, 0.6f);
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;
            box.style.borderTopLeftRadius = 4;
            box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4;
            box.style.borderBottomRightRadius = 4;
            box.style.marginTop = 5;
            box.style.marginBottom = 5;
            box.style.paddingTop = 8;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;

            var label = new Label(message);
            label.style.fontSize = 12;
            label.style.color = textColor ?? new Color(0.8f, 0.9f, 1.0f);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            return box;
        }

        /// <summary>
        /// Create a styled button using the modular system
        /// </summary>
        protected Button CreateHoyoToonStyledButton(string text, System.Action onClick, Color? backgroundColor = null)
        {
            return HoyoToonUIFactory.CreateStyledButton(text, onClick, backgroundColor, 30);
        }

        /// <summary>
        /// Create a primary styled button
        /// </summary>
        protected Button CreatePrimaryButton(string text, System.Action onClick, int height = 30)
        {
            return HoyoToonUIFactory.CreateStyledButton(text, onClick, HoyoToonUIFactory.Colors.Primary, height);
        }

        /// <summary>
        /// Create a success styled button
        /// </summary>
        protected Button CreateSuccessButton(string text, System.Action onClick, int height = 30)
        {
            return HoyoToonUIFactory.CreateStyledButton(text, onClick, HoyoToonUIFactory.Colors.Success, height);
        }

        /// <summary>
        /// Create a warning styled button
        /// </summary>
        protected Button CreateWarningButton(string text, System.Action onClick, int height = 30)
        {
            return HoyoToonUIFactory.CreateStyledButton(text, onClick, HoyoToonUIFactory.Colors.Warning, height);
        }

        /// <summary>
        /// Create an error styled button
        /// </summary>
        protected Button CreateErrorButton(string text, System.Action onClick, int height = 30)
        {
            return HoyoToonUIFactory.CreateStyledButton(text, onClick, HoyoToonUIFactory.Colors.Error, height);
        }

        /// <summary>
        /// Create a foldout with HoyoToon styling
        /// </summary>
        protected Foldout CreateHoyoToonFoldout(string title, bool defaultValue = false)
        {
            var foldout = new Foldout();
            foldout.text = title;
            foldout.value = defaultValue;
            foldout.style.marginTop = 5;
            foldout.style.marginBottom = 5;

            // Style the toggle
            var toggle = foldout.Q<Toggle>();
            if (toggle != null)
            {
                toggle.style.unityFontStyleAndWeight = FontStyle.Bold;
                toggle.style.color = new Color(0.8f, 0.9f, 1f);
            }

            return foldout;
        }

        /// <summary>
        /// Create a text field with HoyoToon styling including proper background and border
        /// </summary>
        protected TextField CreateHoyoToonTextField(string label = "", string value = "")
        {
            var textField = new TextField(label);
            textField.value = value;

            // Apply proper background and border styling
            ApplyTextFieldStyling(textField);

            return textField;
        }

        /// <summary>
        /// Apply consistent text field styling across the UI
        /// </summary>
        protected void ApplyTextFieldStyling(TextField textField)
        {
            // Background color
            textField.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.24f, 0.24f, 1f)
                : new Color(0.9f, 0.9f, 0.9f, 1f);

            // Border styling
            textField.style.borderLeftColor = textField.style.borderRightColor =
                textField.style.borderTopColor = textField.style.borderBottomColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.35f, 0.35f, 0.35f, 1f)
                    : new Color(0.6f, 0.6f, 0.6f, 1f);

            textField.style.borderLeftWidth = textField.style.borderRightWidth =
                textField.style.borderTopWidth = textField.style.borderBottomWidth = 1;

            // Border radius for rounded corners
            textField.style.borderTopLeftRadius = textField.style.borderTopRightRadius =
                textField.style.borderBottomLeftRadius = textField.style.borderBottomRightRadius = 3;

            // Padding for better text spacing
            textField.style.paddingLeft = textField.style.paddingRight = 4;
            textField.style.paddingTop = textField.style.paddingBottom = 2;

            // Minimum height for consistency
            textField.style.minHeight = 20;
        }

        /// <summary>
        /// Apply consistent dropdown styling
        /// </summary>
        protected void ApplyDropdownStyling(DropdownField dropdown)
        {
            // Background color
            dropdown.style.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.24f, 0.24f, 1f)
                : new Color(0.9f, 0.9f, 0.9f, 1f);

            // Border styling
            dropdown.style.borderLeftColor = dropdown.style.borderRightColor =
                dropdown.style.borderTopColor = dropdown.style.borderBottomColor =
                EditorGUIUtility.isProSkin
                    ? new Color(0.35f, 0.35f, 0.35f, 1f)
                    : new Color(0.6f, 0.6f, 0.6f, 1f);

            dropdown.style.borderLeftWidth = dropdown.style.borderRightWidth =
                dropdown.style.borderTopWidth = dropdown.style.borderBottomWidth = 1;

            // Border radius for rounded corners
            dropdown.style.borderTopLeftRadius = dropdown.style.borderTopRightRadius =
                dropdown.style.borderBottomLeftRadius = dropdown.style.borderBottomRightRadius = 3;

            // Minimum height for consistency
            dropdown.style.minHeight = 20;
        }

        /// <summary>
        /// Create a styled dropdown field with HoyoToon styling
        /// </summary>
        protected DropdownField CreateHoyoToonDropdown()
        {
            var dropdown = new DropdownField();
            ApplyDropdownStyling(dropdown);
            return dropdown;
        }

        /// <summary>
        /// Create a styled dropdown field with options and HoyoToon styling
        /// </summary>
        protected DropdownField CreateHoyoToonDropdown(List<string> choices, int defaultIndex = 0)
        {
            var dropdown = new DropdownField(choices, defaultIndex);
            ApplyDropdownStyling(dropdown);
            return dropdown;
        }

        #endregion

        #region Asset Service

        /// <summary>
        /// Centralized asset service to avoid repeated AssetDatabase queries
        /// </summary>
        public static class HoyoToonAssetService
        {
            private static Dictionary<string, string[]> _materialAssetCache = new Dictionary<string, string[]>();
            private static Dictionary<string, string[]> _jsonAssetCache = new Dictionary<string, string[]>();
            private static Dictionary<string, List<LocalResourceInfo>> _resourceCache = new Dictionary<string, List<LocalResourceInfo>>();
            private static Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
            private static bool _allResourcesCached = false;
            private static List<LocalResourceInfo> _allResourcesCache = null;
            private static string _lastCachedModelPath = null;

            /// <summary>
            /// Find materials associated with a specific model, with caching
            /// </summary>
            public static string[] FindMaterialsForModel(GameObject model)
            {
                if (model == null) return new string[0];

                string modelPath = AssetDatabase.GetAssetPath(model);
                string modelDirectory = Path.GetDirectoryName(modelPath);

                // Check cache first
                if (_lastCachedModelPath == modelPath && _materialAssetCache.ContainsKey(modelPath))
                {
                    return _materialAssetCache[modelPath];
                }

                // Clear cache if model changed
                if (_lastCachedModelPath != modelPath)
                {
                    _materialAssetCache.Clear();
                    _jsonAssetCache.Clear();
                    _lastCachedModelPath = modelPath;
                }

                // Search for materials in model directory
                string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { modelDirectory });
                _materialAssetCache[modelPath] = materialGuids;

                return materialGuids;
            }

            /// <summary>
            /// Find JSON files associated with a specific model, with caching
            /// </summary>
            public static string[] FindJsonFilesForModel(GameObject model)
            {
                if (model == null) return new string[0];

                string modelPath = AssetDatabase.GetAssetPath(model);
                string modelDirectory = Path.GetDirectoryName(modelPath);

                // Check cache first
                if (_lastCachedModelPath == modelPath && _jsonAssetCache.ContainsKey(modelPath))
                {
                    return _jsonAssetCache[modelPath];
                }

                // Clear cache if model changed
                if (_lastCachedModelPath != modelPath)
                {
                    _materialAssetCache.Clear();
                    _jsonAssetCache.Clear();
                    _lastCachedModelPath = modelPath;
                }

                // Search for JSON files in model directory
                string[] jsonGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { modelDirectory })
                    .Where(guid =>
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        return Path.GetExtension(path).ToLower() == ".json";
                    }).ToArray();

                _jsonAssetCache[modelPath] = jsonGuids;
                return jsonGuids;
            }

            /// <summary>
            /// Get all local resources with caching
            /// </summary>
            public static List<LocalResourceInfo> GetAllLocalResources()
            {
                if (_allResourcesCached && _allResourcesCache != null)
                    return _allResourcesCache;

                _allResourcesCache = HoyoToonResourceManager.GetAllLocalResources();
                _allResourcesCached = true;
                return _allResourcesCache;
            }

            /// <summary>
            /// Get local resources for a specific game with caching
            /// </summary>
            public static List<LocalResourceInfo> GetLocalResourcesForGame(string gameKey)
            {
                if (_resourceCache.TryGetValue(gameKey, out var cached))
                    return cached;

                var resources = HoyoToonResourceManager.GetLocalResourcesForGame(gameKey);
                _resourceCache[gameKey] = resources;
                return resources;
            }

            /// <summary>
            /// Check if resources are available
            /// </summary>
            public static bool AreResourcesAvailable()
            {
                return HoyoToonResourceManager.AreResourcesAvailable();
            }

            /// <summary>
            /// Check if resources are available for a specific game
            /// </summary>
            public static bool HasResourcesForGame(string gameKey)
            {
                return HoyoToonResourceManager.HasResourcesForGame(gameKey);
            }

            // Material Manager Integration
            /// <summary>
            /// Validate JSON structure using MaterialManager
            /// </summary>
            public static bool ValidateJsonStructure(string jsonPath, out string errorMessage)
            {
                return HoyoToonMaterialManager.ValidateJsonStructure(jsonPath, out errorMessage);
            }

            /// <summary>
            /// Check if JSON is valid material JSON using MaterialManager
            /// </summary>
            public static bool IsValidMaterialJson(string jsonPath)
            {
                return HoyoToonMaterialManager.IsValidMaterialJson(jsonPath);
            }

            /// <summary>
            /// Detect shader from material data using MaterialManager
            /// </summary>
            public static string DetectShaderFromMaterialData(MaterialJsonStructure materialData)
            {
                return HoyoToonMaterialManager.DetectShaderFromMaterialData(materialData);
            }

            /// <summary>
            /// Apply custom settings to material using MaterialManager
            /// </summary>
            public static void ApplyCustomSettingsToMaterial(Material material, string jsonFileName)
            {
                HoyoToonMaterialManager.ApplyCustomSettingsToMaterial(material, jsonFileName);
            }

            /// <summary>
            /// Clear generated materials using MaterialManager
            /// </summary>
            public static void ClearGeneratedMaterials(GameObject fbxModel)
            {
                HoyoToonMaterialManager.ClearGeneratedMaterials(fbxModel);
            }

            // Texture Manager Integration
            /// <summary>
            /// Load texture with caching
            /// </summary>
            public static Texture2D LoadTexture(string texturePath)
            {
                if (string.IsNullOrEmpty(texturePath)) return null;

                if (_textureCache.TryGetValue(texturePath, out var cachedTexture))
                    return cachedTexture;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture != null)
                {
                    _textureCache[texturePath] = texture;
                }
                return texture;
            }

            /// <summary>
            /// Load material asset with validation
            /// </summary>
            public static Material LoadMaterial(string materialPath)
            {
                if (string.IsNullOrEmpty(materialPath)) return null;
                return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }

            /// <summary>
            /// Load text asset (JSON files)
            /// </summary>
            public static TextAsset LoadTextAsset(string assetPath)
            {
                if (string.IsNullOrEmpty(assetPath)) return null;
                return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            }

            /// <summary>
            /// Apply texture hardset using TextureManager
            /// </summary>
            public static void HardsetTexture(Material material, string propertyName, Shader shader)
            {
                HoyoToonTextureManager.HardsetTexture(material, propertyName, shader);
            }

            // Mesh Manager Integration
            /// <summary>
            /// Check if mesh has proper tangents using MeshManager
            /// </summary>
            public static bool HasValidTangents(Mesh mesh)
            {
                // Delegate to MeshManager when needed
                return mesh != null && mesh.tangents != null && mesh.tangents.Length > 0;
            }

            /// <summary>
            /// Get asset path with caching
            /// </summary>
            public static string GetAssetPath(UnityEngine.Object obj)
            {
                if (obj == null) return string.Empty;
                return AssetDatabase.GetAssetPath(obj);
            }

            /// <summary>
            /// Clear all caches manually
            /// </summary>
            public static void ClearCaches()
            {
                _materialAssetCache.Clear();
                _jsonAssetCache.Clear();
                _resourceCache.Clear();
                _textureCache.Clear();
                _allResourcesCached = false;
                _allResourcesCache = null;
                _lastCachedModelPath = null;
            }

            /// <summary>
            /// Get materials by name in model directory
            /// </summary>
            public static string[] FindMaterialsByName(GameObject model, string materialName)
            {
                if (model == null) return new string[0];

                string modelPath = AssetDatabase.GetAssetPath(model);
                string modelDirectory = Path.GetDirectoryName(modelPath);

                return AssetDatabase.FindAssets($"t:Material {materialName}", new[] { modelDirectory });
            }
        }

        #endregion
    }
}