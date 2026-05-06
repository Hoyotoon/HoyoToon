using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoyoToon.Runtime.Rendering.Utilities
{
    internal static class MaterialPassResolver
    {
        private const int DefaultMaxEntries = 512;
        private const int NamedPassQueryId = -1001;
        private const int LightModePassQueryId = -1002;
        private static readonly ShaderTagId s_LightModeTag = new ShaderTagId("LightMode");
        private static readonly Dictionary<CacheKey, CacheEntry> s_PassIndexByKey =
            new Dictionary<CacheKey, CacheEntry>(DefaultMaxEntries);
        private static readonly List<CacheKey> s_RemovalScratch = new List<CacheKey>(DefaultMaxEntries);
        private static int s_UseCounter;

        internal static int ResolveFirstPass(
            Material material,
            IReadOnlyList<string> passNames,
            int queryId,
            bool fallbackToFirstPass,
            int maxEntries = DefaultMaxEntries)
        {
            if (material == null)
            {
                return -1;
            }

            Shader shader = material.shader;
            var key = new CacheKey(
                material.GetInstanceID(),
                shader != null ? shader.GetInstanceID() : 0,
                Mathf.Max(0, material.passCount),
                queryId,
                ComputePassNamesHash(passNames),
                fallbackToFirstPass);

            if (s_PassIndexByKey.TryGetValue(key, out CacheEntry cachedEntry))
            {
                cachedEntry.LastUsed = NextUseCounter();
                s_PassIndexByKey[key] = cachedEntry;
                return cachedEntry.PassIndex;
            }

            int resolvedPassIndex = FindFirstPass(material, passNames, fallbackToFirstPass);
            if (s_PassIndexByKey.Count >= Mathf.Max(1, maxEntries))
            {
                PruneLeastRecentlyUsed(Mathf.Max(1, maxEntries / 2));
            }

            s_PassIndexByKey[key] = new CacheEntry
            {
                PassIndex = resolvedPassIndex,
                LastUsed = NextUseCounter()
            };
            return resolvedPassIndex;
        }

        internal static int ResolveNamedPass(
            Material material,
            string passName,
            int maxEntries = DefaultMaxEntries)
        {
            if (material == null || string.IsNullOrEmpty(passName))
                return -1;

            return ResolveCachedPass(
                material,
                NamedPassQueryId,
                StringComparer.Ordinal.GetHashCode(passName),
                fallbackToFirstPass: false,
                maxEntries,
                () => material.FindPass(passName));
        }

        internal static int ResolveFirstLightModePass(
            Material material,
            IReadOnlyList<ShaderTagId> tags,
            int maxEntries = DefaultMaxEntries)
        {
            if (material == null || tags == null || tags.Count == 0)
                return -1;

            return ResolveCachedPass(
                material,
                LightModePassQueryId,
                ComputeShaderTagsHash(tags),
                fallbackToFirstPass: false,
                maxEntries,
                () => FindFirstLightModePass(material, tags));
        }

        internal static void ClearQuery(int queryId)
        {
            s_RemovalScratch.Clear();
            foreach (CacheKey key in s_PassIndexByKey.Keys)
            {
                if (key.QueryId == queryId)
                {
                    s_RemovalScratch.Add(key);
                }
            }

            for (int i = 0; i < s_RemovalScratch.Count; ++i)
            {
                s_PassIndexByKey.Remove(s_RemovalScratch[i]);
            }

            s_RemovalScratch.Clear();
        }

        internal static void ClearAll()
        {
            s_PassIndexByKey.Clear();
            s_RemovalScratch.Clear();
            s_UseCounter = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ClearAll();
        }

        private static int FindFirstPass(Material material, IReadOnlyList<string> passNames, bool fallbackToFirstPass)
        {
            if (passNames != null)
            {
                for (int i = 0; i < passNames.Count; ++i)
                {
                    string passName = passNames[i];
                    if (string.IsNullOrEmpty(passName))
                    {
                        continue;
                    }

                    int passIndex = material.FindPass(passName);
                    if (passIndex >= 0)
                    {
                        return passIndex;
                    }
                }
            }

            return fallbackToFirstPass && material.passCount > 0 ? 0 : -1;
        }

        private static int ResolveCachedPass(
            Material material,
            int queryId,
            int queryKey,
            bool fallbackToFirstPass,
            int maxEntries,
            Func<int> resolver)
        {
            Shader shader = material.shader;
            var key = new CacheKey(
                material.GetInstanceID(),
                shader != null ? shader.GetInstanceID() : 0,
                Mathf.Max(0, material.passCount),
                queryId,
                queryKey,
                fallbackToFirstPass);

            if (s_PassIndexByKey.TryGetValue(key, out CacheEntry cachedEntry))
            {
                cachedEntry.LastUsed = NextUseCounter();
                s_PassIndexByKey[key] = cachedEntry;
                return cachedEntry.PassIndex;
            }

            int resolvedPassIndex = resolver != null ? resolver() : -1;
            if (s_PassIndexByKey.Count >= Mathf.Max(1, maxEntries))
                PruneLeastRecentlyUsed(Mathf.Max(1, maxEntries / 2));

            s_PassIndexByKey[key] = new CacheEntry
            {
                PassIndex = resolvedPassIndex,
                LastUsed = NextUseCounter()
            };
            return resolvedPassIndex;
        }

        private static int FindFirstLightModePass(Material material, IReadOnlyList<ShaderTagId> tags)
        {
            Shader shader = material != null ? material.shader : null;
            if (shader == null || tags == null)
                return -1;

            int passCount = Mathf.Max(0, material.passCount);
            for (int passIndex = 0; passIndex < passCount; ++passIndex)
            {
                ShaderTagId tagValue = shader.FindPassTagValue(passIndex, s_LightModeTag);
                for (int tagIndex = 0; tagIndex < tags.Count; ++tagIndex)
                {
                    if (tagValue.Equals(tags[tagIndex]))
                        return passIndex;
                }
            }

            return -1;
        }

        private static int ComputePassNamesHash(IReadOnlyList<string> passNames)
        {
            unchecked
            {
                int hash = 17;
                if (passNames == null)
                {
                    return hash;
                }

                for (int i = 0; i < passNames.Count; ++i)
                {
                    hash = (hash * 31) + (passNames[i] != null ? StringComparer.Ordinal.GetHashCode(passNames[i]) : 0);
                }

                return hash;
            }
        }

        private static int ComputeShaderTagsHash(IReadOnlyList<ShaderTagId> tags)
        {
            unchecked
            {
                int hash = 17;
                if (tags == null)
                    return hash;

                for (int i = 0; i < tags.Count; ++i)
                    hash = (hash * 31) + tags[i].GetHashCode();

                return hash;
            }
        }

        private static int NextUseCounter()
        {
            unchecked
            {
                s_UseCounter++;
                if (s_UseCounter <= 0)
                {
                    s_UseCounter = 1;
                }

                return s_UseCounter;
            }
        }

        private static void PruneLeastRecentlyUsed(int removeCount)
        {
            s_RemovalScratch.Clear();
            for (int removeIndex = 0; removeIndex < removeCount && s_PassIndexByKey.Count > 0; ++removeIndex)
            {
                CacheKey oldestKey = default;
                int oldestUse = int.MaxValue;
                bool found = false;
                foreach (KeyValuePair<CacheKey, CacheEntry> entry in s_PassIndexByKey)
                {
                    if (!found || entry.Value.LastUsed < oldestUse)
                    {
                        oldestKey = entry.Key;
                        oldestUse = entry.Value.LastUsed;
                        found = true;
                    }
                }

                if (!found)
                {
                    break;
                }

                s_RemovalScratch.Add(oldestKey);
                s_PassIndexByKey.Remove(oldestKey);
            }

            s_RemovalScratch.Clear();
        }

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            internal CacheKey(
                int materialId,
                int shaderId,
                int passCount,
                int queryId,
                int passNamesHash,
                bool fallbackToFirstPass)
            {
                MaterialId = materialId;
                ShaderId = shaderId;
                PassCount = passCount;
                QueryId = queryId;
                PassNamesHash = passNamesHash;
                FallbackToFirstPass = fallbackToFirstPass ? 1 : 0;
            }

            internal readonly int MaterialId;
            internal readonly int ShaderId;
            internal readonly int PassCount;
            internal readonly int QueryId;
            internal readonly int PassNamesHash;
            internal readonly int FallbackToFirstPass;

            public bool Equals(CacheKey other)
            {
                return MaterialId == other.MaterialId
                    && ShaderId == other.ShaderId
                    && PassCount == other.PassCount
                    && QueryId == other.QueryId
                    && PassNamesHash == other.PassNamesHash
                    && FallbackToFirstPass == other.FallbackToFirstPass;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + MaterialId;
                    hash = (hash * 31) + ShaderId;
                    hash = (hash * 31) + PassCount;
                    hash = (hash * 31) + QueryId;
                    hash = (hash * 31) + PassNamesHash;
                    hash = (hash * 31) + FallbackToFirstPass;
                    return hash;
                }
            }
        }

        private struct CacheEntry
        {
            internal int PassIndex;
            internal int LastUsed;
        }
    }
}
