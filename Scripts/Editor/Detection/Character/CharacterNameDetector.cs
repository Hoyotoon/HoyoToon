using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.Parsing;
using HoyoToon.Runtime.ScriptableObjects;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;

namespace HoyoToon.Editor.Detection.Character
{
	public static class CharacterNameDetector
	{
		private const string ProblemListsAssetFileName = "GameProblemLists.asset";

		public static string TryExtractCharacterName(string gameKey, string contextAssetPath)
		{
			try
			{
				if (!TryResolveProblemListContext(gameKey, contextAssetPath, out GameProblemListsSO problemList, out _))
				{
					return null;
				}

				return TryExtractCharacterNameFromAssetPath(problemList, contextAssetPath);
			}
			catch (Exception exception)
			{
				HoyoToonLogger.Warning(
					HoyoToonLogCategory.Detection,
					$"Character name detection failed for '{contextAssetPath}' in game '{gameKey}'. Continuing without a detected character name.",
					exception);
				return null;
			}
		}

		public static bool TryFindProblemEntry(
			string gameKey,
			string contextAssetPath,
			out GameProblemEntryData problemEntry,
			out string characterName)
		{
			problemEntry = null;
			characterName = null;

			try
			{
				if (!TryResolveProblemListContext(gameKey, contextAssetPath, out GameProblemListsSO problemList, out string assetName))
				{
					return false;
				}

				characterName = ExtractCharacterName(assetName, TryCreateRegex(problemList.Regex));
				if (!string.IsNullOrWhiteSpace(characterName)
					&& TryMatchExactEntry(characterName, problemList.Entries, out problemEntry))
				{
					return true;
				}

				if (TryMatchContainingEntry(assetName, problemList.Entries, out problemEntry))
				{
					if (string.IsNullOrWhiteSpace(characterName))
					{
						characterName = problemEntry.Name;
					}

					return true;
				}

				return false;
			}
			catch (Exception exception)
			{
				HoyoToonLogger.Warning(
					HoyoToonLogCategory.Detection,
					$"Problem list matching failed for '{contextAssetPath}' in game '{gameKey}'. Continuing without a problem prompt.",
					exception);
				return false;
			}
		}

		private static GameProblemListsSO LoadProblemList(string gameKey)
		{
			string assetPath = $"{HoyoToonApi.ScriptablesAssetPath}/{gameKey}/{HoyoToonApi.GeneratedGamesFolderName}/{ProblemListsAssetFileName}";
			return AssetDatabase.LoadAssetAtPath<GameProblemListsSO>(assetPath);
		}

		private static bool TryResolveProblemListContext(
			string gameKey,
			string contextAssetPath,
			out GameProblemListsSO problemList,
			out string assetName)
		{
			problemList = null;
			assetName = null;

			if (string.IsNullOrWhiteSpace(gameKey) || string.IsNullOrWhiteSpace(contextAssetPath))
			{
				return false;
			}

			problemList = LoadProblemList(gameKey);
			assetName = GetContextAssetName(contextAssetPath);
			return problemList != null && !string.IsNullOrWhiteSpace(assetName);
		}

		private static string GetContextAssetName(string contextAssetPath)
		{
			if (string.IsNullOrWhiteSpace(contextAssetPath))
			{
				return null;
			}

			return Path.GetFileNameWithoutExtension(contextAssetPath);
		}

		private static Regex TryCreateRegex(string pattern)
		{
			if (string.IsNullOrWhiteSpace(pattern))
			{
				return null;
			}

			try
			{
				return new Regex(pattern, RegexOptions.CultureInvariant);
			}
			catch (ArgumentException)
			{
				return null;
			}
		}

		private static string ExtractCharacterName(string fileName, Regex characterRegex)
		{
			if (string.IsNullOrWhiteSpace(fileName))
			{
				return null;
			}

			return ExtractRegexMatch(characterRegex, fileName);
		}

		private static string MatchEntryName(string fileName, IReadOnlyList<GameProblemEntryData> entries)
		{
			return TryMatchContainingEntry(fileName, entries, out GameProblemEntryData entry)
				? entry.Name
				: null;
		}

		private static bool TryMatchExactEntry(string characterName, IReadOnlyList<GameProblemEntryData> entries, out GameProblemEntryData entry)
		{
			entry = null;
			if (string.IsNullOrWhiteSpace(characterName) || entries == null)
			{
				return false;
			}

			string normalizedCharacterName = NormalizeToken(characterName);
			if (normalizedCharacterName.Length <= 0)
			{
				return false;
			}

			foreach (GameProblemEntryData candidate in entries)
			{
				if (string.IsNullOrWhiteSpace(candidate?.Name))
				{
					continue;
				}

				string normalizedEntryName = NormalizeToken(candidate.Name);
				if (normalizedEntryName.Length > 0
					&& string.Equals(normalizedCharacterName, normalizedEntryName, StringComparison.OrdinalIgnoreCase))
				{
					entry = candidate;
					return true;
				}
			}

			return false;
		}

		private static bool TryMatchContainingEntry(string fileName, IReadOnlyList<GameProblemEntryData> entries, out GameProblemEntryData entry)
		{
			entry = null;
			if (string.IsNullOrWhiteSpace(fileName) || entries == null)
			{
				return false;
			}

			string normalizedFileName = NormalizeToken(fileName);
			foreach (GameProblemEntryData candidate in entries)
			{
				if (string.IsNullOrWhiteSpace(candidate?.Name))
				{
					continue;
				}

				string normalizedEntryName = NormalizeToken(candidate.Name);
				if (normalizedEntryName.Length > 0 && normalizedFileName.Contains(normalizedEntryName))
				{
					entry = candidate;
					return true;
				}
			}

			return false;
		}

		private static string ExtractRegexMatch(Regex characterRegex, string fileName)
		{
			if (characterRegex == null || string.IsNullOrWhiteSpace(fileName))
			{
				return null;
			}

			Match match = characterRegex.Match(fileName);
			if (!match.Success)
			{
				return null;
			}

			for (int groupIndex = 1; groupIndex < match.Groups.Count; groupIndex++)
			{
				Group group = match.Groups[groupIndex];
				if (group.Success && !string.IsNullOrWhiteSpace(group.Value))
				{
					return group.Value.Trim();
				}
			}

			return match.Value.Trim();
		}

		private static string NormalizeToken(string value)
		{
			return StringTokenUtility.NormalizeAlphanumericLower(value);
		}

		private static string TryExtractCharacterNameFromAssetPath(GameProblemListsSO problemList, string assetPath)
		{
			string assetName = GetContextAssetName(assetPath);
			string characterName = ExtractCharacterName(assetName, TryCreateRegex(problemList?.Regex));
			if (!string.IsNullOrWhiteSpace(characterName))
			{
				return characterName;
			}

			characterName = MatchEntryName(assetName, problemList?.Entries);
			if (!string.IsNullOrWhiteSpace(characterName))
			{
				return characterName;
			}

			return null;
		}
	}
}
