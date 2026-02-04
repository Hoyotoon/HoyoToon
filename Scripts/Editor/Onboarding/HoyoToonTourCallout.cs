#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.EditorTools.Onboarding
{
    internal static class HoyoToonTourCallout
    {
        private static GUIStyle s_TitleStyle;
        private static GUIStyle s_BodyStyle;

        public static void Draw(string title, string body, string actionLabel, System.Action action)
        {
            EnsureStyles();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var barRect = GUILayoutUtility.GetRect(0f, 4f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(barRect, new Color(1f, 0.6f, 0.1f, 0.9f));

                GUILayout.Label(title, s_TitleStyle);
                if (!string.IsNullOrEmpty(body))
                {
                    GUILayout.Label(body, s_BodyStyle);
                }

                if (!string.IsNullOrEmpty(actionLabel) && action != null)
                {
                    GUILayout.Space(4f);
                    if (GUILayout.Button(actionLabel))
                    {
                        action.Invoke();
                    }
                }
            }
        }

        private static void EnsureStyles()
        {
            if (s_TitleStyle == null)
            {
                s_TitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12
                };
            }

            if (s_BodyStyle == null)
            {
                s_BodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 11
                };
            }
        }
    }
}
#endif
