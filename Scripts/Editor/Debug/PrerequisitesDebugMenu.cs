#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using HoyoToon.Prerequisites;
using HoyoToon.Utilities;

namespace HoyoToon.Debugging
{
    internal static class PrerequisitesDebugMenu
    {
        private const string MenuRunChecks = "HoyoToon/Debug/Prerequisites/Run Checks";

        [MenuItem(MenuRunChecks, false, 530)]
        private static void RunChecks()
        {
            var checks = new IPrerequisiteCheck[]
            {
                new ColorSpaceLinearCheck(),
                new ShadowProjectionCloseFitCheck(),
            };

            var sb = new StringBuilder();
            bool anyIssues = false;

            sb.AppendLine("# Prerequisites Report\n");

            foreach (var check in checks)
            {
                var result = check.Evaluate();
                if (result.Passed)
                {
                    HoyoToonLogger.ManagerInfo($"OK: {check.Name} - {result.Message}");
                    sb.AppendLine($"- **{check.Name}**: OK - {result.Message}");
                }
                else
                {
                    anyIssues = true;
                    HoyoToonLogger.ManagerWarning($"Issue: {check.Name} - {result.Message}");
                    sb.AppendLine($"- **{check.Name}**: Needs Attention - {result.Message}");

                    // Try auto-fix
                    if (check.TryFix())
                    {
                        HoyoToonLogger.ManagerInfo($"Auto-fixed: {check.Name}");
                        sb.AppendLine("  -> Auto-fixed");
                    }
                    else
                    {
                        HoyoToonLogger.ManagerWarning($"Could not auto-fix: {check.Name}");
                        sb.AppendLine("  -> Could not auto-fix");
                    }
                }
            }

            var msg = sb.ToString();
            HoyoToonDialogWindow.ShowOk(
                anyIssues ? "Prerequisites - Issues Found" : "Prerequisites - All Good",
                msg,
                anyIssues ? MessageType.Warning : MessageType.Info
            );
        }
    }
}
#endif
