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

            if (IsVrcEnvConfigPresent())
            {
                HoyoToonLogger.ManagerInfo("VRChat EnvConfig detected; leaving Shadow Projection to SDK-managed settings.");
                return PrerequisiteResult.Ok("Managed by VRChat SDK (EnvConfig). Skipping enforcement.");
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

            if (IsVrcEnvConfigPresent())
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

        private static bool IsVrcEnvConfigPresent()
        {
            // Try fast type lookup first
            var t = Type.GetType("VRC.Editor.EnvConfig, Assembly-CSharp-Editor");
            if (t != null) return true;
            t = Type.GetType("VRC.Editor.EnvConfig");
            if (t != null) return true;

            // Fallback: scan loaded assemblies for the type to avoid hardcoding assembly names
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.GetType("VRC.Editor.EnvConfig", throwOnError: false) != null)
                            return true;
                    }
                    catch (Exception)
                    {
                        // Ignore reflection/type-load errors while scanning assemblies.
                    }
                }
            }
            catch (Exception)
            {
                // Ignore top-level assembly enumeration errors; treat as not present.
            }
            return false;
        }
    }
}
#endif
