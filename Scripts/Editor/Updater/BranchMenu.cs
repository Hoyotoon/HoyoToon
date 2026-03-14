#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.Updater
{
    internal static class BranchMenu
    {
        private const string MenuRoot = "HoyoToon/Updater/Branch/";
        private const string Stable = "Stable";
        private const string Beta = "Beta";

        private static string _resolvedStable = "main";
        private static string _resolvedBeta = "Beta";

        [MenuItem(MenuRoot + "Use Beta", priority = 120)]
        private static void UseBeta()
        {
            var msg = "You're about to switch to the Beta branch. Beta builds are experimental and may be unstable. Unless you're actively testing, you probably don't want to enable this. Issues encountered on Beta are not supported. Switching branches will perform a clean install to ensure consistency. Proceed?";
            DialogWindow.ShowYesNo("Switch to Beta", msg, MessageType.Warning, onResult: yes =>
            {
                if (!yes) return;
                RunSafe(() => SwitchBranchAsync(Beta, LogType.Warning));
            });
        }

        [MenuItem(MenuRoot + "Use Stable", priority = 121)]
        private static void UseStable()
        {
            var msg = "You're about to switch to the Stable branch. Switching branches will perform a clean install to ensure consistency. Proceed?";
            DialogWindow.ShowYesNo("Switch to Stable", msg, MessageType.Warning, onResult: yes =>
            {
                if (!yes) return;
                RunSafe(() => SwitchBranchAsync(Stable, LogType.Log));
            });
        }

        [MenuItem(MenuRoot + "Use Beta", true)]
        private static bool UseBetaValidate()
        {
            Menu.SetChecked(MenuRoot + "Use Beta", string.Equals(BranchSelector.GetCurrentBranch(), _resolvedBeta, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        [MenuItem(MenuRoot + "Use Stable", true)]
        private static bool UseStableValidate()
        {
            Menu.SetChecked(MenuRoot + "Use Stable", string.Equals(BranchSelector.GetCurrentBranch(), _resolvedStable, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        private static async Task SwitchBranchAsync(string channel, LogType logType)
        {
            var settings = UpdaterSettings.Instance;
            var current = BranchSelector.GetCurrentBranch();
            var api = new GitHubApiClient(settings.repoOwner, settings.repoName, current, settings.githubToken);
            var remoteBranches = await api.GetBranchNamesAsync();
            var target = ResolveTargetBranch(channel, remoteBranches, settings.defaultBranch);
            if (string.IsNullOrEmpty(target))
            {
                DialogWindow.ShowError("Branch Not Found", $"Could not resolve a remote '{channel}' branch.");
                return;
            }

            BranchSelector.SetBranch(target);
            if (string.Equals(channel, Stable, StringComparison.OrdinalIgnoreCase)) _resolvedStable = target;
            if (string.Equals(channel, Beta, StringComparison.OrdinalIgnoreCase)) _resolvedBeta = target;
            DialogWindow.ShowInfo("Branch Switched", $"You've switched to the '{target}' branch. A clean install will be performed on the next update to ensure consistency.");
            HoyoToonLogger.Always("Updater", $"Branch switched to {target}.", logType);
        }

        private static string ResolveTargetBranch(string channel, string[] remoteBranches, string defaultBeta)
        {
            if (remoteBranches == null || remoteBranches.Length == 0) return null;

            if (string.Equals(channel, Beta, StringComparison.OrdinalIgnoreCase))
            {
                return FindBranch(remoteBranches, defaultBeta)
                       ?? FindBranch(remoteBranches, "beta", "develop", "dev");
            }

            return FindBranch(remoteBranches, "main", "stable", "master");
        }

        private static string FindBranch(string[] remoteBranches, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                var exact = remoteBranches.FirstOrDefault(b => string.Equals(b, candidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(exact)) return exact;
            }
            return null;
        }

        private static void RunSafe(Func<Task> action)
        {
            _ = RunSafeAsync(action);
        }

        private static async Task RunSafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("Updater.BranchMenu", ex.Message);
                DialogWindow.ShowError("Branch Switch Failed", ex.Message);
            }
        }
    }
}
#endif