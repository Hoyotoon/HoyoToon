#if UNITY_EDITOR
using UnityEditor;
using HoyoToon.Runtime.Character;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.Inspector
{
    [CustomEditor(typeof(HSRCharacterController))]
    internal sealed class HSRCharacterControllerEditor : UnityEditor.Editor
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
