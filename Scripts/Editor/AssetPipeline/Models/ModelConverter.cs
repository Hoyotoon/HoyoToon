#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Detection.Character;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Detection.Shader;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.IO;
using HoyoToon.Runtime.ScriptableObjects;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using UnityEngine;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetQueryUtility;

namespace HoyoToon.Editor.AssetPipeline.Models
{
    public static class ModelConverter
    {
        private const HoyoToonLogCategory LogCategory = HoyoToonLogCategory.Converter;
        private const string ToolAssetPath = HoyoToonApi.PackageRootAssetPath + "/Plugins/HoyoConverter.exe";
        private const string ConverterConfigsAssetSuffix = "/Config/GameConverterConfigs.asset";
        private const string DefaultConverterType = "Hoyo2Unity";
        private const bool EnableVerbose = true;
        private const string ConvertedAssetLabel = "HoyoToonConverted";
        private const string OutputExistsMarker = "Output exists. Writing to:";
        private const int ConverterTimeoutMilliseconds = 10 * 60 * 1000;
        private const int ConverterPollMilliseconds = 250;

        private static Dictionary<string, List<GameConverterConfigsSO.Entry>> entriesByGame =
            new Dictionary<string, List<GameConverterConfigsSO.Entry>>(StringComparer.Ordinal);

        private readonly struct DetectionInfo
        {
            public DetectionInfo(
                GameConfigSO game,
                string character,
                string shaderPath,
                string sourceJsonAssetPath)
            {
                Game = game;
                Character = character;
                ShaderPath = shaderPath;
                SourceJsonAssetPath = sourceJsonAssetPath;
            }

            public GameConfigSO Game { get; }

            public string GameKey => Game?.Key;

            public string Character { get; }

            public string ShaderPath { get; }

            public string SourceJsonAssetPath { get; }
        }

        private readonly struct ConverterSelection
        {
            public ConverterSelection(string requestedConverterType, string resolvedConverterType, string source, GameConverterConfigsSO.Entry entry)
            {
                RequestedConverterType = requestedConverterType;
                ResolvedConverterType = resolvedConverterType;
                Source = source;
                Entry = entry;
            }

            public string RequestedConverterType { get; }

            public string ResolvedConverterType { get; }

            public string Source { get; }

            public GameConverterConfigsSO.Entry Entry { get; }

            public string DisplayKey => string.IsNullOrWhiteSpace(Entry?.Key) ? ResolvedConverterType : Entry.Key;
        }

        public static void Initialize()
        {
            entriesByGame = LoadGeneratedAssets<GameConverterConfigsSO>("t:GameConverterConfigsSO", ConverterConfigsAssetSuffix)
                .Where(asset => asset != null && asset.Entries != null)
                .SelectMany(asset => asset.Entries)
                .Where(entry => entry != null
                    && !string.IsNullOrWhiteSpace(entry.GameKey)
                    && !string.IsNullOrWhiteSpace(entry.ConverterType))
                .GroupBy(entry => entry.GameKey, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(entry => entry.ConverterType, StringComparer.Ordinal)
                        .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                        .ToList(),
                    StringComparer.Ordinal);

            HoyoToonLogger.Verbose(
                LogCategory,
                $"Loaded model converter configs for {entriesByGame.Count} game(s).");
        }

        public static void Process(UnityEngine.Object fbxAsset)
        {
            ProcessInternal(fbxAsset, out _);
        }

        public static bool TryProcessAndGetOutput(
            UnityEngine.Object fbxAsset,
            out string outputAssetPath,
            out GameObject outputAsset,
            bool promptForKnownCharacterProblems = true)
        {
            outputAssetPath = ProcessInternal(fbxAsset, out _, promptForKnownCharacterProblems);
            if (string.IsNullOrWhiteSpace(outputAssetPath))
            {
                outputAsset = null;
                return false;
            }

            outputAsset = string.IsNullOrWhiteSpace(outputAssetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(outputAssetPath);

            return outputAsset != null;
        }

        private static string ProcessInternal(
            UnityEngine.Object fbxAsset,
            out string inputAssetPath,
            bool promptForKnownCharacterProblems = true)
        {
            inputAssetPath = null;

            if (fbxAsset == null)
            {
                HoyoToonLogger.Error(LogCategory, "Model conversion was skipped because no FBX asset was provided.");
                return null;
            }

            inputAssetPath = AssetDatabase.GetAssetPath(fbxAsset);
            if (string.IsNullOrWhiteSpace(inputAssetPath))
            {
                HoyoToonLogger.Error(LogCategory, "Model conversion was skipped because the selected object is not a valid asset.", context: fbxAsset);
                return null;
            }

            if (!inputAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonLogger.Error(
                    LogCategory,
                    $"Model conversion was skipped because '{inputAssetPath}' is not an FBX asset.",
                    context: fbxAsset);
                return null;
            }

            if (HasConvertedLabel(inputAssetPath))
            {
                HoyoToonLogger.Info(
                    LogCategory,
                    $"Model conversion was skipped because '{inputAssetPath}' is already tagged '{ConvertedAssetLabel}'.",
                    context: fbxAsset);
                return inputAssetPath;
            }

            string absoluteInputPath = AssetContextJsonQueryUtility.ToAbsolutePath(inputAssetPath);
            if (string.IsNullOrWhiteSpace(absoluteInputPath) || !File.Exists(absoluteInputPath))
            {
                HoyoToonLogger.Error(
                    LogCategory,
                    $"Model conversion could not resolve the source FBX path for '{inputAssetPath}'.",
                    context: fbxAsset);
                return null;
            }

            string exePath = GetToolPath();
            if (!File.Exists(exePath))
            {
                HoyoToonLogger.Error(
                    LogCategory,
                    $"Model conversion could not find the converter executable at '{exePath}'.",
                    context: fbxAsset);
                return null;
            }

            HoyoToonLogger.Info(LogCategory, $"Starting model conversion for '{inputAssetPath}'.", context: fbxAsset);

            DetectionInfo detection = DetectConversionInfo(fbxAsset, inputAssetPath);
            if (promptForKnownCharacterProblems
                && !CharacterProblemPrompt.ConfirmAssetContext(inputAssetPath, "Model Conversion", fbxAsset))
            {
                HoyoToonLogger.Info(
                    LogCategory,
                    $"Model conversion was cancelled because '{inputAssetPath}' has a known character problem.",
                    context: fbxAsset);
                return null;
            }

            ConverterSelection selection = ResolveConverterSelection(detection.GameKey);
            LogConverterSelection(selection, detection, fbxAsset);

            string predictedOutputPath = PredictOutputPath(absoluteInputPath, detection);
            PrepareOverwriteTarget(predictedOutputPath, absoluteInputPath, fbxAsset);
            int exitCode = RunConverterExe(exePath, absoluteInputPath, detection, selection, out string outputPath, fbxAsset);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = predictedOutputPath;
            }

            outputPath = ResolvePredictedOutputPath(outputPath, predictedOutputPath, absoluteInputPath, fbxAsset);
            string outputAssetPath = AssetContextJsonQueryUtility.ToAssetPath(outputPath);
            if (string.IsNullOrWhiteSpace(outputAssetPath))
            {
                outputAssetPath = FindNewestOutputAssetPath(predictedOutputPath, absoluteInputPath);
            }

            ForceReimport(string.IsNullOrWhiteSpace(outputAssetPath) ? inputAssetPath : outputAssetPath);
            HoyoToonLogger.Info(LogCategory, $"Model conversion reimport completed. Output='{outputAssetPath ?? inputAssetPath}'.", context: fbxAsset);

            if (exitCode != 0)
            {
                HoyoToonLogger.Error(LogCategory, $"Model converter exited with code {exitCode}.", context: fbxAsset);
                return null;
            }

            string convertedAssetPath = string.IsNullOrWhiteSpace(outputAssetPath) ? inputAssetPath : outputAssetPath;
            MarkAsConverted(convertedAssetPath, fbxAsset);

            HoyoToonLogger.Info(LogCategory, "Model conversion completed successfully.", context: fbxAsset);
            return string.IsNullOrWhiteSpace(outputAssetPath) ? inputAssetPath : outputAssetPath;
        }

        private static DetectionInfo DetectConversionInfo(UnityEngine.Object fbxAsset, string assetPath)
        {
            if (!GameDetector.TryDetectGameFromAssetContext(assetPath, out GameConfigSO game, out string matchedJsonAssetPath)
                || game == null)
            {
                HoyoToonLogger.Warning(
                    LogCategory,
                    $"Model conversion detection could not determine a game for '{assetPath}'. Conversion will continue with built-in defaults.",
                    context: fbxAsset);
                return default;
            }

            string characterContextPath = string.IsNullOrWhiteSpace(matchedJsonAssetPath) ? assetPath : matchedJsonAssetPath;
            string character = CharacterNameDetector.TryExtractCharacterName(game.Key, characterContextPath);
            string shaderPath = ResolveShaderPath(game, matchedJsonAssetPath);

            string characterLabel = string.IsNullOrWhiteSpace(character) ? "Unknown" : character;
            string shaderLabel = string.IsNullOrWhiteSpace(shaderPath) ? "Unknown" : shaderPath;
            string jsonLabel = string.IsNullOrWhiteSpace(matchedJsonAssetPath) ? "None" : matchedJsonAssetPath;

            HoyoToonLogger.Info(
                LogCategory,
                $"Model conversion detection: Game='{game.Key}', Character='{characterLabel}', Shader='{shaderLabel}', JSON='{jsonLabel}'.",
                context: fbxAsset);

            return new DetectionInfo(game, character, shaderPath, matchedJsonAssetPath);
        }

        private static string ResolveShaderPath(GameConfigSO game, string jsonAssetPath)
        {
            if (game == null || string.IsNullOrWhiteSpace(jsonAssetPath))
            {
                return null;
            }

            string absoluteJsonPath = AssetContextJsonQueryUtility.ToAbsolutePath(jsonAssetPath);
            if (!TextFileUtility.TryReadAllText(absoluteJsonPath, out string rawJson))
            {
                return null;
            }

            return ShaderDetector.TryDetectShader(game, rawJson, out string shaderPath, out _)
                ? shaderPath
                : null;
        }

        private static ConverterSelection ResolveConverterSelection(string gameKey)
        {
            Initialize();

            string requestedConverterType = DefaultConverterType;
            if (!string.IsNullOrWhiteSpace(gameKey)
                && entriesByGame.TryGetValue(gameKey, out List<GameConverterConfigsSO.Entry> entries)
                && entries != null
                && entries.Count > 0)
            {
                GameConverterConfigsSO.Entry entry = SelectBestEntry(entries, requestedConverterType);
                if (entry != null)
                {
                    string resolvedType = string.IsNullOrWhiteSpace(entry.ConverterType)
                        ? requestedConverterType
                        : entry.ConverterType;
                    return new ConverterSelection(requestedConverterType, resolvedType, $"Game '{gameKey}'", entry);
                }
            }

            return new ConverterSelection(requestedConverterType, requestedConverterType, "Built-in defaults", null);
        }

        private static GameConverterConfigsSO.Entry SelectBestEntry(IReadOnlyList<GameConverterConfigsSO.Entry> entries, string requestedConverterType)
        {
            if (entries == null || entries.Count <= 0)
            {
                return null;
            }

            return entries.FirstOrDefault(candidate => MatchesConverterType(candidate, requestedConverterType) && string.IsNullOrWhiteSpace(candidate.Key))
                ?? entries.FirstOrDefault(candidate => MatchesConverterType(candidate, requestedConverterType))
                ?? entries.FirstOrDefault(candidate => MatchesConverterType(candidate, DefaultConverterType) && string.IsNullOrWhiteSpace(candidate.Key))
                ?? entries.FirstOrDefault(candidate => MatchesConverterType(candidate, DefaultConverterType))
                ?? entries.FirstOrDefault(candidate => string.IsNullOrWhiteSpace(candidate.Key))
                ?? entries[0];
        }

        private static bool MatchesConverterType(GameConverterConfigsSO.Entry entry, string converterType)
        {
            return entry != null
                && !string.IsNullOrWhiteSpace(converterType)
                && string.Equals(entry.ConverterType, converterType, StringComparison.OrdinalIgnoreCase);
        }

        private static void LogConverterSelection(ConverterSelection selection, DetectionInfo detection, UnityEngine.Object context)
        {
            string features = NormalizeCsv(GetDefaultCsv(selection.Entry?.Features));
            string disable = NormalizeCsv(GetDefaultCsv(selection.Entry?.Disable));
            string removeMeshes = NormalizeCsv(selection.Entry?.RemoveMeshes?.List);
            string removeBones = NormalizeCsv(selection.Entry?.RemoveBones?.List);
            string renameBones = NormalizeCsv(selection.Entry?.RenameBones?.Mapping);
            string gameLabel = string.IsNullOrWhiteSpace(detection.GameKey) ? "Unknown" : detection.GameKey;

            HoyoToonLogger.Info(
                LogCategory,
                $"Model converter config: Requested='{selection.RequestedConverterType}', Resolved='{selection.DisplayKey}', Source='{selection.Source}', Game='{gameLabel}', Features='{features ?? "<none>"}', Disable='{disable ?? "<none>"}', RemoveMeshes='{removeMeshes ?? "<none>"}', RemoveBones='{removeBones ?? "<none>"}', RenameBones='{renameBones ?? "<none>"}'.",
                context: context);
        }

        private static int RunConverterExe(
            string exePath,
            string absoluteInputPath,
            DetectionInfo detection,
            ConverterSelection selection,
            out string outputPath,
            UnityEngine.Object context)
        {
            outputPath = null;
            List<string> args = BuildArgumentList(absoluteInputPath, detection, selection);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = string.Join(" ", QuoteArgs(args)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
            {
                if (process == null)
                {
                    HoyoToonLogger.Error(LogCategory, "Model conversion failed to start the converter process.", context: context);
                    return -1;
                }

                var stdout = new List<string>();
                var stderr = new List<string>();

                process.OutputDataReceived += (_, eventArgs) =>
                {
                    if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                    {
                        stdout.Add(eventArgs.Data);
                    }
                };
                process.ErrorDataReceived += (_, eventArgs) =>
                {
                    if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                    {
                        stderr.Add(eventArgs.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                Stopwatch stopwatch = Stopwatch.StartNew();
                bool timedOut = false;
                while (!process.WaitForExit(ConverterPollMilliseconds))
                {
                    if (stopwatch.ElapsedMilliseconds < ConverterTimeoutMilliseconds)
                    {
                        continue;
                    }

                    timedOut = true;
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception killException)
                    {
                        HoyoToonLogger.Warning(LogCategory, $"Failed to kill timed-out converter process: {killException.Message}", context: context);
                    }

                    process.WaitForExit(5000);
                    break;
                }

                if (!timedOut)
                {
                    process.WaitForExit();
                }

                LogProcessLines(stdout, stderr, context);

                if (timedOut)
                {
                    HoyoToonLogger.Error(LogCategory, $"Model conversion timed out after {ConverterTimeoutMilliseconds / 1000} seconds.", context: context);
                    return -1;
                }

                outputPath = TryExtractOutputPath(stdout);
                return process.ExitCode;
            }
        }

        private static void LogProcessLines(IReadOnlyList<string> stdout, IReadOnlyList<string> stderr, UnityEngine.Object context)
        {
            if (stdout != null && stdout.Count > 0)
            {
                HoyoToonLogger.Info(LogCategory, "Model converter stdout:", context: context);
                foreach (string line in stdout)
                {
                    HoyoToonLogger.Info(LogCategory, line, context: context);
                }
            }

            if (stderr != null && stderr.Count > 0)
            {
                HoyoToonLogger.Warning(LogCategory, "Model converter stderr:", context: context);
                foreach (string line in stderr)
                {
                    HoyoToonLogger.Warning(LogCategory, line, context: context);
                }
            }
        }

        private static string PredictOutputPath(string absoluteInputPath, DetectionInfo detection)
        {
            if (string.IsNullOrWhiteSpace(absoluteInputPath))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(detection.Character))
            {
                return absoluteInputPath;
            }

            string directoryPath = Path.GetDirectoryName(absoluteInputPath);
            string extension = Path.GetExtension(absoluteInputPath);
            string sanitizedCharacterName = SanitizeFileName(detection.Character);
            if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(sanitizedCharacterName))
            {
                return absoluteInputPath;
            }

            return Path.Combine(directoryPath, sanitizedCharacterName + extension);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                if (invalidCharacters.Contains(characters[i]))
                {
                    characters[i] = '_';
                }
            }

            return new string(characters);
        }

        private static List<string> BuildArgumentList(string absoluteInputPath, DetectionInfo detection, ConverterSelection selection)
        {
            var args = new List<string>
            {
                absoluteInputPath,
                absoluteInputPath,
            };

            string removeMeshes = NormalizeCsv(selection.Entry?.RemoveMeshes?.List);
            string removeBones = NormalizeCsv(selection.Entry?.RemoveBones?.List);
            string renameBones = NormalizeCsv(selection.Entry?.RenameBones?.Mapping);

            HashSet<string> featureSet = ParseFeatureSet(BuildFeaturesCsv(selection.Entry));
            HashSet<string> disableSet = ParseFeatureSet(NormalizeCsv(GetDefaultCsv(selection.Entry?.Disable)));

            bool enableBindPoseFix = ResolveFeatureEnabled(featureSet, disableSet, "bindpose", defaultEnabled: true);
            bool enableRemoveEmpties = ResolveFeatureEnabled(featureSet, disableSet, "remove-empties", defaultEnabled: true);
            bool enableSetUnitsMeters = ResolveFeatureEnabled(featureSet, disableSet, "units-meters", defaultEnabled: true);
            bool enableRemoveMeshes = ResolveFeatureEnabled(featureSet, disableSet, "remove-meshes", defaultEnabled: false);
            bool enableRemoveBones = ResolveFeatureEnabled(featureSet, disableSet, "remove-bones", defaultEnabled: false);
            bool enableRenameBones = ResolveFeatureEnabled(featureSet, disableSet, "rename-bones", defaultEnabled: false);
            bool enableMoveArmatureGround = ResolveFeatureEnabled(featureSet, disableSet, "move-armature-ground", defaultEnabled: false);
            bool enableFaceBake = ResolveFeatureEnabled(featureSet, disableSet, "face-bake", defaultEnabled: false);

            AddBooleanOption(args, "bindpose", enableBindPoseFix, defaultEnabled: true);
            AddBooleanOption(args, "remove-empties", enableRemoveEmpties, defaultEnabled: true);
            AddBooleanOption(args, "set-units-meters", enableSetUnitsMeters, defaultEnabled: true);
            AddBooleanOption(args, "move-armature-ground", enableMoveArmatureGround, defaultEnabled: false);

            if (enableRemoveMeshes)
            {
                AddOptionalArg(args, "--remove-meshes", removeMeshes);
            }

            if (enableRemoveBones)
            {
                AddOptionalArg(args, "--remove-bones", removeBones);
            }

            if (enableRenameBones)
            {
                AddOptionalArg(args, "--rename-bones", renameBones);
            }

            if (enableFaceBake)
            {
                args.Add("--bake-face-shapes");
            }

            if (EnableVerbose)
            {
                args.Add("--verbose");
            }

            AddOptionalArg(args, "--game", detection.GameKey);
            AddOptionalArg(args, "--character", detection.Character);
            AddOptionalArg(args, "--shader", detection.ShaderPath);
            AddOptionalArg(args, "--sourceJson", detection.SourceJsonAssetPath);

            return args;
        }

        private static string BuildFeaturesCsv(GameConverterConfigsSO.Entry entry)
        {
            HashSet<string> set = ParseFeatureSet(GetDefaultCsv(entry?.Features));

            if (!string.IsNullOrEmpty(NormalizeCsv(entry?.RemoveMeshes?.List)))
            {
                set.Add("remove-meshes");
            }

            if (!string.IsNullOrEmpty(NormalizeCsv(entry?.RemoveBones?.List)))
            {
                set.Add("remove-bones");
            }

            if (!string.IsNullOrEmpty(NormalizeCsv(entry?.RenameBones?.Mapping)))
            {
                set.Add("rename-bones");
            }

            return JoinFeatureSet(set);
        }

        private static string GetDefaultCsv(GameConverterSectionData config)
        {
            if (config == null)
            {
                return null;
            }

            if (config.DefaultList != null && config.DefaultList.Count > 0)
            {
                return string.Join(",", config.DefaultList);
            }

            return config.Default;
        }

        private static IEnumerable<string> QuoteArgs(IEnumerable<string> args)
        {
            foreach (string arg in args)
            {
                yield return QuoteArg(arg);
            }
        }

        private static string QuoteArg(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                return "\"\"";
            }

            if (!arg.Any(character => char.IsWhiteSpace(character) || character == '"'))
            {
                return arg;
            }

            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }

        private static HashSet<string> ParseFeatureSet(string raw)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return set;
            }

            string[] tokens = raw.Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                string trimmed = token.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    set.Add(trimmed);
                }
            }

            return set;
        }

        private static bool ResolveFeatureEnabled(HashSet<string> features, HashSet<string> disabled, string featureName, bool defaultEnabled)
        {
            bool isDisabled = disabled != null && (disabled.Contains("all") || disabled.Contains(featureName));
            if (isDisabled)
            {
                return false;
            }

            bool isEnabled = features != null && (features.Contains("all") || features.Contains(featureName));
            return isEnabled || defaultEnabled;
        }

        private static void AddBooleanOption(List<string> args, string optionName, bool enabled, bool defaultEnabled)
        {
            if (defaultEnabled)
            {
                if (!enabled)
                {
                    args.Add("--no-" + optionName);
                }
            }
            else if (enabled)
            {
                args.Add("--" + optionName);
            }
        }

        private static void AddOptionalArg(List<string> args, string flag, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                args.Add(flag);
                args.Add(value);
            }
        }

        private static string JoinFeatureSet(HashSet<string> set)
        {
            return set == null || set.Count <= 0
                ? null
                : string.Join(",", set.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        }

        private static string NormalizeCsv(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string[] tokens = raw.Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens == null || tokens.Length <= 0
                ? null
                : string.Join(",", tokens);
        }

        private static string TryExtractOutputPath(IReadOnlyList<string> stdout)
        {
            if (stdout == null || stdout.Count <= 0)
            {
                return null;
            }

            foreach (string line in stdout)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int markerIndex = line.IndexOf(OutputExistsMarker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                {
                    continue;
                }

                string path = line.Substring(markerIndex + OutputExistsMarker.Length).Trim().Trim('"');
                return string.IsNullOrWhiteSpace(path) ? null : path;
            }

            return null;
        }

        private static void PrepareOverwriteTarget(string predictedOutputPath, string inputFullPath, UnityEngine.Object context)
        {
            if (string.IsNullOrWhiteSpace(predictedOutputPath)
                || string.IsNullOrWhiteSpace(inputFullPath)
                || AreSameNormalizedPath(predictedOutputPath, inputFullPath))
            {
                return;
            }

            string predictedAssetPath = AssetContextJsonQueryUtility.ToAssetPath(predictedOutputPath);
            if (!string.IsNullOrWhiteSpace(predictedAssetPath))
            {
                if (AssetDatabase.DeleteAsset(predictedAssetPath))
                {
                    HoyoToonLogger.Info(
                        LogCategory,
                        $"Deleted existing converted model asset at '{predictedAssetPath}' before overwrite.",
                        context: context);
                    return;
                }
            }

            if (!File.Exists(predictedOutputPath))
            {
                return;
            }

            try
            {
                File.Delete(predictedOutputPath);
                HoyoToonLogger.Info(
                    LogCategory,
                    $"Deleted existing converted model file at '{predictedOutputPath}' before overwrite.",
                    context: context);
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Warning(
                    LogCategory,
                    $"Could not delete existing converted model file at '{predictedOutputPath}' before overwrite: {ex.Message}.",
                    context: context);
            }
        }

        private static string ResolvePredictedOutputPath(
            string outputPath,
            string predictedOutputPath,
            string inputFullPath,
            UnityEngine.Object context)
        {
            if (string.IsNullOrWhiteSpace(outputPath)
                || string.IsNullOrWhiteSpace(predictedOutputPath)
                || string.IsNullOrWhiteSpace(inputFullPath)
                || AreSameNormalizedPath(predictedOutputPath, inputFullPath)
                || AreSameNormalizedPath(outputPath, predictedOutputPath))
            {
                return outputPath;
            }

            try
            {
                if (File.Exists(outputPath))
                {
                    if (File.Exists(predictedOutputPath))
                    {
                        File.Delete(predictedOutputPath);
                    }

                    File.Move(outputPath, predictedOutputPath);
                    HoyoToonLogger.Info(
                        LogCategory,
                        $"Converter output was written to '{outputPath}'. Moving to preferred output '{predictedOutputPath}'.",
                        context: context);
                    return predictedOutputPath;
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Warning(
                    LogCategory,
                    $"Could not move converter output from '{outputPath}' to '{predictedOutputPath}': {ex.Message}.",
                    context: context);
            }

            return outputPath;
        }

        private static bool AreSameNormalizedPath(string firstPath, string secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(firstPath),
                    Path.GetFullPath(secondPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return string.Equals(firstPath.Trim(), secondPath.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(firstPath.Trim(), secondPath.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string FindNewestOutputPath(string predictedOutputPath, string inputFullPath, DateTime sinceUtc)
        {
            if (!string.IsNullOrWhiteSpace(predictedOutputPath) && File.Exists(predictedOutputPath))
            {
                DateTime predictedWriteTimeUtc = File.GetLastWriteTimeUtc(predictedOutputPath);
                if (predictedWriteTimeUtc >= sinceUtc)
                {
                    return predictedOutputPath;
                }
            }

            if (string.IsNullOrWhiteSpace(inputFullPath))
            {
                return null;
            }

            string directoryPath = Path.GetDirectoryName(inputFullPath);
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return null;
            }

            string[] candidateBaseNames = new[]
            {
                Path.GetFileNameWithoutExtension(predictedOutputPath),
                Path.GetFileNameWithoutExtension(inputFullPath),
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

            string bestPath = null;
            DateTime bestWriteTime = DateTime.MinValue;

            foreach (string baseName in candidateBaseNames)
            {
                foreach (string candidatePath in Directory.GetFiles(directoryPath, baseName + "*.fbx", SearchOption.TopDirectoryOnly))
                {
                    DateTime writeTimeUtc = File.GetLastWriteTimeUtc(candidatePath);
                    if (writeTimeUtc < sinceUtc)
                    {
                        continue;
                    }

                    if (writeTimeUtc > bestWriteTime)
                    {
                        bestWriteTime = writeTimeUtc;
                        bestPath = candidatePath;
                    }
                }
            }

            return bestPath;
        }

        private static string FindNewestOutputAssetPath(string predictedOutputPath, string inputFullPath)
        {
            string newestPath = FindNewestOutputPath(predictedOutputPath, inputFullPath, DateTime.UtcNow.AddMinutes(-5));
            return AssetContextJsonQueryUtility.ToAssetPath(newestPath);
        }

        private static void ForceReimport(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        private static bool HasConvertedLabel(string assetPath)
        {
            UnityEngine.Object asset = LoadMainAsset(assetPath);
            if (asset == null)
            {
                return false;
            }

            return AssetDatabase.GetLabels(asset)
                .Any(label => string.Equals(label, ConvertedAssetLabel, StringComparison.OrdinalIgnoreCase));
        }

        private static void MarkAsConverted(string assetPath, UnityEngine.Object context)
        {
            UnityEngine.Object asset = LoadMainAsset(assetPath);
            if (asset == null)
            {
                HoyoToonLogger.Warning(
                    LogCategory,
                    $"Model conversion completed but could not set the converted tag on '{assetPath}'.",
                    context: context);
                return;
            }

            string[] existingLabels = AssetDatabase.GetLabels(asset);
            if (existingLabels.Any(label => string.Equals(label, ConvertedAssetLabel, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var updatedLabels = new List<string>(existingLabels)
            {
                ConvertedAssetLabel,
            };

            AssetDatabase.SetLabels(asset, updatedLabels.ToArray());
        }

        private static UnityEngine.Object LoadMainAsset(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? null
                : AssetDatabase.LoadMainAssetAtPath(assetPath);
        }

        private static string GetToolPath()
        {
            return AssetContextJsonQueryUtility.ToAbsolutePath(ToolAssetPath);
        }
    }
}
#endif
