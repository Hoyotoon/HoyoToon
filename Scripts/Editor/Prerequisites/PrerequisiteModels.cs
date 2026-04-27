#if UNITY_EDITOR
using System.Collections.Generic;

namespace HoyoToon.Editor.Prerequisites
{
    internal enum PrerequisiteSeverity
    {
        Info,
        Warning,
        Error,
    }

    internal enum PrerequisiteFixPolicy
    {
        None,
        SafeOnly,
    }

    internal readonly struct PrerequisiteEvaluation
    {
        public string Id { get; }

        public string DisplayName { get; }

        public bool Passed { get; }

        public bool IsBlocking { get; }

        public bool CanAutoFix { get; }

        public bool RequiresRestart { get; }

        public PrerequisiteSeverity Severity { get; }

        public string Message { get; }

        public string ActionHint { get; }

        public PrerequisiteEvaluation(
            string id,
            string displayName,
            bool passed,
            bool isBlocking,
            bool canAutoFix,
            bool requiresRestart,
            PrerequisiteSeverity severity,
            string message,
            string actionHint)
        {
            Id = id;
            DisplayName = displayName;
            Passed = passed;
            IsBlocking = isBlocking;
            CanAutoFix = canAutoFix;
            RequiresRestart = requiresRestart;
            Severity = severity;
            Message = message;
            ActionHint = actionHint;
        }

        public static PrerequisiteEvaluation Pass(string id, string displayName, string message = null)
        {
            return new PrerequisiteEvaluation(
                id,
                displayName,
                true,
                false,
                false,
                false,
                PrerequisiteSeverity.Info,
                message,
                null);
        }

        public static PrerequisiteEvaluation Warning(
            string id,
            string displayName,
            string message,
            bool isBlocking = false,
            bool canAutoFix = false,
            bool requiresRestart = false,
            string actionHint = null)
        {
            return new PrerequisiteEvaluation(
                id,
                displayName,
                false,
                isBlocking,
                canAutoFix,
                requiresRestart,
                PrerequisiteSeverity.Warning,
                message,
                actionHint);
        }

        public static PrerequisiteEvaluation Error(
            string id,
            string displayName,
            string message,
            bool canAutoFix = false,
            bool requiresRestart = false,
            string actionHint = null)
        {
            return new PrerequisiteEvaluation(
                id,
                displayName,
                false,
                true,
                canAutoFix,
                requiresRestart,
                PrerequisiteSeverity.Error,
                message,
                actionHint);
        }
    }

    internal readonly struct PrerequisiteFixResult
    {
        public bool Attempted { get; }

        public bool Applied { get; }

        public string Message { get; }

        public PrerequisiteFixResult(bool attempted, bool applied, string message)
        {
            Attempted = attempted;
            Applied = applied;
            Message = message;
        }

        public static PrerequisiteFixResult None(string message = null)
        {
            return new PrerequisiteFixResult(false, false, message);
        }

        public static PrerequisiteFixResult Failed(string message = null)
        {
            return new PrerequisiteFixResult(true, false, message);
        }

        public static PrerequisiteFixResult AppliedFix(string message = null)
        {
            return new PrerequisiteFixResult(true, true, message);
        }
    }

    internal readonly struct PrerequisiteCheckResult
    {
        public PrerequisiteEvaluation InitialEvaluation { get; }

        public PrerequisiteEvaluation FinalEvaluation { get; }

        public bool FixAttempted { get; }

        public bool FixApplied { get; }

        public string FixMessage { get; }

        public string Id => FinalEvaluation.Id;

        public string DisplayName => FinalEvaluation.DisplayName;

        public PrerequisiteCheckResult(
            PrerequisiteEvaluation initialEvaluation,
            PrerequisiteEvaluation finalEvaluation,
            bool fixAttempted,
            bool fixApplied,
            string fixMessage)
        {
            InitialEvaluation = initialEvaluation;
            FinalEvaluation = finalEvaluation;
            FixAttempted = fixAttempted;
            FixApplied = fixApplied;
            FixMessage = fixMessage;
        }
    }

    internal sealed class PrerequisiteReport
    {
        public IReadOnlyList<PrerequisiteCheckResult> Results { get; }

        public int TotalCount { get; }

        public int FailedCount { get; }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        public int BlockingCount { get; }

        public bool FixesAttempted { get; }

        public bool FixesApplied { get; }

        public bool AllPassed => FailedCount == 0;

        public PrerequisiteReport(
            IReadOnlyList<PrerequisiteCheckResult> results,
            int totalCount,
            int failedCount,
            int errorCount,
            int warningCount,
            int blockingCount,
            bool fixesAttempted,
            bool fixesApplied)
        {
            Results = results;
            TotalCount = totalCount;
            FailedCount = failedCount;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            BlockingCount = blockingCount;
            FixesAttempted = fixesAttempted;
            FixesApplied = fixesApplied;
        }
    }
}
#endif