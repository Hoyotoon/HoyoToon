using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Runtime.Scene.Placement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.UI.Manager.Modules
{
    internal sealed class SetupModule : ManagerModuleBase
    {
        public override string Id => "setup";
        public override string DisplayName => "Setup";
        public override int Order => 0;

        protected override string Description => "Setup";

        public override VisualElement CreateContent(ModuleContext context)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ht-column");
            root.AddToClassList("ht-gap-8");
            root.AddToClassList("ht-module-root");
            root.Add(CreateAddModelCard(context));
            return root;
        }

        protected override IEnumerable<VisualElement> BuildCards(ModuleContext context)
        {
            yield break;
        }

        private VisualElement CreateAddModelCard(ModuleContext context)
        {
            VisualElement card = CreateCard();
            AddCardHeader(card, "Add Model");

            VisualElement actionRow = new VisualElement();
            actionRow.AddToClassList("ht-row");
            actionRow.AddToClassList("ht-gap-8");
            actionRow.AddToClassList("ht-main-model-row");
            actionRow.style.flexWrap = Wrap.Wrap;

            ObjectField modelField = new ObjectField("Add Model")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false,
                value = context != null ? context.SelectedModelAsset : null
            };
            modelField.AddToClassList("ht-field");
            modelField.AddToClassList("ht-main-model-field");
            OnboardingTargetRegistry.RegisterVisualElement("Setup.ModelInputField", modelField, "Setup model input", "Setup");

            HoyoToonManagerWindow window = context != null ? context.Window as HoyoToonManagerWindow : null;
            modelField.RegisterValueChangedCallback(evt =>
            {
                GameObject model = evt.newValue as GameObject;
                OnboardingSignals.RecordValueChanged("Setup.ModelInputField", model != null ? model.name : string.Empty);
                window?.SetSelectedModelAsset(model);
            });
            actionRow.Add(modelField);

            Button autoSetupButton = new Button(() =>
            {
                OnboardingSignals.ClearOperationSimulation(OnboardingOperationKind.Setup);
                OnboardingSignals.RecordAction("Setup.AutoSetupButton");
                window?.RunAutoSetupForSelectedModel();
            })
            {
                text = "Auto Setup"
            };
            autoSetupButton.AddToClassList("ht-btn-primary");
            autoSetupButton.AddToClassList("ht-main-action-button");
            autoSetupButton.SetEnabled(context != null && context.HasBatchModels);
            OnboardingTargetRegistry.RegisterVisualElement("Setup.AutoSetupButton", autoSetupButton, "Auto Setup button", "Setup");
            actionRow.Add(autoSetupButton);

            card.Add(actionRow);

            VisualElement batchRow = new VisualElement();
            batchRow.AddToClassList("ht-row");
            batchRow.AddToClassList("ht-gap-8");
            batchRow.AddToClassList("ht-main-model-row");
            batchRow.style.flexWrap = Wrap.Wrap;

            ObjectField folderField = new ObjectField("Folder")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false,
                value = context != null ? context.SelectedModelFolder : null
            };
            folderField.AddToClassList("ht-field");
            folderField.AddToClassList("ht-main-model-field");
            folderField.RegisterValueChangedCallback(evt => window?.SetSelectedModelFolder(evt.newValue as DefaultAsset));
            batchRow.Add(folderField);

            card.Add(batchRow);
            card.Add(CreateDivider());
            VisualElement modelInfoSection = CreateModelInfoSection(context);
            OnboardingTargetRegistry.RegisterVisualElement("Setup.ModelList", modelInfoSection, "Setup model list", "Setup");
            OnboardingTargetRegistry.RegisterVisualElement("Setup.ProgressBar", card, "Setup progress", "Setup");
            card.Add(modelInfoSection);

            if (context != null && !string.IsNullOrWhiteSpace(context.ValidationMessage))
            {
                card.Add(CreateHelpBox(context.ValidationMessage, HelpBoxMessageType.Warning));
            }

            return card;
        }

        private VisualElement CreateModelInfoSection(ModuleContext context)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("ht-column");
            section.AddToClassList("ht-gap-8");
            section.style.marginTop = 4f;

            BatchModelDetectionInfo[] detections = context != null
                ? context.BatchModelDetections ?? Array.Empty<BatchModelDetectionInfo>()
                : Array.Empty<BatchModelDetectionInfo>();

            if (detections.Length <= 0)
            {
                Label emptyLabel = new Label("No models queued.");
                emptyLabel.AddToClassList("ht-caption");
                section.Add(emptyLabel);
                return section;
            }

            Label queueCountLabel = new Label(detections.Length == 1 ? "Queued: 1 model" : "Queued: " + detections.Length + " models");
            queueCountLabel.AddToClassList("ht-caption");
            section.Add(queueCountLabel);

            HoyoToonManagerWindow window = context != null ? context.Window as HoyoToonManagerWindow : null;

            foreach (BatchModelDetectionInfo detection in detections)
            {
                section.Add(CreateDetectionCard(detection, window));
            }

            return section;
        }

        private VisualElement CreateDetectionCard(BatchModelDetectionInfo detection, HoyoToonManagerWindow window)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("ht-column");
            card.AddToClassList("ht-gap-8");
            card.style.paddingTop = 2f;
            card.style.paddingBottom = 2f;

            VisualElement headerRow = new VisualElement();
            headerRow.AddToClassList("ht-row");
            headerRow.AddToClassList("ht-gap-8");
            headerRow.style.alignItems = Align.Center;

            Label sourceLabel = new Label(Path.GetFileName(detection != null ? detection.AssetPath : string.Empty));
            sourceLabel.AddToClassList("ht-caption");
            sourceLabel.style.flexGrow = 1f;
            sourceLabel.style.flexShrink = 1f;
            sourceLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            sourceLabel.tooltip = detection != null ? detection.AssetPath : string.Empty;
            headerRow.Add(sourceLabel);

            if (detection != null && detection.IsConverted)
            {
                Label convertedLabel = CreateStatusPill("Converted", true);
                headerRow.Add(convertedLabel);
            }

            Button removeButton = new Button(() => window?.RemoveQueuedModelAsset(detection != null ? detection.ModelAsset : null))
            {
                text = "Remove"
            };
            removeButton.AddToClassList("ht-btn-secondary");
            removeButton.AddToClassList("ht-main-action-button");
            removeButton.style.minWidth = 76f;
            removeButton.SetEnabled(detection != null && detection.ModelAsset != null);
            headerRow.Add(removeButton);

            card.Add(headerRow);
            card.Add(CreateQueuedDetectionStatGrid(detection));

            return card;
        }

        private static VisualElement CreateQueuedDetectionStatGrid(BatchModelDetectionInfo detection)
        {
            VisualElement grid = new VisualElement();
            grid.AddToClassList("ht-stat-grid");
            grid.style.flexWrap = Wrap.NoWrap;

            grid.Add(CreateQueuedDetectionStatItem("Model Name", GetDisplayValue(detection != null ? detection.ModelName : string.Empty)));
            grid.Add(CreateQueuedDetectionStatItem("Game", GetDisplayValue(detection != null ? detection.GameKey : string.Empty)));
            grid.Add(CreateQueuedDetectionStatItem("JSONs", FormatCompatibleCount(detection != null ? detection.CompatibleJsonCount : 0, detection != null ? detection.JsonCount : 0)));
            grid.Add(CreateQueuedDetectionStatItem("Materials", FormatCompatibleCount(detection != null ? detection.CompatibleMaterialCount : 0, detection != null ? detection.MaterialCount : 0)));
            return grid;
        }

        private static VisualElement CreateQueuedDetectionStatItem(string label, string value)
        {
            VisualElement statItem = new VisualElement();
            statItem.AddToClassList("ht-stat-item");
            statItem.style.flexGrow = 1f;
            statItem.style.flexShrink = 1f;
            statItem.style.flexBasis = 0f;
            statItem.style.minWidth = 0f;

            Label valueLabel = new Label(value ?? string.Empty);
            valueLabel.AddToClassList("ht-title-md");
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            statItem.Add(valueLabel);

            Label labelLabel = new Label(label ?? string.Empty);
            labelLabel.AddToClassList("ht-caption");
            labelLabel.style.whiteSpace = WhiteSpace.NoWrap;
            labelLabel.style.overflow = Overflow.Hidden;
            statItem.Add(labelLabel);

            return statItem;
        }

        private static string FormatCompatibleCount(int compatibleCount, int totalCount)
        {
            return Mathf.Max(0, compatibleCount) + "/" + Mathf.Max(0, totalCount);
        }

        internal static VisualElement CreatePlacementSection(ModuleContext context)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("ht-column");
            section.AddToClassList("ht-gap-8");

            CharacterPlacementController controller = context != null ? context.PlacementController : null;
            HoyoToonManagerWindow window = context != null ? context.Window as HoyoToonManagerWindow : null;
            if (controller == null)
            {
                Label emptyLabel = new Label("No active placement controller.");
                emptyLabel.AddToClassList("ht-caption");
                section.Add(emptyLabel);
                return section;
            }

            section.Add(CreatePlacementSelector(context, controller, window));
            section.Add(CreatePlacementOptions(controller, window));
            return section;
        }

        private static VisualElement CreatePlacementSelector(ModuleContext context, CharacterPlacementController controller, HoyoToonManagerWindow window)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ht-row");
            row.AddToClassList("ht-gap-8");
            row.AddToClassList("ht-placement-top-row");

            List<string> options = new List<string>();
            int selectedIndex = -1;
            IReadOnlyList<GameObject> managedModels = controller.ManagedModels;
            string[] detectedNames = context != null ? context.PlacementManagedCharacterNames : Array.Empty<string>();
            for (int index = 0; index < managedModels.Count; index++)
            {
                GameObject model = managedModels[index];
                string label = index < detectedNames.Length ? detectedNames[index] : string.Empty;
                options.Add(!string.IsNullOrWhiteSpace(label) ? label : model != null ? model.name : "Missing");
                if (model == controller.ActiveModel)
                {
                    selectedIndex = index;
                }
            }

            options = BuildUniquePlacementOptions(options);

            if (options.Count == 0)
            {
                options.Add("None");
                selectedIndex = 0;
            }

            if (controller.PlacementMode == ManagerPlacementMode.Team)
            {
                int selectedMask = 0;
                IReadOnlyList<GameObject> activeTeamModels = controller.TeamActiveModels;
                for (int index = 0; index < managedModels.Count; index++)
                {
                    if (activeTeamModels.Contains(managedModels[index]))
                    {
                        selectedMask |= 1 << index;
                    }
                }

                MaskField teamField = new MaskField("Characters", options, selectedMask);
                teamField.AddToClassList("ht-field");
                teamField.AddToClassList("ht-placement-wide-field");
                teamField.SetEnabled(managedModels.Count > 0);
                teamField.RegisterValueChangedCallback(evt =>
                {
                    ApplyPlacementChange(controller, "HoyoToon Change Team Characters", () =>
                    {
                        int newMask = evt.newValue;
                        int firstSelectedIndex = -1;
                        for (int index = 0; index < managedModels.Count; index++)
                        {
                            bool shouldBeActive = (newMask & (1 << index)) != 0;
                            if (shouldBeActive && firstSelectedIndex < 0)
                            {
                                firstSelectedIndex = index;
                            }

                            controller.SetModelTeamActive(managedModels[index], shouldBeActive);
                        }

                        if (firstSelectedIndex >= 0 && firstSelectedIndex < managedModels.Count)
                        {
                            controller.SetFocusedModel(managedModels[firstSelectedIndex]);
                        }
                        else
                        {
                            controller.SetFocusedModel(null);
                        }
                    }, window);
                });
                row.Add(teamField);
                row.Add(CreatePlacementRemoveButton(controller, window));
                return row;
            }

            if (managedModels.Count > 0)
            {
                options.Insert(0, "None");
                selectedIndex = selectedIndex >= 0 ? selectedIndex + 1 : 0;
            }

            DropdownField activeCharacterDropdown = new DropdownField("Active Character");
            activeCharacterDropdown.AddToClassList("ht-field");
            activeCharacterDropdown.AddToClassList("ht-placement-wide-field");
            activeCharacterDropdown.choices = options;
            activeCharacterDropdown.index = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
            activeCharacterDropdown.SetEnabled(managedModels.Count > 0);
            OnboardingTargetRegistry.RegisterVisualElement("Scene.ActiveCharacterDropdown", activeCharacterDropdown, "Active character dropdown", "Scene");
            activeCharacterDropdown.RegisterValueChangedCallback(evt =>
            {
                OnboardingSignals.RecordAction("Scene.ActiveCharacterDropdown");
                OnboardingSignals.RecordValueChanged("Scene.ActiveCharacterDropdown", evt.newValue);
                int newIndex = options.IndexOf(evt.newValue);
                if (newIndex < 0)
                {
                    return;
                }

                ApplyPlacementChange(
                    controller,
                    "HoyoToon Change Placement Focus",
                    () => controller.SetFocusedModel(newIndex == 0 ? null : managedModels[newIndex - 1]),
                    window);
            });
            row.Add(activeCharacterDropdown);
            row.Add(CreatePlacementRemoveButton(controller, window));
            return row;
        }

        private static VisualElement CreatePlacementOptions(CharacterPlacementController controller, HoyoToonManagerWindow window)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("ht-column");
            section.AddToClassList("ht-gap-8");

            VisualElement rowOne = new VisualElement();
            rowOne.AddToClassList("ht-row");
            rowOne.AddToClassList("ht-gap-8");
            rowOne.AddToClassList("ht-placement-option-row");

            EnumField modeField = new EnumField("Camera Mode", controller.PlacementMode);
            modeField.AddToClassList("ht-field");
            modeField.AddToClassList("ht-placement-compact-field");
            OnboardingTargetRegistry.RegisterVisualElement("Scene.CameraModeDropdown", modeField, "Camera mode dropdown", "Scene");
            OnboardingTargetRegistry.RegisterVisualElement("Scene.PlacementModeDropdown", modeField, "Placement mode dropdown", "Scene");
            modeField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is ManagerPlacementMode placementMode)
                {
                    OnboardingSignals.RecordValueChanged("Scene.CameraModeDropdown", placementMode);
                    OnboardingSignals.RecordValueChanged("Scene.PlacementModeDropdown", placementMode);
                    if (placementMode == ManagerPlacementMode.Team)
                    {
                        OnboardingSignals.RecordAction("Scene.PlacementMode.Team");
                    }
                    else if (placementMode == ManagerPlacementMode.Grid)
                    {
                        OnboardingSignals.RecordAction("Scene.PlacementMode.Grid");
                    }
                    else if (placementMode == ManagerPlacementMode.Single)
                    {
                        OnboardingSignals.RecordAction("Scene.PlacementMode.Single");
                    }

                    ApplyPlacementChange(
                        controller,
                        "HoyoToon Change Placement Mode",
                        () => ApplyGuidedPlacementMode(controller, placementMode),
                        window);
                }
            });
            rowOne.Add(modeField);

            if (controller.PlacementMode == ManagerPlacementMode.Team)
            {
                EnumField teamField = new EnumField("Team Game", controller.TeamGame);
                teamField.AddToClassList("ht-field");
                teamField.AddToClassList("ht-placement-compact-field");
                teamField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue is HoyoToonTeamGame teamGame)
                    {
                        ApplyPlacementChange(controller, "HoyoToon Change Team Game", () => controller.TeamGame = teamGame, window);
                    }
                });
                rowOne.Add(teamField);
            }

            if (controller.PlacementMode == ManagerPlacementMode.Grid)
            {
                EnumField gridField = new EnumField("Grid Camera", controller.GridCamera);
                gridField.AddToClassList("ht-field");
                gridField.AddToClassList("ht-placement-compact-field");
                OnboardingTargetRegistry.RegisterVisualElement("Scene.GridModeDropdown", gridField, "Grid camera dropdown", "Scene");
                gridField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue is GridCameraMode gridCameraMode)
                    {
                        OnboardingSignals.RecordValueChanged("Scene.GridModeDropdown", gridCameraMode);
                        ApplyPlacementChange(controller, "HoyoToon Change Grid Camera", () => controller.GridCamera = gridCameraMode, window);
                    }
                });
                rowOne.Add(gridField);
            }

            section.Add(rowOne);

            VisualElement rowTwo = new VisualElement();
            rowTwo.AddToClassList("ht-row");
            rowTwo.AddToClassList("ht-gap-8");
            rowTwo.AddToClassList("ht-placement-option-row");
            rowTwo.Add(CreatePlacementToggle("Auto Discover", controller.AutoDiscoverManagedModels, value => controller.AutoDiscoverManagedModels = value, controller, window));
            section.Add(rowTwo);

            return section;
        }

        private static List<string> BuildUniquePlacementOptions(IReadOnlyList<string> labels)
        {
            List<string> resolvedLabels = new List<string>();
            Dictionary<string, int> totalCountsByLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < (labels != null ? labels.Count : 0); i++)
            {
                string label = string.IsNullOrWhiteSpace(labels[i]) ? "Missing" : labels[i].Trim();
                resolvedLabels.Add(label);
                if (totalCountsByLabel.TryGetValue(label, out int count))
                {
                    totalCountsByLabel[label] = count + 1;
                }
                else
                {
                    totalCountsByLabel[label] = 1;
                }
            }

            Dictionary<string, int> seenCountsByLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < resolvedLabels.Count; i++)
            {
                string label = resolvedLabels[i];
                if (!totalCountsByLabel.TryGetValue(label, out int totalCount) || totalCount <= 1)
                {
                    continue;
                }

                seenCountsByLabel.TryGetValue(label, out int seenCount);
                seenCount++;
                seenCountsByLabel[label] = seenCount;
                resolvedLabels[i] = label + " (" + seenCount + ")";
            }

            return resolvedLabels;
        }

        private static Toggle CreatePlacementToggle(string label, bool value, System.Action<bool> setter, CharacterPlacementController controller, HoyoToonManagerWindow window)
        {
            Toggle toggle = new Toggle(label)
            {
                value = value
            };
            toggle.AddToClassList("ht-toggle");
            toggle.RegisterValueChangedCallback(evt =>
            {
                ApplyPlacementChange(controller, "HoyoToon Change Placement Option", () => setter(evt.newValue), window);
            });
            return toggle;
        }

        private static Button CreatePlacementRemoveButton(CharacterPlacementController controller, HoyoToonManagerWindow window)
        {
            Button button = new Button(() => window?.RemovePlacementModel(controller != null ? controller.ActiveModel : null))
            {
                text = "Remove"
            };
            button.AddToClassList("ht-btn-secondary");
            button.AddToClassList("ht-placement-apply-button");
            button.SetEnabled(controller != null && controller.ActiveModel != null);
            return button;
        }

        private static void ApplyPlacementChange(CharacterPlacementController controller, string undoLabel, System.Action action, HoyoToonManagerWindow window)
        {
            if (controller == null || action == null)
            {
                return;
            }

            Undo.RecordObject(controller, undoLabel);
            action();
            EditorUtility.SetDirty(controller);
            window?.RefreshManagerContext();
        }

        private static void ApplyGuidedPlacementMode(CharacterPlacementController controller, ManagerPlacementMode placementMode)
        {
            if (controller == null)
            {
                return;
            }

            if (!IsGuidedPlacementModeStep())
            {
                controller.PlacementMode = placementMode;
                return;
            }

            controller.EnsureRosterConsistency();
            IReadOnlyList<GameObject> managedModels = controller.ManagedModels;
            GameObject preferredModel = controller.ActiveModel;
            if (preferredModel == null && managedModels.Count > 0)
            {
                preferredModel = managedModels[0];
            }

            controller.PlacementMode = placementMode;
            if (placementMode == ManagerPlacementMode.Team)
            {
                for (int index = 0; index < managedModels.Count; index++)
                {
                    controller.SetModelTeamActive(managedModels[index], true);
                }
            }

            if (preferredModel != null)
            {
                controller.SetFocusedModel(preferredModel);
            }
            else if (managedModels.Count > 0)
            {
                controller.SetFocusedModel(managedModels[0]);
            }
        }

        private static bool IsGuidedPlacementModeStep()
        {
            OnboardingStep step = OnboardingManager.CurrentStep;
            return OnboardingManager.IsRunning
                && step != null
                && step.Id.StartsWith("Scene.PlacementMode.", StringComparison.Ordinal);
        }

        private static VisualElement CreateDivider()
        {
            VisualElement divider = new VisualElement();
            divider.AddToClassList("ht-divider");
            return divider;
        }

        private static string GetDisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
        }
    }
}
