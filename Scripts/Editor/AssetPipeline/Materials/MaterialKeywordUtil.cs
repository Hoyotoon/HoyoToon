#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;

namespace HoyoToon.Editor.AssetPipeline.Materials
{
    internal static class MaterialKeywordUtil
    {
        public static HashSet<string> ExtractKeywords(object raw)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            CollectKeywords(raw, set);
            return set;
        }

        public static void CollectKeywords(object raw, HashSet<string> output)
        {
            if (raw == null || output == null) return;

            if (raw is string keywordString)
            {
                foreach (var token in keywordString.Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = token.Trim();
                    if (!IsArtifactToken(trimmed)) output.Add(trimmed);
                }
                return;
            }

            if (raw is IDictionary<string, object> objectMap)
            {
                foreach (var kv in objectMap)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key) && IsTruthyKeywordMapValue(kv.Value))
                    {
                        var trimmed = kv.Key.Trim();
                        if (!IsArtifactToken(trimmed)) output.Add(trimmed);
                    }
                }
                return;
            }

            if (raw is IDictionary<string, bool> boolMap)
            {
                foreach (var kv in boolMap)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value)
                    {
                        var trimmed = kv.Key.Trim();
                        if (!IsArtifactToken(trimmed)) output.Add(trimmed);
                    }
                }
                return;
            }

            if (raw is IDictionary<string, string> stringMap)
            {
                foreach (var kv in stringMap)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key) && IsTruthyKeywordMapValue(kv.Value))
                    {
                        var trimmed = kv.Key.Trim();
                        if (!IsArtifactToken(trimmed)) output.Add(trimmed);
                    }
                }
                return;
            }

            if (raw is IDictionary nonGenericMap)
            {
                foreach (DictionaryEntry entry in nonGenericMap)
                {
                    if (entry.Key is string key && !string.IsNullOrWhiteSpace(key) && IsTruthyKeywordMapValue(entry.Value))
                    {
                        var trimmed = key.Trim();
                        if (!IsArtifactToken(trimmed)) output.Add(trimmed);
                    }
                }
                return;
            }

            if (raw is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    CollectKeywords(item, output);
                }
                return;
            }

            var fallback = raw.ToString();
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                var trimmedFb = fallback.Trim();
                if (!IsArtifactToken(trimmedFb)) output.Add(trimmedFb);
            }
        }

        public static bool IsArtifactToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return true;
            if (token == ",") return true;
            for (int i = 0; i < token.Length; i++)
            {
                if (char.IsLetterOrDigit(token[i])) return false;
            }
            return true;
        }

        public static List<string> CleanKeywordList(IEnumerable<string> source)
        {
            if (source == null) return null;
            var list = new List<string>();
            foreach (var entry in source)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                var token = entry.Trim();
                if (IsArtifactToken(token)) continue;
                list.Add(token);
            }
            return list.Count > 0 ? list : null;
        }

        private static bool IsTruthyKeywordMapValue(object value)
        {
            // Null values are treated as enabled to preserve legacy keyword-map semantics.
            return MaterialValueParsingUtil.ParseKeywordMapTruthyOrDefault(value);
        }
    }
}
#endif
