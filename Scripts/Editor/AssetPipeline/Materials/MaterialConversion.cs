#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HoyoToon;
using HoyoToon.Editor.API;
namespace HoyoToon.Editor.AssetPipeline.Materials
{
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
                m_ShaderKeywords = CloneKeywordContainer(src.m_ShaderKeywords),
                m_ValidKeywords = CloneKeywordContainer(src.m_ValidKeywords),
                m_InvalidKeywords = CloneKeywordContainer(src.m_InvalidKeywords),
                m_LightmapFlags = src.m_LightmapFlags,
                m_EnableInstancingVariants = src.m_EnableInstancingVariants,
                m_CustomRenderQueue = src.m_CustomRenderQueue,
                m_DisabledShaderPasses = MaterialKeywordUtil.CleanKeywordList(src.m_DisabledShaderPasses),
                m_StringTagMap = src.m_StringTagMap != null
                    ? new Dictionary<string, string>(src.m_StringTagMap, System.StringComparer.Ordinal)
                    : null,
                m_Name = src.m_Name,
            };

            return dst;
        }

        private static Dictionary<string, TVal> RemapDict<TVal>(Dictionary<string, TVal> src, Dictionary<string, string> map)
        {
            if (src == null || src.Count == 0) return src;

            bool anyMatch = false;
            foreach (var k in src.Keys)
            {
                if (k != null && map.ContainsKey(k)) { anyMatch = true; break; }
            }
            if (!anyMatch) return src;

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

        private static object CloneKeywordContainer(object value)
        {
            if (value == null) return null;
            if (value is string s)
            {
                return MaterialKeywordUtil.IsArtifactToken(s) ? null : s;
            }
            if (value is IEnumerable<string> stringList)
            {
                return MaterialKeywordUtil.CleanKeywordList(stringList);
            }
            if (value is IEnumerable<object> objectList)
            {
                return MaterialKeywordUtil.CleanKeywordList(objectList.Select(x => x?.ToString()));
            }

            // Preserve unknown container shapes for compatibility.
            return value;
        }
    }
}
#endif
