#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoyoToon.Editor.AssetPipeline.Materials;
using HoyoToon.Editor.Utilities;

namespace HoyoToon.Editor.UI.ManagerInspector.Setup
{
    internal static class SetupMaterialsHelper
    {
        private const string RawJsonSentinel = "<raw-json>";

        public static bool ShouldGenerateMaterials(ModelSetupUtility.SetupContext context)
        {
            if (context == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(context.DetectedGameKey))
            {
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, "Auto Setup: Material generation disabled (no game detected).");
                return false;
            }

            return true;
        }

        public static bool HasMissingMaterials(ModelSetupUtility.SetupContext context)
        {
            if (context == null || context.MaterialSources == null || context.MaterialSources.Count == 0)
            {
                return false;
            }

            bool missing = false;
            foreach (var source in context.MaterialSources)
            {
                if (string.IsNullOrEmpty(source.gameKey) || string.IsNullOrEmpty(source.sourceJson) || source.sourceJson == RawJsonSentinel)
                {
                    continue;
                }

                bool exists = MaterialAssetExists(source.sourceJson, source.shaderPath);
                HoyoToonLogger.Log(HoyoToonLogger.Categories.Manager, LogLevel.Info, $"Auto Setup: Material check '{Path.GetFileName(source.sourceJson)}' (Game='{source.gameKey}', Shader='{source.shaderPath ?? "?"}') => {(exists ? "Exists" : "Missing")}");
                if (!exists)
                {
                    missing = true;
                }
            }

            return missing;
        }

        public static bool MaterialAssetExists(string sourceJson, string shaderPath)
        {
            var outputDir = ComputeMaterialOutputDirectory(sourceJson);
            var matName = DeriveMaterialName(sourceJson, shaderPath);
            var primaryPath = EditorUtil.AbsoluteToUnityPath(Path.Combine(outputDir, matName + ".mat"));
            if (!string.IsNullOrEmpty(primaryPath) && AssetDatabase.LoadAssetAtPath<Material>(primaryPath) != null)
            {
                return true;
            }

            var fallbackDir = Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
            var fallbackPath = EditorUtil.AbsoluteToUnityPath(Path.Combine(fallbackDir, matName + ".mat"));
            return !string.IsNullOrEmpty(fallbackPath) && AssetDatabase.LoadAssetAtPath<Material>(fallbackPath) != null;
        }

        public static string ComputeMaterialOutputDirectory(string sourceJson)
        {
            if (!string.IsNullOrWhiteSpace(sourceJson) && File.Exists(sourceJson))
            {
                var dir = Path.GetDirectoryName(sourceJson);
                if (!string.IsNullOrEmpty(dir))
                {
                    return dir;
                }
            }

            return Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
        }

        public static string DeriveMaterialName(string sourceJson, string shaderPath)
        {
            if (!string.IsNullOrWhiteSpace(sourceJson))
            {
                var baseName = Path.GetFileNameWithoutExtension(sourceJson);
                if (!string.IsNullOrWhiteSpace(baseName))
                {
                    return EditorUtil.SanitizeFileName(baseName);
                }
            }

            var tail = Path.GetFileName(shaderPath);
            return string.IsNullOrWhiteSpace(tail) ? "GeneratedMaterial" : ($"{tail}_Material");
        }
    }
}
#endif
