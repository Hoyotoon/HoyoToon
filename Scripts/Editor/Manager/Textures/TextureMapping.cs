#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.API;

namespace HoyoToon.API
{
    /// <summary>
    /// Final pass texture mapping: if metadata TextureMappings define a texture for a property,
    /// set it on the material by globally searching AssetDatabase by name.
    /// Called after core assignment to ensure overrides are respected.
    /// </summary>
    public static class TextureMapping
    {
        public static void ApplyMappings(Material mat, GameMetadata meta)
        {
            if (mat == null || meta?.TextureMappings == null || meta.TextureMappings.Count == 0)
                return;

            foreach (var kv in meta.TextureMappings)
            {
                var propName = kv.Key;
                var texName = kv.Value;
                if (string.IsNullOrWhiteSpace(propName) || string.IsNullOrWhiteSpace(texName))
                    continue;
                if (!mat.HasProperty(propName))
                    continue;

                var tex = TextureAssigner.FindTextureGlobal(texName);
                if (tex != null)
                {
                    mat.SetTexture(propName, tex);
                }
            }
        }
    }
}
#endif
