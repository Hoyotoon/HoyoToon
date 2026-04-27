#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace HoyoToon.Editor.Updater
{
    internal enum UpdateAvailabilityState
    {
        Unknown = 0,
        Checking = 1,
        UpToDate = 2,
        UpdateAvailable = 3,
        LocalAhead = 4,
        Applying = 5,
        Error = 6,
    }

    internal enum UpdaterOperationKind
    {
        None = 0,
        Checking = 1,
        Applying = 2,
    }

    [Serializable]
    public sealed class PackageMetadata
    {
        public string version;
    }

    [Serializable]
    public sealed class UpdaterManifest
    {
        public string version;
        public Dictionary<string, string> files;
        public List<string> keys;
        public List<string> values;

        public Dictionary<string, string> GetFiles()
        {
            var normalizedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (files != null)
            {
                foreach (KeyValuePair<string, string> entry in files)
                {
                    if (!PackageUpdaterStorage.TryNormalizeRelativePath(entry.Key, out string normalizedPath, out string error))
                    {
                        throw new InvalidOperationException($"Updater manifest contains an unsafe path '{entry.Key}': {error}");
                    }

                    normalizedFiles[normalizedPath] = entry.Value ?? string.Empty;
                }

                return normalizedFiles;
            }

            if (keys == null || values == null)
            {
                return normalizedFiles;
            }

            int count = Math.Min(keys.Count, values.Count);
            for (int index = 0; index < count; index++)
            {
                if (!PackageUpdaterStorage.TryNormalizeRelativePath(keys[index], out string normalizedPath, out string error))
                {
                    throw new InvalidOperationException($"Updater manifest contains an unsafe path '{keys[index]}': {error}");
                }

                normalizedFiles[normalizedPath] = values[index] ?? string.Empty;
            }

            return normalizedFiles;
        }
    }

    [Serializable]
    public sealed class GitHubCommitReferenceResponse
    {
        public string sha;
    }

    [Serializable]
    public sealed class PendingInstallState
    {
        public string branch = string.Empty;
        public string remoteContentReference = string.Empty;
        public string localVersion = string.Empty;
        public string remoteVersion = string.Empty;
        public bool cleanMissingFiles;
        public List<string> filesToCopy = new List<string>();
        public List<string> filesToDelete = new List<string>();
    }

    [Serializable]
    internal sealed class UpdaterStatusSnapshot
    {
        public int state = (int)UpdateAvailabilityState.Unknown;
        public string branch = string.Empty;
        public string localVersion = string.Empty;
        public string remoteVersion = string.Empty;
        public string statusMessage = string.Empty;
        public long lastCheckedUtcTicks;
        public bool hasPendingApply;
    }

    [Serializable]
    internal sealed class UpdaterLockState
    {
        public int operation = (int)UpdaterOperationKind.None;
        public long createdUtcTicks;
    }

    internal sealed class UpdatePlan
    {
        public UpdateAvailabilityState AvailabilityState = UpdateAvailabilityState.Unknown;
        public string Branch = string.Empty;
        public string RemoteContentReference = string.Empty;
        public string LocalVersion = string.Empty;
        public string RemoteVersion = string.Empty;
        public string RemoteChangelog = string.Empty;
        public string StatusMessage = string.Empty;
        public bool CleanMissingFiles;
        public Dictionary<string, string> RemoteFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> FilesToCopy = new List<string>();
        public List<string> FilesToDelete = new List<string>();

        public int TotalChanges => (FilesToCopy?.Count ?? 0) + (FilesToDelete?.Count ?? 0);
    }
}
#endif
