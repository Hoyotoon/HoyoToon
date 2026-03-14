#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.Onboarding;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.Prerequisites
{
    public readonly struct PrerequisitesAggregateStatus
    {
        public readonly int TotalChecks;
        public readonly int FailedChecks;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly bool AnyAutoFixAttempted;
        public readonly bool AnyAutoFixApplied;

        public bool AllPassed => FailedChecks == 0;

        public PrerequisitesAggregateStatus(
            int totalChecks,
            int failedChecks,
            int errorCount,
            int warningCount,
            bool anyAutoFixAttempted,
            bool anyAutoFixApplied)
        {
            TotalChecks = totalChecks;
            FailedChecks = failedChecks;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            AnyAutoFixAttempted = anyAutoFixAttempted;
            AnyAutoFixApplied = anyAutoFixApplied;
        }
    }

    [InitializeOnLoad]
    internal static class PrerequisitesRunner
    {
        private readonly struct PrerequisiteExecutionOutcome
        {
            public readonly string CheckName;
            public readonly PrerequisiteResult InitialResult;
            public readonly PrerequisiteResult FinalResult;
            public readonly bool AutoFixAttempted;
            public readonly bool AutoFixApplied;

            public PrerequisiteExecutionOutcome(
                string checkName,
                PrerequisiteResult initialResult,
                PrerequisiteResult finalResult,
                bool autoFixAttempted,
                bool autoFixApplied)
            {
                CheckName = checkName;
                InitialResult = initialResult;
                FinalResult = finalResult;
                AutoFixAttempted = autoFixAttempted;
                AutoFixApplied = autoFixApplied;
            }
        }

        private readonly struct PrerequisitesExecutionSummary
        {
            public readonly List<PrerequisiteExecutionOutcome> Outcomes;
            public readonly PrerequisitesAggregateStatus AggregateStatus;
            public readonly int InitialFailedCount;
            public readonly int AutoFixAttemptedCount;
            public readonly int AutoFixAppliedCount;

            public int UnresolvedCount => AggregateStatus.FailedChecks;

            public PrerequisitesExecutionSummary(
                List<PrerequisiteExecutionOutcome> outcomes,
                PrerequisitesAggregateStatus aggregateStatus,
                int initialFailedCount,
                int autoFixAttemptedCount,
                int autoFixAppliedCount)
            {
                Outcomes = outcomes;
                AggregateStatus = aggregateStatus;
                InitialFailedCount = initialFailedCount;
                AutoFixAttemptedCount = autoFixAttemptedCount;
                AutoFixAppliedCount = autoFixAppliedCount;
            }
        }

        private static bool s_ran;
        private static readonly List<IPrerequisiteCheck> s_checks;

        static PrerequisitesRunner()
        {
            s_checks = new List<IPrerequisiteCheck>
            {
                new VRCSDKInstalledCheck(),
                new RenderPipelineCheck(),
                new ColorSpaceLinearCheck(),
                new ShadowProjectionCloseFitCheck(),
                new InputSystemBackendCheck(),
            };
            EditorApplication.update += RunOnce;
        }

        public static PrerequisitesAggregateStatus EvaluateAggregateStatus(bool attemptAutoFix)
        {
            return ExecuteChecks(attemptAutoFix).AggregateStatus;
        }

        private static void RunOnce()
        {
            if (s_ran) return;
            s_ran = true;
            var startupPolicy = OnboardingStartupPolicy.Evaluate();
            var execution = ExecuteChecks(attemptAutoFix: true);

            foreach (var outcome in execution.Outcomes)
            {
                if (outcome.InitialResult.Passed)
                {
                    if (!string.IsNullOrEmpty(outcome.InitialResult.Message))
                    {
                        HoyoToonLogger.Log(
                            HoyoToonLogger.Categories.Manager,
                            LogLevel.Info,
                            $"Prerequisite OK: {outcome.CheckName} - {outcome.InitialResult.Message}");
                    }

                    continue;
                }

                if (outcome.FinalResult.Passed)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Prerequisite fixed: {outcome.CheckName}");
                    if (!string.IsNullOrEmpty(outcome.FinalResult.Message))
                    {
                        HoyoToonLogger.Log(
                            HoyoToonLogger.Categories.Manager,
                            LogLevel.Info,
                            $"{outcome.CheckName} - {outcome.FinalResult.Message}");
                    }

                    continue;
                }

                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Prerequisite could not be fixed automatically: {outcome.CheckName}");
                if (startupPolicy.IsBatchMode)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Batch mode active; skipping prerequisite dialog for: {outcome.CheckName}");
                    continue;
                }

                if (startupPolicy.ShouldDeferPrerequisiteDialogs)
                {
                    HoyoToonLogger.Log(
                        HoyoToonLogger.Categories.Tour,
                        LogLevel.Info,
                        $"OnboardingDiag event=prereq-dialog-deferred check={outcome.CheckName} severity={outcome.FinalResult.Severity}");
                    continue;
                }

                ShowManualFixDialog(outcome);
            }

            HoyoToonLogger.Log(
                HoyoToonLogger.Categories.Tour,
                LogLevel.Info,
                $"OnboardingDiag event=prereq-startup-run total={s_checks.Count} initialFailed={execution.InitialFailedCount} autoFixAttempted={execution.AutoFixAttemptedCount} autoFixApplied={execution.AutoFixAppliedCount} unresolved={execution.UnresolvedCount} finalFailed={execution.AggregateStatus.FailedChecks} errors={execution.AggregateStatus.ErrorCount} warnings={execution.AggregateStatus.WarningCount} allPassed={execution.AggregateStatus.AllPassed} batchMode={startupPolicy.IsBatchMode} deferredToOnboarding={startupPolicy.ShouldDeferPrerequisiteDialogs}");
        }

        private static bool TryFix(IPrerequisiteCheck check)
        {
            try
            {
                return check.TryFix();
            }
            catch (System.Exception ex)
            {
                HoyoToonLogger.Always("Manager", $"Prerequisite TryFix threw: {check.Name} - {ex}", LogType.Exception);
                return false;
            }
        }

        private static PrerequisitesExecutionSummary ExecuteChecks(bool attemptAutoFix)
        {
            var outcomes = new List<PrerequisiteExecutionOutcome>(s_checks.Count);
            int failedChecks = 0;
            int errorCount = 0;
            int warningCount = 0;
            int initialFailedCount = 0;
            int autoFixAttemptedCount = 0;
            int autoFixAppliedCount = 0;
            bool anyAutoFixAttempted = false;
            bool anyAutoFixApplied = false;

            foreach (var check in s_checks)
            {
                var initial = check.Evaluate();
                var final = initial;
                bool autoFixAttempted = false;
                bool autoFixApplied = false;

                if (!initial.Passed)
                {
                    initialFailedCount++;

                    if (attemptAutoFix)
                    {
                        autoFixAttempted = true;
                        anyAutoFixAttempted = true;
                        autoFixAttemptedCount++;
                        autoFixApplied = TryFix(check);
                        final = check.Evaluate();
                        if (autoFixApplied)
                        {
                            anyAutoFixApplied = true;
                            autoFixAppliedCount++;
                        }
                    }
                }

                var effective = attemptAutoFix ? final : initial;
                if (!effective.Passed)
                {
                    failedChecks++;
                    if (effective.Severity == PrerequisiteSeverity.Error)
                    {
                        errorCount++;
                    }
                    else if (effective.Severity == PrerequisiteSeverity.Warning)
                    {
                        warningCount++;
                    }
                }

                outcomes.Add(new PrerequisiteExecutionOutcome(check.Name, initial, final, autoFixAttempted, autoFixApplied));
            }

            return new PrerequisitesExecutionSummary(
                outcomes,
                new PrerequisitesAggregateStatus(
                    totalChecks: s_checks.Count,
                    failedChecks: failedChecks,
                    errorCount: errorCount,
                    warningCount: warningCount,
                    anyAutoFixAttempted: anyAutoFixAttempted,
                    anyAutoFixApplied: anyAutoFixApplied),
                initialFailedCount,
                autoFixAttemptedCount,
                autoFixAppliedCount);
        }

        private static void ShowManualFixDialog(PrerequisiteExecutionOutcome outcome)
        {
            string title = outcome.FinalResult.Severity == PrerequisiteSeverity.Error ? "Project Prerequisite Error" : "Project Prerequisite";
            string message = $"**{outcome.CheckName}**\n\n{outcome.FinalResult.Message}\n\nAuto-fix could not be applied. Please adjust this setting manually.";
            DialogWindow.ShowOk(
                title,
                message,
                outcome.FinalResult.Severity == PrerequisiteSeverity.Error ? MessageType.Error : MessageType.Warning);
        }
    }
}
#endif
