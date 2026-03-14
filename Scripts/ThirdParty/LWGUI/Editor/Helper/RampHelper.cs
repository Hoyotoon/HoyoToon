// Copyright (c) Jason Ma
using System;
using System.IO;
using System.Linq;
using LWGUI.LwguiGradientEditor;
using LWGUI.Runtime.LwguiGradient;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LWGUI
{
	public static class RampHelper
	{
		#region RampEditor

		private static readonly GUIContent _iconAdd     = new GUIContent(EditorGUIUtility.IconContent("d_Toolbar Plus").image, "Add"),
										   _iconEdit    = new GUIContent(EditorGUIUtility.IconContent("editicon.sml").image, "Edit"),
										   _iconDiscard = new GUIContent(EditorGUIUtility.IconContent("d_TreeEditor.Refresh").image, "Discard"),
										   _iconSave    = new GUIContent(EditorGUIUtility.IconContent("SaveActive").image, "Save");

		public static void RampEditor(
			Rect buttonRect,
			ref LwguiGradient gradient,
			ColorSpace colorSpace,
			LwguiGradient.ChannelMask viewChannelMask,
			LwguiGradient.GradientTimeRange timeRange,
			bool isDirty,
			out bool hasChange,
			out bool doEditWhenNoGradient,
			out bool doRegisterUndo,
			out bool doCreate,
			out bool doSave,
			out bool doDiscard,
			LwguiGradientWindow.ChangeGradientCallback onChangeGradient = null
			)
		{
			var hasNoGradient = gradient == null;
			var _doEditWhenNoGradient = false;
			var doOpenWindow = false;
			var singleButtonWidth = buttonRect.width * 0.25f;
			var editRect = new Rect(buttonRect.x + singleButtonWidth * 0, buttonRect.y, singleButtonWidth, buttonRect.height);
			var saveRect = new Rect(buttonRect.x + singleButtonWidth * 1, buttonRect.y, singleButtonWidth, buttonRect.height);
			var addRect = new Rect(buttonRect.x + singleButtonWidth * 2, buttonRect.y, singleButtonWidth, buttonRect.height);
			var discardRect = new Rect(buttonRect.x + singleButtonWidth * 3, buttonRect.y, singleButtonWidth, buttonRect.height);

			// Edit button event
			hasChange = false;
			{
				EditorGUI.BeginChangeCheck();
				LwguiGradientEditorHelper.GradientEditButton(editRect, _iconEdit, gradient, colorSpace, viewChannelMask, timeRange, () =>
				{
					// if the current edited texture is null, create new one
					if (hasNoGradient)
					{
						_doEditWhenNoGradient = true;
						Event.current.Use();
						return false;
					}
					else
					{
						doOpenWindow = true;
						return true;
					}
				}, onChangeGradient);
				if (EditorGUI.EndChangeCheck())
				{
					hasChange = true;
					if (LwguiGradientWindow.instance)
					{
						gradient = LwguiGradientWindow.instance.lwguiGradient;
					}
				}

				doRegisterUndo = doOpenWindow;
			}
			doEditWhenNoGradient = _doEditWhenNoGradient;

			
			// Create button
			doCreate = GUI.Button(addRect, _iconAdd);

			// Save button
			{
				var color = GUI.color;
				if (isDirty) GUI.color = Color.yellow;
				doSave = GUI.Button(saveRect, _iconSave);
				GUI.color = color;
			}
			
			// Discard button
			doDiscard = GUI.Button(discardRect, _iconDiscard);
		}

		public static bool HasGradient(AssetImporter assetImporter) { return assetImporter.userData.Contains("#");}
		
		public static LwguiGradient GetGradientFromTexture(Texture texture, out bool isDirty, bool doDiscard = false, bool doRegisterUndo = false)
		{
			isDirty = false;
			if (texture == null) return null;

			var assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
			if (doRegisterUndo)
			{
				LwguiGradientWindow.RegisterRampMapUndo(texture, assetImporter);
			}
			if (assetImporter != null && HasGradient(assetImporter))
			{
				isDirty = DecodeGradientFromJSON(assetImporter.userData, out var savedGradient, out var editingGradient);
				var outGradient = doDiscard ? savedGradient : editingGradient;
				return outGradient;
			}
			else
			{
				Debug.LogError("LWGUI: Can not find texture: "
							 + texture.name
							 + " or it's userData on disk! \n"
							 + "If you are moving or copying the Ramp Map, make sure your .meta file is not lost!");
				return null;
			}
		}

		public static void SetGradientToTexture(Texture texture, LwguiGradient gradient, bool doSaveToDisk = false)
		{
			if (texture == null || gradient == null) return;

			var texture2D = (Texture2D)texture;
			var path = AssetDatabase.GetAssetPath(texture);
			var assetImporter = AssetImporter.GetAtPath(path);
			VersionControlHelper.Checkout(texture2D);
			
			LwguiGradientWindow.RegisterRampMapUndo(texture2D, assetImporter);

			// Save to texture
			var pixels = gradient.GetPixels(texture.width, texture.height);
			texture2D.SetPixels(pixels);
			texture2D.Apply();

			// Save gradient JSON to userData
			DecodeGradientFromJSON(assetImporter.userData, out var savedGradient, out _);
			assetImporter.userData = EncodeGradientToJSON(doSaveToDisk ? gradient : savedGradient, gradient);

			// Save texture to disk
			if (doSaveToDisk)
			{
				var systemPath = Helper.ProjectPath + path;
				VersionControlHelper.Checkout(path);
				File.WriteAllBytes(systemPath, texture2D.EncodeToPNG());
				assetImporter.SaveAndReimport();
			}
		}

		private static string EncodeGradientToJSON(LwguiGradient savedGradient, LwguiGradient editingGradient)
		{
			string savedJSON = " ", editingJSON = " ";
			if (savedGradient != null)
				savedJSON = EditorJsonUtility.ToJson(savedGradient);
			if (editingGradient != null)
				editingJSON = EditorJsonUtility.ToJson(editingGradient);

			return savedJSON + "#" + editingJSON;
		}

		private static bool DecodeGradientFromJSON(string json, out LwguiGradient savedGradient, out LwguiGradient editingGradient)
		{
			savedGradient = new LwguiGradient(); 
			editingGradient = new LwguiGradient();

			var isLegacyJSON = json.Contains("MonoBehaviour");
			var subJSONs = json.Split('#');
			
			// Upgrading from deprecated GradientObject to LwguiGradient
			if (isLegacyJSON)
			{
				var savedGradientLegacy = ScriptableObject.CreateInstance<GradientObject>();
				var editingGradientLegacy = ScriptableObject.CreateInstance<GradientObject>();
				
				EditorJsonUtility.FromJsonOverwrite(subJSONs[0], savedGradientLegacy);
				EditorJsonUtility.FromJsonOverwrite(subJSONs[1], editingGradientLegacy);

				savedGradient = LwguiGradient.FromGradient(savedGradientLegacy.gradient);
				editingGradient = LwguiGradient.FromGradient(editingGradientLegacy.gradient);
			}
			else
			{
				EditorJsonUtility.FromJsonOverwrite(subJSONs[0], savedGradient);
				EditorJsonUtility.FromJsonOverwrite(subJSONs[1], editingGradient);
			}
			
			return subJSONs[0] != subJSONs[1];
		}

		public static bool CreateAndSaveNewGradientTexture(int width, int height, string unityPath, bool isLinear)
		{
			var gradient = new LwguiGradient();

			var ramp = gradient.GetPreviewRampTexture(width, height, ColorSpace.Linear);
			var png = ramp.EncodeToPNG();

			var systemPath = Helper.ProjectPath + unityPath;
			File.WriteAllBytes(systemPath, png);

			AssetDatabase.ImportAsset(unityPath);
			SetRampTextureImporter(unityPath, true, isLinear, EncodeGradientToJSON(gradient, gradient));

			return true;
		}

		public static void SetRampTextureImporter(string unityPath, bool isReadable = true, bool isLinear = false, string userData = null)
		{
			var textureImporter = AssetImporter.GetAtPath(unityPath) as TextureImporter;
			if (!textureImporter)
			{
				Debug.LogError($"LWGUI: Can NOT get TextureImporter at path: { unityPath }");
				return;
			}
			
			textureImporter.wrapMode = TextureWrapMode.Clamp;
			textureImporter.isReadable = isReadable;
			textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
			textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
			textureImporter.mipmapEnabled = false;
			textureImporter.sRGBTexture = !isLinear;

			foreach (var platformName in Helper.platformNamesForTextureSettings)
			{
				var platformTextureSettings = textureImporter.GetPlatformTextureSettings(platformName);
				platformTextureSettings.format = TextureImporterFormat.RGBA32;
				textureImporter.SetPlatformTextureSettings(platformTextureSettings);
			}

			if (userData != null)
				textureImporter.userData = userData;
			
			textureImporter.SaveAndReimport();
		}

		#endregion


		#region RampSelector

		public static void RampMapSelectorOverride(Rect rect, MaterialProperty prop, string rootPath, RampSelectorWindow.SwitchRampMapCallback switchRampMapEvent)
		{
			var e = Event.current;
			if (e.type == UnityEngine.EventType.MouseDown && rect.Contains(e.mousePosition))
			{
				e.Use();
				var textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { rootPath });
				var rampMaps = textureGUIDs.Select((GUID) =>
				{
					var path = AssetDatabase.GUIDToAssetPath(GUID);
					var assetImporter = AssetImporter.GetAtPath(path);
					if (HasGradient(assetImporter))
					{
						return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
					}
					else
						return null;
				}).ToArray();
				RampSelectorWindow.ShowWindow(prop, rampMaps, switchRampMapEvent);
			}
		}

		public static void RampIndexSelectorOverride(Rect rect, MaterialProperty prop, LwguiRampAtlas rampAtlas, RampSelectorWindow.SwitchRampMapCallback switchRampMapEvent)
		{
			if (!rampAtlas)
				return;
			
			var e = Event.current;
			if (e.type == UnityEngine.EventType.MouseDown && rect.Contains(e.mousePosition))
			{
				e.Use();
				RampSelectorWindow.ShowWindow(prop, rampAtlas.GetTexture2Ds(LwguiGradient.ChannelMask.RGB), switchRampMapEvent);
			}
		}
		#endregion
	}

	public class RampSelectorWindow : EditorWindow
	{
		public delegate void SwitchRampMapCallback(MaterialProperty prop, Texture2D newRampMap, int index);
		
		private Texture2D[] _rampMaps;
		private Vector2 _scrollPosition;
		private MaterialProperty _prop;
		private SwitchRampMapCallback _switchRampMapEvent;

		public static void ShowWindow(MaterialProperty prop, Texture2D[] rampMaps, SwitchRampMapCallback switchRampMapEvent)
		{
			RampSelectorWindow window = ScriptableObject.CreateInstance<RampSelectorWindow>();
			window.titleContent = new GUIContent("Ramp Selector");
			window.minSize = new Vector2(400, 500);
			window._rampMaps = rampMaps;
			window._prop = prop;
			window._switchRampMapEvent = switchRampMapEvent;
			window.ShowAuxWindow();
		}
		
		private void OnGUI()
		{
			EditorGUILayout.BeginVertical();
			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

			for (int i = 0; i < _rampMaps.Length; i++)
			{
				var rampMap = _rampMaps[i];
				EditorGUILayout.BeginHorizontal();
				if (rampMap != null)
				{
					var guiContent = new GUIContent($"{ i }. { rampMap.name }");
					var rect = EditorGUILayout.GetControlRect();
					var buttonWidth = Mathf.Min(300f, Mathf.Max(GUI.skin.button.CalcSize(guiContent).x, rect.width * 0.35f));
					var buttonRect = new Rect(rect.x + rect.width - buttonWidth, rect.y, buttonWidth, rect.height);
					var previewRect = new Rect(rect.x, rect.y, rect.width - buttonWidth - 3.0f, rect.height);
					
					if (GUI.Button(buttonRect, guiContent, Helper.guiStyle_RampSelectButton) && _switchRampMapEvent != null)
					{
						_switchRampMapEvent(_prop, rampMap, i);
						LwguiGradientWindow.CloseWindow();
						Close();
					}
					EditorGUI.DrawPreviewTexture(previewRect, rampMap);
				}
				EditorGUILayout.EndHorizontal();
			}
			
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
		}
	}
}