#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Utf8Json;

namespace HoyoToon.Editor.Updater
{
    internal static class PackageUpdaterService
    {
        private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(15);
        private const string ApplyBackupFolderName = "apply-backup";

        private static UpdaterStatusSnapshot cachedStatusSnapshot;

        internal static UpdaterStatusSnapshot GetStatusSnapshot()
        {
            cachedStatusSnapshot ??= PackageUpdaterStorage.ReadStatusSnapshot() ?? CreateDefaultSnapshot();
            return CloneSnapshot(cachedStatusSnapshot);
        }

        internal static void ResetCachedStatus()
        {
            cachedStatusSnapshot = null;
        }

        internal static bool IsBusy()
        {
            if (EditorCoroutine.IsRunning)
            {
                return true;
            }

            UpdaterLockState existingLock = PackageUpdaterStorage.ReadLockState();
            if (existingLock == null)
            {
                return false;
            }

            if (IsLockExpired(existingLock))
            {
                PackageUpdaterStorage.ClearLockState();
                return false;
            }

            return true;
        }

        internal static void CheckForUpdates(bool showUpToDateDialog, bool automatic, bool cleanMissingFiles)
        {
            if (IsBusy())
            {
                if (!automatic)
                {
                    HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Updater Busy", "The updater is already running.", "OK");
                }

                return;
            }

            SetStatus(
                UpdateAvailabilityState.Checking,
                PackageUpdaterStorage.GetLocalPackageVersion(),
                null,
                $"Checking '{PackageUpdaterStorage.CurrentBranch}' for updates...",
                hasPendingApply: PackageUpdaterStorage.HasPendingState());

            if (!EditorCoroutine.Start(CheckRoutine(showUpToDateDialog, automatic, cleanMissingFiles)))
            {
                cachedStatusSnapshot = PackageUpdaterStorage.ReadStatusSnapshot() ?? CreateDefaultSnapshot();
                if (!automatic)
                {
                    HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Updater Busy", "The updater is already running.", "OK");
                }
            }
        }

        internal static void TryResumePendingInstallOnStartup()
        {
            PendingInstallState pendingState = PackageUpdaterStorage.ReadPendingState();
            if (pendingState == null)
            {
                return;
            }

            bool hasStagedFiles = pendingState.filesToCopy.Count == 0 || Directory.Exists(PackageUpdaterStorage.StagedFilesPath);
            if (!hasStagedFiles)
            {
                PackageUpdaterStorage.ClearPendingArtifacts();
                SetStatus(
                    UpdateAvailabilityState.Error,
                    PackageUpdaterStorage.GetLocalPackageVersion(),
                    pendingState.remoteVersion,
                    "A staged HoyoToon update was found without staged files. The invalid pending state was cleared.",
                    hasPendingApply: false);
                return;
            }

            if (!EditorCoroutine.Start(ResumePendingRoutine()))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, "Unable to resume the staged HoyoToon update because another updater operation is already running.");
            }
        }

        private static IEnumerator CheckRoutine(bool showUpToDateDialog, bool automatic, bool cleanMissingFiles)
        {
            bool progressShown = false;
            UpdatePlan plan = null;
            string errorMessage = null;

            try
            {
                if (!automatic)
                {
                    HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", $"Checking '{PackageUpdaterStorage.CurrentBranch}' for updates...", 0.05f);
                    progressShown = true;
                }

                yield return BuildPlanRoutine(cleanMissingFiles, automatic, value => plan = value, value => errorMessage = value);

                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    Fail(errorMessage, automatic);
                    yield break;
                }

                if (plan == null)
                {
                    yield break;
                }

                SetStatus(plan.AvailabilityState, plan.LocalVersion, plan.RemoteVersion, plan.StatusMessage, hasPendingApply: false);

                if (plan.AvailabilityState == UpdateAvailabilityState.UpToDate)
                {
                    HoyoToonLogger.Info(HoyoToonLogCategory.General, plan.StatusMessage, isBackgroundOperation: automatic);
                    if (showUpToDateDialog)
                    {
                        if (progressShown)
                        {
                            HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
                            progressShown = false;
                        }

                        HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("HoyoToon Updater", plan.StatusMessage, "OK");
                    }

                    yield break;
                }

                if (plan.AvailabilityState == UpdateAvailabilityState.LocalAhead)
                {
                    HoyoToonLogger.Warning(HoyoToonLogCategory.General, plan.StatusMessage, isBackgroundOperation: automatic);
                    if (!automatic)
                    {
                        if (progressShown)
                        {
                            HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
                            progressShown = false;
                        }

                        HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("HoyoToon Updater", plan.StatusMessage, "OK");
                    }

                    yield break;
                }

                if (progressShown)
                {
                    HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
                    progressShown = false;
                }

                if (!ShowUpdateAvailableDialog(plan, automatic))
                {
                    yield break;
                }

                yield return InstallRoutine(plan);
            }
            finally
            {
                if (progressShown)
                {
                    HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
                }
            }
        }

        private static IEnumerator BuildPlanRoutine(bool cleanMissingFiles, bool automatic, Action<UpdatePlan> onSuccess, Action<string> onError)
        {
            string manifestUrl = PackageUpdaterManifestClient.BuildCacheBustedUrl(PackageUpdaterManifestClient.BuildManifestUrl(PackageUpdaterStorage.CurrentBranch));
            using UnityWebRequest manifestRequest = UnityWebRequest.Get(manifestUrl);
            ApplyNoCacheHeaders(manifestRequest);

            if (!automatic)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", "Downloading updater manifest...", 0.1f);
            }

            yield return manifestRequest.SendWebRequest();
            if (manifestRequest.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Failed to download updater manifest from '{PackageUpdaterStorage.CurrentBranch}': {manifestRequest.error}");
                yield break;
            }

            UpdaterManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<UpdaterManifest>(manifestRequest.downloadHandler.text, HoyoToonApi.JsonResolver);
            }
            catch (Exception exception)
            {
                onError?.Invoke($"Updater manifest was empty or invalid JSON: {exception.Message}");
                yield break;
            }

            if (manifest == null)
            {
                onError?.Invoke("Updater manifest was empty or invalid JSON.");
                yield break;
            }

            if (!automatic)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", "Comparing local package files...", 0.35f);
            }

            UpdatePlan plan;
            try
            {
                plan = PackageUpdaterPlanBuilder.BuildPlan(PackageUpdaterStorage.CurrentBranch, manifest, cleanMissingFiles);
            }
            catch (Exception exception)
            {
                onError?.Invoke(exception.Message);
                yield break;
            }

            if (plan.TotalChanges > 0)
            {
                if (!automatic)
                {
                    HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", "Downloading remote changelog...", 0.55f);
                }

                string changelogUrl = PackageUpdaterManifestClient.BuildCacheBustedUrl(
                    PackageUpdaterManifestClient.BuildRawBaseUrl(PackageUpdaterStorage.CurrentBranch).TrimEnd('/') + "/changelog.md");
                using UnityWebRequest changelogRequest = UnityWebRequest.Get(changelogUrl);
                ApplyNoCacheHeaders(changelogRequest);
                yield return changelogRequest.SendWebRequest();
                if (changelogRequest.result == UnityWebRequest.Result.Success)
                {
                    plan.RemoteChangelog = changelogRequest.downloadHandler.text;
                }
            }

            onSuccess?.Invoke(plan);
        }

        private static bool ShowUpdateAvailableDialog(UpdatePlan plan, bool automatic)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"Branch: {plan.Branch}");
            messageBuilder.AppendLine($"Current version: {plan.LocalVersion}");
            messageBuilder.AppendLine($"Available version: {plan.RemoteVersion}");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"Files to update: {plan.FilesToCopy.Count}");
            messageBuilder.AppendLine($"Files to remove: {plan.FilesToDelete.Count}");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Install the update now?");

            if (!string.IsNullOrWhiteSpace(plan.RemoteChangelog))
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("Remote changelog:");
                messageBuilder.AppendLine();
                messageBuilder.AppendLine(plan.RemoteChangelog.Trim());
            }

            if (automatic)
            {
                int choice = HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialogComplex(
                    "HoyoToon Update Available",
                    messageBuilder.ToString(),
                    "Update Now",
                    "Later",
                    "Disable Auto Check");

                if (choice == 2)
                {
                    PackageUpdaterStorage.AutoCheckEnabled = false;
                    HoyoToonLogger.Info(HoyoToonLogCategory.General, "Automatic HoyoToon updater checks were disabled by the user.");
                }

                return choice == 0;
            }

            return HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(
                "HoyoToon Update Available",
                messageBuilder.ToString(),
                "Update Now",
                "Later");
        }

        private static IEnumerator InstallRoutine(UpdatePlan plan)
        {
            string completionTitle = null;
            string completionMessage = null;

            PrepareLock();
            SetStatus(UpdateAvailabilityState.Applying, plan.LocalVersion, plan.RemoteVersion, $"Updating HoyoToon from '{plan.Branch}'...", hasPendingApply: false);
            HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", $"Updating HoyoToon from '{plan.Branch}'...", 0f);

            try
            {
                EnsureCleanStagingArea();

                if (plan.FilesToCopy.Count > 0)
                {
                    var downloadResult = new UpdateDownloader.DownloadResult();
                    yield return UpdateDownloader.DownloadFiles(
                        plan.FilesToCopy,
                        PackageUpdaterManifestClient.BuildRawBaseUrl(plan.Branch),
                        PackageUpdaterStorage.StagedFilesPath,
                        downloadResult,
                        (progress, message) => HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", message, Mathf.Clamp01(progress * 0.75f)));

                    if (!downloadResult.Success)
                    {
                        Fail(downloadResult.ErrorMessage, automatic: false);
                        yield break;
                    }
                }

                WritePendingState(plan);
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", "Applying staged update...", 0.85f);

                if (!ApplyPendingState(PackageUpdaterStorage.ReadPendingState(), out string applyError))
                {
                    Fail(applyError, automatic: false, preservePendingArtifacts: true);
                    yield break;
                }

                string appliedVersion = PackageUpdaterStorage.GetLocalPackageVersion();
                string finalVersion = string.IsNullOrWhiteSpace(plan.RemoteVersion) ? appliedVersion : plan.RemoteVersion;
                SetStatus(
                    UpdateAvailabilityState.UpToDate,
                    finalVersion,
                    finalVersion,
                    $"HoyoToon is up to date on the '{plan.Branch}' branch.",
                    hasPendingApply: false);

                HoyoToonLogger.Info(HoyoToonLogCategory.General, $"Updater applied branch '{plan.Branch}' ({plan.TotalChanges} changes).");
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", "Update complete.", 1f);

                completionTitle = "HoyoToon Updated";
                completionMessage =
                    $"HoyoToon was updated from the '{plan.Branch}' branch.\n\n" +
                    $"Updated files: {plan.FilesToCopy.Count}\n" +
                    $"Removed files: {plan.FilesToDelete.Count}";
            }
            finally
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
                ReleaseLock();

                if (!string.IsNullOrEmpty(completionTitle) && !string.IsNullOrEmpty(completionMessage))
                {
                    EditorApplication.delayCall += () => HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(completionTitle, completionMessage, "OK");
                }
            }
        }

        private static IEnumerator ResumePendingRoutine()
        {
            string completionTitle = null;
            string completionMessage = null;

            PrepareLock();
            HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", "Resuming a staged HoyoToon update...", 0f);

            try
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", "Applying staged files...", 0.5f);
                if (!ApplyPendingState(PackageUpdaterStorage.ReadPendingState(), out string applyError))
                {
                    Fail(applyError, automatic: false, preservePendingArtifacts: true);
                    yield break;
                }

                string finalVersion = PackageUpdaterStorage.GetLocalPackageVersion();
                SetStatus(
                    UpdateAvailabilityState.UpToDate,
                    finalVersion,
                    finalVersion,
                    $"HoyoToon is up to date on the '{PackageUpdaterStorage.CurrentBranch}' branch.",
                    hasPendingApply: false);

                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar("HoyoToon Updater", "Update complete.", 1f);
                completionTitle = "HoyoToon Updated";
                completionMessage = "A previously staged HoyoToon update has been applied.";
            }
            finally
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
                ReleaseLock();

                if (!string.IsNullOrEmpty(completionTitle) && !string.IsNullOrEmpty(completionMessage))
                {
                    EditorApplication.delayCall += () => HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(completionTitle, completionMessage, "OK");
                }
            }
        }

        private static bool ApplyPendingState(PendingInstallState pendingState, out string error)
        {
            error = null;
            if (pendingState == null)
            {
                error = "No staged updater state was available to apply.";
                return false;
            }

            UpdaterKeepRules keepRules = UpdaterKeepRules.Load(PackageUpdaterStorage.PackageRootPath);
            if (!ValidatePendingStatePaths(pendingState, keepRules, out error))
            {
                return false;
            }

            List<string> touchedRelativePaths = BuildTouchedRelativePaths(pendingState, keepRules);
            if (!ValidateStagedFilesExist(pendingState, out error))
            {
                return false;
            }

            string applyBackupRootPath = GetApplyBackupRootPath();
            try
            {
                ManagedFileTransactionUtility.PrepareDirectory(applyBackupRootPath);
                BackupTouchedPaths(applyBackupRootPath, touchedRelativePaths);
            }
            catch (Exception exception)
            {
                ManagedFileTransactionUtility.DeleteDirectoryIfExists(applyBackupRootPath);
                error = $"Failed to prepare the staged update for application: {exception.Message}";
                return false;
            }

            AssetDatabase.DisallowAutoRefresh();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string relativePath in pendingState.filesToCopy ?? new List<string>())
                {
                    if (!PackageUpdaterStorage.TryResolveStagedFilePath(relativePath, out string sourcePath, out string pathError)
                        || !PackageUpdaterStorage.TryResolvePackageFilePath(relativePath, out string destinationPath, out pathError))
                    {
                        throw new IOException($"Unsafe updater path '{relativePath}': {pathError}");
                    }

                    if (!File.Exists(sourcePath))
                    {
                        throw new IOException($"Staged file is missing: {relativePath}");
                    }

                    ManagedFileTransactionUtility.CopyFile(sourcePath, destinationPath);
                }

                foreach (string relativePath in pendingState.filesToDelete ?? new List<string>())
                {
                    if (keepRules.IsKept(relativePath))
                    {
                        continue;
                    }

                    if (!PackageUpdaterStorage.TryResolvePackageFilePath(relativePath, out string destinationPath, out string pathError))
                    {
                        throw new IOException($"Unsafe updater delete path '{relativePath}': {pathError}");
                    }

                    ManagedFileTransactionUtility.DeleteFileAndMeta(destinationPath);
                }
            }
            catch (Exception exception)
            {
                try
                {
                    RollbackTouchedPaths(applyBackupRootPath, touchedRelativePaths);
                    error =
                        $"Failed to apply staged update: {exception.Message}\n\n" +
                        "Any partial file changes were rolled back. The staged update was preserved so you can retry it.";
                }
                catch (Exception rollbackException)
                {
                    error =
                        $"Failed to apply staged update: {exception.Message}\n\n" +
                        $"Rollback also failed: {rollbackException.Message}\n\n" +
                        "The staged update was preserved so you can retry or inspect it manually.";
                }

                return false;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ManagedFileTransactionUtility.DeleteDirectoryIfExists(applyBackupRootPath);
            }

            PackageUpdaterStorage.ClearPendingArtifacts();
            return true;
        }

        private static bool ValidatePendingStatePaths(PendingInstallState pendingState, UpdaterKeepRules keepRules, out string error)
        {
            error = null;
            foreach (string relativePath in pendingState.filesToCopy ?? new List<string>())
            {
                if (!PackageUpdaterStorage.TryNormalizeRelativePath(relativePath, out string normalizedPath, out error)
                    || !string.Equals(relativePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Pending updater copy path '{relativePath}' is unsafe: {error ?? "path normalization changed the value"}";
                    return false;
                }

                if (!PackageUpdaterStorage.TryResolveStagedFilePath(relativePath, out _, out error)
                    || !PackageUpdaterStorage.TryResolvePackageFilePath(relativePath, out _, out error))
                {
                    error = $"Pending updater copy path '{relativePath}' is unsafe: {error}";
                    return false;
                }
            }

            foreach (string relativePath in pendingState.filesToDelete ?? new List<string>())
            {
                if (keepRules != null && keepRules.IsKept(relativePath))
                {
                    continue;
                }

                if (!PackageUpdaterStorage.TryNormalizeRelativePath(relativePath, out string normalizedPath, out error)
                    || !string.Equals(relativePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Pending updater delete path '{relativePath}' is unsafe: {error ?? "path normalization changed the value"}";
                    return false;
                }

                if (!PackageUpdaterStorage.TryResolvePackageFilePath(relativePath, out _, out error))
                {
                    error = $"Pending updater delete path '{relativePath}' is unsafe: {error}";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateStagedFilesExist(PendingInstallState pendingState, out string error)
        {
            error = null;
            if (pendingState == null)
            {
                error = "No staged updater state was available to apply.";
                return false;
            }

            foreach (string relativePath in pendingState.filesToCopy ?? new List<string>())
            {
                if (!PackageUpdaterStorage.TryResolveStagedFilePath(relativePath, out string sourcePath, out error))
                {
                    error = $"Unsafe staged updater path '{relativePath}': {error}";
                    return false;
                }

                if (!File.Exists(sourcePath))
                {
                    error = $"Staged file is missing: {relativePath}";
                    return false;
                }
            }

            return true;
        }

        private static List<string> BuildTouchedRelativePaths(PendingInstallState pendingState, UpdaterKeepRules keepRules)
        {
            var touchedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (pendingState == null)
            {
                return new List<string>();
            }

            foreach (string relativePath in pendingState.filesToCopy ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    touchedRelativePaths.Add(relativePath);
                }
            }

            foreach (string relativePath in pendingState.filesToDelete ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(relativePath) || keepRules.IsKept(relativePath))
                {
                    continue;
                }

                touchedRelativePaths.Add(relativePath);
            }

            return new List<string>(touchedRelativePaths);
        }

        private static string GetApplyBackupRootPath()
        {
            return Path.Combine(PackageUpdaterStorage.UpdaterStoragePath, ApplyBackupFolderName);
        }

        private static void BackupTouchedPaths(string backupRootPath, IReadOnlyList<string> touchedRelativePaths)
        {
            ManagedFileTransactionUtility.BackupTouchedPaths(
                touchedRelativePaths,
                ResolvePackagePathOrEmpty,
                relativePath => ResolveBackupPathOrEmpty(backupRootPath, relativePath));
        }

        private static void RollbackTouchedPaths(string backupRootPath, IReadOnlyList<string> touchedRelativePaths)
        {
            ManagedFileTransactionUtility.RollbackTouchedPaths(
                PackageUpdaterStorage.PackageRootPath,
                touchedRelativePaths,
                ResolvePackagePathOrEmpty,
                relativePath => ResolveBackupPathOrEmpty(backupRootPath, relativePath));
        }

        private static string ResolvePackagePathOrEmpty(string relativePath)
        {
            return PackageUpdaterStorage.TryResolvePackageFilePath(relativePath, out string packagePath, out _)
                ? packagePath
                : string.Empty;
        }

        private static string ResolveBackupPathOrEmpty(string backupRootPath, string relativePath)
        {
            return PackageUpdaterStorage.TryResolveBackupFilePath(backupRootPath, relativePath, out string backupPath, out _)
                ? backupPath
                : string.Empty;
        }

        private static void WritePendingState(UpdatePlan plan)
        {
            PackageUpdaterStorage.WritePendingState(new PendingInstallState
            {
                branch = plan.Branch,
                localVersion = plan.LocalVersion,
                remoteVersion = plan.RemoteVersion,
                cleanMissingFiles = plan.CleanMissingFiles,
                filesToCopy = new List<string>(plan.FilesToCopy),
                filesToDelete = new List<string>(plan.FilesToDelete),
            });
        }

        private static void PrepareLock()
        {
            PackageUpdaterStorage.WriteLockState(new UpdaterLockState
            {
                operation = (int)UpdaterOperationKind.Applying,
                createdUtcTicks = DateTime.UtcNow.Ticks,
            });
        }

        private static void ReleaseLock()
        {
            PackageUpdaterStorage.ClearLockState();
        }

        private static void EnsureCleanStagingArea()
        {
            PackageUpdaterStorage.ClearStagingArea();
            PackageUpdaterStorage.EnsureStagingAreaExists();
        }

        private static void Fail(string message, bool automatic, bool preservePendingArtifacts = false)
        {
            bool hasPendingApply = preservePendingArtifacts && PackageUpdaterStorage.HasPendingState();
            if (!hasPendingApply)
            {
                PackageUpdaterStorage.ClearPendingArtifacts();
            }

            SetStatus(
                UpdateAvailabilityState.Error,
                PackageUpdaterStorage.GetLocalPackageVersion(),
                null,
                message,
                hasPendingApply: hasPendingApply);

            HoyoToonLogger.Error(HoyoToonLogCategory.General, message, isBackgroundOperation: automatic);
            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("HoyoToon Updater", message, "OK");
        }

        private static bool IsLockExpired(UpdaterLockState lockState)
        {
            if (lockState == null || lockState.createdUtcTicks <= 0)
            {
                return true;
            }

            DateTime createdUtc = new DateTime(lockState.createdUtcTicks, DateTimeKind.Utc);
            return DateTime.UtcNow - createdUtc > LockTimeout;
        }

        private static void SetStatus(
            UpdateAvailabilityState state,
            string localVersion,
            string remoteVersion,
            string statusMessage,
            bool hasPendingApply)
        {
            cachedStatusSnapshot = new UpdaterStatusSnapshot
            {
                state = (int)state,
                branch = PackageUpdaterStorage.CurrentBranch,
                localVersion = string.IsNullOrWhiteSpace(localVersion) ? PackageUpdaterStorage.GetLocalPackageVersion() : localVersion,
                remoteVersion = remoteVersion ?? string.Empty,
                statusMessage = statusMessage ?? string.Empty,
                lastCheckedUtcTicks = state == UpdateAvailabilityState.Checking
                    ? (cachedStatusSnapshot?.lastCheckedUtcTicks ?? 0)
                    : DateTime.UtcNow.Ticks,
                hasPendingApply = hasPendingApply,
            };

            PackageUpdaterStorage.WriteStatusSnapshot(cachedStatusSnapshot);
        }

        private static UpdaterStatusSnapshot CreateDefaultSnapshot()
        {
            return new UpdaterStatusSnapshot
            {
                state = (int)UpdateAvailabilityState.Unknown,
                branch = PackageUpdaterStorage.CurrentBranch,
                localVersion = PackageUpdaterStorage.GetLocalPackageVersion(),
                remoteVersion = string.Empty,
                statusMessage = $"No update check has been recorded for '{PackageUpdaterStorage.CurrentBranch}' yet.",
                lastCheckedUtcTicks = 0,
                hasPendingApply = PackageUpdaterStorage.HasPendingState(),
            };
        }

        private static UpdaterStatusSnapshot CloneSnapshot(UpdaterStatusSnapshot snapshot)
        {
            return snapshot == null
                ? null
                : new UpdaterStatusSnapshot
                {
                    state = snapshot.state,
                    branch = snapshot.branch,
                    localVersion = snapshot.localVersion,
                    remoteVersion = snapshot.remoteVersion,
                    statusMessage = snapshot.statusMessage,
                    lastCheckedUtcTicks = snapshot.lastCheckedUtcTicks,
                    hasPendingApply = snapshot.hasPendingApply,
                };
        }

        private static void ApplyNoCacheHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("Cache-Control", "no-cache, no-store, max-age=0");
            request.SetRequestHeader("Pragma", "no-cache");
            request.SetRequestHeader("Expires", "0");
        }
    }
}
#endif
