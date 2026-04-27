#if UNITY_EDITOR
using System.Collections.Generic;
using HoyoToon.Editor.Prerequisites.ColorSpace;
using HoyoToon.Editor.Prerequisites.InputSystem;
using HoyoToon.Editor.Prerequisites.RenderPipeline;
using HoyoToon.Editor.Prerequisites.Shadows;
using HoyoToon.Editor.Prerequisites.Tags;
using HoyoToon.Editor.Prerequisites.Layers;

namespace HoyoToon.Editor.Prerequisites
{
    internal static class PrerequisiteService
    {
        private static readonly IPrerequisiteCheck[] Checks =
        {
            new RenderPipelineCheck(),
            new ColorSpaceLinearCheck(),
            new InputSystemBackendCheck(),
            new ShadowProjectionCloseFitCheck(),
            ProjectTagPrerequisiteChecks.Require(
                "Honkai Star Rail Hair",
                "HSR runtime and rendering"),
            ProjectLayerPrerequisiteChecks.Require(
                "Honkai Star Rail",
                "HSR runtime and rendering"),
            ProjectLayerPrerequisiteChecks.Require(
                "Genshin Impact",
                "GI runtime and rendering"),
            ProjectLayerPrerequisiteChecks.Require(
                "Honkai Impact 3rd",
                "HI3 runtime and rendering"),
            ProjectLayerPrerequisiteChecks.Require(
                "Zenless Zone Zero",
                "ZZZ runtime and rendering"),
        };

        public static PrerequisiteReport LastReport { get; private set; }

        public static PrerequisiteReport Evaluate(PrerequisiteFixPolicy fixPolicy = PrerequisiteFixPolicy.None)
        {
            var results = new List<PrerequisiteCheckResult>(Checks.Length);
            int failedCount = 0;
            int errorCount = 0;
            int warningCount = 0;
            int blockingCount = 0;
            bool fixesAttempted = false;
            bool fixesApplied = false;

            foreach (IPrerequisiteCheck check in Checks)
            {
                PrerequisiteEvaluation initialEvaluation = check.Evaluate();
                PrerequisiteEvaluation finalEvaluation = initialEvaluation;
                PrerequisiteFixResult fixResult = PrerequisiteFixResult.None();

                if (!initialEvaluation.Passed
                    && initialEvaluation.CanAutoFix
                    && fixPolicy == PrerequisiteFixPolicy.SafeOnly)
                {
                    fixResult = check.TryApplySafeFix();
                    fixesAttempted |= fixResult.Attempted;
                    fixesApplied |= fixResult.Applied;

                    if (fixResult.Attempted)
                    {
                        finalEvaluation = check.Evaluate();
                    }
                }

                if (!finalEvaluation.Passed)
                {
                    failedCount++;

                    if (finalEvaluation.IsBlocking)
                    {
                        blockingCount++;
                    }

                    switch (finalEvaluation.Severity)
                    {
                        case PrerequisiteSeverity.Error:
                            errorCount++;
                            break;

                        case PrerequisiteSeverity.Warning:
                            warningCount++;
                            break;
                    }
                }

                results.Add(new PrerequisiteCheckResult(
                    initialEvaluation,
                    finalEvaluation,
                    fixResult.Attempted,
                    fixResult.Applied,
                    fixResult.Message));
            }

            LastReport = new PrerequisiteReport(
                results,
                Checks.Length,
                failedCount,
                errorCount,
                warningCount,
                blockingCount,
                fixesAttempted,
                fixesApplied);

            return LastReport;
        }
    }
}
#endif
