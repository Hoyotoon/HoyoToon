#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.Updater
{
    internal static class UpdaterStartup
    {
        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            // Delay to let editor settle
            EditorApplication.delayCall += async () =>
            {
                try
                {
                    var settings = UpdaterSettings.FindOrCreate();
                    var controller = new UpdaterController(settings);
                    var local = controller.LoadLocalPackage();
                    var branch = BranchSelector.GetCurrentBranch();
                    using (var api = new GitHubApiClient(settings.repoOwner, settings.repoName, branch, settings.githubToken))
                    {
                        var remote = await api.GetPackageInfoAsync(settings.packageJsonRelativePath);
                        if (remote != null && IsNewer(remote.version, local?.version))
                        {
                            HoyoToonLogger.Always("Updater", $"New version available: {remote.version}", LogType.Log);
                            HoyoToonDialogWindow.ShowCustom(
                                "HoyoToon Update Available",
                                $"A newer version of HoyoToon is available on '{branch}' (remote: {remote.version}, local: {local?.version ?? "unknown"}).\n\nOpen the updater to review changes?",
                                MessageType.Info,
                                new[] { "Open Updater", "Later" },
                                0, 1,
                                result => { if (result == 0) UpdaterDialogFlow.Run(); });
                        }
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.Always("Updater", $"Startup update check failed: {ex.Message}", LogType.Warning);
                }
            };
        }

        private static bool IsNewer(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return false; if (string.IsNullOrEmpty(b)) return true;
            try { return new Version(a) > new Version(b); }
            catch { return string.Compare(a, b, StringComparison.OrdinalIgnoreCase) > 0; }
        }
    }
}
#endif
