using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;
using HoyoToon.UI.Core;
using HoyoToon.UI.Components;

namespace HoyoToon
{
    /// <summary>
    /// HoyoToon Materials Tab Controller
    /// Handles material analysis, shader assignment, JSON detection, and material management
    /// Updated to use the new modular UI system
    /// </summary>
    public class HoyoToonMaterialsTabController : HoyoToonModularBaseTabController
    {
        public override string TabName => "Materials";

        // Cache for discovered JSON files and materials
        private List<string> availableJsonFiles = new List<string>();
        private Dictionary<string, Material> materialPreviews = new Dictionary<string, Material>();
        private Dictionary<string, Texture2D> materialPreviewTextures = new Dictionary<string, Texture2D>();
        private Dictionary<string, JsonFileInfo> jsonFileInfoCache = new Dictionary<string, JsonFileInfo>();

        // Cache invalidation tracking
        private GameObject lastCachedModel = null;

        // Pagination and filtering for enhanced material gallery
        private int currentPage = 0;
        private int materialsPerPage = 6; // Fewer per page due to richer material cards
        private bool showOnlyInvalid = false;
        private string searchFilter = "";

        // UI Elements for material gallery
        private VisualElement materialGalleryContainer;
        private Label paginationLabel;
        private Button prevPageBtn;
        private Button nextPageBtn;

        // Data structure for JSON file analysis
        [System.Serializable]
        public class JsonFileInfo
        {
            public string filePath;
            public string fileName;
            public bool isValidMaterialJson;
            public string apiSuggestedShader;
            public string materialType;
            public string currentShaderOverride;
            public List<string> availableShaderOverrides;
            public List<string> expectedTextures;
            public List<string> missingRequiredTextures;
            public string errorMessage;
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            // Clear any cached JSON analysis to ensure fresh shader detection after updates
            jsonFileInfoCache.Clear();
            // Automatically detect JSON files on initialization
            DetectAvailableJsonFiles();
        }

        #region Modular UI System Implementation

        protected override void InitializeTabComponents()
        {
            // Register components for materials tab
            AddComponent<ValidationStatusComponent>();
            AddComponent<ModelInfoComponent>();
            AddComponent<ProgressIndicatorComponent>();
        }

        public override List<QuickAction> GetQuickActions()
        {
            var actions = new List<QuickAction>();

            if (!IsModelAvailable())
                return actions;

            // Generate Materials action (JSON detection is now automatic)
            actions.Add(new QuickAction("Generate Materials", () =>
            {
                try
                {
                    GenerateAllMaterials();
                    // Trigger re-analysis to update the UI
                    HoyoToonUIManager.Instance?.ForceAnalysisRefresh();
                    EditorUtility.DisplayDialog("Success", "Materials generated successfully!", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Material generation failed: {e.Message}", "OK");
                }
            }));

            // Clear Materials action
            actions.Add(new QuickAction("Clear Materials", () =>
            {
                try
                {
                    HoyoToonMaterialManager.ClearGeneratedMaterials(selectedModel);
                    // Trigger re-analysis to update the UI
                    HoyoToonUIManager.Instance?.ForceAnalysisRefresh();
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to clear materials: {e.Message}", "OK");
                }
            }));

            // Export JSONs action
            actions.Add(new QuickAction("Export Materials to JSONs", () =>
            {
                try
                {
                    ExportMaterialsToJson();
                    EditorUtility.DisplayDialog("Success", "Materials exported to JSON successfully!", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to export materials: {e.Message}", "OK");
                }
            }));

            return actions;
        }

        #endregion

        protected override void RefreshTabContent()
        {
            // Only invalidate cache if model changed, not on every refresh
            if (selectedModel != lastCachedModel)
            {
                // Clear cached JSON analysis only when model changes
                jsonFileInfoCache.Clear();
                availableJsonFiles.Clear();
                lastCachedModel = selectedModel;

                // Detect JSON files for new model
                if (selectedModel != null)
                {
                    DetectAvailableJsonFiles();
                }
            }

            base.RefreshTabContent();
        }

        public override void SetSelectedModel(GameObject model)
        {
            base.SetSelectedModel(model);

            // Only clear cache and detect JSON files if model actually changed
            if (model != lastCachedModel)
            {
                // Clear cached JSON analysis when model changes
                jsonFileInfoCache.Clear();
                availableJsonFiles.Clear();
                lastCachedModel = model;

                // Automatically detect JSON files when model changes
                if (model != null)
                {
                    DetectAvailableJsonFiles();
                }
            }
        }

        protected override void CreateTabContent()
        {
            if (!IsModelAvailable())
            {
                ShowNoModelMessage();
                return;
            }

            // Materials Overview Section
            contentView.Add(CreateHoyoToonSectionHeader("Materials Overview"));
            CreateMaterialsOverviewSection();

            // JSON Files Section
            contentView.Add(CreateHoyoToonSectionHeader("Available Jsons"));
            CreateJsonFilesSection();

            // Global Shader Override Section
            if (analysisData.materials.Count > 0)
            {
                contentView.Add(CreateHoyoToonSectionHeader("Global Shader Override"));
                CreateGlobalShaderOverrideSection();
            }

            // Global Keyword Management Section
            if (analysisData.materials.Count > 0)
            {
                contentView.Add(CreateHoyoToonSectionHeader("Global Shader Keywords"));
                CreateGlobalKeywordSection();
            }

            // Material Details Section
            if (analysisData.materials.Count > 0)
            {
                contentView.Add(CreateHoyoToonSectionHeader("Materials"));
                CreateMaterialDetailsSection();
            }

            // Note: Shader recommendations moved to HoyoToonShadersTabController
        }

        private void CreateMaterialsOverviewSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Summary information
            container.Add(CreateHoyoToonInfoRow("Total Materials:", analysisData.materialCount.ToString()));
            container.Add(CreateHoyoToonInfoRow("Valid Materials:",
                analysisData.materials.Count(m => m.isValid).ToString()));
            container.Add(CreateHoyoToonInfoRow("Materials with Issues:",
                analysisData.materials.Count(m => !m.isValid).ToString(),
                analysisData.materials.Count(m => !m.isValid) > 0 ? Color.red : Color.green));

            // JSON files information
            container.Add(CreateHoyoToonInfoRow("Available JSON Files:", availableJsonFiles.Count.ToString(),
                availableJsonFiles.Count > 0 ? Color.green : Color.yellow));

            // Memory usage
            if (analysisData.materials.Any(m => m.memoryUsage > 0))
            {
                long totalMemory = analysisData.materials.Sum(m => m.memoryUsage);
                string memoryFormatted = FormatMemorySize(totalMemory);
                container.Add(CreateHoyoToonInfoRow("Total Material Memory:", memoryFormatted,
                    totalMemory > 50 * 1024 * 1024 ? Color.yellow : Color.white));
            }

            // HoyoToon shader status
            int hoyoToonShaderCount = analysisData.materials.Count(m => HoyoToonDataManager.IsHoyoToonShader(m.currentShader));
            container.Add(CreateHoyoToonInfoRow("HoyoToon Shaders:", $"{hoyoToonShaderCount}/{analysisData.materialCount}",
                hoyoToonShaderCount == analysisData.materialCount ? Color.green : Color.yellow));

            // Status indicators
            if (analysisData.hasMaterials)
            {
                if (hoyoToonShaderCount == analysisData.materialCount)
                {
                    container.Add(CreateHoyoToonSuccessBox("All materials are using HoyoToon shaders"));
                }
                else if (hoyoToonShaderCount > 0)
                {
                    container.Add(CreateHoyoToonWarningBox($"{analysisData.materialCount - hoyoToonShaderCount} materials need shader updates"));
                }
                else
                {
                    container.Add(CreateHoyoToonErrorBox("No materials are using HoyoToon shaders"));
                }
            }
            else
            {
                container.Add(CreateHoyoToonErrorBox("No materials found on model"));
            }

            contentView.Add(container);
        }

        private void CreateGlobalKeywordSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Get all materials from the selected model
            var materials = GetModelMaterials();
            if (materials.Count == 0)
            {
                container.Add(CreateHoyoToonWarningBox("No materials found on the selected model"));
                contentView.Add(container);
                return;
            }

            // Analyze common shader properties across all materials
            var propertyAnalysis = AnalyzeShaderProperties(materials);

            // Create legend for colored indicators
            var legendContainer = new VisualElement();
            legendContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.1f);
            legendContainer.style.borderTopColor = legendContainer.style.borderBottomColor =
                legendContainer.style.borderLeftColor = legendContainer.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            legendContainer.style.borderTopWidth = legendContainer.style.borderBottomWidth =
                legendContainer.style.borderLeftWidth = legendContainer.style.borderRightWidth = 1;
            legendContainer.style.borderTopLeftRadius = legendContainer.style.borderTopRightRadius =
                legendContainer.style.borderBottomLeftRadius = legendContainer.style.borderBottomRightRadius = 4;
            legendContainer.style.paddingTop = legendContainer.style.paddingBottom =
                legendContainer.style.paddingLeft = legendContainer.style.paddingRight = 8;
            legendContainer.style.marginBottom = 10;

            var legendTitle = new Label("Property Status Legend");
            legendTitle.style.fontSize = 12;
            legendTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            legendTitle.style.marginBottom = 5;
            legendContainer.Add(legendTitle);

            // Legend items
            var legendRow1 = new VisualElement();
            legendRow1.style.flexDirection = FlexDirection.Row;
            legendRow1.style.alignItems = Align.Center;
            legendRow1.style.marginBottom = 3;

            var greenCircle = new Label("●");
            greenCircle.style.color = Color.green;
            greenCircle.style.marginRight = 5;
            var greenLabel = new Label("All materials have the same value");
            greenLabel.style.fontSize = 11;

            legendRow1.Add(greenCircle);
            legendRow1.Add(greenLabel);
            legendContainer.Add(legendRow1);

            var legendRow2 = new VisualElement();
            legendRow2.style.flexDirection = FlexDirection.Row;
            legendRow2.style.alignItems = Align.Center;

            var yellowCircle = new Label("○");
            yellowCircle.style.color = Color.yellow;
            yellowCircle.style.marginRight = 5;
            var yellowLabel = new Label("Materials have different values");
            yellowLabel.style.fontSize = 11;

            legendRow2.Add(yellowCircle);
            legendRow2.Add(yellowLabel);
            legendContainer.Add(legendRow2);

            container.Add(legendContainer);

            // // Add materials count info
            // container.Add(CreateHoyoToonInfoRow("Materials Found:", materials.Count.ToString()));
            // container.Add(CreateHoyoToonInfoRow("Common Properties:", propertyAnalysis.commonProperties.Count.ToString()));

            if (propertyAnalysis.commonProperties.Count > 0)
            {
                container.Add(CreateHoyoToonSubsectionHeader("Global Property Controls:"));

                foreach (var property in propertyAnalysis.commonProperties)
                {
                    var propertyContainer = new VisualElement();
                    propertyContainer.style.flexDirection = FlexDirection.Row;
                    propertyContainer.style.alignItems = Align.Center;
                    propertyContainer.style.marginBottom = 8;
                    propertyContainer.style.paddingLeft = 10;

                    // Property label with tooltip
                    var label = new Label(property.displayName);
                    label.style.minWidth = 120;
                    label.style.marginRight = 10;
                    label.tooltip = property.tooltip;

                    // Consistency indicator
                    var consistencyIcon = new Label(propertyAnalysis.hasConsistentValues[property.propertyName] ? "●" : "○");
                    consistencyIcon.style.color = propertyAnalysis.hasConsistentValues[property.propertyName] ? Color.green : Color.yellow;
                    consistencyIcon.style.marginRight = 5;
                    consistencyIcon.tooltip = propertyAnalysis.hasConsistentValues[property.propertyName]
                        ? "All materials have the same value"
                        : "Materials have different values";

                    // Property control based on type
                    VisualElement propertyControl = CreatePropertyControl(property, propertyAnalysis.propertyValues[property.propertyName], materials);
                    propertyControl.style.flexGrow = 1;
                    propertyControl.style.maxWidth = 200;

                    propertyContainer.Add(consistencyIcon);
                    propertyContainer.Add(label);
                    propertyContainer.Add(propertyControl);
                    container.Add(propertyContainer);
                }

                // Action buttons
                var buttonContainer = new VisualElement();
                buttonContainer.style.flexDirection = FlexDirection.Row;
                buttonContainer.style.marginTop = 10;
                buttonContainer.style.justifyContent = Justify.SpaceAround;

                var refreshButton = new Button(() => RefreshTabContent())
                {
                    text = "Refresh Properties"
                };
                refreshButton.style.flexGrow = 1;
                refreshButton.style.marginRight = 5;

                var resetButton = new Button(() => ResetAllProperties(materials))
                {
                    text = "Reset All Properties"
                };
                resetButton.style.flexGrow = 1;
                resetButton.style.marginLeft = 5;

                buttonContainer.Add(refreshButton);
                buttonContainer.Add(resetButton);
                container.Add(buttonContainer);
            }
            else
            {
                container.Add(CreateHoyoToonWarningBox("No common shader properties found across materials"));
            }

            contentView.Add(container);
        }

        private void CreateJsonFilesSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Ensure JSON files are discovered if not already cached
            if (availableJsonFiles.Count == 0 && selectedModel != null)
            {
                DetectAvailableJsonFiles();
            }

            if (availableJsonFiles.Count == 0)
            {
                container.Add(CreateHoyoToonWarningBox("No material JSON files found in the model directory. Make sure your model has been properly converted with Hoyo2VRC or that JSON files are located in the same directory as the FBX model."));
            }
            else
            {
                // Group JSON files by directory
                var jsonGroups = availableJsonFiles
                    .GroupBy(path => Path.GetDirectoryName(path))
                    .OrderBy(g => g.Key);

                foreach (var group in jsonGroups)
                {
                    var groupFoldout = CreateHoyoToonFoldout($"Json ({group.Count()} files)", false);

                    var groupContent = new VisualElement();
                    groupContent.style.marginLeft = 15;
                    groupContent.style.marginTop = 5;

                    foreach (var jsonFile in group.OrderBy(f => Path.GetFileNameWithoutExtension(f)))
                    {
                        CreateJsonFileItem(groupContent, jsonFile);
                    }

                    groupFoldout.Add(groupContent);
                    container.Add(groupFoldout);
                }
            }

            contentView.Add(container);
        }

        private void CreateJsonFileItem(VisualElement parent, string jsonPath)
        {
            // Get cached JSON info
            if (!jsonFileInfoCache.TryGetValue(jsonPath, out JsonFileInfo jsonInfo))
            {
                jsonInfo = AnalyzeJsonFile(jsonPath);
                jsonFileInfoCache[jsonPath] = jsonInfo;
            }

            var jsonItem = new VisualElement();
            jsonItem.style.flexDirection = FlexDirection.Column;
            jsonItem.style.marginBottom = 10;
            jsonItem.style.paddingTop = 10;
            jsonItem.style.paddingBottom = 10;
            jsonItem.style.paddingLeft = 15;
            jsonItem.style.paddingRight = 15;
            jsonItem.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.7f);
            jsonItem.style.borderTopLeftRadius = 6;
            jsonItem.style.borderTopRightRadius = 6;
            jsonItem.style.borderBottomLeftRadius = 6;
            jsonItem.style.borderBottomRightRadius = 6;

            // Header row with file info
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 10;

            // JSON file info
            var infoContainer = new VisualElement();
            infoContainer.style.flexGrow = 1;

            var fileName = Path.GetFileNameWithoutExtension(jsonPath);
            var fileLabel = new Label(fileName);
            fileLabel.style.color = Color.white;
            fileLabel.style.fontSize = 14;
            fileLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var pathLabel = new Label(jsonPath);
            pathLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            pathLabel.style.fontSize = 10;

            infoContainer.Add(fileLabel);
            infoContainer.Add(pathLabel);

            headerRow.Add(infoContainer);

            // API Validation and Shader Info Section
            var apiInfoContainer = new VisualElement();
            apiInfoContainer.style.marginBottom = 10;

            // API Validation Status
            var validationRow = new VisualElement();
            validationRow.style.flexDirection = FlexDirection.Row;
            validationRow.style.justifyContent = Justify.SpaceBetween;
            validationRow.style.alignItems = Align.Center;
            validationRow.style.marginBottom = 5;

            var validationLabel = new Label("API Validation:");
            validationLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            validationLabel.style.fontSize = 11;

            var validationStatus = new Label(jsonInfo.isValidMaterialJson ? "✓ Valid JSON" : "✗ Invalid JSON");
            validationStatus.style.color = jsonInfo.isValidMaterialJson ? Color.green : Color.red;
            validationStatus.style.fontSize = 11;
            validationStatus.style.unityFontStyleAndWeight = FontStyle.Bold;

            validationRow.Add(validationLabel);
            validationRow.Add(validationStatus);

            // Material Type
            var typeRow = new VisualElement();
            typeRow.style.flexDirection = FlexDirection.Row;
            typeRow.style.justifyContent = Justify.SpaceBetween;
            typeRow.style.alignItems = Align.Center;
            typeRow.style.marginBottom = 8;

            var typeLabel = new Label("Material Type:");
            typeLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            typeLabel.style.fontSize = 11;

            // Material Type Dropdown - use material types from the shader's MaterialSettings
            var availableMaterialTypes = HoyoToonDataManager.GetMaterialTypesForShader(jsonInfo.apiSuggestedShader);
            var materialTypeDropdown = new DropdownField();
            materialTypeDropdown.choices = availableMaterialTypes;

            // Set current value, defaulting to first available type if current type isn't valid
            if (availableMaterialTypes.Contains(jsonInfo.materialType))
            {
                materialTypeDropdown.value = jsonInfo.materialType;
            }
            else if (availableMaterialTypes.Count > 0)
            {
                materialTypeDropdown.value = availableMaterialTypes[0];
                jsonInfo.materialType = availableMaterialTypes[0];
            }

            materialTypeDropdown.style.width = 120;
            materialTypeDropdown.style.fontSize = 10;
            materialTypeDropdown.RegisterValueChangedCallback(evt =>
            {
                jsonInfo.materialType = evt.newValue;
                HoyoToonLogs.LogDebug($"Changed material type for {jsonInfo.fileName} to {evt.newValue}");
            });

            typeRow.Add(typeLabel);
            typeRow.Add(materialTypeDropdown);

            apiInfoContainer.Add(validationRow);
            apiInfoContainer.Add(typeRow);

            // Shader Override Section
            var overrideContainer = new VisualElement();
            overrideContainer.style.marginBottom = 10;

            var overrideRow = new VisualElement();
            overrideRow.style.flexDirection = FlexDirection.Row;
            overrideRow.style.justifyContent = Justify.SpaceBetween;
            overrideRow.style.alignItems = Align.Center;

            var overrideLabel = new Label("Select Shader Overwrite:");
            overrideLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            overrideLabel.style.fontSize = 11;

            // Shader Override Dropdown - show full shader names and create a mapping
            var shaderOverrideDropdown = new DropdownField();
            var shaderPathToDisplayName = new Dictionary<string, string>();
            var displayNameToShaderPath = new Dictionary<string, string>();

            if (jsonInfo.availableShaderOverrides.Count > 0)
            {
                var displayChoices = new List<string>();

                // Create display names for each shader, starting with the API suggested shader
                var apiShaderDisplayName = HoyoToonDataManager.GetFriendlyShaderName(jsonInfo.apiSuggestedShader);
                displayChoices.Add(apiShaderDisplayName);
                shaderPathToDisplayName[jsonInfo.apiSuggestedShader] = apiShaderDisplayName;
                displayNameToShaderPath[apiShaderDisplayName] = jsonInfo.apiSuggestedShader;

                // Add other available shaders (excluding the API suggested one to avoid duplicates)
                foreach (var shaderPath in jsonInfo.availableShaderOverrides)
                {
                    if (shaderPath != jsonInfo.apiSuggestedShader)
                    {
                        var displayName = HoyoToonDataManager.GetFriendlyShaderName(shaderPath);
                        displayChoices.Add(displayName);
                        shaderPathToDisplayName[shaderPath] = displayName;
                        displayNameToShaderPath[displayName] = shaderPath;
                    }
                }

                shaderOverrideDropdown.choices = displayChoices;

                // Automatically set to API suggested shader, or current override if different
                if (!string.IsNullOrEmpty(jsonInfo.currentShaderOverride) &&
                    shaderPathToDisplayName.TryGetValue(jsonInfo.currentShaderOverride, out var currentDisplayName))
                {
                    shaderOverrideDropdown.value = currentDisplayName;
                }
                else
                {
                    // Default to API suggested shader
                    shaderOverrideDropdown.value = apiShaderDisplayName;
                    jsonInfo.currentShaderOverride = jsonInfo.apiSuggestedShader;
                }
            }
            else
            {
                shaderOverrideDropdown.choices = new List<string> { "No overrides available" };
                shaderOverrideDropdown.value = "No overrides available";
                shaderOverrideDropdown.SetEnabled(false);
            }

            shaderOverrideDropdown.style.width = 250; // Wider to accommodate full shader names
            shaderOverrideDropdown.style.fontSize = 10;
            shaderOverrideDropdown.RegisterValueChangedCallback(evt =>
            {
                // Find the full shader path from the display name
                if (displayNameToShaderPath.TryGetValue(evt.newValue, out var shaderPath))
                {
                    jsonInfo.currentShaderOverride = shaderPath;
                }
                else
                {
                    jsonInfo.currentShaderOverride = jsonInfo.apiSuggestedShader;
                }
                HoyoToonLogs.LogDebug($"Changed shader selection for {jsonInfo.fileName} to {jsonInfo.currentShaderOverride}");
            });

            overrideRow.Add(overrideLabel);
            overrideRow.Add(shaderOverrideDropdown);
            overrideContainer.Add(overrideRow);

            // Error message if JSON is invalid or outdated
            if (!jsonInfo.isValidMaterialJson && !string.IsNullOrEmpty(jsonInfo.errorMessage))
            {
                var errorBox = CreateHoyoToonErrorBox(jsonInfo.errorMessage);
                overrideContainer.Add(errorBox);
            }

            // Action buttons
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.justifyContent = Justify.FlexEnd;
            buttonsContainer.style.marginTop = 10;

            var generateBtn = CreateHoyoToonStyledButton("Generate Material", () =>
            {
                GenerateMaterialFromJsonWithOverride(jsonPath, jsonInfo);
            }, new Color(0.3f, 0.5f, 0.7f));
            generateBtn.style.height = 28;
            generateBtn.style.minWidth = 120;
            generateBtn.style.fontSize = 11;
            generateBtn.style.marginRight = 8;

            var selectBtn = CreateHoyoToonStyledButton("Select JSON", () =>
            {
                var asset = HoyoToonAssetService.LoadTextAsset(jsonPath);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }, new Color(0.4f, 0.4f, 0.4f));
            selectBtn.style.height = 28;
            selectBtn.style.minWidth = 80;
            selectBtn.style.fontSize = 11;

            buttonsContainer.Add(generateBtn);
            buttonsContainer.Add(selectBtn);

            // Assemble the complete JSON item
            jsonItem.Add(headerRow);
            jsonItem.Add(apiInfoContainer);
            jsonItem.Add(overrideContainer);
            jsonItem.Add(buttonsContainer);

            parent.Add(jsonItem);
        }

        private void CreateGlobalShaderOverrideSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            container.style.borderTopLeftRadius = 5;
            container.style.borderTopRightRadius = 5;
            container.style.borderBottomLeftRadius = 5;
            container.style.borderBottomRightRadius = 5;
            container.style.paddingTop = 15;
            container.style.paddingBottom = 15;
            container.style.paddingLeft = 15;
            container.style.paddingRight = 15;
            container.style.marginBottom = 15;

            // Description
            var descriptionLabel = new Label("Apply a shader override to all materials at once.");
            descriptionLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            descriptionLabel.style.fontSize = 11;
            descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            descriptionLabel.style.marginBottom = 10;
            container.Add(descriptionLabel);

            // Main control row
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            mainRow.style.justifyContent = Justify.SpaceBetween;

            // Shader selection
            var shaderContainer = new VisualElement();
            shaderContainer.style.flexDirection = FlexDirection.Row;
            shaderContainer.style.alignItems = Align.Center;
            shaderContainer.style.flexGrow = 1;

            var shaderLabel = new Label("Shader Override:");
            shaderLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            shaderLabel.style.fontSize = 12;
            shaderLabel.style.marginRight = 10;
            shaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            shaderContainer.Add(shaderLabel);

            // Get all available shader overrides from any valid material
            var allShaderOverrides = new HashSet<string>();
            foreach (var jsonFile in jsonFileInfoCache.Values)
            {
                if (jsonFile.isValidMaterialJson && jsonFile.availableShaderOverrides != null)
                {
                    foreach (var shader in jsonFile.availableShaderOverrides)
                    {
                        allShaderOverrides.Add(shader);
                    }
                }
            }

            // If no cached data, get from data manager
            if (allShaderOverrides.Count == 0)
            {
                var defaultOverrides = GetAvailableShaderOverrides("");
                foreach (var shader in defaultOverrides)
                {
                    allShaderOverrides.Add(shader);
                }
            }

            var globalShaderDropdown = new DropdownField();
            var shaderDisplayChoices = new List<string>();
            var displayNameToShaderPath = new Dictionary<string, string>();

            if (allShaderOverrides.Count > 0)
            {
                // Create display names for each shader
                foreach (var shaderPath in allShaderOverrides.OrderBy(s => s))
                {
                    var displayName = HoyoToonDataManager.GetFriendlyShaderName(shaderPath);
                    shaderDisplayChoices.Add(displayName);
                    displayNameToShaderPath[displayName] = shaderPath;
                }

                globalShaderDropdown.choices = shaderDisplayChoices;
                globalShaderDropdown.value = shaderDisplayChoices.FirstOrDefault();
            }
            else
            {
                globalShaderDropdown.choices = new List<string> { "No shaders available" };
                globalShaderDropdown.value = "No shaders available";
                globalShaderDropdown.SetEnabled(false);
            }

            globalShaderDropdown.style.width = 300;
            globalShaderDropdown.style.fontSize = 11;
            shaderContainer.Add(globalShaderDropdown);

            mainRow.Add(shaderContainer);

            // Apply button
            var applyButton = CreateHoyoToonStyledButton("Apply to All Materials", () =>
            {
                if (globalShaderDropdown.value != "No shaders available" &&
                    displayNameToShaderPath.TryGetValue(globalShaderDropdown.value, out var selectedShaderPath))
                {
                    ApplyGlobalShaderOverride(selectedShaderPath);
                }
            });
            applyButton.style.marginLeft = 15;
            applyButton.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 1.0f);
            mainRow.Add(applyButton);

            container.Add(mainRow);

            // Status row
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.justifyContent = Justify.SpaceBetween;
            statusRow.style.marginTop = 10;

            var warningLabel = new Label("⚠ This will override current materials");
            warningLabel.style.color = new Color(1.0f, 0.8f, 0.0f);
            warningLabel.style.fontSize = 10;
            statusRow.Add(warningLabel);

            container.Add(statusRow);

            contentView.Add(container);
        }

        private void CreateMaterialDetailsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Create controls section for filtering and search
            CreateMaterialControlsSection(container);

            // Create material gallery container
            materialGalleryContainer = new VisualElement();
            container.Add(materialGalleryContainer);

            // Create pagination controls
            CreateMaterialPaginationControls(container);

            // Initial population
            RefreshMaterialGallery();

            contentView.Add(container);
        }

        private void CreateMaterialControlsSection(VisualElement container)
        {
            var controlsContainer = new VisualElement();
            controlsContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            controlsContainer.style.borderTopLeftRadius = 5;
            controlsContainer.style.borderTopRightRadius = 5;
            controlsContainer.style.borderBottomLeftRadius = 5;
            controlsContainer.style.borderBottomRightRadius = 5;
            controlsContainer.style.paddingTop = 10;
            controlsContainer.style.paddingBottom = 10;
            controlsContainer.style.paddingLeft = 15;
            controlsContainer.style.paddingRight = 15;
            controlsContainer.style.marginBottom = 15;

            // First row: Search and filters
            var firstRow = new VisualElement();
            firstRow.style.flexDirection = FlexDirection.Row;
            firstRow.style.alignItems = Align.Center;
            firstRow.style.marginBottom = 10;

            // Search field
            var searchLabel = new Label("Search Materials:");
            searchLabel.style.marginRight = 10;
            searchLabel.style.fontSize = 12;
            firstRow.Add(searchLabel);

            var searchField = CreateHoyoToonTextField();
            searchField.style.width = 200;
            searchField.style.marginRight = 20;
            searchField.value = searchFilter;
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchFilter = evt.newValue;
                currentPage = 0; // Reset to first page
                RefreshMaterialGallery();
            });
            firstRow.Add(searchField);

            // Show only invalid toggle
            var invalidToggle = new Toggle("Show Only Invalid Materials");
            invalidToggle.style.marginRight = 20;
            invalidToggle.value = showOnlyInvalid;
            invalidToggle.RegisterValueChangedCallback(evt =>
            {
                showOnlyInvalid = evt.newValue;
                currentPage = 0; // Reset to first page
                RefreshMaterialGallery();
            });
            firstRow.Add(invalidToggle);

            controlsContainer.Add(firstRow);

            // Second row: Items per page
            var secondRow = new VisualElement();
            secondRow.style.flexDirection = FlexDirection.Row;
            secondRow.style.alignItems = Align.Center;

            var itemsLabel = new Label("Materials per page:");
            itemsLabel.style.marginRight = 10;
            itemsLabel.style.fontSize = 12;
            secondRow.Add(itemsLabel);

            var itemsDropdown = new DropdownField();
            itemsDropdown.choices = new List<string> { "6", "12", "24", "48" };
            itemsDropdown.value = materialsPerPage.ToString();
            itemsDropdown.style.width = 80;
            itemsDropdown.RegisterValueChangedCallback(evt =>
            {
                materialsPerPage = int.Parse(evt.newValue);
                currentPage = 0; // Reset to first page
                RefreshMaterialGallery();
            });
            secondRow.Add(itemsDropdown);

            controlsContainer.Add(secondRow);
            container.Add(controlsContainer);
        }

        private void CreateMaterialPaginationControls(VisualElement container)
        {
            var paginationContainer = new VisualElement();
            paginationContainer.style.flexDirection = FlexDirection.Row;
            paginationContainer.style.justifyContent = Justify.Center;
            paginationContainer.style.alignItems = Align.Center;
            paginationContainer.style.marginTop = 15;
            paginationContainer.style.marginBottom = 10;

            prevPageBtn = CreateHoyoToonStyledButton("Previous", () =>
            {
                if (currentPage > 0)
                {
                    currentPage--;
                    RefreshMaterialGallery();
                }
            }, new Color(0.4f, 0.4f, 0.4f));
            prevPageBtn.style.marginRight = 10;
            paginationContainer.Add(prevPageBtn);

            paginationLabel = new Label("Page 1 of 1");
            paginationLabel.style.alignSelf = Align.Center;
            paginationLabel.style.marginLeft = 10;
            paginationLabel.style.marginRight = 10;
            paginationContainer.Add(paginationLabel);

            nextPageBtn = CreateHoyoToonStyledButton("Next", () =>
            {
                var filteredMaterials = GetFilteredMaterials();
                var totalPages = Mathf.CeilToInt((float)filteredMaterials.Count / materialsPerPage);
                if (currentPage < totalPages - 1)
                {
                    currentPage++;
                    RefreshMaterialGallery();
                }
            }, new Color(0.4f, 0.4f, 0.4f));
            nextPageBtn.style.marginLeft = 10;
            paginationContainer.Add(nextPageBtn);

            container.Add(paginationContainer);
        }

        private List<HoyoToonMaterialInfo> GetFilteredMaterials()
        {
            var materials = analysisData?.materials ?? new List<HoyoToonMaterialInfo>();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                materials = materials.Where(m =>
                    m.name.IndexOf(searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            // Apply invalid filter
            if (showOnlyInvalid)
            {
                materials = materials.Where(m => !m.isValid).ToList();
            }

            return materials;
        }

        private void RefreshMaterialGallery()
        {
            if (materialGalleryContainer == null) return;

            materialGalleryContainer.Clear();

            var filteredMaterials = GetFilteredMaterials();
            var totalPages = Mathf.CeilToInt((float)filteredMaterials.Count / materialsPerPage);

            // Ensure current page is valid
            if (currentPage >= totalPages) currentPage = Math.Max(0, totalPages - 1);

            // Get materials for current page
            var startIndex = currentPage * materialsPerPage;
            var pageMaterials = filteredMaterials.Skip(startIndex).Take(materialsPerPage).ToList();

            // Create material grid
            CreateMaterialGrid(pageMaterials);

            // Update pagination controls
            if (paginationLabel != null)
            {
                paginationLabel.text = $"Page {currentPage + 1} of {Math.Max(totalPages, 1)}";
            }

            if (prevPageBtn != null)
            {
                prevPageBtn.SetEnabled(currentPage > 0);
            }

            if (nextPageBtn != null)
            {
                nextPageBtn.SetEnabled(currentPage < totalPages - 1);
            }
        }

        private void CreateMaterialGrid(List<HoyoToonMaterialInfo> materials)
        {
            const int itemsPerRow = 2; // Two columns for better material card display

            for (int i = 0; i < materials.Count; i += itemsPerRow)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 15;
                row.style.justifyContent = Justify.SpaceBetween; // Even distribution
                row.style.alignItems = Align.Stretch; // Same height for all cards

                for (int j = 0; j < itemsPerRow && i + j < materials.Count; j++)
                {
                    var material = materials[i + j];
                    var materialCard = CreateMaterialCard(material);
                    materialCard.style.flexGrow = 1; // Equal width
                    materialCard.style.flexBasis = Length.Percent(48); // 48% width for 2 columns with gap
                    materialCard.style.marginLeft = j > 0 ? 5 : 0; // Small gap between cards
                    materialCard.style.marginRight = j < itemsPerRow - 1 ? 5 : 0; // Small gap between cards
                    row.Add(materialCard);
                }

                // If only one card in the last row, add spacer to maintain layout
                if ((i + itemsPerRow) > materials.Count && materials.Count % itemsPerRow != 0)
                {
                    var spacer = new VisualElement();
                    spacer.style.flexGrow = 1;
                    spacer.style.flexBasis = Length.Percent(48);
                    row.Add(spacer);
                }

                materialGalleryContainer.Add(row);
            }
        }

        private void RefreshMaterialGrid()
        {
            // Clear current grid
            materialGalleryContainer.Clear();

            // Get filtered materials for current page
            var filteredMaterials = GetFilteredMaterials();
            var startIndex = currentPage * materialsPerPage;
            var endIndex = Math.Min(startIndex + materialsPerPage, filteredMaterials.Count);
            var pageMaterials = filteredMaterials.GetRange(startIndex, endIndex - startIndex);

            // Recreate the grid
            CreateMaterialGrid(pageMaterials);
        }

        private VisualElement CreateMaterialCard(HoyoToonMaterialInfo material)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.paddingTop = 12;
            card.style.paddingBottom = 12;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.minHeight = 480; // Increased height for interactive controls
            card.style.alignSelf = Align.Stretch; // Ensure cards fill container height
            card.style.justifyContent = Justify.SpaceBetween; // Distribute content evenly

            // Header with material name and status
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 10;

            var nameLabel = new Label(material.name);
            nameLabel.style.fontSize = 13;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = Color.white;
            nameLabel.style.flexGrow = 1;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            header.Add(nameLabel);

            var statusLabel = new Label(GetMaterialStatusText(material));
            statusLabel.style.fontSize = 10;
            statusLabel.style.color = GetMaterialStatusColor(material);
            statusLabel.style.backgroundColor = new Color(0, 0, 0, 0.3f);
            statusLabel.style.paddingLeft = 4;
            statusLabel.style.paddingRight = 4;
            statusLabel.style.paddingTop = 2;
            statusLabel.style.paddingBottom = 2;
            statusLabel.style.borderTopLeftRadius = 3;
            statusLabel.style.borderTopRightRadius = 3;
            statusLabel.style.borderBottomLeftRadius = 3;
            statusLabel.style.borderBottomRightRadius = 3;
            header.Add(statusLabel);

            card.Add(header);

            // Material preview section
            var previewSection = new VisualElement();
            previewSection.style.flexDirection = FlexDirection.Row;
            previewSection.style.marginBottom = 12;

            // Preview image
            var previewContainer = new VisualElement();
            previewContainer.style.width = 100;
            previewContainer.style.height = 100;
            previewContainer.style.marginRight = 12;
            previewContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            previewContainer.style.borderTopLeftRadius = 5;
            previewContainer.style.borderTopRightRadius = 5;
            previewContainer.style.borderBottomLeftRadius = 5;
            previewContainer.style.borderBottomRightRadius = 5;

            var materialAsset = AssetDatabase.LoadAssetAtPath<Material>(material.materialPath);
            if (materialAsset != null)
            {
                var preview = CreateMaterialPreview(materialAsset);
                if (preview != null)
                {
                    var previewImage = new VisualElement();
                    previewImage.style.backgroundImage = new StyleBackground(preview);
                    previewImage.style.width = 100;
                    previewImage.style.height = 100;
                    previewImage.style.borderTopLeftRadius = 5;
                    previewImage.style.borderTopRightRadius = 5;
                    previewImage.style.borderBottomLeftRadius = 5;
                    previewImage.style.borderBottomRightRadius = 5;
                    previewImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

                    // Add context menu for preview regeneration
                    previewImage.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
                    {
                        evt.menu.AppendAction("Refresh Preview", (action) =>
                        {
                            // Clear cache and regenerate
                            string materialPath = AssetDatabase.GetAssetPath(materialAsset);
                            if (materialPreviewTextures.ContainsKey(materialPath))
                            {
                                materialPreviewTextures.Remove(materialPath);
                            }

                            var newPreview = CreateMaterialPreview(materialAsset);
                            if (newPreview != null)
                            {
                                previewImage.style.backgroundImage = new StyleBackground(newPreview);
                            }
                        });
                    });

                    previewContainer.Add(previewImage);
                }
                else
                {
                    // Enhanced fallback with retry option
                    var placeholderContainer = new VisualElement();
                    placeholderContainer.style.flexGrow = 1;
                    placeholderContainer.style.alignItems = Align.Center;
                    placeholderContainer.style.justifyContent = Justify.Center;

                    var placeholder = new Label("Preview\nGenerating...");
                    placeholder.style.color = Color.gray;
                    placeholder.style.fontSize = 10;
                    placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
                    placeholder.style.whiteSpace = WhiteSpace.Normal;

                    var retryBtn = new Button(() =>
                    {
                        // Force regenerate preview
                        string materialPath = AssetDatabase.GetAssetPath(materialAsset);
                        if (materialPreviewTextures.ContainsKey(materialPath))
                        {
                            materialPreviewTextures.Remove(materialPath);
                        }

                        var newPreview = CreateMaterialPreview(materialAsset);
                        if (newPreview != null)
                        {
                            // Replace placeholder with actual preview
                            previewContainer.Clear();
                            var previewImage = new VisualElement();
                            previewImage.style.backgroundImage = new StyleBackground(newPreview);
                            previewImage.style.width = 100;
                            previewImage.style.height = 100;
                            previewImage.style.borderTopLeftRadius = 5;
                            previewImage.style.borderTopRightRadius = 5;
                            previewImage.style.borderBottomLeftRadius = 5;
                            previewImage.style.borderBottomRightRadius = 5;
                            previewImage.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                            previewContainer.Add(previewImage);
                        }
                    });
                    retryBtn.text = "Retry";
                    retryBtn.style.fontSize = 8;
                    retryBtn.style.height = 20;
                    retryBtn.style.marginTop = 2;

                    placeholderContainer.Add(placeholder);
                    placeholderContainer.Add(retryBtn);
                    previewContainer.Add(placeholderContainer);
                }
            }

            previewSection.Add(previewContainer);

            // Material info section - basic info only
            var infoSection = new VisualElement();
            infoSection.style.flexGrow = 1;

            // Texture count and memory info
            if (material.textureCount > 0)
            {
                var textureInfo = new Label($"Textures: {material.textureCount}");
                textureInfo.style.fontSize = 10;
                textureInfo.style.color = new Color(0.6f, 0.8f, 0.6f);
                textureInfo.style.marginBottom = 4;
                infoSection.Add(textureInfo);

                if (material.memoryUsage > 0)
                {
                    var memoryInfo = new Label($"Memory: {material.memoryUsageFormatted}");
                    memoryInfo.style.fontSize = 9;
                    memoryInfo.style.color = new Color(0.6f, 0.6f, 0.8f);
                    memoryInfo.style.marginBottom = 4;
                    infoSection.Add(memoryInfo);
                }
            }

            // Path information
            var pathInfo = new Label($"Path: {material.materialPath}");
            pathInfo.style.fontSize = 9;
            pathInfo.style.color = new Color(0.6f, 0.6f, 0.6f);
            pathInfo.style.whiteSpace = WhiteSpace.Normal;
            pathInfo.style.marginBottom = 0;
            infoSection.Add(pathInfo);

            previewSection.Add(infoSection);
            card.Add(previewSection);

            // Interactive Controls Section
            var controlsSection = new VisualElement();
            controlsSection.style.marginBottom = 12;

            // Shader Selection Dropdown (moved to top)
            var shaderContainer = new VisualElement();
            shaderContainer.style.marginBottom = 8;

            var shaderLabel = new Label("Shader:");
            shaderLabel.style.fontSize = 10;
            shaderLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            shaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            shaderLabel.style.marginBottom = 4;
            shaderContainer.Add(shaderLabel);

            // Shader selection dropdown
            if (materialAsset != null)
            {
                // Create shader dropdown with all available options
                var shaderDropdown = new DropdownField();
                var availableShaders = new List<string>();
                var shaderPathToDisplayName = new Dictionary<string, string>();
                var displayNameToShaderPath = new Dictionary<string, string>();

                var currentShaderName = material.currentShader;
                var currentFriendlyName = HoyoToonDataManager.GetFriendlyShaderName(currentShaderName);

                // Add current shader first
                availableShaders.Add($"Current: {currentFriendlyName}");
                shaderPathToDisplayName[currentShaderName] = $"Current: {currentFriendlyName}";
                displayNameToShaderPath[$"Current: {currentFriendlyName}"] = currentShaderName;

                // Add API suggested shader if different from current
                var detectedShader = GetShaderFromMaterialForDisplay(materialAsset);
                if (!string.IsNullOrEmpty(detectedShader) && detectedShader != currentShaderName)
                {
                    var suggestedFriendlyName = HoyoToonDataManager.GetFriendlyShaderName(detectedShader);
                    availableShaders.Add($"[Suggested] {suggestedFriendlyName}");
                    shaderPathToDisplayName[detectedShader] = $"[Suggested] {suggestedFriendlyName}";
                    displayNameToShaderPath[$"[Suggested] {suggestedFriendlyName}"] = detectedShader;
                }

                // Add other available shader overrides
                var availableShaderOverrides = GetAvailableShaderOverrides(currentShaderName);
                foreach (var shaderPath in availableShaderOverrides.Take(8)) // Show more options in dropdown
                {
                    if (shaderPath != currentShaderName && shaderPath != detectedShader)
                    {
                        var displayName = HoyoToonDataManager.GetFriendlyShaderName(shaderPath);
                        availableShaders.Add(displayName);
                        shaderPathToDisplayName[shaderPath] = displayName;
                        displayNameToShaderPath[displayName] = shaderPath;
                    }
                }

                shaderDropdown.choices = availableShaders;
                shaderDropdown.value = $"Current: {currentFriendlyName}"; // Default to current shader
                shaderDropdown.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                shaderDropdown.style.color = Color.white;
                shaderDropdown.style.fontSize = 10; // Match material type dropdown
                shaderDropdown.style.marginBottom = 8;

                shaderDropdown.RegisterValueChangedCallback(evt =>
                {
                    // Find the shader path from the display name
                    if (displayNameToShaderPath.TryGetValue(evt.newValue, out var selectedShaderPath))
                    {
                        var newShader = Shader.Find(selectedShaderPath);
                        if (newShader != null)
                        {
                            materialAsset.shader = newShader;
                            material.currentShader = selectedShaderPath;
                            EditorUtility.SetDirty(materialAsset);
                            HoyoToonLogs.LogDebug($"Applied shader {selectedShaderPath} to {material.name} via dropdown");

                            // Refresh the card to show the new shader
                            RefreshMaterialGrid();
                        }
                    }
                });

                shaderContainer.Add(shaderDropdown);
            }
            else
            {
                var currentShaderName = material.currentShader;
                var friendlyShaderName = HoyoToonDataManager.GetFriendlyShaderName(currentShaderName);
                var shaderValue = new Label($"  {friendlyShaderName}");
                shaderValue.style.fontSize = 10;
                shaderValue.style.color = new Color(0.7f, 0.7f, 0.7f);
                shaderContainer.Add(shaderValue);
            }

            controlsSection.Add(shaderContainer);

            // Material Type Selection (moved below shader)
            var typeContainer = new VisualElement();
            typeContainer.style.marginBottom = 8;

            var typeLabel = new Label("Material Type:");
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            typeLabel.style.marginBottom = 4;
            typeContainer.Add(typeLabel);

            var availableMaterialTypes = HoyoToonDataManager.GetMaterialTypesForShader(material.currentShader);
            if (availableMaterialTypes.Count > 0)
            {
                var typeDropdown = new DropdownField();
                typeDropdown.choices = availableMaterialTypes;
                typeDropdown.value = material.materialType ?? availableMaterialTypes[0];
                typeDropdown.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
                typeDropdown.style.borderLeftColor = new Color(0.4f, 0.4f, 0.4f);
                typeDropdown.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
                typeDropdown.style.borderTopColor = new Color(0.4f, 0.4f, 0.4f);
                typeDropdown.style.borderBottomColor = new Color(0.4f, 0.4f, 0.4f);
                typeDropdown.style.borderTopLeftRadius = 3;
                typeDropdown.style.borderTopRightRadius = 3;
                typeDropdown.style.borderBottomLeftRadius = 3;
                typeDropdown.style.borderBottomRightRadius = 3;
                typeDropdown.style.fontSize = 10;

                typeDropdown.RegisterValueChangedCallback(evt =>
                {
                    material.materialType = evt.newValue;

                    // Update material property if available
                    if (materialAsset != null)
                    {
                        int typeValue = GetMaterialTypeEnumValue(evt.newValue, materialAsset);

                        // Try different property names based on shader
                        bool propertySet = false;

                        // First try variant_selector (used by Genshin, Star Rail, Honkai Impact)
                        if (materialAsset.HasProperty("variant_selector"))
                        {
                            materialAsset.SetFloat("variant_selector", typeValue);
                            propertySet = true;
                            HoyoToonLogs.LogDebug($"Updated variant_selector to {typeValue} ({evt.newValue}) for {material.name}");
                        }
                        // Then try _MaterialType (used by Zenless Zone Zero, Wuthering Waves)
                        else if (materialAsset.HasProperty("_MaterialType"))
                        {
                            materialAsset.SetFloat("_MaterialType", typeValue);
                            propertySet = true;
                            HoyoToonLogs.LogDebug($"Updated _MaterialType to {typeValue} ({evt.newValue}) for {material.name}");
                        }

                        if (propertySet)
                        {
                            EditorUtility.SetDirty(materialAsset);
                            HoyoToonLogs.LogDebug($"Material {material.name} type successfully updated to {evt.newValue} (enum value: {typeValue})");
                        }
                        else
                        {
                            HoyoToonLogs.WarningDebug($"Could not find material type property (variant_selector or _MaterialType) on material {material.name}");
                        }
                    }
                });

                typeContainer.Add(typeDropdown);
            }
            else
            {
                var typeValue = new Label($"  {material.materialType ?? "Unknown"}");
                typeValue.style.fontSize = 10;
                typeValue.style.color = new Color(0.7f, 0.7f, 0.7f);
                typeContainer.Add(typeValue);
            }

            controlsSection.Add(typeContainer);
            card.Add(controlsSection);

            // Issues section
            if (!material.isValid && !string.IsNullOrEmpty(material.invalidReason))
            {
                var issueLabel = new Label($"[Warning] {material.invalidReason}");
                issueLabel.style.fontSize = 10;
                issueLabel.style.color = new Color(1f, 0.8f, 0.4f);
                issueLabel.style.backgroundColor = new Color(0.3f, 0.2f, 0.1f, 0.8f);
                issueLabel.style.paddingLeft = 8;
                issueLabel.style.paddingRight = 8;
                issueLabel.style.paddingTop = 4;
                issueLabel.style.paddingBottom = 4;
                issueLabel.style.borderTopLeftRadius = 4;
                issueLabel.style.borderTopRightRadius = 4;
                issueLabel.style.borderBottomLeftRadius = 4;
                issueLabel.style.borderBottomRightRadius = 4;
                issueLabel.style.marginBottom = 10;
                issueLabel.style.whiteSpace = WhiteSpace.Normal;
                card.Add(issueLabel);
            }

            // View button section - Centered above other buttons
            var viewButtonContainer = new VisualElement();
            viewButtonContainer.style.flexDirection = FlexDirection.Row;
            viewButtonContainer.style.justifyContent = Justify.Center;
            viewButtonContainer.style.marginBottom = 40;

            var viewBtn = CreateHoyoToonStyledButton("View Material", () =>
            {
                ViewMaterialInInspector(material);
            }, new Color(0.2f, 0.4f, 0.6f));
            viewBtn.style.fontSize = 10;
            viewBtn.style.width = 120;
            viewBtn.style.height = 28;
            viewButtonContainer.Add(viewBtn);

            card.Add(viewButtonContainer);

            // Action buttons section - Only Select and Export buttons
            var actionsContainer = new VisualElement();
            actionsContainer.style.flexDirection = FlexDirection.Row;
            actionsContainer.style.justifyContent = Justify.SpaceBetween;
            actionsContainer.style.alignItems = Align.Stretch; // Ensure buttons are same height

            var selectBtn = CreateHoyoToonStyledButton("Select Material", () =>
            {
                SelectMaterial(material);
            }, new Color(0.3f, 0.5f, 0.3f));
            selectBtn.style.fontSize = 10;
            selectBtn.style.flexGrow = 1;
            selectBtn.style.marginRight = 5;
            selectBtn.style.height = 28; // Fixed height for consistency
            actionsContainer.Add(selectBtn);

            var exportBtn = CreateHoyoToonStyledButton("Export JSON", () =>
            {
                ExportMaterialToJson(material);
            }, new Color(0.5f, 0.5f, 0.3f));
            exportBtn.style.fontSize = 10;
            exportBtn.style.flexGrow = 1;
            exportBtn.style.height = 28; // Fixed height for consistency
            actionsContainer.Add(exportBtn);

            card.Add(actionsContainer);

            return card;
        }

        private string GetMaterialStatusText(HoyoToonMaterialInfo material)
        {
            if (!material.isValid) return "Invalid";
            return "Valid";
        }

        private Color GetMaterialStatusColor(HoyoToonMaterialInfo material)
        {
            if (!material.isValid) return new Color(1f, 0.4f, 0.4f); // Red
            return new Color(0.4f, 1f, 0.4f); // Green
        }

        public int GetMaterialTypeEnumValue(string materialType)
        {
            return GetMaterialTypeEnumValue(materialType, null);
        }

        public int GetMaterialTypeEnumValue(string materialType, Material material)
        {
            // Get the shader key to determine which enum mapping to use
            string shaderKey = "Global"; // Default fallback

            if (material != null && material.shader != null)
            {
                shaderKey = HoyoToonDataManager.GetShaderKey(material.shader);
            }

            // Map material type strings to shader-specific enum values
            return GetShaderSpecificEnumValue(shaderKey, materialType);
        }

        private int GetShaderSpecificEnumValue(string shaderKey, string materialType)
        {
            switch (shaderKey)
            {
                case "GIShader":
                    // Genshin Impact: Base=0, Face=1, Weapon=2, Glass=3, Bangs=4
                    switch (materialType?.ToLower())
                    {
                        case "base": return 0;
                        case "face": return 1;
                        case "weapon": return 2;
                        case "glass": return 3;
                        case "bangs": return 4;
                        default: return 0;
                    }

                case "HSRShader":
                    // Star Rail: Base=0, Face=1, EyeShadow=2, Hair=3, Bang=4
                    switch (materialType?.ToLower())
                    {
                        case "base": return 0;
                        case "face": return 1;
                        case "eyeshadow": return 2;
                        case "hair": return 3;
                        case "bang": return 4;
                        default: return 0;
                    }

                case "HI3Shader":
                    // Honkai Impact: Base=0, Face=1, Hair=2, Eye=3, Mouth=4, EyeL=5, EyeR=6
                    switch (materialType?.ToLower())
                    {
                        case "base": return 0;
                        case "face": return 1;
                        case "hair": return 2;
                        case "eye": return 3;
                        case "mouth": return 4;
                        case "eyel": return 5;
                        case "eyer": return 6;
                        default: return 0;
                    }

                case "HI3P2Shader":
                    // Honkai Impact Part 2: Base=0, Face=1, Hair=2, Eye=3
                    switch (materialType?.ToLower())
                    {
                        case "base": return 0;
                        case "face": return 1;
                        case "hair": return 2;
                        case "eye": return 3;
                        default: return 0;
                    }

                case "WuWaShader":
                    // Wuthering Waves: Base=0, Face=1, Eye=2, Bangs=3, Hair=4, Glass=5, Tacet Mark=6
                    switch (materialType?.ToLower())
                    {
                        case "base": return 0;
                        case "face": return 1;
                        case "eye": return 2;
                        case "bangs": return 3;
                        case "hair": return 4;
                        case "glass": return 5;
                        case "tacet mark": return 6;
                        default: return 0;
                    }

                case "ZZZShader":
                    // Zenless Zone Zero: Base=0, Face=1, Eye=2, Shadow=3, Hair=4, EyeHighlight=5, EyeShadow=6
                    switch (materialType?.ToLower())
                    {
                        case "base": return 0;
                        case "face": return 1;
                        case "eye": return 2;
                        case "shadow": return 3;
                        case "hair": return 4;
                        case "eyehighlight": return 5;
                        case "eyeshadow": return 6;
                        default: return 0;
                    }

                default:
                    // Generic fallback mapping
                    switch (materialType?.ToLower())
                    {
                        case "base": return 0;
                        case "face": return 1;
                        case "hair": return 2;
                        case "eye": case "eyes": return 3;
                        case "body": return 4;
                        case "clothing": return 5;
                        case "weapon": return 6;
                        default: return 0;
                    }
            }
        }

        private void CreateShaderRecommendationsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Get shader recommendations based on detected game
            var gameType = analysisData.potentialGameType?.ToString() ?? "Auto";
            var shaderRecommendations = GetShaderRecommendations(gameType);

            container.Add(CreateHoyoToonInfoRow("Detected Game:", gameType));

            if (shaderRecommendations.Count > 0)
            {
                container.Add(CreateHoyoToonSubsectionHeader("Recommended Shaders:"));

                foreach (var rec in shaderRecommendations)
                {
                    var recContainer = new VisualElement();
                    recContainer.style.marginLeft = 10;
                    recContainer.style.marginBottom = 5;
                    recContainer.style.paddingTop = 5;
                    recContainer.style.paddingBottom = 5;
                    recContainer.style.paddingLeft = 10;
                    recContainer.style.paddingRight = 10;
                    recContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.6f);
                    recContainer.style.borderTopLeftRadius = 4;
                    recContainer.style.borderTopRightRadius = 4;
                    recContainer.style.borderBottomLeftRadius = 4;
                    recContainer.style.borderBottomRightRadius = 4;

                    var titleLabel = new Label($"{rec.Key}:");
                    titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    titleLabel.style.color = new Color(0.8f, 0.9f, 1f);

                    var shaderLabel = new Label(rec.Value);
                    shaderLabel.style.color = new Color(0.7f, 0.8f, 0.9f);
                    shaderLabel.style.marginLeft = 10;

                    recContainer.Add(titleLabel);
                    recContainer.Add(shaderLabel);
                    container.Add(recContainer);
                }
            }
            else
            {
                container.Add(CreateHoyoToonWarningBox("No shader recommendations available for detected game type"));
            }

            contentView.Add(container);
        }

        private void CreateMaterialActionsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.flexWrap = Wrap.Wrap;
            buttonsContainer.style.justifyContent = Justify.SpaceAround;

            // Generate materials button
            var generateBtn = CreateHoyoToonStyledButton("Generate Materials", () =>
            {
                try
                {
                    Selection.activeObject = selectedModel;
                    HoyoToonMaterialManager.GenerateMaterialsFromJson();
                    EditorUtility.DisplayDialog("Success", "Materials generated successfully!", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Material generation failed: {e.Message}", "OK");
                }
            }, new Color(0.2f, 0.6f, 0.8f));

            // Clear all materials button
            var clearBtn = CreateHoyoToonStyledButton("Clear Materials", () =>
            {
                if (EditorUtility.DisplayDialog("Confirm",
                    "This will delete all generated .mat files from the Materials folder. Are you sure?", "Yes", "Cancel"))
                {
                    HoyoToonMaterialManager.ClearGeneratedMaterials(selectedModel);
                }
            }, new Color(0.7f, 0.3f, 0.3f));

            buttonsContainer.Add(generateBtn);
            buttonsContainer.Add(clearBtn);

            container.Add(buttonsContainer);

            // Material statistics
            var statsContainer = new VisualElement();
            statsContainer.style.marginTop = 15;
            statsContainer.style.paddingTop = 10;
            statsContainer.style.paddingBottom = 10;
            statsContainer.style.paddingLeft = 15;
            statsContainer.style.paddingRight = 15;
            statsContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            statsContainer.style.borderTopLeftRadius = 8;
            statsContainer.style.borderTopRightRadius = 8;
            statsContainer.style.borderBottomLeftRadius = 8;
            statsContainer.style.borderBottomRightRadius = 8;

            var statsTitle = new Label("Material Statistics");
            statsTitle.style.fontSize = 14;
            statsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            statsTitle.style.color = new Color(0.8f, 0.9f, 1f);
            statsTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            statsTitle.style.marginBottom = 8;

            var validCount = analysisData.materials.Count(m => m.isValid);
            var hoyoToonCount = analysisData.materials.Count(m => HoyoToonDataManager.IsHoyoToonShader(m.currentShader));

            var statsInfo = new Label($"Valid: {validCount}/{analysisData.materialCount} | " +
                                    $"HoyoToon: {hoyoToonCount}/{analysisData.materialCount}");
            statsInfo.style.fontSize = 12;
            statsInfo.style.color = new Color(0.8f, 0.8f, 0.8f);
            statsInfo.style.unityTextAlign = TextAnchor.MiddleCenter;

            statsContainer.Add(statsTitle);
            statsContainer.Add(statsInfo);
            container.Add(statsContainer);

            contentView.Add(container);
        }

        #region JSON Detection and Management

        private void DetectAvailableJsonFiles()
        {
            availableJsonFiles.Clear();
            jsonFileInfoCache.Clear();

            if (selectedModel == null)
            {
                HoyoToonLogs.LogDebug("No model selected for JSON detection");
                return;
            }

            try
            {
                HoyoToonLogs.LogDebug($"Scanning for JSON files for model: {selectedModel.name}");

                // Use centralized asset service instead of direct AssetDatabase calls
                var jsonGuids = HoyoToonAssetService.FindJsonFilesForModel(selectedModel);
                var allJsonFiles = jsonGuids
                    .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                    .Where(path => HoyoToonAssetService.IsValidMaterialJson(path))
                    .ToList();

                foreach (var jsonPath in allJsonFiles)
                {
                    availableJsonFiles.Add(jsonPath);

                    // Analyze JSON content for API validation and shader info
                    var jsonInfo = AnalyzeJsonFile(jsonPath);
                    jsonFileInfoCache[jsonPath] = jsonInfo;
                }

                HoyoToonLogs.LogDebug($"Auto-detected {availableJsonFiles.Count} material JSON files for {selectedModel.name}");

                // If we found JSON files, log them for debugging
                if (availableJsonFiles.Count > 0)
                {
                    HoyoToonLogs.LogDebug($"Found JSON files: {string.Join(", ", availableJsonFiles.Select(Path.GetFileName))}");
                }
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.ErrorDebug($"Error during automatic JSON detection: {e.Message}");
            }
        }
        private JsonFileInfo AnalyzeJsonFile(string jsonPath)
        {
            var info = new JsonFileInfo
            {
                filePath = jsonPath,
                fileName = Path.GetFileNameWithoutExtension(jsonPath),
                availableShaderOverrides = new List<string>(),
                expectedTextures = new List<string>(),
                missingRequiredTextures = new List<string>()
            };

            try
            {
                // Check for outdated JSON structure using centralized validation first
                string validationError;
                bool isValidStructure = HoyoToonAssetService.ValidateJsonStructure(jsonPath, out validationError);
                if (!isValidStructure)
                {
                    info.isValidMaterialJson = false;
                    info.errorMessage = validationError;
                    info.apiSuggestedShader = "Unknown";
                    return info;
                }

                // Use MaterialManager for JSON validation instead of direct file operations
                info.isValidMaterialJson = HoyoToonAssetService.IsValidMaterialJson(jsonPath);

                if (info.isValidMaterialJson)
                {
                    var jsonText = File.ReadAllText(jsonPath);
                    var materialData = JsonConvert.DeserializeObject<MaterialJsonStructure>(jsonText);

                    // Use MaterialManager for shader detection
                    info.apiSuggestedShader = HoyoToonAssetService.DetectShaderFromMaterialData(materialData);

                    // Determine material type from shader name or material data
                    info.materialType = DetermineMaterialType(jsonPath, info.apiSuggestedShader);

                    // Get available shader overrides for the detected game type
                    info.availableShaderOverrides = GetAvailableShaderOverrides(info.apiSuggestedShader);

                    // Get expected textures for this shader type (for info only, not warnings)
                    info.expectedTextures = GetExpectedTexturesForShader(info.apiSuggestedShader);

                    // Set current override to API suggested as default
                    info.currentShaderOverride = info.apiSuggestedShader;

                    HoyoToonLogs.LogDebug($"Analyzed JSON {info.fileName}: API Shader='{info.apiSuggestedShader}', Type='{info.materialType}', Valid={info.isValidMaterialJson}");
                }
                else
                {
                    info.apiSuggestedShader = "Unknown";
                    info.errorMessage = "Invalid material JSON structure";
                }

                // Don't check for missing textures in JSON files - only for material files
                // JSON files are templates and may intentionally have null textures
                info.missingRequiredTextures = new List<string>();
            }
            catch (System.Exception e)
            {
                info.isValidMaterialJson = false;
                info.errorMessage = $"Failed to parse JSON: {e.Message}";
                HoyoToonLogs.WarningDebug($"Failed to analyze JSON {jsonPath}: {e.Message}");
            }

            return info;
        }

        private string DetermineMaterialType(string jsonPath, string shaderName)
        {
            // Try to determine from filename first
            string fileName = Path.GetFileNameWithoutExtension(jsonPath).ToLower();

            if (fileName.Contains("face") || fileName.Contains("head"))
                return "Face";
            if (fileName.Contains("hair"))
                return "Hair";
            if (fileName.Contains("body"))
                return "Body";
            if (fileName.Contains("cloth") || fileName.Contains("dress"))
                return "Clothing";
            if (fileName.Contains("weapon"))
                return "Weapon";
            if (fileName.Contains("eye"))
                return "Eyes";

            // If filename detection fails, return Default which will be replaced with available types
            return "Default";
        }

        public List<string> GetAvailableShaderOverrides(string apiShader)
        {
            var overrides = new List<string>();
            var data = HoyoToonDataManager.Data;

            if (data?.Shaders == null) return overrides;

            // Add ALL available shaders as overrides, not just ones for the same game type
            foreach (var shaderGroup in data.Shaders)
            {
                overrides.AddRange(shaderGroup.Value);
            }

            return overrides.Distinct().ToList();
        }

        private List<string> GetExpectedTexturesForShader(string shaderName)
        {
            var expectedTextures = new List<string>();

            if (string.IsNullOrEmpty(shaderName))
                return expectedTextures;

            // Define expected textures based on shader type
            // This would ideally come from a data file or configuration
            if (shaderName.Contains("Genshin") || shaderName.Contains("GI"))
            {
                expectedTextures.AddRange(new[] { "_MainTex", "_LightMap", "_ShadowRamp" });
            }
            else if (shaderName.Contains("Honkai") || shaderName.Contains("Hi3"))
            {
                expectedTextures.AddRange(new[] { "_MainTex", "_LightMap", "_ShadowRamp" });
            }
            else if (shaderName.Contains("StarRail") || shaderName.Contains("HSR"))
            {
                expectedTextures.AddRange(new[] { "_MainTex", "_LightMap", "_ShadowRamp" });
            }
            else if (shaderName.Contains("Wuthering") || shaderName.Contains("WuWa"))
            {
                expectedTextures.AddRange(new[] { "_MainTex", "_NormalMap", "_MaskMap" });
            }
            else if (shaderName.Contains("Zenless") || shaderName.Contains("ZZZ"))
            {
                expectedTextures.AddRange(new[] { "_MainTex", "_LightMap", "_ShadowRamp" });
            }

            return expectedTextures;
        }

        private List<string> GetMissingRequiredTextures(MaterialJsonStructure materialData, List<string> expectedTextures)
        {
            var missingTextures = new List<string>();

            if (expectedTextures.Count == 0)
                return missingTextures;

            if (materialData.IsUnityFormat && materialData.m_SavedProperties?.m_TexEnvs != null)
            {
                foreach (var expectedTexture in expectedTextures)
                {
                    if (!materialData.m_SavedProperties.m_TexEnvs.ContainsKey(expectedTexture) ||
                        materialData.m_SavedProperties.m_TexEnvs[expectedTexture]?.m_Texture?.IsNull != false)
                    {
                        missingTextures.Add(expectedTexture);
                    }
                }
            }
            else if (materialData.IsUnrealFormat && materialData.Textures != null)
            {
                foreach (var expectedTexture in expectedTextures)
                {
                    if (!materialData.Textures.ContainsKey(expectedTexture) ||
                        string.IsNullOrEmpty(materialData.Textures[expectedTexture]))
                    {
                        missingTextures.Add(expectedTexture);
                    }
                }
            }

            return missingTextures;
        }

        #endregion

        #region Material Preview Generation

        private Texture2D CreateMaterialPreview(Material material)
        {
            if (material == null)
                return null;

            // Try to get from cache first
            string materialPath = AssetDatabase.GetAssetPath(material);
            if (materialPreviewTextures.TryGetValue(materialPath, out Texture2D cachedPreview))
            {
                return cachedPreview;
            }

            HoyoToonLogs.LogDebug($"Generating preview for material: {material.name} at path: {materialPath}");

            Texture2D preview = null;

            try
            {
                // First try Unity's built-in AssetPreview
                preview = AssetPreview.GetAssetPreview(material);

                // If that fails, try to force regeneration and wait a bit
                if (preview == null)
                {
                    AssetPreview.GetAssetPreview(material);
                    EditorUtility.DisplayProgressBar("Generating Preview", $"Creating preview for {material.name}", 0.5f);

                    // Give Unity time to generate the preview
                    System.Threading.Thread.Sleep(100);

                    preview = AssetPreview.GetAssetPreview(material);
                    EditorUtility.ClearProgressBar();
                }

                // If AssetPreview still fails, try using Editor.CreatePreviewTexture
                if (preview == null)
                {
                    try
                    {
                        var materialEditor = Editor.CreateEditor(material) as MaterialEditor;
                        if (materialEditor != null)
                        {
                            try
                            {
                                // Use Unity's material editor preview
                                preview = materialEditor.RenderStaticPreview(materialPath, null, 128, 128);
                                if (preview != null)
                                {
                                    HoyoToonLogs.LogDebug($"Generated MaterialEditor preview for {material.name}");
                                }
                            }
                            catch (System.Exception e)
                            {
                                HoyoToonLogs.LogDebug($"MaterialEditor preview failed for {material.name}: {e.Message}");
                            }
                            finally
                            {
                                UnityEngine.Object.DestroyImmediate(materialEditor);
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        HoyoToonLogs.LogDebug($"MaterialEditor creation failed for {material.name}: {e.Message}");
                    }
                }

                // If MaterialEditor fails, try one more AssetPreview attempt
                if (preview == null)
                {
                    // Sometimes a second attempt works after the first one initializes the system
                    System.Threading.Thread.Sleep(50);
                    preview = AssetPreview.GetAssetPreview(material);
                    if (preview != null)
                    {
                        HoyoToonLogs.LogDebug($"Generated AssetPreview on retry for {material.name}");
                    }
                }
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.LogDebug($"Preview generation failed for {material.name}: {e.Message}");
            }

            // If all else fails, create a fallback preview
            if (preview == null)
            {
                preview = CreateFallbackMaterialPreview(material);
                if (preview != null)
                {
                    HoyoToonLogs.LogDebug($"Generated fallback sphere preview for {material.name}");
                }
            }

            // Cache the preview
            if (preview != null)
            {
                materialPreviewTextures[materialPath] = preview;
            }

            return preview;
        }

        private Texture2D CreateFallbackMaterialPreview(Material material)
        {
            var preview = new Texture2D(128, 128, TextureFormat.RGB24, false);
            Color previewColor = Color.gray;

            // Try to get main texture
            var mainTex = material.GetTexture("_MainTex") ?? material.GetTexture("_BaseMap") ?? material.GetTexture("_DiffuseMap");
            if (mainTex != null && mainTex is Texture2D tex)
            {
                // Sample the center pixel of the texture
                try
                {
                    var renderTex = RenderTexture.GetTemporary(tex.width, tex.height);
                    Graphics.Blit(tex, renderTex);

                    var readableTex = new Texture2D(tex.width, tex.height);
                    RenderTexture.active = renderTex;
                    readableTex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    readableTex.Apply();

                    previewColor = readableTex.GetPixel(tex.width / 2, tex.height / 2);

                    UnityEngine.Object.DestroyImmediate(readableTex);
                    RenderTexture.ReleaseTemporary(renderTex);
                    RenderTexture.active = null;
                }
                catch
                {
                    // Fallback to material color
                    if (material.HasProperty("_Color"))
                        previewColor = material.GetColor("_Color");
                    else if (material.HasProperty("_BaseColor"))
                        previewColor = material.GetColor("_BaseColor");
                }
            }
            else
            {
                // Try to get color property
                if (material.HasProperty("_Color"))
                    previewColor = material.GetColor("_Color");
                else if (material.HasProperty("_BaseColor"))
                    previewColor = material.GetColor("_BaseColor");
            }

            // Create a sphere-like gradient instead of flat color
            var colors = new Color[128 * 128];
            Vector2 center = new Vector2(64f, 64f);
            float radius = 50f;

            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distanceFromCenter = Vector2.Distance(pos, center);

                    if (distanceFromCenter <= radius)
                    {
                        // Create sphere lighting effect
                        float normalizedDistance = distanceFromCenter / radius;
                        float lighting = Mathf.Cos(normalizedDistance * Mathf.PI * 0.5f);
                        lighting = Mathf.Pow(lighting, 0.8f); // Adjust curve for better sphere appearance

                        Color sphereColor = previewColor * lighting;
                        sphereColor.a = 1f;
                        colors[y * 128 + x] = sphereColor;
                    }
                    else
                    {
                        // Background
                        colors[y * 128 + x] = new Color(0.1f, 0.1f, 0.1f, 1f);
                    }
                }
            }

            preview.SetPixels(colors);
            preview.Apply();

            return preview;
        }

        private VisualElement CreateHoyoToonInfoBox(string message)
        {
            var infoBox = new VisualElement();
            infoBox.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 0.3f);
            infoBox.style.borderLeftColor = new Color(0.4f, 0.6f, 0.8f);
            infoBox.style.borderLeftWidth = 3;
            infoBox.style.paddingTop = 6;
            infoBox.style.paddingBottom = 6;
            infoBox.style.paddingLeft = 10;
            infoBox.style.paddingRight = 10;
            infoBox.style.marginTop = 4;
            infoBox.style.marginBottom = 4;
            infoBox.style.borderTopLeftRadius = 4;
            infoBox.style.borderTopRightRadius = 4;
            infoBox.style.borderBottomLeftRadius = 4;
            infoBox.style.borderBottomRightRadius = 4;

            var label = new Label(message);
            label.style.color = new Color(0.8f, 0.9f, 1f);
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;

            infoBox.Add(label);
            return infoBox;
        }

        /// <summary>
        /// Clear all cached material previews and force regeneration
        /// </summary>
        public void ClearPreviewCache()
        {
            materialPreviewTextures.Clear();
            HoyoToonLogs.LogDebug("Material preview cache cleared");

            // Trigger UI refresh to regenerate previews
            EditorApplication.delayCall += () => RefreshTabContent();
        }

        #endregion

        #region Material Operations

        private void GenerateAllMaterials()
        {
            if (availableJsonFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No JSON files found in the model directory. Make sure your model has been properly converted with Hoyo2VRC.", "OK");
                return;
            }

            try
            {
                if (selectedModel == null)
                {
                    EditorUtility.DisplayDialog("Error", "No model selected. Please select an FBX model first.", "OK");
                    return;
                }

                // Set selection to the model so material manager can find the JSON files
                var previousSelection = Selection.objects;
                Selection.activeObject = selectedModel;

                // First generate materials from JSON files
                HoyoToonMaterialManager.GenerateMaterialsFromJson();

                // Invalidate analysis cache since materials changed
                HoyoToonUIManager.RequestAnalysisCacheInvalidation();

                // Then setup FBX which will automatically find and remap the generated materials
                HoyoToonManager.SetupFBX();

                // Restore previous selection
                Selection.objects = previousSelection;

                // Clear preview cache to force refresh
                materialPreviewTextures.Clear();

                // Trigger immediate re-analysis and UI refresh
                HoyoToonUIManager.Instance?.ForceAnalysisRefresh();

                EditorUtility.DisplayDialog("Success", "Materials generated and FBX setup completed successfully!", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Material generation failed: {e.Message}", "OK");
            }
        }

        private void GenerateMaterialFromJsonWithOverride(string jsonPath, JsonFileInfo jsonInfo)
        {
            var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (jsonAsset == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not load JSON file: {jsonPath}", "OK");
                return;
            }

            try
            {
                if (selectedModel == null)
                {
                    EditorUtility.DisplayDialog("Error", "No model selected. Please select an FBX model first.", "OK");
                    return;
                }

                var previousSelection = Selection.objects;

                // Set selection to the model first for context
                Selection.activeObject = selectedModel;

                // Then select the specific JSON file for generation
                Selection.objects = new UnityEngine.Object[] { jsonAsset };

                // Generate material from JSON (this will use the API shader by default)
                HoyoToonMaterialManager.GenerateMaterialsFromJson();

                // If user specified a shader override, apply it after generation
                if (!string.IsNullOrEmpty(jsonInfo.currentShaderOverride) &&
                    jsonInfo.currentShaderOverride != jsonInfo.apiSuggestedShader)
                {
                    var materialPath = Path.ChangeExtension(jsonPath, ".mat");
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material != null)
                    {
                        var overrideShader = Shader.Find(jsonInfo.currentShaderOverride);
                        if (overrideShader != null)
                        {
                            material.shader = overrideShader;
                            EditorUtility.SetDirty(material);
                            HoyoToonLogs.LogDebug($"Applied shader override '{jsonInfo.currentShaderOverride}' to material '{material.name}'");
                        }
                    }
                }

                // Apply to FBX to ensure materials are mapped
                Selection.activeObject = selectedModel;
                HoyoToonManager.SetupFBX();

                // Restore previous selection
                Selection.objects = previousSelection;

                // Clear cache for this material
                var materialPath2 = Path.ChangeExtension(jsonPath, ".mat");
                materialPreviewTextures.Remove(materialPath2);

                // Trigger immediate re-analysis and UI refresh
                HoyoToonUIManager.Instance?.ForceAnalysisRefresh();

                var shaderUsed = !string.IsNullOrEmpty(jsonInfo.currentShaderOverride) &&
                               jsonInfo.currentShaderOverride != jsonInfo.apiSuggestedShader
                    ? $" with override shader {Path.GetFileName(jsonInfo.currentShaderOverride)}"
                    : " with API suggested shader";

                EditorUtility.DisplayDialog("Success",
                    $"Generated material for {Path.GetFileNameWithoutExtension(jsonPath)}{shaderUsed} and applied to FBX", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to generate material: {e.Message}", "OK");
            }
        }

        private void GenerateMaterialFromJson(string jsonPath)
        {
            var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (jsonAsset == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not load JSON file: {jsonPath}", "OK");
                return;
            }

            try
            {
                if (selectedModel == null)
                {
                    EditorUtility.DisplayDialog("Error", "No model selected. Please select an FBX model first.", "OK");
                    return;
                }

                var previousSelection = Selection.objects;

                // Set selection to the model first for context
                Selection.activeObject = selectedModel;

                // Then select the specific JSON file for generation
                Selection.objects = new UnityEngine.Object[] { jsonAsset };

                // Generate material from JSON
                HoyoToonMaterialManager.GenerateMaterialsFromJson();

                // Apply to FBX to ensure materials are mapped
                Selection.activeObject = selectedModel;
                HoyoToonManager.SetupFBX();

                // Restore previous selection
                Selection.objects = previousSelection;

                // Clear cache for this material
                var materialPath = Path.ChangeExtension(jsonPath, ".mat");
                materialPreviewTextures.Remove(materialPath);

                // Trigger immediate re-analysis and UI refresh
                HoyoToonUIManager.Instance?.ForceAnalysisRefresh();

                EditorUtility.DisplayDialog("Success", $"Generated material for {Path.GetFileNameWithoutExtension(jsonPath)} and applied to FBX", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to generate material: {e.Message}", "OK");
            }
        }

        private void ExportMaterialsToJson()
        {
            // Export all materials in the current model to JSON
            var materials = analysisData.materials
                .Where(m => m.isValid && !string.IsNullOrEmpty(m.materialPath))
                .Select(m => AssetDatabase.LoadAssetAtPath<Material>(m.materialPath))
                .Where(mat => mat != null)
                .ToArray();

            if (materials.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No valid materials found to export.", "OK");
                return;
            }

            var previousSelection = Selection.objects;
            Selection.objects = materials;

            try
            {
                HoyoToonMaterialManager.GenerateJsonsFromMaterials();

                // Refresh JSON detection
                DetectAvailableJsonFiles();

                // Trigger analysis refresh for consistency
                HoyoToonUIManager.Instance?.ForceAnalysisRefresh();

                EditorUtility.DisplayDialog("Success", $"Exported {materials.Length} materials to JSON", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to export materials: {e.Message}", "OK");
            }
            finally
            {
                Selection.objects = previousSelection;
            }
        }

        public void ExportMaterialToJson(HoyoToonMaterialInfo materialInfo)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialInfo.materialPath);
            if (material == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not load material: {materialInfo.materialPath}", "OK");
                return;
            }

            var previousSelection = Selection.objects;
            Selection.objects = new UnityEngine.Object[] { material };

            try
            {
                HoyoToonMaterialManager.GenerateJsonsFromMaterials();

                // Refresh JSON detection
                DetectAvailableJsonFiles();

                // Trigger analysis refresh for consistency
                HoyoToonUIManager.Instance?.ForceAnalysisRefresh();

                EditorUtility.DisplayDialog("Success", $"Exported {materialInfo.name} to JSON", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to export material: {e.Message}", "OK");
            }
            finally
            {
                Selection.objects = previousSelection;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get shader suggestion for a material using the same working logic as JSON detection
        /// </summary>
        public string GetShaderFromMaterialForDisplay(Material material)
        {
            if (material == null) return null;

            try
            {
                // Create a minimal material data structure from the Unity material
                var materialData = new MaterialJsonStructure();
                materialData.m_SavedProperties = new MaterialJsonStructure.SavedProperties();
                materialData.m_SavedProperties.m_TexEnvs = new Dictionary<string, MaterialJsonStructure.TexturePropertyInfo>();
                materialData.m_SavedProperties.m_Floats = new Dictionary<string, float>();
                materialData.m_SavedProperties.m_Ints = new Dictionary<string, int>();
                materialData.m_SavedProperties.m_Colors = new Dictionary<string, MaterialJsonStructure.ColorInfo>();

                // Extract material properties for analysis
                var shader = material.shader;
                if (shader != null)
                {
                    for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                    {
                        string propertyName = ShaderUtil.GetPropertyName(shader, i);
                        var propertyType = ShaderUtil.GetPropertyType(shader, i);

                        try
                        {
                            switch (propertyType)
                            {
                                case ShaderUtil.ShaderPropertyType.TexEnv:
                                    var texture = material.GetTexture(propertyName);
                                    if (texture != null)
                                    {
                                        materialData.m_SavedProperties.m_TexEnvs[propertyName] = new MaterialJsonStructure.TexturePropertyInfo
                                        {
                                            m_Texture = new MaterialJsonStructure.TextureInfo { m_FileID = 0 }
                                        };
                                    }
                                    break;
                                case ShaderUtil.ShaderPropertyType.Float:
                                case ShaderUtil.ShaderPropertyType.Range:
                                    materialData.m_SavedProperties.m_Floats[propertyName] = material.GetFloat(propertyName);
                                    break;
                                case ShaderUtil.ShaderPropertyType.Color:
                                    var color = material.GetColor(propertyName);
                                    materialData.m_SavedProperties.m_Colors[propertyName] = new MaterialJsonStructure.ColorInfo
                                    {
                                        r = color.r,
                                        g = color.g,
                                        b = color.b,
                                        a = color.a
                                    };
                                    break;
                                case ShaderUtil.ShaderPropertyType.Vector:
                                    var vector = material.GetVector(propertyName);
                                    materialData.m_SavedProperties.m_Colors[propertyName] = new MaterialJsonStructure.ColorInfo
                                    {
                                        r = vector.x,
                                        g = vector.y,
                                        b = vector.z,
                                        a = vector.w
                                    };
                                    break;
                            }
                        }
                        catch (System.Exception)
                        {
                            // Skip properties that can't be read
                            continue;
                        }
                    }
                }

                // Use the working detection method
                return HoyoToonMaterialManager.DetectShaderFromMaterialData(materialData);
            }
            catch (System.Exception e)
            {
                HoyoToonLogs.WarningDebug($"Failed to detect shader for material {material.name}: {e.Message}");
                return null;
            }
        }

        private System.Collections.Generic.Dictionary<string, string> GetShaderRecommendations(string gameType)
        {
            var recommendations = new System.Collections.Generic.Dictionary<string, string>();
            var data = HoyoToonDataManager.Data;

            if (data?.Shaders == null) return recommendations;

            switch (gameType.ToLower())
            {
                case "genshin":
                case "gi":
                    if (data.Shaders.ContainsKey("GIShader"))
                    {
                        recommendations["Face/Body"] = data.Shaders["GIShader"][0];
                        recommendations["Hair"] = data.Shaders["GIShader"][0];
                        recommendations["Clothing"] = data.Shaders["GIShader"][0];
                    }
                    break;

                case "honkai":
                case "hi3":
                    if (data.Shaders.ContainsKey("HI3Shader"))
                    {
                        recommendations["Face/Body"] = data.Shaders["HI3Shader"][0];
                        recommendations["Hair"] = data.Shaders["HI3Shader"][0];
                    }
                    break;

                case "starrail":
                case "hsr":
                    if (data.Shaders.ContainsKey("HSRShader"))
                    {
                        recommendations["Face/Body"] = data.Shaders["HSRShader"][0];
                        recommendations["Hair"] = data.Shaders["HSRShader"][0];
                    }
                    break;

                case "wuthering":
                case "wuwa":
                    if (data.Shaders.ContainsKey("WuWaShader"))
                    {
                        recommendations["Face/Body"] = data.Shaders["WuWaShader"][0];
                        recommendations["Hair"] = data.Shaders["WuWaShader"][0];
                    }
                    break;

                case "zenless":
                case "zzz":
                    if (data.Shaders.ContainsKey("ZZZShader"))
                    {
                        recommendations["Face/Body"] = data.Shaders["ZZZShader"][0];
                        recommendations["Hair"] = data.Shaders["ZZZShader"][0];
                    }
                    break;

                default:
                    // Default recommendations
                    if (data.Shaders.ContainsKey("GIShader"))
                        recommendations["Default"] = data.Shaders["GIShader"][0];
                    break;
            }

            return recommendations;
        }

        public void SelectMaterial(HoyoToonMaterialInfo materialInfo)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialInfo.materialPath);
            if (material != null)
            {
                Selection.activeObject = material;
                EditorGUIUtility.PingObject(material);
            }
        }

        public void ViewMaterialInInspector(HoyoToonMaterialInfo materialInfo)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialInfo.materialPath);
            if (material != null)
            {
                // Select the material
                Selection.activeObject = material;

                // Open the Inspector window and focus it
                EditorWindow inspectorWindow = EditorWindow.GetWindow(System.Type.GetType("UnityEditor.InspectorWindow,UnityEditor"));
                if (inspectorWindow != null)
                {
                    inspectorWindow.Show();
                    inspectorWindow.Focus();
                }

                // Ping the object to highlight it
                EditorGUIUtility.PingObject(material);
            }
        }

        private string FormatMemorySize(long bytes)
        {
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F1} MB";
        }

        private void CreateShaderOptions(VisualElement detailsContainer, Material material)
        {
            var shaderContainer = new VisualElement();
            shaderContainer.style.marginTop = 8;

            var shaderLabel = new Label("Shader Information");
            shaderLabel.style.fontSize = 12;
            shaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            shaderLabel.style.marginBottom = 4;

            var shaderInfo = new Label();
            shaderInfo.style.fontSize = 11;
            shaderInfo.style.marginBottom = 4;

            var currentShaderName = HoyoToonDataManager.GetFriendlyShaderName(material.shader.name);
            shaderInfo.text = $"Current: {currentShaderName}";

            shaderContainer.Add(shaderLabel);
            shaderContainer.Add(shaderInfo);

            // Add shader selection dropdown
            var shaderSelectionContainer = new VisualElement();
            shaderSelectionContainer.style.flexDirection = FlexDirection.Row;
            shaderSelectionContainer.style.alignItems = Align.Center;
            shaderSelectionContainer.style.marginTop = 4;

            var shaderDropdownLabel = new Label("Change to:");
            shaderDropdownLabel.style.minWidth = 80;
            shaderDropdownLabel.style.fontSize = 11;

            var shaderDropdown = new DropdownField();
            shaderDropdown.style.flexGrow = 1;
            shaderDropdown.style.minWidth = 200;

            // Populate dropdown with available shaders
            var shaderChoices = new List<string>();
            var shaderPaths = new List<string>();

            var availableShaders = HoyoToonDataManager.Data.Shaders;
            foreach (var shaderGroup in availableShaders)
            {
                foreach (var shaderPath in shaderGroup.Value)
                {
                    var friendlyName = HoyoToonDataManager.GetFriendlyShaderName(shaderPath);
                    shaderChoices.Add(friendlyName);
                    shaderPaths.Add(shaderPath);
                }
            }

            shaderDropdown.choices = shaderChoices;
            shaderDropdown.value = shaderDropdown.choices.FirstOrDefault() ?? "";

            shaderDropdown.RegisterValueChangedCallback(evt =>
            {
                var selectedIndex = shaderDropdown.choices.IndexOf(evt.newValue);
                if (selectedIndex >= 0 && selectedIndex < shaderPaths.Count)
                {
                    var selectedShaderPath = shaderPaths[selectedIndex];
                    var shader = Shader.Find(selectedShaderPath);
                    if (shader != null)
                    {
                        Undo.RecordObject(material, "Change Material Shader");
                        material.shader = shader;
                        EditorUtility.SetDirty(material);

                        // Update the shader info display
                        shaderInfo.text = $"Current: {HoyoToonDataManager.GetFriendlyShaderName(shader.name)}";

                        // Clear cached preview for this material to regenerate with new shader
                        string materialPath = AssetDatabase.GetAssetPath(material);
                        if (materialPreviewTextures.ContainsKey(materialPath))
                        {
                            materialPreviewTextures.Remove(materialPath);
                        }

                        // Trigger UI refresh
                        HoyoToonUIManager.Instance?.ForceAnalysisRefresh();
                    }
                }
            });

            shaderSelectionContainer.Add(shaderDropdownLabel);
            shaderSelectionContainer.Add(shaderDropdown);
            shaderContainer.Add(shaderSelectionContainer);

            detailsContainer.Add(shaderContainer);
        }

        #region Global Property Management

        /// <summary>
        /// Get all materials from the currently selected model
        /// </summary>
        private List<Material> GetModelMaterials()
        {
            var materials = new List<Material>();

            if (selectedModel == null) return materials;

            var renderers = selectedModel.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && !materials.Contains(material))
                    {
                        materials.Add(material);
                    }
                }
            }

            return materials;
        }

        /// <summary>
        /// Data structure for property analysis results
        /// </summary>
        private class PropertyAnalysis
        {
            public List<ShaderPropertyInfo> commonProperties = new List<ShaderPropertyInfo>();
            public Dictionary<string, object> propertyValues = new Dictionary<string, object>();
            public Dictionary<string, bool> hasConsistentValues = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Analyze shader properties across all materials to find common ones
        /// </summary>
        private PropertyAnalysis AnalyzeShaderProperties(List<Material> materials)
        {
            var analysis = new PropertyAnalysis();

            if (materials.Count == 0) return analysis;

            // Get unique shaders being used
            var uniqueShaders = materials.Where(m => m.shader != null).Select(m => m.shader).Distinct();

            // Get properties for each shader type
            var availableProperties = new List<ShaderPropertyInfo>();
            foreach (var shader in uniqueShaders)
            {
                var shaderProperties = GetPropertiesForShader(shader);
                foreach (var propertyInfo in shaderProperties)
                {
                    if (!availableProperties.Any(p => p.propertyName == propertyInfo.propertyName))
                    {
                        availableProperties.Add(propertyInfo);
                    }
                }
            }

            analysis.commonProperties = availableProperties.OrderBy(p => p.displayName).ToList();

            // Determine the current value of each property and check consistency across materials
            foreach (var property in analysis.commonProperties)
            {
                var propertyValues = new List<object>();
                bool allMaterialsHaveProperty = true;

                foreach (var material in materials)
                {
                    if (material.HasProperty(property.propertyName))
                    {
                        object value = GetMaterialPropertyValue(material, property);
                        propertyValues.Add(value);
                    }
                    else
                    {
                        allMaterialsHaveProperty = false;
                        propertyValues.Add(property.defaultValue);
                    }
                }

                // Determine if all materials have the same value
                bool hasConsistentValue = propertyValues.Count > 0 && propertyValues.All(v =>
                    (v == null && propertyValues.First() == null) ||
                    (v != null && v.Equals(propertyValues.First())));

                analysis.hasConsistentValues[property.propertyName] = hasConsistentValue && allMaterialsHaveProperty;

                // Store the first value or default if inconsistent
                analysis.propertyValues[property.propertyName] = hasConsistentValue && propertyValues.Count > 0
                    ? propertyValues.First()
                    : property.defaultValue;
            }

            return analysis;
        }

        /// <summary>
        /// Get the current value of a material property based on its type
        /// </summary>
        private object GetMaterialPropertyValue(Material material, ShaderPropertyInfo property)
        {
            if (!material.HasProperty(property.propertyName))
                return property.defaultValue;

            switch (property.propertyType)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    return material.GetFloat(property.propertyName);

                case ShaderPropertyType.Boolean:
                    // Booleans are stored as floats in shaders (0 or 1)
                    return material.GetFloat(property.propertyName) > 0.5f;

                case ShaderPropertyType.Color:
                    return material.GetColor(property.propertyName);

                case ShaderPropertyType.Vector:
                    return material.GetVector(property.propertyName);

                case ShaderPropertyType.Texture:
                    return material.GetTexture(property.propertyName);

                default:
                    return property.defaultValue;
            }
        }

        /// <summary>
        /// Apply a property value to all materials
        /// </summary>
        private void ApplyPropertyToAllMaterials(List<Material> materials, ShaderPropertyInfo property, object value, bool refreshUI = true)
        {
            try
            {
                Undo.RecordObjects(materials.ToArray(), $"Global Property: {property.displayName}");

                foreach (var material in materials)
                {
                    if (material.HasProperty(property.propertyName))
                    {
                        SetMaterialPropertyValue(material, property, value);
                    }
                }

                // Mark materials as dirty for saving
                foreach (var material in materials)
                {
                    EditorUtility.SetDirty(material);
                }

                HoyoToonLogs.LogDebug($"Applied property '{property.propertyName}' = {value} to {materials.Count} materials");

                // Refresh the UI to update the displays (unless suppressed for smooth slider operation)
                if (refreshUI)
                {
                    EditorApplication.delayCall += () => RefreshTabContent();
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to apply property '{property.displayName}': {e.Message}", "OK");
                HoyoToonLogs.ErrorDebug($"Error applying property: {e.Message}");
            }
        }

        /// <summary>
        /// Set a material property value based on its type
        /// </summary>
        private void SetMaterialPropertyValue(Material material, ShaderPropertyInfo property, object value)
        {
            if (!material.HasProperty(property.propertyName))
                return;

            switch (property.propertyType)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    if (value is float floatValue)
                        material.SetFloat(property.propertyName, floatValue);
                    break;

                case ShaderPropertyType.Boolean:
                    // Convert boolean to float for shader (0 or 1)
                    if (value is bool boolValue)
                        material.SetFloat(property.propertyName, boolValue ? 1f : 0f);
                    break;

                case ShaderPropertyType.Color:
                    if (value is Color colorValue)
                        material.SetColor(property.propertyName, colorValue);
                    break;

                case ShaderPropertyType.Vector:
                    if (value is Vector4 vectorValue)
                        material.SetVector(property.propertyName, vectorValue);
                    break;

                case ShaderPropertyType.Texture:
                    if (value is Texture textureValue)
                        material.SetTexture(property.propertyName, textureValue);
                    break;
            }
        }

        /// <summary>
        /// Data structure for shader property information
        /// </summary>
        public class ShaderPropertyInfo
        {
            public string propertyName;
            public string displayName;
            public string tooltip;
            public ShaderPropertyType propertyType;
            public float minValue = 0f;
            public float maxValue = 1f;
            public object defaultValue;

            public ShaderPropertyInfo(string propertyName, string displayName, string tooltip, ShaderPropertyType propertyType, object defaultValue = null, float minValue = 0f, float maxValue = 1f)
            {
                this.propertyName = propertyName;
                this.displayName = displayName;
                this.tooltip = tooltip;
                this.propertyType = propertyType;
                this.defaultValue = defaultValue;
                this.minValue = minValue;
                this.maxValue = maxValue;
            }
        }

        public enum ShaderPropertyType
        {
            Float,
            Range,
            Boolean,
            Color,
            Vector,
            Texture
        }

        /// <summary>
        /// Get the predefined properties for a specific shader with display names, tooltips, and types
        /// </summary>
        private List<ShaderPropertyInfo> GetPropertiesForShader(Shader shader)
        {
            if (shader == null) return new List<ShaderPropertyInfo>();

            var shaderName = shader.name.ToLower();

            // Define properties for each HoyoToon shader
            if (shaderName.Contains("genshin"))
            {
                return new List<ShaderPropertyInfo>
                {
                    new ShaderPropertyInfo("_Scale", "Outline Width", "Change the width of the outline around the model", ShaderPropertyType.Range, 0.03f, 0.01f, 0.06f),
                    new ShaderPropertyInfo("_EnableSelfShadow", "Self Casted Shadows", "Enable self-shadowing on the model", ShaderPropertyType.Boolean, 1f),
                    new ShaderPropertyInfo("_EnableTonemapping", "Tone Mapping", "Toggle built in tone mapping for color correction", ShaderPropertyType.Boolean, 1f)
                };
            }
            else if (shaderName.Contains("character part 1"))
            {
                return new List<ShaderPropertyInfo>
                {
                    new ShaderPropertyInfo("_Scale", "Outline Width", "Change the width of the outline around the model", ShaderPropertyType.Range, 0.03f, 0.01f, 0.06f),
                };
            }
            else if (shaderName.Contains("character part 2"))
            {
                return new List<ShaderPropertyInfo>
                {
                    new ShaderPropertyInfo("_Scale", "Outline Width", "Change the width of the outline around the model", ShaderPropertyType.Range, 0.03f, 0.01f, 0.06f),
                };
            }
            else if (shaderName.Contains("star rail"))
            {
                return new List<ShaderPropertyInfo>
                {
                    new ShaderPropertyInfo("_OutlineScale", "Outline Width", "Change the width of the outline around the model", ShaderPropertyType.Range, 0.0374f, 0.187f, 0.06f),
                    new ShaderPropertyInfo("_UseSelfShadow", "Self Casted Shadows", "Enable self-shadowing on the model", ShaderPropertyType.Boolean, 1f),
                    new ShaderPropertyInfo("_EnableLUT", "Tone Mapping", "Toggle built in tone mapping for color correction", ShaderPropertyType.Boolean, 1f),
                };
            }
            else if (shaderName.Contains("wuthering"))
            {
                return new List<ShaderPropertyInfo>
                {
                    new ShaderPropertyInfo("_OutlineWidth", "Outline Width", "Change the width of the outline around the model", ShaderPropertyType.Range, 0.11f, 0f, 1f),
                    new ShaderPropertyInfo("_UseSelfShadow", "Self Casted Shadows", "Enable self-shadowing on the model", ShaderPropertyType.Boolean, 1f),
                    new ShaderPropertyInfo("_UseToneMapping", "Tone Mapping", "Toggle built in tone mapping for color correction", ShaderPropertyType.Boolean, 1f),
                };
            }
            else if (shaderName.Contains("zenless"))
            {
                return new List<ShaderPropertyInfo>
                {
                    new ShaderPropertyInfo("_OutlineWidth", "Outline Width", "Change the width of the outline around the model", ShaderPropertyType.Range, 1f, 0f, 1f),
                    new ShaderPropertyInfo("_UseSelfShadow", "Self Casted Shadows", "Enable self-shadowing on the model", ShaderPropertyType.Boolean, 1f),
                    new ShaderPropertyInfo("_EnableLUT", "Tone Mapping", "Toggle built in tone mapping for color correction", ShaderPropertyType.Boolean, 1f),
                };
            }
            return new List<ShaderPropertyInfo>();
        }

        /// <summary>
        /// Count how many materials have a specific keyword enabled
        /// </summary>
        /// <summary>
        /// Count how many materials have a specific property with a particular value
        /// </summary>
        private int CountMaterialsWithPropertyValue(List<Material> materials, ShaderPropertyInfo property, object targetValue)
        {
            return materials.Count(m =>
            {
                if (!m.HasProperty(property.propertyName)) return false;

                var currentValue = GetMaterialPropertyValue(m, property);

                // Handle different comparison types
                if (property.propertyType == ShaderPropertyType.Boolean)
                {
                    return (bool)currentValue == (bool)targetValue;
                }
                else if (property.propertyType == ShaderPropertyType.Float || property.propertyType == ShaderPropertyType.Range)
                {
                    return Mathf.Approximately((float)currentValue, (float)targetValue);
                }
                else
                {
                    return currentValue?.Equals(targetValue) ?? (targetValue == null);
                }
            });
        }

        /// <summary>
        /// Create appropriate UI control for a shader property based on its type
        /// </summary>
        private VisualElement CreatePropertyControl(ShaderPropertyInfo property, object currentValue, List<Material> materials)
        {
            switch (property.propertyType)
            {
                case ShaderPropertyType.Boolean:
                    var toggle = new Toggle();
                    toggle.value = currentValue is bool boolVal ? boolVal : false;
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        ApplyPropertyToAllMaterials(materials, property, evt.newValue);
                    });
                    return toggle;

                case ShaderPropertyType.Range:
                    var slider = new Slider(property.minValue, property.maxValue);
                    slider.value = currentValue is float floatVal ? floatVal : (float)(property.defaultValue ?? property.minValue);

                    // Use immediate updates without UI refresh for smooth dragging
                    slider.RegisterValueChangedCallback(evt =>
                    {
                        // Apply changes immediately without UI refresh for smooth dragging
                        ApplyPropertyToAllMaterials(materials, property, evt.newValue, false);
                    });

                    // Add editable value field instead of just a label
                    var sliderContainer = new VisualElement();
                    sliderContainer.style.flexDirection = FlexDirection.Row;
                    sliderContainer.style.alignItems = Align.Center;

                    slider.style.flexGrow = 1;

                    // Create editable float field for precise input
                    var valueField = new FloatField();
                    valueField.value = slider.value;
                    valueField.style.minWidth = 60;
                    valueField.style.maxWidth = 80;
                    valueField.style.marginLeft = 5;

                    // Sync slider -> field
                    slider.RegisterValueChangedCallback(evt =>
                    {
                        if (Math.Abs(valueField.value - evt.newValue) > 0.001f) // Avoid circular updates
                        {
                            valueField.value = evt.newValue;
                        }
                    });

                    // Sync field -> slider (with range clamping)
                    valueField.RegisterValueChangedCallback(evt =>
                    {
                        float clampedValue = Mathf.Clamp(evt.newValue, property.minValue, property.maxValue);
                        if (Math.Abs(slider.value - clampedValue) > 0.001f) // Avoid circular updates
                        {
                            slider.value = clampedValue;
                        }
                        // Apply the clamped value to materials
                        ApplyPropertyToAllMaterials(materials, property, clampedValue, false);
                    });

                    sliderContainer.Add(slider);
                    sliderContainer.Add(valueField);
                    return sliderContainer;

                case ShaderPropertyType.Float:
                    var floatField = new FloatField();
                    floatField.value = currentValue is float floatValue ? floatValue : (float)(property.defaultValue ?? 0f);
                    floatField.RegisterValueChangedCallback(evt =>
                    {
                        ApplyPropertyToAllMaterials(materials, property, evt.newValue);
                    });
                    return floatField;

                case ShaderPropertyType.Color:
                    var colorField = new ColorField();
                    colorField.value = currentValue is Color colorValue ? colorValue : (Color)(property.defaultValue ?? Color.white);
                    colorField.RegisterValueChangedCallback(evt =>
                    {
                        ApplyPropertyToAllMaterials(materials, property, evt.newValue);
                    });
                    return colorField;

                case ShaderPropertyType.Vector:
                    var vectorField = new Vector4Field();
                    vectorField.value = currentValue is Vector4 vectorValue ? vectorValue : (Vector4)(property.defaultValue ?? Vector4.zero);
                    vectorField.RegisterValueChangedCallback(evt =>
                    {
                        ApplyPropertyToAllMaterials(materials, property, evt.newValue);
                    });
                    return vectorField;

                case ShaderPropertyType.Texture:
                    var textureField = new ObjectField();
                    textureField.objectType = typeof(Texture);
                    textureField.value = currentValue as Texture;
                    textureField.RegisterValueChangedCallback(evt =>
                    {
                        ApplyPropertyToAllMaterials(materials, property, evt.newValue);
                    });
                    return textureField;

                default:
                    var defaultLabel = new Label("Unsupported Type");
                    defaultLabel.style.color = Color.red;
                    return defaultLabel;
            }
        }

        /// <summary>
        /// Reset all shader properties on all materials to their default state
        /// </summary>
        private void ResetAllProperties(List<Material> materials)
        {
            if (EditorUtility.DisplayDialog("Reset Properties",
                $"This will reset all shader properties on {materials.Count} materials to their default state. This action cannot be undone easily. Continue?",
                "Reset", "Cancel"))
            {
                try
                {
                    Undo.RecordObjects(materials.ToArray(), "Reset All Properties");

                    foreach (var material in materials)
                    {
                        var shaderProperties = GetPropertiesForShader(material.shader);

                        foreach (var property in shaderProperties)
                        {
                            if (material.HasProperty(property.propertyName))
                            {
                                SetMaterialPropertyValue(material, property, property.defaultValue);
                            }
                        }

                        EditorUtility.SetDirty(material);
                    }

                    HoyoToonLogs.LogDebug($"Reset all properties on {materials.Count} materials");

                    EditorUtility.DisplayDialog("Success",
                        $"Reset all shader properties on {materials.Count} materials to default values.", "OK");

                    // Refresh the UI
                    EditorApplication.delayCall += () => RefreshTabContent();
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Failed to reset properties: {e.Message}", "OK");
                    HoyoToonLogs.ErrorDebug($"Error resetting properties: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Apply a shader override to all generated material assets
        /// </summary>
        private void ApplyGlobalShaderOverride(string shaderPath)
        {
            if (string.IsNullOrEmpty(shaderPath))
            {
                EditorUtility.DisplayDialog("Error", "No shader path provided for global override.", "OK");
                return;
            }

            // Get all materials from the current model
            var materials = GetModelMaterials();

            if (materials.Count == 0)
            {
                EditorUtility.DisplayDialog("No Materials Found",
                    "No materials found on the selected model to apply shader override to.", "OK");
                return;
            }

            // Find the shader
            var targetShader = Shader.Find(shaderPath);
            if (targetShader == null)
            {
                EditorUtility.DisplayDialog("Shader Not Found",
                    $"Could not find shader at path: {shaderPath}", "OK");
                return;
            }

            var friendlyShaderName = HoyoToonDataManager.GetFriendlyShaderName(shaderPath);

            if (EditorUtility.DisplayDialog("Apply Global Shader Override",
                $"This will change the shader of {materials.Count} materials to '{friendlyShaderName}'.\n\n" +
                "This will directly modify the material assets. Continue?",
                "Apply", "Cancel"))
            {
                try
                {
                    int successCount = 0;
                    int failureCount = 0;

                    // Record undo for all materials
                    Undo.RecordObjects(materials.ToArray(), $"Apply Global Shader: {friendlyShaderName}");

                    foreach (var material in materials)
                    {
                        try
                        {
                            var oldShader = material.shader;
                            material.shader = targetShader;
                            EditorUtility.SetDirty(material);

                            successCount++;
                            HoyoToonLogs.LogDebug($"Applied shader '{shaderPath}' to material '{material.name}' (was: {oldShader?.name ?? "None"})");
                        }
                        catch (System.Exception e)
                        {
                            failureCount++;
                            HoyoToonLogs.ErrorDebug($"Failed to apply shader to material '{material.name}': {e.Message}");
                        }
                    }

                    // Clear material preview cache since shaders changed
                    materialPreviewTextures.Clear();

                    // Display results
                    if (failureCount == 0)
                    {
                        EditorUtility.DisplayDialog("Success",
                            $"Successfully applied '{friendlyShaderName}' shader to {successCount} materials.",
                            "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Partial Success",
                            $"Applied shader to {successCount} materials.\n" +
                            $"Failed to apply to {failureCount} materials.\n\n" +
                            "Check the console for detailed error information.",
                            "OK");
                    }

                    // Refresh the UI to show updated materials
                    EditorApplication.delayCall += () => RefreshTabContent();
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error",
                        $"Failed to apply global shader override: {e.Message}", "OK");
                    HoyoToonLogs.ErrorDebug($"Error applying global shader override: {e.Message}");
                }
            }
        }

        #endregion

        #endregion
    }
}