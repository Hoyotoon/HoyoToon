using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetQueryUtility;

namespace HoyoToon.Editor.Detection.Shader
{
    public static class ShaderRegistry
    {
        private const string GameShaderKeywordsAssetSuffix = "/Config/GameShaderKeywords.asset";

        private static Dictionary<string, List<GameShaderKeywordsSO.Entry>> shaderEntriesByGame =
            new Dictionary<string, List<GameShaderKeywordsSO.Entry>>(StringComparer.Ordinal);

        private static bool isInitialized;

        public static IReadOnlyList<GameShaderKeywordsSO.Entry> GetShaderEntries(string gameKey)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(gameKey))
            {
                return Array.Empty<GameShaderKeywordsSO.Entry>();
            }

            return shaderEntriesByGame.TryGetValue(gameKey, out List<GameShaderKeywordsSO.Entry> entries)
                ? entries
                : Array.Empty<GameShaderKeywordsSO.Entry>();
        }

        public static void Initialize()
        {
            shaderEntriesByGame = LoadGeneratedAssets<GameShaderKeywordsSO>("t:GameShaderKeywordsSO", GameShaderKeywordsAssetSuffix)
                .SelectMany(asset => asset.Entries ?? Array.Empty<GameShaderKeywordsSO.Entry>())
                .Where(entry => entry != null
                    && !string.IsNullOrWhiteSpace(entry.GameKey)
                    && !string.IsNullOrWhiteSpace(entry.ShaderPath))
                .GroupBy(entry => entry.GameKey, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(entry => entry.ShaderPath, StringComparer.Ordinal)
                        .ToList(),
                    StringComparer.Ordinal);

            isInitialized = true;
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Detection,
                $"Loaded {shaderEntriesByGame.Sum(pair => pair.Value.Count)} shader keyword entry/entries across {shaderEntriesByGame.Count} game(s).");
        }

        private static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }
    }
}