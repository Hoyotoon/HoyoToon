#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.UI.Windows;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Updater
{
    internal static class UpdaterDialogFlow
    {
        private class DialogProgressSink : IProgressSink
        {
            private readonly DialogWindow _win;
            public DialogProgressSink(DialogWindow win) { _win = win; }
            public void Report(string title, string info, float progress01)
            {
                if (_win == null) return;
                _win.SetTitle(title);
                if (!string.IsNullOrEmpty(info)) _win.SetProgressText(info);
                _win.UpdateProgress(Mathf.Clamp01(progress01));
            }
        }


        [MenuItem("HoyoToon/Updater/Run Updater...", priority = 80)]
        public static void Run()
        {
            var win = DialogWindow.ShowProgressWithCustomButtons(
                title: "HoyoToon Updater",
                message: "Checking for updates...",
                type: MessageType.Info,
                buttons: new string[0],
                topBar: BaseHoyoToonWindow.TopBarConfig.Default(),
                onResultIndex: null,
                keepOpenOnClick: true
            );

            _ = RunAsync(win);
        }

        private static async Task RunAsync(DialogWindow win)
        {
            if (win == null) return;

            var settings = UpdaterSettings.Instance;
            var controller = new UpdaterController(settings);
            var session = controller.CreateSession();

            try
            {
                win.SetTitle("Checking for Updates");
                win.SetMessage("Analyzing repository and building update plan...");
                win.SetShowProgressBar(true);
                win.UpdateProgress(0.1f, "Contacting GitHub...");

                var check = await controller.CheckAsync(session);
                var local = check.localPackage;
                var remote = check.remotePackage;
                var batch = check.batch;
                string changelog = await controller.GetChangelogAsync(remote, batch?.sourceCommitSha, session);

                var sb = new StringBuilder();
                string branch = session.Branch;
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
                    // Show full changelog to avoid cutting off content
                    sb.AppendLine(changelog);
                }

                win.SetTitle("Update Available");
                win.SetMessage(sb.ToString());
                win.UpdateProgress(0f, null);
                win.SetButtons(new[] { "Apply", "Cancel" }, defaultIndex: 0, cancelIndex: 1, onResultIndex: idx =>
                {
                    if (idx == 0)
                    {
                        RunSafe(() => ApplyUpdateAsync(win, controller, session, batch, remote), win, "Update failed");
                    }
                    else
                    {
                        win.Close();
                    }
                }, keepOpenOnClick: true);
            }
            catch (Exception ex)
            {
                DialogWindow.ShowError("Updater Error", ex.Message);
                win.SetTitle("Error");
                win.SetMessage(ex.Message);
                win.SetButtons(new[] { "Close" }, 0, 0, _ => { }, keepOpenOnClick: false);
            }
        }

        private static async Task ApplyUpdateAsync(DialogWindow win, UpdaterController controller, UpdaterSession session, UpdateBatch batch, PackageInfo remote)
        {
            win.SetTitle("Applying Update");
            win.SetMessage("Downloading the full update into staging, then applying it in one Unity import pass. Please wait...");
            win.SetShowProgressBar(true);
            win.UpdateProgress(0.01f, "Preparing...");
            win.SetButtons(Array.Empty<string>(), defaultIndex: 0, cancelIndex: -1, onResultIndex: null, keepOpenOnClick: true);

            var sink = new DialogProgressSink(win);
            try
            {
                await controller.ApplyAsync(batch, remote, sink, session);
                win.CompleteProgress("Done");

                var doneMsg = new StringBuilder();
                doneMsg.AppendLine($"Updated to version {remote?.version ?? "unknown"}.");
                doneMsg.AppendLine("package.json refreshed.");
                doneMsg.AppendLine();
                doneMsg.AppendLine("You can now close this window.");
                EditorApplication.delayCall += () =>
                {
                    if (win == null) return;
                    win.SetTitle("Update Complete");
                    win.SetMessage(doneMsg.ToString());
                    win.SetButtons(new[] { "Close" }, 0, 0, _ => { }, keepOpenOnClick: false);
                };
            }
            catch (Exception ex)
            {
                var err = $"Update failed: {ex.Message}";
                DialogWindow.ShowError("Update Failed", err);
                EditorApplication.delayCall += () =>
                {
                    if (win == null) return;
                    win.SetTitle("Update Failed");
                    win.SetMessage(err + "\n\nDownloads are already staged locally. If Unity closes or reloads, the updater will try to resume the install on the next editor startup.");
                    win.SetButtons(new[] { "Close" }, 0, 0, _ => { }, keepOpenOnClick: false);
                };
            }
        }

        private static void RunSafe(Func<Task> action, DialogWindow win, string title)
        {
            _ = RunSafeAsync(action, win, title);
        }

        private static async Task RunSafeAsync(Func<Task> action, DialogWindow win, string title)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                var message = $"{title}: {ex.Message}";
                HoyoToonLogger.ThrottleWarning("Updater.DialogFlow", message);
                DialogWindow.ShowError("Updater Error", message);
                if (win == null) return;
                win.SetTitle("Error");
                win.SetMessage(message);
                win.SetButtons(new[] { "Close" }, 0, 0, _ => { }, keepOpenOnClick: false);
            }
        }
    }
}
#endif
