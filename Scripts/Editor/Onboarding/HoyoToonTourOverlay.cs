#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HoyoToon.EditorTools.Onboarding
{
    internal static class HoyoToonTourOverlay
    {
        private static readonly Color s_FillColor = new Color(1f, 0.6f, 0.1f, 0.12f);
        private static readonly Color s_OutlineColor = new Color(1f, 0.6f, 0.1f, 0.9f);
        private static GUIStyle s_TooltipStyle;
        private static bool s_updateHooked;
        private static double s_nextRepaintTime;

        static HoyoToonTourOverlay()
        {
            EnsureUpdateHook();
        }

        public static void DrawHighlightIfActive(string targetId, Rect rect, string label, bool blink = true)
        {
            if (!HoyoToonGuidedTourController.IsTargetActive(targetId))
            {
                return;
            }

            EnsureUpdateHook();
            DrawHighlight(rect, label, blink);
        }

        private static void DrawHighlight(Rect rect, string label, bool blink)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Rect padded = new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f);
            float pulse = blink ? 0.35f + Mathf.PingPong((float)EditorApplication.timeSinceStartup * 1.8f, 0.65f) : 1f;
            var fill = new Color(s_FillColor.r, s_FillColor.g, s_FillColor.b, s_FillColor.a * pulse);
            var outline = new Color(s_OutlineColor.r, s_OutlineColor.g, s_OutlineColor.b, s_OutlineColor.a * pulse);
            EditorGUI.DrawRect(padded, fill);
            Handles.DrawSolidRectangleWithOutline(padded, new Color(0f, 0f, 0f, 0f), outline);

            if (!string.IsNullOrEmpty(label))
            {
                EnsureStyle();
                var content = new GUIContent(label);
                Vector2 size = s_TooltipStyle.CalcSize(content);
                Rect tipRect = new Rect(padded.x, padded.y - size.y - 6f, size.x + 10f, size.y + 4f);
                EditorGUI.DrawRect(tipRect, new Color(0f, 0f, 0f, 0.8f));
                GUI.Label(new Rect(tipRect.x + 5f, tipRect.y + 2f, size.x, size.y), content, s_TooltipStyle);
            }
        }

        private static void EnsureStyle()
        {
            if (s_TooltipStyle == null)
            {
                s_TooltipStyle = new GUIStyle(EditorStyles.whiteLabel)
                {
                    fontSize = 10
                };
            }
        }

        private static void EnsureUpdateHook()
        {
            if (s_updateHooked)
            {
                return;
            }

            s_updateHooked = true;
            EditorApplication.update += HandleEditorUpdate;
        }

        private static void HandleEditorUpdate()
        {
            if (!HoyoToonGuidedTourController.IsActive)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < s_nextRepaintTime)
            {
                return;
            }

            s_nextRepaintTime = now + 0.05f; // ~20 fps for blink
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
#endif
