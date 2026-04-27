// Copyright (c) Jason Ma
// Per Shader > Per Material > Per Inspector

using System.Collections.Generic;
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
		// Empty set means "All".
		public HashSet<int> materialIdFilterSelectedIds = new();

		public PerInspectorData() { }

		public void Update(MaterialEditor materialEditor)
		{
			this.materialEditor = materialEditor;
		}
	}
}