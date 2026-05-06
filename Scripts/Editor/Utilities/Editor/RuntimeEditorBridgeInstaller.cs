#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HoyoToon.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Editor
{
    [InitializeOnLoad]
    internal static class RuntimeEditorBridgeInstaller
    {
        private static readonly Dictionary<Action, EditorApplication.CallbackFunction> s_DelayedActionCallbacks =
            new Dictionary<Action, EditorApplication.CallbackFunction>();

        private static readonly Dictionary<Action, AssemblyReloadEvents.AssemblyReloadCallback> s_AssemblyReloadCallbacks =
            new Dictionary<Action, AssemblyReloadEvents.AssemblyReloadCallback>();

        static RuntimeEditorBridgeInstaller()
        {
            RuntimeEditorBridge.MarkDirtyHandler = EditorUtility.SetDirty;
            RuntimeEditorBridge.DestroyHandler = target => UnityEngine.Object.DestroyImmediate(target);
            RuntimeEditorBridge.InstantiatePrefabHandler = (prefab, parent) =>
                PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            RuntimeEditorBridge.RegisterCreatedObjectUndoHandler = Undo.RegisterCreatedObjectUndo;
            RuntimeEditorBridge.IsGameViewFocusedHandler = IsGameViewFocused;
            RuntimeEditorBridge.RequestPlayerLoopUpdateHandler = EditorApplication.QueuePlayerLoopUpdate;
            RuntimeEditorBridge.RegisterHierarchyChangedHandler = handler => EditorApplication.hierarchyChanged += handler;
            RuntimeEditorBridge.UnregisterHierarchyChangedHandler = handler => EditorApplication.hierarchyChanged -= handler;
            RuntimeEditorBridge.ScheduleDelayedEditModeActionHandler = ScheduleDelayedEditModeAction;
            RuntimeEditorBridge.CancelDelayedEditModeActionHandler = CancelDelayedEditModeAction;
            RuntimeEditorBridge.RegisterEditModeCleanupHandler = RegisterEditModeCleanup;
            RuntimeEditorBridge.UnregisterEditModeCleanupHandler = UnregisterEditModeCleanup;
        }

        private static void ScheduleDelayedEditModeAction(Action action)
        {
            if (action == null)
            {
                return;
            }

            CancelDelayedEditModeAction(action);

            EditorApplication.CallbackFunction callback = () =>
            {
                s_DelayedActionCallbacks.Remove(action);
                action();
            };
            s_DelayedActionCallbacks[action] = callback;
            EditorApplication.delayCall += callback;
        }

        private static void CancelDelayedEditModeAction(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (!s_DelayedActionCallbacks.TryGetValue(action, out EditorApplication.CallbackFunction callback))
            {
                return;
            }

            EditorApplication.delayCall -= callback;
            s_DelayedActionCallbacks.Remove(action);
        }

        private static void RegisterEditModeCleanup(Action cleanup)
        {
            if (cleanup == null)
            {
                return;
            }

            UnregisterEditModeCleanup(cleanup);
            AssemblyReloadEvents.AssemblyReloadCallback reloadCallback = GetOrCreateAssemblyReloadCallback(cleanup);
            AssemblyReloadEvents.beforeAssemblyReload += reloadCallback;
            EditorApplication.quitting += cleanup;
        }

        private static void UnregisterEditModeCleanup(Action cleanup)
        {
            if (cleanup == null)
            {
                return;
            }

            if (s_AssemblyReloadCallbacks.TryGetValue(cleanup, out AssemblyReloadEvents.AssemblyReloadCallback reloadCallback))
            {
                AssemblyReloadEvents.beforeAssemblyReload -= reloadCallback;
                s_AssemblyReloadCallbacks.Remove(cleanup);
            }

            EditorApplication.quitting -= cleanup;
        }

        private static AssemblyReloadEvents.AssemblyReloadCallback GetOrCreateAssemblyReloadCallback(Action action)
        {
            if (!s_AssemblyReloadCallbacks.TryGetValue(action, out AssemblyReloadEvents.AssemblyReloadCallback callback))
            {
                callback = () => action();
                s_AssemblyReloadCallbacks[action] = callback;
            }

            return callback;
        }

        private static bool IsGameViewFocused()
        {
            if (!Application.isFocused)
            {
                return false;
            }

            EditorWindow activeWindow = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
            return activeWindow != null
                && string.Equals(activeWindow.GetType().Name, "GameView", StringComparison.Ordinal);
        }
    }
}
#endif
