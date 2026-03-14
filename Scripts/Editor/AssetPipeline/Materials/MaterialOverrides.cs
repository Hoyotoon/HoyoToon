#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.API;

namespace HoyoToon.Editor.AssetPipeline.Materials
{
    internal static class MaterialOverrides
    {
        public static void Apply(Material mat, GameMetadata meta)
        {
            if (mat == null || mat.shader == null || meta == null || meta.PropertyOverrides == null || meta.PropertyOverrides.Count == 0)
                return;

            var shaderPath = mat.shader.name; // shader.name holds the path used in Shader.Find
            if (string.IsNullOrEmpty(shaderPath)) return;

            if (!meta.PropertyOverrides.TryGetValue(shaderPath, out var overridesMap) || overridesMap == null || overridesMap.Count == 0)
                return;

            // Build property-name→type lookup once for this shader
            var shader = mat.shader;
            int count = shader.GetPropertyCount();
            var propTypes = new Dictionary<string, ShaderPropertyType>(count, StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                propTypes[shader.GetPropertyName(i)] = shader.GetPropertyType(i);
            }

            foreach (var kv in overridesMap)
            {
                var propName = kv.Key;
                var value = kv.Value;
                if (string.IsNullOrEmpty(propName)) continue;
                if (!mat.HasProperty(propName)) continue;

                try
                {
                    if (propTypes.TryGetValue(propName, out var propType))
                    {
                        switch (propType)
                        {
                            case ShaderPropertyType.Float:
                            case ShaderPropertyType.Range:
                                mat.SetFloat(propName, value);
                                continue;
                            case ShaderPropertyType.Color:
                                var c = mat.GetColor(propName);
                                c.r = c.g = c.b = value;
                                mat.SetColor(propName, c);
                                continue;
                            case ShaderPropertyType.Vector:
                                var v = mat.GetVector(propName);
                                v.x = value;
                                mat.SetVector(propName, v);
                                continue;
                            default:
                                continue;
                        }
                    }

                    // Fallback if not in property map
                    mat.SetFloat(propName, value);
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Material, LogLevel.Warning, $"MaterialOverrides: Failed applying override '{propName}' on '{shaderPath}': {ex.Message}");
                }
            }
        }
    }
}
#endif
