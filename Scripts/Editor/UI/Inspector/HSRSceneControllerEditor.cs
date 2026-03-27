#if UNITY_EDITOR
using UnityEditor;
using HoyoToon.Runtime.Scene;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.Inspector
{
    [CustomEditor(typeof(HSRSceneController))]
    internal sealed class HSRSceneControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ManagedControllerInspectorGUI.DrawTopBanner();
            UnityEngine.GUILayout.Space(6f);

            if (HoyoToonDebug.Enabled)
            {
                DrawDefaultInspector();
                return;
            }

            ManagedControllerInspectorGUI.DrawManagedNotice();
        }
    }
}
#endif
