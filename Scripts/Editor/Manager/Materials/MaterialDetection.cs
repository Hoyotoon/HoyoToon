#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.Utilities;
using HoyoToon.API;
using HoyoToon.Textures;

namespace HoyoToon.Materials
{
        /// <summary>
        /// Utilities for detecting Game and Shader from material JSON (direct or via context).
        /// </summary>
        public static class MaterialDetection
        {

        /// <summary>
        /// Detect Game/Shader from a JSON string, a JSON path, or by scanning context; also returns the source JSON path.
        /// When both parameters are provided, 'pathOrJson' takes precedence.
        /// </summary>
        public static (string gameKey, string shaderPath, string sourceJson) DetectGameAndShaderAutoWithSource(UnityEngine.Object assetOrNull = null, string pathOrJson = null)
        {
            // Prefer explicit string input
            if (!string.IsNullOrWhiteSpace(pathOrJson))
            {
                if (HoyoToonEditorUtil.LooksLikeJson(pathOrJson))
                {
                    if (TryDetectFromJson(pathOrJson, out var g, out var s, out _, out _))
                    {
                        HoyoToonLogger.APIInfo($"Detection (raw JSON): Game='{g}', Shader='{s}'");
                        return (g, s, "<raw-json>");
                    }
                    return (null, null, null);
                }

                var p = pathOrJson;
                if (p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var abs = HoyoToonEditorUtil.ToAbsolutePath(p);
                    if (TryDetectFromJsonFile(abs, out var g, out var s, out _, out _))
                    {
                        var charName = TryExtractCharacterName(g, abs);
                        if (!string.IsNullOrEmpty(charName))
                        {
                            HoyoToonLogger.APIInfo($"Detection (file): Game='{g}', Shader='{s}', JSON='{abs}', Character='{charName}'");
                            var entry = FindProblemEntry(g, charName);
                            if (entry != null && !string.IsNullOrEmpty(entry.Message))
                            {
                                var msgType = MessageType.Info;
                                var t = entry.Type ?? "Info";
                                if (t.Equals("Warning", StringComparison.OrdinalIgnoreCase)) msgType = MessageType.Warning;
                                else if (t.Equals("Error", StringComparison.OrdinalIgnoreCase)) msgType = MessageType.Error;
                                // De-duplicate: show once per (game|character) per session
                                var key = BuildProblemKey(g, charName);
                                if (!string.IsNullOrEmpty(key) && s_ShownProblemKeys.Contains(key))
                                {
                                    // already shown this session; skip
                                }
                                else
                                {
                                    try { HoyoToonDialogWindow.ShowYesNoWithImageModal($"{g}: {charName}", entry.Message, msgType); } catch { }
                                    if (!string.IsNullOrEmpty(key)) s_ShownProblemKeys.Add(key);
                                }
                            }
                        }
                        else
                        {
                            HoyoToonLogger.APIInfo($"Detection (file): Game='{g}', Shader='{s}', JSON='{abs}'");
                        }
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
                    // Attempt character extraction and include in log if present
                    var charName = TryExtractCharacterName(result.gameKey, assetPath);
                    if (!string.IsNullOrEmpty(charName))
                    {
                        HoyoToonLogger.APIInfo($"Detection (context): Game='{result.gameKey}', Shader='{result.shaderPath}', JSON='{result.sourceJson}', Character='{charName}'");
                        // Show configured ProblemList message with mapped severity directly via HoyoToonDialogWindow
                        var entry = FindProblemEntry(result.gameKey, charName);
                        if (entry != null && !string.IsNullOrEmpty(entry.Message))
                        {
                            var msgType = MessageType.Info;
                            var t = entry.Type ?? "Info";
                            if (t.Equals("Warning", StringComparison.OrdinalIgnoreCase)) msgType = MessageType.Warning;
                            else if (t.Equals("Error", StringComparison.OrdinalIgnoreCase)) msgType = MessageType.Error;
                            try
                            {
                                HoyoToonDialogWindow.ShowYesNoWithImageModal($"{result.gameKey}: {charName}", entry.Message, msgType);
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        HoyoToonLogger.APIInfo($"Detection (context): Game='{result.gameKey}', Shader='{result.shaderPath}', JSON='{result.sourceJson}'");
                    }
                }
                return result;
            }

            return (null, null, null);
        }

        /// <summary>
        /// Detect Game/Shader for potentially multiple JSONs discovered from the given context.
        /// - If a JSON string is provided, returns a single result.
        /// - If a JSON file path is provided, returns a single result.
        /// - If a folder/asset path or Unity object is provided, scans nearby Materials for ALL JSON files and returns per-JSON detections.
        /// </summary>
        public static IReadOnlyList<(string gameKey, string shaderPath, string sourceJson)> DetectGameAndShaderAutoWithSourceMany(UnityEngine.Object assetOrNull = null, string pathOrJson = null)
        {
            var results = new List<(string gameKey, string shaderPath, string sourceJson)>();

            // Prefer explicit string input
            if (!string.IsNullOrWhiteSpace(pathOrJson))
            {
                if (HoyoToonEditorUtil.LooksLikeJson(pathOrJson))
                {
                    if (TryDetectFromJson(pathOrJson, out var g, out var s, out _, out _))
                    {
                        results.Add((g, s, "<raw-json>"));
                        HoyoToonLogger.APIInfo($"DetectionMany (raw JSON): Game='{g}', Shader='{s}'");
                    }
                    return results;
                }

                var p = pathOrJson;
                if (p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var abs = HoyoToonEditorUtil.ToAbsolutePath(p);
                    if (TryDetectFromJsonFile(abs, out var g, out var s, out _, out _))
                    {
                        results.Add((g, s, abs));
                        HoyoToonLogger.APIInfo($"DetectionMany (file): Game='{g}', Shader='{s}', JSON='{abs}'");
                    }
                    else
                    {
                        results.Add((null, null, abs));
                        HoyoToonLogger.APIWarning($"DetectionMany (file): Unsupported JSON '{abs}'");
                    }
                    return results;
                }

                // Context scan for many
                return DetectManyFromContextPath(p);
            }

            if (assetOrNull != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(assetOrNull);

                // Preflight prompt: try detect one character and show a single prompt to continue/stop
                var (preGame, _) = DetectGameAutoOnly(null, assetPath);
                if (!string.IsNullOrEmpty(preGame))
                {
                    var preChar = TryExtractCharacterName(preGame, assetPath);
                    var preEntry = FindProblemEntry(preGame, preChar);
                    if (!string.IsNullOrEmpty(preChar) && preEntry != null)
                    {
                        bool proceed = PromptBatchProceed(preGame, preChar, preEntry);
                        if (!proceed)
                        {
                            HoyoToonLogger.APIInfo($"Batch detection cancelled by user at preflight: Game='{preGame}', Character='{preChar}'");
                            return new List<(string, string, string)>();
                        }
                    }
                }

                var list = DetectManyFromContextPath(assetPath) ?? new List<(string, string, string)>();
                foreach (var (g, s, src) in list)
                {
                    if (!string.IsNullOrEmpty(g))
                    {
                        var charName = TryExtractCharacterName(g, assetPath);
                        if (!string.IsNullOrEmpty(charName))
                            HoyoToonLogger.APIInfo($"DetectionMany (context): Game='{g}', Shader='{s}', JSON='{src}', Character='{charName}'");
                        else
                            HoyoToonLogger.APIInfo($"DetectionMany (context): Game='{g}', Shader='{s}', JSON='{src}'");
                    }
                }
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
                var abs = HoyoToonEditorUtil.ToAbsolutePath(assetPath);
                if (TryDetectFromJsonFile(abs, out var g, out var s, out _, out _)) list.Add((g, s, abs));
                else list.Add((null, null, abs));
                return list;
            }

            if (!TryFindWorkspaceRootAndMaterials(assetPath, out var rootDir, out var materialsDir))
                return list;

            try
            {
                var startDir = Directory.Exists(assetPath) ? assetPath : Path.GetDirectoryName(HoyoToonEditorUtil.ToAbsolutePath(assetPath));
                var jsons = Directory.EnumerateFiles(materialsDir, "*.json", SearchOption.AllDirectories)
                    .Select(p => new { path = p, dist = HoyoToonEditorUtil.DirDistance(startDir, Path.GetDirectoryName(HoyoToonEditorUtil.ToAbsolutePath(p))) })
                    .OrderBy(x => x.dist)
                    .Select(x => x.path);

                foreach (var jsonPath in jsons)
                {
                    var abs = HoyoToonEditorUtil.ToAbsolutePath(jsonPath);
                    if (TryDetectFromJsonFile(abs, out var game, out var shaderPath, out _, out _))
                        list.Add((game, shaderPath, abs));
                    else
                        list.Add((null, null, abs));
                }
            }
            catch (Exception)
            {
                // Intentionally ignore UI exceptions to avoid breaking detection flows.
            }

            return list;
        }
        /// <summary>
        /// Detect Game/Shader (no provenance). Wrapper over DetectGameAndShaderAutoWithSource.
        /// </summary>
        public static (string gameKey, string shaderPath) DetectGameAndShaderAuto(UnityEngine.Object assetOrNull = null, string pathOrJson = null)
        {
            var (g, s, _) = DetectGameAndShaderAutoWithSource(assetOrNull, pathOrJson);
            return (g, s);
        }

        /// <summary>
        /// Detect only the Game and return the source JSON path (recommended for context scans).
        /// </summary>
        public static (string gameKey, string sourceJson) DetectGameAutoOnly(UnityEngine.Object assetOrNull = null, string pathOrJson = null)
        {
            var (g, _, src) = DetectGameAndShaderAutoWithSource(assetOrNull, pathOrJson);
            return (g, src);
        }

        /// <summary>
        /// Try to extract a character name from nearby texture files for the given game and context path
        /// using the game's ProblemList (Texture+Regex). Returns null if not found.
        /// </summary>
        public static string TryExtractCharacterName(string gameKey, string contextAssetPath)
        {
            try
            {
                if (string.IsNullOrEmpty(gameKey) || string.IsNullOrEmpty(contextAssetPath)) return null;
                var metaMap = HoyoToonApi.GetGameMetadata();
                if (metaMap == null || !metaMap.TryGetValue(gameKey, out var meta) || meta == null) return null;
                if (meta.ProblemList == null) return null;

                // Find the Textures directory near the context and scan for a representative texture filename
                if (!TryFindWorkspaceRootAndMaterials(contextAssetPath, out var rootDir, out var materialsDir)) return null;
                var root = rootDir;
                if (string.IsNullOrEmpty(root)) root = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(contextAssetPath));
                var texturesDir = HoyoToonEditorUtil.FindChildDirectoryIgnoreCase(root, "Textures");
                if (string.IsNullOrEmpty(texturesDir))
                {
                    // Probe upwards for any Textures
                    var di = new DirectoryInfo(root);
                    while (di != null && string.IsNullOrEmpty(texturesDir))
                    {
                        texturesDir = HoyoToonEditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Textures");
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
                var startDir = Directory.Exists(contextAssetPath) ? contextAssetPath : Path.GetDirectoryName(HoyoToonEditorUtil.ToAbsolutePath(contextAssetPath));
                var ranked = files.Select(p => new { path = p, dist = HoyoToonEditorUtil.DirDistance(startDir, Path.GetDirectoryName(HoyoToonEditorUtil.ToAbsolutePath(p))) })
                                  .OrderBy(x => x.dist)
                                  .Select(x => x.path);

                foreach (var file in ranked)
                {
                    var name = TextureNameSolver.ExtractCharacterNameUsingMeta(meta, file);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
                return null;
            }
            catch { return null; }
        }

        // Streamlined: High-level auto methods above; low-level Try* remain for explicit control.

        /// <summary>Detect for multiple JSON files (absolute paths).</summary>
        public static IReadOnlyList<(string path, bool ok, string gameKey, string shaderPath, Shader shader, string reason)> DetectManyFromFiles(IEnumerable<string> absolutePaths)
        {
            var results = new List<(string, bool, string, string, Shader, string)>();
            foreach (var p in absolutePaths ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                if (TryDetectFromJsonFile(p, out var game, out var shaderPath, out var shader, out var reason))
                    results.Add((p, true, game, shaderPath, shader, null));
                else
                    results.Add((p, false, null, null, null, reason));
            }
            return results;
        }

        /// <summary>Detect for multiple raw JSON payloads (indexed results).</summary>
        public static IReadOnlyList<(int index, bool ok, string gameKey, string shaderPath, Shader shader, string reason)> DetectManyFromJsons(IEnumerable<string> jsonPayloads)
        {
            var list = jsonPayloads?.ToList() ?? new List<string>();
            var results = new List<(int, bool, string, string, Shader, string)>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var json = list[i];
                if (TryDetectFromJson(json, out var game, out var shaderPath, out var shader, out var reason))
                    results.Add((i, true, game, shaderPath, shader, null));
                else
                    results.Add((i, false, null, null, null, reason));
            }
            return results;
        }

        /// <summary>Try detect from a JSON file (absolute path).</summary>
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

        /// <summary>Try detect from raw JSON.</summary>
        public static bool TryDetectFromJson(string json, out string gameKey, out string shaderPath, out Shader shader, out string reason)
        {
            gameKey = null; shaderPath = null; shader = null; reason = null;

            if (string.IsNullOrWhiteSpace(json)) { reason = "Empty JSON"; return false; }

            // Parse JSON
            if (!HoyoToonApi.Parser.TryParse<MaterialJsonStructure>(json, out var materialData, out var parseError) || materialData == null)
            {
                reason = $"Parse failed: {parseError}";
                return false;
            }

            // Load metadata
            var metaMap = HoyoToonApi.GetGameMetadata();
            if (metaMap == null || metaMap.Count == 0)
            {
                reason = "No game metadata configured.";
                return false;
            }

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
                            if (!string.IsNullOrEmpty(kw) && presentKeys.Contains(kw)) keywordHitsTotal++;
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
            int bestShaderHits = -1;
            if (metaSel.ShaderKeywords != null && metaSel.ShaderKeywords.Count > 0)
            {
                foreach (var kvp in metaSel.ShaderKeywords)
                {
                    var shaderKey = kvp.Key; var list = kvp.Value ?? new List<string>();
                    int hits = 0;
                    foreach (var kw in list) if (!string.IsNullOrEmpty(kw) && presentKeys.Contains(kw)) hits++;
                    if (hits > bestShaderHits)
                    {
                        bestShaderHits = hits;
                        resolvedShader = shaderKey;
                    }
                }
            }

            // Shader fallback: default shader, else any shader key
            if (string.IsNullOrEmpty(resolvedShader))
                resolvedShader = string.IsNullOrEmpty(metaSel.DefaultShader)
                    ? metaSel.ShaderKeywords?.Keys?.FirstOrDefault()
                    : metaSel.DefaultShader;

            shaderPath = ResolveShaderPath(resolvedShader);
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

            if (data.IsUnityFormat && data.m_SavedProperties != null)
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

            if (data.IsUnrealFormat && data.Parameters != null)
            {
                void AddKeys(IDictionary<string, object> dict)
                {
                    if (dict == null) return;
                    foreach (var k in dict.Keys) if (!string.IsNullOrEmpty(k)) set.Add(k);
                }
                void AddKeysGeneric<T>(IDictionary<string, T> dict)
                {
                    if (dict == null) return;
                    foreach (var k in dict.Keys) if (!string.IsNullOrEmpty(k)) set.Add(k);
                }
                AddKeysGeneric(data.Parameters.Colors);
                AddKeysGeneric(data.Parameters.Scalars);
                AddKeysGeneric(data.Parameters.Switches);
                AddKeys(data.Parameters.Properties);
                if (data.Textures != null)
                {
                    foreach (var k in data.Textures.Keys) if (!string.IsNullOrEmpty(k)) set.Add(k);
                }
            }

            return set;
        }

        private static string ResolveShaderPath(string candidateFromMeta)
        {
            // We intentionally ignore the shader name embedded in the JSON now.
            if (!string.IsNullOrEmpty(candidateFromMeta))
                return candidateFromMeta;

            return null;
        }

        // --- Context helpers ---
        private static (string gameKey, string shaderPath, string sourceJson) DetectFromContextPathWithSource(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return (null, null, null);

            // Direct JSON file path
            if (assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var abs = HoyoToonEditorUtil.ToAbsolutePath(assetPath);
                if (TryDetectFromJsonFile(abs, out var g, out var s, out _, out _)) return (g, s, abs);
                return (null, null, null);
            }

            if (!TryFindWorkspaceRootAndMaterials(assetPath, out var rootDir, out var materialsDir))
                return (null, null, null);

            try
            {
                var startDir = Directory.Exists(assetPath) ? assetPath : Path.GetDirectoryName(HoyoToonEditorUtil.ToAbsolutePath(assetPath));
                var jsons = Directory.EnumerateFiles(materialsDir, "*.json", SearchOption.AllDirectories)
                    .Select(p => new { path = p, dist = HoyoToonEditorUtil.DirDistance(startDir, Path.GetDirectoryName(HoyoToonEditorUtil.ToAbsolutePath(p))) })
                    .OrderBy(x => x.dist)
                    .Select(x => x.path);

                foreach (var jsonPath in jsons)
                {
                    var abs = HoyoToonEditorUtil.ToAbsolutePath(jsonPath);
                    if (TryDetectFromJsonFile(abs, out var game, out var shaderPath, out _, out _))
                        return (game, shaderPath, abs);
                }
            }
            catch (Exception)
            {
                // Resilience: ignore filesystem/probing errors during context scan.
            }

            return (null, null, null);
        }

        private static bool TryFindWorkspaceRootAndMaterials(string startAssetPathOrFsPath, out string rootDir, out string materialsDir)
        {
            rootDir = null; materialsDir = null;

            try
            {
                string fullStart = HoyoToonEditorUtil.ToAbsolutePath(startAssetPathOrFsPath);
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
                        var matAtAssets = HoyoToonEditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Materials");
                        var texAtAssets = HoyoToonEditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Textures");
                        if (matAtAssets != null)
                        {
                            rootDir = di.FullName;
                            materialsDir = matAtAssets;
                            return true;
                        }
                        break;
                    }

                    // Check for Materials and Textures in current directory
                    var mat = HoyoToonEditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Materials");
                    var tex = HoyoToonEditorUtil.FindChildDirectoryIgnoreCase(di.FullName, "Textures");
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
            catch (Exception)
            {
                // Resilience: ignore path probing failures; no detection result will be returned.
            }

            return false;
        }

        // Session-level de-duplication for ProblemList popups and batch decisions
        private static readonly HashSet<string> s_ShownProblemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> s_BatchDecisionCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private static string BuildProblemKey(string gameKey, string characterName)
            => string.IsNullOrEmpty(gameKey) || string.IsNullOrEmpty(characterName) ? null : (gameKey + "|" + characterName);

        // Helper: Find ProblemList entry by character name (case-insensitive)
        private static ProblemEntry FindProblemEntry(string gameKey, string characterName)
        {
            try
            {
                var metaMap = HoyoToonApi.GetGameMetadata();
                if (metaMap == null) return null;
                if (!metaMap.TryGetValue(gameKey, out var meta) || meta?.ProblemList?.Entries == null) return null;
                return meta.ProblemList.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e?.Name) && string.Equals(e.Name, characterName, StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }

        // Helper: Blocking prompt for batch flows using HoyoToon dialog; returns true to continue, false to stop
        private static bool PromptBatchProceed(string gameKey, string characterName, ProblemEntry entry)
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
            bool proceed = HoyoToonDialogWindow.ShowYesNoWithImageModal(title, body, MessageType.Warning);
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
