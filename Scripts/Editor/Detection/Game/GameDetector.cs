using System;
using System.Collections.Generic;
using HoyoToon.Editor.Detection.Character;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.IO;
using HoyoToon.Runtime.ScriptableObjects.Games;

namespace HoyoToon.Editor.Detection.Game
{
    public static class GameDetector
    {
        public static GameConfigSO DetectGame(MaterialJson materialJson)
        {
            if (materialJson?.DetectionContext == null)
            {
                return null;
            }

            GameConfigSO matchedGame = null;

            foreach (GameConfigSO game in GameRegistry.GameConfigs)
            {
                if (GetMatchedProperties(game, materialJson.DetectionContext).Count > 0)
                {
                    if (matchedGame != null)
                    {
                        HoyoToonLogger.Warning(
                            HoyoToonLogCategory.Detection,
                            $"Game detection was ambiguous between '{matchedGame.Key}' and '{game.Key}'.");
                        return null;
                    }

                    matchedGame = game;
                }
            }

            return matchedGame;
        }

        public static bool TryDetectGame(string rawJson, out GameConfigSO game)
        {
            game = null;

            if (!MaterialJsonParser.TryParse(rawJson, out MaterialJson materialJson))
            {
                return false;
            }

            game = DetectGame(materialJson);
            return game != null;
        }

        public static bool TryDetectGame(string rawJson, string contextAssetPath, out GameConfigSO game, out string characterName)
        {
            characterName = null;
            if (!TryDetectGame(rawJson, out game) || game == null)
            {
                return false;
            }

            characterName = CharacterNameDetector.TryExtractCharacterName(game.Key, contextAssetPath);
            return true;
        }

        public static bool TryDetectGameFromAssetContext(string contextAssetPath, out GameConfigSO game, out string matchedJsonAssetPath)
        {
            game = null;
            matchedJsonAssetPath = null;

            if (!AssetContextJsonQueryUtility.TryResolveSearchRootDirectory(contextAssetPath, out _))
            {
                return false;
            }

            GameConfigSO matchedGame = null;
            string firstMatchedJsonAssetPath = null;

            foreach (string jsonAssetPath in AssetContextJsonQueryUtility.EnumerateJsonAssetPaths(contextAssetPath))
            {
                if (!TextFileUtility.TryReadAllText(AssetContextJsonQueryUtility.ToAbsolutePath(jsonAssetPath), out string rawJson))
                {
                    continue;
                }

                if (!TryDetectGame(rawJson, out GameConfigSO detectedGame) || detectedGame == null)
                {
                    continue;
                }

                if (matchedGame != null && !string.Equals(matchedGame.Key, detectedGame.Key, StringComparison.Ordinal))
                {
                    HoyoToonLogger.Warning(
                        HoyoToonLogCategory.Detection,
                        $"Game detection was ambiguous for '{contextAssetPath}' between '{matchedGame.Key}' from '{firstMatchedJsonAssetPath}' and '{detectedGame.Key}' from '{jsonAssetPath}'.");
                    return false;
                }

                matchedGame = detectedGame;
                firstMatchedJsonAssetPath ??= jsonAssetPath;
            }

            game = matchedGame;
            matchedJsonAssetPath = firstMatchedJsonAssetPath;

            if (game == null)
            {
                return false;
            }

            return true;
        }

        public static IReadOnlyList<string> GetMatchedProperties(GameConfigSO game, MaterialJson materialJson)
        {
            return GetMatchedProperties(game, materialJson?.DetectionContext);
        }

        private static IReadOnlyList<string> GetMatchedProperties(GameConfigSO game, MaterialDetectionContext detectionContext)
        {
            if (game == null || detectionContext == null || game.GameProperties == null || game.GameProperties.Count <= 0)
            {
                return Array.Empty<string>();
            }

            var matchedProperties = new List<string>(game.GameProperties.Count);
            foreach (string property in game.GameProperties)
            {
                if (!string.IsNullOrWhiteSpace(property) && detectionContext.ContainsIndicator(property))
                {
                    matchedProperties.Add(property);
                }
            }

            return matchedProperties;
        }
    }
}
