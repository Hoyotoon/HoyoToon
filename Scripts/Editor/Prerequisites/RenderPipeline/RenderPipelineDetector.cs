#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HoyoToon.Editor.Prerequisites.RenderPipeline
{
    internal static class RenderPipelineDetector
    {
        public static RenderPipelineDetectionResult Detect()
        {
            string source = "QualitySettings.renderPipeline";
            RenderPipelineAsset pipelineAsset = QualitySettings.renderPipeline;
            if (pipelineAsset == null)
            {
                source = "GraphicsSettings.defaultRenderPipeline";
                pipelineAsset = GraphicsSettings.defaultRenderPipeline;
            }

            if (pipelineAsset == null)
            {
                return new RenderPipelineDetectionResult(RenderPipelineKind.BuiltIn, source, null, null, null);
            }

            string assetPath = AssetDatabase.GetAssetPath(pipelineAsset);
            string assetTypeName = pipelineAsset.GetType().FullName ?? pipelineAsset.GetType().Name;
            return new RenderPipelineDetectionResult(
                Classify(pipelineAsset, assetTypeName),
                source,
                pipelineAsset.name,
                assetPath,
                assetTypeName);
        }

        private static RenderPipelineKind Classify(RenderPipelineAsset pipelineAsset, string assetTypeName)
        {
            if (InheritsFromType(pipelineAsset, "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"))
            {
                return RenderPipelineKind.Universal;
            }

            if (string.IsNullOrWhiteSpace(assetTypeName))
            {
                return RenderPipelineKind.Unknown;
            }

            if (InheritsFromType(pipelineAsset, "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset")
                || assetTypeName.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0
                || assetTypeName.IndexOf("HighDefinition", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return RenderPipelineKind.HighDefinition;
            }

            if (assetTypeName.IndexOf("RenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return RenderPipelineKind.CustomScriptable;
            }

            return RenderPipelineKind.Unknown;
        }

        private static bool InheritsFromType(RenderPipelineAsset pipelineAsset, string fullTypeName)
        {
            if (pipelineAsset == null || string.IsNullOrWhiteSpace(fullTypeName))
            {
                return false;
            }

            Type currentType = pipelineAsset.GetType();
            while (currentType != null)
            {
                if (string.Equals(currentType.FullName, fullTypeName, StringComparison.Ordinal))
                {
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }
    }
}
#endif