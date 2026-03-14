#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using HoyoToon.Editor.ResourceSystem;
using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.UI.ResourcesUI
{
    internal static class ResourceDialogBuilder
    {
        public static void ShowResourceStatusDialog(
            Dictionary<string, FileUpdateInfo> updateInfoMap,
            List<string> missingGames,
            List<string> checkedGames,
            List<string> misconfiguredGames)
        {
            checkedGames ??= new List<string>();
            misconfiguredGames ??= new List<string>();

            var upToDateGames = checkedGames
                .Where(key => !missingGames.Contains(key) && !updateInfoMap.ContainsKey(key))
                .ToList();

            StringBuilder message = new StringBuilder();
            message.AppendLine("HoyoToon Resource Analysis:");
            message.AppendLine();

            if (!checkedGames.Any())
            {
                message.AppendLine("No games are configured for resource checks.");
                if (misconfiguredGames.Any())
                {
                    message.AppendLine();
                    message.AppendLine("Skipped (Missing Local Path):");
                    foreach (var gameKey in misconfiguredGames)
                    {
                        message.AppendLine($"  - {ResourceConfig.Games[gameKey].DisplayName}");
                    }
                }
                DialogWindow.ShowInfo("Resource Configuration Required", message.ToString());
                return;
            }

            if (upToDateGames.Any())
            {
                message.AppendLine("Up to Date:");
                foreach (var gameKey in upToDateGames)
                {
                    message.AppendLine($"  - {ResourceConfig.Games[gameKey].DisplayName}");
                }
                message.AppendLine();
            }

            if (missingGames.Any())
            {
                message.AppendLine("Missing Games:");
                foreach (var gameKey in missingGames)
                {
                    message.AppendLine($"  - {ResourceConfig.Games[gameKey].DisplayName}");
                }
                message.AppendLine();
            }

            if (updateInfoMap.Any())
            {
                message.AppendLine("Files Need Synchronizing:");
                foreach (var kvp in updateInfoMap)
                {
                    var gameName = ResourceConfig.Games[kvp.Key].DisplayName;
                    var updateInfo = kvp.Value;

                    var details = new List<string>();
                    if (updateInfo.MissingFiles.Count > 0)
                        details.Add($"{updateInfo.MissingFiles.Count} missing");
                    if (updateInfo.OutdatedFiles.Count > 0)
                        details.Add($"{updateInfo.OutdatedFiles.Count} outdated");
                    if (updateInfo.DeletedFiles.Count > 0)
                        details.Add($"{updateInfo.DeletedFiles.Count} deleted");

                    var detailText = details.Any() ? $" ({string.Join(", ", details)})" : "";
                    message.AppendLine($"  - {gameName}{detailText}");

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

            if (misconfiguredGames.Any())
            {
                message.AppendLine("Skipped (Missing Local Path):");
                foreach (var gameKey in misconfiguredGames)
                {
                    message.AppendLine($"  - {ResourceConfig.Games[gameKey].DisplayName}");
                }
                message.AppendLine();
            }

            bool hasAnyUpdates = missingGames.Any() || updateInfoMap.Any();

            if (!hasAnyUpdates)
            {
                if (misconfiguredGames.Any())
                {
                    message.AppendLine("Configured games are up to date.");
                    DialogWindow.ShowInfo("Resource Check Complete", message.ToString());
                }
                else
                {
                    message.AppendLine("All resources are up to date!");
                    DialogWindow.ShowInfo("Resources Up to Date", message.ToString());
                }
                return;
            }

            message.AppendLine("What would you like to do?");

            string title = "Resource Synchronization Available";
            string option1, option2, option3;

            if (missingGames.Any() && updateInfoMap.Any())
            {
                option1 = "Download & Sync All";
                option2 = "Synchronize Files";
                option3 = "Cancel";
            }
            else if (missingGames.Any())
            {
                option1 = "Download Missing Games";
                option2 = "Download All Resources";
                option3 = "Cancel";
            }
            else
            {
                option1 = "Synchronize Files";
                option2 = "Refresh All Games";
                option3 = "Cancel";
            }

            string[] buttons = { option1, option2, option3 };
            DialogWindow.ShowCustom(title, message.ToString(), MessageType.Info, buttons, 0, 2, result =>
            {
                switch (result)
                {
                    case 0:
                        if (missingGames.Any() && updateInfoMap.Any())
                        {
                            ResourceOperationRunner.RunCancelableResourceOperation(
                                "Resource sync (missing + changes)",
                                token => ResourceDownloadService.DownloadMissingResourcesAndSynchronizeAsync(missingGames, updateInfoMap, token));
                        }
                        else if (missingGames.Any())
                        {
                            ResourceOperationRunner.RunCancelableResourceOperation(
                                "Download missing game resources",
                                token => ResourceDownloadService.DownloadResourcesAsync(missingGames.ToArray(), cancellationToken: token));
                        }
                        else
                        {
                            if (updateInfoMap.Any())
                            {
                                ResourceOperationRunner.RunCancelableResourceOperation(
                                    "Synchronize files (multiple games)",
                                    token => ResourceDownloadService.SynchronizeFilesForMultipleGamesAsync(updateInfoMap, token));
                            }
                        }
                        break;
                    case 1:
                        if (missingGames.Any() && !updateInfoMap.Any())
                        {
                            ResourceMenuItems.DownloadAllResources();
                        }
                        else
                        {
                            var gamesToRefresh = updateInfoMap.Keys.Concat(missingGames).Distinct().ToArray();
                            ResourceOperationRunner.RunCancelableResourceOperation(
                                "Refresh resources (games)",
                                token => ResourceDownloadService.DownloadResourcesAsync(gamesToRefresh, cancellationToken: token));
                        }
                        break;
                    case 2:
                        break;
                }
            });
        }

        public static bool ShowMissingResourcesDialog(List<GameConfig> missingGameConfigs)
        {
            if (missingGameConfigs.Count == 1)
            {
                var game = missingGameConfigs[0];
                string msg = $"Missing {game.DisplayName} resources are required for this operation.\n\n" +
                             $"Would you like to download {game.DisplayName} resources now?\n\n" +
                             $"Note: You only need resources for the games you're working with.";

                DialogWindow.ShowCustom(
                    $"{game.DisplayName} Resources Required",
                    msg,
                    MessageType.Info,
                    new[] { $"Download {game.DisplayName}", "Download All Games", "Cancel" },
                    defaultIndex: 0,
                    cancelIndex: 2,
                    onResultIndex: result =>
                    {
                        switch (result)
                        {
                            case 0:
                                ResourceOperationRunner.RunCancelableResourceOperation(
                                    "Download specific game resources",
                                    token => ResourceDownloadService.DownloadResourcesAsync(new[] { game.Key }, cancellationToken: token));
                                break;
                            case 1:
                                ResourceMenuItems.DownloadAllResources();
                                break;
                        }
                    }
                );
                return false;
            }
            else
            {
                var gameNames = missingGameConfigs.Select(g => g.DisplayName).ToArray();
                string gameList = string.Join("\n  - ", gameNames);

                string msg = $"The following game resources are missing:\n\n  - {gameList}\n\n" +
                             $"You can download just what you need or get everything at once.\n\n" +
                             $"What would you like to download?";

                DialogWindow.ShowCustom(
                    "Game Resources Required",
                    msg,
                    MessageType.Info,
                    new[] { "Download Missing Only", "Download All Games", "Cancel" },
                    defaultIndex: 0,
                    cancelIndex: 2,
                    onResultIndex: result =>
                    {
                        switch (result)
                        {
                            case 0:
                                ResourceOperationRunner.RunCancelableResourceOperation(
                                    "Download missing game resources",
                                    token => ResourceDownloadService.DownloadResourcesAsync(missingGameConfigs.Select(g => g.Key).ToArray(), cancellationToken: token));
                                break;
                            case 1:
                                ResourceMenuItems.DownloadAllResources();
                                break;
                        }
                    }
                );
                return false;
            }
        }

        public static bool ShowTargetedResourceDialog(List<GameConfig> missingGameConfigs, Dictionary<string, FileUpdateInfo> gamesNeedingUpdates)
        {
            // If we have missing entire games, prioritize that
            if (missingGameConfigs.Any())
            {
                return ShowMissingResourcesDialog(missingGameConfigs);
            }

            if (gamesNeedingUpdates.Count == 1)
            {
                var kvp = gamesNeedingUpdates.First();
                var gameKey = kvp.Key;
                var updateInfo = kvp.Value;
                var gameName = ResourceConfig.Games[gameKey].DisplayName;

                string msg = $"{gameName} has {updateInfo.TotalChanges} files that need synchronizing.\n\n" +
                             $"Would you like to synchronize just the changed files or refresh all {gameName} resources?";

                DialogWindow.ShowCustom(
                    $"{gameName} Files Need Synchronizing",
                    msg,
                    MessageType.Info,
                    new[] { "Synchronize Files", $"Refresh All {gameName}", "Cancel" },
                    defaultIndex: 0,
                    cancelIndex: 2,
                    onResultIndex: result =>
                    {
                        switch (result)
                        {
                            case 0:
                                ResourceOperationRunner.RunCancelableResourceOperation(
                                    "Synchronize files (single game)",
                                    token => ResourceDownloadService.SynchronizeFilesForSingleGameAsync(gameKey, updateInfo, token));
                                break;
                            case 1:
                                ResourceOperationRunner.RunCancelableResourceOperation(
                                    "Refresh resources (single game)",
                                    token => ResourceDownloadService.DownloadResourcesAsync(new[] { gameKey }, cancellationToken: token));
                                break;
                        }
                    }
                );
                return false;
            }
            else
            {
                var totalChanges = gamesNeedingUpdates.Values.Sum(info => info.TotalChanges);
                var gameNames = gamesNeedingUpdates.Keys.Select(k => ResourceConfig.Games[k].DisplayName).ToArray();

                string msg = $"Multiple games have files that need synchronizing:\n\n" +
                             $"- {string.Join("\n- ", gameNames)}\n\n" +
                             $"Total files to synchronize: {totalChanges}\n\n" +
                             $"How would you like to proceed?";
                DialogWindow.ShowCustom(
                    "Resource Synchronization Available",
                    msg,
                    MessageType.Info,
                    new[] { "Synchronize Files", "Refresh All Games", "Cancel" },
                    defaultIndex: 0,
                    cancelIndex: 2,
                    onResultIndex: result =>
                    {
                        switch (result)
                        {
                            case 0:
                                ResourceOperationRunner.RunCancelableResourceOperation(
                                    "Synchronize files (multiple games)",
                                    token => ResourceDownloadService.DownloadSpecificFilesForMultipleGamesAsync(gamesNeedingUpdates, token));
                                break;
                            case 1:
                                ResourceOperationRunner.RunCancelableResourceOperation(
                                    "Refresh resources (games)",
                                    token => ResourceDownloadService.DownloadResourcesAsync(gamesNeedingUpdates.Keys.ToArray(), cancellationToken: token));
                                break;
                        }
                    }
                );
                return false;
            }
        }
    }
}
#endif
