#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.Updater
{
    /// <summary>
    /// Single-window updater experience using HoyoToonDialogWindow.
    /// Orchestrates: Check -> Show Summary -> Apply -> Done within one dialog.
    /// </summary>
    internal static class UpdaterDialogFlow
    {
        private class DialogProgressSink : IProgressSink
        {
            private readonly HoyoToonDialogWindow _win;
            public DialogProgressSink(HoyoToonDialogWindow win) { _win = win; }
            public void Report(string title, string info, float progress01)
            {
                if (_win == null) return;
                _win.SetTitle(title);
                if (!string.IsNullOrEmpty(info)) _win.SetProgressText(info);
                _win.UpdateProgress(Mathf.Clamp01(progress01));
            }
        }

        [MenuItem("HoyoToon/Debug/Updater: Large Changelog Test", priority = 501)]
        public static void RunDebugLargeChangelogTest()
        {
            var win = HoyoToonDialogWindow.ShowProgressWithCustomButtons(
                title: "HoyoToon Updater (Debug)",
                message: "Preparing fake update summary...",
                type: MessageType.Info,
                buttons: new string[0],
                topBar: HoyoToonDialogWindow.TopBarConfig.Default(),
                onResultIndex: null,
                keepOpenOnClick: true
            );

            _ = RunDebugAsync(win);
        }

        private static async Task RunDebugAsync(HoyoToonDialogWindow win)
        {
            if (win == null) return;

            // Fake local/remote versions
            var local = new PackageInfo { version = "1.2.3" };
            var remote = new PackageInfo { version = "1.3.0" };

            // Build a big batch: 150 updates (75 new, 75 modified), 50 deletions
            var batch = new UpdateBatch();
            for (int i = 0; i < 75; i++) batch.fileUpdates.Add(new FileUpdate { path = $"Core/Systems/NewFile_{i}.cs", expectedSha = "sha-new", isNew = true });
            for (int i = 0; i < 75; i++) batch.fileUpdates.Add(new FileUpdate { path = $"Core/Systems/ChangedFile_{i}.cs", expectedSha = "sha-mod", isNew = false });
            for (int i = 0; i < 50; i++) batch.filesToDelete.Add($"Legacy/Remove_{i}.txt");

            // Create a large markdown changelog
            string changelog = BuildLargeMarkdownChangelog(remote.version, 40);

            // Render summary with changelog
            var sb = new StringBuilder();
            sb.AppendLine($"Repository: (debug) @ Beta");
            sb.AppendLine($"Local version: {local.version}");
            sb.AppendLine($"Remote version: {remote.version}");
            sb.AppendLine();
            int add = 75, mod = 75, del = 50; int total = add + mod + del;
            sb.AppendLine($"Planned operations: {total}");
            sb.AppendLine($"• New files: {add}");
            sb.AppendLine($"• Modified files: {mod}");
            sb.AppendLine($"• Deleted files: {del}");
            sb.AppendLine();
            sb.AppendLine("This is a debug-only dry run. No files will be changed.");
            sb.AppendLine("\n---\n");
            sb.AppendLine("Changelog:");
            sb.AppendLine();
            sb.AppendLine(Truncate(changelog, 3000));

            win.SetTitle("Update Available (Debug)");
            win.SetMessage(sb.ToString());
            win.SetShowProgressBar(false);
            win.SetButtons(new[] { "Apply (Simulate)", "Cancel" }, 0, 1, async idx =>
            {
                if (idx != 0) { win.Close(); return; }

                // Simulate apply with progress
                win.SetTitle("Applying Update (Simulated)");
                win.SetShowProgressBar(true);
                var sink = new DialogProgressSink(win);

                int ops = total;
                int count = 0;
                foreach (var u in batch.fileUpdates)
                {
                    count++;
                    sink.Report("Applying Updates (Simulated)", u.path, (float)count / ops);
                    await Task.Delay(15);
                }
                foreach (var d in batch.filesToDelete)
                {
                    count++;
                    sink.Report("Applying Deletions (Simulated)", d, (float)count / ops);
                    await Task.Delay(12);
                }

                sink.Report("Finalizing", "Refreshing package.json", 1f);
                await Task.Delay(200);

                var done = new StringBuilder();
                done.AppendLine($"Updated (simulated) to version {remote.version}.");
                done.AppendLine("package.json refreshed (simulated).");
                done.AppendLine();
                done.AppendLine("You can now close this window.");
                win.SetTitle("Update Complete (Simulated)");
                win.SetMessage(done.ToString());
                win.SetButtons(new[] { "Close" }, 0, 0, _ => { }, keepOpenOnClick: false);
            }, keepOpenOnClick: true);
        }

        private static string BuildLargeMarkdownChangelog(string version, int sections)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Release Notes for {version}");
            for (int i = 1; i <= sections; i++)
            {
                sb.AppendLine($"\n## Section {i}: Improvements");
                for (int j = 1; j <= 10; j++)
                {
                    sb.AppendLine($"- Improved subsystem {i}.{j} performance by {5 + (j % 7)}%.");
                }
                sb.AppendLine($"\n## Section {i}: Fixes");
                for (int j = 1; j <= 8; j++)
                {
                    sb.AppendLine($"- Fixed issue #{i * 100 + j}: **Edge case** when handling `_null_` inputs.");
                }
                sb.AppendLine($"\n## Section {i}: Notes");
                sb.AppendLine("- This section includes additional notes, code snippets, and links.");
                sb.AppendLine("- Example: `CodeBlockExample()` refactored.");
                sb.AppendLine("- See [documentation](https://example.com/docs) for details.");
            }
            return sb.ToString();
        }

        [MenuItem("HoyoToon/Updater/Run Updater...", priority = 80)]
        public static void Run()
        {
            // Start with a progress dialog; we'll update its content dynamically
            var win = HoyoToonDialogWindow.ShowProgressWithCustomButtons(
                title: "HoyoToon Updater",
                message: "Checking for updates...",
                type: MessageType.Info,
                buttons: new string[0],
                topBar: HoyoToonDialogWindow.TopBarConfig.Default(),
                onResultIndex: null,
                keepOpenOnClick: true
            );

            // Fire and forget async workflow bound to this window
            _ = RunAsync(win);
        }

        private static async Task RunAsync(HoyoToonDialogWindow win)
        {
            if (win == null) return;

            var settings = UpdaterSettings.FindOrCreate();
            var controller = new UpdaterController(settings);

            try
            {
                // Phase 1: Check
                win.SetTitle("Checking for Updates");
                win.SetMessage("Analyzing repository and building update plan...");
                win.SetShowProgressBar(true);
                win.UpdateProgress(0.1f, "Contacting GitHub...");

                var check = await controller.CheckAsync();
                var local = check.localPackage;
                var remote = check.remotePackage;
                var batch = check.batch;
                string changelog = await FetchChangelogAsync(remote, settings);

                // Build summary message
                var sb = new StringBuilder();
                string branch = BranchSelector.GetCurrentBranch();
                sb.AppendLine($"Repository: {settings.repoOwner}/{settings.repoName} @ {branch}");
                sb.AppendLine($"Local version: {local?.version ?? "unknown"}");
                sb.AppendLine($"Remote version: {remote?.version ?? "unknown"}");
                sb.AppendLine();

                if (batch == null || batch.totalOperations == 0)
                {
                    sb.AppendLine(check.message ?? "No updates available.");
                    win.SetTitle("You're up to date");
                    win.SetMessage(sb.ToString());
                    win.UpdateProgress(1f, "Nothing to apply");
                    win.SetButtons(new[] { "OK" }, defaultIndex: 0, cancelIndex: 0, onResultIndex: _ => { }, keepOpenOnClick: false);
                    return;
                }

                int add = 0, mod = 0, del = 0;
                foreach (var u in batch.fileUpdates) { if (u.isNew) add++; else mod++; }
                del = batch.filesToDelete.Count;

                sb.AppendLine($"Planned operations: {batch.totalOperations}");
                sb.AppendLine($"• New files: {add}");
                sb.AppendLine($"• Modified files: {mod}");
                sb.AppendLine($"• Deleted files: {del}");
                sb.AppendLine();
                sb.AppendLine("Click Apply to download and install the update. Unity will refresh assets at the end.");

                if (!string.IsNullOrEmpty(changelog))
                {
                    sb.AppendLine("\n---\n");
                    sb.AppendLine("Changelog:");
                    sb.AppendLine();
                    sb.AppendLine(Truncate(changelog, 1600));
                }

                win.SetTitle("Update Available");
                win.SetMessage(sb.ToString());
                win.UpdateProgress(0f, null);
                win.SetButtons(new[] { "Apply", "Cancel" }, defaultIndex: 0, cancelIndex: 1, onResultIndex: async idx =>
                {
                    if (idx == 0)
                    {
                        // Phase 2: Apply
                        win.SetTitle("Applying Update");
                        win.SetMessage("Downloading files, verifying integrity, and writing to disk. Please wait...");
                        win.SetShowProgressBar(true);
                        win.UpdateProgress(0.01f, "Preparing...");

                        var sink = new DialogProgressSink(win);
                        try
                        {
                            await controller.ApplyAsync(batch, remote, sink);
                            win.CompleteProgress("Done");

                            // Phase 3: Completed
                            var doneMsg = new StringBuilder();
                            doneMsg.AppendLine($"Updated to version {remote?.version ?? "unknown"}.");
                            doneMsg.AppendLine("package.json refreshed.");
                            doneMsg.AppendLine();
                            doneMsg.AppendLine("You can now close this window.");
                            win.SetTitle("Update Complete");
                            win.SetMessage(doneMsg.ToString());
                            win.SetButtons(new[] { "Close" }, 0, 0, _ => { }, keepOpenOnClick: false);
                        }
                        catch (Exception ex)
                        {
                            var err = $"Update failed: {ex.Message}";
                            HoyoToonDialogWindow.ShowError("Update Failed", err);
                            win.SetTitle("Update Failed");
                            win.SetMessage(err + "\n\nSome files may have been partially updated.");
                            win.SetButtons(new[] { "Close" }, 0, 0, _ => { }, keepOpenOnClick: false);
                        }
                    }
                    else
                    {
                        // Cancel
                        win.Close();
                    }
                }, keepOpenOnClick: true);
            }
            catch (Exception ex)
            {
                HoyoToonDialogWindow.ShowError("Updater Error", ex.Message);
                win.SetTitle("Error");
                win.SetMessage(ex.Message);
                win.SetButtons(new[] { "Close" }, 0, 0, _ => { }, keepOpenOnClick: false);
            }
        }

        private static async Task<string> FetchChangelogAsync(PackageInfo remote, UpdaterSettings settings)
        {
            if (remote == null || string.IsNullOrEmpty(remote.version)) return null;
            var branch = BranchSelector.GetCurrentBranch();
            using (var api = new GitHubApiClient(settings.repoOwner, settings.repoName, branch, settings.githubToken))
            {
                try
                {
                    // Prefer a GitHub Release matching the version (e.g., tag == version or v<version>)
                    var rel = await api.GetReleaseByTagAsync(remote.version) ?? await api.GetReleaseByTagAsync("v" + remote.version);
                    if (rel != null && !string.IsNullOrEmpty(rel.body))
                        return $"Release Notes for {remote.version}\n\n" + rel.body;
                }
                catch { }

                // Fallback: look for changelog.md
                try
                {
                    var text = await api.GetRawTextAsync("changelog.md");
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Try to extract the section for this version
                        var header = $"## {remote.version}";
                        int idx = text.IndexOf(header, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            int next = text.IndexOf("## ", idx + header.Length, StringComparison.OrdinalIgnoreCase);
                            string section = next > idx ? text.Substring(idx, next - idx) : text.Substring(idx);
                            return section.Trim();
                        }
                        return text.Trim();
                    }
                }
                catch { }
            }
            return null;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "\n...";
        }
    }
}
#endif
