#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Detection;
using HoyoToon.Editor.Utilities.Assets;
using HoyoToon.Editor.Utilities.Debugging;
using HoyoToon.Runtime.ScriptableObjects.Games;
using Utf8Json;
using static HoyoToon.Editor.Utilities.Assets.GeneratedAssetQueryUtility;

namespace HoyoToon.Editor.AssetPipeline.Materials
{
    internal static class MaterialPropertyOverrides
    {
        private const string AssetSuffix = "/Config/GamePropertyOverrides.asset";

        private static Dictionary<string, Dictionary<string, string>> overrideJsonByGameAndShader =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private static bool isInitialized;

        public static void Initialize()
        {
            overrideJsonByGameAndShader = LoadGeneratedAssets<GamePropertyOverridesSO>("t:GamePropertyOverridesSO", AssetSuffix)
                .SelectMany(asset => asset.Entries ?? Array.Empty<GamePropertyOverridesSO.Entry>())
                .Where(entry => entry != null
                    && !string.IsNullOrWhiteSpace(entry.GameKey)
                    && !string.IsNullOrWhiteSpace(entry.ShaderPath)
                    && entry.Overrides != null)
                .GroupBy(entry => entry.GameKey, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => BuildOverrideMap(group),
                    StringComparer.Ordinal);

            isInitialized = true;
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Materials,
                $"Loaded {overrideJsonByGameAndShader.Sum(pair => pair.Value.Count)} material property override entry/entries across {overrideJsonByGameAndShader.Count} game(s).");
        }

        public static void Apply(GameConfigSO game, string shaderPath, MaterialJson materialJson)
        {
            if (game == null || materialJson == null || string.IsNullOrWhiteSpace(shaderPath))
            {
                return;
            }

            EnsureInitialized();

            if (!overrideJsonByGameAndShader.TryGetValue(game.Key, out Dictionary<string, string> overridesByShader)
                || overridesByShader == null
                || !overridesByShader.TryGetValue(shaderPath, out string overrideJson)
                || string.IsNullOrWhiteSpace(overrideJson))
            {
                return;
            }

            try
            {
                if (ApplyOverrideJson(materialJson, overrideJson))
                {
                    materialJson.RefreshDetectionContext();
                    HoyoToonLogger.Verbose(HoyoToonLogCategory.Materials, $"Applied material property overrides for game '{game.Key}' and shader '{shaderPath}'.");
                }
                else
                {
                    HoyoToonLogger.Verbose(HoyoToonLogCategory.Materials, $"Material property overrides for game '{game.Key}' and shader '{shaderPath}' did not change the current JSON.");
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Materials,
                    $"Failed to apply material property overrides for game '{game.Key}' and shader '{shaderPath}'. Material generation will continue without overrides.",
                    ex);
            }
        }

        private static Dictionary<string, string> BuildOverrideMap(IEnumerable<GamePropertyOverridesSO.Entry> entries)
        {
            var overrideMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (GamePropertyOverridesSO.Entry entry in entries.OrderBy(candidate => candidate.ShaderPath, StringComparer.Ordinal))
            {
                overrideMap[entry.ShaderPath] = entry.Overrides.Json;
            }

            return overrideMap;
        }

        private static bool ApplyOverrideJson(MaterialJson materialJson, string overrideJson)
        {
            if (string.IsNullOrWhiteSpace(overrideJson))
            {
                return false;
            }

            SavedProperties savedProperties = EnsureSavedProperties(materialJson);
            byte[] bytes = Encoding.UTF8.GetBytes(overrideJson);
            var reader = new JsonReader(bytes);
            int count = 0;
            bool changed = false;

            reader.ReadIsBeginObjectWithVerify();
            while (!reader.ReadIsEndObjectWithSkipValueSeparator(ref count))
            {
                string propertyName = reader.ReadPropertyName();
                string propertyJson = ReadValueJson(ref reader);

                switch (propertyName)
                {
                    case "m_SavedProperties":
                        changed |= ApplySavedPropertiesOverride(savedProperties, propertyJson);
                        break;

                    case "m_Floats":
                    case "m_Ints":
                    case "m_Colors":
                    case "m_TexEnvs":
                        changed |= ApplySavedPropertiesField(savedProperties, propertyName, propertyJson);
                        break;
                }
            }

            return changed;
        }

        private static bool ApplySavedPropertiesOverride(SavedProperties savedProperties, string overrideJson)
        {
            if (string.IsNullOrWhiteSpace(overrideJson))
            {
                return false;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(overrideJson);
            var reader = new JsonReader(bytes);
            int count = 0;
            bool changed = false;

            reader.ReadIsBeginObjectWithVerify();
            while (!reader.ReadIsEndObjectWithSkipValueSeparator(ref count))
            {
                string propertyName = reader.ReadPropertyName();
                string propertyJson = ReadValueJson(ref reader);
                changed |= ApplySavedPropertiesField(savedProperties, propertyName, propertyJson);
            }

            return changed;
        }

        private static bool ApplySavedPropertiesField(SavedProperties savedProperties, string propertyName, string propertyJson)
        {
            switch (propertyName)
            {
                case "m_Floats":
                    if (TryDeserialize(propertyJson, out Dictionary<string, float> floatOverrides))
                    {
                        return ApplyDictionaryOverride(savedProperties.m_Floats, floatOverrides);
                    }
                    break;

                case "m_Ints":
                    if (TryDeserialize(propertyJson, out Dictionary<string, int> intOverrides))
                    {
                        return ApplyDictionaryOverride(savedProperties.m_Ints, intOverrides);
                    }
                    break;

                case "m_Colors":
                    if (TryDeserialize(propertyJson, out Dictionary<string, ColorData> colorOverrides))
                    {
                        return ApplyDictionaryOverride(savedProperties.m_Colors, colorOverrides);
                    }
                    break;

                case "m_TexEnvs":
                    if (TryDeserialize(propertyJson, out Dictionary<string, TexEnv> texEnvOverrides))
                    {
                        return ApplyDictionaryOverride(savedProperties.m_TexEnvs, texEnvOverrides);
                    }
                    break;
            }

            return false;
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

        private static bool ApplyDictionaryOverride<T>(Dictionary<string, T> target, Dictionary<string, T> overrides)
        {
            if (target == null || overrides == null || overrides.Count <= 0)
            {
                return false;
            }

            bool changed = false;
            foreach (KeyValuePair<string, T> entry in overrides)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                target[entry.Key] = entry.Value;
                changed = true;
            }

            return changed;
        }

        private static string ReadValueJson(ref JsonReader reader)
        {
            ArraySegment<byte> segment = reader.ReadNextBlockSegment();
            return Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
        }

        private static bool TryDeserialize<T>(string json, out T value)
        {
            try
            {
                value = JsonSerializer.Deserialize<T>(json, HoyoToonApi.JsonResolver);
                return true;
            }
            catch
            {
                value = default(T);
                return false;
            }
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