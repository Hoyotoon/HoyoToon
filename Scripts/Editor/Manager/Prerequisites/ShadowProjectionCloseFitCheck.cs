#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.Prerequisites
{
    public sealed class ShadowProjectionCloseFitCheck : IPrerequisiteCheck
    {
        public string Name => "Shadow Projection Close Fit (Windows Editor)";

        public PrerequisiteResult Evaluate()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                HoyoToonLogger.ManagerInfo("Non-Windows editor; skipping Shadow Projection prerequisite.");
                return PrerequisiteResult.Ok("Skipped for non-Windows editor platform.");
            }

            if (VRCSDKInstalledCheck.HasEnvConfig)
            {
                HoyoToonLogger.ManagerInfo($"{VRCSDKInstalledCheck.FriendlySdkName} EnvConfig detected; leaving Shadow Projection to SDK-managed settings.");
                return PrerequisiteResult.Ok($"Managed by {VRCSDKInstalledCheck.FriendlySdkName} (EnvConfig). Skipping enforcement.");
            }

            if (QualitySettings.shadowProjection == ShadowProjection.CloseFit)
            {
                HoyoToonLogger.ManagerInfo("Shadow Projection already Close Fit.");
                return PrerequisiteResult.Ok("Shadow Projection is Close Fit.");
            }

            return PrerequisiteResult.Fail(PrerequisiteSeverity.Warning,
                "Shadow Projection is Stable Fit. For better casted shadows in the Unity Editor on Windows, Close Fit is recommended.");
        }

        public bool TryFix()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                return true; // nothing to do when not on Windows Editor

            if (VRCSDKInstalledCheck.HasEnvConfig)
            {
                HoyoToonLogger.ManagerInfo("Skipping Shadow Projection auto-fix because VRChat EnvConfig manages quality settings.");
                return false; // cannot fix because external manager will override
            }
            try
            {
                QualitySettings.shadowProjection = ShadowProjection.CloseFit;
                HoyoToonLogger.ManagerInfo("Set Shadow Projection to Close Fit.");
                return true;
            }
            catch (System.Exception ex)
            {
                HoyoToonLogger.Always("Manager", $"Failed to set Shadow Projection to Close Fit: {ex}", LogType.Exception);
                return false;
            }
        }

        // EnvConfig detection is centralized in VRCSDKInstalledCheck.
    }
}
#endif
