// Copyright (c) Jason Ma

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LWGUI
{
	public delegate void LWGUICustomGUIEvent(LWGUI lwgui);

	public class LWGUI : ShaderGUI
	{
		public LWGUIMetaDatas metaDatas;
		public bool hasChange;

		public static LWGUICustomGUIEvent onDrawCustomHeader;
		public static LWGUICustomGUIEvent onDrawCustomFooter;

		/// <summary>
		/// Called when switch to a new Material Window, each window has a LWGUI instance
		/// </summary>
		public LWGUI() { }

		/// <summary>
		/// Called every frame when the content is updated, such as the mouse moving in the material editor
		/// </summary>
		public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
		{
			//-----------------------------------------------------------------------------
			// Init Datas
			var material = editor.target as Material;
			var shader = material.shader;
			if (hasChange)
			{
				OnValidate(editor.targets);
				hasChange = false;
			}
			this.metaDatas = MetaDataHelper.BuildMetaDatas(shader, material, editor, this, props);


			//-----------------------------------------------------------------------------
			// Header
			if (onDrawCustomHeader != null)
				onDrawCustomHeader(this);

			// Toolbar
			bool enabled = GUI.enabled;
			GUI.enabled = true;
			var toolBarRect = EditorGUILayout.GetControlRect();
			toolBarRect.xMin = 2;

			Helper.DrawToolbarButtons(ref toolBarRect, metaDatas);

			Helper.DrawSearchField(toolBarRect, metaDatas);

			GUILayoutUtility.GetRect(0, 0); // Space(0)
			GUI.enabled = enabled;
			Helper.DrawSplitLine();
			DrawMaterialIdFilterButtons(props);


			//-----------------------------------------------------------------------------
			// Draw Properties
			{
				// move fields left to make rect for Revert Button
				editor.SetDefaultGUIWidths();
				RevertableHelper.InitRevertableGUIWidths();

				// start drawing properties
				foreach (var prop in props)
				{
					var (propStaticData, propDynamicData) = metaDatas.GetPropDatas(prop);

					// Visibility
					{
						if (!MetaDataHelper.GetPropertyVisibility(prop, material, metaDatas))
							continue;

						// Check entire parent chain for visibility
						bool allParentsVisible = true;
						var currentParent = propStaticData.parent;
						while (currentParent != null)
						{
							if (!MetaDataHelper.GetParentPropertyVisibility(currentParent, material, metaDatas))
							{
								allParentsVisible = false;
								break;
							}
							currentParent = currentParent.parent;
						}

						if (!allParentsVisible)
							continue;
					}               // Indent - count depth in hierarchy
					var indentLevel = EditorGUI.indentLevel;
					if (propStaticData.isAdvancedHeader)
						EditorGUI.indentLevel++;

					// Count parent depth for incremental indentation
					var parentDepth = 0;
					var tempParent = propStaticData.parent;
					while (tempParent != null)
					{
						parentDepth++;
						tempParent = tempParent.parent;
					}
					EditorGUI.indentLevel += parentDepth;                   // Advanced Header
					if (propStaticData.isAdvancedHeader && !propStaticData.isAdvancedHeaderProperty)
					{
						DrawAdvancedHeader(propStaticData, prop);

						if (!propStaticData.isExpanding)
						{
							RevertableHelper.SetRevertableGUIWidths();
							EditorGUI.indentLevel = indentLevel;
							continue;
						}
					}

					DrawProperty(prop);

					RevertableHelper.SetRevertableGUIWidths();
					EditorGUI.indentLevel = indentLevel;
				}

				editor.SetDefaultGUIWidths();
			}


			//-----------------------------------------------------------------------------
			// Footer
			EditorGUILayout.Space();
			Helper.DrawSplitLine();
			EditorGUILayout.Space();

			// Render settings
			if (SupportedRenderingFeatures.active.editableMaterialRenderQueue)
				editor.RenderQueueField();
			editor.EnableInstancingField();
			editor.LightmapEmissionProperty();
			editor.DoubleSidedGIField();

			// Custom Footer
			if (onDrawCustomFooter != null)
				onDrawCustomFooter(this);

			// LOGO
			EditorGUILayout.Space();
			Helper.DrawLogo();
		}

		private void DrawMaterialIdFilterButtons(MaterialProperty[] props)
		{
			if (metaDatas == null || props == null)
				return;

			int idCount = 0;
			foreach (var prop in props)
			{
				var propStaticData = metaDatas.GetPropStaticData(prop);
				if (propStaticData != null && propStaticData.isMaterialIdCountController)
					idCount = Mathf.Max(idCount, propStaticData.materialIdButtonCount);
			}

			var inspectorData = metaDatas.perInspectorData;
			if (idCount <= 0)
			{
				inspectorData.materialIdFilterCount = 0;
				inspectorData.materialIdFilterSelected = -1;
				return;
			}

			if (inspectorData.materialIdFilterCount != idCount)
			{
				inspectorData.materialIdFilterCount = idCount;
				inspectorData.materialIdFilterSelected = -1;
			}

			var rect = EditorGUILayout.GetControlRect();
			rect.xMin = 2;

			int buttonCount = idCount + 1;
			float spacing = 2f;
			float buttonWidth = (rect.width - spacing * (buttonCount - 1)) / buttonCount;

			for (int i = -1; i < idCount; i++)
			{
				int buttonIndex = i + 1;
				var buttonRect = new Rect(rect.x + buttonIndex * (buttonWidth + spacing), rect.y, buttonWidth, rect.height);
				bool isActive = inspectorData.materialIdFilterSelected == i;
				string label = i < 0 ? "All" : "ID " + i;

				if (GUI.Toggle(buttonRect, isActive, label, EditorStyles.miniButton) && !isActive)
					inspectorData.materialIdFilterSelected = i;
			}

			EditorGUILayout.Space(2f);
		}

		private void DrawAdvancedHeader(PropertyStaticData propStaticData, MaterialProperty prop)
		{
			EditorGUILayout.Space(3);
			var rect = EditorGUILayout.GetControlRect();
			var revertButtonRect = RevertableHelper.SplitRevertButtonRect(ref rect);
			var label = string.IsNullOrEmpty(propStaticData.advancedHeaderString) ? "Advanced" : propStaticData.advancedHeaderString;
			propStaticData.isExpanding = EditorGUI.Foldout(rect, propStaticData.isExpanding, label, EditorStyles.foldoutHeader);
			if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
				propStaticData.isExpanding = !propStaticData.isExpanding;
			RevertableHelper.DrawRevertableProperty(revertButtonRect, prop, metaDatas, true);
			Helper.DoPropertyContextMenus(rect, prop, metaDatas);
		}

		private void DrawProperty(MaterialProperty prop)
		{
			var (propStaticData, propDynamicData) = metaDatas.GetPropDatas(prop);
			var materialEditor = metaDatas.GetMaterialEditor();

			if (propStaticData.isAdvancedHeaderProperty)
				EditorGUILayout.Space(3);

			Helper.DrawHelpbox(propStaticData, propDynamicData);

			var label = new GUIContent(propStaticData.displayName, MetaDataHelper.GetPropertyTooltip(propStaticData, propDynamicData));
			var height = metaDatas.perInspectorData.materialEditor.GetPropertyHeight(prop, label.text);
			var rect = EditorGUILayout.GetControlRect(true, height);

			var revertButtonRect = RevertableHelper.SplitRevertButtonRect(ref rect);

			var enabled = GUI.enabled;
			if (propStaticData.isReadOnly) GUI.enabled = false;
			Helper.BeginProperty(rect, prop, metaDatas);
			Helper.DoPropertyContextMenus(rect, prop, metaDatas);

			RevertableHelper.FixGUIWidthMismatch(prop.GetPropertyType(), materialEditor);
			if (propStaticData.isAdvancedHeaderProperty)
				propStaticData.isExpanding = EditorGUI.Foldout(rect, propStaticData.isExpanding, string.Empty);

			RevertableHelper.DrawRevertableProperty(revertButtonRect, prop, metaDatas, propStaticData.isMain || propStaticData.isAdvancedHeaderProperty);
			materialEditor.ShaderProperty(rect, prop, label);

			Helper.EndProperty(metaDatas, prop);
			GUI.enabled = enabled;
		}

		public override void OnClosed(Material material)
		{
			base.OnClosed(material);
			MetaDataHelper.ReleaseMaterialMetadataCache(material);
		}

		public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
		{
			base.AssignNewShaderToMaterial(material, oldShader, newShader);
			if (newShader != oldShader)
				MetaDataHelper.ReleaseMaterialMetadataCache(material);
		}

		public static void OnValidate(Object[] materials)
		{
			VersionControlHelper.Checkout(materials);
			UnityEditorExtension.ApplyMaterialPropertyAndDecoratorDrawers(materials);
			MetaDataHelper.ForceUpdateMaterialsMetadataCache(materials);
		}

		// Called after edit in code
		public static void OnValidate(LWGUIMetaDatas metaDatas)
		{
			OnValidate(metaDatas?.GetMaterialEditor()?.targets);
		}

		public override void ValidateMaterial(Material material)
		{
			// Debug.Log($"ValidateMaterial {material.name}, {metaDatas}, {Event.current?.type}");

			// Validate a Faked Material when select/edit a Material
			if (metaDatas == null && (Event.current == null || Event.current.type == EventType.Layout))
			{
				// Skip to avoid lag when editing large amounts of materials
			}
			// Undo/Edit in Timeline (EventType.Repaint)
			// Note: When modifying the material in Timeline in Unity 2022, this function cannot correctly obtain the modified value.
			else if (metaDatas == null)
			{
				MetaDataHelper.ForceUpdateMaterialMetadataCache(material);
			}
			// Edit
			else
			{
				if (!hasChange) hasChange = true;
			}
		}
	}
}