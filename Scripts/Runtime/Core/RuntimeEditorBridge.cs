using System;
using UnityEngine;

namespace HoyoToon.Runtime.Core
{
    internal static class RuntimeEditorBridge
    {
        internal static Action<UnityEngine.Object> MarkDirtyHandler;
        internal static Action<UnityEngine.Object> DestroyHandler;
        internal static Func<GameObject, Transform, GameObject> InstantiatePrefabHandler;
        internal static Action<UnityEngine.Object, string> RegisterCreatedObjectUndoHandler;
        internal static Func<bool> IsGameViewFocusedHandler;
        internal static Action RequestPlayerLoopUpdateHandler;
        internal static Action<Action> RegisterHierarchyChangedHandler;
        internal static Action<Action> UnregisterHierarchyChangedHandler;
        internal static Action<Action> ScheduleDelayedEditModeActionHandler;
        internal static Action<Action> CancelDelayedEditModeActionHandler;
        internal static Action<Action> RegisterEditModeCleanupHandler;
        internal static Action<Action> UnregisterEditModeCleanupHandler;

        internal static void MarkDirty(UnityEngine.Object target)
        {
            if (target == null || Application.isPlaying)
            {
                return;
            }

            MarkDirtyHandler?.Invoke(target);
        }

        internal static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (!Application.isPlaying && DestroyHandler != null)
            {
                DestroyHandler(target);
                return;
            }

            UnityEngine.Object.Destroy(target);
        }

        internal static GameObject InstantiatePrefabOrClone(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!Application.isPlaying && InstantiatePrefabHandler != null)
            {
                GameObject prefabInstance = InstantiatePrefabHandler(prefab, parent);
                if (prefabInstance != null)
                {
                    return prefabInstance;
                }
            }

            return parent != null
                ? UnityEngine.Object.Instantiate(prefab, parent)
                : UnityEngine.Object.Instantiate(prefab);
        }

        internal static void RegisterCreatedObjectUndo(UnityEngine.Object target, string actionName)
        {
            if (target == null || Application.isPlaying)
            {
                return;
            }

            RegisterCreatedObjectUndoHandler?.Invoke(target, actionName);
        }

        internal static bool IsGameViewFocused()
        {
            return IsGameViewFocusedHandler != null && IsGameViewFocusedHandler.Invoke();
        }

        internal static void RequestPlayerLoopUpdate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            RequestPlayerLoopUpdateHandler?.Invoke();
        }

        internal static void RegisterHierarchyChanged(Action handler)
        {
            if (handler == null || Application.isPlaying)
            {
                return;
            }

            RegisterHierarchyChangedHandler?.Invoke(handler);
        }

        internal static void UnregisterHierarchyChanged(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            UnregisterHierarchyChangedHandler?.Invoke(handler);
        }

        internal static void ScheduleDelayedEditModeAction(Action action)
        {
            if (action == null || Application.isPlaying)
            {
                return;
            }

            ScheduleDelayedEditModeActionHandler?.Invoke(action);
        }

        internal static void CancelDelayedEditModeAction(Action action)
        {
            if (action == null)
            {
                return;
            }

            CancelDelayedEditModeActionHandler?.Invoke(action);
        }

        internal static void RegisterEditModeCleanup(Action cleanup)
        {
            if (cleanup == null)
            {
                return;
            }

            RegisterEditModeCleanupHandler?.Invoke(cleanup);
        }

        internal static void UnregisterEditModeCleanup(Action cleanup)
        {
            if (cleanup == null)
            {
                return;
            }

            UnregisterEditModeCleanupHandler?.Invoke(cleanup);
        }
    }
}
