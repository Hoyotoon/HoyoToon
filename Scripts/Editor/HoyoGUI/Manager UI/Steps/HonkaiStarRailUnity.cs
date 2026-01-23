#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HoyoToon.EditorTools.ManagerUI;
using HoyoToon.EditorTools.ManagerScene;
using HoyoToon.FBX;
using HoyoToon.Utilities;
using HoyoToon.Materials;
using HoyoToon.Models;


namespace HoyoToon.EditorTools.ManagerUI.Steps
{
    [InitializeOnLoad]
    internal static class HonkaiStarRailUnity
    {
        private const string HonkaiStarRailKey = "Honkai Star Rail";
        private const string HairTagName = "Hair";
        private static readonly string[] HairNameTokens = { "Hair", "Bangs" };

        static HonkaiStarRailUnity()
        {
            HoyoToonModelSetupUtility.RegisterStepProvider(BuildSteps);
        }

        private static IEnumerable<HoyoToonModelSetupUtility.SetupStep> BuildSteps(HoyoToonModelSetupUtility.SetupContext context)
        {
            if (context == null)
            {
                return Array.Empty<HoyoToonModelSetupUtility.SetupStep>();
            }

            if (context.IsVrcSdkInstalled)
            {
                HoyoToonLogger.ManagerInfo("Auto Setup (HSR Unity): VRChat SDK detected; skipping Unity steps.");
                return Array.Empty<HoyoToonModelSetupUtility.SetupStep>();
            }

            if (!string.Equals(context.DetectedGameKey, HonkaiStarRailKey, StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonLogger.ManagerInfo("Auto Setup (HSR Unity): Game mismatch; skipping steps.");
                return Array.Empty<HoyoToonModelSetupUtility.SetupStep>();
            }

            HoyoToonLogger.ManagerInfo("Auto Setup (HSR Unity): Step provider active.");

            return new[]
            {
                new HoyoToonModelSetupUtility.SetupStep
                {
                    Id = "hsr-unity-convert",
                    Title = "Convert FBX with Hoyo2VRC",
                    Enabled = HoyoToonSetupModelsHelper.ShouldConvertModel(context),
                    IsApplicable = ctx => ctx.IsFbxAsset,
                    IsRequired = ctx => HoyoToonSetupModelsHelper.NeedsConversion(ctx),
                    Execute = ctx =>
                    {
                        HoyoToonLogger.ManagerInfo("Auto Setup (HSR Unity): FBX not converted. Running converter.");
                        GameObject convertedAsset = null;
                        string convertedPath = null;

                        bool resolved = ModelConverter.TryProcessAndGetOutput(ctx.Asset, out convertedPath, out convertedAsset);

                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                        var preferredName = MaterialDetection.TryExtractCharacterName(ctx.DetectedGameKey, ctx.AssetPath);

                        if (HoyoToonSetupModelsHelper.TryResolveConvertedAsset(ctx, out var resolvedAsset, out var resolvedPath, allowUnmarked: true, preferredBaseName: preferredName))
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
                            HoyoToonLogger.ManagerWarning("Auto Setup (HSR Unity): Converted asset not found. Using original selection.");
                            return;
                        }

                        HoyoToonLogger.ManagerInfo($"Auto Setup (HSR Unity): Converted asset resolved: {convertedPath}");
                        ctx.UpdateAsset(convertedAsset, convertedPath);
                        HoyoToonSetupModelsHelper.MarkConverted(convertedPath, null, false);
                        Selection.activeObject = convertedAsset;
                        EditorGUIUtility.PingObject(convertedAsset);
                    }
                },
                new HoyoToonModelSetupUtility.SetupStep
                {
                    Id = "generate-materials",
                    Title = "Generating materials",
                    Enabled = HoyoToonSetupMaterialsHelper.ShouldGenerateMaterials(context),
                    IsApplicable = ctx => ctx.MaterialSources != null && ctx.MaterialSources.Count > 0,
                    IsRequired = ctx => HoyoToonSetupMaterialsHelper.HasMissingMaterials(ctx),
                    Execute = ctx =>
                    {
                        HoyoToonLogger.ManagerInfo($"Auto Setup: Generating materials (Game: '{ctx.DetectedGameKey ?? "Unknown"}').");
                        MaterialGeneration.GenerateAuto(ctx.Asset, null, null, null, true);
                    }
                },

                new HoyoToonModelSetupUtility.SetupStep
                {
                    Id = "apply-import-settings",
                    Title = "Applying FBX import settings",
                    Enabled = HoyoToonSetupModelsHelper.ShouldApplyImportSettings(context),
                    IsApplicable = ctx => ctx.IsFbxAsset,
                    IsRequired = ctx => HoyoToonSetupModelsHelper.NeedsModelImportSettings(ctx)
                        || HoyoToonSetupModelsHelper.NeedsConversion(ctx),
                    Execute = ctx =>
                    {
                        HoyoToonLogger.ManagerInfo("Auto Setup: Applying model import rules.");
                        ModelImportRulesApplier.TryApplyFromConfigForAsset(ctx.AssetPath, ctx.Asset, true);
                    }
                },

                new HoyoToonModelSetupUtility.SetupStep
                {
                    Id = "scene-light-controller",
                    Title = "Configuring scene light controller",
                    Enabled = true,
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx =>
                    {
                        var manager = ctx.Manager;
                        var controller = UnityEngine.Object.FindObjectsOfType<HoyoToonSceneLightController>(true)
                            .FirstOrDefault(item => item != null && item.Manager == manager);
                        return controller == null || !controller.IsValidForManager(manager, ctx.DetectedGameKey);
                    },
                    Execute = ctx =>
                    {
                        var manager = ctx.Manager;
                        if (manager == null)
                        {
                            return;
                        }

                        HoyoToonLogger.ManagerInfo("Auto Setup: Ensuring Scene Light Controller under HoyoToon Manager/Scripts.");

                        var scriptsParent = manager.transform.Find("Scripts");
                        if (scriptsParent == null)
                        {
                            var scriptsGo = new GameObject("Scripts");
                            scriptsGo.transform.SetParent(manager.transform, false);
                            scriptsParent = scriptsGo.transform;
                        }

                        var controller = HoyoToonSceneLightController.EnsureForManager(manager, scriptsParent, ctx.DetectedGameKey);
                        if (controller == null)
                        {
                            HoyoToonLogger.ManagerWarning("Auto Setup: Scene Light Controller could not be created.");
                        }
                        else
                        {
                            HoyoToonLogger.ManagerInfo($"Auto Setup: Scene Light Controller ready (Game='{controller.GameKey ?? "<none>"}').");
                        }
                    }
                },

                new HoyoToonModelSetupUtility.SetupStep
                {
                    Id = "hair-shadow-mask",
                    Title = "Configuring hair shadow mask",
                    Enabled = true,
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx =>
                    {
                        var manager = ctx.Manager;
                        if (manager == null)
                        {
                            return false;
                        }

                        var existing = UnityEngine.Object.FindObjectsOfType<HoyoToonHairShadowMaskRenderer>(true)
                            .FirstOrDefault(item => item != null && item.transform.IsChildOf(manager.transform));
                        return existing == null;
                    },
                    Execute = ctx =>
                    {
                        var manager = ctx.Manager;
                        if (manager == null)
                        {
                            return;
                        }

                        HoyoToonLogger.ManagerInfo("Auto Setup: Ensuring Hair Shadow Mask renderer under HoyoToon Manager/Scripts.");

                        var scriptsParent = manager.transform.Find("Scripts");
                        if (scriptsParent == null)
                        {
                            var scriptsGo = new GameObject("Scripts");
                            scriptsGo.transform.SetParent(manager.transform, false);
                            scriptsParent = scriptsGo.transform;
                        }

                        var renderer = HoyoToonHairShadowMaskRenderer.EnsureForManager(manager, scriptsParent);
                        if (renderer == null)
                        {
                            HoyoToonLogger.ManagerWarning("Auto Setup: Hair Shadow Mask renderer could not be created.");
                        }
                    }
                },       

                new HoyoToonModelSetupUtility.SetupStep
                {
                    Id = "instantiate-model",
                    Title = "Instantiating model",
                    Enabled = HoyoToonSetupSceneHelper.ShouldInstantiateModel(context),
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx => ctx.Options == null || (ctx.Options.InstantiateInScene && ctx.ExistingInstance == null),
                    Execute = ctx =>
                    {
                        ctx.CreatedInstance = HoyoToonSetupSceneHelper.InstantiateAssetInScene(
                            ctx.Manager,
                            ctx.Asset,
                            ctx.IsFbxAsset ? "Add HoyoToon Model" : "Add HoyoToon Prefab");
                        if (ctx.CreatedInstance == null)
                        {
                            throw new InvalidOperationException("Failed to instantiate the selected asset.");
                        }
                    }
                },

                new HoyoToonModelSetupUtility.SetupStep
                {
                    Id = "tag-hair-meshes",
                    Title = "Tagging hair meshes",
                    Enabled = true,
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx => ctx.Options == null || ctx.Options.InstantiateInScene,
                    Execute = ctx =>
                    {
                        var target = ctx.CreatedInstance ?? ctx.ExistingInstance;
                        if (target == null)
                        {
                            HoyoToonLogger.ManagerWarning("Auto Setup: No instantiated model found to tag hair meshes.");
                            return;
                        }

                        HoyoToonTagUtility.EnsureTagExists(HairTagName);

                        int taggedCount = 0;
                        var renderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        if (renderers == null || renderers.Length == 0)
                        {
                            HoyoToonLogger.ManagerWarning($"Auto Setup: No SkinnedMeshRenderer found under '{target.name}' for hair tagging.");
                            return;
                        }

                        foreach (var renderer in renderers)
                        {
                            if (renderer == null || renderer.gameObject == null)
                            {
                                continue;
                            }

                            var name = renderer.gameObject.name ?? string.Empty;
                            if (!HoyoToonTagUtility.NameContainsAny(name, HairNameTokens))
                            {
                                continue;
                            }

                            if (renderer.gameObject.CompareTag(HairTagName))
                            {
                                continue;
                            }

                            Undo.RecordObject(renderer.gameObject, "Tag Hair Mesh");
                            renderer.gameObject.tag = HairTagName;
                            EditorUtility.SetDirty(renderer.gameObject);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
                            taggedCount++;
                        }

                        HoyoToonLogger.ManagerInfo($"Auto Setup: Tagged {taggedCount} hair mesh object(s) in '{target.name}'.");

                        if (target.scene.IsValid())
                        {
                            EditorSceneManager.MarkSceneDirty(target.scene);
                        }
                    }
                },

                new HoyoToonModelSetupUtility.SetupStep
                {
                    Id = "apply-head-values",
                    Title = "Applying head values",
                    Enabled = true,
                    IsApplicable = ctx => ctx.Manager != null,
                    IsRequired = ctx => ctx.Options == null || ctx.Options.InstantiateInScene,
                    Execute = ctx =>
                    {
                        var target = ctx.CreatedInstance ?? ctx.ExistingInstance;
                        if (target == null)
                        {
                            return;
                        }

                        var applier = target.GetComponent<HoyoToonHeadValuesApplier>();
                        if (applier == null)
                        {
                            applier = target.AddComponent<HoyoToonHeadValuesApplier>();
                            HoyoToonLogger.ManagerInfo("Added applier");
                        }

                        applier.RunOnce();

                        if (target.scene.IsValid())
                        {
                            EditorSceneManager.MarkSceneDirty(target.scene);
                        }
                    }
                },

                new HoyoToonModelSetupUtility.SetupStep
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
                        HoyoToonLogger.ManagerInfo(applied
                            ? "Auto Setup: Tangent rules applied from config."
                            : "Auto Setup: Tangent rules did not apply any changes.");
                    }
                },
            };
        }

    }
}
#endif
