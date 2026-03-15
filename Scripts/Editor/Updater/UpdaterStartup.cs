#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.Updater
{
    internal static class UpdaterStartup
    {
        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            EditorApplication.delayCall += async () =>
            {
                try
                {
                    var settings = UpdaterSettings.Instance;
                    var controller = new UpdaterController(settings);

                    if (PendingInstallStore.Exists())
                    {
                        HoyoToonLogger.Always("Updater", "Resuming interrupted staged update.", LogType.Warning);
                        if (await controller.ResumePendingInstallAsync())
                        {
                            DialogWindow.ShowInfo("HoyoToon Updater", "Resumed and completed the interrupted HoyoToon update.");
                            return;
                        }
                    }

                    var session = controller.CreateSession();
                    var availability = await controller.CheckForAvailableUpdateAsync(session);
                    if (availability.HasUpdate)
                    {
                        HoyoToonLogger.Always("Updater", $"New version available: {availability.remotePackage.version}", LogType.Log);
                        DialogWindow.ShowCustom(
                            "HoyoToon Update Available",
                            $"A newer version of HoyoToon is available on '{availability.branch}' (remote: {availability.remotePackage.version}, local: {availability.localPackage?.version ?? "unknown"}).\n\nOpen the updater to review changes?",
                            MessageType.Info,
                            new[] { "Open Updater", "Later" },
                            0, 1,
                            result => { if (result == 0) UpdaterDialogFlow.Run(); });
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.Always("Updater", $"Startup update check failed: {ex.Message}", LogType.Warning);
                }
            };
        }
    }
}
#endif
