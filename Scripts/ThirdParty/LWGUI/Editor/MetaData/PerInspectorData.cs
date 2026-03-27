// Copyright (c) Jason Ma
// Per Shader > Per Material > Per Inspector

using UnityEditor;
using UnityEngine;

namespace LWGUI
{
	/// <summary>
	/// Contains metadata that may be different for each Inspector
	/// </summary>
	public class PerInspectorData
	{
		public MaterialEditor materialEditor = null;
		public int materialIdFilterCount = 0;
		public int materialIdFilterSelected = -1; // -1 = All

		public PerInspectorData() { }

		public void Update(MaterialEditor materialEditor)
		{
			this.materialEditor = materialEditor;
		}
	}
}