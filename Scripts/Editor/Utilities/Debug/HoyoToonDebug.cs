#if UNITY_EDITOR
using HoyoToon.Editor.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Debugging
{
    internal static class HoyoToonDebug
    {
        private const string ToggleLoggingMenuPath = "HoyoToon/Debug/Toggle Editor Debug Logging";

        internal static bool Enabled
        {
            get => ReadEnabled();
            set => WriteEnabled(value);
        }

        internal static bool ShouldLog(HoyoToonLogLevel level)
        {
            return LogCore.ShouldLog(Enabled, level);
        }

        [MenuItem(ToggleLoggingMenuPath)]
        private static void ToggleLogging()
        {
            Enabled = !Enabled;
            Debug.Log(LogCore.FormatMessage(
                HoyoToonLogCategory.General,
                $"Editor debug logging {(Enabled ? "enabled" : "disabled")}.",
                false));
        }

        [MenuItem(ToggleLoggingMenuPath, true)]
        private static bool ValidateToggleLogging()
        {
            Menu.SetChecked(ToggleLoggingMenuPath, Enabled);
            return true;
        }

        private static bool ReadEnabled()
        {
            string scopedDebugKey = ScopedKey(LogCore.DebugEnabledEditorPrefsKey);
            if (EditorPrefs.HasKey(scopedDebugKey))
            {
                return EditorPrefs.GetBool(scopedDebugKey, false);
            }

            string scopedLegacyKey = ScopedKey(LogCore.LegacyDebugModeEditorPrefsKey);
            if (EditorPrefs.HasKey(scopedLegacyKey))
            {
                return EditorPrefs.GetInt(scopedLegacyKey, 0) > 0;
            }

            return false;
        }

        private static void WriteEnabled(bool value)
        {
            EditorPrefs.SetBool(ScopedKey(LogCore.DebugEnabledEditorPrefsKey), value);

            string scopedLegacyKey = ScopedKey(LogCore.LegacyDebugModeEditorPrefsKey);
            if (EditorPrefs.HasKey(scopedLegacyKey))
            {
                EditorPrefs.DeleteKey(scopedLegacyKey);
            }

            if (EditorPrefs.HasKey(LogCore.DebugEnabledEditorPrefsKey))
            {
                EditorPrefs.DeleteKey(LogCore.DebugEnabledEditorPrefsKey);
            }

            if (EditorPrefs.HasKey(LogCore.LegacyDebugModeEditorPrefsKey))
            {
                EditorPrefs.DeleteKey(LogCore.LegacyDebugModeEditorPrefsKey);
            }
        }

        private static string ScopedKey(string key)
        {
            return HoyoToonEditorPrefs.ProjectKey(key);
        }
    }
}
#endif
