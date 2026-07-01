using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HoyoToon.Editor.Detection;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Detection.Shader;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using UnityEngine;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetSyncUtility;

namespace HoyoToon.Editor.API.Games
{
    internal static class GamesScriptableObjectSync
    {
        private static readonly GameAssetDefinition[] AssetDefinitions =
        {
            CreateAssetDefinition<GameConfigSO>("GameConfig", BuildGameConfigPayload),
            CreateAssetDefinition<GameModelImportSettingsSO>("GameModelImportSettings", BuildModelImportSettingsPayload),
            CreateAssetDefinition<GameProblemListsSO>("GameProblemLists", BuildProblemListsPayload),
            CreateAssetDefinition<GamePropertyConversionsSO>("GamePropertyConversions", BuildPropertyConversionsPayload),
            CreateAssetDefinition<GamePropertyOverridesSO>("GamePropertyOverrides", BuildPropertyOverridesPayload),
            CreateAssetDefinition<GameShaderKeywordsSO>("GameShaderKeywords", BuildShaderKeywordsPayload),
            CreateAssetDefinition<GameTangentSettingsSO>("GameTangentSettings", BuildTangentSettingsPayload),
            CreateAssetDefinition<GameTextureImportSettingsSO>("GameTextureImportSettings", BuildTextureImportSettingsPayload),
            CreateAssetDefinition<GameTextureMappingsSO>("GameTextureMappings", BuildTextureMappingsPayload),
        };

        internal static bool NeedsWrite(IReadOnlyList<GameRecordDto> games)
        {
            List<GameRecordDto> syncGames = GetSyncGames(games);
            if (GetStaleGeneratedGameFolders(syncGames.Select(game => game.config.key)).Count > 0)
            {
                return true;
            }

            if (syncGames.Count <= 0)
            {
                return false;
            }

            foreach (GameRecordDto game in syncGames)
            {
                string assetFolderPath = GetGameGeneratedAssetsFolder(game.config.key);
                if (!AssetDatabase.IsValidFolder(assetFolderPath))
                {
                    return true;
                }

                if (NeedsWriteGameAssets(game, assetFolderPath))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void WriteAssets(IReadOnlyList<GameRecordDto> games)
        {
            List<GameRecordDto> syncGames = GetSyncGames(games);
            List<string> staleGeneratedGameFolders = GetStaleGeneratedGameFolders(syncGames.Select(game => game.config.key));
            if (syncGames.Count <= 0 && staleGeneratedGameFolders.Count <= 0)
            {
                HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, "No game records were eligible for generated asset sync.");
                return;
            }

            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Api,
                $"Synchronizing generated game assets for {syncGames.Count} game(s) and removing {staleGeneratedGameFolders.Count} stale folder(s).");

            using (AssetDatabaseEditingScope.Begin(disallowAutoRefresh: false))
            {
                CleanupStaleGeneratedGameFolders(staleGeneratedGameFolders);

                foreach (GameRecordDto game in syncGames)
                {
                    string assetFolderPath = GetGameGeneratedAssetsFolder(game.config.key);
                    HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Updating generated game assets for '{game.config.key}'.");
                    GeneratedAssetSyncUtility.EnsureAssetFolderExists(assetFolderPath);
                    WriteGameAssets(game, assetFolderPath);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GeneratedAssetCacheRefreshUtility.RefreshAll();

            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Api,
                $"Finished synchronizing generated game assets for {syncGames.Count} game(s) and {staleGeneratedGameFolders.Count} stale folder(s).");
        }

        private static void WriteGameAssets(GameRecordDto game, string assetFolderPath)
        {
            IReadOnlyList<GameRecordDto> gameSlice = new[] { game };

            foreach (GameAssetDefinition assetDefinition in AssetDefinitions)
            {
                assetDefinition.WriteAsset(assetFolderPath, gameSlice);
            }
        }

        private static bool NeedsWriteGameAssets(GameRecordDto game, string assetFolderPath)
        {
            IReadOnlyList<GameRecordDto> gameSlice = new[] { game };

            foreach (GameAssetDefinition assetDefinition in AssetDefinitions)
            {
                if (!assetDefinition.AssetMatchesPayload(assetFolderPath, gameSlice))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameAssetDefinition CreateAssetDefinition<TAsset>(
            string fileName,
            Func<IReadOnlyList<GameRecordDto>, Dictionary<string, object>> payloadBuilder)
            where TAsset : ScriptableObject
        {
            return new TypedGameAssetDefinition<TAsset>(fileName, payloadBuilder);
        }

        private static Dictionary<string, object> BuildGameConfigPayload(IReadOnlyList<GameRecordDto> games)
        {
            GameRecordDto game = games
                .Where(candidate => candidate?.config != null && !string.IsNullOrWhiteSpace(candidate.config.key))
                .OrderBy(candidate => candidate.config.key, StringComparer.Ordinal)
                .FirstOrDefault();

            return CreateObject(
                ("key", game?.config?.key),
                ("defaultShader", game?.config?.defaultShader),
                ("gameProperties", CopyList(game?.config?.gameProperties)));
        }

        private static Dictionary<string, object> BuildModelImportSettingsPayload(IReadOnlyList<GameRecordDto> games)
        {
            GameRecordDto game = games
                .Where(candidate => candidate?.modelImportSettings != null)
                .OrderBy(candidate => ResolveGameKey(candidate.modelImportSettings.gameKey, candidate.config?.key), StringComparer.Ordinal)
                .FirstOrDefault();

            string gameKey = ResolveGameKey(game?.modelImportSettings?.gameKey, game?.config?.key);
            return CreateObject(
                ("gameKey", gameKey),
                ("defaults", CreateModelImportDefaultsPayload(game?.modelImportSettings?.defaults)));
        }

        private static Dictionary<string, object> BuildProblemListsPayload(IReadOnlyList<GameRecordDto> games)
        {
            GameRecordDto game = games
                .Where(candidate => candidate?.problemList != null)
                .OrderBy(candidate => ResolveGameKey(candidate.problemList.gameKey, candidate.config?.key), StringComparer.Ordinal)
                .FirstOrDefault();

            string gameKey = ResolveGameKey(game?.problemList?.gameKey, game?.config?.key);
            List<Dictionary<string, object>> problemEntries = (game?.problemList?.entries ?? new List<ProblemEntryDto>())
                .Select(entry => CreateObject(
                    ("name", entry?.Name),
                    ("message", entry?.Message),
                    ("type", entry?.Type)))
                .ToList();

            return CreateObject(
                ("gameKey", gameKey),
                ("regex", game?.problemList?.regex),
                ("entries", problemEntries));
        }

        private static Dictionary<string, object> BuildPropertyConversionsPayload(IReadOnlyList<GameRecordDto> games)
        {
            List<Dictionary<string, object>> entries = games
            .SelectMany(game => game?.propertyConversions ?? Enumerable.Empty<PropertyConversionDto>(), (game, conversion) => new { game, conversion })
                .Select(item =>
                {
                    string gameKey = ResolveGameKey(item.conversion?.gameKey, item.game?.config?.key);
                    return new
                    {
                        GameKey = gameKey,
                        SourceProperty = item.conversion?.sourceProperty,
                        TargetProperty = item.conversion?.targetProperty,
                        Entry = CreateObject(
                            ("gameKey", gameKey),
                            ("sourceProperty", item.conversion?.sourceProperty),
                            ("targetProperty", item.conversion?.targetProperty))
                    };
                })
                .OrderBy(item => item.GameKey, StringComparer.Ordinal)
                .ThenBy(item => item.SourceProperty, StringComparer.Ordinal)
                .ThenBy(item => item.TargetProperty, StringComparer.Ordinal)
                .Select(item => item.Entry)
                .ToList();

            return CreateEntriesPayload(entries);
        }

        private static Dictionary<string, object> BuildPropertyOverridesPayload(IReadOnlyList<GameRecordDto> games)
        {
            List<Dictionary<string, object>> entries = games
            .SelectMany(game => game?.propertyOverrides ?? Enumerable.Empty<PropertyOverrideDto>(), (game, overrideEntry) => new { game, overrideEntry })
                .Select(item =>
                {
                    string gameKey = ResolveGameKey(item.overrideEntry?.gameKey, item.game?.config?.key);
                    return new
                    {
                        GameKey = gameKey,
                        ShaderPath = item.overrideEntry?.shaderPath,
                        Entry = CreateObject(
                            ("gameKey", gameKey),
                            ("shaderPath", item.overrideEntry?.shaderPath),
                            ("overrides", CreateSerializedJsonValuePayload(NormalizeRawJson(item.overrideEntry?.overrides))))
                    };
                })
                .OrderBy(item => item.GameKey, StringComparer.Ordinal)
                .ThenBy(item => item.ShaderPath, StringComparer.Ordinal)
                .Select(item => item.Entry)
                .ToList();

            return CreateEntriesPayload(entries);
        }

        private static Dictionary<string, object> BuildShaderKeywordsPayload(IReadOnlyList<GameRecordDto> games)
        {
            List<Dictionary<string, object>> entries = games
            .SelectMany(game => game?.shaderKeywords ?? Enumerable.Empty<ShaderKeywordDto>(), (game, keyword) => new { game, keyword })
                .Select(item =>
                {
                    string gameKey = ResolveGameKey(item.keyword?.gameKey, item.game?.config?.key);
                    return new
                    {
                        GameKey = gameKey,
                        ShaderPath = item.keyword?.shaderPath,
                        Entry = CreateObject(
                            ("gameKey", gameKey),
                            ("shaderPath", item.keyword?.shaderPath),
                            ("keywords", CopyList(item.keyword?.keywords)))
                    };
                })
                .OrderBy(item => item.GameKey, StringComparer.Ordinal)
                .ThenBy(item => item.ShaderPath, StringComparer.Ordinal)
                .Select(item => item.Entry)
                .ToList();

            return CreateEntriesPayload(entries);
        }

        private static Dictionary<string, object> BuildTangentSettingsPayload(IReadOnlyList<GameRecordDto> games)
        {
            GameRecordDto game = games
                .Where(candidate => candidate?.tangentSettings != null)
                .OrderBy(candidate => ResolveGameKey(candidate.tangentSettings.gameKey, candidate.config?.key), StringComparer.Ordinal)
                .FirstOrDefault();

            string gameKey = ResolveGameKey(game?.tangentSettings?.gameKey, game?.config?.key);
            return CreateObject(
                ("gameKey", gameKey),
                ("options", CopyList(game?.tangentSettings?.options)),
                ("status", game?.tangentSettings?.status),
                ("skipMeshesContaining", CopyList(game?.tangentSettings?.skipMeshesContaining)));
        }

        private static Dictionary<string, object> BuildTextureImportSettingsPayload(IReadOnlyList<GameRecordDto> games)
        {
            GameRecordDto game = games
                .Where(candidate => candidate?.textureImportSettings != null)
                .OrderBy(candidate => ResolveGameKey(candidate.textureImportSettings.gameKey, candidate.config?.key), StringComparer.Ordinal)
                .FirstOrDefault();

            string gameKey = ResolveGameKey(game?.textureImportSettings?.gameKey, game?.config?.key);
            return CreateObject(
                ("gameKey", gameKey),
                ("defaults", CreateTextureImportRulePayload(game?.textureImportSettings?.defaults)),
                ("nameEquals", CreateTextureImportRuleMapPayload(game?.textureImportSettings?.nameEquals)),
                ("nameContains", CreateTextureImportRuleMapPayload(game?.textureImportSettings?.nameContains)),
                ("nameEndsWith", CreateTextureImportRuleMapPayload(game?.textureImportSettings?.nameEndsWith)));
        }

        private static Dictionary<string, object> BuildTextureMappingsPayload(IReadOnlyList<GameRecordDto> games)
        {
            List<Dictionary<string, object>> entries = games
            .SelectMany(game => game?.textureMappings ?? Enumerable.Empty<TextureMappingDto>(), (game, mapping) => new { game, mapping })
                .Select(item =>
                {
                    string gameKey = ResolveGameKey(item.mapping?.gameKey, item.game?.config?.key);
                    return new
                    {
                        GameKey = gameKey,
                        PropertyName = item.mapping?.propertyName,
                        TextureName = item.mapping?.textureName,
                        Entry = CreateObject(
                            ("gameKey", gameKey),
                            ("propertyName", item.mapping?.propertyName),
                            ("textureName", item.mapping?.textureName))
                    };
                })
                .OrderBy(item => item.GameKey, StringComparer.Ordinal)
                .ThenBy(item => item.PropertyName, StringComparer.Ordinal)
                .ThenBy(item => item.TextureName, StringComparer.Ordinal)
                .Select(item => item.Entry)
                .ToList();

            return CreateEntriesPayload(entries);
        }

        private static Dictionary<string, object> CreateEntriesPayload(List<Dictionary<string, object>> entries)
        {
            return CreateObject(("entries", entries));
        }

        private static Dictionary<string, object> CreateSerializedJsonValuePayload(string value)
        {
            return CreateObject(("json", value));
        }

        private static Dictionary<string, object> CreateModelImportDefaultsPayload(Dictionary<string, object> source)
        {
            return CreateObject(
                ("animationCompression", GetStringValue(source, "AnimationCompression")),
                ("animationType", GetStringValue(source, "AnimationType")),
                ("avatarSetup", GetStringValue(source, "AvatarSetup")),
                ("bakeAxisConversion", CreateOptionalBoolPayload(GetValue(source, "BakeAxisConversion"))),
                ("importAnimation", CreateOptionalBoolPayload(GetValue(source, "ImportAnimation"))),
                ("importBlendShapes", CreateOptionalBoolPayload(GetValue(source, "ImportBlendShapes"))),
                ("importCameras", CreateOptionalBoolPayload(GetValue(source, "ImportCameras"))),
                ("importLights", CreateOptionalBoolPayload(GetValue(source, "ImportLights"))),
                ("importVisibility", CreateOptionalBoolPayload(GetValue(source, "ImportVisibility"))),
                ("isReadable", CreateOptionalBoolPayload(GetValue(source, "IsReadable"))),
                ("legacyBlendshapeNormals", CreateOptionalBoolPayload(GetValue(source, "LegacyBlendshapeNormals"))),
                ("materialImportMode", GetStringValue(source, "MaterialImportMode")),
                ("materialLocation", GetStringValue(source, "MaterialLocation")),
                ("materialName", GetStringValue(source, "MaterialName")),
                ("materialSearch", GetStringValue(source, "MaterialSearch")),
                ("materialSearchAndRemap", CreateOptionalBoolPayload(GetFirstValue(source, "MaterialSearchAndRemap", "MaterialSearch&Remap"))),
                ("normals", GetStringValue(source, "Normals")),
                ("optimizeMeshPolygons", CreateOptionalBoolPayload(GetValue(source, "OptimizeMeshPolygons"))),
                ("optimizeMeshVertices", CreateOptionalBoolPayload(GetValue(source, "OptimizeMeshVertices"))),
                ("resampleCurves", CreateOptionalBoolPayload(GetValue(source, "ResampleCurves"))),
                ("scaleFactor", CreateOptionalFloatPayload(GetValue(source, "ScaleFactor"))),
                ("tangents", GetStringValue(source, "Tangents")),
                ("useFileScale", CreateOptionalBoolPayload(GetValue(source, "UseFileScale"))));
        }

        private static Dictionary<string, object> CreateTextureImportRulePayload(Dictionary<string, object> source)
        {
            return CreateObject(
                ("compression", GetStringValue(source, "Compression")),
                ("filterMode", GetStringValue(source, "FilterMode")),
                ("maxTextureSize", CreateOptionalIntPayload(GetValue(source, "MaxTextureSize"))),
                ("mipmapEnabled", CreateOptionalBoolPayload(GetValue(source, "MipmapEnabled"))),
                ("npotScale", GetStringValue(source, "NPOTScale")),
                ("srgbTexture", CreateOptionalBoolPayload(GetValue(source, "SRGBTexture"))),
                ("streamingMipmaps", CreateOptionalBoolPayload(GetValue(source, "StreamingMipmaps"))),
                ("textureCompression", GetStringValue(source, "TextureCompression")),
                ("textureType", GetStringValue(source, "TextureType")),
                ("wrapMode", GetStringValue(source, "WrapMode")));
        }

        private static Dictionary<string, object> CreateTextureImportRuleMapPayload(Dictionary<string, object> source)
        {
            List<Dictionary<string, object>> entries = (source ?? new Dictionary<string, object>())
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => CreateObject(
                    ("key", item.Key),
                    ("value", CreateTextureImportRulePayload(item.Value as Dictionary<string, object>))))
                .ToList();

            return CreateObject(("entries", entries));
        }

        private static Dictionary<string, object> CreateOptionalBoolPayload(object value)
        {
            return CreateObject(
                ("hasValue", value != null),
                ("value", ToBoolean(value)));
        }

        private static Dictionary<string, object> CreateOptionalIntPayload(object value)
        {
            return CreateObject(
                ("hasValue", value != null),
                ("value", ToInt(value)));
        }

        private static Dictionary<string, object> CreateOptionalFloatPayload(object value)
        {
            return CreateObject(
                ("hasValue", value != null),
                ("value", ToFloat(value)));
        }

        private static List<GameRecordDto> GetSyncGames(IReadOnlyList<GameRecordDto> games)
        {
            return games
                .Where(game => game?.config != null && !string.IsNullOrWhiteSpace(game.config.key))
                .OrderBy(game => game.config.key, StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> GetStaleGeneratedGameFolders(IEnumerable<string> activeGameKeys)
        {
            if (!AssetDatabase.IsValidFolder(HoyoToonApi.ScriptablesAssetPath))
            {
                return new List<string>();
            }

            var activeGameKeySet = new HashSet<string>(
                activeGameKeys?.Where(key => !string.IsNullOrWhiteSpace(key)) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            return AssetDatabase.GetSubFolders(HoyoToonApi.ScriptablesAssetPath)
                .Select(gameFolderPath => new
                {
                    GameKey = GetPathLeafName(gameFolderPath),
                    GeneratedFolderPath = GetGameGeneratedAssetsFolderFromGameFolder(gameFolderPath),
                })
                .Where(candidate => AssetDatabase.IsValidFolder(candidate.GeneratedFolderPath))
                .Where(candidate => !activeGameKeySet.Contains(candidate.GameKey))
                .Select(candidate => candidate.GeneratedFolderPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static void CleanupStaleGeneratedGameFolders(IEnumerable<string> staleGeneratedGameFolders)
        {
            foreach (string generatedFolderPath in staleGeneratedGameFolders ?? Enumerable.Empty<string>())
            {
                if (!AssetDatabase.IsValidFolder(generatedFolderPath))
                {
                    continue;
                }

                HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Removing stale generated game assets at '{generatedFolderPath}'.");
                if (!AssetDatabase.DeleteAsset(generatedFolderPath))
                {
                    HoyoToonLogger.Warning(HoyoToonLogCategory.Api, $"Failed to remove stale generated game assets at '{generatedFolderPath}'.");
                    continue;
                }

                DeleteGameFolderIfEmpty(GetParentFolderPath(generatedFolderPath));
            }
        }

        private static string GetGameGeneratedAssetsFolder(string gameKey)
        {
            return $"{GetGameScriptablesFolder(gameKey)}/{HoyoToonApi.GeneratedGamesFolderName}";
        }

        private static string GetGameGeneratedAssetsFolderFromGameFolder(string gameFolderPath)
        {
            return $"{gameFolderPath}/{HoyoToonApi.GeneratedGamesFolderName}";
        }

        private static string GetGameScriptablesFolder(string gameKey)
        {
            return $"{HoyoToonApi.ScriptablesAssetPath}/{gameKey}";
        }

        private static string GetAssetPath(string assetFolderPath, string fileName)
        {
            return $"{assetFolderPath}/{fileName}.asset";
        }

        private static void DeleteGameFolderIfEmpty(string gameFolderPath)
        {
            if (!AssetDatabase.IsValidFolder(gameFolderPath))
            {
                return;
            }

            if (AssetDatabase.GetSubFolders(gameFolderPath).Length > 0)
            {
                return;
            }

            bool hasRemainingAssets = AssetDatabase.FindAssets(string.Empty, new[] { gameFolderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Any(assetPath =>
                    !string.Equals(assetPath, gameFolderPath, StringComparison.Ordinal)
                    && !AssetDatabase.IsValidFolder(assetPath));

            if (hasRemainingAssets)
            {
                return;
            }

            HoyoToonLogger.Verbose(HoyoToonLogCategory.Api, $"Removing empty generated game folder '{gameFolderPath}'.");
            if (!AssetDatabase.DeleteAsset(gameFolderPath))
            {
                HoyoToonLogger.Warning(HoyoToonLogCategory.Api, $"Failed to remove empty generated game folder '{gameFolderPath}'.");
            }
        }

        private static string GetParentFolderPath(string assetPath)
        {
            int separatorIndex = assetPath.LastIndexOf('/');
            return separatorIndex > 0 ? assetPath.Substring(0, separatorIndex) : string.Empty;
        }

        private static string GetPathLeafName(string assetPath)
        {
            int separatorIndex = assetPath.LastIndexOf('/');
            return separatorIndex >= 0 ? assetPath.Substring(separatorIndex + 1) : assetPath;
        }

        private abstract class GameAssetDefinition
        {
            private readonly string fileName;

            protected GameAssetDefinition(string fileName)
            {
                this.fileName = fileName;
            }

            protected string ResolveAssetPath(string assetFolderPath)
            {
                return GetAssetPath(assetFolderPath, fileName);
            }

            internal abstract bool AssetMatchesPayload(string assetFolderPath, IReadOnlyList<GameRecordDto> games);

            internal abstract void WriteAsset(string assetFolderPath, IReadOnlyList<GameRecordDto> games);
        }

        private sealed class TypedGameAssetDefinition<TAsset> : GameAssetDefinition where TAsset : ScriptableObject
        {
            private readonly Func<IReadOnlyList<GameRecordDto>, Dictionary<string, object>> payloadBuilder;

            internal TypedGameAssetDefinition(
                string fileName,
                Func<IReadOnlyList<GameRecordDto>, Dictionary<string, object>> payloadBuilder)
                : base(fileName)
            {
                this.payloadBuilder = payloadBuilder;
            }

            internal override bool AssetMatchesPayload(string assetFolderPath, IReadOnlyList<GameRecordDto> games)
            {
                string assetPath = ResolveAssetPath(assetFolderPath);
                TAsset asset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
                return GeneratedAssetSyncUtility.AssetMatchesPayload(asset, payloadBuilder(games));
            }

            internal override void WriteAsset(string assetFolderPath, IReadOnlyList<GameRecordDto> games)
            {
                string assetPath = ResolveAssetPath(assetFolderPath);
                TAsset asset = GeneratedAssetSyncUtility.LoadOrCreateAsset<TAsset>(assetPath);
                GeneratedAssetSyncUtility.OverwriteAsset(asset, payloadBuilder(games));
            }
        }

        private static string ResolveGameKey(string preferredValue, string fallbackValue)
        {
            return string.IsNullOrWhiteSpace(preferredValue) ? fallbackValue ?? string.Empty : preferredValue;
        }

        private static object GetValue(Dictionary<string, object> source, string key)
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return source.TryGetValue(key, out object value) ? value : null;
        }

        private static object GetFirstValue(Dictionary<string, object> source, params string[] keys)
        {
            foreach (string key in keys)
            {
                object value = GetValue(source, key);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private static string GetStringValue(Dictionary<string, object> source, string key)
        {
            object value = GetValue(source, key);
            return value?.ToString();
        }

        private static List<string> CopyList(IEnumerable<string> values)
        {
            return values?.Where(value => value != null).ToList() ?? new List<string>();
        }

        private static bool ToBoolean(object value)
        {
            if (value == null)
            {
                return false;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static int ToInt(object value)
        {
            if (value == null)
            {
                return 0;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static float ToFloat(object value)
        {
            if (value == null)
            {
                return 0f;
            }

            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        private static string NormalizeRawJson(RawJson value, string fallback = "{}")
        {
            return string.IsNullOrWhiteSpace(value?.Json) ? fallback : value.Json;
        }
    }
}
