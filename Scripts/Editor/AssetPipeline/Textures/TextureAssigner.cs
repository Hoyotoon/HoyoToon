#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.Editor.API;
using HoyoToon.Editor.AssetPipeline.Materials;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.AssetPipeline.Textures
{
    public static class TextureAssigner
    {
        public sealed class TextureLookupCache
        {
            internal readonly System.Collections.Generic.Dictionary<string, Texture2D> LookupByKey =
                new System.Collections.Generic.Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        }

        public static TextureLookupCache CreateLookupCache() => new TextureLookupCache();

        public static void AssignTextures(MaterialJsonStructure data, Material mat, GameMetadata meta, TextureLookupCache cache = null)
        {
            if (data == null || mat == null) return;

            var props = data.m_SavedProperties;
            if (props?.m_TexEnvs != null)
            {
                int assigned = 0;
                int total = 0;
                System.Collections.Generic.List<string> missing = null;

                foreach (var kv in props.m_TexEnvs)
                {
                    var propName = MaterialGeneration.ConvertName(kv.Key, meta);
                    if (!mat.HasProperty(propName)) continue;

                    var texName = kv.Value?.m_Texture?.Name;
                    var tex = FindTextureGlobalOrMapped(texName, propName, meta, cache);
                    if (!string.IsNullOrWhiteSpace(texName))
                    {
                        total++;
                        if (tex != null) { mat.SetTexture(propName, tex); assigned++; }
                        else { (missing ??= new System.Collections.Generic.List<string>()).Add(propName); }
                    }
                    else if (tex != null)
                    {
                        mat.SetTexture(propName, tex);
                    }

                    if (kv.Value?.m_Scale != null)
                        mat.SetTextureScale(propName, kv.Value.m_Scale.ToVector2());
                    if (kv.Value?.m_Offset != null)
                        mat.SetTextureOffset(propName, kv.Value.m_Offset.ToVector2());
                }

                if (total > 0)
                {
                    if (missing != null && missing.Count > 0)
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Info, $"Textures: Assigned {assigned}/{total} for '{mat.name}' [missing: {string.Join(", ", missing)}]");
                    else
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Info, $"Textures: Assigned {assigned}/{total} for '{mat.name}'");
                }
            }
        }

        private static Texture2D FindTextureGlobalOrMapped(string nameFromJson, string propName, GameMetadata meta, TextureLookupCache cache)
        {
            Texture2D tex = null;
            if (!string.IsNullOrWhiteSpace(nameFromJson))
            {
                tex = FindTextureGlobal(nameFromJson, cache);
                if (tex != null) return tex;
            }
            if (meta?.TextureMappings != null && meta.TextureMappings.TryGetValue(propName, out var mapped))
            {
                tex = FindTextureGlobal(mapped, cache);
                if (tex != null) return tex;
            }
            return null;
        }

        // Exposed for reuse by other texture utilities (e.g., TextureMapping)
        public static Texture2D FindTextureGlobal(string nameOrPath, TextureLookupCache cache = null)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath)) return null;

            if (cache != null && cache.LookupByKey.TryGetValue(nameOrPath, out var cached))
                return cached;

            if (nameOrPath.StartsWith("Assets/") || nameOrPath.StartsWith("Packages/"))
            {
                var direct = AssetDatabase.LoadAssetAtPath<Texture2D>(nameOrPath);
                if (direct != null)
                {
                    cache?.LookupByKey.TryAdd(nameOrPath, direct);
                    return direct;
                }
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
                    if (tex != null)
                    {
                        cache?.LookupByKey.TryAdd(nameOrPath, tex);
                        return tex;
                    }
                }
            }

            // Fallback: broader search with name verification
            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets($"t:Texture2D {baseName}");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileBase = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(fileBase, baseName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    cache?.LookupByKey.TryAdd(nameOrPath, tex);
                    return tex;
                }
            }

            return null;
        }
    }
}
#endif
