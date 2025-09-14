// Copyright (c) Jason Ma

using LWGUI.Runtime.LwguiGradient;
using UnityEditor;
using UnityEngine;

namespace LWGUI.LwguiGradientEditor
{
    [CustomEditor(typeof(LwguiGradientPresetLibrary))]
    internal class LwguiGradientPresetLibraryEditor : Editor
    {
        private GenericPresetLibraryInspector<LwguiGradientPresetLibrary> m_GenericPresetLibraryInspector;

        public void OnEnable()
        {
            m_GenericPresetLibraryInspector = new GenericPresetLibraryInspector<LwguiGradientPresetLibrary>(target, "Lwgui Gradient Preset Library", OnEditButtonClicked)
            {
                presetSize = new Vector2(72, 16),
                lineSpacing = 4f
            };
        }

        public void OnDestroy()
        {
            m_GenericPresetLibraryInspector?.OnDestroy();
        }

        public override void OnInspectorGUI()
        {
            m_GenericPresetLibraryInspector.itemViewMode = PresetLibraryEditorState.GetItemViewMode("LwguiGradient"); // ensure in-sync
            m_GenericPresetLibraryInspector?.OnInspectorGUI();
        }

        private void OnEditButtonClicked(string libraryPath)
        {
            LwguiGradientWindow.Show(new LwguiGradient());
            LwguiGradientWindow.instance.currentPresetLibrary = libraryPath;
        }
    }
} // namespace
