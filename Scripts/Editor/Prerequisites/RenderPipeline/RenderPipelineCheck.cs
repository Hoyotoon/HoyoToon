#if UNITY_EDITOR
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Prerequisites
{
    public sealed class RenderPipelineCheck : IPrerequisiteCheck
    {
        public string Name => "Render Pipeline";

        public static HoyoToonPipeline ActivePipeline => PipelineDetector.ActivePipeline;

        public static bool IsCustomRP => PipelineDetector.IsCustomRP;

        public static bool IsBuiltIn => PipelineDetector.IsBuiltIn;

        public static HoyoToonPipeline DetectedPipeline => PipelineDetector.DetectedPipeline;

        public static string ActiveAssetName => PipelineDetector.ActiveAssetName;

        public static string FriendlyPipelineName => PipelineDetector.FriendlyPipelineName;

        public static string DetectionReason => PipelineDetector.DetectionReason;

        public static void InvalidateCache() => PipelineDetector.InvalidateCache();

        public static void PromptForStartupSelectionIfNeeded()
        {
            PipelineStartupPrompt.PromptForStartupSelectionIfNeeded();
        }

        public static void PromptForStartupSelectionForced()
        {
            PipelineStartupPrompt.PromptForStartupSelectionForced();
        }

        public static bool SetGraphicsPipeline(HoyoToonPipeline pipeline) => PipelineSwitcher.SetGraphicsPipeline(pipeline);

        public static bool SetGraphicsPipeline() => PipelineSwitcher.SetGraphicsPipeline();

        public PrerequisiteResult Evaluate()
        {
            PipelineDetector.InvalidateCache();
            var msg = $"{PipelineDetector.FriendlyPipelineName}. {PipelineDetector.DetectionReason}";
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, msg);

            if (PipelineDetector.ActivePipeline == HoyoToonPipeline.HoyoToonURP)
            {
                return PrerequisiteResult.Ok(msg);
            }

            return PrerequisiteResult.Fail(
                PrerequisiteSeverity.Warning,
                $"HoyoToon requires the packaged URP asset to be assigned in Graphics and Quality settings. {PipelineDetector.DetectionReason}");
        }

        public bool TryFix() => PipelineSwitcher.SetGraphicsPipeline();
    }
}
#endif
