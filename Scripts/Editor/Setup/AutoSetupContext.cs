#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Utilities.AutoSetup;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using UnityEngine;

namespace HoyoToon.Editor.Setup
{
    internal sealed class AutoSetupContext
    {
        private readonly List<UnityEngine.Object> selectedAssets = new List<UnityEngine.Object>();
        private readonly List<string> selectedAssetPaths = new List<string>();
        private readonly List<string> selectedFbxAssetPaths = new List<string>();
        private readonly List<string> modelAssetPaths = new List<string>();
        private readonly List<string> convertedModelAssetPaths = new List<string>();
        private readonly List<GameObject> instantiatedModels = new List<GameObject>();

        public AutoSetupContext(AutoSetupOptions options = null)
        {
            Options = options ?? new AutoSetupOptions();
            Refresh();
        }

        public AutoSetupOptions Options { get; }

        public IReadOnlyList<UnityEngine.Object> SelectedAssets => selectedAssets;

        public IReadOnlyList<string> SelectedAssetPaths => selectedAssetPaths;

        public IReadOnlyList<string> SelectedFbxAssetPaths => selectedFbxAssetPaths;

        public IReadOnlyList<string> ModelAssetPaths => modelAssetPaths;

        public IReadOnlyList<string> ConvertedModelAssetPaths => convertedModelAssetPaths;

        public IReadOnlyList<GameObject> InstantiatedModels => instantiatedModels;

        public GameConfigSO DetectedGame { get; private set; }

        public string DetectedGameKey => DetectedGame != null ? DetectedGame.Key : null;

        public string MatchedJsonAssetPath { get; private set; }

        public void Refresh()
        {
            AutoSetup.RefreshContext(this);
        }

        internal void RefreshInternal()
        {
            selectedAssets.Clear();
            selectedAssets.AddRange(
                Selection.objects
                    .Where(candidate => candidate != null && AssetDatabase.Contains(candidate))
                    .Distinct());

            selectedAssetPaths.Clear();
            selectedAssetPaths.AddRange(AutoSetupModelsUtility.CollectSelectedAssetPaths(selectedAssets));

            selectedFbxAssetPaths.Clear();
            selectedFbxAssetPaths.AddRange(AutoSetupModelsUtility.CollectSelectedFbxAssetPaths(selectedAssets));

            modelAssetPaths.Clear();
            modelAssetPaths.AddRange(
                AutoSetupModelsUtility.CollectKnownModelAssetPaths(
                    selectedAssets,
                    convertedModelAssetPaths));

            ResolveDetectedGame();
        }

        internal void RegisterConvertedModelAssetPath(string assetPath)
        {
            string normalizedAssetPath = AutoSetupModelsUtility.NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(normalizedAssetPath))
            {
                return;
            }

            bool alreadyTracked = convertedModelAssetPaths.Any(
                candidate => string.Equals(candidate, normalizedAssetPath, StringComparison.OrdinalIgnoreCase));
            if (!alreadyTracked)
            {
                convertedModelAssetPaths.Add(normalizedAssetPath);
            }

            bool modelAlreadyTracked = modelAssetPaths.Any(
                candidate => string.Equals(candidate, normalizedAssetPath, StringComparison.OrdinalIgnoreCase));
            if (!modelAlreadyTracked)
            {
                modelAssetPaths.Add(normalizedAssetPath);
                modelAssetPaths.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }

        internal void RegisterInstantiatedModel(GameObject model)
        {
            if (model == null)
            {
                return;
            }

            bool alreadyTracked = instantiatedModels.Any(candidate => candidate == model);
            if (!alreadyTracked)
            {
                instantiatedModels.Add(model);
            }
        }

        private void ResolveDetectedGame()
        {
            DetectedGame = null;
            MatchedJsonAssetPath = null;

            var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidatePath in EnumerateDetectionCandidatePaths())
            {
                if (string.IsNullOrWhiteSpace(candidatePath) || !visitedPaths.Add(candidatePath))
                {
                    continue;
                }

                if (GameDetector.TryDetectGameFromAssetContext(candidatePath, out GameConfigSO detectedGame, out string matchedJsonAssetPath)
                    && detectedGame != null)
                {
                    DetectedGame = detectedGame;
                    MatchedJsonAssetPath = matchedJsonAssetPath;
                    return;
                }
            }
        }

        private IEnumerable<string> EnumerateDetectionCandidatePaths()
        {
            for (int i = 0; i < modelAssetPaths.Count; i++)
            {
                yield return modelAssetPaths[i];
            }

            for (int i = 0; i < selectedAssetPaths.Count; i++)
            {
                yield return selectedAssetPaths[i];
            }
        }
    }
}
#endif
