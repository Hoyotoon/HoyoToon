#if UNITY_EDITOR
using UnityEngine;

namespace HoyoToon.Editor.Prerequisites.Shadows
{
    internal sealed class ShadowProjectionCloseFitCheck : IPrerequisiteCheck
    {
        private const string CheckId = "shadow-projection-close-fit";
        private const string DisplayNameValue = "Shadow Projection";

        public string Id => CheckId;

        public string DisplayName => DisplayNameValue;

        public PrerequisiteEvaluation Evaluate()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                return PrerequisiteEvaluation.Pass(
                    Id,
                    DisplayName,
                    "Shadow Projection enforcement is skipped outside the Windows editor.");
            }

            if (QualitySettings.shadowProjection == ShadowProjection.CloseFit)
            {
                return PrerequisiteEvaluation.Pass(
                    Id,
                    DisplayName,
                    "Editor shadow projection is already set to Close Fit.");
            }

            return PrerequisiteEvaluation.Warning(
                Id,
                DisplayName,
                "Editor shadow projection is set to Stable Fit. Close Fit is recommended for better cast shadow behavior while authoring.",
                canAutoFix: true,
                actionHint: "Change Project Settings > Quality > Shadow Projection to Close Fit or use the safe-fix command.");
        }

        public PrerequisiteFixResult TryApplySafeFix()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                return PrerequisiteFixResult.None("Shadow Projection is only adjusted in the Windows editor.");
            }

            try
            {
                QualitySettings.shadowProjection = ShadowProjection.CloseFit;
                return PrerequisiteFixResult.AppliedFix("Changed Project Settings > Quality > Shadow Projection to Close Fit.");
            }
            catch (System.Exception exception)
            {
                return PrerequisiteFixResult.Failed($"Failed to update Shadow Projection: {exception.Message}");
            }
        }
    }
}
#endif