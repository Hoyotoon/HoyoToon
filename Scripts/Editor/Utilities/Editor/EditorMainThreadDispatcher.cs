#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;

namespace HoyoToon.Editor.Utilities.Editor
{
    internal static class EditorMainThreadDispatcher
    {
        private static readonly Queue<Action> s_Queue = new Queue<Action>();
        private static bool s_IsUpdateRegistered;

        internal static Task InvokeAsync(Action action)
        {
            if (action == null)
                return Task.CompletedTask;

            var completion = new TaskCompletionSource<bool>();
            lock (s_Queue)
            {
                s_Queue.Enqueue(() =>
                {
                    try
                    {
                        action();
                        completion.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        completion.SetException(ex);
                    }
                });
            }

            EnsureUpdateRegistered();
            return completion.Task;
        }

        internal static Task DelayCallAsync(Action action)
        {
            if (action == null)
                return Task.CompletedTask;

            var completion = new TaskCompletionSource<bool>();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    action();
                    completion.SetResult(true);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            };

            return completion.Task;
        }

        private static void EnsureUpdateRegistered()
        {
            if (s_IsUpdateRegistered)
                return;

            s_IsUpdateRegistered = true;
            EditorApplication.update -= ProcessQueue;
            EditorApplication.update += ProcessQueue;
        }

        private static void ProcessQueue()
        {
            while (true)
            {
                Action action;
                lock (s_Queue)
                {
                    if (s_Queue.Count == 0)
                    {
                        s_IsUpdateRegistered = false;
                        EditorApplication.update -= ProcessQueue;
                        return;
                    }

                    action = s_Queue.Dequeue();
                }

                action?.Invoke();
            }
        }
    }
}
#endif
