#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    internal static class EnvironmentDebugMenu
    {
        private const string MenuShowPretty = "HoyoToon/Debug/Environment/Show Snapshot";

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
