#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoyoToon.Editor.Detection.Game;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Parsing;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects;
using HoyoToon.Runtime.ScriptableObjects.Games;
using UnityEditor;
using UnityEngine;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetQueryUtility;

namespace HoyoToon.Editor.AssetPipeline.Textures
{
    internal static class TextureImportSettingsApplicator
    {
        private const string AssetSuffix = "/Config/GameTextureImportSettings.asset";

        private static Dictionary<string, GameTextureImportSettingsSO> settingsByGame =
            new Dictionary<string, GameTextureImportSettingsSO>(StringComparer.Ordinal);

        private static bool isInitialized;

        public static void Initialize()
        {
            settingsByGame = LoadGeneratedAssets<GameTextureImportSettingsSO>("t:GameTextureImportSettingsSO", AssetSuffix)
                .Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.GameKey))
                .GroupBy(asset => asset.GameKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

            isInitialized = true;
            int namedRuleCount = settingsByGame.Values.Sum(CountNamedRules);
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Textures,
                $"Loaded texture import settings for {settingsByGame.Count} game(s) with {namedRuleCount} named rule(s).");
        }

        public static void Apply(GameConfigSO game, string textureAssetPath)
        {
            if (game == null || string.IsNullOrWhiteSpace(textureAssetPath))
            {
                return;
            }

            EnsureInitialized();

            if (!settingsByGame.TryGetValue(game.Key, out GameTextureImportSettingsSO settings) || settings == null)
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            string textureName = Path.GetFileNameWithoutExtension(textureAssetPath);
            bool changed = ApplyRule(importer, settings.Defaults);
            int matchedRuleCount = 0;

            changed |= ApplyMatchingRules(importer, textureName, settings.NameContains?.Entries, ContainsMatch, ref matchedRuleCount);
            changed |= ApplyMatchingRules(importer, textureName, settings.NameEndsWith?.Entries, EndsWithMatch, ref matchedRuleCount);
            changed |= ApplyMatchingRules(importer, textureName, settings.NameEquals?.Entries, EqualsMatch, ref matchedRuleCount);

            if (!changed)
            {
                HoyoToonLogger.Verbose(
                    HoyoToonLogCategory.Textures,
                    $"Texture import settings for game '{game.Key}' did not change '{textureAssetPath}'. {(matchedRuleCount > 0 ? $"Matched {matchedRuleCount} named rule(s)." : "No named rules matched; defaults required no importer changes.")}");
                return;
            }

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Textures,
                $"Applied texture import settings for game '{game.Key}' to '{textureAssetPath}'. {(matchedRuleCount > 0 ? $"Matched {matchedRuleCount} named rule(s)." : "Applied defaults only.")}");
        }

        private static bool ApplyMatchingRules(
            TextureImporter importer,
            string textureName,
            IReadOnlyList<GameTextureImportRuleMapData.Entry> entries,
            Func<string, string, bool> isMatch,
            ref int matchedRuleCount)
        {
            if (importer == null || string.IsNullOrWhiteSpace(textureName) || entries == null || isMatch == null)
            {
                return false;
            }

            bool changed = false;
            foreach (GameTextureImportRuleMapData.Entry entry in entries
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.Key) && isMatch(textureName, candidate.Key))
                .OrderBy(candidate => candidate.Key.Length)
                .ThenBy(candidate => candidate.Key, StringComparer.OrdinalIgnoreCase))
            {
                matchedRuleCount++;
                changed |= ApplyRule(importer, entry.Value);
            }

            return changed;
        }

        private static int CountNamedRules(GameTextureImportSettingsSO settings)
        {
            if (settings == null)
            {
                return 0;
            }

            return CountEntries(settings.NameEquals) + CountEntries(settings.NameContains) + CountEntries(settings.NameEndsWith);
        }

        private static int CountEntries(GameTextureImportRuleMapData ruleMap)
        {
            return ruleMap?.Entries?.Count ?? 0;
        }

        private static bool ApplyRule(TextureImporter importer, GameTextureImportRuleData rule)
        {
            if (importer == null || rule == null)
            {
                return false;
            }

            bool changed = false;

            if (TryParseTextureImporterType(rule.TextureType, out TextureImporterType textureType))
            {
                changed |= SetValue(importer.textureType, textureType, value => importer.textureType = value);
            }

            TextureImporterCompression compression;
            if (TryParseTextureImporterCompression(rule.Compression, out compression))
            {
                changed |= SetValue(importer.textureCompression, compression, value => importer.textureCompression = value);
            }

            if (TryParseTextureImporterCompression(rule.TextureCompression, out compression))
            {
                changed |= SetValue(importer.textureCompression, compression, value => importer.textureCompression = value);
            }

            if (EnumParsingUtility.TryParseFlexibleEnum(rule.FilterMode, out FilterMode filterMode))
            {
                changed |= SetValue(importer.filterMode, filterMode, value => importer.filterMode = value);
            }

            if (TryGetValue(rule.MaxTextureSize, out int maxTextureSize))
            {
                changed |= SetValue(importer.maxTextureSize, maxTextureSize, value => importer.maxTextureSize = value);
            }

            if (TryGetValue(rule.MipmapEnabled, out bool mipmapEnabled))
            {
                changed |= SetValue(importer.mipmapEnabled, mipmapEnabled, value => importer.mipmapEnabled = value);
            }

            if (EnumParsingUtility.TryParseFlexibleEnum(rule.NpotScale, out TextureImporterNPOTScale npotScale))
            {
                changed |= SetValue(importer.npotScale, npotScale, value => importer.npotScale = value);
            }

            if (TryGetValue(rule.SrgbTexture, out bool srgbTexture))
            {
                changed |= SetValue(importer.sRGBTexture, srgbTexture, value => importer.sRGBTexture = value);
            }

            if (TryGetValue(rule.StreamingMipmaps, out bool streamingMipmaps))
            {
                changed |= SetValue(importer.streamingMipmaps, streamingMipmaps, value => importer.streamingMipmaps = value);
            }

            if (EnumParsingUtility.TryParseFlexibleEnum(rule.WrapMode, out TextureWrapMode wrapMode))
            {
                changed |= SetValue(importer.wrapMode, wrapMode, value => importer.wrapMode = value);
            }

            return changed;
        }

        private static bool SetValue<T>(T currentValue, T nextValue, Action<T> applyValue)
        {
            if (EqualityComparer<T>.Default.Equals(currentValue, nextValue))
            {
                return false;
            }

            applyValue(nextValue);
            return true;
        }

        private static bool TryGetValue(GameOptionalBoolData value, out bool result)
        {
            if (value != null && value.HasValue)
            {
                result = value.Value;
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryGetValue(GameOptionalIntData value, out int result)
        {
            if (value != null && value.HasValue)
            {
                result = value.Value;
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryParseTextureImporterType(string value, out TextureImporterType result)
        {
            return EnumParsingUtility.TryParseFlexibleEnum(value, out result);
        }

        private static bool TryParseTextureImporterCompression(string value, out TextureImporterCompression result)
        {
            if (EnumParsingUtility.TryParseFlexibleEnum(value, out result))
            {
                return true;
            }

            switch (EnumParsingUtility.NormalizeToken(value))
            {
                case "none":
                case "uncompressed":
                    result = TextureImporterCompression.Uncompressed;
                    return true;

                case "compressed":
                case "normalquality":
                    result = TextureImporterCompression.Compressed;
                    return true;

                case "compressedlq":
                case "lowquality":
                    result = TextureImporterCompression.CompressedLQ;
                    return true;

                case "compressedhq":
                case "highquality":
                    result = TextureImporterCompression.CompressedHQ;
                    return true;

                default:
                    result = default;
                    return false;
            }
        }

        private static bool ContainsMatch(string textureName, string ruleKey)
        {
            return textureName.IndexOf(ruleKey, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EndsWithMatch(string textureName, string ruleKey)
        {
            return textureName.EndsWith(ruleKey, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EqualsMatch(string textureName, string ruleKey)
        {
            return string.Equals(textureName, ruleKey, StringComparison.OrdinalIgnoreCase);
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