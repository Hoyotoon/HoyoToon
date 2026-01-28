#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.EditorTools.ManagerScene
{
    [CustomEditor(typeof(HoyoToonScriptablesController))]
    internal sealed class HoyoToonScriptablesControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Scriptable settings are managed in the HoyoToon Manager > Materials tab.", MessageType.Info);

            var controller = target as HoyoToonScriptablesController;
            using (new EditorGUI.DisabledScope(controller == null || controller.Manager == null))
            {
                if (GUILayout.Button("Select HoyoToon Manager"))
                {
                    Selection.activeObject = controller.Manager;
                }
            }
        }
    }
}
#endif
