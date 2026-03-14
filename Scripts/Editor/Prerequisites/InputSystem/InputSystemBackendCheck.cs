#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Prerequisites
{
    public sealed class InputSystemBackendCheck : IPrerequisiteCheck
    {
        // activeInputHandler values: 0 = Old, 1 = New, 2 = Both
        private const int InputHandlerBoth = 2;

        public string Name => "Input System backend must be enabled";

        public PrerequisiteResult Evaluate()
        {
#if ENABLE_INPUT_SYSTEM
            return PrerequisiteResult.Ok("New Input System backend is active.");
#else
            var activeInputHandler = GetActiveInputHandlerValue();
            if (activeInputHandler.HasValue && (activeInputHandler.Value == 1 || activeInputHandler.Value == InputHandlerBoth))
            {
                return PrerequisiteResult.Fail(PrerequisiteSeverity.Warning,
                    "Active Input Handling is set correctly but Unity needs a restart for it to take effect.");
            }

            return PrerequisiteResult.Fail(PrerequisiteSeverity.Error,
                "Active Input Handling is set to 'Input Manager (Old)'. " +
                "HoyoToon requires the New Input System. A restart is needed after changing this setting.");
#endif
        }

        public bool TryFix()
        {
#if ENABLE_INPUT_SYSTEM
            return true;
#else
            var activeInputHandler = GetActiveInputHandlerValue();
            if (!activeInputHandler.HasValue)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, "Could not locate activeInputHandler in PlayerSettings.");
                return false;
            }

            if (activeInputHandler.Value != 1 && activeInputHandler.Value != InputHandlerBoth)
            {
                if (!TrySetActiveInputHandler(InputHandlerBoth))
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, "Could not set activeInputHandler in PlayerSettings.");
                    return false;
                }

                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Set Active Input Handling to 'Both'.");
            }

            // Setting was applied but a restart is required for the define to appear.
            if (EditorUtility.DisplayDialog(
                    "Restart Required",
                    "Unity must restart for the Input System backend to become active.",
                    "Restart Now",
                    "Later"))
            {
                EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
            }

            return false; // Still needs restart, the define won't exist until then.
#endif
        }

        private static int? GetActiveInputHandlerValue()
        {
            var playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if (playerSettings == null)
            {
                return null;
            }

            using (var so = new SerializedObject(playerSettings))
            {
                var prop = so.FindProperty("activeInputHandler");
                return prop != null ? (int?)prop.intValue : null;
            }
        }

        private static bool TrySetActiveInputHandler(int value)
        {
            var playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if (playerSettings == null)
            {
                return false;
            }

            using (var so = new SerializedObject(playerSettings))
            {
                var prop = so.FindProperty("activeInputHandler");
                if (prop == null)
                {
                    return false;
                }

                prop.intValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }
        }
    }
}
#endif
