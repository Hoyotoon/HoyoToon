#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

namespace HoyoToon
{
    /// <summary>
    /// Handles automatic resource checking and first-time setup notifications
    /// </summary>
    [InitializeOnLoad]
    public static class HoyoToonResourceInitializer
    {
        #region Constants

        private static readonly string FirstTimeSetupKey = "HoyoToon_FirstTimeSetup_Completed";
        private static readonly string LastUpdateCheckKey = "HoyoToon_LastUpdateCheck";
        private static readonly string SuppressNotificationsKey = "HoyoToon_SuppressResourceNotifications";

        #endregion

        #region Initialization

        static HoyoToonResourceInitializer()
        {
            // Ensure notifications are enabled by default for new installations
            if (!EditorPrefs.HasKey(SuppressNotificationsKey))
            {
                EditorPrefs.SetBool(SuppressNotificationsKey, false);
                HoyoToonLogs.LogDebug("Resource notifications enabled by default for new installation.");
            }
            
            EditorApplication.delayCall += OnEditorReady;
        }

        /// <summary>
        /// Called when the editor is ready
        /// </summary>
        private static void OnEditorReady()
        {
            try
            {
                // Check if this is the first time setup
                bool isFirstTime = !EditorPrefs.GetBool(FirstTimeSetupKey, false);
                
                if (isFirstTime)
                {
                    HandleFirstTimeSetup();
                }
                else
                {
                    // Check for updates if enough time has passed
                    CheckForUpdatesIfNeeded();
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Error during resource initialization: {ex.Message}");
            }
        }

        #endregion

        #region First Time Setup

        /// <summary>
        /// Handle first-time setup and resource download prompt
        /// </summary>
        private static void HandleFirstTimeSetup()
        {
            var missingResources = GetMissingResources();
            
            if (missingResources.Any())
            {
                ShowFirstTimeSetupDialog(missingResources);
            }
            else
            {
                // Resources are already available, mark first time setup as complete
                CompleteFirstTimeSetup();
            }
        }

        /// <summary>
        /// Show the first-time setup dialog
        /// </summary>
        private static void ShowFirstTimeSetupDialog(string[] missingResources)
        {
            string resourceList = string.Join("\n  • ", missingResources);
            string message = $"Welcome to HoyoToon!\n\n" +
                           $"To get started, you need to download the required game resources:\n\n" +
                           $"Missing Resources:\n  • {resourceList}\n\n" +
                           $"These resources contain textures, materials, and other assets needed for HoyoToon to function properly.\n\n" +
                           $"Would you like to download them now?";

            int result = EditorUtility.DisplayDialogComplex(
                "HoyoToon First Time Setup",
                message,
                "Download Now",
                "Skip for Now",
                "Don't Show Again"
            );

            switch (result)
            {
                case 0: // Download Now
                    DownloadAllResourcesAndCompleteSetup();
                    break;
                case 1: // Skip for Now
                    HoyoToonLogs.LogDebug("User chose to skip resource download for now.");
                    break;
                case 2: // Don't Show Again
                    CompleteFirstTimeSetup();
                    EditorPrefs.SetBool(SuppressNotificationsKey, true);
                    HoyoToonLogs.LogDebug("User chose to suppress resource notifications.");
                    break;
            }
        }

        /// <summary>
        /// Download all resources and complete first-time setup
        /// </summary>
        private static async void DownloadAllResourcesAndCompleteSetup()
        {
            try
            {
                var gameKeys = HoyoToonResourceConfig.Games.Keys.ToArray();
                await HoyoToonResourceManager.DownloadResourcesAsync(gameKeys);
                CompleteFirstTimeSetup();
                
                EditorUtility.DisplayDialog(
                    "Setup Complete", 
                    "HoyoToon setup is complete! All required resources have been downloaded.", 
                    "OK"
                );
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Failed to download resources during first-time setup: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "Download Failed", 
                    $"Failed to download resources: {ex.Message}\n\nYou can try again later using the menu: HoyoToon > Resources > Download All Resources", 
                    "OK"
                );
            }
        }

        /// <summary>
        /// Mark first-time setup as completed
        /// </summary>
        private static void CompleteFirstTimeSetup()
        {
            EditorPrefs.SetBool(FirstTimeSetupKey, true);
            EditorPrefs.SetString(LastUpdateCheckKey, DateTime.UtcNow.ToBinary().ToString());
            
            // Ensure notifications are enabled by default after first-time setup
            // Only set if the user hasn't already made a choice about notifications
            if (!EditorPrefs.HasKey(SuppressNotificationsKey))
            {
                EditorPrefs.SetBool(SuppressNotificationsKey, false);
                HoyoToonLogs.LogDebug("Resource notifications enabled by default after setup completion.");
            }
            
            HoyoToonLogs.LogDebug("First-time setup completed.");
        }

        #endregion

        #region Auto Update Checking

        /// <summary>
        /// Check for updates if enough time has passed since last check
        /// </summary>
        private static void CheckForUpdatesIfNeeded()
        {
            // Skip if notifications are suppressed
            if (EditorPrefs.GetBool(SuppressNotificationsKey, false))
                return;

            var cacheData = HoyoToonResourceManager.GetCacheData();
            
            // Check if it's time for an update check
            if (!cacheData.IsUpdateCheckNeeded())
                return;

            // Perform background update check
            PerformBackgroundUpdateCheck();
        }

        /// <summary>
        /// Perform background update check
        /// </summary>
        private static async void PerformBackgroundUpdateCheck()
        {
            try
            {
                HoyoToonLogs.LogDebug("Starting background update check...");
                
                var gameKeys = HoyoToonResourceConfig.Games.Keys.ToArray();
                var updateInfoMap = new Dictionary<string, FileUpdateInfo>();
                var missingGames = new List<string>();

                // Actually check against server instead of using time-based assumptions
                for (int i = 0; i < gameKeys.Length; i++)
                {
                    var gameKey = gameKeys[i];
                    var gameName = HoyoToonResourceConfig.Games[gameKey].DisplayName;
                    
                    HoyoToonLogs.LogDebug($"Background check: Analyzing {gameName}...");

                    // Check if game resources exist at all
                    if (!HoyoToonResourceManager.HasResourcesForGame(gameKey))
                    {
                        missingGames.Add(gameKey);
                    }
                    else
                    {
                        // Get detailed file-level information by checking server
                        var updateInfo = await HoyoToonResourceManager.GetDetailedFileUpdateInfoAsync(gameKey);
                        if (updateInfo.HasChanges)
                        {
                            updateInfoMap[gameKey] = updateInfo;
                            HoyoToonLogs.LogDebug($"Background check: {gameName} has {updateInfo.TotalChanges} changes");
                        }
                        else
                        {
                            HoyoToonLogs.LogDebug($"Background check: {gameName} is up to date");
                        }
                    }
                }

                // Mark that we've done a recent update check regardless of results
                var cacheData = HoyoToonResourceManager.GetCacheData();
                cacheData.MarkUpdateCheckCompleted();
                HoyoToonResourceManager.SaveCacheData();

                // Only show dialog if there are actual changes needed
                if (missingGames.Any() || updateInfoMap.Any())
                {
                    HoyoToonLogs.LogDebug($"Background check found issues: {missingGames.Count} missing games, {updateInfoMap.Count} games with file changes");
                    ShowUpdateNotificationDialog(updateInfoMap, missingGames);
                }
                else
                {
                    HoyoToonLogs.LogDebug("Background check completed: All resources are up to date");
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Failed to perform background update check: {ex.Message}");
                
                // Even if the check failed, mark it as completed to avoid spamming on every startup
                var cacheData = HoyoToonResourceManager.GetCacheData();
                cacheData.MarkUpdateCheckCompleted();
                HoyoToonResourceManager.SaveCacheData();
            }
        }

        /// <summary>
        /// Show update notification dialog
        /// </summary>
        private static void ShowUpdateNotificationDialog(
            Dictionary<string, FileUpdateInfo> updateInfoMap,
            List<string> missingGames)
        {
            var message = "HoyoToon Resource Updates Available\n\n";

            if (missingGames.Any())
            {
                message += "Missing Resources:\n";
                foreach (var gameKey in missingGames)
                {
                    var displayName = HoyoToonResourceConfig.Games[gameKey].DisplayName;
                    message += $"  • {displayName}\n";
                }
                message += "\n";
            }

            if (updateInfoMap.Any())
            {
                message += "Resources with Updates:\n";
                foreach (var kvp in updateInfoMap)
                {
                    var displayName = HoyoToonResourceConfig.Games[kvp.Key].DisplayName;
                    var updateInfo = kvp.Value;
                    
                    var details = new List<string>();
                    if (updateInfo.MissingFiles.Count > 0)
                        details.Add($"{updateInfo.MissingFiles.Count} missing");
                    if (updateInfo.OutdatedFiles.Count > 0)
                        details.Add($"{updateInfo.OutdatedFiles.Count} outdated");
                    if (updateInfo.DeletedFiles.Count > 0)
                        details.Add($"{updateInfo.DeletedFiles.Count} deleted");
                    
                    var detailText = details.Any() ? $" ({string.Join(", ", details)})" : "";
                    message += $"  • {displayName}{detailText}\n";
                }
                message += "\n";
            }

            message += "Would you like to update your resources now?";

            int result = EditorUtility.DisplayDialogComplex(
                "Resource Updates Available",
                message,
                "Update Now",
                "Later",
                "Don't Show Again"
            );

            switch (result)
            {
                case 0: // Update Now
                    UpdateResourcesNow(updateInfoMap, missingGames);
                    break;
                case 1: // Later
                    HoyoToonLogs.LogDebug("User chose to update resources later.");
                    break;
                case 2: // Don't Show Again
                    EditorPrefs.SetBool(SuppressNotificationsKey, true);
                    HoyoToonLogs.LogDebug("User chose to suppress update notifications.");
                    break;
            }
        }

        /// <summary>
        /// Update resources immediately
        /// </summary>
        private static async void UpdateResourcesNow(
            Dictionary<string, FileUpdateInfo> updateInfoMap,
            List<string> missingGames)
        {
            try
            {
                // Download missing games first
                if (missingGames.Any())
                {
                    await HoyoToonResourceManager.DownloadResourcesAsync(missingGames.ToArray());
                }
                
                // Synchronize specific file changes
                if (updateInfoMap.Any())
                {
                    await HoyoToonResourceManager.SynchronizeFilesForMultipleGamesAsync(updateInfoMap);
                }
                
                EditorUtility.DisplayDialog(
                    "Update Complete", 
                    "Resource update completed successfully!", 
                    "OK"
                );
            }
            catch (Exception ex)
            {
                HoyoToonLogs.ErrorDebug($"Failed to update resources: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "Update Failed", 
                    $"Failed to update resources: {ex.Message}", 
                    "OK"
                );
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get list of missing resources
        /// </summary>
        private static string[] GetMissingResources()
        {
            var status = HoyoToonResourceManager.GetResourceStatus();
            return status.Where(kvp => !kvp.Value.HasResources)
                        .Select(kvp => kvp.Value.DisplayName)
                        .ToArray();
        }

        /// <summary>
        /// Reset first-time setup (for debugging)
        /// </summary>
        //[MenuItem("HoyoToon/Resources/Reset First-Time Setup", priority = 30)]
        public static void ResetFirstTimeSetup()
        {
            if (EditorUtility.DisplayDialog("Reset First-Time Setup", 
                "This will reset the first-time setup and show the welcome dialog again next time Unity starts. Continue?", 
                "Yes", "Cancel"))
            {
                EditorPrefs.DeleteKey(FirstTimeSetupKey);
                EditorPrefs.DeleteKey(LastUpdateCheckKey);
                EditorPrefs.DeleteKey(SuppressNotificationsKey);
                
                EditorUtility.DisplayDialog("Reset Complete", 
                    "First-time setup has been reset. The welcome dialog will appear next time Unity starts.", 
                    "OK");
                    
                HoyoToonLogs.LogDebug("First-time setup reset completed.");
            }
        }

        /// <summary>
        /// Enable resource notifications
        /// </summary>
        [MenuItem("HoyoToon/Resources/Enable Notifications", priority = 25)]
        public static void EnableResourceNotifications()
        {
            EditorPrefs.SetBool(SuppressNotificationsKey, false);
            EditorUtility.DisplayDialog("Notifications Enabled", 
                "Resource update notifications have been enabled.", 
                "OK");
            HoyoToonLogs.LogDebug("Resource notifications enabled.");
        }

        /// <summary>
        /// Validate enable notifications menu item
        /// </summary>
        [MenuItem("HoyoToon/Resources/Enable Notifications", true, priority = 25)]
        public static bool ValidateEnableResourceNotifications()
        {
            // Only show if notifications are currently disabled
            return EditorPrefs.GetBool(SuppressNotificationsKey, false);
        }

        /// <summary>
        /// Disable resource notifications
        /// </summary>
        [MenuItem("HoyoToon/Resources/Disable Notifications", priority = 26)]
        public static void DisableResourceNotifications()
        {
            EditorPrefs.SetBool(SuppressNotificationsKey, true);
            EditorUtility.DisplayDialog("Notifications Disabled", 
                "Resource update notifications have been disabled.", 
                "OK");
            HoyoToonLogs.LogDebug("Resource notifications disabled.");
        }

        /// <summary>
        /// Validate disable notifications menu item
        /// </summary>
        [MenuItem("HoyoToon/Resources/Disable Notifications", true, priority = 26)]
        public static bool ValidateDisableResourceNotifications()
        {
            // Only show if notifications are currently enabled
            return !EditorPrefs.GetBool(SuppressNotificationsKey, false);
        }

        #endregion
    }
}
#endif