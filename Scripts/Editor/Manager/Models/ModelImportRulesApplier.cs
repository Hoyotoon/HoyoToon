#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;
using HoyoToon.API;
using System.Reflection;

namespace HoyoToon.Manager.Models
{
    /// <summary>
    /// Utilities to read/modify ModelImporter settings for FBX assets with consistent logging.
    /// </summary>
    public static class ModelImportRulesApplier
    {
        public static bool TryReadModelSnapshot(string assetPath, out HoyoToonModelImportSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonLogger.ModelWarning($"TryReadModelSnapshot ignored non-FBX path: {assetPath}");
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                HoyoToonLogger.ModelError($"No ModelImporter found at path: {assetPath}");
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
                HoyoToonLogger.ModelError("TryApplyModelImportSettings called with null settings.");
                return false;
            }
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonLogger.ModelWarning($"TryApplyModelImportSettings ignored non-FBX path: {assetPath}");
                return false;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                HoyoToonLogger.ModelError($"No ModelImporter found at path: {assetPath}");
                return false;
            }

            bool changed = false;

            // MODEL TAB
            if (settings.globalScale.HasValue && !Mathf.Approximately(importer.globalScale, settings.globalScale.Value))
            { importer.globalScale = settings.globalScale.Value; changed = true; }
            if (settings.useFileScale.HasValue && importer.useFileScale != settings.useFileScale.Value)
            { importer.useFileScale = settings.useFileScale.Value; changed = true; }
            if (settings.importBlendShapes.HasValue && importer.importBlendShapes != settings.importBlendShapes.Value)
            { importer.importBlendShapes = settings.importBlendShapes.Value; changed = true; }
            if (settings.importVisibility.HasValue && importer.importVisibility != settings.importVisibility.Value)
            { importer.importVisibility = settings.importVisibility.Value; changed = true; }
            if (settings.importCameras.HasValue && importer.importCameras != settings.importCameras.Value)
            { importer.importCameras = settings.importCameras.Value; changed = true; }
            if (settings.importLights.HasValue && importer.importLights != settings.importLights.Value)
            { importer.importLights = settings.importLights.Value; changed = true; }
            if (settings.isReadable.HasValue && importer.isReadable != settings.isReadable.Value)
            { importer.isReadable = settings.isReadable.Value; changed = true; }
            if (settings.optimizeMeshPolygons.HasValue && importer.optimizeMeshPolygons != settings.optimizeMeshPolygons.Value)
            { importer.optimizeMeshPolygons = settings.optimizeMeshPolygons.Value; changed = true; }
            if (settings.optimizeMeshVertices.HasValue && importer.optimizeMeshVertices != settings.optimizeMeshVertices.Value)
            { importer.optimizeMeshVertices = settings.optimizeMeshVertices.Value; changed = true; }
            if (settings.normals.HasValue && importer.importNormals != settings.normals.Value)
            { importer.importNormals = settings.normals.Value; changed = true; }
            if (settings.tangents.HasValue && importer.importTangents != settings.tangents.Value)
            { importer.importTangents = settings.tangents.Value; changed = true; }

            // RIG TAB
            if (settings.animationType.HasValue && importer.animationType != settings.animationType.Value)
            { importer.animationType = settings.animationType.Value; changed = true; }
            if (settings.avatarSetup.HasValue && importer.avatarSetup != settings.avatarSetup.Value)
            { importer.avatarSetup = settings.avatarSetup.Value; changed = true; }
            if (settings.sourceAvatar != null && importer.sourceAvatar != settings.sourceAvatar)
            { importer.sourceAvatar = settings.sourceAvatar; changed = true; }
            if (settings.bakeAxisConversion.HasValue && importer.bakeAxisConversion != settings.bakeAxisConversion.Value)
            { importer.bakeAxisConversion = settings.bakeAxisConversion.Value; changed = true; }

            // ANIMATION TAB
            if (settings.importAnimation.HasValue && importer.importAnimation != settings.importAnimation.Value)
            { importer.importAnimation = settings.importAnimation.Value; changed = true; }
            if (settings.animationCompression.HasValue && importer.animationCompression != settings.animationCompression.Value)
            { importer.animationCompression = settings.animationCompression.Value; changed = true; }
            if (settings.resampleCurves.HasValue && importer.resampleCurves != settings.resampleCurves.Value)
            { importer.resampleCurves = settings.resampleCurves.Value; changed = true; }

            // MATERIALS TAB
            if (settings.materialImportMode.HasValue && importer.materialImportMode != settings.materialImportMode.Value)
            { importer.materialImportMode = settings.materialImportMode.Value; changed = true; }
            if (settings.materialSearch.HasValue && importer.materialSearch != settings.materialSearch.Value)
            { importer.materialSearch = settings.materialSearch.Value; changed = true; }
            if (settings.materialName.HasValue && importer.materialName != settings.materialName.Value)
            { importer.materialName = settings.materialName.Value; changed = true; }
            if (settings.materialLocation.HasValue && importer.materialLocation != settings.materialLocation.Value)
            { importer.materialLocation = settings.materialLocation.Value; changed = true; }

            // EXTRA: Legacy Blendshape Normals via reflection
            if (settings.legacyBlendshapeNormals.HasValue)
            {
                try
                {
                    string pName = "legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes";
                    var prop = importer.GetType().GetProperty(pName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (prop != null)
                    {
                        var oldObj = prop.GetValue(importer);
                        bool oldVal = oldObj is bool b && b;
                        if (oldVal != settings.legacyBlendshapeNormals.Value)
                        {
                            prop.SetValue(importer, settings.legacyBlendshapeNormals.Value);
                            changed = true;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    HoyoToonLogger.ModelWarning($"Could not set legacyBlendshapeNormals: {ex.Message}");
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                if (reimport)
                {
                    HoyoToonLogger.ModelInfo($"Reimporting with applied settings: {assetPath}");
                    try
                    {
                        importer.SaveAndReimport();
                    }
                    catch (Exception ex)
                    {
                        HoyoToonLogger.ModelWarning($"SaveAndReimport failed for {assetPath}. Falling back. Error: {ex.Message}");
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    }
                }
                else
                {
                    HoyoToonLogger.ModelInfo($"Applied settings to importer without reimport: {assetPath}");
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
                AssetDatabase.SaveAssets();
            }
            else
            {
                HoyoToonLogger.ModelInfo($"No importer changes detected for: {assetPath}");
            }

            return changed;
        }

        /// <summary>
        /// Detect game using MaterialDetection and apply configured ModelImportSettings.Defaults for that game.
        /// Returns true if changes were applied.
        /// </summary>
        public static bool TryApplyFromConfigForAsset(string assetPath, UnityEngine.Object contextAsset = null, bool reimportIfChanged = true)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return false;

            // Detect game using MaterialDetection, leveraging nearby materials JSONs if available
            string contextPath = !string.IsNullOrEmpty(assetPath) ? assetPath : (contextAsset != null ? AssetDatabase.GetAssetPath(contextAsset) : null);
            var (gameKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(contextAsset, contextPath);
            if (string.IsNullOrEmpty(gameKey))
            {
                HoyoToonLogger.ModelInfo($"Model rules: Could not detect game for '{assetPath}'. Skipping.");
                return false;
            }

            var metaMap = HoyoToonApi.GetGameMetadata();
            if (metaMap == null || !metaMap.TryGetValue(gameKey, out var gameMeta) || gameMeta == null)
            {
                HoyoToonLogger.ModelInfo($"Model rules: No metadata found for game '{gameKey}' while processing '{assetPath}'.");
                return false;
            }

            var defaults = gameMeta.ModelImportSettings != null ? gameMeta.ModelImportSettings.Defaults : null;
            if (defaults == null)
            {
                HoyoToonLogger.ModelInfo($"Model rules: Game '{gameKey}' has no ModelImportSettings.Defaults. Skipping '{assetPath}'.");
                return false;
            }

            var dto = MapRuleToDto(defaults);
            var changed = TryApplyModelImportSettings(assetPath, dto, reimportIfChanged);
            try
            {
                // After applying settings, optionally perform a search & remap pass if requested by config
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null)
                {
                    TrySearchAndRemapMaterials(assetPath, importer, defaults);
                }
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ModelWarning($"Post-apply material remap failed for '{assetPath}': {ex.Message}");
            }
            if (changed)
            {
                HoyoToonLogger.ModelInfo($"Model rules: Applied config defaults for game '{gameKey}' (source: {sourceJson ?? "<auto>"}) to '{assetPath}'.");
            }
            return changed;
        }

        /// <summary>
        /// Batch-apply config defaults for a set of FBX assets using MaterialDetection-based game detection.
        /// Returns number of assets changed.
        /// </summary>
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

        private static HoyoToonModelImportSettings MapRuleToDto(HoyoToon.API.ModelImportRule rule)
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
            if (!string.IsNullOrEmpty(rule.Normals) && EnumTryParseIgnoreCase<ModelImporterNormals>(rule.Normals, out var normals)) dto.normals = normals;
            if (!string.IsNullOrEmpty(rule.Tangents) && EnumTryParseIgnoreCase<ModelImporterTangents>(rule.Tangents, out var tangents)) dto.tangents = tangents;

            // RIG
            if (!string.IsNullOrEmpty(rule.AnimationType) && EnumTryParseIgnoreCase<ModelImporterAnimationType>(rule.AnimationType, out var at)) dto.animationType = at;
            if (!string.IsNullOrEmpty(rule.AvatarSetup) && EnumTryParseIgnoreCase<ModelImporterAvatarSetup>(rule.AvatarSetup, out var av)) dto.avatarSetup = av;
            if (rule.BakeAxisConversion.HasValue) dto.bakeAxisConversion = rule.BakeAxisConversion.Value;

            // ANIM
            if (rule.ImportAnimation.HasValue) dto.importAnimation = rule.ImportAnimation.Value;
            if (!string.IsNullOrEmpty(rule.AnimationCompression) && EnumTryParseIgnoreCase<ModelImporterAnimationCompression>(rule.AnimationCompression, out var ac)) dto.animationCompression = ac;
            if (rule.ResampleCurves.HasValue) dto.resampleCurves = rule.ResampleCurves.Value;

            // MATERIALS
            var materialImportModeStr = rule.MaterialImportMode;
            var materialSearchStr = rule.MaterialSearch;
            var materialNameStr = rule.MaterialName;
            var materialLocationStr = rule.MaterialLocation;

            if (!string.IsNullOrEmpty(materialImportModeStr) && EnumTryParseIgnoreCase<ModelImporterMaterialImportMode>(materialImportModeStr, out var mim)) dto.materialImportMode = mim;
            if (!string.IsNullOrEmpty(materialSearchStr) && EnumTryParseIgnoreCase<ModelImporterMaterialSearch>(materialSearchStr, out var mis)) dto.materialSearch = mis;
            if (!string.IsNullOrEmpty(materialNameStr) && EnumTryParseIgnoreCase<ModelImporterMaterialName>(materialNameStr, out var min)) dto.materialName = min;
            if (!string.IsNullOrEmpty(materialLocationStr) && EnumTryParseIgnoreCase<ModelImporterMaterialLocation>(materialLocationStr, out var mil)) dto.materialLocation = mil;

            // EXTRA
            if (rule.LegacyBlendshapeNormals.HasValue) dto.legacyBlendshapeNormals = rule.LegacyBlendshapeNormals.Value;

            return dto;
        }

        private static bool EnumTryParseIgnoreCase<TEnum>(string value, out TEnum result) where TEnum : struct
        {
            return System.Enum.TryParse(value, true, out result);
        }

        /// <summary>
        /// If config requests a search & remap pass, attempt to relink materials after import according to importer settings.
        /// </summary>
        private static void TrySearchAndRemapMaterials(string assetPath, ModelImporter importer, HoyoToon.API.ModelImportRule rule)
        {
            if (importer == null || rule == null) return;
            bool doRemap = rule.MaterialSearchAndRemap.HasValue && rule.MaterialSearchAndRemap.Value;
            if (!doRemap) return;

            try
            {
                // Ensure importer uses the desired search/naming before remap
                // Unity will perform remapping on SaveAndReimport based on these settings; to be safe, we explicitly call SearchAndRemap
                var search = importer.materialSearch;
                var name = importer.materialName;
                // Perform a remap pass using the current importer settings
                importer.SearchAndRemapMaterials(name, search);
                HoyoToonLogger.ModelInfo($"Material remap attempted for '{assetPath}' using Name={name}, Search={search}.");
            }
            catch (Exception ex)
            {
                HoyoToonLogger.ModelWarning($"Material remap failed for '{assetPath}': {ex.Message}");
            }
        }
    }
}
#endif
