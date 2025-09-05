#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.Prerequisites
{
    [InitializeOnLoad]
    internal static class PrerequisitesRunner
    {
        private static bool s_ran;
        private static readonly List<IPrerequisiteCheck> s_checks;

        static PrerequisitesRunner()
        {
            s_checks = new List<IPrerequisiteCheck>
            {
                new ColorSpaceLinearCheck(),
                new ShadowProjectionCloseFitCheck(),
            };
            EditorApplication.update += RunOnce;
        }

        private static void RunOnce()
        {
            if (s_ran) return;
            s_ran = true;

            // Evaluate all
            foreach (var check in s_checks)
            {
                var result = check.Evaluate();
                if (result.Passed)
                {
                    if (!string.IsNullOrEmpty(result.Message))
                        HoyoToonLogger.ManagerInfo($"Prerequisite OK: {check.Name} - {result.Message}");
                    continue;
                }

                // Try auto-fix BEFORE notifying the user
                bool fixedNow = false;
                try { fixedNow = check.TryFix(); }
                catch (System.Exception ex)
                {
                    HoyoToonLogger.Always("Manager", $"Prerequisite TryFix threw: {check.Name} - {ex}", LogType.Exception);
                }

                // Re-evaluate to confirm state after attempted fix
                var after = check.Evaluate();
                if (fixedNow || after.Passed)
                {
                    HoyoToonLogger.ManagerInfo($"Prerequisite fixed: {check.Name}");
                    if (!string.IsNullOrEmpty(after.Message))
                        HoyoToonLogger.ManagerInfo($"{check.Name} - {after.Message}");
                    continue; // No popup needed if it ended up OK
                }

                // Still failing after auto-fix attempt: notify user
                HoyoToonLogger.ManagerWarning($"Prerequisite could not be fixed automatically: {check.Name}");
                string title = after.Severity == PrerequisiteSeverity.Error ? "Project Prerequisite Error" : "Project Prerequisite";
                string msg = $"**{check.Name}**\n\n{after.Message}\n\nAuto-fix could not be applied. Please adjust this setting manually.";
                HoyoToonDialogWindow.ShowOk(title, msg, after.Severity == PrerequisiteSeverity.Error ? MessageType.Error : MessageType.Warning);
            }
        }
    }
}
#endif
