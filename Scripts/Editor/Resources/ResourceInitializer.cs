#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.Onboarding;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.ResourceSystem
{
    [InitializeOnLoad]
    public static class ResourceInitializer
    {
        #region Initialization

        static ResourceInitializer()
        {
            // Ensure notifications are enabled by default for new installations
            if (!EditorPrefs.HasKey(PrefsKeys.ResourceSuppressNotifications))
            {
                EditorPrefs.SetBool(PrefsKeys.ResourceSuppressNotifications, false);
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Resource notifications enabled by default for new installation.");
            }
            
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            try
            {
                var startupPolicy = OnboardingStartupPolicy.Evaluate();
                
                if (startupPolicy.IsFirstTimeSetup)
                {
                    bool startupEntryAcquired = startupPolicy.CanOnboardingOwnStartup && GuidedTourController.TryAcquireStartupEntryForSession("ResourceInitializer");
                    string decision = startupEntryAcquired
                        ? "start-tour"
                        : startupPolicy.CanOnboardingOwnStartup
                            ? "skip-guard-denied"
                            : startupPolicy.HasSeenTour
                                ? "skip-tour-seen"
                                : "skip-batch-mode";

                    HoyoToonLogger.Log(
                        HoyoToonLogger.Categories.Tour,
                        LogLevel.Info,
                        $"OnboardingDiag event=startup-source source=ResourceInitializer firstTime={startupPolicy.IsFirstTimeSetup} seenTour={startupPolicy.HasSeenTour} batchMode={startupPolicy.IsBatchMode} guardAcquired={startupEntryAcquired} decision={decision}");

                    if (startupEntryAcquired)
                    {
                        GuidedTourController.MarkTourSeen();
                        GuidedTourController.StartTourAtStep(GuidedTourController.StepIds.FirstTime);
                        GuidedTourWindow.EnsureWindowVisible(true);
                    }

                    if (GetMissingResources().Any())
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "First-time resources missing; defer to onboarding.");
                    }

                    CompleteFirstTimeSetup();
                }
                else
                {
                    HoyoToonLogger.Log(
                        HoyoToonLogger.Categories.Tour,
                        LogLevel.Info,
                        "OnboardingDiag event=startup-source source=ResourceInitializer firstTime=false decision=skip-not-first-time");
                    CheckForUpdatesIfNeeded();
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Error during resource initialization: {ex.Message}");
            }
        }

        #endregion

        #region First Time Setup

        private static void CompleteFirstTimeSetup()
        {
            PrefsKeys.SetResourceFirstTimeSetupCompleted(true);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "First-time setup completed.");
        }

        #endregion

        #region Auto Update Checking

        private static void CheckForUpdatesIfNeeded()
        {
            if (EditorPrefs.GetBool(PrefsKeys.ResourceSuppressNotifications, false))
                return;

            var cacheData = ResourceCacheService.GetCacheData();
            
            if (!cacheData.IsUpdateCheckNeeded())
                return;

            AsyncUtil.RunFireAndForget(() => PerformBackgroundUpdateCheck(default), "Background resource update check");
        }

        private static async Task PerformBackgroundUpdateCheck(CancellationToken cancellationToken = default)
        {
            try
            {
            cancellationToken.ThrowIfCancellationRequested();
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Starting background update check...");
                
                var gameKeys = ResourceConfig.Games.Keys.ToArray();
                var updateInfoMap = new Dictionary<string, FileUpdateInfo>();
                var missingGames = new List<string>();

                // Actually check against server instead of using time-based assumptions
                for (int i = 0; i < gameKeys.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var gameKey = gameKeys[i];
                    var gameName = ResourceConfig.Games[gameKey].DisplayName;
                    
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Background check: Analyzing {gameName}...");

                                        if (!ResourceValidation.HasResourcesForGame(gameKey))
                    {
                        missingGames.Add(gameKey);
                    }
                    else
                    {
                                            var updateInfo = await ResourceValidation.GetDetailedFileUpdateInfoAsync(gameKey, cancellationToken);
                        if (updateInfo.HasChanges)
                        {
                            updateInfoMap[gameKey] = updateInfo;
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Background check: {gameName} has {updateInfo.TotalChanges} changes");
                        }
                        else
                        {
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Background check: {gameName} is up to date");
                        }
                    }

                }

                if (missingGames.Any() || updateInfoMap.Any())
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, $"Background check found issues: {missingGames.Count} missing games, {updateInfoMap.Count} games with file changes");
                    ShowUpdateNotificationDialog(updateInfoMap, missingGames);
                }
                else
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Background check completed: All resources are up to date");
                }
            }
            catch (OperationCanceledException)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Background update check cancelled.");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to perform background update check: {ex.Message}");
            }
            finally
            {
                var cacheData = ResourceCacheService.GetCacheData();
                cacheData.MarkUpdateCheckCompleted();
                ResourceCacheService.SaveCacheData();
            }
        }

        private static void ShowUpdateNotificationDialog(
            Dictionary<string, FileUpdateInfo> updateInfoMap,
            List<string> missingGames)
        {
            var message = "HoyoToon Asset Updates Available\n\n";

            if (missingGames.Any())
            {
                message += "Missing Resources:\n";
                foreach (var gameKey in missingGames)
                {
                    var displayName = ResourceConfig.Games[gameKey].DisplayName;
                    message += $"  - {displayName}\n";
                }
                message += "\n";
            }

            if (updateInfoMap.Any())
            {
                message += "Resources with Updates:\n";
                foreach (var kvp in updateInfoMap)
                {
                    var displayName = ResourceConfig.Games[kvp.Key].DisplayName;
                    var updateInfo = kvp.Value;
                    
                    var details = new List<string>();
                    if (updateInfo.MissingFiles.Count > 0)
                        details.Add($"{updateInfo.MissingFiles.Count} missing");
                    if (updateInfo.OutdatedFiles.Count > 0)
                        details.Add($"{updateInfo.OutdatedFiles.Count} outdated");
                    if (updateInfo.DeletedFiles.Count > 0)
                        details.Add($"{updateInfo.DeletedFiles.Count} deleted");
                    
                    var detailText = details.Any() ? $" ({string.Join(", ", details)})" : "";
                    message += $"  - {displayName}{detailText}\n";
                }
                message += "\n";
            }

            message += "Would you like to update your assets now?";

            DialogWindow.ShowCustom(
                "Asset Updates Available",
                message,
                MessageType.Info,
                new[] { "Update Now", "Later", "Don't Show Again" },
                defaultIndex: 0,
                cancelIndex: 1,
                onResultIndex: result =>
                {
                    switch (result)
                    {
                        case 0:
                            AsyncUtil.RunFireAndForget(async () =>
                            {
                                using var cts = new CancellationTokenSource();
                                await UpdateAssetsNow(updateInfoMap, missingGames, cts.Token);
                            }, "Update assets now");
                            break;
                        case 1:
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "User chose to update assets later.");
                            break;
                        case 2:
                            EditorPrefs.SetBool(PrefsKeys.ResourceSuppressNotifications, true);
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "User chose to suppress update notifications.");
                            break;
                    }
                });
        }

        private static async Task UpdateAssetsNow(
            Dictionary<string, FileUpdateInfo> updateInfoMap,
            List<string> missingGames,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (missingGames.Any())
                {
                    await ResourceDownloadService.DownloadResourcesAsync(missingGames.ToArray(), cancellationToken: cancellationToken);
                }
                
                if (updateInfoMap.Any())
                {
                    await ResourceDownloadService.SynchronizeFilesForMultipleGamesAsync(updateInfoMap, cancellationToken);
                }
                
                DialogWindow.ShowInfo("Update Complete", "Asset update completed successfully!");
            }
            catch (OperationCanceledException)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Asset update cancelled by user.");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Error, $"Failed to update assets: {ex.Message}");
                DialogWindow.ShowError("Update Failed", $"Failed to update assets: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        private static string[] GetMissingResources()
        {
            var status = ResourceValidation.GetResourceStatus();
            return status.Where(kvp => !kvp.Value.HasResources)
                        .Select(kvp => kvp.Value.DisplayName)
                        .ToArray();
        }

        

        public static void ResetFirstTimeSetup()
        {
            DialogWindow.ShowOkCancel("Reset First-Time Setup",
                "This will reset the first-time setup and allow onboarding checks to run again next time Unity starts. Continue?",
                MessageType.Warning,
                onResult: ok =>
            {
                if (!ok) return;

                PrefsKeys.ClearResourceFirstTimeSetup();
                EditorPrefs.DeleteKey(PrefsKeys.ResourceSuppressNotifications);

                DialogWindow.ShowInfo("Reset Complete", "First-time setup has been reset.");

                HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "First-time setup reset completed.");
            });
        }

        public static void EnableResourceNotifications()
        {
            EditorPrefs.SetBool(PrefsKeys.ResourceSuppressNotifications, false);
            DialogWindow.ShowInfo("Notifications Enabled", "Resource update notifications have been enabled.");
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Resource notifications enabled.");
        }

        public static bool ValidateEnableResourceNotifications()
        {
            return EditorPrefs.GetBool(PrefsKeys.ResourceSuppressNotifications, false);
        }

        public static void DisableResourceNotifications()
        {
            EditorPrefs.SetBool(PrefsKeys.ResourceSuppressNotifications, true);
            DialogWindow.ShowInfo("Notifications Disabled", "Resource update notifications have been disabled.");
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Resources, LogLevel.Info, "Resource notifications disabled.");
        }

        public static bool ValidateDisableResourceNotifications()
        {
            return !EditorPrefs.GetBool(PrefsKeys.ResourceSuppressNotifications, false);
        }

        #endregion
    }
}
#endif
