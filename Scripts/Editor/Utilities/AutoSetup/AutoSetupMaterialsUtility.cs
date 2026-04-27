#if UNITY_EDITOR
using System.IO;
using System.Linq;
using HoyoToon.Editor.API;
using HoyoToon.Editor.Setup;
using HoyoToon.Editor.AssetPipeline.Materials;
using HoyoToon.Editor.Utilities.Assets;
using UnityEditor;
using UnityEngine;


namespace HoyoToon.Editor.Utilities.AutoSetup
{
    internal static class AutoSetupMaterialsUtility
    {
        private const string HsrCompanionMaterialSourceDirectory = HoyoToonApi.PackageRootAssetPath + "/Resources/Honkai Star Rail/Materials";

        private static readonly string[] HsrCompanionMaterialFileNames =
        {
            "Mat_EyeShadow.mat",
            "Mat_EyeShadow_00.mat",
            "Mat_EyeShadow_01.mat",
            "Mat_FaceMask.mat"
        };

        public static void GenerateFromSelection(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null || context.SelectedAssets.Count <= 0)
            {
                return;
            }

            MaterialGenerator.Result generationResult = MaterialGenerator.GenerateFromSelection(context.SelectedAssets.ToArray());
            result.MaterialsCreated += generationResult.CreatedCount;
            result.MaterialsUpdated += generationResult.UpdatedCount;

            if (generationResult.FailedCount > 0)
            {
                result.RecordWarning(
                    $"Material generation reported {generationResult.FailedCount} failure(s) while processing the current selection.");
            }
        }

        public static void CopyHsrCompanionMaterials(AutoSetupContext context, AutoSetupResult result)
        {
            if (context == null || result == null || context.SelectedAssets.Count <= 0)
            {
                return;
            }

            int copiedCount = 0;
            int failedCount = 0;
            foreach (string jsonAssetPath in AssetContextJsonQueryUtility.CollectJsonAssetPaths(context.SelectedAssets))
            {
                string targetDirectory = Path.GetDirectoryName(jsonAssetPath)?.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(targetDirectory))
                {
                    continue;
                }

                targetDirectory = targetDirectory.TrimEnd('/', '\\');
                GeneratedAssetSyncUtility.EnsureAssetFolderExists(targetDirectory);

                foreach (string materialFileName in HsrCompanionMaterialFileNames)
                {
                    string targetAssetPath = $"{targetDirectory}/{materialFileName}";
                    if (AssetDatabase.LoadAssetAtPath<Material>(targetAssetPath) != null)
                    {
                        continue;
                    }

                    string sourceAssetPath = $"{HsrCompanionMaterialSourceDirectory}/{materialFileName}";
                    if (AssetDatabase.CopyAsset(sourceAssetPath, targetAssetPath))
                    {
                        copiedCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
            }

            if (copiedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                result.MaterialsCreated += copiedCount;
            }

            if (failedCount > 0)
            {
                result.RecordWarning($"HSR companion material copy failed for {failedCount} material(s).");
            }
        }
    }
}
#endif
