using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.UI.Dialogs;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEngine;

namespace HoyoToon.Editor.Detection.Character
{
    public static class CharacterProblemPrompt
    {
        private const int MaxDisplayedProblems = 5;

        public static bool ConfirmAssetContext(string assetPath, string actionName, UnityEngine.Object context = null)
        {
            return ConfirmAssetContexts(new[] { assetPath }, actionName, context);
        }

        public static bool ConfirmAssetContexts(IEnumerable<string> assetPaths, string actionName, UnityEngine.Object context = null)
        {
            List<ProblemPromptMatch> matches = CollectProblemMatches(assetPaths).ToList();
            if (matches.Count <= 0)
            {
                return true;
            }

            string resolvedActionName = string.IsNullOrWhiteSpace(actionName) ? "this action" : actionName.Trim();
            string message = BuildPromptMessage(matches, resolvedActionName);

            HoyoToonLogger.Warning(
                HoyoToonLogCategory.Detection,
                $"Known character problem detected before '{resolvedActionName}'. Waiting for user confirmation.",
                context: context);

            if (Application.isBatchMode)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Detection,
                    $"Known character problem prompt for '{resolvedActionName}' was skipped because Unity is running in batch mode. Continuing.",
                    context: context);
                return true;
            }

            return HoyoToonDialog.DisplayDialog(
                matches.Count == 1 ? "Known Character Problem" : "Known Character Problems",
                message,
                "Continue",
                "Cancel");
        }

        private static IEnumerable<ProblemPromptMatch> CollectProblemMatches(IEnumerable<string> assetPaths)
        {
            var seenMatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string assetPath in assetPaths ?? Enumerable.Empty<string>())
            {
                string normalizedAssetPath = NormalizeAssetPath(assetPath);
                if (string.IsNullOrWhiteSpace(normalizedAssetPath))
                {
                    continue;
                }

                if (!GameDetector.TryDetectGameFromAssetContext(normalizedAssetPath, out GameConfigSO game, out string matchedJsonAssetPath)
                    || game == null)
                {
                    continue;
                }

                ProblemPromptMatch match = TryCreateMatch(game, normalizedAssetPath, normalizedAssetPath)
                    ?? TryCreateMatch(game, matchedJsonAssetPath, normalizedAssetPath);
                if (match == null)
                {
                    continue;
                }

                string matchKey = $"{match.AssetPath}|{match.GameKey}|{match.EntryName}";
                if (seenMatches.Add(matchKey))
                {
                    yield return match;
                }
            }
        }

        private static ProblemPromptMatch TryCreateMatch(GameConfigSO game, string detectionAssetPath, string selectedAssetPath)
        {
            if (game == null || string.IsNullOrWhiteSpace(detectionAssetPath))
            {
                return null;
            }

            if (!CharacterNameDetector.TryFindProblemEntry(
                game.Key,
                detectionAssetPath,
                out GameProblemEntryData entry,
                out string characterName)
                || entry == null)
            {
                return null;
            }

            return new ProblemPromptMatch(
                game.Key,
                string.IsNullOrWhiteSpace(characterName) ? entry.Name : characterName,
                selectedAssetPath,
                entry);
        }

        private static string BuildPromptMessage(IReadOnlyList<ProblemPromptMatch> matches, string actionName)
        {
            var builder = new StringBuilder();
            if (matches.Count == 1)
            {
                ProblemPromptMatch match = matches[0];
                builder.Append("HoyoToon detected a known problem for this character before ");
                builder.Append(actionName);
                builder.AppendLine(".");
                builder.AppendLine();
                AppendMatch(builder, match, includeIndex: false, index: 0);
                builder.AppendLine();
                builder.Append("Do you want to continue?");
                return builder.ToString();
            }

            builder.Append("HoyoToon detected known problems for ");
            builder.Append(matches.Count);
            builder.Append(" selected character");
            builder.Append(matches.Count == 1 ? string.Empty : "s");
            builder.Append(" before ");
            builder.Append(actionName);
            builder.AppendLine(".");
            builder.AppendLine();

            int displayCount = Math.Min(matches.Count, MaxDisplayedProblems);
            for (int i = 0; i < displayCount; i++)
            {
                AppendMatch(builder, matches[i], includeIndex: true, index: i + 1);
                builder.AppendLine();
            }

            int remainingCount = matches.Count - displayCount;
            if (remainingCount > 0)
            {
                builder.Append("And ");
                builder.Append(remainingCount);
                builder.Append(" more known problem");
                builder.Append(remainingCount == 1 ? string.Empty : "s");
                builder.AppendLine(".");
                builder.AppendLine();
            }

            builder.Append("Do you want to continue?");
            return builder.ToString();
        }

        private static void AppendMatch(StringBuilder builder, ProblemPromptMatch match, bool includeIndex, int index)
        {
            if (includeIndex)
            {
                builder.Append(index);
                builder.Append(". ");
            }

            builder.Append("Character: ");
            builder.AppendLine(string.IsNullOrWhiteSpace(match.CharacterName) ? "Unknown" : match.CharacterName);
            builder.Append("Game: ");
            builder.AppendLine(string.IsNullOrWhiteSpace(match.GameKey) ? "Unknown" : match.GameKey);

            if (!string.IsNullOrWhiteSpace(match.Message))
            {
                builder.AppendLine("Problem:");
                builder.AppendLine(match.Message.Trim());
            }
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? null
                : assetPath.Replace('\\', '/');
        }

        private sealed class ProblemPromptMatch
        {
            public ProblemPromptMatch(string gameKey, string characterName, string assetPath, GameProblemEntryData entry)
            {
                GameKey = gameKey ?? string.Empty;
                CharacterName = characterName ?? string.Empty;
                AssetPath = NormalizeAssetPath(assetPath) ?? string.Empty;
                EntryName = entry?.Name ?? string.Empty;
                Message = entry?.Message ?? string.Empty;
                Type = entry?.Type ?? string.Empty;
            }

            public string GameKey { get; }

            public string CharacterName { get; }

            public string AssetPath { get; }

            public string EntryName { get; }

            public string Message { get; }

            public string Type { get; }
        }
    }
}
