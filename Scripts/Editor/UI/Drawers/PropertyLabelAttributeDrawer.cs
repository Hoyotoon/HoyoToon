using HoyoToon.Runtime.Core;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.UI.Drawers
{
    [CustomPropertyDrawer(typeof(PropertyLabelAttribute))]
    internal sealed class PropertyLabelAttributeDrawer : PropertyDrawer
    {
        private const float RowSpacing = 2f;
        private const float MaxContentWidth = 420f;

        private static readonly GUIStyle ComponentLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            clipping = TextClipping.Clip
        };

        private static readonly GUIContent[] DefaultXY =
        {
            new GUIContent("X"),
            new GUIContent("Y")
        };

        private static readonly GUIContent[] DefaultXYZ =
        {
            new GUIContent("X"),
            new GUIContent("Y"),
            new GUIContent("Z")
        };

        private static readonly GUIContent[] DefaultXYZW =
        {
            new GUIContent("X"),
            new GUIContent("Y"),
            new GUIContent("Z"),
            new GUIContent("W")
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PropertyLabelAttribute propertyLabelAttribute = (PropertyLabelAttribute)attribute;
            string displayName = string.IsNullOrWhiteSpace(propertyLabelAttribute.DisplayName) ? label.text : propertyLabelAttribute.DisplayName;
            GUIContent content = new GUIContent(displayName, label.image, label.tooltip);

            if (propertyLabelAttribute.HasCustomAxisLabels && IsSupportedVectorType(property.propertyType))
            {
                DrawVectorWithCustomAxes(position, property, content, propertyLabelAttribute);
                return;
            }

            EditorGUI.PropertyField(position, property, content, includeChildren: true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            PropertyLabelAttribute propertyLabelAttribute = (PropertyLabelAttribute)attribute;
            if (propertyLabelAttribute.HasCustomAxisLabels && IsSupportedVectorType(property.propertyType))
            {
                int dimensions = GetDimensions(property.propertyType);
                return (EditorGUIUtility.singleLineHeight * dimensions) + (RowSpacing * (dimensions - 1));
            }

            return EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
        }

        private static void DrawVectorWithCustomAxes(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            PropertyLabelAttribute propertyLabelAttribute)
        {
            int dimensions = GetDimensions(property.propertyType);
            GUIContent[] axisLabels = BuildAxisLabels(propertyLabelAttribute, dimensions);

            Rect contentRect = EditorGUI.PrefixLabel(position, label);

            int previousIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Vector2:
                {
                    Vector2 value = property.vector2Value;
                    value.x = DrawFloatComponent(contentRect, 0, axisLabels[0], value.x, propertyLabelAttribute);
                    value.y = DrawFloatComponent(contentRect, 1, axisLabels[1], value.y, propertyLabelAttribute);
                    property.vector2Value = value;
                    break;
                }
                case SerializedPropertyType.Vector2Int:
                {
                    Vector2Int value = property.vector2IntValue;
                    value.x = DrawIntComponent(contentRect, 0, axisLabels[0], value.x, propertyLabelAttribute);
                    value.y = DrawIntComponent(contentRect, 1, axisLabels[1], value.y, propertyLabelAttribute);
                    property.vector2IntValue = value;
                    break;
                }
                case SerializedPropertyType.Vector3:
                {
                    Vector3 value = property.vector3Value;
                    value.x = DrawFloatComponent(contentRect, 0, axisLabels[0], value.x, propertyLabelAttribute);
                    value.y = DrawFloatComponent(contentRect, 1, axisLabels[1], value.y, propertyLabelAttribute);
                    value.z = DrawFloatComponent(contentRect, 2, axisLabels[2], value.z, propertyLabelAttribute);
                    property.vector3Value = value;
                    break;
                }
                case SerializedPropertyType.Vector3Int:
                {
                    Vector3Int value = property.vector3IntValue;
                    value.x = DrawIntComponent(contentRect, 0, axisLabels[0], value.x, propertyLabelAttribute);
                    value.y = DrawIntComponent(contentRect, 1, axisLabels[1], value.y, propertyLabelAttribute);
                    value.z = DrawIntComponent(contentRect, 2, axisLabels[2], value.z, propertyLabelAttribute);
                    property.vector3IntValue = value;
                    break;
                }
                case SerializedPropertyType.Vector4:
                {
                    Vector4 value = property.vector4Value;
                    value.x = DrawFloatComponent(contentRect, 0, axisLabels[0], value.x, propertyLabelAttribute);
                    value.y = DrawFloatComponent(contentRect, 1, axisLabels[1], value.y, propertyLabelAttribute);
                    value.z = DrawFloatComponent(contentRect, 2, axisLabels[2], value.z, propertyLabelAttribute);
                    value.w = DrawFloatComponent(contentRect, 3, axisLabels[3], value.w, propertyLabelAttribute);
                    property.vector4Value = value;
                    break;
                }
                case SerializedPropertyType.Quaternion:
                {
                    Quaternion value = property.quaternionValue;
                    value.x = DrawFloatComponent(contentRect, 0, axisLabels[0], value.x, propertyLabelAttribute);
                    value.y = DrawFloatComponent(contentRect, 1, axisLabels[1], value.y, propertyLabelAttribute);
                    value.z = DrawFloatComponent(contentRect, 2, axisLabels[2], value.z, propertyLabelAttribute);
                    value.w = DrawFloatComponent(contentRect, 3, axisLabels[3], value.w, propertyLabelAttribute);
                    property.quaternionValue = value;
                    break;
                }
            }

            EditorGUI.indentLevel = previousIndentLevel;
        }

        private static GUIContent[] BuildAxisLabels(PropertyLabelAttribute propertyLabelAttribute, int dimensions)
        {
            if (dimensions <= 1)
            {
                return DefaultXY;
            }

            GUIContent[] labels = new GUIContent[dimensions];
            switch (dimensions)
            {
                case 2:
                    labels[0] = new GUIContent(string.IsNullOrWhiteSpace(propertyLabelAttribute.XName) ? DefaultXY[0].text : propertyLabelAttribute.XName);
                    labels[1] = new GUIContent(string.IsNullOrWhiteSpace(propertyLabelAttribute.YName) ? DefaultXY[1].text : propertyLabelAttribute.YName);
                    break;
                case 3:
                    labels[0] = new GUIContent(string.IsNullOrWhiteSpace(propertyLabelAttribute.XName) ? DefaultXYZ[0].text : propertyLabelAttribute.XName);
                    labels[1] = new GUIContent(string.IsNullOrWhiteSpace(propertyLabelAttribute.YName) ? DefaultXYZ[1].text : propertyLabelAttribute.YName);
                    labels[2] = new GUIContent(string.IsNullOrWhiteSpace(propertyLabelAttribute.ZName) ? DefaultXYZ[2].text : propertyLabelAttribute.ZName);
                    break;
                default:
                    labels[0] = new GUIContent(string.IsNullOrWhiteSpace(propertyLabelAttribute.XName) ? DefaultXYZW[0].text : propertyLabelAttribute.XName);
                    labels[1] = new GUIContent(string.IsNullOrWhiteSpace(propertyLabelAttribute.YName) ? DefaultXYZW[1].text : propertyLabelAttribute.YName);
                    labels[2] = new GUIContent(string.IsNullOrWhiteSpace(propertyLabelAttribute.ZName) ? DefaultXYZW[2].text : propertyLabelAttribute.ZName);
                    labels[3] = new GUIContent(string.IsNullOrWhiteSpace(propertyLabelAttribute.WName) ? DefaultXYZW[3].text : propertyLabelAttribute.WName);
                    break;
            }

            return labels;
        }

        private static bool IsSupportedVectorType(SerializedPropertyType propertyType)
        {
            return propertyType == SerializedPropertyType.Vector2
                || propertyType == SerializedPropertyType.Vector2Int
                || propertyType == SerializedPropertyType.Vector3
                || propertyType == SerializedPropertyType.Vector3Int
                || propertyType == SerializedPropertyType.Vector4
                || propertyType == SerializedPropertyType.Quaternion;
        }

        private static int GetDimensions(SerializedPropertyType propertyType)
        {
            switch (propertyType)
            {
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector2Int:
                    return 2;
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector3Int:
                    return 3;
                default:
                    return 4;
            }
        }

        private static float DrawFloatComponent(
            Rect contentRect,
            int rowIndex,
            GUIContent axisLabel,
            float value,
            PropertyLabelAttribute propertyLabelAttribute)
        {
            Rect componentRect = BuildComponentRowRect(contentRect, rowIndex);
            BuildLabelAndFieldRects(componentRect, out Rect axisRect, out Rect fieldRect);

            EditorGUI.LabelField(axisRect, axisLabel, ComponentLabelStyle);
            if (propertyLabelAttribute.UseRange)
            {
                return EditorGUI.Slider(fieldRect, value, propertyLabelAttribute.RangeMin, propertyLabelAttribute.RangeMax);
            }

            return EditorGUI.FloatField(fieldRect, value);
        }

        private static int DrawIntComponent(
            Rect contentRect,
            int rowIndex,
            GUIContent axisLabel,
            int value,
            PropertyLabelAttribute propertyLabelAttribute)
        {
            Rect componentRect = BuildComponentRowRect(contentRect, rowIndex);
            BuildLabelAndFieldRects(componentRect, out Rect axisRect, out Rect fieldRect);

            EditorGUI.LabelField(axisRect, axisLabel, ComponentLabelStyle);
            if (propertyLabelAttribute.UseRange)
            {
                return EditorGUI.IntSlider(
                    fieldRect,
                    value,
                    Mathf.RoundToInt(propertyLabelAttribute.RangeMin),
                    Mathf.RoundToInt(propertyLabelAttribute.RangeMax));
            }

            return EditorGUI.IntField(fieldRect, value);
        }

        private static Rect BuildComponentRowRect(Rect contentRect, int rowIndex)
        {
            Rect centeredRect = BuildCenteredContentRect(contentRect);
            float y = contentRect.y + (rowIndex * (EditorGUIUtility.singleLineHeight + RowSpacing));
            return new Rect(centeredRect.x, y, centeredRect.width, EditorGUIUtility.singleLineHeight);
        }

        private static Rect BuildCenteredContentRect(Rect contentRect)
        {
            float width = Mathf.Min(MaxContentWidth, contentRect.width);
            float x = contentRect.x + ((contentRect.width - width) * 0.5f);
            return new Rect(x, contentRect.y, width, contentRect.height);
        }

        private static void BuildLabelAndFieldRects(Rect componentRect, out Rect labelRect, out Rect fieldRect)
        {
            float maxLabelWidth = Mathf.Max(8f, componentRect.width - 16f);
            float labelWidth = Mathf.Clamp(componentRect.width * 0.45f, 10f, maxLabelWidth);

            labelRect = new Rect(componentRect.x, componentRect.y, labelWidth, componentRect.height);
            fieldRect = new Rect(componentRect.x + labelWidth, componentRect.y, componentRect.width - labelWidth, componentRect.height);
        }
    }
}
