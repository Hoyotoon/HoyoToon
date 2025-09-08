#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;
using HoyoToon.API;

namespace HoyoToon.Debugging
{
    internal static class MaterialDetectionDebugMenu
    {
        [MenuItem("Assets/HoyoToon/Detect Game & Shader", false, 2000)]
        private static void Menu_DetectForSelection()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                HoyoToonDialogWindow.ShowInfo("HoyoToon Detection", "Select one or more items (JSON, folders, or assets). We'll try to detect from context.");
                return;
            }

            int ok = 0, fail = 0, skipped = 0;
            var report = new StringBuilder();
            report.AppendLine("# HoyoToon Detection Report\n");
            report.AppendLine($"Scanned {selected.Length} selected item(s).\n");
            report.AppendLine("---\n");
            foreach (var obj in selected)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) { skipped++; continue; }

                // If JSON file(s), keep per-JSON reporting using TryDetectFromJsonFile
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var abs = Path.GetFullPath(path);
                    if (MaterialDetection.TryDetectFromJsonFile(abs, out var gameKey, out var shaderPath, out var shader, out var reason))
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
                    continue;
                }

                // For folders or non-JSON assets, detect only the Game (shader may be unreliable from arbitrary JSON)
                var (game, sourceJson) = MaterialDetection.DetectGameAutoOnly(obj, null);
                if (!string.IsNullOrEmpty(game))
                {
                    ok++;
                    HoyoToonLogger.APIInfo($"Detected (context): Game='{game}'");

                    report.AppendLine($"- **{obj.name}**");
                    // Show the JSON file used for detection if available
                    if (!string.IsNullOrEmpty(sourceJson))
                        report.AppendLine($"  - JSON: `{MakeProjectPathRelative(sourceJson)}`");
                    else
                        report.AppendLine($"  - Path: `{path}`");
                    report.AppendLine($"  - Game: {game}");
                    // Shader intentionally omitted for context runs\n
                    report.AppendLine("");
                }
                else
                {
                    fail++;
                    HoyoToonLogger.APIWarning($"Detection failed for {obj.name} (context)");
                    report.AppendLine($"- **{obj.name}**");
                    report.AppendLine($"  - Path: `{path}`");
                    report.AppendLine($"  - Status: Failed");
                    report.AppendLine($"  - Reason: Not found via context scan\n");
                }
            }

            report.AppendLine("---\n");
            report.AppendLine($"**Summary**: Success: {ok}, Failed: {fail}, Skipped: {skipped}");
            HoyoToonDialogWindow.ShowInfo("HoyoToon Detection", report.ToString());
        }

        [MenuItem("Assets/HoyoToon/Detect Game & Shader (All JSONs)", false, 2001)]
        private static void Menu_DetectForSelection_AllJsons()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                HoyoToonDialogWindow.ShowInfo("HoyoToon Detection (All)", "Select one or more items (folders or assets). We'll scan all nearby Materials JSONs.");
                return;
            }

            int ok = 0, total = 0;
            var report = new StringBuilder();
            report.AppendLine("# HoyoToon Detection Report (All JSONs)\n");
            foreach (var obj in selected)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                var results = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(obj, null);
                if (results == null || results.Count == 0)
                {
                    report.AppendLine($"- **{obj.name}**: No JSONs detected in context\n");
                    continue;
                }

                report.AppendLine($"## {obj.name}\n");
                foreach (var (game, shaderPath, src) in results)
                {
                    total++;
                    if (!string.IsNullOrEmpty(game)) ok++;
                    var rel = MakeProjectPathRelative(src);
                    report.AppendLine($"- JSON: `{rel}`\n  - Game: {game}\n  - Shader: `{shaderPath}`\n");
                }
            }
            report.AppendLine($"\n---\nProcessed {total} JSON(s), detected: {ok}.");
            HoyoToonDialogWindow.ShowInfo("HoyoToon Detection (All)", report.ToString());
        }

        // Helper: convert absolute path under project to Assets-relative for nicer display
        private static string MakeProjectPathRelative(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return fullPath;
            try
            {
                // Unity project root is the parent of Assets; AssetDatabase only uses Assets-relative
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                var normalized = Path.GetFullPath(fullPath);
                if (!string.IsNullOrEmpty(projectRoot) && normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var rel = normalized.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return rel.Replace('\\', '/');
                }
            }
            catch { }
            return fullPath;
        }
    }
}
#endif
