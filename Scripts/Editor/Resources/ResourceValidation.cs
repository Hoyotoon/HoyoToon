#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using HoyoToon.Editor.Utilities;

using HoyoToon.Editor.UI.Windows;

using HoyoToon.Editor.UI.ResourcesUI;

namespace HoyoToon.Editor.ResourceSystem
{
    internal static class ResourceValidation
    {
        internal static async Task CheckResourceStatusWithActions(CancellationToken cancellationToken = default)
        {
            await ResourceOperationRunner.ExecuteWithProgressAsync(
                progressTitle: "Analyzing Resources",
                progressMessage: "Connecting to servers...",
                cancelledDialogMessage: "Resource analysis cancelled.",
                failureTitle: "Status Check Failed",
                failureMessageFactory: ex => $"Failed to check resource status: {ex.Message}\n\nCheck the console for more details.",
                failureLogMessage: "Failed to check resource status",
                cancellationToken: cancellationToken,
                operation: async progress =>
            {
                var operationToken = progress.Token;
            {
                    operationToken.ThrowIfCancellationRequested();

                    var gameKeys = ResourceConfig.Games.Keys.ToArray();
                    var eligibleGameKeys = new List<string>();
                    var misconfiguredGames = new List<string>();
                    var updateInfoMap = new Dictionary<string, FileUpdateInfo>();
                    var missingGames = new List<string>();

                    foreach (var gameKey in gameKeys)
                    {
                        var hasLocalPath = !string.IsNullOrWhiteSpace(ResourceConfig.Games[gameKey].LocalPath);
                        if (hasLocalPath)
                        {
                            eligibleGameKeys.Add(gameKey);
                        }
                        else
                        {
                            misconfiguredGames.Add(gameKey);
                        }
                    }

                    if (eligibleGameKeys.Count == 0)
                    {
                        ResourceDialogBuilder.ShowResourceStatusDialog(updateInfoMap, missingGames, eligibleGameKeys, misconfiguredGames);
                        return;
                    }

                    for (int i = 0; i < eligibleGameKeys.Count; i++)
                    {
                        operationToken.ThrowIfCancellationRequested();
                        var gameKey = eligibleGameKeys[i];
                        var gameName = ResourceConfig.Games[gameKey].DisplayName;

                        progress.Update((float)i / eligibleGameKeys.Count, $"Analyzing {gameName}... ({i + 1}/{eligibleGameKeys.Count})");

                        if (!HasResourcesForGame(gameKey))
                        {
                            missingGames.Add(gameKey);
                        }
                        else
                        {
                            var updateInfo = await GetDetailedFileUpdateInfoAsync(gameKey, operationToken);
                            if (updateInfo.HasChanges)
                            {
                                updateInfoMap[gameKey] = updateInfo;
                            }
                        }
                    }

                    var cacheData = ResourceCacheService.GetCacheData();
                    cacheData.MarkUpdateCheckCompleted();
                    ResourceCacheService.SaveCacheData();

                    ResourceDialogBuilder.ShowResourceStatusDialog(updateInfoMap, missingGames, eligibleGameKeys, misconfiguredGames);
                }
            });
        }

        internal static Dictionary<string, ResourceStatus> GetResourceStatus()
        {
            var cacheData = ResourceCacheService.GetCacheData();
            var status = new Dictionary<string, ResourceStatus>();

            foreach (var gameConfig in ResourceConfig.Games.Values)
            {
                var gameData = cacheData.GetOrCreateGameData(gameConfig.Key);
                var localPath = GetLocalBasePath(gameConfig);
                
                status[gameConfig.Key] = new ResourceStatus
                {
                    GameKey = gameConfig.Key,
                    DisplayName = gameConfig.DisplayName,
                    HasResources = HasResourcesForGame(gameConfig.Key),
                    IsUpToDate = IsGameResourcesUpToDate(gameConfig.Key),
                    LastSync = gameData.LastSync,
                    FileCount = gameData.Files.Count,
                    TotalSize = gameData.TotalDownloaded
                };
            }

            return status;
        }

        private static bool IsGameResourcesUpToDate(string gameKey)
        {
            var cacheData = ResourceCacheService.GetCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            
            // Always check for local file issues
            var needUpdate = gameData.GetFilesNeedingUpdate();
            if (needUpdate.Count > 0)
                return false;

            // Check if we need to validate against server (every 6 hours by default)
            if (!cacheData.IsUpdateCheckNeeded())
                return true;

            // If it's been a while, we should check the server for new files
            return false;
        }

        internal static bool HasResourcesForGame(string gameKey)
        {
            if (!ResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
                return false;
                
            var localPath = GetLocalBasePath(gameConfig);
            if (string.IsNullOrEmpty(localPath)) return false;
            return Directory.Exists(localPath) && Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories)
                .Any(file => !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
        }

        internal static string GetGameResourcePath(string gameKey)
        {
            if (!ResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
                return null;
                
            return GetLocalBasePath(gameConfig);
        }

        internal static List<LocalResourceInfo> GetAllLocalResources()
        {
            var resources = new List<LocalResourceInfo>();
            
            // Consider only games that have actual resource LocalPath
            var gameKeys = ResourceConfig.Games.Values.Where(g => !string.IsNullOrEmpty(g.LocalPath)).Select(g => g.Key).ToArray();
            
            foreach (var gameKey in gameKeys)
            {
                if (HasResourcesForGame(gameKey))
                {
                    resources.AddRange(GetLocalResourcesForGame(gameKey));
                }
            }
            
            return resources;
        }

        internal static List<LocalResourceInfo> GetLocalResourcesForGame(string gameKey)
        {
            var resources = new List<LocalResourceInfo>();
            var gamePath = GetGameResourcePath(gameKey);
            
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                return resources;
                
            try
            {
                var files = Directory.GetFiles(gamePath, "*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta"))
                    .ToArray();

                foreach (var filePath in files)
                {
                    var relativePath = Path.GetRelativePath(gamePath, filePath);
                    var fileName = Path.GetFileName(filePath);
                    var fileInfo = new FileInfo(filePath);

                    var resourceInfo = new LocalResourceInfo
                    {
                        FileName = fileName,
                        RelativePath = relativePath,
                        FullPath = filePath,
                        GameKey = gameKey,
                        FileType = GetFileType(Path.GetExtension(filePath)),
                        Size = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime
                    };

                    resources.Add(resourceInfo);
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to scan resources for {gameKey}: {ex.Message}");
            }
            
            return resources;
        }

        internal static bool AreResourcesAvailable()
        {
             return Directory.Exists(ResourcePathUtility.ResourcesBasePath) && 
                 Directory.EnumerateFileSystemEntries(ResourcePathUtility.ResourcesBasePath, "*", SearchOption.AllDirectories).Any();
        }

        private static string GetFileType(string extension)
        {
            return extension.ToLower() switch
            {
                ".mat" => "Material",
                ".prefab" => "Prefab",
                ".fbx" => "FBX Model",
                ".png" or ".jpg" or ".jpeg" or ".tga" or ".tiff" => "Texture",
                ".json" => "JSON Data",
                ".cs" => "Script",
                ".asset" => "Asset",
                _ => "Other"
            };
        }

        internal static bool ValidateResourcesAvailable()
        {
            var missingGameConfigs = new List<GameConfig>();
            
            foreach (var gameConfig in ResourceConfig.Games.Values)
            {
                if (string.IsNullOrEmpty(gameConfig.LocalPath)) continue;
                if (!HasResourcesForGame(gameConfig.Key))
                {
                    missingGameConfigs.Add(gameConfig);
                }
            }

            if (missingGameConfigs.Any())
            {
                return ResourceDialogBuilder.ShowMissingResourcesDialog(missingGameConfigs);
            }

            return true;
        }

        internal static bool ValidateResourcesForGames(params string[] gameKeys)
        {
            if (gameKeys == null || gameKeys.Length == 0)
                return ValidateResourcesAvailable();

            var missingGameConfigs = new List<GameConfig>();
            var gamesNeedingUpdates = new Dictionary<string, FileUpdateInfo>();
            
            foreach (string gameKey in gameKeys)
            {
                if (ResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
                {
                    if (string.IsNullOrEmpty(gameConfig.LocalPath))
                    {
                        continue;
                    }
                    if (!HasResourcesForGame(gameKey))
                    {
                        missingGameConfigs.Add(gameConfig);
                    }
                    else
                    {
                        var updateInfo = GetFileUpdateInfo(gameKey);
                        if (updateInfo.HasChanges)
                        {
                            gamesNeedingUpdates[gameKey] = updateInfo;
                        }
                    }
                }
            }

            if (missingGameConfigs.Any() || gamesNeedingUpdates.Any())
            {
                return ResourceDialogBuilder.ShowTargetedResourceDialog(missingGameConfigs, gamesNeedingUpdates);
            }

            return true;
        }

        private static FileUpdateInfo GetFileUpdateInfo(string gameKey)
        {
            var cacheData = ResourceCacheService.GetCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            
            var updateInfo = new FileUpdateInfo { GameKey = gameKey };
            
            var needUpdate = gameData.GetFilesNeedingUpdate();
            updateInfo.MissingFiles.AddRange(needUpdate);
            
            return updateInfo;
        }

        internal static async Task<FileUpdateInfo> GetDetailedFileUpdateInfoAsync(string gameKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cacheData = ResourceCacheService.GetCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            var gameConfig = ResourceConfig.Games[gameKey];
            
            var updateInfo = new FileUpdateInfo { GameKey = gameKey };
            
            try
            {
                if (string.IsNullOrEmpty(gameConfig.WebdavUrl))
                {
                    // No server to compare against; fall back to local validation
                    var needUpdateLocal = gameData.GetFilesNeedingUpdate();
                    updateInfo.MissingFiles.AddRange(needUpdateLocal);
                    return updateInfo;
                }
                var remoteFiles = await CloudreveClient.GetFileListAsync(gameConfig.WebdavUrl, cancellationToken: cancellationToken);
                    var serverFiles = remoteFiles.Where(ResourcePathUtility.IsDownloadableFile)
                                             .ToDictionary(f => f.RelativePath, f => f);

                var missingFiles = serverFiles.Keys.Where(path => !gameData.Files.ContainsKey(path)).ToList();
                updateInfo.MissingFiles.AddRange(missingFiles);
                
                var outdatedFiles = serverFiles.Where(kvp => 
                    gameData.Files.TryGetValue(kvp.Key, out var cachedFile) && 
                    !string.IsNullOrEmpty(kvp.Value.ETag) && 
                    cachedFile.RemoteEtag != kvp.Value.ETag
                ).Select(kvp => kvp.Key).ToList();
                updateInfo.OutdatedFiles.AddRange(outdatedFiles);
                
                var deletedFiles = gameData.Files.Keys.Where(path => !ResourcePathUtility.IsThumbMarkerFile(path) && !serverFiles.ContainsKey(path)).ToList();
                updateInfo.DeletedFiles.AddRange(deletedFiles);
                
                var locallyMissingFiles = gameData.Files.Where(kvp => !ResourcePathUtility.IsThumbMarkerFile(kvp.Key) && !File.Exists(kvp.Value.LocalPath))
                                                        .Select(kvp => kvp.Key).ToList();
                updateInfo.MissingFiles.AddRange(locallyMissingFiles.Where(f => !updateInfo.MissingFiles.Contains(f)));
                
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"{gameConfig.DisplayName}: {updateInfo.MissingFiles.Count} missing, {updateInfo.OutdatedFiles.Count} outdated, {updateInfo.DeletedFiles.Count} deleted files");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to get detailed update info for {gameConfig.DisplayName}: {ex.Message}");
                // Fall back to local validation only
                var needUpdate = gameData.GetFilesNeedingUpdate();
                updateInfo.MissingFiles.AddRange(needUpdate.Where(f => !updateInfo.MissingFiles.Contains(f)));
            }
            
            return updateInfo;
        }

        private static string GetLocalBasePath(GameConfig gameConfig) => ResourcePathUtility.GetLocalBasePath(gameConfig);
    }
}
#endif
