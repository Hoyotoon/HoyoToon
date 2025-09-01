#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon
{

    [InitializeOnLoad]
    public static class HoyoToonPrerequisites
    {
        private static bool prerequisitesChecked = false;

        static HoyoToonPrerequisites()
        {
            EditorApplication.update += PrerequisitesValidator;
        }

        private static void PrerequisitesValidator()
        {
            if (!prerequisitesChecked)
            {
                if (PlayerSettings.colorSpace == ColorSpace.Linear)
                {
                    HoyoToonLogs.LogDebug("The Color Space is set to Linear.");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "The Color Space is currently set to Gamma. To ensure proper rendering, we'll set the Color Space to Linear. Unity will then initiate reloading its textures and lighting options.", "OK");
                    HoyoToonLogs.ErrorDebug("The Color Space is currently set to Gamma. To ensure proper rendering, we'll set the Color Space to Linear. Unity will then initiate reloading its textures and lighting options.");

                    PlayerSettings.colorSpace = ColorSpace.Linear;
                    EditorUtility.DisplayDialog("Settings Updated", "The Color Space has been set to Linear.", "OK");
                    HoyoToonLogs.LogDebug("The Color Space has been set to Linear.");
                }
                if (QualitySettings.shadowProjection == ShadowProjection.CloseFit)
                {
                    HoyoToonLogs.LogDebug("The Shadow Projection is set to Close Fit.");
                }
                else
                {
                    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows)
                    {
                        HoyoToonLogs.WarningDebug("Different Build Target detected. Skipping Shadow Projection check.");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", "The Shadow Projection is currently set to Stable Fit. To ensure high quality casted shadows inside the Unity Editor, we'll set the Shadow Projection to Close Fit.", "OK");
                        HoyoToonLogs.LogDebug("The Shadow Projection is currently set to Stable Fit. To ensure high quality casted shadows inside the Unity Editor, we'll set the Shadow Projection to Close Fit.");

                        QualitySettings.shadowProjection = ShadowProjection.CloseFit;
                        EditorUtility.DisplayDialog("Settings Updated", "The Shadow Projection has been set to Close Fit.", "OK");
                        HoyoToonLogs.LogDebug("The Shadow Projection has been set to Close Fit.");
                    }
                }

                prerequisitesChecked = true;
            }
        }
    }
}
#endif