#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using HoyoToon.API;
using HoyoToon.Utilities;

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

                DrawGameSelection();
                DrawCharacterSelection();
                DrawVariantSelection();
                DrawAutoSetupToggle();
                DrawDownloadButton(targetManager);
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
                GUILayout.FlexibleSpace();

                if (_isRefreshing)
                {
                    GUILayout.Space(8f);
                    EditorGUILayout.LabelField("Loading...", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawGameSelection()
        {
            var labels = _availableGames.Select(option => option.DisplayName).ToList();
            int current = NormalizeSelectionIndex(_selectedGameIndex, labels.Count);
            int newIndex = DrawScrollableSelection(
                "Game",
                labels,
                current,
                ref _gameScroll,
                _isRefreshing,
                "No games available from the API or CDN share.");

            if (newIndex != current)
            {
                _selectedGameIndex = newIndex;
                ClearCharacterSelection();
                EnsureCharactersLoaded();
            }
        }

        private void DrawCharacterSelection()
        {
            if (_selectedGameIndex < 0)
            {
                EditorGUILayout.HelpBox("Select a game to load characters.", MessageType.Info);
                return;
            }

            DrawCharacterSearchBar();

            var filteredCharacters = GetFilteredCharacters();
            if (!string.IsNullOrWhiteSpace(_characterSearch) && _characters != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Showing {filteredCharacters.Count:N0} / {_characters.Count:N0}", EditorStyles.miniLabel);
                }
            }

            string currentName = _selectedCharacterIndex >= 0 && _selectedCharacterIndex < _characters.Count
                ? _characters[_selectedCharacterIndex]
                : null;

            int current = !string.IsNullOrEmpty(currentName)
                ? filteredCharacters.FindIndex(name => string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase))
                : -1;

            string emptyMessage = string.IsNullOrWhiteSpace(_characterSearch)
                ? "No characters are available for this game."
                : "No characters match the search filter.";
            int newIndex = DrawScrollableSelection(
                "Character",
                filteredCharacters,
                current,
                ref _characterScroll,
                _isRefreshing,
                emptyMessage);

            if (newIndex != current)
            {
                if (newIndex >= 0 && newIndex < filteredCharacters.Count)
                {
                    string selectedName = filteredCharacters[newIndex];
                    _selectedCharacterIndex = _characters.FindIndex(name => string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase));
                    ClearVariantSelection();
                    EnsureVariantsLoaded();
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

            var variantNames = _variants.Select(v => v.Name).ToList();
            int current = NormalizeSelectionIndex(_selectedVariantIndex, variantNames.Count);
            int newIndex = DrawScrollableSelection(
                "Variant",
                variantNames,
                current,
                ref _variantScroll,
                _isRefreshing,
                "No variants are available for this character.");

            if (newIndex != current)
            {
                _selectedVariantIndex = newIndex;
            }
        }

        private void DrawAutoSetupToggle()
        {
            bool newValue = EditorGUILayout.ToggleLeft("Auto setup after download", _autoSetupAfterDownload);
            if (newValue != _autoSetupAfterDownload)
            {
                _autoSetupAfterDownload = newValue;
                EditorPrefs.SetBool(AutoSetupPrefKey, _autoSetupAfterDownload);
            }
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
            bool ready = _selectedGameIndex >= 0 && _selectedCharacterIndex >= 0 && _selectedVariantIndex >= 0;
            using (new EditorGUI.DisabledScope(!ready || _isRefreshing))
            {
                if (GUILayout.Button("Download Selected Model", GUILayout.Height(28f)))
                {
                    StartDownloadSelected(manager);
                }
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
            if (!TryGetSelection(out var game, out var characterName, out var variant))
            {
                return;
            }

            HoyoToonAsyncUtil.RunFireAndForget(() => DownloadSelectedAsync(manager, game, characterName, variant), "Download model", ex =>
            {
                ScheduleOnMainThread(() =>
                {
                    _statusMessage = $"Download failed: {ex.Message}";
                });
            });
        }

        private async Task DownloadSelectedAsync(HoyoToonManager manager, GameOption game, string characterName, VariantOption variant)
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

            var assetTarget = BuildAssetTargetPath(assetRoot, game, characterName, variant);
            var absoluteTarget = AbsoluteFromAssetsPath(assetTarget);
            Directory.CreateDirectory(absoluteTarget);

            HoyoToonProgressDialog.Start("Downloading Model", "Preparing download...");

            var remoteFiles = await HoyoToonCloudreveClient.GetFileListAsync(ShareUrl, variant.RelativePath, true);
            var files = remoteFiles.Where(file => !file.IsDirectory).ToList();

            if (IsHonkaiStarRail(game.Key, game.DisplayName))
            {
                var fbxFiles = files.Where(file => file.RelativePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).ToList();
                if (fbxFiles.Count > 1)
                {
                    var orderedFbxFiles = fbxFiles
                        .OrderBy(file => Path.GetFileNameWithoutExtension(file.RelativePath) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    int choice = await PromptFbxChoiceAsync(orderedFbxFiles, characterName, variant.Name);
                    if (choice == -2)
                    {
                        // Both: keep all FBX files
                    }
                    else if (choice < 0 || choice >= fbxFiles.Count)
                    {
                        HoyoToonProgressDialog.End("Download cancelled.");
                        return;
                    }

                    if (choice >= 0)
                    {
                        var chosen = orderedFbxFiles[choice];
                        files = files.Where(file => !file.RelativePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                            .Concat(new[] { chosen })
                            .ToList();
                    }
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
                    HoyoToonProgressDialog.End("Download cancelled.");
                    return;
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
                HoyoToonProgressDialog.Update(progress, $"Downloading {Path.GetFileName(localPath)} ({completed}/{total})");
            }

            HoyoToonProgressDialog.End("Download complete!");

            ScheduleOnMainThread(() =>
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ShowPostDownloadPrompt(manager, assetTarget, game.Key, game.DisplayName, characterName, variant.Name);
            });
        }

        private void ShowPostDownloadPrompt(HoyoToonManager manager, string assetTarget, string gameKey, string gameName, string characterName, string variantName)
        {
            if (_autoSetupAfterDownload)
            {
                TryAutoSetupDownloaded(manager, assetTarget, gameKey, gameName, characterName, variantName);
                return;
            }

            var message = $"Downloaded {characterName} ({variantName}) for {gameName}.\n\nWhat would you like to do next?";
            var buttons = new[] { "Auto Setup", "Do Nothing" };

            HoyoToonDialogWindow.ShowCustom("Model Downloaded", message, MessageType.Info, buttons, 1, 1, result =>
            {
                switch (result)
                {
                    case 0:
                        TryAutoSetupDownloaded(manager, assetTarget, gameKey, gameName, characterName, variantName);
                        break;
                    default:
                        break;
                }
            });
        }

        private void TryAutoSetupDownloaded(HoyoToonManager manager, string assetFolder, string gameKey, string gameName, string characterName, string variantName)
        {
            if (manager == null)
            {
                HoyoToonDialogWindow.ShowError("Manager Missing", "Cannot setup without an active HoyoToon Manager in the scene.");
                return;
            }

            ResolvePrimaryAssetWithChoice(assetFolder, gameKey, gameName, characterName, variantName, (assetPath, isFbx, isPrefab) =>
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    HoyoToonDialogWindow.ShowError("No Model Found", "Could not find an FBX or prefab in the downloaded folder.");
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
                        }
                        else
                        {
                            HoyoToonDialogWindow.ShowWarning("Auto Setup Complete", "No setup steps were applicable for this model. Try Instantiate to add it to the scene.");
                        }
                    }
                    return;
                }

                if (isPrefab)
                {
                    if (HoyoToonModelSetupUtility.TryInstantiatePrefabAsset(manager, asset, out var instance))
                    {
                        if (instance != null)
                        {
                            RegisterAndSelect(manager, instance);
                        }
                        else
                        {
                            HoyoToonDialogWindow.ShowWarning("Auto Setup Complete", "No setup steps were applicable for this model. Try Instantiate to add it to the scene.");
                        }
                    }
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

            Selection.activeObject = instance;
            EditorGUIUtility.PingObject(instance);
        }

        private static void ResolvePrimaryAssetWithChoice(string assetFolder, string gameKey, string gameName, string characterName, string variantName, Action<string, bool, bool> onResolved)
        {
            if (onResolved == null)
            {
                return;
            }

            if (!TryGatherAssetCandidates(assetFolder, characterName, variantName, out var prefabs, out var fbxs))
            {
                onResolved(null, false, false);
                return;
            }

            if (IsHonkaiStarRail(gameKey, gameName))
            {
                if (fbxs.Count == 0)
                {
                    onResolved(null, false, false);
                    return;
                }

                if (fbxs.Count > 1)
                {
                    ShowFbxChoiceDialog(fbxs, characterName, variantName, onResolved);
                    return;
                }

                onResolved(AssetsPathFromAbsolute(fbxs[0]), true, false);
                return;
            }

            var bestPrefab = SelectBestCandidate(prefabs, characterName, variantName);
            if (!string.IsNullOrEmpty(bestPrefab))
            {
                onResolved(AssetsPathFromAbsolute(bestPrefab), false, true);
                return;
            }

            if (IsHonkaiStarRail(gameKey, gameName) && fbxs.Count > 1)
            {
                ShowFbxChoiceDialog(fbxs, characterName, variantName, onResolved);
                return;
            }

            var bestFbx = SelectBestCandidate(fbxs, characterName, variantName);
            if (!string.IsNullOrEmpty(bestFbx))
            {
                onResolved(AssetsPathFromAbsolute(bestFbx), true, false);
                return;
            }

            onResolved(null, false, false);
        }

        private static bool TryGatherAssetCandidates(string assetFolder, string characterName, string variantName, out List<string> prefabs, out List<string> fbxs)
        {
            prefabs = new List<string>();
            fbxs = new List<string>();

            var absoluteFolder = AbsoluteFromAssetsPath(assetFolder);
            if (string.IsNullOrEmpty(absoluteFolder) || !Directory.Exists(absoluteFolder))
            {
                return false;
            }

            prefabs = Directory.GetFiles(absoluteFolder, "*.prefab", SearchOption.AllDirectories).ToList();
            fbxs = Directory.GetFiles(absoluteFolder, "*.fbx", SearchOption.AllDirectories).ToList();
            return prefabs.Count > 0 || fbxs.Count > 0;
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

        private static string BuildFbxLabel(string path, string characterName, string variantName)
        {
            var fileName = Path.GetFileNameWithoutExtension(path) ?? "FBX";
            var fbxType = GetFbxVariantType(fileName);
            string hint = fbxType switch
            {
                FbxVariantType.WithAnims => "(With Anims)",
                FbxVariantType.NoAnims => "(No Anims)",
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

        private static int DrawScrollableSelection(string label, IReadOnlyList<string> items, int selectedIndex, ref Vector2 scroll, bool disabled, string emptyMessage)
        {
            using (new EditorGUI.DisabledScope(disabled))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (items != null)
                    {
                        EditorGUILayout.LabelField($"Total: {items.Count:N0}", EditorStyles.miniLabel, GUILayout.Width(90f));
                    }
                }
                if (items == null || items.Count == 0)
                {
                    EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                    return -1;
                }

                using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.MinHeight(70f), GUILayout.MaxHeight(120f)))
                {
                    scroll = scrollScope.scrollPosition;
                    for (int index = 0; index < items.Count; index++)
                    {
                        bool isSelected = index == selectedIndex;
                        bool toggled = GUILayout.Toggle(isSelected, items[index], EditorStyles.miniButton);
                        if (toggled && !isSelected)
                        {
                            selectedIndex = index;
                        }
                    }
                }
            }

            return selectedIndex;
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

        private bool TryGetSelection(out GameOption game, out string characterName, out VariantOption variant)
        {
            game = null;
            characterName = null;
            variant = null;

            if (_selectedGameIndex < 0 || _selectedGameIndex >= _availableGames.Count)
            {
                return false;
            }

            if (_selectedCharacterIndex < 0 || _selectedCharacterIndex >= _characters.Count)
            {
                return false;
            }

            if (_selectedVariantIndex < 0 || _selectedVariantIndex >= _variants.Count)
            {
                return false;
            }

            game = _availableGames[_selectedGameIndex];
            characterName = _characters[_selectedCharacterIndex];
            variant = _variants[_selectedVariantIndex];
            return true;
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
            ClearVariantSelection();
        }

        private void ClearVariantSelection()
        {
            _variants = new List<VariantOption>();
            _selectedVariantIndex = -1;
            _variantScroll = Vector2.zero;
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
    }
}
#endif
