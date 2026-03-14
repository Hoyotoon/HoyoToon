#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoyoToon.Editor.ResourceSystem;
using HoyoToon.Editor.Utilities;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.UI.ResourcesUI
{
    internal static class ResourceMenuItems
    {
        private static void RunDeleteWithConfirmation(string title, string message, Action onConfirmed)
        {
            DialogWindow.ShowOkCancel(title, message, MessageType.Warning, null, confirmed =>
            {
                if (confirmed)
                {
                    onConfirmed();
                }
            });
        }

        [MenuItem("HoyoToon/Resources/Check Resource Status", priority = 5)]
        internal static void CheckResourceStatus()
        {
            ResourceOperationRunner.RunCancelableResourceOperation("Resource status check", ResourceValidation.CheckResourceStatusWithActions);
        }

        internal static void DownloadAllResources()
        {
            // Download only games that have resource LocalPath defined
            var keys = ResourceConfig.Games.Values.Where(g => !string.IsNullOrEmpty(g.LocalPath)).Select(g => g.Key).ToArray();
            ResourceOperationRunner.RunCancelableResourceOperation("Download all resources", token => ResourceDownloadService.DownloadResourcesAsync(keys, cancellationToken: token));
        }

        internal static void DownloadGameResources(string gameKey)
        {
            ResourceOperationRunner.RunCancelableResourceOperation($"Download {gameKey} resources", token => ResourceDownloadService.DownloadResourcesAsync(new[] { gameKey }, cancellationToken: token));
        }

        internal static void DeleteGameResourcesByKey(string gameKey) => DeleteGameResources(gameKey);

        private static void DeleteGameResources(string gameKey)
        {
            if (!ResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
            {
                DialogWindow.ShowError("Error", $"Game configuration not found for: {gameKey}");
                return;
            }

            var cacheData = ResourceCacheService.GetCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            var fileCount = gameData.Files.Count;
            var sizeMB = gameData.TotalDownloaded / (1024d * 1024d);

            var message = $"Delete all {gameConfig.DisplayName} resources?\n\n" +
                         $"Files to delete: {fileCount}\n" +
                         $"Space to free: {sizeMB:F2} MB\n\n" +
                         $"This action cannot be undone.";

            RunDeleteWithConfirmation($"Delete {gameConfig.DisplayName} Resources", message, () =>
            {
                ResourceOperationRunner.RunCancelableResourceOperation($"Delete {gameConfig.DisplayName} resources", async token =>
                {
                    await ResourceOperationRunner.ExecuteWithProgressAsync(
                        progressTitle: "Deleting Resources",
                        progressMessage: $"Removing {gameConfig.DisplayName} resources...",
                        failureTitle: "Deletion Failed",
                        failureMessageFactory: ex => $"Failed to delete {gameConfig.DisplayName} resources: {ex.Message}",
                        failureLogMessage: $"Failed to delete {gameConfig.DisplayName} resources",
                        cancellationToken: token,
                        operation: progress =>
                        {
                            var localGamePath = ResourcePathUtility.GetLocalBasePath(gameConfig);
                            long actualDeletedSize = 0;

                            if (Directory.Exists(localGamePath))
                            {
                                var dirInfo = new DirectoryInfo(localGamePath);
                                actualDeletedSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

                                Directory.Delete(localGamePath, true);
                                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Deleted {gameConfig.DisplayName} directory: {localGamePath}");
                            }

                            cacheData.Games.Remove(gameKey);
                            ResourceCacheService.SaveCacheData();

                            AssetDatabase.Refresh();

                            var actualSizeMB = actualDeletedSize / (1024d * 1024d);
                            var resultMessage = $"{gameConfig.DisplayName} resources deleted successfully!\n\n" +
                                               $"Files removed: {fileCount}\n" +
                                               $"Space freed: {actualSizeMB:F2} MB";

                            DialogWindow.ShowInfo("Resources Deleted", resultMessage);
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"{gameConfig.DisplayName} deletion completed: {fileCount} files, {actualSizeMB:F2} MB freed");
                            return System.Threading.Tasks.Task.CompletedTask;
                        });
                });
            });
        }

        internal static void ClearResourceCache()
        {
            DialogWindow.ShowOkCancel("Clear Resource Cache", 
                "This will remove all cached resource files and force a complete re-download next time. Continue?", 
                MessageType.Warning, null, confirmed =>
            {
                if (confirmed)
                {
                    ResourceCacheService.ClearCacheData();
                    DialogWindow.ShowInfo("Cache Cleared", "Resource cache has been cleared successfully.");
                }
            });
        }

        internal static void DeleteAllResources()
        {
            RunDeleteWithConfirmation("Delete All Resources", 
                "This will permanently delete ALL downloaded resource files from disk and clear the cache.\n\n" +
                "This action cannot be undone and will free up significant disk space.\n\n" +
                "You will need to re-download resources if you want to use them again.\n\nContinue?", 
                () =>
            {
                ResourceOperationRunner.RunCancelableResourceOperation("Delete all resources", async token =>
                {
                    await ResourceOperationRunner.ExecuteWithProgressAsync(
                        progressTitle: "Deleting Resources",
                        progressMessage: "Removing all resource files...",
                        failureTitle: "Deletion Failed",
                        failureMessageFactory: ex => $"Failed to delete all resources: {ex.Message}\n\nCheck the console for more details.",
                        failureLogMessage: "Failed to delete resources",
                        cancellationToken: token,
                        operation: progress =>
                        {
                            var cacheData = ResourceCacheService.GetCacheData();
                            int totalFiles = 0;
                            long totalSize = 0;

                            foreach (var gameData in cacheData.Games.Values)
                            {
                                totalFiles += gameData.Files.Count;
                                totalSize += gameData.TotalDownloaded;
                            }

                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Starting deletion of {totalFiles} resource files ({(totalSize / (1024d * 1024d)):F2} MB)");

                            int deletedFiles = 0;
                            long deletedSize = 0;

                            foreach (var gameConfig in ResourceConfig.Games.Values)
                            {
                                if (string.IsNullOrEmpty(gameConfig.LocalPath))
                                {
                                    continue;
                                }

                                var localGamePath = ResourcePathUtility.GetLocalBasePath(gameConfig);
                                progress.Update(totalFiles > 0 ? (float)deletedFiles / totalFiles : 0f, $"Deleting {gameConfig.DisplayName} resources...");

                                if (Directory.Exists(localGamePath))
                                {
                                    try
                                    {
                                        var dirInfo = new DirectoryInfo(localGamePath);
                                        var dirSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);

                                        Directory.Delete(localGamePath, true);

                                        deletedSize += dirSize;
                                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Deleted {gameConfig.DisplayName} directory: {localGamePath}");
                                    }
                                    catch (Exception ex)
                                    {
                                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to delete {gameConfig.DisplayName} directory: {ex.Message}");
                                    }
                                }

                                deletedFiles += cacheData.Games.ContainsKey(gameConfig.Key)
                                    ? cacheData.Games[gameConfig.Key].Files.Count
                                    : 0;
                            }

                            ResourceCacheService.ClearCacheData();

                            AssetDatabase.Refresh();

                            var deletedSizeMB = deletedSize / (1024d * 1024d);
                            var msgText = $"Successfully deleted all resources!\n\n" +
                                         $"Files removed: {totalFiles}\n" +
                                         $"Space freed: {deletedSizeMB:F2} MB\n\n" +
                                         $"Resources have been completely removed from disk.";

                            DialogWindow.ShowInfo("Resources Deleted", msgText);
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Resource deletion completed: {totalFiles} files, {deletedSizeMB:F2} MB freed");
                            return System.Threading.Tasks.Task.CompletedTask;
                        });
                });
            });
        }

    }
}
#endif
