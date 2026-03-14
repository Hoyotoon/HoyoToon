#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.API;
using HoyoToon.Editor.AssetPipeline.Materials;

namespace HoyoToon.Editor.AssetPipeline.Textures
{
    public static class TextureImportRulesApplier
    {
        private static readonly Dictionary<string, TextureImportSettings> s_caseInsensitiveCache =
            new Dictionary<string, TextureImportSettings>(StringComparer.OrdinalIgnoreCase);

        public static bool TryEvaluateForAsset(string assetPath, UnityEngine.Object contextAsset, out string gameKey, out TextureImportRule matchedRule, out string matchedGroup, out string matchedKey)
        {
            gameKey = null; matchedRule = null; matchedGroup = null; matchedKey = null;
            if (string.IsNullOrWhiteSpace(assetPath)) return false;

            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter)
                return false;

            string contextPath = !string.IsNullOrEmpty(assetPath) ? assetPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
            var (gKey, _, _) = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, contextPath);
            if (string.IsNullOrEmpty(gKey)) return false;

            var metaMap = Api.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(gKey, out var gameMeta) || gameMeta == null) return false;

            var rules = GetCaseInsensitiveRules(gKey, gameMeta.TextureImportSettings);
            if (rules == null) return false;

            string nameNoExt = Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty;
            var rule = FindBestRule(rules, nameNoExt, out matchedGroup, out matchedKey);
            if (rule == null) return false;

            matchedRule = rule;
            gameKey = gKey;
            return true;
        }

        public static bool TryApplyForAsset(string assetPath, UnityEngine.Object contextAsset = null, bool reimportIfChanged = true, string knownGameKey = null)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;
            string gameKey = knownGameKey;
            if (string.IsNullOrEmpty(gameKey))
            {
                string contextPath = !string.IsNullOrEmpty(assetPath) ? assetPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
                var detected = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, contextPath);
                gameKey = detected.gameKey;
            }
            if (string.IsNullOrEmpty(gameKey))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"Texture rules: Could not detect game for '{assetPath}'.");
                return false;
            }

            var metaMap = Api.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(gameKey, out var gameMeta) || gameMeta == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"Texture rules: No metadata found for game '{gameKey}' when processing '{assetPath}'.");
                return false;
            }

            var rules = GetCaseInsensitiveRules(gameKey, gameMeta.TextureImportSettings);
            if (rules == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Info, $"Texture rules: Game '{gameKey}' has no TextureImportSettings for '{assetPath}'.");
                return false;
            }

            bool changed = ResolveAndApply(importer, assetPath, rules, out var matchedGroup, out var matchedKey, out var changes);
            if (!changed) return false;

            if (reimportIfChanged)
            {
                if (matchedKey != null)
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Info, $"Texture rules: Applied defaults + specific (group '{matchedGroup}', key '{matchedKey}') for '{assetPath}'.");
                else
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Info, $"Texture rules: Applied defaults for '{assetPath}'.");
                if (changes != null && changes.Count > 0)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Info, $"  Changes: {string.Join(", ", changes)}");
                }

                try
                {
                    AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"SaveAndReimport failed for '{assetPath}': {ex.Message}");
                }
            }
            return true;
        }

        public static (int applied, List<string> report) TryApplyForAssetsBatch(IEnumerable<string> assetPaths, UnityEngine.Object contextAsset = null)
        {
            var report = new List<string>();
            if (assetPaths == null) return (0, report);

            var changedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var batchMetaMap = Api.GetGameMetadata();

            string batchGameKey = null;
            foreach (var firstPath in assetPaths)
            {
                if (string.IsNullOrWhiteSpace(firstPath)) continue;
                if (AssetImporter.GetAtPath(firstPath) is not TextureImporter) continue;
                string cp = !string.IsNullOrEmpty(firstPath) ? firstPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
                var detected = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, cp);
                if (!string.IsNullOrEmpty(detected.gameKey))
                {
                    batchGameKey = detected.gameKey;
                    break;
                }
            }

            TextureImportSettings batchRules = null;
            if (!string.IsNullOrEmpty(batchGameKey) && batchMetaMap != null
                && batchMetaMap.TryGetValue(batchGameKey, out var batchGameMeta) && batchGameMeta != null)
            {
                batchRules = GetCaseInsensitiveRules(batchGameKey, batchGameMeta.TextureImportSettings);
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var assetPath in assetPaths)
                {
                    if (string.IsNullOrWhiteSpace(assetPath)) continue;
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null) continue;

                    string gameKey = batchGameKey;
                    if (string.IsNullOrEmpty(gameKey))
                    {
                        string contextPath = !string.IsNullOrEmpty(assetPath) ? assetPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
                        var (gk, _, _) = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, contextPath);
                        gameKey = gk;
                    }
                    if (string.IsNullOrEmpty(gameKey))
                    {
                        report.Add($"- {Path.GetFileName(assetPath)}: detect=none → no changes");
                        continue;
                    }

                    TextureImportSettings rules;
                    if (gameKey == batchGameKey && batchRules != null)
                    {
                        rules = batchRules;
                    }
                    else
                    {
                        if (batchMetaMap == null || !batchMetaMap.TryGetValue(gameKey, out var gameMeta) || gameMeta == null)
                        {
                            report.Add($"- {Path.GetFileName(assetPath)}: game '{gameKey}' meta missing → no changes");
                            continue;
                        }
                        rules = GetCaseInsensitiveRules(gameKey, gameMeta.TextureImportSettings);
                    }

                    if (rules == null)
                    {
                        report.Add($"- {Path.GetFileName(assetPath)}: no TextureImportSettings for '{gameKey}' → no changes");
                        continue;
                    }

                    bool changed = ResolveAndApply(importer, assetPath, rules, out var matchedGroup, out var matchedKey, out var changes);
                    if (changed)
                    {
                        changedPaths.Add(assetPath);
                        var changeSummary = changes != null && changes.Count > 0 ? $" changes: {string.Join(", ", changes)}" : string.Empty;
                        if (matchedKey != null)
                        {
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Info, $"Texture rules (batch): Applied defaults + '{matchedGroup}' ('{matchedKey}') for '{assetPath}'.");
                            report.Add($"- {Path.GetFileName(assetPath)}: defaults + {matchedGroup}='{matchedKey}' → CHANGED{changeSummary}");
                        }
                        else
                        {
                            HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Info, $"Texture rules (batch): Applied defaults for '{assetPath}'.");
                            report.Add($"- {Path.GetFileName(assetPath)}: defaults → CHANGED{changeSummary}");
                        }
                    }
                    else
                    {
                        if (matchedKey != null)
                            report.Add($"- {Path.GetFileName(assetPath)}: match {matchedGroup}='{matchedKey}' → no changes");
                        else
                            report.Add($"- {Path.GetFileName(assetPath)}: no rule matched; defaults made no changes");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            int applied = 0;
            foreach (var path in changedPaths)
            {
                try
                {
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    applied++;
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"Batch reimport failed for '{path}': {ex.Message}");
                }
            }
            return (applied, report);
        }

        private static bool ResolveAndApply(TextureImporter importer, string assetPath, TextureImportSettings rules,
            out string matchedGroup, out string matchedKey, out List<string> changes)
        {
            string nameNoExt = Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty;
            var match = FindBestRule(rules, nameNoExt, out matchedGroup, out matchedKey);

            bool changed = false;
            changes = null;

            if (rules.Defaults != null)
            {
                changed |= ApplyRule(importer, assetPath, rules.Defaults, out var defChanges);
                if (defChanges != null && defChanges.Count > 0) changes = defChanges;
            }
            if (match != null)
            {
                changed |= ApplyRule(importer, assetPath, match, out var specChanges);
                if (specChanges != null && specChanges.Count > 0)
                {
                    changes = changes ?? new List<string>();
                    changes.AddRange(specChanges);
                }
            }
            return changed;
        }

        private static TextureImportSettings GetCaseInsensitiveRules(string gameKey, TextureImportSettings src)
        {
            if (src == null) return null;
            if (s_caseInsensitiveCache.TryGetValue(gameKey, out var cached)) return cached;
            var result = EnsureCaseInsensitive(src);
            s_caseInsensitiveCache[gameKey] = result;
            return result;
        }

        // ---- internals ----
        private static TextureImportRule FindBestRule(TextureImportSettings rules, string nameNoExt, out string group, out string key)
        {
            group = null; key = null;
            if (rules == null || string.IsNullOrEmpty(nameNoExt)) return null;

            // 1) NameEquals (exact) – pick the longest key if multiple match exactly (identical length in practice)
            var eq = BestMatch(rules.NameEquals, nameNoExt, (pattern, name) => string.Equals(pattern, name, StringComparison.OrdinalIgnoreCase), allowEmptyKey: false);
            if (eq.rule != null)
            {
                group = "NameEquals"; key = eq.key; return eq.rule;
            }

            // 2) NameEndsWith – pick the longest suffix
            var ends = BestMatch(rules.NameEndsWith, nameNoExt, (pattern, name) => name.EndsWith(pattern, StringComparison.OrdinalIgnoreCase), allowEmptyKey: false);
            if (ends.rule != null)
            {
                group = "NameEndsWith"; key = ends.key; return ends.rule;
            }

            // 3) NameContains – pick the longest substring
            var contains = BestMatch(rules.NameContains, nameNoExt, (pattern, name) =>
            {
                // Treat empty pattern as a catch-all fallback
                if (string.IsNullOrEmpty(pattern)) return true;
                return name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
            }, allowEmptyKey: true);
            if (contains.rule != null)
            {
                group = "NameContains"; key = contains.key; return contains.rule;
            }

            return null;
        }

        private static (TextureImportRule rule, string key) BestMatch(Dictionary<string, TextureImportRule> dict, string name, Func<string, string, bool> predicate, bool allowEmptyKey)
        {
            if (dict == null || dict.Count == 0) return (null, null);
            TextureImportRule best = null; string bestKey = null; int bestLen = -1;
            foreach (var kv in dict)
            {
                var k = kv.Key; var r = kv.Value;
                if ((!allowEmptyKey && string.IsNullOrEmpty(k)) || r == null) continue;
                if (!predicate(k, name)) continue;
                int len = k.Length;
                if (len > bestLen) { best = r; bestKey = k; bestLen = len; }
            }
            return (best, bestKey);
        }

        private static TextureImportSettings EnsureCaseInsensitive(TextureImportSettings src)
        {
            if (src == null) return null;
            return new TextureImportSettings
            {
                Defaults = src.Defaults,
                NameEquals = Rewrap(src.NameEquals),
                NameContains = Rewrap(src.NameContains),
                NameEndsWith = Rewrap(src.NameEndsWith)
            };

            static Dictionary<string, TextureImportRule> Rewrap(Dictionary<string, TextureImportRule> d)
            {
                var m = new Dictionary<string, TextureImportRule>(StringComparer.OrdinalIgnoreCase);
                if (d == null) return m;
                foreach (var kv in d)
                {
                    var k = kv.Key ?? string.Empty;
                    m[k] = kv.Value;
                }
                return m;
            }
        }

        // If the asset is currently selected, modify via SerializedObject to avoid Inspector Apply/Revert bar.
        private static bool ApplyRule(TextureImporter importer, string assetPath, TextureImportRule rule, out List<string> changedProps)
        {
            bool changed = false;
            var propChanges = new List<string>();

            bool selected = IsAssetSelected(assetPath);
            SerializedObject so = null;
            if (selected)
            {
                so = new SerializedObject(importer);
                so.Update();
            }

            void SetBool(string label, Func<bool> getter, Action<bool> directSetter, bool value, params string[] candidates)
            {
                var old = getter();
                if (EqualityComparer<bool>.Default.Equals(old, value)) return;
                changed = true;
                propChanges.Add($"{label}: {old} -> {value}");
                if (so != null)
                {
                    foreach (var name in candidates)
                    {
                        var prop = so.FindProperty(name);
                        if (prop != null)
                        {
                            prop.boolValue = value;
                            return;
                        }
                    }
                    directSetter(value);
                }
                else
                {
                    directSetter(value);
                }
            }

            void SetInt(string label, Func<int> getter, Action<int> directSetter, int value, params string[] candidates)
            {
                var old = getter();
                if (EqualityComparer<int>.Default.Equals(old, value)) return;
                changed = true;
                propChanges.Add($"{label}: {old} -> {value}");
                if (so != null)
                {
                    foreach (var name in candidates)
                    {
                        var prop = so.FindProperty(name);
                        if (prop != null)
                        {
                            prop.intValue = value;
                            return;
                        }
                    }
                    directSetter(value);
                }
                else
                {
                    directSetter(value);
                }
            }

            void SetEnum<TEnum>(string label, Func<TEnum> getter, Action<TEnum> directSetter, TEnum value, params string[] candidates) where TEnum : struct
            {
                var old = getter();
                if (EqualityComparer<TEnum>.Default.Equals(old, value)) return;
                changed = true;
                propChanges.Add($"{label}: {old} -> {value}");
                if (so != null)
                {
                    foreach (var name in candidates)
                    {
                        var prop = so.FindProperty(name);
                        if (prop != null)
                        {
                            prop.enumValueIndex = Convert.ToInt32(value);
                            return;
                        }
                    }
                    directSetter(value);
                }
                else
                {
                    directSetter(value);
                }
            }

            if (rule.SRGBTexture.HasValue)
            {
                SetBool("sRGBTexture", () => importer.sRGBTexture, v => importer.sRGBTexture = v, rule.SRGBTexture.Value,
                        "m_SRGBTexture", "m_sRGBTexture");
            }

            if (rule.MipmapEnabled.HasValue)
            {
                SetBool("mipmapEnabled", () => importer.mipmapEnabled, v => importer.mipmapEnabled = v, rule.MipmapEnabled.Value,
                        "m_EnableMipMap", "m_EnableMipmap", "mipMapEnabled");
            }

            if (rule.StreamingMipmaps.HasValue)
            {
                SetBool("streamingMipmaps", () => importer.streamingMipmaps, v => importer.streamingMipmaps = v, rule.StreamingMipmaps.Value,
                        "m_EnableStreamingMipmaps", "m_StreamingMipmaps");
            }

            var compressionName = rule.TextureCompression;
            if (!string.IsNullOrEmpty(compressionName))
            {
                if (Enum.TryParse<TextureImporterCompression>(compressionName, true, out var comp))
                {
            SetEnum("textureCompression", () => importer.textureCompression, v => importer.textureCompression = v, comp,
                            "m_TextureCompression", "m_TextureCompressionMode");
                }
                else
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"Texture rules: Unknown TextureImporterCompression '{compressionName}'.");
                }
            }

            if (!string.IsNullOrEmpty(rule.NPOTScale))
            {
                if (Enum.TryParse<TextureImporterNPOTScale>(rule.NPOTScale, true, out var npot))
                {
            SetEnum("npotScale", () => importer.npotScale, v => importer.npotScale = v, npot,
                            "m_NPOTScale");
                }
                else
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"Texture rules: Unknown TextureImporterNPOTScale '{rule.NPOTScale}'.");
                }
            }

            if (!string.IsNullOrEmpty(rule.TextureType))
            {
                if (Enum.TryParse<TextureImporterType>(rule.TextureType, true, out var ttype))
                {
            SetEnum("textureType", () => importer.textureType, v => importer.textureType = v, ttype,
                            "m_TextureType");
                }
                else
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"Texture rules: Unknown TextureImporterType '{rule.TextureType}'.");
                }
            }

            if (!string.IsNullOrEmpty(rule.WrapMode))
            {
                if (Enum.TryParse<TextureWrapMode>(rule.WrapMode, true, out var wrap))
                {
            SetEnum("wrapMode", () => importer.wrapMode, v => importer.wrapMode = v, wrap,
                            "m_WrapMode");
                }
                else
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"Texture rules: Unknown TextureWrapMode '{rule.WrapMode}'.");
                }
            }

            if (rule.MaxTextureSize.HasValue)
            {
          var clamped = Mathf.Max(32, rule.MaxTextureSize.Value);
          SetInt("maxTextureSize", () => importer.maxTextureSize, v => importer.maxTextureSize = v, clamped,
                       "m_MaxTextureSize");
            }

            if (!string.IsNullOrEmpty(rule.FilterMode))
            {
                if (Enum.TryParse<FilterMode>(rule.FilterMode, true, out var fmode))
                {
                    SetEnum("filterMode", () => importer.filterMode, v => importer.filterMode = v, fmode,
                            "m_FilterMode");
                }
                else
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Texture, LogLevel.Warning, $"Texture rules: Unknown FilterMode '{rule.FilterMode}'.");
                }
            }

            if (so != null && changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            changedProps = propChanges;
            return changed;
        }

        private static bool IsAssetSelected(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var active = Selection.activeObject;
            if (active == null) return false;
            var selPath = AssetDatabase.GetAssetPath(active);
            if (string.IsNullOrEmpty(selPath)) return false;
            return string.Equals(selPath, assetPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
