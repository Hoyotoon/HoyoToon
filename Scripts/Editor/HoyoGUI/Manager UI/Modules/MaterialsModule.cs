#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.Materials;
using HoyoToon.EditorTools.ManagerUI.Utilities;

namespace HoyoToon.EditorTools.ManagerUI.Modules
{
	internal sealed class MaterialsModule : HoyoToonManagerModule
	{
		private const float GlobalPropertyLabelWidth = 170f;

		/// <summary>
		/// Configure per-game global property layouts here. Each entry is keyed by the display name shown in the manager.
		/// </summary>
		private static readonly Dictionary<string, GameGlobalConfig> GameGlobalPropertyMap = new Dictionary<string, GameGlobalConfig>(StringComparer.OrdinalIgnoreCase)
		{
			{
				"Honkai Star Rail",
				new GameGlobalConfig(
					"Honkai Star Rail",
					new[]
					{
						Section("General Material",
							Prop("_ES_CharacterToonRampMode", "Use Cold Ramp", GlobalPropertyControlHint.Toggle),
							Prop("_ES_SPColor", "SP Color"),
							Prop("_ES_SPIntensity", "SP Intensity"),
							Prop("_ShadowBoost", "Enable Shadow Boost", GlobalPropertyControlHint.Toggle),
							Prop("_ShadowBoostVal", "Shadow Boost")
						),
						Section("Height Light",
							Prop("_UseHeightLerp", "Height Lerp Enable", GlobalPropertyControlHint.Toggle),
							Prop("_ES_HeightLerpTop", "Height Lerp Top"),
							Prop("_ES_HeightLerpBottom", "Height Lerp Bottom"),
							Prop("_ES_HeightLerpTopColor", "Height Lerp Top Color"),
							Prop("_ES_HeightLerpMiddleColor", "Height Lerp Middle Color"),
							Prop("_ES_HeightLerpBottomColor", "Height Lerp Bottom Color")
						),
						Section("Rim Light",
							Prop("_ES_RimLightWidth", "Rim Light Width"),
							Prop("_ES_RimLightAddMode", "Rim Light Intensity"),
							Prop("_ES_RimLightOffset", "Rim Light Offset")
						),
						Section("Rim Shadow",
							Prop("_ES_RimShadowIntensity", "Rim Shadow Intensity"),
							Prop("_ES_RimShadowColor", "Rim Shadow Color")
						),
						Section("Story Lighting",
							Prop("_ES_LEVEL_ADJUST_ON", "Level Adjust Enable", GlobalPropertyControlHint.Toggle),
							Prop("_ES_LevelSkinLightColor", "Skin Light Color"),
							Prop("_ES_LevelSkinShadowColor", "Skin Shadow Color"),
							Prop("_ES_LevelHighLightColor", "Highlight Color"),
							Prop("_ES_LevelShadowColor", "Shadow Color"),
							Prop("_ES_LevelShadow", "Shadow Level"),
							Prop("_ES_LevelMid", "Mid Level"),
							Prop("_ES_LevelHighLight", "Highlight Level")
						),
						Section("Point Lights",
							Prop("_FakePointLightNum", "Active Lights"),
							Prop("_FakePointLight0", "Light 1 Enable", GlobalPropertyControlHint.Toggle),
							Prop("_FakePointLight0Color", "Light 1 Color"),
							Prop("_FakePointLight0Pos", "Light 1 Position"),
							Prop("_FakePointLight0Intensity", "Light 1 Intensity"),
							Prop("_FakePointLight0AttenuationRadius", "Light 1 Radius"),
							Prop("_FakePointLight1", "Light 2 Enable", GlobalPropertyControlHint.Toggle),
							Prop("_FakePointLight1Color", "Light 2 Color"),
							Prop("_FakePointLight1Pos", "Light 2 Position"),
							Prop("_FakePointLight1Intensity", "Light 2 Intensity"),
							Prop("_FakePointLight1AttenuationRadius", "Light 2 Radius"),
							Prop("_FakePointLight2", "Light 3 Enable", GlobalPropertyControlHint.Toggle),
							Prop("_FakePointLight2Color", "Light 3 Color"),
							Prop("_FakePointLight2Pos", "Light 3 Position"),
							Prop("_FakePointLight2Intensity", "Light 3 Intensity"),
							Prop("_FakePointLight2AttenuationRadius", "Light 3 Radius")
						),
						Section("Directional & Fog",
							Prop("_UseFakeDirectionalLight", "Use Fake Directional Light", GlobalPropertyControlHint.Toggle),
							Prop("_FakeDirectionalLightRotation", "Directional Light Rotation"),
							Prop("_FakeFogColor", "Fake Fog Color"),
							Prop("_FakeFogDensity", "Fake Fog Density"),
							Prop("_FakeFogHeightFalloff", "Fake Fog Height Falloff"),
							Prop("_FakeFogStartHeight", "Fake Fog Start Height")
						)
					})
			}
		};

		private readonly List<Material> _uniqueMaterials = new List<Material>();
		private readonly Dictionary<string, bool> _sectionFoldoutStates = new Dictionary<string, bool>(StringComparer.Ordinal);

		private bool _materialsDirty = true;
		private HoyoToonManager _lastManager;
		private GameObject _lastModel;
		private string _gameKey;
		private string _gameDisplayName;

		public override string DisplayName => "Materials";

		public override void OnGUI(HoyoToonManager targetManager)
		{
			if (targetManager == null)
			{
				EditorGUILayout.HelpBox("Select a HoyoToon Manager to edit game globals.", MessageType.Info);
				return;
			}

			if (ShouldRebuildCache(targetManager))
			{
				RebuildMaterialCache(targetManager);
			}

			if (_uniqueMaterials.Count == 0)
			{
				EditorGUILayout.HelpBox("No materials were found on the active model.", MessageType.Info);
				return;
			}

			EditorGUILayout.Space(8f);
			DrawGlobalSettingsPanel();
		}

		private bool ShouldRebuildCache(HoyoToonManager manager)
		{
			if (_materialsDirty)
			{
				return true;
			}

			if (manager != _lastManager)
			{
				return true;
			}

			if (manager.ActiveModel != _lastModel)
			{
				return true;
			}

			return false;
		}

		private void RebuildMaterialCache(HoyoToonManager manager)
		{
			_materialsDirty = false;
			_lastManager = manager;
			_lastModel = manager != null ? manager.ActiveModel : null;
			_gameKey = null;
			_gameDisplayName = null;
			_uniqueMaterials.Clear();
			_sectionFoldoutStates.Clear();

			if (_lastModel == null)
			{
				return;
			}

			var renderers = _lastModel.GetComponentsInChildren<Renderer>(true);
			var seenMaterials = new HashSet<Material>();
			foreach (var renderer in renderers)
			{
				if (renderer == null)
				{
					continue;
				}

				var sharedMaterials = renderer.sharedMaterials;
				if (sharedMaterials == null || sharedMaterials.Length == 0)
				{
					continue;
				}

				for (int slot = 0; slot < sharedMaterials.Length; slot++)
				{
					var mat = sharedMaterials[slot];
					if (mat == null)
					{
						continue;
					}

					if (seenMaterials.Add(mat))
					{
						_uniqueMaterials.Add(mat);
					}
				}
			}

			UpdateActiveGameMetadata(manager, _lastModel);
		}

		private void DrawGlobalSettingsPanel()
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				var config = GetCurrentGameConfig();
				var headerLabel = config != null ? config.DisplayName : (!string.IsNullOrEmpty(_gameDisplayName) ? _gameDisplayName : null);
				var headerText = string.IsNullOrEmpty(headerLabel) ? "Game Globals" : $"Game Globals ({headerLabel})";
				EditorGUILayout.LabelField(headerText, EditorStyles.boldLabel);

				if (string.IsNullOrEmpty(_gameKey) && string.IsNullOrEmpty(_gameDisplayName))
				{
					EditorGUILayout.HelpBox("Select an active model so its game can be detected before editing global overrides.", MessageType.Info);
					return;
				}

				if (config == null || !config.HasProperties)
				{
					var missingLabel = !string.IsNullOrEmpty(_gameDisplayName)
						? _gameDisplayName
						: (!string.IsNullOrEmpty(_gameKey) ? _gameKey : "Game");
					EditorGUILayout.HelpBox($"No global property layout is configured for '{missingLabel}'. Update 'GameGlobalPropertyMap' in MaterialsModule with the sections you want to expose.", MessageType.Info);
					return;
				}

				foreach (var section in config.Sections)
				{
					DrawSection(section);
					GUILayout.Space(EditorGUIUtility.standardVerticalSpacing * 2f);
				}
			}
		}

		private void DrawSection(GlobalPropertySection section)
		{
			if (section == null || !section.HasProperties)
			{
				return;
			}

			if (!_sectionFoldoutStates.TryGetValue(section.Title, out var expanded))
			{
				expanded = true;
			}

			expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, section.Title);
			_sectionFoldoutStates[section.Title] = expanded;
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (!expanded)
			{
				return;
			}

			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			using (new EditorGUI.IndentLevelScope())
			{
				EditorGUILayout.Space(2f);
				foreach (var property in section.Properties)
				{
					DrawGlobalPropertyRow(property);
				}
			}
		}

		private void DrawGlobalPropertyRow(GlobalPropertyDefinition definition)
		{
			if (definition == null)
			{
				return;
			}

			var property = TryGetMaterialProperty(definition.PropertyName);
			var labelContent = BuildLabel(definition, property);
			var previousLabelWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = GlobalPropertyLabelWidth;
			try
			{
				if (property == null)
				{
					using (new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.LabelField(labelContent, new GUIContent("Not present on active materials"), EditorStyles.centeredGreyMiniLabel);
					}
					return;
				}

				DrawPropertyControl(definition, property, labelContent);
			}
			finally
			{
				EditorGUIUtility.labelWidth = previousLabelWidth;
			}

			GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
		}

		private void DrawPropertyControl(GlobalPropertyDefinition definition, MaterialProperty property, GUIContent labelContent)
		{
			bool previousMixedValue = EditorGUI.showMixedValue;
			EditorGUI.showMixedValue = property.hasMixedValue;

			try
			{
				if (definition.ControlHint == GlobalPropertyControlHint.Toggle && IsFloatLike(property))
				{
					EditorGUI.BeginChangeCheck();
					var newBool = EditorGUILayout.Toggle(labelContent, property.floatValue > 0.5f);
					if (EditorGUI.EndChangeCheck())
					{
						property.floatValue = newBool ? 1f : 0f;
					}
					return;
				}

				switch (property.type)
				{
					case MaterialProperty.PropType.Float:
					case MaterialProperty.PropType.Int:
						DrawFloatField(labelContent, property);
						break;
					case MaterialProperty.PropType.Range:
						DrawRangeField(labelContent, property);
						break;
					case MaterialProperty.PropType.Color:
						DrawColorField(labelContent, property);
						break;
					case MaterialProperty.PropType.Vector:
						DrawVectorField(labelContent, property);
						break;
					case MaterialProperty.PropType.Texture:
						DrawTextureField(labelContent, property);
						break;
					default:
						using (new EditorGUI.DisabledScope(true))
						{
							EditorGUILayout.LabelField(labelContent, new GUIContent("Unsupported type"), EditorStyles.centeredGreyMiniLabel);
						}
						break;
				}
			}
			finally
			{
				EditorGUI.showMixedValue = previousMixedValue;
			}
		}

		private void UpdateActiveGameMetadata(HoyoToonManager manager, GameObject model)
		{
			_gameKey = null;
			_gameDisplayName = null;

			if (manager != null && HoyoToonManagerModelCache.TryGetGameInfo(manager, out var cachedInfo) && cachedInfo.HasGame)
			{
				_gameKey = cachedInfo.GameKey;
				_gameDisplayName = string.IsNullOrEmpty(cachedInfo.GameDisplayName)
					? HoyoToonManagerModelCache.ResolveGameDisplayName(cachedInfo.GameKey)
					: cachedInfo.GameDisplayName;
			}

			if (model == null || (!string.IsNullOrEmpty(_gameKey) || !string.IsNullOrEmpty(_gameDisplayName)))
			{
				return;
			}

			var assetPath = AssetDatabase.GetAssetPath(model);
			var (gameKey, _) = MaterialDetection.DetectGameAutoOnly(model, assetPath);
			_gameKey = gameKey;
			_gameDisplayName = HoyoToonManagerModelCache.ResolveGameDisplayName(gameKey);

			if (manager != null && (!string.IsNullOrEmpty(_gameKey) || !string.IsNullOrEmpty(_gameDisplayName)))
			{
				HoyoToonManagerModelCache.SetGameInfo(manager, _gameKey, _gameDisplayName);
			}
		}

		private GameGlobalConfig GetCurrentGameConfig()
		{
			if (GameGlobalPropertyMap == null)
			{
				return null;
			}

			if (!string.IsNullOrEmpty(_gameDisplayName) && GameGlobalPropertyMap.TryGetValue(_gameDisplayName, out var configByDisplay))
			{
				return configByDisplay;
			}

			if (!string.IsNullOrEmpty(_gameKey) && GameGlobalPropertyMap.TryGetValue(_gameKey, out var configByKey))
			{
				return configByKey;
			}

			return null;
		}


		private MaterialProperty TryGetMaterialProperty(string propertyName)
		{
			if (string.IsNullOrEmpty(propertyName))
			{
				return null;
			}

			var targets = _uniqueMaterials
				.Where(mat => mat != null && mat.HasProperty(propertyName))
				.Cast<UnityEngine.Object>()
				.ToArray();

			if (targets.Length == 0)
			{
				return null;
			}

			try
			{
				return MaterialEditor.GetMaterialProperty(targets, propertyName);
			}
			catch (ArgumentException)
			{
				return null;
			}
		}

		private static bool IsFloatLike(MaterialProperty property)
		{
			return property != null && (property.type == MaterialProperty.PropType.Float || property.type == MaterialProperty.PropType.Range || property.type == MaterialProperty.PropType.Int);
		}

		private static void DrawFloatField(GUIContent label, MaterialProperty property)
		{
			EditorGUI.BeginChangeCheck();
			var newValue = EditorGUILayout.FloatField(label, property.floatValue);
			if (EditorGUI.EndChangeCheck())
			{
				property.floatValue = newValue;
			}
		}

		private static void DrawRangeField(GUIContent label, MaterialProperty property)
		{
			EditorGUI.BeginChangeCheck();
			var newValue = EditorGUILayout.Slider(label, property.floatValue, property.rangeLimits.x, property.rangeLimits.y);
			if (EditorGUI.EndChangeCheck())
			{
				property.floatValue = newValue;
			}
		}

		private static void DrawColorField(GUIContent label, MaterialProperty property)
		{
			EditorGUI.BeginChangeCheck();
			var newValue = EditorGUILayout.ColorField(label, property.colorValue);
			if (EditorGUI.EndChangeCheck())
			{
				property.colorValue = newValue;
			}
		}

		private static void DrawVectorField(GUIContent label, MaterialProperty property)
		{
			EditorGUI.BeginChangeCheck();
			var newValue = EditorGUILayout.Vector4Field(label.text, property.vectorValue);
			if (EditorGUI.EndChangeCheck())
			{
				property.vectorValue = newValue;
			}
		}

		private static void DrawTextureField(GUIContent label, MaterialProperty property)
		{
			using (new EditorGUILayout.VerticalScope())
			{
				EditorGUI.BeginChangeCheck();
				var textureValue = (Texture)EditorGUILayout.ObjectField(label, property.textureValue, typeof(Texture), false);
				if (EditorGUI.EndChangeCheck())
				{
					property.textureValue = textureValue;
				}

				var scaleOffset = property.textureScaleAndOffset;
				using (new EditorGUI.IndentLevelScope())
				{
					EditorGUI.BeginChangeCheck();
					var newScale = EditorGUILayout.Vector2Field("Tiling", new Vector2(scaleOffset.x, scaleOffset.y));
					var newOffset = EditorGUILayout.Vector2Field("Offset", new Vector2(scaleOffset.z, scaleOffset.w));
					if (EditorGUI.EndChangeCheck())
					{
						property.textureScaleAndOffset = new Vector4(newScale.x, newScale.y, newOffset.x, newOffset.y);
					}
				}
			}
		}

		private static GUIContent BuildLabel(GlobalPropertyDefinition definition, MaterialProperty property)
		{
			var labelText = !string.IsNullOrEmpty(definition.Label)
				? definition.Label
				: (!string.IsNullOrEmpty(property?.displayName) ? property.displayName : definition.PropertyName);

			return string.IsNullOrEmpty(definition.Tooltip)
				? new GUIContent(labelText)
				: new GUIContent(labelText, definition.Tooltip);
		}


		private static GlobalPropertySection Section(string title, params GlobalPropertyDefinition[] properties)
		{
			return new GlobalPropertySection(title, properties);
		}

		private static GlobalPropertyDefinition Prop(string propertyName, string label = null, GlobalPropertyControlHint controlHint = GlobalPropertyControlHint.Auto, string tooltip = null)
		{
			if (string.IsNullOrEmpty(propertyName))
			{
				throw new ArgumentException("Property name cannot be null or empty.", nameof(propertyName));
			}

			return new GlobalPropertyDefinition(propertyName, label, tooltip, controlHint);
		}

		private enum GlobalPropertyControlHint
		{
			Auto = 0,
			Toggle = 1
		}

		private sealed class GameGlobalConfig
		{
			public GameGlobalConfig(string displayName, IEnumerable<GlobalPropertySection> sections)
			{
				DisplayName = string.IsNullOrEmpty(displayName) ? "Unknown Game" : displayName;
				Sections = sections?.Where(section => section != null && section.HasProperties).ToList() ?? new List<GlobalPropertySection>();
				TotalPropertyCount = Sections.Sum(section => section.Properties.Count);
			}

			public string DisplayName { get; }
			public IReadOnlyList<GlobalPropertySection> Sections { get; }
			public int TotalPropertyCount { get; }
			public bool HasProperties => TotalPropertyCount > 0;
		}

		private sealed class GlobalPropertySection
		{
			public GlobalPropertySection(string title, IEnumerable<GlobalPropertyDefinition> properties)
			{
				Title = string.IsNullOrEmpty(title) ? "Properties" : title;
				Properties = properties?.Where(prop => prop != null).ToList() ?? new List<GlobalPropertyDefinition>();
			}

			public string Title { get; }
			public IReadOnlyList<GlobalPropertyDefinition> Properties { get; }
			public bool HasProperties => Properties.Count > 0;
		}

		private sealed class GlobalPropertyDefinition
		{
			public GlobalPropertyDefinition(string propertyName, string label, string tooltip, GlobalPropertyControlHint controlHint)
			{
				PropertyName = propertyName;
				Label = label;
				Tooltip = tooltip;
				ControlHint = controlHint;
			}

			public string PropertyName { get; }
			public string Label { get; }
			public string Tooltip { get; }
			public GlobalPropertyControlHint ControlHint { get; }
		}
	}
}
#endif
