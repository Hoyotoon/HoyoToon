#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.AssetPipeline.Models;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ManagerInspector.Setup
{
    internal static class SetupModelsHelper
    {
        private const string ConvertedMarker = "Hoyo2VRC_Converted";
        private const string OptionsHashPrefix = "Hoyo2VRC_OptionsHash_";
        private const string ConvertedMarkerEquals = "Hoyo2VRC_Converted=true";

        public static bool ShouldConvertModel(ModelSetupUtility.SetupContext context)
        {
            if (context == null || !context.IsFbxAsset)
            {
                return false;
            }

            if (string.IsNullOrEmpty(context.DetectedGameKey))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: Model conversion disabled (no game detected).");
                return false;
            }

            return true;
        }

        public static bool NeedsConversion(ModelSetupUtility.SetupContext context, string optionsHash = null)
        {
            if (context == null || !context.IsFbxAsset)
            {
                return false;
            }

            var check = EvaluateConversion(context.AssetPath, optionsHash);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Auto Setup: Conversion check => {check.Message}");
            return !check.IsConverted;
        }
        public static bool ShouldApplyImportSettings(ModelSetupUtility.SetupContext context)
        {
            if (context == null || !context.IsFbxAsset)
            {
                return false;
            }

            if (string.IsNullOrEmpty(context.DetectedGameKey))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: Import rules disabled (no game detected).");
                return false;
            }

            return true;
        }

        public static bool NeedsModelImportSettings(ModelSetupUtility.SetupContext context)
        {
            if (context == null || !context.IsFbxAsset)
            {
                return false;
            }

            if (ModelImportRulesApplier.TryEvaluateFromConfigForAsset(context.AssetPath, context.Asset, out var gameKey, out var differences))
            {
                var diffText = differences != null && differences.Count > 0
                    ? string.Join("; ", differences)
                    : "<no differences>";
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Auto Setup: Import rules needed for game '{gameKey}'. Differences: {diffText}");
                return true;
            }

            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: Import rules already satisfy defaults. No changes needed.");
            return false;
        }

        
        public static bool IsConverted(string assetPath, string optionsHash = null)
        {
            return EvaluateConversion(assetPath, optionsHash).IsConverted;
        }

        public static void MarkConverted(string assetPath, string optionsHash = null, bool isVrcProfile = false)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            var marker = ConvertedMarker;
            var optionsKey = OptionsHashPrefix;

            var props = importer.extraUserProperties ?? Array.Empty<string>();
            var propList = new List<string>(props);
            if (!propList.Any(p => p.Equals(marker, StringComparison.OrdinalIgnoreCase)))
            {
                propList.Add(marker);
            }

            if (!string.IsNullOrEmpty(optionsHash))
            {
                var hashFlag = $"{optionsKey}{optionsHash}";
                if (!propList.Any(p => p.Equals(hashFlag, StringComparison.OrdinalIgnoreCase)))
                {
                    propList.Add(hashFlag);
                }
            }

            importer.extraUserProperties = propList.ToArray();

            var userData = importer.userData ?? string.Empty;
            if (!ContainsConvertedMarker(userData))
            {
                userData = AppendUserData(userData, marker);
            }

            if (!string.IsNullOrEmpty(optionsHash))
            {
                var hashFlag = $"{optionsKey}{optionsHash}";
                if (userData.IndexOf(hashFlag, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    userData = AppendUserData(userData, hashFlag);
                }
            }

            importer.userData = userData;

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath) as GameObject;
            if (asset != null)
            {
                var labels = AssetDatabase.GetLabels(asset).ToList();
                if (!labels.Any(label => label.Equals(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    labels.Add(marker);
                }

                if (!string.IsNullOrEmpty(optionsHash))
                {
                    var hashFlag = $"{optionsKey}{optionsHash}";
                    if (!labels.Any(label => label.Equals(hashFlag, StringComparison.OrdinalIgnoreCase)))
                    {
                        labels.Add(hashFlag);
                    }
                }

                AssetDatabase.SetLabels(asset, labels.ToArray());
            }

            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }

        private struct ConversionCheck
        {
            public bool IsConverted;
            public string Message;
        }

        private static ConversionCheck EvaluateConversion(string assetPath, string optionsHash)
        {
            bool converted = IsConverted(assetPath, optionsHash, out _);
            return new ConversionCheck
            {
                IsConverted = converted,
                Message = converted ? "Already converted" : "Needs conversion"
            };
        }

        private static bool IsConverted(string assetPath, string optionsHash, out string[] props)
        {
            props = null;
            if (string.IsNullOrEmpty(assetPath)) return false;

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return false;

            props = importer.extraUserProperties ?? Array.Empty<string>();
            if (props.Length == 0) return false;

            bool converted = props.Any(p => p.Equals(ConvertedMarker, StringComparison.OrdinalIgnoreCase)
                                         || p.Equals(ConvertedMarkerEquals, StringComparison.OrdinalIgnoreCase));
            if (!converted) return false;

            if (string.IsNullOrEmpty(optionsHash)) return true;

            string hashFlag = $"{OptionsHashPrefix}{optionsHash}";
            return props.Any(p => p.Equals(hashFlag, StringComparison.OrdinalIgnoreCase));
        }

        private static string AppendUserData(string existing, string entry)
        {
            if (string.IsNullOrWhiteSpace(existing))
            {
                return entry;
            }

            if (existing.IndexOf(entry, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return existing;
            }

            var separator = existing.EndsWith(";", StringComparison.Ordinal) ? string.Empty : ";";
            return existing + separator + entry;
        }

        private static bool ContainsConvertedMarker(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf(ConvertedMarker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool TryResolveConvertedAsset(ModelSetupUtility.SetupContext context, out GameObject asset, out string assetPath, bool allowUnmarked = false, string preferredBaseName = null)
        {
            asset = null;
            assetPath = null;

            if (context == null || string.IsNullOrEmpty(context.AssetPath))
            {
                return false;
            }

            var unityPath = context.AssetPath;
            if (TryLoadCandidate(unityPath, requireConverted: true, out asset, out assetPath))
            {
                return true;
            }

            var fullPath = EditorUtil.ToAbsolutePath(unityPath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            var baseName = Path.GetFileNameWithoutExtension(fullPath);
            if (string.IsNullOrEmpty(baseName))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(preferredBaseName))
            {
                var preferredPath = Path.Combine(directory, preferredBaseName + ".fbx");
                var unityPreferred = EditorUtil.ToUnityAssetPath(preferredPath);
                if (!string.IsNullOrEmpty(unityPreferred)
                    && !string.Equals(unityPreferred, unityPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(preferredPath))
                {
                    if (TryLoadCandidate(unityPreferred, requireConverted: false, out asset, out assetPath))
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Auto Setup: Using character-named converted asset '{unityPreferred}'.");
                        return true;
                    }
                }
            }

            var candidates = Directory.GetFiles(directory, "*.fbx", SearchOption.TopDirectoryOnly)
                .Select(path => new
                {
                    unityPath = EditorUtil.ToUnityAssetPath(path),
                    time = File.GetLastWriteTimeUtc(path),
                    nameMatch = Path.GetFileNameWithoutExtension(path)
                        .StartsWith(baseName, StringComparison.OrdinalIgnoreCase)
                })
                .Where(entry => !string.IsNullOrEmpty(entry.unityPath))
                .OrderByDescending(entry => entry.nameMatch)
                .ThenByDescending(entry => entry.time)
                .Select(entry => entry.unityPath)
                .ToList();

            if (candidates.Count == 0)
            {
                return false;
            }

            foreach (var unityCandidate in candidates)
            {
                if (TryLoadCandidate(unityCandidate, requireConverted: true, out asset, out assetPath))
                {
                    return true;
                }

                var nameOnly = Path.GetFileNameWithoutExtension(unityCandidate);
                if (string.IsNullOrEmpty(nameOnly)
                    || (nameOnly.IndexOf("Hoyo2", StringComparison.OrdinalIgnoreCase) < 0
                        && nameOnly.IndexOf("Converted", StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                if (!TryLoadCandidate(unityCandidate, requireConverted: false, out asset, out assetPath))
                {
                    continue;
                }

                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Auto Setup: Using name-based converted candidate '{unityCandidate}'.");
                return true;
            }

            if (!allowUnmarked)
            {
                return false;
            }

            foreach (var unityCandidate in candidates)
            {
                if (string.Equals(unityCandidate, unityPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryLoadCandidate(unityCandidate, requireConverted: false, out asset, out assetPath))
                {
                    continue;
                }

                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Auto Setup: Using newest converted candidate '{unityCandidate}'.");
                return true;
            }

            return false;
        }

        private static bool TryLoadCandidate(string unityPath, bool requireConverted, out GameObject asset, out string assetPath)
        {
            asset = null;
            assetPath = null;

            if (string.IsNullOrEmpty(unityPath))
            {
                return false;
            }

            if (requireConverted && !IsConverted(unityPath))
            {
                return false;
            }

            asset = AssetDatabase.LoadAssetAtPath<GameObject>(unityPath);
            if (asset == null)
            {
                return false;
            }

            assetPath = unityPath;
            return true;
        }
    }
}
#endif
