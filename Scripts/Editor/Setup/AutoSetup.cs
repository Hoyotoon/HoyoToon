#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Text;
using HoyoToon.Editor.Detection.Character;
using HoyoToon.Editor.Prerequisites;
using HoyoToon.Editor.Prerequisites.RenderPipeline;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.Editor;

namespace HoyoToon.Editor.Setup
{
    internal static class AutoSetup
    {
        public static AutoSetupResult RunSelection(AutoSetupOptions options = null)
        {
            AutoSetupOptions resolvedOptions = options ?? new AutoSetupOptions();
            if (!EditorReadinessUtility.IsReadyForEditorWork())
            {
                var blockedResult = new AutoSetupResult(null, "Auto Setup");
                blockedResult.RecordError("Auto setup is unavailable while the Unity editor is compiling or updating.");

                if (resolvedOptions.ShowDialogs)
                {
                    HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(
                        "HoyoToon Auto Setup",
                        "Wait for Unity to finish compiling or updating, then run auto setup again.",
                        "OK");
                }

                return blockedResult;
            }

            using (LogCore.PushSetupScope())
            {
                var runStopwatch = Stopwatch.StartNew();

                try
                {
                    ShowProgress("Inspecting selection and detecting game...", 0f);

                    AutoSetupContext context = new AutoSetupContext(resolvedOptions);
                    if (context.SelectedFbxAssetPaths.Count <= 0)
                    {
                        var invalidSelectionResult = new AutoSetupResult(null, "Auto Setup");
                        invalidSelectionResult.RecordError("Select at least one FBX asset before running auto setup.");

                        if (resolvedOptions.ShowDialogs)
                        {
                            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(
                                "HoyoToon Auto Setup",
                                "Right-click one or more FBX assets and choose HoyoToon/Auto Setup.",
                                "OK");
                        }

                        return invalidSelectionResult;
                    }

                    if (resolvedOptions.ShowDialogs
                        && resolvedOptions.PromptForKnownCharacterProblems
                        && !CharacterProblemPrompt.ConfirmAssetContexts(context.SelectedFbxAssetPaths, "Auto Setup"))
                    {
                        var cancelledResult = new AutoSetupResult(context.DetectedGameKey, "Auto Setup");
                        cancelledResult.RecordWarning("Auto setup was cancelled because the selected character has a known problem.");
                        HoyoToonLogger.Info(
                            HoyoToonLogCategory.Setup,
                            "Auto setup was cancelled from the known character problem prompt.",
                            isBackgroundOperation: true);
                        return cancelledResult;
                    }

                    AutoSetupGameProfile profile = AutoSetupRegistry.Resolve(context);
                    if (profile == null)
                    {
                        var missingProfileResult = new AutoSetupResult(context.DetectedGameKey, "Auto Setup");
                        missingProfileResult.RecordError("No matching auto setup profile was found for the current selection.");

                        string message = "Currently " + context.DetectedGameKey + " is not supported.";
                        HoyoToonLogger.Warning(
                            HoyoToonLogCategory.Setup,
                            message,
                            isBackgroundOperation: true);

                        if (context.Options.ShowDialogs)
                        {
                            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("HoyoToon Auto Setup", message, "OK");
                        }

                        return missingProfileResult;
                    }

                    return RunProfile(profile, context, runStopwatch);
                }
                finally
                {
                    ClearProgress();
                }
            }
        }

        internal static void RefreshContext(AutoSetupContext context)
        {
            context?.RefreshInternal();
        }

        internal static void ApplyPrerequisites(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null)
            {
                return;
            }

            PrerequisiteReport report = PrerequisiteService.Evaluate(PrerequisiteFixPolicy.SafeOnly);
            if (context.Options.PromptForRenderPipelineSelection
                && RenderPipelineStartupPrompt.PromptForSelectionIfNeeded(report))
            {
                report = PrerequisiteService.Evaluate(PrerequisiteFixPolicy.SafeOnly);
            }

            result.PrerequisiteFailures = report.FailedCount;
            if (!report.AllPassed)
            {
                result.RecordWarning(
                    $"Prerequisite checks still report {report.FailedCount} issue(s) after safe fixes.");
            }
        }

        private static AutoSetupResult RunProfile(
            AutoSetupGameProfile profile,
            AutoSetupContext context,
            Stopwatch runStopwatch)
        {
            var result = new AutoSetupResult(profile.GameKey, profile.DisplayName);
            HoyoToonLogger.Info(
                HoyoToonLogCategory.Setup,
                $"Starting auto setup for '{profile.DisplayName}'.",
                isBackgroundOperation: true);

            int totalFeatures = profile.Features.Count;
            for (int i = 0; i < totalFeatures; i++)
            {
                AutoSetupFeature feature = profile.Features[i];
                if (feature == null || !feature.CanRun(context))
                {
                    continue;
                }

                int featureNumber = i + 1;
                ShowProgress(
                    $"{profile.DisplayName}: Step {featureNumber}/{totalFeatures} - {feature.DisplayName}",
                    CalculateFeatureProgress(featureNumber - 1, totalFeatures));

                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Setup,
                    $"Running auto setup step {featureNumber}/{totalFeatures}: {feature.DisplayName}.",
                    isBackgroundOperation: true);

                var featureStopwatch = Stopwatch.StartNew();
                try
                {
                    feature.Execute(context, result);
                    result.RecordFeature(feature.Id);

                    ShowProgress(
                        $"{profile.DisplayName}: Completed {feature.DisplayName}",
                        CalculateFeatureProgress(featureNumber, totalFeatures));

                    HoyoToonLogger.Info(
                        HoyoToonLogCategory.Setup,
                        $"Completed auto setup step {featureNumber}/{totalFeatures}: {feature.DisplayName} in {FormatDuration(featureStopwatch.Elapsed)}.",
                        isBackgroundOperation: true);
                }
                catch (Exception exception)
                {
                    string message =
                        $"Auto setup step {featureNumber}/{totalFeatures} '{feature.DisplayName}' failed after {FormatDuration(featureStopwatch.Elapsed)}.";
                    result.RecordError(message);
                    HoyoToonLogger.Error(
                        HoyoToonLogCategory.Setup,
                        message,
                        exception,
                        isBackgroundOperation: true);
                }
            }

            ShowProgress($"{profile.DisplayName}: Finalizing...", 1f);

            if (context.Options.LogSummary)
            {
                LogSummary(result, runStopwatch.Elapsed);
            }

            if (!result.Succeeded && context.Options.ShowDialogs)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(
                    "HoyoToon Auto Setup",
                    BuildErrorDialogMessage(result),
                    "OK");
            }

            return result;
        }

        private static void LogSummary(AutoSetupResult result, TimeSpan elapsed)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.Append("Auto setup finished for '");
            messageBuilder.Append(result.ProfileDisplayName);
            messageBuilder.Append("'. Features=");
            messageBuilder.Append(result.ExecutedFeatures.Count);
            messageBuilder.Append(", materials created=");
            messageBuilder.Append(result.MaterialsCreated);
            messageBuilder.Append(", materials updated=");
            messageBuilder.Append(result.MaterialsUpdated);
            messageBuilder.Append(", models converted=");
            messageBuilder.Append(result.ModelsConverted);
            messageBuilder.Append(", import settings applied=");
            messageBuilder.Append(result.ModelImportSettingsApplied);
            messageBuilder.Append(", tangents applied=");
            messageBuilder.Append(result.TangentApplications);
            messageBuilder.Append(", prerequisite failures=");
            messageBuilder.Append(result.PrerequisiteFailures);
            messageBuilder.Append(", warnings=");
            messageBuilder.Append(result.Warnings.Count);
            messageBuilder.Append(", errors=");
            messageBuilder.Append(result.Errors.Count);
            messageBuilder.Append(", duration=");
            messageBuilder.Append(FormatDuration(elapsed));
            messageBuilder.Append('.');

            if (result.Succeeded)
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Setup,
                    messageBuilder.ToString(),
                    isBackgroundOperation: true);
                return;
            }

            HoyoToonLogger.Warning(
                HoyoToonLogCategory.Setup,
                messageBuilder.ToString(),
                isBackgroundOperation: true);
        }

        private static void ShowProgress(string message, float progress)
        {
            if (UnityEngine.Application.isBatchMode)
            {
                return;
            }

            float clampedProgress = Math.Max(0f, Math.Min(1f, progress));
            UnityEditor.EditorUtility.DisplayProgressBar("HoyoToon Auto Setup", message, clampedProgress);
        }

        private static void ClearProgress()
        {
            if (UnityEngine.Application.isBatchMode)
            {
                return;
            }

            UnityEditor.EditorUtility.ClearProgressBar();
        }

        private static float CalculateFeatureProgress(int completedFeatures, int totalFeatures)
        {
            if (totalFeatures <= 0)
            {
                return 1f;
            }

            return (float)completedFeatures / totalFeatures;
        }

        private static string FormatDuration(TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds < 1d)
            {
                return $"{Math.Max(1d, elapsed.TotalMilliseconds):0} ms";
            }

            if (elapsed.TotalMinutes < 1d)
            {
                return $"{elapsed.TotalSeconds:0.0} s";
            }

            return $"{Math.Floor(elapsed.TotalMinutes):0}m {elapsed.Seconds:00}s";
        }

        private static string BuildErrorDialogMessage(AutoSetupResult result)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.Append("Auto setup finished with ");
            messageBuilder.Append(result.Errors.Count);
            messageBuilder.Append(" error(s).");

            if (result.Warnings.Count > 0)
            {
                messageBuilder.AppendLine();
                messageBuilder.Append("Warnings: ");
                messageBuilder.Append(result.Warnings.Count);
                messageBuilder.Append('.');
            }

            if (result.Errors.Count > 0)
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine();
                messageBuilder.Append("First error: ");
                messageBuilder.Append(result.Errors[0]);
            }

            return messageBuilder.ToString();
        }
    }
}
#endif
