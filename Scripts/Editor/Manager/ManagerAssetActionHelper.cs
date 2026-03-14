#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.AssetPipeline.Materials;
using HoyoToon.Editor.Onboarding;
using HoyoToon.Editor.Utilities;
using StepIds = HoyoToon.Editor.Onboarding.GuidedTourController.StepIds;

namespace HoyoToon.Editor.UI.ManagerInspector
{
    internal static class ManagerAssetActionHelper
    {
        internal static bool TryGetActiveModelFolder(GameObject activeModel, out string folder)
        {
            folder = null;
            if (activeModel == null)
            {
                return false;
            }

            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(activeModel);
            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = AssetDatabase.GetAssetPath(activeModel);
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            folder = directory.Replace('\\', '/');
            return true;
        }

        internal static bool IsTourPrefabStep()
        {
            if (!GuidedTourController.IsActive)
            {
                return false;
            }

            string stepId = GuidedTourController.CurrentStep.id;
            return string.Equals(stepId, StepIds.Footer, StringComparison.OrdinalIgnoreCase)
                || string.Equals(stepId, StepIds.Prefab, StringComparison.OrdinalIgnoreCase);
        }

        internal static string EnsureTourPrefabFolder()
        {
            const string root = "Assets/HoyoToon";
            const string prefabs = "Assets/HoyoToon/Prefabs";

            if (!AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.CreateFolder("Assets", "HoyoToon");
            }

            if (!AssetDatabase.IsValidFolder(prefabs))
            {
                AssetDatabase.CreateFolder(root, "Prefabs");
            }

            return prefabs;
        }

        internal static bool TryCreatePrefabFromActiveModel(
            GameObject activeModel,
            Func<GameObject, string> folderResolver,
            out GameObject prefab,
            out string savePath,
            out string errorTitle,
            out string errorMessage)
        {
            prefab = null;
            savePath = null;
            errorTitle = null;
            errorMessage = null;

            if (activeModel == null)
            {
                return false;
            }

            var folder = folderResolver != null ? folderResolver(activeModel) : null;
            if (string.IsNullOrEmpty(folder))
            {
                errorTitle = "Cannot Create Prefab";
                errorMessage = "Unable to locate the original asset folder for this model.";
                return false;
            }

            var prefabName = $"{activeModel.name}.prefab";
            savePath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefabName}");

            try
            {
                prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(activeModel, savePath, InteractionMode.UserAction);
                if (prefab == null)
                {
                    errorTitle = "Prefab Creation Failed";
                    errorMessage = "Unity could not create the prefab asset. Check the Console for more information.";
                    return false;
                }

                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception ex)
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Error, $"Failed to create prefab for {activeModel.name}: {ex.Message}");
                errorTitle = "Prefab Creation Failed";
                errorMessage = $"Could not create prefab: {ex.Message}";
                return false;
            }
        }

        internal static bool TryRegenerateMaterialsForActiveModel(GameObject activeModel, out string warningOrError)
        {
            warningOrError = null;
            if (activeModel == null)
            {
                warningOrError = "No active model selected.";
                return false;
            }

            if (!FbxAssetResolver.TryResolve(activeModel, out GameObject sourceAsset))
            {
                warningOrError = "Could not locate the source FBX asset for the active model.";
                return false;
            }

            MaterialGeneration.GenerateAuto(sourceAsset, null, null, null, true);
            return true;
        }
    }
}
#endif
