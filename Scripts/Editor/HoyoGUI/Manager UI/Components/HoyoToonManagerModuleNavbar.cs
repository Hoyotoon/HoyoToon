#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.EditorTools.ManagerUI.Modules;

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
            }
        }
    }
}
#endif // UNITY_EDITOR
