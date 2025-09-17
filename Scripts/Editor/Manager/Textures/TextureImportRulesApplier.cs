#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;
using HoyoToon.API;
using HoyoToon.Materials;

namespace HoyoToon.Textures
{
    /// <summary>
    /// Applies per-game TextureImporter settings based on API-configured rules.
    /// - Uses MaterialDetection.DetectGameAndShaderAutoWithSource to determine the game.
    /// - Supports NameEquals, NameEndsWith, NameContains rule sets (case-insensitive).
    /// - Applies only the most specific single matching rule (Equals > EndsWith > Contains; longest key wins within a group).
    /// </summary>
    public static class TextureImportRulesApplier
    {
        // Debug/reporting: last batch run details for UI display
        private static List<string> _lastBatchReport = new List<string>();
        public static string GetLastBatchReport()
            => _lastBatchReport != null && _lastBatchReport.Count > 0 ? string.Join("\n", _lastBatchReport) : "";

        /// <summary>
        /// Evaluate and return the matched rule for an asset without applying any changes.
        /// Returns true with the matched rule when a rule is found; false otherwise.
        /// </summary>
        public static bool TryEvaluateForAsset(string assetPath, UnityEngine.Object contextAsset, out string gameKey, out TextureImportRule matchedRule, out string matchedGroup, out string matchedKey)
        {
            gameKey = null; matchedRule = null; matchedGroup = null; matchedKey = null;
            if (string.IsNullOrWhiteSpace(assetPath)) return false;

            // Ensure this is a texture asset
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter)
                return false;

            // Detect game using provided context or the asset itself
            string contextPath = !string.IsNullOrEmpty(assetPath) ? assetPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
            var (gKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, contextPath);
            if (string.IsNullOrEmpty(gKey)) return false;

            var metaMap = HoyoToonApi.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(gKey, out var gameMeta) || gameMeta == null) return false;

            var rules = gameMeta.TextureImportSettings;
            if (rules == null) return false;

            // Normalize rule dictionaries to case-insensitive at usage time
            rules = EnsureCaseInsensitive(rules);

            string nameNoExt = Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty;
            var rule = FindBestRule(rules, nameNoExt, out matchedGroup, out matchedKey);
            if (rule == null) return false;

            matchedRule = rule;
            gameKey = gKey;
            return true;
        }

        /// <summary>
        /// Try to apply texture import settings for a specific asset path.
        /// Returns true if settings changed and the asset was reimported (when reimportIfChanged is true), false otherwise.
        /// </summary>
        public static bool TryApplyForAsset(string assetPath, UnityEngine.Object contextAsset = null, bool reimportIfChanged = true)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false; // Not a texture

            // Modify importer, then commit via WriteImportSettingsIfDirty + ImportAsset(ForceUpdate) when needed.

            // Detect game using provided context or the asset itself
            string contextPath = !string.IsNullOrEmpty(assetPath) ? assetPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
            var (gameKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, contextPath);
            if (string.IsNullOrEmpty(gameKey))
            {
                HoyoToonLogger.TextureWarning($"Texture rules: Could not detect game for '{assetPath}'. Applying pending changes (if any) and skipping.");
                return false;
            }

            var metaMap = HoyoToonApi.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(gameKey, out var gameMeta) || gameMeta == null)
            {
                HoyoToonLogger.TextureWarning($"Texture rules: No metadata found for game '{gameKey}' when processing '{assetPath}'. Applying pending changes (if any).");
                return false;
            }

            var rules = gameMeta.TextureImportSettings;
            if (rules == null)
            {
                HoyoToonLogger.TextureInfo($"Texture rules: Game '{gameKey}' has no TextureImportSettings. Applying pending changes (if any) for '{assetPath}'.");
                return false;
            }

            // Normalize rule dictionaries to case-insensitive at usage time
            rules = EnsureCaseInsensitive(rules);

            string nameNoExt = Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty;
            var defaults = rules.Defaults;
            var match = FindBestRule(rules, nameNoExt, out var matchedGroup, out var matchedKey);

            bool changed = false; List<string> changes = null;
            if (defaults != null)
            {
                changed |= ApplyRule(importer, assetPath, defaults, out var defChanges);
                if (defChanges != null && defChanges.Count > 0) changes = defChanges;
            }
            if (match != null)
            {
                changed |= ApplyRule(importer, assetPath, match, out var specChanges);
                if (specChanges != null && specChanges.Count > 0)
                {
                    changes = (changes ?? new List<string>());
                    changes.AddRange(specChanges);
                }
            }
            if (!changed)
            {
                return false;
            }
            if (changed && reimportIfChanged)
            {
                if (match != null)
                    HoyoToonLogger.TextureInfo($"Texture rules: Applied defaults{(match != null ? " + specific" : "")} (group '{matchedGroup}', key '{matchedKey}') for '{assetPath}'.");
                else
                    HoyoToonLogger.TextureInfo($"Texture rules: Applied defaults for '{assetPath}'.");
                if (changes != null && changes.Count > 0)
                {
                    HoyoToonLogger.TextureInfo($"  Changes: {string.Join(", ", changes)}");
                }

                try
                {
                    AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.TextureWarning($"SaveAndReimport failed for '{assetPath}': {ex.Message}");
                }
            }
            return changed;
        }

        /// <summary>
        /// Batch-apply texture import settings: modifies all eligible importers first, then commits once per changed asset.
        /// Returns number of assets changed.
        /// </summary>
        public static int TryApplyForAssetsBatch(IEnumerable<string> assetPaths, UnityEngine.Object contextAsset = null)
        {
            if (assetPaths == null) return 0;

            var changedImporters = new List<(TextureImporter importer, string path)>();
            _lastBatchReport = new List<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var assetPath in assetPaths)
                {
                    if (string.IsNullOrWhiteSpace(assetPath)) continue;
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null) continue;
                    // No need to track pre-existing dirty state; we only reimport when rules actually changed

                    // Detect and find rule
                    string contextPath = !string.IsNullOrEmpty(assetPath) ? assetPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
                    var (gameKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, contextPath);
                    if (string.IsNullOrEmpty(gameKey))
                    {
                        _lastBatchReport.Add($"- {Path.GetFileName(assetPath)}: detect=none → no changes");
                        continue;
                    }

                    var metaMap = HoyoToonApi.GetGameMetadata();
                    if (metaMap == null || !metaMap.TryGetValue(gameKey, out var gameMeta) || gameMeta == null)
                    {
                        _lastBatchReport.Add($"- {Path.GetFileName(assetPath)}: game '{gameKey}' meta missing → no changes");
                        continue;
                    }

                    var rules = EnsureCaseInsensitive(gameMeta.TextureImportSettings);
                    if (rules == null)
                    {
                        _lastBatchReport.Add($"- {Path.GetFileName(assetPath)}: no TextureImportSettings for '{gameKey}' → no changes");
                        continue;
                    }

                    string nameNoExt = Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty;
                    var defaults = rules.Defaults;
                    var match = FindBestRule(rules, nameNoExt, out var matchedGroup, out var matchedKey);

                    bool changed = false; List<string> changes = null;
                    if (defaults != null)
                    {
                        changed |= ApplyRule(importer, assetPath, defaults, out var defChanges);
                        if (defChanges != null && defChanges.Count > 0) changes = defChanges;
                    }
                    if (match != null)
                    {
                        changed |= ApplyRule(importer, assetPath, match, out var specChanges);
                        if (specChanges != null && specChanges.Count > 0)
                        {
                            changes = (changes ?? new List<string>());
                            changes.AddRange(specChanges);
                        }
                    }
                    if (changed)
                    {
                        changedImporters.Add((importer, assetPath));
                        var changeSummary = changes != null && changes.Count > 0 ? $" changes: {string.Join(", ", changes)}" : string.Empty;
                        if (match != null)
                        {
                            HoyoToonLogger.TextureInfo($"Texture rules (batch): Applied defaults + '{matchedGroup}' ('{matchedKey}') for '{assetPath}'.");
                            _lastBatchReport.Add($"- {Path.GetFileName(assetPath)}: defaults + {matchedGroup}='{matchedKey}' → CHANGED{changeSummary}");
                        }
                        else
                        {
                            HoyoToonLogger.TextureInfo($"Texture rules (batch): Applied defaults for '{assetPath}'.");
                            _lastBatchReport.Add($"- {Path.GetFileName(assetPath)}: defaults → CHANGED{changeSummary}");
                        }
                    }
                    else
                    {
                        if (match != null)
                            _lastBatchReport.Add($"- {Path.GetFileName(assetPath)}: match {matchedGroup}='{matchedKey}' → no changes");
                        else
                            _lastBatchReport.Add($"- {Path.GetFileName(assetPath)}: no rule matched; defaults made no changes");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            int applied = 0;
            // De-duplicate by path and apply only changed assets
            var toApply = changedImporters
                          .GroupBy(t => t.path, StringComparer.OrdinalIgnoreCase)
                          .Select(g => g.First());
            foreach (var (importer, path) in toApply)
            {
                try
                {
                    // Fetch fresh importer instance and commit via WriteImportSettingsIfDirty + ImportAsset(ForceUpdate)
                    var fresh = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (fresh != null)
                    {
                        try {
                            AssetDatabase.WriteImportSettingsIfDirty(path);
                            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        }
                        catch (Exception ex)
                        {
                            HoyoToonLogger.TextureWarning($"Batch reimport failed for '{path}': {ex.Message}");
                        }
                    }
                    applied++;
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.TextureWarning($"Batch apply failed for '{path}': {ex.Message}");
                }
            }
            return applied;
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

            // Helper to set a SerializedProperty if found; otherwise fall back to direct setter
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
                    // fallback
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

            // sRGBTexture
            if (rule.SRGBTexture.HasValue)
            {
                SetBool("sRGBTexture", () => importer.sRGBTexture, v => importer.sRGBTexture = v, rule.SRGBTexture.Value,
                        "m_SRGBTexture", "m_sRGBTexture");
            }

            // mipmapEnabled
            if (rule.MipmapEnabled.HasValue)
            {
                SetBool("mipmapEnabled", () => importer.mipmapEnabled, v => importer.mipmapEnabled = v, rule.MipmapEnabled.Value,
                        "m_EnableMipMap", "m_EnableMipmap", "mipMapEnabled");
            }

            // streamingMipmaps
            if (rule.StreamingMipmaps.HasValue)
            {
                SetBool("streamingMipmaps", () => importer.streamingMipmaps, v => importer.streamingMipmaps = v, rule.StreamingMipmaps.Value,
                        "m_EnableStreamingMipmaps", "m_StreamingMipmaps");
            }

            // textureCompression
            var compressionName = rule.TextureCompression;
            if (string.IsNullOrEmpty(compressionName) && !string.IsNullOrEmpty(rule.Compression))
                compressionName = rule.Compression;
            if (!string.IsNullOrEmpty(compressionName))
            {
                if (EnumTryParseIgnoreCase<TextureImporterCompression>(compressionName, out var comp))
                {
            SetEnum("textureCompression", () => importer.textureCompression, v => importer.textureCompression = v, comp,
                            "m_TextureCompression", "m_TextureCompressionMode");
                }
                else
                {
                    HoyoToonLogger.TextureWarning($"Texture rules: Unknown TextureImporterCompression '{compressionName}'.");
                }
            }

            // npotScale
            if (!string.IsNullOrEmpty(rule.NPOTScale))
            {
                if (EnumTryParseIgnoreCase<TextureImporterNPOTScale>(rule.NPOTScale, out var npot))
                {
            SetEnum("npotScale", () => importer.npotScale, v => importer.npotScale = v, npot,
                            "m_NPOTScale");
                }
                else
                {
                    HoyoToonLogger.TextureWarning($"Texture rules: Unknown TextureImporterNPOTScale '{rule.NPOTScale}'.");
                }
            }

            // textureType
            if (!string.IsNullOrEmpty(rule.TextureType))
            {
                if (EnumTryParseIgnoreCase<TextureImporterType>(rule.TextureType, out var ttype))
                {
            SetEnum("textureType", () => importer.textureType, v => importer.textureType = v, ttype,
                            "m_TextureType");
                }
                else
                {
                    HoyoToonLogger.TextureWarning($"Texture rules: Unknown TextureImporterType '{rule.TextureType}'.");
                }
            }

            // wrapMode
            if (!string.IsNullOrEmpty(rule.WrapMode))
            {
                if (EnumTryParseIgnoreCase<TextureWrapMode>(rule.WrapMode, out var wrap))
                {
            SetEnum("wrapMode", () => importer.wrapMode, v => importer.wrapMode = v, wrap,
                            "m_WrapMode");
                }
                else
                {
                    HoyoToonLogger.TextureWarning($"Texture rules: Unknown TextureWrapMode '{rule.WrapMode}'.");
                }
            }

            // maxTextureSize
            if (rule.MaxTextureSize.HasValue)
            {
          var clamped = Mathf.Max(32, rule.MaxTextureSize.Value);
          SetInt("maxTextureSize", () => importer.maxTextureSize, v => importer.maxTextureSize = v, clamped,
                       "m_MaxTextureSize");
            }

            // filterMode
            if (!string.IsNullOrEmpty(rule.FilterMode))
            {
                if (EnumTryParseIgnoreCase<FilterMode>(rule.FilterMode, out var fmode))
                {
                    SetEnum("filterMode", () => importer.filterMode, v => importer.filterMode = v, fmode,
                            "m_FilterMode");
                }
                else
                {
                    HoyoToonLogger.TextureWarning($"Texture rules: Unknown FilterMode '{rule.FilterMode}'.");
                }
            }

            if (so != null && changed)
            {
                // Apply without creating an undo record; does not dirty Inspector selection
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            changedProps = propChanges;
            return changed;
        }

        private static bool EnumTryParseIgnoreCase<TEnum>(string value, out TEnum result) where TEnum : struct
            => HoyoToonEditorUtil.EnumTryParseIgnoreCase(value, out result);

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
