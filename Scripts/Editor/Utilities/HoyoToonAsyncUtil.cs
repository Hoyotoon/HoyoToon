#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace HoyoToon.Utilities
{
    /// <summary>
    /// Safe fire-and-forget helpers for editor async flows.
    /// </summary>
    public static class HoyoToonAsyncUtil
    {
        public static void RunFireAndForget(Func<Task> taskFactory, string context, Action<Exception> onError = null)
        {
            if (taskFactory == null) return;
            try
            {
                var task = taskFactory();
                if (task == null) return;
                task.ContinueWith(t =>
                {
                    var ex = t.Exception?.GetBaseException();
                    if (ex == null) return;
                    onError?.Invoke(ex);
                    HoyoToonLogger.Always("Async", $"{context} failed: {ex}", LogType.Exception);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                HoyoToonLogger.Always("Async", $"{context} failed: {ex}", LogType.Exception);
            }
        }
    }
}
#endif
