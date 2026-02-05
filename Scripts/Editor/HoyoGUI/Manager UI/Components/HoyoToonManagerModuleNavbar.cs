#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.EditorTools.ManagerUI.Modules;
using HoyoToon.EditorTools.Onboarding;

namespace HoyoToon.EditorTools.ManagerUI.Components
{
    internal sealed class HoyoToonManagerModuleNavbar : IDisposable
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

        private static readonly GUIContent s_TempContent = new GUIContent();
        private readonly List<HoyoToonManagerModule> _modules = new List<HoyoToonManagerModule>();
        private int _selectedIndex;

        public int SelectedIndex => _modules.Count == 0 ? -1 : Mathf.Clamp(_selectedIndex, 0, _modules.Count - 1);

        public void RegisterModule(HoyoToonManagerModule module)
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

        public HoyoToonManagerModule GetSelectedModule()
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
                        HoyoToonGuidedTourController.NotifyModuleSelected(module?.DisplayName);
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
                for (int i = 0; i < _modules.Count; i++)
                {
                    DrawButton(i);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawButton(int index)
        {
            var module = _modules[index];
            s_TempContent.text = module?.DisplayName ?? $"Module {index + 1}";
            bool isActive = index == _selectedIndex;

            if (GUILayout.Toggle(isActive, s_TempContent, Styles.ToolbarButton, GUILayout.MinWidth(60f)) && !isActive)
            {
                _selectedIndex = index;
                GUI.FocusControl(null);
                GUI.changed = true;
                HoyoToonGuidedTourController.NotifyModuleSelected(module?.DisplayName);
            }

            var buttonRect = GUILayoutUtility.GetLastRect();
            if (string.Equals(module?.DisplayName, "Models", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.modules.models", buttonRect, "Models", onClick: () => SelectModuleByDisplayName("Models"));
            }
            else if (string.Equals(module?.DisplayName, "Main", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.modules.main", buttonRect, "Main", onClick: () => SelectModuleByDisplayName("Main"));
            }
            else if (string.Equals(module?.DisplayName, "Lighting", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.modules.lighting", buttonRect, "Lighting", onClick: () => SelectModuleByDisplayName("Lighting"));
            }
            else if (string.Equals(module?.DisplayName, "Scriptables", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.modules.scriptables", buttonRect, "Scriptables", onClick: () => SelectModuleByDisplayName("Scriptables"));
            }
            else if (string.Equals(module?.DisplayName, "Post Processing", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.modules.postprocessing", buttonRect, "Post FX", onClick: () => SelectModuleByDisplayName("Post Processing"));
            }
            else if (string.Equals(module?.DisplayName, "Renders", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonTourOverlay.DrawHighlightIfActive("tour.modules.renders", buttonRect, "Renders", onClick: () => SelectModuleByDisplayName("Renders"));
            }
        }
    }
}
#endif // UNITY_EDITOR
