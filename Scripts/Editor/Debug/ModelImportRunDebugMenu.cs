#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using HoyoToon.Utilities;
using HoyoToon.Models;

namespace HoyoToon.Debugging
{
    internal static class ModelImportRunDebugMenu
    {
        // Snapshot of key ModelImporter properties for diff reporting
        private struct ImporterState
        {
            public float GlobalScale;
            public bool UseFileScale;
            public bool ImportBlendShapes;
            public bool ImportVisibility;
            public bool ImportCameras;
            public bool ImportLights;
            public bool IsReadable;
            public bool OptimizeMeshPolygons;
            public bool OptimizeMeshVertices;
            public ModelImporterNormals Normals;
            public ModelImporterTangents Tangents;
            public ModelImporterAnimationType AnimationType;
            public ModelImporterAvatarSetup AvatarSetup;
            public bool BakeAxisConversion;
            public bool ImportAnimation;
            public ModelImporterAnimationCompression AnimationCompression;
            public bool ResampleCurves;
            public ModelImporterMaterialImportMode MaterialImportMode;
            public ModelImporterMaterialSearch MaterialSearch;
            public ModelImporterMaterialName MaterialName;
            public ModelImporterMaterialLocation MaterialLocation;
            public bool? LegacyBlendshapeNormals;
        }

        private static ImporterState? TryCaptureState(string assetPath)
        {
            var imp = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (imp == null) return null;
            return new ImporterState
            {
                GlobalScale = imp.globalScale,
                UseFileScale = imp.useFileScale,
                ImportBlendShapes = imp.importBlendShapes,
                ImportVisibility = imp.importVisibility,
                ImportCameras = imp.importCameras,
                ImportLights = imp.importLights,
                IsReadable = imp.isReadable,
                OptimizeMeshPolygons = imp.optimizeMeshPolygons,
                OptimizeMeshVertices = imp.optimizeMeshVertices,
                Normals = imp.importNormals,
                Tangents = imp.importTangents,
                AnimationType = imp.animationType,
                AvatarSetup = imp.avatarSetup,
                BakeAxisConversion = imp.bakeAxisConversion,
                ImportAnimation = imp.importAnimation,
                AnimationCompression = imp.animationCompression,
                ResampleCurves = imp.resampleCurves,
                MaterialImportMode = imp.materialImportMode,
                MaterialSearch = imp.materialSearch,
                MaterialName = imp.materialName,
                MaterialLocation = imp.materialLocation,
                LegacyBlendshapeNormals = TryGetLegacyBlendshapeNormals(imp)
            };
        }

        private static bool? TryGetLegacyBlendshapeNormals(ModelImporter importer)
        {
            try
            {
                const string propName = "legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes";
                var prop = importer.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (prop == null) return null;
                var val = prop.GetValue(importer);
                if (val is bool b) return b;
            }
            catch { }
            return null;
        }

        private static List<string> DiffStates(ImporterState before, ImporterState after)
        {
            var diffs = new List<string>();

            void Add(string name, object a, object b) { diffs.Add($"  - {name}: {a} -> {b}"); }
            bool Approx(float a, float b) => Math.Abs(a - b) > 1e-4f;

            if (Approx(before.GlobalScale, after.GlobalScale)) Add("GlobalScale", before.GlobalScale, after.GlobalScale);
            if (before.UseFileScale != after.UseFileScale) Add("UseFileScale", before.UseFileScale, after.UseFileScale);
            if (before.ImportBlendShapes != after.ImportBlendShapes) Add("ImportBlendShapes", before.ImportBlendShapes, after.ImportBlendShapes);
            if (before.ImportVisibility != after.ImportVisibility) Add("ImportVisibility", before.ImportVisibility, after.ImportVisibility);
            if (before.ImportCameras != after.ImportCameras) Add("ImportCameras", before.ImportCameras, after.ImportCameras);
            if (before.ImportLights != after.ImportLights) Add("ImportLights", before.ImportLights, after.ImportLights);
            if (before.IsReadable != after.IsReadable) Add("IsReadable", before.IsReadable, after.IsReadable);
            if (before.OptimizeMeshPolygons != after.OptimizeMeshPolygons) Add("OptimizeMeshPolygons", before.OptimizeMeshPolygons, after.OptimizeMeshPolygons);
            if (before.OptimizeMeshVertices != after.OptimizeMeshVertices) Add("OptimizeMeshVertices", before.OptimizeMeshVertices, after.OptimizeMeshVertices);
            if (!Equals(before.Normals, after.Normals)) Add("Normals", before.Normals, after.Normals);
            if (!Equals(before.Tangents, after.Tangents)) Add("Tangents", before.Tangents, after.Tangents);
            if (!Equals(before.AnimationType, after.AnimationType)) Add("AnimationType", before.AnimationType, after.AnimationType);
            if (!Equals(before.AvatarSetup, after.AvatarSetup)) Add("AvatarSetup", before.AvatarSetup, after.AvatarSetup);
            if (before.BakeAxisConversion != after.BakeAxisConversion) Add("BakeAxisConversion", before.BakeAxisConversion, after.BakeAxisConversion);
            if (before.ImportAnimation != after.ImportAnimation) Add("ImportAnimation", before.ImportAnimation, after.ImportAnimation);
            if (!Equals(before.AnimationCompression, after.AnimationCompression)) Add("AnimationCompression", before.AnimationCompression, after.AnimationCompression);
            if (before.ResampleCurves != after.ResampleCurves) Add("ResampleCurves", before.ResampleCurves, after.ResampleCurves);
            if (!Equals(before.MaterialImportMode, after.MaterialImportMode)) Add("MaterialImportMode", before.MaterialImportMode, after.MaterialImportMode);
            if (!Equals(before.MaterialSearch, after.MaterialSearch)) Add("MaterialSearch", before.MaterialSearch, after.MaterialSearch);
            if (!Equals(before.MaterialName, after.MaterialName)) Add("MaterialName", before.MaterialName, after.MaterialName);
            if (!Equals(before.MaterialLocation, after.MaterialLocation)) Add("MaterialLocation", before.MaterialLocation, after.MaterialLocation);

            if (before.LegacyBlendshapeNormals != after.LegacyBlendshapeNormals)
            {
                var a = before.LegacyBlendshapeNormals.HasValue ? before.LegacyBlendshapeNormals.Value.ToString() : "n/a";
                var b = after.LegacyBlendshapeNormals.HasValue ? after.LegacyBlendshapeNormals.Value.ToString() : "n/a";
                Add("LegacyBlendshapeNormals", a, b);
            }

            return diffs;
        }

        [MenuItem("Assets/HoyoToon/Models/Run Model Import For Selection", false, 2095)]
        private static void RunModelImportForSelection()
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                HoyoToonDialogWindow.ShowInfo("HoyoToon Models", "Select one or more FBX assets or folders to run the Model Import using config defaults (auto-detected per game).");
                return;
            }

            var assetPaths = selected
                .SelectMany(EnumerateFbxAssetsUnder)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (assetPaths.Count == 0)
            {
                HoyoToonDialogWindow.ShowInfo("HoyoToon Models", "No FBX assets found in selection.");
                return;
            }

            EditorUtility.DisplayProgressBar("HoyoToon Models", "Running Model Import...", 0f);
            int changed = 0;
            var sb = new StringBuilder();
            sb.AppendLine("# HoyoToon Model Import\n");
            try
            {
                for (int i = 0; i < assetPaths.Count; i++)
                {
                    var path = assetPaths[i];
                    float p = (float)i / Math.Max(1, assetPaths.Count);
                    EditorUtility.DisplayProgressBar("HoyoToon Models", $"Applying: {Path.GetFileName(path)}", p);

                    var before = TryCaptureState(path);
                    bool applied = ModelImportRulesApplier.TryApplyFromConfigForAsset(path, null, true);
                    var after = TryCaptureState(path);

                    if (applied)
                    {
                        changed++;
                        sb.AppendLine($"- Applied: `{path}`");
                        if (before.HasValue && after.HasValue)
                        {
                            var diffs = DiffStates(before.Value, after.Value);
                            if (diffs.Count > 0)
                            {
                                foreach (var d in diffs) sb.AppendLine(d);
                            }
                            else
                            {
                                sb.AppendLine("  - No field differences detected (already matched)");
                            }
                        }
                        else
                        {
                            sb.AppendLine("  - (Could not capture importer state for diff)");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"- Skipped: `{path}`");
                        if (before.HasValue && after.HasValue)
                        {
                            var diffs = DiffStates(before.Value, after.Value);
                            if (diffs.Count > 0)
                            {
                                sb.AppendLine("  - Note: Importer changed despite 'skipped':");
                                foreach (var d in diffs) sb.AppendLine(d);
                            }
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            sb.AppendLine("\n---\n");
            sb.AppendLine($"Processed {assetPaths.Count} FBX asset(s). Changed: {changed}.");
            HoyoToonDialogWindow.ShowInfo("HoyoToon Models", sb.ToString());
        }

        private static IEnumerable<string> EnumerateFbxAssetsUnder(UnityEngine.Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) yield break;

            if (Directory.Exists(path))
            {
                // Enumerate model assets in folder and filter .fbx
                var guids = AssetDatabase.FindAssets("t:Model", new[] { path });
                foreach (var guid in guids)
                {
                    var ap = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(ap) && ap.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                        yield return ap;
                }
            }
            else if (File.Exists(path) && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }
}
#endif
