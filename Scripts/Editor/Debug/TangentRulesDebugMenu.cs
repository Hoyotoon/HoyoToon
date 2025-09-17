#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HoyoToon.Models;
using HoyoToon.API;
using HoyoToon.Utilities;

namespace HoyoToon.Debugging
{
    /// <summary>
    /// Editor menu to test tangent reset/regenerate scenarios per-config and per-mode.
    /// </summary>
    public static class TangentRulesDebugMenu
    {
        private static GameObject RequireSelectionModel()
        {
            var go = Selection.activeObject as GameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("HoyoToon Tangents", "Select a model GameObject (Project asset or a Scene object).", "OK");
            }
            return go;
        }

        [MenuItem("HoyoToon/Debug/Tangents/Apply From Config (Auto)")]
        [MenuItem("GameObject/HoyoToon/Debug/Tangents/Apply From Config (Auto)")]
        public static void ApplyFromConfig()
        {
            var go = RequireSelectionModel();
            if (go == null) return;
            // Works for Scene or Project: call GO-based entry
            var ok = TangentRulesApplier.TryApplyFromConfig(go);
            EditorUtility.DisplayDialog("HoyoToon Tangents", ok ? "Applied tangent rules from config." : "No changes applied.", "OK");
        }

        [MenuItem("HoyoToon/Debug/Tangents/Reset (No Tangents)")]
        public static void Reset()
        {
            var go = RequireSelectionModel();
            if (go == null) return;
            var ok = TangentRulesApplier.Reset(go);
            EditorUtility.DisplayDialog("HoyoToon Tangents", ok ? "Reset completed." : "Reset did not change anything.", "OK");
        }

        [MenuItem("HoyoToon/Debug/Tangents/Generate (ModifyMeshTangents)")]
        public static void Generate()
        {
            var go = RequireSelectionModel();
            if (go == null) return;
            var ok = TangentRulesApplier.Apply(go, TangentRulesApplier.TangentMode.Generate);
            EditorUtility.DisplayDialog("HoyoToon Tangents", ok ? "Generated tangents." : "No changes applied.", "OK");
        }

        [MenuItem("HoyoToon/Debug/Tangents/From Vertex Color (MoveColors)")]
        public static void FromVertexColor()
        {
            var go = RequireSelectionModel();
            if (go == null) return;
            var ok = TangentRulesApplier.Apply(go, TangentRulesApplier.TangentMode.FromVertexColor);
            EditorUtility.DisplayDialog("HoyoToon Tangents", ok ? "Applied vertex-color-based tangents." : "No changes applied.", "OK");
        }
    }
}
#endif
