#if UNITY_EDITOR
using System;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Debugging
{
    public static class HoyoToonLogger
    {
        public static void Verbose(
            HoyoToonLogCategory category,
            string message,
            bool isBackgroundOperation = false,
            UnityEngine.Object context = null)
        {
            Log(HoyoToonLogLevel.Verbose, category, message, null, isBackgroundOperation, context);
        }

        public static void Info(
            HoyoToonLogCategory category,
            string message,
            bool isBackgroundOperation = false,
            UnityEngine.Object context = null)
        {
            Log(HoyoToonLogLevel.Info, category, message, null, isBackgroundOperation, context);
        }

        public static void Warning(
            HoyoToonLogCategory category,
            string message,
            Exception exception = null,
            bool isBackgroundOperation = false,
            UnityEngine.Object context = null)
        {
            Log(HoyoToonLogLevel.Warning, category, message, exception, isBackgroundOperation, context);
        }

        public static void Error(
            HoyoToonLogCategory category,
            string message,
            Exception exception = null,
            bool isBackgroundOperation = false,
            UnityEngine.Object context = null)
        {
            Log(HoyoToonLogLevel.Error, category, message, exception, isBackgroundOperation, context);
        }

        internal static void Log(
            HoyoToonLogLevel level,
            HoyoToonLogCategory category,
            string message,
            Exception exception,
            bool isBackgroundOperation,
            UnityEngine.Object context)
        {
            if (!HoyoToonDebug.ShouldLog(level))
            {
                return;
            }

            string formattedMessage = LogCore.FormatMessage(category, message, isBackgroundOperation);
            if (exception != null)
            {
                formattedMessage = HoyoToonDebug.Enabled
                    ? $"{formattedMessage}\n{exception}"
                    : $"{formattedMessage} ({exception.GetType().Name}: {exception.Message})";
            }

            switch (level)
            {
                case HoyoToonLogLevel.Error:
                    if (context != null)
                    {
                        Debug.LogError(formattedMessage, context);
                    }
                    else
                    {
                        Debug.LogError(formattedMessage);
                    }
                    break;

                case HoyoToonLogLevel.Warning:
                    if (context != null)
                    {
                        Debug.LogWarning(formattedMessage, context);
                    }
                    else
                    {
                        Debug.LogWarning(formattedMessage);
                    }
                    break;

                default:
                    if (context != null)
                    {
                        Debug.Log(formattedMessage, context);
                    }
                    else
                    {
                        Debug.Log(formattedMessage);
                    }
                    break;
            }
        }
    }
}
#endif