#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using HoyoToon.API;
using HoyoToon.Utilities;
using HoyoToon.EditorTools.Onboarding;

namespace HoyoToon.EditorTools.ManagerUI.Modules
{
    internal sealed class ModelsModule : HoyoToonManagerModule
    {
        private const string ShareUrl = "https://cdn.hoyotoon.com/home?path=cloudreve%3A%2F%2FpXIz%40share";
        private const string CharactersFolderName = "Characters";
        private const string DefaultVariantName = "Default";
        private const string DownloadPathPrefKey = "HoyoToon.ModelsDownloader.DownloadRoot";
        private const string DefaultDownloadRoot = "Assets/HoyoToon/Characters";
        private const string AutoSetupPrefKey = "HoyoToon.ModelsDownloader.AutoSetupAfterDownload";
        private const int MaxParallelDownloads = 8;

        private static readonly int MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

        private readonly Dictionary<string, GameFolderCache> _gameCache = new Dictionary<string, GameFolderCache>(StringComparer.OrdinalIgnoreCase);
        private readonly List<GameOption> _availableGames = new List<GameOption>();

        private int _selectedGameIndex = -1;
        private int _selectedCharacterIndex = -1;
        private int _selectedVariantIndex = -1;

        private List<string> _characters = new List<string>();
        private List<VariantOption> _variants = new List<VariantOption>();

        private bool _hasLoadedOnce;
        private bool _isRefreshing;
        private string _statusMessage;
        private string _downloadRoot;
        private bool _hasLoadedAutoSetupPreference;
        private bool _autoSetupAfterDownload = true;
        private string _characterSearch;
        private Vector2 _gameScroll;
        private Vector2 _characterScroll;
        private Vector2 _variantScroll;
        private Vector2 _variantSectionScroll;
        private readonly Dictionary<string, Vector2> _variantScrollByCharacter = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _selectedCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _selectedVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _selectedVariantsByCharacter = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loadingVariantsForCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HsrFbxChoice _hsrFbxChoice = HsrFbxChoice.WithAnims;

        public override string DisplayName => "Models";

        public override void OnGUI(HoyoToonManager targetManager)
        {
            if (targetManager == null)
            {
                EditorGUILayout.HelpBox("Assign a HoyoToon Manager to download and configure models.", MessageType.Info);
                return;
            }

            EnsureDownloadRoot();
            EnsureAutoSetupPreference();
            EnsureInitialLoad();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatus();
                DrawRefreshRow();
                EditorGUILayout.Space(4f);
                DrawGameSelection();
                DrawCharacterSelection();
                DrawVariantSelection();
                EditorGUILayout.Space(4f);
                DrawDownloadActions(targetManager);
            }
        }

        private void EnsureDownloadRoot()
        {
            if (!string.IsNullOrEmpty(_downloadRoot))
            {
                return;
            }

            _downloadRoot = EditorPrefs.GetString(DownloadPathPrefKey, DefaultDownloadRoot);
            if (string.IsNullOrWhiteSpace(_downloadRoot))
            {
                _downloadRoot = DefaultDownloadRoot;
            }
        }

        private void EnsureAutoSetupPreference()
        {
            if (_hasLoadedAutoSetupPreference)
            {
                return;
            }

            _hasLoadedAutoSetupPreference = true;
            _autoSetupAfterDownload = EditorPrefs.GetBool(AutoSetupPrefKey, true);
        }

        private void EnsureInitialLoad()
        {
            if (_hasLoadedOnce)
            {
                return;
            }

            _hasLoadedOnce = true;
            StartRefreshGameList();
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }
        }

        private void DrawRefreshRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh Game List", GUILayout.Width(160f)))
                {
                    StartRefreshGameList();
                }
                var refreshRect = GUILayoutUtility.GetLastRect();
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.models.refresh", refreshRect, "Refresh");
                GUILayout.FlexibleSpace();

                if (_isRefreshing)
                {
                    EditorGUILayout.LabelField("Loading...", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawGameSelection()
        {
            DrawTourCalloutForStep("game");
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                var labels = _availableGames.Select(option => option.DisplayName).ToList();
                bool lockSelection = HoyoToonGuidedTourController.ShouldLockGameSelection();
                int desiredIndex = lockSelection
                    ? _availableGames.FindIndex(option => string.Equals(option.DisplayName, HoyoToonGuidedTourController.DesiredGameName, StringComparison.OrdinalIgnoreCase))
                    : -1;
                int current = NormalizeSelectionIndex(_selectedGameIndex, labels.Count);
                int newIndex = DrawScrollableSelection(
                    "Game",
                    labels,
                    current,
                    ref _gameScroll,
                    _isRefreshing || (lockSelection && desiredIndex < 0),
                    "No games available from the API or CDN share.",
                    HoyoToonGuidedTourController.DesiredGameName,
                    "tour.models.game",
                    lockSelection);


                if (lockSelection && desiredIndex < 0)
                {
                    EditorGUILayout.HelpBox($"Guided tour expects '{HoyoToonGuidedTourController.DesiredGameName}'. Waiting for that game to load.", MessageType.Info);
                    return;
                }

                if (newIndex != current)
                {
                    _selectedGameIndex = newIndex;
                    if (_selectedGameIndex >= 0 && _selectedGameIndex < _availableGames.Count)
                    {
                        HoyoToonGuidedTourController.NotifyGameSelected(_availableGames[_selectedGameIndex].DisplayName);
                    }
                    ClearCharacterSelection();
                    EnsureCharactersLoaded();
                }
                else if (lockSelection && desiredIndex == current && desiredIndex >= 0 && _selectedGameIndex >= 0 && _selectedGameIndex < _availableGames.Count)
                {
                    HoyoToonGuidedTourController.NotifyGameSelected(_availableGames[_selectedGameIndex].DisplayName);
                }
            }
        }

        private void DrawCharacterSelection()
        {
            if (_selectedGameIndex < 0)
            {
                EditorGUILayout.HelpBox("Select a game to load characters.", MessageType.Info);
                return;
            }

            DrawTourCalloutForStep("character");
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                DrawCharacterSearchBar();

                var filteredCharacters = GetFilteredCharacters();
                if (HoyoToonGuidedTourController.ShouldLockCharacterSelection())
                {
                    ClearNonTourCharacterSelection();
                }
                if (!string.IsNullOrWhiteSpace(_characterSearch) && _characters != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Showing {filteredCharacters.Count:N0} / {_characters.Count:N0}", EditorStyles.miniLabel);
                    }
                }

                string emptyMessage = string.IsNullOrWhiteSpace(_characterSearch)
                    ? "No characters are available for this game."
                    : "No characters match the search filter.";

                DrawMultiSelectList(
                    "Character",
                    filteredCharacters,
                    _selectedCharacters,
                    ref _characterScroll,
                    _isRefreshing,
                    emptyMessage,
                    110f,
                    210f,
                    selectedName =>
                    {
                        _selectedCharacterIndex = _characters.FindIndex(name => string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase));
                        ClearVariantSelection();
                        EnsureVariantsLoaded();
                        HoyoToonGuidedTourController.NotifyCharacterSelected(selectedName);
                    },
                    HoyoToonGuidedTourController.DesiredCharacterName,
                    "tour.models.character",
                    HoyoToonGuidedTourController.ShouldLockCharacterSelection());

                if (_selectedCharacters.Count == 0)
                {
                    _selectedCharacterIndex = -1;
                }
                else if (_selectedCharacterIndex < 0 || _selectedCharacterIndex >= _characters.Count
                         || !_selectedCharacters.Contains(_characters[_selectedCharacterIndex]))
                {
                    _selectedCharacterIndex = _characters.FindIndex(name => _selectedCharacters.Contains(name));
                }
            }
        }

        private void DrawCharacterSearchBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Search", GUILayout.Width(50f));
                string newSearch = EditorGUILayout.TextField(_characterSearch ?? string.Empty, EditorStyles.toolbarSearchField);
                var clearStyle = GetToolbarSearchCancelStyle(string.IsNullOrEmpty(newSearch));
                if (GUILayout.Button(GUIContent.none, clearStyle))
                {
                    newSearch = string.Empty;
                    GUI.FocusControl(null);
                }

                if (!string.Equals(_characterSearch, newSearch, StringComparison.Ordinal))
                {
                    _characterSearch = newSearch;
                }
            }
        }

        private static GUIStyle _toolbarSearchCancelButton;
        private static GUIStyle _toolbarSearchCancelButtonEmpty;

        private static GUIStyle GetToolbarSearchCancelStyle(bool empty)
        {
            if (empty)
            {
                if (_toolbarSearchCancelButtonEmpty == null)
                {
                    _toolbarSearchCancelButtonEmpty = FindStyle(
                        "ToolbarSearchFieldCancelButtonEmpty",
                        "ToolbarSeachFieldCancelButtonEmpty",
                        "ToolbarSearchCancelButtonEmpty");
                    if (_toolbarSearchCancelButtonEmpty == null)
                    {
                        _toolbarSearchCancelButtonEmpty = GUI.skin.button;
                    }
                }

                return _toolbarSearchCancelButtonEmpty;
            }

            if (_toolbarSearchCancelButton == null)
            {
                _toolbarSearchCancelButton = FindStyle(
                    "ToolbarSearchFieldCancelButton",
                    "ToolbarSeachFieldCancelButton",
                    "ToolbarSearchCancelButton");
                if (_toolbarSearchCancelButton == null)
                {
                    _toolbarSearchCancelButton = GUI.skin.button;
                }
            }

            return _toolbarSearchCancelButton;
        }

        private static GUIStyle FindStyle(params string[] names)
        {
            if (GUI.skin == null || names == null)
            {
                return null;
            }

            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var style = GUI.skin.FindStyle(name);
                if (style != null)
                {
                    return style;
                }
            }

            return null;
        }

        private List<string> GetFilteredCharacters()
        {
            if (_characters == null)
            {
                return new List<string>();
            }

            if (string.IsNullOrWhiteSpace(_characterSearch))
            {
                return _characters.ToList();
            }

            return _characters
                .Where(name => !string.IsNullOrEmpty(name) && name.IndexOf(_characterSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void DrawVariantSelection()
        {
            if (_selectedCharacterIndex < 0)
            {
                EditorGUILayout.HelpBox("Select a character to load variants.", MessageType.Info);
                return;
            }

            DrawTourCalloutForStep("variant");
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                var game = _selectedGameIndex >= 0 && _selectedGameIndex < _availableGames.Count ? _availableGames[_selectedGameIndex] : null;
                var selectedCharacters = GetSelectedCharacterList();
                bool multipleCharacters = selectedCharacters.Count > 1;
                if (!multipleCharacters)
                {
                    var variantNames = _variants.Select(v => v.Name).ToList();
                    if (HoyoToonGuidedTourController.ShouldLockVariantSelection())
                    {
                        ClearNonTourVariantSelection();
                    }
                    DrawMultiSelectList(
                        "Variant",
                        variantNames,
                        _selectedVariants,
                        ref _variantScroll,
                        _isRefreshing,
                        "No variants are available for this character.",
                        60f,
                        110f,
                        selectedName =>
                        {
                            _selectedVariantIndex = _variants.FindIndex(option => string.Equals(option.Name, selectedName, StringComparison.OrdinalIgnoreCase));
                            HoyoToonGuidedTourController.NotifyVariantSelected(selectedName);
                        },
                        HoyoToonGuidedTourController.DesiredVariantName,
                        "tour.models.variant",
                        HoyoToonGuidedTourController.ShouldLockVariantSelection());

                    if (_selectedVariants.Count == 0)
                    {
                        _selectedVariantIndex = -1;
                    }
                    else if (_selectedVariantIndex < 0 || _selectedVariantIndex >= _variants.Count
                             || !_selectedVariants.Contains(_variants[_selectedVariantIndex].Name))
                    {
                        _selectedVariantIndex = _variants.FindIndex(option => _selectedVariants.Contains(option.Name));
                    }
                }
                else
                {
                    DrawPerCharacterVariantLists(selectedCharacters);
                }

                if (game != null && IsHonkaiStarRail(game.Key, game.DisplayName) && selectedCharacters.Count > 0)
                {
                    DrawTourCalloutForStep("fbx");
                    DrawStarRailFbxSelection(game);
                }
            }
        }

        private void DrawDownloadActions(HoyoToonManager manager)
        {
            DrawTourCalloutForStep("download");
            using (new EditorGUILayout.HorizontalScope())
            {
                bool tourActive = HoyoToonGuidedTourController.IsActive;
                bool enforcedAutoSetup = tourActive ? false : _autoSetupAfterDownload;
                bool newValue;
                using (new EditorGUI.DisabledScope(tourActive))
                {
                    newValue = EditorGUILayout.ToggleLeft("Auto setup after download", enforcedAutoSetup);
                }
                var toggleRect = GUILayoutUtility.GetLastRect();
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.models.autosetup", toggleRect, "Auto setup");
                if (tourActive)
                {
                    _autoSetupAfterDownload = false;
                }
                else if (newValue != _autoSetupAfterDownload)
                {
                    _autoSetupAfterDownload = newValue;
                    EditorPrefs.SetBool(AutoSetupPrefKey, _autoSetupAfterDownload);
                }

                GUILayout.FlexibleSpace();
                DrawDownloadButton(manager);
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

                using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll, false, true, GUILayout.MinHeight(minHeight), GUILayout.MaxHeight(maxHeight)))
                {
                    scroll = scrollScope.scrollPosition;
                    DrawToggleGrid(items, selected, onActivated, EditorGUIUtility.currentViewWidth - 40f, highlightName, highlightTargetId, lockToHighlight);
                }
            }
        }

        private List<string> GetSelectedCharacterList()
        {
            if (_selectedCharacters.Count > 0)
            {
                var ordered = new List<string>();
                foreach (var name in _characters)
                {
                    if (_selectedCharacters.Contains(name))
                    {
                        ordered.Add(name);
                    }
                }

                return ordered;
            }

            if (_selectedCharacterIndex >= 0 && _selectedCharacterIndex < _characters.Count)
            {
                return new List<string> { _characters[_selectedCharacterIndex] };
            }

            return new List<string>();
        }

        private void DrawPerCharacterVariantLists(List<string> characters)
        {
            if (characters == null || characters.Count == 0)
            {
                return;
            }

            var game = _selectedGameIndex >= 0 && _selectedGameIndex < _availableGames.Count ? _availableGames[_selectedGameIndex] : null;
            float maxHeight = Mathf.Clamp(100f + characters.Count * 22f, 120f, 220f);
            using (var scrollScope = new EditorGUILayout.ScrollViewScope(_variantSectionScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(maxHeight)))
            {
                _variantSectionScroll = scrollScope.scrollPosition;
                foreach (var characterName in characters)
                {
                    var selection = GetOrCreateVariantSelection(characterName);
                    var variants = GetVariantsForCharacter(game, characterName);

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

                        var variantNames = variants.Select(v => v.Name).ToList();
                        bool useScroll = variantNames.Count > 6;
                        if (useScroll)
                        {
                            var scroll = GetVariantScroll(characterName);
                            using (var variantsScroll = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.MinHeight(50f), GUILayout.MaxHeight(90f)))
                            {
                                scroll = variantsScroll.scrollPosition;
                                DrawToggleGrid(variantNames, selection, null, EditorGUIUtility.currentViewWidth - 60f, null, null, false);
                            }

                            SetVariantScroll(characterName, scroll);
                        }
                        else
                        {
                            DrawToggleGrid(variantNames, selection, null, EditorGUIUtility.currentViewWidth - 60f, null, null, false);
                        }
                    }
                }
            }
        }

        private static void DrawToggleGrid(
            IReadOnlyList<string> items,
            HashSet<string> selected,
            Action<string> onActivated,
            float viewWidth,
            string highlightName,
            string highlightTargetId,
            bool lockToHighlight)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            float available = Mathf.Max(120f, viewWidth - 20f);
            int columns = Mathf.Clamp(Mathf.FloorToInt(available / 150f), 1, 4);
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

                        var name = items[index];
                        bool wasSelected = selected.Contains(name);
                        bool allowSelect = !lockToHighlight
                                           || string.IsNullOrEmpty(highlightName)
                                           || string.Equals(name, highlightName, StringComparison.OrdinalIgnoreCase);
                        bool toggled;
                        using (new EditorGUI.DisabledScope(!allowSelect))
                        {
                            toggled = GUILayout.Toggle(wasSelected, name, EditorStyles.miniButton, GUILayout.Width(columnWidth), GUILayout.Height(20f));
                            if (toggled != wasSelected)
                            {
                                if (toggled)
                                {
                                    selected.Add(name);
                                    onActivated?.Invoke(name);
                                }
                                else
                                {
                                    selected.Remove(name);
                                }
                            }
                        }

                        var buttonRect = GUILayoutUtility.GetLastRect();
                        if (!string.IsNullOrEmpty(highlightName)
                            && string.Equals(name, highlightName, StringComparison.OrdinalIgnoreCase))
                        {
                            HoyoToonTourOverlay.DrawHighlightIfActive(highlightTargetId, buttonRect, name);
                        }
                    }
                }
            }
        }

        private void DrawStarRailFbxSelection(GameOption game)
        {
            if (game == null || !IsHonkaiStarRail(game.Key, game.DisplayName))
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("FBX (Star Rail)", EditorStyles.boldLabel);
            var options = new[] { "With Anims", "No Anims", "Both" };
            bool lockChoice = HoyoToonGuidedTourController.ShouldLockFbxSelection();
            int newIndex = DrawSingleSelectGrid(
                options,
                (int)_hsrFbxChoice,
                EditorGUIUtility.currentViewWidth - 40f,
                HoyoToonGuidedTourController.DesiredFbxChoiceName,
                "tour.models.fbx.noanims",
                lockChoice);

            if (newIndex != (int)_hsrFbxChoice)
            {
                _hsrFbxChoice = (HsrFbxChoice)newIndex;
                HoyoToonGuidedTourController.NotifyFbxChoiceSelected(options[newIndex]);
            }
            else if (lockChoice)
            {
                HoyoToonGuidedTourController.NotifyFbxChoiceSelected(options[(int)_hsrFbxChoice]);
            }
        }

        private static int DrawSingleSelectGrid(
            IReadOnlyList<string> options,
            int selectedIndex,
            float viewWidth,
            string highlightName,
            string highlightTargetId,
            bool lockToHighlight)
        {
            if (options == null || options.Count == 0)
            {
                return selectedIndex;
            }

            float available = Mathf.Max(120f, viewWidth - 20f);
            int columns = Mathf.Clamp(Mathf.FloorToInt(available / 150f), 1, 3);
            float columnWidth = Mathf.Floor(available / columns);
            int rowCount = Mathf.CeilToInt(options.Count / (float)columns);

            for (int row = 0; row < rowCount; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        if (index >= options.Count)
                        {
                            GUILayout.FlexibleSpace();
                            break;
                        }

                        bool isSelected = index == selectedIndex;
                        bool allowSelect = !lockToHighlight
                                           || string.IsNullOrEmpty(highlightName)
                                           || string.Equals(options[index], highlightName, StringComparison.OrdinalIgnoreCase);
                        bool toggled;
                        using (new EditorGUI.DisabledScope(!allowSelect))
                        {
                            toggled = GUILayout.Toggle(isSelected, options[index], EditorStyles.miniButton, GUILayout.Width(columnWidth), GUILayout.Height(20f));
                            if (toggled && !isSelected)
                            {
                                selectedIndex = index;
                            }
                        }

                        var buttonRect = GUILayoutUtility.GetLastRect();
                        if (!string.IsNullOrEmpty(highlightName)
                            && string.Equals(options[index], highlightName, StringComparison.OrdinalIgnoreCase))
                        {
                            HoyoToonTourOverlay.DrawHighlightIfActive(highlightTargetId, buttonRect, options[index]);
                        }
                    }
                }
            }

            return selectedIndex;
        }

        private Vector2 GetVariantScroll(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return Vector2.zero;
            }

            if (!_variantScrollByCharacter.TryGetValue(characterName, out var scroll))
            {
                scroll = Vector2.zero;
                _variantScrollByCharacter[characterName] = scroll;
            }

            return scroll;
        }

        private void SetVariantScroll(string characterName, Vector2 scroll)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return;
            }

            _variantScrollByCharacter[characterName] = scroll;
        }

        private HashSet<string> GetOrCreateVariantSelection(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!_selectedVariantsByCharacter.TryGetValue(characterName, out var selection))
            {
                selection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _selectedVariantsByCharacter[characterName] = selection;
            }

            return selection;
        }

        private List<VariantOption> GetVariantsForCharacter(GameOption game, string characterName)
        {
            if (game == null || string.IsNullOrEmpty(characterName))
            {
                return null;
            }

            var cache = GetOrCreateCache(game);
            if (cache != null && cache.VariantsByCharacter.TryGetValue(characterName, out var cached) && cached.Count > 0)
            {
                return cached;
            }

            EnsureVariantsLoadedForCharacter(game, characterName);
            return null;
        }

        private void EnsureVariantsLoadedForCharacter(GameOption game, string characterName)
        {
            if (game == null || string.IsNullOrEmpty(characterName))
            {
                return;
            }

            if (_loadingVariantsForCharacters.Contains(characterName))
            {
                return;
            }

            _loadingVariantsForCharacters.Add(characterName);

            HoyoToonAsyncUtil.RunFireAndForget(() => LoadVariantsAsync(game, characterName), "Load model variants", ex =>
            {
                ScheduleOnMainThread(() =>
                {
                    _loadingVariantsForCharacters.Remove(characterName);
                    _statusMessage = $"Failed to load variants: {ex.Message}";
                });
            });
        }

        private void DrawDownloadPath()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _downloadRoot = EditorGUILayout.TextField("Download Folder", _downloadRoot);
                if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                {
                    var selected = EditorUtility.OpenFolderPanel("Choose Download Folder", Application.dataPath, string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        if (TryConvertToAssetsPath(selected, out var assetsPath))
                        {
                            _downloadRoot = assetsPath;
                            EditorPrefs.SetString(DownloadPathPrefKey, _downloadRoot);
                        }
                        else
                        {
                            HoyoToonDialogWindow.ShowError("Invalid Folder", "Please select a folder inside the project's Assets directory.");
                        }
                    }
                }
            }
        }

        private void DrawDownloadButton(HoyoToonManager manager)
        {
            bool ready = TryGetBatchSelection(out _, out var characters, out var variants)
                         && characters.Count > 0
                         && variants.Count > 0;
            using (new EditorGUI.DisabledScope(!ready || _isRefreshing))
            {
                if (GUILayout.Button("Download Selected Models", GUILayout.Width(190f)))
                {
                    StartDownloadSelected(manager);
                }
                var downloadRect = GUILayoutUtility.GetLastRect();
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.models.download", downloadRect, "Download");
            }
        }

        private void StartRefreshGameList()
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            _statusMessage = "Loading games from CDN...";
            ClearSelections();

            HoyoToonAsyncUtil.RunFireAndForget(RefreshGameListAsync, "Refresh model game list", ex =>
            {
                ScheduleOnMainThread(() =>
                {
                    _isRefreshing = false;
                    _statusMessage = $"Failed to load games: {ex.Message}";
                });
            });
        }

        private async Task RefreshGameListAsync()
        {
            var apiGames = HoyoToonApi.GetGames().Values
                .Where(game => game != null && !string.IsNullOrEmpty(game.Key))
                .ToList();

            var rootEntries = await HoyoToonCloudreveClient.GetDirectoryEntriesAsync(ShareUrl, string.Empty);
            var rootFolders = rootEntries.Where(entry => entry.IsDirectory).Select(entry => entry.Name).ToList();

            var options = new List<GameOption>();
            foreach (var game in apiGames)
            {
                if (TryMatchFolder(rootFolders, game, out var folderName))
                {
                    options.Add(new GameOption(game, folderName));
                }
            }

            options = options.OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

            ScheduleOnMainThread(() =>
            {
                _availableGames.Clear();
                _availableGames.AddRange(options);
                _isRefreshing = false;
                _statusMessage = options.Count > 0 ? null : "No matching games were found in the CDN share.";
                _selectedGameIndex = options.Count > 0 ? 0 : -1;
                _gameCache.Clear();
                ClearCharacterSelection();
                EnsureCharactersLoaded();
            });
        }

        private void EnsureCharactersLoaded()
        {
            if (_selectedGameIndex < 0 || _selectedGameIndex >= _availableGames.Count)
            {
                return;
            }

            var game = _availableGames[_selectedGameIndex];
            var cache = GetOrCreateCache(game);
            if (cache.Characters != null && cache.Characters.Count > 0)
            {
                _characters = cache.Characters;
                _selectedCharacterIndex = _characters.Count > 0 ? 0 : -1;
                EnsureVariantsLoaded();
                return;
            }

            if (cache.IsLoadingCharacters)
            {
                return;
            }

            cache.IsLoadingCharacters = true;
            _statusMessage = "Loading characters...";

            HoyoToonAsyncUtil.RunFireAndForget(() => LoadCharactersAsync(game), "Load model characters", ex =>
            {
                ScheduleOnMainThread(() =>
                {
                    cache.IsLoadingCharacters = false;
                    _statusMessage = $"Failed to load characters: {ex.Message}";
                });
            });
        }

        private async Task LoadCharactersAsync(GameOption game)
        {
            var cache = GetOrCreateCache(game);
            var gameFolder = game.FolderName;

            var gameEntries = await HoyoToonCloudreveClient.GetDirectoryEntriesAsync(ShareUrl, gameFolder);
            var charactersFolder = FindFolderName(gameEntries, CharactersFolderName) ?? CharactersFolderName;

            var characterPath = CombineSharePath(gameFolder, charactersFolder);
            var entries = await HoyoToonCloudreveClient.GetDirectoryEntriesAsync(ShareUrl, characterPath);
            var characters = entries.Where(entry => entry.IsDirectory)
                .Select(entry => entry.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ScheduleOnMainThread(() =>
            {
                cache.CharactersFolderName = charactersFolder;
                cache.Characters = characters;
                cache.IsLoadingCharacters = false;

                if (!IsSelectedGame(game.Key))
                {
                    return;
                }

                _characters = characters;
                _selectedCharacterIndex = characters.Count > 0 ? 0 : -1;
                _statusMessage = characters.Count > 0 ? null : "No characters were found for this game.";
                EnsureVariantsLoaded();
            });
        }

        private void EnsureVariantsLoaded()
        {
            if (_selectedGameIndex < 0 || _selectedGameIndex >= _availableGames.Count)
            {
                return;
            }

            if (_selectedCharacterIndex < 0 || _selectedCharacterIndex >= _characters.Count)
            {
                return;
            }

            var game = _availableGames[_selectedGameIndex];
            var cache = GetOrCreateCache(game);
            var characterName = _characters[_selectedCharacterIndex];

            if (cache.VariantsByCharacter.TryGetValue(characterName, out var cachedVariants) && cachedVariants.Count > 0)
            {
                _variants = cachedVariants;
                _selectedVariantIndex = _variants.Count > 0 ? 0 : -1;
                return;
            }

            if (cache.IsLoadingVariants)
            {
                return;
            }

            cache.IsLoadingVariants = true;
            _statusMessage = "Loading variants...";

            HoyoToonAsyncUtil.RunFireAndForget(() => LoadVariantsAsync(game, characterName), "Load model variants", ex =>
            {
                ScheduleOnMainThread(() =>
                {
                    cache.IsLoadingVariants = false;
                    _statusMessage = $"Failed to load variants: {ex.Message}";
                });
            });
        }

        private async Task LoadVariantsAsync(GameOption game, string characterName)
        {
            var cache = GetOrCreateCache(game);
            var gameFolder = game.FolderName;
            var charactersFolder = cache.CharactersFolderName ?? CharactersFolderName;

            var characterPath = CombineSharePath(gameFolder, charactersFolder, characterName);
            var entries = await HoyoToonCloudreveClient.GetDirectoryEntriesAsync(ShareUrl, characterPath);
            var variants = entries.Where(entry => entry.IsDirectory)
                .Select(entry => new VariantOption(entry.Name, CombineSharePath(characterPath, entry.Name)))
                .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool hasFiles = entries.Any(entry => !entry.IsDirectory);
            if (hasFiles || variants.Count == 0)
            {
                variants.Insert(0, new VariantOption(DefaultVariantName, characterPath));
            }

            ScheduleOnMainThread(() =>
            {
                cache.IsLoadingVariants = false;
                cache.VariantsByCharacter[characterName] = variants;

                if (!IsSelectedGame(game.Key) || !IsSelectedCharacter(characterName))
                {
                    return;
                }

                _variants = variants;
                _selectedVariantIndex = _variants.Count > 0 ? 0 : -1;
                _statusMessage = _variants.Count > 0 ? null : "No variants were found for this character.";
            });
        }

        private void StartDownloadSelected(HoyoToonManager manager)
        {
            if (!TryGetBatchSelection(out var game, out var characters, out var variants))
            {
                return;
            }

            if (characters.Count == 1 && variants.Count == 1)
            {
                string characterName = characters[0];
                string variantName = variants[0];
                var variant = ResolveVariantForCharacter(game, characterName, variantName);
                if (variant == null)
                {
                    return;
                }

                HoyoToonAsyncUtil.RunFireAndForget(
                    () => DownloadSelectedAsync(manager, game, characterName, variant, true, true, true, _hsrFbxChoice),
                    "Download model",
                    ex => ScheduleOnMainThread(() => { _statusMessage = $"Download failed: {ex.Message}"; }));
                return;
            }

            StartBatchDownload(game, characters, variants);
        }

        private async Task<string> DownloadSelectedAsync(
            HoyoToonManager manager,
            GameOption game,
            string characterName,
            VariantOption variant,
            bool showProgress,
            bool showPostDownloadPrompt,
            bool refreshAssets,
            HsrFbxChoice fbxChoice)
        {
            var assetRoot = EnsureAssetsRoot(_downloadRoot);
            if (string.IsNullOrEmpty(assetRoot))
            {
                ScheduleOnMainThread(() =>
                {
                    HoyoToonDialogWindow.ShowError("Invalid Folder", "Please select a download folder inside Assets.");
                });
                return null;
            }

            var assetTarget = BuildAssetTargetPath(assetRoot, game, characterName, variant);
            var absoluteTarget = AbsoluteFromAssetsPath(assetTarget);
            Directory.CreateDirectory(absoluteTarget);

            if (showProgress)
            {
                if (showProgress)
                {
                    HoyoToonProgressDialog.Start("Downloading Model", "Preparing download...");
                }
            }

            var remoteFiles = await HoyoToonCloudreveClient.GetFileListAsync(ShareUrl, variant.RelativePath, true);
            var files = remoteFiles.Where(file => !file.IsDirectory).ToList();

            if (IsHonkaiStarRail(game.Key, game.DisplayName))
            {
                var rootEntries = await HoyoToonCloudreveClient.GetFileListAsync(ShareUrl, variant.RelativePath, false);
                var rootFbxFiles = rootEntries
                    .Where(file => !file.IsDirectory && file.RelativePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (rootFbxFiles.Count > 0)
                {
                    var orderedFbxFiles = rootFbxFiles
                        .OrderBy(file => Path.GetFileNameWithoutExtension(file.RelativePath) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var chosen = FilterFbxFilesByChoice(orderedFbxFiles, fbxChoice);
                    var chosenPaths = new HashSet<string>(chosen.Select(file => file.RelativePath), StringComparer.OrdinalIgnoreCase);
                    files = files.Where(file => !file.RelativePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        .Concat(files.Where(file => chosenPaths.Contains(file.RelativePath)))
                        .ToList();
                }
            }

            int total = files.Count;
            int completed = 0;

            var existingFiles = new List<string>();
            foreach (var file in files)
            {
                var relative = TrimPrefix(file.RelativePath, variant.RelativePath);
                var safeRelative = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                var localPath = Path.Combine(absoluteTarget, safeRelative);
                if (File.Exists(localPath))
                {
                    existingFiles.Add(localPath);
                }
            }

            if (existingFiles.Count > 0)
            {
                var overwrite = await PromptOverwriteAsync(existingFiles.Count, characterName, variant.Name);
                if (!overwrite.HasValue)
                {
                    if (showProgress)
                    {
                        HoyoToonProgressDialog.End("Download cancelled.");
                    }
                    return null;
                }

                if (overwrite.Value == false)
                {
                    files = files.Where(file =>
                    {
                        var relative = TrimPrefix(file.RelativePath, variant.RelativePath);
                        var safeRelative = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                        var localPath = Path.Combine(absoluteTarget, safeRelative);
                        return !File.Exists(localPath);
                    }).ToList();
                }
            }

            foreach (var file in files)
            {
                var relative = TrimPrefix(file.RelativePath, variant.RelativePath);
                var safeRelative = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                var localPath = Path.Combine(absoluteTarget, safeRelative);

                var directory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await HoyoToonCloudreveClient.DownloadFileAsync(HoyoToonCloudreveClient.SharedClient, file, ShareUrl, localPath);

                completed++;
                var progress = total > 0 ? (float)completed / total : 1f;
                if (showProgress)
                {
                    HoyoToonProgressDialog.Update(progress, $"Downloading {Path.GetFileName(localPath)} ({completed}/{total})");
                }
            }

            if (showProgress)
            {
                HoyoToonProgressDialog.End("Download complete!");
            }

            if (refreshAssets || showPostDownloadPrompt)
            {
                ScheduleOnMainThread(() =>
                {
                    if (refreshAssets)
                    {
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }

                    if (showPostDownloadPrompt)
                    {
                        ShowPostDownloadPrompt(manager, assetTarget, game.Key, game.DisplayName, characterName, variant.Name);
                    }

                    HoyoToonGuidedTourController.NotifyModelDownloaded();
                });
            }

            return assetTarget;
        }

        private void StartBatchDownload(GameOption game, List<string> characters, List<string> variants)
        {
            if (game == null || characters == null || variants == null || characters.Count == 0 || variants.Count == 0)
            {
                return;
            }

            if (IsHonkaiStarRail(game.Key, game.DisplayName) && variants.Count > 1)
            {
                PromptBatchVariantChoice(variants, selectedVariant =>
                {
                    if (string.IsNullOrEmpty(selectedVariant))
                    {
                        return;
                    }

                    HoyoToonAsyncUtil.RunFireAndForget(
                        () => DownloadBatchAsync(game, BuildBatchVariantMap(characters, new List<string> { selectedVariant })),
                        "Batch download models",
                        ex => ScheduleOnMainThread(() => { _statusMessage = $"Batch download failed: {ex.Message}"; }));
                });
                return;
            }

            HoyoToonAsyncUtil.RunFireAndForget(
                () => DownloadBatchAsync(game, BuildBatchVariantMap(characters, variants)),
                "Batch download models",
                ex => ScheduleOnMainThread(() => { _statusMessage = $"Batch download failed: {ex.Message}"; }));
        }

        private async Task DownloadBatchAsync(GameOption game, Dictionary<string, List<string>> characterVariants)
        {
            var assetRoot = EnsureAssetsRoot(_downloadRoot);
            if (string.IsNullOrEmpty(assetRoot))
            {
                ScheduleOnMainThread(() =>
                {
                    HoyoToonDialogWindow.ShowError("Invalid Folder", "Please select a download folder inside Assets.");
                });
                return;
            }

            if (game == null || characterVariants == null || characterVariants.Count == 0)
            {
                return;
            }

            var manager = UnityEngine.Object.FindObjectOfType<HoyoToonManager>(true);
            var cache = GetOrCreateCache(game);
            var resolved = new List<(string characterName, VariantOption variant)>();
            foreach (var entry in characterVariants)
            {
                var characterName = entry.Key;
                var variantNames = entry.Value ?? new List<string>();
                foreach (var variantName in variantNames)
                {
                    var variant = await ResolveVariantForCharacterAsync(game, cache, characterName, variantName);
                    if (variant != null)
                    {
                        resolved.Add((characterName, variant));
                    }
                }
            }

            int total = resolved.Count;
            int completed = 0;
            var downloaded = new ConcurrentBag<(string assetTarget, string characterName, string variantName)>();

            if (total > 0)
            {
                HoyoToonProgressDialog.Start("Downloading Models", "Preparing batch download...");
            }

            var gate = new SemaphoreSlim(MaxParallelDownloads, MaxParallelDownloads);
            var tasks = new List<Task>();
            foreach (var job in resolved)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await gate.WaitAsync();
                    try
                    {
                        var assetTarget = await DownloadSelectedAsync(manager, game, job.characterName, job.variant, false, false, false, _hsrFbxChoice);
                        if (!string.IsNullOrEmpty(assetTarget))
                        {
                            downloaded.Add((assetTarget, job.characterName, job.variant.Name));
                        }
                    }
                    finally
                    {
                        gate.Release();
                        int done = Interlocked.Increment(ref completed);
                        if (total > 0)
                        {
                            ScheduleOnMainThread(() =>
                            {
                                float progress = total > 0 ? (float)done / total : 1f;
                                HoyoToonProgressDialog.Update(progress, $"Downloading {job.characterName} ({done}/{total})");
                            });
                        }
                    }
                }));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            finally
            {
                gate.Dispose();
                if (total > 0)
                {
                    HoyoToonProgressDialog.End("Batch download complete!");
                    HoyoToonGuidedTourController.NotifyModelDownloaded();
                }
            }

            ScheduleOnMainThread(() =>
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (_autoSetupAfterDownload)
                {
                    foreach (var item in downloaded)
                    {
                        TryAutoSetupDownloaded(manager, item.assetTarget, game.Key, game.DisplayName, item.characterName, item.variantName);
                    }
                }
            });
        }

        private Dictionary<string, List<string>> BuildBatchVariantMap(List<string> characters, List<string> variants)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (characters == null || variants == null)
            {
                return map;
            }

            bool usePerCharacter = _selectedCharacters.Count > 1 && _selectedVariantsByCharacter.Count > 0;
            foreach (var character in characters)
            {
                if (string.IsNullOrWhiteSpace(character))
                {
                    continue;
                }

                if (usePerCharacter && _selectedVariantsByCharacter.TryGetValue(character, out var selected) && selected.Count > 0)
                {
                    map[character] = selected.ToList();
                }
                else
                {
                    map[character] = new List<string>(variants);
                }
            }

            return map;
        }

        private async Task<VariantOption> ResolveVariantForCharacterAsync(GameOption game, GameFolderCache cache, string characterName, string variantName)
        {
            if (cache != null && cache.VariantsByCharacter.TryGetValue(characterName, out var cached) && cached.Count > 0)
            {
                return PickVariant(cached, variantName);
            }

            var gameFolder = game.FolderName;
            var charactersFolder = cache?.CharactersFolderName ?? CharactersFolderName;
            var characterPath = CombineSharePath(gameFolder, charactersFolder, characterName);
            var entries = await HoyoToonCloudreveClient.GetDirectoryEntriesAsync(ShareUrl, characterPath);
            var variants = entries.Where(entry => entry.IsDirectory)
                .Select(entry => new VariantOption(entry.Name, CombineSharePath(characterPath, entry.Name)))
                .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool hasFiles = entries.Any(entry => !entry.IsDirectory);
            if (hasFiles || variants.Count == 0)
            {
                variants.Insert(0, new VariantOption(DefaultVariantName, characterPath));
            }

            if (cache != null)
            {
                cache.VariantsByCharacter[characterName] = variants;
            }

            return PickVariant(variants, variantName);
        }

        private VariantOption ResolveVariantForCharacter(GameOption game, string characterName, string variantName)
        {
            if (game == null || string.IsNullOrEmpty(characterName))
            {
                return null;
            }

            var cache = GetOrCreateCache(game);
            if (cache.VariantsByCharacter.TryGetValue(characterName, out var cached) && cached.Count > 0)
            {
                return PickVariant(cached, variantName);
            }

            if (IsSelectedCharacter(characterName) && _variants != null && _variants.Count > 0)
            {
                return PickVariant(_variants, variantName);
            }

            return null;
        }

        private void PromptBatchVariantChoice(List<string> variants, Action<string> onSelected)
        {
            if (variants == null || variants.Count == 0)
            {
                onSelected?.Invoke(null);
                return;
            }

            var options = variants
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            options.Add("Cancel");

            HoyoToonDialogWindow.ShowCustom(
                "Choose Variant",
                "Multiple variants are selected. Choose one variant to apply to all selected characters:",
                MessageType.Info,
                options.ToArray(),
                0,
                options.Count - 1,
                result =>
                {
                    if (result < 0 || result >= options.Count - 1)
                    {
                        onSelected?.Invoke(null);
                        return;
                    }

                    onSelected?.Invoke(options[result]);
                });
        }

        private static VariantOption PickVariant(List<VariantOption> variants, string variantName)
        {
            if (variants == null || variants.Count == 0)
            {
                return null;
            }

            var match = variants.FirstOrDefault(option => string.Equals(option.Name, variantName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }

            return variants.FirstOrDefault(option => string.Equals(option.Name, DefaultVariantName, StringComparison.OrdinalIgnoreCase))
                   ?? variants[0];
        }

        private void ShowPostDownloadPrompt(HoyoToonManager manager, string assetTarget, string gameKey, string gameName, string characterName, string variantName)
        {
            if (_autoSetupAfterDownload)
            {
                TryAutoSetupDownloaded(manager, assetTarget, gameKey, gameName, characterName, variantName);
            }
        }

        private void TryAutoSetupDownloaded(HoyoToonManager manager, string assetFolder, string gameKey, string gameName, string characterName, string variantName)
        {
            if (manager == null)
            {
                HoyoToonDialogWindow.ShowError("Manager Missing", "Cannot setup without an active HoyoToon Manager in the scene.");
                return;
            }

            ResolvePrimaryAssetWithChoice(assetFolder, gameKey, gameName, characterName, variantName, _hsrFbxChoice, (assetPath, isFbx, isPrefab) =>
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    HoyoToonDialogWindow.ShowError("No Model Found", "Could not find an FBX in the downloaded folder.");
                    return;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null)
                {
                    HoyoToonDialogWindow.ShowError("Missing Asset", "Could not load the downloaded model asset.");
                    return;
                }

                if (isFbx)
                {
                    if (HoyoToonModelSetupUtility.TryProcessFbxAndInstantiate(manager, asset, out var instance))
                    {
                        if (instance != null)
                        {
                            RegisterAndSelect(manager, instance);
                            HoyoToonGuidedTourController.NotifyAutoSetupCompleted(instance);
                        }
                        else
                        {
                            HoyoToonDialogWindow.ShowWarning("Auto Setup Complete", "No setup steps were applicable for this model. Try Instantiate to add it to the scene.");
                        }
                    }
                    return;
                }
            });
        }

        private static void RegisterAndSelect(HoyoToonManager manager, GameObject instance)
        {
            if (instance == null || manager == null)
            {
                return;
            }

            manager.RegisterModel(instance);
            if (manager.ManagedModels != null && manager.ManagedModels.Count > 0)
            {
                manager.ActiveModelIndex = manager.ManagedModels.Count - 1;
            }

            if (!HoyoToonGuidedTourController.IsActive
                || !string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "autosetup", StringComparison.OrdinalIgnoreCase))
            {
                Selection.activeObject = instance;
                EditorGUIUtility.PingObject(instance);
            }
        }

        private static void ResolvePrimaryAssetWithChoice(string assetFolder, string gameKey, string gameName, string characterName, string variantName, HsrFbxChoice fbxChoice, Action<string, bool, bool> onResolved)
        {
            if (onResolved == null)
            {
                return;
            }

            bool isStarRail = IsHonkaiStarRail(gameKey, gameName);
            if (!TryGatherAssetCandidates(assetFolder, characterName, variantName, isStarRail, out var prefabs, out var fbxs))
            {
                onResolved(null, false, false);
                return;
            }
            if (fbxs.Count == 0)
            {
                onResolved(null, false, false);
                return;
            }

            if (isStarRail)
            {
                fbxs = FilterLocalFbxByChoice(fbxs, fbxChoice);
            }

            string chosen = fbxs.Count > 1
                ? SelectBestCandidate(fbxs, characterName, variantName)
                : fbxs[0];

            onResolved(AssetsPathFromAbsolute(chosen), true, false);
        }

        private static bool TryGatherAssetCandidates(string assetFolder, string characterName, string variantName, bool rootOnly, out List<string> prefabs, out List<string> fbxs)
        {
            prefabs = new List<string>();
            fbxs = new List<string>();

            var absoluteFolder = AbsoluteFromAssetsPath(assetFolder);
            if (string.IsNullOrEmpty(absoluteFolder) || !Directory.Exists(absoluteFolder))
            {
                return false;
            }

            var searchOption = rootOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            fbxs = Directory.GetFiles(absoluteFolder, "*.fbx", searchOption).ToList();
            return fbxs.Count > 0;
        }

        private static List<string> FilterLocalFbxByChoice(List<string> fbxs, HsrFbxChoice choice)
        {
            if (fbxs == null || fbxs.Count == 0)
            {
                return fbxs ?? new List<string>();
            }

            if (choice == HsrFbxChoice.Both)
            {
                return fbxs;
            }

            var desired = choice == HsrFbxChoice.NoAnims ? FbxVariantType.NoAnims : FbxVariantType.WithAnims;
            var matches = fbxs.Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                return GetFbxVariantType(name) == desired;
            }).ToList();

            if (matches.Count > 0)
            {
                return matches;
            }

            return fbxs.Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                var type = GetFbxVariantType(name);
                return type == FbxVariantType.Unknown || type == desired;
            }).ToList();
        }

        private static bool IsHonkaiStarRail(string gameKey, string gameName)
        {
            if (!string.IsNullOrEmpty(gameKey))
            {
                if (gameKey.IndexOf("StarRail", StringComparison.OrdinalIgnoreCase) >= 0
                    || gameKey.IndexOf("HonkaiStarRail", StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(gameKey, "HSR", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(gameName))
            {
                return gameName.IndexOf("Honkai Star Rail", StringComparison.OrdinalIgnoreCase) >= 0
                       || gameName.IndexOf("Star Rail", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return false;
        }

        private static void ShowFbxChoiceDialog(List<string> fbxs, string characterName, string variantName, Action<string, bool, bool> onResolved)
        {
            if (fbxs == null || fbxs.Count == 0)
            {
                onResolved(null, false, false);
                return;
            }

            var ordered = fbxs
                .OrderBy(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var labels = ordered
                .Take(2)
                .Select(path => BuildFbxLabel(path, characterName, variantName))
                .ToList();

            if (ordered.Count > 1)
            {
                labels.Add("Both");
            }

            labels.Add("Cancel");

            var message = "Multiple FBX files were found for this model. Choose which one to use:";
            HoyoToonDialogWindow.ShowCustom("Choose FBX", message, MessageType.Info, labels.ToArray(), 0, labels.Count - 1, result =>
            {
                if (result < 0)
                {
                    onResolved(null, false, false);
                    return;
                }

                int maxSingle = Math.Min(ordered.Count, 2);
                if (result < maxSingle)
                {
                    onResolved(AssetsPathFromAbsolute(ordered[result]), true, false);
                    return;
                }

                onResolved(null, false, false);
            });
        }

        private static Task<int> PromptFbxChoiceAsync(List<RemoteFileInfo> fbxs, string characterName, string variantName)
        {
            var tcs = new TaskCompletionSource<int>();
            if (fbxs == null || fbxs.Count == 0)
            {
                tcs.SetResult(-1);
                return tcs.Task;
            }

            var labels = fbxs
                .Take(2)
                .Select(file => BuildFbxLabel(file.RelativePath, characterName, variantName))
                .ToList();

            if (fbxs.Count > 1)
            {
                labels.Add("Both");
            }

            labels.Add("Cancel");

            ScheduleOnMainThread(() =>
            {
                var message = "Multiple FBX files were found for this variant. Choose which FBX to download:";
                HoyoToonDialogWindow.ShowCustom("Choose FBX", message, MessageType.Info, labels.ToArray(), 0, labels.Count - 1, result =>
                {
                    if (result < 0)
                    {
                        tcs.SetResult(-1);
                        return;
                    }

                    int maxSingle = Math.Min(fbxs.Count, 2);
                    if (result < maxSingle)
                    {
                        tcs.SetResult(result);
                        return;
                    }

                    tcs.SetResult(-2);
                });
            });

            return tcs.Task;
        }

        private static Task<bool?> PromptOverwriteAsync(int existingCount, string characterName, string variantName)
        {
            var tcs = new TaskCompletionSource<bool?>();
            var message = $"{existingCount} files already exist for {characterName} ({variantName}).\n\nWhat would you like to do?";
            var buttons = new[] { "Overwrite", "Skip Existing", "Cancel" };

            ScheduleOnMainThread(() =>
            {
                HoyoToonDialogWindow.ShowCustom("Files Already Exist", message, MessageType.Warning, buttons, 0, 2, result =>
                {
                    switch (result)
                    {
                        case 0:
                            tcs.SetResult(true);
                            break;
                        case 1:
                            tcs.SetResult(false);
                            break;
                        default:
                            tcs.SetResult(null);
                            break;
                    }
                });
            });

            return tcs.Task;
        }

        private static List<RemoteFileInfo> FilterFbxFilesByChoice(List<RemoteFileInfo> fbxs, HsrFbxChoice choice)
        {
            if (fbxs == null || fbxs.Count == 0)
            {
                return fbxs ?? new List<RemoteFileInfo>();
            }

            if (choice == HsrFbxChoice.Both)
            {
                return fbxs;
            }

            var desired = choice == HsrFbxChoice.NoAnims ? FbxVariantType.NoAnims : FbxVariantType.WithAnims;
            var matches = fbxs.Where(file =>
            {
                var fileName = Path.GetFileNameWithoutExtension(file.RelativePath) ?? string.Empty;
                return GetFbxVariantType(fileName) == desired;
            }).ToList();

            if (matches.Count > 0)
            {
                return matches;
            }

            return fbxs.Where(file =>
            {
                var fileName = Path.GetFileNameWithoutExtension(file.RelativePath) ?? string.Empty;
                var type = GetFbxVariantType(fileName);
                return type == FbxVariantType.Unknown || type == desired;
            }).ToList();
        }

        private static string BuildFbxLabel(string path, string characterName, string variantName)
        {
            var fileName = Path.GetFileNameWithoutExtension(path) ?? "FBX";
            var fbxType = GetFbxVariantType(fileName);
            string hint = fbxType switch
            {
                FbxVariantType.NoAnims => "(No Anims)",
                FbxVariantType.WithAnims => "(With Anims)",
                _ => string.Empty
            };

            return string.IsNullOrEmpty(hint) ? fileName : $"{fileName} {hint}";
        }

        private static FbxVariantType GetFbxVariantType(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return FbxVariantType.Unknown;
            }

            var tokens = ExtractTokens(fileName);
            bool hasNo = tokens.Contains("no");
            bool hasWith = tokens.Contains("with");
            bool hasAnim = tokens.Contains("anim") || tokens.Contains("anims") || tokens.Contains("animation") || tokens.Contains("animations");

            if (tokens.Contains("noanim") || tokens.Contains("noanims") || tokens.Contains("noanimation") || tokens.Contains("noanimations") || (hasNo && hasAnim))
            {
                return FbxVariantType.NoAnims;
            }

            if (tokens.Contains("withanim") || tokens.Contains("withanims") || tokens.Contains("withanimation") || tokens.Contains("withanimations") || (hasWith && hasAnim) || hasAnim)
            {
                return FbxVariantType.WithAnims;
            }

            return FbxVariantType.Unknown;
        }

        private static List<string> ExtractTokens(string value)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(value))
            {
                return tokens;
            }

            var buffer = new StringBuilder();
            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    buffer.Append(char.ToLowerInvariant(character));
                }
                else if (buffer.Length > 0)
                {
                    tokens.Add(buffer.ToString());
                    buffer.Clear();
                }
            }

            if (buffer.Length > 0)
            {
                tokens.Add(buffer.ToString());
            }

            return tokens;
        }

        private static string SelectBestCandidate(List<string> paths, string characterName, string variantName)
        {
            if (paths == null || paths.Count == 0)
            {
                return null;
            }

            string normalizedCharacter = NormalizeKey(characterName);
            string normalizedVariant = NormalizeKey(variantName);
            bool hasVariant = !string.IsNullOrEmpty(normalizedVariant) && !string.Equals(variantName, DefaultVariantName, StringComparison.OrdinalIgnoreCase);

            string bestPath = null;
            int bestScore = int.MinValue;
            long bestSize = -1;
            int bestDepth = int.MaxValue;

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                string normalizedName = NormalizeKey(fileName);

                int score = 0;
                if (!string.IsNullOrEmpty(normalizedCharacter) && normalizedName.Contains(normalizedCharacter))
                {
                    score += 100;
                }

                if (hasVariant && normalizedName.Contains(normalizedVariant))
                {
                    score += 50;
                }

                long size = 0;
                try
                {
                    size = new FileInfo(path).Length;
                }
                catch
                {
                    size = 0;
                }

                int depth = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;

                if (score > bestScore
                    || (score == bestScore && size > bestSize)
                    || (score == bestScore && size == bestSize && depth < bestDepth))
                {
                    bestScore = score;
                    bestSize = size;
                    bestDepth = depth;
                    bestPath = path;
                }
            }

            return bestPath ?? paths[0];
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
                            HoyoToonTourOverlay.DrawHighlightIfActive(highlightTargetId, buttonRect, items[index]);
                        }
                    }
                }
            }

            return selectedIndex;
        }

        private void ClearNonTourCharacterSelection()
        {
            if (string.IsNullOrEmpty(HoyoToonGuidedTourController.DesiredCharacterName))
            {
                return;
            }

            if (_selectedCharacters.Count == 1 && _selectedCharacters.Contains(HoyoToonGuidedTourController.DesiredCharacterName))
            {
                return;
            }

            _selectedCharacters.Clear();
            _selectedCharacterIndex = -1;
        }

        private void ClearNonTourVariantSelection()
        {
            if (string.IsNullOrEmpty(HoyoToonGuidedTourController.DesiredVariantName))
            {
                return;
            }

            if (_selectedVariants.Count == 1 && _selectedVariants.Contains(HoyoToonGuidedTourController.DesiredVariantName))
            {
                return;
            }

            _selectedVariants.Clear();
            _selectedVariantIndex = -1;
        }

        private static void DrawTourCalloutForStep(string stepId)
        {
            if (!HoyoToonGuidedTourController.IsActive)
            {
                return;
            }

            var step = HoyoToonGuidedTourController.CurrentStep;
            if (!string.Equals(step.id, stepId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string body = step.instruction;
            if (stepId == "game" && HoyoToonGuidedTourController.ShouldShowManagerHandoffInline())
            {
                body = $"{body}\n\nYou're now in the HoyoToon Manager. Follow the inline highlights to continue.";
                HoyoToonGuidedTourController.MarkManagerHandoffShown();
            }

            HoyoToonTourCallout.Draw(
                $"Guided Tour: {step.title}",
                body,
                null,
                null);
        }


        private static string AssetsPathFromAbsolute(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return null;
            }

            var normalized = absolutePath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return "Assets" + normalized.Substring(dataPath.Length);
        }

        private static string AbsoluteFromAssetsPath(string assetsPath)
        {
            if (string.IsNullOrEmpty(assetsPath))
            {
                return null;
            }

            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var normalized = assetsPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = normalized.Substring("Assets".Length).TrimStart('/');
            return Path.Combine(root, "Assets", relative);
        }

        private static string EnsureAssetsRoot(string assetsPath)
        {
            if (string.IsNullOrEmpty(assetsPath))
            {
                return null;
            }

            var normalized = assetsPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return normalized.TrimEnd('/');
        }

        private static string TrimPrefix(string value, string prefix)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(prefix))
            {
                return value.TrimStart('/');
            }

            var normalizedValue = value.Replace('\\', '/');
            var normalizedPrefix = prefix.Replace('\\', '/').TrimEnd('/');

            if (normalizedValue.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedValue.Substring(normalizedPrefix.Length).TrimStart('/');
            }

            return normalizedValue.TrimStart('/');
        }

        private static bool IsRootVariantFile(string variantPath, string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var relative = TrimPrefix(filePath, variantPath);
            if (string.IsNullOrEmpty(relative))
            {
                return false;
            }

            var normalized = relative.Replace('\\', '/').TrimStart('/');
            return normalized.IndexOf('/') < 0;
        }

        private static bool TryConvertToAssetsPath(string absolutePath, out string assetsPath)
        {
            assetsPath = null;
            if (string.IsNullOrEmpty(absolutePath))
            {
                return false;
            }

            var normalized = absolutePath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            assetsPath = "Assets" + normalized.Substring(dataPath.Length);
            return true;
        }

        private static string BuildAssetTargetPath(string assetsRoot, GameOption game, string characterName, VariantOption variant)
        {
            var segments = new List<string> { assetsRoot, SanitizeFolderName(game.FolderName), SanitizeFolderName(characterName) };
            if (!string.Equals(variant.Name, DefaultVariantName, StringComparison.OrdinalIgnoreCase))
            {
                segments.Add(SanitizeFolderName(variant.Name));
            }

            return string.Join("/", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        }

        private static string SanitizeFolderName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unknown";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Trim();
        }

        private static string CombineSharePath(params string[] parts)
        {
            return string.Join("/", parts.Where(part => !string.IsNullOrEmpty(part))
                .Select(part => part.Trim().Trim('/'))
                .Where(part => !string.IsNullOrEmpty(part)));
        }

        private static bool TryMatchFolder(List<string> folderNames, GameConfig game, out string folderName)
        {
            folderName = null;
            if (folderNames == null || game == null)
            {
                return false;
            }

            string matchKey = NormalizeKey(game.Key);
            string matchDisplay = NormalizeKey(game.DisplayName);

            foreach (var folder in folderNames)
            {
                var normalizedFolder = NormalizeKey(folder);
                if (!string.IsNullOrEmpty(matchKey) && string.Equals(normalizedFolder, matchKey, StringComparison.OrdinalIgnoreCase))
                {
                    folderName = folder;
                    return true;
                }

                if (!string.IsNullOrEmpty(matchDisplay) && string.Equals(normalizedFolder, matchDisplay, StringComparison.OrdinalIgnoreCase))
                {
                    folderName = folder;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static string FindFolderName(IEnumerable<HoyoToonCloudreveClient.RemoteEntryInfo> entries, string target)
        {
            if (entries == null || string.IsNullOrEmpty(target))
            {
                return null;
            }

            return entries.Where(entry => entry.IsDirectory)
                .Select(entry => entry.Name)
                .FirstOrDefault(name => string.Equals(name, target, StringComparison.OrdinalIgnoreCase));
        }

        private GameFolderCache GetOrCreateCache(GameOption option)
        {
            if (option == null)
            {
                return null;
            }

            if (!_gameCache.TryGetValue(option.Key, out var cache))
            {
                cache = new GameFolderCache(option.FolderName);
                _gameCache[option.Key] = cache;
            }

            return cache;
        }

        private bool TryGetBatchSelection(out GameOption game, out List<string> characters, out List<string> variants)
        {
            game = null;
            characters = new List<string>();
            variants = new List<string>();

            if (_selectedGameIndex < 0 || _selectedGameIndex >= _availableGames.Count)
            {
                return false;
            }

            game = _availableGames[_selectedGameIndex];

            if (_selectedCharacters.Count > 0)
            {
                foreach (var name in _characters)
                {
                    if (_selectedCharacters.Contains(name))
                    {
                        characters.Add(name);
                    }
                }
            }
            else if (_selectedCharacterIndex >= 0 && _selectedCharacterIndex < _characters.Count)
            {
                characters.Add(_characters[_selectedCharacterIndex]);
            }

            if (_selectedVariants.Count > 0)
            {
                foreach (var option in _variants)
                {
                    if (_selectedVariants.Contains(option.Name))
                    {
                        variants.Add(option.Name);
                    }
                }
            }
            else if (_selectedVariantIndex >= 0 && _selectedVariantIndex < _variants.Count)
            {
                variants.Add(_variants[_selectedVariantIndex].Name);
            }
            else if (_selectedCharacters.Count > 1 && _selectedVariantsByCharacter.Count > 0)
            {
                var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var selection in _selectedVariantsByCharacter.Values)
                {
                    foreach (var name in selection)
                    {
                        distinct.Add(name);
                    }
                }

                variants.AddRange(distinct);
            }

            return characters.Count > 0 && variants.Count > 0;
        }

        private bool IsSelectedGame(string gameKey)
        {
            return _selectedGameIndex >= 0
                   && _selectedGameIndex < _availableGames.Count
                   && string.Equals(_availableGames[_selectedGameIndex].Key, gameKey, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSelectedCharacter(string characterName)
        {
            return _selectedCharacterIndex >= 0
                   && _selectedCharacterIndex < _characters.Count
                   && string.Equals(_characters[_selectedCharacterIndex], characterName, StringComparison.OrdinalIgnoreCase);
        }

        private void ClearSelections()
        {
            _availableGames.Clear();
            _selectedGameIndex = -1;
            _gameScroll = Vector2.zero;
            ClearCharacterSelection();
        }

        private void ClearCharacterSelection()
        {
            _characters = new List<string>();
            _selectedCharacterIndex = -1;
            _characterScroll = Vector2.zero;
            _characterSearch = string.Empty;
            _selectedCharacters.Clear();
            ClearVariantSelection();
        }

        private void ClearVariantSelection()
        {
            _variants = new List<VariantOption>();
            _selectedVariantIndex = -1;
            _variantScroll = Vector2.zero;
            _variantSectionScroll = Vector2.zero;
            _selectedVariants.Clear();
            _selectedVariantsByCharacter.Clear();
            _loadingVariantsForCharacters.Clear();
            _variantScrollByCharacter.Clear();
        }

        private static void ScheduleOnMainThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (System.Threading.Thread.CurrentThread.ManagedThreadId == MainThreadId)
            {
                action();
                return;
            }

            EditorApplication.delayCall += () => action();
        }

        private sealed class GameFolderCache
        {
            public GameFolderCache(string gameFolder)
            {
                GameFolderName = gameFolder;
                VariantsByCharacter = new Dictionary<string, List<VariantOption>>(StringComparer.OrdinalIgnoreCase);
            }

            public string GameFolderName { get; }
            public string CharactersFolderName { get; set; }
            public List<string> Characters { get; set; } = new List<string>();
            public Dictionary<string, List<VariantOption>> VariantsByCharacter { get; }
            public bool IsLoadingCharacters { get; set; }
            public bool IsLoadingVariants { get; set; }
        }

        private sealed class VariantOption
        {
            public VariantOption(string name, string relativePath)
            {
                Name = name;
                RelativePath = relativePath;
            }

            public string Name { get; }
            public string RelativePath { get; }
        }

        private sealed class GameOption
        {
            public GameOption(GameConfig config, string folderName)
            {
                Config = config;
                FolderName = folderName;
            }

            public GameConfig Config { get; }
            public string FolderName { get; }
            public string Key => Config?.Key;
            public string DisplayName => string.IsNullOrEmpty(Config?.DisplayName) ? Config?.Key : Config.DisplayName;
        }

        private enum FbxVariantType
        {
            Unknown,
            WithAnims,
            NoAnims
        }

        private enum HsrFbxChoice
        {
            WithAnims,
            NoAnims,
            Both
        }
    }
}
#endif
