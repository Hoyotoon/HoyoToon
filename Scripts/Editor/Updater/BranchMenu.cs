#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.Updater
{
    internal static class BranchMenu
    {
        private const string MenuRoot = "HoyoToon/Updater/Branch/";
        private const string Stable = "main"; // Stable branch name
        private const string Beta = "Beta";   // Beta branch name

        [MenuItem(MenuRoot + "Use Beta", priority = 120)]
        private static void UseBeta()
        {
            // Warning message for opting into Beta
                var msg = "You're about to switch to the Beta branch. Beta builds are experimental and may be unstable. Unless you're actively testing, you probably don't want to enable this. Issues encountered on Beta are not supported. Switching branches will perform a clean install to ensure consistency. Proceed?";
            HoyoToonDialogWindow.ShowYesNo("Switch to Beta", msg, MessageType.Warning, onResult: yes =>
            {
                if (!yes) return;
                BranchSelector.SetBranch(Beta);
                HoyoToonLogger.Always("Updater", "Branch switched to Beta.", LogType.Warning);
            });
        }

        [MenuItem(MenuRoot + "Use Stable", priority = 121)]
        private static void UseStable()
        {
                BranchSelector.SetBranch(Stable);
                HoyoToonDialogWindow.ShowInfo("Branch Switched", "You've switched to the Stable branch. A clean install will be performed on the next update to ensure consistency.");
            HoyoToonLogger.Always("Updater", "Branch switched to Stable.", LogType.Log);
        }

        [MenuItem(MenuRoot + "Use Beta", true)]
        private static bool UseBetaValidate()
        {
            Menu.SetChecked(MenuRoot + "Use Beta", BranchSelector.GetCurrentBranch() == Beta);
            return true;
        }

        [MenuItem(MenuRoot + "Use Stable", true)]
        private static bool UseStableValidate()
        {
            Menu.SetChecked(MenuRoot + "Use Stable", BranchSelector.GetCurrentBranch() == Stable);
            return true;
        }
    }
}
#endif