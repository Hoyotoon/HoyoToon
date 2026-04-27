#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEngine;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetQueryUtility;

namespace HoyoToon.Editor.AssetPipeline.Textures
{
    internal static class TexturePropertyMappings
    {
        private const string AssetSuffix = "/Config/GameTextureMappings.asset";

        private static Dictionary<string, Dictionary<string, string>> textureMappingsByGame =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private static bool isInitialized;

        public static void Initialize()
        {
            textureMappingsByGame = LoadGeneratedAssets<GameTextureMappingsSO>("t:GameTextureMappingsSO", AssetSuffix)
                .SelectMany(asset => asset.Entries ?? Array.Empty<GameTextureMappingsSO.Entry>())
                .Where(entry => entry != null
                    && !string.IsNullOrWhiteSpace(entry.GameKey)
                    && !string.IsNullOrWhiteSpace(entry.PropertyName)
                    && !string.IsNullOrWhiteSpace(entry.TextureName))
                .GroupBy(entry => entry.GameKey, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => BuildMappingMap(group),
                    StringComparer.Ordinal);

            isInitialized = true;
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Textures,
                $"Loaded {textureMappingsByGame.Sum(pair => pair.Value.Count)} texture property mapping(s) across {textureMappingsByGame.Count} game(s).");
        }

        public static void Apply(GameConfigSO game, Material material, Func<string, Texture> resolveTexture, ref int appliedTextureCount)
        {
            if (game == null || material == null || resolveTexture == null)
            {
                return;
            }

            EnsureInitialized();

            if (!textureMappingsByGame.TryGetValue(game.Key, out Dictionary<string, string> textureMappings)
                || textureMappings == null
                || textureMappings.Count <= 0)
            {
                return;
            }

            int appliedByMappings = 0;
            int matchedPropertyCount = 0;
            var unresolvedMappings = new List<string>();

            foreach (KeyValuePair<string, string> mapping in textureMappings)
            {
                if (!material.HasProperty(mapping.Key))
                {
                    continue;
                }

                matchedPropertyCount++;

                Texture texture = resolveTexture(mapping.Value);
                if (texture == null)
                {
                    unresolvedMappings.Add($"{mapping.Key}->{mapping.Value}");
                    continue;
                }

                material.SetTexture(mapping.Key, texture);
                appliedTextureCount++;
                appliedByMappings++;
            }

            if (appliedByMappings > 0)
            {
                string unresolvedText = unresolvedMappings.Count <= 0
                    ? string.Empty
                    : $" Unresolved mappings: {string.Join(", ", unresolvedMappings.Distinct(StringComparer.Ordinal))}.";
                HoyoToonLogger.Verbose(
                    HoyoToonLogCategory.Textures,
                    $"Applied {appliedByMappings} texture property mapping(s) for game '{game.Key}' to material '{material.name}'.{unresolvedText}");
                return;
            }

            if (matchedPropertyCount > 0)
            {
                string unresolvedText = unresolvedMappings.Count <= 0
                    ? " The mapped textures resolved to null."
                    : $" Unresolved mappings: {string.Join(", ", unresolvedMappings.Distinct(StringComparer.Ordinal))}.";
                HoyoToonLogger.Verbose(
                    HoyoToonLogCategory.Textures,
                    $"Texture property mappings for game '{game.Key}' matched {matchedPropertyCount} material propert{(matchedPropertyCount == 1 ? "y" : "ies")} on '{material.name}' but did not apply any textures.{unresolvedText}");
                return;
            }

            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Textures,
                $"Texture property mappings for game '{game.Key}' found no matching material properties on '{material.name}'.");
        }

        private static Dictionary<string, string> BuildMappingMap(IEnumerable<GameTextureMappingsSO.Entry> entries)
        {
            var textureMappings = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (GameTextureMappingsSO.Entry entry in entries
                .OrderBy(candidate => candidate.PropertyName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.TextureName, StringComparer.Ordinal))
            {
                textureMappings[entry.PropertyName] = entry.TextureName;
            }

            return textureMappings;
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