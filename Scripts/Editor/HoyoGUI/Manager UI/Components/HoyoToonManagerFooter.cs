#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.EditorTools.ManagerUI.Components
{
    internal sealed class HoyoToonManagerFooter
    {
        private readonly string _title;
        private readonly string _message;
        private readonly Action _renderBody;
        private readonly Func<GameObject> _activeModelProvider;
        private readonly Func<GameObject, string> _prefabFolderResolver;
        private readonly Action<GameObject> _createPrefabAction;

        public HoyoToonManagerFooter(
            string title = "Global Controls",
            string message = null,
            Action renderBody = null,
            Func<GameObject> activeModelProvider = null,
            Func<GameObject, string> prefabFolderResolver = null,
            Action<GameObject> createPrefabAction = null)
        {
            _title = string.IsNullOrEmpty(title) ? "Global Controls" : title;
            _message = message;
            _renderBody = renderBody;
            _activeModelProvider = activeModelProvider;
            _prefabFolderResolver = prefabFolderResolver;
            _createPrefabAction = createPrefabAction;
        }

        public void Draw()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(_title, EditorStyles.boldLabel);
                if (_renderBody != null)
                {
                    _renderBody.Invoke();
                }
                else if (!string.IsNullOrEmpty(_message))
                {
                    EditorGUILayout.HelpBox(_message, MessageType.Info);
                }
                else if (_createPrefabAction == null)
                {
                    EditorGUILayout.HelpBox("Future global controls will appear here.", MessageType.Info);
                }

                DrawPrefabControls();
            }
        }

        private void DrawPrefabControls()
        {
            if (_createPrefabAction == null || _activeModelProvider == null)
            {
            return;
            }

            EditorGUILayout.Space(4f);

            var activeModel = _activeModelProvider.Invoke();
            string folder = _prefabFolderResolver?.Invoke(activeModel);
            bool hasFolder = !string.IsNullOrEmpty(folder);
            bool isValid = activeModel != null && hasFolder;

            using (new EditorGUI.DisabledScope(!isValid))
            {
            var content = new GUIContent(
                "Create Prefab",
                isValid ? $"Create a prefab next to:\n{folder}" : 
                activeModel == null ? "No model selected" : "Prefab source folder could not be located.");

            if (GUILayout.Button(content, GUILayout.Width(140f)))
            {
                _createPrefabAction.Invoke(activeModel);
            }
            }
        }
    }
}
#endif // UNITY_EDITOR
