#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.Prerequisites
{
    internal sealed class ColorSpaceLinearCheck : IPrerequisiteCheck
    {
        public string Name => "Color Space must be Linear";

        public PrerequisiteResult Evaluate()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Linear)
            {
                HoyoToonLogger.ManagerInfo("Color Space already Linear.");
                return PrerequisiteResult.Ok("Color Space is Linear.");
            }
            return PrerequisiteResult.Fail(PrerequisiteSeverity.Error,
                "The Color Space is currently set to Gamma. To ensure proper rendering, it should be Linear.");
        }

        public bool TryFix()
        {
            try
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                HoyoToonLogger.ManagerInfo("Set Color Space to Linear.");
                return true;
            }
            catch (System.Exception ex)
            {
                HoyoToonLogger.Always("Manager", $"Failed to set Color Space to Linear: {ex}", LogType.Exception);
                return false;
            }
        }
    }
}
#endif
