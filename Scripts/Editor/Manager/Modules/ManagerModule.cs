#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Onboarding;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal abstract class ManagerModule : IDisposable
    {
        public abstract string DisplayName { get; }

        internal virtual string NavbarTourTarget => null;

        internal virtual string NavbarTourLabel => DisplayName;

        public abstract void OnGUI(HoyoToonManager targetManager);

        public virtual void Dispose()
        {
        }

        protected static void DrawInlineCalloutIfNeeded(string stepId, string body)
        {
            if (!GuidedTourController.IsActive)
            {
                return;
            }

            if (!string.Equals(GuidedTourController.CurrentStep.id, stepId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TourCallout.Draw(
                $"Guided Tour: {GuidedTourController.CurrentStep.title}",
                body,
                null,
                null);
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
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(indent);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        drawer?.Invoke();
                    }
                }
                return true;
            }
        }
    }
}
#endif
