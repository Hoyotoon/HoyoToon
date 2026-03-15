#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Onboarding;

namespace HoyoToon.Editor.UI.ManagerInspector.Modules
{
    internal abstract class DynamicControllerModule : ManagerModule
    {
        private readonly Dictionary<string, bool> _sectionFoldouts = new Dictionary<string, bool>(StringComparer.Ordinal);

        private MonoBehaviour _controller;
        private SerializedObject _controllerSerializedObject;

        protected SerializedObject ControllerSerializedObject => _controllerSerializedObject;

        protected abstract string ControllerTypeSuffix { get; }

        protected abstract string ControllerInterfaceSuffix { get; }

        protected abstract string PreferredRuntimeNamespace { get; }

        protected abstract string EmptyPropertiesMessage { get; }

        protected abstract IReadOnlyList<string> PreferredSectionOrder { get; }

        public override void Dispose()
        {
            _controller = null;
            _controllerSerializedObject = null;
            _sectionFoldouts.Clear();
            base.Dispose();
        }

        protected void EnsureSerializedObject(MonoBehaviour controller)
        {
            if (controller == null)
            {
                _controller = null;
                _controllerSerializedObject = null;
                return;
            }

            if (_controllerSerializedObject != null && _controller == controller && _controllerSerializedObject.targetObject == controller)
            {
                return;
            }

            _controller = controller;
            _controllerSerializedObject = new SerializedObject(controller);
        }

        protected MonoBehaviour FindBestControllerInHierarchy(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            MonoBehaviour best = null;
            int bestScore = int.MinValue;
            var candidates = root.GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (!IsCompatibleController(candidate))
                {
                    continue;
                }

                int score = ScoreController(candidate.GetType());
                if (best == null || score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        protected bool IsCompatibleController(MonoBehaviour component)
        {
            if (component == null)
            {
                return false;
            }

            var type = component.GetType();
            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                return false;
            }

            if (type.Name.EndsWith(ControllerTypeSuffix, StringComparison.Ordinal))
            {
                return true;
            }

            var interfaces = type.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                string interfaceName = interfaces[i].Name;
                if (interfaceName.EndsWith(ControllerTypeSuffix, StringComparison.Ordinal)
                    || interfaceName.EndsWith(ControllerInterfaceSuffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        protected int ScoreController(Type type)
        {
            int score = 0;

            if (type.Name.EndsWith(ControllerTypeSuffix, StringComparison.Ordinal))
            {
                score += 100;
            }

            string ns = type.Namespace ?? string.Empty;
            if (ns.IndexOf(PreferredRuntimeNamespace, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 50;
            }

            if (type.GetMethod("SyncToRenderer", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) != null)
            {
                score += 25;
            }

            return score;
        }

        protected void DrawDynamicPropertyGroups()
        {
            var grouped = BuildGroupedProperties();
            if (grouped.Count == 0)
            {
                EditorGUILayout.HelpBox(EmptyPropertiesMessage, MessageType.Info);
                return;
            }

            foreach (var sectionName in GetSectionOrder(grouped))
            {
                if (!grouped.TryGetValue(sectionName, out var properties) || properties.Count == 0)
                {
                    continue;
                }

                bool expanded = DrawSectionFoldout(sectionName, IsDefaultSectionExpanded(sectionName), () =>
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var property in properties)
                        {
                            if (property == null)
                            {
                                continue;
                            }

                            float propertyHeight = EditorGUI.GetPropertyHeight(property, true);
                            Rect propertyRect = EditorGUILayout.GetControlRect(true, propertyHeight);
                            EditorGUI.PropertyField(propertyRect, property, true);

                            string tourTarget = ResolveTourTarget(property);
                            if (!string.IsNullOrEmpty(tourTarget))
                            {
                                TourOverlay.DrawHighlightIfActive(tourTarget, propertyRect, property.displayName);
                            }

                            OnPropertyValueObserved(property);
                        }
                    }
                });

                _sectionFoldouts[sectionName] = expanded;
                GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            }
        }

        protected Dictionary<string, List<SerializedProperty>> BuildGroupedProperties()
        {
            var grouped = new Dictionary<string, List<SerializedProperty>>(StringComparer.OrdinalIgnoreCase);
            var iterator = _controllerSerializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (ShouldSkipProperty(iterator))
                {
                    continue;
                }

                string groupName = ResolveGroupName(iterator);
                if (!grouped.TryGetValue(groupName, out var properties))
                {
                    properties = new List<SerializedProperty>();
                    grouped[groupName] = properties;
                }

                properties.Add(iterator.Copy());
            }

            return grouped;
        }

        protected bool DrawSectionFoldout(string title, bool defaultExpanded, Action drawer)
        {
            if (!_sectionFoldouts.TryGetValue(title, out var expanded))
            {
                expanded = defaultExpanded;
            }

            if (ShouldForceSectionExpanded(title))
            {
                expanded = true;
            }

            expanded = DrawFoldoutSection(title, expanded, drawer, 8f);
            return expanded;
        }

        protected static bool ShouldSkipProperty(SerializedProperty property)
        {
            if (property == null)
            {
                return true;
            }

            if (string.Equals(property.name, "m_Script", StringComparison.Ordinal))
            {
                return true;
            }

            if (property.name.StartsWith("m_", StringComparison.Ordinal))
            {
                return true;
            }

            if (property.name.IndexOf("k__BackingField", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (string.Equals(property.name, "renderers", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        protected IReadOnlyList<string> GetSectionOrder(IReadOnlyDictionary<string, List<SerializedProperty>> grouped)
        {
            var ordered = new List<string>(grouped.Count);

            for (int i = 0; i < PreferredSectionOrder.Count; i++)
            {
                string sectionName = PreferredSectionOrder[i];
                if (grouped.ContainsKey(sectionName))
                {
                    ordered.Add(sectionName);
                }
            }

            foreach (var key in grouped.Keys)
            {
                if (!ordered.Contains(key))
                {
                    ordered.Add(key);
                }
            }

            return ordered;
        }

        protected static bool ContainsAny(string name, string displayName, params string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                    || displayName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        protected static void TrySyncController(MonoBehaviour controller)
        {
            if (controller == null)
            {
                return;
            }

            try
            {
                var method = controller.GetType().GetMethod("SyncToRenderer", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    method.Invoke(controller, null);
                }
            }
            catch (Exception)
            {
            }
        }

        protected virtual bool IsDefaultSectionExpanded(string sectionName)
        {
            return string.Equals(sectionName, "General", StringComparison.OrdinalIgnoreCase);
        }

        protected virtual bool ShouldForceSectionExpanded(string sectionName)
        {
            return false;
        }

        protected virtual string ResolveTourTarget(SerializedProperty property)
        {
            return null;
        }

        protected virtual void OnPropertyValueObserved(SerializedProperty property)
        {
        }

        protected abstract string ResolveGroupName(SerializedProperty property);
    }
}
#endif
