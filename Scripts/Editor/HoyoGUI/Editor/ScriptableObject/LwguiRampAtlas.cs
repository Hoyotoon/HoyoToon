// Copyright (c) Jason Ma
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LWGUI.LwguiGradientEditor;
using LWGUI.Runtime.LwguiGradient;
using UnityEngine;
using UnityEditor;
using UnityEngine.Serialization;


namespace LWGUI
{
	[CreateAssetMenu(fileName = "LWGUI_RampAtlas.asset", menuName = "LWGUI/Ramp Atlas", order = 84)]
	public class LwguiRampAtlas : ScriptableObject
	{
		[Serializable]
		public class Ramp
		{
			public string name = "New Ramp";
			public LwguiGradient gradient = LwguiGradient.white;
			public ColorSpace colorSpace = ColorSpace.Gamma;
			public LwguiGradient.ChannelMask channelMask = LwguiGradient.ChannelMask.All;
			public LwguiGradient.GradientTimeRange timeRange = LwguiGradient.GradientTimeRange.One;
		}
		
		public const string RampAtlasSOExtensionName = "asset";
		public const string RampAtlasTextureExtensionName = "tga";
		
		public int rampAtlasWidth = 256;
		public int rampAtlasHeight = 4;
		public bool rampAtlasSRGB = true;
		
		[NonSerialized] public Texture2D rampAtlasTexture = null;
		
		[SerializeField] private List<Ramp> _ramps = new List<Ramp>();
		public List<Ramp> ramps
		{
			get => _ramps ?? new List<Ramp>();

			set => _ramps = value ?? new List<Ramp>();
		}

		[SerializeField] private bool _saveTextureToggle;
		private string _rampAtlasSOPath = string.Empty;
		private string _rampAtlasTexturePath = string.Empty;

		public void InitData()
		{
			if (AssetDatabase.Contains(this))
			{
				_rampAtlasSOPath = AssetDatabase.GetAssetPath(this);
				_rampAtlasTexturePath = Path.ChangeExtension(_rampAtlasSOPath, RampAtlasTextureExtensionName);
			}
		}

		public bool LoadTexture()
		{
			if (!AssetDatabase.Contains(this))
				return false;
			
			// Try to load
			rampAtlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(_rampAtlasTexturePath);

			// Create
			if (!rampAtlasTexture)
			{
				CreateRampAtlasTexture();
				rampAtlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(_rampAtlasTexturePath);
			}

			if (!rampAtlasTexture)
			{
				Debug.LogError($"LWGUI: Can NOT create a Ramp Atlas Texture at path: { _rampAtlasTexturePath }");
				return false;
			}

			return true;
		}

		public Color[] GetPixels()
		{
			Color[] pixels = Enumerable.Repeat(Color.white, rampAtlasWidth * rampAtlasHeight).ToArray();
			int currentIndex = 0;
			foreach (var ramp in ramps)
			{
				ramp.gradient.GetPixels(ref pixels, ref currentIndex, rampAtlasWidth, 1, ramp.channelMask);
			}

			return pixels;
		}

		public Texture2D[] GetTexture2Ds(LwguiGradient.ChannelMask channelMask = LwguiGradient.ChannelMask.All)
		{
			Texture2D[] textures = new Texture2D[ramps.Count];
			for (int i = 0; i < ramps.Count; i++)
			{
				var ramp = ramps[i];
				textures[i] = Instantiate(ramp.gradient?.GetPreviewRampTexture(rampAtlasWidth, 1, ramp.colorSpace, ramp.channelMask & channelMask));
				textures[i].name = ramp.name;
			}

			return textures;
		}

		public Ramp GetRamp(int index)
		{
			if (index < ramps.Count && index >= 0)
			{
				return ramps[index] ?? new Ramp();
			}
			return null;
		}

		public void CreateRampAtlasTexture()
		{
			var rampAtlasTexture = new Texture2D(rampAtlasWidth, rampAtlasHeight, TextureFormat.RGBA32, false, !rampAtlasSRGB);
			rampAtlasTexture.SetPixels(GetPixels());
			rampAtlasTexture.wrapMode = TextureWrapMode.Clamp;
			rampAtlasTexture.name = Path.GetFileName(_rampAtlasTexturePath);
			rampAtlasTexture.Apply();

			SaveTexture(rampAtlasTexture);

			AssetDatabase.ImportAsset(_rampAtlasTexturePath);
			RampHelper.SetRampTextureImporter(_rampAtlasTexturePath, true, !rampAtlasSRGB, EditorJsonUtility.ToJson(this));
		}

		public void SaveTexture(Texture2D rampAtlasTexture = null, string targetRelativePath = null, bool checkoutAndForceWrite = false)
		{
			targetRelativePath ??= _rampAtlasTexturePath;
			rampAtlasTexture ??= this.rampAtlasTexture;
			if (!rampAtlasTexture || string.IsNullOrEmpty(targetRelativePath))
				return;
			
			var absPath = Helper.ProjectPath + targetRelativePath;
			if (File.Exists(absPath))
			{
				var existRampTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(targetRelativePath);
				if (!VersionControlHelper.IsWriteable(existRampTexture))
				{
					if (checkoutAndForceWrite)
					{
						if (!VersionControlHelper.Checkout(targetRelativePath))
						{
							Debug.LogError($"LWGUI: Can NOT write the Ramp Atlas Texture to path: { absPath }");
							return;
						}
					}
					else
					{
						return;
					}
				}
			}

			try
			{
				File.WriteAllBytes(absPath, rampAtlasTexture.EncodeToTGA());
				SaveTextureUserData(targetRelativePath);
				
				Debug.Log($"LWGUI: Saved the Ramp Atlas Texture at path: { absPath }");
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}
		}

		public void SaveTextureUserData(string targetRelativePath = null)
		{
			targetRelativePath ??= _rampAtlasTexturePath;
			if (!string.IsNullOrEmpty(targetRelativePath))
			{
				var importer = AssetImporter.GetAtPath(targetRelativePath);
				if (importer)
				{
					importer.userData = EditorJsonUtility.ToJson(this);
					importer.SaveAndReimport();
				}
			}
		}

		public void SaveRampAtlasSO()
		{
			AssetDatabase.SaveAssetIfDirty(this);
		}

		public void UpdateTexturePixels()
		{
			if (!rampAtlasTexture)
				return;
			
			LwguiGradientWindow.RegisterSerializedObjectUndo(this);
			rampAtlasTexture.Reinitialize(rampAtlasWidth, rampAtlasHeight);
			rampAtlasTexture.SetPixels(GetPixels());
			rampAtlasTexture.Apply();
		}
		
		public void DiscardChanges()
		{
			var importer = AssetImporter.GetAtPath(_rampAtlasTexturePath);
			if (!importer)
				return;
			
			EditorJsonUtility.FromJsonOverwrite(importer.userData, this);
			InitData();
			AssetDatabase.ImportAsset(_rampAtlasTexturePath, ImportAssetOptions.ForceUpdate);
			LoadTexture();
			EditorUtility.ClearDirty(this);
		}

		public void ConvertColorSpace(ColorSpace targetColorSpace)
		{
			foreach (var ramp in ramps)
			{
				if (ramp.colorSpace != targetColorSpace)
				{
					ramp.colorSpace = targetColorSpace;
					ramp.gradient.ConvertColorSpaceWithoutCopy(
						targetColorSpace != ColorSpace.Gamma
						? ColorSpace.Linear
						: ColorSpace.Gamma);
				}
			}
			
			rampAtlasSRGB = targetColorSpace == ColorSpace.Gamma;
			RampHelper.SetRampTextureImporter(_rampAtlasTexturePath, true, !rampAtlasSRGB, EditorJsonUtility.ToJson(this));
			UpdateTexturePixels();
			SaveTexture();
		}
		
		[ContextMenu("Convert Gamma To Linear")]
		public void ConvertGammaToLinear()
		{
			ConvertColorSpace(ColorSpace.Linear);
		}

		[ContextMenu("Convert Linear To Gamma")]
		public void ConvertLinearToGamma()
		{
			ConvertColorSpace(ColorSpace.Gamma);
		}

		private void OnEnable()
		{
			InitData();
			LoadTexture();
		}

		private void OnValidate()
		{
			// Skip at the end of compilation
			if (Event.current == null
			// Skip when editing Text Field
			    || EditorGUIUtility.editingTextField)
				return;
			
			InitData();
			
			if (!LoadTexture())
				return;

			UpdateTexturePixels();
			SaveTexture();
		}
		
		public static Texture LoadRampAtlasTexture(LwguiRampAtlas rampAtlasSO)
		{
			if (!rampAtlasSO || !AssetDatabase.Contains(rampAtlasSO))
			{
				return null;
			}
			
			var soPath = Path.ChangeExtension(AssetDatabase.GetAssetPath(rampAtlasSO), RampAtlasTextureExtensionName);
			return AssetDatabase.LoadAssetAtPath<Texture>(soPath);
		}
		
		public static LwguiRampAtlas LoadRampAtlasSO(Texture texture)
		{
			if (!texture || !AssetDatabase.Contains(texture))
			{
				return null;
			}

			var soPath = Path.ChangeExtension(AssetDatabase.GetAssetPath(texture), RampAtlasSOExtensionName);
			return AssetDatabase.LoadAssetAtPath<LwguiRampAtlas>(soPath);
		}
		
		public static LwguiRampAtlas CreateRampAtlasSO(MaterialProperty rampAtlasProp, LWGUIMetaDatas metaDatas)
		{
			if (rampAtlasProp == null || metaDatas == null)
				return null;

			var shader = metaDatas.GetShader();
			
			// Get default ramps
			RampAtlasDrawer targetRampAtlasDrawer = null;
			List<(int defaultIndex, RampAtlasIndexerDrawer indexerDrawer)> defaultRampAtlasIndexerDrawers = new ();
			// Unity Bug: The cache of MaterialPropertyHandler must be cleared first, otherwise the default value cannot be obtained correctly.
			ReflectionHelper.InvalidatePropertyCache(shader);
			for (int i = 0; i < metaDatas.perMaterialData.defaultPropertiesWithPresetOverride.Length; i++)
			{
				var prop = metaDatas.perMaterialData.defaultPropertiesWithPresetOverride[i];
				var drawer = ReflectionHelper.GetPropertyDrawer(shader, prop);
				if (drawer == null)
					continue;

				if (drawer is RampAtlasDrawer rampAtlasDrawer && prop.name == rampAtlasProp.name)
					targetRampAtlasDrawer = rampAtlasDrawer;
				
				if (drawer is RampAtlasIndexerDrawer rampAtlasIndexerDrawer && rampAtlasIndexerDrawer.rampAtlasPropName == rampAtlasProp.name)
					defaultRampAtlasIndexerDrawers.Add(((int)prop.GetNumericValue(), rampAtlasIndexerDrawer));
			}
			
			if (targetRampAtlasDrawer == null)
			{
				Debug.LogError($"LWGUI: Can NOT find RampAtlasDrawer { rampAtlasProp.name } in Shader { shader }");
				return null;
			}
			
			// Init Ramp Atlas
			var newRampAtlasSO = ScriptableObject.CreateInstance<LwguiRampAtlas>();
			newRampAtlasSO.name = targetRampAtlasDrawer.defaultFileName;
			newRampAtlasSO.rampAtlasWidth = targetRampAtlasDrawer.defaultAtlasWidth;
			newRampAtlasSO.rampAtlasHeight = targetRampAtlasDrawer.defaultAtlasHeight;
			newRampAtlasSO.rampAtlasSRGB = targetRampAtlasDrawer.defaultAtlasSRGB;

			if (defaultRampAtlasIndexerDrawers.Count > 0)
			{
				defaultRampAtlasIndexerDrawers.Sort(((x, y) => x.defaultIndex.CompareTo(y.defaultIndex)));

				// Set Ramps Count
				var maxIndex = defaultRampAtlasIndexerDrawers.Max((tuple => tuple.defaultIndex));
				for (int i = 0; i < maxIndex + 1; i++)
				{
					newRampAtlasSO.ramps.Add(new LwguiRampAtlas.Ramp());
					if (newRampAtlasSO.ramps.Count >= newRampAtlasSO.rampAtlasHeight)
						newRampAtlasSO.rampAtlasHeight *= 2;
				}
				
				// Set Ramps Default Value
				for (int i = 0; i < defaultRampAtlasIndexerDrawers.Count; i++)
				{
					var defaultRampAtlasIndexerDrawer = defaultRampAtlasIndexerDrawers[i];
					var ramp = newRampAtlasSO.ramps[defaultRampAtlasIndexerDrawer.defaultIndex];
					var drawer = defaultRampAtlasIndexerDrawer.indexerDrawer;
					ramp.name = drawer.defaultRampName;
					ramp.colorSpace = drawer.colorSpace;
					ramp.channelMask = drawer.viewChannelMask;
					ramp.timeRange = drawer.timeRange;
				}
			}

			return SaveRampAtlasSOToAsset(newRampAtlasSO, targetRampAtlasDrawer.rootPath, targetRampAtlasDrawer.defaultFileName);
		}

		public static LwguiRampAtlas CloneRampAtlasSO(LwguiRampAtlas rampAtlasSO)
		{
			if (!rampAtlasSO)
				return null;

			var newRampAtlasSO = Instantiate(rampAtlasSO);
			var rootPath = Path.GetDirectoryName(rampAtlasSO._rampAtlasSOPath);
			var defaultFileName = Path.GetFileName(rampAtlasSO._rampAtlasSOPath);

			if (SaveRampAtlasSOToAsset(newRampAtlasSO, rootPath, defaultFileName))
			{
				newRampAtlasSO.InitData();
				newRampAtlasSO.LoadTexture();
				return newRampAtlasSO;
			}

			return null;
		}
		
		public static LwguiRampAtlas SaveRampAtlasSOToAsset(LwguiRampAtlas rampAtlasSO, string rootPath, string defaultFileName)
		{
			if (!rampAtlasSO)
				return null;

			// Save Ramp Atlas
			string createdFileRelativePath = string.Empty;
			while (true)
			{
				// TODO: Warning:
				// PropertiesGUI() is being called recursively. If you want to render the default gui for shader properties then call PropertiesDefaultGUI() instead
				var absPath = EditorUtility.SaveFilePanel("Create a Ramp Atlas SO", rootPath, defaultFileName, "asset");
					
				if (absPath.StartsWith(Helper.ProjectPath))
				{
					createdFileRelativePath = absPath.Replace(Helper.ProjectPath, string.Empty);
					break;
				}
				else if (absPath != string.Empty)
				{
					var retry = EditorUtility.DisplayDialog("Invalid Path", "Please select the subdirectory of '" + Helper.ProjectPath + "'", "Retry", "Cancel");
					if (!retry) break;
				}
				else
				{
					break;
				}
			}
				
			if (!string.IsNullOrEmpty(createdFileRelativePath))
			{
				AssetDatabase.CreateAsset(rampAtlasSO, createdFileRelativePath);
				rampAtlasSO.InitData();
				rampAtlasSO.LoadTexture();
				return rampAtlasSO;
			}
			
			return null;
		}
	}
}