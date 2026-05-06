using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Assets;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Editor.UI.Manager;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.UI.Manager.Modules
{
    internal sealed class AssetsModule : ManagerModuleBase
    {
        private const string TutorialGameDisplayName = "Honkai Star Rail";
        private static readonly List<string> HsrChoiceLabels = new List<string>
        {
            "With Anims",
            "No Anims",
            "Both"
        };

        public override string Id => "assets";
        public override string DisplayName => "Assets";
        public override int Order => 1;

        protected override string Description => "Assets";

        public override VisualElement CreateContent(ModuleContext context)
        {
            AssetDownloadWindow backend = AssetDownloadWindow.GetOrCreateSharedBackend();
            backend.EnsureInitializedForManager();

            VisualElement root = new VisualElement();
            root.AddToClassList("ht-column");
            root.AddToClassList("ht-gap-12");
            root.AddToClassList("ht-module-root");
            root.AddToClassList("ht-assets-module");

            Action requestSync = null;
            Button refreshLibraryButton = new Button(() =>
            {
                backend.RefreshLibraryForManager();
                requestSync?.Invoke();
            })
            {
                text = "Refresh Library"
            };
            refreshLibraryButton.AddToClassList("ht-btn-secondary");
            refreshLibraryButton.AddToClassList("ht-assets-refresh-button");
            refreshLibraryButton.style.display = DisplayStyle.None;

            ScrollView gameScroll = CreateSelectionScroll();
            gameScroll.style.maxHeight = 156f;
            gameScroll.style.marginTop = 12f;
            VisualElement gameListHost = new VisualElement();
            gameListHost.AddToClassList("ht-column");
            gameListHost.AddToClassList("ht-gap-4");
            gameScroll.Add(gameListHost);

            Label charactersSubtitleLabel = CreateModuleSubtitle(string.Empty);
            TextField searchField = new TextField("Search")
            {
                value = backend.GetCharacterSearchForManager()
            };
            searchField.AddToClassList("ht-field");
            OnboardingTargetRegistry.RegisterVisualElement("Assets.CharacterSearch", searchField, "Character search", "Assets");

            Label characterSummaryLabel = new Label();
            characterSummaryLabel.AddToClassList("ht-assets-summary");

            ScrollView characterScroll = CreateSelectionScroll();
            VisualElement characterGrid = CreateSelectionGridHost();
            characterScroll.Add(characterGrid);
            OnboardingTargetRegistry.RegisterVisualElement("Assets.CharacterDropdown", characterScroll, "Character selection", "Assets");

            VisualElement variantOptionsHost = new VisualElement();
            variantOptionsHost.AddToClassList("ht-column");
            variantOptionsHost.AddToClassList("ht-gap-8");

            VisualElement variantListHost = new VisualElement();
            variantListHost.AddToClassList("ht-column");
            variantListHost.AddToClassList("ht-gap-8");

            Label actionsSubtitleLabel = CreateModuleSubtitle("Choose download options for the current selection.");
            Label actionSummaryLabel = new Label();
            actionSummaryLabel.AddToClassList("ht-assets-summary");

            Toggle autoSetupToggle = new Toggle("Auto Setup after download")
            {
                value = backend.GetAutoSetupAfterDownloadForManager()
            };
            autoSetupToggle.AddToClassList("ht-toggle");
            OnboardingTargetRegistry.RegisterVisualElement("Assets.AutoSetupToggle", autoSetupToggle, "Auto Setup toggle", "Assets");

            Button clearSelectionButton = new Button(() =>
            {
                backend.ClearSelectionForManager();
                requestSync?.Invoke();
            })
            {
                text = "Clear Selection"
            };
            clearSelectionButton.AddToClassList("ht-btn-secondary");

            Button downloadButton = new Button(() =>
            {
                OnboardingSignals.ClearOperationSimulation(OnboardingOperationKind.Download);
                OnboardingSignals.RecordAction("Assets.DownloadButton");
                backend.StartDownloadForManager();
            })
            {
                text = "Download Selected Assets"
            };
            downloadButton.AddToClassList("ht-btn-primary");
            OnboardingTargetRegistry.RegisterVisualElement("Assets.DownloadButton", downloadButton, "Download selected assets", "Assets");

            VisualElement libraryCard = CreateCard();
            AddCardHeader(libraryCard, "Games");
            VisualElement libraryActionRow = new VisualElement();
            libraryActionRow.AddToClassList("ht-row");
            libraryActionRow.AddToClassList("ht-assets-refresh-row");
            libraryActionRow.Add(refreshLibraryButton);
            libraryCard.Add(libraryActionRow);
            libraryCard.Add(gameScroll);
            root.Add(libraryCard);
            root.Add(CreateDivider());

            VisualElement charactersCard = CreateCard();
            AddCardHeader(charactersCard, "Characters");
            charactersCard.Add(charactersSubtitleLabel);
            charactersCard.Add(searchField);
            charactersCard.Add(characterSummaryLabel);
            charactersCard.Add(characterScroll);
            root.Add(charactersCard);
            root.Add(CreateDivider());

            VisualElement variantsCard = CreateCard();
            AddCardHeader(variantsCard, "Variants");
            OnboardingTargetRegistry.RegisterVisualElement("Assets.VariantDropdown", variantsCard, "Variant selection", "Assets");
            OnboardingTargetRegistry.RegisterVisualElement("Assets.ModelTypeDropdown", variantOptionsHost, "FBX type selection", "Assets");
            variantsCard.Add(variantOptionsHost);
            variantsCard.Add(variantListHost);
            root.Add(variantsCard);
            root.Add(CreateDivider());

            VisualElement actionsCard = CreateCard();
            AddCardHeader(actionsCard, "Download");
            actionsCard.Add(actionsSubtitleLabel);
            actionsCard.Add(actionSummaryLabel);
            OnboardingTargetRegistry.RegisterVisualElement("Assets.DownloadProgress", actionSummaryLabel, "Download progress", "Assets");
            actionsCard.Add(autoSetupToggle);
            VisualElement actionRow = new VisualElement();
            actionRow.AddToClassList("ht-row");
            actionRow.AddToClassList("ht-gap-8");
            actionRow.AddToClassList("ht-assets-action-row");
            actionRow.style.flexWrap = Wrap.Wrap;
            actionRow.Add(clearSelectionButton);
            actionRow.Add(downloadButton);
            actionsCard.Add(actionRow);
            root.Add(actionsCard);

            bool syncingSearchField = false;
            bool syncingAutoSetupToggle = false;
            string lastGameSnapshot = string.Empty;
            string lastCharacterSnapshot = string.Empty;
            string lastVariantSnapshot = string.Empty;

            searchField.RegisterValueChangedCallback(evt =>
            {
                if (syncingSearchField)
                {
                    return;
                }

                backend.SetCharacterSearchForManager(evt.newValue);
                OnboardingSignals.RecordValueChanged("Assets.CharacterSearch", evt.newValue);
            });

            autoSetupToggle.RegisterValueChangedCallback(evt =>
            {
                if (syncingAutoSetupToggle)
                {
                    return;
                }

                backend.SetAutoSetupAfterDownloadForManager(evt.newValue);
                OnboardingSignals.RecordAction("Assets.AutoSetupToggle");
                OnboardingSignals.RecordValueChanged("Assets.AutoSetupToggle", evt.newValue);
            });

            void SyncModule()
            {
                backend.EnsureInitializedForManager();
                bool isBusy = backend.IsBusyForManager();

                bool lockGameSelectionToTutorial = IsTutorialGameSelectionStep();
                List<string> gameChoices = backend.GetAvailableGameNamesForManager().ToList();
                int selectedGameIndex = backend.GetSelectedGameIndexForManager();
                string currentGameSnapshot = string.Join("|", gameChoices) + "::" + selectedGameIndex + "::" + lockGameSelectionToTutorial;
                if (!string.Equals(lastGameSnapshot, currentGameSnapshot, StringComparison.Ordinal))
                {
                    RebuildGameList(gameListHost, gameChoices, selectedGameIndex, backend, lockGameSelectionToTutorial);
                    lastGameSnapshot = currentGameSnapshot;
                }

                refreshLibraryButton.SetEnabled(!isBusy && !lockGameSelectionToTutorial);

                string backendSearch = backend.GetCharacterSearchForManager();
                if (!string.Equals(searchField.value, backendSearch, StringComparison.Ordinal))
                {
                    syncingSearchField = true;
                    searchField.SetValueWithoutNotify(backendSearch);
                    syncingSearchField = false;
                }

                string selectedGameName = backend.GetSelectedGameDisplayNameForManager();
                charactersSubtitleLabel.text = string.IsNullOrWhiteSpace(selectedGameName)
                    ? "Select a game first."
                    : "Select one or more " + selectedGameName + " characters.";

                List<string> filteredCharacters = backend.GetFilteredCharactersForManager().ToList();
                List<string> selectedCharacters = backend.GetSelectedCharacterNamesForManager().ToList();
                string currentCharacterSnapshot = backendSearch + "::"
                    + string.Join("|", filteredCharacters) + "::"
                    + string.Join("|", selectedCharacters);
                if (!string.Equals(lastCharacterSnapshot, currentCharacterSnapshot, StringComparison.Ordinal))
                {
                    RebuildCharacterGrid(characterGrid, filteredCharacters, selectedCharacters, backend, SyncModule);
                    characterSummaryLabel.text = filteredCharacters.Count + " shown | " + selectedCharacters.Count + " selected";
                    lastCharacterSnapshot = currentCharacterSnapshot;
                }

                bool supportsAutoSetup = backend.SupportsAutoSetupForManager();
                bool autoSetupValue = backend.GetAutoSetupAfterDownloadForManager();
                if (autoSetupToggle.value != autoSetupValue)
                {
                    syncingAutoSetupToggle = true;
                    autoSetupToggle.SetValueWithoutNotify(autoSetupValue);
                    syncingAutoSetupToggle = false;
                }
                autoSetupToggle.SetEnabled(supportsAutoSetup && !isBusy);

                bool showHsrChoice = backend.ShouldShowHsrChoiceForManager();
                variantOptionsHost.Clear();
                if (showHsrChoice && selectedCharacters.Count == 1)
                {
                    int hsrChoiceIndex = backend.GetHsrChoiceIndexForManager();
                    variantOptionsHost.Add(
                        CreateHsrChoiceRow(
                            "FBX Type",
                            hsrChoiceIndex,
                            isBusy,
                            backend.SetHsrChoiceIndexForManager));
                }

                string currentVariantSnapshot = string.Join("|", selectedCharacters.Select(characterName =>
                    characterName + "=" + GetHsrChoiceSnapshotForCharacter(backend, characterName)
                    + ":" + string.Join(",", backend.GetVariantNamesForManager(characterName))
                    + "[" + string.Join(",",
                        backend.GetVariantNamesForManager(characterName)
                            .Where(variantName => backend.IsVariantSelectedForManager(characterName, variantName))) + "]"));
                if (!string.Equals(lastVariantSnapshot, currentVariantSnapshot, StringComparison.Ordinal))
                {
                    bool showPerCharacterHsrChoice = showHsrChoice && selectedCharacters.Count > 1;
                    RebuildVariantList(
                        variantListHost,
                        selectedCharacters,
                        backend,
                        showPerCharacterHsrChoice,
                        isBusy,
                        SyncModule);
                    lastVariantSnapshot = currentVariantSnapshot;
                }

                actionSummaryLabel.text = backend.GetActionSummaryForManager();
                clearSelectionButton.SetEnabled(backend.CanClearSelectionForManager() && !isBusy);
                downloadButton.SetEnabled(backend.CanDownloadSelectionForManager() && !isBusy);
            }

            requestSync = SyncModule;
            SyncModule();
            root.schedule.Execute(SyncModule).Every(300);
            return root;
        }

        protected override IEnumerable<VisualElement> BuildCards(ModuleContext context)
        {
            yield break;
        }

        private static Label CreateModuleSubtitle(string text)
        {
            Label subtitle = new Label(text ?? string.Empty);
            subtitle.AddToClassList("ht-assets-subtitle");
            return subtitle;
        }

        private static ScrollView CreateSelectionScroll()
        {
            ScrollView list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList("ht-assets-scroll");
            list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            list.verticalScrollerVisibility = ScrollerVisibility.Auto;
            list.style.maxHeight = 220f;
            list.style.minHeight = 96f;
            return list;
        }

        private static VisualElement CreateSelectionGridHost()
        {
            VisualElement grid = new VisualElement();
            grid.AddToClassList("ht-assets-grid");
            return grid;
        }

        private static void RebuildCharacterGrid(
            VisualElement host,
            IReadOnlyList<string> filteredCharacters,
            IReadOnlyCollection<string> selectedCharacters,
            AssetDownloadWindow backend,
            Action requestSync)
        {
            host.Clear();

            if (filteredCharacters == null || filteredCharacters.Count <= 0)
            {
                Label emptyLabel = new Label("No characters available.");
                emptyLabel.AddToClassList("ht-caption");
                host.Add(emptyLabel);
                return;
            }

            foreach (string characterName in filteredCharacters)
            {
                bool isSelected = selectedCharacters != null && selectedCharacters.Contains(characterName);
                bool guidedSelection = TryGetGuidedRequiredValue(".SelectCharacter", out string requiredCharacter);
                bool isRequiredCharacter = guidedSelection
                    && string.Equals(characterName, requiredCharacter, StringComparison.OrdinalIgnoreCase);
                Button chip = CreateSelectionChip(
                    characterName,
                    isSelected,
                    () =>
                    {
                        OnboardingSignals.RecordAction("Assets.CharacterDropdown");
                        OnboardingSignals.RecordValueChanged("Assets.CharacterDropdown", characterName);
                        backend.SetCharacterSelectedForManager(
                            characterName,
                            isRequiredCharacter ? true : !isSelected);
                        requestSync?.Invoke();
                    });

                if (isRequiredCharacter)
                {
                    OnboardingTargetRegistry.RegisterVisualElement("Assets.CharacterDropdown", chip, characterName, "Assets");
                }

                if (guidedSelection && !isRequiredCharacter)
                {
                    chip.SetEnabled(false);
                }

                host.Add(chip);
            }
        }

        private static void RebuildGameList(
            VisualElement host,
            IReadOnlyList<string> gameChoices,
            int selectedGameIndex,
            AssetDownloadWindow backend,
            bool lockSelectionToTutorialGame)
        {
            host.Clear();

            if (gameChoices == null || gameChoices.Count <= 0)
            {
                Label emptyLabel = new Label("No games available.");
                emptyLabel.AddToClassList("ht-caption");
                host.Add(emptyLabel);
                return;
            }

            for (int index = 0; index < gameChoices.Count; index++)
            {
                int capturedIndex = index;
                string gameName = gameChoices[index];
                bool isSelected = index == selectedGameIndex;
                bool isTutorialGame = IsTutorialGameChoice(gameName);
                Button chip = CreateSelectionChip(
                    gameName,
                    isSelected,
                    () =>
                    {
                        OnboardingSignals.RecordAction("Assets.GameDropdown");
                        OnboardingSignals.RecordValueChanged("Assets.GameDropdown", gameName);
                        backend.SetSelectedGameIndexForManager(capturedIndex);
                    },
                    block: true);

                if (isTutorialGame)
                {
                    OnboardingTargetRegistry.RegisterVisualElement("Assets.GameDropdown", chip, TutorialGameDisplayName, "Assets");
                }

                if (lockSelectionToTutorialGame && !isTutorialGame)
                {
                    chip.SetEnabled(false);
                }

                host.Add(chip);
            }
        }

        private static bool IsTutorialGameSelectionStep()
        {
            OnboardingStep step = OnboardingManager.CurrentStep;
            return OnboardingManager.IsRunning
                && step != null
                && step.Id.EndsWith(".SelectGame", StringComparison.Ordinal);
        }

        private static bool IsTutorialGameChoice(string gameName)
        {
            return string.Equals(gameName, TutorialGameDisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static void RebuildVariantList(
            VisualElement host,
            IReadOnlyList<string> selectedCharacters,
            AssetDownloadWindow backend,
            bool showPerCharacterHsrChoice,
            bool controlsDisabled,
            Action requestSync)
        {
            host.Clear();

            if (selectedCharacters == null || selectedCharacters.Count <= 0)
            {
                Label emptyLabel = new Label("Select one or more characters to load variants.");
                emptyLabel.AddToClassList("ht-caption");
                host.Add(emptyLabel);
                return;
            }

            if (selectedCharacters.Count == 1)
            {
                string characterName = selectedCharacters[0];
                host.Add(
                    CreateVariantSelectionGroup(
                        characterName,
                        backend.GetVariantNamesForManager(characterName),
                        backend,
                        false,
                        false,
                        controlsDisabled,
                        requestSync));
                return;
            }

            for (int index = 0; index < selectedCharacters.Count; index++)
            {
                string characterName = selectedCharacters[index];
                host.Add(
                    CreateVariantSelectionGroup(
                        characterName,
                        backend.GetVariantNamesForManager(characterName),
                        backend,
                        true,
                        showPerCharacterHsrChoice,
                        controlsDisabled,
                        requestSync));
                if (index < selectedCharacters.Count - 1)
                {
                    host.Add(CreateVariantDivider());
                }
            }
        }

        private static VisualElement CreateVariantSelectionGroup(
            string characterName,
            IReadOnlyList<string> variants,
            AssetDownloadWindow backend,
            bool includeLabel,
            bool includeHsrChoice,
            bool controlsDisabled,
            Action requestSync)
        {
            VisualElement group = new VisualElement();
            group.AddToClassList("ht-column");
            group.AddToClassList("ht-gap-8");

            if (includeLabel)
            {
                Label nameLabel = new Label(characterName);
                nameLabel.AddToClassList("ht-assets-summary");
                group.Add(nameLabel);
            }

            if (includeHsrChoice)
            {
                int hsrChoiceIndex = backend.GetHsrChoiceIndexForCharacterForManager(characterName);
                group.Add(
                    CreateHsrChoiceRow(
                        "FBX Type",
                        hsrChoiceIndex,
                        controlsDisabled,
                        index =>
                        {
                            OnboardingSignals.RecordValueChanged("Assets.ModelTypeDropdown", HsrChoiceLabels[Math.Max(0, Math.Min(index, HsrChoiceLabels.Count - 1))]);
                            backend.SetHsrChoiceIndexForCharacterForManager(characterName, index);
                            requestSync?.Invoke();
                        }));
            }

            if (variants == null || variants.Count <= 0)
            {
                Label emptyLabel = new Label("Loading variants...");
                emptyLabel.AddToClassList("ht-caption");
                group.Add(emptyLabel);
                return group;
            }

            VisualElement grid = CreateSelectionGridHost();
            foreach (string variantName in variants)
            {
                bool isSelected = backend.IsVariantSelectedForManager(characterName, variantName);
                bool guidedVariant = TryGetGuidedRequiredValue(".SelectVariant", out string requiredVariant);
                bool isRequiredVariant = guidedVariant
                    && string.Equals(variantName, requiredVariant, StringComparison.OrdinalIgnoreCase);
                Button chip = CreateSelectionChip(
                    variantName,
                    isSelected,
                    () =>
                    {
                        OnboardingSignals.RecordAction("Assets.VariantDropdown");
                        OnboardingSignals.RecordValueChanged("Assets.VariantDropdown", variantName);
                        backend.SetVariantSelectedForManager(
                            characterName,
                            variantName,
                            isRequiredVariant ? true : !isSelected);
                        requestSync?.Invoke();
                    });

                if (isRequiredVariant)
                {
                    OnboardingTargetRegistry.RegisterVisualElement("Assets.VariantDropdown", chip, variantName, "Assets");
                }

                if (guidedVariant && !isRequiredVariant)
                {
                    chip.SetEnabled(false);
                }

                grid.Add(chip);
            }

            group.Add(grid);
            return group;
        }

        private static VisualElement CreateVariantDivider()
        {
            VisualElement divider = new VisualElement();
            divider.AddToClassList("ht-divider");
            divider.style.marginTop = 2f;
            divider.style.marginBottom = 2f;
            return divider;
        }

        private static VisualElement CreateDivider()
        {
            return ManagerUiFactory.CreateDivider();
        }

        private static Button CreateSelectionChip(string text, bool isSelected, Action onClick)
        {
            return CreateSelectionChip(text, isSelected, onClick, false);
        }

        private static Button CreateSelectionChip(string text, bool isSelected, Action onClick, bool block)
        {
            Button chip = new Button(() => onClick?.Invoke())
            {
                text = text ?? string.Empty,
                tooltip = text ?? string.Empty
            };
            chip.AddToClassList("ht-assets-chip");
            if (block)
            {
                chip.AddToClassList("ht-assets-chip--block");
            }

            if (isSelected)
            {
                chip.AddToClassList("ht-assets-chip--active");
            }

            return chip;
        }

        private static VisualElement CreateHsrChoiceRow(
            string labelText,
            int selectedIndex,
            bool isDisabled,
            Action<int> onChoiceChanged)
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("ht-row");
            root.AddToClassList("ht-gap-6");

            Label label = new Label(string.IsNullOrWhiteSpace(labelText) ? "FBX Type" : labelText);
            label.AddToClassList("ht-caption");
            root.Add(label);

            for (int index = 0; index < HsrChoiceLabels.Count; index++)
            {
                string text = HsrChoiceLabels[index];
                bool isSelected = index == selectedIndex;
                int capturedIndex = index;
                bool guidedType = TryGetGuidedRequiredValue(".SelectType", out string requiredType);
                bool isRequiredType = guidedType
                    && (index == 1
                        || string.Equals(text, requiredType, StringComparison.OrdinalIgnoreCase)
                        || string.Equals("FBX " + text, requiredType, StringComparison.OrdinalIgnoreCase));
                Button chip = new Button(() =>
                {
                    OnboardingSignals.RecordAction("Assets.ModelTypeDropdown");
                    OnboardingSignals.RecordValueChanged("Assets.ModelTypeDropdown", text);
                    onChoiceChanged?.Invoke(capturedIndex);
                })
                {
                    text = text
                };
                chip.AddToClassList("ht-assets-chip");
                if (index == 1)
                {
                    OnboardingTargetRegistry.RegisterVisualElement("Assets.ModelTypeDropdown", chip, "FBX No Animations", "Assets");
                }
                chip.SetEnabled(!isDisabled && (!guidedType || isRequiredType));
                if (isSelected)
                {
                    chip.AddToClassList("ht-assets-chip--active");
                }

                root.Add(chip);
            }

            return root;
        }

        private static bool TryGetGuidedRequiredValue(string stepSuffix, out string requiredValue)
        {
            OnboardingStep step = OnboardingManager.CurrentStep;
            requiredValue = step != null ? step.RequiredValue : string.Empty;
            return OnboardingManager.IsRunning
                && step != null
                && step.Id.EndsWith(stepSuffix, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(requiredValue);
        }

        private static string GetHsrChoiceSnapshotForCharacter(AssetDownloadWindow backend, string characterName)
        {
            if (backend == null || string.IsNullOrWhiteSpace(characterName))
            {
                return "0";
            }

            int choiceIndex = backend.GetHsrChoiceIndexForCharacterForManager(characterName);
            return choiceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
