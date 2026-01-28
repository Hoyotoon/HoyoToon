#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.EditorTools.ManagerUI.Modules
{
    internal abstract class HoyoToonManagerModule : IDisposable
    {
        public abstract string DisplayName { get; }

        /// <summary>
        /// Called to draw the module's UI. Modules can assume GUI layout context.
        /// </summary>
        public abstract void OnGUI(HoyoToonManager targetManager);

        public virtual void Dispose()
        {
        }

        protected static bool DrawFoldoutSection(string title, bool expanded, Action drawer, float indent = 8f)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(10f);
                    expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldout);
                }

                if (!expanded)
                {
                    return false;
                }

                EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                drawer?.Invoke();
                return true;
            }
        }
    }
}
#endif
