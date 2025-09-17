#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.API;
using HoyoToon.Materials;

namespace HoyoToon.Textures
{
    /// <summary>
    /// Assigns textures to a material by globally searching AssetDatabase by texture name.
    /// Works for both Unity and Unreal JSON inputs; no game/package scoping.
    /// </summary>
    public static class TextureAssigner
    {
        public static void AssignTextures(MaterialJsonStructure data, Material mat, GameMetadata meta)
        {
            if (data == null || mat == null) return;

            // Unity format: TexEnvs with optional scale/offset
            var props = data.m_SavedProperties;
            if (props?.m_TexEnvs != null)
            {
                foreach (var kv in props.m_TexEnvs)
                {
                    var propName = MaterialGeneration.ConvertName(kv.Key, meta);
                    if (!mat.HasProperty(propName)) continue;

                    var texName = kv.Value?.m_Texture?.Name;
                    var tex = FindTextureGlobalOrMapped(texName, propName, meta);
                    if (tex != null) mat.SetTexture(propName, tex);

                    if (kv.Value?.m_Scale != null)
                        mat.SetTextureScale(propName, kv.Value.m_Scale.ToVector2());
                    if (kv.Value?.m_Offset != null)
                        mat.SetTextureOffset(propName, kv.Value.m_Offset.ToVector2());
                }
            }

            // Unreal format: Textures dictionary (name-based)
            if (data.Textures != null)
            {
                foreach (var kv in data.Textures)
                {
                    var propName = MaterialGeneration.ConvertName(kv.Key, meta);
                    if (!mat.HasProperty(propName)) continue;
                    var tex = FindTextureGlobalOrMapped(kv.Value, propName, meta);
                    if (tex != null) mat.SetTexture(propName, tex);
                }
            }
        }

        private static Texture2D FindTextureGlobalOrMapped(string nameFromJson, string propName, GameMetadata meta)
        {
            Texture2D tex = null;
            if (!string.IsNullOrWhiteSpace(nameFromJson))
            {
                tex = FindTextureGlobal(nameFromJson);
                if (tex != null) return tex;
            }
            if (meta?.TextureMappings != null && meta.TextureMappings.TryGetValue(propName, out var mapped))
            {
                tex = FindTextureGlobal(mapped);
                if (tex != null) return tex;
            }
            return null;
        }

        // Exposed for reuse by other texture utilities (e.g., TextureMapping)
        public static Texture2D FindTextureGlobal(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath)) return null;

            if (nameOrPath.StartsWith("Assets/") || nameOrPath.StartsWith("Packages/"))
            {
                var direct = AssetDatabase.LoadAssetAtPath<Texture2D>(nameOrPath);
                if (direct != null) return direct;
            }

            var baseName = Path.GetFileNameWithoutExtension(nameOrPath);
            if (string.IsNullOrEmpty(baseName)) baseName = nameOrPath;

            var guids = AssetDatabase.FindAssets($"{baseName} t:Texture2D");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileBase = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileBase, baseName, StringComparison.OrdinalIgnoreCase))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null) return tex;
                }
            }

            if (guids.Length == 0)
            {
                guids = AssetDatabase.FindAssets($"t:Texture2D {baseName}");
            }
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) return tex;
            }
            return null;
        }
    }
}
#endif
