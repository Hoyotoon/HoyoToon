#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoyoToon.Editor.Prerequisites
{
    internal static class PipelineDetector
    {
        private static bool s_cachedInit;
        private static HoyoToonPipeline s_cachedPipeline;
        private static string s_cachedAssetName;
        private static string s_cachedReason;

        public static HoyoToonPipeline ActivePipeline
        {
            get
            {
                EnsureCached();
                return s_cachedPipeline;
            }
        }

        public static bool IsCustomRP => ActivePipeline == HoyoToonPipeline.HoyoToonURP;

        public static bool IsBuiltIn => ActivePipeline == HoyoToonPipeline.BuiltIn;

        public static HoyoToonPipeline DetectedPipeline
        {
            get
            {
                EnsureCached();
                return s_cachedPipeline;
            }
        }

        public static string ActiveAssetName
        {
            get
            {
                EnsureCached();
                return s_cachedAssetName;
            }
        }

        public static string DetectionReason
        {
            get
            {
                EnsureCached();
                return s_cachedReason ?? string.Empty;
            }
        }

        public static string FriendlyPipelineName => FriendlyName(ActivePipeline);

        public static void InvalidateCache()
        {
            s_cachedInit = false;
        }

        public static string FriendlyName(HoyoToonPipeline pipeline)
        {
            switch (pipeline)
            {
                case HoyoToonPipeline.BuiltIn:
                    return "Built-in Render Pipeline";
                case HoyoToonPipeline.HoyoToonURP:
                    return "HoyoToon URP";
                case HoyoToonPipeline.Unsupported:
                    return "Unsupported Render Pipeline";
                default:
                    return pipeline.ToString();
            }
        }

        private static void EnsureCached()
        {
            if (s_cachedInit)
            {
                return;
            }

            s_cachedInit = true;

            s_cachedPipeline = Detect(out s_cachedAssetName, out s_cachedReason);
        }

        private static HoyoToonPipeline Detect(out string assetName, out string reason)
        {
            var pipelineAsset = GetEffectivePipelineAsset(out var sourceLabel);

            if (pipelineAsset == null)
            {
                assetName = null;
                reason = "No RenderPipelineAsset assigned (quality override and graphics default are null).";
                return HoyoToonPipeline.BuiltIn;
            }

            assetName = pipelineAsset.name;
            var typeName = pipelineAsset.GetType().Name;
            var assetPath = AssetDatabase.GetAssetPath(pipelineAsset);

            if (PipelineSwitcher.IsRequiredAsset(pipelineAsset))
            {
                reason = $"Required HoyoToon URP asset is assigned via {sourceLabel}.";
                return HoyoToonPipeline.HoyoToonURP;
            }

            reason = $"A different RenderPipelineAsset is assigned via {sourceLabel}: type='{typeName}', name='{assetName}', path='{assetPath}'. Expected '{PipelineSwitcher.RequiredAssetPath}'.";
            return HoyoToonPipeline.Unsupported;
        }

        private static RenderPipelineAsset GetEffectivePipelineAsset(out string sourceLabel)
        {
            var qualityPipeline = QualitySettings.renderPipeline;
            if (qualityPipeline != null)
            {
                sourceLabel = "QualitySettings.renderPipeline";
                return qualityPipeline;
            }

            sourceLabel = "GraphicsSettings.renderPipelineAsset";
            return GraphicsSettings.defaultRenderPipeline;
        }

    }
}
#endif
