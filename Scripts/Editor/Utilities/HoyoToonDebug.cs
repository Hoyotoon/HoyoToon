#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Centralized editor-only debug toggle for HoyoToon.
    /// Owns persistence, menu integration, and change event.
    /// </summary>
    public static class HoyoToonDebug
    {
        private const string DebugEnabledKey = "HoyoToon_DebugEnabled";
        private const string MenuPath = "HoyoToon/Settings/Debug Mode";

        public static event Action<bool> OnChanged;

        static HoyoToonDebug()
        {
            Enabled = EditorPrefs.GetBool(DebugEnabledKey, false);
        }

        public static bool Enabled { get; private set; }

        public static void SetEnabled(bool enabled)
        {
            if (Enabled == enabled) return;
            Enabled = enabled;
            EditorPrefs.SetBool(DebugEnabledKey, Enabled);
            try { OnChanged?.Invoke(Enabled); }
            catch (Exception ex) { HoyoToonLogCore.LogAlways($"HoyoToonDebug OnChanged exception: {ex}", LogType.Exception); }
        }

        [MenuItem(MenuPath, false, 98)]
        private static void ToggleMenu()
        {
            bool newState = !Enabled;
            SetEnabled(newState);

            if (!Application.isBatchMode)
            {
                HoyoToonDialogWindow.ShowInfo("Debug Status", $"Debug mode has been {(newState ? "enabled" : "disabled")}.");
                HoyoToonLogger.Info($"Debug mode {(newState ? "enabled" : "disabled")}.");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleMenuValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Menu.SetChecked(MenuPath, Enabled);
        }
    }
}
#endif
