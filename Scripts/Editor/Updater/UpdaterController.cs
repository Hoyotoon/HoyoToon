#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Updater
{
    internal sealed class UpdaterController
    {
        private sealed class StagedUpdateData
        {
            public string stagingRoot;
            public string filesRoot;
            public string packageJsonPath;
        }

        public class AvailabilityResult
        {
            public PackageInfo localPackage;
            public PackageInfo remotePackage;
            public string branch;

            public bool HasUpdate => remotePackage != null && IsNewerVersion(remotePackage.version, localPackage?.version);
        }

        public class CheckResult
        {
            public PackageInfo localPackage;
            public PackageInfo remotePackage;
            public LocalPackageTracker tracker;
            public UpdateBatch batch;
            public string message;
        }

        private readonly UpdaterSettings _settings;
        private readonly string _packageRoot;
        private readonly string _toolRoot;

        public UpdaterController(UpdaterSettings settings)
        {
            _settings = settings ?? UpdaterSettings.Instance;

            _packageRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", _settings.packageFolderRelativeToProject));
            _toolRoot = string.IsNullOrEmpty(_settings.toolRelativeRoot)
                ? _packageRoot
                : Path.GetFullPath(Path.Combine(_packageRoot, _settings.toolRelativeRoot));
        }

        public string PackageRoot => _packageRoot;
        public string ToolRoot => _toolRoot;

        public UpdaterSession CreateSession()
        {
            return new UpdaterSession(_settings);
        }

        public PackageInfo LoadLocalPackage()
        {
            try
            {
                var path = Path.Combine(_toolRoot, _settings.packageJsonRelativePath);
                if (File.Exists(path))
                {
                    if (Api.Parser.TryParseFile<PackageInfo>(path, out var pkg, out var _))
                    {
                        return pkg;
                    }
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("Updater.LoadLocalPackage", $"Failed to load local package.json: {ex.Message}");
            }
            return null;
        }

        public async Task<AvailabilityResult> CheckForAvailableUpdateAsync(UpdaterSession session = null)
        {
            var activeSession = session ?? CreateSession();

            return new AvailabilityResult
            {
                localPackage = LoadLocalPackage(),
                remotePackage = await activeSession.Api.GetPackageInfoAsync(_settings.packageJsonRelativePath),
                branch = activeSession.Branch
            };
        }

        public async Task<CheckResult> CheckAsync(UpdaterSession session = null)
        {
            var activeSession = session ?? CreateSession();
            var result = new CheckResult
            {
                localPackage = LoadLocalPackage(),
                tracker = PackageTrackerStore.Load(),
                batch = new UpdateBatch()
            };

            // Pin to a specific commit to avoid race conditions if branch moves between check and apply
            var headSha = await activeSession.Api.GetBranchHeadShaAsync();
            result.remotePackage = await activeSession.Api.GetPackageInfoAsync(_settings.packageJsonRelativePath);
            if (result.remotePackage == null)
            {
                result.message = "Failed to fetch remote package.json.";
                return result;
            }

            if (!IsNewerVersion(result.remotePackage.version, result.localPackage?.version))
            {
                result.tracker.lastUpdateCheck = Now();
                PackageTrackerStore.Save(result.tracker);
                result.message = "You have the latest version.";
                return result;
            }

            var tree = await activeSession.Api.GetRepoTreeAsync();
            if (tree?.tree == null)
            {
                result.message = "Failed to fetch repository tree.";
                return result;
            }

            var packageJsonItem = tree.tree.FirstOrDefault(item =>
                item != null &&
                item.type == "blob" &&
                item.path.Equals(_settings.packageJsonRelativePath, StringComparison.OrdinalIgnoreCase));
            result.batch.packageJsonSha = packageJsonItem?.sha;

            if (!string.IsNullOrEmpty(result.tracker.lastTreeSha) && result.tracker.lastTreeSha == tree.sha)
            {
                result.message = "Repository unchanged - no updates.";
                return result;
            }

            var remoteFiles = BuildRemoteFileMap(tree);
            BuildFileUpdatePlan(result, remoteFiles);

            result.tracker.lastTreeSha = tree.sha;
            result.batch.sourceCommitSha = headSha;
            result.tracker.lastUpdateCheck = Now();
            PackageTrackerStore.Save(result.tracker);
            result.message = result.batch.totalOperations == 0 ? "No file changes detected." : $"Update ready: {result.batch.totalOperations} operations.";

            return result;
        }

        public async Task<string> GetChangelogAsync(PackageInfo remotePackage, UpdaterSession session = null)
        {
            if (remotePackage == null || string.IsNullOrEmpty(remotePackage.version))
            {
                return null;
            }

            var activeSession = session ?? CreateSession();

            try
            {
                var release = await activeSession.Api.GetReleaseByTagAsync(remotePackage.version)
                    ?? await activeSession.Api.GetReleaseByTagAsync("v" + remotePackage.version);
                if (release != null && !string.IsNullOrEmpty(release.body))
                {
                    return $"Release Notes for {remotePackage.version}\n\n" + release.body;
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("Updater.Changelog.Release", $"Failed to fetch release notes: {ex.Message}");
            }

            try
            {
                var text = await activeSession.Api.GetRawTextAsync("changelog.md");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var header = $"## {remotePackage.version}";
                    int index = text.IndexOf(header, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        int nextIndex = text.IndexOf("## ", index + header.Length, StringComparison.OrdinalIgnoreCase);
                        string section = nextIndex > index ? text.Substring(index, nextIndex - index) : text.Substring(index);
                        return section.Trim();
                    }

                    return text.Trim();
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("Updater.Changelog.Fallback", $"Failed to fetch changelog.md: {ex.Message}");
            }

            return null;
        }

        public async Task ApplyAsync(UpdateBatch batch, PackageInfo remotePkg, IProgressSink progress = null, UpdaterSession session = null)
        {
            if (batch == null) return;

            var activeSession = session ?? CreateSession();
            GitIgnoreFilter gitIgnore = null;
            StagedUpdateData stagedUpdate = null;
            bool pendingSaved = false;
            bool installCompleted = false;
            bool requiresBranchClean = BranchSelector.IsCleanPending();

            try
            {
                if (_settings.respectGitIgnoreForDeletions)
                {
                    gitIgnore = GitIgnoreFilter.Load(_toolRoot);
                }

                int cleanOperations = requiresBranchClean ? CountCleanOperations(gitIgnore) : 0;
                int downloadOperations = batch.fileUpdates.Count + 1;
                int installOperations = batch.fileUpdates.Count + batch.filesToDelete.Count + 1;
                int total = Math.Max(1, downloadOperations + installOperations + cleanOperations);
                int completed = 0;

                stagedUpdate = await StageUpdateAsync(batch, activeSession.Api, activeSession.Branch, total, completed, progress);
                completed += downloadOperations;
                SavePendingInstall(batch, stagedUpdate, activeSession.Branch, remotePkg?.version, requiresBranchClean);
                pendingSaved = true;

                await ApplyStagedUpdateAsync(batch, remotePkg?.version, stagedUpdate, gitIgnore, requiresBranchClean, total, completed, progress);
                installCompleted = true;
            }
            finally
            {
                if (installCompleted)
                {
                    PendingInstallStore.Clear();
                    CleanupStaging(stagedUpdate);
                }
                else if (!pendingSaved)
                {
                    CleanupStaging(stagedUpdate);
                }
            }
        }

        public async Task<bool> ResumePendingInstallAsync(IProgressSink progress = null)
        {
            var pending = PendingInstallStore.Load();
            if (pending == null) return false;

            StagedUpdateData stagedUpdate;
            try
            {
                stagedUpdate = LoadStagedUpdate(pending);
            }
            catch
            {
                PendingInstallStore.Clear();
                throw;
            }

            var gitIgnore = _settings.respectGitIgnoreForDeletions ? GitIgnoreFilter.Load(_toolRoot) : null;
            bool installCompleted = false;

            try
            {
                int cleanOperations = pending.requiresBranchClean ? CountCleanOperations(gitIgnore) : 0;
                int installOperations = pending.batch.fileUpdates.Count + pending.batch.filesToDelete.Count + 1;
                int total = Math.Max(1, cleanOperations + installOperations);

                await ApplyStagedUpdateAsync(pending.batch, pending.remoteVersion, stagedUpdate, gitIgnore, pending.requiresBranchClean, total, 0, progress);
                installCompleted = true;
                return true;
            }
            finally
            {
                if (installCompleted)
                {
                    PendingInstallStore.Clear();
                    CleanupStaging(stagedUpdate);
                }
            }
        }

        private Dictionary<string, GitTreeItem> BuildRemoteFileMap(GitTreeResponse tree)
        {
            var remoteFiles = new Dictionary<string, GitTreeItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in tree.tree.Where(t => t.type == "blob"))
            {
                if (item.path.Equals(_settings.packageJsonRelativePath, StringComparison.OrdinalIgnoreCase)) continue;
                if (item.path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsExcludedPath(item.path)) continue;
                remoteFiles[item.path] = item;
            }

            return remoteFiles;
        }

        private void BuildFileUpdatePlan(CheckResult result, Dictionary<string, GitTreeItem> remoteFiles)
        {
            foreach (var kv in remoteFiles)
            {
                var rel = kv.Key;
                var remote = kv.Value;
                var localFull = Path.Combine(_toolRoot, rel);
                if (!File.Exists(localFull))
                {
                    result.batch.fileUpdates.Add(new FileUpdate
                    {
                        path = rel,
                        downloadUrl = null,
                        expectedSha = remote.sha,
                        isNew = true
                    });
                    continue;
                }

                result.tracker.fileHashes.TryGetValue(rel, out var trackedSha);
                if (!string.Equals(trackedSha, remote.sha, StringComparison.Ordinal))
                {
                    result.batch.fileUpdates.Add(new FileUpdate
                    {
                        path = rel,
                        downloadUrl = null,
                        expectedSha = remote.sha,
                        isNew = false
                    });
                }
            }

            if (result.tracker.trackedFiles == null) return;
            foreach (var tracked in result.tracker.trackedFiles)
            {
                if (IsExcludedPath(tracked)) continue;
                if (!remoteFiles.ContainsKey(tracked))
                {
                    result.batch.filesToDelete.Add(tracked);
                }
            }
        }

        private async Task<StagedUpdateData> StageUpdateAsync(UpdateBatch batch, GitHubApiClient api, string branch, int total, int completed, IProgressSink progress)
        {
            var staged = CreateStagingArea(batch, branch);

            foreach (var update in batch.fileUpdates)
            {
                if (IsExcludedPath(update.path))
                {
                    completed++;
                    ReportProgress(progress, "Skipping Excluded", update.path, (float)completed / total);
                    continue;
                }

                var bytes = !string.IsNullOrEmpty(batch.sourceCommitSha)
                    ? await api.DownloadRawAtCommitAsync(update.path, batch.sourceCommitSha)
                    : await api.DownloadRawAsync(update.path);
                var sha = HashUtil.GitBlobSha(bytes);
                if (!string.Equals(sha, update.expectedSha, StringComparison.Ordinal))
                    throw new Exception($"Integrity check failed for {update.path} (expected {update.expectedSha}, got {sha}, commit {batch.sourceCommitSha ?? branch})");

                WriteStagedFile(staged.filesRoot, update.path, bytes);
                completed++;
                ReportProgress(progress, "Downloading Update", update.path, (float)completed / total);
            }

            var packageJsonBytes = !string.IsNullOrEmpty(batch.sourceCommitSha)
                ? await api.DownloadRawAtCommitAsync(_settings.packageJsonRelativePath, batch.sourceCommitSha)
                : await api.DownloadRawAsync(_settings.packageJsonRelativePath);
            if (!string.IsNullOrEmpty(batch.packageJsonSha))
            {
                var packageJsonSha = HashUtil.GitBlobSha(packageJsonBytes);
                if (!string.Equals(packageJsonSha, batch.packageJsonSha, StringComparison.Ordinal))
                    throw new Exception($"Integrity check failed for {_settings.packageJsonRelativePath} (expected {batch.packageJsonSha}, got {packageJsonSha}, commit {batch.sourceCommitSha ?? branch})");
            }

            WriteStagedFile(staged.filesRoot, _settings.packageJsonRelativePath, packageJsonBytes);
            completed++;
            ReportProgress(progress, "Downloading Update", _settings.packageJsonRelativePath, (float)completed / total);

            return staged;
        }

        private int CleanForBranchSwitch(GitIgnoreFilter gitIgnore, int total, int completed, IProgressSink progress)
        {
            try
            {
                var allFiles = Directory.GetFiles(_toolRoot, "*", SearchOption.AllDirectories)
                    .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var fullPath in allFiles)
                {
                    var rel = Path.GetRelativePath(_toolRoot, fullPath).Replace("\\", "/");
                    if (string.Equals(rel, _settings.packageJsonRelativePath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (IsExcludedPath(rel)) continue;
                    if (gitIgnore != null && gitIgnore.IsIgnored(rel, false))
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Info, $"Preserving local gitignored file '{rel}' during clean.");
                        completed++;
                        ReportProgress(progress, "Skipping (gitignored)", rel, (float)completed / total);
                        continue;
                    }

                    DeleteTrackedPath(fullPath, rel);
                    completed++;
                    ReportProgress(progress, "Cleaning for Branch Switch", rel, (float)completed / total);
                }

                return completed;
            }
            catch (Exception ex)
            {
                throw new Exception($"Branch clean failed: {ex.Message}", ex);
            }
        }

        private int ApplyStagedFileUpdates(UpdateBatch batch, StagedUpdateData staged, int total, int completed, IProgressSink progress)
        {
            foreach (var update in batch.fileUpdates)
            {
                if (IsExcludedPath(update.path))
                {
                    completed++;
                    ReportProgress(progress, "Skipping Excluded", update.path, (float)completed / total);
                    continue;
                }

                var full = Path.Combine(_toolRoot, update.path);
                var stagedFull = Path.Combine(staged.filesRoot, update.path);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.Copy(stagedFull, full, overwrite: true);
                completed++;
                ReportProgress(progress, "Installing Update", update.path, (float)completed / total);
            }

            return completed;
        }

        private int ApplyFileDeletions(UpdateBatch batch, GitIgnoreFilter gitIgnore, int total, int completed, IProgressSink progress)
        {
            foreach (var deletion in batch.filesToDelete)
            {
                if (IsExcludedPath(deletion))
                {
                    completed++;
                    ReportProgress(progress, "Skipping Excluded", deletion, (float)completed / total);
                    continue;
                }

                if (gitIgnore != null && gitIgnore.IsIgnored(deletion, false))
                {
                    completed++;
                    ReportProgress(progress, "Skipping (gitignored)", deletion, (float)completed / total);
                    continue;
                }

                var fullDel = Path.Combine(_toolRoot, deletion);
                DeleteTrackedPath(fullDel, deletion);
                completed++;
                ReportProgress(progress, "Installing Update", deletion, (float)completed / total);
            }

            return completed;
        }

        private void ApplyStagedPackageJson(StagedUpdateData staged, int total, int completed, IProgressSink progress)
        {
            var fullPkg = Path.Combine(_toolRoot, _settings.packageJsonRelativePath);
            var stagedPkg = Path.Combine(staged.filesRoot, staged.packageJsonPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPkg));
            File.Copy(stagedPkg, fullPkg, overwrite: true);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Info, $"Wrote latest {_settings.packageJsonRelativePath} to {fullPkg}");
            ReportProgress(progress, "Finalizing", _settings.packageJsonRelativePath, (float)(completed + 1) / total);
        }

        private StagedUpdateData CreateStagingArea(UpdateBatch batch, string branch)
        {
            var safeBranch = SanitizePathComponent(branch);
            var safeCommit = SanitizePathComponent(batch.sourceCommitSha ?? "working");
            var stagingRoot = Path.Combine(Application.persistentDataPath, "HoyoToon_Updater_Staging", safeBranch, safeCommit);
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }

            var filesRoot = Path.Combine(stagingRoot, "files");
            Directory.CreateDirectory(filesRoot);
            return new StagedUpdateData
            {
                stagingRoot = stagingRoot,
                filesRoot = filesRoot,
                packageJsonPath = _settings.packageJsonRelativePath
            };
        }

        private static void WriteStagedFile(string stagingRoot, string relativePath, byte[] bytes)
        {
            var fullPath = Path.Combine(stagingRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, bytes);
        }

        private static void DeleteTrackedPath(string fullPath, string relativePath)
        {
            if (relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(fullPath)) File.Delete(fullPath);
                return;
            }

            var assetPath = EditorUtil.ToUnityAssetPath(fullPath);
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.DeleteAsset(assetPath)) return;
            if (File.Exists(fullPath)) File.Delete(fullPath);
            var meta = fullPath + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
        }

        private int CountCleanOperations(GitIgnoreFilter gitIgnore)
        {
            return Directory.GetFiles(_toolRoot, "*", SearchOption.AllDirectories)
                .Where(fullPath => !fullPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .Count(fullPath =>
                {
                    var rel = Path.GetRelativePath(_toolRoot, fullPath).Replace("\\", "/");
                    if (string.Equals(rel, _settings.packageJsonRelativePath, StringComparison.OrdinalIgnoreCase)) return false;
                    if (IsExcludedPath(rel)) return false;
                    if (gitIgnore != null && gitIgnore.IsIgnored(rel, false)) return true;
                    return true;
                });
        }

        private void PruneEmptyDirectories()
        {
            if (!Directory.Exists(_toolRoot)) return;

            var directories = Directory.GetDirectories(_toolRoot, "*", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length)
                .ToList();

            foreach (var directory in directories)
            {
                if (Directory.EnumerateFileSystemEntries(directory).Any()) continue;
                Directory.Delete(directory, recursive: false);

                var metaPath = directory + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }
        }

        private static void CleanupStaging(StagedUpdateData staged)
        {
            if (staged == null || string.IsNullOrEmpty(staged.stagingRoot) || !Directory.Exists(staged.stagingRoot)) return;

            try
            {
                Directory.Delete(staged.stagingRoot, recursive: true);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("Updater.Staging.Cleanup", $"Failed to clean staging folder: {ex.Message}");
            }
        }

        private static string SanitizePathComponent(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "default";

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }

        private async Task ApplyStagedUpdateAsync(UpdateBatch batch, string remoteVersion, StagedUpdateData stagedUpdate, GitIgnoreFilter gitIgnore, bool requiresBranchClean, int total, int completed, IProgressSink progress)
        {
            bool autoRefreshDisabled = false;
            bool assetEditing = false;

            try
            {
                AssetDatabase.DisallowAutoRefresh();
                autoRefreshDisabled = true;
                AssetDatabase.StartAssetEditing();
                assetEditing = true;

                if (requiresBranchClean)
                {
                    BranchSelector.ConsumeCleanFlag();
                    completed = CleanForBranchSwitch(gitIgnore, total, completed, progress);
                    var reset = new LocalPackageTracker();
                    PackageTrackerStore.Save(reset);
                }

                completed = ApplyStagedFileUpdates(batch, stagedUpdate, total, completed, progress);
                completed = ApplyFileDeletions(batch, gitIgnore, total, completed, progress);
                ApplyStagedPackageJson(stagedUpdate, total, completed, progress);
                PruneEmptyDirectories();

                var tracker = PackageTrackerStore.Load();
                tracker.currentVersion = remoteVersion;
                tracker.lastUpdateCheck = Now();
                await PackageTrackerStore.SnapshotAsync(tracker, _toolRoot);
            }
            finally
            {
                if (assetEditing)
                {
                    AssetDatabase.StopAssetEditing();
                }

                if (progress == null) EditorUtility.ClearProgressBar();

                if (autoRefreshDisabled)
                {
                    AssetDatabase.AllowAutoRefresh();
                    AssetDatabase.Refresh();
                }
            }
        }

        private void SavePendingInstall(UpdateBatch batch, StagedUpdateData stagedUpdate, string branch, string remoteVersion, bool requiresBranchClean)
        {
            PendingInstallStore.Save(new PendingInstallState
            {
                batch = batch,
                branch = branch,
                remoteVersion = remoteVersion,
                stagingRoot = stagedUpdate.stagingRoot,
                filesRoot = stagedUpdate.filesRoot,
                packageJsonPath = stagedUpdate.packageJsonPath,
                requiresBranchClean = requiresBranchClean,
                createdAt = Now()
            });
        }

        private static StagedUpdateData LoadStagedUpdate(PendingInstallState pending)
        {
            if (pending == null) throw new ArgumentNullException(nameof(pending));
            if (string.IsNullOrWhiteSpace(pending.stagingRoot) || string.IsNullOrWhiteSpace(pending.filesRoot))
                throw new Exception("Pending install marker is missing staging paths.");
            if (!Directory.Exists(pending.filesRoot))
                throw new Exception($"Pending install staging folder not found: {pending.filesRoot}");

            return new StagedUpdateData
            {
                stagingRoot = pending.stagingRoot,
                filesRoot = pending.filesRoot,
                packageJsonPath = string.IsNullOrWhiteSpace(pending.packageJsonPath) ? "package.json" : pending.packageJsonPath
            };
        }

        private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        private static void ReportProgress(IProgressSink progress, string title, string info, float value)
        {
            if (progress != null) progress.Report(title, info, value);
            else EditorUtility.DisplayProgressBar(title, info, value);
        }

        internal static bool IsNewerVersion(string newVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(newVersion)) return false;
            if (string.IsNullOrEmpty(currentVersion)) return true;
            try
            {
                var a = new Version(newVersion);
                var b = new Version(currentVersion);
                return a > b;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("Updater.VersionCompare", $"Version compare failed ('{newVersion}' vs '{currentVersion}'): {ex.Message}");
                return string.Compare(newVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
            }
        }

        private static bool IsExcludedPath(string rel)
        {
            if (string.IsNullOrEmpty(rel)) return true;
            var p = rel.Replace("\\", "/");
            // Exclude top-level dotfiles and known dot-directories
            if (p.StartsWith(".github/", StringComparison.OrdinalIgnoreCase)) return true;
            if (p.StartsWith(".git/", StringComparison.OrdinalIgnoreCase)) return true;
            if (p.StartsWith(".vscode/", StringComparison.OrdinalIgnoreCase)) return true;
            if (p.StartsWith(".idea/", StringComparison.OrdinalIgnoreCase)) return true;
            // Exclude common VCS/editor config files at any level (allow .gitignore to pass through)
            var fileName = System.IO.Path.GetFileName(p);
            if (fileName.Equals(".gitattributes", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.Equals(".gitmodules", StringComparison.OrdinalIgnoreCase)) return true;
            if (fileName.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
#endif
