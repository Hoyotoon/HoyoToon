// Copyright (c) Jason Ma
// Per Shader > Per Material > Per Inspector

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LWGUI
{

	public enum SearchMode
	{
		Auto     = 0, // Search by group first, and search by property when there are no results
		Property = 1, // Search by property
		Group    = 2, // Search by group
		Num      = 3
	}

	public class DisplayModeData
	{
		public bool showAllAdvancedProperties;
		public bool showAllHiddenProperties;
		public bool showOnlyModifiedProperties;
		public bool showOnlyModifiedGroups;

		public int advancedCount;
		public int hiddenCount;

		public bool IsDefaultDisplayMode() { return !(showAllAdvancedProperties || showAllHiddenProperties || showOnlyModifiedProperties || showOnlyModifiedGroups); }
	}

	/// <summary>
	/// The static metadata of Material Property is only related to Shader.
	/// </summary>
	public partial class PropertyStaticData
	{
		public string name        = string.Empty;
		public string displayName = string.Empty; // Decoded displayName (Helpbox and Tooltip are encoded in displayName)

		// Structure
		public string                   groupName                = string.Empty; // [Group(groupName)] / [Sub(groupName)] / [Advanced(groupName)] / [SubGroup(mainGroupName, subGroupName)]
		public bool                     isMain                   = false;        // [Group]
		public bool                     isSubGroup               = false;        // [SubGroup]
		public string                   subGroupName             = string.Empty; // Name of the SubGroup used for foldout display
		public bool                     isAdvanced               = false;        // [Advanced]
		public bool                     isAdvancedHeader         = false;        // the first [Advanced] in the same group
		public bool                     isAdvancedHeaderProperty = false;
		public string                   advancedHeaderString     = string.Empty; 
		public PropertyStaticData       parent                   = null;
		public List<PropertyStaticData> children                 = new List<PropertyStaticData>();

		// Visibility
		public bool                             isSearchMatched           = true;                                   // Search filter result
		public bool                             isExpanding               = false;                                  // Children are displayed only when expanded
		public bool                             isReadOnly                = false;                                  // [ReadOnly]
		public bool                             isHidden                  = false;                                  // [Hidden]
		public List<ShowIfDecorator.ShowIfData> showIfDatas               = new List<ShowIfDecorator.ShowIfData>(); // [ShowIf()]
		public string                           conditionalDisplayKeyword = string.Empty;                           // [Group(groupName_conditionalDisplayKeyword)]

		// Drawers
		public IPresetDrawer presetDrawer = null;
		public List<IBaseDrawer> baseDrawers  = null;

		// Metadata
		public List<string>         extraPropNames      = new List<string>(); // Other Props that have been associated
		public string               helpboxMessages     = string.Empty;
		public string               tooltipMessages     = string.Empty;
		public LwguiShaderPropertyPreset propertyPresetAsset = null; // The Referenced Preset Asset

		public void AddExtraProperty(string propName)
		{
			if (!extraPropNames.Contains(propName)) extraPropNames.Add(propName);
		}
	}

	/// <summary>
	/// All Shader static metadata can be determined after Shader is compiled and will not change.
	/// </summary>
	public class PerShaderData
	{
		public Dictionary<string, PropertyStaticData> propStaticDatas = new Dictionary<string, PropertyStaticData>();
		public Shader                                 shader          = null;
		public DisplayModeData                        displayModeData = new DisplayModeData();
		public SearchMode                             searchMode      = SearchMode.Auto;
		public string                                 searchString    = string.Empty;
		// public List<string>                           favoriteproperties    = new List<string>();

		// UnityEngine.Object may be destroyed when loading new scene, so must manually check null reference
		private Material _defaultMaterial = null;

		public Material defaultMaterial
		{
			get
			{
				if (!_defaultMaterial && shader) _defaultMaterial = new Material(shader);
				return _defaultMaterial;
			}
		}

		public PerShaderData(Shader shader, MaterialProperty[] props)
		{
			this.shader = shader;

			// Get Property Static Data
			foreach (var prop in props)
			{
				var propStaticData = new PropertyStaticData() { name = prop.name };
				propStaticDatas[prop.name] = propStaticData;

				// Get Drawers and Build Drawer StaticMetaData
				bool hasDecodedStaticMetaData = false;
				{
					var drawer = ReflectionHelper.GetPropertyDrawer(shader, prop, out var decoratorDrawers);

					if (drawer is IPresetDrawer)
						propStaticData.presetDrawer = drawer as IPresetDrawer;

					var baseDrawer = drawer as IBaseDrawer;
					if (baseDrawer != null)
					{
						propStaticData.baseDrawers = new List<IBaseDrawer>() { baseDrawer };
						baseDrawer.BuildStaticMetaData(shader, prop, props, propStaticData);
						hasDecodedStaticMetaData = true;
					}

					decoratorDrawers?.ForEach(decoratorDrawer =>
					{
						baseDrawer = decoratorDrawer as IBaseDrawer;
						if (baseDrawer != null)
						{
							if (propStaticData.baseDrawers == null)
								propStaticData.baseDrawers = new List<IBaseDrawer>() { baseDrawer };
							else
								propStaticData.baseDrawers.Add(baseDrawer);

							baseDrawer.BuildStaticMetaData(shader, prop, props, propStaticData);
						}
					});
				}

				if (!hasDecodedStaticMetaData)
					DecodeMetaDataFromDisplayName(prop, propStaticData);
			}

			// Check Data
			foreach (var prop in props)
			{
				var propStaticData = propStaticDatas[prop.name];
				propStaticData.extraPropNames.RemoveAll((extraPropName =>
															string.IsNullOrEmpty(extraPropName) || !propStaticDatas.ContainsKey(extraPropName)));
			}

		// Build Property Structure
		{
			var groupToMainPropertyDic = new Dictionary<string, MaterialProperty>();
			var pathToPropertyDic = new Dictionary<string, MaterialProperty>(); // Maps any path (Main, SubGroup, or nested) to its property
			var subGroupNameToPathDic = new Dictionary<string, string>(); // Maps simple SubGroup names to their full paths

			// Collection Main Groups and any groups/subgroups
			foreach (var prop in props)
			{
				var propData = propStaticDatas[prop.name];
				if (propData.isMain 
				 && !string.IsNullOrEmpty(propData.groupName)
				 && !groupToMainPropertyDic.ContainsKey(propData.groupName))
				{
					groupToMainPropertyDic.Add(propData.groupName, prop);
				}

				// Register any SubGroup with its full path (parentPath_subGroupName)
				if (propData.isSubGroup
				 && !string.IsNullOrEmpty(propData.groupName)
				 && !string.IsNullOrEmpty(propData.subGroupName))
				{
					var fullPath = propData.groupName + "_" + propData.subGroupName;
					if (!pathToPropertyDic.ContainsKey(fullPath))
					{
						pathToPropertyDic.Add(fullPath, prop);
					}
					// Also map just the SubGroup name to its full path for easy lookup
					if (!subGroupNameToPathDic.ContainsKey(propData.subGroupName))
					{
						subGroupNameToPathDic.Add(propData.subGroupName, fullPath);
					}
				}
			}

			// Register SubProps to their parents (non-SubGroup properties)
			foreach (var prop in props)
			{
				var propData = propStaticDatas[prop.name];
				if (!propData.isMain && !propData.isSubGroup
				 && !string.IsNullOrEmpty(propData.groupName))
				{
					// Try to find the longest matching parent path
					bool foundParent = false;
					
					// First check if it's a simple SubGroup name and resolve it
					if (!propData.groupName.Contains("_") && subGroupNameToPathDic.ContainsKey(propData.groupName))
					{
						propData.groupName = subGroupNameToPathDic[propData.groupName];
					}
					
					// First check all registered subgroups/nested paths
					var sortedPaths = pathToPropertyDic.Keys.OrderByDescending(k => k.Length).ToList();
					foreach (var pathKey in sortedPaths)
					{
						if (propData.groupName.StartsWith(pathKey))
						{
							var parentProp = pathToPropertyDic[pathKey];
							propData.parent = propStaticDatas[parentProp.name];
							propStaticDatas[parentProp.name].children.Add(propData);
							foundParent = true;

							// Extract conditional display keyword if present
							if (propData.groupName.Length > pathKey.Length)
							{
								var keywordPart = propData.groupName.Substring(pathKey.Length, propData.groupName.Length - pathKey.Length);
								// Remove leading underscore if present
								if (keywordPart.StartsWith("_"))
									keywordPart = keywordPart.Substring(1);
								propData.conditionalDisplayKeyword = keywordPart.ToUpper();
								propData.groupName = pathKey;
							}
							break;
						}
					}

					// If not found in subgroups, try to match with MainProps
					if (!foundParent)
					{
						var sortedMainGroups = groupToMainPropertyDic.Keys.OrderByDescending(k => k.Length).ToList();
						foreach (var groupName in sortedMainGroups)
						{
							if (propData.groupName.StartsWith(groupName))
							{
								// Update Structure
								var mainProp = groupToMainPropertyDic[groupName];
								propData.parent = propStaticDatas[mainProp.name];
								propStaticDatas[mainProp.name].children.Add(propData);

								// Split groupName and conditional display keyword
								if (propData.groupName.Length > groupName.Length)
								{
									var keywordPart = propData.groupName.Substring(groupName.Length, propData.groupName.Length - groupName.Length);
									// Remove leading underscore if present
									if (keywordPart.StartsWith("_"))
										keywordPart = keywordPart.Substring(1);
									propData.conditionalDisplayKeyword = keywordPart.ToUpper();
									propData.groupName = groupName;
								}
								break;
							}
						}
					}
				}
			}

			// Register SubGroups to their parents (can be Main or another SubGroup)
			// Use multiple passes to handle deeply nested hierarchies
			// Also resolve simple SubGroup names to full paths
			bool madeProgress = true;
			int maxIterations = 10; // Prevent infinite loops
			int iterations = 0;
			
			while (madeProgress && iterations < maxIterations)
			{
				madeProgress = false;
				iterations++;
				
				foreach (var prop in props)
				{
					var propData = propStaticDatas[prop.name];
					
					// Only process SubGroups that don't have a parent yet
					if (propData.isSubGroup && !string.IsNullOrEmpty(propData.groupName) && propData.parent == null)
					{
						// First, check if groupName is a simple SubGroup name (no underscores) and resolve it
						if (!propData.groupName.Contains("_") && subGroupNameToPathDic.ContainsKey(propData.groupName))
						{
							// Resolve simple SubGroup name to full path
							propData.groupName = subGroupNameToPathDic[propData.groupName];
						}

						// Try to find parent in existing subgroups first (for nested subgroups)
						bool foundSubGroupParent = false;
						var sortedPaths = pathToPropertyDic.Keys.OrderByDescending(k => k.Length).ToList();
						foreach (var pathKey in sortedPaths)
						{
							if (propData.groupName == pathKey)
							{
								var parentProp = pathToPropertyDic[pathKey];
								propData.parent = propStaticDatas[parentProp.name];
								propStaticDatas[parentProp.name].children.Add(propData);
								foundSubGroupParent = true;
								madeProgress = true;
								break;
							}
						}

						// If not found in subgroups, try main groups
						if (!foundSubGroupParent)
						{
							var sortedMainGroups = groupToMainPropertyDic.Keys.OrderByDescending(k => k.Length).ToList();
							foreach (var groupName in sortedMainGroups)
							{
								if (propData.groupName == groupName)
								{
									var mainProp = groupToMainPropertyDic[groupName];
									propData.parent = propStaticDatas[mainProp.name];
									propStaticDatas[mainProp.name].children.Add(propData);
									foundSubGroupParent = true;
									madeProgress = true;
									break;
								}
							}
						}
					}
				}
			}
		}			// Build Advanced Structure
			{
				PropertyStaticData lastPropData = null;
				PropertyStaticData lastHeaderPropData = null;
				PropertyStaticData lastAdvancedParent = null; // Track the parent context for Advanced headers
				
				for (int i = 0; i < props.Length; i++)
				{
					var prop = props[i];
					var propStaticData = propStaticDatas[prop.name];

					// Build Advanced Structure
					if (propStaticData.isAdvanced)
					{
						// If it is the first prop in a Advanced Block, set to Header
						if (lastPropData == null
						 || !lastPropData.isAdvanced
						 || propStaticData.isAdvancedHeaderProperty
						 || (!string.IsNullOrEmpty(propStaticData.advancedHeaderString)
						  && propStaticData.advancedHeaderString != lastPropData.advancedHeaderString))
						{
							propStaticData.isAdvancedHeader = true;
							lastHeaderPropData = propStaticData;
							lastAdvancedParent = propStaticData.parent; // Remember parent context
							
							// Advanced Header should be a child of its actual parent (SubGroup or MainGroup)
							// Parent is already set from earlier processing, no need to change it
						}
						// Else set to child of Advanced Header
						else
						{
							propStaticData.parent = lastHeaderPropData;
							lastHeaderPropData.children.Add(propStaticData);
						}
					}
					else if (lastAdvancedParent != null)
					{
						// Exit Advanced context
						lastAdvancedParent = null;
					}

					lastPropData = propStaticData;
				}
			}

			// Build Display Mode Data
			{
				PropertyStaticData lastPropData = null;
				PropertyStaticData lastHeaderPropData = null;
				for (int i = 0; i < props.Length; i++)
				{
					var prop = props[i];
					var propStaticData = propStaticDatas[prop.name];

					// Counting
					if (propStaticData.isHidden
					 || (propStaticData.parent != null
					  && (propStaticData.parent.isHidden
					   || (propStaticData.parent.parent != null && propStaticData.parent.parent.isHidden))))
						displayModeData.hiddenCount++;
					if (propStaticData.isAdvanced
					 || (propStaticData.parent != null
					  && (propStaticData.parent.isAdvanced
					   || (propStaticData.parent.parent != null && propStaticData.parent.parent.isAdvanced))))
						displayModeData.advancedCount++;

					lastPropData = propStaticData;
				}
			}
		}

		public PropertyStaticData GetPropStaticData(string propName)
		{
			propStaticDatas.TryGetValue(propName, out var propStaticData);
			return propStaticData;
		}

		private static readonly string _tooltipSplitter = "#";

		private static readonly string _helpboxSplitter = "%";

		public static void DecodeMetaDataFromDisplayName(MaterialProperty prop, PropertyStaticData propStaticData)
		{
			var tooltips = prop.displayName.Split(new String[] { _tooltipSplitter }, StringSplitOptions.None);
			if (tooltips.Length > 1)
			{
				for (int i = 1; i <= tooltips.Length - 1; i++)
				{
					var str = tooltips[i];
					var helpboxIndex = tooltips[i].IndexOf(_helpboxSplitter, StringComparison.Ordinal);
					if (helpboxIndex > 0)
						str = tooltips[i].Substring(0, helpboxIndex);
					propStaticData.tooltipMessages += str + "\n";
				}
			}

			var helpboxes = prop.displayName.Split(new String[] { _helpboxSplitter }, StringSplitOptions.None);
			if (helpboxes.Length > 1)
			{
				for (int i = 1; i <= helpboxes.Length - 1; i++)
				{
					var str = helpboxes[i];
					var tooltipIndex = helpboxes[i].IndexOf(_tooltipSplitter, StringComparison.Ordinal);
					if (tooltipIndex > 0)
						str = tooltips[i].Substring(0, tooltipIndex);
					propStaticData.helpboxMessages += str + "\n";
				}
			}

			if (propStaticData.helpboxMessages.EndsWith("\n"))
				propStaticData.helpboxMessages = propStaticData.helpboxMessages.Substring(0, propStaticData.helpboxMessages.Length - 1);

			propStaticData.displayName = prop.displayName.Split(new String[] { _tooltipSplitter, _helpboxSplitter }, StringSplitOptions.None)[0];
		}

		public void UpdateSearchFilter()
		{
			var isSearchStringEmpty = string.IsNullOrEmpty(searchString);
			var searchStringLower = searchString.ToLower();
			var searchKeywords = searchStringLower.Split(' ', ',', ';', '|', '，', '；'); // Some possible separators

			// The First Search
			foreach (var propStaticDataKWPair in propStaticDatas)
			{
				propStaticDataKWPair.Value.isSearchMatched = isSearchStringEmpty
					? true
					: IsWholeWordMatch(propStaticDataKWPair.Value.displayName, propStaticDataKWPair.Value.name, searchKeywords);
			}

			// Further adjust visibility
			if (!isSearchStringEmpty)
			{
				var searchModeTemp = searchMode;
				// Auto: search by group first, and search by property when there are no results
				if (searchModeTemp == SearchMode.Auto)
				{
					// if has no group
					if (!propStaticDatas.Any((propStaticDataKWPair => propStaticDataKWPair.Value.isSearchMatched && propStaticDataKWPair.Value.isMain)))
						searchModeTemp = SearchMode.Property;
					else
						searchModeTemp = SearchMode.Group;
				}

				// search by property
				if (searchModeTemp == SearchMode.Property)
				{
					// when a SubProp is displayed, the MainProp is also displayed
					foreach (var propStaticDataKWPair in propStaticDatas)
					{
						var propStaticData = propStaticDataKWPair.Value;
						if (propStaticData.isMain
						 && propStaticData.children.Any((childPropStaticData => propStaticDatas[childPropStaticData.name].isSearchMatched)))
							propStaticDataKWPair.Value.isSearchMatched = true;
					}
				}
				// search by group
				else if (searchModeTemp == SearchMode.Group)
				{
					// when search by group, all SubProps should display with MainProp
					foreach (var propStaticDataKWPair in propStaticDatas)
					{
						var propStaticData = propStaticDataKWPair.Value;
						if (propStaticData.isMain)
							foreach (var childPropStaticData in propStaticData.children)
								propStaticDatas[childPropStaticData.name].isSearchMatched = propStaticData.isSearchMatched;
					}
				}
			}
		}

		private static bool IsWholeWordMatch(string displayName, string propertyName, string[] searchingKeywords)
		{
			bool contains = true;
			displayName = displayName.ToLower();
			var name = propertyName.ToLower();

			foreach (var keyword in searchingKeywords)
			{
				var isMatch = false;
				isMatch |= displayName.Contains(keyword);
				isMatch |= name.Contains(keyword);
				contains &= isMatch;
			}
			return contains;
		}

		public void ToggleShowAllAdvancedProperties()
		{
			foreach (var propStaticDataKWPair in propStaticDatas)
			{
				if (propStaticDataKWPair.Value.isAdvancedHeader)
					propStaticDataKWPair.Value.isExpanding = displayModeData.showAllAdvancedProperties;
			}
		}

	}
}