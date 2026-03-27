#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.UI.ManagerInspector
{
    [CustomPropertyDrawer(typeof(PropertyLabelAttribute))]
    public sealed class PropertyLabelAttributeDrawer : PropertyDrawer
    {
        private const float RowSpacing = 2f;
        private const float MaxContentWidth = 420f;

        private static readonly GUIStyle s_ComponentLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            clipping = TextClipping.Clip
        };

        private static readonly GUIContent[] s_DefaultXY =
        {
            new GUIContent("X"),
            new GUIContent("Y")
        };

        private static readonly GUIContent[] s_DefaultXYZ =
        {
            new GUIContent("X"),
            new GUIContent("Y"),
            new GUIContent("Z")
        };

        private static readonly GUIContent[] s_DefaultXYZW =
        {
            new GUIContent("X"),
            new GUIContent("Y"),
            new GUIContent("Z"),
            new GUIContent("W")
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attribute = (PropertyLabelAttribute)this.attribute;
            string customLabel = string.IsNullOrWhiteSpace(attribute.DisplayName) ? label.text : attribute.DisplayName;
            var content = new GUIContent(customLabel, label.image, label.tooltip);

            if (attribute.HasCustomAxisLabels && IsSupportedVectorType(property.propertyType))
            {
                DrawVectorWithCustomAxes(position, property, content, attribute);
                return;
            }

            EditorGUI.PropertyField(position, property, content, includeChildren: true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attribute = (PropertyLabelAttribute)this.attribute;
            if (attribute.HasCustomAxisLabels && IsSupportedVectorType(property.propertyType))
            {
                int dimensions = GetDimensions(property.propertyType);
                return (EditorGUIUtility.singleLineHeight * dimensions) + (RowSpacing * (dimensions - 1));
            }

            return EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
        }

        private static void DrawVectorWithCustomAxes(Rect position, SerializedProperty property, GUIContent label, PropertyLabelAttribute attribute)
        {
            int dimensions = GetDimensions(property.propertyType);
            GUIContent[] axisLabels = BuildAxisLabels(attribute, dimensions);

            Rect contentRect = EditorGUI.PrefixLabel(position, label);

            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Vector2:
                {
                    Vector2 value = property.vector2Value;
                    value.x = DrawFloatComponent(contentRect, 0, axisLabels[0], value.x, attribute);
                    value.y = DrawFloatComponent(contentRect, 1, axisLabels[1], value.y, attribute);
                    property.vector2Value = value;
                    break;
                }
                case SerializedPropertyType.Vector2Int:
                {
                    Vector2Int value = property.vector2IntValue;
                    value.x = DrawIntComponent(contentRect, 0, axisLabels[0], value.x, attribute);
                    value.y = DrawIntComponent(contentRect, 1, axisLabels[1], value.y, attribute);
                    property.vector2IntValue = value;
                    break;
                }
                case SerializedPropertyType.Vector3:
                {
                    Vector3 value = property.vector3Value;
                    value.x = DrawFloatComponent(contentRect, 0, axisLabels[0], value.x, attribute);
                    value.y = DrawFloatComponent(contentRect, 1, axisLabels[1], value.y, attribute);
                    value.z = DrawFloatComponent(contentRect, 2, axisLabels[2], value.z, attribute);
                    property.vector3Value = value;
                    break;
                }
                case SerializedPropertyType.Vector3Int:
                {
                    Vector3Int value = property.vector3IntValue;
                    value.x = DrawIntComponent(contentRect, 0, axisLabels[0], value.x, attribute);
                    value.y = DrawIntComponent(contentRect, 1, axisLabels[1], value.y, attribute);
                    value.z = DrawIntComponent(contentRect, 2, axisLabels[2], value.z, attribute);
                    property.vector3IntValue = value;
                    break;
                }
                case SerializedPropertyType.Vector4:
                {
                    Vector4 value = property.vector4Value;
                    value.x = DrawFloatComponent(contentRect, 0, axisLabels[0], value.x, attribute);
                    value.y = DrawFloatComponent(contentRect, 1, axisLabels[1], value.y, attribute);
                    value.z = DrawFloatComponent(contentRect, 2, axisLabels[2], value.z, attribute);
                    value.w = DrawFloatComponent(contentRect, 3, axisLabels[3], value.w, attribute);
                    property.vector4Value = value;
                    break;
                }
                case SerializedPropertyType.Quaternion:
                {
                    Quaternion value = property.quaternionValue;
                    value.x = DrawFloatComponent(contentRect, 0, axisLabels[0], value.x, attribute);
                    value.y = DrawFloatComponent(contentRect, 1, axisLabels[1], value.y, attribute);
                    value.z = DrawFloatComponent(contentRect, 2, axisLabels[2], value.z, attribute);
                    value.w = DrawFloatComponent(contentRect, 3, axisLabels[3], value.w, attribute);
                    property.quaternionValue = value;
                    break;
                }
            }

            EditorGUI.indentLevel = previousIndent;
        }

        private static GUIContent[] BuildAxisLabels(PropertyLabelAttribute attribute, int dimensions)
        {
            if (dimensions <= 1)
            {
                return s_DefaultXY;
            }

            GUIContent[] labels = new GUIContent[dimensions];
            switch (dimensions)
            {
                case 2:
                    labels[0] = new GUIContent(string.IsNullOrWhiteSpace(attribute.XName) ? s_DefaultXY[0].text : attribute.XName);
                    labels[1] = new GUIContent(string.IsNullOrWhiteSpace(attribute.YName) ? s_DefaultXY[1].text : attribute.YName);
                    break;
                case 3:
                    labels[0] = new GUIContent(string.IsNullOrWhiteSpace(attribute.XName) ? s_DefaultXYZ[0].text : attribute.XName);
                    labels[1] = new GUIContent(string.IsNullOrWhiteSpace(attribute.YName) ? s_DefaultXYZ[1].text : attribute.YName);
                    labels[2] = new GUIContent(string.IsNullOrWhiteSpace(attribute.ZName) ? s_DefaultXYZ[2].text : attribute.ZName);
                    break;
                default:
                    labels[0] = new GUIContent(string.IsNullOrWhiteSpace(attribute.XName) ? s_DefaultXYZW[0].text : attribute.XName);
                    labels[1] = new GUIContent(string.IsNullOrWhiteSpace(attribute.YName) ? s_DefaultXYZW[1].text : attribute.YName);
                    labels[2] = new GUIContent(string.IsNullOrWhiteSpace(attribute.ZName) ? s_DefaultXYZW[2].text : attribute.ZName);
                    labels[3] = new GUIContent(string.IsNullOrWhiteSpace(attribute.WName) ? s_DefaultXYZW[3].text : attribute.WName);
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

        private static float DrawFloatComponent(Rect contentRect, int rowIndex, GUIContent axisLabel, float value, PropertyLabelAttribute attribute)
        {
            Rect componentRect = BuildComponentRowRect(contentRect, rowIndex);
            BuildLabelAndFieldRects(componentRect, out var axisRect, out var fieldRect);

            EditorGUI.LabelField(axisRect, axisLabel, s_ComponentLabelStyle);
            if (attribute.UseRange)
            {
                return EditorGUI.Slider(fieldRect, value, attribute.RangeMin, attribute.RangeMax);
            }

            return EditorGUI.FloatField(fieldRect, value);
        }

        private static int DrawIntComponent(Rect contentRect, int rowIndex, GUIContent axisLabel, int value, PropertyLabelAttribute attribute)
        {
            Rect componentRect = BuildComponentRowRect(contentRect, rowIndex);
            BuildLabelAndFieldRects(componentRect, out var axisRect, out var fieldRect);

            EditorGUI.LabelField(axisRect, axisLabel, s_ComponentLabelStyle);
            if (attribute.UseRange)
            {
                return EditorGUI.IntSlider(fieldRect, value, Mathf.RoundToInt(attribute.RangeMin), Mathf.RoundToInt(attribute.RangeMax));
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
#endif
