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
		private const string EntityCatalogAssetFileName = "GameEntityCatalog.asset";
		private const string ProblemListsAssetFileName = "GameProblemLists.asset";
		private const string WithAnimsSuffix = "_WithAnims";

		public static string TryExtractCharacterName(string gameKey, string contextAssetPath)
		{
			try
			{
				if (TryExtractCharacterNameFromCatalog(gameKey, contextAssetPath, out string catalogCharacterName))
				{
					return catalogCharacterName;
				}

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

		public static string TryExtractCatalogCharacterName(string gameKey, string contextAssetPath)
		{
			try
			{
				return TryExtractCharacterNameFromCatalog(gameKey, contextAssetPath, out string characterName)
					? characterName
					: null;
			}
			catch (Exception exception)
			{
				HoyoToonLogger.Warning(
					HoyoToonLogCategory.Detection,
					$"Catalog character name detection failed for '{contextAssetPath}' in game '{gameKey}'. Continuing without a catalog character name.",
					exception);
				return null;
			}
		}

		public static bool IsCharacterMatch(string gameKey, string contextAssetPath, string characterName)
		{
			if (string.IsNullOrWhiteSpace(gameKey)
				|| string.IsNullOrWhiteSpace(contextAssetPath)
				|| string.IsNullOrWhiteSpace(characterName))
			{
				return false;
			}

			string detectedName = TryExtractCharacterName(gameKey, contextAssetPath);
			if (string.IsNullOrWhiteSpace(detectedName))
			{
				return false;
			}

			if (TryResolveCatalogCharacterName(gameKey, characterName, out string expectedCatalogName)
				&& TryResolveCatalogCharacterName(gameKey, detectedName, out string detectedCatalogName))
			{
				return string.Equals(expectedCatalogName, detectedCatalogName, StringComparison.OrdinalIgnoreCase);
			}

			return string.Equals(detectedName, characterName, StringComparison.OrdinalIgnoreCase);
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
				if (string.IsNullOrWhiteSpace(gameKey) || string.IsNullOrWhiteSpace(contextAssetPath))
				{
					return false;
				}

				GameProblemListsSO problemList = LoadProblemList(gameKey);
				string assetName = GetContextAssetName(contextAssetPath);
				characterName = TryExtractCharacterNameFromCatalog(gameKey, contextAssetPath, out string catalogCharacterName)
					? catalogCharacterName
					: ExtractCharacterName(assetName, TryCreateRegex(problemList?.Regex));
				if (problemList == null || string.IsNullOrWhiteSpace(assetName))
				{
					return false;
				}

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

		private static GameEntityCatalogSO LoadEntityCatalog(string gameKey)
		{
			string assetPath = $"{HoyoToonApi.ScriptablesAssetPath}/{gameKey}/{HoyoToonApi.GeneratedGamesFolderName}/{EntityCatalogAssetFileName}";
			return AssetDatabase.LoadAssetAtPath<GameEntityCatalogSO>(assetPath);
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

		private static bool TryExtractCharacterNameFromCatalog(string gameKey, string contextAssetPath, out string characterName)
		{
			characterName = null;
			GameEntityCatalogSO catalog = LoadEntityCatalog(gameKey);
			if (catalog == null)
			{
				return false;
			}

			foreach (string candidateName in EnumerateContextNameCandidates(contextAssetPath))
			{
				if (!catalog.TryFindCharacterExact(candidateName, out GameEntityCatalogSO.Entry entry) || entry == null)
				{
					continue;
				}

				characterName = ResolveEntryName(entry);
				return !string.IsNullOrWhiteSpace(characterName);
			}

			return false;
		}

		private static bool TryResolveCatalogCharacterName(string gameKey, string characterName, out string resolvedName)
		{
			resolvedName = null;
			GameEntityCatalogSO catalog = LoadEntityCatalog(gameKey);
			if (catalog == null
				|| string.IsNullOrWhiteSpace(characterName)
				|| !catalog.TryFindCharacterExact(characterName, out GameEntityCatalogSO.Entry entry)
				|| entry == null)
			{
				return false;
			}

			resolvedName = ResolveEntryName(entry);
			return !string.IsNullOrWhiteSpace(resolvedName);
		}

		private static IEnumerable<string> EnumerateContextNameCandidates(string contextAssetPath)
		{
			string assetName = GetContextAssetName(contextAssetPath);
			if (!string.IsNullOrWhiteSpace(assetName))
			{
				foreach (string candidate in EnumerateNameCandidateVariants(assetName))
				{
					yield return candidate;
				}
			}

			string normalizedPath = contextAssetPath?.Replace('\\', '/');
			if (string.IsNullOrWhiteSpace(normalizedPath))
			{
				yield break;
			}

			string directoryPath = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
			while (!string.IsNullOrWhiteSpace(directoryPath))
			{
				string directoryName = Path.GetFileName(directoryPath);
				if (!string.IsNullOrWhiteSpace(directoryName))
				{
					foreach (string candidate in EnumerateNameCandidateVariants(directoryName))
					{
						yield return candidate;
					}
				}

				string parentPath = Path.GetDirectoryName(directoryPath)?.Replace('\\', '/');
				if (string.Equals(parentPath, directoryPath, StringComparison.Ordinal))
				{
					yield break;
				}

				directoryPath = parentPath;
			}
		}

		private static IEnumerable<string> EnumerateNameCandidateVariants(string name)
		{
			yield return name;

			if (name.EndsWith(WithAnimsSuffix, StringComparison.OrdinalIgnoreCase))
			{
				yield return name.Substring(0, name.Length - WithAnimsSuffix.Length);
			}
		}

		private static string ResolveEntryName(GameEntityCatalogSO.Entry entry)
		{
			if (entry == null)
			{
				return null;
			}

			if (!string.IsNullOrWhiteSpace(entry.DisplayName))
			{
				return entry.DisplayName;
			}

			if (!string.IsNullOrWhiteSpace(entry.SourceName))
			{
				return entry.SourceName;
			}

			if (!string.IsNullOrWhiteSpace(entry.InternalName))
			{
				return entry.InternalName;
			}

			return string.IsNullOrWhiteSpace(entry.EntityId) ? null : entry.EntityId;
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
