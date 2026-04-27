#if UNITY_EDITOR
using HoyoToon.Runtime.Simulator.Camera;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace HoyoToon.Editor.Utilities.Editor
{
    [CustomEditor(typeof(CameraTargetSyncTester))]
    internal sealed class CameraTargetSyncTesterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CameraTargetSyncTester tester = target as CameraTargetSyncTester;
            if (tester == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(tester.BuildStatusReport(), MessageType.None);

            EditorGUILayout.Space();
            if (GUILayout.Button("Test Sync Camera Targets"))
            {
                bool changed = tester.SyncNow();
                HandlePostSync(tester, changed);
            }

            if (GUILayout.Button("Clear Cached Baselines"))
            {
                tester.ClearBaselines();
                Debug.Log("Cleared cached camera target baselines.", tester);
            }
        }

        private static void HandlePostSync(CameraTargetSyncTester tester, bool changed)
        {
            if (Application.isPlaying || !changed)
            {
                return;
            }

            EditorUtility.SetDirty(tester);
            MarkSceneDirty(tester);
            SceneView.RepaintAll();
        }

        private static void MarkSceneDirty(CameraTargetSyncTester tester)
        {
            UnityScene scene = tester.PreferredRoot != null
                ? tester.PreferredRoot.gameObject.scene
                : tester.ActiveModel != null
                    ? tester.ActiveModel.scene
                    : tester.gameObject.scene;

            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
#endif
