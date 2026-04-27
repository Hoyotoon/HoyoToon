#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Detection;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Games;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetQueryUtility;

namespace HoyoToon.Editor.AssetPipeline.Materials
{
    internal static class MaterialPropertyConversions
    {
        private const string AssetSuffix = "/Config/GamePropertyConversions.asset";

        private static Dictionary<string, Dictionary<string, string>> propertyMapsByGame =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private static bool isInitialized;

        public static void Initialize()
        {
            propertyMapsByGame = LoadGeneratedAssets<GamePropertyConversionsSO>("t:GamePropertyConversionsSO", AssetSuffix)
                .SelectMany(asset => asset.Entries ?? Array.Empty<GamePropertyConversionsSO.Entry>())
                .Where(entry => entry != null
                    && !string.IsNullOrWhiteSpace(entry.GameKey)
                    && !string.IsNullOrWhiteSpace(entry.SourceProperty)
                    && !string.IsNullOrWhiteSpace(entry.TargetProperty))
                .GroupBy(entry => entry.GameKey, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => BuildPropertyMap(group),
                    StringComparer.Ordinal);

            isInitialized = true;
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Materials,
                $"Loaded {propertyMapsByGame.Sum(pair => pair.Value.Count)} material property conversion(s) across {propertyMapsByGame.Count} game(s).");
        }

        public static void Apply(GameConfigSO game, MaterialJson materialJson)
        {
            if (game == null || materialJson == null)
            {
                return;
            }

            EnsureInitialized();

            if (!propertyMapsByGame.TryGetValue(game.Key, out Dictionary<string, string> propertyMap)
                || propertyMap == null
                || propertyMap.Count <= 0)
            {
                return;
            }

            SavedProperties savedProperties = EnsureSavedProperties(materialJson);
            bool changed = RemapDictionary(savedProperties.m_TexEnvs, propertyMap);
            changed |= RemapDictionary(savedProperties.m_Floats, propertyMap);
            changed |= RemapDictionary(savedProperties.m_Colors, propertyMap);
            changed |= RemapDictionary(savedProperties.m_Ints, propertyMap);

            if (changed)
            {
                materialJson.RefreshDetectionContext();
                HoyoToonLogger.Verbose(HoyoToonLogCategory.Materials, $"Applied material property conversions for game '{game.Key}'.");
            }
            else
            {
                HoyoToonLogger.Verbose(HoyoToonLogCategory.Materials, $"Material property conversions for game '{game.Key}' found no matching properties in the current JSON.");
            }
        }

        private static Dictionary<string, string> BuildPropertyMap(IEnumerable<GamePropertyConversionsSO.Entry> entries)
        {
            var propertyMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (GamePropertyConversionsSO.Entry entry in entries
                .OrderBy(candidate => candidate.SourceProperty, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.TargetProperty, StringComparer.Ordinal))
            {
                propertyMap[entry.SourceProperty] = entry.TargetProperty;
            }

            return propertyMap;
        }

        private static SavedProperties EnsureSavedProperties(MaterialJson materialJson)
        {
            if (materialJson.m_SavedProperties == null)
            {
                materialJson.m_SavedProperties = new SavedProperties();
            }

            if (materialJson.m_SavedProperties.m_TexEnvs == null)
            {
                materialJson.m_SavedProperties.m_TexEnvs = new Dictionary<string, TexEnv>(StringComparer.Ordinal);
            }

            if (materialJson.m_SavedProperties.m_Floats == null)
            {
                materialJson.m_SavedProperties.m_Floats = new Dictionary<string, float>(StringComparer.Ordinal);
            }

            if (materialJson.m_SavedProperties.m_Colors == null)
            {
                materialJson.m_SavedProperties.m_Colors = new Dictionary<string, ColorData>(StringComparer.Ordinal);
            }

            if (materialJson.m_SavedProperties.m_Ints == null)
            {
                materialJson.m_SavedProperties.m_Ints = new Dictionary<string, int>(StringComparer.Ordinal);
            }

            return materialJson.m_SavedProperties;
        }

        private static bool RemapDictionary<T>(Dictionary<string, T> properties, IReadOnlyDictionary<string, string> propertyMap)
        {
            if (properties == null || properties.Count <= 0 || propertyMap == null || propertyMap.Count <= 0)
            {
                return false;
            }

            bool changed = false;
            var remapped = new Dictionary<string, T>(properties.Count, StringComparer.Ordinal);
            var explicitTargets = new Dictionary<string, bool>(properties.Count, StringComparer.Ordinal);

            foreach (KeyValuePair<string, T> entry in properties)
            {
                string sourceKey = entry.Key;
                if (string.IsNullOrWhiteSpace(sourceKey))
                {
                    continue;
                }

                string targetKey = sourceKey;
                if (propertyMap.TryGetValue(sourceKey, out string mappedKey) && !string.IsNullOrWhiteSpace(mappedKey))
                {
                    targetKey = mappedKey;
                }

                bool isExplicitTarget = string.Equals(sourceKey, targetKey, StringComparison.Ordinal);
                if (remapped.ContainsKey(targetKey))
                {
                    if (isExplicitTarget && !explicitTargets[targetKey])
                    {
                        remapped[targetKey] = entry.Value;
                        explicitTargets[targetKey] = true;
                    }

                    if (!isExplicitTarget)
                    {
                        changed = true;
                    }

                    continue;
                }

                remapped[targetKey] = entry.Value;
                explicitTargets[targetKey] = isExplicitTarget;

                if (!isExplicitTarget)
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            properties.Clear();
            foreach (KeyValuePair<string, T> entry in remapped)
            {
                properties[entry.Key] = entry.Value;
            }

            return true;
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
#endif