#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoyoToon.Editor.AssetPipeline.Models;
using HoyoToon.Editor.Detection.Character;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Setup;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.IO;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Utilities.AutoSetup
{
    internal static class AutoSetupModelsUtility
    {
        public static List<string> CollectSelectedAssetPaths(IEnumerable<UnityEngine.Object> selectedAssets)
        {
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object selectedAsset in selectedAssets ?? Enumerable.Empty<UnityEngine.Object>())
            {
                string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(selectedAsset));
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    uniquePaths.Add(assetPath);
                }
            }

            return uniquePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<string> CollectSelectedFbxAssetPaths(IEnumerable<UnityEngine.Object> selectedAssets)
        {
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object selectedAsset in selectedAssets ?? Enumerable.Empty<UnityEngine.Object>())
            {
                string selectedAssetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(selectedAsset));
                if (string.IsNullOrWhiteSpace(selectedAssetPath))
                {
                    continue;
                }

                if (selectedAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    uniquePaths.Add(selectedAssetPath);
                    continue;
                }

                if (!AssetDatabase.IsValidFolder(selectedAssetPath))
                {
                    continue;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { selectedAssetPath }))
                {
                    string assetPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (!string.IsNullOrWhiteSpace(assetPath)
                        && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    {
                        uniquePaths.Add(assetPath);
                    }
                }
            }

            return uniquePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<string> CollectKnownModelAssetPaths(IEnumerable<UnityEngine.Object> selectedAssets)
        {
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddModelAssetPaths(uniquePaths, selectedAssets);

            return uniquePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? null
                : EditorPathUtility.NormalizeAssetPath(assetPath);
        }

        public static void ApplyDetectedImportSettings(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null)
            {
                return;
            }

            ModelImportSettingsApplicator.Initialize();

            IReadOnlyList<string> targetModelAssetPaths = CollectTargetModelAssetPaths(context);
            for (int i = 0; i < targetModelAssetPaths.Count; i++)
            {
                string modelAssetPath = targetModelAssetPaths[i];
                ModelImportSettingsApplyOutcome outcome = ModelImportSettingsApplicator.ApplyDetected(modelAssetPath);
                switch (outcome)
                {
                    case ModelImportSettingsApplyOutcome.Applied:
                        result.ModelImportSettingsApplied++;
                        break;

                    case ModelImportSettingsApplyOutcome.Failed:
                        result.RecordWarning($"Applying detected model import settings failed for '{modelAssetPath}'.");
                        break;
                }
            }
        }

        public static void RenameDetectedModels(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null)
            {
                return;
            }

            bool renamedAny = false;
            IReadOnlyList<string> targetModelAssetPaths = CollectTargetModelAssetPaths(context);
            for (int i = 0; i < targetModelAssetPaths.Count; i++)
            {
                string modelAssetPath = targetModelAssetPaths[i];
                if (!modelAssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string displayName = ResolveDetectedModelName(context, modelAssetPath);
                string safeDisplayName = SanitizeFileName(displayName);
                if (string.IsNullOrWhiteSpace(safeDisplayName)
                    || string.Equals(Path.GetFileNameWithoutExtension(modelAssetPath), safeDisplayName, StringComparison.Ordinal))
                {
                    continue;
                }

                string error = AssetDatabase.RenameAsset(modelAssetPath, safeDisplayName);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    result.RecordWarning($"Auto setup could not rename '{modelAssetPath}' to '{safeDisplayName}': {error}");
                    continue;
                }

                renamedAny = true;
            }

            if (!renamedAny)
            {
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            context.Refresh();
        }

        public static string ResolveDetectedModelName(AutoSetupContext context, string modelAssetPath)
        {
            string gameKey = context?.DetectedGameKey;
            string matchedJsonAssetPath = context?.MatchedJsonAssetPath;

            if (GameDetector.TryDetectGameFromAssetContext(modelAssetPath, out GameConfigSO detectedGame, out string detectedJsonAssetPath)
                && detectedGame != null)
            {
                gameKey = detectedGame.Key;
                matchedJsonAssetPath = detectedJsonAssetPath;
            }

            if (string.IsNullOrWhiteSpace(gameKey))
            {
                return null;
            }

            string displayName = CharacterNameDetector.TryExtractCharacterName(gameKey, modelAssetPath);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return string.IsNullOrWhiteSpace(matchedJsonAssetPath)
                ? null
                : CharacterNameDetector.TryExtractCharacterName(gameKey, matchedJsonAssetPath);
        }

        public static void CacheCharacterIcons(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null || string.IsNullOrWhiteSpace(context.DetectedGameKey))
            {
                return;
            }

            IReadOnlyList<string> targetModelAssetPaths = CollectTargetModelAssetPaths(context);
            for (int i = 0; i < targetModelAssetPaths.Count; i++)
            {
                string modelAssetPath = targetModelAssetPaths[i];
                string characterName = ResolveDetectedModelName(context, modelAssetPath);
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    result.RecordWarning($"Auto setup could not resolve a character name for '{modelAssetPath}'.");
                    continue;
                }

                if (!CharacterIconDetector.TryResolveCharacterIconUrls(
                    context.DetectedGameKey,
                    characterName,
                    out string characterId,
                    out string avatarIconUrl,
                    out string roundIconUrl,
                    out string splashIconUrl)
                    || string.IsNullOrWhiteSpace(splashIconUrl))
                {
                    result.RecordWarning($"Auto setup detected '{characterName}' for '{modelAssetPath}', but no catalog splash icon URL was available.");
                    continue;
                }

                CharacterIconCacheUtility.TryEnsureCharacterIconsCached(
                    modelAssetPath,
                    context.DetectedGameKey,
                    characterId,
                    avatarIconUrl,
                    roundIconUrl,
                    splashIconUrl,
                    out _);

                if (CharacterIconCacheUtility.TryGetCachedCharacterIconPath(
                    modelAssetPath,
                    null,
                    null,
                    splashIconUrl,
                    out _))
                {
                    result.CharacterIconsCached++;
                    continue;
                }

                result.RecordWarning($"Auto setup detected '{characterName}' for '{modelAssetPath}', but the splash icon could not be cached.");
            }
        }

        public static void ApplyTangents(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null)
            {
                return;
            }

            TangentSettingsApplicator.Initialize();

            if (context.InstantiatedModels.Count > 0)
            {
                for (int i = 0; i < context.InstantiatedModels.Count; i++)
                {
                    GameObject instantiatedModel = context.InstantiatedModels[i];
                    if (instantiatedModel == null)
                    {
                        continue;
                    }

                    TangentSettingsApplyOutcome outcome = TangentSettingsApplicator.Apply(instantiatedModel);
                    if (outcome == TangentSettingsApplyOutcome.Applied)
                    {
                        result.TangentApplications++;
                    }
                    else if (outcome == TangentSettingsApplyOutcome.Failed)
                    {
                        result.RecordWarning($"Applying tangent settings failed for instantiated model '{instantiatedModel.name}'.");
                    }
                }

                return;
            }

            IReadOnlyList<string> targetModelAssetPaths = CollectTargetModelAssetPaths(context);
            for (int i = 0; i < targetModelAssetPaths.Count; i++)
            {
                string modelAssetPath = targetModelAssetPaths[i];
                TangentSettingsApplyOutcome outcome = TangentSettingsApplicator.ApplyDetected(modelAssetPath);
                if (outcome == TangentSettingsApplyOutcome.Applied)
                {
                    result.TangentApplications++;
                }
                else if (outcome == TangentSettingsApplyOutcome.Failed)
                {
                    result.RecordWarning($"Applying tangent settings failed for '{modelAssetPath}'.");
                }
            }
        }

        private static void AddModelAssetPaths(HashSet<string> uniquePaths, IEnumerable<UnityEngine.Object> selectedAssets)
        {
            foreach (UnityEngine.Object selectedAsset in selectedAssets ?? Enumerable.Empty<UnityEngine.Object>())
            {
                string selectedAssetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(selectedAsset));
                if (string.IsNullOrWhiteSpace(selectedAssetPath))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(selectedAssetPath))
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { selectedAssetPath }))
                    {
                        string assetPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                        if (IsModelAssetPath(assetPath))
                        {
                            uniquePaths.Add(assetPath);
                        }
                    }

                    continue;
                }

                if (IsModelAssetPath(selectedAssetPath))
                {
                    uniquePaths.Add(selectedAssetPath);
                }
            }
        }

        private static bool IsModelAssetPath(string assetPath)
        {
            string normalizedAssetPath = NormalizeAssetPath(assetPath);
            return !string.IsNullOrWhiteSpace(normalizedAssetPath)
                && !AssetDatabase.IsValidFolder(normalizedAssetPath)
                && AssetDatabase.LoadAssetAtPath<GameObject>(normalizedAssetPath) != null;
        }

        private static IReadOnlyList<string> CollectTargetModelAssetPaths(AutoSetupContext context)
        {
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string sourcePath in context.ModelAssetPaths)
            {
                if (IsModelAssetPath(sourcePath))
                {
                    uniquePaths.Add(NormalizeAssetPath(sourcePath));
                }
            }

            return uniquePaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            char[] characters = value.Trim().ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                if (invalidCharacters.Contains(characters[i]))
                {
                    characters[i] = '_';
                }
            }

            return new string(characters).TrimEnd('.', ' ');
        }
    }
}
#endif
