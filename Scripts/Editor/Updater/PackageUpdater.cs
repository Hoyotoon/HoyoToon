#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Networking;
using Utf8Json;

using HoyoToon.Editor.API;
using HoyoToon.Editor.UI.Windows;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Updater
{
    [InitializeOnLoad]
    internal static class PackageUpdater
    {
        internal enum UpdateAvailabilityState
        {
            Unknown = 0,
            Checking = 1,
            UpToDate = 2,
            UpdateAvailable = 3,
            LocalAhead = 4,
            Error = 5
        }

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private const string Owner = "HoyoToon";
        private const string Repo = "HoyoToon";
        private const string MainBranch = "main";
        private const string BetaBranch = "Beta";
        private static readonly TimeSpan AutoCheckInterval = TimeSpan.FromHours(6);
        private const double AutoCheckHeartbeatSeconds = 60.0;

        private const string MenuRoot = "HoyoToon/Updater/";
        private const string MenuCheck = MenuRoot + "Check For Updates";
        private const string MenuBranchMain = MenuRoot + "Branch/Main";
        private const string MenuBranchBeta = MenuRoot + "Branch/Beta";
        private const string MenuAutoCheck = MenuRoot + "Automatic Check On Startup";
        private const string MenuCleanOnSwitch = MenuRoot + "Clean Missing Files On Branch Switch";

        private const string PackageRootRelative = "Packages/com.hoyotoon.hoyotoon";
        private const string PendingDirectoryRelative = "Library/HoyoToon/Updater";
        private const string PendingStateFileName = "pending_install.json";
        private const string LockFileName = "package_update.lock";

        private static bool s_EditorReadyHandled;
        private static double s_NextAutoCheckHeartbeat;
        private static UpdateStatusSnapshot s_cachedStatus;

        static PackageUpdater()
        {
            EnsureDefaults();
            s_cachedStatus = LoadPersistedStatus();
            EditorApplication.delayCall += OnEditorReady;
            EditorApplication.update += OnEditorUpdate;
        }

        internal sealed class UpdateStatusSnapshot
        {
            public UpdateAvailabilityState State;
            public string Branch;
            public string LocalVersion;
            public string RemoteVersion;
            public string StatusMessage;
            public DateTime LastCheckedUtc;
        }

        private sealed class UpdatePlan
        {
            public UpdateAvailabilityState AvailabilityState;
            public string Branch;
            public string LocalVersion;
            public string RemoteVersion;
            public string RemoteChangelog;
            public string StatusMessage;
            public bool CleanMissingFiles;
            public Dictionary<string, string> RemoteFiles;
            public List<string> FilesToCopy;
            public List<string> FilesToDelete;

            public int TotalChanges => (FilesToCopy?.Count ?? 0) + (FilesToDelete?.Count ?? 0);
        }

        internal static string NormalizeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Replace('\\', '/').TrimStart('/');
        }

        internal static string ToPlatformPath(string path)
        {
            return NormalizeRelativePath(path).Replace('/', Path.DirectorySeparatorChar);
        }

        internal static string BuildRawFileUrl(string baseUrl, string relativePath)
        {
            string normalizedBase = (baseUrl ?? string.Empty).TrimEnd('/');
            string[] segments = NormalizeRelativePath(relativePath)
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                return normalizedBase;
            }

            string escapedPath = string.Join("/", segments.Select(Uri.EscapeDataString).ToArray());
            return string.Concat(normalizedBase, "/", escapedPath);
        }

        internal static string BuildCacheBustedUrl(string url)
        {
            string separator = (url ?? string.Empty).Contains("?") ? "&" : "?";
            return string.Concat(url ?? string.Empty, separator, "ts=", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        }

        internal static void ApplyNoCacheHeaders(UnityWebRequest request)
        {
            if (request == null)
            {
                return;
            }

            request.SetRequestHeader("Cache-Control", "no-cache, no-store, max-age=0");
            request.SetRequestHeader("Pragma", "no-cache");
            request.SetRequestHeader("Expires", "0");
        }

        private static string CurrentBranch => NormalizeBranch(EditorPrefs.GetString(PrefsKeys.UpdaterCurrentBranch, MainBranch));

        private static bool AutoCheckEnabled => EditorPrefs.GetBool(PrefsKeys.UpdaterAutoCheckOnStartup, true);

        private static bool CleanOnBranchSwitch => EditorPrefs.GetBool(PrefsKeys.UpdaterCleanOnSwitch, false);

        private static bool IsBusy => File.Exists(LockFilePath) || EditorCoroutine.IsRunning;

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string PackageRoot => Path.GetFullPath(Path.Combine(ProjectRoot, PackageRootRelative));

        private static string PendingDirectory => Path.GetFullPath(Path.Combine(ProjectRoot, PendingDirectoryRelative));

        private static string TempRoot => Path.Combine(PendingDirectory, "staged");

        private static string PendingStatePath => Path.Combine(PendingDirectory, PendingStateFileName);

        private static string LockFilePath => Path.Combine(PendingDirectory, LockFileName);

        private static string ManifestUrl =>
            $"https://raw.githubusercontent.com/{Owner}/{Repo}/{CurrentBranch}/updater_manifest.json";

        private static string RawBaseUrl =>
            $"https://raw.githubusercontent.com/{Owner}/{Repo}/{CurrentBranch}";

        private static void EnsureDefaults()
        {
            if (!EditorPrefs.HasKey(PrefsKeys.UpdaterCurrentBranch))
            {
                EditorPrefs.SetString(PrefsKeys.UpdaterCurrentBranch, MainBranch);
            }

            if (!EditorPrefs.HasKey(PrefsKeys.UpdaterAutoCheckOnStartup))
            {
                EditorPrefs.SetBool(PrefsKeys.UpdaterAutoCheckOnStartup, true);
            }

            if (!EditorPrefs.HasKey(PrefsKeys.UpdaterCleanOnSwitch))
            {
                EditorPrefs.SetBool(PrefsKeys.UpdaterCleanOnSwitch, false);
            }
        }

        private static void OnEditorReady()
        {
            if (s_EditorReadyHandled)
            {
                return;
            }

            s_EditorReadyHandled = true;

            if (Application.isBatchMode)
            {
                return;
            }

            ResumePendingInstallIfNeeded();

            s_NextAutoCheckHeartbeat = EditorApplication.timeSinceStartup + AutoCheckHeartbeatSeconds;
            TryRunAutomaticCheck(force: false);
        }

        private static void OnEditorUpdate()
        {
            if (Application.isBatchMode || !AutoCheckEnabled || EditorApplication.timeSinceStartup < s_NextAutoCheckHeartbeat)
            {
                return;
            }

            s_NextAutoCheckHeartbeat = EditorApplication.timeSinceStartup + AutoCheckHeartbeatSeconds;
            TryRunAutomaticCheck(force: false);
        }

        [MenuItem(MenuCheck, false, 10)]
        private static void CheckForUpdatesMenu()
        {
            StartCheck(showNoUpdatesDialog: true, automatic: false, cleanMissingFiles: false);
        }

        [MenuItem(MenuBranchMain, false, 20)]
        private static void SetMainBranchMenu()
        {
            SetBranch(MainBranch);
        }

        [MenuItem(MenuBranchMain, true)]
        private static bool SetMainBranchMenuValidate()
        {
            Menu.SetChecked(MenuBranchMain, string.Equals(CurrentBranch, MainBranch, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        [MenuItem(MenuBranchBeta, false, 21)]
        private static void SetBetaBranchMenu()
        {
            SetBranch(BetaBranch);
        }

        [MenuItem(MenuBranchBeta, true)]
        private static bool SetBetaBranchMenuValidate()
        {
            Menu.SetChecked(MenuBranchBeta, string.Equals(CurrentBranch, BetaBranch, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        [MenuItem(MenuAutoCheck, false, 30)]
        private static void ToggleAutoCheckMenu()
        {
            bool enabled = !AutoCheckEnabled;
            EditorPrefs.SetBool(PrefsKeys.UpdaterAutoCheckOnStartup, enabled);
            DialogWindow.ShowInfo("Updater Settings", $"Automatic update checks have been {(enabled ? "enabled" : "disabled")}.");
        }

        [MenuItem(MenuAutoCheck, true)]
        private static bool ToggleAutoCheckMenuValidate()
        {
            Menu.SetChecked(MenuAutoCheck, AutoCheckEnabled);
            return true;
        }

        [MenuItem(MenuCleanOnSwitch, false, 31)]
        private static void ToggleCleanOnSwitchMenu()
        {
            bool enabled = !CleanOnBranchSwitch;
            EditorPrefs.SetBool(PrefsKeys.UpdaterCleanOnSwitch, enabled);
            DialogWindow.ShowInfo("Updater Settings", $"Branch-switch cleanup has been {(enabled ? "enabled" : "disabled")}.");
        }

        [MenuItem(MenuCleanOnSwitch, true)]
        private static bool ToggleCleanOnSwitchMenuValidate()
        {
            Menu.SetChecked(MenuCleanOnSwitch, CleanOnBranchSwitch);
            return true;
        }

        private static string NormalizeBranch(string branch)
        {
            if (string.Equals(branch, BetaBranch, StringComparison.OrdinalIgnoreCase))
            {
                return BetaBranch;
            }

            return MainBranch;
        }

        private static void TryRunAutomaticCheck(bool force)
        {
            if (IsBusy || !AutoCheckEnabled)
            {
                return;
            }

            if (!force && !ShouldRunAutomaticCheckNow())
            {
                return;
            }

            RecordAutomaticCheckTimestamp();
            StartCheck(showNoUpdatesDialog: false, automatic: true, cleanMissingFiles: false);
        }

        internal static void HandleManagerHeaderBadgeClick()
        {
            UpdateStatusSnapshot status = GetStatusSnapshot();
            if (status != null && status.State == UpdateAvailabilityState.Checking)
            {
                return;
            }

            bool showUpdaterDialog = status != null && status.State == UpdateAvailabilityState.UpdateAvailable;
            StartCheck(showNoUpdatesDialog: false, automatic: !showUpdaterDialog, cleanMissingFiles: false);
        }

        private static bool ShouldRunAutomaticCheckNow()
        {
            string value = EditorPrefs.GetString(PrefsKeys.UpdaterLastAutoCheckUtc, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime lastCheckUtc))
            {
                return true;
            }

            return DateTime.UtcNow - lastCheckUtc >= AutoCheckInterval;
        }

        private static void RecordAutomaticCheckTimestamp()
        {
            EditorPrefs.SetString(PrefsKeys.UpdaterLastAutoCheckUtc, DateTime.UtcNow.ToString("O"));
        }

        private static void SetBranch(string branch)
        {
            string normalized = NormalizeBranch(branch);
            if (string.Equals(CurrentBranch, normalized, StringComparison.OrdinalIgnoreCase))
            {
                DialogWindow.ShowInfo("Updater Branch", $"HoyoToon is already using the '{normalized}' branch.");
                return;
            }

            if (IsBusy)
            {
                DialogWindow.ShowWarning("Updater Busy", "The updater is already running. Wait for it to finish before switching branches.");
                return;
            }

            EditorPrefs.SetString(PrefsKeys.UpdaterCurrentBranch, normalized);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Info, $"Updater branch switched to '{normalized}'.");

            string message = $"HoyoToon will now use the '{normalized}' branch.\n\n" +
                             $"Clean missing files on switch: {(CleanOnBranchSwitch ? "Yes" : "No")}\n\n" +
                             "Check for updates on the new branch now?";

            DialogWindow.ShowOkCancel("Updater Branch Changed", message, MessageType.Info, onResult: confirmed =>
            {
                if (!confirmed)
                {
                    return;
                }

                StartCheck(showNoUpdatesDialog: true, automatic: false, cleanMissingFiles: CleanOnBranchSwitch);
            });
        }

        private static void StartCheck(bool showNoUpdatesDialog, bool automatic, bool cleanMissingFiles)
        {
            if (IsBusy)
            {
                if (!automatic)
                {
                    DialogWindow.ShowWarning("Updater Busy", "The updater is already running.");
                }
                return;
            }

            SetStatus(
                UpdateAvailabilityState.Checking,
                PackageVersionUtility.GetLocalPackageVersion(forceRefresh: true),
                null,
                $"Checking '{CurrentBranch}' for updates...",
                persist: false);

            if (!EditorCoroutine.Start(CheckRoutine(showNoUpdatesDialog, automatic, cleanMissingFiles)))
            {
                RestorePersistedStatus();
                if (!automatic)
                {
                    DialogWindow.ShowWarning("Updater Busy", "The updater is already running.");
                }
            }
        }

        private static IEnumerator CheckRoutine(bool showNoUpdatesDialog, bool automatic, bool cleanMissingFiles)
        {
            bool showProgress = !automatic;
            if (showProgress)
            {
                ProgressDialog.Start("HoyoToon Updater", $"Checking '{CurrentBranch}' for updates...");
            }

            UpdatePlan plan = null;
            string error = null;

            yield return BuildPlanRoutine(cleanMissingFiles, showProgress, value =>
            {
                plan = value;
            }, value =>
            {
                error = value;
            });

            if (showProgress)
            {
                ProgressDialog.End();
            }

            if (!string.IsNullOrEmpty(error))
            {
                SetStatus(
                    UpdateAvailabilityState.Error,
                    PackageVersionUtility.GetLocalPackageVersion(forceRefresh: true),
                    null,
                    error,
                    persist: true);
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Error, error);
                if (!automatic)
                {
                    DialogWindow.ShowError("Updater Check Failed", error);
                }
                yield break;
            }

            if (plan == null)
            {
                RestorePersistedStatus();
                yield break;
            }

            SetStatus(plan.AvailabilityState, plan.LocalVersion, plan.RemoteVersion, plan.StatusMessage, persist: true);

            if (plan.TotalChanges == 0)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Info, $"No updates found on '{plan.Branch}'.");
                if (showNoUpdatesDialog)
                {
                    string message = string.IsNullOrEmpty(plan.StatusMessage)
                        ? $"HoyoToon is already up to date on the '{plan.Branch}' branch."
                        : plan.StatusMessage;
                    DialogWindow.ShowInfo("HoyoToon Updater", message);
                }
                yield break;
            }

            ShowUpdateAvailableDialog(plan, automatic);
        }

        private static IEnumerator BuildPlanRoutine(bool cleanMissingFiles, bool showProgress, Action<UpdatePlan> onSuccess, Action<string> onError)
        {
            if (showProgress)
            {
                ProgressDialog.Update(0.1f, "Downloading updater manifest...");
            }

            var manifestRequest = UnityWebRequest.Get(BuildCacheBustedUrl(ManifestUrl));
            ApplyNoCacheHeaders(manifestRequest);
            yield return manifestRequest.SendWebRequest();

            try
            {
                if (manifestRequest.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Failed to download updater manifest from '{CurrentBranch}': {manifestRequest.error}");
                    yield break;
                }

                if (!Api.Parser.TryParse(manifestRequest.downloadHandler.data, out Manifest manifest, out string manifestError) || manifest == null)
                {
                    onError?.Invoke($"Updater manifest was empty or invalid JSON: {manifestError}");
                    yield break;
                }

                Dictionary<string, string> remoteFiles = manifest.GetFiles();
                if (remoteFiles.Count == 0)
                {
                    onError?.Invoke("Updater manifest did not contain any files.");
                    yield break;
                }

                string localVersion = GetLocalPackageVersion();
                string remoteVersion = string.IsNullOrEmpty(manifest.version) ? "unknown" : manifest.version;
                int versionComparison = CompareVersionStrings(localVersion, remoteVersion);
                if (versionComparison > 0)
                {
                    onSuccess?.Invoke(new UpdatePlan
                    {
                        AvailabilityState = UpdateAvailabilityState.LocalAhead,
                        Branch = CurrentBranch,
                        LocalVersion = localVersion,
                        RemoteVersion = remoteVersion,
                        StatusMessage = $"Local HoyoToon version {localVersion} is newer than remote branch '{CurrentBranch}' version {remoteVersion}. Mana why are you trying to update our development version?",
                        CleanMissingFiles = cleanMissingFiles,
                        RemoteFiles = remoteFiles,
                        FilesToCopy = new List<string>(),
                        FilesToDelete = new List<string>()
                    });
                    yield break;
                }

                if (string.Equals(localVersion, remoteVersion, StringComparison.OrdinalIgnoreCase))
                {
                    onSuccess?.Invoke(new UpdatePlan
                    {
                        AvailabilityState = UpdateAvailabilityState.UpToDate,
                        Branch = CurrentBranch,
                        LocalVersion = localVersion,
                        RemoteVersion = remoteVersion,
                        CleanMissingFiles = cleanMissingFiles,
                        RemoteFiles = remoteFiles,
                        FilesToCopy = new List<string>(),
                        FilesToDelete = new List<string>()
                    });
                    yield break;
                }

                if (showProgress)
                {
                    ProgressDialog.Update(0.35f, "Comparing local package files...");
                }

                var filesToCopy = BuildPatchList(remoteFiles);
                var filesToDelete = FindMissingLocalFiles(remoteFiles);
                string remoteChangelog = null;
                if ((filesToCopy.Count + filesToDelete.Count) > 0)
                {
                    if (showProgress)
                    {
                        ProgressDialog.Update(0.55f, "Downloading remote changelog...");
                    }

                    yield return DownloadRemoteTextRoutine("changelog.md", value =>
                    {
                        remoteChangelog = value;
                    });
                }

                onSuccess?.Invoke(new UpdatePlan
                {
                    AvailabilityState = DetermineAvailabilityState(localVersion, remoteVersion),
                    Branch = CurrentBranch,
                    LocalVersion = localVersion,
                    RemoteVersion = remoteVersion,
                    RemoteChangelog = remoteChangelog,
                    CleanMissingFiles = cleanMissingFiles,
                    RemoteFiles = remoteFiles,
                    FilesToCopy = filesToCopy,
                    FilesToDelete = filesToDelete
                });
            }
            finally
            {
                manifestRequest.Dispose();
            }
        }

        private static void ShowUpdateAvailableDialog(UpdatePlan plan, bool automatic)
        {
            string message = $"Branch: {plan.Branch}\n" +
                             $"Current version: {plan.LocalVersion}\n" +
                             $"Available version: {plan.RemoteVersion}\n\n" +
                             $"Files to update: {plan.FilesToCopy.Count}\n" +
                             $"Files to remove: {plan.FilesToDelete.Count}\n\n" +
                             "Would you like to install the update now?";

            if (!string.IsNullOrWhiteSpace(plan.RemoteChangelog))
            {
                message += "\n\n# Remote Changelog\n\n" + plan.RemoteChangelog.Trim();
            }

            string[] buttons = automatic
                ? new[] { "Update Now", "Later", "Disable Auto Check" }
                : new[] { "Update Now", "Later" };

            DialogWindow.ShowCustom(
                "HoyoToon Update Available",
                message,
                MessageType.Info,
                buttons,
                defaultIndex: 0,
                cancelIndex: 1,
                onResultIndex: result =>
                {
                    if (result == 0)
                    {
                        StartInstall(plan);
                        return;
                    }

                    if (automatic && result == 2)
                    {
                        EditorPrefs.SetBool(PrefsKeys.UpdaterAutoCheckOnStartup, false);
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Info, "Automatic updater checks disabled by user.");
                    }
                });
        }

        private static void StartInstall(UpdatePlan plan)
        {
            if (plan == null)
            {
                return;
            }

            if (IsBusy)
            {
                DialogWindow.ShowWarning("Updater Busy", "The updater is already running.");
                return;
            }

            if (!EditorCoroutine.Start(InstallRoutine(plan)))
            {
                DialogWindow.ShowWarning("Updater Busy", "The updater is already running.");
            }
        }

        private static IEnumerator InstallRoutine(UpdatePlan plan)
        {
            string completionDialogTitle = null;
            string completionDialogMessage = null;
            PrepareLock();
            ProgressDialog.Start("HoyoToon Updater", $"Updating HoyoToon from '{plan.Branch}'...");
            try
            {
                EnsureCleanStagingArea();

                if (plan.FilesToCopy.Count > 0)
                {
                    var downloadResult = new UpdateDownloader.DownloadResult();
                    yield return UpdateDownloader.DownloadFiles(
                        plan.FilesToCopy,
                        RawBaseUrl,
                        TempRoot,
                        downloadResult,
                        (progress, message) => ProgressDialog.Update(progress * 0.75f, message));

                    if (!downloadResult.Success)
                    {
                        Fail(downloadResult.ErrorMessage);
                        yield break;
                    }
                }

                WritePendingState(plan);
                ProgressDialog.Update(0.85f, "Applying staged update...");

                if (!ApplyPendingState(ReadPendingState(), out string applyError))
                {
                    Fail(applyError);
                    yield break;
                }

                ProgressDialog.Update(1f, "Update complete.");
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Info, $"Updater applied branch '{plan.Branch}' ({plan.TotalChanges} changes).");
                SetStatus(
                    UpdateAvailabilityState.UpToDate,
                    string.IsNullOrWhiteSpace(plan.RemoteVersion) ? plan.LocalVersion : plan.RemoteVersion,
                    string.IsNullOrWhiteSpace(plan.RemoteVersion) ? plan.LocalVersion : plan.RemoteVersion,
                    $"HoyoToon is up to date on the '{plan.Branch}' branch.",
                    persist: true);
                completionDialogTitle = "HoyoToon Updated";
                completionDialogMessage = $"HoyoToon was updated from the '{plan.Branch}' branch.\n\nUpdated files: {plan.FilesToCopy.Count}\nRemoved files: {plan.FilesToDelete.Count}";
            }
            finally
            {
                ProgressDialog.End();
                ReleaseLock();
                ShowDeferredInfoDialog(completionDialogTitle, completionDialogMessage);
            }
        }

        private static void ResumePendingInstallIfNeeded()
        {
            PendingInstallState pending = ReadPendingState();
            if (pending == null)
            {
                return;
            }

            bool hasStagedFiles = pending.filesToCopy.Count == 0 || Directory.Exists(TempRoot);
            if (!hasStagedFiles)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Warning, "Pending updater state found without staged files. Clearing pending state.");
                CleanupPendingArtifacts();
                return;
            }

            if (!EditorCoroutine.Start(ResumePendingRoutine()))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Warning, "Unable to resume staged updater install because another editor coroutine is running.");
            }
        }

        private static IEnumerator ResumePendingRoutine()
        {
            string completionDialogTitle = null;
            string completionDialogMessage = null;
            PrepareLock();
            ProgressDialog.Start("HoyoToon Updater", "Resuming a staged HoyoToon update...");
            try
            {
                ProgressDialog.Update(0.5f, "Applying staged files...");
                if (!ApplyPendingState(ReadPendingState(), out string error))
                {
                    Fail(error);
                    yield break;
                }

                ProgressDialog.Update(1f, "Update complete.");
                completionDialogTitle = "HoyoToon Updated";
                completionDialogMessage = "A previously staged HoyoToon update has been applied.";
            }
            finally
            {
                ProgressDialog.End();
                ReleaseLock();
                ShowDeferredInfoDialog(completionDialogTitle, completionDialogMessage);
            }
        }

        private static List<string> BuildPatchList(Dictionary<string, string> remoteFiles)
        {
            var patch = new List<string>();
            foreach (KeyValuePair<string, string> file in remoteFiles)
            {
                if (file.Key.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string localPath = Path.Combine(PackageRoot, ToPlatformPath(file.Key));
                if (!File.Exists(localPath))
                {
                    patch.Add(file.Key);
                    continue;
                }

                string localHash = GetHash(localPath);
                if (!string.Equals(localHash, file.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    patch.Add(file.Key);
                }
            }

            return patch;
        }

        private static List<string> FindMissingLocalFiles(Dictionary<string, string> remoteFiles)
        {
            var extras = new List<string>();
            if (!Directory.Exists(PackageRoot))
            {
                return extras;
            }

            UpdaterKeepRules keepRules = UpdaterKeepRules.Load(PackageRoot);
            var remoteSet = new HashSet<string>(remoteFiles.Keys.Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);
            foreach (string localFile in Directory.GetFiles(PackageRoot, "*", SearchOption.AllDirectories))
            {
                if (localFile.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = NormalizeRelativePath(GetRelativePath(PackageRoot, localFile));
                if (keepRules.IsKept(relativePath, isDirectory: false))
                {
                    continue;
                }

                if (!remoteSet.Contains(relativePath))
                {
                    extras.Add(relativePath);
                }
            }

            return extras;
        }

        private static bool ApplyPendingState(PendingInstallState pending, out string error)
        {
            error = null;
            if (pending == null)
            {
                error = "No staged updater state was available to apply.";
                return false;
            }

            UpdaterKeepRules keepRules = UpdaterKeepRules.Load(PackageRoot);
            AssetDatabase.DisallowAutoRefresh();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string relativePath in pending.filesToCopy)
                {
                    string source = Path.Combine(TempRoot, ToPlatformPath(relativePath));
                    string destination = Path.Combine(PackageRoot, ToPlatformPath(relativePath));

                    if (!File.Exists(source))
                    {
                        error = $"Staged file is missing: {relativePath}";
                        return false;
                    }

                    string directory = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.Copy(source, destination, true);
                }

                foreach (string relativePath in pending.filesToDelete)
                {
                    if (keepRules.IsKept(relativePath, isDirectory: false))
                    {
                        continue;
                    }

                    string destination = Path.Combine(PackageRoot, ToPlatformPath(relativePath));
                    if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }

                    string metaPath = destination + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }
                }
            }
            catch (Exception ex)
            {
                error = $"Failed to apply staged update: {ex.Message}";
                return false;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            CleanupPendingArtifacts();
            return true;
        }

        private static void WritePendingState(UpdatePlan plan)
        {
            Directory.CreateDirectory(PendingDirectory);
            var state = new PendingInstallState
            {
                branch = plan.Branch,
                cleanMissingFiles = plan.CleanMissingFiles,
                filesToCopy = new List<string>(plan.FilesToCopy),
                filesToDelete = new List<string>(plan.FilesToDelete)
            };

            byte[] bytes = JsonSerializer.PrettyPrintByteArray(JsonSerializer.Serialize(state));
            File.WriteAllBytes(PendingStatePath, bytes);
        }

        private static PendingInstallState ReadPendingState()
        {
            if (!File.Exists(PendingStatePath))
            {
                return null;
            }

            try
            {
                if (!Api.Parser.TryParseFile(PendingStatePath, out PendingInstallState state, out string parseError) || state == null)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Error, $"Failed to parse pending updater state: {parseError}");
                    return null;
                }

                state.filesToCopy = state.filesToCopy ?? new List<string>();
                state.filesToDelete = state.filesToDelete ?? new List<string>();
                return state;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Error, $"Failed to read pending updater state: {ex.Message}");
                return null;
            }
        }

        private static void PrepareLock()
        {
            Directory.CreateDirectory(PendingDirectory);
            File.WriteAllText(LockFilePath, DateTime.UtcNow.ToString("O"));
        }

        private static void ReleaseLock()
        {
            if (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
            }
        }

        private static void EnsureCleanStagingArea()
        {
            if (Directory.Exists(TempRoot))
            {
                Directory.Delete(TempRoot, true);
            }

            Directory.CreateDirectory(TempRoot);
        }

        private static void CleanupPendingArtifacts()
        {
            if (Directory.Exists(TempRoot))
            {
                Directory.Delete(TempRoot, true);
            }

            if (File.Exists(PendingStatePath))
            {
                File.Delete(PendingStatePath);
            }
        }

        private static void Fail(string message)
        {
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Error, message);
            CleanupPendingArtifacts();
            DialogWindow.ShowError("HoyoToon Updater", message);
        }

        private static string GetLocalPackageVersion()
        {
            return PackageVersionUtility.GetLocalPackageVersion(forceRefresh: true);
        }

        private static void ShowDeferredInfoDialog(string title, string message)
        {
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
            {
                return;
            }

            EditorApplication.delayCall += () => DialogWindow.ShowInfo(title, message);
        }

        private static IEnumerator DownloadRemoteTextRoutine(string relativePath, Action<string> onSuccess)
        {
            string requestUrl = BuildCacheBustedUrl(BuildRawFileUrl(RawBaseUrl, relativePath));
            var request = UnityWebRequest.Get(requestUrl);
            ApplyNoCacheHeaders(request);
            yield return request.SendWebRequest();

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    yield break;
                }

                onSuccess?.Invoke(request.downloadHandler.text);
            }
            finally
            {
                request.Dispose();
            }
        }

        private static int CompareVersionStrings(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (TryParseVersionParts(left, out List<int> leftParts) && TryParseVersionParts(right, out List<int> rightParts))
            {
                int maxCount = Math.Max(leftParts.Count, rightParts.Count);
                for (int i = 0; i < maxCount; i++)
                {
                    int leftValue = i < leftParts.Count ? leftParts[i] : 0;
                    int rightValue = i < rightParts.Count ? rightParts[i] : 0;
                    if (leftValue != rightValue)
                    {
                        return leftValue.CompareTo(rightValue);
                    }
                }

                return 0;
            }

            return string.Compare(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        internal static UpdateStatusSnapshot GetStatusSnapshot()
        {
            if (s_cachedStatus == null)
            {
                s_cachedStatus = LoadPersistedStatus();
            }

            if (s_cachedStatus == null)
            {
                return CreateStatusSnapshot(
                    UpdateAvailabilityState.Unknown,
                    PackageVersionUtility.GetLocalPackageVersion(),
                    null,
                    $"No update check has been recorded for '{CurrentBranch}' yet.",
                    DateTime.MinValue,
                    CurrentBranch);
            }

            if (s_cachedStatus.State == UpdateAvailabilityState.Checking)
            {
                return s_cachedStatus;
            }

            if (!string.Equals(s_cachedStatus.Branch, CurrentBranch, StringComparison.OrdinalIgnoreCase))
            {
                return CreateStatusSnapshot(
                    UpdateAvailabilityState.Unknown,
                    PackageVersionUtility.GetLocalPackageVersion(),
                    null,
                    $"No update check has been recorded for '{CurrentBranch}' yet.",
                    DateTime.MinValue,
                    CurrentBranch);
            }

            return s_cachedStatus;
        }

        private static UpdateAvailabilityState DetermineAvailabilityState(string localVersion, string remoteVersion)
        {
            if (string.Equals(localVersion, "unknown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(remoteVersion, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                return UpdateAvailabilityState.Unknown;
            }

            int comparison = CompareVersionStrings(localVersion, remoteVersion);
            if (comparison < 0)
            {
                return UpdateAvailabilityState.UpdateAvailable;
            }

            if (comparison > 0)
            {
                return UpdateAvailabilityState.LocalAhead;
            }

            return UpdateAvailabilityState.UpToDate;
        }

        private static void SetStatus(UpdateAvailabilityState state, string localVersion, string remoteVersion, string statusMessage, bool persist)
        {
            DateTime lastCheckedUtc = state == UpdateAvailabilityState.Checking
                ? (s_cachedStatus?.LastCheckedUtc ?? DateTime.MinValue)
                : DateTime.UtcNow;

            s_cachedStatus = CreateStatusSnapshot(state, localVersion, remoteVersion, statusMessage, lastCheckedUtc, CurrentBranch);

            if (persist)
            {
                PersistStatus(s_cachedStatus);
            }

            EditorApplication.delayCall += InternalEditorUtility.RepaintAllViews;
        }

        private static UpdateStatusSnapshot CreateStatusSnapshot(
            UpdateAvailabilityState state,
            string localVersion,
            string remoteVersion,
            string statusMessage,
            DateTime lastCheckedUtc,
            string branch)
        {
            return new UpdateStatusSnapshot
            {
                State = state,
                Branch = string.IsNullOrWhiteSpace(branch) ? CurrentBranch : branch,
                LocalVersion = string.IsNullOrWhiteSpace(localVersion) ? "unknown" : localVersion,
                RemoteVersion = string.IsNullOrWhiteSpace(remoteVersion) ? "unknown" : remoteVersion,
                StatusMessage = statusMessage ?? string.Empty,
                LastCheckedUtc = lastCheckedUtc
            };
        }

        private static void PersistStatus(UpdateStatusSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            EditorPrefs.SetInt(PrefsKeys.UpdaterStatusState, (int)snapshot.State);
            EditorPrefs.SetString(PrefsKeys.UpdaterStatusBranch, snapshot.Branch ?? string.Empty);
            EditorPrefs.SetString(PrefsKeys.UpdaterStatusLocalVersion, snapshot.LocalVersion ?? string.Empty);
            EditorPrefs.SetString(PrefsKeys.UpdaterStatusRemoteVersion, snapshot.RemoteVersion ?? string.Empty);
            EditorPrefs.SetString(PrefsKeys.UpdaterStatusMessage, snapshot.StatusMessage ?? string.Empty);
            EditorPrefs.SetString(PrefsKeys.UpdaterStatusLastCheckedUtc, snapshot.LastCheckedUtc == DateTime.MinValue ? string.Empty : snapshot.LastCheckedUtc.ToString("O"));
        }

        private static UpdateStatusSnapshot LoadPersistedStatus()
        {
            if (!EditorPrefs.HasKey(PrefsKeys.UpdaterStatusState))
            {
                return null;
            }

            DateTime lastCheckedUtc = DateTime.MinValue;
            string storedTimestamp = EditorPrefs.GetString(PrefsKeys.UpdaterStatusLastCheckedUtc, string.Empty);
            if (!string.IsNullOrWhiteSpace(storedTimestamp))
            {
                DateTime.TryParse(storedTimestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out lastCheckedUtc);
            }

            return CreateStatusSnapshot(
                (UpdateAvailabilityState)EditorPrefs.GetInt(PrefsKeys.UpdaterStatusState, (int)UpdateAvailabilityState.Unknown),
                EditorPrefs.GetString(PrefsKeys.UpdaterStatusLocalVersion, PackageVersionUtility.GetLocalPackageVersion()),
                EditorPrefs.GetString(PrefsKeys.UpdaterStatusRemoteVersion, "unknown"),
                EditorPrefs.GetString(PrefsKeys.UpdaterStatusMessage, string.Empty),
                lastCheckedUtc,
                EditorPrefs.GetString(PrefsKeys.UpdaterStatusBranch, CurrentBranch));
        }

        private static void RestorePersistedStatus()
        {
            s_cachedStatus = LoadPersistedStatus();
            EditorApplication.delayCall += InternalEditorUtility.RepaintAllViews;
        }

        private static bool TryParseVersionParts(string value, out List<int> parts)
        {
            parts = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] segments = value.Split('.');
            var parsed = new List<int>(segments.Length);
            for (int i = 0; i < segments.Length; i++)
            {
                if (!int.TryParse(segments[i], out int number))
                {
                    return false;
                }

                parsed.Add(number);
            }

            parts = parsed;
            return true;
        }

        private static string GetHash(string path)
        {
            byte[] bytes = NormalizeBytesForHash(File.ReadAllBytes(path));
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static byte[] NormalizeBytesForHash(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            bool hasUtf8Bom = HasUtf8Bom(bytes);
            int offset = hasUtf8Bom ? 3 : 0;
            int length = bytes.Length - offset;
            if (length <= 0 || ContainsNullByte(bytes, offset))
            {
                return bytes;
            }

            string text;
            try
            {
                text = StrictUtf8.GetString(bytes, offset, length);
            }
            catch (DecoderFallbackException)
            {
                return bytes;
            }

            string normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (string.Equals(text, normalizedText, StringComparison.Ordinal))
            {
                return bytes;
            }

            byte[] normalizedBytes = StrictUtf8.GetBytes(normalizedText);
            if (!hasUtf8Bom)
            {
                return normalizedBytes;
            }

            byte[] preamble = StrictUtf8.GetPreamble();
            var combined = new byte[preamble.Length + normalizedBytes.Length];
            Buffer.BlockCopy(preamble, 0, combined, 0, preamble.Length);
            Buffer.BlockCopy(normalizedBytes, 0, combined, preamble.Length, normalizedBytes.Length);
            return combined;
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        }

        private static bool ContainsNullByte(byte[] bytes, int offset)
        {
            for (int i = offset; i < bytes.Length; i++)
            {
                if (bytes[i] == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetRelativePath(string rootPath, string fullPath)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(rootPath));
            Uri fileUri = new Uri(fullPath);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString());
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
#endif