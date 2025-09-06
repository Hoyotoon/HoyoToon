#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Updater
{
    internal sealed class UpdaterController
    {
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
            _settings = settings ?? UpdaterSettings.FindOrCreate();

            // Resolve package root path (absolute)
            _packageRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", _settings.packageFolderRelativeToProject));
            _toolRoot = string.IsNullOrEmpty(_settings.toolRelativeRoot)
                ? _packageRoot
                : Path.GetFullPath(Path.Combine(_packageRoot, _settings.toolRelativeRoot));
        }

        public string PackageRoot => _packageRoot;
        public string ToolRoot => _toolRoot;

        public PackageInfo LoadLocalPackage()
        {
            try
            {
                var path = Path.Combine(_toolRoot, _settings.packageJsonRelativePath);
                if (File.Exists(path))
                {
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<PackageInfo>(File.ReadAllText(path));
                }
            }
            catch { }
            return null;
        }

        public async Task<CheckResult> CheckAsync()
        {
            var result = new CheckResult
            {
                localPackage = LoadLocalPackage(),
                tracker = PackageTrackerStore.Load(),
                batch = new UpdateBatch()
            };

            var branch = BranchSelector.GetCurrentBranch();
            using (var api = new GitHubApiClient(_settings.repoOwner, _settings.repoName, branch, _settings.githubToken))
            {
                // Pin to a specific commit to avoid race conditions if branch moves between check and apply
                var headSha = await api.GetBranchHeadShaAsync();
                // 1. Fetch remote package.json
                result.remotePackage = await api.GetPackageInfoAsync(_settings.packageJsonRelativePath);
                if (result.remotePackage == null)
                {
                    result.message = "Failed to fetch remote package.json.";
                    return result;
                }

                // 2. Version compare
                if (!IsNewerVersion(result.remotePackage.version, result.localPackage?.version))
                {
                    result.tracker.lastUpdateCheck = Now();
                    PackageTrackerStore.Save(result.tracker);
                    result.message = "You have the latest version.";
                    return result;
                }

                // 3. Get repo tree
                var tree = await api.GetRepoTreeAsync();
                if (tree?.tree == null)
                {
                    result.message = "Failed to fetch repository tree.";
                    return result;
                }

                // 4. Early exit if tree SHA unchanged
                if (!string.IsNullOrEmpty(result.tracker.lastTreeSha) && result.tracker.lastTreeSha == tree.sha)
                {
                    result.message = "Repository unchanged - no updates.";
                    return result;
                }

                // 5. Build update batch
                var remoteFiles = new Dictionary<string, GitTreeItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in tree.tree.Where(t => t.type == "blob"))
                {
                    if (item.path.Equals(_settings.packageJsonRelativePath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (item.path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    remoteFiles[item.path] = item;
                }

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
                            downloadUrl = null, // compute when applying
                            expectedSha = remote.sha,
                            isNew = true
                        });
                    }
                    else
                    {
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
                }

                if (result.tracker.trackedFiles != null)
                {
                    foreach (var tracked in result.tracker.trackedFiles)
                    {
                        if (!remoteFiles.ContainsKey(tracked))
                        {
                            result.batch.filesToDelete.Add(tracked);
                        }
                    }
                }

                result.tracker.lastTreeSha = tree.sha;
                result.batch.sourceCommitSha = headSha;
                result.tracker.lastUpdateCheck = Now();
                PackageTrackerStore.Save(result.tracker);
                result.message = result.batch.totalOperations == 0 ? "No file changes detected." : $"Update ready: {result.batch.totalOperations} operations.";
            }

            return result;
        }

        public async Task ApplyAsync(UpdateBatch batch, PackageInfo remotePkg)
        {
                    if (batch == null) return;

            // Disable auto-refresh to avoid compile/import churn
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                var branch = BranchSelector.GetCurrentBranch();
                using (var api = new GitHubApiClient(_settings.repoOwner, _settings.repoName, branch, _settings.githubToken))
                {
                    // If branch was just switched, perform a clean install before applying the batch.
                    if (BranchSelector.ConsumeCleanFlag())
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
                                var assetPathClean = ToAssetPath(fullPath);
                                if (!string.IsNullOrEmpty(assetPathClean))
                                {
                                    if (!AssetDatabase.DeleteAsset(assetPathClean))
                                    {
                                        if (File.Exists(fullPath)) File.Delete(fullPath);
                                        var meta = fullPath + ".meta"; if (File.Exists(meta)) File.Delete(meta);
                                    }
                                }
                                else
                                {
                                    if (File.Exists(fullPath)) File.Delete(fullPath);
                                    var meta = fullPath + ".meta"; if (File.Exists(meta)) File.Delete(meta);
                                }
                                removed++;
                                EditorUtility.DisplayProgressBar("Cleaning for Branch Switch", rel, (float)removed / Math.Max(1, totalRemovals));
                            }
                        }
                        finally { EditorUtility.ClearProgressBar(); }
                        // Reset tracker before proceeding
                        var reset = new LocalPackageTracker();
                        PackageTrackerStore.Save(reset);
                    }

                    int completed = 0;
                    int total = Math.Max(1, batch?.totalOperations ?? 0);
                    if (batch != null)
                    {
                    foreach (var update in batch.fileUpdates)
                    {
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
                        EditorUtility.DisplayProgressBar("Applying Updates", update.path, (float)completed / total);
                        await Task.Delay(10);
                    }

                    foreach (var deletion in batch.filesToDelete)
                    {
                        var fullDel = Path.Combine(_toolRoot, deletion);
                        var assetPathDel = ToAssetPath(fullDel);
                        if (!string.IsNullOrEmpty(assetPathDel))
                        {
                            if (!AssetDatabase.DeleteAsset(assetPathDel))
                            {
                                if (File.Exists(fullDel)) File.Delete(fullDel);
                                var meta = fullDel + ".meta";
                                if (File.Exists(meta)) File.Delete(meta);
                            }
                        }
                        else
                        {
                            if (File.Exists(fullDel)) File.Delete(fullDel);
                            var meta = fullDel + ".meta";
                            if (File.Exists(meta)) File.Delete(meta);
                        }
                        completed++;
                        EditorUtility.DisplayProgressBar("Applying Updates", deletion, (float)completed / total);
                        await Task.Delay(10);
                    }
                    }

                    // Always update package.json to the latest from the pinned commit (if available) so version reflects accurately
                    try
                    {
                        var pkgPath = _settings.packageJsonRelativePath;
                        var pkgBytes = !string.IsNullOrEmpty(batch?.sourceCommitSha)
                            ? await api.DownloadRawAtCommitAsync(pkgPath, batch.sourceCommitSha)
                            : await api.DownloadRawAsync(pkgPath);
                        if (pkgBytes != null && pkgBytes.Length > 0)
                        {
                            var fullPkg = Path.Combine(_toolRoot, pkgPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPkg));
                            await File.WriteAllBytesAsync(fullPkg, pkgBytes);
                            Debug.Log($"[Updater] Wrote latest {pkgPath} ({pkgBytes.Length} bytes) to {fullPkg}");
                            // Small progress nudge (doesn't count toward total as it's implicit)
                            EditorUtility.DisplayProgressBar("Finalizing", "package.json", 1f);
                        }
                    }
                    catch (Exception pkgEx)
                    {
                        Debug.LogWarning($"[Updater] package.json update skipped: {pkgEx.Message}");
                    }
                }

                // Rebuild tracker snapshot and set version
                var tracker = PackageTrackerStore.Load();
                tracker.currentVersion = remotePkg?.version;
                tracker.lastUpdateCheck = Now();
                await PackageTrackerStore.SnapshotAsync(tracker, _toolRoot);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh();
            }
        }

        private static string ToAssetPath(string fullPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var normalized = Path.GetFullPath(fullPath);
            if (!normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)) return null;
            var rel = normalized.Substring(projectRoot.Length + 1).Replace("\\", "/");
            return rel;
        }

        private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        private static bool IsNewerVersion(string newVersion, string currentVersion)
        {
            if (string.IsNullOrEmpty(currentVersion)) return true;
            try
            {
                var a = new Version(newVersion);
                var b = new Version(currentVersion);
                return a > b;
            }
            catch { return string.Compare(newVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0; }
        }
    }
}
#endif
