#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Editor.Resources;
using HoyoToon.Editor.Setup;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.Editor;
using HoyoToon.Runtime.ScriptableObjects.Games;
using HoyoToon.Runtime.ScriptableObjects.Resources;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Assets
{
    internal sealed class AssetDownloadWindow : EditorWindow
    {
        private const string MenuPath = "HoyoToon/Assets/Downloader";
        private const string WindowTitle = "Asset Downloader";
        private const string DefaultShareUrl = "https://cdn.hoyotoon.com/s/pXIz";
        private const string DefaultDownloadRoot = "Assets/HoyoToon/Characters";
        private const string CharactersFolderName = "Characters";
        private const string PrefsPrefix = "HoyoToon.Editor.AssetDownloader.";
        private const string HsrFbxChoiceKey = PrefsPrefix + "HsrFbxChoice";
        private const string AutoSetupAfterDownloadKey = PrefsPrefix + "AutoSetupAfterDownload";
        private const int DownloadBatchSize = 4;
        private const float SectionSpacing = 6f;
        private const float SectionInnerSpacing = 4f;
        private const float ActionButtonHeight = 24f;
        private const float SelectionButtonHeight = 20f;
        private const float SelectionGridGap = 4f;
        private const float SelectionColumnWidth = 124f;
        private const float SelectionViewHorizontalPadding = 24f;

        private static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;
        private static readonly ConcurrentQueue<Action> MainThreadQueue = new ConcurrentQueue<Action>();
        private static AssetDownloadWindow sharedBackend;
        private static GUIStyle sectionTitleStyle;
        private static GUIStyle sectionSubtitleStyle;
        private static GUIStyle summaryStyle;
        private static GUIStyle actionButtonStyle;
        private static GUIStyle selectionButtonStyle;
        private static GUIStyle toolbarSearchCancelButton;
        private static GUIStyle toolbarSearchCancelButtonEmpty;

        private readonly List<AssetDownloadGameOption> availableGames = new List<AssetDownloadGameOption>();
        private readonly Dictionary<string, AssetDownloadGameCache> gameCacheByKey = new Dictionary<string, AssetDownloadGameCache>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> characters = new List<string>();
        private readonly List<AssetDownloadVariantOption> variants = new List<AssetDownloadVariantOption>();
        private readonly HashSet<string> selectedCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> selectedVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> selectedVariantsByCharacter = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AssetDownloadHsrFbxChoice> hsrFbxChoiceByCharacter = new Dictionary<string, AssetDownloadHsrFbxChoice>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> loadingVariantsForCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> lastManagerDownloadedCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> lastManagerDownloadedModelAssetPathsByCharacter = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Vector2> variantScrollByCharacter = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> filteredCharactersCache = new List<string>();

        private AssetDownloadConnectionContext connectionContext;
        private Vector2 windowScroll;
        private Vector2 gameScroll;
        private Vector2 characterScroll;
        private Vector2 variantScroll;
        private Vector2 variantSectionScroll;
        private int selectedGameIndex = -1;
        private bool hasLoadedOnce;
        private bool pendingAutoRefresh;
        private bool isRefreshingGames;
        private bool isDownloading;
        private bool isImportingDownloadedAssets;
        private bool autoSetupAfterDownload;
        private bool lastManagerDownloadSucceeded;
        private bool lastManagerDownloadFailed;
        private bool filteredCharactersDirty = true;
        private double nextAutoRefreshAttemptTime;
        private string activeCharacterName = string.Empty;
        private string characterSearch = string.Empty;
        private string statusMessage = string.Empty;
        private AssetDownloadHsrFbxChoice hsrFbxChoice = AssetDownloadHsrFbxChoice.WithAnims;
        private CancellationTokenSource refreshCancellationSource;
        private CancellationTokenSource downloadCancellationSource;

        static AssetDownloadWindow()
        {
            EditorApplication.update -= ProcessMainThreadQueue;
            EditorApplication.update += ProcessMainThreadQueue;
        }

        internal static AssetDownloadWindow GetOrCreateSharedBackend()
        {
            if (sharedBackend != null)
            {
                return sharedBackend;
            }

            sharedBackend = UnityEngine.Resources.FindObjectsOfTypeAll<AssetDownloadWindow>().FirstOrDefault();
            if (sharedBackend == null)
            {
                sharedBackend = CreateInstance<AssetDownloadWindow>();
                sharedBackend.hideFlags = HideFlags.HideAndDontSave;
            }

            return sharedBackend;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(460f, 560f);
            LoadPrefs();
            EditorApplication.update -= HandleWindowUpdate;
            EditorApplication.update += HandleWindowUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;

            if (!hasLoadedOnce)
            {
                hasLoadedOnce = true;
                BeginAutomaticRefresh();
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleWindowUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            pendingAutoRefresh = false;
            CancelOutstandingOperations();
            SavePrefs();
            ClearProgressBarOnMainThread();
        }

        private void OnBeforeAssemblyReload()
        {
            CancelOutstandingOperations();
            SavePrefs();
            ClearProgressBarOnMainThread();
        }

        private void OnEditorQuitting()
        {
            CancelOutstandingOperations();
            SavePrefs();
            ClearProgressBarOnMainThread();
        }

        private void OnGUI()
        {
            EnsureStyles();

            using (var scrollView = new EditorGUILayout.ScrollViewScope(windowScroll))
            {
                windowScroll = scrollView.scrollPosition;
                EditorGUILayout.Space(SectionInnerSpacing);

                DrawGameSection();
                DrawCharacterSection();
                DrawVariantSection();
                DrawActionSection();
            }
        }

        private void DrawGameSection()
        {
            DrawSection(
                "Game",
                availableGames.Count > 0
                    ? $"{availableGames.Count:N0} game folder(s) available."
                    : "Refresh the library to load games.",
                () =>
            {
                if (availableGames.Count <= 0)
                {
                    EditorGUILayout.HelpBox("No games are loaded from the current Cloudreve share.", MessageType.Info);
                    return;
                }

                int currentIndex = NormalizeSelectionIndex(selectedGameIndex, availableGames.Count);
                int newIndex = DrawScrollableSelection(
                    availableGames.Select(option => option.DisplayName).ToList(),
                    currentIndex,
                    ref gameScroll,
                    isRefreshingGames || isDownloading,
                    "No games are available.",
                    4,
                    6);

                if (newIndex != currentIndex)
                {
                    selectedGameIndex = newIndex;
                    ClearCharacterSelection();
                    EnsureCharactersLoaded();
                }
            });
        }

        private void DrawCharacterSection()
        {
            string characterSubtitle = TryGetSelectedGame(out AssetDownloadGameOption selectedGame)
                ? $"Select one or more {selectedGame.DisplayName} characters."
                : "Select a game first.";

            DrawSection("Characters", characterSubtitle, () =>
            {
                if (!TryGetSelectedGame(out AssetDownloadGameOption game))
                {
                    EditorGUILayout.HelpBox("Select a game to load its characters.", MessageType.Info);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    string newSearch = EditorGUILayout.TextField(characterSearch ?? string.Empty, EditorStyles.toolbarSearchField);
                    GUIStyle clearStyle = GetToolbarSearchCancelStyle(string.IsNullOrEmpty(newSearch));
                    if (GUILayout.Button(GUIContent.none, clearStyle))
                    {
                        newSearch = string.Empty;
                        GUI.FocusControl(null);
                    }

                    if (!string.Equals(characterSearch, newSearch, StringComparison.Ordinal))
                    {
                        characterSearch = newSearch;
                        filteredCharactersDirty = true;
                    }
                }

                IReadOnlyList<string> filteredCharacters = GetFilteredCharacters();
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    $"{filteredCharacters.Count:N0} shown  |  {selectedCharacters.Count:N0} selected",
                    summaryStyle);

                if (filteredCharacters.Count <= 0)
                {
                    string emptyMessage = string.IsNullOrWhiteSpace(characterSearch)
                        ? "No characters are available for this game."
                        : "No characters match the current search.";
                    EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                    return;
                }

                DrawMultiSelectList(
                    filteredCharacters,
                    selectedCharacters,
                    ref characterScroll,
                    isRefreshingGames || isDownloading,
                    selectedName =>
                    {
                        activeCharacterName = selectedName;
                        EnsureVariantsLoadedForCharacter(game, selectedName);
                    },
                    HandleCharacterToggle);
            });
        }

        private void DrawVariantSection()
        {
            string variantSubtitle = GetVariantSectionSubtitle();
            DrawSection("Variants", variantSubtitle, () =>
            {
                if (!TryGetSelectedGame(out AssetDownloadGameOption game))
                {
                    EditorGUILayout.HelpBox("Select a game first.", MessageType.Info);
                    return;
                }

                List<string> orderedSelectedCharacters = GetOrderedSelectedCharacters();
                if (orderedSelectedCharacters.Count <= 0)
                {
                    EditorGUILayout.HelpBox("Select at least one character to load variants.", MessageType.Info);
                    return;
                }

                if (orderedSelectedCharacters.Count == 1)
                {
                    string characterName = orderedSelectedCharacters[0];
                    DrawSingleCharacterVariants(game, characterName);
                }
                else
                {
                    DrawPerCharacterVariants(game, orderedSelectedCharacters);
                }

                if (AssetDownloadPathUtility.IsHonkaiStarRail(game.Key, game.DisplayName))
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("FBX Type", EditorStyles.boldLabel);
                    int newChoice = GUILayout.Toolbar((int)hsrFbxChoice, new[] { "With Anims", "No Anims", "Both" });
                    if (newChoice != (int)hsrFbxChoice)
                    {
                        hsrFbxChoice = (AssetDownloadHsrFbxChoice)newChoice;
                        SavePrefs();
                    }
                }
            });
        }

        private void DrawSingleCharacterVariants(AssetDownloadGameOption game, string characterName)
        {
            EnsureVariantsLoadedForCharacter(game, characterName);
            AssetDownloadGameCache cache = GetOrCreateCache(game);
            if (!cache.VariantsByCharacter.TryGetValue(characterName, out List<AssetDownloadVariantOption> currentVariants))
            {
                EditorGUILayout.LabelField("Loading variants...", EditorStyles.miniLabel);
                return;
            }

            if (!string.Equals(activeCharacterName, characterName, StringComparison.OrdinalIgnoreCase))
            {
                activeCharacterName = characterName;
            }

            variants.Clear();
            variants.AddRange(currentVariants);
            EnsureDefaultVariantSelection(variants, selectedVariants);

            DrawMultiSelectList(
                currentVariants.Select(option => option.Name).ToList(),
                selectedVariants,
                ref variantScroll,
                isRefreshingGames || isDownloading,
                _ => { },
                (_, __) => { });
        }

        private void DrawPerCharacterVariants(AssetDownloadGameOption game, IReadOnlyList<string> orderedSelectedCharacters)
        {
            float maxHeight = Mathf.Clamp(100f + (orderedSelectedCharacters.Count * 28f), 120f, 260f);
            using (var scrollView = new EditorGUILayout.ScrollViewScope(variantSectionScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(maxHeight)))
            {
                variantSectionScroll = scrollView.scrollPosition;

                for (int index = 0; index < orderedSelectedCharacters.Count; index++)
                {
                    string characterName = orderedSelectedCharacters[index];
                    EnsureVariantsLoadedForCharacter(game, characterName);
                    AssetDownloadGameCache cache = GetOrCreateCache(game);
                    HashSet<string> selection = GetOrCreateVariantSelection(characterName);
                    cache.VariantsByCharacter.TryGetValue(characterName, out List<AssetDownloadVariantOption> currentVariants);
                    int variantCount = currentVariants != null ? currentVariants.Count : 0;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(new GUIContent(characterName, characterName), summaryStyle);
                            GUILayout.FlexibleSpace();
                        }

                        if (currentVariants == null)
                        {
                            EditorGUILayout.LabelField("Loading variants...", EditorStyles.miniLabel);
                            continue;
                        }

                        EnsureDefaultVariantSelection(currentVariants, selection);
                        List<string> names = currentVariants.Select(option => option.Name).ToList();
                        Vector2 scroll = GetVariantScroll(characterName);
                        DrawMultiSelectList(
                            names,
                            selection,
                            ref scroll,
                            isRefreshingGames || isDownloading,
                            _ => { },
                            (_, __) => { },
                            minHeight: 56f,
                            maxHeight: 96f);
                        SetVariantScroll(characterName, scroll);
                    }
                }
            }
        }

        private void DrawActionSection()
        {
            DrawSection("Actions", BuildActionSummary(), () =>
            {
                bool supportsAutoSetup = TryGetSelectedGame(out AssetDownloadGameOption selectedGame)
                    && SupportsAutoSetup(selectedGame);

                EditorGUI.BeginChangeCheck();
                using (new EditorGUI.DisabledScope(isRefreshingGames || isDownloading || !supportsAutoSetup))
                {
                    bool newAutoSetupAfterDownload = EditorGUILayout.ToggleLeft(
                        "Automatically run Auto Setup after download",
                        autoSetupAfterDownload);
                    if (newAutoSetupAfterDownload != autoSetupAfterDownload)
                    {
                        autoSetupAfterDownload = newAutoSetupAfterDownload;
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    SavePrefs();
                }

                if (!supportsAutoSetup && selectedGame != null)
                {
                    EditorGUILayout.HelpBox(
                        $"Auto Setup is not available for {selectedGame.DisplayName}.",
                        MessageType.Info);
                }

                EditorGUILayout.Space(SectionInnerSpacing);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!CanClearSelection()))
                    {
                        if (GUILayout.Button("Clear Selection", actionButtonStyle))
                        {
                            ClearCurrentSelection();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!CanDownloadSelection()))
                    {
                        if (GUILayout.Button("Download Selected Assets", actionButtonStyle))
                        {
                            StartDownloadSelected();
                        }
                    }
                }

                if (ShouldShowLibraryStatus())
                {
                    EditorGUILayout.Space(SectionInnerSpacing);
                    EditorGUILayout.HelpBox(statusMessage, GetStatusMessageType());
                }

                EditorGUILayout.Space(SectionInnerSpacing);
                using (new EditorGUI.DisabledScope(isRefreshingGames || isDownloading))
                {
                    if (GUILayout.Button("Refresh Library", actionButtonStyle))
                    {
                        StartRefreshGameList();
                    }
                }
            });
        }

        private void StartRefreshGameList()
        {
            if (isRefreshingGames || isDownloading)
            {
                return;
            }

            if (!EditorReadinessUtility.IsReadyForEditorWork())
            {
                statusMessage = "The Unity editor is busy compiling or importing. Refresh again once it is idle.";
                Repaint();
                return;
            }

            pendingAutoRefresh = false;
            isRefreshingGames = true;
            statusMessage = "Loading games from the Cloudreve share...";
            connectionContext = null;
            availableGames.Clear();
            gameCacheByKey.Clear();
            ClearCharacterSelection();
            Repaint();

            CancellationToken cancellationToken = CreateRefreshCancellationToken();
            _ = RefreshGameListAsync(cancellationToken);
        }

        private void BeginAutomaticRefresh()
        {
            pendingAutoRefresh = true;
            nextAutoRefreshAttemptTime = EditorApplication.timeSinceStartup;
            if (string.IsNullOrWhiteSpace(statusMessage))
            {
                statusMessage = "Loading the asset library...";
            }

            Repaint();
        }

        private void HandleWindowUpdate()
        {
            if (!pendingAutoRefresh || isRefreshingGames || isDownloading)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < nextAutoRefreshAttemptTime)
            {
                return;
            }

            if (!EditorReadinessUtility.IsReadyForEditorWork())
            {
                const string waitingMessage = "Waiting for the Unity editor to finish compiling or importing before loading the asset library...";
                if (!string.Equals(statusMessage, waitingMessage, StringComparison.Ordinal))
                {
                    statusMessage = waitingMessage;
                    Repaint();
                }

                nextAutoRefreshAttemptTime = EditorApplication.timeSinceStartup + 0.5d;
                return;
            }

            StartRefreshGameList();
        }

        private async Task RefreshGameListAsync(CancellationToken cancellationToken)
        {
            try
            {
                await InvokeOnMainThreadAsync(() =>
                {
                    statusMessage = "Connecting to the Cloudreve share...";
                    Repaint();
                });

                AssetDownloadConnectionContext connection = await AssetDownloadCloudreveService
                    .ConnectAsync(DefaultShareUrl, cancellationToken)
                    .ConfigureAwait(false);

                await InvokeOnMainThreadAsync(() =>
                {
                    statusMessage = "Reading the Cloudreve root folder...";
                    Repaint();
                });

                List<AssetDownloadDirectoryEntry> rootEntries = await AssetDownloadCloudreveService
                    .ListFolderEntriesAsync(connection, connection.ResolvedRootUri, cancellationToken)
                    .ConfigureAwait(false);

                List<AssetDownloadGameOption> options = await InvokeOnMainThreadAsync(() =>
                {
                    statusMessage = "Matching Cloudreve folders with local HoyoToon game definitions...";
                    Repaint();
                    GameRegistry.Initialize();
                    ResourceRegistry.Initialize();
                    return BuildGameOptions(rootEntries);
                });

                await InvokeOnMainThreadAsync(() =>
                {
                    connectionContext = connection;
                    availableGames.Clear();
                    availableGames.AddRange(options);
                    selectedGameIndex = options.Count > 0 ? 0 : -1;
                    gameCacheByKey.Clear();
                    ClearCharacterSelection();
                    statusMessage = options.Count > 0
                        ? $"Loaded {options.Count} available game folder(s)."
                        : "No matching local game definitions were found in the current share.";
                    isRefreshingGames = false;

                    if (selectedGameIndex >= 0)
                    {
                        EnsureCharactersLoaded();
                    }

                    Repaint();
                });
            }
            catch (OperationCanceledException)
            {
                await InvokeOnMainThreadAsync(() =>
                {
                    isRefreshingGames = false;
                    statusMessage = "Asset library refresh cancelled.";
                    Repaint();
                });
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.Models, "Failed to refresh asset download games.", exception);
                await InvokeOnMainThreadAsync(() =>
                {
                    isRefreshingGames = false;
                    statusMessage = $"Failed to load games: {exception.Message}";
                    Repaint();
                });
            }
        }

        private void EnsureCharactersLoaded()
        {
            if (!TryGetSelectedGame(out AssetDownloadGameOption game))
            {
                return;
            }

            AssetDownloadGameCache cache = GetOrCreateCache(game);
            if (cache.Characters.Count > 0)
            {
                ApplyLoadedCharacters(game, cache.Characters);
                return;
            }

            if (cache.IsLoadingCharacters || connectionContext == null)
            {
                return;
            }

            cache.IsLoadingCharacters = true;
            statusMessage = $"Loading characters for {game.DisplayName}...";
            Repaint();
            CancellationToken cancellationToken = CreateRefreshCancellationToken();
            _ = LoadCharactersAsync(game, cancellationToken);
        }

        private async Task LoadCharactersAsync(AssetDownloadGameOption game, CancellationToken cancellationToken)
        {
            try
            {
                AssetDownloadConnectionContext connection = connectionContext;
                if (connection == null)
                {
                    throw new InvalidOperationException("No Cloudreve connection is available.");
                }

                List<AssetDownloadDirectoryEntry> gameEntries = await AssetDownloadCloudreveService
                    .ListFolderEntriesAsync(connection, game.FolderUri, cancellationToken)
                    .ConfigureAwait(false);

                AssetDownloadDirectoryEntry charactersFolder = AssetDownloadPathUtility.FindFolder(gameEntries, CharactersFolderName);
                string characterRootUri = charactersFolder != null ? charactersFolder.Uri : game.FolderUri;

                List<AssetDownloadDirectoryEntry> characterEntries = await AssetDownloadCloudreveService
                    .ListFolderEntriesAsync(connection, characterRootUri, cancellationToken)
                    .ConfigureAwait(false);

                List<string> loadedCharacters = characterEntries
                    .Where(entry => entry != null && entry.IsDirectory && !string.IsNullOrWhiteSpace(entry.Name))
                    .Select(entry => entry.Name)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var characterFolderUrisByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (AssetDownloadDirectoryEntry entry in characterEntries.Where(entry => entry != null && entry.IsDirectory))
                {
                    characterFolderUrisByName[entry.Name] = entry.Uri ?? string.Empty;
                }

                await InvokeOnMainThreadAsync(() =>
                {
                    AssetDownloadGameCache cache = GetOrCreateCache(game);
                    cache.CharactersFolderName = charactersFolder?.Name ?? string.Empty;
                    cache.CharactersFolderUri = characterRootUri;
                    cache.Characters = loadedCharacters;
                    cache.CharacterFolderUrisByName.Clear();
                    foreach (KeyValuePair<string, string> entry in characterFolderUrisByName)
                    {
                        cache.CharacterFolderUrisByName[entry.Key] = entry.Value;
                    }

                    cache.IsLoadingCharacters = false;
                    ApplyLoadedCharacters(game, loadedCharacters);
                    statusMessage = loadedCharacters.Count > 0
                        ? $"Loaded {loadedCharacters.Count} character(s) for {game.DisplayName}."
                        : $"No character folders were found for {game.DisplayName}.";
                    Repaint();
                });
            }
            catch (OperationCanceledException)
            {
                await InvokeOnMainThreadAsync(() =>
                {
                    if (game != null)
                    {
                        GetOrCreateCache(game).IsLoadingCharacters = false;
                    }

                    statusMessage = "Character refresh cancelled.";
                    Repaint();
                });
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.Models, $"Failed to load characters for '{game?.DisplayName}'.", exception);
                await InvokeOnMainThreadAsync(() =>
                {
                    if (game != null)
                    {
                        GetOrCreateCache(game).IsLoadingCharacters = false;
                    }

                    statusMessage = $"Failed to load characters: {exception.Message}";
                    Repaint();
                });
            }
        }

        private void EnsureVariantsLoadedForCharacter(AssetDownloadGameOption game, string characterName)
        {
            if (game == null || string.IsNullOrWhiteSpace(characterName))
            {
                return;
            }

            AssetDownloadGameCache cache = GetOrCreateCache(game);
            if (cache.VariantsByCharacter.TryGetValue(characterName, out _))
            {
                if (selectedCharacters.Count == 1
                    && string.Equals(activeCharacterName, characterName, StringComparison.OrdinalIgnoreCase))
                {
                    variants.Clear();
                    variants.AddRange(cache.VariantsByCharacter[characterName]);
                    EnsureDefaultVariantSelection(variants, selectedVariants);
                }

                return;
            }

            if (loadingVariantsForCharacters.Contains(characterName) || connectionContext == null)
            {
                return;
            }

            loadingVariantsForCharacters.Add(characterName);
            statusMessage = $"Loading variants for {characterName}...";
            Repaint();
            CancellationToken cancellationToken = refreshCancellationSource != null && !refreshCancellationSource.IsCancellationRequested
                ? refreshCancellationSource.Token
                : CreateRefreshCancellationToken();
            _ = LoadVariantsAsync(game, characterName, cancellationToken);
        }

        private async Task LoadVariantsAsync(AssetDownloadGameOption game, string characterName, CancellationToken cancellationToken)
        {
            try
            {
                await ResolveVariantForCharacterAsync(
                    game,
                    characterName,
                    AssetDownloadPathUtility.DefaultVariantName,
                    loadVariantsIfMissing: true,
                    cancellationToken).ConfigureAwait(false);

                List<AssetDownloadVariantOption> resolvedVariants = await InvokeOnMainThreadAsync(() =>
                {
                    AssetDownloadGameCache cache = GetOrCreateCache(game);
                    return cache.VariantsByCharacter.TryGetValue(characterName, out List<AssetDownloadVariantOption> loadedVariants)
                        ? new List<AssetDownloadVariantOption>(loadedVariants)
                        : new List<AssetDownloadVariantOption>();
                }).ConfigureAwait(false);

                await InvokeOnMainThreadAsync(() =>
                {
                    loadingVariantsForCharacters.Remove(characterName);
                    if (selectedCharacters.Count == 1
                        && string.Equals(activeCharacterName, characterName, StringComparison.OrdinalIgnoreCase))
                    {
                        variants.Clear();
                        variants.AddRange(resolvedVariants);
                        EnsureDefaultVariantSelection(variants, selectedVariants);
                    }
                    else
                    {
                        HashSet<string> selection = GetOrCreateVariantSelection(characterName);
                        EnsureDefaultVariantSelection(resolvedVariants, selection);
                    }

                    statusMessage = resolvedVariants.Count > 0
                        ? $"Loaded {resolvedVariants.Count} variant(s) for {characterName}."
                        : $"No variants were found for {characterName}.";
                    Repaint();
                });
            }
            catch (OperationCanceledException)
            {
                await InvokeOnMainThreadAsync(() =>
                {
                    loadingVariantsForCharacters.Remove(characterName);
                    statusMessage = $"Variant refresh cancelled for {characterName}.";
                    Repaint();
                });
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.Models, $"Failed to load variants for '{characterName}'.", exception);
                await InvokeOnMainThreadAsync(() =>
                {
                    loadingVariantsForCharacters.Remove(characterName);
                    statusMessage = $"Failed to load variants: {exception.Message}";
                    Repaint();
                });
            }
        }

        private void StartDownloadSelected()
        {
            if (isDownloading || isRefreshingGames)
            {
                return;
            }

            if (!EditorReadinessUtility.IsReadyForEditorWork())
            {
                statusMessage = "The Unity editor is busy compiling or importing. Start downloads once it is idle.";
                Repaint();
                return;
            }

            isDownloading = true;
            isImportingDownloadedAssets = false;
            lastManagerDownloadSucceeded = false;
            lastManagerDownloadFailed = false;
            lastManagerDownloadedCharacters.Clear();
            lastManagerDownloadedModelAssetPathsByCharacter.Clear();
            statusMessage = "Preparing selected downloads...";
            Repaint();
            CancellationToken cancellationToken = CreateDownloadCancellationToken();
            _ = DownloadSelectedAsync(cancellationToken);
        }

        private async Task DownloadSelectedAsync(CancellationToken cancellationToken)
        {
            AssetDownloadGameOption downloadedGame = null;
            string normalizedRoot = string.Empty;
            string summary = null;
            bool shouldShowSummary = false;
            bool failed = false;
            bool batchImportScopeOpened = false;
            var importedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var autoSetupAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> selectedCharacterNames = new List<string>();

            try
            {
                if (!TryGetBatchSelection(out downloadedGame, out selectedCharacterNames, out List<string> selectedVariantNames))
                {
                    throw new InvalidOperationException("Choose at least one character and one variant to download.");
                }

                Dictionary<string, List<string>> selectionSnapshot = CaptureVariantSelectionSnapshot(selectedCharacterNames);
                Dictionary<string, List<string>> selectionMap = await ResolveSelectionVariantMapAsync(
                    downloadedGame,
                    selectedCharacterNames,
                    selectionSnapshot,
                    selectedVariantNames,
                    cancellationToken).ConfigureAwait(false);

                List<AssetDownloadJob> jobs = await ResolveJobsAsync(downloadedGame, selectionMap, cancellationToken).ConfigureAwait(false);
                if (jobs.Count <= 0)
                {
                    throw new InvalidOperationException("No valid download targets could be resolved from the current selection.");
                }

                normalizedRoot = AssetDownloadPathUtility.EnsureProjectAssetsRoot(DefaultDownloadRoot);
                if (string.IsNullOrWhiteSpace(normalizedRoot))
                {
                    throw new InvalidOperationException("Choose a valid destination inside the Unity project's Assets folder.");
                }

                List<AssetDownloadPreparedJob> preparedJobs = await PrepareDownloadJobsAsync(
                    jobs,
                    normalizedRoot,
                    cancellationToken).ConfigureAwait(false);

                AssetDownloadPreparedJob cancelledJob = preparedJobs.FirstOrDefault(job => job != null && job.Cancelled);
                if (cancelledJob != null)
                {
                    statusMessage = $"Download cancelled while processing {cancelledJob.Job.CharacterName} ({cancelledJob.Job.Variant?.Name}).";
                    return;
                }

                foreach (AssetDownloadPreparedJob preparedJob in preparedJobs)
                {
                    if (preparedJob != null && !string.IsNullOrWhiteSpace(preparedJob.AssetTargetPath))
                    {
                        autoSetupAssetPaths.Add(preparedJob.AssetTargetPath);
                    }
                }

                int totalDownloadCount = preparedJobs.Sum(job => job?.FilesToDownload.Count ?? 0);
                if (totalDownloadCount > 0)
                {
                    await BeginDownloadBatchAsync().ConfigureAwait(false);
                    batchImportScopeOpened = true;
                    await DownloadPreparedJobsAsync(preparedJobs, cancellationToken).ConfigureAwait(false);
                }

                int totalDownloadedFiles = 0;
                int totalSkippedFiles = 0;
                foreach (AssetDownloadPreparedJob preparedJob in preparedJobs)
                {
                    if (preparedJob == null)
                    {
                        continue;
                    }

                    totalDownloadedFiles += preparedJob.FilesToDownload.Count;
                    totalSkippedFiles += Math.Max(0, preparedJob.SkippedFileCount);

                    foreach (string assetPath in preparedJob.ImportedAssetPaths)
                    {
                        if (!string.IsNullOrWhiteSpace(assetPath))
                        {
                            importedAssetPaths.Add(assetPath);
                            RecordDownloadedModelAssetPath(preparedJob.Job.CharacterName, assetPath);
                        }
                    }

                    HoyoToonLogger.Info(
                        HoyoToonLogCategory.Models,
                        $"Downloaded '{preparedJob.Job.CharacterName}' ({preparedJob.Job.Variant.Name}) to '{preparedJob.AssetTargetPath}'. Downloaded {preparedJob.FilesToDownload.Count} file(s), skipped {preparedJob.SkippedFileCount}.");
                }

                summary = $"Finished downloading {jobs.Count} selection(s). Downloaded files: {totalDownloadedFiles}. Skipped existing files: {totalSkippedFiles}.";
                shouldShowSummary = true;
                statusMessage = importedAssetPaths.Count > 0
                    ? "Importing downloaded assets..."
                    : "Finalizing download batch...";
                await InvokeOnMainThreadAsync(Repaint);
            }
            catch (OperationCanceledException)
            {
                failed = true;
                statusMessage = "Download cancelled.";
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.Models, "Asset download failed.", exception);
                failed = true;
                statusMessage = $"Download failed: {exception.Message}";
            }
            finally
            {
                if (batchImportScopeOpened)
                {
                    try
                    {
                        await BeginDownloadImportPhaseAsync(importedAssetPaths.Count > 0).ConfigureAwait(false);
                        await CompleteDownloadBatchAsync(importedAssetPaths, normalizedRoot).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        HoyoToonLogger.Error(HoyoToonLogCategory.Models, "Failed to finalize downloaded assets.", exception);
                        shouldShowSummary = false;
                        failed = true;
                        statusMessage = $"Download failed: {exception.Message}";
                    }
                }

                if (!failed && ShouldRunAutoSetupForGame(downloadedGame))
                {
                    IReadOnlyCollection<string> setupAssetPaths = autoSetupAssetPaths.Count > 0
                        ? autoSetupAssetPaths
                        : importedAssetPaths;

                    if (setupAssetPaths.Count > 0)
                    {
                        try
                        {
                            statusMessage = "Running Auto Setup on downloaded models...";
                            await InvokeOnMainThreadAsync(Repaint);

                            AutoSetupResult autoSetupResult = await RunAutoSetupForDownloadedAssetsAsync(setupAssetPaths).ConfigureAwait(false);
                            if (autoSetupResult != null)
                            {
                                string autoSetupSummary = autoSetupResult.Succeeded
                                    ? $"Auto Setup completed for {autoSetupResult.ProfileDisplayName}."
                                    : $"Auto Setup finished with {autoSetupResult.Errors.Count} error(s).";
                                summary = string.IsNullOrWhiteSpace(summary)
                                    ? autoSetupSummary
                                    : summary + " " + autoSetupSummary;
                            }
                        }
                        catch (Exception exception)
                        {
                            HoyoToonLogger.Error(HoyoToonLogCategory.Models, "Auto setup after download failed.", exception);
                            shouldShowSummary = false;
                            failed = true;
                            statusMessage = $"Auto Setup failed: {exception.Message}";
                        }
                    }
                }

                await InvokeOnMainThreadAsync(() =>
                {
                    isDownloading = false;
                    isImportingDownloadedAssets = false;
                    HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
                    if (failed)
                    {
                        lastManagerDownloadFailed = true;
                        lastManagerDownloadSucceeded = false;
                        OnboardingSignals.RecordOperationFailure(OnboardingOperationKind.Download, statusMessage);
                    }
                    else if (shouldShowSummary)
                    {
                        lastManagerDownloadFailed = false;
                        lastManagerDownloadSucceeded = true;
                        foreach (string characterName in selectedCharacterNames)
                        {
                            if (!string.IsNullOrWhiteSpace(characterName))
                            {
                                lastManagerDownloadedCharacters.Add(characterName);
                            }
                        }

                        OnboardingSignals.RecordOperationSuccess(OnboardingOperationKind.Download);
                        ClearCurrentSelection();
                    }

                    Repaint();
                });
            }

            if (shouldShowSummary && !string.IsNullOrWhiteSpace(summary))
            {
                HoyoToonLogger.Info(HoyoToonLogCategory.Models, summary);
                statusMessage = summary;
                await InvokeOnMainThreadAsync(() => HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(WindowTitle, summary, "OK"));
            }
        }

        private async Task<List<AssetDownloadPreparedJob>> PrepareDownloadJobsAsync(
            IReadOnlyList<AssetDownloadJob> jobs,
            string normalizedRoot,
            CancellationToken cancellationToken)
        {
            var preparedJobs = new List<AssetDownloadPreparedJob>();
            if (jobs == null || jobs.Count <= 0)
            {
                return preparedJobs;
            }

            for (int index = 0; index < jobs.Count; index++)
            {
                AssetDownloadJob job = jobs[index];
                AssetDownloadHsrFbxChoice hsrChoice = GetResolvedHsrChoiceForCharacter(job.CharacterName);
                await InvokeOnMainThreadAsync(() =>
                {
                    statusMessage = $"Preparing download {index + 1}/{jobs.Count}: {job.CharacterName} ({job.Variant?.Name})...";
                    Repaint();
                });

                AssetDownloadPreparedJob preparedJob = await PrepareDownloadJobAsync(
                    job,
                    normalizedRoot,
                    hsrChoice,
                    cancellationToken)
                    .ConfigureAwait(false);
                preparedJobs.Add(preparedJob);
                if (preparedJob != null && preparedJob.Cancelled)
                {
                    break;
                }
            }

            return preparedJobs;
        }

        private async Task<AssetDownloadPreparedJob> PrepareDownloadJobAsync(
            AssetDownloadJob job,
            string normalizedRoot,
            AssetDownloadHsrFbxChoice hsrChoice,
            CancellationToken cancellationToken)
        {
            if (job == null || job.Game == null || job.Variant == null)
            {
                throw new InvalidOperationException("A valid download job is required.");
            }

            if (connectionContext == null)
            {
                throw new InvalidOperationException("No Cloudreve connection is available.");
            }

            string assetTargetPath = AssetDownloadPathUtility.BuildAssetTargetPath(
                normalizedRoot,
                job.Game,
                job.CharacterName,
                job.Variant);
            string absoluteTargetPath = AssetDownloadPathUtility.AbsoluteFromAssetsPath(assetTargetPath);
            if (string.IsNullOrWhiteSpace(absoluteTargetPath))
            {
                throw new InvalidOperationException("The download target could not be resolved into a project Assets path.");
            }

            Directory.CreateDirectory(absoluteTargetPath);
            var preparedJob = new AssetDownloadPreparedJob(job, assetTargetPath);

            List<RemoteResourceEntry> files = await AssetDownloadCloudreveService
                .ListAllFilesAsync(connectionContext, job.Variant.FolderUri, cancellationToken)
                .ConfigureAwait(false);
            files = files.Where(file => file != null).ToList();

            if (AssetDownloadPathUtility.IsHonkaiStarRail(job.Game.Key, job.Game.DisplayName))
            {
                List<RemoteResourceEntry> rootFbxFiles = files
                    .Where(file => AssetDownloadPathUtility.IsRootLevelFile(file)
                        && file.RelativePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (rootFbxFiles.Count > 0)
                {
                    HashSet<string> allowedRootFbxPaths = new HashSet<string>(
                        AssetDownloadPathUtility.FilterRemoteFbxByChoice(rootFbxFiles, hsrChoice)
                            .Select(file => file.RelativePath),
                        StringComparer.OrdinalIgnoreCase);

                    files = files
                        .Where(file =>
                            !file.RelativePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                            || allowedRootFbxPaths.Contains(file.RelativePath))
                        .ToList();
                }
            }

            List<string> existingLocalPaths = new List<string>();
            for (int index = 0; index < files.Count; index++)
            {
                string localFilePath = AssetDownloadPathUtility.GetLocalFilePath(absoluteTargetPath, files[index].RelativePath);
                if (File.Exists(localFilePath))
                {
                    existingLocalPaths.Add(localFilePath);
                }
            }

            AssetDownloadOverwriteMode overwriteMode = AssetDownloadOverwriteMode.Overwrite;
            if (existingLocalPaths.Count > 0)
            {
                overwriteMode = await PromptOverwriteModeAsync(existingLocalPaths.Count, job.CharacterName, job.Variant.Name).ConfigureAwait(false);
                if (overwriteMode == AssetDownloadOverwriteMode.Cancel)
                {
                    preparedJob.Cancelled = true;
                    return preparedJob;
                }

                if (overwriteMode == AssetDownloadOverwriteMode.SkipExisting)
                {
                    files = files
                        .Where(file => !File.Exists(AssetDownloadPathUtility.GetLocalFilePath(absoluteTargetPath, file.RelativePath)))
                        .ToList();
                }
            }

            var importedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            preparedJob.SkippedFileCount = overwriteMode == AssetDownloadOverwriteMode.SkipExisting ? existingLocalPaths.Count : 0;

            for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
            {
                RemoteResourceEntry remoteFile = files[fileIndex];
                string localFilePath = AssetDownloadPathUtility.GetLocalFilePath(absoluteTargetPath, remoteFile.RelativePath);
                string importedAssetPath = AssetDownloadPathUtility.TryConvertAbsolutePathToAssetsPath(localFilePath, out string convertedAssetPath)
                    ? convertedAssetPath
                    : string.Empty;
                preparedJob.FilesToDownload.Add(new AssetDownloadPreparedFile(job, remoteFile, localFilePath, importedAssetPath));
                if (!string.IsNullOrWhiteSpace(importedAssetPath))
                {
                    importedAssetPaths.Add(importedAssetPath);
                }
            }

            if (preparedJob.FilesToDownload.Count > 0)
            {
                importedAssetPaths.Add(assetTargetPath);
            }

            preparedJob.ImportedAssetPaths.AddRange(importedAssetPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            return preparedJob;
        }

        private async Task DownloadPreparedJobsAsync(
            IReadOnlyList<AssetDownloadPreparedJob> preparedJobs,
            CancellationToken cancellationToken)
        {
            List<AssetDownloadPreparedFile> preparedFiles = BuildRoundRobinPreparedFiles(preparedJobs);
            int totalFileCount = preparedFiles.Count;
            if (totalFileCount <= 0)
            {
                return;
            }

            int completedFileCount = 0;
            await InvokeOnMainThreadAsync(() =>
            {
                statusMessage = $"Downloading 0/{totalFileCount} files...";
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar(WindowTitle, statusMessage, 0f);
                Repaint();
            });

            for (int index = 0; index < totalFileCount; index += DownloadBatchSize)
            {
                int batchSize = Math.Min(DownloadBatchSize, totalFileCount - index);
                List<AssetDownloadPreparedFile> batch = preparedFiles.GetRange(index, batchSize);
                Task[] batchTasks = batch
                    .Select(file => DownloadPreparedFileAsync(
                        file,
                        totalFileCount,
                        () => Interlocked.Increment(ref completedFileCount),
                        cancellationToken))
                    .ToArray();

                await Task.WhenAll(batchTasks).ConfigureAwait(false);
            }
        }

        private async Task DownloadPreparedFileAsync(
            AssetDownloadPreparedFile preparedFile,
            int totalFileCount,
            Func<int> reportCompleted,
            CancellationToken cancellationToken)
        {
            if (preparedFile == null || preparedFile.Job == null || preparedFile.RemoteFile == null)
            {
                throw new InvalidOperationException("A valid prepared download file is required.");
            }

            try
            {
                await AssetDownloadCloudreveService
                    .DownloadFileAsync(connectionContext, preparedFile.RemoteFile, preparedFile.LocalFilePath, cancellationToken)
                    .ConfigureAwait(false);

                int completedFileCount = reportCompleted?.Invoke() ?? 0;
                float progress = totalFileCount <= 0
                    ? 1f
                    : (float)completedFileCount / totalFileCount;
                string characterLabel = string.IsNullOrWhiteSpace(preparedFile.Job.CharacterName)
                    ? preparedFile.Job.Game.DisplayName
                    : preparedFile.Job.CharacterName;
                string progressMessage = $"Downloading {completedFileCount}/{totalFileCount} file(s)...\n{characterLabel}: {Path.GetFileName(preparedFile.RemoteFile.RelativePath)}";

                await InvokeOnMainThreadAsync(() =>
                {
                    statusMessage = $"Downloading {completedFileCount}/{totalFileCount} file(s)...";
                    HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar(WindowTitle, progressMessage, progress);
                    Repaint();
                });
            }
            catch (Exception exception)
            {
                string characterLabel = string.IsNullOrWhiteSpace(preparedFile.Job.CharacterName)
                    ? preparedFile.Job.Game.DisplayName
                    : preparedFile.Job.CharacterName;
                string variantLabel = preparedFile.Job.Variant?.Name ?? AssetDownloadPathUtility.DefaultVariantName;
                throw new InvalidOperationException(
                    $"Failed to download '{preparedFile.RemoteFile.RelativePath}' for {characterLabel} ({variantLabel}).",
                    exception);
            }
        }

        private static List<AssetDownloadPreparedFile> BuildRoundRobinPreparedFiles(IReadOnlyList<AssetDownloadPreparedJob> preparedJobs)
        {
            var queues = preparedJobs?
                .Where(job => job != null && job.FilesToDownload.Count > 0)
                .Select(job => new Queue<AssetDownloadPreparedFile>(job.FilesToDownload))
                .ToList() ?? new List<Queue<AssetDownloadPreparedFile>>();
            var orderedFiles = new List<AssetDownloadPreparedFile>();

            while (queues.Count > 0)
            {
                for (int index = 0; index < queues.Count;)
                {
                    Queue<AssetDownloadPreparedFile> queue = queues[index];
                    orderedFiles.Add(queue.Dequeue());
                    if (queue.Count <= 0)
                    {
                        queues.RemoveAt(index);
                        continue;
                    }

                    index++;
                }
            }

            return orderedFiles;
        }

        private Dictionary<string, List<string>> CaptureVariantSelectionSnapshot(IReadOnlyList<string> selectedCharacterNames)
        {
            var snapshot = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (selectedCharacterNames == null || selectedCharacterNames.Count <= 0)
            {
                return snapshot;
            }

            foreach (string characterName in selectedCharacterNames)
            {
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                if (selectedVariantsByCharacter.TryGetValue(characterName, out HashSet<string> selection) && selection.Count > 0)
                {
                    snapshot[characterName] = selection
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    continue;
                }

                snapshot[characterName] = new List<string>();
            }

            return snapshot;
        }

        private async Task<Dictionary<string, List<string>>> ResolveSelectionVariantMapAsync(
            AssetDownloadGameOption game,
            IReadOnlyList<string> selectedCharacterNames,
            IReadOnlyDictionary<string, List<string>> selectionSnapshot,
            IReadOnlyList<string> selectedVariantNames,
            CancellationToken cancellationToken)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (game == null || selectedCharacterNames == null || selectedCharacterNames.Count <= 0)
            {
                return map;
            }

            bool usePerCharacterSelection = selectedCharacterNames.Count > 1;
            List<string> sharedVariantNames = selectedVariantNames?
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            var unresolvedCharacters = new List<string>();

            foreach (string characterName in selectedCharacterNames)
            {
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                List<string> variantNames = selectionSnapshot != null
                    && selectionSnapshot.TryGetValue(characterName, out List<string> snapshotSelection)
                    && snapshotSelection != null
                    ? snapshotSelection
                    : new List<string>();

                if (!usePerCharacterSelection)
                {
                    variantNames = sharedVariantNames;
                }
                else if (variantNames.Count <= 0)
                {
                    variantNames = await ResolveDefaultVariantSelectionAsync(game, characterName, cancellationToken).ConfigureAwait(false);
                }

                if (variantNames.Count <= 0)
                {
                    unresolvedCharacters.Add(characterName);
                    continue;
                }

                map[characterName] = variantNames;
            }

            if (unresolvedCharacters.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Unable to resolve variant selections for: {string.Join(", ", unresolvedCharacters)}.");
            }

            return map;
        }

        private async Task<List<string>> ResolveDefaultVariantSelectionAsync(
            AssetDownloadGameOption game,
            string characterName,
            CancellationToken cancellationToken)
        {
            AssetDownloadVariantOption resolvedVariant = await ResolveVariantForCharacterAsync(
                game,
                characterName,
                AssetDownloadPathUtility.DefaultVariantName,
                loadVariantsIfMissing: true,
                cancellationToken).ConfigureAwait(false);

            if (resolvedVariant == null || string.IsNullOrWhiteSpace(resolvedVariant.Name))
            {
                return new List<string>();
            }

            await InvokeOnMainThreadAsync(() =>
            {
                HashSet<string> selection = GetOrCreateVariantSelection(characterName);
                if (selection.Count <= 0)
                {
                    selection.Add(resolvedVariant.Name);
                }

                Repaint();
            });

            return new List<string> { resolvedVariant.Name };
        }

        private Task BeginDownloadBatchAsync()
        {
            return InvokeOnMainThreadAsync(() =>
            {
                AssetDatabase.DisallowAutoRefresh();
                AssetDatabase.StartAssetEditing();
            });
        }

        private async Task BeginDownloadImportPhaseAsync(bool hasImportedAssetPaths)
        {
            await InvokeOnMainThreadAsync(() =>
            {
                isImportingDownloadedAssets = true;
                statusMessage = hasImportedAssetPaths
                    ? "Importing downloaded assets into Unity..."
                    : "Refreshing Unity AssetDatabase...";
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar(WindowTitle, statusMessage, 0.92f);
                Repaint();
            }).ConfigureAwait(false);

            await WaitForEditorDelayCallAsync().ConfigureAwait(false);
        }

        private Task CompleteDownloadBatchAsync(IEnumerable<string> assetPaths, string rootAssetPath)
        {
            return InvokeOnMainThreadAsync(() =>
            {
                List<string> normalizedAssetPaths = assetPaths?
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => path.Replace('\\', '/'))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();
                string normalizedRootAssetPath = string.IsNullOrWhiteSpace(rootAssetPath)
                    ? string.Empty
                    : rootAssetPath.Replace('\\', '/');

                if (normalizedAssetPaths.Count <= 0 && string.IsNullOrWhiteSpace(normalizedRootAssetPath))
                {
                    try
                    {
                        AssetDatabase.StopAssetEditing();
                    }
                    finally
                    {
                        AssetDatabase.AllowAutoRefresh();
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }

                    return;
                }

                try
                {
                    AssetDatabase.StopAssetEditing();
                }
                finally
                {
                    AssetDatabase.AllowAutoRefresh();
                }

                statusMessage = "Refreshing Unity AssetDatabase for downloaded assets...";
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar(WindowTitle, statusMessage, 0.95f);
                Repaint();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                statusMessage = "Unity asset import complete.";
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar(WindowTitle, statusMessage, 1f);
                Repaint();
            });
        }

        private async Task<List<AssetDownloadJob>> ResolveJobsAsync(
            AssetDownloadGameOption game,
            IReadOnlyDictionary<string, List<string>> selectionMap,
            CancellationToken cancellationToken)
        {
            var jobs = new List<AssetDownloadJob>();
            if (game == null || selectionMap == null || selectionMap.Count <= 0)
            {
                return jobs;
            }

            foreach (KeyValuePair<string, List<string>> entry in selectionMap)
            {
                string characterName = entry.Key;
                List<string> variantNames = entry.Value ?? new List<string>();
                foreach (string variantName in variantNames)
                {
                    AssetDownloadVariantOption variant = await ResolveVariantForCharacterAsync(
                        game,
                        characterName,
                        variantName,
                        loadVariantsIfMissing: true,
                        cancellationToken).ConfigureAwait(false);
                    if (variant != null)
                    {
                        jobs.Add(new AssetDownloadJob(game, characterName, variant));
                    }
                }
            }

            return jobs;
        }

        private async Task<AssetDownloadVariantOption> ResolveVariantForCharacterAsync(
            AssetDownloadGameOption game,
            string characterName,
            string variantName,
            bool loadVariantsIfMissing,
            CancellationToken cancellationToken)
        {
            if (game == null || string.IsNullOrWhiteSpace(characterName))
            {
                return null;
            }

            bool hasCachedVariants = false;
            AssetDownloadVariantOption cachedVariant = null;
            string characterFolderUri = null;
            AssetDownloadConnectionContext connection = null;

            await InvokeOnMainThreadAsync(() =>
            {
                AssetDownloadGameCache cache = GetOrCreateCache(game);
                connection = connectionContext;

                if (cache.VariantsByCharacter.TryGetValue(characterName, out List<AssetDownloadVariantOption> cachedVariants)
                    && cachedVariants.Count > 0)
                {
                    hasCachedVariants = true;
                    cachedVariant = PickVariant(cachedVariants, variantName);
                    return;
                }

                if (!loadVariantsIfMissing || connectionContext == null)
                {
                    return;
                }

                if (!cache.CharacterFolderUrisByName.TryGetValue(characterName, out string cachedCharacterFolderUri)
                    || string.IsNullOrWhiteSpace(cachedCharacterFolderUri))
                {
                    if (cache.Characters.Count <= 0)
                    {
                        return;
                    }

                    cachedCharacterFolderUri = cache.CharacterFolderUrisByName.TryGetValue(characterName, out string resolvedUri)
                        ? resolvedUri
                        : null;
                }

                characterFolderUri = cachedCharacterFolderUri;
            }).ConfigureAwait(false);

            if (hasCachedVariants)
            {
                return cachedVariant;
            }

            if (!loadVariantsIfMissing || connection == null || string.IsNullOrWhiteSpace(characterFolderUri))
            {
                return null;
            }

            List<AssetDownloadDirectoryEntry> entries = await AssetDownloadCloudreveService
                .ListFolderEntriesAsync(connection, characterFolderUri, cancellationToken)
                .ConfigureAwait(false);

            List<AssetDownloadVariantOption> loadedVariants = entries
                .Where(entry => entry != null && entry.IsDirectory && !string.IsNullOrWhiteSpace(entry.Name))
                .Select(entry => new AssetDownloadVariantOption(entry.Name, entry.Uri))
                .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool hasFilesAtCharacterRoot = entries.Any(entry => entry != null && !entry.IsDirectory);
            if (hasFilesAtCharacterRoot || loadedVariants.Count <= 0)
            {
                loadedVariants.Insert(0, new AssetDownloadVariantOption(AssetDownloadPathUtility.DefaultVariantName, characterFolderUri));
            }

            await InvokeOnMainThreadAsync(() =>
            {
                AssetDownloadGameCache cache = GetOrCreateCache(game);
                cache.VariantsByCharacter[characterName] = loadedVariants;
            }).ConfigureAwait(false);

            return PickVariant(loadedVariants, variantName);
        }

        private bool TryGetBatchSelection(
            out AssetDownloadGameOption game,
            out List<string> selectedCharacterNames,
            out List<string> selectedVariantNames)
        {
            game = null;
            selectedCharacterNames = new List<string>();
            selectedVariantNames = new List<string>();

            if (!TryGetSelectedGame(out game))
            {
                return false;
            }

            selectedCharacterNames = GetOrderedSelectedCharacters();
            if (selectedCharacterNames.Count <= 0)
            {
                return false;
            }

            if (selectedCharacterNames.Count == 1)
            {
                string selectedCharacter = selectedCharacterNames[0];
                AssetDownloadGameCache cache = GetOrCreateCache(game);
                if (!cache.VariantsByCharacter.TryGetValue(selectedCharacter, out List<AssetDownloadVariantOption> availableVariants))
                {
                    return false;
                }

                foreach (AssetDownloadVariantOption option in availableVariants)
                {
                    if (selectedVariants.Contains(option.Name))
                    {
                        selectedVariantNames.Add(option.Name);
                    }
                }

                return selectedVariantNames.Count > 0;
            }

            var distinctVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string characterName in selectedCharacterNames)
            {
                if (!selectedVariantsByCharacter.TryGetValue(characterName, out HashSet<string> selection) || selection.Count <= 0)
                {
                    continue;
                }

                foreach (string variantName in selection)
                {
                    distinctVariants.Add(variantName);
                }
            }

            selectedVariantNames.AddRange(distinctVariants);
            selectedVariantNames.Sort(StringComparer.OrdinalIgnoreCase);
            return selectedVariantNames.Count > 0;
        }

        private Dictionary<string, List<string>> BuildSelectionVariantMap(
            IReadOnlyList<string> selectedCharacterNames,
            IReadOnlyList<string> selectedVariantNames)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (selectedCharacterNames == null || selectedCharacterNames.Count <= 0)
            {
                return map;
            }

            bool usePerCharacterSelection = selectedCharacterNames.Count > 1 && selectedVariantsByCharacter.Count > 0;
            foreach (string characterName in selectedCharacterNames)
            {
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                if (usePerCharacterSelection
                    && selectedVariantsByCharacter.TryGetValue(characterName, out HashSet<string> selection)
                    && selection.Count > 0)
                {
                    map[characterName] = selection.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
                    continue;
                }

                map[characterName] = selectedVariantNames?.Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();
            }

            return map;
        }

        private static AssetDownloadVariantOption PickVariant(IReadOnlyList<AssetDownloadVariantOption> availableVariants, string variantName)
        {
            if (availableVariants == null || availableVariants.Count <= 0)
            {
                return null;
            }

            for (int index = 0; index < availableVariants.Count; index++)
            {
                AssetDownloadVariantOption option = availableVariants[index];
                if (option != null && string.Equals(option.Name, variantName, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            for (int index = 0; index < availableVariants.Count; index++)
            {
                AssetDownloadVariantOption option = availableVariants[index];
                if (option != null && string.Equals(option.Name, AssetDownloadPathUtility.DefaultVariantName, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            return availableVariants[0];
        }

        private List<AssetDownloadGameOption> BuildGameOptions(IReadOnlyList<AssetDownloadDirectoryEntry> rootEntries)
        {
            IReadOnlyList<GameConfigSO> gameConfigs = GameRegistry.GameConfigs;
            Dictionary<string, string> displayNameByKey = ResourceRegistry.Resources
                .Where(resource => resource != null && !string.IsNullOrWhiteSpace(resource.Key))
                .GroupBy(resource => resource.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        HoyoToonResourcesSO resource = group.FirstOrDefault();
                        return string.IsNullOrWhiteSpace(resource?.DisplayName) ? group.Key : resource.DisplayName;
                    },
                    StringComparer.OrdinalIgnoreCase);

            var options = new List<AssetDownloadGameOption>();
            foreach (GameConfigSO gameConfig in gameConfigs)
            {
                if (gameConfig == null || string.IsNullOrWhiteSpace(gameConfig.Key))
                {
                    continue;
                }

                string displayName = displayNameByKey.TryGetValue(gameConfig.Key, out string mappedDisplayName)
                    ? mappedDisplayName
                    : gameConfig.Key;

                if (!AssetDownloadPathUtility.TryMatchFolder(rootEntries, gameConfig.Key, displayName, out AssetDownloadDirectoryEntry folderEntry))
                {
                    continue;
                }

                options.Add(new AssetDownloadGameOption(gameConfig.Key, displayName, folderEntry.Name, folderEntry.Uri));
            }

            return options
                .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private AssetDownloadGameCache GetOrCreateCache(AssetDownloadGameOption game)
        {
            if (game == null)
            {
                return null;
            }

            if (!gameCacheByKey.TryGetValue(game.Key, out AssetDownloadGameCache cache))
            {
                cache = new AssetDownloadGameCache(game);
                gameCacheByKey[game.Key] = cache;
            }

            return cache;
        }

        private bool TryGetSelectedGame(out AssetDownloadGameOption game)
        {
            game = null;
            if (selectedGameIndex < 0 || selectedGameIndex >= availableGames.Count)
            {
                return false;
            }

            game = availableGames[selectedGameIndex];
            return game != null;
        }

        private IReadOnlyList<string> GetFilteredCharacters()
        {
            if (!filteredCharactersDirty)
            {
                return filteredCharactersCache;
            }

            filteredCharactersCache.Clear();
            if (string.IsNullOrWhiteSpace(characterSearch))
            {
                filteredCharactersCache.AddRange(characters);
                filteredCharactersDirty = false;
                return filteredCharactersCache;
            }

            for (int index = 0; index < characters.Count; index++)
            {
                string characterName = characters[index];
                if (!string.IsNullOrWhiteSpace(characterName)
                    && characterName.IndexOf(characterSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filteredCharactersCache.Add(characterName);
                }
            }

            filteredCharactersDirty = false;
            return filteredCharactersCache;
        }

        private List<string> GetOrderedSelectedCharacters()
        {
            var ordered = new List<string>();
            foreach (string characterName in characters)
            {
                if (selectedCharacters.Contains(characterName))
                {
                    ordered.Add(characterName);
                }
            }

            return ordered;
        }

        private HashSet<string> GetOrCreateVariantSelection(string characterName)
        {
            if (!selectedVariantsByCharacter.TryGetValue(characterName, out HashSet<string> selection))
            {
                selection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                selectedVariantsByCharacter[characterName] = selection;
            }

            return selection;
        }

        private void RecordDownloadedModelAssetPath(string characterName, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(characterName)
                || string.IsNullOrWhiteSpace(assetPath)
                || !IsModelAssetPath(assetPath))
            {
                return;
            }

            if (!lastManagerDownloadedModelAssetPathsByCharacter.TryGetValue(characterName, out List<string> paths))
            {
                paths = new List<string>();
                lastManagerDownloadedModelAssetPathsByCharacter[characterName] = paths;
            }

            string normalizedPath = assetPath.Replace('\\', '/');
            if (!paths.Any(path => string.Equals(path, normalizedPath, StringComparison.OrdinalIgnoreCase)))
            {
                paths.Add(normalizedPath);
            }
        }

        private static bool IsModelAssetPath(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                && (assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                    || assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
        }

        private void HandleCharacterToggle(string characterName, bool isSelected)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return;
            }

            bool wasMultiSelection = selectedCharacters.Count > 1;
            if (isSelected)
            {
                selectedCharacters.Add(characterName);
                activeCharacterName = characterName;
            }
            else
            {
                selectedCharacters.Remove(characterName);
                if (string.Equals(activeCharacterName, characterName, StringComparison.OrdinalIgnoreCase))
                {
                    activeCharacterName = selectedCharacters.FirstOrDefault() ?? string.Empty;
                }
            }

            bool isMultiSelection = selectedCharacters.Count > 1;
            if (!wasMultiSelection && isMultiSelection)
            {
                MigrateSharedVariantSelectionToActiveCharacter();
            }
            else if (wasMultiSelection && !isMultiSelection)
            {
                MigratePerCharacterSelectionToSharedSelection();
            }
            else if (selectedCharacters.Count <= 0)
            {
                ClearVariantSelection();
            }

            filteredCharactersDirty = true;
            Repaint();
        }

        private void ApplyLoadedCharacters(AssetDownloadGameOption game, IReadOnlyList<string> loadedCharacters)
        {
            if (!TryGetSelectedGame(out AssetDownloadGameOption selectedGame)
                || !string.Equals(selectedGame.Key, game.Key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            characters.Clear();
            characters.AddRange(loadedCharacters ?? Array.Empty<string>());
            filteredCharactersDirty = true;
            selectedCharacters.Clear();
            selectedVariantsByCharacter.Clear();
            selectedVariants.Clear();
            hsrFbxChoiceByCharacter.Clear();
            variants.Clear();
            activeCharacterName = string.Empty;
            variantSectionScroll = Vector2.zero;
        }

        private void EnsureDefaultVariantSelection(
            IReadOnlyList<AssetDownloadVariantOption> availableVariants,
            HashSet<string> selection)
        {
            if (selection == null || selection.Count > 0 || availableVariants == null || availableVariants.Count <= 0)
            {
                return;
            }

            AssetDownloadVariantOption defaultVariant = PickVariant(availableVariants, AssetDownloadPathUtility.DefaultVariantName)
                ?? availableVariants[0];
            if (defaultVariant != null && !string.IsNullOrWhiteSpace(defaultVariant.Name))
            {
                selection.Add(defaultVariant.Name);
            }
        }

        private void MigrateSharedVariantSelectionToActiveCharacter()
        {
            if (selectedVariants.Count <= 0 || string.IsNullOrWhiteSpace(activeCharacterName))
            {
                return;
            }

            HashSet<string> selection = GetOrCreateVariantSelection(activeCharacterName);
            selection.Clear();
            selection.UnionWith(selectedVariants);
            selectedVariants.Clear();
        }

        private void MigratePerCharacterSelectionToSharedSelection()
        {
            selectedVariants.Clear();
            string remainingCharacter = selectedCharacters.FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(remainingCharacter))
            {
                return;
            }

            activeCharacterName = remainingCharacter;
            if (selectedVariantsByCharacter.TryGetValue(remainingCharacter, out HashSet<string> selection))
            {
                selectedVariants.UnionWith(selection);
            }

            if (TryGetSelectedGame(out AssetDownloadGameOption game))
            {
                AssetDownloadGameCache cache = GetOrCreateCache(game);
                if (cache.VariantsByCharacter.TryGetValue(remainingCharacter, out List<AssetDownloadVariantOption> currentVariants))
                {
                    variants.Clear();
                    variants.AddRange(currentVariants);
                    EnsureDefaultVariantSelection(currentVariants, selectedVariants);
                }
            }
        }

        private Vector2 GetVariantScroll(string characterName)
        {
            return variantScrollByCharacter.TryGetValue(characterName, out Vector2 scroll)
                ? scroll
                : Vector2.zero;
        }

        private void SetVariantScroll(string characterName, Vector2 scroll)
        {
            if (!string.IsNullOrWhiteSpace(characterName))
            {
                variantScrollByCharacter[characterName] = scroll;
            }
        }

        private void ClearCurrentSelection()
        {
            selectedCharacters.Clear();
            activeCharacterName = string.Empty;
            ClearVariantSelection();
            statusMessage = string.Empty;
            Repaint();
        }

        private void ClearCharacterSelection()
        {
            characters.Clear();
            filteredCharactersCache.Clear();
            selectedCharacters.Clear();
            activeCharacterName = string.Empty;
            characterSearch = string.Empty;
            filteredCharactersDirty = true;
            characterScroll = Vector2.zero;
            ClearVariantSelection();
        }

        private void ClearVariantSelection()
        {
            variants.Clear();
            selectedVariants.Clear();
            selectedVariantsByCharacter.Clear();
            loadingVariantsForCharacters.Clear();
            hsrFbxChoiceByCharacter.Clear();
            variantScrollByCharacter.Clear();
            variantScroll = Vector2.zero;
            variantSectionScroll = Vector2.zero;
        }

        private bool CanClearSelection()
        {
            return !isRefreshingGames
                && !isDownloading
                && (selectedCharacters.Count > 0 || selectedVariants.Count > 0 || selectedVariantsByCharacter.Count > 0);
        }

        private bool CanDownloadSelection()
        {
            return !isRefreshingGames && !isDownloading && TryGetBatchSelection(out _, out _, out _);
        }

        private static bool SupportsAutoSetup(AssetDownloadGameOption game)
        {
            return game != null && AutoSetupRegistry.TryGetProfile(game.Key, out _);
        }

        private bool ShouldRunAutoSetupForGame(AssetDownloadGameOption game)
        {
            return autoSetupAfterDownload && SupportsAutoSetup(game);
        }

        private void LoadPrefs()
        {
            hsrFbxChoice = (AssetDownloadHsrFbxChoice)EditorPrefs.GetInt(PrefsKey(HsrFbxChoiceKey), (int)AssetDownloadHsrFbxChoice.WithAnims);
            autoSetupAfterDownload = EditorPrefs.GetBool(PrefsKey(AutoSetupAfterDownloadKey), false);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(PrefsKey(HsrFbxChoiceKey), (int)hsrFbxChoice);
            EditorPrefs.SetBool(PrefsKey(AutoSetupAfterDownloadKey), autoSetupAfterDownload);
        }

        private static string PrefsKey(string key)
        {
            return HoyoToonEditorPrefs.ProjectKey(key);
        }

        private string GetVariantSectionSubtitle()
        {
            List<string> orderedSelectedCharacters = GetOrderedSelectedCharacters();
            if (orderedSelectedCharacters.Count <= 0)
            {
                return "Select one or more characters to load their variants.";
            }

            if (orderedSelectedCharacters.Count == 1)
            {
                return $"Choose one or more variants for {orderedSelectedCharacters[0]}.";
            }

            return "Each selected character keeps its own variant selection for batch downloads.";
        }

        private string BuildActionSummary()
        {
            int characterCount = selectedCharacters.Count;
            int variantCount = GetDistinctSelectedVariantCount();
            if (characterCount <= 0 || variantCount <= 0)
            {
                return $"Select characters and variants, then download to {DefaultDownloadRoot}.";
            }

            return $"{characterCount:N0} character(s) and {variantCount:N0} variant(s) are ready to download.";
        }

        private bool ShouldShowLibraryStatus()
        {
            if (string.IsNullOrWhiteSpace(statusMessage))
            {
                return false;
            }

            return isRefreshingGames
                || isDownloading
                || pendingAutoRefresh
                || statusMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase)
                || statusMessage.StartsWith("Download failed", StringComparison.OrdinalIgnoreCase)
                || statusMessage.StartsWith("Download cancelled", StringComparison.OrdinalIgnoreCase)
                || statusMessage.StartsWith("Waiting", StringComparison.OrdinalIgnoreCase)
                || statusMessage.StartsWith("The Unity editor is busy", StringComparison.OrdinalIgnoreCase);
        }

        private MessageType GetStatusMessageType()
        {
            if (string.IsNullOrWhiteSpace(statusMessage))
            {
                return MessageType.None;
            }

            return statusMessage.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                ? MessageType.Error
                : MessageType.Info;
        }

        private int GetDistinctSelectedVariantCount()
        {
            if (selectedCharacters.Count <= 1)
            {
                return selectedVariants.Count;
            }

            var distinctVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HashSet<string> selection in selectedVariantsByCharacter.Values)
            {
                if (selection == null)
                {
                    continue;
                }

                foreach (string variantName in selection)
                {
                    distinctVariants.Add(variantName);
                }
            }

            return distinctVariants.Count;
        }

        private static void EnsureStyles()
        {
            if (sectionTitleStyle == null)
            {
                sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    margin = new RectOffset(0, 0, 0, 2),
                };
            }

            if (sectionSubtitleStyle == null)
            {
                sectionSubtitleStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                {
                    margin = new RectOffset(0, 0, 0, 4),
                };
            }

            if (summaryStyle == null)
            {
                summaryStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true,
                };
            }

            if (actionButtonStyle == null)
            {
                actionButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fixedHeight = ActionButtonHeight,
                };
            }

            if (selectionButtonStyle == null)
            {
                selectionButtonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fixedHeight = 0f,
                    padding = new RectOffset(6, 6, 2, 2),
                    margin = new RectOffset(0, 0, 0, 0),
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true,
                };
            }
        }

        private static void DrawSection(string title, string subtitle, Action drawContents)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, sectionTitleStyle);
                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    EditorGUILayout.LabelField(subtitle, sectionSubtitleStyle);
                }

                drawContents?.Invoke();
            }

            EditorGUILayout.Space(SectionSpacing);
        }

        private static GUIStyle GetToolbarSearchCancelStyle(bool isEmpty)
        {
            if (isEmpty)
            {
                if (toolbarSearchCancelButtonEmpty == null)
                {
                    toolbarSearchCancelButtonEmpty = GUI.skin?.FindStyle("ToolbarSearchCancelButtonEmpty") ?? GUIStyle.none;
                }

                return toolbarSearchCancelButtonEmpty;
            }

            if (toolbarSearchCancelButton == null)
            {
                toolbarSearchCancelButton = GUI.skin?.FindStyle("ToolbarSearchCancelButton") ?? GUIStyle.none;
            }

            return toolbarSearchCancelButton;
        }

        private static int DrawScrollableSelection(
            IReadOnlyList<string> items,
            int selectedIndex,
            ref Vector2 scroll,
            bool disabled,
            string emptyMessage,
            int minVisibleItems = 3,
            int maxVisibleItems = 5)
        {
            if (items == null || items.Count <= 0)
            {
                EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                return -1;
            }

            using (new EditorGUI.DisabledScope(disabled))
            {
                int visibleItemCount = Mathf.Clamp(items.Count, minVisibleItems, maxVisibleItems);
                float itemWidth = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - SelectionViewHorizontalPadding);
                float viewportHeight = (visibleItemCount * SelectionButtonHeight) + ((visibleItemCount - 1) * 2f) + 8f;
                using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Height(viewportHeight)))
                {
                    scroll = scrollView.scrollPosition;
                    for (int index = 0; index < items.Count; index++)
                    {
                        string item = items[index];
                        float itemHeight = GetSelectionItemHeight(item, itemWidth);
                        bool toggled = GUILayout.Toggle(
                            index == selectedIndex,
                            new GUIContent(item, item),
                            selectionButtonStyle,
                            GUILayout.Height(itemHeight));
                        if (toggled && index != selectedIndex)
                        {
                            selectedIndex = index;
                        }

                        if (index < items.Count - 1)
                        {
                            EditorGUILayout.Space(2f);
                        }
                    }
                }
            }

            return selectedIndex;
        }

        private static void DrawMultiSelectList(
            IReadOnlyList<string> items,
            HashSet<string> selection,
            ref Vector2 scroll,
            bool disabled,
            Action<string> onSelected,
            Action<string, bool> onToggle,
            float minHeight = 90f,
            float maxHeight = 180f)
        {
            if (items == null || items.Count <= 0)
            {
                EditorGUILayout.HelpBox("No items are available.", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(disabled))
            {
                float viewWidth = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - SelectionViewHorizontalPadding);
                float contentHeight = GetSelectionGridHeight(items, viewWidth, 4);
                if (contentHeight <= maxHeight)
                {
                    DrawSelectionGrid(items, selection, onSelected, onToggle, viewWidth, 4);
                    scroll = Vector2.zero;
                    return;
                }

                float viewportHeight = Mathf.Clamp(contentHeight, minHeight, maxHeight);
                using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll, false, false, GUILayout.Height(viewportHeight)))
                {
                    scroll = scrollView.scrollPosition;
                    DrawSelectionGrid(items, selection, onSelected, onToggle, viewWidth, 4);
                }
            }
        }

        private static void DrawSelectionGrid(
            IReadOnlyList<string> items,
            HashSet<string> selection,
            Action<string> onSelected,
            Action<string, bool> onToggle,
            float viewWidth,
            int maxColumns)
        {
            if (items == null || items.Count <= 0 || selection == null)
            {
                return;
            }

            float availableWidth = Mathf.Max(120f, viewWidth);
            int columns = Mathf.Clamp(
                Mathf.FloorToInt((availableWidth + SelectionGridGap) / (SelectionColumnWidth + SelectionGridGap)),
                1,
                maxColumns);
            float totalGapWidth = (columns - 1) * SelectionGridGap;
            float columnWidth = Mathf.Floor((availableWidth - totalGapWidth) / columns);
            int rowCount = Mathf.CeilToInt(items.Count / (float)columns);

            for (int row = 0; row < rowCount; row++)
            {
                float rowHeight = GetSelectionRowHeight(items, row, columns, columnWidth);
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int index = row * columns + column;
                        if (index >= items.Count)
                        {
                            GUILayout.FlexibleSpace();
                            break;
                        }

                        if (column > 0)
                        {
                            GUILayout.Space(SelectionGridGap);
                        }

                        string item = items[index];
                        bool wasSelected = selection.Contains(item);
                        bool isSelected = GUILayout.Toggle(
                            wasSelected,
                            new GUIContent(item, item),
                            selectionButtonStyle,
                            GUILayout.Width(columnWidth),
                            GUILayout.Height(rowHeight));

                        if (isSelected == wasSelected)
                        {
                            continue;
                        }

                        if (isSelected)
                        {
                            selection.Add(item);
                            onSelected?.Invoke(item);
                        }
                        else
                        {
                            selection.Remove(item);
                        }

                        onToggle?.Invoke(item, isSelected);
                    }
                }

                if (row < rowCount - 1)
                {
                    GUILayout.Space(2f);
                }
            }
        }

        private static float GetSelectionGridHeight(int itemCount, float viewWidth, int maxColumns)
        {
            if (itemCount <= 0)
            {
                return 0f;
            }

            float availableWidth = Mathf.Max(120f, viewWidth);
            int columns = Mathf.Clamp(
                Mathf.FloorToInt((availableWidth + SelectionGridGap) / (SelectionColumnWidth + SelectionGridGap)),
                1,
                maxColumns);
            int rowCount = Mathf.CeilToInt(itemCount / (float)columns);
            return rowCount * (SelectionButtonHeight + 2f);
        }

        private static float GetSelectionGridHeight(IReadOnlyList<string> items, float viewWidth, int maxColumns)
        {
            if (items == null || items.Count <= 0)
            {
                return 0f;
            }

            float availableWidth = Mathf.Max(120f, viewWidth);
            int columns = Mathf.Clamp(
                Mathf.FloorToInt((availableWidth + SelectionGridGap) / (SelectionColumnWidth + SelectionGridGap)),
                1,
                maxColumns);
            float totalGapWidth = (columns - 1) * SelectionGridGap;
            float columnWidth = Mathf.Floor((availableWidth - totalGapWidth) / columns);
            int rowCount = Mathf.CeilToInt(items.Count / (float)columns);
            float height = 0f;

            for (int row = 0; row < rowCount; row++)
            {
                height += GetSelectionRowHeight(items, row, columns, columnWidth);
                if (row < rowCount - 1)
                {
                    height += 2f;
                }
            }

            return height;
        }

        private static float GetSelectionRowHeight(
            IReadOnlyList<string> items,
            int row,
            int columns,
            float columnWidth)
        {
            if (items == null || items.Count <= 0)
            {
                return SelectionButtonHeight;
            }

            int startIndex = row * columns;
            int endIndex = Mathf.Min(startIndex + columns, items.Count);
            float rowHeight = SelectionButtonHeight;

            for (int index = startIndex; index < endIndex; index++)
            {
                rowHeight = Mathf.Max(rowHeight, GetSelectionItemHeight(items[index], columnWidth));
            }

            return rowHeight;
        }

        private static float GetSelectionItemHeight(string item, float width)
        {
            float targetWidth = Mathf.Max(32f, width - selectionButtonStyle.padding.horizontal);
            return Mathf.Max(
                SelectionButtonHeight,
                selectionButtonStyle.CalcHeight(new GUIContent(item, item), targetWidth));
        }

        private async Task<AutoSetupResult> RunAutoSetupForDownloadedAssetsAsync(IEnumerable<string> importedAssetPaths)
        {
            return await InvokeOnMainThreadAsync(() =>
            {
                List<UnityEngine.Object> selectionTargets = ResolveAutoSetupSelectionTargets(importedAssetPaths);
                if (selectionTargets.Count <= 0)
                {
                    return null;
                }

                UnityEngine.Object[] previousSelection = Selection.objects?.ToArray() ?? Array.Empty<UnityEngine.Object>();
                UnityEngine.Object previousActiveObject = Selection.activeObject;
                AutoSetupResult aggregateResult = null;

                try
                {
                    foreach (UnityEngine.Object selectionTarget in selectionTargets)
                    {
                        if (selectionTarget == null)
                        {
                            continue;
                        }

                        Selection.objects = new[] { selectionTarget };
                        Selection.activeObject = selectionTarget;

                        AutoSetupResult result = AutoSetup.RunSelection(new AutoSetupOptions
                        {
                            ShowDialogs = true,
                            PromptForRenderPipelineSelection = false,
                        });
                        aggregateResult = MergeAutoSetupResult(aggregateResult, result);
                    }

                    return aggregateResult;
                }
                finally
                {
                    Selection.objects = previousSelection;
                    Selection.activeObject = previousActiveObject;
                }
            }).ConfigureAwait(false);
        }

        private static AutoSetupResult MergeAutoSetupResult(AutoSetupResult aggregateResult, AutoSetupResult result)
        {
            if (result == null)
            {
                return aggregateResult;
            }

            if (aggregateResult == null)
            {
                aggregateResult = new AutoSetupResult(result.ProfileKey, result.ProfileDisplayName);
            }

            foreach (string featureId in result.ExecutedFeatures)
            {
                aggregateResult.RecordFeature(featureId);
            }

            foreach (string warning in result.Warnings)
            {
                aggregateResult.RecordWarning(warning);
            }

            foreach (string error in result.Errors)
            {
                aggregateResult.RecordError(error);
            }

            aggregateResult.MaterialsCreated += result.MaterialsCreated;
            aggregateResult.MaterialsUpdated += result.MaterialsUpdated;
            aggregateResult.ModelsConverted += result.ModelsConverted;
            aggregateResult.ModelImportSettingsApplied += result.ModelImportSettingsApplied;
            aggregateResult.TangentApplications += result.TangentApplications;
            aggregateResult.PrerequisiteFailures += result.PrerequisiteFailures;
            return aggregateResult;
        }

        private static List<UnityEngine.Object> ResolveAutoSetupSelectionTargets(IEnumerable<string> importedAssetPaths)
        {
            var targets = new List<UnityEngine.Object>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string assetPath in importedAssetPaths ?? Enumerable.Empty<string>())
            {
                string normalizedAssetPath = string.IsNullOrWhiteSpace(assetPath)
                    ? string.Empty
                    : assetPath.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(normalizedAssetPath)
                    || !seenPaths.Add(normalizedAssetPath))
                {
                    continue;
                }

                bool isFbx = normalizedAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
                bool isFolder = AssetDatabase.IsValidFolder(normalizedAssetPath);
                if (!isFbx && !isFolder)
                {
                    continue;
                }

                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(normalizedAssetPath);
                if (asset != null)
                {
                    targets.Add(asset);
                }
            }

            return targets;
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

        private static void ScheduleOnMainThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == MainThreadId)
            {
                action();
                return;
            }

            MainThreadQueue.Enqueue(action);
        }

        private CancellationToken CreateRefreshCancellationToken()
        {
            CancelAndDispose(ref refreshCancellationSource);
            refreshCancellationSource = new CancellationTokenSource();
            return refreshCancellationSource.Token;
        }

        private CancellationToken CreateDownloadCancellationToken()
        {
            CancelAndDispose(ref downloadCancellationSource);
            downloadCancellationSource = new CancellationTokenSource();
            return downloadCancellationSource.Token;
        }

        private void CancelOutstandingOperations()
        {
            pendingAutoRefresh = false;
            isRefreshingGames = false;
            isDownloading = false;
            isImportingDownloadedAssets = false;
            loadingVariantsForCharacters.Clear();
            CancelAndDispose(ref refreshCancellationSource);
            CancelAndDispose(ref downloadCancellationSource);
        }

        private static void CancelAndDispose(ref CancellationTokenSource source)
        {
            if (source == null)
            {
                return;
            }

            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            source.Dispose();
            source = null;
        }

        private static Task InvokeOnMainThreadAsync(Action action)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            if (Thread.CurrentThread.ManagedThreadId == MainThreadId)
            {
                action();
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            MainThreadQueue.Enqueue(() =>
            {
                try
                {
                    action();
                    completion.TrySetResult(true);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            });
            return completion.Task;
        }

        private static Task<T> InvokeOnMainThreadAsync<T>(Func<T> action)
        {
            if (action == null)
            {
                return Task.FromResult(default(T));
            }

            if (Thread.CurrentThread.ManagedThreadId == MainThreadId)
            {
                return Task.FromResult(action());
            }

            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            MainThreadQueue.Enqueue(() =>
            {
                try
                {
                    completion.TrySetResult(action());
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            });
            return completion.Task;
        }

        private static Task WaitForEditorDelayCallAsync()
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ScheduleOnMainThread(() =>
            {
                EditorApplication.delayCall += Complete;
            });

            return completion.Task;

            void Complete()
            {
                completion.TrySetResult(true);
            }
        }

        private static void ProcessMainThreadQueue()
        {
            while (MainThreadQueue.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static Task<AssetDownloadOverwriteMode> PromptOverwriteModeAsync(int existingCount, string characterName, string variantName)
        {
            return InvokeOnMainThreadAsync(() =>
            {
                string message = string.Format(
                    "{0} file(s) already exist for {1} ({2}).\n\nWhat would you like to do?",
                    existingCount,
                    string.IsNullOrWhiteSpace(characterName) ? "this selection" : characterName,
                    string.IsNullOrWhiteSpace(variantName) ? AssetDownloadPathUtility.DefaultVariantName : variantName);
                int result = HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialogComplex(
                    WindowTitle,
                    message,
                    "Overwrite",
                    "Cancel",
                    "Skip Existing");

                switch (result)
                {
                    case 0:
                        return AssetDownloadOverwriteMode.Overwrite;
                    case 2:
                        return AssetDownloadOverwriteMode.SkipExisting;
                    default:
                        return AssetDownloadOverwriteMode.Cancel;
                }
            });
        }

        private static void ClearProgressBarOnMainThread()
        {
            ScheduleOnMainThread(HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar);
        }

        internal void EnsureInitializedForManager()
        {
            if (!hasLoadedOnce)
            {
                hasLoadedOnce = true;
                LoadPrefs();
            }

            EditorApplication.update -= HandleWindowUpdate;
            EditorApplication.update += HandleWindowUpdate;

            if (availableGames.Count <= 0 && !pendingAutoRefresh && !isRefreshingGames && !isDownloading)
            {
                BeginAutomaticRefresh();
            }
        }

        internal IReadOnlyList<string> GetAvailableGameNamesForManager()
        {
            return availableGames.Select(option => option.DisplayName).ToList();
        }

        internal string GetSelectedGameDisplayNameForManager()
        {
            return TryGetSelectedGame(out AssetDownloadGameOption selectedGame)
                ? selectedGame.DisplayName
                : string.Empty;
        }

        internal int GetSelectedGameIndexForManager()
        {
            return NormalizeSelectionIndex(selectedGameIndex, availableGames.Count);
        }

        internal void SetSelectedGameIndexForManager(int index)
        {
            int normalizedIndex = NormalizeSelectionIndex(index, availableGames.Count);
            if (selectedGameIndex == normalizedIndex)
            {
                return;
            }

            selectedGameIndex = normalizedIndex;
            ClearCharacterSelection();
            hsrFbxChoiceByCharacter.Clear();
            EnsureCharactersLoaded();
            Repaint();
        }

        internal string GetCharacterSearchForManager()
        {
            return characterSearch ?? string.Empty;
        }

        internal void SetCharacterSearchForManager(string search)
        {
            string normalizedSearch = search ?? string.Empty;
            if (string.Equals(characterSearch, normalizedSearch, StringComparison.Ordinal))
            {
                return;
            }

            characterSearch = normalizedSearch;
            filteredCharactersDirty = true;
            Repaint();
        }

        internal IReadOnlyList<string> GetFilteredCharactersForManager()
        {
            return TryGetSelectedGame(out _)
                ? GetFilteredCharacters().ToList()
                : Array.Empty<string>();
        }

        internal IReadOnlyList<string> GetSelectedCharacterNamesForManager()
        {
            return GetOrderedSelectedCharacters();
        }

        internal string GetVariantSectionSubtitleForManager()
        {
            return GetVariantSectionSubtitle();
        }

        internal bool IsCharacterSelectedForManager(string characterName)
        {
            return !string.IsNullOrWhiteSpace(characterName)
                && selectedCharacters.Contains(characterName);
        }

        internal void SetCharacterSelectedForManager(string characterName, bool isSelected)
        {
            if (!TryGetSelectedGame(out AssetDownloadGameOption game)
                || string.IsNullOrWhiteSpace(characterName))
            {
                return;
            }

            bool wasMultiSelection = selectedCharacters.Count > 1;
            if (isSelected)
            {
                selectedCharacters.Add(characterName);
                activeCharacterName = characterName;
                EnsureVariantsLoadedForCharacter(game, characterName);
            }
            else
            {
                selectedCharacters.Remove(characterName);
                selectedVariantsByCharacter.Remove(characterName);
                loadingVariantsForCharacters.Remove(characterName);
                if (string.Equals(activeCharacterName, characterName, StringComparison.OrdinalIgnoreCase))
                {
                    activeCharacterName = GetOrderedSelectedCharacters().FirstOrDefault() ?? string.Empty;
                }
            }

            bool isMultiSelection = selectedCharacters.Count > 1;
            if (!wasMultiSelection && isMultiSelection)
            {
                MigrateSharedVariantSelectionToActiveCharacter();
            }
            else if (wasMultiSelection && !isMultiSelection)
            {
                MigratePerCharacterSelectionToSharedSelection();
            }
            else if (selectedCharacters.Count <= 0)
            {
                ClearVariantSelection();
            }

            Repaint();
        }

        internal IReadOnlyList<string> GetVariantNamesForManager(string characterName)
        {
            if (!TryGetSelectedGame(out AssetDownloadGameOption game)
                || string.IsNullOrWhiteSpace(characterName))
            {
                return Array.Empty<string>();
            }

            EnsureVariantsLoadedForCharacter(game, characterName);
            AssetDownloadGameCache cache = GetOrCreateCache(game);
            if (!cache.VariantsByCharacter.TryGetValue(characterName, out List<AssetDownloadVariantOption> currentVariants)
                || currentVariants == null)
            {
                return Array.Empty<string>();
            }

            HashSet<string> selection = GetVariantSelectionForManager(characterName);
            EnsureDefaultVariantSelection(currentVariants, selection);

            if (selectedCharacters.Count <= 1)
            {
                variants.Clear();
                variants.AddRange(currentVariants);
            }

            return currentVariants.Select(option => option.Name).ToList();
        }

        internal bool IsVariantSelectedForManager(string characterName, string variantName)
        {
            return !string.IsNullOrWhiteSpace(variantName)
                && GetVariantSelectionForManager(characterName).Contains(variantName);
        }

        internal void SetVariantSelectedForManager(string characterName, string variantName, bool isSelected)
        {
            if (string.IsNullOrWhiteSpace(variantName))
            {
                return;
            }

            HashSet<string> selection = GetVariantSelectionForManager(characterName);
            if (isSelected)
            {
                selection.Add(variantName);
            }
            else
            {
                selection.Remove(variantName);
            }

            Repaint();
        }

        internal bool GetAutoSetupAfterDownloadForManager()
        {
            return autoSetupAfterDownload;
        }

        internal void SetAutoSetupAfterDownloadForManager(bool value)
        {
            if (autoSetupAfterDownload == value)
            {
                return;
            }

            autoSetupAfterDownload = value;
            SavePrefs();
            Repaint();
        }

        internal bool SupportsAutoSetupForManager()
        {
            return TryGetSelectedGame(out AssetDownloadGameOption selectedGame)
                && SupportsAutoSetup(selectedGame);
        }

        internal bool ShouldShowHsrChoiceForManager()
        {
            return TryGetSelectedGame(out AssetDownloadGameOption selectedGame)
                && AssetDownloadPathUtility.IsHonkaiStarRail(selectedGame.Key, selectedGame.DisplayName);
        }

        internal int GetHsrChoiceIndexForManager()
        {
            return (int)hsrFbxChoice;
        }

        internal void SetHsrChoiceIndexForManager(int index)
        {
            int clampedIndex = Mathf.Clamp(index, 0, 2);
            if ((int)hsrFbxChoice == clampedIndex)
            {
                return;
            }

            hsrFbxChoice = (AssetDownloadHsrFbxChoice)clampedIndex;
            SavePrefs();
            Repaint();
        }

        internal int GetHsrChoiceIndexForCharacterForManager(string characterName)
        {
            return (int)GetResolvedHsrChoiceForCharacter(characterName);
        }

        internal void SetHsrChoiceIndexForCharacterForManager(string characterName, int index)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return;
            }

            int clampedIndex = Mathf.Clamp(index, 0, 2);
            if (hsrFbxChoiceByCharacter.TryGetValue(characterName, out AssetDownloadHsrFbxChoice currentChoice)
                && (int)currentChoice == clampedIndex)
            {
                return;
            }

            hsrFbxChoiceByCharacter[characterName] = (AssetDownloadHsrFbxChoice)clampedIndex;
            Repaint();
        }

        internal bool IsBusyForManager()
        {
            return isRefreshingGames || isDownloading;
        }

        internal bool IsImportingDownloadedAssetsForManager()
        {
            return isImportingDownloadedAssets;
        }

        internal string GetStatusMessageForManager()
        {
            return statusMessage ?? string.Empty;
        }

        internal string GetActionSummaryForManager()
        {
            return BuildActionSummary();
        }

        internal bool CanClearSelectionForManager()
        {
            return CanClearSelection();
        }

        internal void ClearSelectionForManager()
        {
            ClearCurrentSelection();
            Repaint();
        }

        internal bool CanDownloadSelectionForManager()
        {
            return CanDownloadSelection();
        }

        internal void RefreshLibraryForManager()
        {
            StartRefreshGameList();
        }

        internal void StartDownloadForManager()
        {
            StartDownloadSelected();
        }

        internal bool WasManagerDownloadSuccessfulFor(string characterName)
        {
            if (!lastManagerDownloadSucceeded || string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            return lastManagerDownloadedCharacters.Contains(characterName);
        }

        internal IReadOnlyList<string> GetLastManagerDownloadedModelAssetPathsFor(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName)
                || !lastManagerDownloadedModelAssetPathsByCharacter.TryGetValue(characterName, out List<string> paths)
                || paths == null)
            {
                return Array.Empty<string>();
            }

            return paths.ToArray();
        }

        internal bool WasLastManagerDownloadFailed()
        {
            return lastManagerDownloadFailed;
        }

        private HashSet<string> GetVariantSelectionForManager(string characterName)
        {
            return selectedCharacters.Count <= 1
                ? selectedVariants
                : GetOrCreateVariantSelection(characterName);
        }

        private AssetDownloadHsrFbxChoice GetResolvedHsrChoiceForCharacter(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return hsrFbxChoice;
            }

            if (hsrFbxChoiceByCharacter.TryGetValue(characterName, out AssetDownloadHsrFbxChoice choice))
            {
                return choice;
            }

            return hsrFbxChoice;
        }
    }
}
#endif
