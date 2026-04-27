#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Runtime.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HoyoToon.Editor.Utilities.UI
{
    internal static class SerializedControllerInspectorUtility
    {
        internal readonly struct ControllerLayoutSection
        {
            public readonly string Title;
            public readonly IReadOnlyList<FieldInfo> Fields;

            public ControllerLayoutSection(string title, IReadOnlyList<FieldInfo> fields)
            {
                Title = string.IsNullOrWhiteSpace(title) ? "General" : title;
                Fields = fields ?? Array.Empty<FieldInfo>();
            }
        }

        internal sealed class FieldClassNames
        {
            public string FieldClass = "ht-character-controller-field";
            public string ToggleClass = "ht-character-controller-toggle";
            public string ObjectFieldClass;
            public string CollectionClass = "ht-character-controller-field--collection";
            public string CompositeClass = "ht-character-controller-composite";
            public string CompositeLabelClass = "ht-character-controller-composite-label";
        }

        internal static IReadOnlyList<ControllerLayoutSection> BuildLayoutSections(
            Type controllerType,
            IReadOnlyList<string> preferredSectionOrder,
            Func<FieldInfo, bool> includeField,
            Func<Type, FieldInfo, string> sectionOverride = null)
        {
            if (controllerType == null)
            {
                return Array.Empty<ControllerLayoutSection>();
            }

            Dictionary<string, List<FieldInfo>> fieldsBySection = new Dictionary<string, List<FieldInfo>>();
            List<string> sectionOrder = new List<string>();

            Type current = controllerType;
            while (current != null && current != typeof(MonoBehaviour) && current != typeof(Component) && current != typeof(object))
            {
                FieldInfo[] declaredFields = current.GetFields(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly);

                string activeHeader = string.Empty;
                for (int fieldIndex = 0; fieldIndex < declaredFields.Length; fieldIndex++)
                {
                    FieldInfo field = declaredFields[fieldIndex];
                    if (includeField != null ? !includeField(field) : !IsInspectableField(field))
                    {
                        continue;
                    }

                    string sectionTitle = sectionOverride != null ? sectionOverride(controllerType, field) : string.Empty;
                    if (string.IsNullOrWhiteSpace(sectionTitle))
                    {
                        sectionTitle = GetFieldSectionTitle(field, ref activeHeader);
                    }

                    if (!fieldsBySection.TryGetValue(sectionTitle, out List<FieldInfo> sectionFields))
                    {
                        sectionFields = new List<FieldInfo>();
                        fieldsBySection[sectionTitle] = sectionFields;
                        sectionOrder.Add(sectionTitle);
                    }

                    sectionFields.Add(field);
                }

                current = current.BaseType;
            }

            List<ControllerLayoutSection> sections = new List<ControllerLayoutSection>();
            for (int sectionIndex = 0; sectionIndex < sectionOrder.Count; sectionIndex++)
            {
                string title = sectionOrder[sectionIndex];
                if (fieldsBySection.TryGetValue(title, out List<FieldInfo> fields) && fields.Count > 0)
                {
                    sections.Add(new ControllerLayoutSection(title, fields.ToArray()));
                }
            }

            return sections
                .Select((section, index) => new
                {
                    Section = section,
                    OriginalIndex = index,
                    Priority = GetSectionPriority(section.Title, preferredSectionOrder)
                })
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.OriginalIndex)
                .Select(item => item.Section)
                .ToList();
        }

        internal static bool IsInspectableField(FieldInfo field)
        {
            if (field == null)
            {
                return false;
            }

            if (field.IsStatic || field.IsLiteral || field.IsInitOnly)
            {
                return false;
            }

            if (field.IsDefined(typeof(HideInInspector), false) || field.IsDefined(typeof(NonSerializedAttribute), false))
            {
                return false;
            }

            return field.IsPublic || field.IsDefined(typeof(SerializeField), false);
        }

        internal static string GetFieldSectionTitle(FieldInfo field, ref string activeHeader)
        {
            HeaderAttribute header = field.GetCustomAttribute<HeaderAttribute>();
            if (header != null && !string.IsNullOrWhiteSpace(header.header))
            {
                activeHeader = header.header.Trim();
                return activeHeader;
            }

            return string.IsNullOrWhiteSpace(activeHeader) ? "General" : activeHeader;
        }

        internal static VisualElement CreateFieldElement(
            FieldInfo field,
            SerializedProperty property,
            FieldClassNames classNames = null)
        {
            if (field == null || property == null)
            {
                return null;
            }

            classNames ??= new FieldClassNames();

            PropertyLabelAttribute propertyLabel = field.GetCustomAttribute<PropertyLabelAttribute>();
            RangeAttribute rangeAttribute = field.GetCustomAttribute<RangeAttribute>();
            TooltipAttribute tooltipAttribute = field.GetCustomAttribute<TooltipAttribute>();

            string label = ResolveFieldLabel(property, propertyLabel);
            string tooltip = tooltipAttribute != null ? tooltipAttribute.tooltip ?? string.Empty : string.Empty;

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                return CreateFallbackPropertyField(property, label, classNames);
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return CreateToggleField(property, label, tooltip, classNames);
                case SerializedPropertyType.Integer:
                    return CreateIntegerField(property, label, tooltip, rangeAttribute, propertyLabel, classNames);
                case SerializedPropertyType.Float:
                    return CreateFloatField(property, label, tooltip, rangeAttribute, propertyLabel, classNames);
                case SerializedPropertyType.Color:
                    return CreateColorField(property, label, tooltip, classNames);
                case SerializedPropertyType.Enum:
                    return CreateEnumField(field, property, label, tooltip, classNames);
                case SerializedPropertyType.ObjectReference:
                    return CreateObjectField(field, property, label, tooltip, classNames);
                case SerializedPropertyType.Vector2:
                    return CreateVector2Field(property, label, tooltip, propertyLabel, classNames);
                case SerializedPropertyType.Vector2Int:
                    return CreateVector2IntField(property, label, tooltip, propertyLabel, classNames);
                case SerializedPropertyType.Vector3:
                    return CreateVector3Field(property, label, tooltip, propertyLabel, classNames);
                case SerializedPropertyType.Vector3Int:
                    return CreateVector3IntField(property, label, tooltip, propertyLabel, classNames);
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Quaternion:
                    return CreateCompositeVectorField(property, label, tooltip, propertyLabel, classNames);
                default:
                    return CreateFallbackPropertyField(property, label, classNames);
            }
        }

        internal static string ResolveFieldLabel(SerializedProperty property, PropertyLabelAttribute propertyLabel)
        {
            if (propertyLabel != null && !string.IsNullOrWhiteSpace(propertyLabel.DisplayName))
            {
                return propertyLabel.DisplayName;
            }

            return property != null ? property.displayName : string.Empty;
        }

        internal static void RegisterValueChangeSignals(VisualElement element, string targetId)
        {
            if (element == null || string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            element.RegisterCallback<ChangeEvent<bool>>(evt => OnboardingSignals.RecordRestorableValueChanged(targetId, evt.previousValue, evt.newValue));
            element.RegisterCallback<ChangeEvent<int>>(evt => OnboardingSignals.RecordRestorableValueChanged(targetId, evt.previousValue, evt.newValue));
            element.RegisterCallback<ChangeEvent<float>>(evt => OnboardingSignals.RecordRestorableValueChanged(targetId, evt.previousValue, evt.newValue));
            element.RegisterCallback<ChangeEvent<Color>>(evt => OnboardingSignals.RecordRestorableValueChanged(targetId, evt.previousValue, evt.newValue));
            element.RegisterCallback<ChangeEvent<Vector2>>(evt => OnboardingSignals.RecordRestorableValueChanged(targetId, evt.previousValue, evt.newValue));
            element.RegisterCallback<ChangeEvent<Vector3>>(evt => OnboardingSignals.RecordRestorableValueChanged(targetId, evt.previousValue, evt.newValue));
            element.RegisterCallback<ChangeEvent<string>>(evt => OnboardingSignals.RecordRestorableValueChanged(targetId, evt.previousValue, evt.newValue));
        }

        internal static bool ContainsAny(string value, params string[] terms)
        {
            if (string.IsNullOrWhiteSpace(value) || terms == null)
            {
                return false;
            }

            for (int index = 0; index < terms.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(terms[index])
                    && value.IndexOf(terms[index], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ContainsAll(string value, params string[] terms)
        {
            if (string.IsNullOrWhiteSpace(value) || terms == null)
            {
                return false;
            }

            for (int index = 0; index < terms.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(terms[index])
                    && value.IndexOf(terms[index], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetSectionPriority(string sectionTitle, IReadOnlyList<string> preferredSectionOrder)
        {
            if (preferredSectionOrder == null)
            {
                return int.MaxValue;
            }

            for (int index = 0; index < preferredSectionOrder.Count; index++)
            {
                if (string.Equals(sectionTitle, preferredSectionOrder[index], StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return preferredSectionOrder.Count;
        }

        private static VisualElement CreateToggleField(SerializedProperty property, string label, string tooltip, FieldClassNames classNames)
        {
            Toggle toggle = new Toggle(label)
            {
                tooltip = tooltip
            };
            toggle.BindProperty(property);
            AddClass(toggle, "ht-toggle");
            AddClass(toggle, classNames.ToggleClass);
            return toggle;
        }

        private static VisualElement CreateIntegerField(
            SerializedProperty property,
            string label,
            string tooltip,
            RangeAttribute rangeAttribute,
            PropertyLabelAttribute propertyLabel,
            FieldClassNames classNames)
        {
            if (rangeAttribute != null || (propertyLabel != null && propertyLabel.UseRange))
            {
                int min = rangeAttribute != null ? Mathf.RoundToInt(rangeAttribute.min) : Mathf.RoundToInt(propertyLabel.RangeMin);
                int max = rangeAttribute != null ? Mathf.RoundToInt(rangeAttribute.max) : Mathf.RoundToInt(propertyLabel.RangeMax);
                SliderInt slider = new SliderInt(label, min, max)
                {
                    tooltip = tooltip,
                    showInputField = true
                };
                slider.BindProperty(property);
                AddFieldClasses(slider, classNames);
                return slider;
            }

            IntegerField integerField = new IntegerField(label)
            {
                tooltip = tooltip
            };
            integerField.BindProperty(property);
            AddFieldClasses(integerField, classNames);
            return integerField;
        }

        private static VisualElement CreateFloatField(
            SerializedProperty property,
            string label,
            string tooltip,
            RangeAttribute rangeAttribute,
            PropertyLabelAttribute propertyLabel,
            FieldClassNames classNames)
        {
            if (rangeAttribute != null || (propertyLabel != null && propertyLabel.UseRange))
            {
                float min = rangeAttribute != null ? rangeAttribute.min : propertyLabel.RangeMin;
                float max = rangeAttribute != null ? rangeAttribute.max : propertyLabel.RangeMax;
                Slider slider = new Slider(label, min, max)
                {
                    tooltip = tooltip,
                    showInputField = true
                };
                slider.BindProperty(property);
                AddFieldClasses(slider, classNames);
                return slider;
            }

            FloatField floatField = new FloatField(label)
            {
                tooltip = tooltip
            };
            floatField.BindProperty(property);
            AddFieldClasses(floatField, classNames);
            return floatField;
        }

        private static VisualElement CreateColorField(SerializedProperty property, string label, string tooltip, FieldClassNames classNames)
        {
            ColorField colorField = new ColorField(label)
            {
                tooltip = tooltip
            };
            colorField.BindProperty(property);
            AddFieldClasses(colorField, classNames);
            return colorField;
        }

        private static VisualElement CreateEnumField(FieldInfo field, SerializedProperty property, string label, string tooltip, FieldClassNames classNames)
        {
            if (field == null || field.FieldType == null || !field.FieldType.IsEnum)
            {
                return CreateFallbackPropertyField(property, label, classNames);
            }

            Enum enumValue = (Enum)Enum.ToObject(field.FieldType, property.intValue);
            EnumField enumField = new EnumField(label, enumValue)
            {
                tooltip = tooltip
            };
            enumField.BindProperty(property);
            AddFieldClasses(enumField, classNames);
            return enumField;
        }

        private static VisualElement CreateObjectField(FieldInfo field, SerializedProperty property, string label, string tooltip, FieldClassNames classNames)
        {
            ObjectField objectField = new ObjectField(label)
            {
                tooltip = tooltip,
                objectType = field != null ? field.FieldType : typeof(UnityEngine.Object),
                allowSceneObjects = true
            };
            objectField.BindProperty(property);
            AddFieldClasses(objectField, classNames);
            AddClass(objectField, classNames.ObjectFieldClass);
            return objectField;
        }

        private static VisualElement CreateVector2Field(SerializedProperty property, string label, string tooltip, PropertyLabelAttribute propertyLabel, FieldClassNames classNames)
        {
            if (propertyLabel != null && propertyLabel.HasCustomAxisLabels)
            {
                return CreateCompositeVectorField(property, label, tooltip, propertyLabel, classNames);
            }

            Vector2Field vectorField = new Vector2Field(label) { tooltip = tooltip };
            vectorField.BindProperty(property);
            AddFieldClasses(vectorField, classNames);
            return vectorField;
        }

        private static VisualElement CreateVector2IntField(SerializedProperty property, string label, string tooltip, PropertyLabelAttribute propertyLabel, FieldClassNames classNames)
        {
            if (propertyLabel != null && propertyLabel.HasCustomAxisLabels)
            {
                return CreateCompositeVectorField(property, label, tooltip, propertyLabel, classNames);
            }

            Vector2IntField vectorField = new Vector2IntField(label) { tooltip = tooltip };
            vectorField.BindProperty(property);
            AddFieldClasses(vectorField, classNames);
            return vectorField;
        }

        private static VisualElement CreateVector3Field(SerializedProperty property, string label, string tooltip, PropertyLabelAttribute propertyLabel, FieldClassNames classNames)
        {
            if (propertyLabel != null && propertyLabel.HasCustomAxisLabels)
            {
                return CreateCompositeVectorField(property, label, tooltip, propertyLabel, classNames);
            }

            Vector3Field vectorField = new Vector3Field(label) { tooltip = tooltip };
            vectorField.BindProperty(property);
            AddFieldClasses(vectorField, classNames);
            return vectorField;
        }

        private static VisualElement CreateVector3IntField(SerializedProperty property, string label, string tooltip, PropertyLabelAttribute propertyLabel, FieldClassNames classNames)
        {
            if (propertyLabel != null && propertyLabel.HasCustomAxisLabels)
            {
                return CreateCompositeVectorField(property, label, tooltip, propertyLabel, classNames);
            }

            Vector3IntField vectorField = new Vector3IntField(label) { tooltip = tooltip };
            vectorField.BindProperty(property);
            AddFieldClasses(vectorField, classNames);
            return vectorField;
        }

        private static VisualElement CreateCompositeVectorField(
            SerializedProperty property,
            string label,
            string tooltip,
            PropertyLabelAttribute propertyLabel,
            FieldClassNames classNames)
        {
            VisualElement root = new VisualElement();
            AddClass(root, classNames.CompositeClass);
            AddClass(root, "ht-column");
            AddClass(root, "ht-gap-4");

            Label title = new Label(label)
            {
                tooltip = tooltip
            };
            AddClass(title, classNames.CompositeLabelClass);
            root.Add(title);

            string[] componentNames = GetComponentNames(property.propertyType);
            string[] componentLabels = GetComponentLabels(propertyLabel, componentNames.Length);
            for (int index = 0; index < componentNames.Length; index++)
            {
                SerializedProperty componentProperty = property.FindPropertyRelative(componentNames[index]);
                if (componentProperty == null)
                {
                    continue;
                }

                VisualElement componentField = componentProperty.propertyType == SerializedPropertyType.Integer
                    ? CreateIntegerField(componentProperty, componentLabels[index], tooltip, null, propertyLabel, classNames)
                    : CreateFloatField(componentProperty, componentLabels[index], tooltip, null, propertyLabel, classNames);

                if (componentField != null)
                {
                    root.Add(componentField);
                }
            }

            return root;
        }

        private static string[] GetComponentNames(SerializedPropertyType propertyType)
        {
            switch (propertyType)
            {
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector2Int:
                    return new[] { "x", "y" };
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector3Int:
                    return new[] { "x", "y", "z" };
                default:
                    return new[] { "x", "y", "z", "w" };
            }
        }

        private static string[] GetComponentLabels(PropertyLabelAttribute propertyLabel, int count)
        {
            string[] defaults = count == 2
                ? new[] { "X", "Y" }
                : count == 3
                    ? new[] { "X", "Y", "Z" }
                    : new[] { "X", "Y", "Z", "W" };

            if (propertyLabel == null)
            {
                return defaults;
            }

            string[] values =
            {
                propertyLabel.XName,
                propertyLabel.YName,
                propertyLabel.ZName,
                propertyLabel.WName
            };

            for (int index = 0; index < count; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    defaults[index] = values[index];
                }
            }

            return defaults;
        }

        private static VisualElement CreateFallbackPropertyField(SerializedProperty property, string label, FieldClassNames classNames)
        {
            bool hideOuterLabel = property != null && property.isArray && property.propertyType != SerializedPropertyType.String;
            PropertyField propertyField = new PropertyField(property, hideOuterLabel ? string.Empty : label);
            AddClass(propertyField, classNames.FieldClass);
            if (hideOuterLabel)
            {
                AddClass(propertyField, classNames.CollectionClass);
            }

            propertyField.BindProperty(property);
            return propertyField;
        }

        private static void AddFieldClasses(VisualElement element, FieldClassNames classNames)
        {
            AddClass(element, "ht-field");
            AddClass(element, classNames.FieldClass);
        }

        private static void AddClass(VisualElement element, string className)
        {
            if (element != null && !string.IsNullOrWhiteSpace(className))
            {
                element.AddToClassList(className);
            }
        }
    }
}
#endif
