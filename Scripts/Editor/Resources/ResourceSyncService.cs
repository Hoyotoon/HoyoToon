#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoyoToon.Editor.Resources.Cloudreve;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Cloudreve;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.Editor;
using HoyoToon.Editor.Utilities.IO;
using UnityEditor;

namespace HoyoToon.Editor.Resources
{
    public static class ResourceSyncService
    {
        private const string ProgressTitle = "HoyoToon Resources";
        private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(30);
        private const int DownloadBatchSize = 4;

        private static bool isSyncRunning;

        internal static bool IsBusy()
        {
            return isSyncRunning;
        }

        public static async Task CheckAllGamesAsync()
        {
            await CheckAllGamesAsync(showUpToDateDialog: true, promptToSyncAfterCheck: true, automatic: false);
        }

        public static async Task CheckAllGamesInBackgroundAsync()
        {
            await CheckAllGamesAsync(showUpToDateDialog: false, promptToSyncAfterCheck: true, automatic: true);
        }

        internal static async Task SyncAllGamesForOnboardingAsync()
        {
            if (isSyncRunning)
            {
                return;
            }

            ResourceRegistry.Initialize();

            if (!ResourceSyncTargetResolver.TryResolveAllTargets(out List<ResourceSyncTarget> targets, out List<string> messages))
            {
                ResourceSyncNotifier.LogBatchSummary(BuildResolutionFailureMessage(messages), hasIssues: true);
                return;
            }

            await RunBatchAsync(
                targets,
                messages,
                applyChanges: true,
                promptToSyncAfterCheck: false,
                showUpToDateDialog: false,
                automatic: true,
                showCompletionDialog: false);
        }

        private static async Task CheckAllGamesAsync(bool showUpToDateDialog, bool promptToSyncAfterCheck, bool automatic)
        {
            if (isSyncRunning)
            {
                if (!automatic)
                {
                    ResourceSyncNotifier.ShowInfo("A HoyoToon resource sync is already running. Wait for it to finish before starting another one.");
                }

                return;
            }

            ResourceRegistry.Initialize();

            if (!ResourceSyncTargetResolver.TryResolveAllTargets(out List<ResourceSyncTarget> targets, out List<string> messages))
            {
                string failureMessage = BuildResolutionFailureMessage(messages);
                if (automatic)
                {
                    ResourceSyncNotifier.LogBatchSummary(failureMessage, hasIssues: true);
                }
                else
                {
                    ResourceSyncNotifier.ShowError(failureMessage);
                }

                return;
            }

            await RunBatchAsync(
                targets,
                messages,
                applyChanges: false,
                promptToSyncAfterCheck: promptToSyncAfterCheck,
                showUpToDateDialog: showUpToDateDialog,
                automatic: automatic);
        }

        public static async Task SyncAllGamesAsync()
        {
            if (isSyncRunning)
            {
                ResourceSyncNotifier.ShowInfo("A HoyoToon resource sync is already running. Wait for it to finish before starting another one.");
                return;
            }

            ResourceRegistry.Initialize();

            if (!ResourceSyncTargetResolver.TryResolveAllTargets(out List<ResourceSyncTarget> targets, out List<string> messages))
            {
                ResourceSyncNotifier.ShowError(BuildResolutionFailureMessage(messages));
                return;
            }

            await RunBatchAsync(
                targets,
                messages,
                applyChanges: true,
                promptToSyncAfterCheck: false,
                showUpToDateDialog: true,
                automatic: false);
        }

        public static async Task SyncDetectedGameAsync()
        {
            if (isSyncRunning)
            {
                ResourceSyncNotifier.ShowInfo("A HoyoToon resource sync is already running. Wait for it to finish before starting another one.");
                return;
            }

            ResourceRegistry.Initialize();

            if (!ResourceSyncTargetResolver.TryResolveDetectedTarget(out ResourceSyncTarget target, out string message))
            {
                ResourceSyncNotifier.ShowError(message);
                return;
            }

            await RunBatchAsync(
                new[] { target },
                Array.Empty<string>(),
                applyChanges: true,
                promptToSyncAfterCheck: false,
                showUpToDateDialog: true,
                automatic: false);
        }

        public static void ClearAllGameResources()
        {
            if (isSyncRunning)
            {
                ResourceSyncNotifier.ShowInfo("A HoyoToon resource sync is already running. Wait for it to finish before starting another one.");
                return;
            }

            ResourceRegistry.Initialize();

            if (!ResourceSyncTargetResolver.TryResolveAllTargets(out List<ResourceSyncTarget> targets, out List<string> messages))
            {
                ResourceSyncNotifier.ShowError(BuildResolutionFailureMessage(messages));
                return;
            }

            ClearTargets(targets);
        }

        public static void ClearDetectedGameResources()
        {
            if (isSyncRunning)
            {
                ResourceSyncNotifier.ShowInfo("A HoyoToon resource sync is already running. Wait for it to finish before starting another one.");
                return;
            }

            ResourceRegistry.Initialize();

            if (!ResourceSyncTargetResolver.TryResolveDetectedTarget(out ResourceSyncTarget target, out string message))
            {
                ResourceSyncNotifier.ShowError(message);
                return;
            }

            ClearTargets(new[] { target });
        }

        private static async Task RunBatchAsync(
            IReadOnlyList<ResourceSyncTarget> targets,
            IReadOnlyList<string> resolutionErrors,
            bool applyChanges,
            bool promptToSyncAfterCheck,
            bool showUpToDateDialog,
            bool automatic,
            bool showCompletionDialog = true)
        {
            isSyncRunning = true;
            var results = new List<ResourceSyncResult>();
            var preparedTargets = new List<PreparedSyncTarget>();
            var lockedTargets = new List<ResourceSyncTarget>();
            bool progressShown = false;
            bool userAcceptedBackgroundSync = false;
            string[] configurationErrors = (resolutionErrors ?? Array.Empty<string>())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            try
            {
                EnsureEditorReady();

                int totalTargets = targets?.Count ?? 0;
                for (int index = 0; index < totalTargets; index++)
                {
                    ResourceSyncTarget target = targets[index];
                    float progress = totalTargets <= 0
                        ? 0.1f
                        : 0.05f + (0.45f * ((float)index / totalTargets));
                    if (!automatic)
                    {
                        HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar(
                            ProgressTitle,
                            $"Checking {index + 1} of {totalTargets} resource set(s): '{target.OperationLabel}'...",
                            progress);
                        progressShown = true;
                    }

                    try
                    {
                        AcquireLock(target);
                        lockedTargets.Add(target);

                        PreparedSyncTarget prepared = await PrepareTargetAsync(target, CancellationToken.None);
                        preparedTargets.Add(prepared);

                        ResourceSyncResult result = CreateCheckResult(prepared);
                        results.Add(result);
                        WriteStatus(target, result, prepared.ShareUrl, prepared.ResolvedRootUri);
                    }
                    catch (Exception exception)
                    {
                        ResourceSyncResult failure = CreateFailureResult(target, exception);
                        results.Add(failure);
                        WriteStatus(target, failure, target?.ResourceAsset?.WebdavUrl, failure.ResolvedRootUri);
                    }
                }

                bool hasPendingChanges = preparedTargets.Any(prepared => prepared.Plan != null && prepared.Plan.HasChanges);
                if (!applyChanges && promptToSyncAfterCheck && hasPendingChanges)
                {
                    string pendingSummary = BuildBatchSummary(results, configurationErrors, changesApplied: false);
                    bool promptClearedProgress = progressShown;
                    if (!ResourceSyncNotifier.PromptToSyncChanged(pendingSummary, promptClearedProgress))
                    {
                        return;
                    }

                    progressShown = false;
                    userAcceptedBackgroundSync = automatic;
                    applyChanges = true;
                }

                if (applyChanges)
                {
                    List<PreparedSyncTarget> targetsToSync = preparedTargets
                        .Where(prepared => prepared.Plan != null && prepared.Plan.HasChanges)
                        .ToList();

                    for (int index = 0; index < targetsToSync.Count; index++)
                    {
                        PreparedSyncTarget prepared = targetsToSync[index];
                        bool showSyncProgress = !automatic || userAcceptedBackgroundSync;
                        ResourceSyncNotifier notifier = new ResourceSyncNotifier(prepared.Target, showSyncProgress);
                        progressShown |= showSyncProgress;

                        try
                        {
                            ResourceSyncResult syncResult = await SyncPreparedTargetAsync(prepared, notifier, CancellationToken.None);
                            ReplaceResult(results, syncResult);
                        }
                        catch (Exception exception)
                        {
                            ResourceSyncResult failure = CreateFailureResult(prepared.Target, exception, prepared.ResolvedRootUri);
                            ReplaceResult(results, failure);
                            WriteStatus(prepared.Target, failure, prepared.ShareUrl, prepared.ResolvedRootUri);
                        }
                    }
                }

                bool hasIssues = HasIssues(results, configurationErrors);
                bool hasDetectedChanges = results.Any(result => result != null && result.Success && result.ChangesPending);
                string summary = BuildBatchSummary(results, configurationErrors, changesApplied: applyChanges);

                if (applyChanges || hasIssues || hasDetectedChanges || showUpToDateDialog)
                {
                    if (showCompletionDialog && (!automatic || applyChanges))
                    {
                        ResourceSyncNotifier.ShowBatchSummary(summary, hasIssues, progressShown);
                        progressShown = false;
                    }
                    else
                    {
                        ResourceSyncNotifier.LogBatchSummary(summary, hasIssues);
                    }
                }
                else if (!automatic)
                {
                    ResourceSyncNotifier.LogBatchSummary(summary, hasIssues: false);
                }
            }
            finally
            {
                if (progressShown)
                {
                    HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
                }

                foreach (ResourceSyncTarget target in lockedTargets)
                {
                    ResourceSyncStorage.ClearStageRoot(target?.GameKey);
                    ResourceSyncStorage.ClearLock(target?.GameKey);
                }

                isSyncRunning = false;
            }
        }

        private static void ClearTargets(IReadOnlyList<ResourceSyncTarget> targets)
        {
            IReadOnlyList<ResourceSyncTarget> targetList = targets ?? Array.Empty<ResourceSyncTarget>();
            if (targetList.Count <= 0)
            {
                ResourceSyncNotifier.ShowInfo("No HoyoToon resource sets were available to clear.");
                return;
            }

            EnsureEditorReady();

            using (AssetDatabaseEditingScope.Begin())
            {
                foreach (ResourceSyncTarget target in targetList)
                {
                    ClearTarget(target);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string summary = targetList.Count == 1
                ? $"Cleared resources for '{targetList[0].OperationLabel}'."
                : $"Cleared resources for {targetList.Count} game resource set(s).";
            ResourceSyncNotifier.ShowInfo(summary);
        }

        private static void ClearTarget(ResourceSyncTarget target)
        {
            if (target == null)
            {
                return;
            }

            if (Directory.Exists(target.DestinationAbsolutePath))
            {
                Directory.Delete(target.DestinationAbsolutePath, true);
            }

            DeleteFileIfExists(ResourceSyncStorage.GetAbsolutePath(target.DestinationAssetPath + ".meta"));
            DeleteFileIfExists(ResourceSyncStorage.GetManifestPath(target.GameKey));
            DeleteFileIfExists(ResourceSyncStorage.GetStatusPath(target.GameKey));
            DeleteFileIfExists(ResourceSyncStorage.GetLockPath(target.GameKey));
            ResourceSyncStorage.ClearStageRoot(target.GameKey);
        }

        private static void DeleteFileIfExists(string filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private static async Task<PreparedSyncTarget> PrepareTargetAsync(ResourceSyncTarget target, CancellationToken cancellationToken)
        {
            if (!CloudreveShareUriParser.TryParse(target.ResourceAsset.WebdavUrl, out CloudreveShareDescriptor descriptor, out string parseError))
            {
                throw new ResourceSyncException("share-parse", parseError);
            }

            var client = new CloudreveResourceClient(descriptor.ApiBaseUrl);
            CloudreveShareInfo shareInfo = await client.GetShareInfoAsync(descriptor, cancellationToken);
            if (!CloudreveShareConnectionUtility.TryValidateFolderShare(shareInfo, descriptor, out string shareError))
            {
                throw new ResourceSyncException("share-info", shareError);
            }

            ResourceSyncManifest manifest = ResourceSyncStorage.ReadManifest(target.GameKey);
            string resolvedRootUri = await ResolveRootUriAsync(client, descriptor, manifest, cancellationToken);

            List<RemoteResourceEntry> remoteFiles = await client.ListAllFilesAsync(resolvedRootUri, cancellationToken);
            Directory.CreateDirectory(target.DestinationAbsolutePath);
            Dictionary<string, LocalResourceEntry> localFiles = ResourceSyncStorage.ReadLocalFiles(target.DestinationAbsolutePath);
            ResourceSyncPlan plan = BuildPlan(remoteFiles, localFiles, manifest);

            return new PreparedSyncTarget
            {
                Target = target,
                ShareUrl = descriptor.ShareUrl,
                ResolvedRootUri = resolvedRootUri,
                RemoteFiles = remoteFiles,
                Plan = plan,
            };
        }

        private static async Task<ResourceSyncResult> SyncPreparedTargetAsync(
            PreparedSyncTarget prepared,
            ResourceSyncNotifier notifier,
            CancellationToken cancellationToken)
        {
            if (prepared == null)
            {
                throw new ResourceSyncException("sync", "No prepared resource sync target was provided.");
            }

            ResourceSyncResult result = CreateCheckResult(prepared);
            if (prepared.Plan == null || !prepared.Plan.HasChanges)
            {
                WriteStatus(prepared.Target, result, prepared.ShareUrl, prepared.ResolvedRootUri);
                return result;
            }

            if (!CloudreveShareUriParser.TryParse(prepared.ShareUrl, out CloudreveShareDescriptor descriptor, out string parseError))
            {
                throw new ResourceSyncException("share-parse", parseError);
            }

            var client = new CloudreveResourceClient(descriptor.ApiBaseUrl);
            ResourceSyncStorage.PrepareStageRoot(prepared.Target.GameKey);
            await DownloadFilesAsync(client, prepared.Target, prepared.Plan, notifier, cancellationToken);

            notifier.ShowPhase(
                $"Applying {prepared.Plan.Downloads.Count} download(s) and {prepared.Plan.Deletes.Count} deletion(s) for '{prepared.Target.OperationLabel}'...",
                0.9f);
            ApplyPlan(prepared.Target, prepared.Plan);

            Dictionary<string, LocalResourceEntry> finalLocalFiles = ResourceSyncStorage.ReadLocalFiles(prepared.Target.DestinationAbsolutePath);
            WriteManifest(prepared.Target, prepared.ShareUrl, prepared.ResolvedRootUri, prepared.RemoteFiles, finalLocalFiles);

            result.Success = true;
            result.UpToDate = false;
            result.ChangesPending = false;
            result.DownloadedCount = prepared.Plan.Downloads.Count;
            result.RemovedCount = prepared.Plan.Deletes.Count;
            result.SkippedCount = prepared.Plan.UnchangedCount;

            WriteStatus(prepared.Target, result, prepared.ShareUrl, prepared.ResolvedRootUri);
            return result;
        }

        private static void EnsureEditorReady()
        {
            if (!EditorReadinessUtility.IsReadyForEditorWork())
            {
                throw new ResourceSyncException(
                    "preflight",
                    "The Unity editor is busy compiling, importing assets, or entering Play Mode. Wait for it to become idle before syncing resources.");
            }
        }

        private static void AcquireLock(ResourceSyncTarget target)
        {
            ResourceSyncLockState lockState = ResourceSyncStorage.ReadLock(target.GameKey);
            if (lockState != null && !IsLockExpired(lockState))
            {
                throw new ResourceSyncException("lock", $"Resource sync for '{target.OperationLabel}' is already running.");
            }

            ResourceSyncStorage.ClearLock(target.GameKey);
            ResourceSyncStorage.WriteLock(target.GameKey, new ResourceSyncLockState
            {
                gameKey = target.GameKey,
                createdUtcTicks = DateTime.UtcNow.Ticks,
            });
        }

        private static bool IsLockExpired(ResourceSyncLockState lockState)
        {
            if (lockState == null || lockState.createdUtcTicks <= 0)
            {
                return true;
            }

            DateTime createdUtc = new DateTime(lockState.createdUtcTicks, DateTimeKind.Utc);
            return DateTime.UtcNow - createdUtc > LockTimeout;
        }

        private static async Task<string> ResolveRootUriAsync(
            CloudreveResourceClient client,
            CloudreveShareDescriptor descriptor,
            ResourceSyncManifest manifest,
            CancellationToken cancellationToken)
        {
            string cachedRootUri = manifest != null
                && string.Equals(manifest.shareUrl, descriptor.ShareUrl, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(manifest.resolvedRootUri)
                ? manifest.resolvedRootUri
                : null;

            try
            {
                return await CloudreveShareConnectionUtility
                    .ResolveRootUriAsync(client, descriptor, cachedRootUri, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                throw new ResourceSyncException("share-root-detection", exception.Message, exception);
            }
        }

        private static ResourceSyncPlan BuildPlan(
            IReadOnlyList<RemoteResourceEntry> remoteFiles,
            IReadOnlyDictionary<string, LocalResourceEntry> localFiles,
            ResourceSyncManifest manifest)
        {
            var plan = new ResourceSyncPlan
            {
                TotalRemoteFiles = remoteFiles?.Count ?? 0,
            };

            var remotePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var manifestEntries = new Dictionary<string, ResourceSyncManifestEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ResourceSyncManifestEntry entry in manifest?.files ?? Enumerable.Empty<ResourceSyncManifestEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.relativePath))
                {
                    continue;
                }

                manifestEntries[entry.relativePath] = entry;
            }

            foreach (RemoteResourceEntry remoteFile in (remoteFiles ?? Array.Empty<RemoteResourceEntry>()).OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                if (!remotePaths.Add(remoteFile.RelativePath))
                {
                    throw new ResourceSyncException(
                        "planning",
                        $"The remote share contains two files that collapse to the same local path on Windows: '{remoteFile.RelativePath}'.");
                }

                if (localFiles == null || !localFiles.TryGetValue(remoteFile.RelativePath, out LocalResourceEntry localFile))
                {
                    plan.Downloads.Add(remoteFile);
                    plan.NewFileCount++;
                    continue;
                }

                if (!manifestEntries.TryGetValue(remoteFile.RelativePath, out ResourceSyncManifestEntry manifestEntry))
                {
                    plan.Downloads.Add(remoteFile);
                    plan.UpdatedFileCount++;
                    continue;
                }

                if (!manifestEntry.MatchesLocalFile(localFile)
                    || !manifestEntry.MatchesAssignedLocalState(remoteFile))
                {
                    plan.Downloads.Add(remoteFile);
                    plan.UpdatedFileCount++;
                    continue;
                }

                plan.UnchangedCount++;
            }

            foreach (string managedPath in manifestEntries.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (remotePaths.Contains(managedPath))
                {
                    continue;
                }

                if (localFiles != null && localFiles.ContainsKey(managedPath))
                {
                    plan.Deletes.Add(managedPath);
                }
            }

            return plan;
        }

        private static async Task DownloadFilesAsync(
            CloudreveResourceClient client,
            ResourceSyncTarget target,
            ResourceSyncPlan plan,
            ResourceSyncNotifier notifier,
            CancellationToken cancellationToken)
        {
            if (plan == null || plan.Downloads.Count <= 0)
            {
                return;
            }

            int total = plan.Downloads.Count;
            for (int index = 0; index < total; index += DownloadBatchSize)
            {
                int batchSize = Math.Min(DownloadBatchSize, total - index);
                IReadOnlyList<RemoteResourceEntry> batch = plan.Downloads.GetRange(index, batchSize);
                float progress = 0.55f + (0.3f * ((float)index / total));
                notifier.ShowPhase($"Downloading {index + 1}-{index + batchSize} of {total} file(s) for '{target.OperationLabel}'...", progress);

                var batchTasks = batch
                    .Select(download => DownloadSingleFileAsync(client, target, download, cancellationToken))
                    .ToArray();

                await Task.WhenAll(batchTasks);
            }
        }

        private static async Task DownloadSingleFileAsync(
            CloudreveResourceClient client,
            ResourceSyncTarget target,
            RemoteResourceEntry remoteFile,
            CancellationToken cancellationToken)
        {
            try
            {
                string downloadUrl = await client.CreateDownloadUrlAsync(remoteFile, cancellationToken);
                byte[] payload = await client.DownloadBytesAsync(downloadUrl, cancellationToken);

                string stagedFilePath = ResourceSyncStorage.GetStagedFilePath(target.GameKey, remoteFile.RelativePath);
                string stagedDirectory = Path.GetDirectoryName(stagedFilePath);
                if (!string.IsNullOrWhiteSpace(stagedDirectory))
                {
                    Directory.CreateDirectory(stagedDirectory);
                }

                File.WriteAllBytes(stagedFilePath, payload);
            }
            catch (Exception exception)
            {
                throw new ResourceSyncException(
                    "download",
                    $"Failed to download '{remoteFile.RelativePath}' for '{target.OperationLabel}'.",
                    exception);
            }
        }

        private static void ApplyPlan(ResourceSyncTarget target, ResourceSyncPlan plan)
        {
            IReadOnlyList<string> touchedRelativePaths = GetTouchedRelativePaths(plan);
            ValidateStagedFilesExist(target, plan);

            string rollbackRootPath = GetRollbackRootPath(target);
            ManagedFileTransaction transaction = ManagedFileTransaction.Begin(
                rollbackRootPath,
                target.DestinationAbsolutePath,
                touchedRelativePaths,
                relativePath => ResourceSyncStorage.GetManagedFileAbsolutePath(target.DestinationAbsolutePath, relativePath),
                relativePath => Path.Combine(rollbackRootPath, ResourceSyncStorage.ToPlatformPath(relativePath)));

            AssetDatabaseEditingScope editingScope = AssetDatabaseEditingScope.Begin();
            bool rollbackFailed = false;

            try
            {
                foreach (RemoteResourceEntry remoteFile in plan.Downloads)
                {
                    string stagedFilePath = ResourceSyncStorage.GetStagedFilePath(target.GameKey, remoteFile.RelativePath);
                    if (!File.Exists(stagedFilePath))
                    {
                        throw new ResourceSyncException("apply", $"The staged file '{remoteFile.RelativePath}' is missing.");
                    }

                    string destinationFilePath = ResourceSyncStorage.GetManagedFileAbsolutePath(target.DestinationAbsolutePath, remoteFile.RelativePath);
                    ManagedFileTransactionUtility.CopyFile(stagedFilePath, destinationFilePath);
                }

                foreach (string relativePath in plan.Deletes)
                {
                    string destinationFilePath = ResourceSyncStorage.GetManagedFileAbsolutePath(target.DestinationAbsolutePath, relativePath);
                    ManagedFileTransactionUtility.DeleteFileAndMeta(destinationFilePath);
                }

                ResourceSyncStorage.DeleteEmptyDirectories(target.DestinationAbsolutePath);
                transaction.Commit();
            }
            catch (Exception exception)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackException)
                {
                    rollbackFailed = true;
                    throw new ResourceSyncException(
                        "apply",
                        $"Failed to apply resource changes for '{target.OperationLabel}', and rollback also failed: {rollbackException.Message}. Backup preserved at: {transaction.BackupRootPath}",
                        exception);
                }

                throw new ResourceSyncException(
                    "apply",
                    $"Failed to apply resource changes for '{target.OperationLabel}'. Partial changes were rolled back.",
                    exception);
            }
            finally
            {
                editingScope.Dispose();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (rollbackFailed)
                {
                    HoyoToonLogger.Error(HoyoToonLogCategory.Resources, $"Resource sync rollback failed. Backup preserved at: {transaction.BackupRootPath}");
                }
            }
        }

        private static IReadOnlyList<string> GetTouchedRelativePaths(ResourceSyncPlan plan)
        {
            var touchedRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (plan == null)
            {
                return Array.Empty<string>();
            }

            foreach (RemoteResourceEntry remoteFile in plan.Downloads ?? new List<RemoteResourceEntry>())
            {
                if (remoteFile != null && !string.IsNullOrWhiteSpace(remoteFile.RelativePath))
                {
                    touchedRelativePaths.Add(remoteFile.RelativePath);
                }
            }

            foreach (string relativePath in plan.Deletes ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(relativePath))
                {
                    touchedRelativePaths.Add(relativePath);
                }
            }

            return touchedRelativePaths.ToArray();
        }

        private static void ValidateStagedFilesExist(ResourceSyncTarget target, ResourceSyncPlan plan)
        {
            foreach (RemoteResourceEntry remoteFile in plan?.Downloads ?? new List<RemoteResourceEntry>())
            {
                string stagedFilePath = ResourceSyncStorage.GetStagedFilePath(target.GameKey, remoteFile.RelativePath);
                if (!File.Exists(stagedFilePath))
                {
                    throw new ResourceSyncException("apply", $"The staged file '{remoteFile.RelativePath}' is missing.");
                }
            }
        }

        private static string GetRollbackRootPath(ResourceSyncTarget target)
        {
            return Path.Combine(ResourceSyncStorage.GetStageRootPath(target.GameKey), "__rollback__");
        }

        private static void WriteManifest(
            ResourceSyncTarget target,
            string shareUrl,
            string resolvedRootUri,
            IReadOnlyList<RemoteResourceEntry> remoteFiles,
            IReadOnlyDictionary<string, LocalResourceEntry> finalLocalFiles)
        {
            var manifest = new ResourceSyncManifest
            {
                shareUrl = shareUrl ?? string.Empty,
                resolvedRootUri = resolvedRootUri ?? string.Empty,
                files = new List<ResourceSyncManifestEntry>(),
            };

            foreach (RemoteResourceEntry remoteFile in (remoteFiles ?? Array.Empty<RemoteResourceEntry>()).OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                if (finalLocalFiles == null || !finalLocalFiles.TryGetValue(remoteFile.RelativePath, out LocalResourceEntry localFile))
                {
                    continue;
                }

                var manifestEntry = new ResourceSyncManifestEntry
                {
                    relativePath = remoteFile.RelativePath,
                };
                manifestEntry.CaptureAssignedState(remoteFile, localFile);
                manifest.files.Add(manifestEntry);
            }

            ResourceSyncStorage.WriteManifest(target.GameKey, manifest);
        }

        private static void WriteStatus(ResourceSyncTarget target, ResourceSyncResult result, string shareUrl, string resolvedRootUri)
        {
            if (target == null)
            {
                return;
            }

            ResourceSyncOutcomeState state = ResourceSyncOutcomeState.Failed;
            if (result != null)
            {
                if (result.Success && result.UpToDate)
                {
                    state = ResourceSyncOutcomeState.UpToDate;
                }
                else if (result.Success && result.ChangesPending)
                {
                    state = ResourceSyncOutcomeState.ChangesDetected;
                }
                else if (result.Success)
                {
                    state = ResourceSyncOutcomeState.Succeeded;
                }
            }

            ResourceSyncStorage.WriteStatus(target.GameKey, new ResourceSyncStatusSnapshot
            {
                state = (int)state,
                gameKey = target.GameKey,
                shareUrl = shareUrl ?? string.Empty,
                resolvedRootUri = resolvedRootUri ?? string.Empty,
                statusMessage = result?.BuildSummary() ?? string.Empty,
                lastUpdatedUtcTicks = DateTime.UtcNow.Ticks,
            });
        }

        private static ResourceSyncResult CreateCheckResult(PreparedSyncTarget prepared)
        {
            bool hasChanges = prepared?.Plan != null && prepared.Plan.HasChanges;

            return new ResourceSyncResult
            {
                Target = prepared?.Target,
                Plan = prepared?.Plan,
                ResolvedRootUri = prepared?.ResolvedRootUri ?? string.Empty,
                Success = true,
                UpToDate = !hasChanges,
                ChangesPending = hasChanges,
                RemoteFileCount = prepared?.RemoteFiles?.Count ?? 0,
                SkippedCount = prepared?.Plan?.UnchangedCount ?? 0,
            };
        }

        private static ResourceSyncResult CreateFailureResult(ResourceSyncTarget target, Exception exception, string resolvedRootUri = null)
        {
            var result = new ResourceSyncResult
            {
                Target = target,
                ResolvedRootUri = resolvedRootUri ?? string.Empty,
            };

            if (exception is ResourceSyncException resourceSyncException)
            {
                result.FailureStep = resourceSyncException.Step;
                result.ErrorMessage = resourceSyncException.Message;
                result.Exception = resourceSyncException.InnerException ?? resourceSyncException;
                return result;
            }

            result.FailureStep = "sync";
            result.ErrorMessage = string.IsNullOrWhiteSpace(exception?.Message) ? "Resource sync failed." : exception.Message;
            result.Exception = exception;
            return result;
        }

        private static void ReplaceResult(List<ResourceSyncResult> results, ResourceSyncResult replacement)
        {
            if (results == null || replacement?.Target == null)
            {
                return;
            }

            int existingIndex = results.FindIndex(candidate =>
                candidate?.Target != null
                && string.Equals(candidate.Target.GameKey, replacement.Target.GameKey, StringComparison.Ordinal));

            if (existingIndex >= 0)
            {
                results[existingIndex] = replacement;
                return;
            }

            results.Add(replacement);
        }

        private static bool HasIssues(IReadOnlyList<ResourceSyncResult> results, IReadOnlyList<string> configurationErrors)
        {
            return (configurationErrors?.Count ?? 0) > 0
                || (results?.Any(result => result == null || !result.Success) ?? false);
        }

        private static string BuildResolutionFailureMessage(IReadOnlyList<string> messages)
        {
            var lines = new List<string>
            {
                "No valid HoyoToon resource definitions were available for syncing.",
            };

            foreach (string message in messages ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    lines.Add("- " + message);
                }
            }

            return string.Join("\n", lines);
        }

        private static string BuildBatchSummary(
            IReadOnlyList<ResourceSyncResult> results,
            IReadOnlyList<string> configurationErrors,
            bool changesApplied)
        {
            int checkedCount = results?.Count ?? 0;
            int configErrorCount = configurationErrors?.Count ?? 0;
            int upToDateCount = results?.Count(result => result != null && result.Success && result.UpToDate) ?? 0;
            int pendingCount = results?.Count(result => result != null && result.Success && result.ChangesPending) ?? 0;
            int syncedCount = results?.Count(result => result != null && result.Success && !result.UpToDate && !result.ChangesPending) ?? 0;
            int failureCount = configErrorCount + (results?.Count(result => result == null || !result.Success) ?? 0);
            int downloadedCount = results?.Sum(result => Math.Max(0, result?.DownloadedCount ?? 0)) ?? 0;
            int removedCount = results?.Sum(result => Math.Max(0, result?.RemovedCount ?? 0)) ?? 0;

            var lines = new List<string>
            {
                changesApplied
                    ? $"Processed {checkedCount + configErrorCount} configured resource set(s)."
                    : $"Checked {checkedCount + configErrorCount} configured resource set(s).",
                changesApplied ? $"Synced: {syncedCount}" : $"Needs sync: {pendingCount}",
                $"Up to date: {upToDateCount}",
                $"Failed: {failureCount}",
            };

            if (changesApplied)
            {
                lines.Add($"Downloaded files: {downloadedCount}");
                lines.Add($"Removed files: {removedCount}");
            }

            IEnumerable<ResourceSyncResult> pendingResults = results?
                .Where(result => result != null && result.Success && result.ChangesPending)
                .OrderBy(result => result.Target?.OperationLabel ?? result.Target?.GameKey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                ?? Enumerable.Empty<ResourceSyncResult>();

            if (!changesApplied && pendingResults.Any())
            {
                lines.Add(string.Empty);
                lines.Add("Needs sync:");

                foreach (ResourceSyncResult result in pendingResults)
                {
                    lines.Add($"- {GetTargetLabel(result)}: {result.Plan?.Downloads?.Count ?? 0} download(s), {result.Plan?.Deletes?.Count ?? 0} deletion(s), {result.SkippedCount} unchanged.");
                }
            }

            IEnumerable<ResourceSyncResult> syncedResults = results?
                .Where(result => result != null && result.Success && !result.UpToDate && !result.ChangesPending)
                .OrderBy(result => result.Target?.OperationLabel ?? result.Target?.GameKey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                ?? Enumerable.Empty<ResourceSyncResult>();

            if (changesApplied && syncedResults.Any())
            {
                lines.Add(string.Empty);
                lines.Add("Synced resource sets:");

                foreach (ResourceSyncResult result in syncedResults)
                {
                    lines.Add($"- {GetTargetLabel(result)}: downloaded {result.DownloadedCount}, removed {result.RemovedCount}, unchanged {result.SkippedCount}.");
                }
            }

            IEnumerable<ResourceSyncResult> failures = results?
                .Where(result => result == null || !result.Success)
                .OrderBy(result => result?.Target?.OperationLabel ?? result?.Target?.GameKey ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                ?? Enumerable.Empty<ResourceSyncResult>();

            if (failures.Any())
            {
                lines.Add(string.Empty);
                lines.Add("Failures:");

                foreach (ResourceSyncResult result in failures)
                {
                    string message = result == null
                        ? "Resource sync failed for an unknown target."
                        : $"{GetTargetLabel(result)}: {result.FailureStep} - {result.ErrorMessage}";
                    lines.Add("- " + message);
                }
            }

            if ((configurationErrors?.Count ?? 0) > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Configuration issues:");

                foreach (string error in configurationErrors.Where(message => !string.IsNullOrWhiteSpace(message)).OrderBy(message => message, StringComparer.OrdinalIgnoreCase))
                {
                    lines.Add("- " + error);
                }
            }

            return string.Join("\n", lines.Where(line => line != null));
        }

        private static string GetTargetLabel(ResourceSyncResult result)
        {
            if (!string.IsNullOrWhiteSpace(result?.Target?.OperationLabel))
            {
                return result.Target.OperationLabel;
            }

            if (!string.IsNullOrWhiteSpace(result?.Target?.GameKey))
            {
                return result.Target.GameKey;
            }

            return "Unknown";
        }

        private sealed class PreparedSyncTarget
        {
            public ResourceSyncTarget Target;
            public string ShareUrl = string.Empty;
            public string ResolvedRootUri = string.Empty;
            public List<RemoteResourceEntry> RemoteFiles = new List<RemoteResourceEntry>();
            public ResourceSyncPlan Plan;
        }

        private sealed class ResourceSyncException : Exception
        {
            internal ResourceSyncException(string step, string message, Exception innerException = null)
                : base(message, innerException)
            {
                Step = step ?? string.Empty;
            }

            internal string Step { get; }
        }
    }
}
#endif
