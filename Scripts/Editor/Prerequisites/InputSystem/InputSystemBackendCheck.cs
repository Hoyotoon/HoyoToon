#if UNITY_EDITOR
namespace HoyoToon.Editor.Prerequisites.InputSystem
{
    internal sealed class InputSystemBackendCheck : IPrerequisiteCheck
    {
        private const string CheckId = "input-system-backend";
        private const string DisplayNameValue = "Input System Backend";

        public string Id => CheckId;

        public string DisplayName => DisplayNameValue;

        public PrerequisiteEvaluation Evaluate()
        {
#if ENABLE_INPUT_SYSTEM
            return PrerequisiteEvaluation.Pass(
                Id,
                DisplayName,
                "The Unity Input System backend is active.");
#else
            InputSystemBackendMode configuredMode = InputSystemBackendUtility.GetConfiguredBackendMode();
            switch (configuredMode)
            {
                case InputSystemBackendMode.NewInputSystem:
                case InputSystemBackendMode.Both:
                    return PrerequisiteEvaluation.Warning(
                        Id,
                        DisplayName,
                        "Active Input Handling is already configured to enable the Input System, but Unity has not restarted yet.",
                        isBlocking: true,
                        requiresRestart: true,
                        actionHint: "Restart the Unity editor to finish activating the Input System backend.");

                case InputSystemBackendMode.OldInputManager:
                    return PrerequisiteEvaluation.Error(
                        Id,
                        DisplayName,
                        "Active Input Handling is set to the old Input Manager only, but HoyoToon requires the Unity Input System.",
                        canAutoFix: true,
                        requiresRestart: true,
                        actionHint: "Set Active Input Handling to Both or Input System Package (New) in Player Settings, then restart the editor.");

                default:
                    return PrerequisiteEvaluation.Error(
                        Id,
                        DisplayName,
                        "The current Active Input Handling setting could not be resolved while the Input System define is inactive.",
                        actionHint: "Inspect Player Settings > Active Input Handling, enable the Input System backend, and restart the editor.");
            }
#endif
        }

        public PrerequisiteFixResult TryApplySafeFix()
        {
            InputSystemBackendMode configuredMode = InputSystemBackendUtility.GetConfiguredBackendMode();
            if (configuredMode == InputSystemBackendMode.Both || configuredMode == InputSystemBackendMode.NewInputSystem)
            {
                return PrerequisiteFixResult.None("Active Input Handling already enables the Input System. Restart Unity to finish activation.");
            }

            if (configuredMode != InputSystemBackendMode.OldInputManager)
            {
                return PrerequisiteFixResult.Failed("Active Input Handling could not be resolved, so the Input System setting was left unchanged.");
            }

            if (!InputSystemBackendUtility.TrySetConfiguredBackendMode(InputSystemBackendMode.Both))
            {
                return PrerequisiteFixResult.Failed("Failed to change Active Input Handling to Both.");
            }

            return PrerequisiteFixResult.AppliedFix("Changed Active Input Handling to Both. Restart Unity to finish enabling the Input System backend.");
        }
    }
}
#endif