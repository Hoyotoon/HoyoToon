using System;
using HoyoToon.Editor.Detection.Character;
using HoyoToon.Runtime.ScriptableObjects.Games;
using HoyoToon.Editor.Detection.Game;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HoyoToon.Editor.Utilities.Debugging;

namespace HoyoToon.Editor.Detection.Shader
{
    public static class ShaderDetector
    {
        public enum ShaderMatchSource
        {
            RuleMatch,
            AssetFallback,
            GameDefaultShader,
        }

        public static string DetectShader(GameConfigSO game, MaterialJson materialJson, string rawJson = null)
        {
            _ = TryDetectShader(game, materialJson, rawJson, out string shaderPath, out _);
            return shaderPath;
        }

        public static bool TryDetectShader(GameConfigSO game, MaterialJson materialJson, string rawJson, out string shaderPath, out ShaderMatchSource source)
        {
            return TryDetectShader(game, materialJson, rawJson, out shaderPath, out source, out _, out _);
        }

        public static bool TryDetectShader(
            GameConfigSO game,
            MaterialJson materialJson,
            string rawJson,
            out string shaderPath,
            out ShaderMatchSource source,
            out IReadOnlyList<string> lookedForKeywords,
            out IReadOnlyList<string> matchedKeywords)
        {
            shaderPath = null;
            source = ShaderMatchSource.RuleMatch;
            lookedForKeywords = Array.Empty<string>();
            matchedKeywords = Array.Empty<string>();

            if (game == null || materialJson == null)
            {
                return false;
            }

            GameShaderKeywordsSO.Entry fallbackEntry = null;
            string bestShaderPath = null;
            int bestMatchCount = 0;
            IReadOnlyList<string> bestLookedForKeywords = Array.Empty<string>();
            IReadOnlyList<string> bestMatchedKeywords = Array.Empty<string>();

            foreach (GameShaderKeywordsSO.Entry entry in ShaderRegistry.GetShaderEntries(game.Key))
            {
                IReadOnlyList<string> entryKeywords = GetNormalizedKeywords(entry);
                if (entryKeywords.Count <= 0)
                {
                    fallbackEntry ??= entry;
                    continue;
                }

                IReadOnlyList<string> entryMatchedKeywords = GetMatchedKeywords(entryKeywords, materialJson);
                int matchCount = entryMatchedKeywords.Count;
                if (matchCount <= 0)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.ShaderPath) && matchCount > bestMatchCount)
                {
                    bestShaderPath = entry.ShaderPath;
                    bestMatchCount = matchCount;
                    bestLookedForKeywords = entryKeywords;
                    bestMatchedKeywords = entryMatchedKeywords;
                }
            }

            if (!string.IsNullOrWhiteSpace(bestShaderPath))
            {
                shaderPath = bestShaderPath;
                source = ShaderMatchSource.RuleMatch;
                lookedForKeywords = bestLookedForKeywords;
                matchedKeywords = bestMatchedKeywords;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fallbackEntry?.ShaderPath))
            {
                shaderPath = fallbackEntry.ShaderPath;
                source = ShaderMatchSource.AssetFallback;
                lookedForKeywords = GetNormalizedKeywords(fallbackEntry);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(game.DefaultShader))
            {
                shaderPath = game.DefaultShader;
                source = ShaderMatchSource.GameDefaultShader;
                return true;
            }

            return false;
        }

        public static bool TryDetectShader(GameConfigSO game, string rawJson, out string shaderPath)
        {
            return TryDetectShader(game, rawJson, out shaderPath, out _);
        }

        public static bool TryDetectShader(GameConfigSO game, string rawJson, out string shaderPath, out ShaderMatchSource source)
        {
            shaderPath = null;
            source = ShaderMatchSource.RuleMatch;

            if (game == null || !MaterialJsonParser.TryParse(rawJson, out MaterialJson materialJson))
            {
                return false;
            }

            return TryDetectShader(game, materialJson, rawJson, out shaderPath, out source, out _, out _);
        }

        public static bool TryDetectShader(string rawJson, out GameConfigSO game, out string shaderPath)
        {
            return TryDetectShader(rawJson, out game, out shaderPath, out _);
        }

        public static bool TryDetectShader(string rawJson, out GameConfigSO game, out string shaderPath, out ShaderMatchSource source)
        {
            game = null;
            shaderPath = null;
            source = ShaderMatchSource.RuleMatch;

            if (!MaterialJsonParser.TryParse(rawJson, out MaterialJson materialJson))
            {
                return false;
            }

            game = GameDetector.DetectGame(materialJson);
            if (game == null)
            {
                return false;
            }

            return TryDetectShader(game, materialJson, rawJson, out shaderPath, out source, out _, out _);
        }

        public static bool TryDetectShader(
            string rawJson,
            string contextAssetPath,
            out GameConfigSO game,
            out string shaderPath,
            out string characterName,
            out ShaderMatchSource source)
        {
            game = null;
            shaderPath = null;
            characterName = null;
            source = ShaderMatchSource.RuleMatch;

            if (!MaterialJsonParser.TryParse(rawJson, out MaterialJson materialJson))
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Detection,
                    $"Detection result: failed to parse material JSON.\nJson: {contextAssetPath ?? "-"}\nMessage: Failed to parse material JSON.");
                return false;
            }

            game = GameDetector.DetectGame(materialJson);
            if (game == null)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Detection,
                    $"Detection result: no matching game detected.\nJson: {contextAssetPath ?? "-"}\nMessage: No matching game detected.");
                return false;
            }

            characterName = CharacterNameDetector.TryExtractCharacterName(game.Key, contextAssetPath);

            if (TryDetectShader(game, materialJson, rawJson, out shaderPath, out source, out _, out _))
            {
                return true;
            }

            IReadOnlyList<string> matchedGameProperties = GameDetector.GetMatchedProperties(game, materialJson);
            string lookedForGamePropertiesText = game.GameProperties == null
                ? null
                : string.Join(", ", game.GameProperties.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));
            string matchedGamePropertiesText = matchedGameProperties == null
                ? null
                : string.Join(", ", matchedGameProperties.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));

            var shaderMissMessageBuilder = new StringBuilder();
            shaderMissMessageBuilder.AppendLine("Detection result: game detected, but no matching shader was detected.");
            shaderMissMessageBuilder.AppendLine($"Json: {contextAssetPath ?? "-"}");
            shaderMissMessageBuilder.AppendLine($"Game: {game.Key}");
            shaderMissMessageBuilder.AppendLine($"Character: {characterName ?? "-"}");
            shaderMissMessageBuilder.AppendLine($"Looked For (Game): {lookedForGamePropertiesText ?? "-"}");
            shaderMissMessageBuilder.AppendLine($"Matched (Game): {matchedGamePropertiesText ?? "-"}");
            shaderMissMessageBuilder.AppendLine("Message: Game detected, but no matching shader was detected.");
            HoyoToonLogger.Warning(HoyoToonLogCategory.Detection, shaderMissMessageBuilder.ToString());
            return false;
        }

        private static IReadOnlyList<string> GetMatchedKeywords(IReadOnlyList<string> keywords, MaterialJson materialJson)
        {
            if (keywords == null || keywords.Count <= 0)
            {
                return Array.Empty<string>();
            }

            var matchedKeywords = new List<string>();
            foreach (string keyword in keywords)
            {
                if (MatchesKeyword(materialJson, keyword))
                {
                    matchedKeywords.Add(keyword);
                }
            }

            return matchedKeywords;
        }

        private static IReadOnlyList<string> GetNormalizedKeywords(GameShaderKeywordsSO.Entry entry)
        {
            IReadOnlyList<string> keywords = entry?.Keywords;
            if (keywords == null || keywords.Count <= 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(keywords.Count);
            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    result.Add(keyword);
                }
            }

            return result;
        }

        private static bool MatchesKeyword(MaterialJson materialJson, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            if (TryParseCondition(keyword, out string propertyName, out string op, out string expectedValue))
            {
                return materialJson?.DetectionContext != null && materialJson.DetectionContext.MatchesCondition(propertyName, op, expectedValue);
            }

            return materialJson?.DetectionContext != null && materialJson.DetectionContext.ContainsIndicator(keyword);
        }

        private static bool TryParseCondition(string keyword, out string propertyName, out string op, out string expectedValue)
        {
            propertyName = null;
            op = null;
            expectedValue = null;

            foreach (string candidate in SupportedOperators)
            {
                int operatorIndex = keyword.IndexOf(candidate, StringComparison.Ordinal);
                if (operatorIndex <= 0)
                {
                    continue;
                }

                string left = keyword.Substring(0, operatorIndex).Trim();
                string right = keyword.Substring(operatorIndex + candidate.Length).Trim();
                if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                {
                    return false;
                }

                propertyName = left;
                op = candidate;
                expectedValue = right;
                return true;
            }

            return false;
        }

        private static readonly string[] SupportedOperators = { ">=", "<=", "!=", "==", ">", "<", "=" };
    }
}
