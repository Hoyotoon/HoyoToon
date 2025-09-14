// Copyright (c) Jason Ma

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LWGUI
{
	public static class UnityEditorExtension
	{

		#region MaterialEditor

		// For Developers: Call this after a material has modified in code
		public static void ApplyMaterialPropertyAndDecoratorDrawers(Material material)
		{
			var objs = new Object[] { material };
			ApplyMaterialPropertyAndDecoratorDrawers(objs);
		}

		// Called after edit, sets its attached value for each Drawer (usually Keywords, no modification of other properties is involved)
		// If you find that the default value of the newly created material does not meet expectations, please call PresetHelper.ApplyPresetsInMaterial()
		public static void ApplyMaterialPropertyAndDecoratorDrawers(Object[] targets)
		{
			if (!EditorMaterialUtility.disableApplyMaterialPropertyDrawers)
			{
				if (targets == null || targets.Length == 0)
					return;
				var target = targets[0] as Material;
				if (target == null)
					return;

				var shader = target.shader;
				string[] propNames = MaterialEditor.GetMaterialPropertyNames(targets);
				for (int i = 0; i < propNames.Length; i++)
				{
					var prop = MaterialEditor.GetMaterialProperty(targets, i);
					var drawer = ReflectionHelper.GetPropertyDrawer(shader, prop, out var decoratorDrawers);

					if (drawer != null)
					{
						drawer.Apply(prop);
					}
					if (decoratorDrawers != null)
					{
						foreach (var decoratorDrawer in decoratorDrawers)
						{
							decoratorDrawer.Apply(prop);
						}
					}
				}
			}
		}

		#endregion

		#region MaterialProperty

		public static ShaderPropertyType GetPropertyType(this MaterialProperty prop) 
#if UNITY_6000_1_OR_NEWER
			=> prop.propertyType;
#else
			=> (ShaderPropertyType)prop.type;
#endif

		public static ShaderPropertyFlags GetPropertyFlags(this MaterialProperty prop) 
#if UNITY_6000_1_OR_NEWER
			=> prop.propertyFlags;
#else
			=> (ShaderPropertyFlags)prop.flags;
#endif

		public static float GetNumericValue(this MaterialProperty prop)
		{
			switch (prop.GetPropertyType())
			{
				case ShaderPropertyType.Float or ShaderPropertyType.Range:
					return prop.floatValue;
				case ShaderPropertyType.Int:
					return prop.intValue;
				default:
					Debug.LogError($"LWGUI: Material Property { prop.name } is NOT numeric type.");
					return 0;
			}
		}

		public static void SetNumericValue(this MaterialProperty prop, float value)
		{
			switch (prop.GetPropertyType())
			{
				case ShaderPropertyType.Float or ShaderPropertyType.Range:
					prop.floatValue = value;
					break;
				case ShaderPropertyType.Int:
					prop.intValue = (int)value;
					break;
				default:
					Debug.LogError($"LWGUI: Material Property { prop.name } is NOT numeric type.");
					break;
			}
		}

		#endregion

	}
}