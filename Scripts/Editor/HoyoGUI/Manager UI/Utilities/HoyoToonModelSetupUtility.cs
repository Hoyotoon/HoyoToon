#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.Materials;
using HoyoToon.Prerequisites;
using HoyoToon.Utilities;
using HoyoToon.API;

namespace HoyoToon.EditorTools.ManagerUI
{
    internal static class HoyoToonModelSetupUtility
    {
        private static readonly List<Func<SetupContext, IEnumerable<SetupStep>>> s_StepProviders =
            new List<Func<SetupContext, IEnumerable<SetupStep>>>
            {
                BuildGameSpecificSteps,
                BuildVrcSpecificSteps
            };

        /// <summary>
        /// Register additional setup step providers. This enables modular, code-driven setup flows.
        /// </summary>
        public static void RegisterStepProvider(Func<SetupContext, IEnumerable<SetupStep>> provider, bool prepend = false)
        {
            if (provider == null)
            {
                return;
            }

            if (prepend)
            {
                s_StepProviders.Insert(0, provider);
            }
            else
            {
                s_StepProviders.Add(provider);
            }
        }
        internal sealed class SetupContext
        {
            public SetupContext(HoyoToonManager manager, GameObject asset, string assetPath, HoyoToonSetupOptions options)
            {
                Manager = manager;
                Asset = asset;
                AssetPath = assetPath;
                Options = options ?? new HoyoToonSetupOptions();
            }

            public HoyoToonManager Manager { get; }
            public GameObject Asset { get; private set; }
            public string AssetPath { get; private set; }
            public HoyoToonSetupOptions Options { get; }

            public bool IsFbxAsset => !string.IsNullOrEmpty(AssetPath) && AssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
            public bool IsPrefabAsset => !string.IsNullOrEmpty(AssetPath) && AssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

            public bool IsVrcSdkInstalled { get; set; }
            public VRCSDKInstalledCheck.VrcSdkKind VrcSdkKind { get; set; }

            public string DetectedGameKey { get; set; }
            public string DetectedShaderPath { get; set; }
            public string DetectedSourceJson { get; set; }
            public IReadOnlyList<(string gameKey, string shaderPath, string sourceJson)> MaterialSources { get; set; }
            public GameMetadata DetectedGameMetadata { get; set; }
            public GameObject ExistingInstance { get; set; }
            public GameObject CreatedInstance { get; set; }

            public void UpdateAsset(GameObject asset, string assetPath)
            {
                Asset = asset;
                AssetPath = assetPath;

                var (gameKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(asset, assetPath);
                DetectedGameKey = gameKey;
                DetectedShaderPath = shaderPath;
                DetectedSourceJson = sourceJson;
                MaterialSources = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(asset, assetPath);
                DetectedGameMetadata = ResolveGameMetadata(DetectedGameKey);
                ExistingInstance = HoyoToonSetupSceneHelper.FindExistingSceneInstance(Manager, asset, assetPath);

                HoyoToonSetupLoggingHelper.LogContextSummary(this);
            }
        }

        internal sealed class SetupStep
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public Func<SetupContext, bool> IsApplicable { get; set; }
            public Func<SetupContext, bool> IsRequired { get; set; }
            public Action<SetupContext> Execute { get; set; }
            public bool Enabled { get; set; }
        }

        internal readonly struct StepDecision
        {
            public StepDecision(SetupStep step, bool applicable, bool required)
            {
                Step = step;
                Applicable = applicable;
                Required = required;
            }

            public SetupStep Step { get; }
            public bool Applicable { get; }
            public bool Required { get; }
        }

        public static bool TryProcessFbxAndInstantiate(HoyoToonManager manager, GameObject fbxAsset, out GameObject instance)
        {
            return TryProcessFbxAndInstantiate(manager, fbxAsset, null, out instance);
        }

        public static bool TryProcessFbxAndInstantiate(HoyoToonManager manager, GameObject fbxAsset, out GameObject instance, out GameObject resolvedAsset)
        {
            return TryProcessFbxAndInstantiate(manager, fbxAsset, null, out instance, out resolvedAsset);
        }

        public static bool TryProcessFbxAndInstantiate(HoyoToonManager manager, GameObject fbxAsset, HoyoToonSetupOptions options, out GameObject instance)
        {
            return TryProcessFbxAndInstantiate(manager, fbxAsset, options, out instance, out _);
        }

        public static bool TryProcessFbxAndInstantiate(HoyoToonManager manager, GameObject fbxAsset, HoyoToonSetupOptions options, out GameObject instance, out GameObject resolvedAsset)
        {
            instance = null;
            resolvedAsset = null;
            if (manager == null)
            {
                HoyoToonDialogWindow.ShowError("Manager Missing", "Cannot add a model without an active HoyoToon Manager in the scene.");
                return false;
            }

            if (fbxAsset == null)
            {
                HoyoToonDialogWindow.ShowError("Invalid Selection", "Please supply an FBX asset when adding a model.");
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(fbxAsset);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonDialogWindow.ShowError("Unsupported Asset", "Only FBX assets can be added through this panel.");
                return false;
            }

            var context = BuildSetupContext(manager, fbxAsset, assetPath, options);
            if (!TryRunSetupPlan(context))
            {
                return false;
            }

            instance = context.CreatedInstance;
            resolvedAsset = ResolveAssetFromContext(context) ?? fbxAsset;
            return true;
        }

        public static bool TryInstantiatePrefabAsset(HoyoToonManager manager, GameObject prefabAsset, out GameObject instance)
        {
            return TryInstantiatePrefabAsset(manager, prefabAsset, null, out instance);
        }

        public static bool TryInstantiatePrefabAsset(HoyoToonManager manager, GameObject prefabAsset, out GameObject instance, out GameObject resolvedAsset)
        {
            return TryInstantiatePrefabAsset(manager, prefabAsset, null, out instance, out resolvedAsset);
        }

        public static bool TryInstantiatePrefabAsset(HoyoToonManager manager, GameObject prefabAsset, HoyoToonSetupOptions options, out GameObject instance)
        {
            return TryInstantiatePrefabAsset(manager, prefabAsset, options, out instance, out _);
        }

        public static bool TryInstantiatePrefabAsset(HoyoToonManager manager, GameObject prefabAsset, HoyoToonSetupOptions options, out GameObject instance, out GameObject resolvedAsset)
        {
            instance = null;
            resolvedAsset = null;

            if (manager == null)
            {
                HoyoToonDialogWindow.ShowError("Manager Missing", "Cannot add a model without an active HoyoToon Manager in the scene.");
                return false;
            }

            if (prefabAsset == null)
            {
                HoyoToonDialogWindow.ShowError("Invalid Selection", "Please supply a prefab asset when using this option.");
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonDialogWindow.ShowError("Unsupported Asset", "Only prefab assets can be added with this option.");
                return false;
            }

            var context = BuildSetupContext(manager, prefabAsset, assetPath, options);
            if (!TryRunSetupPlan(context))
            {
                return false;
            }

            instance = context.CreatedInstance;
            resolvedAsset = ResolveAssetFromContext(context) ?? prefabAsset;
            return true;
        }

        private static GameObject ResolveAssetFromContext(SetupContext context)
        {
            if (context == null)
            {
                return null;
            }

            if (context.Asset != null)
            {
                return context.Asset;
            }

            if (!string.IsNullOrEmpty(context.AssetPath))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(context.AssetPath);
            }

            return null;
        }

        private static SetupContext BuildSetupContext(HoyoToonManager manager, GameObject asset, string assetPath, HoyoToonSetupOptions options)
        {
            var context = new SetupContext(manager, asset, assetPath, options?.Clone());
            var (gameKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(asset, assetPath);
            context.DetectedGameKey = gameKey;
            context.DetectedShaderPath = shaderPath;
            context.DetectedSourceJson = sourceJson;
            context.MaterialSources = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(asset, assetPath);
            context.DetectedGameMetadata = ResolveGameMetadata(context.DetectedGameKey);
            context.ExistingInstance = HoyoToonSetupSceneHelper.FindExistingSceneInstance(manager, asset, assetPath);
            context.IsVrcSdkInstalled = VRCSDKInstalledCheck.IsVRCSDKInstalled;
            context.VrcSdkKind = VRCSDKInstalledCheck.InstalledKind;

            HoyoToonSetupLoggingHelper.LogContextSummary(context);
            return context;
        }

        private static GameMetadata ResolveGameMetadata(string gameKey)
        {
            if (string.IsNullOrEmpty(gameKey))
            {
                return null;
            }

            var metaMap = HoyoToonApi.GetGameMetadata();
            if (metaMap == null)
            {
                return null;
            }

            return metaMap.TryGetValue(gameKey, out var meta) ? meta : null;
        }

        private static bool TryRunSetupPlan(SetupContext context)
        {
            const int maxPasses = 2;

            for (int pass = 0; pass < maxPasses; pass++)
            {
                var steps = BuildSetupSteps(context);
                var decisions = new List<StepDecision>();
                foreach (var step in steps)
                {
                    if (step == null)
                    {
                        continue;
                    }

                    bool applicable = step.IsApplicable == null || step.IsApplicable(context);
                    bool required = step.IsRequired == null || step.IsRequired(context);
                    decisions.Add(new StepDecision(step, applicable, required));
                }

                HoyoToonSetupLoggingHelper.LogStepDecisions(context, decisions);

                var runnableSteps = decisions
                    .Where(decision => decision.Step != null)
                    .Where(decision => decision.Step.Enabled)
                    .Where(decision => decision.Applicable)
                    .Where(decision => decision.Required)
                    .Select(decision => decision.Step)
                    .ToList();

                if (runnableSteps.Count == 0)
                {
                    HoyoToonLogger.ManagerInfo("Auto Setup: No setup steps were required for the selected asset.");
                    return true;
                }

                bool restartRequested = false;

                try
                {
                    for (int i = 0; i < runnableSteps.Count; i++)
                    {
                        var step = runnableSteps[i];
                        var progress = (i + 0.15f) / runnableSteps.Count;
                        EditorUtility.DisplayProgressBar("HoyoToon", step.Title, Mathf.Clamp01(progress));

                        var previousAsset = context.Asset;
                        var previousPath = context.AssetPath;

                        step.Execute?.Invoke(context);

                        if (!ReferenceEquals(previousAsset, context.Asset)
                            || !string.Equals(previousPath, context.AssetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            RefreshContextAsset(context);
                            HoyoToonLogger.ManagerInfo("Auto Setup: Asset updated during setup; restarting steps with converted asset.");
                            restartRequested = true;
                            break;
                        }
                    }

                    if (!restartRequested)
                    {
                        EditorUtility.DisplayProgressBar("HoyoToon", "Finishing", 1f);
                        RefreshContextAsset(context);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    HoyoToonLogger.ManagerError($"Auto setup failed for '{context?.Asset?.name}': {ex.Message}");
                    HoyoToonDialogWindow.ShowError("Auto Setup Failed", $"An exception interrupted the setup process. See console for details.\n\n{ex.Message}");
                    return false;
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            return true;
        }

        private static List<SetupStep> BuildSetupSteps(SetupContext context)
        {
            var steps = new List<SetupStep>();
            foreach (var provider in s_StepProviders)
            {
                if (provider == null)
                {
                    continue;
                }

                var provided = provider(context);
                if (provided == null)
                {
                    continue;
                }

                foreach (var step in provided)
                {
                    if (step != null)
                    {
                        steps.Add(step);
                    }
                }
            }

            return steps;
        }

        private static void RefreshContextAsset(SetupContext context)
        {
            if (context == null || !context.IsFbxAsset)
            {
                return;
            }

            if (string.IsNullOrEmpty(context.AssetPath))
            {
                return;
            }

            AssetDatabase.ImportAsset(context.AssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            var refreshedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(context.AssetPath);
            if (refreshedAsset != null)
            {
                context.UpdateAsset(refreshedAsset, context.AssetPath);
            }
        }


        private static IEnumerable<SetupStep> BuildGameSpecificSteps(SetupContext context)
        {
            if (context == null || string.IsNullOrEmpty(context.DetectedGameKey))
            {
                HoyoToonLogger.ManagerInfo("Auto Setup: No game-specific steps registered (game not detected).");
                return Array.Empty<SetupStep>();
            }

            // Extend here with per-game steps or register custom providers via RegisterStepProvider.
            HoyoToonLogger.ManagerInfo($"Auto Setup: Game-specific step provider evaluated for '{context.DetectedGameKey}'.");
            return Array.Empty<SetupStep>();
        }

        private static IEnumerable<SetupStep> BuildVrcSpecificSteps(SetupContext context)
        {
            if (context == null)
            {
                return Array.Empty<SetupStep>();
            }

            if (!context.IsVrcSdkInstalled)
            {
                HoyoToonLogger.ManagerInfo("Auto Setup: VRChat SDK not detected; skipping VRChat-specific steps.");
                return Array.Empty<SetupStep>();
            }

            // Extend here with VRChat-specific steps or register custom providers via RegisterStepProvider.
            HoyoToonLogger.ManagerInfo($"Auto Setup: VRChat SDK detected ({context.VrcSdkKind}); VRChat-specific provider active.");
            return Array.Empty<SetupStep>();
        }

    }
}
#endif
