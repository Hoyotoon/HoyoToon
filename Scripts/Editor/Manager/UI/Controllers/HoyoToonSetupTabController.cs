using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using HoyoToon.UI.Core;
using HoyoToon.UI.Components;

namespace HoyoToon
{
    /// <summary>
    /// HoyoToon Setup Tab Controller
    /// Handles HoyoToon detection, setup progress, and requirements
    /// Updated to use the new modular UI system
    /// </summary>
    public class HoyoToonSetupTabController : HoyoToonModularBaseTabController
    {
        public override string TabName => "Setup";

        #region Modular UI System Implementation

        protected override void InitializeTabComponents()
        {
            // Register components for Setup tab
            AddComponent<ValidationStatusComponent>();
            AddComponent<ModelInfoComponent>();
            AddComponent<ProgressIndicatorComponent>();
        }

        public override List<QuickAction> GetQuickActions()
        {
            var actions = new List<QuickAction>();

            if (!IsModelAvailable())
                return actions;

            // Generate Materials action
            actions.Add(new QuickAction("Generate Materials", () =>
            {
                try
                {
                    if (selectedModel == null)
                    {
                        EditorUtility.DisplayDialog("Error", "No model selected. Please select an FBX model first.", "OK");
                        return;
                    }

                    // Set selection to the model so material manager can find the JSON files
                    Selection.activeObject = selectedModel;

                    // First generate materials from JSON files
                    HoyoToonMaterialManager.GenerateMaterialsFromJson();

                    // Then setup FBX which will automatically find and remap the generated materials
                    HoyoToonManager.SetupFBX();

                    // Trigger immediate re-analysis and UI refresh
                    HoyoToonUIManager.Instance?.ForceAnalysisRefresh();

                    EditorUtility.DisplayDialog("Success", "Materials generated and FBX setup completed successfully!", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Material generation failed: {e.Message}", "OK");
                }
            }));

            // Setup FBX action
            actions.Add(new QuickAction("Setup FBX", () =>
            {
                try
                {
                    if (selectedModel == null)
                    {
                        EditorUtility.DisplayDialog("Error", "No model selected. Please select an FBX model first.", "OK");
                        return;
                    }

                    Selection.activeObject = selectedModel;
                    HoyoToonManager.SetupFBX();

                    // Trigger immediate re-analysis and UI refresh
                    HoyoToonUIManager.Instance?.ForceAnalysisRefresh();

                    EditorUtility.DisplayDialog("Success", "FBX setup completed successfully!", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"FBX setup failed: {e.Message}", "OK");
                }
            }));

            // Generate Tangents action
            if (analysisData?.needsTangentGeneration == true)
            {
                actions.Add(new QuickAction("Generate Tangents", () =>
                {
                    try
                    {
                        if (selectedModel == null)
                        {
                            EditorUtility.DisplayDialog("Error", "No model selected. Please select an FBX model first.", "OK");
                            return;
                        }

                        HoyoToonMeshManager.GenTangents(selectedModel);

                        // Trigger immediate re-analysis and UI refresh
                        HoyoToonUIManager.Instance?.ForceAnalysisRefresh();

                        EditorUtility.DisplayDialog("Success", "Tangents generated successfully!", "OK");
                    }
                    catch (System.Exception e)
                    {
                        EditorUtility.DisplayDialog("Error", $"Tangent generation failed: {e.Message}", "OK");
                    }
                }));
            }

            return actions;
        }

        protected override void CreateTabContent()
        {
            if (!IsModelAvailable())
            {
                ShowNoModelMessage();
                return;
            }

            // Setup Progress Overview Section
            contentView.Add(CreateHoyoToonSectionHeader("Setup Progress"));
            CreateSetupProgressSection();

            // HoyoToon Detection Section
            contentView.Add(CreateHoyoToonSectionHeader("HoyoToon Detection"));
            CreateHoyoToonDetectionSection();

            // Setup Requirements Section - only show if there are actual requirements to display
            var hasIssues = analysisData.issues.Count > 0;
            var hasValidWarnings = analysisData.warnings.Any(warning => !string.IsNullOrEmpty(ConvertWarningToSetupText(warning)));

            if (hasIssues || hasValidWarnings)
            {
                contentView.Add(CreateHoyoToonSectionHeader("Setup Requirements"));
                CreateSetupRequirementsSection();
            }
        }

        private void CreateSetupProgressSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Progress display
            var progressContainer = new VisualElement();
            progressContainer.style.marginTop = 10;
            progressContainer.style.marginBottom = 15;
            progressContainer.style.paddingTop = 15;
            progressContainer.style.paddingBottom = 15;
            progressContainer.style.paddingLeft = 20;
            progressContainer.style.paddingRight = 20;
            progressContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            progressContainer.style.borderTopLeftRadius = 8;
            progressContainer.style.borderTopRightRadius = 8;
            progressContainer.style.borderBottomLeftRadius = 8;
            progressContainer.style.borderBottomRightRadius = 8;

            var progressLabel = new Label($"Setup Progress: {analysisData.preparationProgress}%");
            progressLabel.style.fontSize = 18;
            progressLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            progressLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            // Color based on progress
            if (analysisData.preparationProgress >= 80)
                progressLabel.style.color = new Color(0.4f, 0.8f, 0.4f);
            else if (analysisData.preparationProgress >= 60)
                progressLabel.style.color = new Color(0.8f, 0.8f, 0.4f);
            else
                progressLabel.style.color = new Color(0.8f, 0.4f, 0.4f);

            progressContainer.Add(progressLabel);

            // Progress bar
            var progressBarContainer = new VisualElement();
            progressBarContainer.style.height = 8;
            progressBarContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            progressBarContainer.style.borderTopLeftRadius = 4;
            progressBarContainer.style.borderTopRightRadius = 4;
            progressBarContainer.style.borderBottomLeftRadius = 4;
            progressBarContainer.style.borderBottomRightRadius = 4;
            progressBarContainer.style.marginTop = 10;

            var progressBar = new VisualElement();
            progressBar.style.height = 8;
            progressBar.style.width = new Length(analysisData.preparationProgress, LengthUnit.Percent);
            progressBar.style.backgroundColor = progressLabel.style.color.value;
            progressBar.style.borderTopLeftRadius = 4;
            progressBar.style.borderTopRightRadius = 4;
            progressBar.style.borderBottomLeftRadius = 4;
            progressBar.style.borderBottomRightRadius = 4;

            progressBarContainer.Add(progressBar);
            progressContainer.Add(progressBarContainer);

            var summaryLabel = new Label(GetSetupStatusText());
            summaryLabel.style.fontSize = 14;
            summaryLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            summaryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            summaryLabel.style.marginTop = 10;
            progressContainer.Add(summaryLabel);

            container.Add(progressContainer);
            contentView.Add(container);
        }

        private void CreateHoyoToonDetectionSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Hoyo2VRC conversion check
            if (analysisData.isHoyo2VRCConverted)
            {
                container.Add(CreateHoyoToonSuccessBox("Model has been converted with Hoyo2VRC"));
            }
            else
            {
                container.Add(CreateHoyoToonErrorBox("Model must be converted with Hoyo2VRC for it to be fully compatible with HoyoToon."));
            }

            // Game detection
            if (analysisData.potentialGameType.HasValue)
            {
                container.Add(CreateHoyoToonInfoRow("Detected Game:", analysisData.potentialGameType.Value.ToString(),
                    new Color(0.4f, 0.8f, 0.4f)));
            }
            else
            {
                container.Add(CreateHoyoToonInfoRow("Detected Game:", "Unknown", Color.yellow));
            }

            // Character detection
            if (!string.IsNullOrEmpty(analysisData.detectedCharacter))
            {
                container.Add(CreateHoyoToonInfoRow("Detected Character:", analysisData.detectedCharacter,
                    new Color(0.4f, 0.8f, 0.4f)));
            }

            // Body type
            if (!string.IsNullOrEmpty(analysisData.bodyType))
            {
                container.Add(CreateHoyoToonInfoRow("Body Type:", analysisData.bodyType));
            }

            // Special features
            if (analysisData.isHI3NewFace)
            {
                container.Add(CreateHoyoToonSuccessBox("HI3 New Face system detected"));
            }

            if (analysisData.isPrenatlaNPrenodkrai)
            {
                container.Add(CreateHoyoToonSuccessBox("Genshin Impact character detected"));
            }

            contentView.Add(container);
        }

        private void CreateSetupRequirementsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            // Combine all setup items into a single unified list
            var setupItems = new List<(string text, Color color)>();

            // Add critical items (former issues)
            foreach (var issue in analysisData.issues)
            {
                string setupText = ConvertIssueToSetupText(issue);
                setupItems.Add((setupText, new Color(0.8f, 0.4f, 0.4f))); // Red for critical
            }

            // Add optimization items (former warnings)
            foreach (var warning in analysisData.warnings)
            {
                string setupText = ConvertWarningToSetupText(warning);
                if (!string.IsNullOrEmpty(setupText))
                {
                    setupItems.Add((setupText, new Color(0.7f, 0.5f, 0.2f))); // Orange for optimization
                }
            }

            if (setupItems.Count > 0)
            {
                // Single header explaining what Full Setup will do
                var headerText = setupItems.Any(item => item.color.r > 0.7f && item.color.g < 0.5f)
                    ? "Full Setup will handle the following requirements and optimizations:"
                    : "Full Setup will apply the following optimizations:";

                container.Add(CreateHoyoToonSubsectionHeader(headerText));

                // Add all items in a unified list
                foreach (var (text, color) in setupItems)
                {
                    container.Add(CreateHoyoToonInfoBox(text, color));
                }

                // Add helpful footer
                var footerText = new Label("Click 'Full Setup' below to automatically configure all items listed above.");
                footerText.style.fontSize = 12;
                footerText.style.color = new Color(0.6f, 0.6f, 0.6f);
                footerText.style.marginTop = 10;
                footerText.style.marginLeft = 15;
                footerText.style.unityFontStyleAndWeight = FontStyle.Italic;
                container.Add(footerText);
            }

            contentView.Add(container);
        }

        private string GetSetupStatusText()
        {
            if (analysisData.preparationProgress >= 100)
                return "[Complete] Setup complete! Your model is ready for use. Feel free to add it to the scene.";
            else if (analysisData.preparationProgress >= 80)
                return "[Ready] Almost ready! Just a few more steps needed. Run Full Setup to finish configuration.";
            else if (analysisData.preparationProgress >= 60)
                return "[Nearly Ready] Nearly complete. Most requirements are met. Use Full Setup to finish the remaining tasks.";
            else if (analysisData.preparationProgress == 0)
                return "[Warning] Model must be converted with Hoyo2VRC first before HoyoToon setup can begin.";
            else
                return "[Setup Required] Setup required. Your model needs configuration. Use Full Setup to prepare it for HoyoToon.";
        }

        private string ConvertIssueToSetupText(string issue)
        {
            // Convert negative issue language to positive setup language
            if (issue.Contains("No materials found"))
                return "[Materials] Materials will be generated from JSON files";
            if (issue.Contains("PREREQUISITE") && issue.Contains("Hoyo2VRC"))
                return "[Blocked] Model must be converted with Hoyo2VRC first before HoyoToon setup can begin";
            if (issue.Contains("No valid asset"))
                return "[Validation] Asset validation - model is ready for processing";

            return $"[Setup] Setup will handle: {issue}";
        }

        private string ConvertWarningToSetupText(string warning)
        {
            // Convert warning language to setup language
            if (warning.Contains("No HoyoToon shaders"))
                return "[Shaders] Shaders will be assigned during material generation";
            if (warning.Contains("No textures found"))
                return "[Textures] Textures will be properly configured";
            if (warning.Contains("Tangents need to be generated"))
                return "[Tangents] Tangents will be generated for proper lighting";
            if (warning.Contains("High triangle count"))
                return "[Optimization] Performance optimization available";
            if (warning.Contains("High texture memory usage") || warning.Contains("texture memory") || warning.Contains("memory usage"))
                return null; // Skip all texture memory warnings in setup tab
            if (warning.Contains("No skinned mesh renderers") || warning.Contains("No renderers"))
                return null; // Skip renderer warnings - not supported model if no renderers

            return $"[Setup] Setup will handle: {warning}";
        }

        #endregion

    }
}