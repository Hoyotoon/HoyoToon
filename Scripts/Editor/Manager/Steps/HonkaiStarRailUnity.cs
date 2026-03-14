#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HoyoToon.Editor.UI.ManagerInspector;
using HoyoToon.Editor.UI.ManagerInspector.Setup;
using HoyoToon;
using HoyoToon.Editor.AssetPipeline.Models;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.AssetPipeline.Materials;
using HoyoToon.Simulator.Camera;
using HoyoToon.Simulator.Utilities;
using HoyoToon.Runtime.Character;
using HoyoToon.Editor.UI.ManagerInspector.Modules;


using HoyoToon.Editor.AssetPipeline.Scene;

namespace HoyoToon.Editor.UI.ManagerInspector.Setup
{
    [InitializeOnLoad]
    internal static class HonkaiStarRailUnity
    {
        private const string HonkaiStarRailKey = GameConstants.HonkaiStarRail;
        private static readonly string[] s_RequiredCompanionMaterialNames =
        {
            "Mat_EyeShadow.mat",
            "Mat_EyeShadow_00.mat",
            "Mat_EyeShadow_01.mat",
            "Mat_FaceMask.mat"
        };
        private static readonly string[] s_CompanionMaterialSourceRoots =
        {
            "Packages/HoyoToon/Resources/Honkai Star Rail/Materials/",
            "Packages/com.hoyotoon.hoyotoon/Resources/Honkai Star Rail/Materials/"
        };

        static HonkaiStarRailUnity()
        {
            ModelSetupUtility.RegisterStepProvider(BuildSteps);
        }

        private static IEnumerable<ModelSetupUtility.SetupStep> BuildSteps(ModelSetupUtility.SetupContext context)
        {
            if (context == null)
            {
                return Array.Empty<ModelSetupUtility.SetupStep>();
            }

            if (context.IsVrcSdkInstalled)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup (HSR Unity): VRChat SDK detected; skipping Unity steps.");
                return Array.Empty<ModelSetupUtility.SetupStep>();
            }

            if (!string.Equals(context.DetectedGameKey, HonkaiStarRailKey, StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup (HSR Unity): Game mismatch; skipping steps.");
                return Array.Empty<ModelSetupUtility.SetupStep>();
            }

            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup (HSR Unity): Step provider active.");

            return new[]
            {
                new ModelSetupUtility.SetupStep
                {
                    Id = "hsr-unity-convert",
                    Title = "Convert FBX with Hoyo2VRC",
                    Enabled = SetupModelsHelper.ShouldConvertModel(context),
                    IsApplicable = ctx => ctx.IsFbxAsset,
                    IsRequired = ctx => SetupModelsHelper.NeedsConversion(ctx),
                    Execute = ExecuteConvertFbx
                },
                new ModelSetupUtility.SetupStep
                {
                    Id = "generate-materials",
                    Title = "Generating materials",
                    Enabled = SetupMaterialsHelper.ShouldGenerateMaterials(context),
                    IsApplicable = ctx => ctx.MaterialSources != null && ctx.MaterialSources.Count > 0,
                    IsRequired = ctx => SetupMaterialsHelper.HasMissingMaterials(ctx) || HasMissingCompanionMaterials(ctx),
                    Execute = ctx =>
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Auto Setup: Generating materials (Game: '{ctx.DetectedGameKey ?? "Unknown"}').");
                        MaterialGeneration.GenerateAuto(ctx.Asset, null, null, null, true);
                        EnsureRequiredCompanionMaterials(ctx);
                    }
                },

                new ModelSetupUtility.SetupStep
                {
                    Id = "apply-import-settings",
                    Title = "Applying FBX import settings",
                    Enabled = SetupModelsHelper.ShouldApplyImportSettings(context),
                    IsApplicable = ctx => ctx.IsFbxAsset,
                    IsRequired = ctx => SetupModelsHelper.NeedsModelImportSettings(ctx)
                        || SetupModelsHelper.NeedsConversion(ctx),
                    Execute = ctx =>
                    {
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: Applying model import rules.");
                        ModelImportRulesApplier.TryApplyFromConfigForAsset(ctx.AssetPath, ctx.Asset, true);
                    }
                },

                new ModelSetupUtility.SetupStep
                {
                    Id = "scene-light-controller",
                    Title = "Configuring Scene Controller",
                    Enabled = true,
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx =>
                    {
                        if (ctx.Manager == null) return true;
                        var controllerType = SceneControllerTypeResolver.ResolveHSRSceneControllerType();
                        if (controllerType == null) return false;
                        return UnityEngine.Object.FindFirstObjectByType(controllerType) == null;
                    },
                    Execute = ExecuteConfigureSceneController
                },

                new ModelSetupUtility.SetupStep
                {
                    Id = "instantiate-model",
                    Title = "Instantiating model",
                    Enabled = SetupSceneHelper.ShouldInstantiateModel(context),
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx => ctx.Options == null || (ctx.Options.InstantiateInScene && ctx.ExistingInstance == null),
                    Execute = ctx =>
                    {
                        ctx.CreatedInstance = SetupSceneHelper.InstantiateAssetInScene(
                            ctx.Manager,
                            ctx.Asset,
                            ctx.IsFbxAsset ? "Add HoyoToon Model" : "Add HoyoToon Prefab");
                        if (ctx.CreatedInstance == null)
                        {
                            throw new InvalidOperationException("Failed to instantiate the selected asset.");
                        }
                    }
                },

                new ModelSetupUtility.SetupStep
                {
                    Id = "setup-hsr-character-controller",
                    Title = "Adding HSR Character Controller",
                    Enabled = true,
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx =>
                    {
                        var existing = ctx.ExistingInstance;
                        if (existing != null)
                            return existing.GetComponent<HSRCharacterController>() == null;

                        return ctx.Options == null || ctx.Options.InstantiateInScene;
                    },
                    Execute = ExecuteSetupHsrCharacterController
                },

                new ModelSetupUtility.SetupStep
                {
                    Id = "apply-bone-constraints",
                    Title = "Applying bone constraints",
                    Enabled = true,
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx =>
                    {
                        var meta = ctx.DetectedGameMetadata;
                        if (meta?.BoneConstraints == null || meta.BoneConstraints.Count == 0)
                        {
                            return false;
                        }

                        return ctx.ExistingInstance != null
                               || (ctx.Options == null || ctx.Options.InstantiateInScene);
                    },
                    Execute = ExecuteApplyBoneConstraints
                },

                new ModelSetupUtility.SetupStep
                {
                    Id = "apply-tangents",
                    Title = "Applying tangent rules",
                    Enabled = true,
                    IsApplicable = ctx => (ctx.CreatedInstance ?? ctx.ExistingInstance ?? ctx.Asset) != null,
                    IsRequired = ctx => ctx.Options == null || ctx.Options.InstantiateInScene,
                    Execute = ctx =>
                    {
                        var target = ctx.CreatedInstance ?? ctx.ExistingInstance ?? ctx.Asset;
                        if (target == null)
                        {
                            return;
                        }

                        bool applied = TangentRulesApplier.TryApplyFromConfig(target);
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, applied
                            ? "Auto Setup: Tangent rules applied from config."
                            : "Auto Setup: Tangent rules did not apply any changes.");
                    }
                },
     
                new ModelSetupUtility.SetupStep
                {
                    Id = "setup-camera-targets",
                    Title = "Configuring camera targets",
                    Enabled = true,
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx =>
                    {
                        var existing = ctx.ExistingInstance;
                        if (existing != null)
                            return existing.GetComponent<CharacterCameraSetup>() == null;

                        return ctx.Options == null || ctx.Options.InstantiateInScene;
                    },
                    Execute = ExecuteSetupCameraTargets
                },

                
            };
        }

        #region Extracted Execute Methods

        private static bool HasMissingCompanionMaterials(ModelSetupUtility.SetupContext ctx)
        {
            if (ctx?.MaterialSources == null || ctx.MaterialSources.Count == 0)
            {
                return false;
            }

            HashSet<string> outputDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in ctx.MaterialSources)
            {
                if (string.IsNullOrEmpty(source.sourceJson) || string.Equals(source.sourceJson, "<raw-json>", StringComparison.Ordinal))
                {
                    continue;
                }

                string outputDirectory = SetupMaterialsHelper.ComputeMaterialOutputDirectory(source.sourceJson);
                if (string.IsNullOrEmpty(outputDirectory) || !outputDirectories.Add(outputDirectory))
                {
                    continue;
                }

                foreach (string materialFileName in s_RequiredCompanionMaterialNames)
                {
                    if (AssetDatabase.LoadAssetAtPath<Material>(GetOutputMaterialPath(outputDirectory, materialFileName)) == null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void EnsureRequiredCompanionMaterials(ModelSetupUtility.SetupContext ctx)
        {
            if (ctx?.MaterialSources == null || ctx.MaterialSources.Count == 0)
            {
                return;
            }

            bool copiedAny = false;
            HashSet<string> outputDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in ctx.MaterialSources)
            {
                if (string.IsNullOrEmpty(source.sourceJson) || string.Equals(source.sourceJson, "<raw-json>", StringComparison.Ordinal))
                {
                    continue;
                }

                string outputDirectory = SetupMaterialsHelper.ComputeMaterialOutputDirectory(source.sourceJson);
                if (string.IsNullOrEmpty(outputDirectory) || !outputDirectories.Add(outputDirectory))
                {
                    continue;
                }

                EditorUtil.EnsureDirectory(outputDirectory);
                copiedAny |= CopyCompanionMaterialsToDirectory(outputDirectory);
            }

            if (copiedAny)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static bool CopyCompanionMaterialsToDirectory(string outputDirectory)
        {
            bool copiedAny = false;
            foreach (string materialFileName in s_RequiredCompanionMaterialNames)
            {
                string sourceAssetPath = ResolveCompanionMaterialSourceAssetPath(materialFileName);
                if (string.IsNullOrEmpty(sourceAssetPath))
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning,
                        $"Auto Setup (HSR Unity): Companion material '{materialFileName}' was not found in the package resources.");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<Material>(sourceAssetPath) == null)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning,
                        $"Auto Setup (HSR Unity): Companion material '{materialFileName}' is missing from the package resources.");
                    continue;
                }

                string destinationAssetPath = GetOutputMaterialPath(outputDirectory, materialFileName);
                if (AssetDatabase.LoadAssetAtPath<Material>(destinationAssetPath) != null)
                {
                    continue;
                }

                if (!AssetDatabase.CopyAsset(sourceAssetPath, destinationAssetPath))
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning,
                        $"Auto Setup (HSR Unity): Failed to copy companion material '{materialFileName}' to '{destinationAssetPath}'.");
                    continue;
                }

                copiedAny = true;
            }

            return copiedAny;
        }

        private static string GetOutputMaterialPath(string outputDirectory, string materialFileName)
        {
            return EditorUtil.AbsoluteToUnityPath(outputDirectory.TrimEnd('/', '\\') + "/" + materialFileName);
        }

        private static string ResolveCompanionMaterialSourceAssetPath(string materialFileName)
        {
            for (int i = 0; i < s_CompanionMaterialSourceRoots.Length; i++)
            {
                string sourceAssetPath = s_CompanionMaterialSourceRoots[i] + materialFileName;
                if (AssetDatabase.LoadAssetAtPath<Material>(sourceAssetPath) != null)
                {
                    return sourceAssetPath;
                }
            }

            return null;
        }

        private static void ExecuteConvertFbx(ModelSetupUtility.SetupContext ctx)
        {
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup (HSR Unity): FBX not converted. Running converter.");
            GameObject convertedAsset = null;
            string convertedPath = null;

            bool resolved = ModelConverter.TryProcessAndGetOutput(ctx.Asset, out convertedPath, out convertedAsset);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var preferredName = MaterialDetection.TryExtractCharacterName(ctx.DetectedGameKey, ctx.AssetPath);

            if (SetupModelsHelper.TryResolveConvertedAsset(ctx, out var resolvedAsset, out var resolvedPath, allowUnmarked: true, preferredBaseName: preferredName))
            {
                if (resolvedAsset != null
                    && !string.IsNullOrEmpty(resolvedPath)
                    && !string.Equals(resolvedPath, ctx.AssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    convertedAsset = resolvedAsset;
                    convertedPath = resolvedPath;
                    resolved = true;
                }
                else if (!resolved && resolvedAsset != null)
                {
                    convertedAsset = resolvedAsset;
                    convertedPath = resolvedPath;
                    resolved = true;
                }
            }

            if (!resolved || convertedAsset == null || string.IsNullOrEmpty(convertedPath))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, "Auto Setup (HSR Unity): Converted asset not found. Using original selection.");
                return;
            }

            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Auto Setup (HSR Unity): Converted asset resolved: {convertedPath}");
            ctx.UpdateAsset(convertedAsset, convertedPath);
            SetupModelsHelper.MarkConverted(convertedPath, null, false);
            Selection.activeObject = convertedAsset;
            EditorGUIUtility.PingObject(convertedAsset);
        }

        private static void ExecuteConfigureSceneController(ModelSetupUtility.SetupContext ctx)
        {
            var manager = ctx.Manager;
            if (manager == null) return;

            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: Ensuring HSR Scene Controller in scene.");

            var controllerType = SceneControllerTypeResolver.ResolveHSRSceneControllerType();
            if (controllerType == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, "Auto Setup: HSRSceneController type not found.");
                return;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType(controllerType) as MonoBehaviour;
            if (existing != null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: HSR Scene Controller already exists.");
                return;
            }

            var go = new GameObject("HSR Scene Controller");
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Controller");
            go.AddComponent(controllerType);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: HSR Scene Controller created.");
        }

        private static void ExecuteSetupHsrCharacterController(ModelSetupUtility.SetupContext ctx)
        {
            var target = ctx.CreatedInstance ?? ctx.ExistingInstance;
            if (target == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: Skipping HSR Character Controller setup because no scene instance is available.");
                return;
            }

            var controller = target.GetComponent<HSRCharacterController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<HSRCharacterController>(target);
            }

            if (controller == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Auto Setup: Failed to add HSRCharacterController to '{target.name}'.");
                return;
            }

            EditorUtility.SetDirty(target);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Auto Setup: HSRCharacterController ready on '{target.name}'.");

            if (target.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(target.scene);
            }
        }

        private static void ExecuteApplyBoneConstraints(ModelSetupUtility.SetupContext ctx)
        {
            var target = ctx.CreatedInstance ?? ctx.ExistingInstance;
            if (target == null)
            {
                return;
            }

            var meta = ctx.DetectedGameMetadata;
            if (meta == null)
            {
                return;
            }

            int applied = SceneConstraintApplier.ApplyConstraints(target.transform, meta, false, true);
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, applied > 0
                ? $"Auto Setup: Applied {applied} bone constraint(s)."
                : "Auto Setup: No bone constraints were applied.");

            if (target.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(target.scene);
            }
        }

        private static void ExecuteSetupCameraTargets(ModelSetupUtility.SetupContext ctx)
        {
            var target = ctx.CreatedInstance ?? ctx.ExistingInstance;
            if (target == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, "Auto Setup: No scene instance found for camera target setup.");
                return;
            }

            var setup = target.GetComponent<CharacterCameraSetup>();
            if (setup == null)
            {
                setup = Undo.AddComponent<CharacterCameraSetup>(target);
            }

            if (setup == null)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Auto Setup: Failed to add CharacterCameraSetup to '{target.name}'.");
                return;
            }

            setup.FindBoneReferences();
            EditorUtility.SetDirty(target);

            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, 
                $"Auto Setup: Camera targets configured for '{target.name}' " +
                $"(Center: {(setup.CharacterCenter != null ? setup.CharacterCenter.name : "none")}, " +
                $"Head: {(setup.CharacterHead != null ? setup.CharacterHead.name : "none")}).");

            if (target.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(target.scene);
            }
        }

        #endregion
    }
}
#endif
