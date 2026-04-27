using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetQueryUtility;

namespace HoyoToon.Editor.Detection.Game
{
    public static class GameRegistry
    {
        private const string GameConfigAssetSuffix = "/Config/GameConfig.asset";

        private static List<GameConfigSO> gameConfigs = new List<GameConfigSO>();
        private static bool isInitialized;

        public static IReadOnlyList<GameConfigSO> GameConfigs
        {
            get
            {
                EnsureInitialized();
                return gameConfigs;
            }
        }

        public static void Initialize()
        {
            gameConfigs = LoadGeneratedAssets<GameConfigSO>("t:GameConfigSO", GameConfigAssetSuffix)
                .Where(config => config != null && !string.IsNullOrWhiteSpace(config.Key))
                .OrderBy(config => config.Key, StringComparer.Ordinal)
                .ToList();

            isInitialized = true;
            HoyoToonLogger.Verbose(HoyoToonLogCategory.Detection, $"Loaded {gameConfigs.Count} game definition(s) into the game registry.");
        }

        public static bool TryGetGame(string key, out GameConfigSO gameConfig)
        {
            EnsureInitialized();
            gameConfig = gameConfigs.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
            return gameConfig != null;
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