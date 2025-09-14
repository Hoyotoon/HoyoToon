// Copyright (c) Jason Ma

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LWGUI.Timeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LWGUI
{
	/// <summary>
	/// Misc Function
	/// </summary>
	public static class Helper
	{
		
		#region Misc
		
		public static readonly string ProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - 6);

		public static bool IsPropertyHideInInspector(MaterialProperty prop)
		{
			return (prop.GetPropertyFlags() & ShaderPropertyFlags.HideInInspector) != 0;
		}

		public static bool StringToBool(string str) => str?.ToLower() is "on" or "true";
		
		public static string GetKeywordName(string keyword, string propName)
		{
			string k;
			if (string.IsNullOrEmpty(keyword) || keyword == "__")
			{
				k = propName.ToUpperInvariant() + "_ON";
			}
			else
			{
				k = keyword.ToUpperInvariant();
			}
			return k;
		}

		public static void SetShaderKeywordEnabled(Object[] materials, string keywordName, bool isEnable)
		{
			if (string.IsNullOrEmpty(keywordName) || string.IsNullOrEmpty(keywordName)) return;

			foreach (Material m in materials)
			{
				// delete "_" keywords
				if (keywordName == "_")
				{
					if (m.IsKeywordEnabled(keywordName))
					{
						m.DisableKeyword(keywordName);
					}
					continue;
				}

				if (m.IsKeywordEnabled(keywordName))
				{
					if (!isEnable) m.DisableKeyword(keywordName);
				}
				else
				{
					if (isEnable) m.EnableKeyword(keywordName);
				}
			}
		}

		public static void SelectShaderKeyword(Object[] materials, string[] keywordNames, int index)
		{
			Debug.Assert(keywordNames.Length >= 1 && index < keywordNames.Length && index >= 0,
						 "KeyWords Length: " + keywordNames.Length + " or Index: " + index + " Error! ");
			for (int i = 0; i < keywordNames.Length; i++)
			{
				SetShaderKeywordEnabled(materials, keywordNames[i], index == i);
			}
		}

		public static void SetShaderPassEnabled(Object[] materials, string[] lightModeNames, bool enabled)
		{
			if (lightModeNames.Length == 0) return;

			foreach (Material material in materials)
			{
				for (int i = 0; i < lightModeNames.Length; i++)
				{
					material.SetShaderPassEnabled(lightModeNames[i], enabled);
				}
			}
		}

		public static LWGUI GetLWGUI(MaterialEditor editor)
		{
			var customShaderGUI = editor.customShaderGUI;
			if (customShaderGUI != null && customShaderGUI is LWGUI)
			{
				LWGUI gui = customShaderGUI as LWGUI;
				return gui;
			}
			else
			{
				Debug.LogWarning("LWGUI: Please add \"CustomEditor \"LWGUI.LWGUI\"\" to the end of your shader!");
				return null;
			}
		}

		public static LWGUIMetaDatas GetLWGUIMetadatas(MaterialEditor editor) => GetLWGUI(editor).metaDatas;

		public static void AdaptiveFieldWidth(GUIStyle style, GUIContent content)
		{
			var extraTextWidth = Mathf.Max(0, style.CalcSize(content).x - (EditorGUIUtility.fieldWidth - RevertableHelper.revertButtonWidth));
			EditorGUIUtility.labelWidth -= extraTextWidth;
			EditorGUIUtility.fieldWidth += extraTextWidth;
		}

		public static void BeginProperty(Rect rect, MaterialProperty property, LWGUIMetaDatas metaDatas)
		{
#if UNITY_2022_1_OR_NEWER
			MaterialEditor.BeginProperty(rect, property);
			foreach (var extraPropName in metaDatas.GetPropStaticData(property.name).extraPropNames)
				MaterialEditor.BeginProperty(rect, metaDatas.GetPropDynamicData(extraPropName).property);
#endif
		}

		public static void EndProperty(LWGUIMetaDatas metaDatas, MaterialProperty property)
		{
#if UNITY_2022_1_OR_NEWER
			MaterialEditor.EndProperty();
			foreach (var extraPropName in metaDatas.GetPropStaticData(property.name).extraPropNames)
				MaterialEditor.EndProperty();
#endif
		}

		public static bool EndChangeCheck(LWGUIMetaDatas metaDatas, MaterialProperty property)
		{
			return metaDatas.perMaterialData.EndChangeCheck(property.name);
		}

		public static float GetCurrentPropertyLayoutWidth()
		{
			return ReflectionHelper.EditorGUILayout_kLabelFloatMinW - ReflectionHelper.EditorGUI_Indent - RevertableHelper.revertButtonWidth - 2;
		}

		#endregion


		#region Math

		public const double Float_Epsilon = 1e-10;

		public static bool Approximately(float a, float b) => Mathf.Abs(a - b) < Float_Epsilon;
		
		public static bool PropertyValueEquals(MaterialProperty prop1, MaterialProperty prop2)
		{
			if (prop1.textureValue == prop2.textureValue
			    && prop1.vectorValue == prop2.vectorValue
			    && prop1.colorValue == prop2.colorValue
			    && Approximately(prop1.floatValue, prop2.floatValue)
			    && prop1.intValue == prop2.intValue
			   )
				return true;
			else
				return false;
		}

		public static float PowPreserveSign(float f, float p)
		{
			float num = Mathf.Pow(Mathf.Abs(f), p);
			if ((double)f < 0.0)
				return -num;
			return num;
		}

		#endregion


		#region GUI Styles

		// Tips: Use properties to fix null reference errors

		private static GUIStyle _guiStyles_IconButton;
		public static GUIStyle guiStyles_IconButton => _guiStyles_IconButton ?? new GUIStyle(EditorStyles.iconButton) { fixedHeight = 0, fixedWidth = 0 };

		private static GUIStyle _guiStyle_Foldout;
		public static GUIStyle guiStyle_Foldout => _guiStyle_Foldout ?? new GUIStyle(EditorStyles.miniButton)
		{
			contentOffset = new Vector2(22, 0),
			fixedHeight = 27,
			alignment = TextAnchor.MiddleLeft,
			font = EditorStyles.boldLabel.font,
			fontSize = EditorStyles.boldLabel.fontSize + 1
		};

		private static GUIStyle _guiStyle_Helpbox;
		public static GUIStyle guiStyle_Helpbox => _guiStyle_Helpbox ?? new GUIStyle(EditorStyles.helpBox) { fontSize = 12 };
		
		private static GUIStyle _guiStyle_RampSelectButton;
		public static GUIStyle guiStyle_RampSelectButton => _guiStyle_RampSelectButton ?? new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft };

		private static GUIStyle _guiStyles_ToolbarSearchTextFieldPopup;
		public static GUIStyle guiStyles_ToolbarSearchTextFieldPopup
		{
			get
			{
				if (_guiStyles_ToolbarSearchTextFieldPopup == null)
				{
					string toolbarSeachTextFieldPopupStr = "ToolbarSeachTextFieldPopup";
					{
						// ToolbarSeachTextFieldPopup has renamed at Unity 2021.3.28+
#if !UNITY_2022_3_OR_NEWER
						string[] versionParts = Application.unityVersion.Split('.');
						int majorVersion = int.Parse(versionParts[0]);
						int minorVersion = int.Parse(versionParts[1]);
						Match patchVersionMatch = Regex.Match(versionParts[2], @"\d+");
						int patchVersion = int.Parse(patchVersionMatch.Value);
						if (majorVersion >= 2021 && minorVersion >= 3 && patchVersion >= 28)
#endif
						{
							toolbarSeachTextFieldPopupStr = "ToolbarSearchTextFieldPopup";
						}
					}
					_guiStyles_ToolbarSearchTextFieldPopup = new GUIStyle(toolbarSeachTextFieldPopupStr);
				}
				return _guiStyles_ToolbarSearchTextFieldPopup;
			}
		}

		#endregion


		#region Draw GUI for Drawers

		// TODO: use Reflection
		// copy and edit of https://github.com/GucioDevs/SimpleMinMaxSlider/blob/master/Assets/SimpleMinMaxSlider/Scripts/Editor/MinMaxSliderDrawer.cs
		public static Rect[] SplitRect(Rect rectToSplit, int n)
		{
			Rect[] rects = new Rect[n];

			for (int i = 0; i < n; i++)
			{
				rects[i] = new Rect(rectToSplit.position.x + (i * rectToSplit.width / n), rectToSplit.position.y,
									rectToSplit.width / n, rectToSplit.height);
			}

			int padding = (int)rects[0].width - 50; // use 50, enough to show 0.xx (2 digits)
			int space = 5;

			rects[0].width -= padding + space;
			rects[2].width -= padding + space;

			rects[1].x -= padding;
			rects[1].width += padding * 2;

			rects[2].x += padding + space;

			return rects;
		}

		public static bool DrawFoldout(Rect rect, ref bool isFolding, bool toggleValue, bool hasToggle, GUIContent label)
		{
			var toggleRect = new Rect(rect.x + 8f, rect.y + 7f, 13f, 13f);

			// Toggle Event
			if (hasToggle)
			{
				if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && toggleRect.Contains(Event.current.mousePosition))
				{
					toggleValue = !toggleValue;
					Event.current.Use();
					GUI.changed = true;
				}
			}

			// Button
			{
				// Cancel Right Click
				if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
					Event.current.Use();

				var enabled = GUI.enabled;
				GUI.enabled = true;
				var guiColor = GUI.backgroundColor;
				GUI.backgroundColor = isFolding ? Color.white : new Color(0.85f, 0.85f, 0.85f);
				if (GUI.Button(rect, label, guiStyle_Foldout))
				{
					isFolding = !isFolding;
					GUI.changed = false;
				}
				GUI.backgroundColor = guiColor;
				GUI.enabled = enabled;
			}

			// Toggle Icon
			if (hasToggle)
			{
				EditorGUI.Toggle(toggleRect, string.Empty, toggleValue);
			}

			return toggleValue;
		}

		public static bool ToggleButton(Rect position, GUIContent label, bool on, GUIStyle style = null, float padding = 0)
		{
			var paddedRect = new Rect(position.x + padding, position.y, position.width - padding * 2, position.height);
			style ??= EditorStyles.miniButton;
			
			bool flag = GUI.Button(paddedRect, label, style);
			if (Event.current.type == EventType.Repaint)
			{
				bool isHover = paddedRect.Contains(Event.current.mousePosition);
				style.Draw(position, label, isHover, false, on, false);
			}

			return flag;
		}

		public static void DrawShaderPropertyWithErrorLabel(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor, string message)
		{
			var c = GUI.color;
			GUI.color = Color.red;
			var newLabel = $"{ label.text } ({ message })";
			editor.DefaultShaderProperty(position, prop, newLabel);
			GUI.color = c;
		}

		#endregion


		#region Draw GUI for Materials

		public static void DrawSplitLine()
		{
			var rect = EditorGUILayout.GetControlRect(true, 1);
			rect.x = 0;
			rect.width = EditorGUIUtility.currentViewWidth;
			EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.45f));
		}

		private static readonly Texture2D _helpboxIcon = EditorGUIUtility.IconContent("console.infoicon").image as Texture2D;

		public static void DrawHelpbox(PropertyStaticData propertyStaticData, PropertyDynamicData propertyDynamicData)
		{
			var helpboxStr = propertyStaticData.helpboxMessages;
			if (!string.IsNullOrEmpty(helpboxStr))
			{
				var content = new GUIContent(helpboxStr, _helpboxIcon);
				var textWidth = GetCurrentPropertyLayoutWidth();
				var textHeight = guiStyle_Helpbox.CalcHeight(content, textWidth);
				var helpboxRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(true, textHeight));
				helpboxRect.xMax -= RevertableHelper.revertButtonWidth;
				GUI.Label(helpboxRect, content, guiStyle_Helpbox);
				// EditorGUI.HelpBox(helpboxRect, helpboxStr, MessageType.Info);
			}
		}

		private static Texture _logoCache;
		private static GUIContent _logoGuiContentCache;
		private static Texture _logo => _logoCache = _logoCache ?? AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath("26b9d845eb7b1a747bf04dc84e5bcc2c"));
		private static GUIContent _logoGuiContent => _logoGuiContentCache = _logoGuiContentCache ?? new GUIContent(string.Empty, _logo,
																   "LWGUI (Light Weight Shader GUI)\n\n"
																 + "A Lightweight, Flexible, Powerful Unity Shader GUI system.\n\n"
																 + "Copyright (c) Jason Ma");

		public static void DrawLogo()
		{
			var logoRect = EditorGUILayout.GetControlRect(false, _logo.height);
			var w = logoRect.width;
			logoRect.xMin += w * 0.5f - _logo.width * 0.5f;
			logoRect.xMax -= w * 0.5f - _logo.width * 0.5f;

			if (EditorGUIUtility.currentViewWidth >= logoRect.width && GUI.Button(logoRect, _logoGuiContent, guiStyles_IconButton))
			{
				Application.OpenURL("https://github.com/JasonMa0012/LWGUI");
			}
		}

		#endregion


		#region Toolbar Buttons

		private static Material     _copiedMaterial;
		private static List<string> _copiedProps = new List<string>();

		private const string _iconCopyGUID       = "9cdef444d18d2ce4abb6bbc4fed4d109";
		private const string _iconPasteGUID      = "8e7a78d02e4c3574998524a0842a8ccb";
		private const string _iconSelectGUID     = "6f44e40b24300974eb607293e4224ecc";
		private const string _iconCheckoutGUID   = "72488141525eaa8499e65e52755cb6d0";
		private const string _iconExpandGUID     = "2382450e7f4ddb94c9180d6634c41378";
		private const string _iconCollapseGUID   = "929b6e5dfacc42b429d715a3e1ca2b57";
		private const string _iconVisibilityGUID = "9576e23a695b35d49a9fc55c9a948b4f";

		private const string _iconCopyTooltip       = "Copy Material Properties";
		private const string _iconPasteTooltip      = "Paste Material Properties\n\nRight-click to paste values by type.";
		private const string _iconSelectTooltip     = "Select the Material Asset\n\nUsed to jump from a Runtime Material Instance to a Material Asset.";
		private const string _iconCheckoutTooltip   = "Checkout selected Material Assets";
		private const string _iconExpandTooltip     = "Expand All Groups";
		private const string _iconCollapseTooltip   = "Collapse All Groups";
		private const string _iconVisibilityTooltip = "Display Mode";

		private static GUIContent _guiContentCopyCache;
		private static GUIContent _guiContentPasteCache;
		private static GUIContent _guiContentSelectCache;
		private static GUIContent _guiContentChechoutCache;
		private static GUIContent _guiContentExpandCache;
		private static GUIContent _guiContentCollapseCache;
		private static GUIContent _guiContentVisibilityCache;
		
		private static GUIContent _guiContentCopy       => _guiContentCopyCache = _guiContentCopyCache ?? new GUIContent("", AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(_iconCopyGUID)), _iconCopyTooltip);
		private static GUIContent _guiContentPaste      => _guiContentPasteCache = _guiContentPasteCache ?? new GUIContent("", AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(_iconPasteGUID)), _iconPasteTooltip);
		private static GUIContent _guiContentSelect     => _guiContentSelectCache = _guiContentSelectCache ?? new GUIContent("", AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(_iconSelectGUID)), _iconSelectTooltip);
		private static GUIContent _guiContentChechout   => _guiContentChechoutCache = _guiContentChechoutCache ?? new GUIContent("", AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(_iconCheckoutGUID)), _iconCheckoutTooltip);
		private static GUIContent _guiContentExpand     => _guiContentExpandCache = _guiContentExpandCache ?? new GUIContent("", AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(_iconExpandGUID)), _iconExpandTooltip);
		private static GUIContent _guiContentCollapse   => _guiContentCollapseCache = _guiContentCollapseCache ?? new GUIContent("", AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(_iconCollapseGUID)), _iconCollapseTooltip);
		private static GUIContent _guiContentVisibility => _guiContentVisibilityCache = _guiContentVisibilityCache ?? new GUIContent("", AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(_iconVisibilityGUID)), _iconVisibilityTooltip);


		private enum CopyMaterialValueMask
		{
			Float       = 1 << 0,
			Vector      = 1 << 1,
			Texture     = 1 << 2,
			Keyword     = 1 << 3,
			RenderQueue = 1 << 4,
			Number      = Float | Vector,
			All         = (1 << 5) - 1,
		}

		private static GUIContent[] _pasteMaterialMenus = new[]
		{
			new GUIContent("Paste Number Values"),
			new GUIContent("Paste Texture Values"),
			new GUIContent("Paste Keywords"),
			new GUIContent("Paste RenderQueue"),
		};

		private static uint[] _pasteMaterialMenuValueMasks = new[]
		{
			(uint)CopyMaterialValueMask.Number,
			(uint)CopyMaterialValueMask.Texture,
			(uint)CopyMaterialValueMask.Keyword,
			(uint)CopyMaterialValueMask.RenderQueue,
		};

		private static void DoPasteMaterialProperties(LWGUIMetaDatas metaDatas, uint valueMask)
		{
			if (!_copiedMaterial)
			{
				Debug.LogError("LWGUI: Please copy Material Properties first!");
				return;
			}

			var targetMaterials = metaDatas.GetMaterialEditor().targets;
			if (!VersionControlHelper.Checkout(targetMaterials))
			{
				Debug.LogError("LWGUI: One or more materials unable to write!");
				return;
			}

			Undo.RecordObjects(targetMaterials, "LWGUI: Paste Material Properties");
			foreach (Material material in targetMaterials)
			{
				for (int i = 0; i < _copiedMaterial.shader.GetPropertyCount(); i++)
				{
					var name = _copiedMaterial.shader.GetPropertyName(i);
					var type = _copiedMaterial.shader.GetPropertyType(i);
					PastePropertyValueToMaterial(material, name, name, type, valueMask);
				}
				if ((valueMask & (uint)CopyMaterialValueMask.Keyword) != 0)
					material.shaderKeywords = _copiedMaterial.shaderKeywords;
				if ((valueMask & (uint)CopyMaterialValueMask.RenderQueue) != 0)
					material.renderQueue = _copiedMaterial.renderQueue;
			}
		}

		private static void PastePropertyValueToMaterial(Material material, string srcName, string dstName)
		{
			for (int i = 0; i < _copiedMaterial.shader.GetPropertyCount(); i++)
			{
				var name = _copiedMaterial.shader.GetPropertyName(i);
				if (name == srcName)
				{
					var type = _copiedMaterial.shader.GetPropertyType(i);
					PastePropertyValueToMaterial(material, srcName, dstName, type);
					return;
				}
			}
		}

		private static void PastePropertyValueToMaterial(Material material, string srcName, string dstName, ShaderPropertyType type, uint valueMask = (uint)CopyMaterialValueMask.All)
		{
			switch (type)
			{
				case ShaderPropertyType.Color:
					if ((valueMask & (uint)CopyMaterialValueMask.Vector) != 0)
						material.SetColor(dstName, _copiedMaterial.GetColor(srcName));
					break;
				case ShaderPropertyType.Vector:
					if ((valueMask & (uint)CopyMaterialValueMask.Vector) != 0)
						material.SetVector(dstName, _copiedMaterial.GetVector(srcName));
					break;
				case ShaderPropertyType.Texture:
					if ((valueMask & (uint)CopyMaterialValueMask.Texture) != 0)
						material.SetTexture(dstName, _copiedMaterial.GetTexture(srcName));
					break;
				// Float
				default:
					if ((valueMask & (uint)CopyMaterialValueMask.Float) != 0)
						material.SetFloat(dstName, _copiedMaterial.GetFloat(srcName));
					break;
			}
		}

		public static void DrawToolbarButtons(ref Rect toolBarRect, LWGUIMetaDatas metaDatas)
		{
			var (perShaderData, perMaterialData, perInspectorData) = metaDatas.GetDatas();

			// Copy
			var buttonRectOffset = toolBarRect.height + 2;
			var buttonRect = new Rect(toolBarRect.x, toolBarRect.y, toolBarRect.height, toolBarRect.height);
			toolBarRect.xMin += buttonRectOffset;
			if (GUI.Button(buttonRect, _guiContentCopy, Helper.guiStyles_IconButton))
			{
				_copiedMaterial = UnityEngine.Object.Instantiate(metaDatas.GetMaterial());
			}

			// Paste
			buttonRect.x += buttonRectOffset;
			toolBarRect.xMin += buttonRectOffset;
			// Right Click
			if (Event.current.type == EventType.MouseDown
			 && Event.current.button == 1
			 && buttonRect.Contains(Event.current.mousePosition))
			{
				EditorUtility.DisplayCustomMenu(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0), _pasteMaterialMenus, -1,
												(data, options, selected) =>
												{
													DoPasteMaterialProperties(metaDatas, _pasteMaterialMenuValueMasks[selected]);
												}, null);
				Event.current.Use();
			}
			// Left Click
			if (GUI.Button(buttonRect, _guiContentPaste, Helper.guiStyles_IconButton))
			{
				DoPasteMaterialProperties(metaDatas, (uint)CopyMaterialValueMask.All);
			}

			// Select Material Asset, jump from a Runtime Material Instance to a Material Asset
			buttonRect.x += buttonRectOffset;
			toolBarRect.xMin += buttonRectOffset;
			if (GUI.Button(buttonRect, _guiContentSelect, Helper.guiStyles_IconButton))
			{
				var material = metaDatas.GetMaterial();

				if (AssetDatabase.Contains(material))
				{
					Selection.activeObject = material;
				}
				else
				{
					if (FindMaterialAssetByMaterialInstance(material, metaDatas, out var materialAsset))
					{
						Selection.activeObject = materialAsset;
					}
				}
			}

			// Checkout
			buttonRect.x += buttonRectOffset;
			toolBarRect.xMin += buttonRectOffset;
			if (GUI.Button(buttonRect, _guiContentChechout, Helper.guiStyles_IconButton))
			{
				VersionControlHelper.Checkout(metaDatas.GetMaterialEditor().targets);
			}

			// Expand
			buttonRect.x += buttonRectOffset;
			toolBarRect.xMin += buttonRectOffset;
			if (GUI.Button(buttonRect, _guiContentExpand, Helper.guiStyles_IconButton))
			{
				foreach (var propStaticDataKVPair in perShaderData.propStaticDatas)
				{
					if (propStaticDataKVPair.Value.isMain || propStaticDataKVPair.Value.isAdvancedHeader)
						propStaticDataKVPair.Value.isExpanding = true;
				}
			}

			// Collapse
			buttonRect.x += buttonRectOffset;
			toolBarRect.xMin += buttonRectOffset;
			if (GUI.Button(buttonRect, _guiContentCollapse, Helper.guiStyles_IconButton))
			{
				foreach (var propStaticDataKVPair in perShaderData.propStaticDatas)
				{
					if (propStaticDataKVPair.Value.isMain || propStaticDataKVPair.Value.isAdvancedHeader)
						propStaticDataKVPair.Value.isExpanding = false;
				}
			}

			// Display Mode
			buttonRect.x += buttonRectOffset;
			toolBarRect.xMin += buttonRectOffset;
			var color = GUI.color;
			var displayModeData = perShaderData.displayModeData;
			if (!displayModeData.IsDefaultDisplayMode())
				GUI.color = Color.yellow;
			if (GUI.Button(buttonRect, _guiContentVisibility, Helper.guiStyles_IconButton))
			{
				// Build Display Mode Menu Items
				var displayModeMenus = new[]
				{
					$"Show All Advanced Properties				({ displayModeData.advancedCount } - { perShaderData.propStaticDatas.Count })",
					$"Show All Hidden Properties				({ displayModeData.hiddenCount } - { perShaderData.propStaticDatas.Count })",
					$"Show Only Modified Properties				({ perMaterialData.modifiedCount } - { perShaderData.propStaticDatas.Count })",
					$"Show Only Modified Properties by Group	({ perMaterialData.modifiedCount } - { perShaderData.propStaticDatas.Count })",
				};
				var enabled = new[] { true, true, true, true };
				var separator = new bool[4];
				var selected = new[]
				{
					displayModeData.showAllAdvancedProperties ? 0 : -1,
					displayModeData.showAllHiddenProperties ? 1 : -1,
					displayModeData.showOnlyModifiedProperties ? 2 : -1,
					displayModeData.showOnlyModifiedGroups ? 3 : -1,
				};


				// Click Event
				void OnSwitchDisplayMode(object data, string[] options, int selectedIndex)
				{
					switch (selectedIndex)
					{
						case 0: // Show All Advanced Properties
							displayModeData.showAllAdvancedProperties = !displayModeData.showAllAdvancedProperties;
							perShaderData.ToggleShowAllAdvancedProperties();
							break;
						case 1: // Show All Hidden Properties
							displayModeData.showAllHiddenProperties = !displayModeData.showAllHiddenProperties;
							break;
						case 2: // Show Only Modified Properties
							displayModeData.showOnlyModifiedProperties = !displayModeData.showOnlyModifiedProperties;
							if (displayModeData.showOnlyModifiedProperties) displayModeData.showOnlyModifiedGroups = false;
							MetaDataHelper.ForceUpdateAllMaterialsMetadataCache(metaDatas.GetShader());
							break;
						case 3: // Show Only Modified Groups
							displayModeData.showOnlyModifiedGroups = !displayModeData.showOnlyModifiedGroups;
							if (displayModeData.showOnlyModifiedGroups) displayModeData.showOnlyModifiedProperties = false;
							MetaDataHelper.ForceUpdateAllMaterialsMetadataCache(metaDatas.GetShader());
							break;
					}
				}

				ReflectionHelper.DisplayCustomMenuWithSeparators(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0),
																 displayModeMenus, enabled, separator, selected, OnSwitchDisplayMode);
			}
			GUI.color = color;

			toolBarRect.xMin += 2;
		}

		public static Func<Renderer, Material, Material> onFindMaterialAssetInRendererByMaterialInstance;
		
		private static bool FindMaterialAssetByMaterialInstance(Material material, LWGUIMetaDatas metaDatas, out Material materialAsset)
		{
			materialAsset = null;
			
			var renderers = metaDatas.perInspectorData.materialEditor.GetMeshRenderersByMaterialEditor();
			foreach (var renderer in renderers)
			{
				if (onFindMaterialAssetInRendererByMaterialInstance != null)
				{
					materialAsset = onFindMaterialAssetInRendererByMaterialInstance(renderer, material);
				}
				
				if (materialAsset == null)
				{
					int index = renderer.materials.ToList().FindIndex(materialInstance => materialInstance == material);
					if (index >= 0 && index < renderer.sharedMaterials.Length)
					{
						materialAsset = renderer.sharedMaterials[index];
					}
				}
				
				if (materialAsset != null && AssetDatabase.Contains(materialAsset))
					return true;
			}
			
			Debug.LogError("LWGUI: Can not find the Material Assets of: " + material.name);

			return false;
		}

		#endregion


		#region Search Field

		private static readonly int s_TextFieldHash = "EditorTextField".GetHashCode();
		private static readonly GUIContent[] _searchModeMenus = Enumerable.Range(0, (int)SearchMode.Num - 1).Select(i => 
				new GUIContent(((SearchMode)i).ToString())).ToArray();

		/// <returns>is has changed?</returns>
		public static bool DrawSearchField(Rect rect, LWGUIMetaDatas metaDatas)
		{
			var (perShaderData, perMaterialData, perInspectorData) = metaDatas.GetDatas();

			bool hasChanged = false;
			EditorGUI.BeginChangeCheck();

			var revertButtonRect = RevertableHelper.SplitRevertButtonRect(ref rect);

			// Get internal TextField ControlID
			int controlId = GUIUtility.GetControlID(s_TextFieldHash, FocusType.Keyboard, rect) + 1;

			// searching mode
			Rect modeRect = new Rect(rect);
			modeRect.width = 20f;
			if (Event.current.type == UnityEngine.EventType.MouseDown && modeRect.Contains(Event.current.mousePosition))
			{
				EditorUtility.DisplayCustomMenu(rect, _searchModeMenus, (int)perShaderData.searchMode,
												(data, options, selected) =>
												{
													perShaderData.searchMode = (SearchMode)selected;
													hasChanged = true;
												}, null);
				Event.current.Use();
			}

			perShaderData.searchString = EditorGUI.TextField(rect, String.Empty, perShaderData.searchString, guiStyles_ToolbarSearchTextFieldPopup);

			if (EditorGUI.EndChangeCheck())
				hasChanged = true;

			// revert button
			if (!string.IsNullOrEmpty(perShaderData.searchString)
			 && RevertableHelper.DrawRevertButton(revertButtonRect))
			{
				perShaderData.searchString = string.Empty;
				hasChanged = true;
				GUIUtility.keyboardControl = 0;
			}

			// display search mode
			if (GUIUtility.keyboardControl != controlId
			 && string.IsNullOrEmpty(perShaderData.searchString)
			 && Event.current.type == UnityEngine.EventType.Repaint)
			{
				using (new EditorGUI.DisabledScope(true))
				{
					var disableTextRect = new Rect(rect.x, rect.y, rect.width,
												   guiStyles_ToolbarSearchTextFieldPopup.fixedHeight > 0.0
													   ? guiStyles_ToolbarSearchTextFieldPopup.fixedHeight
													   : rect.height);
					disableTextRect = guiStyles_ToolbarSearchTextFieldPopup.padding.Remove(disableTextRect);
					int fontSize = EditorStyles.label.fontSize;
					EditorStyles.label.fontSize = guiStyles_ToolbarSearchTextFieldPopup.fontSize;
					EditorStyles.label.Draw(disableTextRect, new GUIContent(perShaderData.searchMode.ToString()), false, false, false, false);
					EditorStyles.label.fontSize = fontSize;
				}
			}

			if (hasChanged) perShaderData.UpdateSearchFilter();

			return hasChanged;
		}

		#endregion


		#region Context Menu

		private static void EditPresetEvent(string mode, LwguiShaderPropertyPreset presetAsset, List<LwguiShaderPropertyPreset.Preset> targetPresets, MaterialProperty prop, LWGUIMetaDatas metaDatas)
		{
			if (!VersionControlHelper.Checkout(presetAsset))
			{
				Debug.LogError("LWGUI: Can not edit the preset: " + presetAsset);
				return;
			}
			foreach (var targetPreset in targetPresets)
			{
				switch (mode)
				{
					case "Add":
					case "Update":
						targetPreset.AddOrUpdateIncludeExtraProperties(metaDatas, prop);
						break;
					case "Remove":
						targetPreset.RemoveIncludeExtraProperties(metaDatas, prop.name);
						break;
				}
			}
			EditorUtility.SetDirty(presetAsset);
			MetaDataHelper.ForceUpdateMaterialMetadataCache(metaDatas.GetMaterial());
		}

		public static void DoPropertyContextMenus(Rect rect, MaterialProperty prop, LWGUIMetaDatas metaDatas)
		{
			if (Event.current.type != EventType.ContextClick || !rect.Contains(Event.current.mousePosition)) return;

			Event.current.Use();

			var (perShaderData, perMaterialData, perInspectorData) = metaDatas.GetDatas();
			var (propStaticData, propDynamicData) = metaDatas.GetPropDatas(prop);
			var menu = new GenericMenu();

			// 2022+ Material Varant Menus
#if UNITY_2022_1_OR_NEWER
			ReflectionHelper.HandleApplyRevert(menu, prop);
#endif

			// Copy
			menu.AddItem(new GUIContent("Copy"), false, () =>
			{
				_copiedMaterial = UnityEngine.Object.Instantiate(metaDatas.GetMaterial());
				_copiedProps.Clear();
				_copiedProps.Add(prop.name);
				foreach (var extraPropName in propStaticData.extraPropNames)
				{
					_copiedProps.Add(extraPropName);
				}

				// Copy Children
				foreach (var childPropStaticData in propStaticData.children)
				{
					_copiedProps.Add(childPropStaticData.name);
					foreach (var extraPropName in childPropStaticData.extraPropNames)
					{
						_copiedProps.Add(extraPropName);
					}

					foreach (var childChildPropStaticData in childPropStaticData.children)
					{
						_copiedProps.Add(childChildPropStaticData.name);
						foreach (var extraPropName in childChildPropStaticData.extraPropNames)
						{
							_copiedProps.Add(extraPropName);
						}
					}
				}
			});

			// Paste
			GenericMenu.MenuFunction pasteAction = () =>
			{
				if (!VersionControlHelper.Checkout(prop.targets))
				{
					Debug.LogError("LWGUI: One or more materials unable to write!");
					return;
				}

				Undo.RecordObjects(prop.targets, "LWGUI: Paste Material Properties");

				foreach (Material material in prop.targets)
				{
					var index = 0;

					PastePropertyValueToMaterial(material, _copiedProps[index++], prop.name);
					foreach (var extraPropName in propStaticData.extraPropNames)
					{
						if (index == _copiedProps.Count) break;
						PastePropertyValueToMaterial(material, _copiedProps[index++], extraPropName);
					}

					// Paste Children
					foreach (var childPropStaticData in propStaticData.children)
					{
						if (index == _copiedProps.Count) break;
						PastePropertyValueToMaterial(material, _copiedProps[index++], childPropStaticData.name);
						foreach (var extraPropName in childPropStaticData.extraPropNames)
						{
							if (index == _copiedProps.Count) break;
							PastePropertyValueToMaterial(material, _copiedProps[index++], extraPropName);
						}

						foreach (var childChildPropStaticData in childPropStaticData.children)
						{
							if (index == _copiedProps.Count) break;
							PastePropertyValueToMaterial(material, _copiedProps[index++], childChildPropStaticData.name);
							foreach (var extraPropName in childChildPropStaticData.extraPropNames)
							{
								if (index == _copiedProps.Count) break;
								PastePropertyValueToMaterial(material, _copiedProps[index++], extraPropName);
							}
						}
					}
					MetaDataHelper.ForceUpdateMaterialMetadataCache(material);
				}
			};

			if (_copiedMaterial != null && _copiedProps.Count > 0 && GUI.enabled)
				menu.AddItem(new GUIContent("Paste"), false, pasteAction);
			else
				menu.AddDisabledItem(new GUIContent("Paste"));

			menu.AddSeparator("");

			// Copy Display Name
			menu.AddItem(new GUIContent("Copy Display Name"), false, () =>
			{
				EditorGUIUtility.systemCopyBuffer = propStaticData.displayName;
			});

			// Copy Property Names
			menu.AddItem(new GUIContent("Copy Property Names"), false, () =>
			{
				EditorGUIUtility.systemCopyBuffer = prop.name;
				foreach (var extraPropName in propStaticData.extraPropNames)
				{
					EditorGUIUtility.systemCopyBuffer += ", " + extraPropName;
				}
			});

			// menus.AddSeparator("");
			//
			// // Add to Favorites
			// menus.AddItem(new GUIContent("Add to Favorites"), false, () =>
			// {
			// });
			//
			// // Remove from Favorites
			// menus.AddItem(new GUIContent("Remove from Favorites"), false, () =>
			// {
			// });

			// Preset
			if (GUI.enabled)
			{
				menu.AddSeparator("");
				foreach (var activePresetData in perMaterialData.activePresetDatas)
				{
					// Cull self
					if (activePresetData.property == prop) continue;

					var activePreset = activePresetData.preset;
					var (presetPropStaticData, presetPropDynamicData) = metaDatas.GetPropDatas(activePresetData.property);
					var presetAsset = presetPropStaticData.propertyPresetAsset;
					var presetPropDisplayName = presetPropStaticData.displayName;
					
					// Cull invisible presets
					if (!presetPropDynamicData.isShowing) continue;

					if (activePreset.GetPropertyValue(prop.name) != null)
					{
						menu.AddItem(new GUIContent("Update to Preset/" + presetPropDisplayName + "/" + "All"), false, () => EditPresetEvent("Update", presetAsset, presetAsset.GetPresets(), prop, metaDatas));
						menu.AddItem(new GUIContent("Update to Preset/" + presetPropDisplayName + "/" + activePreset.presetName), false, () => EditPresetEvent("Update", presetAsset, new List<LwguiShaderPropertyPreset.Preset>(){activePreset}, prop, metaDatas));
						menu.AddItem(new GUIContent("Remove from Preset/" + presetPropDisplayName + "/" + "All"), false, () => EditPresetEvent("Remove", presetAsset, presetAsset.GetPresets(), prop, metaDatas));
						menu.AddItem(new GUIContent("Remove from Preset/" + presetPropDisplayName + "/" + activePreset.presetName), false, () => EditPresetEvent("Remove", presetAsset, new List<LwguiShaderPropertyPreset.Preset>(){activePreset}, prop, metaDatas));
					}
					else
					{
						menu.AddItem(new GUIContent("Add to Preset/" + presetPropDisplayName + "/" + "All"), false, () => EditPresetEvent("Add", presetAsset, presetAsset.GetPresets(), prop, metaDatas));
						menu.AddItem(new GUIContent("Add to Preset/" + presetPropDisplayName + "/" + activePreset.presetName), false, () => EditPresetEvent("Add", presetAsset, new List<LwguiShaderPropertyPreset.Preset>(){activePreset}, prop, metaDatas));
					}
				}
			}
			
			// Custom
			if (propStaticData.baseDrawers != null)
			{
				foreach (var baseDrawer in propStaticData.baseDrawers)
				{
					baseDrawer.GetCustomContextMenus(menu, rect, prop, metaDatas);
				}
			}

			menu.ShowAsContext();
		}

		#endregion

		#region Importer

		// https://docs.unity3d.com/ScriptReference/TextureImporter.GetPlatformTextureSettings.html
		public static string[] platformNamesForTextureSettings => new[] { "DefaultTexturePlatform", "Standalone", "Web", "iPhone", "Android", "WebGL", "Windows Store Apps", "PS4", "XboxOne", "Nintendo Switch", "tvOS" };

		#endregion
	}
}