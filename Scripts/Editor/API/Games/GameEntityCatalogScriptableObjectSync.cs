using System;
using System.Collections.Generic;
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
    internal static class GameEntityCatalogScriptableObjectSync
    {
        private const string AssetFileName = "GameEntityCatalog";
        private const string AssetSuffix = "/Config/GameEntityCatalog.asset";
        private const string LegacyAssetSuffix = "/Config/GameCharacterIds.asset";

        internal static bool NeedsWrite(IReadOnlyList<EntityCatalogRecordDto> records)
        {
            Dictionary<string, List<EntityCatalogRecordDto>> groupedRecords = GroupRecords(records);
            if (GetStaleAssetPaths(groupedRecords.Keys).Count > 0 || GetLegacyAssetPaths().Count > 0)
            {
                return true;
            }

            foreach (KeyValuePair<string, List<EntityCatalogRecordDto>> pair in groupedRecords)
            {
                string assetPath = GetAssetPath(pair.Key);
                GameEntityCatalogSO asset = AssetDatabase.LoadAssetAtPath<GameEntityCatalogSO>(assetPath);
                if (!GeneratedAssetSyncUtility.AssetMatchesPayload(asset, BuildPayload(pair.Key, pair.Value)))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void WriteAssets(IReadOnlyList<EntityCatalogRecordDto> records)
        {
            Dictionary<string, List<EntityCatalogRecordDto>> groupedRecords = GroupRecords(records);
            List<string> staleAssetPaths = GetStaleAssetPaths(groupedRecords.Keys);
            List<string> legacyAssetPaths = GetLegacyAssetPaths();
            if (groupedRecords.Count <= 0 && staleAssetPaths.Count <= 0 && legacyAssetPaths.Count <= 0)
            {
                HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, "No entity catalog records were eligible for generated asset sync.");
                return;
            }

            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Api,
                $"Synchronizing generated entity catalog assets for {groupedRecords.Count} game(s), removing {staleAssetPaths.Count} stale catalog asset(s), and removing {legacyAssetPaths.Count} legacy character ID asset(s).");

            using (AssetDatabaseEditingScope.Begin(disallowAutoRefresh: false))
            {
                CleanupAssets(staleAssetPaths, "stale generated entity catalog");
                CleanupAssets(legacyAssetPaths, "legacy generated character ID");

                foreach (KeyValuePair<string, List<EntityCatalogRecordDto>> pair in groupedRecords)
                {
                    string assetFolderPath = GetAssetFolderPath(pair.Key);
                    GeneratedAssetSyncUtility.EnsureAssetFolderExists(assetFolderPath);

                    string assetPath = GetAssetPath(pair.Key);
                    GameEntityCatalogSO asset = GeneratedAssetSyncUtility.LoadOrCreateAsset<GameEntityCatalogSO>(assetPath);
                    GeneratedAssetSyncUtility.OverwriteAsset(asset, BuildPayload(pair.Key, pair.Value));
                    HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Updated generated entity catalog asset for '{pair.Key}'.");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GeneratedAssetCacheRefreshUtility.RefreshAll();

            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Api,
                $"Finished synchronizing generated entity catalog assets for {groupedRecords.Count} game(s).");
        }

        private static Dictionary<string, object> BuildPayload(string gameKey, IReadOnlyList<EntityCatalogRecordDto> records)
        {
            string version = records
                .Select(record => record?.version?.Trim())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            List<Dictionary<string, object>> entries = records
                .Where(IsValidRecord)
                .OrderBy(record => record.entityKind, StringComparer.Ordinal)
                .ThenBy(record => record.displayName, StringComparer.Ordinal)
                .ThenBy(record => ResolveEntityId(record), StringComparer.Ordinal)
                .Select(record => CreateObject(
                    ("entityKind", NormalizeEntityKind(record.entityKind)),
                    ("entityId", ResolveEntityId(record)),
                    ("characterId", NormalizeString(record.characterId)),
                    ("monsterId", NormalizeString(record.monsterId)),
                    ("weaponId", NormalizeString(record.weaponId)),
                    ("displayName", NormalizeString(record.displayName)),
                    ("sourceName", NormalizeString(record.sourceName)),
                    ("variantName", NormalizeString(record.variantName)),
                    ("internalName", NormalizeString(record.internalName)),
                    ("weaponType", NormalizeString(record.weaponType)),
                    ("rarity", record.rarity),
                    ("primaryArtName", NormalizeString(record.primaryArtName)),
                    ("artNames", NormalizeList(record.artNames)),
                    ("artNameMappings", NormalizeArtNameMappings(record.artNameMappings)),
                    ("aliases", NormalizeList(record.aliases)),
                    ("avatarIcon", NormalizeIconUrl(record.avatarIcon)),
                    ("roundIcon", NormalizeIconUrl(record.roundIcon)),
                    ("splashIcon", NormalizeIconUrl(record.splashIcon)),
                    ("displayImageJson", NormalizeRawJson(record.displayImage)),
                    ("iconsJson", NormalizeRawJson(record.icons)),
                    ("iconPathsJson", NormalizeRawJson(record.iconPaths)),
                    ("mediaRefsJson", NormalizeRawJson(record.mediaRefs)),
                    ("assetRefsJson", NormalizeRawJson(record.assetRefs)),
                    ("available", record.available ?? true)))
                .ToList();

            return CreateObject(
                ("gameKey", gameKey),
                ("version", version),
                ("entries", entries));
        }

        private static Dictionary<string, List<EntityCatalogRecordDto>> GroupRecords(IReadOnlyList<EntityCatalogRecordDto> records)
        {
            var groupedRecords = new Dictionary<string, List<EntityCatalogRecordDto>>(StringComparer.Ordinal);
            if (records == null)
            {
                return groupedRecords;
            }

            foreach (EntityCatalogRecordDto record in records)
            {
                if (!IsValidRecord(record))
                {
                    continue;
                }

                string gameKey = record.gameKey.Trim();
                if (!groupedRecords.TryGetValue(gameKey, out List<EntityCatalogRecordDto> group))
                {
                    group = new List<EntityCatalogRecordDto>();
                    groupedRecords.Add(gameKey, group);
                }

                group.Add(record);
            }

            return groupedRecords;
        }

        private static bool IsValidRecord(EntityCatalogRecordDto record)
        {
            return record != null
                && !string.IsNullOrWhiteSpace(record.gameKey)
                && !string.IsNullOrWhiteSpace(NormalizeEntityKind(record.entityKind))
                && !string.IsNullOrWhiteSpace(ResolveEntityId(record))
                && (!string.IsNullOrWhiteSpace(record.displayName)
                    || !string.IsNullOrWhiteSpace(record.internalName)
                    || (record.artNames?.Count ?? 0) > 0);
        }

        private static string ResolveEntityId(EntityCatalogRecordDto record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            return NormalizeString(record.entityId)
                ?? NormalizeString(record.characterId)
                ?? NormalizeString(record.monsterId)
                ?? NormalizeString(record.weaponId)
                ?? string.Empty;
        }

        private static string NormalizeEntityKind(string value)
        {
            string normalized = NormalizeString(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (string.Equals(normalized, "Characters", StringComparison.OrdinalIgnoreCase))
            {
                return nameof(GameEntityCatalogSO.EntityKind.Character);
            }

            if (string.Equals(normalized, "Monsters", StringComparison.OrdinalIgnoreCase))
            {
                return nameof(GameEntityCatalogSO.EntityKind.Monster);
            }

            if (string.Equals(normalized, "Weapons", StringComparison.OrdinalIgnoreCase))
            {
                return nameof(GameEntityCatalogSO.EntityKind.Weapon);
            }

            return Enum.TryParse(normalized, true, out GameEntityCatalogSO.EntityKind entityKind)
                ? entityKind.ToString()
                : string.Empty;
        }

        private static List<string> NormalizeList(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Select(NormalizeString)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static List<Dictionary<string, object>> NormalizeArtNameMappings(IEnumerable<EntityCatalogArtNameMappingDto> mappings)
        {
            return (mappings ?? Enumerable.Empty<EntityCatalogArtNameMappingDto>())
                .Where(mapping => mapping != null)
                .Select(mapping => CreateObject(
                    ("fileName", NormalizeString(mapping.fileName)),
                    ("finalName", NormalizeString(mapping.finalName))))
                .Where(mapping => !string.IsNullOrWhiteSpace(mapping["fileName"] as string)
                    || !string.IsNullOrWhiteSpace(mapping["finalName"] as string))
                .ToList();
        }

        private static string NormalizeString(string value)
        {
            string normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static string NormalizeIconUrl(string value)
        {
            return ImageProxyUrlUtility.ToUnitySupportedPngUrl(NormalizeString(value));
        }

        private static string NormalizeRawJson(RawJson value)
        {
            string json = value?.Json?.Trim();
            return string.IsNullOrWhiteSpace(json) || string.Equals(json, "null", StringComparison.Ordinal)
                ? string.Empty
                : json;
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

            return AssetDatabase.FindAssets("t:GameEntityCatalogSO", new[] { HoyoToonApi.ScriptablesAssetPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(assetPath => assetPath.EndsWith(AssetSuffix, StringComparison.Ordinal))
                .Where(assetPath => !activeGameKeySet.Contains(GetGameKeyFromAssetPath(assetPath)))
                .OrderBy(assetPath => assetPath, StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> GetLegacyAssetPaths()
        {
            if (!AssetDatabase.IsValidFolder(HoyoToonApi.ScriptablesAssetPath))
            {
                return new List<string>();
            }

            return AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(LegacyAssetSuffix), new[] { HoyoToonApi.ScriptablesAssetPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(assetPath => assetPath.EndsWith(LegacyAssetSuffix, StringComparison.Ordinal))
                .OrderBy(assetPath => assetPath, StringComparer.Ordinal)
                .ToList();
        }

        private static void CleanupAssets(IEnumerable<string> assetPaths, string label)
        {
            foreach (string assetPath in assetPaths ?? Enumerable.Empty<string>())
            {
                HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Removing {label} asset at '{assetPath}'.");
                if (!AssetDatabase.DeleteAsset(assetPath))
                {
                    HoyoToonLogger.Warning(HoyoToonLogCategory.Api, $"Failed to remove {label} asset at '{assetPath}'.");
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
