#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Setup;
using HoyoToon.Editor.AssetPipeline.Models;
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

        public static List<string> CollectKnownModelAssetPaths(
            IEnumerable<UnityEngine.Object> selectedAssets,
            IEnumerable<string> convertedModelAssetPaths)
        {
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddModelAssetPaths(uniquePaths, selectedAssets);

            foreach (string convertedModelAssetPath in convertedModelAssetPaths ?? Enumerable.Empty<string>())
            {
                if (IsModelAssetPath(convertedModelAssetPath))
                {
                    uniquePaths.Add(NormalizeAssetPath(convertedModelAssetPath));
                }
            }

            return uniquePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? null
                : assetPath.Replace('\\', '/');
        }

        public static void ConvertSelectedModels(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null || context.SelectedFbxAssetPaths.Count <= 0)
            {
                return;
            }

            ModelConverter.Initialize();

            for (int i = 0; i < context.SelectedFbxAssetPaths.Count; i++)
            {
                string fbxAssetPath = context.SelectedFbxAssetPaths[i];
                UnityEngine.Object fbxAsset = AssetDatabase.LoadMainAssetAtPath(fbxAssetPath);
                if (fbxAsset == null)
                {
                    result.RecordWarning($"Auto setup could not load the FBX asset at '{fbxAssetPath}'.");
                    continue;
                }

                if (!ModelConverter.TryProcessAndGetOutput(
                    fbxAsset,
                    out string outputAssetPath,
                    out _,
                    promptForKnownCharacterProblems: false))
                {
                    result.RecordWarning($"Auto setup failed to convert '{fbxAssetPath}'. Check the Console for converter details.");
                    continue;
                }

                result.ModelsConverted++;
                context.RegisterConvertedModelAssetPath(outputAssetPath);
            }
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
            IEnumerable<string> sourcePaths = context.ConvertedModelAssetPaths.Count > 0
                ? context.ConvertedModelAssetPaths
                : context.ModelAssetPaths;

            foreach (string sourcePath in sourcePaths)
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
    }
}
#endif
