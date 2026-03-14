#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.Prerequisites
{
    internal static class PipelineSwitcher
    {
        private const string RequiredAssetRelativePath = "Rendering/HoyoToon.asset";

        public static string RequiredAssetPath => ResolvePackagePath(RequiredAssetRelativePath);

        public static bool SetGraphicsPipeline()
        {
            if (!TryLoadRequiredPipelineAsset(out var asset, out var packagePath))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Required HoyoToon URP asset was not found at '{packagePath}'.");
                return false;
            }

            ApplyGraphicsPipeline(asset);
            return true;
        }

        public static bool SetGraphicsPipeline(HoyoToonPipeline pipeline)
        {
            if (pipeline != HoyoToonPipeline.HoyoToonURP)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Warning, $"Only {HoyoToonPipeline.HoyoToonURP} is supported by HoyoToon's render pipeline setup.");
                return false;
            }

            return SetGraphicsPipeline();
        }

        public static bool TryLoadRequiredPipelineAsset(out RenderPipelineAsset asset, out string packagePath)
        {
            asset = null;
            packagePath = RequiredAssetPath;
            if (string.IsNullOrEmpty(packagePath))
            {
                return false;
            }

            asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(packagePath);
            return asset != null;
        }

        public static bool IsRequiredAsset(RenderPipelineAsset asset)
        {
            if (asset == null)
            {
                return false;
            }

            var currentPath = AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrEmpty(currentPath)
                   && string.Equals(currentPath, RequiredAssetPath, StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyGraphicsPipeline(RenderPipelineAsset asset)
        {
            GraphicsSettings.defaultRenderPipeline = asset;

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }

            EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
            AssetDatabase.SaveAssets();

            PipelineDetector.InvalidateCache();
        }

        public static string ResolvePackagePath(string relativePath)
        {
            try
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RenderPipelineCheck).Assembly);
                if (packageInfo != null)
                {
                    return $"{packageInfo.assetPath}/{relativePath}";
                }
            }
            catch (Exception)
            {
                // Fall back to known package id path when package metadata is unavailable.
            }

            return $"Packages/com.hoyotoon.hoyotoon/{relativePath}";
        }
    }
}
#endif
