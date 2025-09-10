// Copyright (c) Jason Ma

using System;
using UnityEngine;
using UnityEditor;
using LWGUI.Runtime.LwguiGradient;

namespace LWGUI.LwguiGradientEditor
{
    [System.AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class LwguiGradientUsageAttribute : PropertyAttribute
    {
        public ColorSpace colorSpace;
        public LwguiGradient.ChannelMask viewChannelMask;
        public LwguiGradient.GradientTimeRange timeRange;

        public LwguiGradientUsageAttribute(ColorSpace colorSpace = ColorSpace.Gamma, LwguiGradient.ChannelMask viewChannelMask = LwguiGradient.ChannelMask.All, LwguiGradient.GradientTimeRange timeRange = LwguiGradient.GradientTimeRange.One)
        {
            this.colorSpace = colorSpace;
            this.viewChannelMask = viewChannelMask;
            this.timeRange = timeRange;
        }
    }
    
    
    [CustomPropertyDrawer(typeof(LwguiGradientUsageAttribute))]
    internal sealed class LwguiGradientUsageDrawer : LwguiGradientPropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var colorUsage = (LwguiGradientUsageAttribute)attribute;

            colorSpace = colorUsage.colorSpace;
            viewChannelMask = colorUsage.viewChannelMask;
            timeRange = colorUsage.timeRange;
            base.OnGUI(position, property, label);
        }
    }


    [CustomPropertyDrawer(typeof(LwguiGradient))]
    public class LwguiGradientPropertyDrawer : PropertyDrawer
    {
        public ColorSpace colorSpace = ColorSpace.Gamma;
        public LwguiGradient.ChannelMask viewChannelMask = LwguiGradient.ChannelMask.All;
        public LwguiGradient.GradientTimeRange timeRange = LwguiGradient.GradientTimeRange.One;
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginChangeCheck();
            // var lwguiGradient = property.GetLwguiGradientValue();
            LwguiGradientEditorHelper.GradientField(position, label, property, colorSpace, viewChannelMask, timeRange);
            if (EditorGUI.EndChangeCheck())
            {
                // property.SetLwguiGradientValue(LwguiGradientWindow.instance.lwguiGradient);
                // property.serializedObject.ApplyModifiedProperties();
                // EditorUtility.SetDirty(property.serializedObject.targetObject);
            }
        }
    }
}