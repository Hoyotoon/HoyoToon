#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using System.Xml.Linq;
using System.Text;

namespace HoyoToon
{
    /// <summary>
    /// Manages downloading and caching of HoyoToon resources from WebDAV
    /// </summary>
    public static class HoyoToonResourceManager
    {
        #region Constants and Paths

        private static readonly string PackageName = "com.meliverse.hoyotoon";
        private static readonly string PackagePath = Path.Combine(HoyoToonParseManager.GetPackagePath(PackageName));
        private static readonly string CacheDataPath = Path.Combine(PackagePath, "Scripts/Editor/Manager/Resources", "ResourceCache.json");
        private static readonly string ResourcesBasePath = Path.Combine(PackagePath, "Resources");

        #endregion

        #region Cache Management

        private static HoyoToonResourceCacheData _cacheData;
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// Load cache data from disk or create new if doesn't exist
        /// </summary>
        private static HoyoToonResourceCacheData LoadCacheData()
        {
            lock (_cacheLock)
            {
                if (_cacheData != null)
                    return _cacheData;

                try
                {
                    if (File.Exists(CacheDataPath))
                    {
                        string json = File.ReadAllText(CacheDataPath);
                        _cacheData = JsonConvert.DeserializeObject<HoyoToonResourceCacheData>(json);
                        HoyoToonLogs.LogDebug("Resource cache data loaded successfully.");
                    }
                    else
                    {
                        _cacheData = new HoyoToonResourceCacheData();
                        HoyoToonLogs.LogDebug("Created new resource cache data.");
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogs.ErrorDebug($"Failed to load resource cache data: {ex.Message}");
                    _cacheData = new HoyoToonResourceCacheData();
                }

                return _cacheData;
            }
        }

        /// <summary>
        /// Save cache data to disk
        /// </summary>
        public static void SaveCacheData()
        {
            lock (_cacheLock)
            {
                try
                {
                    string directoryPath = Path.GetDirectoryName(CacheDataPath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    string json = JsonConvert.SerializeObject(_cacheData, Formatting.Indented);
                    File.WriteAllText(CacheDataPath, json);
                    HoyoToonLogs.LogDebug("Resource cache data saved successfully.");
                }
                catch (Exception ex)
                {
                    HoyoToonLogs.ErrorDebug($"Failed to save resource cache data: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get cached data, loading if necessary
        /// </summary>
        public static HoyoToonResourceCacheData GetCacheData()
        {
            return LoadCacheData();
        }

        #endregion

        #region Menu Items

        [MenuItem("HoyoToon/Resources/Check Resource Status", priority = 5)]
        public static async void CheckResourceStatus()
        {
            await CheckResourceStatusWithActions();
        }

        /// <summary>
        /// Check resource status with detailed file-level analysis and immediate action options
        /// </summary>
        private static async Task CheckResourceStatusWithActions()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Analyzing Resources", "Connecting to servers...", 0f);

                var gameKeys = HoyoToonResourceConfig.Games.Keys.ToArray();
                var updateInfoMap = new Dictionary<string, FileUpdateInfo>();
                var missingGames = new List<string>();

                for (int i = 0; i < gameKeys.Length; i++)
                {
                    var gameKey = gameKeys[i];
                    var gameName = HoyoToonResourceConfig.Games[gameKey].DisplayName;
                    
                    EditorUtility.DisplayProgressBar("Analyzing Resources", 
                        $"Analyzing {gameName}... ({i + 1}/{gameKeys.Length})", 
                        (float)i / gameKeys.Length);

                    // Check if game resources exist at all
                    if (!HasResourcesForGame(gameKey))
                    {
                        missingGames.Add(gameKey);
                    }
                    else
                    {
                        // Get detailed file-level information for existing games
                        var updateInfo = await GetDetailedFileUpdateInfoAsync(gameKey);
                        if (updateInfo.HasChanges)
                        {
                            updateInfoMap[gameKey] = updateInfo;
                        }
                    }
                }

                EditorUtility.ClearProgressBar();

                // Mark that we've done a recent update check
                var cacheData = LoadCacheData();
                cacheData.MarkUpdateCheckCompleted();
                SaveCacheData();

                // Show comprehensive status dialog with action options
                ShowResourceStatusDialog(updateInfoMap, missingGames);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                HoyoToonLogs.ErrorDebug($"Failed to check resource status: {ex.Message}");
                EditorUtility.DisplayDialog("Status Check Failed", 
                    $"Failed to check resource status: {ex.Message}\n\nCheck the console for more details.", "OK");
            }
        }

        /// <summary>
        /// Show comprehensive resource status dialog with immediate action options
        /// </summary>
        private static void ShowResourceStatusDialog(Dictionary<string, FileUpdateInfo> updateInfoMap, List<string> missingGames)
        {
            var upToDateGames = HoyoToonResourceConfig.Games.Keys
                .Where(key => !missingGames.Contains(key) && !updateInfoMap.ContainsKey(key))
                .ToList();

                StringBuilder message = new StringBuilder();
            message.AppendLine("HoyoToon Resource Analysis:");
                message.AppendLine();

            // Show up-to-date games
                if (upToDateGames.Any())
                {
                    message.AppendLine("✓ Up to Date:");
                foreach (var gameKey in upToDateGames)
                    {
                    message.AppendLine($"  • {HoyoToonResourceConfig.Games[gameKey].DisplayName}");
                    }
                    message.AppendLine();
                }

            // Show missing games (entire game missing)
            if (missingGames.Any())
                {
                message.AppendLine("❌ Missing Games:");
                foreach (var gameKey in missingGames)
                    {
                    message.AppendLine($"  • {HoyoToonResourceConfig.Games[gameKey].DisplayName}");
                    }
                    message.AppendLine();
            }

            // Show games with file-level changes needed
            if (updateInfoMap.Any())
            {
                message.AppendLine("⚠ Files Need Synchronizing:");
                foreach (var kvp in updateInfoMap)
                {
                    var gameName = HoyoToonResourceConfig.Games[kvp.Key].DisplayName;
                    var updateInfo = kvp.Value;
                    
                    var details = new List<string>();
                    if (updateInfo.MissingFiles.Count > 0)
                        details.Add($"{updateInfo.MissingFiles.Count} missing");
                    if (updateInfo.OutdatedFiles.Count > 0)
                        details.Add($"{updateInfo.OutdatedFiles.Count} outdated");
                    if (updateInfo.DeletedFiles.Count > 0)
                        details.Add($"{updateInfo.DeletedFiles.Count} deleted");
                    
                    var detailText = details.Any() ? $" ({string.Join(", ", details)})" : "";
                    message.AppendLine($"  • {gameName}{detailText}");
                    
                    // Show specific file examples (limit to 3 for readability)
                    var allFiles = updateInfo.MissingFiles.Concat(updateInfo.OutdatedFiles).Concat(updateInfo.DeletedFiles).Take(3).ToList();
                    foreach (var file in allFiles)
                    {
                        var fileName = System.IO.Path.GetFileName(file);
                        var status = "";
                        if (updateInfo.MissingFiles.Contains(file)) status = " (missing)";
                        else if (updateInfo.OutdatedFiles.Contains(file)) status = " (outdated)";
                        else if (updateInfo.DeletedFiles.Contains(file)) status = " (deleted)";
                        
                        message.AppendLine($"    - {fileName}{status}");
                    }
                    if (updateInfo.TotalChanges > 3)
                    {
                        message.AppendLine($"    - ...and {updateInfo.TotalChanges - 3} more files");
                    }
                    message.AppendLine();
                }
            }

            // Determine what action options to show
            bool hasAnyUpdates = missingGames.Any() || updateInfoMap.Any();
            
            if (!hasAnyUpdates)
            {
                message.AppendLine("All resources are up to date! 🎉");
                EditorUtility.DisplayDialog("Resources Up to Date", message.ToString(), "OK");
                return;
            }

            // Show action dialog
            message.AppendLine("What would you like to do?");
            
            string title = "Resource Synchronization Available";
            string option1, option2, option3;
            
            if (missingGames.Any() && updateInfoMap.Any())
            {
                // Both missing games and file synchronization
                option1 = "Download & Sync All";
                option2 = "Sync Files Only";
                option3 = "Cancel";
            }
            else if (missingGames.Any())
            {
                // Only missing games
                option1 = "Download Missing Games";
                option2 = "Download All Resources";
                option3 = "Cancel";
            }
            else
            {
                // Only file synchronization
                option1 = "Synchronize Files";
                option2 = "Refresh All Games";
                option3 = "Cancel";
            }

            int result = EditorUtility.DisplayDialogComplex(title, message.ToString(), option1, option2, option3);

            switch (result)
            {
                case 0: // First option
                    if (missingGames.Any() && updateInfoMap.Any())
                    {
                        // Download everything and synchronize files
                        DownloadMissingResourcesAndSynchronizeAsync(missingGames, updateInfoMap);
                    }
                    else if (missingGames.Any())
                    {
                        // Download missing games only
                        DownloadMissingGamesAsync(missingGames.ToArray());
                    }
                    else
                    {
                        // Synchronize files only (downloads + deletions)
                        SynchronizeFilesForMultipleGamesAsyncWrapper(updateInfoMap);
                    }
                    break;
                case 1: // Second option
                    if (missingGames.Any() && !updateInfoMap.Any())
                    {
                        // Download all resources
                        DownloadAllResources();
                    }
                    else
                    {
                        // Refresh entire games that need updates
                        var gamesToRefresh = updateInfoMap.Keys.Concat(missingGames).ToArray();
                        DownloadResourcesAsync(gamesToRefresh);
                    }
                    break;
                case 2: // Cancel
                    break;
            }
        }

        /// <summary>
        /// Download missing games and synchronize specific files (downloads + deletions)
        /// </summary>
                private static async void DownloadMissingResourcesAndSynchronizeAsync(List<string> missingGames, Dictionary<string, FileUpdateInfo> updateInfoMap)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Synchronizing Resources", "Preparing synchronization...", 0f);
                
                // Download missing games first
                if (missingGames.Any())
                {
                    await DownloadResourcesAsync(missingGames.ToArray());
                }
                
                // Then synchronize specific files (downloads + deletions)
                if (updateInfoMap.Any())
                {
                    await SynchronizeFilesForMultipleGamesInternalAsync(updateInfoMap);
                }
                
                EditorUtility.ClearProgressBar();
                
                var totalChanges = updateInfoMap.Values.Sum(info => info.TotalChanges);
                var totalDeleted = updateInfoMap.Values.Sum(info => info.DeletedFiles.Count);
                EditorUtility.DisplayDialog("Synchronization Complete", 
                    $"Successfully downloaded {missingGames.Count} games, updated {totalChanges - totalDeleted} files, and removed {totalDeleted} deleted files!", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                HoyoToonLogs.ErrorDebug($"Failed to synchronize resources: {ex.Message}");
                EditorUtility.DisplayDialog("Synchronization Failed", 
                    $"Failed to synchronize resources: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Synchronize files for multiple games (downloads + deletions)
        /// </summary>
        public static async Task SynchronizeFilesForMultipleGamesAsync(Dictionary<string, FileUpdateInfo> updateInfoMap)
        {
            try
            {
                var totalChanges = updateInfoMap.Values.Sum(info => info.TotalChanges);
                EditorUtility.DisplayProgressBar("Synchronizing Files", $"Processing {totalChanges} file changes...", 0f);
                
                await SynchronizeFilesForMultipleGamesInternalAsync(updateInfoMap);
                
                EditorUtility.ClearProgressBar();
                
                var totalUpdated = updateInfoMap.Values.Sum(info => info.TotalFiles);
                var totalDeleted = updateInfoMap.Values.Sum(info => info.DeletedFiles.Count);
                HoyoToonLogs.LogDebug($"Synchronization complete: updated {totalUpdated} files, removed {totalDeleted} deleted files");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                HoyoToonLogs.ErrorDebug($"Failed to synchronize files: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Internal method to synchronize files for multiple games
        /// </summary>
        private static async Task SynchronizeFilesForMultipleGamesInternalAsync(Dictionary<string, FileUpdateInfo> updateInfoMap)
        {
            foreach (var kvp in updateInfoMap)
            {
                var gameKey = kvp.Key;
                var updateInfo = kvp.Value;
                
                // Download/update files
                if (updateInfo.HasUpdates)
                {
                    var allFilesToUpdate = updateInfo.MissingFiles.Concat(updateInfo.OutdatedFiles).ToList();
                    await DownloadSpecificFilesForGameAsync(gameKey, allFilesToUpdate);
                }
                
                // Delete files that no longer exist on server
                if (updateInfo.HasDeletions)
                {
                    await DeleteSpecificFilesForGameAsync(gameKey, updateInfo.DeletedFiles);
                }
            }
        }

        /// <summary>
        /// Delete specific files for a game and update cache
        /// </summary>
        private static async Task DeleteSpecificFilesForGameAsync(string gameKey, List<string> filePaths)
        {
            if (filePaths == null || !filePaths.Any())
                return;

            var gameConfig = HoyoToonResourceConfig.Games[gameKey];
            var cacheData = LoadCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            
            HoyoToonLogs.LogDebug($"Deleting {filePaths.Count} files for {gameConfig.DisplayName}");
            
            int deletedCount = 0;
            foreach (var filePath in filePaths)
            {
                if (gameData.Files.TryGetValue(filePath, out var cachedFile))
                {
                    try
                    {
                        // Delete the local file if it exists
                        if (File.Exists(cachedFile.LocalPath))
                        {
                            File.Delete(cachedFile.LocalPath);
                            HoyoToonLogs.LogDebug($"Deleted local file: {cachedFile.LocalPath}");
                        }
                        
                        // Remove from cache
                        gameData.Files.Remove(filePath);
                        deletedCount++;
                        
                        EditorUtility.DisplayProgressBar("Deleting Files", 
                            $"Deleted {deletedCount}/{filePaths.Count} files...", 
                            (float)deletedCount / filePaths.Count);
                    }
                    catch (Exception ex)
                    {
                        HoyoToonLogs.WarningDebug($"Failed to delete file {cachedFile.LocalPath}: {ex.Message}");
                    }
                }
            }
            
            // Clean up empty directories
            try
            {
                var localBasePath = Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
                CleanupEmptyDirectories(localBasePath);
            }
            catch (Exception ex)
            {
                HoyoToonLogs.WarningDebug($"Failed to cleanup empty directories: {ex.Message}");
            }
            
            gameData.LastSync = DateTime.UtcNow;
            SaveCacheData();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Recursively remove empty directories
        /// </summary>
        private static void CleanupEmptyDirectories(string startPath)
        {
            if (!Directory.Exists(startPath))
                return;

            foreach (var directory in Directory.GetDirectories(startPath))
            {
                CleanupEmptyDirectories(directory);
                
                try
                {
                    // Delete directory if it's empty (no files or subdirectories)
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                        HoyoToonLogs.LogDebug($"Cleaned up empty directory: {directory}");
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogs.WarningDebug($"Failed to delete empty directory {directory}: {ex.Message}");
                }
            }
        }

        [MenuItem("HoyoToon/Resources/Download All Resources", priority = 10)]
        public static async void DownloadAllResources()
        {
            await DownloadResourcesAsync(HoyoToonResourceConfig.Games.Keys.ToArray());
        }

        [MenuItem("HoyoToon/Resources/Download Genshin Resources", priority = 11)]
        public static async void DownloadGenshinResources()
        {
            await DownloadResourcesAsync(new[] { "Genshin" });
        }

        [MenuItem("HoyoToon/Resources/Download StarRail Resources", priority = 12)]
        public static async void DownloadStarRailResources()
        {
            await DownloadResourcesAsync(new[] { "StarRail" });
        }

        [MenuItem("HoyoToon/Resources/Download Hi3 Resources", priority = 13)]
        public static async void DownloadHi3Resources()
        {
            await DownloadResourcesAsync(new[] { "Hi3" });
        }

        [MenuItem("HoyoToon/Resources/Download Wuwa Resources", priority = 14)]
        public static async void DownloadWuwaResources()
        {
            await DownloadResourcesAsync(new[] { "Wuwa" });
        }

        [MenuItem("HoyoToon/Resources/Download ZZZ Resources", priority = 15)]
        public static async void DownloadZZZResources()
        {
            await DownloadResourcesAsync(new[] { "ZZZ" });
        }

        [MenuItem("HoyoToon/Resources/Delete Genshin Resources", priority = 16)]
        public static void DeleteGenshinResources()
        {
            DeleteGameResources("Genshin");
        }

        [MenuItem("HoyoToon/Resources/Delete StarRail Resources", priority = 17)]
        public static void DeleteStarRailResources()
        {
            DeleteGameResources("StarRail");
        }

        [MenuItem("HoyoToon/Resources/Delete Hi3 Resources", priority = 18)]
        public static void DeleteHi3Resources()
        {
            DeleteGameResources("Hi3");
        }

        [MenuItem("HoyoToon/Resources/Delete Wuwa Resources", priority = 19)]
        public static void DeleteWuwaResources()
        {
            DeleteGameResources("Wuwa");
        }

        [MenuItem("HoyoToon/Resources/Delete ZZZ Resources", priority = 20)]
        public static void DeleteZZZResources()
        {
            DeleteGameResources("ZZZ");
        }

        /// <summary>
        /// Delete resources for a specific game
        /// </summary>
        private static void DeleteGameResources(string gameKey)
        {
            if (!HoyoToonResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
            {
                EditorUtility.DisplayDialog("Error", $"Game configuration not found for: {gameKey}", "OK");
                return;
            }

            var cacheData = LoadCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            var fileCount = gameData.Files.Count;
            var sizeMB = gameData.TotalDownloaded / (1024 * 1024);

            var message = $"Delete all {gameConfig.DisplayName} resources?\n\n" +
                         $"Files to delete: {fileCount}\n" +
                         $"Space to free: {sizeMB} MB\n\n" +
                         $"This action cannot be undone.";

            if (EditorUtility.DisplayDialog($"Delete {gameConfig.DisplayName} Resources", message, "Delete", "Cancel"))
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Deleting Resources", $"Removing {gameConfig.DisplayName} resources...", 0f);
                    
                    var localGamePath = Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
                    long actualDeletedSize = 0;
                    
                    if (Directory.Exists(localGamePath))
                    {
                        // Calculate actual disk size
                        var dirInfo = new DirectoryInfo(localGamePath);
                        actualDeletedSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                        
                        // Delete the entire game directory
                        Directory.Delete(localGamePath, true);
                        HoyoToonLogs.LogDebug($"Deleted {gameConfig.DisplayName} directory: {localGamePath}");
                    }
                    
                    // Remove from cache
                    cacheData.Games.Remove(gameKey);
                    SaveCacheData();
                    
                    // Refresh Unity's asset database
                    AssetDatabase.Refresh();
                    
                    EditorUtility.ClearProgressBar();
                    
                    var actualSizeMB = actualDeletedSize / (1024 * 1024);
                    var resultMessage = $"{gameConfig.DisplayName} resources deleted successfully!\n\n" +
                                       $"Files removed: {fileCount}\n" +
                                       $"Space freed: {actualSizeMB} MB";
                    
                    EditorUtility.DisplayDialog("Resources Deleted", resultMessage, "OK");
                    HoyoToonLogs.LogDebug($"{gameConfig.DisplayName} deletion completed: {fileCount} files, {actualSizeMB} MB freed");
                }
                catch (Exception ex)
                {
                    EditorUtility.ClearProgressBar();
                    HoyoToonLogs.ErrorDebug($"Failed to delete {gameConfig.DisplayName} resources: {ex.Message}");
                    EditorUtility.DisplayDialog("Deletion Failed", 
                        $"Failed to delete {gameConfig.DisplayName} resources: {ex.Message}", "OK");
                }
            }
        }

        [MenuItem("HoyoToon/Resources/Clear Resource Cache", priority = 22)]
        public static void ClearResourceCache()
        {
            if (EditorUtility.DisplayDialog("Clear Resource Cache", 
                "This will remove all cached resource files and force a complete re-download next time. Continue?", 
                "Yes", "Cancel"))
            {
                ClearCacheData();
                EditorUtility.DisplayDialog("Cache Cleared", "Resource cache has been cleared successfully.", "OK");
            }
        }

        [MenuItem("HoyoToon/Resources/Delete All Resources", priority = 21)]
        public static void DeleteAllResources()
        {
            if (EditorUtility.DisplayDialog("Delete All Resources", 
                "This will permanently delete ALL downloaded resource files from disk and clear the cache.\n\n" +
                "This action cannot be undone and will free up significant disk space.\n\n" +
                "You will need to re-download resources if you want to use them again.\n\nContinue?", 
                "Delete All", "Cancel"))
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Deleting Resources", "Removing all resource files...", 0f);
                    
                    // Get total count for progress tracking
                    var cacheData = LoadCacheData();
                    int totalFiles = 0;
                    long totalSize = 0;
                    
                    foreach (var gameData in cacheData.Games.Values)
                    {
                        totalFiles += gameData.Files.Count;
                        totalSize += gameData.TotalDownloaded;
                    }
                    
                    HoyoToonLogs.LogDebug($"Starting deletion of {totalFiles} resource files ({totalSize / (1024 * 1024)} MB)");
                    
                    int deletedFiles = 0;
                    long deletedSize = 0;
                    
                    // Delete files for each game
                    foreach (var gameConfig in HoyoToonResourceConfig.Games.Values)
                    {
                        var localGamePath = Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
                        
                        EditorUtility.DisplayProgressBar("Deleting Resources", 
                            $"Deleting {gameConfig.DisplayName} resources...", 
                            (float)deletedFiles / totalFiles);
                        
                        if (Directory.Exists(localGamePath))
                        {
                            try
                            {
                                // Get size before deletion for reporting
                                var dirInfo = new DirectoryInfo(localGamePath);
                                var dirSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                                
                                // Delete the entire game directory
                                Directory.Delete(localGamePath, true);
                                
                                deletedSize += dirSize;
                                HoyoToonLogs.LogDebug($"Deleted {gameConfig.DisplayName} directory: {localGamePath}");
                            }
                            catch (Exception ex)
                            {
                                HoyoToonLogs.ErrorDebug($"Failed to delete {gameConfig.DisplayName} directory: {ex.Message}");
                            }
                        }
                        
                        // Update progress based on game count
                        deletedFiles += cacheData.Games.ContainsKey(gameConfig.Key) 
                            ? cacheData.Games[gameConfig.Key].Files.Count 
                            : 0;
                    }
                    
                    // Clear the cache data
                    ClearCacheData();
                    
                    // Refresh Unity's asset database
                    AssetDatabase.Refresh();
                    
                    EditorUtility.ClearProgressBar();
                    
                    var deletedSizeMB = deletedSize / (1024 * 1024);
                    var message = $"Successfully deleted all resources!\n\n" +
                                 $"Files removed: {totalFiles}\n" +
                                 $"Space freed: {deletedSizeMB} MB\n\n" +
                                 $"Resources have been completely removed from disk.";
                    
                    EditorUtility.DisplayDialog("Resources Deleted", message, "OK");
                    HoyoToonLogs.LogDebug($"Resource deletion completed: {totalFiles} files, {deletedSizeMB} MB freed");
                }
                catch (Exception ex)
                {
                    EditorUtility.ClearProgressBar();
                    HoyoToonLogs.ErrorDebug($"Failed to delete resources: {ex.Message}");
                    EditorUtility.DisplayDialog("Deletion Failed", 
                        $"Failed to delete all resources: {ex.Message}\n\nCheck the console for more details.", "OK");
                }
            }
        }

        #endregion

        #region Core Resource Management

        /// <summary>
        /// Get status of all game resources
        /// </summary>
        public static Dictionary<string, ResourceStatus> GetResourceStatus()
        {
            var cacheData = LoadCacheData();
            var status = new Dictionary<string, ResourceStatus>();

            foreach (var gameConfig in HoyoToonResourceConfig.Games.Values)
            {
                var gameData = cacheData.GetOrCreateGameData(gameConfig.Key);
                var localPath = Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
                
                status[gameConfig.Key] = new ResourceStatus
                {
                    GameKey = gameConfig.Key,
                    DisplayName = gameConfig.DisplayName,
                    HasResources = Directory.Exists(localPath) && gameData.Files.Any(),
                    IsUpToDate = IsGameResourcesUpToDate(gameConfig.Key),
                    LastSync = gameData.LastSync,
                    FileCount = gameData.Files.Count,
                    TotalSize = gameData.TotalDownloaded
                };
            }

            return status;
        }

        /// <summary>
        /// Check if game resources are up to date
        /// </summary>
        private static bool IsGameResourcesUpToDate(string gameKey)
        {
            var cacheData = LoadCacheData();
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

        /// <summary>
        /// Check if game resources are up to date by comparing with server
        /// </summary>
        private static async Task<bool> IsGameResourcesUpToDateAsync(string gameKey, bool forceCheck = false)
        {
            try
            {
                var cacheData = LoadCacheData();
                var gameData = cacheData.GetOrCreateGameData(gameKey);
                var gameConfig = HoyoToonResourceConfig.Games[gameKey];
                
                // First check local file integrity
                var needUpdate = gameData.GetFilesNeedingUpdate();
                if (needUpdate.Count > 0)
                    return false;

                // Check if we need to validate against server
                if (!forceCheck && cacheData.IsUpdateCheckNeeded() == false)
                    return true;

                HoyoToonLogs.LogDebug($"Checking server for {gameConfig.DisplayName} updates...");

                // Get current file list from server
                var remoteFiles = await HoyoToonNextCloudClient.GetFileListAsync(gameConfig.WebdavUrl);
                
                // Filter out directories only, keep all files
                var serverFiles = remoteFiles.Where(f => !f.IsDirectory && 
                                                   !f.RelativePath.EndsWith("/") && 
                                                   !f.RelativePath.EndsWith("\\"))
                                             .ToDictionary(f => f.RelativePath, f => f);

                // Check for new files on server
                var newFiles = serverFiles.Keys.Where(path => !gameData.Files.ContainsKey(path)).ToList();
                if (newFiles.Any())
                {
                    HoyoToonLogs.LogDebug($"Found {newFiles.Count} new files for {gameConfig.DisplayName}");
                    return false;
                }

                // Check for updated files (different ETags)
                var updatedFiles = serverFiles.Where(kvp => 
                    gameData.Files.TryGetValue(kvp.Key, out var cachedFile) && 
                    !string.IsNullOrEmpty(kvp.Value.ETag) && 
                    cachedFile.RemoteEtag != kvp.Value.ETag
                ).ToList();
                
                if (updatedFiles.Any())
                {
                    HoyoToonLogs.LogDebug($"Found {updatedFiles.Count} updated files for {gameConfig.DisplayName}");
                    return false;
                }

                // Check for deleted files on server
                var deletedFiles = gameData.Files.Keys.Where(path => !serverFiles.ContainsKey(path)).ToList();
                if (deletedFiles.Any())
                {
                    HoyoToonLogs.LogDebug($"Found {deletedFiles.Count} deleted files for {gameConfig.DisplayName}, cleaning up...");
                    foreach (var deletedFile in deletedFiles)
                    {
                        var cachedFile = gameData.Files[deletedFile];
                        if (File.Exists(cachedFile.LocalPath))
                        {
                            File.Delete(cachedFile.LocalPath);
                        }
                        gameData.RemoveFile(deletedFile);
                    }
                    SaveCacheData();
                }

                HoyoToonLogs.LogDebug($"{gameConfig.DisplayName} is up to date");
                return true;
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Failed to check server updates for {gameKey}: {ex.Message}");
                // If we can't check the server, assume local files are good enough
                return true;
            }
        }

        /// <summary>
        /// Download resources for specified games simultaneously
        /// </summary>
        public static async Task DownloadResourcesAsync(string[] gameKeys, bool forceDownload = false)
        {
            if (gameKeys == null || gameKeys.Length == 0)
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Downloading Resources", "Initializing parallel downloads...", 0f);

                // Validate game keys
                var validGameKeys = gameKeys.Where(key => HoyoToonResourceConfig.Games.ContainsKey(key)).ToArray();
                if (validGameKeys.Length == 0)
                {
                    EditorUtility.DisplayDialog("Error", "No valid game keys specified for download.", "OK");
                    return;
                }

                var cacheData = LoadCacheData();
                
                // Create shared progress tracker for all games
                var globalProgress = new GlobalDownloadProgress
                {
                    TotalGames = validGameKeys.Length,
                    CompletedGames = 0,
                    GameProgresses = new Dictionary<string, DownloadProgress>()
                };

                // Initialize progress trackers for each game
                foreach (var gameKey in validGameKeys)
                {
                    globalProgress.GameProgresses[gameKey] = new DownloadProgress
                    {
                        GameKey = gameKey,
                        StatusMessage = "Waiting to start...",
                        TotalFiles = 0,
                        FilesCompleted = 0
                    };
                }

                // Start progress monitoring in background
                var progressTask = MonitorProgressAsync(globalProgress);

                // Start all downloads simultaneously (but with reasonable concurrency)
                var semaphore = new SemaphoreSlim(3, 3); // Limit to 3 concurrent downloads to avoid overwhelming servers
                var downloadTasks = validGameKeys.Select(async gameKey => 
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await DownloadGameResourcesWithProgressAsync(gameKey, globalProgress.GameProgresses[gameKey], forceDownload);
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

                // Wait for all downloads to complete
                var results = await Task.WhenAll(downloadTasks);

                // Stop progress monitoring
                globalProgress.IsCompleted = true;
                await progressTask;

                cacheData.MarkUpdateCheckCompleted();
                SaveCacheData();

                EditorUtility.ClearProgressBar();
                
                // Report results
                var successfulGames = results.Where(r => r.Success).ToArray();
                var failedGames = results.Where(r => !r.Success).ToArray();

                if (failedGames.Length == 0)
                {
                    string successMessage = validGameKeys.Length == 1 
                        ? $"Successfully downloaded resources for {HoyoToonResourceConfig.Games[validGameKeys[0]].DisplayName}!"
                        : $"Successfully downloaded resources for all {validGameKeys.Length} games!";
                        
                    EditorUtility.DisplayDialog("Download Complete", successMessage, "OK");
                }
                else if (successfulGames.Length > 0)
                {
                    var successfulGameNames = successfulGames.Select(g => 
                        HoyoToonResourceConfig.Games[g.GameKey].DisplayName
                    ).ToArray();
                    
                    var failedGameNames = failedGames.Select(g => 
                        $"{HoyoToonResourceConfig.Games[g.GameKey].DisplayName}: {g.Error.Message}"
                    ).ToArray();
                    
                    EditorUtility.DisplayDialog("Partial Success", 
                        $"Downloaded {successfulGames.Length}/{validGameKeys.Length} games successfully:\n" +
                        $"✓ {string.Join(", ", successfulGameNames)}\n\n" +
                        $"Failed:\n✗ {string.Join("\n✗ ", failedGameNames)}", "OK");
                }
                else
                {
                    var failedGameDetails = failedGames.Select(g => 
                        $"{HoyoToonResourceConfig.Games[g.GameKey].DisplayName}: {g.Error.Message}"
                    ).ToArray();
                    
                    EditorUtility.DisplayDialog("Download Failed", 
                        $"All downloads failed:\n✗ {string.Join("\n✗ ", failedGameDetails)}", "OK");
                }

                // Force Unity to import all downloaded assets synchronously
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                HoyoToonLogs.ErrorDebug($"Resource download failed: {ex.Message}");
                EditorUtility.DisplayDialog("Download Failed", $"Resource download failed: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Monitor overall progress across all downloads
        /// </summary>
        private static async Task MonitorProgressAsync(GlobalDownloadProgress globalProgress)
        {
            while (!globalProgress.IsCompleted)
            {
                // Update completed games count
                globalProgress.UpdateCompletedGames();
                
                var overallProgress = globalProgress.CalculateOverallProgress();
                var statusMessage = globalProgress.GetProgressSummary();

                EditorUtility.DisplayProgressBar("Downloading Resources", statusMessage, overallProgress);
                
                await Task.Delay(100); // Update UI every 100ms
            }
        }

        /// <summary>
        /// Download resources for a specific game with progress tracking
        /// </summary>
        private static async Task DownloadGameResourcesWithProgressAsync(string gameKey, DownloadProgress progress, bool forceDownload = false)
        {
            try
            {
                progress.StatusMessage = $"Scanning {HoyoToonResourceConfig.Games[gameKey].DisplayName} files...";
                await DownloadGameResourcesAsync(gameKey, progress, forceDownload);
                progress.StatusMessage = $"{HoyoToonResourceConfig.Games[gameKey].DisplayName} completed!";
            }
            catch (Exception ex)
            {
                progress.StatusMessage = $"{HoyoToonResourceConfig.Games[gameKey].DisplayName} failed: {ex.Message}";
                HoyoToonLogs.ErrorDebug($"Failed to download {HoyoToonResourceConfig.Games[gameKey].DisplayName}: {ex.Message}");
                throw; // Re-throw to be caught by Task.WhenAll
            }
        }

        /// <summary>
        /// Download resources for a specific game
        /// </summary>
        private static async Task DownloadGameResourcesAsync(string gameKey, DownloadProgress progress, bool forceDownload = false)
        {
            var gameConfig = HoyoToonResourceConfig.Games[gameKey];
            var cacheData = LoadCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            gameData.WebdavUrl = gameConfig.WebdavUrl;

            try
            {
                progress.StatusMessage = $"Connecting to {gameConfig.DisplayName} server...";
                
                // Get file list from NextCloud public share
                var remoteFiles = await HoyoToonNextCloudClient.GetFileListAsync(gameConfig.WebdavUrl);
                
                progress.StatusMessage = $"Scanning {gameConfig.DisplayName} files...";
                
                // Filter out any directories that might have slipped through
                remoteFiles = remoteFiles.Where(f => !f.IsDirectory && 
                                               !f.RelativePath.EndsWith("/") && 
                                               !f.RelativePath.EndsWith("\\")).ToList();
                
                HoyoToonLogs.LogDebug($"Found {remoteFiles.Count} files for {gameConfig.DisplayName} (all file types)");
                progress.TotalFiles = remoteFiles.Count;
                
                // Log new/updated files if not forcing download
                if (!forceDownload)
                {
                    var newFiles = remoteFiles.Where(f => !gameData.Files.ContainsKey(f.RelativePath)).ToList();
                    var updatedFiles = remoteFiles.Where(f => 
                        gameData.Files.TryGetValue(f.RelativePath, out var cached) &&
                        !string.IsNullOrEmpty(f.ETag) &&
                        cached.RemoteEtag != f.ETag).ToList();
                    
                    if (newFiles.Any())
                    {
                        HoyoToonLogs.LogDebug($"Found {newFiles.Count} new files for {gameConfig.DisplayName}");
                    }
                    
                    if (updatedFiles.Any())
                    {
                        HoyoToonLogs.LogDebug($"Found {updatedFiles.Count} updated files for {gameConfig.DisplayName}");
                    }
                }
                else
                {
                    HoyoToonLogs.LogDebug($"Force downloading all {remoteFiles.Count} files for {gameConfig.DisplayName}");
                }
                
                progress.StatusMessage = $"Starting {gameConfig.DisplayName} downloads...";

                var localBasePath = Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
                Directory.CreateDirectory(localBasePath);

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(10); // Long timeout for large files

                    foreach (var remoteFile in remoteFiles)
                    {
                        progress.CurrentFile = remoteFile.RelativePath;
                        progress.StatusMessage = $"Downloading {gameConfig.DisplayName} files...";

                        // Ensure proper file path with decoded characters
                        var cleanRelativePath = remoteFile.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                        var localPath = Path.Combine(localBasePath, cleanRelativePath);
                        var localDir = Path.GetDirectoryName(localPath);
                        
                        // Create directory if it doesn't exist
                        if (!Directory.Exists(localDir))
                        {
                            Directory.CreateDirectory(localDir);
                        }

                        // Check if file needs downloading
                        // Only download if:
                        // 1. Force download requested, OR
                        // 2. File not in cache (new file), OR  
                        // 3. File doesn't exist locally, OR
                        // 4. Source file on server has changed (different ETag)
                        // NOTE: Unity processing files differently (import settings) does NOT trigger re-download
                        var cachedFile = gameData.GetFileInfo(remoteFile.RelativePath);
                        bool shouldDownload = forceDownload ||
                                            cachedFile == null || 
                                            !cachedFile.IsValid() || 
                                            cachedFile.RemoteEtag != remoteFile.ETag;

                        if (shouldDownload)
                        {
                            // Additional validation before download
                            if (remoteFile.IsDirectory || localPath.EndsWith("\\") || localPath.EndsWith("/"))
                            {
                                HoyoToonLogs.LogDebug($"Skipping directory: {remoteFile.RelativePath}");
                                progress.FilesCompleted++;
                                continue;
                            }

                            var token = ExtractTokenFromShareUrl(gameConfig.WebdavUrl);
                            await HoyoToonNextCloudClient.DownloadFileAsync(httpClient, remoteFile.DownloadUrl, localPath, token);
                            
                            var fileInfo = new FileInfo(localPath);
                            var cachedFileInfo = new CachedFileInfo
                            {
                                RelativePath = remoteFile.RelativePath,
                                LocalPath = localPath,
                                FileSize = fileInfo.Length,
                                LastModified = fileInfo.LastWriteTime,
                                DownloadDate = DateTime.UtcNow,
                                RemoteEtag = remoteFile.ETag,
                                CachedValid = true
                            };
                            
                            cachedFileInfo.CalculateChecksum();
                            gameData.UpdateFileInfo(remoteFile.RelativePath, cachedFileInfo);
                            gameData.TotalDownloaded += fileInfo.Length;
                        }

                        progress.FilesCompleted++;
                    }
                }

                gameData.LastSync = DateTime.UtcNow;
                gameData.DownloadErrors.Clear();
                SaveCacheData();
                
                HoyoToonLogs.LogDebug($"Successfully downloaded {gameConfig.DisplayName} resources ({remoteFiles.Count} files).");
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Failed to download {gameConfig.DisplayName} resources: {ex.Message}");
                gameData.DownloadErrors.Add($"{DateTime.UtcNow}: {ex.Message}");
                SaveCacheData();
                throw;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Extract token from NextCloud share URL
        /// </summary>
        private static string ExtractTokenFromShareUrl(string shareUrl)
        {
            try
            {
                var uri = new Uri(shareUrl);
                var pathSegments = uri.AbsolutePath.Split('/');

                // Look for /s/TOKEN pattern
                for (int i = 0; i < pathSegments.Length - 1; i++)
                {
                    if (pathSegments[i] == "s" && !string.IsNullOrEmpty(pathSegments[i + 1]))
                    {
                        return pathSegments[i + 1];
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Error extracting token from URL {shareUrl}: {ex.Message}");
            }

            return null;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Clear all cache data
        /// </summary>
        public static void ClearCacheData()
        {
            lock (_cacheLock)
            {
                try
                {
                    _cacheData = new HoyoToonResourceCacheData();
                    SaveCacheData();
                    
                    // Remove cached files but keep the directory structure
                    foreach (var gameConfig in HoyoToonResourceConfig.Games.Values)
                    {
                        var localPath = Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
                        if (Directory.Exists(localPath))
                        {
                            Directory.Delete(localPath, true);
                        }
                    }
                    
                    HoyoToonLogs.LogDebug("Resource cache cleared successfully.");
                }
                catch (Exception ex)
                {
                    HoyoToonLogs.ErrorDebug($"Failed to clear resource cache: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Check if resources are available for a specific game
        /// </summary>
        public static bool HasResourcesForGame(string gameKey)
        {
            if (!HoyoToonResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
                return false;
                
            var localPath = Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
            return Directory.Exists(localPath) && Directory.GetFileSystemEntries(localPath, "*", SearchOption.AllDirectories).Length > 0;
        }

        /// <summary>
        /// Get the local path for a game's resources
        /// </summary>
        public static string GetGameResourcePath(string gameKey)
        {
            if (!HoyoToonResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
                return null;
                
            return Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
        }

        /// <summary>
        /// Validate that required resources are available and offer targeted downloads for missing ones
        /// </summary>
        public static bool ValidateResourcesAvailable()
        {
            var missingGameConfigs = new List<GameConfig>();
            
            foreach (var gameConfig in HoyoToonResourceConfig.Games.Values)
            {
                if (!HasResourcesForGame(gameConfig.Key))
                {
                    missingGameConfigs.Add(gameConfig);
                }
            }

            if (missingGameConfigs.Any())
            {
                return ShowMissingResourcesDialog(missingGameConfigs);
            }

            return true;
        }

        /// <summary>
        /// Show targeted dialog for missing resources with specific download options
        /// </summary>
        private static bool ShowMissingResourcesDialog(List<GameConfig> missingGameConfigs)
        {
            if (missingGameConfigs.Count == 1)
            {
                // Single game missing - offer specific download
                var game = missingGameConfigs[0];
                string message = $"Missing {game.DisplayName} resources are required for this operation.\n\n" +
                               $"Would you like to download {game.DisplayName} resources now?\n\n" +
                               $"Note: You only need resources for the games you're working with.";

                int result = EditorUtility.DisplayDialogComplex(
                    $"{game.DisplayName} Resources Required",
                    message,
                    $"Download {game.DisplayName}",
                    "Download All Games",
                    "Cancel"
                );

                switch (result)
                {
                    case 0: // Download specific game
                        DownloadSpecificGameAsync(game.Key);
                        return false; // User will retry after download
                    case 1: // Download all
                        DownloadAllResourcesAsync();
                        return false; // User will retry after download
                    case 2: // Cancel
                return false;
                }
            }
            else
            {
                // Multiple games missing - offer options
                var gameNames = missingGameConfigs.Select(g => g.DisplayName).ToArray();
                string gameList = string.Join("\n  • ", gameNames);
                
                string message = $"The following game resources are missing:\n\n  • {gameList}\n\n" +
                               $"You can download just what you need or get everything at once.\n\n" +
                               $"What would you like to download?";

                int result = EditorUtility.DisplayDialogComplex(
                    "Game Resources Required",
                    message,
                    "Download Missing Only",
                    "Download All Games", 
                    "Cancel"
                );

                switch (result)
                {
                    case 0: // Download only missing
                        DownloadMissingGamesAsync(missingGameConfigs.Select(g => g.Key).ToArray());
                        return false; // User will retry after download
                    case 1: // Download all
                        DownloadAllResourcesAsync();
                        return false; // User will retry after download
                    case 2: // Cancel
                        return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Download resources for a specific game (async wrapper for menu compatibility)
        /// </summary>
        private static async void DownloadSpecificGameAsync(string gameKey)
        {
            await DownloadResourcesAsync(new[] { gameKey });
        }

        /// <summary>
        /// Download resources for multiple missing games (async wrapper)
        /// </summary>
        private static async void DownloadMissingGamesAsync(string[] gameKeys)
        {
            await DownloadResourcesAsync(gameKeys);
        }

        /// <summary>
        /// Download all game resources (async wrapper for compatibility)
        /// </summary>
        private static async void DownloadAllResourcesAsync()
        {
            await DownloadResourcesAsync(HoyoToonResourceConfig.Games.Keys.ToArray());
        }

        /// <summary>
        /// Validate resources for specific games and offer targeted downloads
        /// </summary>
        public static bool ValidateResourcesForGames(params string[] gameKeys)
        {
            if (gameKeys == null || gameKeys.Length == 0)
                return ValidateResourcesAvailable(); // Fall back to checking all

            var missingGameConfigs = new List<GameConfig>();
            var gamesNeedingUpdates = new Dictionary<string, FileUpdateInfo>();
            
            foreach (string gameKey in gameKeys)
            {
                if (HoyoToonResourceConfig.Games.TryGetValue(gameKey, out var gameConfig))
                {
                    if (!HasResourcesForGame(gameKey))
                    {
                        missingGameConfigs.Add(gameConfig);
                    }
                    else
                    {
                        // Check for specific file updates needed
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
                return ShowTargetedResourceDialog(missingGameConfigs, gamesNeedingUpdates);
            }

            return true;
        }

        /// <summary>
        /// Get information about files that need updating for a specific game
        /// </summary>
        private static FileUpdateInfo GetFileUpdateInfo(string gameKey)
        {
            var cacheData = LoadCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            
            var updateInfo = new FileUpdateInfo { GameKey = gameKey };
            
            // Check for files that need updating (missing locally or validation failed)
            var needUpdate = gameData.GetFilesNeedingUpdate();
            updateInfo.MissingFiles.AddRange(needUpdate);
            
            return updateInfo;
        }

        /// <summary>
        /// Get detailed file update information by checking against server (async version)
        /// </summary>
        public static async Task<FileUpdateInfo> GetDetailedFileUpdateInfoAsync(string gameKey)
        {
            var cacheData = LoadCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            var gameConfig = HoyoToonResourceConfig.Games[gameKey];
            
            var updateInfo = new FileUpdateInfo { GameKey = gameKey };
            
            try
            {
                // Get current file list from server
                var remoteFiles = await HoyoToonNextCloudClient.GetFileListAsync(gameConfig.WebdavUrl);
                var serverFiles = remoteFiles.Where(f => !f.IsDirectory && 
                                                   !f.RelativePath.EndsWith("/") && 
                                                   !f.RelativePath.EndsWith("\\"))
                                             .ToDictionary(f => f.RelativePath, f => f);

                // Check for missing files (on server but not in cache)
                var missingFiles = serverFiles.Keys.Where(path => !gameData.Files.ContainsKey(path)).ToList();
                updateInfo.MissingFiles.AddRange(missingFiles);
                
                // Check for outdated files (different ETags)
                var outdatedFiles = serverFiles.Where(kvp => 
                    gameData.Files.TryGetValue(kvp.Key, out var cachedFile) && 
                    !string.IsNullOrEmpty(kvp.Value.ETag) && 
                    cachedFile.RemoteEtag != kvp.Value.ETag
                ).Select(kvp => kvp.Key).ToList();
                updateInfo.OutdatedFiles.AddRange(outdatedFiles);
                
                // Check for deleted files (in cache but no longer on server)
                var deletedFiles = gameData.Files.Keys.Where(path => !serverFiles.ContainsKey(path)).ToList();
                updateInfo.DeletedFiles.AddRange(deletedFiles);
                
                // Check for locally missing files (in cache but not on disk)
                var locallyMissingFiles = gameData.Files.Where(kvp => !System.IO.File.Exists(kvp.Value.LocalPath))
                                                        .Select(kvp => kvp.Key).ToList();
                updateInfo.MissingFiles.AddRange(locallyMissingFiles.Where(f => !updateInfo.MissingFiles.Contains(f)));
                
                HoyoToonLogs.LogDebug($"{gameConfig.DisplayName}: {updateInfo.MissingFiles.Count} missing, {updateInfo.OutdatedFiles.Count} outdated, {updateInfo.DeletedFiles.Count} deleted files");
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Failed to get detailed update info for {gameConfig.DisplayName}: {ex.Message}");
                // Fall back to local validation only
                var needUpdate = gameData.GetFilesNeedingUpdate();
                updateInfo.MissingFiles.AddRange(needUpdate.Where(f => !updateInfo.MissingFiles.Contains(f)));
            }
            
            return updateInfo;
        }

        /// <summary>
        /// Show dialog with granular update options
        /// </summary>
        private static bool ShowTargetedResourceDialog(List<GameConfig> missingGameConfigs, Dictionary<string, FileUpdateInfo> gamesNeedingUpdates)
        {
            // If we have missing entire games, prioritize that
            if (missingGameConfigs.Any())
            {
                return ShowMissingResourcesDialog(missingGameConfigs);
            }
            
            // Handle file-level updates
            if (gamesNeedingUpdates.Count == 1)
            {
                var kvp = gamesNeedingUpdates.First();
                var gameKey = kvp.Key;
                var updateInfo = kvp.Value;
                var gameName = HoyoToonResourceConfig.Games[gameKey].DisplayName;
                
                string message = $"{gameName} has {updateInfo.TotalChanges} files that need synchronizing.\n\n" +
                               $"Would you like to synchronize just the changed files or refresh all {gameName} resources?";

                int result = EditorUtility.DisplayDialogComplex(
                    $"{gameName} Files Need Synchronizing",
                    message,
                    "Synchronize Files",
                    $"Refresh All {gameName}",
                    "Cancel"
                );

                switch (result)
                {
                    case 0: // Synchronize files
                        SynchronizeFilesForSingleGameAsync(gameKey, updateInfo);
                        return false;
                    case 1: // Refresh entire game
                        DownloadSpecificGameAsync(gameKey);
                        return false;
                    case 2: // Cancel
                        return false;
                }
            }
            else
            {
                // Multiple games need synchronization
                var totalChanges = gamesNeedingUpdates.Values.Sum(info => info.TotalChanges);
                var gameNames = gamesNeedingUpdates.Keys.Select(k => HoyoToonResourceConfig.Games[k].DisplayName).ToArray();
                
                string message = $"Multiple games have files that need synchronizing:\n\n" +
                               $"• {string.Join("\n• ", gameNames)}\n\n" +
                               $"Total files to synchronize: {totalChanges}\n\n" +
                               $"How would you like to proceed?";

                int result = EditorUtility.DisplayDialogComplex(
                    "Resource Synchronization Available",
                    message,
                    "Synchronize Files",
                    "Refresh All Games",
                    "Cancel"
                );

                switch (result)
                {
                    case 0: // Update specific files
                        DownloadSpecificFilesForMultipleGamesAsync(gamesNeedingUpdates);
                        return false;
                    case 1: // Refresh all games
                        DownloadMissingGamesAsync(gamesNeedingUpdates.Keys.ToArray());
                        return false;
                    case 2: // Cancel
                        return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Synchronize files for a single game (downloads + deletions)
        /// </summary>
        private static async void SynchronizeFilesForSingleGameAsync(string gameKey, FileUpdateInfo updateInfo)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Synchronizing Files", $"Processing {updateInfo.TotalChanges} file changes...", 0f);
                
                var updateInfoMap = new Dictionary<string, FileUpdateInfo> { { gameKey, updateInfo } };
                await SynchronizeFilesForMultipleGamesInternalAsync(updateInfoMap);
                
                EditorUtility.ClearProgressBar();
                
                var gameName = HoyoToonResourceConfig.Games[gameKey].DisplayName;
                EditorUtility.DisplayDialog("Synchronization Complete", 
                    $"Successfully synchronized {updateInfo.TotalChanges} files for {gameName}!\n\n" +
                    $"Updated: {updateInfo.TotalFiles} files\n" +
                    $"Deleted: {updateInfo.DeletedFiles.Count} files", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                HoyoToonLogs.ErrorDebug($"Failed to synchronize files: {ex.Message}");
                EditorUtility.DisplayDialog("Synchronization Failed", 
                    $"Failed to synchronize files: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Download specific files for a single game
        /// </summary>
        private static async void DownloadSpecificFilesAsync(string gameKey, List<string> filePaths)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Updating Files", $"Downloading {filePaths.Count} files...", 0f);
                
                await DownloadSpecificFilesForGameAsync(gameKey, filePaths);
                
                EditorUtility.ClearProgressBar();
                
                var gameName = HoyoToonResourceConfig.Games[gameKey].DisplayName;
                EditorUtility.DisplayDialog("Update Complete", 
                    $"Successfully updated {filePaths.Count} files for {gameName}!", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                HoyoToonLogs.ErrorDebug($"Failed to update specific files: {ex.Message}");
                EditorUtility.DisplayDialog("Update Failed", 
                    $"Failed to update files: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Download specific files for multiple games
        /// </summary>
        private static async void DownloadSpecificFilesForMultipleGamesAsync(Dictionary<string, FileUpdateInfo> gamesNeedingUpdates)
        {
            try
            {
                var totalFiles = gamesNeedingUpdates.Values.Sum(info => info.TotalFiles);
                EditorUtility.DisplayProgressBar("Updating Files", $"Downloading {totalFiles} files across {gamesNeedingUpdates.Count} games...", 0f);
                
                foreach (var kvp in gamesNeedingUpdates)
                {
                    var allFilesToUpdate = kvp.Value.MissingFiles.Concat(kvp.Value.OutdatedFiles).ToList();
                    await DownloadSpecificFilesForGameAsync(kvp.Key, allFilesToUpdate);
                }
                
                EditorUtility.ClearProgressBar();
                
                EditorUtility.DisplayDialog("Update Complete", 
                    $"Successfully updated {totalFiles} files across {gamesNeedingUpdates.Count} games!", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                HoyoToonLogs.ErrorDebug($"Failed to update specific files: {ex.Message}");
                EditorUtility.DisplayDialog("Update Failed", 
                    $"Failed to update files: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Download specific files for a game (core implementation)
        /// </summary>
        private static async Task DownloadSpecificFilesForGameAsync(string gameKey, List<string> filePaths)
        {
            if (filePaths == null || !filePaths.Any())
                return;

            var gameConfig = HoyoToonResourceConfig.Games[gameKey];
            var cacheData = LoadCacheData();
            var gameData = cacheData.GetOrCreateGameData(gameKey);
            
            HoyoToonLogs.LogDebug($"Downloading {filePaths.Count} specific files for {gameConfig.DisplayName}");
            
            // Get full file list from server to find the files we need
            var remoteFiles = await HoyoToonNextCloudClient.GetFileListAsync(gameConfig.WebdavUrl);
            var remoteFilesDict = remoteFiles.Where(f => !f.IsDirectory)
                                            .ToDictionary(f => f.RelativePath, f => f);
            
            var localBasePath = Path.Combine(ResourcesBasePath, gameConfig.LocalPath.Replace("Resources/", ""));
            Directory.CreateDirectory(localBasePath);

            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMinutes(10);
                
                int downloadedCount = 0;
                foreach (var filePath in filePaths)
                {
                    if (remoteFilesDict.TryGetValue(filePath, out var remoteFile))
                    {
                        var cleanRelativePath = remoteFile.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                        var localPath = Path.Combine(localBasePath, cleanRelativePath);
                        var localDir = Path.GetDirectoryName(localPath);
                        
                        if (!Directory.Exists(localDir))
                        {
                            Directory.CreateDirectory(localDir);
                        }

                        var token = ExtractTokenFromShareUrl(gameConfig.WebdavUrl);
                        await HoyoToonNextCloudClient.DownloadFileAsync(httpClient, remoteFile.DownloadUrl, localPath, token);
                        
                        var fileInfo = new FileInfo(localPath);
                        var cachedFileInfo = new CachedFileInfo
                        {
                            RelativePath = remoteFile.RelativePath,
                            LocalPath = localPath,
                            FileSize = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime,
                            DownloadDate = DateTime.UtcNow,
                            RemoteEtag = remoteFile.ETag,
                            CachedValid = true
                        };
                        
                        cachedFileInfo.CalculateChecksum();
                        gameData.UpdateFileInfo(remoteFile.RelativePath, cachedFileInfo);
                        
                        downloadedCount++;
                        EditorUtility.DisplayProgressBar("Updating Files", 
                            $"Downloaded {downloadedCount}/{filePaths.Count} files...", 
                            (float)downloadedCount / filePaths.Count);
                    }
                    else
                    {
                        HoyoToonLogs.WarningDebug($"File not found on server: {filePath}");
                    }
                }
            }
            
            gameData.LastSync = DateTime.UtcNow;
            SaveCacheData();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Synchronize files for multiple games (downloads + deletions) - Async wrapper for menu compatibility
        /// </summary>
        private static async void SynchronizeFilesForMultipleGamesAsyncWrapper(Dictionary<string, FileUpdateInfo> updateInfoMap)
        {
            try
            {
                await SynchronizeFilesForMultipleGamesAsync(updateInfoMap);
                
                var totalUpdated = updateInfoMap.Values.Sum(info => info.TotalFiles);
                var totalDeleted = updateInfoMap.Values.Sum(info => info.DeletedFiles.Count);
                EditorUtility.DisplayDialog("Synchronization Complete", 
                    $"Successfully updated {totalUpdated} files and removed {totalDeleted} deleted files!", "OK");
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Failed to synchronize files: {ex.Message}");
                EditorUtility.DisplayDialog("Synchronization Failed", 
                    $"Failed to synchronize files: {ex.Message}", "OK");
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Information about a remote file
    /// </summary>
    public class RemoteFileInfo
    {
        public string RelativePath { get; set; }
        public string DownloadUrl { get; set; }
        public long Size { get; set; }
        public string ETag { get; set; }
        public bool IsDirectory { get; set; }
    }

    /// <summary>
    /// Status of game resources
    /// </summary>
    public class ResourceStatus
    {
        public string GameKey { get; set; }
        public string DisplayName { get; set; }
        public bool HasResources { get; set; }
        public bool IsUpToDate { get; set; }
        public DateTime LastSync { get; set; }
        public int FileCount { get; set; }
        public long TotalSize { get; set; }
    }

    /// <summary>
    /// Information about files that need updating for a specific game
    /// </summary>
    public class FileUpdateInfo
    {
        public string GameKey { get; set; }
        public List<string> MissingFiles { get; set; } = new List<string>();
        public List<string> OutdatedFiles { get; set; } = new List<string>();
        public List<string> DeletedFiles { get; set; } = new List<string>();
        
        public bool HasUpdates => MissingFiles.Any() || OutdatedFiles.Any();
        public bool HasDeletions => DeletedFiles.Any();
        public bool HasChanges => HasUpdates || HasDeletions;
        public int TotalFiles => MissingFiles.Count + OutdatedFiles.Count;
        public int TotalChanges => MissingFiles.Count + OutdatedFiles.Count + DeletedFiles.Count;
    }

    /// <summary>
    /// Global download progress tracker for multiple games
    /// </summary>
    public class GlobalDownloadProgress
    {
        public int TotalGames { get; set; }
        public int CompletedGames { get; set; }
        public Dictionary<string, DownloadProgress> GameProgresses { get; set; } = new Dictionary<string, DownloadProgress>();
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Get total files across all games
        /// </summary>
        public int GetTotalFiles()
        {
            return GameProgresses.Values.Sum(p => p.TotalFiles);
        }

        /// <summary>
        /// Get total completed files across all games
        /// </summary>
        public int GetTotalCompletedFiles()
        {
            return GameProgresses.Values.Sum(p => p.FilesCompleted);
        }

        /// <summary>
        /// Calculate overall progress based on total files
        /// </summary>
        public float CalculateOverallProgress()
        {
            var totalFiles = GetTotalFiles();
            if (totalFiles == 0) return 0f;

            var completedFiles = GetTotalCompletedFiles();
            return (float)completedFiles / totalFiles;
        }

        /// <summary>
        /// Get progress summary string
        /// </summary>
        public string GetProgressSummary()
        {
            var totalFiles = GetTotalFiles();
            var completedFiles = GetTotalCompletedFiles();
            var progressPercent = totalFiles > 0 ? (int)(((float)completedFiles / totalFiles) * 100) : 0;
            
            if (totalFiles == 0)
            {
                // Check if we're still scanning files
                var hasAnyProgress = GameProgresses.Values.Any(p => !string.IsNullOrEmpty(p.StatusMessage));
                if (hasAnyProgress)
                {
                    var activeMessages = GameProgresses.Values
                        .Where(p => !string.IsNullOrEmpty(p.StatusMessage) && p.TotalFiles == 0)
                        .Select(p => p.StatusMessage)
                        .Take(2)
                        .ToArray();
                    
                    if (activeMessages.Any())
                    {
                        return activeMessages.Length == 1 
                            ? activeMessages[0] 
                            : $"{activeMessages[0]} +{GameProgresses.Count - 1} more...";
                    }
                }
                
                return "Initializing downloads...";
            }
            
            return $"Downloading resources... {completedFiles:N0}/{totalFiles:N0} files ({progressPercent}%)";
        }

        /// <summary>
        /// Update completed games count
        /// </summary>
        public void UpdateCompletedGames()
        {
            CompletedGames = GameProgresses.Values.Count(p => p.TotalFiles > 0 && p.FilesCompleted >= p.TotalFiles);
        }

        /// <summary>
        /// Check if all file counts have been determined
        /// </summary>
        public bool AreFileCountsReady()
        {
            return GameProgresses.Values.All(p => p.TotalFiles > 0);
        }
    }

    #endregion
}
#endif