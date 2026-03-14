#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.API;
using HoyoToon.Editor.AssetPipeline.Materials;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.Prerequisites;

namespace HoyoToon.Editor.AssetPipeline.Models
{
    public static class ModelConverter
    {
        private const string ToolRelativePath = "Packages/com.hoyotoon.hoyotoon/Plugins/Hoyo2VRC.exe";
        private const string DefaultConverterKey = "Hoyo2Unity";
        private const string VrcConverterKey = "Hoyo2VRC";
        private const bool EnableVerbose = true;
        private const int DefaultFaceBakeFps = 30;
        private const int DefaultFaceBakeMaxShapes = 100;

        private static void ForceReimport(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return;

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport
            );
        }

        public static void Process(UnityEngine.Object fbxAsset)
        {
            ProcessInternal(fbxAsset, out _);
        }

        public static bool TryProcessAndGetOutput(UnityEngine.Object fbxAsset, out string outputAssetPath, out GameObject outputAsset)
        {
            outputAssetPath = ProcessInternal(fbxAsset, out var inputAssetPath);
            if (string.IsNullOrWhiteSpace(outputAssetPath))
            {
                outputAssetPath = inputAssetPath;
            }

            outputAsset = string.IsNullOrWhiteSpace(outputAssetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(outputAssetPath);

            return outputAsset != null;
        }

        private static string ProcessInternal(UnityEngine.Object fbxAsset, out string inputAssetPath)
        {
            inputAssetPath = null;

            if (fbxAsset == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Error, "No FBX selected.");
                return null;
            }

            inputAssetPath = AssetDatabase.GetAssetPath(fbxAsset);
            if (string.IsNullOrWhiteSpace(inputAssetPath))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Error, "Selected object is not a valid asset.");
                return null;
            }

            if (!inputAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Error, "Selected asset is not an FBX file.");
                return null;
            }

            string exePath = GetToolPath();
            if (!File.Exists(exePath))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Error, $"FBX Tool EXE not found at '{exePath}'.");
                return null;
            }

            string fullPath = Path.GetFullPath(inputAssetPath);

            HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Info, $"Starting FBX conversion for '{inputAssetPath}'.");
            var detection = DetectGameAndCharacter(fbxAsset, inputAssetPath);
            string profileSource;
            var converterProfile = GetConverterProfile(detection.gameKey, out profileSource);
            LogConverterProfile(converterProfile, profileSource, detection.gameKey);

            if (!string.IsNullOrEmpty(detection.gameKey))
            {
                string characterLabel = string.IsNullOrEmpty(detection.character) ? "Unknown" : detection.character;
                HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Info, $"Detection summary: Game='{detection.gameKey}', Character='{characterLabel}'.");
            }

            string outputPath;
            int exitCode = RunConverterExe(exePath, fullPath, detection, converterProfile, out outputPath);
            if (exitCode != 0)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Error, $"FBX converter exited with code {exitCode}.");
            }
            else
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Info, "FBX converter completed successfully.");
            }

            string outputAssetPath = EditorUtil.ToUnityAssetPath(outputPath);
            if (string.IsNullOrWhiteSpace(outputAssetPath))
            {
                outputAssetPath = FindNewestOutputAssetPath(fullPath);
            }

            ForceReimport(string.IsNullOrWhiteSpace(outputAssetPath) ? inputAssetPath : outputAssetPath);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Info, "Reimport complete.");

            if (exitCode != 0)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(outputAssetPath) ? inputAssetPath : outputAssetPath;
        }

        private static (string gameKey, string character, string shaderPath, string sourceJson) DetectGameAndCharacter(UnityEngine.Object fbxAsset, string assetPath)
        {
            var (gameKey, shaderPath, sourceJson) =
                MaterialDetection.DetectGameAndShaderAutoWithSource(fbxAsset, assetPath);

            if (string.IsNullOrEmpty(gameKey))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Warning, "FBX detection: Game not detected.");
                return (null, null, null, null);
            }

            string character = MaterialDetection.TryExtractCharacterName(gameKey, assetPath);
            string characterLabel = string.IsNullOrEmpty(character) ? "Unknown" : character;

            HoyoToonLogger.Log(HoyoToonLogger.Categories.API, LogLevel.Info, 
                $"FBX detection: Game='{gameKey}', Character='{characterLabel}', Shader='{shaderPath}', JSON='{sourceJson}'"
            );

            return (gameKey, character, shaderPath, sourceJson);
        }

        private static int RunConverterExe(string exePath, string fullPath, (string gameKey, string character, string shaderPath, string sourceJson) detection, ConverterProfile profile, out string outputPath)
        {
            outputPath = null;
            DateTime startUtc = DateTime.UtcNow;
            var toolName = Path.GetFileNameWithoutExtension(exePath);
            var args = BuildArgumentList(string.IsNullOrEmpty(toolName) ? "HoyoFBXTool" : toolName, fullPath, detection, profile);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = string.Join(" ", QuoteArgs(args.GetRange(1, args.Count - 1))),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var p = System.Diagnostics.Process.Start(startInfo))
            {
                var stdout = new List<string>();
                var stderr = new List<string>();

                p.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) stdout.Add(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) stderr.Add(e.Data); };

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();

                if (stdout.Count > 0)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Info, "Converter stdout:");
                    foreach (var line in stdout)
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Info, line);
                }

                if (stderr.Count > 0)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Warning, "Converter stderr:");
                    foreach (var line in stderr)
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Warning, line);
                }

                outputPath = TryExtractOutputPath(stdout);
                if (string.IsNullOrWhiteSpace(outputPath))
                    outputPath = FindNewestOutputPath(fullPath, startUtc);

                return p.ExitCode;
            }
        }

        private static string GetToolPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, ToolRelativePath));
        }
        private static ConverterProfile GetConverterProfile(string gameKey, out string source)
        {
            string key = VRCSDKInstalledCheck.IsVRC ? VrcConverterKey : DefaultConverterKey;
            ConverterProfile profile = null;
            source = null;

            if (!string.IsNullOrWhiteSpace(gameKey))
            {
                var metadata = Api.GetGameMetadata();
                if (metadata != null && metadata.TryGetValue(gameKey, out var game))
                {
                    profile = key == VrcConverterKey ? game.Hoyo2VRC : game.Hoyo2Unity;
                    if (profile != null && string.IsNullOrWhiteSpace(profile.Key))
                        profile.Key = key;
                    if (profile != null)
                        source = $"Game '{gameKey}'";
                }
            }

            if (profile == null)
            {
                profile = Api.GetConverterProfile(key);
                if (profile != null)
                    source = "API";
            }

            if (profile == null)
            {
                string fallbackSource = string.IsNullOrWhiteSpace(gameKey) ? "API" : $"Game '{gameKey}'";
                HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Warning, $"Converter profile '{key}' not found in {fallbackSource}. Using defaults.");
                profile = new ConverterProfile { Key = key };
                profile.Features.Default = "all";
                profile.Disable.Default = string.Empty;
                source = "Defaults";
            }
            return profile;
        }

        private static void LogConverterProfile(ConverterProfile profile, string source, string gameKey)
        {
            string features = NormalizeCsv(GetDefaultCsv(profile?.Features));
            string disable = NormalizeCsv(GetDefaultCsv(profile?.Disable));
            string removeMeshes = NormalizeCsv(profile?.RemoveMeshes?.List);
            string removeBones = NormalizeCsv(profile?.RemoveBones?.List);
            string renameBones = NormalizeCsv(profile?.RenameBones?.Mapping);
            string resolvedSource = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;

            HoyoToonLogger.Log(HoyoToonLogger.Categories.FBXConverter, LogLevel.Info, 
                $"Converter profile: '{profile?.Key ?? "<none>"}' ({resolvedSource}) | Features: {features ?? "<none>"} | Disable: {disable ?? "<none>"} | RemoveMeshes: {removeMeshes ?? "<none>"} | RemoveBones: {removeBones ?? "<none>"} | RenameBones: {renameBones ?? "<none>"}"
            );
        }

        private static List<string> BuildArgumentList(string toolName, string fullPath, (string gameKey, string character, string shaderPath, string sourceJson) detection, ConverterProfile profile)
        {
            var args = new List<string> { toolName, fullPath, fullPath };

            string featuresCsv = BuildFeaturesCsv(profile);
            string disableCsv = NormalizeCsv(GetDefaultCsv(profile?.Disable));
            string removeMeshes = NormalizeCsv(profile?.RemoveMeshes?.List);
            string removeBones = NormalizeCsv(profile?.RemoveBones?.List);
            string renameBones = NormalizeCsv(profile?.RenameBones?.Mapping);

            var featureSet = ParseFeatureSet(featuresCsv);
            var disableSet = ParseFeatureSet(disableCsv);

            bool enableBindPoseFix = ResolveFeatureEnabled(featureSet, disableSet, "bindpose", defaultEnabled: true);
            bool enableRemoveEmpties = ResolveFeatureEnabled(featureSet, disableSet, "remove-empties", defaultEnabled: true);
            bool enableSetUnitsMeters = ResolveFeatureEnabled(featureSet, disableSet, "units-meters", defaultEnabled: true);
            bool enableMoveArmatureGround = ResolveFeatureEnabled(featureSet, disableSet, "move-armature-ground", defaultEnabled: false);
            bool enableFaceBake = ResolveFeatureEnabled(featureSet, disableSet, "face-bake", defaultEnabled: false);

            AddOptionalArg(args, "--features", featuresCsv);
            AddOptionalArg(args, "--disable", disableCsv);

            AddBooleanOption(args, "bindpose", enableBindPoseFix, defaultEnabled: true);
            AddBooleanOption(args, "remove-empties", enableRemoveEmpties, defaultEnabled: true);
            AddBooleanOption(args, "units-meters", enableSetUnitsMeters, defaultEnabled: true);
            AddBooleanOption(args, "move-armature-ground", enableMoveArmatureGround, defaultEnabled: false);

            AddOptionalArg(args, "--remove-meshes", removeMeshes);
            AddOptionalArg(args, "--remove-bones", removeBones);
            AddOptionalArg(args, "--rename-bones", renameBones);

            if (enableFaceBake)
            {
                args.Add("--face-bake");
                args.AddRange(new[] { "--face-bake-fps", DefaultFaceBakeFps.ToString() });
                args.AddRange(new[] { "--face-bake-max-shapes", DefaultFaceBakeMaxShapes.ToString() });
            }

            if (EnableVerbose)
                args.Add("--verbose");

            AddOptionalArg(args, "--game", detection.gameKey);
            AddOptionalArg(args, "--character", detection.character);
            AddOptionalArg(args, "--shader", detection.shaderPath);
            AddOptionalArg(args, "--sourceJson", detection.sourceJson);

            return args;
        }

        private static string BuildFeaturesCsv(ConverterProfile profile)
        {
            var set = ParseFeatureSet(GetDefaultCsv(profile?.Features));

            if (!string.IsNullOrEmpty(NormalizeCsv(profile?.RemoveMeshes?.List)))
                set.Add("remove-meshes");
            if (!string.IsNullOrEmpty(NormalizeCsv(profile?.RemoveBones?.List)))
                set.Add("remove-bones");
            if (!string.IsNullOrEmpty(NormalizeCsv(profile?.RenameBones?.Mapping)))
                set.Add("rename-bones");

            return JoinFeatureSet(set);
        }

        private static string GetDefaultCsv(ConverterFeatureConfig config)
        {
            if (config == null) return null;
            if (config.DefaultList != null && config.DefaultList.Count > 0)
                return string.Join(",", config.DefaultList);
            return config.Default;
        }

        private static IEnumerable<string> QuoteArgs(IEnumerable<string> args)
        {
            foreach (var a in args)
                yield return a.Contains(" ") ? $"\"{a}\"" : a;
        }

        private static HashSet<string> ParseFeatureSet(string raw)
        {
            var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw)) return set;

            var tokens = raw.Split(new[] { ',', ' ', '\t', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var trimmed = token.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    set.Add(trimmed);
            }
            return set;
        }

        private static bool ResolveFeatureEnabled(HashSet<string> features, HashSet<string> disabled, string featureName, bool defaultEnabled)
        {
            bool isDisabled = disabled != null && (disabled.Contains("all") || disabled.Contains(featureName));
            if (isDisabled) return false;

            bool isEnabled = features != null && (features.Contains("all") || features.Contains(featureName));
            return isEnabled || defaultEnabled;
        }

        private static void AddBooleanOption(List<string> args, string optionName, bool enabled, bool defaultEnabled)
        {
            if (defaultEnabled)
            {
                if (!enabled)
                    args.Add("--no-" + optionName);
            }
            else
            {
                if (enabled)
                    args.Add("--" + optionName);
            }
        }

        private static void AddOptionalArg(List<string> args, string flag, string value)
        {
            if (!string.IsNullOrEmpty(value))
                args.AddRange(new[] { flag, value });
        }

        private static string JoinFeatureSet(HashSet<string> set)
        {
            if (set == null || set.Count == 0) return null;
            return string.Join(",", set);
        }

        private static string NormalizeCsv(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var tokens = raw.Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens == null || tokens.Length == 0) return null;
            return string.Join(",", tokens);
        }

        private static string TryExtractOutputPath(List<string> stdout)
        {
            if (stdout == null || stdout.Count == 0)
                return null;

            for (int i = 0; i < stdout.Count; i++)
            {
                var line = stdout[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                const string marker = "Output exists. Writing to:";
                int index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    string path = line.Substring(index + marker.Length).Trim();
                    return string.IsNullOrWhiteSpace(path) ? null : path;
                }
            }

            return null;
        }

        private static string FindNewestOutputPath(string inputFullPath, DateTime sinceUtc)
        {
            if (string.IsNullOrWhiteSpace(inputFullPath)) return null;
            string dir = Path.GetDirectoryName(inputFullPath);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return null;

            string baseName = Path.GetFileNameWithoutExtension(inputFullPath);
            var candidates = Directory.GetFiles(dir, baseName + "*.fbx", SearchOption.TopDirectoryOnly);
            string best = null;
            DateTime bestTime = DateTime.MinValue;

            foreach (var file in candidates)
            {
                DateTime writeTime = File.GetLastWriteTimeUtc(file);
                if (writeTime < sinceUtc) continue;
                if (writeTime > bestTime)
                {
                    bestTime = writeTime;
                    best = file;
                }
            }

            return best;
        }

        private static string FindNewestOutputAssetPath(string inputFullPath)
        {
            string newest = FindNewestOutputPath(inputFullPath, DateTime.UtcNow.AddMinutes(-5));
            return EditorUtil.ToUnityAssetPath(newest);
        }

    }
}
#endif
