#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.Editor.UI.ManagerInspector.Components;

namespace HoyoToon.Editor.UI.Inspector
{
    internal static class ManagedControllerInspectorGUI
    {
        private static readonly ManagerHeader s_ManagerHeader = new ManagerHeader();

        public static void DrawManagedHeader()
        {
            DrawTopBanner();
            GUILayout.Space(6f);

            DrawManagedNotice();
        }

        public static void DrawManagedNotice()
        {

            EditorGUILayout.HelpBox(
                "This component is managed by the HoyoToon Manager.",
                MessageType.Info);

            HoyoToonManager manager = Object.FindFirstObjectByType<HoyoToonManager>(FindObjectsInactive.Include);
            using (new EditorGUI.DisabledScope(manager == null))
            {
                if (GUILayout.Button("Go To HoyoToon Manager"))
                {
                    Selection.activeObject = manager;
                    EditorGUIUtility.PingObject(manager);
                }
            }

            if (manager == null)
            {
                EditorGUILayout.HelpBox("No HoyoToon Manager was found in the current scene.", MessageType.Warning);
            }

            EditorGUILayout.Space();
        }

        public static void DrawTopBanner()
        {
            s_ManagerHeader.Draw();
        }
    }
}
#endif
