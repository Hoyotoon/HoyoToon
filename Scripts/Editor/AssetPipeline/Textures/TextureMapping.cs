#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.API;


namespace HoyoToon.Editor.AssetPipeline.Textures
{
    public static class TextureMapping
    {
        public static void ApplyMappings(Material mat, GameMetadata meta, TextureAssigner.TextureLookupCache cache = null)
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

                var tex = TextureAssigner.FindTextureGlobal(texName, cache);
                if (tex != null)
                {
                    mat.SetTexture(propName, tex);
                }
            }
        }
    }
}
#endif
