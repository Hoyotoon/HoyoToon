#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon;
using HoyoToon.Editor.AssetPipeline.Materials;
using HoyoToon.Editor.Prerequisites;
using HoyoToon.Editor.Utilities;
using HoyoToon.Editor.API;
using HoyoToon.Editor.UI.ManagerInspector.Setup;
using HoyoToon.Editor.Onboarding;

using HoyoToon.Editor.UI.Windows;

namespace HoyoToon.Editor.UI.ManagerInspector
{
    internal static class ModelSetupUtility
    {
        private static readonly List<Func<SetupContext, IEnumerable<SetupStep>>> s_StepProviders =
            new List<Func<SetupContext, IEnumerable<SetupStep>>>();

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
            public SetupContext(HoyoToonManager manager, GameObject asset, string assetPath, SetupOptions options)
            {
                Manager = manager;
                Asset = asset;
                AssetPath = assetPath;
                Options = options ?? new SetupOptions();
            }

            public HoyoToonManager Manager { get; }
            public GameObject Asset { get; private set; }
            public string AssetPath { get; private set; }
            public SetupOptions Options { get; }

            public bool IsFbxAsset => !string.IsNullOrEmpty(AssetPath) && AssetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
            public bool IsPrefabAsset => !string.IsNullOrEmpty(AssetPath) && AssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

            public bool IsVrcSdkInstalled { get; set; }
            public VRCSDKInstalledCheck.VrcSdkKind VrcSdkKind { get; set; }

            public HoyoToonPipeline ActivePipeline { get; set; }
            public bool IsCustomPipeline { get; set; }

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
                ExistingInstance = SetupSceneHelper.FindExistingSceneInstance(Manager, asset, assetPath);

                SetupLoggingHelper.LogContextSummary(this);
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

        internal sealed class SetupPlanPreview
        {
            public SetupPlanPreview(SetupContext context, List<StepDecision> decisions)
            {
                Context = context;
                Decisions = decisions ?? new List<StepDecision>();
            }

            public SetupContext Context { get; }
            public List<StepDecision> Decisions { get; }
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

        internal enum SetupRequestSource
        {
            AddModel,
            BatchSetup,
            DownloadAutoSetup,
            Preview,
            PrefabAdd,
        }

        internal enum SetupAssetKind
        {
            Fbx,
            Prefab,
        }

        internal sealed class PreparedSetupRequest
        {
            public PreparedSetupRequest(SetupRequest request, SetupContext context)
            {
                Request = request;
                Context = context;
            }

            public SetupRequest Request { get; }
            public SetupContext Context { get; }
        }

        internal sealed class SetupRequest
        {
            public SetupRequest(HoyoToonManager manager, GameObject asset, SetupOptions options, SetupAssetKind assetKind, SetupRequestSource source)
            {
                Manager = manager;
                Asset = asset;
                Options = options;
                AssetKind = assetKind;
                Source = source;
            }

            public HoyoToonManager Manager { get; }
            public GameObject Asset { get; }
            public SetupOptions Options { get; }
            public SetupAssetKind AssetKind { get; }
            public SetupRequestSource Source { get; }
        }

        private readonly struct SetupValidationFailure
        {
            public SetupValidationFailure(string title, string message)
            {
                Title = title;
                Message = message;
            }

            public string Title { get; }
            public string Message { get; }
        }

        private sealed class SetupExecutionPlan
        {
            public SetupExecutionPlan(SetupContext context, List<StepDecision> decisions, List<SetupStep> runnableSteps)
            {
                Context = context;
                Decisions = decisions ?? new List<StepDecision>();
                RunnableSteps = runnableSteps ?? new List<SetupStep>();
            }

            public SetupContext Context { get; }
            public List<StepDecision> Decisions { get; }
            public List<SetupStep> RunnableSteps { get; }
        }

        public static bool TryProcessFbxAndInstantiate(HoyoToonManager manager, GameObject fbxAsset, out GameObject instance, SetupOptions options = null)
        {
            return TryProcessFbxAndInstantiate(manager, fbxAsset, options, out instance, out _);
        }

        public static bool TryProcessFbxAndInstantiate(HoyoToonManager manager, GameObject fbxAsset, SetupOptions options, out GameObject instance, out GameObject resolvedAsset)
        {
            instance = null;
            resolvedAsset = null;

            if (!TryPrepareFbxRequest(manager, fbxAsset, options, SetupRequestSource.AddModel, out var preparedRequest))
            {
                return false;
            }

            if (!TryRunPreparedRequest(preparedRequest, out instance, out resolvedAsset))
            {
                return false;
            }

            if (instance != null)
            {
                GuidedTourController.NotifyAutoSetupCompleted(instance);
            }

            return true;
        }

        internal static bool TryBuildSetupPlanPreview(HoyoToonManager manager, GameObject fbxAsset, SetupOptions options, out SetupPlanPreview preview, out string error)
        {
            preview = null;
            error = null;

            if (!TryPrepareRequest(new SetupRequest(manager, fbxAsset, options, SetupAssetKind.Fbx, SetupRequestSource.Preview), false, out var preparedRequest, out var validationFailure))
            {
                error = validationFailure.Message;
                return false;
            }

            var plan = BuildExecutionPlan(preparedRequest.Context);

            preview = new SetupPlanPreview(preparedRequest.Context, plan.Decisions);
            return true;
        }

        public static bool TryInstantiatePrefabAsset(HoyoToonManager manager, GameObject prefabAsset, out GameObject instance, SetupOptions options = null)
        {
            return TryInstantiatePrefabAsset(manager, prefabAsset, options, out instance, out _);
        }

        public static bool TryInstantiatePrefabAsset(HoyoToonManager manager, GameObject prefabAsset, SetupOptions options, out GameObject instance, out GameObject resolvedAsset)
        {
            instance = null;
            resolvedAsset = null;

            if (!TryPreparePrefabRequest(manager, prefabAsset, options, SetupRequestSource.PrefabAdd, out var preparedRequest))
            {
                return false;
            }

            if (!TryRunPreparedRequest(preparedRequest, out instance, out resolvedAsset))
            {
                return false;
            }

            return true;
        }

        internal static bool TryPrepareFbxRequest(HoyoToonManager manager, GameObject fbxAsset, SetupOptions options, SetupRequestSource source, out PreparedSetupRequest preparedRequest, bool showDialogs = true)
        {
            return TryPrepareRequest(new SetupRequest(manager, fbxAsset, options, SetupAssetKind.Fbx, source), showDialogs, out preparedRequest, out _);
        }

        internal static bool TryRunPreparedRequest(PreparedSetupRequest preparedRequest, out GameObject instance, out GameObject resolvedAsset)
        {
            instance = null;
            resolvedAsset = null;
            if (preparedRequest == null)
            {
                return false;
            }

            if (!TryRunSetupPlan(preparedRequest.Context))
            {
                return false;
            }

            instance = preparedRequest.Context.CreatedInstance ?? preparedRequest.Context.ExistingInstance;
            resolvedAsset = ResolveAssetFromContext(preparedRequest.Context) ?? preparedRequest.Request.Asset;
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

        private static SetupContext BuildSetupContext(HoyoToonManager manager, GameObject asset, string assetPath, SetupOptions options)
        {
            var context = new SetupContext(manager, asset, assetPath, options?.Clone());
            var (gameKey, shaderPath, sourceJson) = MaterialDetection.DetectGameAndShaderAutoWithSource(asset, assetPath);
            context.DetectedGameKey = gameKey;
            context.DetectedShaderPath = shaderPath;
            context.DetectedSourceJson = sourceJson;
            context.MaterialSources = MaterialDetection.DetectGameAndShaderAutoWithSourceMany(asset, assetPath);
            context.DetectedGameMetadata = ResolveGameMetadata(context.DetectedGameKey);
            context.ExistingInstance = SetupSceneHelper.FindExistingSceneInstance(manager, asset, assetPath);
            context.IsVrcSdkInstalled = VRCSDKInstalledCheck.IsVRCSDKInstalled;
            context.VrcSdkKind = VRCSDKInstalledCheck.InstalledKind;
            context.ActivePipeline = RenderPipelineCheck.ActivePipeline;
            context.IsCustomPipeline = RenderPipelineCheck.IsCustomRP;

            SetupLoggingHelper.LogContextSummary(context);
            return context;
        }

        private static GameMetadata ResolveGameMetadata(string gameKey)
        {
            if (string.IsNullOrEmpty(gameKey))
            {
                return null;
            }

            var metaMap = Api.GetGameMetadata();
            if (metaMap == null)
            {
                return null;
            }

            return metaMap.TryGetValue(gameKey, out var meta) ? meta : null;
        }

        private static bool TryPreparePrefabRequest(HoyoToonManager manager, GameObject prefabAsset, SetupOptions options, SetupRequestSource source, out PreparedSetupRequest preparedRequest)
        {
            return TryPrepareRequest(new SetupRequest(manager, prefabAsset, options, SetupAssetKind.Prefab, source), true, out preparedRequest, out _);
        }

        private static bool TryPrepareRequest(SetupRequest request, bool showDialogs, out PreparedSetupRequest preparedRequest, out SetupValidationFailure validationFailure)
        {
            preparedRequest = null;
            validationFailure = default;

            if (request == null)
            {
                validationFailure = new SetupValidationFailure("Invalid Selection", "No asset was supplied for setup.");
                return false;
            }

            if (!TryValidateRequest(request, out var assetPath, out validationFailure))
            {
                if (showDialogs)
                {
                    DialogWindow.ShowError(validationFailure.Title, validationFailure.Message);
                }

                return false;
            }

            preparedRequest = new PreparedSetupRequest(request, BuildSetupContext(request.Manager, request.Asset, assetPath, request.Options));
            return true;
        }

        private static bool TryValidateRequest(SetupRequest request, out string assetPath, out SetupValidationFailure validationFailure)
        {
            assetPath = null;
            validationFailure = default;

            if (request.Manager == null)
            {
                validationFailure = BuildMissingManagerFailure(request.Source);
                return false;
            }

            if (request.Asset == null)
            {
                validationFailure = BuildMissingAssetFailure(request.Source, request.AssetKind);
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(request.Asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                validationFailure = BuildUnsupportedAssetFailure(request.Source, request.AssetKind, assetPathMissing: true);
                return false;
            }

            if (!IsSupportedAssetPath(assetPath, request.AssetKind))
            {
                validationFailure = BuildUnsupportedAssetFailure(request.Source, request.AssetKind, assetPathMissing: false);
                return false;
            }

            return true;
        }

        private static SetupValidationFailure BuildMissingManagerFailure(SetupRequestSource source)
        {
            switch (source)
            {
                case SetupRequestSource.BatchSetup:
                case SetupRequestSource.DownloadAutoSetup:
                    return new SetupValidationFailure("Manager Missing", "Cannot setup without an active HoyoToon Manager in the scene.");
                case SetupRequestSource.Preview:
                    return new SetupValidationFailure("Preview Unavailable", "No active HoyoToon Manager found in the scene.");
                default:
                    return new SetupValidationFailure("Manager Missing", "Cannot add a model without an active HoyoToon Manager in the scene.");
            }
        }

        private static SetupValidationFailure BuildMissingAssetFailure(SetupRequestSource source, SetupAssetKind assetKind)
        {
            if (source == SetupRequestSource.Preview)
            {
                return new SetupValidationFailure("Preview Unavailable", "Please supply an FBX asset for preview.");
            }

            if (source == SetupRequestSource.DownloadAutoSetup)
            {
                return new SetupValidationFailure("Missing Asset", "Could not load the downloaded model asset.");
            }

            if (assetKind == SetupAssetKind.Prefab)
            {
                return new SetupValidationFailure("Invalid Selection", "Please supply a prefab asset when using this option.");
            }

            if (source == SetupRequestSource.AddModel)
            {
                return new SetupValidationFailure("Invalid Selection", "Please supply an FBX asset when adding a model.");
            }

            return new SetupValidationFailure("Unsupported Asset", "Please select an FBX asset.");
        }

        private static SetupValidationFailure BuildUnsupportedAssetFailure(SetupRequestSource source, SetupAssetKind assetKind, bool assetPathMissing)
        {
            if (source == SetupRequestSource.Preview)
            {
                return new SetupValidationFailure("Preview Unavailable", "Only FBX assets can be previewed by this setup flow.");
            }

            if (source == SetupRequestSource.DownloadAutoSetup)
            {
                return new SetupValidationFailure("Unsupported Asset", "Only FBX assets are supported right now.");
            }

            if (assetKind == SetupAssetKind.Prefab)
            {
                return new SetupValidationFailure("Unsupported Asset", "Only prefab assets can be added with this option.");
            }

            if (source == SetupRequestSource.AddModel)
            {
                if (assetPathMissing)
                {
                    return new SetupValidationFailure("Unsupported Asset", "Please select an FBX asset.");
                }

                return new SetupValidationFailure("Unsupported Asset", "Only FBX assets are supported right now.");
            }

            if (assetPathMissing)
            {
                return new SetupValidationFailure("Unsupported Asset", "Please select an FBX asset.");
            }

            return new SetupValidationFailure("Unsupported Asset", "Only FBX assets are supported right now.");
        }

        private static bool IsSupportedAssetPath(string assetPath, SetupAssetKind assetKind)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            switch (assetKind)
            {
                case SetupAssetKind.Prefab:
                    return assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
                default:
                    return assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool TryRunSetupPlan(SetupContext context)
        {
            const int maxPasses = 2;

            for (int pass = 0; pass < maxPasses; pass++)
            {
                var plan = BuildExecutionPlan(context);
                SetupLoggingHelper.LogStepDecisions(context, plan.Decisions);

                if (plan.RunnableSteps.Count == 0)
                {
                    HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: No setup steps were required for the selected asset.");
                    return true;
                }

                if (!TryExecutePlanPass(plan, out var restartRequested))
                {
                    return false;
                }

                if (!restartRequested)
                {
                    return true;
                }
            }

            return true;
        }

        private static SetupExecutionPlan BuildExecutionPlan(SetupContext context)
        {
            var steps = BuildSetupSteps(context);
            var decisions = BuildStepDecisions(context, steps);
            var runnableSteps = decisions
                .Where(decision => decision.Step != null)
                .Where(decision => decision.Step.Enabled)
                .Where(decision => decision.Applicable)
                .Where(decision => decision.Required)
                .Select(decision => decision.Step)
                .ToList();

            return new SetupExecutionPlan(context, decisions, runnableSteps);
        }

        private static bool TryExecutePlanPass(SetupExecutionPlan plan, out bool restartRequested)
        {
            restartRequested = false;

            try
            {
                for (int i = 0; i < plan.RunnableSteps.Count; i++)
                {
                    var step = plan.RunnableSteps[i];
                    var progress = (i + 0.15f) / plan.RunnableSteps.Count;
                    EditorUtility.DisplayProgressBar("HoyoToon", step.Title, Mathf.Clamp01(progress));

                    var previousAsset = plan.Context.Asset;
                    var previousPath = plan.Context.AssetPath;

                    step.Execute?.Invoke(plan.Context);

                    if (!ReferenceEquals(previousAsset, plan.Context.Asset)
                        || !string.Equals(previousPath, plan.Context.AssetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        RefreshContextAsset(plan.Context);
                        HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: Asset updated during setup; restarting steps with converted asset.");
                        restartRequested = true;
                        return true;
                    }
                }

                EditorUtility.DisplayProgressBar("HoyoToon", "Finishing", 1f);
                RefreshContextAsset(plan.Context);
                return true;
            }
            catch (Exception ex)
            {
                return HandleSetupExecutionFailure(plan.Context, ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool HandleSetupExecutionFailure(SetupContext context, Exception ex)
        {
            HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Error, $"Auto setup failed for '{context?.Asset?.name}': {ex.Message}");
            try
            {
                DialogWindow.ShowError("Auto Setup Failed", $"An exception interrupted the setup process. See console for details.\n\n{ex.Message}");
            }
            catch (Exception dialogEx)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Auto Setup: Failed to show error dialog: {dialogEx.Message}");
            }

            return false;
        }

        private static List<StepDecision> BuildStepDecisions(SetupContext context, IReadOnlyList<SetupStep> steps)
        {
            var decisions = new List<StepDecision>();
            if (steps == null)
            {
                return decisions;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null)
                {
                    continue;
                }

                bool applicable = step.IsApplicable == null || step.IsApplicable(context);
                bool required = step.IsRequired == null || step.IsRequired(context);
                decisions.Add(new StepDecision(step, applicable, required));
            }

            return decisions;
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

    }
}
#endif
