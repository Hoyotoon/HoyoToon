#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.EditorTools.ManagerScene;
using HoyoToon.EditorTools.Onboarding;

namespace HoyoToon.EditorTools.ManagerUI.Modules
{
	internal sealed class ScriptablesModule : HoyoToonManagerModule
	{
		private static readonly Dictionary<int, int> SceneLightSelectedGameIndexByInstance = new Dictionary<int, int>();
		private static readonly Dictionary<int, string> SceneLightDetectedSignatureByInstance = new Dictionary<int, string>();
		private readonly Dictionary<string, bool> _sceneLightSectionFoldouts = new Dictionary<string, bool>(StringComparer.Ordinal);

		private HoyoToonScriptablesController _sceneLightController;
		private SerializedObject _sceneLightSerializedObject;
		private SerializedProperty _sceneLightGameSettings;

		public override string DisplayName => "Scriptables";

		public override void OnGUI(HoyoToonManager targetManager)
		{
			DrawTourCalloutIfNeeded();
			if (targetManager == null)
			{
				EditorGUILayout.HelpBox("Assign a HoyoToon Manager to edit scriptable settings.", MessageType.Info);
				return;
			}

			EditorGUILayout.Space(8f);
			DrawSceneLightSettingsPanel(targetManager);
		}

		private static void DrawTourCalloutIfNeeded()
		{
			if (!HoyoToonGuidedTourController.IsActive)
			{
				return;
			}

			var step = HoyoToonGuidedTourController.CurrentStep;
			if (step.id != "scriptables")
			{
				return;
			}

			HoyoToonTourCallout.Draw(
				$"Guided Tour: {step.title}",
				"Scriptables contain global game profiles and shared shader settings. Review the active profile to match the game lighting style.",
				"Continue to Post Processing",
				() =>
				{
					HoyoToonGuidedTourController.CompleteScriptablesStep();
					HoyoToonGuidedTourController.Advance();
				});
		}

		private void DrawSceneLightSettingsPanel(HoyoToonManager manager)
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				var controller = GetSceneLightController(manager);
				if (controller == null)
				{
					EditorGUILayout.HelpBox("No Scriptables Controller found for this manager.", MessageType.Info);
					if (GUILayout.Button("Create Scriptables Controller"))
					{
						var scriptsParent = ResolveScriptsParent(manager);
						_sceneLightController = HoyoToonScriptablesController.EnsureForManager(manager, scriptsParent, null);
						InvalidateSceneLightSerialized();
					}
					return;
				}

				if (!Application.isPlaying)
				{
					controller.Refresh();
				}

				EnsureSceneLightSerialized(controller);
				if (_sceneLightSerializedObject == null || _sceneLightGameSettings == null)
				{
					EditorGUILayout.HelpBox("Scriptables Controller settings are unavailable.", MessageType.Warning);
					return;
				}

				_sceneLightSerializedObject.Update();
				DrawSceneLightGameSettings(controller, _sceneLightGameSettings);
				_sceneLightSerializedObject.ApplyModifiedProperties();
			}
		}

		private HoyoToonScriptablesController GetSceneLightController(HoyoToonManager manager)
		{
			if (manager == null)
			{
				return null;
			}

			if (_sceneLightController != null && _sceneLightController.Manager == manager)
			{
				return _sceneLightController;
			}

			_sceneLightController = UnityEngine.Object.FindObjectsOfType<HoyoToonScriptablesController>(true)
				.FirstOrDefault(controller => controller != null && controller.Manager == manager);

			return _sceneLightController;
		}

		private void EnsureSceneLightSerialized(HoyoToonScriptablesController controller)
		{
			if (controller == null)
			{
				InvalidateSceneLightSerialized();
				return;
			}

			if (_sceneLightSerializedObject != null && _sceneLightSerializedObject.targetObject == controller)
			{
				return;
			}

			_sceneLightSerializedObject = new SerializedObject(controller);
			_sceneLightGameSettings = _sceneLightSerializedObject.FindProperty("GameSettings");
		}

		private void InvalidateSceneLightSerialized()
		{
			_sceneLightSerializedObject = null;
			_sceneLightGameSettings = null;
		}

		private static Transform ResolveScriptsParent(HoyoToonManager manager)
		{
			if (manager == null)
			{
				return null;
			}

			var scriptsParent = manager.transform.Find("Scripts");
			if (scriptsParent == null)
			{
				var scriptsGo = new GameObject("Scripts");
				Undo.RegisterCreatedObjectUndo(scriptsGo, "Create Scripts");
				scriptsGo.transform.SetParent(manager.transform, false);
				scriptsParent = scriptsGo.transform;
			}

			return scriptsParent;
		}

		private void DrawSceneLightGameSettings(HoyoToonScriptablesController controller, SerializedProperty gameSettings)
		{
			var detectedGames = controller?.RendererGroups?
				.Where(group => group != null && !string.IsNullOrEmpty(group.GameKey))
				.Select(group => group.GameKey)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList() ?? new List<string>();

			if (detectedGames.Count == 0 && controller != null && !string.IsNullOrEmpty(controller.GameKey))
			{
				detectedGames.Add(controller.GameKey);
			}

			var gameTabs = detectedGames
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(value => value)
				.ToList();

			int instanceId = controller != null ? controller.GetInstanceID() : 0;
			var signature = string.Join("|", detectedGames.OrderBy(value => value));
			if (!SceneLightDetectedSignatureByInstance.TryGetValue(instanceId, out var lastSignature) || lastSignature != signature)
			{
				SceneLightDetectedSignatureByInstance[instanceId] = signature;
			}

			if (gameTabs.Count == 0)
			{
				EditorGUILayout.HelpBox("No compatible games detected in the scene.", MessageType.Info);
				return;
			}

			EnsureSceneLightGameSettings(gameSettings, detectedGames);
			if (!SceneLightSelectedGameIndexByInstance.TryGetValue(instanceId, out var selectedIndex))
			{
				selectedIndex = 0;
				SceneLightSelectedGameIndexByInstance[instanceId] = selectedIndex;
			}

			if (gameTabs.Count > 1)
			{
				using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
				{
					selectedIndex = GUILayout.Toolbar(selectedIndex, gameTabs.ToArray(), EditorStyles.toolbarButton);
					SceneLightSelectedGameIndexByInstance[instanceId] = selectedIndex;
				}

				selectedIndex = Mathf.Clamp(selectedIndex, 0, gameTabs.Count - 1);
				EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
				DrawSceneLightSettingsForKey(gameSettings, gameTabs[selectedIndex], detectedGames);
			}
			else
			{
				DrawSceneLightSettingsForKey(gameSettings, gameTabs[0], detectedGames);
			}
		}

		private void DrawSceneLightSettingsForKey(SerializedProperty gameSettings, string gameKey, IReadOnlyCollection<string> detectedGames)
		{
			bool isDetected = detectedGames.Any(value => string.Equals(value, gameKey, StringComparison.OrdinalIgnoreCase));
			if (!isDetected)
			{
				EditorGUILayout.HelpBox("No compatible game detected in the scene for this tab.", MessageType.Info);
				return;
			}

			for (int i = 0; i < gameSettings.arraySize; i++)
			{
				var settings = gameSettings.GetArrayElementAtIndex(i);
				var keyProp = settings.FindPropertyRelative("GameKey");
				if (!string.Equals(keyProp.stringValue, gameKey, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				DrawSceneLightSettingsForGame(settings, gameKey);
				return;
			}

			EditorGUILayout.HelpBox("No lighting settings defined for this game yet.", MessageType.Info);
		}

		private void EnsureSceneLightGameSettings(SerializedProperty gameSettings, IEnumerable<string> gameKeys)
		{
			foreach (var gameKey in gameKeys)
			{
				if (HasSceneLightSettingsForKey(gameSettings, gameKey))
				{
					continue;
				}

				int newIndex = gameSettings.arraySize;
				gameSettings.InsertArrayElementAtIndex(newIndex);
				var entry = gameSettings.GetArrayElementAtIndex(newIndex);
				var keyProp = entry.FindPropertyRelative("GameKey");
				if (keyProp != null)
				{
					keyProp.stringValue = gameKey;
				}
			}
		}

		private static bool HasSceneLightSettingsForKey(SerializedProperty gameSettings, string gameKey)
		{
			for (int i = 0; i < gameSettings.arraySize; i++)
			{
				var settings = gameSettings.GetArrayElementAtIndex(i);
				var keyProp = settings.FindPropertyRelative("GameKey");
				if (keyProp != null && string.Equals(keyProp.stringValue, gameKey, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private void DrawSceneLightSettingsForGame(SerializedProperty settings, string gameKey)
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

			if (HoyoToonScriptablesController.TryGetUiGrouping(gameKey, out var groupPatterns, out var defaultGroup))
			{
				DrawGroupedSettings(gameSettingsProperty, groupPatterns, defaultGroup);
				return;
			}

			DrawGroupedSettings(gameSettingsProperty, null, "Settings");
		}

		private void DrawGroupedSettings(SerializedProperty settings, IReadOnlyDictionary<string, string[]> groupPatterns, string defaultGroup)
		{
			if (settings == null)
			{
				return;
			}

			var grouped = new Dictionary<string, List<SerializedProperty>>(StringComparer.OrdinalIgnoreCase);
			var iterator = settings.Copy();
			var endProperty = iterator.GetEndProperty();
			bool enterChildren = true;
			while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
			{
				enterChildren = false;
				if (iterator.propertyType == SerializedPropertyType.Generic)
				{
					continue;
				}

				string groupName = ResolveGroupName(iterator.name, groupPatterns, defaultGroup);
				if (!grouped.TryGetValue(groupName, out var list))
				{
					list = new List<SerializedProperty>();
					grouped[groupName] = list;
				}

				list.Add(iterator.Copy());
			}

			foreach (var groupName in GetOrderedGroupNames(grouped, groupPatterns, defaultGroup))
			{
				if (!grouped.TryGetValue(groupName, out var groupProps))
				{
					continue;
				}

				DrawSectionFoldout(groupName, groupName.Equals("General", StringComparison.OrdinalIgnoreCase), () =>
				{
					using (new EditorGUI.IndentLevelScope())
					{
						foreach (var prop in groupProps)
						{
							EditorGUILayout.PropertyField(prop, true);
						}
					}
				});
				GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
			}
		}

		private static IEnumerable<string> GetOrderedGroupNames(Dictionary<string, List<SerializedProperty>> grouped, IReadOnlyDictionary<string, string[]> groupPatterns, string defaultGroup)
		{
			var ordered = new List<string>();
			if (groupPatterns != null && groupPatterns.Count > 0)
			{
				foreach (var groupName in groupPatterns.Keys)
				{
					if (grouped.ContainsKey(groupName))
					{
						ordered.Add(groupName);
					}
				}
			}

			if (!string.IsNullOrEmpty(defaultGroup) && grouped.ContainsKey(defaultGroup) && !ordered.Contains(defaultGroup))
			{
				ordered.Add(defaultGroup);
			}

			foreach (var groupName in grouped.Keys.OrderBy(name => name))
			{
				if (!ordered.Contains(groupName))
				{
					ordered.Add(groupName);
				}
			}

			return ordered;
		}

		private static string ResolveGroupName(string propertyName, IReadOnlyDictionary<string, string[]> groupPatterns, string defaultGroup)
		{
			if (string.IsNullOrEmpty(propertyName) || groupPatterns == null || groupPatterns.Count == 0)
			{
				return defaultGroup;
			}

			foreach (var kvp in groupPatterns)
			{
				foreach (var pattern in kvp.Value)
				{
					if (MatchesPattern(propertyName, pattern))
					{
						return kvp.Key;
					}
				}
			}

			return defaultGroup;
		}

		private static bool MatchesPattern(string value, string pattern)
		{
			if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(pattern))
			{
				return false;
			}

			if (pattern.EndsWith("*", StringComparison.Ordinal))
			{
				var prefix = pattern.Substring(0, pattern.Length - 1);
				return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
			}

			return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);
		}

		private bool DrawSectionFoldout(string title, bool defaultExpanded, Action drawer)
		{
			if (!_sceneLightSectionFoldouts.TryGetValue(title, out var expanded))
			{
				expanded = defaultExpanded;
			}

			expanded = HoyoToonManagerModule.DrawFoldoutSection(title, expanded, drawer, 8f);
			_sceneLightSectionFoldouts[title] = expanded;
			return expanded;
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
	}
}
#endif