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
		private SerializedProperty _shadowBoostProp;
		private SerializedProperty _levelAdjustProp;
		private string _lastTourStepId;

		public override string DisplayName => "Scriptables";

		public override void OnGUI(HoyoToonManager targetManager)
		{
			if (targetManager == null)
			{
				EditorGUILayout.HelpBox("Assign a HoyoToon Manager to edit scriptable settings.", MessageType.Info);
				return;
			}

			EditorGUILayout.Space(8f);
			DrawSceneLightSettingsPanel(targetManager);
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

			ForceDisableTourLightingFlags(settings);

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

				bool forceExpand = ShouldForceExpandForTour(groupProps);
				bool defaultExpanded = groupName.Equals("General", StringComparison.OrdinalIgnoreCase) || forceExpand;
				DrawSectionFoldout(groupName, defaultExpanded, () =>
				{
					using (new EditorGUI.IndentLevelScope())
					{
						foreach (var prop in groupProps)
						{
							DrawScriptablesProperty(prop);
						}
					}
				}, forceExpand);
				GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
			}
		}

		private void DrawScriptablesProperty(SerializedProperty prop)
		{
			if (prop == null)
			{
				return;
			}

			bool isShadowBoost = string.Equals(prop.name, "EnableShadowBoost", StringComparison.Ordinal);
			bool isLevelAdjust = string.Equals(prop.name, "LevelAdjustEnable", StringComparison.Ordinal);
			bool isTarget = isShadowBoost || isLevelAdjust;

			if (isShadowBoost)
			{
				DrawInlineCallout("scriptables_shadowboost",
					"Enable Shadow Boost, then check the scene.\n\nShadow Boost strengthens contact shadows.");
				if (string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "scriptables_reset", StringComparison.OrdinalIgnoreCase))
				{
					DrawInlineCallout("scriptables_reset",
						"Click the highlighted toggle to continue.\n\nShadow Boost and Level Adjust are turned off for you so the rest of the tour uses neutral lighting.");
				}
			}
			else if (isLevelAdjust)
			{
				DrawInlineCallout("scriptables_leveladjust",
					"Enable Level Adjust, then check the scene.\n\nLevel Adjust lifts overall brightness for cutscene looks.");
			}

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(prop, true);
			bool changed = EditorGUI.EndChangeCheck();
			var rect = GUILayoutUtility.GetLastRect();

			if (isShadowBoost)
			{
				_shadowBoostProp = prop;
				if (changed && prop.boolValue && string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "scriptables_shadowboost", StringComparison.OrdinalIgnoreCase))
				{
					HoyoToonGuidedTourController.NotifyScriptablesShadowBoostEnabled();
				}
				if (string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "scriptables_reset", StringComparison.OrdinalIgnoreCase))
				{
					HoyoToonTourOverlay.DrawHighlightIfActive("tour.scriptables.reset", rect, "Reset", onClick: ResetScriptablesLighting);
				}
				else
				{
					HoyoToonTourOverlay.DrawHighlightIfActive("tour.scriptables.shadowboost", rect, "Shadow Boost");
				}
			}
			else if (isLevelAdjust)
			{
				_levelAdjustProp = prop;
				if (changed && prop.boolValue && string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "scriptables_leveladjust", StringComparison.OrdinalIgnoreCase))
				{
					HoyoToonGuidedTourController.NotifyScriptablesLevelAdjustEnabled();
				}
				if (string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "scriptables_reset", StringComparison.OrdinalIgnoreCase))
				{
					HoyoToonTourOverlay.DrawHighlightIfActive("tour.scriptables.reset", rect, "Reset", onClick: ResetScriptablesLighting);
				}
				else
				{
					HoyoToonTourOverlay.DrawHighlightIfActive("tour.scriptables.leveladjust", rect, "Level Adjust");
				}
			}
			else if (!isTarget)
			{
				return;
			}
		}

		private void ResetScriptablesLighting()
		{
			if (!HoyoToonGuidedTourController.IsActive
				|| !string.Equals(HoyoToonGuidedTourController.CurrentStep.id, "scriptables_reset", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			if (_shadowBoostProp != null)
			{
				_shadowBoostProp.boolValue = false;
			}
			if (_levelAdjustProp != null)
			{
				_levelAdjustProp.boolValue = false;
			}

			HoyoToonGuidedTourController.NotifyScriptablesReset();
		}

		private static void DrawInlineCallout(string stepId, string body)
		{
			if (!HoyoToonGuidedTourController.IsActive)
			{
				return;
			}

			if (!string.Equals(HoyoToonGuidedTourController.CurrentStep.id, stepId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			HoyoToonTourCallout.Draw(
				$"Guided Tour: {HoyoToonGuidedTourController.CurrentStep.title}",
				body,
				null,
				null);
		}

		private void ForceDisableTourLightingFlags(SerializedProperty settings)
		{
			if (!HoyoToonGuidedTourController.IsActive)
			{
				return;
			}

			string stepId = HoyoToonGuidedTourController.CurrentStep.id;
			if (!string.Equals(stepId, "scriptables_shadowboost", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(stepId, "scriptables_leveladjust", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(stepId, "scriptables_reset", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			if (string.Equals(_lastTourStepId, stepId, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			_lastTourStepId = stepId;

			var iterator = settings.Copy();
			var endProperty = iterator.GetEndProperty();
			bool enterChildren = true;
			while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
			{
				enterChildren = false;
				if (iterator.propertyType != SerializedPropertyType.Boolean)
				{
					continue;
				}

				if (string.Equals(iterator.name, "EnableShadowBoost", StringComparison.Ordinal)
					|| string.Equals(iterator.name, "LevelAdjustEnable", StringComparison.Ordinal))
				{
					iterator.boolValue = false;
				}
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

		private bool DrawSectionFoldout(string title, bool defaultExpanded, Action drawer, bool forceExpanded)
		{
			if (!_sceneLightSectionFoldouts.TryGetValue(title, out var expanded))
			{
				expanded = defaultExpanded;
			}

			if (forceExpanded)
			{
				expanded = true;
			}

			expanded = HoyoToonManagerModule.DrawFoldoutSection(title, expanded, drawer, 8f);
			if (forceExpanded)
			{
				expanded = true;
			}
			_sceneLightSectionFoldouts[title] = expanded;
			return expanded;
		}

		private static bool ShouldForceExpandForTour(IEnumerable<SerializedProperty> groupProps)
		{
			if (!HoyoToonGuidedTourController.IsActive)
			{
				return false;
			}

			string stepId = HoyoToonGuidedTourController.CurrentStep.id;
			if (string.IsNullOrEmpty(stepId) || !stepId.StartsWith("scriptables_", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			bool wantsShadowBoost = string.Equals(stepId, "scriptables_shadowboost", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(stepId, "scriptables_reset", StringComparison.OrdinalIgnoreCase);
			bool wantsLevelAdjust = string.Equals(stepId, "scriptables_leveladjust", StringComparison.OrdinalIgnoreCase);

			foreach (var prop in groupProps)
			{
				if (prop == null)
				{
					continue;
				}

				if (wantsShadowBoost && string.Equals(prop.name, "EnableShadowBoost", StringComparison.Ordinal))
				{
					return true;
				}
				if (wantsLevelAdjust && string.Equals(prop.name, "LevelAdjustEnable", StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
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