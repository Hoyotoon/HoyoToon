#if UNITY_EDITOR
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.UI.ResourcesUI;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.ResourceSystem
{
    internal static class ResourceDownloadService
    {
        // Cache for remote file lists keyed by gameKey, populated during planning to avoid re-fetching
        private static readonly Dictionary<string, (DateTime fetchTime, List<RemoteFileInfo> files)> _remoteFileListCache
            = new Dictionary<string, (DateTime, List<RemoteFileInfo>)>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

        private static void CacheRemoteFileList(string gameKey, List<RemoteFileInfo> files)
        {
            _remoteFileListCache[gameKey] = (DateTime.UtcNow, files);
        }

        private static List<RemoteFileInfo> GetCachedRemoteFileList(string gameKey)
        {
            if (_remoteFileListCache.TryGetValue(gameKey, out var cached)
                && (DateTime.UtcNow - cached.fetchTime) < _cacheExpiry)
            {
                return cached.files;
            }
            _remoteFileListCache.Remove(gameKey);
            return null;
        }

        internal static async Task DownloadMissingResourcesAndSynchronizeAsync(List<string> missingGames, Dictionary<string, FileUpdateInfo> updateInfoMap, CancellationToken cancellationToken = default)
        {
            await ResourceOperationRunner.ExecuteWithProgressAsync(
                progressTitle: "Synchronizing Resources",
                progressMessage: "Preparing synchronization...",
                failureTitle: "Synchronization Failed",
                failureMessageFactory: ex => $"Failed to synchronize resources: {ex.Message}",
                failureLogMessage: "Failed to synchronize resources",
                cancellationToken: cancellationToken,
                operation: async progress =>
                {
                    progress.Token.ThrowIfCancellationRequested();

                    if (missingGames.Any())
                    {
                        await DownloadResourcesCoreAsync(missingGames.ToArray(), false, progress, showDialogs: false, progressCompletionMessage: null);
                    }

                    if (updateInfoMap.Any())
                    {
                        await SynchronizeFilesForMultipleGamesInternalAsync(updateInfoMap, cancellationToken: progress.Token);
                    }

                    var totalChanges = updateInfoMap.Values.Sum(info => info.TotalChanges);
                    var totalDeleted = updateInfoMap.Values.Sum(info => info.DeletedFiles.Count);
                    DialogWindow.ShowInfo(
                        "Synchronization Complete",
                        $"Successfully downloaded {missingGames.Count} games, updated {totalChanges - totalDeleted} files, and removed {totalDeleted} deleted files!");
                });
        }

        internal static async Task SynchronizeFilesForMultipleGamesAsync(Dictionary<string, FileUpdateInfo> updateInfoMap, CancellationToken cancellationToken = default)
        {
            var totalChanges = updateInfoMap.Values.Sum(info => info.TotalChanges);

            await ResourceOperationRunner.ExecuteWithProgressAsync(
                progressTitle: "Synchronizing Files",
                progressMessage: $"Processing {totalChanges} file changes...",
                cancelledDialogMessage: "Resource synchronization cancelled.",
                failureLogMessage: "Failed to synchronize files",
                rethrowOnFailure: true,
                cancellationToken: cancellationToken,
                operation: async progress =>
                {
                    progress.Token.ThrowIfCancellationRequested();
                    var shared = new SharedOperationProgress(totalChanges);
                    await SynchronizeFilesForMultipleGamesInternalAsync(updateInfoMap, shared, progress.Token);

                    var totalUpdated = updateInfoMap.Values.Sum(info => info.TotalFiles);
                    var totalDeleted = updateInfoMap.Values.Sum(info => info.DeletedFiles.Count);
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Synchronization complete: updated {totalUpdated} files, removed {totalDeleted} deleted files");
                });
        }

        private static async Task SynchronizeFilesForMultipleGamesInternalAsync(Dictionary<string, FileUpdateInfo> updateInfoMap, SharedOperationProgress shared = null, CancellationToken cancellationToken = default)
        {
            foreach (var kvp in updateInfoMap)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var gameKey = kvp.Key;
                var updateInfo = kvp.Value;
                if (updateInfo.HasUpdates)
                {
                    var allFilesToUpdate = updateInfo.MissingFiles.Concat(updateInfo.OutdatedFiles).ToList();
                    if (allFilesToUpdate.Count > 0)
                    {
                        await DownloadSpecificFilesForGameAsync(gameKey, allFilesToUpdate, shared, cancellationToken);
                    }
                }
                if (updateInfo.HasDeletions)
                {
                    DeleteSpecificFilesForGame(gameKey, updateInfo.DeletedFiles, shared, cancellationToken);
                }
            }
        }

        private static void DeleteSpecificFilesForGame(string gameKey, List<string> filePaths, SharedOperationProgress shared = null, CancellationToken cancellationToken = default)
        {
            if (filePaths == null || !filePaths.Any())
                return;

            var gameConfig = ResourceConfig.Games[gameKey];
            var cacheData = ResourceCacheService.GetCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Deleting {filePaths.Count} files for {gameConfig.DisplayName}");
            int deletedCount = 0;
            bool useLocalProgress = shared == null;
            using var localProgress = useLocalProgress
                ? ResourceOperationRunner.CreateProgress("Deleting Files", $"Deleting 0/{filePaths.Count} files...", cancellationToken)
                : null;
            var operationToken = localProgress?.Token ?? cancellationToken;
            foreach (var filePath in filePaths)
            {
                operationToken.ThrowIfCancellationRequested();
                if (gameData.Files.TryGetValue(filePath, out var cachedFile))
                {
                    try
                    {
                        if (File.Exists(cachedFile.LocalPath))
                        {
                            File.Delete(cachedFile.LocalPath);
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Deleted local file: {cachedFile.LocalPath}");
                        }
                        gameData.Files.Remove(filePath);
                        deletedCount++;
                        if (useLocalProgress)
                        {
                            localProgress?.Update((float)deletedCount / filePaths.Count, $"Deleted {deletedCount}/{filePaths.Count} files...");
                        }
                        else
                        {
                            shared.Increment();
                            ProgressDialog.Update(shared.Progress, $"Processed {shared.Completed}/{shared.Total} changes...");
                        }
                    }
                    catch (Exception ex)
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Warning, $"Failed to delete file {cachedFile.LocalPath}: {ex.Message}");
                    }
                }
            }
            try
            {
                var localBasePath = ResourcePathUtility.GetLocalBasePath(gameConfig);
                CleanupEmptyDirectories(localBasePath);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Warning, $"Failed to cleanup empty directories: {ex.Message}");
            }
            gameData.LastSync = DateTime.UtcNow;
            ResourceCacheService.SaveCacheData();
            AssetDatabase.Refresh();
            localProgress?.Complete();
        }

        private static void CleanupEmptyDirectories(string startPath)
        {
            if (!Directory.Exists(startPath))
                return;
            foreach (var directory in Directory.GetDirectories(startPath))
            {
                CleanupEmptyDirectories(directory);
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Cleaned up empty directory: {directory}");
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Warning, $"Failed to delete empty directory {directory}: {ex.Message}");
                }
            }
        }

        internal static async Task DownloadResourcesAsync(string[] gameKeys, bool forceDownload = false, CancellationToken cancellationToken = default)
        {
            if (gameKeys == null || gameKeys.Length == 0)
            {
                return;
            }

            await ResourceOperationRunner.ExecuteWithProgressAsync(
                progressTitle: "Downloading Resources",
                progressMessage: "Preparing download plan...",
                cancelledDialogMessage: "Resource download cancelled.",
                failureTitle: "Download Failed",
                failureMessageFactory: ex => $"Resource download failed: {ex.Message}",
                failureLogMessage: "Resource download failed",
                cancellationToken: cancellationToken,
                operation: progress => DownloadResourcesCoreAsync(gameKeys, forceDownload, progress));
        }

        private static async Task DownloadResourcesCoreAsync(
            string[] gameKeys,
            bool forceDownload,
            ResourceOperationProgress progress,
            bool showDialogs = true,
            string progressCompletionMessage = null)
        {
            var operationToken = progress.Token;
            var validGameKeys = ResolveCanonicalGameKeys(gameKeys);
            if (validGameKeys.Length == 0)
            {
                if (showDialogs)
                {
                    DialogWindow.ShowError("Error", "No valid game keys specified for download.");
                }

                return;
            }

            var cacheData = ResourceCacheService.GetCacheData();
            var globalProgress = new GlobalDownloadProgress
            {
                TotalGames = validGameKeys.Length,
                CompletedGames = 0,
                GameProgresses = new Dictionary<string, DownloadProgress>()
            };

            foreach (var gameKey in validGameKeys)
            {
                globalProgress.GameProgresses[gameKey] = new DownloadProgress
                {
                    GameKey = gameKey,
                    StatusMessage = "Planning...",
                    TotalFiles = 0,
                    FilesCompleted = 0
                };
            }

            var precompute = await PrecomputeDownloadPlanAsync(validGameKeys, forceDownload, operationToken);
            foreach (var gameKey in validGameKeys)
            {
                operationToken.ThrowIfCancellationRequested();
                var perGameProgress = globalProgress.GameProgresses[gameKey];
                if (precompute.Plan.TryGetValue(gameKey, out var plannedFiles))
                {
                    perGameProgress.TotalFiles = plannedFiles.Count;
                    perGameProgress.StatusMessage = plannedFiles.Count > 0 ? "Ready" : "Up to date";
                }
                else if (precompute.Errors.TryGetValue(gameKey, out var planningError))
                {
                    perGameProgress.HasErrors = true;
                    perGameProgress.StatusMessage = $"Planning failed: {planningError.Message}";
                    perGameProgress.TotalFiles = 0;
                }
            }

            var grandTotalFiles = globalProgress.GetTotalFiles();
            if (grandTotalFiles == 0 && precompute.Errors.Count == 0)
            {
                var affectedGames = validGameKeys
                    .Where(gameKey =>
                    {
                        var hasPlannedFiles = precompute.Plan.TryGetValue(gameKey, out var plannedFiles) && plannedFiles.Count > 0;
                        return !hasPlannedFiles && !ResourceValidation.HasResourcesForGame(gameKey);
                    })
                    .Select(gameKey => ResourceConfig.Games[gameKey].DisplayName)
                    .Distinct()
                    .ToArray();

                if (affectedGames.Length > 0)
                {
                    var message =
                        "No downloadable files were discovered for the selected game(s), but local resources are missing.\n\n" +
                        "Affected games: " + string.Join(", ", affectedGames) + "\n\n" +
                        "This usually indicates a resource configuration or server listing issue.";
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, message);
                    if (showDialogs)
                    {
                        DialogWindow.ShowError("Download Planning Failed", message);
                    }

                    return;
                }

                globalProgress.IsCompleted = true;
                cacheData.MarkUpdateCheckCompleted();
                ResourceCacheService.SaveCacheData();
                progress.Complete(progressCompletionMessage ?? "All resources are already up to date.");
                if (showDialogs)
                {
                    DialogWindow.ShowInfo("Nothing to Download", "All selected games are up to date.");
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return;
            }

            var progressTask = MonitorProgressAsync(globalProgress, progress, operationToken);
            using var semaphore = new SemaphoreSlim(3, 3);
            var downloadTasks = validGameKeys.Select(async gameKey =>
            {
                await semaphore.WaitAsync(operationToken);
                try
                {
                    operationToken.ThrowIfCancellationRequested();
                    if (precompute.Errors.TryGetValue(gameKey, out var planningError))
                    {
                        throw planningError;
                    }

                    var plannedFiles = precompute.Plan.TryGetValue(gameKey, out var fileList) ? fileList : new List<RemoteFileInfo>();
                    await DownloadGameResourcesWithProgressAsync(gameKey, globalProgress.GameProgresses[gameKey], forceDownload, plannedFiles, operationToken);
                    return new { GameKey = gameKey, Success = true, Error = (Exception)null };
                }
                catch (Exception ex)
                {
                    return new { GameKey = gameKey, Success = false, Error = ex };
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            var results = await Task.WhenAll(downloadTasks);
            globalProgress.IsCompleted = true;
            await progressTask;
            cacheData.MarkUpdateCheckCompleted();
            ResourceCacheService.SaveCacheData();

            var successfulGames = results.Where(result => result.Success).ToArray();
            var failedGames = results.Where(result => !result.Success).ToArray();
            if (!showDialogs)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return;
            }

            if (failedGames.Length == 0)
            {
                string successMessage = validGameKeys.Length == 1
                    ? $"Successfully downloaded resources for {ResourceConfig.Games[validGameKeys[0]].DisplayName}!"
                    : $"Successfully downloaded resources for all {validGameKeys.Length} games!";
                DialogWindow.ShowInfo("Download Complete", successMessage);
            }
            else if (successfulGames.Length > 0)
            {
                var successfulGameNames = successfulGames.Select(result => ResourceConfig.Games[result.GameKey].DisplayName).ToArray();
                var failedGameNames = failedGames.Select(result => ResourceConfig.Games[result.GameKey].DisplayName + ": " + result.Error.Message).ToArray();
                var partialMessage = "Downloaded " + successfulGames.Length + "/" + validGameKeys.Length + " games successfully:\n" +
                                     "OK: " + string.Join(", ", successfulGameNames) + "\n\n" +
                                     "Failed:\n- " + string.Join("\n- ", failedGameNames);
                DialogWindow.ShowInfo("Partial Success", partialMessage);
            }
            else
            {
                var failedGameDetails = failedGames.Select(result => ResourceConfig.Games[result.GameKey].DisplayName + ": " + result.Error.Message).ToArray();
                var failureMessage = "All downloads failed:\n- " + string.Join("\n- ", failedGameDetails);
                DialogWindow.ShowError("Download Failed", failureMessage);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static async Task MonitorProgressAsync(GlobalDownloadProgress globalProgress, ResourceOperationProgress progress, CancellationToken cancellationToken = default)
        {
            while (!globalProgress.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                globalProgress.UpdateCompletedGames();
                var overallProgress = globalProgress.CalculateOverallProgress();
                var statusMessage = globalProgress.GetProgressSummary();
                progress.Update(overallProgress, statusMessage);
                await Task.Delay(100, cancellationToken);
            }
        }

        private static async Task DownloadGameResourcesWithProgressAsync(string gameKey, DownloadProgress progress, bool forceDownload = false, List<RemoteFileInfo> plannedFiles = null, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress.StatusMessage = plannedFiles == null ? $"Scanning {ResourceConfig.Games[gameKey].DisplayName} files..." : $"Downloading {ResourceConfig.Games[gameKey].DisplayName}...";
                await DownloadGameResourcesAsync(gameKey, progress, forceDownload, plannedFiles, cancellationToken);
                progress.StatusMessage = $"{ResourceConfig.Games[gameKey].DisplayName} completed!";
            }
            catch (Exception ex)
            {
                progress.StatusMessage = $"{ResourceConfig.Games[gameKey].DisplayName} failed: {ex.Message}";
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to download {ResourceConfig.Games[gameKey].DisplayName}: {ex.Message}");
                throw;
            }
        }

        private static async Task DownloadGameResourcesAsync(string gameKey, DownloadProgress progress, bool forceDownload = false, List<RemoteFileInfo> plannedFiles = null, CancellationToken cancellationToken = default)
        {
            var gameConfig = ResourceConfig.Games[gameKey];
            var cacheData = ResourceCacheService.GetCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            gameData.WebdavUrl = gameConfig.WebdavUrl;
            if (string.IsNullOrEmpty(gameConfig.WebdavUrl))
            {
                throw new InvalidOperationException($"No WebDAV URL configured for {gameConfig.DisplayName}");
            }
            if (string.IsNullOrEmpty(gameConfig.LocalPath))
            {
                throw new InvalidOperationException($"No LocalPath configured for {gameConfig.DisplayName}");
            }
            try
            {
                List<RemoteFileInfo> remoteFiles;
                if (plannedFiles != null)
                {
                    remoteFiles = plannedFiles.Where(ResourcePathUtility.IsDownloadableFile).ToList();
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress.StatusMessage = $"Connecting to {gameConfig.DisplayName} server...";
                    var discovered = await CloudreveClient.GetFileListAsync(gameConfig.WebdavUrl, cancellationToken: cancellationToken);
                    remoteFiles = discovered.Where(ResourcePathUtility.IsDownloadableFile).ToList();
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Found {remoteFiles.Count} files for {gameConfig.DisplayName} (all file types)");
                    if (progress.TotalFiles == 0) progress.TotalFiles = remoteFiles.Count;
                }
                if (!forceDownload)
                {
                    var newFiles = remoteFiles.Where(f => !gameData.Files.ContainsKey(f.RelativePath)).ToList();
                    var updatedFiles = remoteFiles.Where(f =>
                        gameData.Files.TryGetValue(f.RelativePath, out var cached) &&
                        !string.IsNullOrEmpty(f.ETag) &&
                        cached.RemoteEtag != f.ETag).ToList();
                    if (newFiles.Any()) HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Found {newFiles.Count} new files for {gameConfig.DisplayName}");
                    if (updatedFiles.Any()) HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Found {updatedFiles.Count} updated files for {gameConfig.DisplayName}");
                }
                else
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Force downloading all {remoteFiles.Count} files for {gameConfig.DisplayName}");
                }
                progress.StatusMessage = $"Starting {gameConfig.DisplayName} downloads...";
                var localBasePath = ResourcePathUtility.GetLocalBasePath(gameConfig);
                Directory.CreateDirectory(localBasePath);
                var httpClient = CloudreveClient.SharedClient;
                foreach (var remoteFile in remoteFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                        if (ResourcePathUtility.IsThumbMarkerFile(remoteFile.RelativePath))
                        {
                            progress.FilesCompleted++;
                            continue;
                        }
                        progress.CurrentFile = remoteFile.RelativePath;
                        progress.StatusMessage = $"Downloading {gameConfig.DisplayName} files...";
                        var cleanRelativePath = remoteFile.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                        var localPath = Path.Combine(localBasePath, cleanRelativePath);
                        var localDir = Path.GetDirectoryName(localPath);
                        if (!Directory.Exists(localDir))
                        {
                            Directory.CreateDirectory(localDir);
                        }
                        var cachedFile = gameData.GetFileInfo(remoteFile.RelativePath);
                        bool shouldDownload = plannedFiles != null
                            || ShouldDownloadFile(cachedFile, remoteFile, forceDownload, localBasePath);
                        if (shouldDownload)
                        {
                            if (remoteFile.IsDirectory || localPath.EndsWith("\\") || localPath.EndsWith("/"))
                            {
                                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Skipping directory: {remoteFile.RelativePath}");
                                progress.FilesCompleted++;
                                continue;
                            }
                            await CloudreveClient.DownloadFileAsync(httpClient, remoteFile, gameConfig.WebdavUrl, localPath, cancellationToken);
                            var fileInfo = new FileInfo(localPath);
                            var cachedFileInfo = CreateCachedFileInfo(remoteFile, localPath, fileInfo);
                            cachedFileInfo.CalculateChecksum();
                            gameData.UpdateFileInfo(remoteFile.RelativePath, cachedFileInfo);
                            gameData.TotalDownloaded += fileInfo.Length;
                        }
                        progress.FilesCompleted++;
                }
                gameData.LastSync = DateTime.UtcNow;
                gameData.DownloadErrors.Clear();
                ResourceCacheService.SaveCacheData();
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Successfully downloaded {gameConfig.DisplayName} resources ({remoteFiles.Count} files).");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to download {gameConfig.DisplayName} resources: {ex.Message}");
                gameData.DownloadErrors.Add($"{DateTime.UtcNow}: {ex.Message}");
                ResourceCacheService.SaveCacheData();
                throw;
            }
        }

        private static async Task<PrecomputeResult> PrecomputeDownloadPlanAsync(string[] gameKeys, bool forceDownload, CancellationToken cancellationToken = default)
        {
            var result = new PrecomputeResult();
            var cacheData = ResourceCacheService.GetCacheData();
            using var semaphore = new SemaphoreSlim(3, 3);
            var tasks = new List<Task>();
            foreach (var gameKey in gameKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!ResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
                        {
                            lock (result.Gate)
                            {
                                result.Errors[gameKey] = new InvalidOperationException($"Unknown game key: {gameKey}");
                            }
                            return;
                        }
                        if (string.IsNullOrEmpty(gameConfig.WebdavUrl) || string.IsNullOrEmpty(gameConfig.LocalPath))
                        {
                            lock (result.Gate)
                            {
                                var missingFields = new List<string>();
                                if (string.IsNullOrEmpty(gameConfig.WebdavUrl)) missingFields.Add("WebDAV URL");
                                if (string.IsNullOrEmpty(gameConfig.LocalPath)) missingFields.Add("Local Path");
                                result.Errors[gameKey] = new InvalidOperationException($"Missing configuration for {gameConfig.DisplayName}: {string.Join(", ", missingFields)}");
                            }
                            return;
                        }
                        var remoteFiles = await CloudreveClient.GetFileListAsync(gameConfig.WebdavUrl, cancellationToken: cancellationToken);
                        CacheRemoteFileList(gameKey, remoteFiles);
                        var files = remoteFiles.Where(ResourcePathUtility.IsDownloadableFile).ToList();
                        if (!forceDownload)
                        {
                            var gameData = cacheData.GetOrCreateGameData(gameKey);
                            var localBasePath = ResourcePathUtility.GetLocalBasePath(gameConfig);
                            var planned = new List<RemoteFileInfo>(files.Count);
                            foreach (var f in files)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                gameData.Files.TryGetValue(f.RelativePath, out var cached);
                                if (ShouldDownloadFile(cached, f, forceDownload: false, localBasePath))
                                {
                                    planned.Add(f);
                                }
                            }
                            lock (result.Gate)
                            {
                                result.Plan[gameKey] = planned;
                            }
                        }
                        else
                        {
                            lock (result.Gate)
                            {
                                result.Plan[gameKey] = files;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lock (result.Gate)
                        {
                            result.Errors[gameKey] = ex;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }
            await Task.WhenAll(tasks);
            return result;
        }

        internal static async Task SynchronizeFilesForSingleGameAsync(string gameKey, FileUpdateInfo updateInfo, CancellationToken cancellationToken = default)
        {
            await ResourceOperationRunner.ExecuteWithProgressAsync(
                progressTitle: "Synchronizing Files",
                progressMessage: $"Processing {updateInfo.TotalChanges} file changes...",
                cancelledDialogMessage: "File synchronization cancelled.",
                failureTitle: "Synchronization Failed",
                failureMessageFactory: ex => $"Failed to synchronize files: {ex.Message}",
                failureLogMessage: "Failed to synchronize files",
                cancellationToken: cancellationToken,
                operation: async progress =>
                {
                    progress.Token.ThrowIfCancellationRequested();
                    var updateInfoMap = new Dictionary<string, FileUpdateInfo> { { gameKey, updateInfo } };
                    var shared = new SharedOperationProgress(updateInfo.TotalChanges);
                    await SynchronizeFilesForMultipleGamesInternalAsync(updateInfoMap, shared, progress.Token);

                    var gameName = ResourceConfig.Games[gameKey].DisplayName;
                    DialogWindow.ShowInfo(
                        "Synchronization Complete",
                        $"Successfully synchronized {updateInfo.TotalChanges} files for {gameName}!\n\n" +
                        $"Updated: {updateInfo.TotalFiles} files\n" +
                        $"Deleted: {updateInfo.DeletedFiles.Count} files");
                });
        }

        internal static async Task DownloadSpecificFilesForMultipleGamesAsync(Dictionary<string, FileUpdateInfo> gamesNeedingUpdates, CancellationToken cancellationToken = default)
        {
            var totalFiles = gamesNeedingUpdates.Values.Sum(info => info.TotalFiles);

            await ResourceOperationRunner.ExecuteWithProgressAsync(
                progressTitle: "Updating Files",
                progressMessage: $"Downloading {totalFiles} files across {gamesNeedingUpdates.Count} games...",
                cancelledDialogMessage: "File update cancelled.",
                failureTitle: "Update Failed",
                failureMessageFactory: ex => $"Failed to update files: {ex.Message}",
                failureLogMessage: "Failed to update specific files",
                cancellationToken: cancellationToken,
                operation: async progress =>
                {
                    progress.Token.ThrowIfCancellationRequested();
                    foreach (var kvp in gamesNeedingUpdates)
                    {
                        progress.Token.ThrowIfCancellationRequested();
                        var allFilesToUpdate = kvp.Value.MissingFiles.Concat(kvp.Value.OutdatedFiles).ToList();
                        await DownloadSpecificFilesForGameAsync(kvp.Key, allFilesToUpdate, cancellationToken: progress.Token);
                    }

                    DialogWindow.ShowInfo(
                        "Update Complete",
                        $"Successfully updated {totalFiles} files across {gamesNeedingUpdates.Count} games!");
                });
        }

        private static async Task DownloadSpecificFilesForGameAsync(string gameKey, List<string> filePaths, SharedOperationProgress shared = null, CancellationToken cancellationToken = default)
        {
            if (filePaths == null || !filePaths.Any())
                return;
            filePaths = filePaths.Where(path => !ResourcePathUtility.IsThumbMarkerFile(path)).ToList();
            if (filePaths.Count == 0) return;
            var gameConfig = ResourceConfig.Games[gameKey];
            var cacheData = ResourceCacheService.GetCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Downloading {filePaths.Count} specific files for {gameConfig.DisplayName}");
            var cachedFiles = GetCachedRemoteFileList(gameKey);
            List<RemoteFileInfo> remoteFiles;
            if (cachedFiles != null)
            {
                remoteFiles = cachedFiles;
            }
            else
            {
                remoteFiles = await CloudreveClient.GetFileListAsync(gameConfig.WebdavUrl, cancellationToken: cancellationToken);
                CacheRemoteFileList(gameKey, remoteFiles);
            }
            var remoteFilesDict = remoteFiles.Where(ResourcePathUtility.IsDownloadableFile)
                                            .ToDictionary(f => f.RelativePath, f => f);
            var localBasePath = ResourcePathUtility.GetLocalBasePath(gameConfig);
            Directory.CreateDirectory(localBasePath);
            var httpClient = CloudreveClient.SharedClient;
            int downloadedCount = 0;
            foreach (var filePath in filePaths)
            {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (remoteFilesDict.TryGetValue(filePath, out var remoteFile))
                    {
                        var cleanRelativePath = remoteFile.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                        var localPath = Path.Combine(localBasePath, cleanRelativePath);
                        var localDir = Path.GetDirectoryName(localPath);
                        if (!Directory.Exists(localDir))
                        {
                            Directory.CreateDirectory(localDir);
                        }
                        await CloudreveClient.DownloadFileAsync(httpClient, remoteFile, gameConfig.WebdavUrl, localPath, cancellationToken);
                        var fileInfo = new FileInfo(localPath);
                        var cachedFileInfo = CreateCachedFileInfo(remoteFile, localPath, fileInfo);
                        cachedFileInfo.CalculateChecksum();
                        gameData.UpdateFileInfo(remoteFile.RelativePath, cachedFileInfo);
                        downloadedCount++;
                        if (shared == null)
                        {
                            ProgressDialog.Update((float)downloadedCount / filePaths.Count, $"Downloaded {downloadedCount}/{filePaths.Count} files...");
                        }
                        else
                        {
                            shared.Increment();
                            ProgressDialog.Update(shared.Progress, $"Processed {shared.Completed}/{shared.Total} changes...");
                        }
                    }
                    else
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Warning, $"File not found on server: {filePath}");
                    }
            }
            gameData.LastSync = DateTime.UtcNow;
            ResourceCacheService.SaveCacheData();
            AssetDatabase.Refresh();
        }

        private static bool ShouldDownloadFile(
            CachedFileInfo cachedFile,
            RemoteFileInfo remoteFile,
            bool forceDownload,
            string expectedLocalBasePath)
        {
            if (forceDownload)
            {
                return true;
            }

            if (cachedFile == null)
            {
                return true;
            }

            if (!cachedFile.IsValid())
            {
                return true;
            }

            if (!IsPathUnderRoot(cachedFile.LocalPath, expectedLocalBasePath))
            {
                return true;
            }

            if (string.IsNullOrEmpty(remoteFile.ETag))
            {
                return true;
            }

            if (cachedFile.RemoteEtag != remoteFile.ETag)
            {
                return true;
            }

            return false;
        }

        private static CachedFileInfo CreateCachedFileInfo(RemoteFileInfo remoteFile, string localPath, FileInfo fileInfo)
        {
            return new CachedFileInfo
            {
                RelativePath = remoteFile.RelativePath,
                LocalPath = localPath,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                DownloadDate = DateTime.UtcNow,
                RemoteEtag = remoteFile.ETag,
                CachedValid = true
            };
        }

        private static string[] ResolveCanonicalGameKeys(IEnumerable<string> requestedGameKeys)
        {
            var resolved = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawKey in requestedGameKeys ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    continue;
                }

                var key = rawKey.Trim();

                if (!ResourceConfig.Games.ContainsKey(key))
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Warning, $"Ignoring unknown resource game key: '{rawKey}'");
                    continue;
                }

                if (seen.Add(key))
                {
                    resolved.Add(key);
                }
            }

            return resolved.ToArray();
        }

        private static bool IsPathUnderRoot(string path, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            try
            {
                var normalizedPath = NormalizePath(path);
                var normalizedRoot = NormalizePath(rootPath);
                if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    normalizedRoot += Path.DirectorySeparatorChar;
                }

                return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
#endif
