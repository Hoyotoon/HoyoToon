#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using HoyoToon.Runtime.ScriptableObjects.Resources;

namespace HoyoToon.Editor.Resources
{
    internal enum ResourceSyncOutcomeState
    {
        Unknown = 0,
        InProgress = 1,
        UpToDate = 2,
        Succeeded = 3,
        Failed = 4,
        ChangesDetected = 5,
    }

    [Serializable]
    internal sealed class ResourceSyncManifest
    {
        public string shareUrl = string.Empty;
        public string resolvedRootUri = string.Empty;
        public List<ResourceSyncManifestEntry> files = new List<ResourceSyncManifestEntry>();
    }

    [Serializable]
    internal sealed class ResourceSyncManifestEntry
    {
        public string relativePath = string.Empty;
        public string remoteFingerprint = string.Empty;
        public string localFingerprint = string.Empty;
        public long localSize;
        public long localLastWriteUtcTicks;

        internal void CaptureAssignedState(RemoteResourceEntry remoteFile, LocalResourceEntry localFile)
        {
            string assignedFingerprint = remoteFile?.GetFingerprint() ?? string.Empty;
            remoteFingerprint = assignedFingerprint;
            localFingerprint = assignedFingerprint;
            localSize = localFile?.Size ?? 0L;
            localLastWriteUtcTicks = localFile?.LastWriteUtcTicks ?? 0L;
        }

        internal bool MatchesAssignedLocalState(RemoteResourceEntry remoteFile)
        {
            if (remoteFile == null)
            {
                return false;
            }

            string currentFingerprint = remoteFile.GetFingerprint();
            string assignedLocalFingerprint = GetAssignedLocalFingerprint();
            if (string.Equals(assignedLocalFingerprint, currentFingerprint, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(assignedLocalFingerprint))
            {
                return false;
            }

            if (!TryParseTimeFingerprint(currentFingerprint, out long currentSize, out DateTime currentUpdatedAt)
                || !TryParseTimeFingerprint(assignedLocalFingerprint, out long assignedSize, out DateTime assignedUpdatedAt))
            {
                return false;
            }

            if (currentSize != assignedSize)
            {
                return false;
            }

            return currentUpdatedAt <= assignedUpdatedAt;
        }

        internal bool MatchesLocalFile(LocalResourceEntry localFile)
        {
            if (localFile == null || localFile.Size != localSize)
            {
                return false;
            }

            if (localLastWriteUtcTicks <= 0L)
            {
                return true;
            }

            return localFile.LastWriteUtcTicks == localLastWriteUtcTicks;
        }

        private string GetAssignedLocalFingerprint()
        {
            if (!string.IsNullOrWhiteSpace(localFingerprint))
            {
                return localFingerprint;
            }

            return remoteFingerprint ?? string.Empty;
        }

        private static bool TryParseTimeFingerprint(string fingerprint, out long size, out DateTime updatedAt)
        {
            size = 0L;
            updatedAt = default;

            if (string.IsNullOrWhiteSpace(fingerprint) || fingerprint.StartsWith("md5:", StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts = fingerprint.Split('|');
            if (parts.Length < 2)
            {
                return false;
            }

            string updatedAtText = parts[parts.Length - 1] ?? string.Empty;
            string sizeText = parts[parts.Length - 2] ?? string.Empty;

            return long.TryParse(sizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out size)
                && DateTime.TryParse(
                    updatedAtText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out updatedAt);
        }
    }

    [Serializable]
    internal sealed class ResourceSyncStatusSnapshot
    {
        public int state = (int)ResourceSyncOutcomeState.Unknown;
        public string gameKey = string.Empty;
        public string shareUrl = string.Empty;
        public string resolvedRootUri = string.Empty;
        public string statusMessage = string.Empty;
        public long lastUpdatedUtcTicks;
    }

    [Serializable]
    internal sealed class ResourceSyncLockState
    {
        public string gameKey = string.Empty;
        public long createdUtcTicks;
    }

    internal sealed class ResourceSyncTarget
    {
        public string GameKey = string.Empty;
        public string DisplayName = string.Empty;
        public string DestinationAssetPath = string.Empty;
        public string DestinationAbsolutePath = string.Empty;
        public ResourcesSO ResourceAsset;

        public string OperationLabel => string.IsNullOrWhiteSpace(DisplayName) ? GameKey : DisplayName;
    }

    internal sealed class RemoteResourceEntry
    {
        public string RelativePath = string.Empty;
        public string Uri = string.Empty;
        public string ContextHint = string.Empty;
        public string UpdatedAt = string.Empty;
        public string Md5 = string.Empty;
        public long Size;

        internal string GetFingerprint()
        {
            if (!string.IsNullOrWhiteSpace(Md5))
            {
                return "md5:" + Md5.Trim().ToLowerInvariant();
            }

            return string.Join(
                "|",
                Size.ToString(CultureInfo.InvariantCulture),
                UpdatedAt ?? string.Empty);
        }
    }

    internal sealed class LocalResourceEntry
    {
        public string RelativePath = string.Empty;
        public long Size;
        public long LastWriteUtcTicks;
    }

    internal sealed class ResourceSyncPlan
    {
        public List<RemoteResourceEntry> Downloads = new List<RemoteResourceEntry>();
        public List<string> Deletes = new List<string>();
        public int UnchangedCount;
        public int NewFileCount;
        public int UpdatedFileCount;
        public int TotalRemoteFiles;

        public bool HasChanges => Downloads.Count > 0 || Deletes.Count > 0;
    }

    internal sealed class ResourceSyncResult
    {
        public ResourceSyncTarget Target;
        public ResourceSyncPlan Plan;
        public string ResolvedRootUri = string.Empty;
        public string FailureStep = string.Empty;
        public string ErrorMessage = string.Empty;
        public Exception Exception;
        public bool Success;
        public bool UpToDate;
        public bool ChangesPending;
        public int RemoteFileCount;
        public int DownloadedCount;
        public int RemovedCount;
        public int SkippedCount;

        internal string BuildSummary()
        {
            if (Target == null)
            {
                return string.IsNullOrWhiteSpace(ErrorMessage)
                    ? "Resource sync did not start."
                    : ErrorMessage;
            }

            if (!Success)
            {
                return string.Join(
                    "\n",
                    $"Failed to sync '{Target.OperationLabel}' resources.",
                    $"Step: {FailureStep}",
                    $"Destination: {Target.DestinationAssetPath}",
                    $"Message: {ErrorMessage}");
            }

            if (UpToDate)
            {
                return string.Join(
                    "\n",
                    $"'{Target.OperationLabel}' resources are already up to date.",
                    $"Destination: {Target.DestinationAssetPath}",
                    $"Remote files scanned: {RemoteFileCount}",
                    $"Unchanged files: {SkippedCount}");
            }

            if (ChangesPending)
            {
                int pendingNewFiles = Plan?.NewFileCount ?? 0;
                int pendingUpdatedFiles = Plan?.UpdatedFileCount ?? 0;
                int pendingRemovedFiles = Plan?.Deletes?.Count ?? 0;

                return string.Join(
                    "\n",
                    $"Resource changes detected for '{Target.OperationLabel}'.",
                    $"Destination: {Target.DestinationAssetPath}",
                    $"Resolved share root: {ResolvedRootUri}",
                    $"Remote files scanned: {RemoteFileCount}",
                    $"Files to download: {Plan?.Downloads?.Count ?? 0}",
                    $"New files: {pendingNewFiles}",
                    $"Updated files: {pendingUpdatedFiles}",
                    $"Removed files: {pendingRemovedFiles}",
                    $"Unchanged files: {SkippedCount}");
            }

            int newFiles = Plan?.NewFileCount ?? 0;
            int updatedFiles = Plan?.UpdatedFileCount ?? 0;
            int removedFiles = Plan?.Deletes?.Count ?? RemovedCount;

            return string.Join(
                "\n",
                $"Synced '{Target.OperationLabel}' resources.",
                $"Destination: {Target.DestinationAssetPath}",
                $"Resolved share root: {ResolvedRootUri}",
                $"Remote files scanned: {RemoteFileCount}",
                $"Downloaded files: {DownloadedCount}",
                $"New files: {newFiles}",
                $"Updated files: {updatedFiles}",
                $"Removed files: {removedFiles}",
                $"Unchanged files: {SkippedCount}");
        }
    }
}
#endif

