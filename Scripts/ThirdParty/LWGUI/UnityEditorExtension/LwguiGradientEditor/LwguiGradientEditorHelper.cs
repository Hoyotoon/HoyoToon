// Copyright (c) Jason Ma

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LWGUI.Runtime.LwguiGradient;

namespace LWGUI.LwguiGradientEditor
{
    public static class LwguiGradientEditorHelper
    {
        private static readonly int s_LwguiGradientHash = "s_LwguiGradientHash".GetHashCode();
        private static int s_LwguiGradientID;

        // GradientEditor.DrawGradientWithBackground()
        public static void DrawGradientWithBackground(Rect position, LwguiGradient gradient, ColorSpace colorSpace, LwguiGradient.ChannelMask viewChannelMask)
        {
            Texture2D gradientTexture = gradient.GetPreviewRampTexture(256, 1, colorSpace, viewChannelMask);
            Rect r2 = new Rect(position.x + 1, position.y + 1, position.width - 2, position.height - 2);

            // Background checkers
            Texture2D backgroundTexture = GradientEditor.GetBackgroundTexture();
            Rect texCoordsRect = new Rect(0, 0, r2.width / backgroundTexture.width, r2.height / backgroundTexture.height);
            GUI.DrawTextureWithTexCoords(r2, backgroundTexture, texCoordsRect, false);

            // Outline for Gradinet Texture, used to be Frame over texture.
            // LWGUI: GUI.Box() will cause subsequent attributes to be unable to be selected
            // GUI.Box(position, GUIContent.none);
            
            // Gradient texture
            Color oldColor = GUI.color;
            GUI.color = Color.white;            //Dont want the Playmode tint to be applied to gradient textures.
            if (gradientTexture != null)
                GUI.DrawTexture(r2, gradientTexture, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;

            // HDR label
            // float maxColorComponent = GetMaxColorComponent(gradient);
            // if (maxColorComponent > 1.0f)
            // {
            //     GUI.Label(new Rect(position.x, position.y, position.width - 3, position.height), "HDR", EditorStyles.centeredGreyMiniLabel);
            // }
        }

        public static void DrawGradientWithSeparateAlphaChannel(Rect position, LwguiGradient gradient, ColorSpace colorSpace, LwguiGradient.ChannelMask viewChannelMask)
        {
            if (!LwguiGradient.HasChannelMask(viewChannelMask, LwguiGradient.ChannelMask.Alpha) || viewChannelMask == LwguiGradient.ChannelMask.Alpha)
            {
                DrawGradientWithBackground(position, gradient, colorSpace, viewChannelMask);
            }
            else
            {
                var r2 = new Rect(position.x + 1, position.y + 1, position.width - 2, position.height - 2);
                var rgbRect = new Rect(r2.x, r2.y, r2.width, r2.height * 0.8f);
                var alphaRect = new Rect(rgbRect.x, rgbRect.yMax, r2.width, r2.height * 0.2f);
                
                var rgbTexture = gradient.GetPreviewRampTexture(256, 1, colorSpace, viewChannelMask ^ LwguiGradient.ChannelMask.Alpha);
                var alphaTexture = gradient.GetPreviewRampTexture(256, 1, colorSpace, LwguiGradient.ChannelMask.Alpha);
                
                Color oldColor = GUI.color;
                GUI.color = Color.white;            //Dont want the Playmode tint to be applied to gradient textures.
                GUI.DrawTexture(rgbRect, rgbTexture, ScaleMode.StretchToFill, false);
                GUI.DrawTexture(alphaRect, alphaTexture, ScaleMode.StretchToFill, false);
                GUI.color = oldColor;
            }
        }

        public static void GradientField(Rect position, GUIContent label, LwguiGradient gradient, 
            ColorSpace colorSpace = ColorSpace.Gamma, 
            LwguiGradient.ChannelMask viewChannelMask = LwguiGradient.ChannelMask.All, 
            LwguiGradient.GradientTimeRange timeRange = LwguiGradient.GradientTimeRange.One,
            Action onOpenWindow = null)
        {
            int id = GUIUtility.GetControlID(s_LwguiGradientHash, FocusType.Keyboard, position);
            var rect = EditorGUI.PrefixLabel(position, id, label);
            var evt = Event.current;
            

            // internal static Gradient DoGradientField(Rect position, int id, Gradient value, SerializedProperty property, bool hdr, ColorSpace space)
            switch (evt.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (rect.Contains(evt.mousePosition))
                    {
                        if (evt.button == 0)
                        {
                            s_LwguiGradientID = id;
                            GUIUtility.keyboardControl = id;
                            LwguiGradientWindow.Show(gradient, colorSpace, viewChannelMask, timeRange, GUIView.current);
                            onOpenWindow?.Invoke();
                            GUIUtility.ExitGUI();
                        }
                        else if (evt.button == 1)
                        {
                            // if (property != null)
                            //     GradientContextMenu.Show(property.Copy());
                            // // TODO: make work for Gradient value
                        }
                    }
                    break;
                case EventType.KeyDown:
                    if (GUIUtility.keyboardControl == id && (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
                    {
                        evt.Use();
                        LwguiGradientWindow.Show(gradient, colorSpace, viewChannelMask, timeRange, GUIView.current);
                        onOpenWindow?.Invoke();
                        GUIUtility.ExitGUI();
                    }

                    break;
                case EventType.Repaint:
                    DrawGradientWithSeparateAlphaChannel(rect, gradient, colorSpace, viewChannelMask);
                    break;
                case EventType.ExecuteCommand:
                    // When drawing the modifying Gradient Field and it has changed
                    if ((GUIUtility.keyboardControl == id || s_LwguiGradientID == id)
                        && (evt.commandName is LwguiGradientWindow.LwguiGradientChangedCommand))
                    {
                        GUI.changed = true;
                        LwguiGradientHelper.ClearRampPreviewCaches();
                        HandleUtility.Repaint();
                    }
                    break;
                case EventType.ValidateCommand:
                    // Sync Undo/Redo result to editor window
                    if (s_LwguiGradientID == id && evt.commandName == "UndoRedoPerformed")
                    {
                        LwguiGradientWindow.UpdateCurrentGradient(gradient);
                    }
                    break;
            }
        }

        /// Lwgui Gradient Field with full Undo/Redo/ContextMenu functions
        public static void GradientField(Rect position, GUIContent label, SerializedProperty property, 
            ColorSpace colorSpace = ColorSpace.Gamma, 
            LwguiGradient.ChannelMask viewChannelMask = LwguiGradient.ChannelMask.All, 
            LwguiGradient.GradientTimeRange timeRange = LwguiGradient.GradientTimeRange.One)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            var gradient = property.GetLwguiGradientValue();
            GradientField(position, label, gradient, colorSpace, viewChannelMask, timeRange, 
                () => LwguiGradientWindow.RegisterSerializedObjectUndo(property.serializedObject.targetObject));

            if (EditorGUI.EndChangeCheck())
            {
                GUI.changed = true;
                LwguiGradientWindow.RegisterSerializedObjectUndo(property.serializedObject.targetObject);
                if (LwguiGradientWindow.instance)
                    property.SetLwguiGradientValue(LwguiGradientWindow.instance.lwguiGradient);
            }
            EditorGUI.EndProperty();
        }

        public static bool GradientEditButton(Rect position, GUIContent icon, LwguiGradient gradient,
            ColorSpace colorSpace = ColorSpace.Gamma,
            LwguiGradient.ChannelMask viewChannelMask = LwguiGradient.ChannelMask.All,
            LwguiGradient.GradientTimeRange timeRange = LwguiGradient.GradientTimeRange.One,
            Func<bool> shouldOpenWindowAfterClickingEvent = null,
            LwguiGradientWindow.ChangeGradientCallback onChange = null)
        {
            int id = GUIUtility.GetControlID(s_LwguiGradientHash, FocusType.Keyboard, position);
            var evt = Event.current;
            
            // When drawing the modifying Gradient Field and it has changed
            if ((GUIUtility.keyboardControl == id || s_LwguiGradientID == id)
                && evt.GetTypeForControl(id) == EventType.ExecuteCommand 
                && evt.commandName == LwguiGradientWindow.LwguiGradientChangedCommand)
            {
                GUI.changed = true;
                HandleUtility.Repaint();
            }

            // Sync Undo/Redo result to editor window
            if (s_LwguiGradientID == id 
                && evt.commandName == "UndoRedoPerformed")
            {
                LwguiGradientWindow.UpdateCurrentGradient(gradient);
            }

            // Open editor window
            var clicked = ReflectionHelper.GUI_Button(position, id, icon, GUI.skin.button);
            if (clicked)
            {
                if (shouldOpenWindowAfterClickingEvent == null || shouldOpenWindowAfterClickingEvent.Invoke())
                {
                    s_LwguiGradientID = id;
                    GUIUtility.keyboardControl = id;
                    LwguiGradientWindow.Show(gradient, colorSpace, viewChannelMask, timeRange, GUIView.current, onChange);
                    GUIUtility.ExitGUI();
                }
            }
            
            return clicked;
        }

        public static LwguiGradient GetLwguiGradientValue(this SerializedProperty property)
        {
            LwguiGradient lwguiGradient = new();
            
            var curversProp = property.FindPropertyRelative("_curves");
            for (int i = 0; i < curversProp.arraySize; i++)
            {
                var curveProp = curversProp.GetArrayElementAtIndex(i);
                lwguiGradient.rawCurves[i] = curveProp.animationCurveValue;
            }

            return lwguiGradient;
        }

        public static void SetLwguiGradientValue(this SerializedProperty property, LwguiGradient lwguiGradient)
        {
            var curversProp = property.FindPropertyRelative("_curves");
            for (int i = 0; i < curversProp.arraySize && i < lwguiGradient.rawCurves.Count; i++)
            {
                var curveProp = curversProp.GetArrayElementAtIndex(i);
                curveProp.animationCurveValue = lwguiGradient.rawCurves[i];
            }
        }
    }
}