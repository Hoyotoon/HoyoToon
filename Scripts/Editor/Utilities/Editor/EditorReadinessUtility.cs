#if UNITY_EDITOR
using UnityEditor;

namespace HoyoToon.Editor.Utilities.Editor
{
    internal static class EditorReadinessUtility
    {
        public static bool IsReadyForEditorWork()
        {
            return !EditorApplication.isCompiling
                && !EditorApplication.isUpdating
                && !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
#endif