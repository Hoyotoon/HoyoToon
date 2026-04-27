#if UNITY_EDITOR
using HoyoToon.Runtime.Rendering.PostProcessing.HSR.Uber;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using HoyoToon.Editor.Utilities.PostProcessing;
using HoyoToon.Editor.Utilities.Debugging;

namespace HoyoToon.Editor.Rendering.PostProcessing.HSR.Uber
{
    [CustomEditor(typeof(RPGUber))]
    internal sealed class RPGUberEditor : VolumeComponentEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(targets.Length != 1))
            {
                if (!GUILayout.Button("Add Missing RPG Components To Profile"))
                {
                    return;
                }
            }

            RPGUber component = target as RPGUber;
            if (component == null)
            {
                return;
            }

            if (!RPGUberProfileUtility.TryAddMissingProfileComponents(component, out var owningProfile, out bool changed, out string message))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.General, message);
                return;
            }

            if (changed)
            {
                HoyoToonLogger.Info(HoyoToonLogCategory.General, message);
                return;
            }

            HoyoToonLogger.Info(HoyoToonLogCategory.General, message);
        }
    }
}
#endif
