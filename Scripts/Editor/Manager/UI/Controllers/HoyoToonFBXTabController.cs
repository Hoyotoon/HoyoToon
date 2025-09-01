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
    /// HoyoToon FBX Tab Controller - UI Driver Only
    /// Delegates all logic to HoyoToonMeshManager and uses modular UI components
    /// Follows strict separation of concerns: UI driver only, no business logic
    /// </summary>
    public class HoyoToonFBXTabController : HoyoToonModularBaseTabController
    {
        public override string TabName => "FBX";

        #region Modular UI System Implementation

        protected override void InitializeTabComponents()
        {
            // Register components for FBX tab - uses modular UI system
            AddComponent<ValidationStatusComponent>();
            AddComponent<ModelInfoComponent>();
            AddComponent<ProgressIndicatorComponent>();
            AddComponent<QuickActionsComponent>();
        }

        public override List<QuickAction> GetQuickActions()
        {
            var actions = new List<QuickAction>();

            if (!IsModelAvailable())
                return actions;

            // Setup FBX action - delegate to Manager
            actions.Add(new QuickAction("Setup FBX", () =>
            {
                HoyoToonMeshManager.SetupFBXFromUI(selectedModel, OnOperationComplete);
            }));

            // Generate Tangents action - delegate to Manager  
            if (analysisData?.needsTangentGeneration == true)
            {
                actions.Add(new QuickAction("Generate Tangents", () =>
                {
                    HoyoToonMeshManager.GenerateTangentsFromUI(selectedModel, OnOperationComplete);
                }));
            }

            // Reimport action - delegate to Manager
            if (analysisData?.canReimport == true)
            {
                actions.Add(new QuickAction("Reimport", () =>
                {
                    HoyoToonMeshManager.ReimportModelFromUI(selectedModel, OnOperationComplete);
                }));
            }

            // Reset Model action - delegate to Manager with confirmation
            actions.Add(new QuickAction("Reset Model", () =>
            {
                if (EditorUtility.DisplayDialog("Reset Model", "Are you sure you want to reset the model? This will remove all custom settings and revert to default.", "Yes", "No"))
                {
                    HoyoToonMeshManager.ResetModelFromUI(selectedModel, OnOperationComplete);
                }
            }));

            // Select in Project action - basic UI operation
            actions.Add(new QuickAction("Select in Project", () =>
            {
                HoyoToonMeshManager.SelectModelInProject(selectedModel);
            }));

            return actions;
        }

        #endregion

        protected override void CreateTabContent()
        {
            if (!IsModelAvailable())
            {
                ShowNoModelMessage();
                return;
            }

            // Use modular UI components for all content - no hardcoded UI logic

            // Basic model information section
            contentView.Add(CreateHoyoToonSectionHeader("FBX Model Information"));
            var modelInfoComponent = GetComponent<ModelInfoComponent>();
            if (modelInfoComponent != null)
            {
                var modelData = new Dictionary<string, object>
                {
                    { "analysisData", analysisData }
                };
                modelInfoComponent.UpdateComponent(modelData);
                contentView.Add(modelInfoComponent.RootElement);
            }

            // FBX-specific information using modular factory methods
            contentView.Add(CreateHoyoToonSectionHeader("Import Settings"));
            CreateFBXSpecificInfo();
        }

        /// <summary>
        /// Create FBX-specific information using modular UI factory
        /// </summary>
        private void CreateFBXSpecificInfo()
        {
            var container = HoyoToonUIFactory.CreateContainer(HoyoToonUIFactory.ContainerStyle.Default);
            HoyoToonUILayout.ApplyMargin(container, left: 10, right: 10);

            if (!analysisData.hasImportSettingsSet)
            {
                container.Add(HoyoToonUIFactory.CreateErrorBox("Could not access import settings"));
                contentView.Add(container);
                return;
            }

            // Import settings info using modular factory
            container.Add(HoyoToonUIFactory.CreateInfoRow("Read/Write Enabled:", analysisData.isReadWriteEnabled ? "Yes" : "No",
                analysisData.isReadWriteEnabled ? Color.white : Color.yellow));
            container.Add(HoyoToonUIFactory.CreateInfoRow("Mesh Compression:", analysisData.meshCompression));
            container.Add(HoyoToonUIFactory.CreateInfoRow("Rig Type:", analysisData.rigType,
                analysisData.isHumanoidRig ? Color.green : Color.yellow));

            // Rig status using modular factory
            if (analysisData.isHumanoidRig)
            {
                container.Add(HoyoToonUIFactory.CreateSuccessBox("Model has Humanoid rig - compatible with HoyoToon"));
            }
            else
            {
                container.Add(HoyoToonUIFactory.CreateWarningBox("Model is not rigged as Humanoid. HoyoToon works best with Humanoid rigs."));
            }

            contentView.Add(container);
        }

        /// <summary>
        /// Callback for operation completion - standardized UI feedback
        /// </summary>
        private void OnOperationComplete(bool success, string message)
        {
            if (success)
            {
                // Trigger UI refresh through manager
                HoyoToonUIManager.Instance?.InvalidateAnalysisCache();
                HoyoToonUIManager.Instance?.ForceAnalysisRefresh();

                EditorUtility.DisplayDialog("Success", message, "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", message, "OK");
            }
        }
    }
}