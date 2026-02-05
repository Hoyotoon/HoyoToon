#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.EditorTools.Onboarding
{
    internal static class HoyoToonTourCallout
    {
        private static GUIStyle s_TitleStyle;
        private static GUIStyle s_BodyStyle;

        public static void Draw(string title, string body, string actionLabel, System.Action action, bool highlightAction = false)
        {
            DrawInternal(title, body, actionLabel, action, highlightAction, out _);
        }

        public static Rect DrawWithRect(string title, string body, string actionLabel, System.Action action, bool highlightAction = false)
        {
            DrawInternal(title, body, actionLabel, action, highlightAction, out var rect);
            return rect;
        }

        private static void DrawInternal(string title, string body, string actionLabel, System.Action action, bool highlightAction, out Rect rect)
        {
            EnsureStyles();

            bool hasRect = false;
            Rect unionRect = new Rect();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var barRect = GUILayoutUtility.GetRect(0f, 4f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(barRect, new Color(1f, 0.6f, 0.1f, 0.9f));
                unionRect = barRect;
                hasRect = true;

                GUILayout.Label(title, s_TitleStyle);
                unionRect = UnionRect(unionRect, GUILayoutUtility.GetLastRect(), ref hasRect);

                if (!string.IsNullOrEmpty(body))
                {
                    GUILayout.Label(body, s_BodyStyle);
                    unionRect = UnionRect(unionRect, GUILayoutUtility.GetLastRect(), ref hasRect);
                }

                if (!string.IsNullOrEmpty(actionLabel) && action != null)
                {
                    GUILayout.Space(4f);
                    var prevBg = GUI.backgroundColor;
                    if (highlightAction)
                    {
                        GUI.backgroundColor = EditorGUIUtility.isProSkin
                            ? new Color(1f, 0.6f, 0.1f, 1f)
                            : new Color(1f, 0.7f, 0.2f, 1f);
                    }

                    if (GUILayout.Button(actionLabel))
                    {
                        action.Invoke();
                    }

                    if (highlightAction)
                    {
                        GUI.backgroundColor = prevBg;
                    }

                    unionRect = UnionRect(unionRect, GUILayoutUtility.GetLastRect(), ref hasRect);
                }
            }

            rect = hasRect ? ExpandRect(unionRect, 6f) : Rect.zero;
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

        private static Rect UnionRect(Rect current, Rect next, ref bool hasRect)
        {
            if (!hasRect)
            {
                hasRect = true;
                return next;
            }

            float xMin = Mathf.Min(current.xMin, next.xMin);
            float yMin = Mathf.Min(current.yMin, next.yMin);
            float xMax = Mathf.Max(current.xMax, next.xMax);
            float yMax = Mathf.Max(current.yMax, next.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Rect ExpandRect(Rect rect, float padding)
        {
            return new Rect(rect.x - padding, rect.y - padding, rect.width + padding * 2f, rect.height + padding * 2f);
        }
    }
}
#endif
