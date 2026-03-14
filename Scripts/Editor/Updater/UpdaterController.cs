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

            // Disable auto-refresh to avoid compile/import churn
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                GitIgnoreFilter gitIgnore = null;
                if (_settings.respectGitIgnoreForDeletions)
                {
                    gitIgnore = GitIgnoreFilter.Load(_toolRoot);
                }

                if (BranchSelector.ConsumeCleanFlag())
                {
                    await CleanForBranchSwitchAsync(gitIgnore, progress);
                    var reset = new LocalPackageTracker();
                    PackageTrackerStore.Save(reset);
                }

                int total = Math.Max(1, batch.totalOperations);
                int completed = 0;
                completed = await ApplyFileUpdatesAsync(batch, activeSession.Api, activeSession.Branch, total, completed, progress);
                completed = await ApplyFileDeletionsAsync(batch, gitIgnore, total, completed, progress);
                await RefreshPackageJsonAsync(batch, activeSession.Api, progress);

                var tracker = PackageTrackerStore.Load();
                tracker.currentVersion = remotePkg?.version;
                tracker.lastUpdateCheck = Now();
                await PackageTrackerStore.SnapshotAsync(tracker, _toolRoot);
            }
            finally
            {
                if (progress == null) EditorUtility.ClearProgressBar();
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh();
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

        private Task CleanForBranchSwitchAsync(GitIgnoreFilter gitIgnore, IProgressSink progress)
        {
            try
            {
                var allFiles = Directory.GetFiles(_toolRoot, "*", SearchOption.AllDirectories)
                    .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                int removed = 0;
                int totalRemovals = allFiles.Count;
                foreach (var fullPath in allFiles)
                {
                    var rel = Path.GetRelativePath(_toolRoot, fullPath).Replace("\\", "/");
                    if (string.Equals(rel, _settings.packageJsonRelativePath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (IsExcludedPath(rel)) continue;
                    if (gitIgnore != null && gitIgnore.IsIgnored(rel, false))
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Info, $"Preserving local gitignored file '{rel}' during clean.");
                        removed++;
                        ReportProgress(progress, "Skipping (gitignored)", rel, (float)removed / Math.Max(1, totalRemovals));
                        continue;
                    }

                    DeleteFileWithMeta(fullPath);
                    removed++;
                    ReportProgress(progress, "Cleaning for Branch Switch", rel, (float)removed / Math.Max(1, totalRemovals));
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
            finally
            {
                if (progress == null) EditorUtility.ClearProgressBar();
            }
        }

        private async Task<int> ApplyFileUpdatesAsync(UpdateBatch batch, GitHubApiClient api, string branch, int total, int completed, IProgressSink progress)
        {
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

                var full = Path.Combine(_toolRoot, update.path);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                await File.WriteAllBytesAsync(full, bytes);
                completed++;
                ReportProgress(progress, "Applying Updates", update.path, (float)completed / total);
                await Task.Delay(10);
            }

            return completed;
        }

        private async Task<int> ApplyFileDeletionsAsync(UpdateBatch batch, GitIgnoreFilter gitIgnore, int total, int completed, IProgressSink progress)
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
                DeleteFileWithMeta(fullDel);
                completed++;
                ReportProgress(progress, "Applying Updates", deletion, (float)completed / total);
                await Task.Delay(10);
            }

            return completed;
        }

        private async Task RefreshPackageJsonAsync(UpdateBatch batch, GitHubApiClient api, IProgressSink progress)
        {
            try
            {
                var pkgPath = _settings.packageJsonRelativePath;
                var pkgBytes = !string.IsNullOrEmpty(batch.sourceCommitSha)
                    ? await api.DownloadRawAtCommitAsync(pkgPath, batch.sourceCommitSha)
                    : await api.DownloadRawAsync(pkgPath);
                if (pkgBytes != null && pkgBytes.Length > 0)
                {
                    var fullPkg = Path.Combine(_toolRoot, pkgPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPkg));
                    await File.WriteAllBytesAsync(fullPkg, pkgBytes);
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Info, $"Wrote latest {pkgPath} ({pkgBytes.Length} bytes) to {fullPkg}");
                    ReportProgress(progress, "Finalizing", "package.json", 1f);
                }
            }
            catch (Exception pkgEx)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Warning, $"package.json update skipped: {pkgEx.Message}");
            }
        }

        private static void DeleteFileWithMeta(string fullPath)
        {
            var assetPath = EditorUtil.ToUnityAssetPath(fullPath);
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.DeleteAsset(assetPath)) return;
            if (File.Exists(fullPath)) File.Delete(fullPath);
            var meta = fullPath + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
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
