#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    internal static class HoyoToonLoggerTest
    {
        private const string MenuPath = "HoyoToon/Debug/Test Logger";
        private const string MenuPathDialogImage = "HoyoToon/Debug/Test Dialog With Image";

        [MenuItem(MenuPath, false, 501)]
        private static void RunAllLoggerTypes()
        {
            bool original = HoyoToonDebug.Enabled;
            int emitted = 0;

            // Ensure we can see gated logs
            HoyoToonDebug.SetEnabled(true);

            // Count via event to provide a summary at the end
            void OnLog(string m, LogType t) { emitted++; }
            HoyoToonLogger.OnLog += OnLog;

            // Context object
            GameObject ctx = new GameObject("HoyoToonLoggerTestContext");
            try
            {
                // Demonstrate category color customization. Optional.
                HoyoToonLogCore.SetCategoryColor("Shader", "#7EC8E3");
                HoyoToonLogCore.SetCategoryColor("UI", Color.yellow);

                // Reflect over HoyoToonLogger to find all public static void methods
                var methods = typeof(HoyoToonLogger)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.ReturnType == typeof(void))
                    .ToArray();

                foreach (var m in methods)
                {
                    var ps = m.GetParameters();
                    try
                    {
                        if (ps.Length == 1 && ps[0].ParameterType == typeof(string))
                        {
                            // e.g., Info(string), ShaderInfo(string), Always(string)
                            m.Invoke(null, new object[] { $"{m.Name} sample" });
                        }
                        else if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(UnityEngine.Object))
                        {
                            // e.g., Info(string, Object)
                            m.Invoke(null, new object[] { $"{m.Name} with context", ctx });
                        }
                        else if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(LogType))
                        {
                            // e.g., Always(string, LogType)
                            m.Invoke(null, new object[] { $"{m.Name} Warning", LogType.Warning });
                        }
                        else if (ps.Length == 3 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(string) && ps[2].ParameterType == typeof(LogType))
                        {
                            // e.g., Always(string category, string message, LogType)
                            m.Invoke(null, new object[] { "TestCategory", $"{m.Name} cat warning", LogType.Warning });
                        }
                    }
                    catch (Exception ex)
                    {
                        HoyoToonLogger.Warning($"Logger test skipped method {m.Name}: {ex.Message}");
                    }
                }
            }
            finally
            {
                // Cleanup context object immediately to avoid polluting scene
                if (ctx != null) UnityEngine.Object.DestroyImmediate(ctx);

                // Unsubscribe
                HoyoToonLogger.OnLog -= OnLog;

                // Restore original debug state
                HoyoToonDebug.SetEnabled(original);
            }

            // Summary notification
            HoyoToonDialogWindow.ShowOk("Logger Test", $"Emitted {emitted} logger messages.", MessageType.Info);
        }

        [MenuItem(MenuPathDialogImage, false, 502)]
        private static void ShowDialogWithImage()
        {
            string title = "Logger Test: Dialog With Image";
            string message =
                "# Setup Guide\n\n" +
                "Follow the steps shown in the image below.\n\n" +
                "1. Open the `Manager` window.\n" +
                "2. Click **Scan Project**.\n" +
                "3. Review changes and press **Apply**.\n\n" +
                "Tip: You can Copy the message from the toolbar. [Learn more](docs://logger).\n";

            // Use an existing image under Resources/UI; omit extension in the resource path
            // Available examples include: background, hoyotoon, managerlogo, scriptslogo, etc.
            const string resourcePath = "UI/managerlogo";

            HoyoToonDialogWindow.ShowYesNoWithImage(
                title,
                message,
                MessageType.Info,
                topBar: null,
                onResult: yes =>
                {
                    if (yes) HoyoToonLogger.Info("User confirmed the setup guide.");
                    else HoyoToonLogger.Warning("User cancelled the setup guide.");
                },
                contentImageResourcePath: resourcePath,
                contentImage: null,
                contentImageMaxHeight: 240f
            );
        }
    }
}
#endif
