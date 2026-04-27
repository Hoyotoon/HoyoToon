#if UNITY_EDITOR
using System;
using HoyoToon.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.Editor
{
    [InitializeOnLoad]
    internal static class RuntimeEditorBridgeInstaller
    {
        static RuntimeEditorBridgeInstaller()
        {
            RuntimeEditorBridge.MarkDirtyHandler = EditorUtility.SetDirty;
            RuntimeEditorBridge.DestroyHandler = target => UnityEngine.Object.DestroyImmediate(target);
            RuntimeEditorBridge.IsGameViewFocusedHandler = IsGameViewFocused;
            RuntimeEditorBridge.RequestPlayerLoopUpdateHandler = EditorApplication.QueuePlayerLoopUpdate;
        }

        static bool IsGameViewFocused()
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
