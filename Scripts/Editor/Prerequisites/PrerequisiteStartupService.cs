#if UNITY_EDITOR
using HoyoToon.Editor.Utilities.Editor;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Editor.Prerequisites.RenderPipeline;
using UnityEditor;

namespace HoyoToon.Editor.Prerequisites
{
    [InitializeOnLoad]
    internal static class PrerequisiteStartupService
    {
        private static bool hasRun;

        static PrerequisiteStartupService()
        {
            EditorApplication.delayCall += TryRunOnce;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            TryRunOnce();
        }

        private static void TryRunOnce()
        {
            if (hasRun)
            {
                Unsubscribe();
                return;
            }

            if (!EditorReadinessUtility.IsReadyForEditorWork())
            {
                return;
            }

            hasRun = true;
            Unsubscribe();

            if (OnboardingPersistence.ShouldStartOnboarding)
            {
                OnboardingManager.StartFirstTimeIfNeeded();
                return;
            }

            PrerequisiteReport report = PrerequisiteService.Evaluate(PrerequisiteFixPolicy.SafeOnly);
            bool pipelineSelectionApplied = RenderPipelineStartupPrompt.PromptForSelectionIfNeeded(report);
            if (pipelineSelectionApplied)
            {
                report = PrerequisiteService.Evaluate(PrerequisiteFixPolicy.SafeOnly);
            }

            PrerequisiteReportLogger.LogReport(report, isStartup: true, fixPolicy: PrerequisiteFixPolicy.SafeOnly);
        }

        private static void Unsubscribe()
        {
            EditorApplication.delayCall -= TryRunOnce;
            EditorApplication.update -= OnEditorUpdate;
        }
    }
}
#endif
