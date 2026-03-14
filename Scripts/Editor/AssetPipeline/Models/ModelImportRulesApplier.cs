#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.API;
using System.Reflection;
using HoyoToon.Editor.AssetPipeline.Materials;

namespace HoyoToon.Editor.AssetPipeline.Models
{
    public static class ModelImportRulesApplier
    {
        public static bool TryReadModelSnapshot(string assetPath, out HoyoToonModelImportSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"TryReadModelSnapshot ignored non-FBX path: {assetPath}");
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Error, $"No ModelImporter found at path: {assetPath}");
                return false;
            }

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            int meshCount = 0, skinnedCount = 0, boneCount = 0;
            if (go != null)
            {
                var meshes = go.GetComponentsInChildren<MeshFilter>(true);
                meshCount = meshes != null ? meshes.Length : 0;
                var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                skinnedCount = skinned != null ? skinned.Length : 0;
                foreach (var smr in skinned)
                {
                    if (smr == null || smr.bones == null) continue;
                    boneCount += smr.bones.Length;
                }
            }

            snapshot = new HoyoToonModelImportSnapshot
            {
                assetPath = assetPath,
                assetName = Path.GetFileNameWithoutExtension(assetPath),
                globalScale = importer.globalScale,
                importBlendShapes = importer.importBlendShapes,
                importAnimation = importer.importAnimation,
                animationType = importer.animationType,
                materialImportMode = importer.materialImportMode,
                materialLocation = importer.materialLocation,
                meshCount = meshCount,
                skinnedMeshCount = skinnedCount,
                boneCount = boneCount
            };
            return true;
        }

        public static bool TryApplyModelImportSettings(string assetPath, HoyoToonModelImportSettings settings, bool reimport = true)
        {
            if (settings == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Error, "TryApplyModelImportSettings called with null settings.");
                return false;
            }
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"TryApplyModelImportSettings ignored non-FBX path: {assetPath}");
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Error, $"No ModelImporter found at path: {assetPath}");
                return false;
            }

            bool changed = false;

            ProcessCommonSettings(importer, settings, applyChanges: true, ref changed, null);

            if (settings.legacyBlendshapeNormals.HasValue)
            {
                if (TryGetLegacyBlendshapeNormals(importer, out var oldVal))
                {
                    bool requested = settings.legacyBlendshapeNormals.Value;

                    if (requested && importer.importBlendShapes && importer.importNormals == ModelImporterNormals.Calculate)
                    {
                        requested = false;
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Disabled legacy blendshape normals for '{assetPath}' to avoid smoothing-group warnings.");
                    }

                    if (oldVal != requested && TrySetLegacyBlendshapeNormals(importer, requested))
                        changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                if (reimport)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Reimporting with applied settings: {assetPath}");
                    try
                    {
                        importer.SaveAndReimport();
                    }
                    catch (Exception ex)
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"SaveAndReimport failed for {assetPath}. Falling back. Error: {ex.Message}");
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    }
                }
                else
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Applied settings to importer without reimport: {assetPath}");
                }
            }
            else
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"No importer changes detected for: {assetPath}");
            }

            return changed;
        }

        public static bool TryEvaluateFromConfigForAsset(string assetPath, UnityEngine.Object contextAsset, out string gameKey, out System.Collections.Generic.List<string> differences)
        {
            gameKey = null;
            differences = null;

            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string contextPath = !string.IsNullOrEmpty(assetPath) ? assetPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
            var (detectedGameKey, _, _) = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, contextPath, silent: true);
            if (string.IsNullOrEmpty(detectedGameKey))
            {
                return false;
            }

            var metaMap = Api.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(detectedGameKey, out var gameMeta) || gameMeta == null)
            {
                return false;
            }

            var defaults = gameMeta.ModelImportSettings != null ? gameMeta.ModelImportSettings.Defaults : null;
            if (defaults == null)
            {
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            var dto = MapRuleToDto(defaults);
            var hasChanges = HasDifferences(importer, dto, out differences);
            var remapRequested = defaults.MaterialSearchAndRemap.HasValue && defaults.MaterialSearchAndRemap.Value;
            if (remapRequested && !hasChanges)
            {
                differences = differences ?? new System.Collections.Generic.List<string>();
                differences.Add("MaterialSearchAndRemap requested");
                hasChanges = true;
            }

            if (hasChanges)
            {
                gameKey = detectedGameKey;
            }

            return hasChanges;
        }

        public static bool TryApplyFromConfigForAsset(string assetPath, UnityEngine.Object contextAsset = null, bool reimportIfChanged = true)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return false;

            string contextPath = !string.IsNullOrEmpty(assetPath) ? assetPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
            var (gameKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, contextPath, silent: true);
            if (string.IsNullOrEmpty(gameKey))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Model rules: Could not detect game for '{assetPath}'. Skipping.");
                return false;
            }

            var metaMap = Api.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(gameKey, out var gameMeta) || gameMeta == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Model rules: No metadata found for game '{gameKey}' while processing '{assetPath}'.");
                return false;
            }

            var defaults = gameMeta.ModelImportSettings != null ? gameMeta.ModelImportSettings.Defaults : null;
            if (defaults == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Model rules: Game '{gameKey}' has no ModelImportSettings.Defaults. Skipping '{assetPath}'.");
                return false;
            }

            var dto = MapRuleToDto(defaults);
            var changed = TryApplyModelImportSettings(assetPath, dto, reimportIfChanged);
            try
            {
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null)
                {
                    TrySearchAndRemapMaterials(assetPath, importer, defaults);
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Post-apply material remap failed for '{assetPath}': {ex.Message}");
            }
            if (changed)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Model rules: Applied config defaults for game '{gameKey}' (source: {sourceJson ?? "<auto>"}) to '{assetPath}'.");
            }
            return changed;
        }
        public static int TryApplyFromConfigBatch(System.Collections.Generic.IEnumerable<string> assetPaths, UnityEngine.Object contextAsset = null)
        {
            if (assetPaths == null) return 0;
            int applied = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var assetPath in assetPaths)
                {
                    if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                    if (TryApplyFromConfigForAsset(assetPath, contextAsset, true)) applied++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            if (applied > 0) AssetDatabase.SaveAssets();
            return applied;
        }

        private static HoyoToonModelImportSettings MapRuleToDto(HoyoToon.Editor.API.ModelImportRule rule)
        {
            var dto = new HoyoToonModelImportSettings();
            if (rule == null) return dto;

            // MODEL
            if (rule.ScaleFactor.HasValue) dto.globalScale = rule.ScaleFactor.Value;
            if (rule.UseFileScale.HasValue) dto.useFileScale = rule.UseFileScale.Value;
            if (rule.ImportBlendShapes.HasValue) dto.importBlendShapes = rule.ImportBlendShapes.Value;
            if (rule.ImportVisibility.HasValue) dto.importVisibility = rule.ImportVisibility.Value;
            if (rule.ImportCameras.HasValue) dto.importCameras = rule.ImportCameras.Value;
            if (rule.ImportLights.HasValue) dto.importLights = rule.ImportLights.Value;
            if (rule.IsReadable.HasValue) dto.isReadable = rule.IsReadable.Value;
            if (rule.OptimizeMeshPolygons.HasValue) dto.optimizeMeshPolygons = rule.OptimizeMeshPolygons.Value;
            if (rule.OptimizeMeshVertices.HasValue) dto.optimizeMeshVertices = rule.OptimizeMeshVertices.Value;
            if (!string.IsNullOrEmpty(rule.Normals) && Enum.TryParse<ModelImporterNormals>(rule.Normals, true, out var normals)) dto.normals = normals;
            if (!string.IsNullOrEmpty(rule.Tangents) && Enum.TryParse<ModelImporterTangents>(rule.Tangents, true, out var tangents)) dto.tangents = tangents;

            // RIG
            if (!string.IsNullOrEmpty(rule.AnimationType) && Enum.TryParse<ModelImporterAnimationType>(rule.AnimationType, true, out var at)) dto.animationType = at;
            if (!string.IsNullOrEmpty(rule.AvatarSetup) && Enum.TryParse<ModelImporterAvatarSetup>(rule.AvatarSetup, true, out var av)) dto.avatarSetup = av;
            if (rule.BakeAxisConversion.HasValue) dto.bakeAxisConversion = rule.BakeAxisConversion.Value;

            // ANIM
            if (rule.ImportAnimation.HasValue) dto.importAnimation = rule.ImportAnimation.Value;
            if (!string.IsNullOrEmpty(rule.AnimationCompression) && Enum.TryParse<ModelImporterAnimationCompression>(rule.AnimationCompression, true, out var ac)) dto.animationCompression = ac;
            if (rule.ResampleCurves.HasValue) dto.resampleCurves = rule.ResampleCurves.Value;

            // MATERIALS
            var materialImportModeStr = rule.MaterialImportMode;
            var materialSearchStr = rule.MaterialSearch;
            var materialNameStr = rule.MaterialName;
            var materialLocationStr = rule.MaterialLocation;

            if (!string.IsNullOrEmpty(materialImportModeStr) && Enum.TryParse<ModelImporterMaterialImportMode>(materialImportModeStr, true, out var mim)) dto.materialImportMode = mim;
            if (!string.IsNullOrEmpty(materialSearchStr) && Enum.TryParse<ModelImporterMaterialSearch>(materialSearchStr, true, out var mis)) dto.materialSearch = mis;
            if (!string.IsNullOrEmpty(materialNameStr) && Enum.TryParse<ModelImporterMaterialName>(materialNameStr, true, out var min)) dto.materialName = min;
            if (!string.IsNullOrEmpty(materialLocationStr) && Enum.TryParse<ModelImporterMaterialLocation>(materialLocationStr, true, out var mil)) dto.materialLocation = mil;

            // EXTRA
            if (rule.LegacyBlendshapeNormals.HasValue) dto.legacyBlendshapeNormals = rule.LegacyBlendshapeNormals.Value;

            return dto;
        }

        private static bool HasDifferences(ModelImporter importer, HoyoToonModelImportSettings settings, out System.Collections.Generic.List<string> differences)
        {
            var diffs = new System.Collections.Generic.List<string>();
            if (importer == null || settings == null)
            {
                differences = diffs;
                return false;
            }

            void AddDiff(string label, object current, object desired)
            {
                diffs.Add($"{label}: {current} -> {desired}");
            }

            bool changed = false;
            ProcessCommonSettings(importer, settings, applyChanges: false, ref changed, AddDiff);

            if (settings.legacyBlendshapeNormals.HasValue)
            {
                var legacyValue = TryGetLegacyBlendshapeNormals(importer, out var current) ? (bool?)current : null;
                if (legacyValue.HasValue && legacyValue.Value != settings.legacyBlendshapeNormals.Value)
                {
                    AddDiff("Legacy Blendshape Normals", legacyValue.Value, settings.legacyBlendshapeNormals.Value);
                }
            }

            differences = diffs;
            return diffs.Count > 0;
        }

        private static void ProcessCommonSettings(
            ModelImporter importer,
            HoyoToonModelImportSettings settings,
            bool applyChanges,
            ref bool changed,
            Action<string, object, object> addDiff)
        {
            // MODEL TAB
            ProcessNullableSetting("Global Scale", settings.globalScale, () => importer.globalScale, v => importer.globalScale = v, ref changed, applyChanges, addDiff, Mathf.Approximately);
            ProcessNullableSetting("Use File Scale", settings.useFileScale, () => importer.useFileScale, v => importer.useFileScale = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Import BlendShapes", settings.importBlendShapes, () => importer.importBlendShapes, v => importer.importBlendShapes = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Import Visibility", settings.importVisibility, () => importer.importVisibility, v => importer.importVisibility = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Import Cameras", settings.importCameras, () => importer.importCameras, v => importer.importCameras = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Import Lights", settings.importLights, () => importer.importLights, v => importer.importLights = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Read/Write Enabled", settings.isReadable, () => importer.isReadable, v => importer.isReadable = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Optimize Mesh Polygons", settings.optimizeMeshPolygons, () => importer.optimizeMeshPolygons, v => importer.optimizeMeshPolygons = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Optimize Mesh Vertices", settings.optimizeMeshVertices, () => importer.optimizeMeshVertices, v => importer.optimizeMeshVertices = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Normals", settings.normals, () => importer.importNormals, v => importer.importNormals = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Tangents", settings.tangents, () => importer.importTangents, v => importer.importTangents = v, ref changed, applyChanges, addDiff);

            // RIG TAB
            ProcessNullableSetting("Rig Animation Type", settings.animationType, () => importer.animationType, v => importer.animationType = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Avatar Setup", settings.avatarSetup, () => importer.avatarSetup, v => importer.avatarSetup = v, ref changed, applyChanges, addDiff);
            ProcessReferenceSetting(
                "Source Avatar",
                settings.sourceAvatar,
                () => importer.sourceAvatar,
                v => importer.sourceAvatar = v,
                ref changed,
                applyChanges,
                addDiff,
                current => current ? current.name : "<none>",
                desired => desired ? desired.name : "<none>");
            ProcessNullableSetting("Bake Axis Conversion", settings.bakeAxisConversion, () => importer.bakeAxisConversion, v => importer.bakeAxisConversion = v, ref changed, applyChanges, addDiff);

            // ANIMATION TAB
            ProcessNullableSetting("Import Animation", settings.importAnimation, () => importer.importAnimation, v => importer.importAnimation = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Animation Compression", settings.animationCompression, () => importer.animationCompression, v => importer.animationCompression = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Resample Curves", settings.resampleCurves, () => importer.resampleCurves, v => importer.resampleCurves = v, ref changed, applyChanges, addDiff);

            // MATERIALS TAB
            ProcessNullableSetting("Material Import Mode", settings.materialImportMode, () => importer.materialImportMode, v => importer.materialImportMode = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Material Search", settings.materialSearch, () => importer.materialSearch, v => importer.materialSearch = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Material Name", settings.materialName, () => importer.materialName, v => importer.materialName = v, ref changed, applyChanges, addDiff);
            ProcessNullableSetting("Material Location", settings.materialLocation, () => importer.materialLocation, v => importer.materialLocation = v, ref changed, applyChanges, addDiff);
        }

        private static void ProcessNullableSetting<T>(
            string label,
            T? desired,
            Func<T> getter,
            Action<T> setter,
            ref bool changed,
            bool applyChanges,
            Action<string, object, object> addDiff,
            Func<T, T, bool> equals = null)
            where T : struct
        {
            if (!desired.HasValue)
            {
                return;
            }

            var current = getter();
            var desiredValue = desired.Value;
            var isEqual = equals != null ? equals(current, desiredValue) : EqualityComparer<T>.Default.Equals(current, desiredValue);
            if (isEqual)
            {
                return;
            }

            if (applyChanges)
            {
                setter(desiredValue);
                changed = true;
                return;
            }

            addDiff?.Invoke(label, current, desiredValue);
            changed = true;
        }

        private static void ProcessReferenceSetting<T>(
            string label,
            T desired,
            Func<T> getter,
            Action<T> setter,
            ref bool changed,
            bool applyChanges,
            Action<string, object, object> addDiff,
            Func<T, object> currentDisplay,
            Func<T, object> desiredDisplay)
            where T : class
        {
            if (desired == null)
            {
                return;
            }

            var current = getter();
            if (ReferenceEquals(current, desired))
            {
                return;
            }

            if (applyChanges)
            {
                setter(desired);
                changed = true;
                return;
            }

            addDiff?.Invoke(label, currentDisplay(current), desiredDisplay(desired));
            changed = true;
        }


        private static bool TryGetLegacyBlendshapeNormals(ModelImporter importer, out bool value)
        {
            value = false;
            if (importer == null)
            {
                return false;
            }

            try
            {
                string pName = "legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes";
                var prop = importer.GetType().GetProperty(pName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (prop != null)
                {
                    var oldObj = prop.GetValue(importer);
                    if (oldObj is bool b)
                    {
                        value = b;
                        return true;
                    }
                }
            }
            catch
            {
                // ignore reflection failures
            }

            return false;
        }

        private static bool TrySetLegacyBlendshapeNormals(ModelImporter importer, bool value)
        {
            if (importer == null) return false;
            try
            {
                string pName = "legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes";
                var prop = importer.GetType().GetProperty(pName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (prop != null)
                {
                    prop.SetValue(importer, value);
                    return true;
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Could not set legacyBlendshapeNormals: {ex.Message}");
            }
            return false;
        }

        private static void TrySearchAndRemapMaterials(string assetPath, ModelImporter importer, HoyoToon.Editor.API.ModelImportRule rule)
        {
            if (importer == null || rule == null) return;
            bool doRemap = rule.MaterialSearchAndRemap.HasValue && rule.MaterialSearchAndRemap.Value;
            if (!doRemap) return;

            try
            {
                var search = importer.materialSearch;
                var name = importer.materialName;
                importer.SearchAndRemapMaterials(name, search);
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Info, $"Material remap attempted for '{assetPath}' using Name={name}, Search={search}.");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Model, LogLevel.Warning, $"Material remap failed for '{assetPath}': {ex.Message}");
            }
        }
    }
}
#endif
