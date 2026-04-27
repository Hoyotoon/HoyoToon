#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Prerequisites.RenderPipeline
{
    internal static class RenderPipelineStartupPrompt
    {
        public static bool PromptForSelectionIfNeeded(PrerequisiteReport report)
        {
            if (Application.isBatchMode || report == null || !TryGetPipelineResult(report, out PrerequisiteCheckResult pipelineResult))
            {
                return false;
            }

            if (pipelineResult.FinalEvaluation.Passed)
            {
                return false;
            }

            string message = BuildMessage();
            if (!HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(
                "HoyoToon Render Pipeline",
                message,
                "Set Up HoyoToon URP",
                "Not Now"))
            {
                return false;
            }

            return TryApplySelection(RenderPipelineSelection.Universal);
        }

        private static bool TryApplySelection(RenderPipelineSelection selection)
        {
            if (RenderPipelineSwitcher.ApplySelection(selection, out string message))
            {
                return true;
            }

            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog("HoyoToon Render Pipeline", message, "OK");
            return false;
        }

        private static bool TryGetPipelineResult(PrerequisiteReport report, out PrerequisiteCheckResult pipelineResult)
        {
            foreach (PrerequisiteCheckResult result in report.Results)
            {
                if (string.Equals(result.Id, "render-pipeline", System.StringComparison.Ordinal))
                {
                    pipelineResult = result;
                    return true;
                }
            }

            pipelineResult = default;
            return false;
        }

        private static string BuildMessage()
        {
            return "HoyoToon needs to use its included Universal Render Pipeline (URP) asset so shaders, lighting, and render features work correctly.\n\n"
                + "Set up HoyoToon URP to assign that asset automatically. Choosing 'Not Now' keeps the current render pipeline unchanged, but HoyoToon may not render correctly until this is set up.";
        }
    }
}
#endif
