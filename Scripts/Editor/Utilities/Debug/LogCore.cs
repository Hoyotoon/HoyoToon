#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoyoToon.Editor.Utilities
{
    public static class LogCore
    {
        private readonly struct ThrottleEntry
        {
            public readonly DateTime LastLogUtc;
            public readonly TimeSpan Gate;

            public ThrottleEntry(DateTime lastLogUtc, TimeSpan gate)
            {
                LastLogUtc = lastLogUtc;
                Gate = gate;
            }
        }

        private const string Prefix = "<color=purple>[HoyoToon]</color>";
        private const string DefaultCategoryColor = "#C0C0C0";
        private static readonly Dictionary<string, ThrottleEntry> s_LastLogUtc = new Dictionary<string, ThrottleEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly object s_ThrottleGate = new object();
        private static readonly TimeSpan DefaultThrottle = TimeSpan.FromMinutes(2);
        private const int ThrottleCleanupStride = 64;
        private static int s_ThrottleWriteCount;

        private static readonly Dictionary<string, string> s_CategoryColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Shader",   "#80C7FF" },
            { "UI",       "#FFB000" },
            { "Model",    "#A0FF80" },
            { "API",      "#80E5FF" },
            { "Texture",  "#FF80A0" },
            { "Material", "#B180FF" },
            { "Manager",  "#FFD480" },
            { "Resources", "#FF8080" },
            { "Updater",  "#40FF40" },
            { "FBX Converter", "#FFA040" },
            { "Async",    "#80FF80" },
            { "Tour",     "#FF80FF" },
            { "System",   "#C0C0C0" },
        };

        public static event Action<string, LogType> OnLog;

        public static void Log(string message, UnityEngine.Object context = null) => InternalLog(message, LogType.Log, context);
        public static void Warn(string message, UnityEngine.Object context = null) => InternalLog(message, LogType.Warning, context);
        public static void Error(string message, UnityEngine.Object context = null) => InternalLog(message, LogType.Error, context);

        public static void LogCategory(string category, string message, UnityEngine.Object context = null) => InternalLog(message, LogType.Log, context, false, category);
        public static void WarnCategory(string category, string message, UnityEngine.Object context = null) => InternalLog(message, LogType.Warning, context, false, category);
        public static void ErrorCategory(string category, string message, UnityEngine.Object context = null) => InternalLog(message, LogType.Error, context, false, category);

        public static void LogAlways(string message, LogType type = LogType.Log) => InternalLog(message, type, null, true);
        public static void LogAlwaysCategory(string category, string message, LogType type = LogType.Log) => InternalLog(message, type, null, true, category);

        public static void ThrottleLog(string key, string message, TimeSpan? throttle = null, string category = "System")
            => ThrottleInternal(key, message, LogType.Log, throttle ?? DefaultThrottle, category);

        public static void ThrottleWarn(string key, string message, TimeSpan? throttle = null, string category = "System")
            => ThrottleInternal(key, message, LogType.Warning, throttle ?? DefaultThrottle, category);

        public static void ThrottleError(string key, string message, TimeSpan? throttle = null, string category = "System")
            => ThrottleInternal(key, message, LogType.Error, throttle ?? DefaultThrottle, category);

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

            RouteToUnityLog(formatted, type, context);

            try { OnLog?.Invoke(message, type); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        private static void RouteToUnityLog(string formatted, LogType type, UnityEngine.Object context)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    if (context != null) Debug.LogError(formatted, context); else Debug.LogError(formatted);
                    return;
                case LogType.Warning:
                    if (context != null) Debug.LogWarning(formatted, context); else Debug.LogWarning(formatted);
                    return;
                default:
                    if (context != null) Debug.Log(formatted, context); else Debug.Log(formatted);
                    return;
            }
        }

        private static void ThrottleInternal(string key, string message, LogType type, TimeSpan throttle, string category)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message)) return;
            var now = DateTime.UtcNow;
            lock (s_ThrottleGate)
            {
                if (s_LastLogUtc.TryGetValue(key, out var entry) && now - entry.LastLogUtc < throttle)
                    return;

                s_LastLogUtc[key] = new ThrottleEntry(now, throttle);

                s_ThrottleWriteCount++;
                if (s_ThrottleWriteCount >= ThrottleCleanupStride)
                {
                    s_ThrottleWriteCount = 0;
                    CleanupStaleThrottleEntries(now);
                }
            }

            InternalLog(message, type, null, true, category);
        }

        private static void CleanupStaleThrottleEntries(DateTime now)
        {
            if (s_LastLogUtc.Count == 0) return;

            var staleKeys = new List<string>();
            foreach (var kv in s_LastLogUtc)
            {
                var gateTicks = kv.Value.Gate.Ticks;
                var evictionGate = gateTicks > (long.MaxValue / 2)
                    ? TimeSpan.MaxValue
                    : TimeSpan.FromTicks(gateTicks * 2);

                if (now - kv.Value.LastLogUtc > evictionGate)
                    staleKeys.Add(kv.Key);
            }

            for (int i = 0; i < staleKeys.Count; i++)
                s_LastLogUtc.Remove(staleKeys[i]);
        }
    }
}
#endif
