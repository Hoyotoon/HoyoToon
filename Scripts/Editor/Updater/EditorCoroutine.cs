#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using HoyoToon.Editor.Utilities.Debugging;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Updater
{
    internal static class EditorCoroutine
    {
        private static Stack<IEnumerator> routineStack;
        private static object currentYield;

        internal static bool IsRunning => routineStack != null && routineStack.Count > 0;

        internal static bool Start(IEnumerator routine)
        {
            if (routine == null || IsRunning)
            {
                return false;
            }

            routineStack = new Stack<IEnumerator>();
            routineStack.Push(routine);
            currentYield = null;

            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            return true;
        }

        private static void Update()
        {
            if (!IsRunning)
            {
                Stop();
                return;
            }

            try
            {
                Advance();
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(HoyoToonLogCategory.General, $"Updater coroutine failed: {exception.Message}", exception);
                Stop();
                PackageUpdaterService.HandleUnhandledCoroutineException(exception);
            }
        }

        private static void Advance()
        {
            if (currentYield != null)
            {
                if (!IsYieldComplete(currentYield))
                {
                    return;
                }

                currentYield = null;
            }

            int safetyCounter = 0;
            while (IsRunning)
            {
                if (++safetyCounter > 1024)
                {
                    HoyoToonLogger.Warning(HoyoToonLogCategory.General, "Updater coroutine hit its nested-yield safety limit.");
                    return;
                }

                IEnumerator routine = routineStack.Peek();
                if (!routine.MoveNext())
                {
                    routineStack.Pop();
                    continue;
                }

                object yielded = routine.Current;
                if (yielded is IEnumerator nestedRoutine)
                {
                    routineStack.Push(nestedRoutine);
                    continue;
                }

                if (yielded == null)
                {
                    return;
                }

                if (yielded is AsyncOperation || yielded is CustomYieldInstruction)
                {
                    currentYield = yielded;
                    return;
                }

                currentYield = yielded;
                return;
            }

            Stop();
        }

        private static bool IsYieldComplete(object yielded)
        {
            if (yielded == null)
            {
                return true;
            }

            if (yielded is AsyncOperation asyncOperation)
            {
                return asyncOperation.isDone;
            }

            if (yielded is CustomYieldInstruction customYieldInstruction)
            {
                return !customYieldInstruction.keepWaiting;
            }

            return true;
        }

        private static void Stop()
        {
            Stack<IEnumerator> routinesToDispose = routineStack;
            routineStack = null;
            currentYield = null;
            EditorApplication.update -= Update;

            if (routinesToDispose == null)
            {
                return;
            }

            while (routinesToDispose.Count > 0)
            {
                if (routinesToDispose.Pop() is IDisposable disposableRoutine)
                {
                    try
                    {
                        disposableRoutine.Dispose();
                    }
                    catch (Exception exception)
                    {
                        HoyoToonLogger.Warning(HoyoToonLogCategory.General, $"Failed to dispose updater coroutine: {exception.Message}");
                    }
                }
            }
        }
    }
}
#endif
