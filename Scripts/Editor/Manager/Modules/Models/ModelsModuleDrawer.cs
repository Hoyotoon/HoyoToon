#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.Onboarding;
using StepIds = HoyoToon.Editor.Onboarding.GuidedTourController.StepIds;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class ModelsModuleDrawer
    {
        private readonly ModelsModule _module;

        private static GUIStyle _toolbarSearchCancelButton;
        private static GUIStyle _toolbarSearchCancelButtonEmpty;

        private List<string> _cachedGameLabels;
        private int _cachedGameLabelsHash;
        private List<string> _cachedVariantNames;
        private int _cachedVariantNamesVersion;
        private readonly Dictionary<string, (List<string> names, int version)> _cachedPerCharVariantNames = new Dictionary<string, (List<string>, int)>(StringComparer.OrdinalIgnoreCase);

        public ModelsModuleDrawer(ModelsModule module)
        {
            _module = module;
        }

        internal void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_module._statusMessage))
            {
                EditorGUILayout.HelpBox(_module._statusMessage, MessageType.Info);
            }
        }

        internal void DrawRefreshRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh Game List", GUILayout.Width(160f)))
                {
                    _module.StartRefreshGameList();
                }
                var refreshRect = GUILayoutUtility.GetLastRect();
                TourOverlay.DrawHighlightIfActive("tour.models.refresh", refreshRect, "Refresh");
                GUILayout.FlexibleSpace();

                if (_module._isRefreshing)
                {
                    EditorGUILayout.LabelField("Loading...", EditorStyles.miniLabel);
                }
            }
        }

        internal void DrawGameSelection()
        {
            DrawTourCalloutForStep(StepIds.Game);
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                var labels = GetCachedGameLabels();
                bool lockSelection = GuidedTourController.ShouldLockGameSelection();
                int desiredIndex = lockSelection
                    ? _module._availableGames.FindIndex(option => string.Equals(option.DisplayName, GuidedTourController.DesiredGameName, StringComparison.OrdinalIgnoreCase))
                    : -1;
                int current = NormalizeSelectionIndex(_module._selectedGameIndex, labels.Count);
                int newIndex = DrawScrollableSelection(
                    "Game",
                    labels,
                    current,
                    ref _module._gameScroll,
                    _module._isRefreshing || (lockSelection && desiredIndex < 0),
                    "No games available from the API or CDN share.",
                    GuidedTourController.DesiredGameName,
                    "tour.models.game",
                    lockSelection);


                if (lockSelection && desiredIndex < 0)
                {
                    EditorGUILayout.HelpBox($"Guided tour expects '{GuidedTourController.DesiredGameName}'. Waiting for that game to load.", MessageType.Info);
                    return;
                }

                if (newIndex != current)
                {
                    _module._selectedGameIndex = newIndex;
                    if (_module._selectedGameIndex >= 0 && _module._selectedGameIndex < _module._availableGames.Count)
                    {
                        GuidedTourController.NotifyGameSelected(_module._availableGames[_module._selectedGameIndex].DisplayName);
                    }
                    _module.ClearCharacterSelection();
                    _module.EnsureCharactersLoaded();
                }
                else if (lockSelection && desiredIndex == current && desiredIndex >= 0 && _module._selectedGameIndex >= 0 && _module._selectedGameIndex < _module._availableGames.Count)
                {
                    GuidedTourController.NotifyGameSelected(_module._availableGames[_module._selectedGameIndex].DisplayName);
                }
            }
        }

        internal void DrawCharacterSelection()
        {
            if (_module._selectedGameIndex < 0)
            {
                EditorGUILayout.HelpBox("Select a game to load characters.", MessageType.Info);
                return;
            }

            DrawTourCalloutForStep(StepIds.Character);
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                DrawCharacterSearchBar();

                var filteredCharacters = _module.GetFilteredCharacters();
                if (GuidedTourController.ShouldLockCharacterSelection())
                {
                    _module.ClearNonTourCharacterSelection();
                }
                if (!string.IsNullOrWhiteSpace(_module._characterSearch) && _module._characters != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Showing {filteredCharacters.Count:N0} / {_module._characters.Count:N0}", EditorStyles.miniLabel);
                    }
                }

                string emptyMessage = string.IsNullOrWhiteSpace(_module._characterSearch)
                    ? "No characters are available for this game."
                    : "No characters match the search filter.";

                DrawMultiSelectList(
                    "Character",
                    filteredCharacters,
                    _module._selectedCharacters,
                    ref _module._characterScroll,
                    _module._isRefreshing,
                    emptyMessage,
                    110f,
                    210f,
                    selectedName =>
                    {
                        _module._selectedCharacterIndex = _module._characters.FindIndex(name => string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase));
                        _module.ClearVariantSelection();
                        _module.EnsureVariantsLoaded();
                        GuidedTourController.NotifyCharacterSelected(selectedName);
                    },
                    GuidedTourController.DesiredCharacterName,
                    "tour.models.character",
                    GuidedTourController.ShouldLockCharacterSelection());

                if (_module._selectedCharacters.Count == 0)
                {
                    _module._selectedCharacterIndex = -1;
                }
                else if (_module._selectedCharacterIndex < 0 || _module._selectedCharacterIndex >= _module._characters.Count
                         || !_module._selectedCharacters.Contains(_module._characters[_module._selectedCharacterIndex]))
                {
                    _module._selectedCharacterIndex = _module._characters.FindIndex(name => _module._selectedCharacters.Contains(name));
                }
            }
        }

        private void DrawCharacterSearchBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(50f));
                string newSearch = EditorGUILayout.TextField(_module._characterSearch ?? string.Empty, EditorStyles.toolbarSearchField);
                var clearStyle = GetToolbarSearchCancelStyle(string.IsNullOrEmpty(newSearch));
                if (GUILayout.Button(GUIContent.none, clearStyle))
                {
                    newSearch = string.Empty;
                    GUI.FocusControl(null);
                }

                if (!string.Equals(_module._characterSearch, newSearch, StringComparison.Ordinal))
                {
                    _module.SetCharacterSearch(newSearch);
                }
            }
        }

        private static GUIStyle GetToolbarSearchCancelStyle(bool empty)
        {
            ref var cachedStyle = ref (empty ? ref _toolbarSearchCancelButtonEmpty : ref _toolbarSearchCancelButton);
            if (cachedStyle == null)
            {
                string styleName = empty ? "ToolbarSearchCancelButtonEmpty" : "ToolbarSearchCancelButton";
                cachedStyle = GUI.skin?.FindStyle(styleName) ?? GUIStyle.none;
            }

            return cachedStyle;
        }

        internal void DrawVariantSelection()
        {
            if (_module._selectedCharacterIndex < 0)
            {
                EditorGUILayout.HelpBox("Select a character to load variants.", MessageType.Info);
                return;
            }

            DrawTourCalloutForStep(StepIds.Variant);
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                var game = _module._selectedGameIndex >= 0 && _module._selectedGameIndex < _module._availableGames.Count ? _module._availableGames[_module._selectedGameIndex] : null;
                var selectedCharacters = _module.GetSelectedCharacterList();
                bool multipleCharacters = selectedCharacters.Count > 1;
                if (!multipleCharacters)
                {
                    var variantNames = GetCachedVariantNames();
                    if (GuidedTourController.ShouldLockVariantSelection())
                    {
                        _module.ClearNonTourVariantSelection();
                    }
                    DrawMultiSelectList(
                        "Variant",
                        variantNames,
                        _module._selectedVariants,
                        ref _module._variantScroll,
                        _module._isRefreshing,
                        "No variants are available for this character.",
                        60f,
                        110f,
                        selectedName =>
                        {
                            _module._selectedVariantIndex = _module._variants.FindIndex(option => string.Equals(option.Name, selectedName, StringComparison.OrdinalIgnoreCase));
                            GuidedTourController.NotifyVariantSelected(selectedName);
                        },
                        GuidedTourController.DesiredVariantName,
                        "tour.models.variant",
                        GuidedTourController.ShouldLockVariantSelection());

                    if (_module._selectedVariants.Count == 0)
                    {
                        _module._selectedVariantIndex = -1;
                    }
                    else if (_module._selectedVariantIndex < 0 || _module._selectedVariantIndex >= _module._variants.Count
                             || !_module._selectedVariants.Contains(_module._variants[_module._selectedVariantIndex].Name))
                    {
                        _module._selectedVariantIndex = _module._variants.FindIndex(option => _module._selectedVariants.Contains(option.Name));
                    }
                }
                else
                {
                    DrawPerCharacterVariantLists(selectedCharacters);
                }

                if (game != null && ModelsPathHelper.IsHonkaiStarRail(game.Key, game.DisplayName) && selectedCharacters.Count > 0)
                {
                    DrawTourCalloutForStep(StepIds.Fbx);
                    DrawStarRailFbxSelection(game);
                }
            }
        }

        internal void DrawDownloadActions(HoyoToonManager manager)
        {
            DrawTourCalloutForStep(StepIds.Download);
            bool tourActive = GuidedTourController.IsActive;
            bool enforcedAutoSetup = tourActive ? false : _module._autoSetupAfterDownload;
            bool newValue;

            Rect toggleRect = EditorGUILayout.GetControlRect();
            using (new EditorGUI.DisabledScope(tourActive))
            {
                newValue = EditorGUI.ToggleLeft(toggleRect, "Auto setup after download", enforcedAutoSetup);
            }

            TourOverlay.DrawHighlightIfActive("tour.models.autosetup", toggleRect, "Auto setup");
            if (tourActive)
            {
                _module._autoSetupAfterDownload = false;
            }
            else if (newValue != _module._autoSetupAfterDownload)
            {
                _module._autoSetupAfterDownload = newValue;
                EditorPrefs.SetBool(PrefsKeys.ModelsAutoSetup, _module._autoSetupAfterDownload);
            }

            const float actionSpacing = 6f;
            const float stackedThreshold = 340f;
            float singleLineHeight = EditorGUIUtility.singleLineHeight;
            Rect actionRowRect = EditorGUILayout.GetControlRect(false, singleLineHeight);

            if (actionRowRect.width < stackedThreshold)
            {
                DrawClearSelectionButton(actionRowRect);
                Rect stackedDownloadRect = EditorGUILayout.GetControlRect(false, singleLineHeight);
                DrawDownloadButton(stackedDownloadRect, manager);
                return;
            }

            float buttonWidth = Mathf.Floor((actionRowRect.width - actionSpacing) * 0.5f);
            float clearWidth = buttonWidth;
            float downloadWidth = actionRowRect.width - clearWidth - actionSpacing;
            Rect clearRect = new Rect(actionRowRect.x, actionRowRect.y, clearWidth, actionRowRect.height);
            Rect downloadRect = new Rect(clearRect.xMax + actionSpacing, actionRowRect.y, downloadWidth, actionRowRect.height);

            DrawClearSelectionButton(clearRect);
            DrawDownloadButton(downloadRect, manager);
        }

        private void DrawDownloadButton(Rect rect, HoyoToonManager manager)
        {
            bool ready = _module.TryGetBatchSelection(out _, out var characters, out var variants)
                         && characters.Count > 0
                         && variants.Count > 0;
            using (new EditorGUI.DisabledScope(!ready || _module._isRefreshing))
            {
                if (GUI.Button(rect, "Download Selected Models"))
                {
                    _module.StartDownloadSelected(manager);
                }

                TourOverlay.DrawHighlightIfActive("tour.models.download", rect, "Download");
            }
        }

        private static void DrawMultiSelectList(
            string label,
            IReadOnlyList<string> items,
            HashSet<string> selected,
            ref Vector2 scroll,
            bool disabled,
            string emptyMessage,
            float minHeight,
            float maxHeight,
            Action<string> onActivated = null,
            string highlightName = null,
            string highlightTargetId = null,
            bool lockToHighlight = false)
        {
            using (new EditorGUI.DisabledScope(disabled))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (items != null)
                    {
                        EditorGUILayout.LabelField($"{items.Count:N0} items", EditorStyles.miniLabel, GUILayout.Width(90f));
                    }
                }

                if (items == null || items.Count == 0)
                {
                    EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                    return;
                }

                float contentHeight = GetSelectionGridHeight(items.Count, EditorGUIUtility.currentViewWidth - 40f, 4);
                if (contentHeight <= maxHeight)
                {
                    int _ = DrawSelectionGrid(items, selected, onActivated, -1, EditorGUIUtility.currentViewWidth - 40f, highlightName, highlightTargetId, lockToHighlight, true, 4);
                    scroll = Vector2.zero;
                    return;
                }

                float viewportHeight = Mathf.Clamp(contentHeight, minHeight, maxHeight);
                using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll, false, false, GUILayout.Height(viewportHeight)))
                {
                    scroll = scrollScope.scrollPosition;
                    int _ = DrawSelectionGrid(items, selected, onActivated, -1, EditorGUIUtility.currentViewWidth - 40f, highlightName, highlightTargetId, lockToHighlight, true, 4);
                }
            }
        }

        private static float GetSelectionGridHeight(int itemCount, float viewWidth, int maxColumns)
        {
            if (itemCount <= 0)
            {
                return 0f;
            }

            float available = Mathf.Max(120f, viewWidth - 20f);
            int columns = Mathf.Clamp(Mathf.FloorToInt(available / 150f), 1, maxColumns);
            int rowCount = Mathf.CeilToInt(itemCount / (float)columns);
            return rowCount * 22f;
        }

        private void DrawPerCharacterVariantLists(List<string> characters)
        {
            if (characters == null || characters.Count == 0)
            {
                return;
            }

            var game = _module._selectedGameIndex >= 0 && _module._selectedGameIndex < _module._availableGames.Count ? _module._availableGames[_module._selectedGameIndex] : null;
            float maxHeight = Mathf.Clamp(100f + characters.Count * 22f, 120f, 220f);
            using (var scrollScope = new EditorGUILayout.ScrollViewScope(_module._variantSectionScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(maxHeight)))
            {
                _module._variantSectionScroll = scrollScope.scrollPosition;
                foreach (var characterName in characters)
                {
                    var selection = _module.GetOrCreateVariantSelection(characterName);
                    var variants = _module.GetVariantsForCharacter(game, characterName);

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(characterName, EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();
                            if (variants != null)
                            {
                                EditorGUILayout.LabelField($"{selection.Count}/{variants.Count}", EditorStyles.miniLabel, GUILayout.Width(60f));
                            }
                        }

                        if (variants == null)
                        {
                            EditorGUILayout.LabelField("Loading variants...", EditorStyles.miniLabel);
                            continue;
                        }

                        var variantNames = GetCachedPerCharVariantNames(characterName, variants);
                        bool useScroll = variantNames.Count > 6;
                        if (useScroll)
                        {
                            var scroll = _module.GetVariantScroll(characterName);
                            using (var variantsScroll = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.MinHeight(50f), GUILayout.MaxHeight(90f)))
                            {
                                scroll = variantsScroll.scrollPosition;
                                int _ = DrawSelectionGrid(variantNames, selection, null, -1, EditorGUIUtility.currentViewWidth - 60f, null, null, false, true, 4);
                            }

                            _module.SetVariantScroll(characterName, scroll);
                        }
                        else
                        {
                            int _ = DrawSelectionGrid(variantNames, selection, null, -1, EditorGUIUtility.currentViewWidth - 60f, null, null, false, true, 4);
                        }
                    }
                }
            }
        }

        private void DrawStarRailFbxSelection(GameOption game)
        {
            if (game == null || !ModelsPathHelper.IsHonkaiStarRail(game.Key, game.DisplayName))
            {
                return;
            }

            EditorGUILayout.Space(ManagerUILayout.SpacingSmall);
            EditorGUILayout.LabelField("FBX (Star Rail)", EditorStyles.boldLabel);
            var options = new[] { "With Anims", "No Anims", "Both" };
            bool lockChoice = GuidedTourController.ShouldLockFbxSelection();
            int newIndex = DrawSelectionGrid(
                options,
                null,
                null,
                (int)_module._hsrFbxChoice,
                EditorGUIUtility.currentViewWidth - 40f,
                GuidedTourController.DesiredFbxChoiceName,
                "tour.models.fbx.noanims",
                lockChoice,
                false,
                3);

            if (newIndex != (int)_module._hsrFbxChoice)
            {
                _module._hsrFbxChoice = (HsrFbxChoice)newIndex;
                GuidedTourController.NotifyFbxChoiceSelected(options[newIndex]);
            }
            else if (lockChoice)
            {
                GuidedTourController.NotifyFbxChoiceSelected(options[(int)_module._hsrFbxChoice]);
            }
        }

        private static int DrawSelectionGrid(
            IReadOnlyList<string> items,
            HashSet<string> selected,
            Action<string> onActivated,
            int selectedIndex,
            float viewWidth,
            string highlightName,
            string highlightTargetId,
            bool lockToHighlight,
            bool multiSelect,
            int maxColumns)
        {
            if (items == null || items.Count == 0)
            {
                return selectedIndex;
            }

            float available = Mathf.Max(120f, viewWidth - 20f);
            int columns = Mathf.Clamp(Mathf.FloorToInt(available / 150f), 1, maxColumns);
            float columnWidth = Mathf.Floor(available / columns);
            int rowCount = Mathf.CeilToInt(items.Count / (float)columns);

            for (int row = 0; row < rowCount; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        if (index >= items.Count)
                        {
                            GUILayout.FlexibleSpace();
                            break;
                        }

                        var item = items[index];
                        bool allowSelect = !lockToHighlight
                                           || string.IsNullOrEmpty(highlightName)
                                           || string.Equals(item, highlightName, StringComparison.OrdinalIgnoreCase);
                        bool isSelected = multiSelect
                            ? selected != null && selected.Contains(item)
                            : index == selectedIndex;

                        using (new EditorGUI.DisabledScope(!allowSelect))
                        {
                            bool toggled = GUILayout.Toggle(isSelected, item, EditorStyles.miniButton, GUILayout.Width(columnWidth), GUILayout.Height(20f));
                            if (multiSelect)
                            {
                                if (selected == null || toggled == isSelected)
                                {
                                    // no-op
                                }
                                else if (toggled)
                                {
                                    selected.Add(item);
                                    onActivated?.Invoke(item);
                                }
                                else
                                {
                                    selected.Remove(item);
                                }
                            }
                            else if (toggled && !isSelected)
                            {
                                selectedIndex = index;
                            }
                        }

                        var buttonRect = GUILayoutUtility.GetLastRect();
                        if (!string.IsNullOrEmpty(highlightName)
                            && string.Equals(item, highlightName, StringComparison.OrdinalIgnoreCase))
                        {
                            TourOverlay.DrawHighlightIfActive(highlightTargetId, buttonRect, item);
                        }
                    }
                }
            }

            return selectedIndex;
        }

        private void DrawClearSelectionButton(Rect rect)
        {
            bool canClear = _module.HasCurrentSelection() && !_module._isRefreshing && !GuidedTourController.IsActive;
            using (new EditorGUI.DisabledScope(!canClear))
            {
                if (GUI.Button(rect, "Clear Selection"))
                {
                    _module.ClearCurrentSelection();
                }
            }
        }

        private static int NormalizeSelectionIndex(int index, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (index < 0)
            {
                return 0;
            }

            return Mathf.Clamp(index, 0, count - 1);
        }

        private static int DrawScrollableSelection(
            string label,
            IReadOnlyList<string> items,
            int selectedIndex,
            ref Vector2 scroll,
            bool disabled,
            string emptyMessage,
            string highlightName = null,
            string highlightTargetId = null,
            bool lockToHighlight = false)
        {
            using (new EditorGUI.DisabledScope(disabled))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (items != null)
                    {
                        EditorGUILayout.LabelField($"{items.Count:N0} items", EditorStyles.miniLabel, GUILayout.Width(90f));
                    }
                }
                if (items == null || items.Count == 0)
                {
                    EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                    return -1;
                }

                using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.MinHeight(60f), GUILayout.MaxHeight(100f)))
                {
                    scroll = scrollScope.scrollPosition;
                    for (int index = 0; index < items.Count; index++)
                    {
                        bool isSelected = index == selectedIndex;
                        bool allowSelect = !lockToHighlight
                                           || string.IsNullOrEmpty(highlightName)
                                           || string.Equals(items[index], highlightName, StringComparison.OrdinalIgnoreCase);
                        bool toggled;
                        using (new EditorGUI.DisabledScope(!allowSelect))
                        {
                            toggled = GUILayout.Toggle(isSelected, items[index], EditorStyles.miniButton);
                            if (toggled && !isSelected)
                            {
                                selectedIndex = index;
                            }
                        }

                        var buttonRect = GUILayoutUtility.GetLastRect();
                        if (!string.IsNullOrEmpty(highlightName)
                            && string.Equals(items[index], highlightName, StringComparison.OrdinalIgnoreCase))
                        {
                            TourOverlay.DrawHighlightIfActive(highlightTargetId, buttonRect, items[index]);
                        }
                    }
                }
            }

            return selectedIndex;
        }

        private List<string> GetCachedGameLabels()
        {
            var hashBuilder = new HashCode();
            hashBuilder.Add(_module._availableGames.Count);
            for (int i = 0; i < _module._availableGames.Count; i++)
            {
                var option = _module._availableGames[i];
                hashBuilder.Add(option.Key ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                hashBuilder.Add(option.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                hashBuilder.Add(option.FolderName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            }

            int hash = hashBuilder.ToHashCode();

            if (_cachedGameLabels == null || _cachedGameLabelsHash != hash)
            {
                _cachedGameLabels = new List<string>(_module._availableGames.Count);
                for (int i = 0; i < _module._availableGames.Count; i++)
                {
                    _cachedGameLabels.Add(_module._availableGames[i].DisplayName);
                }

                _cachedGameLabelsHash = hash;
            }

            return _cachedGameLabels;
        }

        private List<string> GetCachedVariantNames()
        {
            int version = _module._variants.Count;
            if (_cachedVariantNames == null || _cachedVariantNamesVersion != version)
            {
                _cachedVariantNames = new List<string>(version);
                for (int i = 0; i < _module._variants.Count; i++)
                    _cachedVariantNames.Add(_module._variants[i].Name);
                _cachedVariantNamesVersion = version;
            }
            return _cachedVariantNames;
        }

        private List<string> GetCachedPerCharVariantNames(string characterName, List<VariantOption> variants)
        {
            int version = variants != null ? variants.Count : 0;
            if (!_cachedPerCharVariantNames.TryGetValue(characterName, out var cached) || cached.version != version)
            {
                var names = new List<string>(version);
                if (variants != null)
                {
                    for (int i = 0; i < variants.Count; i++)
                        names.Add(variants[i].Name);
                }
                _cachedPerCharVariantNames[characterName] = (names, version);
                return names;
            }
            return cached.names;
        }

        private static void DrawTourCalloutForStep(string stepId)
        {
            if (!GuidedTourController.IsActive)
            {
                return;
            }

            var step = GuidedTourController.CurrentStep;
            if (!string.Equals(step.id, stepId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string body = step.instruction;
            if (stepId == StepIds.Game && GuidedTourController.ShouldShowManagerHandoffInline())
            {
                body = $"{body}\n\nYou're now in the HoyoToon Manager. Follow the inline highlights to continue.";
                GuidedTourController.MarkManagerHandoffShown();
            }

            TourCallout.Draw(
                $"Guided Tour: {step.title}",
                body,
                null,
                null);
        }
    }
}
#endif
