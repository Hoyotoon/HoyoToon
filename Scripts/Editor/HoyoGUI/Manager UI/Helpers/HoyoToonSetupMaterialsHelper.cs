#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HoyoToon.Materials;
using HoyoToon.Utilities;

namespace HoyoToon.EditorTools.ManagerUI
{
    internal static class HoyoToonSetupMaterialsHelper
    {
        public static bool ShouldGenerateMaterials(HoyoToonModelSetupUtility.SetupContext context)
        {
            if (context == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(context.DetectedGameKey))
            {
                HoyoToonLogger.ManagerInfo("Auto Setup: Material generation disabled (no game detected).");
                return false;
            }

            return true;
        }

        public static bool HasMissingMaterials(HoyoToonModelSetupUtility.SetupContext context)
        {
            if (context == null || context.MaterialSources == null || context.MaterialSources.Count == 0)
            {
                return false;
            }

            bool missing = false;
            foreach (var source in context.MaterialSources)
            {
                if (string.IsNullOrEmpty(source.gameKey) || string.IsNullOrEmpty(source.sourceJson) || source.sourceJson == "<raw-json>")
                {
                    continue;
                }

                bool exists = MaterialAssetExists(source.sourceJson, source.shaderPath);
                HoyoToonLogger.ManagerInfo($"Auto Setup: Material check '{Path.GetFileName(source.sourceJson)}' (Game='{source.gameKey}', Shader='{source.shaderPath ?? "?"}') => {(exists ? "Exists" : "Missing")}");
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
            var primaryPath = HoyoToonEditorUtil.ToProjectRelative(Path.Combine(outputDir, matName + ".mat"));
            if (!string.IsNullOrEmpty(primaryPath) && AssetDatabase.LoadAssetAtPath<Material>(primaryPath) != null)
            {
                return true;
            }

            var fallbackDir = Path.Combine(Application.dataPath, "HoyoToon", "GeneratedMaterials");
            var fallbackPath = HoyoToonEditorUtil.ToProjectRelative(Path.Combine(fallbackDir, matName + ".mat"));
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
                    return HoyoToonEditorUtil.SanitizeFileName(baseName);
                }
            }

            var tail = shaderPath?.Split('/')?.LastOrDefault();
            return string.IsNullOrWhiteSpace(tail) ? "GeneratedMaterial" : ($"{tail}_Material");
        }
    }
}
#endif
