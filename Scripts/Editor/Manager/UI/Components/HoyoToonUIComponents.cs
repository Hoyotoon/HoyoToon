using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.UI.Components
{
    /// <summary>
    /// Component for displaying model validation status with colored indicators
    /// </summary>
    public class ValidationStatusComponent : HoyoToon.UI.Core.HoyoToonUIComponent
    {
        #region Properties

        public override string ComponentId => "ValidationStatus";

        #endregion

        #region Fields

        private VisualElement validationContainer;
        private Dictionary<string, Label> validationIndicators = new Dictionary<string, Label>();

        #endregion

        #region Component Implementation

        protected override void CreateComponentUI()
        {
            validationContainer = HoyoToon.UI.Core.HoyoToonUIFactory.CreateContainer(HoyoToon.UI.Core.HoyoToonUIFactory.ContainerStyle.Panel);

            HoyoToon.UI.Core.HoyoToonUILayout.MakeFlexRow(validationContainer, Justify.SpaceBetween);
            HoyoToon.UI.Core.HoyoToonUILayout.SetDimensions(validationContainer, minHeight: 40);
            HoyoToon.UI.Core.HoyoToonUILayout.PreventShrink(validationContainer);

            // Create validation indicators
            CreateValidationIndicator("Model", "● Model");
            CreateValidationIndicator("Rig", "● Rig");
            CreateValidationIndicator("Materials", "● Materials");
            CreateValidationIndicator("Textures", "● Textures");
            CreateValidationIndicator("Resources", "● Resources");

            rootElement.Add(validationContainer);
        }

        protected override void RefreshComponentUI()
        {
            if (!HasData("analysisData"))
                return;

            var analysisData = GetData<HoyoToonModelAnalysisData>("analysisData");

            UpdateValidationStatus("Model", analysisData.hasValidModel ? ValidationStatus.Success : ValidationStatus.Error,
                analysisData.hasValidModel ? "Model Loaded" : "No Model Selected");

            // Rig status
            ValidationStatus rigStatus;
            string rigText;
            if (!analysisData.isHoyo2VRCConverted)
            {
                rigStatus = ValidationStatus.Error;
                rigText = "Needs Hoyo2VRC";
            }
            else if (analysisData.isHumanoidRig)
            {
                rigStatus = ValidationStatus.Success;
                rigText = "Humanoid Ready";
            }
            else
            {
                rigStatus = ValidationStatus.Warning;
                rigText = "Generic - Setup Needed";
            }
            UpdateValidationStatus("Rig", rigStatus, rigText);

            // Materials status
            ValidationStatus materialsStatus;
            string materialsText;
            if (analysisData.hasMaterials && analysisData.hasCorrectShaders)
            {
                materialsStatus = ValidationStatus.Success;
                materialsText = "Materials Ready";
            }
            else if (analysisData.hasMaterials)
            {
                materialsStatus = ValidationStatus.Warning;
                materialsText = "Mat's Need Generation";
            }
            else
            {
                materialsStatus = ValidationStatus.Error;
                materialsText = "Missing Materials";
            }
            UpdateValidationStatus("Materials", materialsStatus, materialsText);

            // Textures status
            UpdateValidationStatus("Textures",
                analysisData.hasTextures ? ValidationStatus.Success : ValidationStatus.Warning,
                analysisData.hasTextures ? "Textures Ready" : "Textures Need Configuration");

            // Resources status
            bool hasAllResources = GetData<bool>("hasAllResources", false);
            UpdateValidationStatus("Resources",
                hasAllResources ? ValidationStatus.Success : ValidationStatus.Warning,
                hasAllResources ? "Resources Ready" : "Need Download");
        }

        #endregion

        #region Helper Methods

        private void CreateValidationIndicator(string key, string text)
        {
            var indicator = new Label(text);
            indicator.style.fontSize = 12;
            indicator.style.unityFontStyleAndWeight = FontStyle.Bold;
            indicator.style.marginLeft = 5;
            indicator.style.marginRight = 5;
            indicator.style.color = HoyoToon.UI.Core.HoyoToonUIFactory.Colors.StatusNeutral;

            validationIndicators[key] = indicator;
            validationContainer.Add(indicator);
        }

        private void UpdateValidationStatus(string key, ValidationStatus status, string text)
        {
            if (!validationIndicators.TryGetValue(key, out var indicator))
                return;

            indicator.text = $"● {text}";

            Color statusColor;
            switch (status)
            {
                case ValidationStatus.Success:
                    statusColor = HoyoToon.UI.Core.HoyoToonUIFactory.Colors.StatusReady;
                    break;
                case ValidationStatus.Warning:
                    statusColor = HoyoToon.UI.Core.HoyoToonUIFactory.Colors.StatusWarning;
                    break;
                case ValidationStatus.Error:
                    statusColor = HoyoToon.UI.Core.HoyoToonUIFactory.Colors.StatusError;
                    break;
                default:
                    statusColor = HoyoToon.UI.Core.HoyoToonUIFactory.Colors.StatusNeutral;
                    break;
            }

            indicator.style.color = statusColor;
        }

        #endregion

        #region Enums

        public enum ValidationStatus
        {
            Success,
            Warning,
            Error,
            Neutral
        }

        #endregion
    }

    /// <summary>
    /// Component for displaying quick actions with dynamic button generation
    /// </summary>
    public class QuickActionsComponent : HoyoToon.UI.Core.HoyoToonUIComponent
    {
        #region Properties

        public override string ComponentId => "QuickActions";

        #endregion

        #region Fields

        private VisualElement headerContainer;
        private VisualElement buttonsContainer;

        #endregion

        #region Component Implementation

        protected override void CreateComponentUI()
        {
            var container = HoyoToon.UI.Core.HoyoToonUIFactory.CreateContainer(HoyoToon.UI.Core.HoyoToonUIFactory.ContainerStyle.Panel);

            // Header
            headerContainer = new VisualElement();
            HoyoToon.UI.Core.HoyoToonUILayout.MakeFlexRow(headerContainer, Justify.FlexStart, Align.Center);
            HoyoToon.UI.Core.HoyoToonUILayout.ApplyMargin(headerContainer, bottom: 8);

            var headerLabel = HoyoToon.UI.Core.HoyoToonUIFactory.CreateLabel("Quick Actions", 14, HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextPrimary);
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.marginRight = 10;

            headerContainer.Add(headerLabel);

            // Buttons container
            buttonsContainer = new VisualElement();
            HoyoToon.UI.Core.HoyoToonUILayout.MakeFlexRow(buttonsContainer, Justify.FlexStart, Align.Stretch, Wrap.Wrap);

            container.Add(headerContainer);
            container.Add(buttonsContainer);
            rootElement.Add(container);
        }

        protected override void RefreshComponentUI()
        {
            buttonsContainer.Clear();

            var quickActions = GetData<List<HoyoToon.UI.Core.QuickAction>>("quickActions");
            if (quickActions != null && quickActions.Count > 0)
            {
                foreach (var action in quickActions)
                {
                    var button = CreateQuickActionButton(action);
                    buttonsContainer.Add(button);
                }
            }
            else
            {
                var noActionsLabel = HoyoToon.UI.Core.HoyoToonUIFactory.CreateLabel(
                    "No quick actions available for this tab", 12, HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextMuted);
                noActionsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                noActionsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                HoyoToon.UI.Core.HoyoToonUILayout.ApplyMargin(noActionsLabel, top: 5, bottom: 5);
                buttonsContainer.Add(noActionsLabel);
            }

            // Fire component event to notify that quick actions were refreshed
            HoyoToon.UI.Core.HoyoToonUIComponentManager.Instance.FireComponentEvent(
                ComponentId, "QuickActionsRefreshed", new Dictionary<string, object>
                {
                    { "actionCount", quickActions?.Count ?? 0 }
                });
        }

        #endregion

        #region Helper Methods

        private Button CreateQuickActionButton(HoyoToon.UI.Core.QuickAction action)
        {
            var button = HoyoToon.UI.Core.HoyoToonUIFactory.CreateStyledButton(action.Label, () =>
            {
                action.Action?.Invoke();
                // Fire component event to notify UI manager
                HoyoToon.UI.Core.HoyoToonUIComponentManager.Instance.FireComponentEvent(
                    ComponentId, "QuickActionExecuted", new Dictionary<string, object>
                    {
                        { "actionLabel", action.Label }
                    });
            }, HoyoToon.UI.Core.HoyoToonUIFactory.Colors.Primary, 28);

            button.SetEnabled(action.IsEnabled);
            button.style.minWidth = 100;
            button.style.maxWidth = 150;
            button.style.flexGrow = 1;
            button.style.marginRight = 5;
            button.style.marginBottom = 3;
            button.style.fontSize = 11;

            if (!string.IsNullOrEmpty(action.Tooltip))
            {
                button.tooltip = action.Tooltip;
            }

            return button;
        }

        #endregion
    }

    /// <summary>
    /// Component for displaying progress indicators with color coding
    /// </summary>
    public class ProgressIndicatorComponent : HoyoToon.UI.Core.HoyoToonUIComponent
    {
        #region Properties

        public override string ComponentId => "ProgressIndicator";

        #endregion

        #region Fields

        private Label progressLabel;
        private Label summaryLabel;

        #endregion

        #region Component Implementation

        protected override void CreateComponentUI()
        {
            var container = HoyoToon.UI.Core.HoyoToonUIFactory.CreateContainer(HoyoToon.UI.Core.HoyoToonUIFactory.ContainerStyle.Panel);

            progressLabel = HoyoToon.UI.Core.HoyoToonUIFactory.CreateLabel("", 16);
            progressLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            progressLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            summaryLabel = HoyoToon.UI.Core.HoyoToonUIFactory.CreateLabel("", 12, HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextSecondary);
            summaryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            HoyoToon.UI.Core.HoyoToonUILayout.ApplyMargin(summaryLabel, top: 5);

            container.Add(progressLabel);
            container.Add(summaryLabel);
            rootElement.Add(container);
        }

        protected override void RefreshComponentUI()
        {
            var percentage = GetData<int>("percentage", 0);
            var label = GetData<string>("label", "Progress");
            var summary = GetData<string>("summary", "");

            progressLabel.text = $"{label}: {percentage}%";
            summaryLabel.text = summary;

            // Color based on progress
            Color progressColor;
            if (percentage >= 80)
                progressColor = HoyoToon.UI.Core.HoyoToonUIFactory.Colors.StatusReady;
            else if (percentage >= 60)
                progressColor = HoyoToon.UI.Core.HoyoToonUIFactory.Colors.StatusWarning;
            else
                progressColor = HoyoToon.UI.Core.HoyoToonUIFactory.Colors.StatusError;

            progressLabel.style.color = progressColor;
        }

        #endregion
    }

    /// <summary>
    /// Component for displaying model information in a structured format
    /// </summary>
    public class ModelInfoComponent : HoyoToon.UI.Core.HoyoToonUIComponent
    {
        #region Properties

        public override string ComponentId => "ModelInfo";

        #endregion

        #region Fields

        private VisualElement infoContainer;

        #endregion

        #region Component Implementation

        protected override void CreateComponentUI()
        {
            infoContainer = HoyoToon.UI.Core.HoyoToonUIFactory.CreateContainer(HoyoToon.UI.Core.HoyoToonUIFactory.ContainerStyle.Default);
            HoyoToon.UI.Core.HoyoToonUILayout.ApplyMargin(infoContainer, left: 10, right: 10);
            rootElement.Add(infoContainer);
        }

        protected override void RefreshComponentUI()
        {
            infoContainer.Clear();

            var analysisData = GetData<HoyoToonModelAnalysisData>("analysisData");
            if (analysisData == null)
            {
                // Show placeholder message using modular factory
                var noDataLabel = HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoBox(
                    "No model analysis data available",
                    HoyoToon.UI.Core.HoyoToonUIFactory.Colors.BackgroundMedium,
                    HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextMuted);
                infoContainer.Add(noDataLabel);
                return;
            }

            // Basic information using modular factory
            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("Model Name:", analysisData.modelName));
            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("File Path:", analysisData.modelPath));
            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("File Size:", analysisData.fileSizeFormatted));
            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("Creation Date:", analysisData.creationDate.ToString("yyyy-MM-dd HH:mm")));

            // Mesh information with warning colors using modular factory
            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("Vertices:", analysisData.vertexCount.ToString(),
                analysisData.vertexCount > 50000 ? HoyoToon.UI.Core.HoyoToonUIFactory.Colors.Warning : HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextPrimary));
            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("Triangles:", analysisData.triangleCount.ToString(),
                analysisData.triangleCount > 100000 ? HoyoToon.UI.Core.HoyoToonUIFactory.Colors.Warning : HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextPrimary));
            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("Blendshapes:", analysisData.blendshapeCount.ToString(),
                analysisData.blendshapeCount > 100 ? HoyoToon.UI.Core.HoyoToonUIFactory.Colors.Warning : HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextPrimary));

            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("Total Bones:", analysisData.boneCount.ToString(),
                analysisData.boneCount > 200 ? HoyoToon.UI.Core.HoyoToonUIFactory.Colors.Warning : HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextPrimary));
            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("Materials:", analysisData.materialCount.ToString(),
                analysisData.materialCount > 20 ? HoyoToon.UI.Core.HoyoToonUIFactory.Colors.Warning : HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextPrimary));
            infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("Textures:", analysisData.textures.Count.ToString()));

            if (analysisData.totalTextureMemory > 0)
            {
                infoContainer.Add(HoyoToon.UI.Core.HoyoToonUIFactory.CreateInfoRow("Texture Memory:", analysisData.textureMemoryFormatted,
                    analysisData.totalTextureMemory > 100 * 1024 * 1024 ? HoyoToon.UI.Core.HoyoToonUIFactory.Colors.Warning : HoyoToon.UI.Core.HoyoToonUIFactory.Colors.TextPrimary));
            }

            // Fire component event to notify that model info was refreshed  
            HoyoToon.UI.Core.HoyoToonUIComponentManager.Instance.FireComponentEvent(
                ComponentId, "ModelInfoRefreshed", new Dictionary<string, object>
                {
                    { "modelName", analysisData.modelName },
                    { "vertexCount", analysisData.vertexCount },
                    { "triangleCount", analysisData.triangleCount }
                });
        }

        #endregion
    }
}