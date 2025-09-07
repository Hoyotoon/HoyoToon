#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HoyoToon; // For MaterialJsonStructure
using HoyoToon.Utilities;
using HoyoToon.API;

namespace HoyoToon.API
{
    /// <summary>
    /// Standalone utility to detect the Game and Shader for a given material JSON
    /// using the configured API services and metadata.
    /// </summary>
    public static class MaterialDetection
    {
        /// <summary>
        /// Detect for multiple absolute file paths. Returns results per path.
        /// </summary>
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

        /// <summary>
        /// Detect for multiple raw JSON payloads. The index in the list is preserved in results.
        /// </summary>
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

        /// <summary>
        /// Try to detect from a file path.
        /// </summary>
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

        /// <summary>
        /// Try to detect from raw JSON.
        /// </summary>
        public static bool TryDetectFromJson(string json, out string gameKey, out string shaderPath, out Shader shader, out string reason)
        {
            gameKey = null; shaderPath = null; shader = null; reason = null;

            if (string.IsNullOrWhiteSpace(json)) { reason = "Empty JSON"; return false; }

            // Parse using high-performance parser
            if (!HoyoToonApi.Parser.TryParse<MaterialJsonStructure>(json, out var materialData, out var parseError) || materialData == null)
            {
                reason = $"Parse failed: {parseError}";
                return false;
            }

            // Load metadata from config
            var metaMap = HoyoToonApi.GetGameMetadata();
            if (metaMap == null || metaMap.Count == 0)
            {
                reason = "No game metadata configured.";
                return false;
            }

            // Build a set of property keys for efficient keyword lookup
            var presentKeys = ExtractKeySet(materialData);

            // Score and select game using GameProperties first, then total keyword hits
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

            // If no decisive match and only one game configured, pick it
            if (string.IsNullOrEmpty(bestGame) && metaMap.Count == 1)
            {
                bestGame = metaMap.First().Key;
            }

            if (string.IsNullOrEmpty(bestGame))
            {
                reason = "Could not determine game.";
                return false;
            }

            // Determine best shader for the selected game using per-shader keyword hits
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

            // Fallbacks: default shader, then any shader key
            if (string.IsNullOrEmpty(resolvedShader))
                resolvedShader = string.IsNullOrEmpty(metaSel.DefaultShader)
                    ? metaSel.ShaderKeywords?.Keys?.FirstOrDefault()
                    : metaSel.DefaultShader;

            shaderPath = ResolveShaderPath(null, resolvedShader, null);
            if (string.IsNullOrEmpty(shaderPath))
            {
                reason = $"Game '{bestGame}' detected but shader path could not be resolved.";
                gameKey = bestGame;
                return false;
            }

            // Attempt to locate shader in project, but do not fail detection if it's missing.
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

        private static string ResolveShaderPath(MaterialJsonStructure data, string candidateFromMeta, string shaderFromJson)
        {
            // We intentionally ignore the shader name embedded in the JSON now.
            if (!string.IsNullOrEmpty(candidateFromMeta))
                return candidateFromMeta;

            return null;
        }

        private static bool LooksLikeShaderPath(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            // Typical paths like "HoyoToon/HSR/Character" or similar
            return shaderName.Contains("/");
        }

        [MenuItem("Assets/HoyoToon/Detect Game & Shader", false, 2000)]
        private static void Menu_DetectForSelectedJson()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                HoyoToonDialogWindow.ShowInfo("HoyoToon Detection", "Select one or more JSON assets.");
                return;
            }

            int ok = 0, fail = 0;
            var report = new StringBuilder();
            report.AppendLine("# HoyoToon Detection Report\n");
            report.AppendLine($"Scanned {selected.Length} selected item(s).\n");
            report.AppendLine("---\n");
            foreach (var obj in selected)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                var abs = Path.GetFullPath(path);
                if (TryDetectFromJsonFile(abs, out var gameKey, out var shaderPath, out var shader, out var reason))
                {
                    ok++;
                    var shaderState = shader == null ? "Not Found (not installed)" : "Found";
                    HoyoToonLogger.APIInfo($"Detected: Game='{gameKey}', Shader='{shaderPath}' ({shaderState})");

                    report.AppendLine($"- **{Path.GetFileName(path)}**");
                    report.AppendLine($"  - Path: `{path}`");
                    report.AppendLine($"  - Game: {gameKey}");
                    report.AppendLine($"  - Shader: `{shaderPath}`");
                    report.AppendLine($"  - Shader asset: {shaderState}\n");
                }
                else
                {
                    fail++;
                    HoyoToonLogger.APIWarning($"Detection failed for {obj.name}: {reason}");
                    report.AppendLine($"- **{Path.GetFileName(path)}**");
                    report.AppendLine($"  - Path: `{path}`");
                    report.AppendLine($"  - Status: Failed");
                    report.AppendLine($"  - Reason: {reason}\n");
                }
            }

            report.AppendLine("---\n");
            report.AppendLine($"**Summary**: Success: {ok}, Failed: {fail}");
            HoyoToonDialogWindow.ShowInfo("HoyoToon Detection", report.ToString());
        }
    }
}
#endif
