#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ManagerInspector.Setup
{
    internal static class SetupLoggingHelper
    {
        public static void LogContextSummary(ModelSetupUtility.SetupContext context)
        {
            if (context == null)
            {
                return;
            }

            var builder = new StringBuilder(384);
            builder.AppendLine("Auto Setup: Context summary");
            builder.AppendLine($"- Asset: {context.Asset?.name ?? "<none>"}");
            builder.AppendLine($"- AssetPath: {context.AssetPath ?? "<none>"}");
            builder.AppendLine($"- Game: {context.DetectedGameKey ?? "<unknown>"}");
            builder.AppendLine($"- Shader: {context.DetectedShaderPath ?? "<unknown>"}");
            builder.AppendLine($"- Source JSON: {context.DetectedSourceJson ?? "<none>"}");
            builder.AppendLine($"- Material JSON count: {(context.MaterialSources?.Count ?? 0)}");
            builder.AppendLine($"- Game Metadata: {(context.DetectedGameMetadata != null ? "Loaded" : "<none>")}");
            builder.AppendLine($"- VRC SDK Installed: {context.IsVrcSdkInstalled} ({context.VrcSdkKind})");
            builder.AppendLine($"- Render Pipeline: {context.ActivePipeline} (Custom: {context.IsCustomPipeline})");
            builder.Append($"- Existing Scene Instance: {(context.ExistingInstance != null ? context.ExistingInstance.name : "<none>")}");
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, builder.ToString());
        }

        public static void LogStepDecisions(ModelSetupUtility.SetupContext context, List<ModelSetupUtility.StepDecision> decisions)
        {
            if (decisions == null || decisions.Count == 0)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: No steps registered.");
                return;
            }

            var builder = new StringBuilder(256 + (decisions.Count * 96));
            builder.AppendLine($"Auto Setup: Evaluating {decisions.Count} step(s)...");
            for (int i = 0; i < decisions.Count; i++)
            {
                var decision = decisions[i];
                var step = decision.Step;
                if (step == null)
                {
                    continue;
                }

                builder.AppendLine($"- Step '{step.Id}': Enabled={step.Enabled}, Applicable={decision.Applicable}, Required={decision.Required}");
            }

            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, builder.ToString().TrimEnd('\r', '\n'));
        }
    }
}
#endif
