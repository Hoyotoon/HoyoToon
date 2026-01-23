#if UNITY_EDITOR
using System;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Obsolete shim for backward compatibility. Use HoyoToonLogger.Throttle* instead.
    /// </summary>
    [Obsolete("Use HoyoToonLogger.Throttle* methods instead.")]
    public static class HoyoToonLogThrottle
    {
        public static void Info(string key, string message, TimeSpan? throttle = null)
            => HoyoToonLogger.ThrottleInfo(key, message, throttle);

        public static void Warn(string key, string message, TimeSpan? throttle = null)
            => HoyoToonLogger.ThrottleWarning(key, message, throttle);

        public static void Error(string key, string message, TimeSpan? throttle = null)
            => HoyoToonLogger.ThrottleError(key, message, throttle);
    }
}
#endif
