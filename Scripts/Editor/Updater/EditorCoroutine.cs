#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Updater
{
    internal static class EditorCoroutine
    {
        private static Stack<IEnumerator> s_RoutineStack;
        private static object s_CurrentYield;

        internal static bool IsRunning => s_RoutineStack != null && s_RoutineStack.Count > 0;

        internal static bool Start(IEnumerator routine)
        {
            if (routine == null || IsRunning)
            {
                return false;
            }

            s_RoutineStack = new Stack<IEnumerator>();
            s_RoutineStack.Push(routine);
            s_CurrentYield = null;

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
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Error, $"Editor coroutine failed: {ex.Message}");
                Stop();
            }
        }

        private static void Advance()
        {
            if (s_CurrentYield != null)
            {
                if (!IsYieldComplete(s_CurrentYield))
                {
                    return;
                }

                s_CurrentYield = null;
            }

            int safetyCounter = 0;
            while (IsRunning)
            {
                if (++safetyCounter > 1024)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Updater, LogLevel.Warning, "Editor coroutine hit safety limit while advancing nested yields.");
                    return;
                }

                IEnumerator routine = s_RoutineStack.Peek();
                if (!routine.MoveNext())
                {
                    s_RoutineStack.Pop();
                    continue;
                }

                object yielded = routine.Current;
                if (yielded is IEnumerator nestedRoutine)
                {
                    s_RoutineStack.Push(nestedRoutine);
                    continue;
                }

                if (yielded == null)
                {
                    return;
                }

                if (yielded is AsyncOperation || yielded is CustomYieldInstruction)
                {
                    s_CurrentYield = yielded;
                    return;
                }

                s_CurrentYield = yielded;
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
            s_RoutineStack = null;
            s_CurrentYield = null;
            EditorApplication.update -= Update;
        }
    }
}
#endif