#if UNITY_EDITOR
using System;
using HoyoToon.Editor.Utilities.Editor;
using UnityEditor;

namespace HoyoToon.Editor.Updater
{
    [InitializeOnLoad]
    internal static class PackageUpdaterEntryPoints
    {
        private static readonly TimeSpan AutoCheckInterval = TimeSpan.FromHours(6);
        private const double AutoCheckHeartbeatSeconds = 60d;
        private const string MenuRoot = "HoyoToon/Updater/";
        private const string CheckForUpdatesMenuPath = MenuRoot + "Check For Updates";
        private const string BranchMainMenuPath = MenuRoot + "Branch/Main";
        private const string BranchBetaMenuPath = MenuRoot + "Branch/Beta";
        private const string AutoCheckMenuPath = MenuRoot + "Automatic Check On Startup";

        private static bool startupReadyHandled;
        private static double nextAutoCheckHeartbeat;

        static PackageUpdaterEntryPoints()
        {
            PackageUpdaterStorage.EnsureDefaults();
            EditorApplication.delayCall += TryHandleStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem(CheckForUpdatesMenuPath, false, 10)]
        private static void CheckForUpdatesMenu()
        {
            PackageUpdaterService.CheckForUpdates(showUpToDateDialog: true, automatic: false, cleanMissingFiles: true);
        }

        [MenuItem(BranchMainMenuPath, false, 20)]
        private static void SetMainBranchMenu()
        {
            SetBranch(PackageUpdaterStorage.MainBranchName);
        }

        [MenuItem(BranchMainMenuPath, true)]
        private static bool SetMainBranchMenuValidate()
        {
            Menu.SetChecked(BranchMainMenuPath, string.Equals(PackageUpdaterStorage.CurrentBranch, PackageUpdaterStorage.MainBranchName, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        [MenuItem(BranchBetaMenuPath, false, 21)]
        private static void SetBetaBranchMenu()
        {
            SetBranch(PackageUpdaterStorage.BetaBranchName);
        }

        [MenuItem(BranchBetaMenuPath, true)]
        private static bool SetBetaBranchMenuValidate()
        {
            Menu.SetChecked(BranchBetaMenuPath, string.Equals(PackageUpdaterStorage.CurrentBranch, PackageUpdaterStorage.BetaBranchName, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        [MenuItem(AutoCheckMenuPath, false, 30)]
        private static void ToggleAutoCheckMenu()
        {
            PackageUpdaterStorage.AutoCheckEnabled = !PackageUpdaterStorage.AutoCheckEnabled;
            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(
                "Updater Settings",
                $"Automatic update checks have been {(PackageUpdaterStorage.AutoCheckEnabled ? "enabled" : "disabled")}.",
                "OK");
        }

        [MenuItem(AutoCheckMenuPath, true)]
        private static bool ToggleAutoCheckMenuValidate()
        {
            Menu.SetChecked(AutoCheckMenuPath, PackageUpdaterStorage.AutoCheckEnabled);
            return true;
        }

        private static void OnEditorUpdate()
        {
            TryHandleStartup();

            if (!startupReadyHandled || !PackageUpdaterStorage.AutoCheckEnabled || EditorApplication.timeSinceStartup < nextAutoCheckHeartbeat)
            {
                return;
            }

            nextAutoCheckHeartbeat = EditorApplication.timeSinceStartup + AutoCheckHeartbeatSeconds;
            TryRunAutomaticCheck(force: false);
        }

        private static void TryHandleStartup()
        {
            if (startupReadyHandled)
            {
                return;
            }

            if (!EditorReadinessUtility.IsReadyForEditorWork())
            {
                return;
            }

            startupReadyHandled = true;
            nextAutoCheckHeartbeat = EditorApplication.timeSinceStartup + AutoCheckHeartbeatSeconds;
            PackageUpdaterService.TryResumePendingInstallOnStartup();
            TryRunAutomaticCheck(force: false);
        }

        private static void TryRunAutomaticCheck(bool force)
        {
            if (PackageUpdaterService.IsBusy())
            {
                return;
            }

            if (!force && !ShouldRunAutomaticCheckNow())
            {
                return;
            }

            PackageUpdaterStorage.SetLastAutoCheckUtc(DateTime.UtcNow);
            PackageUpdaterService.CheckForUpdates(showUpToDateDialog: false, automatic: true, cleanMissingFiles: true);
        }

        private static bool ShouldRunAutomaticCheckNow()
        {
            DateTime? lastCheckUtc = PackageUpdaterStorage.GetLastAutoCheckUtc();
            return !lastCheckUtc.HasValue || DateTime.UtcNow - lastCheckUtc.Value >= AutoCheckInterval;
        }

        private static void SetBranch(string branch)
        {
            string normalizedBranch = PackageUpdaterStorage.NormalizeBranch(branch);
            if (string.Equals(PackageUpdaterStorage.CurrentBranch, normalizedBranch, StringComparison.OrdinalIgnoreCase))
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Updater Branch", $"HoyoToon is already using the '{normalizedBranch}' branch.", "OK");
                return;
            }

            if (PackageUpdaterService.IsBusy())
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Updater Busy", "The updater is already running. Wait for it to finish before switching branches.", "OK");
                return;
            }

            PackageUpdaterStorage.CurrentBranch = normalizedBranch;
            PackageUpdaterService.ResetCachedStatus();

            string message =
                $"HoyoToon will now use the '{normalizedBranch}' branch.\n\n" +
                "Missing files will be cleaned automatically during the next update.\n\n" +
                "Check for updates on the new branch now?";

            if (!HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("Updater Branch Changed", message, "Check Now", "Later"))
            {
                return;
            }

            PackageUpdaterService.CheckForUpdates(showUpToDateDialog: true, automatic: false, cleanMissingFiles: true);
        }
    }
}
#endif
