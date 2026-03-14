#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.Prerequisites
{
    internal static class PipelineStartupPrompt
    {
        private const string StartupPromptSessionKey = "HoyoToon.RenderPipeline.StartupPromptShown";

        public static void PromptForStartupSelectionIfNeeded()
        {
            PromptForStartupSelectionInternal(false);
        }

        public static void PromptForStartupSelectionForced()
        {
            PromptForStartupSelectionInternal(true);
        }

        private static void PromptForStartupSelectionInternal(bool forceShow)
        {
            if (Application.isBatchMode)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, "Batch mode active; skipping render pipeline notification.");
                return;
            }

            if (!forceShow && SessionState.GetBool(StartupPromptSessionKey, false))
            {
                return;
            }

            PipelineDetector.InvalidateCache();
            var detected = PipelineDetector.DetectedPipeline;
            EditorPrefs.SetInt(PrefsKeys.RenderPipelineLast, (int)detected);

            if (!forceShow && detected == HoyoToonPipeline.HoyoToonURP)
            {
                return;
            }

            SessionState.SetBool(StartupPromptSessionKey, true);

            if (forceShow && detected == HoyoToonPipeline.HoyoToonURP)
            {
                DialogWindow.ShowOk(
                    "HoyoToon Render Pipeline",
                    $"The packaged HoyoToon URP asset is already assigned correctly.\n\nRequired asset: **{PipelineSwitcher.RequiredAssetPath}**\nCurrent state: **{PipelineDetector.FriendlyPipelineName}**\n\n{PipelineDetector.DetectionReason}",
                    MessageType.Info);
                return;
            }

            if (PipelineSwitcher.TryLoadRequiredPipelineAsset(out _, out var requiredPath))
            {
                DialogWindow.ShowOk(
                    "HoyoToon Render Pipeline",
                    $"HoyoToon expects the packaged URP asset to be assigned in Project Settings > Graphics and every Quality level.\n\nRequired asset: **{requiredPath}**\nCurrent state: **{PipelineDetector.FriendlyPipelineName}**\n\n{PipelineDetector.DetectionReason}",
                    MessageType.Warning);
                return;
            }

            DialogWindow.ShowOk(
                "HoyoToon Render Pipeline",
                $"The required HoyoToon URP asset could not be found at **{PipelineSwitcher.RequiredAssetPath}**. Reimport the package or restore the asset before using HoyoToon shaders.",
                MessageType.Warning);
        }
    }
}
#endif
