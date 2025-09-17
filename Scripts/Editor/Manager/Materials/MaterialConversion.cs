#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HoyoToon;
using HoyoToon.API;
namespace HoyoToon.Materials
{
    /// <summary>
    /// Applies metadata-based property name conversions to a parsed material JSON structure,
    /// without modifying the original JSON string. Returns a transformed view used for generation.
    /// </summary>
    public static class MaterialConversion
    {
        public static MaterialJsonStructure ApplyPropertyConversions(MaterialJsonStructure src, GameMetadata meta)
        {
            if (src == null || meta?.PropertyConversions == null || meta.PropertyConversions.Count == 0)
                return src;

            var map = meta.PropertyConversions; // old -> new
            var dst = new MaterialJsonStructure
            {
                m_Shader = src.m_Shader,
                m_SavedProperties = src.m_SavedProperties != null ? new MaterialJsonStructure.SavedProperties
                {
                    m_Floats = RemapDict(src.m_SavedProperties.m_Floats, map),
                    m_Ints = RemapDict(src.m_SavedProperties.m_Ints, map),
                    m_Colors = RemapDict(src.m_SavedProperties.m_Colors, map),
                    m_TexEnvs = RemapDict(src.m_SavedProperties.m_TexEnvs, map)
                } : null,
                Parameters = src.Parameters != null ? new MaterialJsonStructure.UnrealParameters
                {
                    Scalars = RemapDict(src.Parameters.Scalars, map),
                    Switches = RemapDict(src.Parameters.Switches, map),
                    Colors = RemapDict(src.Parameters.Colors, map),
                    Properties = RemapDict(src.Parameters.Properties, map),
                    BlendMode = src.Parameters.BlendMode,
                    ShadingModel = src.Parameters.ShadingModel,
                    RenderQueue = src.Parameters.RenderQueue
                } : null,
                Textures = RemapDict(src.Textures, map)
            };

            return dst;
        }

        private static Dictionary<string, TVal> RemapDict<TVal>(Dictionary<string, TVal> src, Dictionary<string, string> map)
        {
            if (src == null || src.Count == 0) return src;
            var dst = new Dictionary<string, TVal>(src.Count, System.StringComparer.Ordinal);
            foreach (var kv in src)
            {
                var key = (kv.Key != null && map.TryGetValue(kv.Key, out var mapped)) ? mapped : kv.Key;
                // If a collision occurs (both old and new exist), prefer the explicit new key already present
                if (dst.ContainsKey(key)) continue;
                dst[key] = kv.Value;
            }
            return dst;
        }
    }
}
#endif
