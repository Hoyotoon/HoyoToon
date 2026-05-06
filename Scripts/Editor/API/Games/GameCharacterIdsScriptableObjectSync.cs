using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Editor.Utilities.IO;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetSyncUtility;

namespace HoyoToon.Editor.API.Games
{
    internal static class GameCharacterIdsScriptableObjectSync
    {
        private const string AssetFileName = "GameCharacterIds";
        private const string AssetSuffix = "/Config/GameCharacterIds.asset";

        internal static bool NeedsWrite(IReadOnlyList<CharacterIdRecordDto> records)
        {
            Dictionary<string, List<CharacterIdRecordDto>> groupedRecords = GroupRecords(records);
            if (GetStaleAssetPaths(groupedRecords.Keys).Count > 0)
            {
                return true;
            }

            foreach (KeyValuePair<string, List<CharacterIdRecordDto>> pair in groupedRecords)
            {
                string assetPath = GetAssetPath(pair.Key);
                GameCharacterIdsSO asset = AssetDatabase.LoadAssetAtPath<GameCharacterIdsSO>(assetPath);
                if (!GeneratedAssetSyncUtility.AssetMatchesPayload(asset, BuildPayload(pair.Key, pair.Value)))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void WriteAssets(IReadOnlyList<CharacterIdRecordDto> records)
        {
            Dictionary<string, List<CharacterIdRecordDto>> groupedRecords = GroupRecords(records);
            List<string> staleAssetPaths = GetStaleAssetPaths(groupedRecords.Keys);
            if (groupedRecords.Count <= 0 && staleAssetPaths.Count <= 0)
            {
                HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, "No character ID records were eligible for generated asset sync.");
                return;
            }

            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Api,
                $"Synchronizing generated character ID assets for {groupedRecords.Count} game(s) and removing {staleAssetPaths.Count} stale asset(s).");

            using (AssetDatabaseEditingScope.Begin(disallowAutoRefresh: false))
            {
                CleanupStaleAssets(staleAssetPaths);

                foreach (KeyValuePair<string, List<CharacterIdRecordDto>> pair in groupedRecords)
                {
                    string assetFolderPath = GetAssetFolderPath(pair.Key);
                    GeneratedAssetSyncUtility.EnsureAssetFolderExists(assetFolderPath);

                    string assetPath = GetAssetPath(pair.Key);
                    GameCharacterIdsSO asset = GeneratedAssetSyncUtility.LoadOrCreateAsset<GameCharacterIdsSO>(assetPath);
                    GeneratedAssetSyncUtility.OverwriteAsset(asset, BuildPayload(pair.Key, pair.Value));
                    HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Updated generated character ID asset for '{pair.Key}'.");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GeneratedAssetCacheRefreshUtility.RefreshAll();

            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Api,
                $"Finished synchronizing generated character ID assets for {groupedRecords.Count} game(s) and {staleAssetPaths.Count} stale asset(s).");
        }

        private static Dictionary<string, object> BuildPayload(string gameKey, IReadOnlyList<CharacterIdRecordDto> records)
        {
            List<Dictionary<string, object>> entries = records
                .Where(record => record != null && record.CharacterId > 0)
                .OrderBy(record => record.CharacterId)
                .ThenBy(record => record.Name, StringComparer.Ordinal)
                .Select(record => CreateObject(
                    ("characterId", record.CharacterId),
                    ("name", record.Name),
                    ("sourceName", string.IsNullOrWhiteSpace(record.SourceName) ? record.Name : record.SourceName),
                    ("overrideName", record.OverrideName),
                    ("avatarIcon", record.AvatarIcon),
                    ("roundIcon", record.RoundIcon),
                    ("splashIcon", record.SplashIcon)))
                .ToList();

            return CreateObject(
                ("gameKey", gameKey),
                ("entries", entries));
        }

        private static Dictionary<string, List<CharacterIdRecordDto>> GroupRecords(IReadOnlyList<CharacterIdRecordDto> records)
        {
            var groupedRecords = new Dictionary<string, List<CharacterIdRecordDto>>(StringComparer.Ordinal);
            if (records == null)
            {
                return groupedRecords;
            }

            foreach (CharacterIdRecordDto record in records)
            {
                if (record == null
                    || string.IsNullOrWhiteSpace(record.GameKey)
                    || record.CharacterId <= 0
                    || string.IsNullOrWhiteSpace(record.Name))
                {
                    continue;
                }

                string gameKey = record.GameKey.Trim();
                if (!groupedRecords.TryGetValue(gameKey, out List<CharacterIdRecordDto> group))
                {
                    group = new List<CharacterIdRecordDto>();
                    groupedRecords.Add(gameKey, group);
                }

                group.Add(new CharacterIdRecordDto
                {
                    GameKey = gameKey,
                    CharacterId = record.CharacterId,
                    Name = record.Name?.Trim(),
                    SourceName = record.SourceName?.Trim(),
                    OverrideName = record.OverrideName?.Trim(),
                    AvatarIcon = record.AvatarIcon?.Trim(),
                    RoundIcon = record.RoundIcon?.Trim(),
                    SplashIcon = record.SplashIcon?.Trim(),
                });
            }

            return groupedRecords;
        }

        private static List<string> GetStaleAssetPaths(IEnumerable<string> activeGameKeys)
        {
            if (!AssetDatabase.IsValidFolder(HoyoToonApi.ScriptablesAssetPath))
            {
                return new List<string>();
            }

            var activeGameKeySet = new HashSet<string>(
                activeGameKeys?.Where(key => !string.IsNullOrWhiteSpace(key)) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            return AssetDatabase.FindAssets("t:GameCharacterIdsSO", new[] { HoyoToonApi.ScriptablesAssetPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(assetPath => assetPath.EndsWith(AssetSuffix, StringComparison.Ordinal))
                .Where(assetPath => !activeGameKeySet.Contains(GetGameKeyFromAssetPath(assetPath)))
                .OrderBy(assetPath => assetPath, StringComparer.Ordinal)
                .ToList();
        }

        private static void CleanupStaleAssets(IEnumerable<string> staleAssetPaths)
        {
            foreach (string staleAssetPath in staleAssetPaths ?? Enumerable.Empty<string>())
            {
                if (!AssetDatabase.LoadAssetAtPath<GameCharacterIdsSO>(staleAssetPath))
                {
                    continue;
                }

                HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Removing stale generated character ID asset at '{staleAssetPath}'.");
                if (!AssetDatabase.DeleteAsset(staleAssetPath))
                {
                    HoyoToonLogger.Warning(HoyoToonLogCategory.Api, $"Failed to remove stale generated character ID asset at '{staleAssetPath}'.");
                }
            }
        }

        private static string GetAssetFolderPath(string gameKey)
        {
            return $"{HoyoToonApi.ScriptablesAssetPath}/{gameKey}/{HoyoToonApi.GeneratedGamesFolderName}";
        }

        private static string GetAssetPath(string gameKey)
        {
            return $"{GetAssetFolderPath(gameKey)}/{AssetFileName}.asset";
        }

        private static string GetGameKeyFromAssetPath(string assetPath)
        {
            string normalizedPath = EditorPathUtility.NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return string.Empty;
            }

            string scriptablesPrefix = HoyoToonApi.ScriptablesAssetPath + "/";
            if (!normalizedPath.StartsWith(scriptablesPrefix, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string relativePath = normalizedPath.Substring(scriptablesPrefix.Length);
            int separatorIndex = relativePath.IndexOf('/');
            return separatorIndex >= 0 ? relativePath.Substring(0, separatorIndex) : relativePath;
        }
    }
}
