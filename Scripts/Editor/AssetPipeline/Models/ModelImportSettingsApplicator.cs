#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

namespace HoyoToon.Editor.AssetPipeline.Models
{
    public enum ModelImportSettingsApplyOutcome
    {
        Applied,
        Unchanged,
        Skipped,
        Failed,
    }

    public static class ModelImportSettingsApplicator
    {
        private const string AssetSuffix = "/Config/GameModelImportSettings.asset";

        private static Dictionary<string, GameModelImportSettingsSO> settingsByGame =
            new Dictionary<string, GameModelImportSettingsSO>(StringComparer.Ordinal);

        private static bool isInitialized;

        public static void Initialize()
        {
            settingsByGame = LoadGeneratedAssets<GameModelImportSettingsSO>("t:GameModelImportSettingsSO", AssetSuffix)
                .Where(asset => asset != null && !string.IsNullOrWhiteSpace(asset.GameKey))
                .GroupBy(asset => asset.GameKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

            isInitialized = true;
            HoyoToonLogger.Verbose(
                HoyoToonLogCategory.Models,
                $"Loaded model import settings for {settingsByGame.Count} game(s).");
        }

        public static ModelImportSettingsApplyOutcome Apply(GameConfigSO game, string modelAssetPath)
        {
            if (game == null)
            {
                HoyoToonLogger.Info(HoyoToonLogCategory.Models, "Model import settings application was skipped because no game was provided.");
                return ModelImportSettingsApplyOutcome.Skipped;
            }

            EnsureInitialized();

            if (!settingsByGame.TryGetValue(game.Key, out GameModelImportSettingsSO settings) || settings == null)
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Model import settings application was skipped for game '{game.Key}' because no generated settings asset was found.",
                    context: game);
                return ModelImportSettingsApplyOutcome.Skipped;
            }

            return Apply(settings, modelAssetPath);
        }

        public static ModelImportSettingsApplyOutcome ApplyDetected(string modelAssetPath)
        {
            if (string.IsNullOrWhiteSpace(modelAssetPath))
            {
                HoyoToonLogger.Info(HoyoToonLogCategory.Models, "Model import settings application was skipped because no model asset path was provided.");
                return ModelImportSettingsApplyOutcome.Skipped;
            }

            if (!(AssetImporter.GetAtPath(modelAssetPath) is ModelImporter))
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Model import settings application skipped '{modelAssetPath}' because it is not a model importer asset.");
                return ModelImportSettingsApplyOutcome.Skipped;
            }

            if (!GameDetector.TryDetectGameFromAssetContext(modelAssetPath, out GameConfigSO game, out _) || game == null)
            {
                return ModelImportSettingsApplyOutcome.Skipped;
            }

            return Apply(game, modelAssetPath);
        }

        public static ModelImportSettingsApplyOutcome Apply(GameModelImportSettingsSO settings, string modelAssetPath)
        {
            if (settings == null)
            {
                HoyoToonLogger.Info(HoyoToonLogCategory.Models, "Model import settings application was skipped because no settings asset was provided.");
                return ModelImportSettingsApplyOutcome.Skipped;
            }

            if (string.IsNullOrWhiteSpace(modelAssetPath))
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Model import settings application was skipped for game '{FormatGameKey(settings.GameKey)}' because no FBX asset path was provided.",
                    context: settings);
                return ModelImportSettingsApplyOutcome.Skipped;
            }

            ModelImporter importer = AssetImporter.GetAtPath(modelAssetPath) as ModelImporter;
            if (importer == null)
            {
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Model import settings application skipped '{modelAssetPath}' because it is not a model importer asset.",
                    context: settings);
                return ModelImportSettingsApplyOutcome.Skipped;
            }

            try
            {
                bool changed = ApplyToImporter(importer, settings, modelAssetPath);
                if (!changed)
                {
                    HoyoToonLogger.Verbose(
                        HoyoToonLogCategory.Models,
                        $"Model import settings for game '{FormatGameKey(settings.GameKey)}' did not change '{modelAssetPath}'.",
                        context: settings);
                    return ModelImportSettingsApplyOutcome.Unchanged;
                }

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                HoyoToonLogger.Info(
                    HoyoToonLogCategory.Models,
                    $"Applied model import settings for game '{FormatGameKey(settings.GameKey)}' to '{modelAssetPath}'.",
                    context: settings);
                return ModelImportSettingsApplyOutcome.Applied;
            }
            catch (Exception exception)
            {
                HoyoToonLogger.Error(
                    HoyoToonLogCategory.Models,
                    $"Failed to apply model import settings for game '{FormatGameKey(settings.GameKey)}' to '{modelAssetPath}'.",
                    exception,
                    context: settings);
                return ModelImportSettingsApplyOutcome.Failed;
            }
        }

        public static bool ApplyToImporter(ModelImporter importer, GameModelImportSettingsSO settings, string assetPath = null)
        {
            if (importer == null || settings == null)
            {
                return false;
            }

            return ApplyToImporter(importer, settings.Defaults, assetPath, settings);
        }

        private static bool ApplyToImporter(
            ModelImporter importer,
            GameModelImportDefaultsData defaults,
            string assetPath,
            GameModelImportSettingsSO settings)
        {
            if (importer == null || defaults == null)
            {
                return false;
            }

            string resolvedAssetPath = string.IsNullOrWhiteSpace(assetPath) ? importer.assetPath : assetPath;
            bool changed = ApplyAnimationSettings(importer, defaults, resolvedAssetPath, settings);
            changed |= ApplySceneAndReadabilitySettings(importer, defaults);
            changed |= ApplyLegacyBlendshapeNormals(importer, defaults.LegacyBlendshapeNormals, resolvedAssetPath, settings);
            changed |= ApplyMaterialSettings(importer, defaults, resolvedAssetPath, settings);
            changed |= ApplyGeometrySettings(importer, defaults, resolvedAssetPath, settings);
            return changed;
        }

        private static bool ApplyAnimationSettings(
            ModelImporter importer,
            GameModelImportDefaultsData defaults,
            string assetPath,
            GameModelImportSettingsSO settings)
        {
            bool changed = false;
            changed |= ApplyEnumSetting(
                defaults.AnimationCompression,
                nameof(defaults.AnimationCompression),
                importer.animationCompression,
                value => importer.animationCompression = value,
                assetPath,
                settings);
            changed |= ApplyEnumSetting(
                defaults.AnimationType,
                nameof(defaults.AnimationType),
                importer.animationType,
                value => importer.animationType = value,
                assetPath,
                settings);
            changed |= ApplyEnumSetting(
                defaults.AvatarSetup,
                nameof(defaults.AvatarSetup),
                importer.avatarSetup,
                value => importer.avatarSetup = value,
                assetPath,
                settings);
            changed |= ApplyBoolSetting(defaults.ImportAnimation, importer.importAnimation, value => importer.importAnimation = value);
            changed |= ApplyBoolSetting(defaults.ResampleCurves, importer.resampleCurves, value => importer.resampleCurves = value);
            return changed;
        }

        private static bool ApplySceneAndReadabilitySettings(ModelImporter importer, GameModelImportDefaultsData defaults)
        {
            bool changed = false;
            changed |= ApplyBoolSetting(defaults.BakeAxisConversion, importer.bakeAxisConversion, value => importer.bakeAxisConversion = value);
            changed |= ApplyBoolSetting(defaults.ImportBlendShapes, importer.importBlendShapes, value => importer.importBlendShapes = value);
            changed |= ApplyBoolSetting(defaults.ImportCameras, importer.importCameras, value => importer.importCameras = value);
            changed |= ApplyBoolSetting(defaults.ImportLights, importer.importLights, value => importer.importLights = value);
            changed |= ApplyBoolSetting(defaults.ImportVisibility, importer.importVisibility, value => importer.importVisibility = value);
            changed |= ApplyBoolSetting(defaults.IsReadable, importer.isReadable, value => importer.isReadable = value);
            return changed;
        }

        private static bool ApplyMaterialSettings(
            ModelImporter importer,
            GameModelImportDefaultsData defaults,
            string assetPath,
            GameModelImportSettingsSO settings)
        {
            bool changed = false;
            changed |= ApplyEnumSetting(
                defaults.MaterialImportMode,
                nameof(defaults.MaterialImportMode),
                importer.materialImportMode,
                value => importer.materialImportMode = value,
                assetPath,
                settings);
            changed |= ApplyEnumSetting(
                defaults.MaterialLocation,
                nameof(defaults.MaterialLocation),
                importer.materialLocation,
                value => importer.materialLocation = value,
                assetPath,
                settings);
            changed |= ApplyOptionalEnumSetting(
                defaults.MaterialName,
                nameof(defaults.MaterialName),
                importer.materialName,
                value => importer.materialName = value,
                assetPath,
                settings,
                out bool hasMaterialName,
                out ModelImporterMaterialName materialName);
            changed |= ApplyOptionalEnumSetting(
                defaults.MaterialSearch,
                nameof(defaults.MaterialSearch),
                importer.materialSearch,
                value => importer.materialSearch = value,
                assetPath,
                settings,
                out bool hasMaterialSearch,
                out ModelImporterMaterialSearch materialSearch);
            changed |= ApplyMaterialSearchAndRemap(
                importer,
                defaults,
                assetPath,
                settings,
                hasMaterialName,
                materialName,
                hasMaterialSearch,
                materialSearch);
            return changed;
        }

        private static bool ApplyGeometrySettings(
            ModelImporter importer,
            GameModelImportDefaultsData defaults,
            string assetPath,
            GameModelImportSettingsSO settings)
        {
            bool changed = false;
            changed |= ApplyEnumSetting(
                defaults.Normals,
                nameof(defaults.Normals),
                importer.importNormals,
                value => importer.importNormals = value,
                assetPath,
                settings);
            changed |= ApplyBoolSetting(defaults.OptimizeMeshPolygons, importer.optimizeMeshPolygons, value => importer.optimizeMeshPolygons = value);
            changed |= ApplyBoolSetting(defaults.OptimizeMeshVertices, importer.optimizeMeshVertices, value => importer.optimizeMeshVertices = value);
            changed |= ApplyFloatSetting(defaults.ScaleFactor, importer.globalScale, value => importer.globalScale = value);
            changed |= ApplyEnumSetting(
                defaults.Tangents,
                nameof(defaults.Tangents),
                importer.importTangents,
                value => importer.importTangents = value,
                assetPath,
                settings);
            changed |= ApplyBoolSetting(defaults.UseFileScale, importer.useFileScale, value => importer.useFileScale = value);
            return changed;
        }

        private static bool ApplyEnumSetting<TEnum>(
            string value,
            string fieldName,
            TEnum currentValue,
            Action<TEnum> applyValue,
            string assetPath,
            GameModelImportSettingsSO settings)
            where TEnum : struct, Enum
        {
            return ApplyOptionalEnumSetting(value, fieldName, currentValue, applyValue, assetPath, settings, out _, out _);
        }

        private static bool ApplyOptionalEnumSetting<TEnum>(
            string value,
            string fieldName,
            TEnum currentValue,
            Action<TEnum> applyValue,
            string assetPath,
            GameModelImportSettingsSO settings,
            out bool hasParsedValue,
            out TEnum parsedValue)
            where TEnum : struct, Enum
        {
            hasParsedValue = TryParseEnumSetting(value, fieldName, assetPath, settings, out parsedValue);
            return hasParsedValue && SetValue(currentValue, parsedValue, applyValue);
        }

        private static bool ApplyBoolSetting(
            GameOptionalBoolData value,
            bool currentValue,
            Action<bool> applyValue)
        {
            return TryGetValue(value, out bool parsedValue)
                && SetValue(currentValue, parsedValue, applyValue);
        }

        private static bool ApplyFloatSetting(
            GameOptionalFloatData value,
            float currentValue,
            Action<float> applyValue)
        {
            return TryGetValue(value, out float parsedValue)
                && SetFloatValue(currentValue, parsedValue, applyValue);
        }

        private static bool ApplyLegacyBlendshapeNormals(
            ModelImporter importer,
            GameOptionalBoolData value,
            string assetPath,
            GameModelImportSettingsSO settings)
        {
            if (!TryGetValue(value, out bool parsedValue))
            {
                return false;
            }

            if (ModelImporterPropertyUtility.TryGetLegacyBlendshapeNormals(importer, out bool currentValue))
            {
                if (currentValue == parsedValue)
                {
                    return false;
                }

                if (ModelImporterPropertyUtility.TrySetLegacyBlendshapeNormals(importer, parsedValue))
                {
                    return true;
                }
            }

            HoyoToonLogger.Warning(
                HoyoToonLogCategory.Models,
                $"Unable to apply hidden model import setting '{nameof(GameModelImportDefaultsData.LegacyBlendshapeNormals)}'='{parsedValue}' for '{assetPath}'. The current Unity editor version does not expose a writable value for this importer setting.",
                context: settings);
            return false;
        }

        private static bool ApplyMaterialSearchAndRemap(
            ModelImporter importer,
            GameModelImportDefaultsData defaults,
            string assetPath,
            GameModelImportSettingsSO settings,
            bool hasMaterialName,
            ModelImporterMaterialName materialName,
            bool hasMaterialSearch,
            ModelImporterMaterialSearch materialSearch)
        {
            if (!TryGetValue(defaults.MaterialSearchAndRemap, out bool materialSearchAndRemap) || !materialSearchAndRemap)
            {
                return false;
            }

            if (hasMaterialName && hasMaterialSearch)
            {
                return importer.SearchAndRemapMaterials(materialName, materialSearch);
            }

            HoyoToonLogger.Warning(
                HoyoToonLogCategory.Models,
                $"Skipping model material search and remap for '{assetPath}' because '{nameof(defaults.MaterialName)}' or '{nameof(defaults.MaterialSearch)}' is missing or invalid.",
                context: settings);
            return false;
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

        private static bool SetFloatValue(float currentValue, float nextValue, Action<float> applyValue)
        {
            if (Mathf.Approximately(currentValue, nextValue))
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

        private static bool TryGetValue(GameOptionalFloatData value, out float result)
        {
            if (value != null && value.HasValue)
            {
                result = value.Value;
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryParseEnumSetting<TEnum>(
            string value,
            string fieldName,
            string assetPath,
            GameModelImportSettingsSO settings,
            out TEnum result)
            where TEnum : struct, Enum
        {
            if (EnumParsingUtility.TryParseFlexibleEnum(value, out result))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                HoyoToonLogger.Warning(
                    HoyoToonLogCategory.Models,
                    $"Skipping invalid model import setting '{fieldName}'='{value}' for '{assetPath}'. Expected a {typeof(TEnum).Name} value.",
                    context: settings);
            }

            result = default;
            return false;
        }

        private static string FormatGameKey(string gameKey)
        {
            return string.IsNullOrWhiteSpace(gameKey) ? "unknown" : gameKey;
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