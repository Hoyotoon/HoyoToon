#if UNITY_EDITOR
using System;
using UnityEngine;

namespace HoyoToon.Editor.Utilities
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
    }

    public static class HoyoToonLogger
    {
        public static class Categories
        {
            public const string System = "System";
            public const string Shader = "Shader";
            public const string UI = "UI";
            public const string Model = "Model";
            public const string API = "API";
            public const string Texture = "Texture";
            public const string Material = "Material";
            public const string Manager = "Manager";
            public const string Resources = "Resources";
            public const string Updater = "Updater";
            public const string FBXConverter = "FBX Converter";
            public const string Async = "Async";
            public const string Tour = "Tour";
        }

        public static void Log(string category, LogLevel level, string message, UnityEngine.Object context = null)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    LogCore.WarnCategory(category, message, context);
                    break;
                case LogLevel.Error:
                    LogCore.ErrorCategory(category, message, context);
                    break;
                default:
                    LogCore.LogCategory(category, message, context);
                    break;
            }
        }

        public static void ThrottleInfo(string key, string message, TimeSpan? throttle = null, string category = Categories.System)
            => LogCore.ThrottleLog(key, message, throttle, category);

        public static void ThrottleWarning(string key, string message, TimeSpan? throttle = null, string category = Categories.System)
            => LogCore.ThrottleWarn(key, message, throttle, category);

        public static void ThrottleError(string key, string message, TimeSpan? throttle = null, string category = Categories.System)
            => LogCore.ThrottleError(key, message, throttle, category);

        public static void Always(string message, LogType type = LogType.Log) => LogCore.LogAlways(message, type);
        public static void Always(string category, string message, LogType type) => LogCore.LogAlwaysCategory(category, message, type);

        public static event Action<string, LogType> OnLog
        {
            add { LogCore.OnLog += value; }
            remove { LogCore.OnLog -= value; }
        }
    }
}
#endif
