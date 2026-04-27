#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Prerequisites.ColorSpace
{
    internal sealed class ColorSpaceLinearCheck : IPrerequisiteCheck
    {
        private const string CheckId = "color-space-linear";
        private const string DisplayNameValue = "Color Space";

        public string Id => CheckId;

        public string DisplayName => DisplayNameValue;

        public PrerequisiteEvaluation Evaluate()
        {
            if (PlayerSettings.colorSpace == UnityEngine.ColorSpace.Linear)
            {
                return PrerequisiteEvaluation.Pass(
                    Id,
                    DisplayName,
                    "Project color space is already set to Linear.");
            }

            return PrerequisiteEvaluation.Error(
                Id,
                DisplayName,
                "Project color space is set to Gamma, but HoyoToon shaders expect Linear color space.",
                canAutoFix: true,
                actionHint: "Change Player Settings > Color Space to Linear or use the safe-fix command.");
        }

        public PrerequisiteFixResult TryApplySafeFix()
        {
            try
            {
                PlayerSettings.colorSpace = UnityEngine.ColorSpace.Linear;
                return PrerequisiteFixResult.AppliedFix("Changed Player Settings > Color Space to Linear.");
            }
            catch (System.Exception exception)
            {
                return PrerequisiteFixResult.Failed($"Failed to update Color Space: {exception.Message}");
            }
        }
    }
}
#endif