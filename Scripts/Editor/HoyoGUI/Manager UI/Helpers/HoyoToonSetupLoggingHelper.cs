#if UNITY_EDITOR
using System.Collections.Generic;
using HoyoToon.Utilities;

namespace HoyoToon.EditorTools.ManagerUI
{
    internal static class HoyoToonSetupLoggingHelper
    {
        public static void LogContextSummary(HoyoToonModelSetupUtility.SetupContext context)
        {
            if (context == null)
            {
                return;
            }

            HoyoToonLogger.ManagerInfo("Auto Setup: Context summary");
            HoyoToonLogger.ManagerInfo($"- Asset: {context.Asset?.name ?? "<none>"}");
            HoyoToonLogger.ManagerInfo($"- AssetPath: {context.AssetPath ?? "<none>"}");
            HoyoToonLogger.ManagerInfo($"- Game: {context.DetectedGameKey ?? "<unknown>"}");
            HoyoToonLogger.ManagerInfo($"- Shader: {context.DetectedShaderPath ?? "<unknown>"}");
            HoyoToonLogger.ManagerInfo($"- Source JSON: {context.DetectedSourceJson ?? "<none>"}");
            HoyoToonLogger.ManagerInfo($"- Material JSON count: {(context.MaterialSources?.Count ?? 0)}");
            HoyoToonLogger.ManagerInfo($"- VRC SDK Installed: {context.IsVrcSdkInstalled} ({context.VrcSdkKind})");
            HoyoToonLogger.ManagerInfo($"- Existing Scene Instance: {(context.ExistingInstance != null ? context.ExistingInstance.name : "<none>")}");
        }

        public static void LogStepDecisions(HoyoToonModelSetupUtility.SetupContext context, List<HoyoToonModelSetupUtility.StepDecision> decisions)
        {
            if (decisions == null || decisions.Count == 0)
            {
                HoyoToonLogger.ManagerInfo("Auto Setup: No steps registered.");
                return;
            }

            HoyoToonLogger.ManagerInfo($"Auto Setup: Evaluating {decisions.Count} step(s)...");
            foreach (var decision in decisions)
            {
                var step = decision.Step;
                if (step == null)
                {
                    continue;
                }

                HoyoToonLogger.ManagerInfo($"- Step '{step.Id}': Enabled={step.Enabled}, Applicable={decision.Applicable}, Required={decision.Required}");
            }
        }
    }
}
#endif
