#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using HoyoToon.Utilities;

namespace HoyoToon.API
{
    /// <summary>
    /// Applies per-shader property override values defined in GameMetadata.PropertyOverrides.
    /// These run after JSON property assignment but before texture mappings/import rules.
    /// Supports float/int properties only; color/vector overrides can be added later if needed.
    /// </summary>
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

            foreach (var kv in overridesMap)
            {
                var propName = kv.Key;
                var value = kv.Value;
                if (string.IsNullOrEmpty(propName)) continue;
                if (!mat.HasProperty(propName)) continue;

                // Determine property type and assign appropriately (manual scan; FindPropertyIndex not available)
                try
                {
                    var shader = mat.shader;
                    int count = UnityEditor.ShaderUtil.GetPropertyCount(shader);
                    UnityEditor.ShaderUtil.ShaderPropertyType? foundType = null;
                    for (int i = 0; i < count; i++)
                    {
                        if (UnityEditor.ShaderUtil.GetPropertyName(shader, i) == propName)
                        {
                            foundType = UnityEditor.ShaderUtil.GetPropertyType(shader, i);
                            break;
                        }
                    }

                    if (foundType.HasValue)
                    {
                        switch (foundType.Value)
                        {
                            case UnityEditor.ShaderUtil.ShaderPropertyType.Float:
                            case UnityEditor.ShaderUtil.ShaderPropertyType.Range:
                                mat.SetFloat(propName, value);
                                continue;
                            case UnityEditor.ShaderUtil.ShaderPropertyType.Color:
                                var c = mat.GetColor(propName);
                                c.r = c.g = c.b = value;
                                mat.SetColor(propName, c);
                                continue;
                            case UnityEditor.ShaderUtil.ShaderPropertyType.Vector:
                                var v = mat.GetVector(propName);
                                v.x = value;
                                mat.SetVector(propName, v);
                                continue;
                            default:
                                // Ignore TexEnv or other types for numeric overrides
                                continue;
                        }
                    }

                    // Fallback if type not found
                    mat.SetFloat(propName, value);
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.MaterialWarning($"MaterialOverrides: Failed applying override '{propName}' on '{shaderPath}': {ex.Message}");
                }
            }
        }
    }
}
#endif
