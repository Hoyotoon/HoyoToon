#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon.Models;
using HoyoToon.Utilities;

namespace HoyoToon.EditorTools.ManagerUI
{
    internal static class HoyoToonSetupModelsHelper
    {
        public static bool ShouldConvertModel(HoyoToonModelSetupUtility.SetupContext context)
        {
            if (context == null || !context.IsFbxAsset)
            {
                return false;
            }

            if (string.IsNullOrEmpty(context.DetectedGameKey))
            {
                HoyoToonLogger.ManagerInfo("Auto Setup: Model conversion disabled (no game detected).");
                return false;
            }

            return true;
        }

        public static bool NeedsConversion(HoyoToonModelSetupUtility.SetupContext context, string optionsHash = null)
        {
            if (context == null || !context.IsFbxAsset)
            {
                return false;
            }

            var check = EvaluateConversion(context.AssetPath, optionsHash);
            HoyoToonLogger.ManagerInfo($"Auto Setup: Conversion check => {check.Message}");
            return !check.IsConverted;
        }
        public static bool ShouldApplyImportSettings(HoyoToonModelSetupUtility.SetupContext context)
        {
            if (context == null || !context.IsFbxAsset)
            {
                return false;
            }

            if (string.IsNullOrEmpty(context.DetectedGameKey))
            {
                HoyoToonLogger.ManagerInfo("Auto Setup: Import rules disabled (no game detected).");
                return false;
            }

            return true;
        }

        public static bool NeedsModelImportSettings(HoyoToonModelSetupUtility.SetupContext context)
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
                HoyoToonLogger.ManagerInfo($"Auto Setup: Import rules needed for game '{gameKey}'. Differences: {diffText}");
                return true;
            }

            HoyoToonLogger.ManagerInfo("Auto Setup: Import rules already satisfy defaults. No changes needed.");
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

            var marker = "Hoyo2VRC_Converted";
            var optionsKey = "Hoyo2VRC_OptionsHash_";

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
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private struct ConversionCheck
        {
            public bool IsConverted;
            public bool HasMarker;
            public bool OptionsMatch;
            public string Message;
        }

        private static ConversionCheck EvaluateConversion(string assetPath, string optionsHash)
        {
            bool converted = IsConverted(assetPath, optionsHash, out _);
            return new ConversionCheck
            {
                IsConverted = converted,
                HasMarker = converted,
                OptionsMatch = converted,
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

            bool converted = props.Any(p => p.Equals("Hoyo2VRC_Converted", StringComparison.OrdinalIgnoreCase)
                                         || p.Equals("Hoyo2VRC_Converted=true", StringComparison.OrdinalIgnoreCase));
            if (!converted) return false;

            if (string.IsNullOrEmpty(optionsHash)) return true;

            string hashFlag = $"Hoyo2VRC_OptionsHash_{optionsHash}";
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

            return value.IndexOf("Hoyo2", StringComparison.OrdinalIgnoreCase) >= 0
                && value.IndexOf("Converted", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool TryResolveConvertedAsset(HoyoToonModelSetupUtility.SetupContext context, out GameObject asset, out string assetPath, int lookbackMinutes = 10, bool allowUnmarked = false, string preferredBaseName = null)
        {
            asset = null;
            assetPath = null;

            if (context == null || string.IsNullOrEmpty(context.AssetPath))
            {
                return false;
            }

            var unityPath = context.AssetPath;
            if (IsConverted(unityPath))
            {
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(unityPath);
                if (asset != null)
                {
                    assetPath = unityPath;
                    return true;
                }
            }

            var fullPath = HoyoToonEditorUtil.ToAbsolutePath(unityPath);
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
                var unityPreferred = HoyoToonEditorUtil.ToUnityAssetPath(preferredPath);
                if (!string.IsNullOrEmpty(unityPreferred)
                    && !string.Equals(unityPreferred, unityPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(preferredPath))
                {
                    var preferredAsset = AssetDatabase.LoadAssetAtPath<GameObject>(unityPreferred);
                    if (preferredAsset != null)
                    {
                        asset = preferredAsset;
                        assetPath = unityPreferred;
                        HoyoToonLogger.ManagerInfo($"Auto Setup: Using character-named converted asset '{unityPreferred}'.");
                        return true;
                    }
                }
            }

            DateTime sinceUtc = DateTime.UtcNow.AddMinutes(-Mathf.Abs(lookbackMinutes));
            var candidates = Directory.GetFiles(directory, baseName + "*.fbx", SearchOption.TopDirectoryOnly)
                .Select(path => new { path, time = File.GetLastWriteTimeUtc(path) })
                .Where(entry => entry.time >= sinceUtc)
                .OrderByDescending(entry => entry.time)
                .ToList();

            if (candidates.Count == 0)
            {
                candidates = Directory.GetFiles(directory, baseName + "*.fbx", SearchOption.TopDirectoryOnly)
                    .Select(path => new { path, time = File.GetLastWriteTimeUtc(path) })
                    .OrderByDescending(entry => entry.time)
                    .ToList();
            }

            if (candidates.Count == 0)
            {
                candidates = Directory.GetFiles(directory, "*.fbx", SearchOption.TopDirectoryOnly)
                    .Select(path => new { path, time = File.GetLastWriteTimeUtc(path) })
                    .Where(entry => entry.time >= sinceUtc)
                    .OrderByDescending(entry => entry.time)
                    .ToList();
            }

            if (candidates.Count == 0)
            {
                candidates = Directory.GetFiles(directory, "*.fbx", SearchOption.TopDirectoryOnly)
                    .Select(path => new { path, time = File.GetLastWriteTimeUtc(path) })
                    .OrderByDescending(entry => entry.time)
                    .ToList();
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            foreach (var entry in candidates)
            {
                var unityCandidate = HoyoToonEditorUtil.ToUnityAssetPath(entry.path);
                if (string.IsNullOrEmpty(unityCandidate))
                {
                    continue;
                }

                if (!IsConverted(unityCandidate))
                {
                    var nameOnly = Path.GetFileNameWithoutExtension(unityCandidate);
                    if (string.IsNullOrEmpty(nameOnly)
                        || (nameOnly.IndexOf("Hoyo2", StringComparison.OrdinalIgnoreCase) < 0
                            && nameOnly.IndexOf("Converted", StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        continue;
                    }

                    HoyoToonLogger.ManagerInfo($"Auto Setup: Using name-based converted candidate '{unityCandidate}'.");
                }

                asset = AssetDatabase.LoadAssetAtPath<GameObject>(unityCandidate);
                if (asset == null)
                {
                    continue;
                }

                assetPath = unityCandidate;
                return true;
            }

            if (!allowUnmarked)
            {
                return false;
            }

            foreach (var entry in candidates)
            {
                var unityCandidate = HoyoToonEditorUtil.ToUnityAssetPath(entry.path);
                if (string.IsNullOrEmpty(unityCandidate))
                {
                    continue;
                }

                if (string.Equals(unityCandidate, unityPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var candidateAsset = AssetDatabase.LoadAssetAtPath<GameObject>(unityCandidate);
                if (candidateAsset == null)
                {
                    continue;
                }

                asset = candidateAsset;
                assetPath = unityCandidate;
                HoyoToonLogger.ManagerInfo($"Auto Setup: Using newest converted candidate '{unityCandidate}'.");
                return true;
            }

            return false;
        }
    }
}
#endif
