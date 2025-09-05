#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    internal static class EnvironmentDebugMenu
    {
        private const string MenuDumpJson = "HoyoToon/Debug/Environment/Dump All As JSON";
        private const string MenuShowPretty = "HoyoToon/Debug/Environment/Show Snapshot";

        [MenuItem(MenuDumpJson, false, 520)]
        private static void DumpAllAsJson()
        {
            var json = HoyoToonEnvironment.DumpAllAsJson();
            if (string.IsNullOrEmpty(json))
            {
                HoyoToonLogger.Warning("DumpAllAsJson returned empty output.");
                return;
            }
            EditorGUIUtility.systemCopyBuffer = json;
            HoyoToonDialogWindow.ShowOk("Environment Dump", "Full environment JSON copied to clipboard.");
        }

        [MenuItem(MenuShowPretty, false, 521)]
        private static void ShowSnapshot()
        {
            var snap = HoyoToonEnvironment.Capture();
            var pretty = HoyoToonEnvironment.ToPrettyString(snap);
            HoyoToonDialogWindow.ShowOk("Environment Snapshot", pretty, MessageType.Info);
        }
    }
}
#endif
