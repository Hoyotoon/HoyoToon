#if UNITY_EDITOR
using System.Text;
using HoyoToon.Editor.Utilities.Debugging;

namespace HoyoToon.Editor.Prerequisites
{
    internal static class PrerequisiteReportLogger
    {
        public static void LogReport(PrerequisiteReport report, bool isStartup, PrerequisiteFixPolicy fixPolicy)
        {
            if (report == null)
            {
                return;
            }

            foreach (PrerequisiteCheckResult result in report.Results)
            {
                LogResult(result, isStartup);
            }

            if (report.AllPassed)
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Prerequisites,
                    BuildSuccessSummary(report, fixPolicy),
                    isBackgroundOperation: isStartup);
                return;
            }

            HoyoToonLogger.Warning(
                HoyoToonLogCategory.Prerequisites,
                BuildFailureSummary(report, fixPolicy),
                isBackgroundOperation: isStartup);
        }

        private static void LogResult(PrerequisiteCheckResult result, bool isStartup)
        {
            PrerequisiteEvaluation evaluation = result.FinalEvaluation;

            if (evaluation.Passed)
            {
                if (result.FixApplied)
                {
                    string fixMessage = string.IsNullOrWhiteSpace(result.FixMessage)
                        ? $"Applied safe fix for '{result.DisplayName}'."
                        : $"Applied safe fix for '{result.DisplayName}': {result.FixMessage}";
                    HoyoToonLogger.Info(HoyoToonLogCategory.Prerequisites, fixMessage, isBackgroundOperation: isStartup);
                }
                else
                {
                    HoyoToonLogger.Verbose(
                        HoyoToonLogCategory.Prerequisites,
                        $"Prerequisite passed: {result.DisplayName}. {evaluation.Message}",
                        isBackgroundOperation: isStartup);
                }

                return;
            }

            string message = BuildFailureMessage(result);
            if (evaluation.Severity == PrerequisiteSeverity.Error)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.Prerequisites, message, isBackgroundOperation: isStartup);
                return;
            }

            HoyoToonLogger.Warning(HoyoToonLogCategory.Prerequisites, message, isBackgroundOperation: isStartup);
        }

        private static string BuildFailureMessage(PrerequisiteCheckResult result)
        {
            PrerequisiteEvaluation evaluation = result.FinalEvaluation;
            var messageBuilder = new StringBuilder();
            messageBuilder.Append(result.DisplayName);
            messageBuilder.Append(": ");
            messageBuilder.Append(evaluation.Message);

            if (!string.IsNullOrWhiteSpace(evaluation.ActionHint))
            {
                messageBuilder.Append(" Action: ");
                messageBuilder.Append(evaluation.ActionHint);
            }

            if (evaluation.RequiresRestart)
            {
                messageBuilder.Append(" A Unity editor restart is still required.");
            }

            if (result.FixAttempted && !result.FixApplied && !string.IsNullOrWhiteSpace(result.FixMessage))
            {
                messageBuilder.Append(" Safe fix result: ");
                messageBuilder.Append(result.FixMessage);
            }

            return messageBuilder.ToString();
        }

        private static string BuildSuccessSummary(PrerequisiteReport report, PrerequisiteFixPolicy fixPolicy)
        {
            string prefix = fixPolicy == PrerequisiteFixPolicy.SafeOnly
                ? "Editor prerequisite safe-fix pass"
                : "Editor prerequisite check";
            return report.FixesApplied
                ? $"{prefix} passed after applying {report.Results.Count} check(s)."
                : $"{prefix} passed ({report.TotalCount} check(s)).";
        }

        private static string BuildFailureSummary(PrerequisiteReport report, PrerequisiteFixPolicy fixPolicy)
        {
            string prefix = fixPolicy == PrerequisiteFixPolicy.SafeOnly
                ? "Editor prerequisite safe-fix pass"
                : "Editor prerequisite check";
            return $"{prefix} found {report.FailedCount} unresolved check(s) ({report.ErrorCount} error(s), {report.WarningCount} warning(s)).";
        }
    }
}
#endif