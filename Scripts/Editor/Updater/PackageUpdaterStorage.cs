#if UNITY_EDITOR
using System;
using System.IO;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities.Editor;
using HoyoToon.Editor.Utilities.IO;
using HoyoToon.Editor.Utilities.Serialization;
using Utf8Json;
using UnityEditor;

namespace HoyoToon.Editor.Updater
{
    internal static class PackageUpdaterStorage
    {
        private const string MainBranch = "main";
        private const string BetaBranch = "Beta";
        private const string UpdaterStorageRelativePath = "Library/HoyoToon/Updater";
        private const string StagedFilesFolderName = "staged";
        private const string DownloadedFilesFolderName = "downloaded";
        private const string PendingStateFileName = "pending_install.json";
        private const string StatusFileName = "status.json";
        private const string LockFileName = "operation.lock.json";
        private const string PackageMetadataFileName = "package.json";
        private const string CurrentBranchEditorPrefsKey = "HoyoToon.Updater.CurrentBranch";
        private const string AutoCheckEditorPrefsKey = "HoyoToon.Updater.AutoCheckOnStartup";
        private const string LastAutoCheckUtcEditorPrefsKey = "HoyoToon.Updater.LastAutoCheckUtc";
        private const string DefaultBranchMigrationEditorPrefsKey = "HoyoToon.Updater.DefaultBranchMigratedToBeta";

        internal static string DefaultBranch => BetaBranch;

        internal static string MainBranchName => MainBranch;

        internal static string BetaBranchName => BetaBranch;

        internal static string ProjectRootPath => EditorPathUtility.ProjectRootPath;

        internal static string PackageRootPath => GetAbsolutePath(HoyoToonApi.PackageRootAssetPath);

        internal static string PackageMetadataPath => Path.Combine(PackageRootPath, PackageMetadataFileName);

        internal static string UpdaterStoragePath => Path.Combine(ProjectRootPath, ToPlatformPath(UpdaterStorageRelativePath));

        internal static string StagedFilesPath => Path.Combine(UpdaterStoragePath, StagedFilesFolderName);

        internal static string DownloadedFilesPath => Path.Combine(UpdaterStoragePath, DownloadedFilesFolderName);

        internal static string PendingStatePath => Path.Combine(UpdaterStoragePath, PendingStateFileName);

        internal static string StatusPath => Path.Combine(UpdaterStoragePath, StatusFileName);

        internal static string LockPath => Path.Combine(UpdaterStoragePath, LockFileName);

        internal static string CurrentBranch
        {
            get => NormalizeBranch(EditorPrefs.GetString(PrefsKey(CurrentBranchEditorPrefsKey), DefaultBranch));
            set => EditorPrefs.SetString(PrefsKey(CurrentBranchEditorPrefsKey), NormalizeBranch(value));
        }

        internal static bool AutoCheckEnabled
        {
            get => EditorPrefs.GetBool(PrefsKey(AutoCheckEditorPrefsKey), true);
            set => EditorPrefs.SetBool(PrefsKey(AutoCheckEditorPrefsKey), value);
        }

        internal static void EnsureDefaults()
        {
            string currentBranchKey = PrefsKey(CurrentBranchEditorPrefsKey);
            string migrationKey = PrefsKey(DefaultBranchMigrationEditorPrefsKey);

            if (!EditorPrefs.HasKey(currentBranchKey))
            {
                EditorPrefs.SetString(currentBranchKey, DefaultBranch);
            }
            else if (!EditorPrefs.HasKey(migrationKey))
            {
                string normalizedBranch = NormalizeBranch(EditorPrefs.GetString(currentBranchKey, string.Empty));
                EditorPrefs.SetString(currentBranchKey,
                    string.Equals(normalizedBranch, MainBranch, StringComparison.OrdinalIgnoreCase)
                        ? DefaultBranch
                        : normalizedBranch);
            }

            if (!EditorPrefs.HasKey(migrationKey))
            {
                EditorPrefs.SetBool(migrationKey, true);
            }

            if (!EditorPrefs.HasKey(PrefsKey(AutoCheckEditorPrefsKey)))
            {
                EditorPrefs.SetBool(PrefsKey(AutoCheckEditorPrefsKey), true);
            }
        }

        internal static DateTime? GetLastAutoCheckUtc()
        {
            string rawValue = EditorPrefs.GetString(PrefsKey(LastAutoCheckUtcEditorPrefsKey), string.Empty);
            if (string.IsNullOrWhiteSpace(rawValue)
                || !DateTime.TryParse(rawValue, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsedValue))
            {
                return null;
            }

            return parsedValue.ToUniversalTime();
        }

        internal static void SetLastAutoCheckUtc(DateTime utcDateTime)
        {
            EditorPrefs.SetString(PrefsKey(LastAutoCheckUtcEditorPrefsKey), utcDateTime.ToUniversalTime().ToString("O"));
        }

        internal static string NormalizeBranch(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
            {
                return DefaultBranch;
            }

            branch = branch.Trim();

            if (string.Equals(branch, MainBranch, StringComparison.OrdinalIgnoreCase))
            {
                return MainBranch;
            }

            if (string.Equals(branch, BetaBranch, StringComparison.OrdinalIgnoreCase))
            {
                return BetaBranch;
            }

            return DefaultBranch;
        }

        internal static string GetAbsolutePath(string assetRelativePath)
        {
            if (!TryNormalizeRelativePath(assetRelativePath, out string normalized, out string error))
            {
                throw new InvalidOperationException($"Unsafe project-relative path '{assetRelativePath}': {error}");
            }

            return Path.GetFullPath(Path.Combine(ProjectRootPath, ToPlatformPath(normalized)));
        }

        internal static string NormalizeRelativePath(string path)
        {
            return PackagePathUtility.TryNormalizePackageRelativePath(path, out string normalizedPath, out _)
                ? normalizedPath
                : string.Empty;
        }

        internal static bool TryNormalizeRelativePath(string path, out string normalizedPath, out string error)
        {
            return PackagePathUtility.TryNormalizePackageRelativePath(path, out normalizedPath, out error);
        }

        internal static string ToPlatformPath(string path)
        {
            return PackagePathUtility.ToPlatformPath(NormalizeRelativePath(path));
        }

        internal static bool TryResolvePackageFilePath(string relativePath, out string absolutePath, out string error)
        {
            return PackagePathUtility.TryResolveUnderRoot(PackageRootPath, relativePath, out absolutePath, out error);
        }

        internal static bool TryResolveStagedFilePath(string relativePath, out string absolutePath, out string error)
        {
            return PackagePathUtility.TryResolveUnderRoot(StagedFilesPath, relativePath, out absolutePath, out error);
        }

        internal static bool TryResolveBackupFilePath(string backupRootPath, string relativePath, out string absolutePath, out string error)
        {
            return PackagePathUtility.TryResolveUnderRoot(backupRootPath, relativePath, out absolutePath, out error);
        }

        internal static void EnsureUpdaterStorageExists()
        {
            Directory.CreateDirectory(UpdaterStoragePath);
        }

        internal static void EnsureStagingAreaExists()
        {
            Directory.CreateDirectory(StagedFilesPath);
        }

        internal static void ClearStagingArea()
        {
            if (Directory.Exists(StagedFilesPath))
            {
                Directory.Delete(StagedFilesPath, true);
            }
        }

        internal static void EnsureDownloadedFilesAreaExists()
        {
            Directory.CreateDirectory(DownloadedFilesPath);
        }

        internal static void ClearDownloadedFilesArea()
        {
            if (Directory.Exists(DownloadedFilesPath))
            {
                Directory.Delete(DownloadedFilesPath, true);
            }
        }

        internal static void ClearPendingArtifacts()
        {
            ClearStagingArea();
            ClearDownloadedFilesArea();

            if (File.Exists(PendingStatePath))
            {
                File.Delete(PendingStatePath);
            }
        }

        internal static bool HasPendingState()
        {
            return File.Exists(PendingStatePath);
        }

        internal static string GetLocalPackageVersion()
        {
            if (!File.Exists(PackageMetadataPath))
            {
                return "unknown";
            }

            try
            {
                string json = File.ReadAllText(PackageMetadataPath);
                PackageMetadata packageMetadata = JsonSerializer.Deserialize<PackageMetadata>(json, HoyoToonApi.JsonResolver);
                return string.IsNullOrWhiteSpace(packageMetadata?.version)
                    ? "unknown"
                    : packageMetadata.version.Trim();
            }
            catch
            {
                return "unknown";
            }
        }

        internal static UpdaterStatusSnapshot ReadStatusSnapshot()
        {
            UpdaterStatusSnapshot snapshot = ReadJsonFile<UpdaterStatusSnapshot>(StatusPath);
            if (snapshot == null)
            {
                return null;
            }

            snapshot.branch = string.IsNullOrWhiteSpace(snapshot.branch) ? CurrentBranch : NormalizeBranch(snapshot.branch);
            snapshot.localVersion = snapshot.localVersion ?? string.Empty;
            snapshot.remoteVersion = snapshot.remoteVersion ?? string.Empty;
            snapshot.statusMessage = snapshot.statusMessage ?? string.Empty;
            snapshot.hasPendingApply = HasPendingState();
            return snapshot;
        }

        internal static void WriteStatusSnapshot(UpdaterStatusSnapshot snapshot)
        {
            EnsureUpdaterStorageExists();
            WriteJsonFile(StatusPath, snapshot ?? new UpdaterStatusSnapshot());
        }

        internal static UpdaterLockState ReadLockState()
        {
            return ReadJsonFile<UpdaterLockState>(LockPath);
        }

        internal static PendingInstallState ReadPendingState()
        {
            PendingInstallState state = ReadJsonFile<PendingInstallState>(PendingStatePath);
            if (state == null)
            {
                return null;
            }

            state.branch = string.IsNullOrWhiteSpace(state.branch) ? CurrentBranch : NormalizeBranch(state.branch);
            state.remoteCommitSha = NormalizeStoredReference(state.remoteCommitSha);
            state.remoteContentReference = NormalizeStoredReference(state.remoteContentReference);
            if (string.IsNullOrWhiteSpace(state.remoteCommitSha))
            {
                state.remoteCommitSha = state.remoteContentReference;
            }
            state.localVersion = state.localVersion ?? string.Empty;
            state.remoteVersion = state.remoteVersion ?? string.Empty;
            state.filesToCopy = state.filesToCopy ?? new System.Collections.Generic.List<string>();
            state.filesToDelete = state.filesToDelete ?? new System.Collections.Generic.List<string>();
            return state;
        }

        internal static void WritePendingState(PendingInstallState state)
        {
            EnsureUpdaterStorageExists();
            WriteJsonFile(PendingStatePath, state ?? new PendingInstallState());
        }

        internal static void WriteLockState(UpdaterLockState lockState)
        {
            EnsureUpdaterStorageExists();
            WriteJsonFile(LockPath, lockState ?? new UpdaterLockState());
        }

        internal static void ClearLockState()
        {
            if (File.Exists(LockPath))
            {
                File.Delete(LockPath);
            }
        }

        private static T ReadJsonFile<T>(string filePath) where T : class
        {
            return EditorJsonFileStore.TryRead(filePath, out T value) ? value : null;
        }

        private static void WriteJsonFile<T>(string filePath, T value) where T : class
        {
            EditorJsonFileStore.WriteAtomic(filePath, value);
        }

        private static string NormalizeStoredReference(string reference)
        {
            return string.IsNullOrWhiteSpace(reference)
                ? string.Empty
                : reference.Trim();
        }

        private static string PrefsKey(string key)
        {
            return HoyoToonEditorPrefs.ProjectKey(key);
        }
    }
}
#endif
