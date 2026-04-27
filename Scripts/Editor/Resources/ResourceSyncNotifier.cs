#if UNITY_EDITOR
using HoyoToon.Editor.Utilities.Debugging;

namespace HoyoToon.Editor.Resources
{
    internal sealed class ResourceSyncNotifier
    {
        private const string ProgressTitle = "HoyoToon Resources";

        private readonly ResourceSyncTarget target;
        private readonly bool showProgress;

        internal ResourceSyncNotifier(ResourceSyncTarget target, bool showProgress = true)
        {
            this.target = target;
            this.showProgress = showProgress;
        }

        internal void ShowPhase(string message, float progress)
        {
            string displayMessage = string.IsNullOrWhiteSpace(message)
                ? target?.OperationLabel ?? "Running resource sync..."
                : message;

            if (showProgress)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.DisplayProgressBar(ProgressTitle, displayMessage, progress);
            }

            HoyoToonLogger.Verbose(HoyoToonLogCategory.Resources, displayMessage, context: target?.ResourceAsset);
        }

        internal static void LogBatchSummary(string message, bool hasIssues)
        {
            if (hasIssues)
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.Resources, message);
                return;
            }

            HoyoToonLogger.Info(HoyoToonLogCategory.Resources, message);
        }

        internal static void ShowBatchSummary(string message, bool hasIssues, bool clearProgress = true)
        {
            LogBatchSummary(message, hasIssues);
            if (clearProgress)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
            }

            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(ProgressTitle, message, "OK");
        }

        internal static bool PromptToSyncChanged(string message, bool clearProgress = true)
        {
            LogBatchSummary(message, hasIssues: false);
            string prompt = string.Join("\n\n", message, "Sync the resource sets with detected changes now?");
            if (clearProgress)
            {
                HoyoToon.Editor.UI.Dialogs.HoyoToonProgress.ClearProgressBar();
            }

            return HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(ProgressTitle, prompt, "Sync Changed", "Close");
        }

        internal static void ShowInfo(string message)
        {
            HoyoToonLogger.Info(HoyoToonLogCategory.Resources, message);
            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(ProgressTitle, message, "OK");
        }

        internal static void ShowError(string message)
        {
            HoyoToonLogger.Error(HoyoToonLogCategory.Resources, message);
            HoyoToon.Editor.UI.Dialogs.HoyoToonDialog.DisplayDialog(ProgressTitle, message, "OK");
        }
    }
}
#endif
