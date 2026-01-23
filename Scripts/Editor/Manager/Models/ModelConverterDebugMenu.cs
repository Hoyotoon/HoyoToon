#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HoyoToon.FBX;
using HoyoToon.Materials;
using HoyoToon.EditorTools.ManagerUI;
using HoyoToon.Utilities;

namespace HoyoToon.Utilities
{
    public static class ModelConverterDebugMenu
    {
        [MenuItem("Assets/HoyoToon/FBX/Convert FBX", false, 2000)]
        private static void Run()
        {
            var selection = Selection.activeObject as GameObject;
            if (selection == null)
            {
                HoyoToonLogger.FBXConverterError("Please select an FBX asset.");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(selection);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                HoyoToonLogger.FBXConverterError("Selected asset is not an FBX file.");
                return;
            }

            GameObject convertedAsset = null;
            string convertedPath = null;
            bool resolved = ModelConverter.TryProcessAndGetOutput(selection, out convertedPath, out convertedAsset);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var detection = MaterialDetection.DetectGameAndShaderAutoWithSource(selection, assetPath);
            string preferredName = MaterialDetection.TryExtractCharacterName(detection.gameKey, assetPath);

            var context = new HoyoToonModelSetupUtility.SetupContext(null, selection, assetPath, new HoyoToonSetupOptions());
            if (HoyoToonSetupModelsHelper.TryResolveConvertedAsset(context, out var resolvedAsset, out var resolvedPath, allowUnmarked: true, preferredBaseName: preferredName))
            {
                if (resolvedAsset != null
                    && !string.IsNullOrEmpty(resolvedPath)
                    && !string.Equals(resolvedPath, assetPath, StringComparison.OrdinalIgnoreCase))
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
                HoyoToonLogger.FBXConverterWarning("Converted asset not found. Using original selection.");
                return;
            }

            HoyoToonLogger.FBXConverterInfo($"Converted asset resolved: {convertedPath}");
            HoyoToonSetupModelsHelper.MarkConverted(convertedPath, null, false);

            Selection.activeObject = convertedAsset;
            EditorGUIUtility.PingObject(convertedAsset);
        }
    }
}
#endif
