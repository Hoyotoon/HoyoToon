#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace HoyoToon.Editor.Utilities
{
    public static class AsyncUtil
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
