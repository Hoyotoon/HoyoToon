#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.UI.ManagerInspector.Modules;
using HoyoToon.Editor.Onboarding;

namespace HoyoToon.Editor.UI.ManagerInspector.Components
{
    internal sealed class ManagerNavbar : IDisposable
    {
        private static class Styles
        {
            public static readonly GUIStyle ToolbarButton;

            static Styles()
            {
                ToolbarButton = new GUIStyle(EditorStyles.toolbarButton)
                {
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private readonly List<ManagerModule> _modules = new List<ManagerModule>();
        private int _selectedIndex;

        public int SelectedIndex => _modules.Count == 0 ? -1 : Mathf.Clamp(_selectedIndex, 0, _modules.Count - 1);

        public void RegisterModule(ManagerModule module)
        {
            if (module == null || _modules.Contains(module))
            {
                return;
            }

            _modules.Add(module);
        }

        public void Draw()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);
                DrawToolbar();
            }
        }

        public ManagerModule GetSelectedModule()
        {
            if (_modules.Count == 0)
            {
                return null;
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _modules.Count - 1);
            return _modules[_selectedIndex];
        }

        public bool SelectModuleByDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName) || _modules.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                if (string.Equals(module?.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    if (_selectedIndex != i)
                    {
                        _selectedIndex = i;
                        GUI.FocusControl(null);
                        GUI.changed = true;
                        GuidedTourController.NotifyModuleSelected(module?.DisplayName);
                    }
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            foreach (var module in _modules)
            {
                module?.Dispose();
            }

            _modules.Clear();
        }

        private void DrawToolbar()
        {
            if (_modules.Count == 0)
            {
                EditorGUILayout.HelpBox("No modules are registered.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var tempContent = new GUIContent();
                for (int i = 0; i < _modules.Count; i++)
                {
                    DrawButton(i, tempContent);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawButton(int index, GUIContent tempContent)
        {
            var module = _modules[index];
            tempContent.text = module?.DisplayName ?? $"Module {index + 1}";
            bool isActive = index == _selectedIndex;

            if (GUILayout.Toggle(isActive, tempContent, Styles.ToolbarButton, GUILayout.MinWidth(60f)) && !isActive)
            {
                _selectedIndex = index;
                GUI.FocusControl(null);
                GUI.changed = true;
                GuidedTourController.NotifyModuleSelected(module?.DisplayName);
            }

            var buttonRect = GUILayoutUtility.GetLastRect();
            var tourTarget = module?.NavbarTourTarget;
            if (!string.IsNullOrEmpty(tourTarget))
            {
                var displayName = module.DisplayName;
                TourOverlay.DrawHighlightIfActive(tourTarget, buttonRect, module.NavbarTourLabel, onClick: () => SelectModuleByDisplayName(displayName));
            }
        }
    }
}
#endif
