#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Core logger for HoyoToon Editor utilities. Handles formatting, eventing, and the debug gate via HoyoToonDebug.
    /// </summary>
    public static class HoyoToonLogCore
    {
        private const string Prefix = "<color=purple>[HoyoToon]</color>";
        private const string DefaultCategoryColor = "#C0C0C0"; // light gray

        // Category -> Color hex (e.g., "#FF80FF" or named color)
        private static readonly Dictionary<string, string> s_CategoryColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Shader",   "#80C7FF" }, // blue
            { "UI",       "#FFB000" }, // amber
            { "Model",    "#A0FF80" }, // green
            { "API",      "#80E5FF" }, // cyan
            { "Texture",  "#FF80A0" }, // pink
            { "Material", "#B180FF" }, // violet
            { "Manager",  "#FFD480" }, // peach
            { "Resources", "#FF8080" }, // red
            { "Updater",  "#40FF40" }, // bright green
        };

        public static event Action<string, LogType> OnLog;

        /// <summary>
        /// Logs an informational message if debug is enabled.
        /// </summary>
        public static void Log(string message) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Log); }
        public static void Warn(string message) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Warning); }
        public static void Error(string message) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Error); }

        public static void Log(string message, UnityEngine.Object context) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Log, context); }
        public static void Warn(string message, UnityEngine.Object context) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Warning, context); }
        public static void Error(string message, UnityEngine.Object context) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Error, context); }

        /// <summary>
        /// Category-aware logging. Category can have a custom color. These respect the debug toggle.
        /// </summary>
        public static void LogCategory(string category, string message) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Log, null, false, category); }
        public static void WarnCategory(string category, string message) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Warning, null, false, category); }
        public static void ErrorCategory(string category, string message) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Error, null, false, category); }

        public static void LogCategory(string category, string message, UnityEngine.Object context) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Log, context, false, category); }
        public static void WarnCategory(string category, string message, UnityEngine.Object context) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Warning, context, false, category); }
        public static void ErrorCategory(string category, string message, UnityEngine.Object context) { if (!HoyoToonDebug.Enabled) return; InternalLog(message, LogType.Error, context, false, category); }

        /// <summary>
        /// Unconditional logging.
        /// </summary>
        public static void LogAlways(string message, LogType type = LogType.Log) => InternalLog(message, type, null, true);
        public static void LogAlwaysCategory(string category, string message, LogType type = LogType.Log) => InternalLog(message, type, null, true, category);

        /// <summary>
        /// Configure a category color at runtime.
        /// </summary>
        public static void SetCategoryColor(string category, Color color)
        {
            if (string.IsNullOrEmpty(category)) return;
            s_CategoryColors[category] = "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        public static void SetCategoryColor(string category, string htmlColor)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(htmlColor)) return;
            s_CategoryColors[category] = htmlColor;
        }

        private static void InternalLog(string message, LogType type, UnityEngine.Object context = null, bool force = false, string category = null)
        {
            if (!force && !HoyoToonDebug.Enabled) return;
            string categoryPart = string.Empty;
            if (!string.IsNullOrEmpty(category))
            {
                string color = s_CategoryColors.TryGetValue(category, out var hex) ? hex : DefaultCategoryColor;
                categoryPart = $" <color={color}>[{category}]</color>";
            }
            string formatted = $"{Prefix}{categoryPart} {message}";

            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    if (context != null) Debug.LogError(formatted, context); else Debug.LogError(formatted);
                    break;
                case LogType.Warning:
                    if (context != null) Debug.LogWarning(formatted, context); else Debug.LogWarning(formatted);
                    break;
                default:
                    if (context != null) Debug.Log(formatted, context); else Debug.Log(formatted);
                    break;
            }

            try { OnLog?.Invoke(message, type); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
#endif
