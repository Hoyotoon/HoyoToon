#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.API;
using HoyoToon.Editor.ResourceSystem;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.Onboarding;
using StepIds = HoyoToon.Editor.Onboarding.GuidedTourController.StepIds;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal sealed class ModelsDownloadService
    {
        private readonly ModelsModule _module;

        public ModelsDownloadService(ModelsModule module)
        {
            _module = module;
        }

        internal List<VariantOption> GetVariantsForCharacter(GameOption game, string characterName)
        {
            if (game == null || string.IsNullOrEmpty(characterName))
            {
                return null;
            }

            var cache = _module.GetOrCreateCache(game);
            if (cache != null && cache.VariantsByCharacter.TryGetValue(characterName, out var cached) && cached.Count > 0)
            {
                return cached;
            }

            EnsureVariantsLoadedForCharacter(game, characterName);
            return null;
        }

        internal void EnsureVariantsLoadedForCharacter(GameOption game, string characterName)
        {
            if (game == null || string.IsNullOrEmpty(characterName))
            {
                return;
            }

            if (_module._loadingVariantsForCharacters.Contains(characterName))
            {
                return;
            }

            _module._loadingVariantsForCharacters.Add(characterName);

            AsyncUtil.RunFireAndForget(() => LoadVariantsAsync(game, characterName), "Load model variants", ex =>
            {
                ModelsModule.ScheduleOnMainThread(() =>
                {
                    _module._loadingVariantsForCharacters.Remove(characterName);
                    _module._statusMessage = $"Failed to load variants: {ex.Message}";
                });
            });
        }

        internal void StartRefreshGameList()
        {
            if (_module._isRefreshing)
            {
                return;
            }

            _module._isRefreshing = true;
            _module._statusMessage = "Loading games from CDN...";
            _module.ClearSelections();

            AsyncUtil.RunFireAndForget(RefreshGameListAsync, "Refresh model game list", ex =>
            {
                ModelsModule.ScheduleOnMainThread(() =>
                {
                    _module._isRefreshing = false;
                    _module._statusMessage = $"Failed to load games: {ex.Message}";
                });
            });
        }

        private async Task RefreshGameListAsync()
        {
            var apiGames = Api.GetGames().Values
                .Where(game => game != null && !string.IsNullOrEmpty(game.Key))
                .ToList();

            var rootEntries = await CloudreveClient.GetDirectoryEntriesAsync(ModelsModule.ShareUrl, string.Empty);
            var rootFolders = rootEntries.Where(entry => entry.IsDirectory).Select(entry => entry.Name).ToList();

            var options = new List<GameOption>();
            foreach (var game in apiGames)
            {
                if (ModelsPathHelper.TryMatchFolder(rootFolders, game, out var folderName))
                {
                    options.Add(new GameOption(game, folderName));
                }
            }

            options = options.OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

            ModelsModule.ScheduleOnMainThread(() =>
            {
                _module._availableGames.Clear();
                _module._availableGames.AddRange(options);
                _module._isRefreshing = false;
                _module._statusMessage = options.Count > 0 ? null : "No matching games were found in the CDN share.";
                _module._selectedGameIndex = options.Count > 0 ? 0 : -1;
                _module._gameCache.Clear();
                _module.ClearCharacterSelection();
                EnsureCharactersLoaded();
            });
        }

        internal void EnsureCharactersLoaded()
        {
            if (_module._selectedGameIndex < 0 || _module._selectedGameIndex >= _module._availableGames.Count)
            {
                return;
            }

            var game = _module._availableGames[_module._selectedGameIndex];
            var cache = _module.GetOrCreateCache(game);
            if (cache.Characters != null && cache.Characters.Count > 0)
            {
                _module._characters = cache.Characters;
                _module.MarkFilteredCharactersDirty();
                _module._selectedCharacterIndex = _module._characters.Count > 0 ? 0 : -1;
                EnsureVariantsLoaded();
                return;
            }

            if (cache.IsLoadingCharacters)
            {
                return;
            }

            cache.IsLoadingCharacters = true;
            _module._statusMessage = "Loading characters...";

            AsyncUtil.RunFireAndForget(() => LoadCharactersAsync(game), "Load model characters", ex =>
            {
                ModelsModule.ScheduleOnMainThread(() =>
                {
                    cache.IsLoadingCharacters = false;
                    _module._statusMessage = $"Failed to load characters: {ex.Message}";
                });
            });
        }

        private async Task LoadCharactersAsync(GameOption game)
        {
            var cache = _module.GetOrCreateCache(game);
            var gameFolder = game.FolderName;

            var gameEntries = await CloudreveClient.GetDirectoryEntriesAsync(ModelsModule.ShareUrl, gameFolder);
            var charactersFolder = ModelsPathHelper.FindFolderName(gameEntries, ModelsModule.CharactersFolderName) ?? ModelsModule.CharactersFolderName;

            var characterPath = ModelsPathHelper.CombineSharePath(gameFolder, charactersFolder);
            var entries = await CloudreveClient.GetDirectoryEntriesAsync(ModelsModule.ShareUrl, characterPath);
            var characters = entries.Where(entry => entry.IsDirectory)
                .Select(entry => entry.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ModelsModule.ScheduleOnMainThread(() =>
            {
                cache.CharactersFolderName = charactersFolder;
                cache.Characters = characters;
                cache.IsLoadingCharacters = false;

                if (!_module.IsSelectedGame(game.Key))
                {
                    return;
                }

                _module._characters = characters;
                _module.MarkFilteredCharactersDirty();
                _module._selectedCharacterIndex = characters.Count > 0 ? 0 : -1;
                _module._statusMessage = characters.Count > 0 ? null : "No characters were found for this game.";
                EnsureVariantsLoaded();
            });
        }

        internal void EnsureVariantsLoaded()
        {
            if (_module._selectedGameIndex < 0 || _module._selectedGameIndex >= _module._availableGames.Count)
            {
                return;
            }

            if (_module._selectedCharacterIndex < 0 || _module._selectedCharacterIndex >= _module._characters.Count)
            {
                return;
            }

            var game = _module._availableGames[_module._selectedGameIndex];
            var cache = _module.GetOrCreateCache(game);
            var characterName = _module._characters[_module._selectedCharacterIndex];

            if (cache.VariantsByCharacter.TryGetValue(characterName, out var cachedVariants) && cachedVariants.Count > 0)
            {
                _module._variants = cachedVariants;
                _module._selectedVariantIndex = _module._variants.Count > 0 ? 0 : -1;
                return;
            }

            if (cache.IsLoadingVariants)
            {
                return;
            }

            cache.IsLoadingVariants = true;
            _module._statusMessage = "Loading variants...";

            AsyncUtil.RunFireAndForget(() => LoadVariantsAsync(game, characterName), "Load model variants", ex =>
            {
                ModelsModule.ScheduleOnMainThread(() =>
                {
                    cache.IsLoadingVariants = false;
                    _module._statusMessage = $"Failed to load variants: {ex.Message}";
                });
            });
        }

        private async Task LoadVariantsAsync(GameOption game, string characterName)
        {
            var cache = _module.GetOrCreateCache(game);
            var gameFolder = game.FolderName;
            var charactersFolder = cache.CharactersFolderName ?? ModelsModule.CharactersFolderName;

            var characterPath = ModelsPathHelper.CombineSharePath(gameFolder, charactersFolder, characterName);
            var entries = await CloudreveClient.GetDirectoryEntriesAsync(ModelsModule.ShareUrl, characterPath);
            var variants = entries.Where(entry => entry.IsDirectory)
                .Select(entry => new VariantOption(entry.Name, ModelsPathHelper.CombineSharePath(characterPath, entry.Name)))
                .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool hasFiles = entries.Any(entry => !entry.IsDirectory);
            if (hasFiles || variants.Count == 0)
            {
                variants.Insert(0, new VariantOption(ModelsModule.DefaultVariantName, characterPath));
            }

            ModelsModule.ScheduleOnMainThread(() =>
            {
                cache.IsLoadingVariants = false;
                cache.VariantsByCharacter[characterName] = variants;

                if (!_module.IsSelectedGame(game.Key) || !_module.IsSelectedCharacter(characterName))
                {
                    return;
                }

                _module._variants = variants;
                _module._selectedVariantIndex = _module._variants.Count > 0 ? 0 : -1;
                _module._statusMessage = _module._variants.Count > 0 ? null : "No variants were found for this character.";
            });
        }

        internal void StartDownloadSelected(HoyoToonManager manager, CancellationToken cancellationToken = default)
        {
            if (!_module.TryGetBatchSelection(out var game, out var characters, out var variants))
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

                AsyncUtil.RunFireAndForget(
                    () => DownloadSelectedAsync(manager, game, characterName, variant, true, true, true, _module._hsrFbxChoice, cancellationToken),
                    "Download model",
                    ex => ModelsModule.ScheduleOnMainThread(() => { _module._statusMessage = $"Download failed: {ex.Message}"; }));
                return;
            }

            StartBatchDownload(game, characters, variants, cancellationToken);
        }

        private async Task<string> DownloadSelectedAsync(
            HoyoToonManager manager,
            GameOption game,
            string characterName,
            VariantOption variant,
            bool showProgress,
            bool showPostDownloadPrompt,
            bool refreshAssets,
            HsrFbxChoice fbxChoice,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var assetRoot = ModelsPathHelper.EnsureAssetsRoot(_module._downloadRoot);
            if (string.IsNullOrEmpty(assetRoot))
            {
                ModelsModule.ScheduleOnMainThread(() =>
                {
                    DialogWindow.ShowError("Invalid Folder", "Please select a download folder inside Assets.");
                });
                return null;
            }

            var assetTarget = ModelsPathHelper.BuildAssetTargetPath(assetRoot, game, characterName, variant);
            var absoluteTarget = ModelsPathHelper.AbsoluteFromAssetsPath(assetTarget);
            Directory.CreateDirectory(absoluteTarget);

            Action<float, string> updateProgress = null;
            Action<string> endProgress = null;

            if (showProgress)
            {
                ProgressDialog.Start("Downloading Model", "Preparing download...");
                updateProgress = (progress, message) => ProgressDialog.Update(progress, message);
                endProgress = message => ProgressDialog.End(message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var remoteFiles = await CloudreveClient.GetFileListAsync(ModelsModule.ShareUrl, variant.RelativePath, true);
            var files = remoteFiles.Where(file => !file.IsDirectory).ToList();

            if (ModelsPathHelper.IsHonkaiStarRail(game.Key, game.DisplayName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rootEntries = await CloudreveClient.GetFileListAsync(ModelsModule.ShareUrl, variant.RelativePath, false);
                var rootFbxFiles = rootEntries
                    .Where(file => !file.IsDirectory && file.RelativePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (rootFbxFiles.Count > 0)
                {
                    var orderedFbxFiles = rootFbxFiles
                        .OrderBy(file => Path.GetFileNameWithoutExtension(file.RelativePath) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var chosen = ModelsPathHelper.FilterFbxFilesByChoice(orderedFbxFiles, fbxChoice);
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
                cancellationToken.ThrowIfCancellationRequested();
                var relative = ModelsPathHelper.TrimPrefix(file.RelativePath, variant.RelativePath);
                var safeRelative = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                var localPath = Path.Combine(absoluteTarget, safeRelative);
                if (File.Exists(localPath))
                {
                    existingFiles.Add(localPath);
                }
            }

            if (existingFiles.Count > 0)
            {
                var overwrite = await PromptOverwriteAsync(existingCount: existingFiles.Count, characterName, variant.Name);
                if (!overwrite.HasValue)
                {
                    endProgress?.Invoke("Download cancelled.");
                    return null;
                }

                if (overwrite.Value == false)
                {
                    files = files.Where(file =>
                    {
                        var relative = ModelsPathHelper.TrimPrefix(file.RelativePath, variant.RelativePath);
                        var safeRelative = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                        var localPath = Path.Combine(absoluteTarget, safeRelative);
                        return !File.Exists(localPath);
                    }).ToList();
                }
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = ModelsPathHelper.TrimPrefix(file.RelativePath, variant.RelativePath);
                var safeRelative = relative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                var localPath = Path.Combine(absoluteTarget, safeRelative);

                var directory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await CloudreveClient.DownloadFileAsync(CloudreveClient.SharedClient, file, ModelsModule.ShareUrl, localPath);

                completed++;
                var progress = total > 0 ? (float)completed / total : 1f;
                updateProgress?.Invoke(progress, $"Downloading {Path.GetFileName(localPath)} ({completed}/{total})");
            }

            endProgress?.Invoke("Download complete!");

            if (refreshAssets || showPostDownloadPrompt)
            {
                ModelsModule.ScheduleOnMainThread(() =>
                {
                    if (refreshAssets)
                    {
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }

                    if (showPostDownloadPrompt)
                    {
                        ShowPostDownloadPrompt(manager, assetTarget, game.Key, game.DisplayName, characterName, variant.Name);
                    }

                    GuidedTourController.NotifyModelDownloaded();
                });
            }

            return assetTarget;
        }

        private void StartBatchDownload(GameOption game, List<string> characters, List<string> variants, CancellationToken cancellationToken = default)
        {
            if (game == null || characters == null || variants == null || characters.Count == 0 || variants.Count == 0)
            {
                return;
            }

            if (ModelsPathHelper.IsHonkaiStarRail(game.Key, game.DisplayName) && variants.Count > 1)
            {
                PromptBatchVariantChoice(variants, selectedVariant =>
                {
                    if (string.IsNullOrEmpty(selectedVariant))
                    {
                        return;
                    }

                    AsyncUtil.RunFireAndForget(
                        () => DownloadBatchAsync(game, BuildBatchVariantMap(characters, new List<string> { selectedVariant }), cancellationToken),
                        "Batch download models",
                        ex => ModelsModule.ScheduleOnMainThread(() => { _module._statusMessage = $"Batch download failed: {ex.Message}"; }));
                });
                return;
            }

            AsyncUtil.RunFireAndForget(
                () => DownloadBatchAsync(game, BuildBatchVariantMap(characters, variants), cancellationToken),
                "Batch download models",
                ex => ModelsModule.ScheduleOnMainThread(() => { _module._statusMessage = $"Batch download failed: {ex.Message}"; }));
        }

        private async Task DownloadBatchAsync(GameOption game, Dictionary<string, List<string>> characterVariants, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var assetRoot = ModelsPathHelper.EnsureAssetsRoot(_module._downloadRoot);
            if (string.IsNullOrEmpty(assetRoot))
            {
                ModelsModule.ScheduleOnMainThread(() =>
                {
                    DialogWindow.ShowError("Invalid Folder", "Please select a download folder inside Assets.");
                });
                return;
            }

            if (game == null || characterVariants == null || characterVariants.Count == 0)
            {
                return;
            }

            var manager = UnityEngine.Object.FindFirstObjectByType<HoyoToonManager>(FindObjectsInactive.Include);
            var cache = _module.GetOrCreateCache(game);
            var resolved = new List<(string characterName, VariantOption variant)>();
            foreach (var entry in characterVariants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var characterName = entry.Key;
                var variantNames = entry.Value ?? new List<string>();
                foreach (var variantName in variantNames)
                {
                    var variant = await ResolveVariantForCharacterAsync(game, cache, characterName, variantName, cancellationToken);
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
                ProgressDialog.Start("Downloading Models", "Preparing batch download...");
            }

            var gate = new SemaphoreSlim(ModelsModule.MaxParallelDownloads, ModelsModule.MaxParallelDownloads);
            var tasks = new List<Task>();
            foreach (var job in resolved)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var assetTarget = await DownloadSelectedAsync(manager, game, job.characterName, job.variant, false, false, false, _module._hsrFbxChoice, cancellationToken);
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
                            ModelsModule.ScheduleOnMainThread(() =>
                            {
                                float progress = total > 0 ? (float)done / total : 1f;
                                ProgressDialog.Update(progress, $"Downloading {job.characterName} ({done}/{total})");
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
                    ProgressDialog.End("Batch download complete!");
                    GuidedTourController.NotifyModelDownloaded();
                }
            }

            ModelsModule.ScheduleOnMainThread(() =>
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (_module._autoSetupAfterDownload)
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

            bool usePerCharacter = _module._selectedCharacters.Count > 1 && _module._selectedVariantsByCharacter.Count > 0;
            foreach (var character in characters)
            {
                if (string.IsNullOrWhiteSpace(character))
                {
                    continue;
                }

                if (usePerCharacter && _module._selectedVariantsByCharacter.TryGetValue(character, out var selected) && selected.Count > 0)
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

        private async Task<VariantOption> ResolveVariantForCharacterAsync(GameOption game, GameFolderCache cache, string characterName, string variantName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (cache != null && cache.VariantsByCharacter.TryGetValue(characterName, out var cached) && cached.Count > 0)
            {
                return PickVariant(cached, variantName);
            }

            var gameFolder = game.FolderName;
            var charactersFolder = cache?.CharactersFolderName ?? ModelsModule.CharactersFolderName;
            var characterPath = ModelsPathHelper.CombineSharePath(gameFolder, charactersFolder, characterName);
            cancellationToken.ThrowIfCancellationRequested();
            var entries = await CloudreveClient.GetDirectoryEntriesAsync(ModelsModule.ShareUrl, characterPath);
            var variants = entries.Where(entry => entry.IsDirectory)
                .Select(entry => new VariantOption(entry.Name, ModelsPathHelper.CombineSharePath(characterPath, entry.Name)))
                .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool hasFiles = entries.Any(entry => !entry.IsDirectory);
            if (hasFiles || variants.Count == 0)
            {
                variants.Insert(0, new VariantOption(ModelsModule.DefaultVariantName, characterPath));
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

            var cache = _module.GetOrCreateCache(game);
            if (cache.VariantsByCharacter.TryGetValue(characterName, out var cached) && cached.Count > 0)
            {
                return PickVariant(cached, variantName);
            }

            if (_module.IsSelectedCharacter(characterName) && _module._variants != null && _module._variants.Count > 0)
            {
                return PickVariant(_module._variants, variantName);
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

            DialogWindow.ShowCustom(
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

            return variants.FirstOrDefault(option => string.Equals(option.Name, ModelsModule.DefaultVariantName, StringComparison.OrdinalIgnoreCase))
                   ?? variants[0];
        }

        private void ShowPostDownloadPrompt(HoyoToonManager manager, string assetTarget, string gameKey, string gameName, string characterName, string variantName)
        {
            if (_module._autoSetupAfterDownload)
            {
                TryAutoSetupDownloaded(manager, assetTarget, gameKey, gameName, characterName, variantName);
            }
        }

        private void TryAutoSetupDownloaded(HoyoToonManager manager, string assetFolder, string gameKey, string gameName, string characterName, string variantName)
        {
            if (manager == null)
            {
                DialogWindow.ShowError("Manager Missing", "Cannot setup without an active HoyoToon Manager in the scene.");
                return;
            }

            ResolvePrimaryAssetWithChoice(assetFolder, gameKey, gameName, characterName, variantName, _module._hsrFbxChoice, (assetPath, isFbx, isPrefab) =>
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    DialogWindow.ShowError("No Model Found", "Could not find an FBX in the downloaded folder.");
                    return;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null)
                {
                    DialogWindow.ShowError("Missing Asset", "Could not load the downloaded model asset.");
                    return;
                }

                if (isFbx)
                {
                    if (ModelSetupUtility.TryPrepareFbxRequest(manager, asset, null, ModelSetupUtility.SetupRequestSource.DownloadAutoSetup, out var preparedRequest)
                        && ModelSetupUtility.TryRunPreparedRequest(preparedRequest, out var instance, out _))
                    {
                        if (instance != null)
                        {
                            RegisterAndSelect(manager, instance);
                            GuidedTourController.NotifyAutoSetupCompleted(instance);
                        }
                        else
                        {
                            DialogWindow.ShowWarning("Auto Setup Complete", "No setup steps were applicable for this model. Try Instantiate to add it to the scene.");
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

            if (!GuidedTourController.IsActive
                || !string.Equals(GuidedTourController.CurrentStep.id, StepIds.AutoSetup, StringComparison.OrdinalIgnoreCase))
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

            bool isStarRail = ModelsPathHelper.IsHonkaiStarRail(gameKey, gameName);
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
                fbxs = ModelsPathHelper.FilterLocalFbxByChoice(fbxs, fbxChoice);
            }

            string chosen = fbxs.Count > 1
                ? ModelsPathHelper.SelectBestCandidate(fbxs, characterName, variantName)
                : fbxs[0];

            if (!ModelsPathHelper.TryConvertToAssetsPath(chosen, out var assetsPath))
            {
                onResolved(null, false, false);
                return;
            }

            onResolved(assetsPath, true, false);
        }

        private static bool TryGatherAssetCandidates(string assetFolder, string characterName, string variantName, bool rootOnly, out List<string> prefabs, out List<string> fbxs)
        {
            prefabs = new List<string>();
            fbxs = new List<string>();

            var absoluteFolder = ModelsPathHelper.AbsoluteFromAssetsPath(assetFolder);
            if (string.IsNullOrEmpty(absoluteFolder) || !Directory.Exists(absoluteFolder))
            {
                return false;
            }

            var searchOption = rootOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
            fbxs = Directory.GetFiles(absoluteFolder, "*.fbx", searchOption).ToList();
            return fbxs.Count > 0;
        }

        private Task<bool?> PromptOverwriteAsync(int existingCount, string characterName, string variantName)
        {
            var tcs = new TaskCompletionSource<bool?>();
            var message = $"{existingCount} files already exist for {characterName} ({variantName}).\n\nWhat would you like to do?";
            var buttons = new[] { "Overwrite", "Skip Existing", "Cancel" };

            ModelsModule.ScheduleOnMainThread(() =>
            {
                DialogWindow.ShowCustom("Files Already Exist", message, MessageType.Warning, buttons, 0, 2, result =>
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
    }
}
#endif
