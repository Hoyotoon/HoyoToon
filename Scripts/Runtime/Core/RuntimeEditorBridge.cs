using System;
using UnityEngine;

namespace HoyoToon.Runtime.Core
{
    internal static class RuntimeEditorBridge
    {
        internal static Action<UnityEngine.Object> MarkDirtyHandler;
        internal static Action<UnityEngine.Object> DestroyHandler;
        internal static Func<bool> IsGameViewFocusedHandler;
        internal static Action RequestPlayerLoopUpdateHandler;

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
    }
}
