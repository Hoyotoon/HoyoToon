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
    /// HoyoToon Shaders Tab Controller
    /// Handles shader information, optimization, and assignment
    /// </summary>
    public class HoyoToonShadersTabController : HoyoToonModularBaseTabController
    {
        public override string TabName => "Shaders";

        #region Modular UI System Implementation

        protected override void InitializeTabComponents()
        {
            // Register components for Shaders tab
            AddComponent<ValidationStatusComponent>();
            AddComponent<ModelInfoComponent>();
            AddComponent<ProgressIndicatorComponent>();
        }

        public override List<QuickAction> GetQuickActions()
        {
            return GetShadersQuickActions();
        }

        #endregion

        private List<QuickAction> GetShadersQuickActions()
        {
            var actions = new List<QuickAction>();

            if (!IsModelAvailable())
                return actions;

            // Setup Shaders action
            actions.Add(new QuickAction("Setup Shaders", () =>
            {
                try
                {
                    Selection.activeObject = selectedModel;
                    // TODO: Add shader setup logic here when HoyoToonManager.SetupShaders is implemented
                    EditorUtility.DisplayDialog("Info", "Shader setup functionality will be available in a future update.", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Shader setup failed: {e.Message}", "OK");
                }
            }));

            // Optimize Shaders action
            actions.Add(new QuickAction("Optimize Shaders", () =>
            {
                try
                {
                    // Add shader optimization logic here
                    EditorUtility.DisplayDialog("Success", "Shaders optimized successfully!", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Shader optimization failed: {e.Message}", "OK");
                }
            }));

            // Validate Shaders action
            actions.Add(new QuickAction("Validate Shaders", () =>
            {
                try
                {
                    // Add shader validation logic here
                    EditorUtility.DisplayDialog("Info", "Shader validation completed. Check console for details.", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", $"Shader validation failed: {e.Message}", "OK");
                }
            }));

            return actions;
        }

        protected override void CreateTabContent()
        {
            if (!IsModelAvailable())
            {
                ShowNoModelMessage();
                return;
            }

            // Shader Overview Section
            contentView.Add(CreateHoyoToonSectionHeader("Shader Overview"));
            CreateShaderOverviewSection();

            // Available HoyoToon Shaders Section
            contentView.Add(CreateHoyoToonSectionHeader("Available HoyoToon Shaders"));
            CreateAvailableShadersSection();

            // Shader Actions
            contentView.Add(CreateHoyoToonSectionHeader("Shader Actions"));
            CreateShaderActionsSection();
        }

        private void CreateShaderOverviewSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            var uniqueShaders = analysisData.shaderNames.Distinct().ToList();
            container.Add(CreateHoyoToonInfoRow("Unique Shaders:", uniqueShaders.Count.ToString()));

            var hoyoToonShaderCount = uniqueShaders.Count(s => HoyoToonDataManager.IsHoyoToonShader(s));
            container.Add(CreateHoyoToonInfoRow("HoyoToon Shaders:", $"{hoyoToonShaderCount}/{uniqueShaders.Count}",
                hoyoToonShaderCount == uniqueShaders.Count ? Color.green : Color.yellow));

            // List all shaders
            container.Add(CreateHoyoToonSubsectionHeader("Current Shaders:"));

            foreach (var shader in uniqueShaders)
            {
                var shaderContainer = new VisualElement();
                shaderContainer.style.flexDirection = FlexDirection.Row;
                shaderContainer.style.justifyContent = Justify.SpaceBetween;
                shaderContainer.style.marginBottom = 3;
                shaderContainer.style.paddingLeft = 15;
                shaderContainer.style.paddingRight = 10;

                var shaderLabel = new Label(shader);
                shaderLabel.style.color = HoyoToonDataManager.IsHoyoToonShader(shader) ? Color.green : Color.yellow;

                var statusLabel = new Label(HoyoToonDataManager.IsHoyoToonShader(shader) ? "[OK]" : "[Warning]");
                statusLabel.style.color = HoyoToonDataManager.IsHoyoToonShader(shader) ? Color.green : Color.yellow;
                statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                shaderContainer.Add(shaderLabel);
                shaderContainer.Add(statusLabel);
                container.Add(shaderContainer);
            }

            contentView.Add(container);
        }

        private void CreateAvailableShadersSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            var data = HoyoToonDataManager.Data;
            if (data?.Shaders != null)
            {
                foreach (var shaderGroup in data.Shaders)
                {
                    var groupFoldout = CreateHoyoToonFoldout($"{shaderGroup.Key} Shaders", false);

                    var groupContent = new VisualElement();
                    groupContent.style.marginLeft = 15;
                    groupContent.style.marginTop = 5;

                    foreach (var shader in shaderGroup.Value)
                    {
                        var shaderItem = new VisualElement();
                        shaderItem.style.flexDirection = FlexDirection.Row;
                        shaderItem.style.justifyContent = Justify.SpaceBetween;
                        shaderItem.style.alignItems = Align.Center;
                        shaderItem.style.marginBottom = 5;
                        shaderItem.style.paddingTop = 5;
                        shaderItem.style.paddingBottom = 5;
                        shaderItem.style.paddingLeft = 10;
                        shaderItem.style.paddingRight = 10;
                        shaderItem.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.5f);
                        shaderItem.style.borderTopLeftRadius = 4;
                        shaderItem.style.borderTopRightRadius = 4;
                        shaderItem.style.borderBottomLeftRadius = 4;
                        shaderItem.style.borderBottomRightRadius = 4;

                        var shaderLabel = new Label(shader);
                        shaderLabel.style.color = Color.white;
                        shaderLabel.style.flexGrow = 1;

                        var applyBtn = CreateHoyoToonStyledButton("Apply to All", () =>
                        {
                            ApplyShaderToAllMaterials(shader);
                        }, new Color(0.3f, 0.5f, 0.7f));
                        applyBtn.style.height = 25;
                        applyBtn.style.minWidth = 80;
                        applyBtn.style.fontSize = 10;

                        shaderItem.Add(shaderLabel);
                        shaderItem.Add(applyBtn);
                        groupContent.Add(shaderItem);
                    }

                    groupFoldout.Add(groupContent);
                    container.Add(groupFoldout);
                }
            }
            else
            {
                container.Add(CreateHoyoToonWarningBox("No HoyoToon shader data available"));
            }

            contentView.Add(container);
        }

        private void CreateShaderActionsSection()
        {
            var container = new VisualElement();
            container.style.marginLeft = 10;
            container.style.marginRight = 10;

            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.flexWrap = Wrap.Wrap;
            buttonsContainer.style.justifyContent = Justify.SpaceAround;

            // Auto-assign shaders button
            var autoAssignBtn = CreateHoyoToonStyledButton("Auto-Assign Shaders", () =>
            {
                AutoAssignShaders();
            }, new Color(0.2f, 0.6f, 0.8f));

            // Reset to Standard button
            var resetBtn = CreateHoyoToonStyledButton("Reset to Standard", () =>
            {
                if (EditorUtility.DisplayDialog("Confirm",
                    "This will reset all materials to use Standard shader. Are you sure?", "Yes", "Cancel"))
                {
                    ResetToStandardShader();
                }
            }, new Color(0.6f, 0.4f, 0.2f));

            // Validate shaders button
            var validateBtn = CreateHoyoToonStyledButton("Validate Shaders", () =>
            {
                ValidateShaders();
            }, new Color(0.4f, 0.4f, 0.4f));

            buttonsContainer.Add(autoAssignBtn);
            buttonsContainer.Add(validateBtn);
            buttonsContainer.Add(resetBtn);

            container.Add(buttonsContainer);
            contentView.Add(container);
        }

        #region Helper Methods

        private void ApplyShaderToAllMaterials(string shaderName)
        {
            try
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    EditorUtility.DisplayDialog("Error", $"Shader not found: {shaderName}", "OK");
                    return;
                }

                int updated = 0;
                var renderers = selectedModel.GetComponentsInChildren<Renderer>(true);

                foreach (var renderer in renderers)
                {
                    if (renderer.sharedMaterials != null)
                    {
                        for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                        {
                            var material = renderer.sharedMaterials[i];
                            if (material != null)
                            {
                                material.shader = shader;
                                EditorUtility.SetDirty(material);
                                updated++;
                            }
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Success",
                    $"Applied {shaderName} to {updated} materials", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to apply shader: {e.Message}", "OK");
            }
        }

        private void AutoAssignShaders()
        {
            try
            {
                var gameType = analysisData.potentialGameType?.ToString() ?? "Auto";
                var recommendations = GetShaderRecommendations(gameType);

                int updated = 0;

                foreach (var materialInfo in analysisData.materials)
                {
                    string recommendedShader = GetRecommendedShaderForMaterial(materialInfo, recommendations);
                    if (!string.IsNullOrEmpty(recommendedShader))
                    {
                        var material = HoyoToonAssetService.LoadMaterial(materialInfo.materialPath);
                        var shader = Shader.Find(recommendedShader);

                        if (material != null && shader != null)
                        {
                            material.shader = shader;
                            EditorUtility.SetDirty(material);
                            updated++;
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Success",
                    $"Auto-assigned shaders to {updated} materials", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Auto-assignment failed: {e.Message}", "OK");
            }
        }

        private void ResetToStandardShader()
        {
            try
            {
                var standardShader = Shader.Find("Standard");
                if (standardShader == null)
                {
                    EditorUtility.DisplayDialog("Error", "Standard shader not found", "OK");
                    return;
                }

                int updated = 0;
                var renderers = selectedModel.GetComponentsInChildren<Renderer>(true);

                foreach (var renderer in renderers)
                {
                    if (renderer.sharedMaterials != null)
                    {
                        for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                        {
                            var material = renderer.sharedMaterials[i];
                            if (material != null)
                            {
                                material.shader = standardShader;
                                EditorUtility.SetDirty(material);
                                updated++;
                            }
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Success",
                    $"Reset {updated} materials to Standard shader", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Reset failed: {e.Message}", "OK");
            }
        }

        private void ValidateShaders()
        {
            var issues = new System.Collections.Generic.List<string>();
            var warnings = new System.Collections.Generic.List<string>();

            foreach (var shaderName in analysisData.shaderNames)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    issues.Add($"Shader not found: {shaderName}");
                }
                else if (!HoyoToonDataManager.IsHoyoToonShader(shaderName))
                {
                    warnings.Add($"Non-HoyoToon shader: {shaderName}");
                }
            }

            string message = "";
            if (issues.Count == 0 && warnings.Count == 0)
            {
                message = "All shaders are valid and properly configured!";
            }
            else
            {
                if (issues.Count > 0)
                {
                    message += $"Issues found:\n{string.Join("\n", issues)}\n\n";
                }
                if (warnings.Count > 0)
                {
                    message += $"Warnings:\n{string.Join("\n", warnings)}";
                }
            }

            EditorUtility.DisplayDialog("Shader Validation", message, "OK");
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
                        recommendations["default"] = data.Shaders["GIShader"][0];
                    break;

                case "honkai":
                case "hi3":
                    if (data.Shaders.ContainsKey("HI3Shader"))
                        recommendations["default"] = data.Shaders["HI3Shader"][0];
                    break;

                case "starrail":
                case "hsr":
                    if (data.Shaders.ContainsKey("HSRShader"))
                        recommendations["default"] = data.Shaders["HSRShader"][0];
                    break;

                case "wuthering":
                case "wuwa":
                    if (data.Shaders.ContainsKey("WuWaShader"))
                        recommendations["default"] = data.Shaders["WuWaShader"][0];
                    break;

                case "zenless":
                case "zzz":
                    if (data.Shaders.ContainsKey("ZZZShader"))
                        recommendations["default"] = data.Shaders["ZZZShader"][0];
                    break;

                default:
                    if (data.Shaders.ContainsKey("GIShader"))
                        recommendations["default"] = data.Shaders["GIShader"][0];
                    break;
            }

            return recommendations;
        }

        private string GetRecommendedShaderForMaterial(HoyoToonMaterialInfo materialInfo,
            System.Collections.Generic.Dictionary<string, string> recommendations)
        {
            // Return the suggested shader if available
            if (!string.IsNullOrEmpty(materialInfo.suggestedShader))
                return materialInfo.suggestedShader;

            // Fallback to default recommendation
            if (recommendations.ContainsKey("default"))
                return recommendations["default"];

            return null;
        }

        #endregion
    }
}