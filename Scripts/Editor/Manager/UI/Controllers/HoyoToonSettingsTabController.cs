using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HoyoToon.UI.Core;
using HoyoToon.UI.Components;

namespace HoyoToon
{
    /// <summary>
    /// HoyoToon Settings Tab Controller
    /// Handles global application settings and preferences
    /// Updated to use the new modular UI system
    /// </summary>
    public class HoyoToonSettingsTabController : HoyoToonModularBaseTabController
    {
        public override string TabName => "Settings";
        public override bool RequiresModel => false; // Settings can work without model

        private HoyoToonUISettings settings;

        #region Modular UI System Implementation

        protected override void InitializeTabComponents()
        {
            // Register components for Settings tab (no model required)
            AddComponent<ProgressIndicatorComponent>();
        }

        public override List<QuickAction> GetQuickActions()
        {
            var actions = new List<QuickAction>();

            // Settings tab quick actions - utilities that don't require a model
            actions.Add(new QuickAction("Clear Cache", () =>
            {
                if (EditorUtility.DisplayDialog("Clear Cache",
                    "This will clear all cached data. Continue?", "Yes", "Cancel"))
                {
                    ClearAllCaches();
                }
            }));

            actions.Add(new QuickAction("Reset to Defaults", () =>
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "This will reset all settings to defaults. Continue?", "Yes", "Cancel"))
                {
                    ResetToDefaults();
                }
            }));

            return actions;
        }

        #endregion

        protected override void OnInitialize()
        {
            settings = new HoyoToonUISettings();
            settings.LoadFromEditorPrefs();
        }

        protected override void CreateTabContent()
        {
            // UI Settings Section
            contentView.Add(CreateHoyoToonSectionHeader("UI Settings"));
            CreateUISettingsSection();

            // Shader Settings Section
            contentView.Add(CreateHoyoToonSectionHeader("Shader Settings"));
            CreateShaderSettingsSection();

            // Performance Settings Section
            contentView.Add(CreateHoyoToonSectionHeader("Performance Settings"));
            CreatePerformanceSettingsSection();

            // Debug Settings Section
            contentView.Add(CreateHoyoToonSectionHeader("Debug Settings"));
            CreateDebugSettingsSection();

            // Data Management Section
            contentView.Add(CreateHoyoToonSectionHeader("Data Management"));
            CreateDataManagementSection();

            // Actions Section
            contentView.Add(CreateHoyoToonSectionHeader("Settings Actions"));
            CreateSettingsActionsSection();
        }

        private void CreateUISettingsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Auto-save settings toggle
            var autoSaveToggle = new Toggle("Auto-save settings");
            autoSaveToggle.value = settings.autoSaveSettings;
            autoSaveToggle.RegisterValueChangedCallback(evt =>
            {
                settings.autoSaveSettings = evt.newValue;
                SaveSettings();
            });
            container.Add(autoSaveToggle);

            // Show detailed analysis toggle
            var detailedAnalysisToggle = new Toggle("Show detailed analysis");
            detailedAnalysisToggle.value = settings.showDetailedAnalysis;
            detailedAnalysisToggle.RegisterValueChangedCallback(evt =>
            {
                settings.showDetailedAnalysis = evt.newValue;
                SaveSettings();
            });
            container.Add(detailedAnalysisToggle);

            // Show memory usage toggle
            var memoryUsageToggle = new Toggle("Show memory usage");
            memoryUsageToggle.value = settings.showMemoryUsage;
            memoryUsageToggle.RegisterValueChangedCallback(evt =>
            {
                settings.showMemoryUsage = evt.newValue;
                SaveSettings();
            });
            container.Add(memoryUsageToggle);

            // Enable preview toggle
            var previewToggle = new Toggle("Enable model preview");
            previewToggle.value = settings.enablePreview;
            previewToggle.RegisterValueChangedCallback(evt =>
            {
                settings.enablePreview = evt.newValue;
                SaveSettings();
            });
            container.Add(previewToggle);

            // Auto-refresh on select toggle
            var autoRefreshToggle = new Toggle("Auto-refresh on model select");
            autoRefreshToggle.value = settings.autoRefreshOnSelect;
            autoRefreshToggle.RegisterValueChangedCallback(evt =>
            {
                settings.autoRefreshOnSelect = evt.newValue;
                SaveSettings();
            });
            container.Add(autoRefreshToggle);

            // Preview quality dropdown
            var previewQualityContainer = new VisualElement();
            previewQualityContainer.style.flexDirection = FlexDirection.Row;
            previewQualityContainer.style.alignItems = Align.Center;
            previewQualityContainer.style.marginTop = 10;

            var previewQualityLabel = new Label("Preview Quality:");
            previewQualityLabel.style.minWidth = 120;

            var previewQualityDropdown = new DropdownField();
            previewQualityDropdown.choices = new System.Collections.Generic.List<string> { "Low", "Medium", "High" };
            previewQualityDropdown.value = previewQualityDropdown.choices[settings.previewQuality];
            previewQualityDropdown.RegisterValueChangedCallback(evt =>
            {
                settings.previewQuality = previewQualityDropdown.choices.IndexOf(evt.newValue);
                SaveSettings();
            });

            previewQualityContainer.Add(previewQualityLabel);
            previewQualityContainer.Add(previewQualityDropdown);
            container.Add(previewQualityContainer);

            contentView.Add(container);
        }

        private void CreateShaderSettingsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Preferred game type dropdown
            var gameTypeContainer = new VisualElement();
            gameTypeContainer.style.flexDirection = FlexDirection.Row;
            gameTypeContainer.style.alignItems = Align.Center;
            gameTypeContainer.style.marginBottom = 10;

            var gameTypeLabel = new Label("Preferred Game Type:");
            gameTypeLabel.style.minWidth = 150;

            var gameTypeDropdown = new DropdownField();
            gameTypeDropdown.choices = new System.Collections.Generic.List<string> {
                "Auto", "Genshin Impact", "Honkai Impact 3rd", "Honkai Star Rail", "Wuthering Waves", "Zenless Zone Zero"
            };
            gameTypeDropdown.value = settings.preferredGameType;
            gameTypeDropdown.RegisterValueChangedCallback(evt =>
            {
                settings.preferredGameType = evt.newValue;
                SaveSettings();
            });

            gameTypeContainer.Add(gameTypeLabel);
            gameTypeContainer.Add(gameTypeDropdown);
            container.Add(gameTypeContainer);

            // Preferred body type dropdown
            var bodyTypeContainer = new VisualElement();
            bodyTypeContainer.style.flexDirection = FlexDirection.Row;
            bodyTypeContainer.style.alignItems = Align.Center;
            bodyTypeContainer.style.marginBottom = 10;

            var bodyTypeLabel = new Label("Preferred Body Type:");
            bodyTypeLabel.style.minWidth = 150;

            var bodyTypeDropdown = new DropdownField();
            bodyTypeDropdown.choices = new System.Collections.Generic.List<string> {
                "Auto", "Girl", "Boy", "Male", "Female", "Lady", "Loli"
            };
            bodyTypeDropdown.value = settings.preferredBodyType;
            bodyTypeDropdown.RegisterValueChangedCallback(evt =>
            {
                settings.preferredBodyType = evt.newValue;
                SaveSettings();
            });

            bodyTypeContainer.Add(bodyTypeLabel);
            bodyTypeContainer.Add(bodyTypeDropdown);
            container.Add(bodyTypeContainer);

            // Auto-detect character toggle
            var autoDetectToggle = new Toggle("Auto-detect character from model name");
            autoDetectToggle.value = settings.autoDetectCharacter;
            autoDetectToggle.RegisterValueChangedCallback(evt =>
            {
                settings.autoDetectCharacter = evt.newValue;
                SaveSettings();
            });
            container.Add(autoDetectToggle);

            // Custom character override field
            var characterOverrideContainer = new VisualElement();
            characterOverrideContainer.style.flexDirection = FlexDirection.Row;
            characterOverrideContainer.style.alignItems = Align.Center;
            characterOverrideContainer.style.marginTop = 10;

            var characterOverrideLabel = new Label("Character Override:");
            characterOverrideLabel.style.minWidth = 150;

            var characterOverrideField = CreateHoyoToonTextField();
            characterOverrideField.value = settings.customCharacterOverride;
            characterOverrideField.RegisterValueChangedCallback(evt =>
            {
                settings.customCharacterOverride = evt.newValue;
                SaveSettings();
            });

            characterOverrideContainer.Add(characterOverrideLabel);
            characterOverrideContainer.Add(characterOverrideField);
            container.Add(characterOverrideContainer);

            // Shader-specific settings
            container.Add(CreateHoyoToonSubsectionHeader("Shader Parameters:"));

            // Outline width slider
            var outlineContainer = new VisualElement();
            outlineContainer.style.flexDirection = FlexDirection.Row;
            outlineContainer.style.alignItems = Align.Center;
            outlineContainer.style.marginTop = 5;

            var outlineLabel = new Label("Outline Width:");
            outlineLabel.style.minWidth = 120;

            var outlineSlider = new Slider(0f, 5f);
            outlineSlider.value = settings.outlineWidth;
            outlineSlider.style.flexGrow = 1;
            outlineSlider.RegisterValueChangedCallback(evt =>
            {
                settings.outlineWidth = evt.newValue;
                SaveSettings();
            });

            var outlineValueLabel = new Label(settings.outlineWidth.ToString("F2"));
            outlineValueLabel.style.minWidth = 40;
            outlineSlider.RegisterValueChangedCallback(evt =>
            {
                outlineValueLabel.text = evt.newValue.ToString("F2");
            });

            outlineContainer.Add(outlineLabel);
            outlineContainer.Add(outlineSlider);
            outlineContainer.Add(outlineValueLabel);
            container.Add(outlineContainer);

            // Rim width slider
            var rimContainer = new VisualElement();
            rimContainer.style.flexDirection = FlexDirection.Row;
            rimContainer.style.alignItems = Align.Center;
            rimContainer.style.marginTop = 5;

            var rimLabel = new Label("Rim Width:");
            rimLabel.style.minWidth = 120;

            var rimSlider = new Slider(0f, 5f);
            rimSlider.value = settings.rimWidth;
            rimSlider.style.flexGrow = 1;
            rimSlider.RegisterValueChangedCallback(evt =>
            {
                settings.rimWidth = evt.newValue;
                SaveSettings();
            });

            var rimValueLabel = new Label(settings.rimWidth.ToString("F2"));
            rimValueLabel.style.minWidth = 40;
            rimSlider.RegisterValueChangedCallback(evt =>
            {
                rimValueLabel.text = evt.newValue.ToString("F2");
            });

            rimContainer.Add(rimLabel);
            rimContainer.Add(rimSlider);
            rimContainer.Add(rimValueLabel);
            container.Add(rimContainer);

            // Use built-in tone mapping toggle
            var toneMappingToggle = new Toggle("Use built-in tone mapping");
            toneMappingToggle.value = settings.useBuiltInToneMapping;
            toneMappingToggle.RegisterValueChangedCallback(evt =>
            {
                settings.useBuiltInToneMapping = evt.newValue;
                SaveSettings();
            });
            container.Add(toneMappingToggle);

            // Use self shadows toggle
            var selfShadowsToggle = new Toggle("Use self shadows");
            selfShadowsToggle.value = settings.useSelfShadows;
            selfShadowsToggle.RegisterValueChangedCallback(evt =>
            {
                settings.useSelfShadows = evt.newValue;
                SaveSettings();
            });
            container.Add(selfShadowsToggle);

            contentView.Add(container);
        }

        private void CreatePerformanceSettingsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Performance info
            container.Add(CreateHoyoToonInfoRow("Unity Version:", Application.unityVersion));
            container.Add(CreateHoyoToonInfoRow("Graphics API:", SystemInfo.graphicsDeviceType.ToString()));
            container.Add(CreateHoyoToonInfoRow("GPU Memory:", $"{SystemInfo.graphicsMemorySize} MB"));
            container.Add(CreateHoyoToonInfoRow("System Memory:", $"{SystemInfo.systemMemorySize} MB"));

            // Performance recommendations
            var recommendations = new System.Collections.Generic.List<string>();

            if (SystemInfo.graphicsMemorySize < 2048)
                recommendations.Add("Low GPU memory - consider texture optimization");

            if (SystemInfo.systemMemorySize < 8192)
                recommendations.Add("Low system memory - disable preview for better performance");

            if (recommendations.Count > 0)
            {
                container.Add(CreateHoyoToonSubsectionHeader("Performance Recommendations:"));
                foreach (var rec in recommendations)
                {
                    container.Add(CreateHoyoToonWarningBox(rec));
                }
            }

            contentView.Add(container);
        }

        private void CreateDebugSettingsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Expert mode toggle
            var expertModeToggle = new Toggle("Expert mode (show advanced options)");
            expertModeToggle.value = settings.expertModeOverride;
            expertModeToggle.RegisterValueChangedCallback(evt =>
            {
                settings.expertModeOverride = evt.newValue;
                SaveSettings();
            });
            container.Add(expertModeToggle);

            // Debug info
            container.Add(CreateHoyoToonSubsectionHeader("Debug Information:"));
            container.Add(CreateHoyoToonInfoRow("Package Path:", HoyoToonParseManager.GetPackagePath("com.meliverse.hoyotoon")));

            // HoyoToon data status
            var data = HoyoToonDataManager.Data;
            container.Add(CreateHoyoToonInfoRow("HoyoToon Data Loaded:", data != null ? "Yes" : "No",
                data != null ? Color.green : Color.red));

            if (data != null)
            {
                container.Add(CreateHoyoToonInfoRow("Available Shaders:", data.Shaders?.Count.ToString() ?? "0"));
                container.Add(CreateHoyoToonInfoRow("Texture Assignments:", data.TextureAssignments?.Count.ToString() ?? "0"));
                container.Add(CreateHoyoToonInfoRow("Material Settings:", data.MaterialSettings?.Count.ToString() ?? "0"));
            }

            contentView.Add(container);
        }

        private void CreateDataManagementSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Data cache info
            var cacheData = HoyoToonResourceManager.GetCacheData();
            container.Add(CreateHoyoToonInfoRow("Resource Cache:", cacheData != null ? "Loaded" : "Not loaded"));

            if (cacheData != null)
            {
                container.Add(CreateHoyoToonInfoRow("Cached Games:", cacheData.Games?.Count.ToString() ?? "0"));
                container.Add(CreateHoyoToonInfoRow("Last Update:", cacheData.LastUpdateCheck.ToString("yyyy-MM-dd HH:mm")));
            }

            // Data management actions
            var actionsContainer = new VisualElement();
            actionsContainer.style.flexDirection = FlexDirection.Row;
            actionsContainer.style.flexWrap = Wrap.Wrap;
            actionsContainer.style.marginTop = 10;

            var refreshDataBtn = CreateHoyoToonStyledButton("Refresh Data", () =>
            {
                RefreshHoyoToonData();
            }, new Color(0.2f, 0.6f, 0.8f));
            refreshDataBtn.style.marginRight = 5;
            refreshDataBtn.style.marginBottom = 5;

            var clearCacheBtn = CreateHoyoToonStyledButton("Clear Cache", () =>
            {
                if (EditorUtility.DisplayDialog("Confirm",
                    "Clear all cached data? This will force re-download from server.", "Yes", "Cancel"))
                {
                    ClearHoyoToonCache();
                }
            }, new Color(0.6f, 0.4f, 0.2f));
            clearCacheBtn.style.marginRight = 5;
            clearCacheBtn.style.marginBottom = 5;

            actionsContainer.Add(refreshDataBtn);
            actionsContainer.Add(clearCacheBtn);
            container.Add(actionsContainer);

            contentView.Add(container);
        }

        private void CreateSettingsActionsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.flexWrap = Wrap.Wrap;
            buttonsContainer.style.justifyContent = Justify.SpaceAround;

            // Save settings button
            var saveBtn = CreateHoyoToonStyledButton("Save Settings", () =>
            {
                SaveSettings();
                EditorUtility.DisplayDialog("Success", "Settings saved successfully!", "OK");
            }, new Color(0.2f, 0.6f, 0.8f));

            // Reset to defaults button
            var resetBtn = CreateHoyoToonStyledButton("Reset to Defaults", () =>
            {
                if (EditorUtility.DisplayDialog("Confirm",
                    "Reset all settings to default values?", "Yes", "Cancel"))
                {
                    ResetToDefaults();
                }
            }, new Color(0.6f, 0.4f, 0.2f));

            // Export settings button
            var exportBtn = CreateHoyoToonStyledButton("Export Settings", () =>
            {
                ExportSettings();
            }, new Color(0.5f, 0.3f, 0.7f));

            // Import settings button
            var importBtn = CreateHoyoToonStyledButton("Import Settings", () =>
            {
                ImportSettings();
            }, new Color(0.3f, 0.7f, 0.4f));

            buttonsContainer.Add(saveBtn);
            buttonsContainer.Add(resetBtn);
            buttonsContainer.Add(exportBtn);
            buttonsContainer.Add(importBtn);

            container.Add(buttonsContainer);

            // Settings file info
            var infoContainer = new VisualElement();
            infoContainer.style.marginTop = 15;
            infoContainer.style.paddingTop = 10;
            infoContainer.style.paddingBottom = 10;
            infoContainer.style.paddingLeft = 15;
            infoContainer.style.paddingRight = 15;
            infoContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            infoContainer.style.borderTopLeftRadius = 8;
            infoContainer.style.borderTopRightRadius = 8;
            infoContainer.style.borderBottomLeftRadius = 8;
            infoContainer.style.borderBottomRightRadius = 8;

            var infoLabel = new Label("Settings are automatically saved to EditorPrefs and persist between sessions.");
            infoLabel.style.fontSize = 11;
            infoLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            infoLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            infoLabel.style.whiteSpace = WhiteSpace.Normal;

            infoContainer.Add(infoLabel);
            container.Add(infoContainer);

            contentView.Add(container);
        }

        #region Settings Operations

        private void SaveSettings()
        {
            if (settings != null)
            {
                settings.SaveToEditorPrefs();
            }
        }

        private void RefreshHoyoToonData()
        {
            try
            {
                // Force refresh of HoyoToon data
                var newData = HoyoToonDataManager.GetHoyoToonData();
                if (newData != null)
                {
                    EditorUtility.DisplayDialog("Success", "HoyoToon data refreshed successfully!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Warning", "Failed to refresh data, using cached version", "OK");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to refresh data: {e.Message}", "OK");
            }
        }

        private void ClearHoyoToonCache()
        {
            try
            {
                // Clear resource cache
                HoyoToonResourceManager.SaveCacheData();
                EditorUtility.DisplayDialog("Success", "Cache cleared successfully!", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to clear cache: {e.Message}", "OK");
            }
        }

        private void ExportSettings()
        {
            try
            {
                var path = EditorUtility.SaveFilePanel("Export HoyoToon Settings", "", "HoyoToonSettings", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    var json = EditorJsonUtility.ToJson(settings, true);
                    System.IO.File.WriteAllText(path, json);
                    EditorUtility.DisplayDialog("Success", $"Settings exported to {path}", "OK");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to export settings: {e.Message}", "OK");
            }
        }

        private void ImportSettings()
        {
            try
            {
                var path = EditorUtility.OpenFilePanel("Import HoyoToon Settings", "", "json");
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    var json = System.IO.File.ReadAllText(path);
                    var importedSettings = new HoyoToonUISettings();
                    EditorJsonUtility.FromJsonOverwrite(json, importedSettings);

                    settings = importedSettings;
                    settings.SaveToEditorPrefs();
                    RefreshTabContent();

                    EditorUtility.DisplayDialog("Success", "Settings imported successfully!", "OK");
                }
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to import settings: {e.Message}", "OK");
            }
        }

        private void ClearAllCaches()
        {
            try
            {
                // Clear Unity's asset database cache
                AssetDatabase.Refresh();

                // Clear asset service caches
                HoyoToonAssetService.ClearCaches();

                // Clear editor prefs related to HoyoToon (be careful not to clear all)
                if (EditorPrefs.HasKey("HoyoToon_CachedData"))
                    EditorPrefs.DeleteKey("HoyoToon_CachedData");

                // Clear any temporary files
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HoyoToon");
                if (System.IO.Directory.Exists(tempPath))
                {
                    System.IO.Directory.Delete(tempPath, true);
                }

                EditorUtility.DisplayDialog("Success", "All caches cleared successfully!", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to clear caches: {e.Message}", "OK");
            }
        }

        private void ResetToDefaults()
        {
            try
            {
                settings = new HoyoToonUISettings(); // Create new default settings
                settings.SaveToEditorPrefs();
                RefreshTabContent();

                EditorUtility.DisplayDialog("Success", "Settings reset to defaults successfully!", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to reset settings: {e.Message}", "OK");
            }
        }

        #endregion
    }
}