#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.API;
using HoyoToon.Editor.AssetPipeline.Textures;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.AssetPipeline.Materials
{
        public static class MaterialDetection
        {

        public static (string gameKey, string shaderPath, string sourceJson) DetectGameAndShaderAutoWithSource(UnityEngine.Object assetOrNull = null, string pathOrJson = null, bool silent = false)
        {
            if (!string.IsNullOrWhiteSpace(pathOrJson))
            {
                if (EditorUtil.LooksLikeJson(pathOrJson))
                {
                    if (TryDetectFromJson(pathOrJson, out var g, out var s, out _, out _))
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, $"Detection (raw JSON): Game='{g}', Shader='{s}'");
                        return (g, s, "<raw-json>");
                    }
                    return (null, null, null);
                }

                var p = pathOrJson;
                if (p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var abs = EditorUtil.ToAbsolutePath(p);
                    if (TryDetectFromJsonFile(abs, out var g, out var s, out _, out _))
                    {
                        var charName = TryExtractCharacterName(g, abs);
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info,
                            !string.IsNullOrEmpty(charName)
                                ? $"Detection (file): Game='{g}', Shader='{s}', JSON='{abs}', Character='{charName}'"
                                : $"Detection (file): Game='{g}', Shader='{s}', JSON='{abs}'");
                        if (!silent) NotifyProblemIfNeeded(g, charName);
                        return (g, s, abs);
                    }
                    return (null, null, null);
                }
                return DetectFromContextPathWithSource(p);
            }

            if (assetOrNull != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(assetOrNull);
                var result = DetectFromContextPathWithSource(assetPath);
                if (!string.IsNullOrEmpty(result.gameKey))
                {
                    var charName = TryExtractCharacterName(result.gameKey, assetPath);
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info,
                        !string.IsNullOrEmpty(charName)
                            ? $"Detection (context): Game='{result.gameKey}', Shader='{result.shaderPath}', JSON='{result.sourceJson}', Character='{charName}'"
                            : $"Detection (context): Game='{result.gameKey}', Shader='{result.shaderPath}', JSON='{result.sourceJson}'");
                    if (!silent) NotifyProblemIfNeeded(result.gameKey, charName);
                }
                return result;
            }

            return (null, null, null);
        }

        public static IReadOnlyList<(string gameKey, string shaderPath, string sourceJson)> DetectGameAndShaderAutoWithSourceMany(UnityEngine.Object assetOrNull = null, string pathOrJson = null)
        {
            var results = new List<(string gameKey, string shaderPath, string sourceJson)>();

            // Prefer explicit string input
            if (!string.IsNullOrWhiteSpace(pathOrJson))
            {
                if (EditorUtil.LooksLikeJson(pathOrJson))
                {
                    if (TryDetectFromJson(pathOrJson, out var g, out var s, out _, out _))
                    {
                        results.Add((g, s, "<raw-json>"));
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, $"DetectionMany (raw JSON): Game='{g}', Shader='{s}'");
                    }
                    return results;
                }

                var p = pathOrJson;
                if (p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var abs = EditorUtil.ToAbsolutePath(p);
                    if (TryDetectFromJsonFile(abs, out var g, out var s, out _, out _))
                    {
                        results.Add((g, s, abs));
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, $"DetectionMany (file): Game='{g}', Shader='{s}', JSON='{abs}'");
                    }
                    else
                    {
                        results.Add((null, null, abs));
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Warning, $"DetectionMany (file): Unsupported JSON '{abs}'");
                    }
                    return results;
                }

                // Context scan for many
                return DetectManyFromContextPath(p);
            }

            if (assetOrNull != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(assetOrNull);
                var list = DetectManyFromContextPath(assetPath) ?? new List<(string, string, string)>();

                // Preflight prompt from scan results (avoids redundant filesystem scan)
                string preGame = null;
                foreach (var (g, _, _) in list)
                {
                    if (!string.IsNullOrEmpty(g)) { preGame = g; break; }
                }
                if (!string.IsNullOrEmpty(preGame))
                {
                    var preChar = TryExtractCharacterName(preGame, assetPath);
                    if (!string.IsNullOrEmpty(preChar))
                    {
                        var preEntry = FindProblemEntry(preGame, preChar);
                        if (preEntry != null)
                        {
                            bool proceed = PromptBatchProceed(preGame, preChar, preEntry);
                            if (!proceed)
                            {
                                HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, $"Batch detection cancelled by user at preflight: Game='{preGame}', Character='{preChar}'");
                                return new List<(string, string, string)>();
                            }
                        }
                    }
                }

                int validCount = 0;
                string firstGame = null;
                foreach (var (g, s, src) in list)
                {
                    if (!string.IsNullOrEmpty(g)) { validCount++; firstGame ??= g; }
                }
                if (validCount > 0)
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, $"DetectionMany: Found {validCount} material(s) for game '{firstGame}' near '{Path.GetFileName(assetPath)}'");
                return list;
            }

            return results;
        }

        private static IReadOnlyList<(string gameKey, string shaderPath, string sourceJson)> DetectManyFromContextPath(string assetPath)
        {
            var list = new List<(string gameKey, string shaderPath, string sourceJson)>();
            if (string.IsNullOrWhiteSpace(assetPath)) return list;

            // Direct JSON file path
            if (assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var abs = EditorUtil.ToAbsolutePath(assetPath);
                if (TryDetectFromJsonFile(abs, out var g, out var s, out _, out _)) list.Add((g, s, abs));
                else list.Add((null, null, abs));
                return list;
            }

            if (!TryFindWorkspaceRootAndMaterials(assetPath, out var rootDir, out var materialsDir))
                return list;

            try
            {
                var startDir = Directory.Exists(assetPath) ? assetPath : Path.GetDirectoryName(EditorUtil.ToAbsolutePath(assetPath));
                var jsons = Directory.EnumerateFiles(materialsDir, "*.json", SearchOption.AllDirectories)
                    .Select(p => new { path = p, dist = EditorUtil.DirDistance(startDir, Path.GetDirectoryName(EditorUtil.ToAbsolutePath(p))) })
                    .OrderBy(x => x.dist)
                    .Select(x => x.path);

                foreach (var jsonPath in jsons)
                {
                    var abs = EditorUtil.ToAbsolutePath(jsonPath);
                    if (TryDetectFromJsonFile(abs, out var game, out var shaderPath, out _, out _))
                        list.Add((game, shaderPath, abs));
                }
            }
            catch (Exception ex)
            {
                // Intentionally avoid breaking detection flows.
                HoyoToonLogger.ThrottleWarning("MaterialDetection.ContextScan", $"Context scan failed: {ex.Message}");
            }

            return list;
        }
        public static (string gameKey, string sourceJson) DetectGameAutoOnly(UnityEngine.Object assetOrNull = null, string pathOrJson = null, bool silent = false)
        {
            var (g, _, src) = DetectGameAndShaderAutoWithSource(assetOrNull, pathOrJson, silent);
            return (g, src);
        }

        public static string TryExtractCharacterName(string gameKey, string contextAssetPath)
        {
            try
            {
                if (string.IsNullOrEmpty(gameKey) || string.IsNullOrEmpty(contextAssetPath)) return null;
                var metaMap = Api.GetGameMetadata();
                if (metaMap == null || !metaMap.TryGetValue(gameKey, out var meta) || meta == null) return null;
                if (meta.ProblemList == null) return null;

                // Find the Textures directory near the context and scan for a representative texture filename
                if (!TryFindWorkspaceRootAndMaterials(contextAssetPath, out var rootDir, out var materialsDir)) return null;
                var root = rootDir;
                if (string.IsNullOrEmpty(root)) root = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(contextAssetPath));
                var texturesDir = EditorUtil.FindChildDirectoryIgnoreCase(root, "Textures");
                if (string.IsNullOrEmpty(texturesDir))
                {
                    // Probe upwards for any Textures
                    var di = new DirectoryInfo(root);
                    while (di != null && string.IsNullOrEmpty(texturesDir))
                    {
                        texturesDir = EditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Textures");
                        di = di.Parent;
                    }
                }
                if (string.IsNullOrEmpty(texturesDir) || !Directory.Exists(texturesDir)) return null;

                string containsToken = meta.ProblemList.Texture;
                IEnumerable<string> files = Directory.EnumerateFiles(texturesDir, "*.*", SearchOption.AllDirectories)
                    .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".exr", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(containsToken))
                {
                    files = files.Where(p => System.IO.Path.GetFileNameWithoutExtension(p).IndexOf(containsToken, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                // Order by proximity to the context directory
                var startDir = Directory.Exists(contextAssetPath) ? contextAssetPath : Path.GetDirectoryName(EditorUtil.ToAbsolutePath(contextAssetPath));
                var ranked = files.Select(p => new { path = p, dist = EditorUtil.DirDistance(startDir, Path.GetDirectoryName(EditorUtil.ToAbsolutePath(p))) })
                                  .OrderBy(x => x.dist)
                                  .Select(x => x.path);

                foreach (var file in ranked)
                {
                    var name = TextureNameSolver.ExtractCharacterNameUsingMeta(meta, file);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
                return null;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("MaterialDetection.TryExtractCharacter", $"Character extraction failed: {ex.Message}");
                return null;
            }
        }

        public static bool TryDetectFromJsonFile(string absolutePath, out string gameKey, out string shaderPath, out Shader shader, out string reason)
        {
            gameKey = null; shaderPath = null; shader = null; reason = null;
            try
            {
                if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                {
                    reason = "File not found.";
                    return false;
                }
                var json = File.ReadAllText(absolutePath);
                return TryDetectFromJson(json, out gameKey, out shaderPath, out shader, out reason);
            }
            catch (Exception ex)
            {
                reason = $"Exception reading file: {ex.Message}";
                return false;
            }
        }

        public static bool TryDetectFromJson(string json, out string gameKey, out string shaderPath, out Shader shader, out string reason)
        {
            gameKey = null; shaderPath = null; shader = null; reason = null;

            if (string.IsNullOrWhiteSpace(json)) { reason = "Empty JSON"; return false; }

            // Parse JSON
            if (!Api.Parser.TryParse<MaterialJsonStructure>(json, out var materialData, out var parseError) || materialData == null)
            {
                reason = $"Parse failed: {parseError}";
                return false;
            }

            return TryDetectFromParsed(materialData, out gameKey, out shaderPath, out shader, out reason);
        }

        public static bool TryDetectFromParsed(MaterialJsonStructure materialData, out string gameKey, out string shaderPath, out Shader shader, out string reason)
        {
            gameKey = null; shaderPath = null; shader = null; reason = null;
            if (materialData == null)
            {
                reason = "Material data is null.";
                return false;
            }

            // Load metadata
            var metaMap = Api.GetGameMetadata();
            if (metaMap == null || metaMap.Count == 0)
            {
                reason = "No game metadata configured.";
                return false;
            }

            return TryDetectFromParsedWithMetadata(materialData, metaMap, out gameKey, out shaderPath, out shader, out reason);
        }

        private static bool TryDetectFromParsedWithMetadata(
            MaterialJsonStructure materialData,
            IReadOnlyDictionary<string, GameMetadata> metaMap,
            out string gameKey,
            out string shaderPath,
            out Shader shader,
            out string reason)
        {
            gameKey = null;
            shaderPath = null;
            shader = null;
            reason = null;

            // Collect property keys for keyword lookup
            var presentKeys = ExtractKeySet(materialData);

            // Score/select game: weight GameProperties heavily, then total keyword hits
            string bestGame = null; int bestScore = int.MinValue;
            foreach (var kv in metaMap)
            {
                var game = kv.Key; var meta = kv.Value ?? new GameMetadata();
                int gamePropHits = 0;
                if (meta.GameProperties != null)
                {
                    foreach (var gp in meta.GameProperties)
                        if (!string.IsNullOrEmpty(gp) && presentKeys.Contains(gp)) gamePropHits++;
                }

                int keywordHitsTotal = 0;
                if (meta.ShaderKeywords != null)
                {
                    foreach (var list in meta.ShaderKeywords.Values)
                    {
                        if (list == null) continue;
                        foreach (var kw in list)
                            if (!string.IsNullOrEmpty(kw) && KeywordMatches(materialData, presentKeys, kw)) keywordHitsTotal++;
                    }
                }

                // Heavily weight GameProperties; use keyword hits as secondary signal
                int score = (gamePropHits * 1000) + keywordHitsTotal;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestGame = game;
                }
            }

            // Reject if no positive evidence (score <= 0 means zero property + keyword hits)
            if (string.IsNullOrEmpty(bestGame) || bestScore <= 0)
            {
                reason = "No matching game metadata (no property / keyword hits).";
                return false;
            }

            // Select best shader by per-shader keyword hits
            string resolvedShader = null;
            var metaSel = metaMap[bestGame];
            int bestShaderHits = 0;
            if (metaSel.ShaderKeywords != null && metaSel.ShaderKeywords.Count > 0)
            {
                foreach (var kvp in metaSel.ShaderKeywords)
                {
                    var shaderKey = kvp.Key; var list = kvp.Value ?? new List<string>();
                    int hits = 0;
                    foreach (var kw in list)
                    {
                        if (MaterialKeywordUtil.IsArtifactToken(kw)) continue;
                        if (KeywordMatches(materialData, presentKeys, kw)) hits++;
                    }
                    if (hits > bestShaderHits)
                    {
                        bestShaderHits = hits;
                        resolvedShader = shaderKey;
                    }
                }
            }

            // Shader fallback: if we have no positive shader-keyword evidence, prefer metadata defaults.
            if (string.IsNullOrEmpty(resolvedShader))
                resolvedShader = string.IsNullOrEmpty(metaSel.DefaultShader)
                    ? metaSel.ShaderKeywords?.Keys?.FirstOrDefault()
                    : metaSel.DefaultShader;

            shaderPath = resolvedShader;
            if (string.IsNullOrEmpty(shaderPath))
            {
                reason = $"Game '{bestGame}' detected but shader path could not be resolved.";
                gameKey = bestGame;
                return false;
            }

            // Try to locate shader asset; non-fatal if missing.
            shader = Shader.Find(shaderPath);

            gameKey = bestGame;
            return true;
        }

        private static HashSet<string> ExtractKeySet(MaterialJsonStructure data)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);

            if (data == null) return set;

            if (data.m_SavedProperties != null)
            {
                void AddKeys<TKey, TValue>(IDictionary<TKey, TValue> dict)
                {
                    if (dict == null) return;
                    foreach (var k in dict.Keys)
                    {
                        var s = k?.ToString();
                        if (!string.IsNullOrEmpty(s)) set.Add(s);
                    }
                }
                AddKeys(data.m_SavedProperties.m_Floats);
                AddKeys(data.m_SavedProperties.m_Ints);
                AddKeys(data.m_SavedProperties.m_Colors);
                AddKeys(data.m_SavedProperties.m_TexEnvs);
            }

            MaterialKeywordUtil.CollectKeywords(data.m_ShaderKeywords, set);
            MaterialKeywordUtil.CollectKeywords(data.m_ValidKeywords, set);
            MaterialKeywordUtil.CollectKeywords(data.m_InvalidKeywords, set);

            if (data.m_StringTagMap != null)
            {
                foreach (var k in data.m_StringTagMap.Keys)
                {
                    if (!string.IsNullOrWhiteSpace(k)) set.Add(k);
                }
            }

            if (data.m_DisabledShaderPasses != null)
            {
                foreach (var pass in data.m_DisabledShaderPasses)
                {
                    if (!string.IsNullOrWhiteSpace(pass)) set.Add(pass.Trim());
                }
            }

            return set;
        }

        private enum KeywordOp
        {
            None,
            Eq,
            Ne,
            Gt,
            Lt,
            Ge,
            Le
        }

        private readonly struct KeywordCondition
        {
            public readonly string Property;
            public readonly KeywordOp Op;
            public readonly string RawValue;
            public readonly float? Number;
            public readonly bool? Bool;

            public KeywordCondition(string property, KeywordOp op)
            {
                Property = property;
                Op = op;
                RawValue = null;
                Number = null;
                Bool = null;
            }

            public KeywordCondition(string property, KeywordOp op, string rawValue, float? number, bool? boolean)
            {
                Property = property;
                Op = op;
                RawValue = rawValue;
                Number = number;
                Bool = boolean;
            }
        }

        private static bool KeywordMatches(MaterialJsonStructure data, HashSet<string> presentKeys, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var normalized = raw.Trim();
            if (MaterialKeywordUtil.IsArtifactToken(normalized)) return false;

            // Backward compatible behavior: plain property name means "property exists".
            if (!TryParseKeywordCondition(normalized, out var cond) || cond.Op == KeywordOp.None)
            {
                return presentKeys != null && presentKeys.Contains(normalized);
            }

            // Operator behavior: property must exist AND value must match.
            if (string.IsNullOrEmpty(cond.Property)) return false;
            if (presentKeys == null || !presentKeys.Contains(cond.Property)) return false;

            // String condition path (e.g., RenderType==Transparent)
            if (!cond.Bool.HasValue && !cond.Number.HasValue)
            {
                if (!TryGetStringPropertyValue(data, cond.Property, out var actualText)) return false;
                var expectedText = NormalizeConditionString(cond.RawValue);

                switch (cond.Op)
                {
                    case KeywordOp.Eq: return string.Equals(actualText, expectedText, StringComparison.OrdinalIgnoreCase);
                    case KeywordOp.Ne: return !string.Equals(actualText, expectedText, StringComparison.OrdinalIgnoreCase);
                    default: return false; // Relational operators are undefined for strings.
                }
            }

            // Numeric/bool condition path
            if (!TryGetNumericPropertyValue(data, cond.Property, out var actual)) return false;

            float expected = cond.Bool.HasValue
                ? (cond.Bool.Value ? 1f : 0f)
                : cond.Number.Value;

            switch (cond.Op)
            {
                case KeywordOp.Eq: return actual == expected;
                case KeywordOp.Ne: return actual != expected;
                case KeywordOp.Gt: return actual > expected;
                case KeywordOp.Lt: return actual < expected;
                case KeywordOp.Ge: return actual >= expected;
                case KeywordOp.Le: return actual <= expected;
                default: return false;
            }
        }

        // Longest-first operator scan to avoid splitting "==" as "=" etc.
        private static readonly (string token, KeywordOp op)[] s_Operators =
        {
            (">=", KeywordOp.Ge),
            ("<=", KeywordOp.Le),
            ("!=", KeywordOp.Ne),
            ("==", KeywordOp.Eq),
            (">", KeywordOp.Gt),
            ("<", KeywordOp.Lt),
            ("=", KeywordOp.Eq),
        };

        private static bool TryParseKeywordCondition(string raw, out KeywordCondition condition)
        {
            condition = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var s = raw.Trim();

            foreach (var (token, op) in s_Operators)
            {
                var idx = s.IndexOf(token, StringComparison.Ordinal);
                if (idx <= 0) continue; // property must have at least 1 char
                if (idx + token.Length >= s.Length) continue; // needs a value

                var left = s.Substring(0, idx).Trim();
                var right = s.Substring(idx + token.Length).Trim();
                if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) continue;

                // Parse boolean first
                if (bool.TryParse(right, out var b))
                {
                    condition = new KeywordCondition(left, op, right, null, b);
                    return true;
                }

                // Parse number (InvariantCulture)
                if (float.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    condition = new KeywordCondition(left, op, right, f, null);
                    return true;
                }

                // If value isn't numeric/bool, we still treat it as a condition (future string support)
                condition = new KeywordCondition(left, op, right, null, null);
                return true;
            }

            // No operator found => plain property name
            condition = new KeywordCondition(s, KeywordOp.None);
            return true;
        }

        private static bool TryGetNumericPropertyValue(MaterialJsonStructure data, string property, out float value)
        {
            value = 0f;
            if (data == null || string.IsNullOrEmpty(property)) return false;

            // Unity material JSON
            if (data.m_SavedProperties != null)
            {
                if (data.m_SavedProperties.m_Floats != null && data.m_SavedProperties.m_Floats.TryGetValue(property, out var f))
                {
                    value = f;
                    return true;
                }
                if (data.m_SavedProperties.m_Ints != null && data.m_SavedProperties.m_Ints.TryGetValue(property, out var i))
                {
                    value = i;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetStringPropertyValue(MaterialJsonStructure data, string property, out string value)
        {
            value = null;
            if (data == null || string.IsNullOrEmpty(property)) return false;

            if (data.m_StringTagMap != null && data.m_StringTagMap.TryGetValue(property, out var tagValue) && !string.IsNullOrWhiteSpace(tagValue))
            {
                value = tagValue.Trim();
                return true;
            }

            return false;
        }

        private static string NormalizeConditionString(string raw)
        {
            if (raw == null) return string.Empty;

            var s = raw.Trim();
            if (s.Length >= 2)
            {
                var first = s[0];
                var last = s[s.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                    s = s.Substring(1, s.Length - 2).Trim();
            }

            return s;
        }

        // --- Context helpers ---
        private static (string gameKey, string shaderPath, string sourceJson) DetectFromContextPathWithSource(string assetPath)
        {
            var results = DetectManyFromContextPath(assetPath);
            foreach (var r in results)
            {
                if (!string.IsNullOrEmpty(r.gameKey)) return r;
            }
            return (null, null, null);
        }

        private static readonly string[] VirtualAssetNames = { "unity_builtin_extra", "unity_default_resources" };

        private static bool TryFindWorkspaceRootAndMaterials(string startAssetPathOrFsPath, out string rootDir, out string materialsDir)
        {
            rootDir = null; materialsDir = null;

            // Skip virtual Unity asset paths that don't exist on disk
            string fileName = Path.GetFileName(startAssetPathOrFsPath);
            for (int i = 0; i < VirtualAssetNames.Length; i++)
            {
                if (string.Equals(fileName, VirtualAssetNames[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            try
            {
                string fullStart = EditorUtil.ToAbsolutePath(startAssetPathOrFsPath);
                string startPath = fullStart;
                if (File.Exists(fullStart)) startPath = Path.GetDirectoryName(fullStart);

                var di = new DirectoryInfo(startPath);
                // Track nearest Materials dir as a fallback if we never see a Textures sibling
                string fallbackMaterials = null;

                while (di != null)
                {
                    // Stop at Unity project root marker (Assets parent) to avoid leaving project
                    if (string.Equals(di.Name, "Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if Assets itself has a workspace (rare)
                        var matAtAssets = EditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Materials");
                        var texAtAssets = EditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Textures");
                        if (matAtAssets != null)
                        {
                            rootDir = di.FullName;
                            materialsDir = matAtAssets;
                            return true;
                        }
                        break;
                    }

                    // Check for Materials and Textures in current directory
                    var mat = EditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Materials");
                    var tex = EditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Textures");
                    if (!string.IsNullOrEmpty(mat))
                    {
                        if (string.IsNullOrEmpty(fallbackMaterials)) fallbackMaterials = mat;
                        if (!string.IsNullOrEmpty(tex))
                        {
                            rootDir = di.FullName;
                            materialsDir = mat;
                            return true;
                        }
                    }

                    di = di.Parent;
                }

                if (!string.IsNullOrEmpty(fallbackMaterials))
                {
                    rootDir = Directory.GetParent(fallbackMaterials)?.FullName ?? fallbackMaterials;
                    materialsDir = fallbackMaterials;
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Resilience: ignore path probing failures; no detection result will be returned.
                HoyoToonLogger.ThrottleWarning("MaterialDetection.WorkspaceRoot", $"Workspace root probing failed: {ex.Message}");
            }

            return false;
        }

        // Session-level de-duplication for ProblemList popups and batch decisions
        private static readonly HashSet<string> s_ShownProblemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> s_BatchDecisionCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private static string BuildProblemKey(string gameKey, string characterName)
            => string.IsNullOrEmpty(gameKey) || string.IsNullOrEmpty(characterName) ? null : (gameKey + "|" + characterName);

        private static void NotifyProblemIfNeeded(string gameKey, string characterName)
        {
            if (string.IsNullOrEmpty(gameKey) || string.IsNullOrEmpty(characterName)) return;
            var entry = FindProblemEntry(gameKey, characterName);
            if (entry == null || string.IsNullOrEmpty(entry.Message)) return;

            var key = BuildProblemKey(gameKey, characterName);
            if (!string.IsNullOrEmpty(key) && s_ShownProblemKeys.Contains(key)) return;

            var msgType = MessageType.Info;
            var t = entry.Type ?? "Info";
            if (t.Equals("Warning", StringComparison.OrdinalIgnoreCase)) msgType = MessageType.Warning;
            else if (t.Equals("Error", StringComparison.OrdinalIgnoreCase)) msgType = MessageType.Error;

            try
            {
                DialogWindow.ShowYesNoWithImageModal($"{gameKey}: {characterName}", entry.Message, msgType);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("MaterialDetection.ProblemDialog", $"Failed to show problem dialog for {gameKey}/{characterName}: {ex.Message}");
            }
            if (!string.IsNullOrEmpty(key)) s_ShownProblemKeys.Add(key);
        }

        // Helper: Find ProblemList entry by character name (case-insensitive)
        internal static ProblemEntry FindProblemEntry(string gameKey, string characterName)
        {
            try
            {
                var metaMap = Api.GetGameMetadata();
                if (metaMap == null) return null;
                if (!metaMap.TryGetValue(gameKey, out var meta) || meta?.ProblemList?.Entries == null) return null;
                return meta.ProblemList.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e?.Name) && string.Equals(e.Name, characterName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ThrottleWarning("MaterialDetection.FindProblemEntry", $"Problem entry lookup failed: {ex.Message}");
                return null;
            }
        }

        // Helper: Blocking prompt for batch flows using HoyoToon dialog; returns true to continue, false to stop
        internal static bool PromptBatchProceed(string gameKey, string characterName, ProblemEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Message)) return true; // nothing to warn about
            var key = BuildProblemKey(gameKey, characterName);
            if (!string.IsNullOrEmpty(key))
            {
                if (s_BatchDecisionCache.TryGetValue(key, out var cached)) return cached;
                // If a single popup for this problem was already shown earlier, default to continue without re-prompting
                if (s_ShownProblemKeys.Contains(key)) return true;
            }
            string title = $"{gameKey}: {characterName}";
            string body = entry.Message + "\n\nProceed with processing?";
            bool proceed = DialogWindow.ShowYesNoWithImageModal(title, body, MessageType.Warning);
            if (!string.IsNullOrEmpty(key))
            {
                s_BatchDecisionCache[key] = proceed;
                s_ShownProblemKeys.Add(key);
            }
            return proceed;
        }
    }
}
#endif
