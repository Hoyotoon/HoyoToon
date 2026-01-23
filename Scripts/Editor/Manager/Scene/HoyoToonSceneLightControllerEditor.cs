#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon.EditorTools.ManagerScene;

namespace HoyoToon.EditorTools.ManagerScene
{
    [CustomEditor(typeof(HoyoToonSceneLightController))]
    internal sealed class HoyoToonSceneLightControllerEditor : Editor
    {
        private SerializedProperty _gameSettings;
        private static readonly System.Collections.Generic.Dictionary<int, int> SelectedGameIndexByInstance = new System.Collections.Generic.Dictionary<int, int>();
        private static readonly System.Collections.Generic.Dictionary<int, string> DetectedSignatureByInstance = new System.Collections.Generic.Dictionary<int, string>();

        private void OnEnable()
        {
            _gameSettings = serializedObject.FindProperty("GameSettings");
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
        }

        private void HandleHierarchyChanged()
        {
            var controller = target as HoyoToonSceneLightController;
            if (controller == null)
            {
                return;
            }

            controller.Refresh();
            _gameSettings.serializedObject.Update();
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSection("Game Settings", DrawGameSettings);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGameSettings()
        {
            var controller = target as HoyoToonSceneLightController;
            if (controller != null && !Application.isPlaying)
            {
                controller.Refresh();
            }
            var detectedGames = controller?.RendererGroups?
                .Where(group => group != null && !string.IsNullOrEmpty(group.GameKey))
                .Select(group => group.GameKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new System.Collections.Generic.List<string>();

            var gameTabs = detectedGames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList();

            int instanceId = target != null ? target.GetInstanceID() : 0;
            var signature = string.Join("|", detectedGames.OrderBy(value => value));
            if (!DetectedSignatureByInstance.TryGetValue(instanceId, out var lastSignature) || lastSignature != signature)
            {
                DetectedSignatureByInstance[instanceId] = signature;
                Repaint();
            }

            if (gameTabs.Count == 0)
            {
                EditorGUILayout.HelpBox("No compatible games detected in the scene.", MessageType.Info);
                return;
            }

            EnsureGameSettings(detectedGames);
            if (!SelectedGameIndexByInstance.TryGetValue(instanceId, out var selectedIndex))
            {
                selectedIndex = 0;
                SelectedGameIndexByInstance[instanceId] = selectedIndex;
            }

            if (gameTabs.Count > 1)
            {
                selectedIndex = GUILayout.Toolbar(selectedIndex, gameTabs.ToArray());
                SelectedGameIndexByInstance[instanceId] = selectedIndex;

                selectedIndex = Mathf.Clamp(selectedIndex, 0, gameTabs.Count - 1);
                var activeGameKey = gameTabs[selectedIndex];
                DrawGameSettingsForKey(activeGameKey, detectedGames);
            }
            else
            {
                DrawGameSettingsForKey(gameTabs[0], detectedGames);
            }
        }

        private void DrawGameSettingsForKey(string gameKey, System.Collections.Generic.IReadOnlyCollection<string> detectedGames)
        {
            bool isDetected = detectedGames.Any(value => string.Equals(value, gameKey, StringComparison.OrdinalIgnoreCase));
            if (!isDetected)
            {
                EditorGUILayout.HelpBox("No compatible game detected in the scene for this tab.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _gameSettings.arraySize; i++)
            {
                var settings = _gameSettings.GetArrayElementAtIndex(i);
                var keyProp = settings.FindPropertyRelative("GameKey");
                if (!string.Equals(keyProp.stringValue, gameKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DrawSettingsForGame(settings, gameKey);
                return;
            }

            EditorGUILayout.HelpBox("No lighting settings defined for this game yet.", MessageType.Info);
        }

        private void EnsureGameSettings(System.Collections.Generic.IEnumerable<string> gameKeys)
        {
            foreach (var gameKey in gameKeys)
            {
                if (HasSettingsForKey(gameKey))
                {
                    continue;
                }

                int newIndex = _gameSettings.arraySize;
                _gameSettings.InsertArrayElementAtIndex(newIndex);
                var entry = _gameSettings.GetArrayElementAtIndex(newIndex);
                var keyProp = entry.FindPropertyRelative("GameKey");
                if (keyProp != null)
                {
                    keyProp.stringValue = gameKey;
                }
            }
        }

        private bool HasSettingsForKey(string gameKey)
        {
            for (int i = 0; i < _gameSettings.arraySize; i++)
            {
                var settings = _gameSettings.GetArrayElementAtIndex(i);
                var keyProp = settings.FindPropertyRelative("GameKey");
                if (keyProp != null && string.Equals(keyProp.stringValue, gameKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawSettingsForGame(SerializedProperty settings, string gameKey)
        {
            if (settings == null)
            {
                return;
            }

            var fieldName = NormalizeGameKeyToFieldName(gameKey);
            var gameSettingsProperty = settings.FindPropertyRelative(fieldName);
            if (gameSettingsProperty == null)
            {
                EditorGUILayout.HelpBox("No lighting settings defined for this game yet.", MessageType.Info);
                return;
            }

            var iterator = gameSettingsProperty.Copy();
            var endProperty = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        private static string NormalizeGameKeyToFieldName(string gameKey)
        {
            if (string.IsNullOrEmpty(gameKey))
            {
                return string.Empty;
            }

            var chars = gameKey.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars);
        }

        private static void DrawSection(string title, Action drawer)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                drawer?.Invoke();
            }
        }
    }
}
#endif
