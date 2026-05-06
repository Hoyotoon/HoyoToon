using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoyoToon.Runtime.Utilities
{
    internal static class HsrRendererMaterialQueryUtility
    {
        internal const int MaterialPassCacheMaxEntries = 512;

        static int s_MaterialPassCacheVersion;

        internal static int MaterialPassCacheVersion => s_MaterialPassCacheVersion;

        internal readonly struct MaterialPassCacheKey : IEquatable<MaterialPassCacheKey>
        {
            readonly int m_MaterialId;
            readonly int m_ShaderId;
            readonly int m_ResolverId;
            readonly int m_ExcludedStateVersion;
            readonly string m_PreferredPassName;

            public MaterialPassCacheKey(Material material, string preferredPassName, int resolverId, int excludedStateVersion)
            {
                m_MaterialId = material != null ? material.GetInstanceID() : 0;
                Shader shader = material != null ? material.shader : null;
                m_ShaderId = shader != null ? shader.GetInstanceID() : 0;
                m_ResolverId = resolverId;
                m_ExcludedStateVersion = excludedStateVersion;
                m_PreferredPassName = preferredPassName ?? string.Empty;
            }

            public bool Equals(MaterialPassCacheKey other)
            {
                return m_MaterialId == other.m_MaterialId
                    && m_ShaderId == other.m_ShaderId
                    && m_ResolverId == other.m_ResolverId
                    && m_ExcludedStateVersion == other.m_ExcludedStateVersion
                    && string.Equals(m_PreferredPassName, other.m_PreferredPassName, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is MaterialPassCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = m_MaterialId;
                    hash = (hash * 397) ^ m_ShaderId;
                    hash = (hash * 397) ^ m_ResolverId;
                    hash = (hash * 397) ^ m_ExcludedStateVersion;
                    hash = (hash * 397) ^ (m_PreferredPassName != null ? StringComparer.Ordinal.GetHashCode(m_PreferredPassName) : 0);
                    return hash;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMaterialPassCacheVersion()
        {
            s_MaterialPassCacheVersion = 0;
        }

        internal static void InvalidateMaterialPassCaches()
        {
            unchecked
            {
                ++s_MaterialPassCacheVersion;
            }

            if (s_MaterialPassCacheVersion == 0)
                s_MaterialPassCacheVersion = 1;
        }

        internal static bool TryGetCachedMaterialPassIndex(
            Dictionary<MaterialPassCacheKey, int> cache,
            Material material,
            string preferredPassName,
            int resolverId,
            int excludedStateVersion,
            out int passIndex)
        {
            passIndex = -1;
            return cache != null
                && material != null
                && cache.TryGetValue(new MaterialPassCacheKey(material, preferredPassName, resolverId, excludedStateVersion), out passIndex);
        }

        internal static void StoreCachedMaterialPassIndex(
            Dictionary<MaterialPassCacheKey, int> cache,
            Material material,
            string preferredPassName,
            int resolverId,
            int excludedStateVersion,
            int passIndex)
        {
            if (cache == null || material == null)
                return;

            if (cache.Count >= MaterialPassCacheMaxEntries)
                cache.Clear();

            cache[new MaterialPassCacheKey(material, preferredPassName, resolverId, excludedStateVersion)] = passIndex;
        }

        internal static bool TryGetSharedMaterials(Renderer renderer, List<Material> scratch, out int materialCount)
        {
            materialCount = 0;

            if (renderer == null || scratch == null)
                return false;

            scratch.Clear();
            renderer.GetSharedMaterials(scratch);
            materialCount = scratch.Count;
            return materialCount > 0;
        }

        internal static void AddSharedMaterials(Renderer renderer, List<Material> scratch, HashSet<Material> uniqueMaterials)
        {
            if (uniqueMaterials == null)
                return;

            if (!TryGetSharedMaterials(renderer, scratch, out int materialCount))
                return;

            try
            {
                for (int i = 0; i < materialCount; ++i)
                {
                    Material material = scratch[i];
                    if (material != null)
                        uniqueMaterials.Add(material);
                }
            }
            finally
            {
                scratch.Clear();
            }
        }

        internal static bool HasMaterialTagValue(Material material, string tagName, string expectedValue, StringComparison comparison)
        {
            if (material == null || string.IsNullOrEmpty(tagName) || expectedValue == null)
                return false;

            string actualValue = material.GetTag(tagName, false, string.Empty);
            return string.Equals(actualValue, expectedValue, comparison);
        }

        internal static bool HasShaderPassWithTagValue(Material material, ShaderTagId tagName, string expectedValue, StringComparison comparison)
        {
            if (material == null || expectedValue == null)
                return false;

            Shader shader = material.shader;
            if (shader == null)
                return false;

            int passCount = shader.passCount;
            for (int passIndex = 0; passIndex < passCount; ++passIndex)
            {
                ShaderTagId tagValue = shader.FindPassTagValue(passIndex, tagName);
                if (string.Equals(tagValue.name, expectedValue, comparison))
                    return true;
            }

            return false;
        }

        internal static bool HasNamedPass(Material material, string passName)
        {
            return material != null
                && !string.IsNullOrEmpty(passName)
                && material.FindPass(passName) >= 0;
        }

        internal static bool HasMaterialTagOrShaderPassOrNamedPass(
            Material material,
            string materialTagName,
            ShaderTagId shaderTagName,
            string expectedValue,
            StringComparison comparison)
        {
            return HasMaterialTagValue(material, materialTagName, expectedValue, comparison)
                || HasShaderPassWithTagValue(material, shaderTagName, expectedValue, comparison)
                || HasNamedPass(material, expectedValue);
        }

        internal static int FindFirstShaderPassWithAnyTagValue(Material material, ShaderTagId tagName, IReadOnlyList<ShaderTagId> expectedValues)
        {
            if (material == null || expectedValues == null || expectedValues.Count == 0)
                return -1;

            Shader shader = material.shader;
            if (shader == null)
                return -1;

            int passCount = shader.passCount;
            for (int passIndex = 0; passIndex < passCount; ++passIndex)
            {
                ShaderTagId tagValue = shader.FindPassTagValue(passIndex, tagName);
                for (int i = 0; i < expectedValues.Count; ++i)
                {
                    if (tagValue == expectedValues[i])
                        return passIndex;
                }
            }

            return -1;
        }
    }
}
